using System;
using Studio.Foundation;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Drone Sentinela Subaquático Hostil: patrulha áreas de naufrágio e recifes de corais,
    /// persegue o mergulhador quando detectado, causa dano elétrico por proximidade e pode ser
    /// abatido pelo Rifle de Plasma ou pela Faca Tática.
    /// </summary>
    public class AbyssalThreatDrone : MonoBehaviour
    {
        [Header("Stats")]
        [SerializeField] private float _maxHealth = 100.0f;
        [SerializeField] private float _currentHealth = 100.0f;
        [SerializeField] private float _detectionRadius = 18.0f;
        [SerializeField] private float _attackRadius = 2.6f;
        [SerializeField] private float _contactDamagePerSec = 16.0f;
        [SerializeField] private float _patrolRadius = 3.5f;
        [SerializeField] private float _patrolSpeed = 1.8f;
        [SerializeField] private float _chaseSpeed = 3.6f;

        private Vector3 _anchorPosition;
        private float _patrolPhase;
        private bool _isDestroyed;

        public float MaxHealth => _maxHealth;
        public float CurrentHealth => _currentHealth;
        public bool IsDestroyed => _isDestroyed;

        public event Action<AbyssalThreatDrone> OnDroneDestroyed;

        private void Awake()
        {
            _anchorPosition = transform.position;
            _patrolPhase = Mathf.Abs(transform.position.z * 0.35f);
        }

        private void Update()
        {
            if (_isDestroyed) return;
            var gm = AbyssalGameManager.Instance;
            if (gm != null && gm.CurrentState != GameState.Playing) return;

            StepDroneAI(Time.deltaTime);
        }

        public void StepDroneAI(float deltaTime)
        {
            if (_isDestroyed || deltaTime <= 0f) return;

            _patrolPhase += deltaTime * _patrolSpeed;

            var gm = AbyssalGameManager.Instance;
            var playerMotor = gm != null ? gm.PlayerMotor : null;

            if (playerMotor != null)
            {
                Vector3 toPlayer = playerMotor.transform.position - transform.position;
                float dist = toPlayer.magnitude;

                if (dist <= _detectionRadius && dist > 0.1f)
                {
                    Vector3 targetPos = playerMotor.transform.position + new Vector3(0f, 0.3f, 0f);
                    transform.position = Vector3.MoveTowards(transform.position, targetPos, _chaseSpeed * deltaTime);
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(toPlayer.normalized), deltaTime * 6f);

                    if (dist <= _attackRadius)
                    {
                        var survival = playerMotor.GetComponent<OxygenSurvivalSystem>();
                        if (survival != null)
                        {
                            survival.ApplyDamage(_contactDamagePerSec * deltaTime);
                        }
                    }
                    return;
                }
            }

            // Patrulha orbital suave ao redor da âncora
            Vector3 orbitOffset = new Vector3(
                Mathf.Cos(_patrolPhase) * _patrolRadius,
                Mathf.Sin(_patrolPhase * 1.4f) * 0.65f,
                Mathf.Sin(_patrolPhase) * (_patrolRadius * 0.6f)
            );
            transform.position = Vector3.Lerp(transform.position, _anchorPosition + orbitOffset, deltaTime * 3f);
            transform.Rotate(0f, 45f * deltaTime, 0f, Space.World);
        }

        public bool TakeDamage(float damage)
        {
            if (_isDestroyed || damage <= 0f) return false;

            _currentHealth = Mathf.Max(0f, _currentHealth - damage);
            ProceduralAudioService.Instance?.PlayHit();

            if (_currentHealth <= 0.001f)
            {
                _currentHealth = 0f;
                _isDestroyed = true;
                gameObject.SetActive(false);
                OnDroneDestroyed?.Invoke(this);
                AbyssalGameManager.Instance?.RegisterDroneDestroyed();
                return true;
            }
            return false;
        }

        public void ResetDrone()
        {
            _isDestroyed = false;
            _currentHealth = _maxHealth;
            transform.position = _anchorPosition;
            gameObject.SetActive(true);
        }
    }
}
