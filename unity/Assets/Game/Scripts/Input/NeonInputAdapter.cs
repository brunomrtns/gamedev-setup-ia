using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Studio.Input
{
    public class NeonInputAdapter : MonoBehaviour
    {
        private static NeonInputAdapter _instance;
        public static NeonInputAdapter Instance => _instance;

        public event Action OnMoveLeft;
        public event Action OnMoveRight;
        public event Action OnJump;
        public event Action OnSlide;
        public event Action OnRestart;

        [Header("Swipe Tuning")]
        [SerializeField] private float _minSwipeDistancePixels = 40.0f;
        [SerializeField] private float _maxSwipeTimeSeconds = 0.8f;

        private Vector2 _touchStartPosition;
        private float _touchStartTime;
        private bool _isTrackingTouch;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Update()
        {
            PollInputSystem();
        }

        private void PollInputSystem()
        {
            // 1. New Input System Keyboard
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.wasPressedThisFrame || keyboard.leftArrowKey.wasPressedThisFrame)
                {
                    TriggerMoveLeft();
                }
                else if (keyboard.dKey.wasPressedThisFrame || keyboard.rightArrowKey.wasPressedThisFrame)
                {
                    TriggerMoveRight();
                }

                if (keyboard.wKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
                {
                    TriggerJump();
                }

                if (keyboard.sKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame || keyboard.leftShiftKey.wasPressedThisFrame)
                {
                    TriggerSlide();
                }

                if (keyboard.rKey.wasPressedThisFrame)
                {
                    TriggerRestart();
                }
            }

            // 2. New Input System Touchscreen
            var touch = Touchscreen.current;
            if (touch != null)
            {
                if (touch.primaryTouch.press.wasPressedThisFrame)
                {
                    _touchStartPosition = touch.primaryTouch.position.ReadValue();
                    _touchStartTime = Time.unscaledTime;
                    _isTrackingTouch = true;
                }
                else if (touch.primaryTouch.press.wasReleasedThisFrame && _isTrackingTouch)
                {
                    _isTrackingTouch = false;
                    float duration = Time.unscaledTime - _touchStartTime;
                    if (duration <= _maxSwipeTimeSeconds)
                    {
                        Vector2 currentPos = touch.primaryTouch.position.ReadValue();
                        Vector2 delta = currentPos - _touchStartPosition;
                        if (delta.magnitude >= _minSwipeDistancePixels)
                        {
                            if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                            {
                                if (delta.x > 0) TriggerMoveRight();
                                else TriggerMoveLeft();
                            }
                            else
                            {
                                if (delta.y > 0) TriggerJump();
                                else TriggerSlide();
                            }
                        }
                    }
                }
            }

            // 3. Fallback seguro para legado
            try
            {
                PollLegacyInput();
            }
            catch (InvalidOperationException)
            {
                // Silenciosamente ignorado quando modo legado estiver inativo
            }
        }

        private void PollLegacyInput()
        {
            if (UnityEngine.Input.GetKeyDown(KeyCode.A) || UnityEngine.Input.GetKeyDown(KeyCode.LeftArrow))
            {
                TriggerMoveLeft();
            }
            else if (UnityEngine.Input.GetKeyDown(KeyCode.D) || UnityEngine.Input.GetKeyDown(KeyCode.RightArrow))
            {
                TriggerMoveRight();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.W) || UnityEngine.Input.GetKeyDown(KeyCode.UpArrow) || UnityEngine.Input.GetKeyDown(KeyCode.Space))
            {
                TriggerJump();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.S) || UnityEngine.Input.GetKeyDown(KeyCode.DownArrow) || UnityEngine.Input.GetKeyDown(KeyCode.LeftShift))
            {
                TriggerSlide();
            }

            if (UnityEngine.Input.GetKeyDown(KeyCode.R))
            {
                TriggerRestart();
            }
        }

        public void TriggerMoveLeft() => OnMoveLeft?.Invoke();
        public void TriggerMoveRight() => OnMoveRight?.Invoke();
        public void TriggerJump() => OnJump?.Invoke();
        public void TriggerSlide() => OnSlide?.Invoke();
        public void TriggerRestart() => OnRestart?.Invoke();
    }
}
