using Studio.Foundation;
using Studio.Input;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Motor em primeira pessoa para movimentação subaquática tática (6-DoF moderado).
    /// Implementa ICharacterMotor, aplicando arrasto hidrodinâmico, flutuabilidade suave (buoyancy),
    /// nado vertical ascendente/descendente, bobbing de câmera e zoom de FOV em modo ADS.
    /// </summary>
    public class AbyssalFirstPersonMotor : MonoBehaviour, ICharacterMotor
    {
        [Header("Underwater Locomotion")]
        [SerializeField] private float _swimSpeed = 5.5f;
        [SerializeField] private float _sprintSwimSpeed = 8.5f;
        [SerializeField] private float _verticalSwimSpeed = 4.2f;
        [SerializeField] private float _acceleration = 14.0f;
        [SerializeField] private float _waterDrag = 5.5f;
        [SerializeField] private float _buoyancyAmplitude = 0.22f;
        [SerializeField] private float _buoyancyFrequency = 1.6f;

        [Header("Trench Boundaries")]
        [SerializeField] private float _minX = -16.0f;
        [SerializeField] private float _maxX = 16.0f;
        [SerializeField] private float _minY = 1.2f;
        [SerializeField] private float _maxY = 18.0f;
        [SerializeField] private float _minZ = -5.0f;
        [SerializeField] private float _maxZ = 135.0f;

        [Header("Camera & Aiming")]
        [SerializeField] private Transform _cameraPivot;
        [SerializeField] private Camera _fpsCamera;
        [SerializeField] private float _lookSensitivity = 1.2f;
        [SerializeField] private float _fovLerpSpeed = 12.0f;

        private Vector3 _initialPosition = new Vector3(0f, 3.2f, 0f);
        private Vector3 _velocity;
        private float _yaw;
        private float _pitch;
        private float _simTime;
        private float _bobTimer;
        private float _currentFov = AbyssalMath.DEFAULT_FOV;
        private bool _canMove = true;
        private bool _isAimingADS;
        private bool _isSprinting;
        private AbyssalInputAdapter _subscribedAdapter;

        public bool CanMove => _canMove;
        public Vector3 Velocity => _velocity;
        public float CurrentSpeed => _velocity.magnitude;
        public bool IsSprinting => _isSprinting;
        public bool IsAimingADS => _isAimingADS;
        public float CurrentFov => _currentFov;
        public float CurrentDepthMeters => AbyssalMath.ComputeDepthMeters(transform.position.y);
        public float CurrentHeadingDegrees => AbyssalMath.ComputeCompassHeadingDegrees(transform.forward);
        public Transform CameraPivot => _cameraPivot != null ? _cameraPivot : transform;
        public Camera FpsCamera => _fpsCamera;
        public bool IsSubscribedToInput => _subscribedAdapter != null;

        private void Awake()
        {
            _initialPosition = transform.position;
            if (_initialPosition.y < _minY)
            {
                _initialPosition.y = 3.2f;
                transform.position = _initialPosition;
            }
            _yaw = transform.eulerAngles.y;
            EnsureCameraReferences();
        }

        private void OnEnable()
        {
            EnsureInputSubscribed();
        }

        private void OnDisable()
        {
            UnsubscribeInput();
        }

        public void EnsureCameraReferences()
        {
            if (_cameraPivot == null)
            {
                var pivotChild = transform.Find("CameraPivot");
                if (pivotChild != null) _cameraPivot = pivotChild;
            }

            if (_fpsCamera == null)
            {
                _fpsCamera = GetComponentInChildren<Camera>();
                if (_fpsCamera == null) _fpsCamera = Camera.main;
            }
        }

        public void SetCameraReferences(Transform pivot, Camera cam)
        {
            _cameraPivot = pivot;
            _fpsCamera = cam;
        }

        public void EnsureInputSubscribed()
        {
            var adapter = AbyssalInputAdapter.Instance;
            if (adapter != null && _subscribedAdapter != adapter)
            {
                UnsubscribeInput();
                _subscribedAdapter = adapter;
                _subscribedAdapter.OnAimChanged += HandleAimChanged;
            }
        }

        private void UnsubscribeInput()
        {
            if (_subscribedAdapter != null)
            {
                _subscribedAdapter.OnAimChanged -= HandleAimChanged;
                _subscribedAdapter = null;
            }
        }

        private void HandleAimChanged(bool aiming)
        {
            _isAimingADS = aiming;
        }

        private void Update()
        {
            StepSimulation(Time.deltaTime);
        }

        public void StepSimulation(float deltaTime)
        {
            if (deltaTime <= 0f) return;

            EnsureInputSubscribed();
            EnsureCameraReferences();

            var adapter = _subscribedAdapter != null ? _subscribedAdapter : AbyssalInputAdapter.Instance;

            // Se o jogador emitir comando de movimento no StartMenu, iniciar automaticamente a operação
            if (!_canMove && adapter != null &&
                (adapter.MoveInput.sqrMagnitude > 0.01f || Mathf.Abs(adapter.VerticalInput) > 0.01f))
            {
                var gm = AbyssalGameManager.Instance;
                if (gm != null && gm.CurrentState == GameState.StartMenu)
                {
                    gm.StartGame();
                }
            }

            if (!_canMove)
            {
                _velocity = Vector3.MoveTowards(_velocity, Vector3.zero, _waterDrag * deltaTime);
                return;
            }

            _simTime += deltaTime;

            Vector2 moveInput = adapter != null ? adapter.MoveInput : Vector2.zero;
            float verticalInput = adapter != null ? adapter.VerticalInput : 0f;
            Vector2 lookDelta = adapter != null ? adapter.LookDelta : Vector2.zero;
            _isSprinting = adapter != null && adapter.IsSprinting && !_isAimingADS;
            if (adapter != null)
            {
                _isAimingADS = adapter.IsAimingADS;
            }

            // 1. Orientação (Yaw no corpo, Pitch no CameraPivot)
            if (lookDelta.sqrMagnitude > 0.00001f)
            {
                _yaw += lookDelta.x * _lookSensitivity;
                _pitch = Mathf.Clamp(_pitch - lookDelta.y * _lookSensitivity, -75f, 75f);
                transform.rotation = Quaternion.Euler(0f, _yaw, 0f);
                if (_cameraPivot != null && _cameraPivot != transform)
                {
                    _cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
                }
            }

            // 2. Direção de Nado 3D
            Vector3 planarWish = (transform.right * moveInput.x + transform.forward * moveInput.y);
            if (planarWish.sqrMagnitude > 1f) planarWish.Normalize();

            float targetPlanarSpeed = _isSprinting ? _sprintSwimSpeed : _swimSpeed;
            if (_isAimingADS) targetPlanarSpeed *= 0.68f;

            Vector2 planarVel = new Vector2(_velocity.x, _velocity.z);
            Vector2 planarWishVel = new Vector2(planarWish.x, planarWish.z) * targetPlanarSpeed;
            float planarStep = (planarWishVel.sqrMagnitude > 0.0001f ? _acceleration : _waterDrag) * deltaTime;
            planarVel = Vector2.MoveTowards(planarVel, planarWishVel, planarStep);

            float wishY = verticalInput * _verticalSwimSpeed;
            float verticalStep = (Mathf.Abs(wishY) > 0.0001f ? _acceleration : _waterDrag) * deltaTime;
            float velY = Mathf.MoveTowards(_velocity.y, wishY, verticalStep);

            _velocity = new Vector3(planarVel.x, velY, planarVel.y);

            // 3. Flutuabilidade Hidrostática Suave (Buoyancy)
            float buoyancyVelY = 0f;
            if (Mathf.Abs(verticalInput) < 0.01f)
            {
                buoyancyVelY = AbyssalMath.ComputeBuoyancyVerticalVelocity(_simTime, _buoyancyAmplitude, _buoyancyFrequency);
            }

            Vector3 displacement = (_velocity + new Vector3(0f, buoyancyVelY, 0f)) * deltaTime;
            Vector3 nextPos = transform.position + displacement;

            nextPos.x = Mathf.Clamp(nextPos.x, _minX, _maxX);
            nextPos.y = Mathf.Clamp(nextPos.y, _minY, _maxY);
            nextPos.z = Mathf.Clamp(nextPos.z, _minZ, _maxZ);

            transform.position = nextPos;

            // 4. Bobbing Hidrodinâmico e FOV Dinâmico (ADS)
            float targetFov = AbyssalMath.ComputeTargetCameraFov(_isAimingADS);
            _currentFov = Mathf.MoveTowards(_currentFov, targetFov, _fovLerpSpeed * 15f * deltaTime);
            if (_fpsCamera != null)
            {
                _fpsCamera.fieldOfView = _currentFov;
            }

            if (_cameraPivot != null && _cameraPivot != transform)
            {
                float planarSpeed = new Vector2(_velocity.x, _velocity.z).magnitude;
                if (planarSpeed > 0.2f)
                {
                    _bobTimer += deltaTime * (_isSprinting ? 7.5f : 5.0f);
                    float bobY = Mathf.Sin(_bobTimer) * (_isAimingADS ? 0.012f : 0.038f);
                    float bobX = Mathf.Cos(_bobTimer * 0.5f) * (_isAimingADS ? 0.008f : 0.025f);
                    _cameraPivot.localPosition = new Vector3(bobX, 0.65f + bobY, 0f);
                }
                else
                {
                    _cameraPivot.localPosition = Vector3.Lerp(_cameraPivot.localPosition, new Vector3(0f, 0.65f, 0f), deltaTime * 6f);
                }
            }
        }

        public void SetMovementActive(bool active)
        {
            _canMove = active;
            if (!active)
            {
                _velocity = Vector3.zero;
            }
        }

        public void ResetPosition()
        {
            transform.position = _initialPosition;
            transform.rotation = Quaternion.identity;
            _velocity = Vector3.zero;
            _yaw = 0f;
            _pitch = 0f;
            _simTime = 0f;
            _isAimingADS = false;
            _isSprinting = false;
            _currentFov = AbyssalMath.DEFAULT_FOV;
            if (_fpsCamera != null) _fpsCamera.fieldOfView = _currentFov;
            if (_cameraPivot != null && _cameraPivot != transform)
            {
                _cameraPivot.localPosition = new Vector3(0f, 0.65f, 0f);
                _cameraPivot.localRotation = Quaternion.identity;
            }
        }
    }
}
