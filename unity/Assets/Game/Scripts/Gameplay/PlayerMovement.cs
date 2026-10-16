using System;
using Studio.Foundation;
using UnityEngine;

namespace Studio.Gameplay
{
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Speed Settings")]
        [SerializeField] private float _forwardSpeed = GameConstants.DEFAULT_FORWARD_SPEED;
        [SerializeField] private float _maxForwardSpeed = GameConstants.MAX_FORWARD_SPEED;
        [SerializeField] private float _accelerationRate = 0.05f;

        [Header("Lane Settings")]
        [SerializeField] private int _currentLane = 0; // -1: Left, 0: Center, 1: Right
        [SerializeField] private float _laneChangeSpeed = 16.0f;

        [Header("Jump & Slide Tuning")]
        [SerializeField] private float _jumpHeight = GameConstants.JUMP_HEIGHT;
        [SerializeField] private float _jumpDuration = GameConstants.JUMP_DURATION;
        [SerializeField] private float _slideDuration = GameConstants.SLIDE_DURATION;

        [Header("Colliders")]
        [SerializeField] private BoxCollider _boxCollider;

        private float _targetX = 0f;
        private float _jumpTimer = 0f;
        private bool _isJumping = false;
        private float _slideTimer = 0f;
        private bool _isSliding = false;
        private bool _canMove = false;

        private Vector3 _originalColliderCenter;
        private Vector3 _originalColliderSize;

        public int CurrentLane => _currentLane;
        public float TargetX => _targetX;
        public bool IsJumping => _isJumping;
        public bool IsSliding => _isSliding;
        public float ForwardSpeed => _forwardSpeed;
        public bool CanMove => _canMove;

        public event Action OnLaneChanged;
        public event Action OnJumpStarted;
        public event Action OnSlideStarted;
        public event Action OnSlideEnded;

        private void Awake()
        {
            if (_boxCollider == null)
            {
                _boxCollider = GetComponent<BoxCollider>();
            }

            if (_boxCollider != null)
            {
                _originalColliderCenter = _boxCollider.center;
                _originalColliderSize = _boxCollider.size;
            }
        }

        public void SetMovementActive(bool active)
        {
            _canMove = active;
        }

        public void ChangeLane(int direction)
        {
            if (!_canMove) return;

            int newLane = LaneMath.ClampLane(_currentLane + direction);
            if (newLane != _currentLane)
            {
                _currentLane = newLane;
                _targetX = LaneMath.GetLaneX(_currentLane);
                OnLaneChanged?.Invoke();
            }
        }

        public void Jump()
        {
            if (!_canMove || _isJumping) return;

            _isJumping = true;
            _jumpTimer = 0f;

            // Se estava deslizando, cancela o slide
            if (_isSliding)
            {
                EndSlide();
            }

            OnJumpStarted?.Invoke();
        }

        public void Slide()
        {
            if (!_canMove || _isSliding) return;

            // Se estiver no ar, antecipa a queda rápido
            if (_isJumping)
            {
                _jumpTimer = _jumpDuration * 0.9f;
            }

            _isSliding = true;
            _slideTimer = 0f;

            // Reduz colisor para passar sob vigas altas
            if (_boxCollider != null)
            {
                _boxCollider.size = new Vector3(_originalColliderSize.x, _originalColliderSize.y * 0.45f, _originalColliderSize.z);
                _boxCollider.center = new Vector3(_originalColliderCenter.x, _originalColliderCenter.y * 0.45f, _originalColliderCenter.z);
            }

            OnSlideStarted?.Invoke();
        }

        private void EndSlide()
        {
            _isSliding = false;
            _slideTimer = 0f;

            if (_boxCollider != null)
            {
                _boxCollider.size = _originalColliderSize;
                _boxCollider.center = _originalColliderCenter;
            }

            OnSlideEnded?.Invoke();
        }

        private void Update()
        {
            if (!_canMove) return;

            float dt = Time.deltaTime;

            // 1. Aceleração progressiva ao longo do percurso
            _forwardSpeed = Mathf.Min(_forwardSpeed + _accelerationRate * dt, _maxForwardSpeed);

            // 2. Movimento para frente no eixo Z
            Vector3 pos = transform.position;
            pos.z += _forwardSpeed * dt;

            // 3. Interpolação suave lateral no eixo X
            pos.x = Mathf.MoveTowards(pos.x, _targetX, _laneChangeSpeed * dt);

            // 4. Salto parabólico no eixo Y
            if (_isJumping)
            {
                _jumpTimer += dt;
                float normalizedTime = Mathf.Clamp01(_jumpTimer / _jumpDuration);
                pos.y = LaneMath.ComputeParabolicJumpHeight(normalizedTime, _jumpHeight);

                if (_jumpTimer >= _jumpDuration)
                {
                    _isJumping = false;
                    _jumpTimer = 0f;
                    pos.y = 0f;
                }
            }
            else
            {
                pos.y = 0f;
            }

            // 5. Timer de deslizamento
            if (_isSliding)
            {
                _slideTimer += dt;
                if (_slideTimer >= _slideDuration)
                {
                    EndSlide();
                }
            }

            transform.position = pos;
        }

        public void ResetPosition()
        {
            _currentLane = 0;
            _targetX = 0f;
            _forwardSpeed = GameConstants.DEFAULT_FORWARD_SPEED;
            _isJumping = false;
            _isSliding = false;
            _jumpTimer = 0f;
            _slideTimer = 0f;

            if (_boxCollider != null)
            {
                _boxCollider.size = _originalColliderSize;
                _boxCollider.center = _originalColliderCenter;
            }

            transform.position = Vector3.zero;
        }
    }
}
