using System.Collections;
using UnityEngine;
using Studio.Foundation;

namespace Studio.Gameplay
{
    /// <summary>
    /// Dano progressivo veicular (padrão physics-and-combat):
    /// zonas Col_Hood(1.5x)/Col_Tank(2.0x)/demais(1.0x); estágios:
    /// >50% faíscas (VFX pooled CFX metal), 1-35% fogo no capô + auto-ignição
    /// 5-8s, 0% explosão radial + carcaça burned + fogos residuais 12s.
    /// Tiros isolados nunca explodem (~9 tiros pra destruir).
    /// </summary>
    public class VehicleDamageController : MonoBehaviour, IShootable
    {
        public enum DamageState { Intact, Light, Severe, Burning, Exploded }

        [SerializeField] private float _maxHealth = 140f; // tráfego; player = 350 (builder)
        [SerializeField] private float _bulletDamage = 12f;
        [SerializeField] private float _collisionDamageScale = 500f;
        [SerializeField] private float _collisionCooldown = 0.35f; // entre contatos
        [SerializeField] private float _burnMin = 5f, _burnMax = 8f;
        [SerializeField] private float _explosionForce = 5500f;
        [SerializeField] private float _explosionRadius = 9f;
        [SerializeField] private GameObject _vfxImpactMetal, _vfxFire;

        private bool _permanentlyDisabled;
        /// <summary>Carro explodido nunca mais pode ser dirigido.</summary>
        public bool PermanentlyDisabled => _permanentlyDisabled;
        private static readonly Collider[] _shock = new Collider[32];
        private float _health;
        private float _nextCollisionDamageTime;

        /// <summary>Define HP máximo pós-AddComponent (builder/spawner).</summary>
        public void ConfigureHealth(float maxHealth)
        {
            _maxHealth = maxHealth;
            _health = maxHealth;
        }

        /// <summary>QA: impactos ainda tocam VFX/SFX, saúde nunca &lt; 50%.</summary>
        public bool IsGodMode { get; set; }
        /// <summary>Alias de compatibilidade com a spec original de QA.</summary>
        public bool IsGodModeActive { get => IsGodMode; set => IsGodMode = value; }
        private DamageState _state = DamageState.Intact;
        private Transform _severe, _burned;
        private GameObject _fire;
        private Coroutine _burnRoutine;

        public DamageState State => _state;
        public float Health => _health;

        /// <summary>Multiplicador por zona de colisão — puro/testável.</summary>
        public static float ZoneMultiplier(string colliderName)
        {
            if (colliderName != null)
            {
                if (colliderName.Contains("Hood")) return 1.5f;
                if (colliderName.Contains("Tank")) return 2.0f;
            }
            return 1.0f;
        }

        /// <summary>Decaimento quadrático da explosão — puro/testável.</summary>
        public static float ExplosionFalloff(float distance, float radius)
        {
            if (distance >= radius) return 0f;
            float t = 1f - Mathf.Clamp01(distance / radius);
            return t * t;
        }

        /// <summary>Estado por faixa de vida — puro/testável.</summary>
        public static DamageState StateForHealth(float h, float max)
        {
            float r = max > 0f ? h / max : 0f;
            if (r <= 0f) return DamageState.Exploded;
            if (r <= 0.35f) return DamageState.Burning;
            if (r <= 0.50f) return DamageState.Severe;
            return r <= 0.99f ? DamageState.Light : DamageState.Intact;
        }

        public void SetVfxPrefabs(GameObject impact, GameObject fire)
        { _vfxImpactMetal = impact; _vfxFire = fire; }

        private void Awake()
        {
            _health = _maxHealth;
            _severe = transform.Find("__wrecked_severe");
            _burned = transform.Find("__wrecked_burned");
            var lib = VfxLibrary.Instance;
            if (lib != null && _vfxImpactMetal == null)
                SetVfxPrefabs(lib.impactMetal, lib.fireSmall);
        }

        public void OnShot(RaycastHit hit)
        {
            float dmg = _bulletDamage * ZoneMultiplier(hit.collider.name);
            VfxPool.Spawn("impact_metal", hit.point,
                Quaternion.LookRotation(hit.normal));
            TakeDamage(dmg);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (_state >= DamageState.Burning) return;
            // Cooldown de contato: múltiplos colliders do mesmo impacto não
            // somam dano instantâneo (0.35s entre registros).
            if (Time.time < _nextCollisionDamageTime) return;
            _nextCollisionDamageTime = Time.time + _collisionCooldown;
            TakeDamage(collision.impulse.magnitude / _collisionDamageScale);
        }

        public void TakeDamage(float dmg)
        {
            if (_state == DamageState.Exploded || dmg <= 0f) return;
            _health = Mathf.Max(IsGodMode ? _maxHealth * 0.5f : 0f,
                _health - dmg);
            var next = StateForHealth(_health, _maxHealth);
            if (next != _state) Transition(next);
        }

        private void Transition(DamageState next)
        {
            _state = next;
            switch (next)
            {
                case DamageState.Severe:
                    // NÃO troca de malha aqui — amassado sutil não vale swap
                    // (variante FBX embutida renderizava translúcida). O carro
                    // só muda de malha quando realmente explode.
                    Foundation.ProceduralAudioService.Instance?.PlayImpactMetal();
                    break;
                case DamageState.Burning:
                    Ignite();
                    _burnRoutine = StartCoroutine(BurnThenExplode());
                    break;
                case DamageState.Exploded:
                    Explode();
                    break;
            }
        }

