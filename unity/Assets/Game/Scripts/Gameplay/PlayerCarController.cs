using Studio.Foundation;
using Studio.Input;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Controlador arcade do carro do jogador (NEON DISTRICT).
    /// Rigidbody não-kinematic com integração manual de velocidade: grip lateral
    /// amortecido (drift reduz o grip), yaw rate atenuado por velocidade,
    /// centro de massa baixo anti-capotamento. Sem WheelColliders.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerCarController : MonoBehaviour
    {
        [Header("Powertrain")]
        [SerializeField] private float _maxSpeed = 33f; // ~119 km/h
        [SerializeField] private float _maxReverse = -14f;
        [SerializeField] private float _acceleration = 17f;
        [SerializeField] private float _brakeDeceleration = 28f;
        [SerializeField] private float _drag = 0.32f;

        [Header("Steering & Drift")]
        [SerializeField] private float _maxSteerDegPerSec = 100f;
        [SerializeField] private float _steerRefSpeed = 18f;
        [SerializeField] private float _gripAlignRate = 4.2f;   // rad/s — quão rápido a velocidade segue o bico
        [SerializeField] private float _driftAlignRate = 0.55f; // rad/s — drift segura o slide
        [SerializeField] private float _powerPull = 18f;        // m/s² — autoridade do motor sobre o vetor
        [SerializeField] private float _driftYawBoost = 1.45f;
        [SerializeField] private float _driftMinSpeed = 5f;
        [SerializeField] private float _idleDampen = 8f;

        [Header("Visuals")]
        [SerializeField] private Transform[] _steerWheels;
        [SerializeField] private Transform[] _driveWheels;
        [SerializeField] private TrailRenderer[] _skidTrails;
        [SerializeField] private float _wheelRadius = 0.34f;
        [SerializeField] private float _steerWheelMaxDeg = 32f;
        [SerializeField] private float _steerVisualSpeed = 260f; // deg/s — resposta do volante visual

        [Header("Audio")]
        [SerializeField] private float _minImpactSpeed = 2.5f;

        private Rigidbody _rb;
        private Transform _modelBody;
        private float _rollDeg, _pitchDeg;
        private float _forwardSpeed;
        private float _wheelSpinDeg;
        private float _visualSteerDeg;
        private Quaternion[] _steerWheelBaseRot;
        private Quaternion[] _driveWheelBaseRot;
        private float[] _steerWheelSpinSign;
        private float[] _driveWheelSpinSign;

        // Canal de rede: quando UseNetInput, ignora adapter/teclado — o
        // servidor alimenta via NetSteer/Throttle/Brake/Drift (NdNet).
        [HideInInspector] public bool UseNetInput;
        [HideInInspector] public float NetSteer, NetThrottle, NetBrake;
        [HideInInspector] public bool NetDrift;

        public float ForwardSpeed => _forwardSpeed;
        public float SpeedKmh => DriveMath.MsToKmh(Mathf.Abs(_forwardSpeed));
        public float SpeedNorm => Mathf.Clamp01(Mathf.Abs(_forwardSpeed) / _maxSpeed);
        public bool IsDrifting { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            // Idempotente: sem ResetCenterOfMass o += acumulava a cada Awake
            // (sessões sem reload de domínio) e o carro virava pião
            _rb.ResetCenterOfMass();
            _rb.centerOfMass += new Vector3(0f, -0.4f, 0f);
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.maxAngularVelocity = 7f;

            if (_steerWheels != null)
            {
                _steerWheelBaseRot = new Quaternion[_steerWheels.Length];
                _steerWheelSpinSign = new float[_steerWheels.Length];
                for (int i = 0; i < _steerWheels.Length; i++)
                {
                    if (_steerWheels[i] != null)
                    {
                        _steerWheelBaseRot[i] = _steerWheels[i].localRotation;
                        _steerWheelSpinSign[i] = WheelSpinSign(_steerWheels[i]);
                    }
                }
            }
            if (_driveWheels != null)
            {
                _driveWheelBaseRot = new Quaternion[_driveWheels.Length];
                _driveWheelSpinSign = new float[_driveWheels.Length];
                for (int i = 0; i < _driveWheels.Length; i++)
                {
                    if (_driveWheels[i] != null)
                    {
                        _driveWheelBaseRot[i] = _driveWheels[i].localRotation;
                        _driveWheelSpinSign[i] = WheelSpinSign(_driveWheels[i]);
                    }
                }
            }
        }

        private void Start()
        {
            var mb = transform.Find("SportCar_Model");
            _modelBody = mb != null ? mb : (transform.childCount > 0 ? transform.GetChild(0) : transform);
            // Música procedural removida — a RadioSystem toca as faixas reais
        }

        private void OnDisable()
        {
            // Carro desativado (explodir/sair a pé) → motor+cantada param
            ProceduralAudioService.Instance?.SetEngineOff();
        }

        private void FixedUpdate()
        {
            float steer, throttle, brake; bool drift;
            if (UseNetInput)
            {
                steer = NetSteer; throttle = NetThrottle;
                brake = NetBrake; drift = NetDrift;
            }
            else
            {
                var input = DriveInputAdapter.Instance;
                steer = input != null ? input.Steer : 0f;
                throttle = input != null ? input.Throttle : 0f;
                brake = input != null ? input.Brake : 0f;
                drift = input != null && input.DriftHeld;
            }

            // Bypass direto: se o adapter não entregar nada, lê as teclas aqui
            if (!UseNetInput && throttle == 0f && brake == 0f && steer == 0f)
            {
                if (UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow)) throttle = 1f;
                if (UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow)) brake = 1f;
                if (UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow)) steer = -1f;
                if (UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow)) steer = 1f;
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.F11))
                Debug.Log($"[CARDIAG] enabled={enabled} kin={_rb.isKinematic} cons={(int)_rb.constraints} " +
                    $"com={_rb.centerOfMass} thr={throttle:F2} steer={steer:F2} fwd={_forwardSpeed:F2} " +
                    $"vel={_rb.linearVelocity} net={UseNetInput} scale={Time.timeScale}");

            float dt = Time.fixedDeltaTime;
            Vector3 vel = _rb.linearVelocity;
            Vector3 fwd = transform.forward;
            var flatVel = new Vector3(vel.x, 0f, vel.z);

            _forwardSpeed = Vector3.Dot(flatVel, fwd);

            IsDrifting = drift && Mathf.Abs(_forwardSpeed) > _driftMinSpeed;

            _forwardSpeed = DriveMath.IntegrateForwardSpeed(
                _forwardSpeed, throttle, brake,
                _maxSpeed, _maxReverse,
                _acceleration, _brakeDeceleration, _drag, dt);

            // Modelo arcade de tração: a VELOCIDADE gira junto com o bico do carro.
            // Grip alto = carro cola na direção; drift = grip baixo → slide mantém
            // o vetor antigo enquanto o carro guina (derrapagem real).
            Vector3 desired = fwd * _forwardSpeed;
            float alignRate = IsDrifting ? _driftAlignRate : _gripAlignRate;
            Vector3 aligned = flatVel.sqrMagnitude > 0.01f
                ? Vector3.RotateTowards(flatVel, desired.normalized * flatVel.magnitude, alignRate * dt, 0f)
                : desired;
            Vector3 newFlat = Vector3.MoveTowards(aligned, desired, _powerPull * dt);

            // Parado e sem input: amortecer qualquer deslize residual
            if (throttle < 0.01f && brake < 0.01f && Mathf.Abs(_forwardSpeed) < 0.5f)
                newFlat *= Mathf.Exp(-_idleDampen * dt);

            // Anti-launch: clamp de velocidade total e de subida
            Vector3 finalVel = newFlat + Vector3.up * vel.y;
            float maxVel = _maxSpeed * 1.35f;
            if (finalVel.magnitude > maxVel) finalVel = finalVel.normalized * maxVel;
            if (finalVel.y > 4.5f) finalVel.y = Mathf.Lerp(finalVel.y, 4.5f, 8f * dt);
            _rb.linearVelocity = finalVel;

            float yawRate = DriveMath.SteerYawRate(
                steer, _forwardSpeed, _maxSteerDegPerSec, _steerRefSpeed, _driftYawBoost, IsDrifting);
            float yaw = _rb.rotation.eulerAngles.y + yawRate * dt;
            _rb.MoveRotation(Quaternion.Euler(0f, yaw, 0f));

            UpdateWheelVisuals(steer, dt);
            UpdateBodyRoll(steer, yawRate, dt);
            UpdateSkidTrails();
            UpdateAudio(throttle);
        }

        private void OnCollisionEnter(Collision collision)
        {
            // pedestre não faz som de batida de lataria (o ped trata o dele)
            bool hitPed = collision.gameObject
                .GetComponentInParent<Pedestrian>() != null;
            if (collision.relativeVelocity.magnitude >= _minImpactSpeed
                && !hitPed)
            {
                ProceduralAudioService.Instance?.PlayCollisionThud(
                    Mathf.Clamp01(collision.relativeVelocity.magnitude / 18f));
                // Colisão drena um pouco a velocidade (bounce com perda)
                _forwardSpeed *= 0.75f;
            }
        }

        /// <summary>Teleporta o carro para uma pose de via (reset/recenter).</summary>
        public void ResetToPose(Vector3 position, Quaternion rotation)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _rb.position = position;
            _rb.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
            _forwardSpeed = 0f;
        }

        private float _prevFwdSpeed;

        /// <summary>
        /// Body roll/pitch na carroceria (só visual — física continua yaw-only):
        /// rolagem contra a guinada (força lateral) + arfagem na aceleração/freio.
        /// </summary>
        private void UpdateBodyRoll(float steer, float yawRate, float dt)
        {
            if (_modelBody == null) return;
            // Roll sutil: ~2° p/ fora da curva; pitch por input (sem ruído de delta)
            float targetRoll = Mathf.Clamp(-steer * SpeedNorm * 2.2f, -2.2f, 2.2f);
            var inp = DriveInputAdapter.Instance;
            float accelDir = (inp != null ? inp.Throttle - inp.Brake : 0f);
            float targetPitch = Mathf.Clamp(-accelDir * 1.2f - SpeedNorm * 0.4f, -1.8f, 1.8f);
            _rollDeg = Mathf.Lerp(_rollDeg, targetRoll, dt * 4f);
            _pitchDeg = Mathf.Lerp(_pitchDeg, targetPitch, dt * 4f);
            _modelBody.localRotation = Quaternion.Euler(_pitchDeg, 0f, _rollDeg);
        }

        /// <summary>
        /// Ângulo visual máximo do esterço pela velocidade: ±32° completo em
        /// manobra lenta, reduz progressivamente em alta (fisicamente real).
        /// </summary>
        private float VisualSteerLimit()
        {
            // ±32° completo parado/em manobra lenta; reduz suavemente até ~40%
            // acima de 2× a velocidade de cruzeiro (rodas não travam de giro).
            float t = Mathf.Clamp01(Mathf.Abs(_forwardSpeed)
                / Mathf.Max(0.01f, _steerRefSpeed * 2f));
            return _steerWheelMaxDeg * Mathf.Lerp(1f, 0.4f, t);
        }

        private void UpdateWheelVisuals(float steer, float dt)
        {
            _wheelSpinDeg += (_forwardSpeed / Mathf.Max(0.05f, _wheelRadius)) * Mathf.Rad2Deg * dt;

            if (_steerWheels != null)
            {
                // Volante visual suavizado — MoveTowards para o alvo do input,
                // limitado pela atenuação de velocidade (sem giro brusco).
                _visualSteerDeg = Mathf.MoveTowards(_visualSteerDeg,
                    steer * VisualSteerLimit(), _steerVisualSpeed * dt);
                var steerRot = Quaternion.Euler(0f, _visualSteerDeg, 0f);
                for (int i = 0; i < _steerWheels.Length; i++)
                {
                    if (_steerWheels[i] != null)
                        _steerWheels[i].localRotation = _steerWheelBaseRot[i] * steerRot * Quaternion.Euler(_wheelSpinDeg * _steerWheelSpinSign[i], 0f, 0f);
                }
            }
            if (_driveWheels != null)
            {
                for (int i = 0; i < _driveWheels.Length; i++)
                {
                    if (_driveWheels[i] != null)
                        _driveWheels[i].localRotation = _driveWheelBaseRot[i] * Quaternion.Euler(_wheelSpinDeg * _driveWheelSpinSign[i], 0f, 0f);
                }
            }
        }

        /// <summary>
        /// Sinal de giro da roda: prefabs com rodas esquerdas espelhadas (yaw 180°)
        /// precisam de sinal invertido para rolar na mesma direção visual.
        /// </summary>
        private float WheelSpinSign(Transform wheel)
        {
            var axisInCar = transform.InverseTransformDirection(wheel.TransformDirection(Vector3.right));
            return Vector3.Dot(axisInCar, Vector3.right) >= 0f ? 1f : -1f;
        }

        private void UpdateSkidTrails()
        {
            if (_skidTrails == null) return;
            bool emit = IsDrifting && Mathf.Abs(_forwardSpeed) > 3f;
            for (int i = 0; i < _skidTrails.Length; i++)
            {
                if (_skidTrails[i] != null) _skidTrails[i].emitting = emit;
            }
        }

        private void UpdateAudio(float throttle)
        {
            var audio = ProceduralAudioService.Instance;
            if (audio == null) return;
            audio.SetEngineState(SpeedNorm, throttle);
            audio.SetSkidActive(IsDrifting);
        }
    }
}
