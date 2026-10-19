using System;
using Studio.Foundation;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Sistema de Sobrevivência Subaquática: gerencia o nível de Oxigênio (O2) e a Integridade do Traje (HP).
    /// Consome O2 progressivamente (com taxa ampliada durante Sprint) e aplica dano de hipóxia quando O2 se esgota.
    /// </summary>
    public class OxygenSurvivalSystem : MonoBehaviour
    {
        [Header("Oxygen Configuration")]
        [SerializeField] private float _maxOxygen = AbyssalMath.DEFAULT_MAX_OXYGEN;
        [SerializeField] private float _currentOxygen = AbyssalMath.DEFAULT_MAX_OXYGEN;
        [SerializeField] private float _normalDrainRate = AbyssalMath.NORMAL_OXYGEN_DRAIN_PER_SEC;
        [SerializeField] private float _sprintDrainRate = AbyssalMath.SPRINT_OXYGEN_DRAIN_PER_SEC;
        [SerializeField] private float _hypoxiaDamageRate = AbyssalMath.HYPOXIA_DAMAGE_PER_SEC;

        [Header("Suit Integrity (Health)")]
        [SerializeField] private float _maxHealth = AbyssalMath.DEFAULT_MAX_HEALTH;
        [SerializeField] private float _currentHealth = AbyssalMath.DEFAULT_MAX_HEALTH;

        private bool _isActive;

        public float MaxOxygen => _maxOxygen;
        public float CurrentOxygen => _currentOxygen;
        public float OxygenRatio => _maxOxygen > 0f ? Mathf.Clamp01(_currentOxygen / _maxOxygen) : 0f;
        public bool IsHypoxic => _currentOxygen <= 0.001f;
        public bool IsLowOxygen => OxygenRatio <= 0.25f;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public float HealthRatio => _maxHealth > 0f ? Mathf.Clamp01(_currentHealth / _maxHealth) : 0f;
        public bool IsDead => _currentHealth <= 0.001f;
        public bool IsActive => _isActive;

        public event Action<float, float> OnVitalsChanged; // (oxygen, health)
        public event Action OnPlayerDepleted;

        public void SetSurvivalActive(bool active)
        {
            _isActive = active;
        }

        private void Update()
        {
            if (_isActive)
            {
                var motor = GetComponent<AbyssalFirstPersonMotor>();
                bool sprinting = motor != null && motor.IsSprinting;
                StepSurvival(Time.deltaTime, sprinting);
            }
        }

        public void StepSurvival(float deltaTime, bool isSprinting)
        {
            if (deltaTime <= 0f || IsDead) return;

            _currentOxygen = AbyssalMath.ComputeOxygenAfterStep(
                _currentOxygen,
                _maxOxygen,
                isSprinting,
                _normalDrainRate,
                _sprintDrainRate,
                deltaTime);

            float hypoxiaDamage = AbyssalMath.ComputeHypoxiaDamage(_currentOxygen, _hypoxiaDamageRate, deltaTime);
            if (hypoxiaDamage > 0f)
            {
                ApplyDamage(hypoxiaDamage);
            }
            else
            {
                OnVitalsChanged?.Invoke(_currentOxygen, _currentHealth);
            }
        }

        public void AddOxygen(float amount)
        {
            if (IsDead || amount <= 0f) return;
            _currentOxygen = Mathf.Clamp(_currentOxygen + amount, 0f, _maxOxygen);
            ProceduralAudioService.Instance?.PlayOxygenRefill();
            OnVitalsChanged?.Invoke(_currentOxygen, _currentHealth);
        }

        public void HealSuit(float amount)
        {
            if (IsDead || amount <= 0f) return;
            _currentHealth = Mathf.Clamp(_currentHealth + amount, 0f, _maxHealth);
            OnVitalsChanged?.Invoke(_currentOxygen, _currentHealth);
        }

        public void ApplyDamage(float damage)
        {
            if (IsDead || damage <= 0f) return;

            _currentHealth = Mathf.Clamp(_currentHealth - damage, 0f, _maxHealth);
            OnVitalsChanged?.Invoke(_currentOxygen, _currentHealth);

            if (_currentHealth <= 0.001f)
            {
                _currentHealth = 0f;
                ProceduralAudioService.Instance?.PlayHit();
                OnPlayerDepleted?.Invoke();
                AbyssalGameManager.Instance?.GameOver();
            }
        }

        public void ResetVitals()
        {
            _currentOxygen = _maxOxygen;
            _currentHealth = _maxHealth;
            OnVitalsChanged?.Invoke(_currentOxygen, _currentHealth);
        }
    }
}
