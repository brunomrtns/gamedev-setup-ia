using Studio.Foundation;
using Studio.Input;
using UnityEngine;

namespace Studio.Gameplay
{
    [RequireComponent(typeof(PlayerMovement))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private PlayerMovement _movement;
        [SerializeField] private RobotAnimator _animator;

        private float _lastX = 0f;

        private void Awake()
        {
            if (_movement == null) _movement = GetComponent<PlayerMovement>();
            if (_animator == null) _animator = GetComponentInChildren<RobotAnimator>();
        }

        private void OnEnable()
        {
            if (NeonInputAdapter.Instance != null)
            {
                NeonInputAdapter.Instance.OnMoveLeft += HandleMoveLeft;
                NeonInputAdapter.Instance.OnMoveRight += HandleMoveRight;
                NeonInputAdapter.Instance.OnJump += HandleJump;
                NeonInputAdapter.Instance.OnSlide += HandleSlide;
            }
        }

        private void OnDisable()
        {
            if (NeonInputAdapter.Instance != null)
            {
                NeonInputAdapter.Instance.OnMoveLeft -= HandleMoveLeft;
                NeonInputAdapter.Instance.OnMoveRight -= HandleMoveRight;
                NeonInputAdapter.Instance.OnJump -= HandleJump;
                NeonInputAdapter.Instance.OnSlide -= HandleSlide;
            }
        }

        private void Start()
        {
            _lastX = transform.position.x;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            float currentX = transform.position.x;
            float lateralDelta = currentX - _lastX;
            _lastX = currentX;

            if (_animator != null && _movement != null)
            {
                _animator.Animate(lateralDelta, _movement.IsJumping, _movement.IsSliding, dt);
            }
        }

        private void HandleMoveLeft()
        {
            _movement.ChangeLane(-1);
        }

        private void HandleMoveRight()
        {
            _movement.ChangeLane(1);
        }

        private void HandleJump()
        {
            if (!_movement.IsJumping)
            {
                _movement.Jump();
                if (ProceduralAudioService.Instance != null)
                    ProceduralAudioService.Instance.PlayJump();
            }
        }

        private void HandleSlide()
        {
            if (!_movement.IsSliding)
            {
                _movement.Slide();
                if (ProceduralAudioService.Instance != null)
                    ProceduralAudioService.Instance.PlaySlide();
            }
        }

        public void HandleCollisionWithHazard()
        {
            _movement.SetMovementActive(false);
            if (_animator != null)
            {
                _animator.PlayHitWobble();
            }
            if (ProceduralAudioService.Instance != null)
            {
                ProceduralAudioService.Instance.PlayHit();
            }
        }
    }
}
