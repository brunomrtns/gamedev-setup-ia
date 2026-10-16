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
        private bool _isSubscribed = false;
        private NeonInputAdapter _subscribedAdapter;

        public bool IsSubscribedToInput => _isSubscribed;

        private void Awake()
        {
            ResolveReferences();
        }

        public void ResolveReferences()
        {
            if (_movement == null) _movement = GetComponent<PlayerMovement>();
            if (_animator == null) _animator = GetComponentInChildren<RobotAnimator>();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureInputSubscribed();
        }

        private void Start()
        {
            ResolveReferences();
            EnsureInputSubscribed();
            _lastX = transform.position.x;
        }

        private void OnDisable()
        {
            UnsubscribeInput();
        }

        private void OnDestroy()
        {
            UnsubscribeInput();
        }

        public void EnsureInputSubscribed()
        {
            var adapter = NeonInputAdapter.Instance;
            if (adapter == null) return;

            if (_isSubscribed && _subscribedAdapter == adapter) return;

            UnsubscribeInput();

            adapter.OnMoveLeft += HandleMoveLeft;
            adapter.OnMoveRight += HandleMoveRight;
            adapter.OnJump += HandleJump;
            adapter.OnSlide += HandleSlide;

            _subscribedAdapter = adapter;
            _isSubscribed = true;
        }

        private void UnsubscribeInput()
        {
            if (_subscribedAdapter != null)
            {
                _subscribedAdapter.OnMoveLeft -= HandleMoveLeft;
                _subscribedAdapter.OnMoveRight -= HandleMoveRight;
                _subscribedAdapter.OnJump -= HandleJump;
                _subscribedAdapter.OnSlide -= HandleSlide;
            }
            _subscribedAdapter = null;
            _isSubscribed = false;
        }

        private void Update()
        {
            if (!_isSubscribed)
            {
                EnsureInputSubscribed();
            }
        }

        private void LateUpdate()
        {
            StepController(Time.deltaTime);
        }

        public void StepController(float dt)
        {
            ResolveReferences();
            if (!_isSubscribed)
            {
                EnsureInputSubscribed();
            }

            float currentX = transform.position.x;
            float lateralDelta = currentX - _lastX;
            _lastX = currentX;

            if (_animator != null && _movement != null)
            {
                _animator.Animate(lateralDelta, _movement.IsJumping, _movement.IsSliding, dt);
            }
        }

        private void EnsureActiveGameplayIfOnStartMenu()
        {
            if (_movement != null && !_movement.CanMove && GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.StartMenu)
            {
                GameManager.Instance.StartGame();
            }
        }

        private void HandleMoveLeft()
        {
            ResolveReferences();
            EnsureActiveGameplayIfOnStartMenu();
            _movement.ChangeLane(-1);
        }

        private void HandleMoveRight()
        {
            ResolveReferences();
            EnsureActiveGameplayIfOnStartMenu();
            _movement.ChangeLane(1);
        }

        private void HandleJump()
        {
            ResolveReferences();
            EnsureActiveGameplayIfOnStartMenu();
            if (!_movement.IsJumping)
            {
                _movement.Jump();
                if (ProceduralAudioService.Instance != null)
                    ProceduralAudioService.Instance.PlayJump();
            }
        }

        private void HandleSlide()
        {
            ResolveReferences();
            EnsureActiveGameplayIfOnStartMenu();
            if (!_movement.IsSliding)
            {
                _movement.Slide();
                if (ProceduralAudioService.Instance != null)
                    ProceduralAudioService.Instance.PlaySlide();
            }
        }

        public void HandleCollisionWithHazard()
        {
            ResolveReferences();
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
