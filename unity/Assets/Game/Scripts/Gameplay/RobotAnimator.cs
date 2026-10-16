using UnityEngine;

namespace Studio.Gameplay
{
    public class RobotAnimator : MonoBehaviour
    {
        [Header("Chassis & Visual Transforms")]
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private Transform _leftThruster;
        [SerializeField] private Transform _rightThruster;
        [SerializeField] private Transform _sensorVisor;
        [SerializeField] private Animator _animator;

        [Header("Vertical Placement & Grounding")]
        [SerializeField] private float _baseVisualY = 0.0f; // 0.0m para bípedes no chão, 0.4m para droides hover
        [SerializeField] private bool _isHoverDrone = false;

        [Header("Hover Bobbing")]
        [SerializeField] private float _hoverFrequency = 4.0f;
        [SerializeField] private float _hoverAmplitude = 0.08f;

        [Header("Banking (Tilt)")]
        [SerializeField] private float _maxBankAngle = 14.0f;
        [SerializeField] private float _bankSmoothSpeed = 12.0f;

        private float _currentBankAngle = 0f;
        private float _hoverTimer = 0f;
        private Vector3 _originalRootScale = Vector3.one;
        private Vector3 _targetScale = Vector3.one;

        private static readonly int AnimHashRun = Animator.StringToHash("RunCycle");
        private static readonly int AnimHashJump = Animator.StringToHash("Jump");
        private static readonly int AnimHashSlide = Animator.StringToHash("Slide");

        private void Awake()
        {
            if (_visualRoot != null)
            {
                _originalRootScale = _visualRoot.localScale;
                _targetScale = _originalRootScale;
            }
            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        public void Initialize(Transform visualRoot, Animator animator = null, bool isHoverDrone = false, float baseY = 0.0f)
        {
            _visualRoot = visualRoot;
            _animator = animator;
            _isHoverDrone = isHoverDrone;
            _baseVisualY = baseY;

            if (_visualRoot != null)
            {
                _originalRootScale = _visualRoot.localScale;
                _targetScale = _originalRootScale;
            }
        }

        public void Animate(float lateralDelta, bool isJumping, bool isSliding, float dt)
        {
            if (_visualRoot == null) return;

            // 1. Controle do Animator Esquelético (Mecanim Humanoid/Generic)
            if (_animator != null)
            {
                if (isJumping)
                {
                    _animator.CrossFade(AnimHashJump, 0.1f);
                }
                else if (isSliding)
                {
                    _animator.CrossFade(AnimHashSlide, 0.15f);
                }
                else
                {
                    _animator.CrossFade(AnimHashRun, 0.2f);
                }
            }

            // 2. Flutuação (apenas para droide hover; bípedes mantêm pés no chão)
            float bobOffset = 0f;
            if (_isHoverDrone)
            {
                _hoverTimer += dt * _hoverFrequency;
                bobOffset = Mathf.Sin(_hoverTimer) * _hoverAmplitude;
            }

            // 3. Banking / Roll lateral suave
            float targetBank = 0f;
            if (lateralDelta > 0.01f)
            {
                targetBank = -_maxBankAngle; // Incline para a direita ao mover à direita
            }
            else if (lateralDelta < -0.01f)
            {
                targetBank = _maxBankAngle; // Incline para a esquerda ao mover à esquerda
            }

            _currentBankAngle = Mathf.Lerp(_currentBankAngle, targetBank, _bankSmoothSpeed * dt);

            // 4. Squash & Stretch procedural (usado se não houver animator)
            if (_animator == null)
            {
                if (isSliding)
                {
                    _targetScale = new Vector3(_originalRootScale.x * 1.3f, _originalRootScale.y * 0.45f, _originalRootScale.z * 1.2f);
                }
                else if (isJumping)
                {
                    _targetScale = new Vector3(_originalRootScale.x * 0.85f, _originalRootScale.y * 1.25f, _originalRootScale.z * 0.85f);
                }
                else
                {
                    _targetScale = _originalRootScale;
                }
                _visualRoot.localScale = Vector3.Lerp(_visualRoot.localScale, _targetScale, 15f * dt);
            }

            // Posicionamento e rotação normalizados (Y = 0 nos pés, sem tombar de costas)
            _visualRoot.localPosition = new Vector3(0f, _baseVisualY + bobOffset, 0f);
            _visualRoot.localRotation = Quaternion.Euler(0f, 0f, _currentBankAngle);

            // Propulsores se existirem
            if (_leftThruster != null && _rightThruster != null)
            {
                float thrusterPitch = isSliding ? 45f : (isJumping ? -25f : 0f);
                _leftThruster.localRotation = Quaternion.Euler(thrusterPitch, 0f, 0f);
                _rightThruster.localRotation = Quaternion.Euler(thrusterPitch, 0f, 0f);
            }
        }

        public void PlayHitWobble()
        {
            if (_visualRoot != null)
            {
                _visualRoot.localRotation = Quaternion.Euler(-15f, 20f, -25f);
                _visualRoot.localScale = _originalRootScale * 0.8f;
            }
        }
    }
}