        /// <summary>
        /// Carboniza o modelo INTACTO em vez de trocar de malha — a variante
        /// FBX podia renderizar invisível (materiais embutidos sem URP). O carro
        /// nunca some: só escurece e perde brilho (explodido/queimado).
        /// </summary>
        private void SwapVisual(Transform variant)
        {
            bool burned = variant == _burned;
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r.transform.name.StartsWith("Col_") || r.transform.name.StartsWith("__"))
                    continue;
                var mats = r.materials; // instancia (runtime) — não toca o asset
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (burned)
                    {
                        // Carbonizado COM textura: tint escuro sobre a malha
                        // (preto absoluto = borrão sem textura)
                        m.SetColor("_BaseColor", new Color(0.22f, 0.19f, 0.17f, 1f));
                        if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.12f);
                        m.SetFloat("_Surface", 0f);
                    }
                    else
                    {
                        m.SetColor("_BaseColor", m.GetColor("_BaseColor") * 0.55f);
                    }
                }
            }
        }

        /// <summary>Fogo no capô: prefab CFX ou partícula procedural de fallback.</summary>
        private void Ignite()
        {
            // Lazy: Awake do controller pode rodar antes do VfxLibrary
            if (_vfxFire == null && VfxLibrary.Instance != null)
                SetVfxPrefabs(VfxLibrary.Instance.impactMetal, VfxLibrary.Instance.fireSmall);
            var hoodPos = transform.position + transform.forward * 1.1f + Vector3.up * 0.9f;
            // Estritamente prefab War FX validado — emissores procedurais por
            // script geravam quadrados pretos (shader incompleto).
            if (_vfxFire == null) return;
            _fire = VfxPool.Spawn("fire_small", hoodPos, Quaternion.identity, 12f);
            if (_fire != null) _fire.transform.SetParent(transform, true);
        }

        private IEnumerator BurnThenExplode()
        {
            yield return new WaitForSeconds(Random.Range(_burnMin, _burnMax));
            Explode();
        }

        /// <summary>
        /// Detonação: OverlapSphereNonAlloc + AddExplosionForce + dano
        /// quadrático em outros veículos + shake de câmera + fogos residuais.
        /// </summary>
        private void Explode()
        {
            _state = DamageState.Exploded;
            if (_burnRoutine != null) StopCoroutine(_burnRoutine);
            if (_fire != null) { VfxPool.Release("fire_small", _fire); _fire = null; }
            SwapVisual(_burned != null ? _burned : _severe);

            Vector3 pos = transform.position;
            // explosão alerta pedestres num raio amplo
            PedestrianSystem.Instance?.ScareAt(pos, 30f);
            BirdSystem.Instance?.ScatterAt(pos, 35f);
            VfxPool.Spawn("explosion", pos + Vector3.up * 0.8f, Quaternion.identity);
            int n = Physics.OverlapSphereNonAlloc(pos, _explosionRadius, _shock, ~0, QueryTriggerInteraction.Ignore);
            var rb = GetComponent<Rigidbody>();
            for (int i = 0; i < n; i++)
            {
                var r = _shock[i].GetComponentInParent<Rigidbody>();
                if (r != null && r != rb && !r.isKinematic)
                    r.AddExplosionForce(_explosionForce, pos, _explosionRadius, 1.2f);
                // Dano quadrático em outros carros
                var other = _shock[i].GetComponentInParent<VehicleDamageController>();
                if (other != null && other != this)
                    other.TakeDamage(40f * ExplosionFalloff(
                        Vector3.Distance(pos, other.transform.position), _explosionRadius));
            }

            // Dano ao jogador por proximidade da explosão veicular
            PlayerHealth.Instance?.OnExplosion(pos, _explosionRadius);

            // Shake: máximo perto, zera a 30m (bus desacoplado)
            Transform camT = Camera.main != null ? Camera.main.transform : null;
            if (camT != null)
            {
                float intensity = Mathf.Clamp01(1f - Vector3.Distance(pos, camT.position) / 30f);
                if (intensity > 0.01f) CameraShakeBus.Impulse(intensity);
            }

            // Fogos residuais: vivem 60s ou até o player sair do campo
            // (pool recicla por distância — nunca Destroy em objeto pooled)
            for (int i = 0; i < 4; i++)
            {
                var off = new Vector3(Random.Range(-2f, 2f), 0.15f, Random.Range(-2f, 2f));
                VfxPool.Spawn("fire_small", pos + off, Quaternion.identity, 60f);
            }
            Foundation.ProceduralAudioService.Instance?.PlayExplosion();

            // Carcaça: drag alto para rápido + disable permanente (nunca
            // volta a andar se o player tentar reentrar)
            var ownRb = GetComponent<Rigidbody>();
            if (ownRb != null) { ownRb.linearDamping = 2.5f; ownRb.angularDamping = 3f; }
            _permanentlyDisabled = true;
            var player2 = GetComponent<PlayerCarController>();
            if (player2 != null) player2.enabled = false;
            Foundation.ProceduralAudioService.Instance?.SetEngineOff();
        }
    }
}
