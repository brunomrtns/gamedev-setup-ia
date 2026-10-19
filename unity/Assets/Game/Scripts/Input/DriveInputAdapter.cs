using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Studio.Input
{
    /// <summary>
    /// Adaptador de input de direção para NEON DISTRICT.
    /// Teclado (novo Input System): W/↑ gas, S/↓ freio/ré, A/D esterço, Space/Shift drift, R recenter.
    /// Touch: estado virtual alimentado por DriveTouchButton (UI).
    /// </summary>
    public class DriveInputAdapter : MonoBehaviour
    {
        public enum DriveControl { Gas, Brake, SteerLeft, SteerRight, Drift, Fire, Ads, Reload, Sprint, Jump, Crouch }

        private static DriveInputAdapter _instance;
        public static DriveInputAdapter Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<DriveInputAdapter>();
                }
                return _instance;
            }
        }

        public event Action OnRecenter;

        [Header("Steering Tuning")]
        [SerializeField] private float _steerResponseSpeed = 4.5f;
        [SerializeField] private float _steerReleaseSpeed = 7f;

        private float _steer;
        private float _throttle;
        private float _brake;
        private bool _driftHeld;
        private readonly bool[] _virtualHeld = new bool[11];

        // --- On-Foot state ---
        private Vector2 _footMove;
        private Vector2 _footLook;
        private bool _sprintHeld;
        private bool _fireHeld;
        private bool _firePressed;
        private bool _adsHeld;
        private bool _reloadQueued;
        private bool _enterExitQueued;
        private Vector2 _lookAccum;
        private bool _jumpQueued;
        private bool _crouchQueued;
        private Vector2 _qaFootMove;

        public Vector2 FootMove => _footMove;
        public Vector2 FootLook => _footLook;
        public bool SprintHeld => _sprintHeld || _virtualHeld[(int)DriveControl.Sprint];
        public bool FireHeld => _fireHeld || _virtualHeld[(int)DriveControl.Fire];
        public bool FirePressed => _firePressed;
        public bool AdsHeld => _adsHeld || _virtualHeld[(int)DriveControl.Ads];
        /// <summary>Consome o pulo (Space/botão) uma única vez.</summary>
        public bool ConsumeJump()
        {
            bool j = _jumpQueued || _virtualHeld[(int)DriveControl.Jump]
                || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
            _jumpQueued = false;
            _virtualHeld[(int)DriveControl.Jump] = false;
            return j;
        }
        /// <summary>Consome o toggle de crouch (Ctrl/C/botão) uma única vez.</summary>
        public bool ConsumeCrouch()
        {
            bool c = _crouchQueued || _virtualHeld[(int)DriveControl.Crouch]
                || (Keyboard.current != null && (Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.leftCtrlKey.wasPressedThisFrame));
            _crouchQueued = false;
            _virtualHeld[(int)DriveControl.Crouch] = false;
            return c;
        }
        /// <summary>Lê jump pendente SEM consumir (rede le intenção; motor consome).</summary>
        public bool PeekJump() => _jumpQueued || _virtualHeld[(int)DriveControl.Jump]
            || (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame);
        /// <summary>Lê crouch pendente SEM consumir.</summary>
        public bool PeekCrouch() => _crouchQueued || _virtualHeld[(int)DriveControl.Crouch]
            || (Keyboard.current != null && (Keyboard.current.cKey.wasPressedThisFrame || Keyboard.current.leftCtrlKey.wasPressedThisFrame));
        /// <summary>Consome o evento E/botão SAIR uma única vez (seguro p/ ordem de Update).</summary>
        public bool ConsumeEnterExit()
        {
            bool e = _enterExitQueued || (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame);
            _enterExitQueued = false;
            return e;
        }
        /// <summary>Consome o evento R/botão RELOAD uma única vez.</summary>
        public bool ConsumeReload()
        {
            bool r = _reloadQueued || _virtualHeld[(int)DriveControl.Reload]
                || (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame);
            _reloadQueued = false;
            return r;
        }

        /// <summary>Alimentado pelo joystick virtual (UI).</summary>
        public void SetFootMove(Vector2 v) { _footMove = v; }
        /// <summary>Canal de QA: soma ao input de teclado (bot autônomo).</summary>
        public void SetQaFootMove(Vector2 v) { _qaFootMove = v; }
        /// <summary>Alimentado pela zona de drag direita (UI) — acumula por frame.</summary>
        public void SetFootLook(Vector2 delta) { _lookAccum += delta; }
        /// <summary>Dispara o evento Enter/Exit carro (tecla E / botão).</summary>
        public void TriggerEnterExit() { _enterExitQueued = true; }

        public float Steer => _steer;
        public float Throttle => _throttle;
        public float Brake => _brake;
        public bool DriftHeld => _driftHeld;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            float steerTarget = 0f;
            float throttle = 0f;
            float brake = 0f;
            bool drift = _virtualHeld[(int)DriveControl.Drift];

            // Fallback legado (Input Manager): Keyboard.current pode vir null
            // em sessões sem devices — Input.GetKey sempre funciona em mode Both
            bool wKey = UnityEngine.Input.GetKey(KeyCode.W) || UnityEngine.Input.GetKey(KeyCode.UpArrow);
            bool sKey = UnityEngine.Input.GetKey(KeyCode.S) || UnityEngine.Input.GetKey(KeyCode.DownArrow);
            bool aKey = UnityEngine.Input.GetKey(KeyCode.A) || UnityEngine.Input.GetKey(KeyCode.LeftArrow);
            bool dKey = UnityEngine.Input.GetKey(KeyCode.D) || UnityEngine.Input.GetKey(KeyCode.RightArrow);
            bool shKey = UnityEngine.Input.GetKey(KeyCode.LeftShift) || UnityEngine.Input.GetKey(KeyCode.Space);
            if (wKey) throttle = 1f;
            if (sKey) brake = 1f;
            if (aKey) steerTarget -= 1f;
            if (dKey) steerTarget += 1f;
            if (shKey) drift = true;
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) TriggerRecenter();

            if (_virtualHeld[(int)DriveControl.SteerLeft]) steerTarget -= 1f;
            if (_virtualHeld[(int)DriveControl.SteerRight]) steerTarget += 1f;
            if (_virtualHeld[(int)DriveControl.Gas]) throttle = 1f;
            if (_virtualHeld[(int)DriveControl.Brake]) brake = 1f;

            float speed = Mathf.Abs(steerTarget) > 0.01f ? _steerResponseSpeed : _steerReleaseSpeed;
            _steer = Mathf.MoveTowards(_steer, Mathf.Clamp(steerTarget, -1f, 1f), speed * Time.deltaTime);
            _throttle = throttle;
            _brake = brake;
            _driftHeld = drift;

            // --- On-foot inputs (legado: Input Manager cobre quando devices faltam) ---
            _footMove.x = (aKey ? -1f : 0f) + (dKey ? 1f : 0f) + _qaFootMove.x;
            _footMove.y = (sKey ? -1f : 0f) + (wKey ? 1f : 0f) + _qaFootMove.y;
            if (_footMove.sqrMagnitude > 1f) _footMove.Normalize();
            _sprintHeld = UnityEngine.Input.GetKey(KeyCode.LeftShift);
            if (UnityEngine.Input.GetKeyDown(KeyCode.E)) _enterExitQueued = true;
            if (UnityEngine.Input.GetKeyDown(KeyCode.R)) _reloadQueued = true;
            _footLook = new Vector2(UnityEngine.Input.GetAxisRaw("Mouse X"),
                UnityEngine.Input.GetAxisRaw("Mouse Y")) * 0.5f + _lookAccum;
            _lookAccum = Vector2.zero;
            _fireHeld = UnityEngine.Input.GetMouseButton(0);
            _firePressed = UnityEngine.Input.GetMouseButtonDown(0);
            _adsHeld = UnityEngine.Input.GetMouseButton(1);
        }

        /// <summary>Alimentado por DriveTouchButton (pointer down/up).</summary>
        public void SetVirtualControl(DriveControl control, bool held)
        {
            _virtualHeld[(int)control] = held;
        }

        /// <summary>Estado virtual do controle (inspeção/testes).</summary>
        public bool IsVirtualHeld(DriveControl control)
        {
            return _virtualHeld[(int)control];
        }

        public void TriggerRecenter()
        {
            OnRecenter?.Invoke();
        }

        public void ClearVirtual()
        {
            for (int i = 0; i < _virtualHeld.Length; i++) _virtualHeld[i] = false;
        }
    }
}
