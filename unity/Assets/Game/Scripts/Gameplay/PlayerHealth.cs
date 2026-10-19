using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Vida do jogador (100 HP): dano por atropelamento (colisão com rigidbody
    /// pesado), queda de altura (impacto de aterrissagem |vy| &gt; 11 m/s) e
    /// proximidade de explosões veiculares. Integrado ao NeonDistrictHUD.
    /// </summary>
    public class PlayerHealth : MonoBehaviour
    {
        public static PlayerHealth Instance { get; private set; }

        [SerializeField] private float _maxHealth = 100f;
        [SerializeField] private float _runoverMinSpeed = 4f;
        [SerializeField] private float _runoverDamageScale = 4.5f;
        [SerializeField] private float _fallSafeSpeed = 11f;
        [SerializeField] private float _fallDamageScale = 7f;
        [SerializeField] private float _explosionMaxDamage = 65f;
        [SerializeField] private float _regenDelay = 6f;
        [SerializeField] private float _regenRate = 6f;

        private float _health;
        private float _lastDamageTime;
        private OnFootMotor _motor;

        public float Health => _health;
        public float MaxHealth => _maxHealth;
        public bool IsDead => _health <= 0f;
        public event System.Action<float, float> OnHealthChanged; // (health, max)

        private void Awake()
        {
            Instance = this;
            _health = _maxHealth;
            _motor = GetComponent<OnFootMotor>();
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Update()
        {
            // Queda: o motor publica a velocidade de impacto do pouso
            if (_motor != null && _motor.LastLandImpactSpeed > _fallSafeSpeed)
            {
                float v = _motor.LastLandImpactSpeed;
                _motor.LastLandImpactSpeed = 0f;
                TakeDamage((v - _fallSafeSpeed) * _fallDamageScale);
            }
            else if (_motor != null)
            {
                _motor.LastLandImpactSpeed = 0f;
            }

            // Regen fora de combate
            if (_health > 0f && _health < _maxHealth
                && Time.time - _lastDamageTime > _regenDelay)
            {
                _health = Mathf.Min(_maxHealth, _health + _regenRate * Time.deltaTime);
                OnHealthChanged?.Invoke(_health, _maxHealth);
            }
        }

        /// <summary>Atropelamento: CharacterController colide com rb pesado.</summary>
        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            var rb = hit.rigidbody;
            if (rb == null || rb.mass < 200f || IsDead) return;
            // a pé o jogador não sofre dano do próprio carro parado
            float speed = rb.linearVelocity.magnitude;
            if (speed < _runoverMinSpeed) return;
            TakeDamage(Mathf.Min(80f, speed * _runoverDamageScale));
            // empurra o jogador pra fora da trajetória
            if (_motor != null)
            {
                var push = hit.moveDirection;
                push.y = 0f;
                if (push.sqrMagnitude > 0.001f)
                    transform.position += push.normalized * 0.5f + Vector3.up * 0.1f;
            }
        }

        /// <summary>Explosão veicular próxima — decaimento quadrático.</summary>
        public void OnExplosion(Vector3 pos, float radius)
        {
            float d = Vector3.Distance(transform.position, pos);
            float fall = VehicleDamageController.ExplosionFalloff(d, radius);
            if (fall > 0f) TakeDamage(_explosionMaxDamage * fall);
        }

        public void TakeDamage(float dmg)
        {
            if (IsDead || dmg <= 0f) return;
            _health = Mathf.Max(0f, _health - dmg);
            _lastDamageTime = Time.time;
            OnHealthChanged?.Invoke(_health, _maxHealth);
            if (_health <= 0f) Die();
        }

        private void Die()
        {
            Debug.Log("[PlayerHealth] jogador morreu — respawn no carro");
            // Respawn suave: volta ao carro com vida cheia
            var car = FindAnyObjectByType<PlayerCarController>();
            if (car != null)
            {
                transform.position = car.transform.position
                    - car.transform.right * 1.6f + Vector3.up * 0.5f;
                var cc = GetComponent<CharacterController>();
                if (cc != null && cc.enabled) { cc.enabled = false; }
            }
            _health = _maxHealth;
            OnHealthChanged?.Invoke(_health, _maxHealth);
        }
    }
}
