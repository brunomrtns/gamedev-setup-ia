using System;
using Studio.Foundation;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Studio.Input
{
    /// <summary>
    /// Adaptador de entrada tática em primeira pessoa para "Abyssal Breach".
    /// Implementa IGameInputAdapter e oferece métodos determinísticos livres de condições de corrida
    /// para nado 3D (6-DoF moderado), mira óptica (ADS), disparo de rifle, recarga e faca tática.
    /// </summary>
    public class AbyssalInputAdapter : MonoBehaviour, IGameInputAdapter
    {
        private static AbyssalInputAdapter _instance;
        public static AbyssalInputAdapter Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<AbyssalInputAdapter>();
                }
                return _instance;
            }
        }

        public Vector2 MoveInput { get; private set; }
        public float VerticalInput { get; private set; }
        public Vector2 LookDelta { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsAimingADS { get; private set; }

        public event Action OnFire;
        public event Action<bool> OnAimChanged;
        public event Action OnReload;
        public event Action OnMelee;
        public event Action OnSwitchWeapon;
        public event Action OnInteract;
        public event Action OnRestart;
        public event Action<string> OnActionTriggered;

        private bool _externalOverrideActive;

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
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Update()
        {
            if (!_externalOverrideActive)
            {
                PollHardwareInput();
            }
        }

        private void PollHardwareInput()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;

            if (kb != null)
            {
                float moveX = 0f;
                float moveZ = 0f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) moveX -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveX += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) moveZ -= 1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) moveZ += 1f;
                MoveInput = Vector2.ClampMagnitude(new Vector2(moveX, moveZ), 1f);

                float vert = 0f;
                if (kb.spaceKey.isPressed) vert += 1f;
                if (kb.cKey.isPressed || kb.leftCtrlKey.isPressed) vert -= 1f;
                VerticalInput = Mathf.Clamp(vert, -1f, 1f);

                IsSprinting = kb.leftShiftKey.isPressed;

                if (kb.qKey.wasPressedThisFrame)
                {
                    TriggerToggleAim();
                }

                if (kb.fKey.wasPressedThisFrame)
                {
                    TriggerFire();
                }

                if (kb.rKey.wasPressedThisFrame)
                {
                    TriggerReload();
                }

                if (kb.vKey.wasPressedThisFrame)
                {
                    TriggerMelee();
                }

                if (kb.digit1Key.wasPressedThisFrame || kb.digit2Key.wasPressedThisFrame || kb.tabKey.wasPressedThisFrame)
                {
                    TriggerSwitchWeapon();
                }

                if (kb.eKey.wasPressedThisFrame)
                {
                    TriggerInteract();
                }

                if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                {
                    TriggerRestart();
                }
            }

            if (mouse != null)
            {
                LookDelta = mouse.delta.ReadValue() * 0.12f;

                if (mouse.leftButton.wasPressedThisFrame)
                {
                    TriggerFire();
                }

                if (mouse.rightButton.wasPressedThisFrame)
                {
                    SetAiming(true);
                }
                else if (mouse.rightButton.wasReleasedThisFrame)
                {
                    SetAiming(false);
                }

                if (mouse.middleButton.wasPressedThisFrame)
                {
                    TriggerMelee();
                }
            }
        }

        #region Métodos Determinísticos e de Simulação (Testes & Gameplay)

        public void SetMoveInput(Vector2 move)
        {
            _externalOverrideActive = true;
            MoveInput = Vector2.ClampMagnitude(move, 1f);
            if (move.sqrMagnitude > 0.001f)
            {
                OnActionTriggered?.Invoke("SwimMove");
            }
        }

        public void SetVerticalInput(float vertical)
        {
            _externalOverrideActive = true;
            VerticalInput = Mathf.Clamp(vertical, -1f, 1f);
            if (Mathf.Abs(vertical) > 0.001f)
            {
                OnActionTriggered?.Invoke(vertical > 0f ? "Ascend" : "Descend");
            }
        }

        public void SetLookDelta(Vector2 lookDelta)
        {
            _externalOverrideActive = true;
            LookDelta = lookDelta;
        }

        public void SetSprint(bool sprinting)
        {
            _externalOverrideActive = true;
            IsSprinting = sprinting;
            OnActionTriggered?.Invoke(sprinting ? "SprintStart" : "SprintStop");
        }

        public void SetAiming(bool aiming)
        {
            if (IsAimingADS == aiming) return;
            IsAimingADS = aiming;
            OnAimChanged?.Invoke(IsAimingADS);
            OnActionTriggered?.Invoke(IsAimingADS ? "AimADS_On" : "AimADS_Off");
        }

        public void TriggerToggleAim()
        {
            SetAiming(!IsAimingADS);
        }

        public void TriggerFire()
        {
            OnFire?.Invoke();
            OnActionTriggered?.Invoke("Fire");
        }

        public void TriggerReload()
        {
            OnReload?.Invoke();
            OnActionTriggered?.Invoke("Reload");
        }

        public void TriggerMelee()
        {
            OnMelee?.Invoke();
            OnActionTriggered?.Invoke("Melee");
        }

        public void TriggerSwitchWeapon()
        {
            OnSwitchWeapon?.Invoke();
            OnActionTriggered?.Invoke("SwitchWeapon");
        }

        public void TriggerInteract()
        {
            OnInteract?.Invoke();
            OnActionTriggered?.Invoke("Interact");
        }

        public void TriggerRestart()
        {
            OnRestart?.Invoke();
            OnActionTriggered?.Invoke("Restart");
        }

        public void ClearOverrideInputs()
        {
            _externalOverrideActive = false;
            MoveInput = Vector2.zero;
            VerticalInput = 0f;
            LookDelta = Vector2.zero;
            IsSprinting = false;
            SetAiming(false);
        }

        public void TriggerAction(string actionId)
        {
            if (string.IsNullOrEmpty(actionId)) return;
            switch (actionId)
            {
                case "Fire": TriggerFire(); break;
                case "AimToggle": TriggerToggleAim(); break;
                case "AimOn": SetAiming(true); break;
                case "AimOff": SetAiming(false); break;
                case "Reload": TriggerReload(); break;
                case "Melee": TriggerMelee(); break;
                case "SwitchWeapon": TriggerSwitchWeapon(); break;
                case "Interact": TriggerInteract(); break;
                case "Restart": TriggerRestart(); break;
                case "Ascend": SetVerticalInput(1f); break;
                case "Descend": SetVerticalInput(-1f); break;
                default: OnActionTriggered?.Invoke(actionId); break;
            }
        }

        #endregion
    }
}
