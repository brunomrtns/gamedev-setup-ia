using Studio.Input;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Motor de primeira pessoa do NEON DISTRICT v2.1 — CharacterController.
    /// Look: raw input + SmoothDamp (~0.03s), yaw no corpo / pitch na câmera,
    /// cursor locked a pé. Movimento: walk/sprint/jump/crouch com gravidade,
    /// head-bob amortecido e dip de aterrissagem.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class OnFootMotor : MonoBehaviour
    {
        public const float HUMAN_HEIGHT = 1.75f;
        public const float EYE_HEIGHT = 1.65f;
        public const float CROUCH_HEIGHT = 1.0f;
        public const float CROUCH_EYE = 0.95f;

        [Header("Movimento")]
        [SerializeField] private float _walkSpeed = 3.4f;
        [SerializeField] private float _sprintSpeed = 7.0f;
        [SerializeField] private float _crouchSpeed = 1.8f;
        [SerializeField] private float _accel = 22f;
        [SerializeField] private float _gravity = -22f;
        [SerializeField] private float _jumpSpeed = 6.5f;

        [Header("Look")]
        [SerializeField] private float _lookSensitivity = 0.50f; // deg por pixel
        [SerializeField] private float _lookSmoothTime = 0.03f;
        [SerializeField] private float _pitchClamp = 85f;

        [Header("Câmera feel")]
        [SerializeField] private float _bobAmpWalk = 0.035f;
        [SerializeField] private float _bobAmpSprint = 0.055f;
        [SerializeField] private float _bobFreqWalk = 8f;
        [SerializeField] private float _bobFreqSprint = 11f;
        [SerializeField] private float _landDipDepth = 0.09f;

        private CharacterController _cc;
        private Transform _eye;
        private float _yaw, _pitch, _vy;
        private Vector2 _lookVel;
        private Vector3 _hVel;
        private bool _enabled, _crouched;
        private float _bobPhase, _eyeOffsetY, _dipVel;
        private float _stepTimer;

        // Canal de rede: servidor usa Net* em vez do adapter local.
        [HideInInspector] public bool UseNetInput;
        [HideInInspector] public Vector2 NetMove;
        [HideInInspector] public float NetYaw = float.NaN; // NaN = sem look
        [HideInInspector] public bool NetSprint, NetJump, NetCrouch; // edge: consome 1x

        public Transform Eye => _eye;
        public Camera EyeCamera => _eye != null ? _eye.GetComponentInChildren<Camera>(true) : null;
        /// <summary>|vy| no momento do pouso — consumido pelo PlayerHealth.</summary>
        [HideInInspector] public float LastLandImpactSpeed;
        public bool IsMoving => _hVel.sqrMagnitude > 0.6f;
        public bool IsGrounded => _cc != null && _cc.isGrounded;
        public bool IsSprinting { get; private set; }
        public bool IsCrouched => _crouched;
        public float BobPhase => _bobPhase;
        public float SpeedNorm => Mathf.InverseLerp(0f, _sprintSpeed, _hVel.magnitude);

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            ApplyCanonicalScale(_cc);
        }

        public static void ApplyCanonicalScale(CharacterController cc)
        {
            cc.height = HUMAN_HEIGHT;
            cc.radius = 0.35f;
            cc.center = new Vector3(0f, HUMAN_HEIGHT * 0.5f, 0f);
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.04f;
        }

        public void Activate(Transform eye)
        {
            _eye = eye != null ? eye : transform.Find("Eye");
            _yaw = transform.eulerAngles.y;
            _enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        public void Deactivate()
        {
            _enabled = false;
            _hVel = Vector3.zero;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        public void Face(Quaternion rot) { _yaw = rot.eulerAngles.y; _pitch = 0f; }

        private void Update()
        {
            if (!_enabled) return;
            var input = DriveInputAdapter.Instance;
            float dt = Time.deltaTime;

            if (UseNetInput)
            {
                // --- Ramo de rede: servidor consome Net* (yaw absoluto, flags edge) ---
                if (float.IsFinite(NetYaw)) _yaw = NetYaw;
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
                Vector2 nmove = NetMove;
                IsSprinting = NetSprint && nmove.y > 0.1f && !_crouched;
                if (NetCrouch) { _crouched = !_crouched; NetCrouch = false; }
                float nh = _crouched ? CROUCH_HEIGHT : HUMAN_HEIGHT;
                if (Mathf.Abs(_cc.height - nh) > 0.01f)
                {
                    _cc.height = Mathf.MoveTowards(_cc.height, nh, dt * 4f);
                    _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
                }
                float nsp = _crouched ? _crouchSpeed : (IsSprinting ? _sprintSpeed : _walkSpeed);
                Vector3 nwish = transform.right * nmove.x + transform.forward * nmove.y;
                if (nwish.sqrMagnitude > 1f) nwish.Normalize();
                _hVel = Vector3.MoveTowards(_hVel, nwish * nsp, _accel * dt);
                bool gnd = _cc.isGrounded;
                if (gnd && _vy < 0f) _vy = -2f;
                if (gnd && NetJump && !_crouched) { _vy = _jumpSpeed; NetJump = false; }
                NetJump = false;
                _vy += _gravity * dt;
                _cc.Move((_hVel + Vector3.up * _vy) * dt);
                return;
            }

            // --- Look: raw input, SmoothDamp curto, yaw corpo / pitch olho ---
            Vector2 look = input != null ? input.FootLook : Vector2.zero;
            Vector2 target = look * _lookSensitivity;
            _yaw += Mathf.SmoothDamp(0f, target.x, ref _lookVel.x, _lookSmoothTime);
            _pitch -= Mathf.SmoothDamp(0f, target.y, ref _lookVel.y, _lookSmoothTime);
            _pitch = Mathf.Clamp(_pitch, -_pitchClamp, _pitchClamp);
            transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
            if (_eye != null) _eye.localRotation = Quaternion.Euler(_pitch, 0f, 0f);

            // --- Movimento ---
            Vector2 move = input != null ? input.FootMove : Vector2.zero;
            IsSprinting = input != null && input.SprintHeld && move.y > 0.1f && !_crouched;

            // Crouch toggle (Ctrl/C/botão)
            if (input != null && input.ConsumeCrouch()) _crouched = !_crouched;
            float targetH = _crouched ? CROUCH_HEIGHT : HUMAN_HEIGHT;
            if (Mathf.Abs(_cc.height - targetH) > 0.01f)
            {
                _cc.height = Mathf.MoveTowards(_cc.height, targetH, dt * 4f);
                _cc.center = new Vector3(0f, _cc.height * 0.5f, 0f);
            }
            float eyeH = _crouched ? CROUCH_EYE : EYE_HEIGHT;

            float speed = _crouched ? _crouchSpeed : (IsSprinting ? _sprintSpeed : _walkSpeed);
            Vector3 wish = transform.right * move.x + transform.forward * move.y;
            if (wish.sqrMagnitude > 1f) wish.Normalize();
            _hVel = Vector3.MoveTowards(_hVel, wish * speed, _accel * dt);

            bool grounded = _cc.isGrounded;
            if (grounded && _vy < 0f) _vy = -2f;
            if (grounded && input != null && input.ConsumeJump() && !_crouched)
            {
                _vy = _jumpSpeed;
                Foundation.ProceduralAudioService.Instance?.PlayJumpWhoosh();
            }
            _vy += _gravity * dt;
            var moveVel = (_hVel + Vector3.up * _vy) * dt;
            var flags = _cc.Move(moveVel);
            if ((flags & CollisionFlags.Below) != 0 && _vy < -8f)
            {
                LastLandImpactSpeed = -_vy; // dano de queda decidido pelo PlayerHealth
                _eyeOffsetY = -_landDipDepth; // dip de aterrissagem
                Foundation.ProceduralAudioService.Instance?.PlayFootstep();
            }
            if ((flags & CollisionFlags.Below) != 0 && _vy < 0f) _vy = -2f;

            // --- Head-bob + dip ---
            bool moving = grounded && _hVel.sqrMagnitude > 0.5f;
            float freq = IsSprinting ? _bobFreqSprint : _bobFreqWalk;
            float amp = IsSprinting ? _bobAmpSprint : _bobAmpWalk;
            _bobPhase = moving ? _bobPhase + dt * freq : Mathf.MoveTowards(_bobPhase, Mathf.Round(_bobPhase / Mathf.PI) * Mathf.PI, dt * 8f);

            // passos sonoros a cada meio-ciclo de bob
            if (moving)
            {
                _stepTimer += dt * freq;
                if (_stepTimer > Mathf.PI) { _stepTimer -= Mathf.PI; Foundation.ProceduralAudioService.Instance?.PlayFootstep(); }
            }

            _eyeOffsetY = Mathf.SmoothDamp(_eyeOffsetY, 0f, ref _dipVel, 0.15f);
            if (_eye != null)
            {
                float bobY = moving ? Mathf.Sin(_bobPhase * 2f) * amp : 0f;
                float bobX = moving ? Mathf.Cos(_bobPhase) * amp * 0.6f : 0f;
                _eye.localPosition = new Vector3(bobX, eyeH + bobY + _eyeOffsetY, 0f);
            }
        }
    }
}
