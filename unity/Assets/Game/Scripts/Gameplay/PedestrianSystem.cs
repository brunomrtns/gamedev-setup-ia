using System.Collections.Generic;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// População de pedestres: NPCs Quaternius (Humanoid Mecanim, esqueleto
    /// limpo — sem rebind nem rotação manual de ossos) andando pelas calçadas,
    /// entram em pânico com tiros/explosões/carro veloz, morrem atropelados
    /// ou baleados. Pool fixo de ~16 ativos.
    /// </summary>
    public class PedestrianSystem : MonoBehaviour
    {
        /// <summary>Cliente de rede: a IA roda no servidor — este sistema
        /// se desativa; entidades chegam como réplicas (Studio.Net).</summary>
        public static bool NetworkClientMode;
        /// <summary>Hook p/ net code: (spawn, despawn) de pedestres.</summary>
        public static System.Action<GameObject> OnPedSpawned;
        public static System.Action<GameObject> OnPedDespawned;
        public static PedestrianSystem Instance { get; private set; }
        private readonly List<Pedestrian> _peds = new();
        [SerializeField] private int _count = 16;
        [SerializeField] private float _panicRadius = 20f;

        private Transform _player;
        private GameObject[] _prefabs;
        private RuntimeAnimatorController _controller;

        private void Awake()
        {
            Instance = this;
            // Pedestres: prefabs Quaternius (quat_*) — esqueleto humanóide
            // limpo que anima nativamente. NÃO usar os npc_csl_dressed_*
            // (esqueleto unificado à mão deforma a animação).
            _prefabs = System.Array.FindAll(
                Resources.LoadAll<GameObject>("Art/Pedestrians"),
                g => g.name.StartsWith("quat_"));
            if (_prefabs.Length == 0)
                _prefabs = System.Array.FindAll(
                    Resources.LoadAll<GameObject>("Art/Pedestrians"),
                    g => g.name.StartsWith("npc_csl_"));
            _controller = Resources.Load<RuntimeAnimatorController>(
                "Art/Pedestrians/Pedestrian");
        }

        public void SetPlayer(Transform p) { _player = p; }

        private void Start()
        {
            // cliente de rede: IA só no servidor; réplicas chegam pela rede
            if (NetworkClientMode) { enabled = false; return; }
            if (_prefabs == null || _prefabs.Length == 0) { enabled = false; return; }
            var rand = new System.Random(7);
            for (int i = 0; i < _count; i++)
            {
                bool alongNS = rand.Next(2) == 0;
                int street = rand.Next(Foundation.CityLayout.LINES);
                int side = rand.Next(2) == 0 ? -1 : 1;
                float t;
                int tries = 0;
                do { t = -120f + (float)rand.NextDouble() * 240f; }
                while (Foundation.CityLayout.InIntersectionBand(t) && ++tries < 8);
                var p = Foundation.CityLayout.SidewalkPoint(alongNS, street, side, t);
                SpawnPedestrian(p + Vector3.up, rand, alongNS);
            }
        }

        private void SpawnPedestrian(Vector3 pos, System.Random rand,
            bool alongNS = true)
        {
            var pf = _prefabs[rand.Next(_prefabs.Length)];
            var go = Instantiate(pf, pos,
                Quaternion.Euler(0, (float)rand.NextDouble() * 360f, 0));
            // aterra por OSSOS: menor Y de qualquer bone do SMR toca o chão
            if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down,
                    out var hit, 10f))
            {
                float minY = float.MaxValue;
                foreach (var smr in
                        go.GetComponentsInChildren<SkinnedMeshRenderer>())
                    foreach (var b in smr.bones)
                        if (b != null && b.position.y < minY)
                            minY = b.position.y;
                if (minY < float.MaxValue)
                    go.transform.position += Vector3.up * (hit.point.y - minY);
            }
            if (_controller != null)
            {
                var anim = go.GetComponentInChildren<Animator>();
                if (anim == null) anim = go.AddComponent<Animator>();
                anim.runtimeAnimatorController = _controller;
                anim.applyRootMotion = false; // morte/agachar não move o root
            }
            var ped = go.AddComponent<Pedestrian>();
            ped.Init(rand, _player, alongNS);
            _peds.Add(ped);
            OnPedSpawned?.Invoke(go); // net hook (no-op fora de rede)
        }

        /// <summary>Disparo/explosão/impacto — dispara pânico na vizinhança.</summary>
        public void ScareAt(Vector3 pos, float radius = 0f)
        {
            float r = radius > 0f ? radius : _panicRadius;
            foreach (var p in _peds)
                if (p != null && Vector3.Distance(p.transform.position, pos) < r)
                    p.FleeFrom(pos);
        }

        /// <summary>Atropelamento — o carro chama com a posição do impacto.</summary>
        public void TryRunOver(Vector3 carPos, Vector3 carVel, float radius = 2.2f)
        {
            foreach (var p in _peds)
                if (p != null && !p.IsDown
                    && Vector3.Distance(p.transform.position, carPos) < radius)
                    p.RunOver(carVel);
        }
    }

    /// <summary>
    /// Pedestre individual (Quaternius Humanoid): caminha entre waypoints na
    /// calçada (Walk_Loop), foge em pânico (Sprint_Loop) e morre (Death01)
    /// atropelado ou baleado. A apresentação é 100% Mecanim — nenhum osso é
    /// rotacionado por script.
    /// </summary>
    public class Pedestrian : MonoBehaviour, IShootable
    {
        /// <summary>Estado p/ NetPedestrian: 0 idle/walk,1 flee,2 dead,3 cower.</summary>
        [HideInInspector] public byte NetState;
        [HideInInspector] public float NetSpeedNorm;
        private Vector3 _target;
        private float _speed = 1.2f;
        private bool _panic;
        private Vector3 _fleeFrom;
        private float _panicEnd;
        private bool _down;
        private Transform _player;
        private Animator _anim;
        private CapsuleCollider _capCol;
        private bool _alongNS = true;

        public bool IsDown => _down;

        public void Init(System.Random rand, Transform player,
            bool alongNS = true)
        {
            _player = player;
            _alongNS = alongNS;
            _anim = GetComponentInChildren<Animator>();
            if (_anim != null)
                _anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _speed = 0.9f + (float)rand.NextDouble() * 0.7f;
            PickWaypoint();
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.height = 1.7f; col.radius = 0.3f;
            col.center = new Vector3(0f, 0.85f, 0f);
            _capCol = col;
            // cinemático vivo: registra o atropelamento sem ser empurrado
            var rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            // cola o pé no solo a cada frame (evita flutuar/afundar)
            InvokeRepeating(nameof(SnapY), 0.05f, 0.1f);
        }

        private void SnapY()
        {
            if (_down) return;
            if (Physics.Raycast(transform.position + Vector3.up * 1.5f,
                    Vector3.down, out var hit, 4f))
            {
                transform.position = new Vector3(transform.position.x,
                    hit.point.y, transform.position.z);
            }
        }

        private void PickWaypoint()
        {
            // anda AO LONGO da calçada (paralelo à via) — a rua só é cruzada
            // quando o waypoint passa pela banda de um cruzamento (faixa).
            var t = transform.position;
            float step = Random.Range(8f, 20f) * (Random.value < 0.5f ? -1f : 1f);
            _target = _alongNS
                ? new Vector3(t.x, t.y, t.z + step)
                : new Vector3(t.x + step, t.y, t.z);
        }

        public void FleeFrom(Vector3 danger)
        {
            if (_down) return;
            _panic = true;
            _fleeFrom = danger;
            _panicEnd = Time.time + 6f;
        }

        /// <summary>IShootable — tiro direto no pedestre.</summary>
        public void OnShot(RaycastHit hit)
        {
            KillByShot(hit.point, -hit.normal);
        }

        /// <summary>
        /// Morte por tiro: transição imediata pra Death01 (cai no solo),
        /// colisor desliga. Partículas de impacto são efêmeras — NUNCA decal
        /// flutuando no ar (o Ak47Weapon pula o decal em hits de pedestre).
        /// </summary>
        public void KillByShot(Vector3 point, Vector3 dir)
        {
            if (_down) return;
            // pequeno arrasto na direção do tiro — vende o impacto
            var flat = new Vector3(dir.x, 0f, dir.z);
            if (flat.sqrMagnitude > 0.001f)
            {
                transform.position += flat.normalized * 0.35f;
                transform.rotation = Quaternion.LookRotation(-flat.normalized);
            }
            Die();
        }

        /// <summary>
        /// Atropelamento = morte: animação Death (cai e fica no chão),
        /// colisor desliga (carro passa por cima), corpo permanece.
        /// </summary>
        public void RunOver(Vector3 carVel)
        {
            if (_down) return;
            // corpo jogado levemente na direção do impacto
            var flat = new Vector3(carVel.x, 0f, carVel.z);
            if (flat.sqrMagnitude > 0.001f)
                transform.position += flat.normalized * 0.5f;
            Die();
        }

        private void Die()
        {
            _down = true; NetState = 2;
            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;
            if (_anim != null)
            {
                _anim.SetTrigger("Dead");
                StartCoroutine(SnapCorpseToGround());
            }
            else
            {
                // fallback sem anim: deita o modelo
                transform.rotation = Quaternion.Euler(-80f,
                    transform.eulerAngles.y, 0f);
                enabled = false;
            }
            Foundation.ProceduralAudioService.Instance?.PlayCollisionThud(0.8f);
        }

        /// <summary>
        /// Pós-Death: encaixa o cadáver no chão pelos bounds reais e congela
        /// a pose deitada quando o clip termina.
        /// </summary>
        private System.Collections.IEnumerator SnapCorpseToGround()
        {
            // Death01 dura ~1.6s — esperar o corpo cair DE VERDADE antes de
            // congelar (antes congelava no meio da queda = torto/flutuando)
            yield return new WaitForSeconds(1.8f);
            // aterra pelo menor OSSO do esqueleto (bounds de renderer ficam
            // defasados após a pose de morte)
            float minY = float.MaxValue;
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>())
                foreach (var b in smr.bones)
                    if (b != null && b.position.y < minY) minY = b.position.y;
            if (minY < float.MaxValue
                && Physics.Raycast(transform.position + Vector3.up * 1.5f,
                        Vector3.down, out var hit, 8f))
                transform.position += Vector3.up * (hit.point.y - minY);
            if (_anim != null) _anim.enabled = false; // congela a pose deitada
            enabled = false;
        }

        private void OnCollisionEnter(Collision c)
        {
            if (_down || c.rigidbody == null || c.rigidbody.mass < 200f) return;
            if (c.relativeVelocity.magnitude > 3f)
                RunOver(c.relativeVelocity);
        }

        private void Update()
        {
            if (_down) return;
            var t = transform;
            if (_panic)
            {
                NetState = 1; NetSpeedNorm = 1f;
                var away = (t.position - _fleeFrom).normalized;
                t.position += away * (_speed * 3.2f) * Time.deltaTime;
                t.rotation = Quaternion.Slerp(t.rotation,
                    Quaternion.LookRotation(away), 8f * Time.deltaTime);
                _anim?.SetFloat("Speed", 2f);
                if (Time.time > _panicEnd) _panic = false;
                return;
            }
            // carro perto: veloz → foge; lento rondando → agacha de medo
            if (_player != null)
            {
                var rb = _player.GetComponent<Rigidbody>();
                float d = Vector3.Distance(t.position, _player.position);
                float spd = rb != null ? rb.linearVelocity.magnitude : 0f;
                if (spd > 9f && d < 14f) FleeFrom(_player.position);
                bool cower = !_panic && spd < 9f && d < 8f;
                _anim?.SetBool("Cowering", cower);
                if (cower)
                {
                    NetState = 3; NetSpeedNorm = 0f;
                    _anim?.SetFloat("Speed", 0f);
                    return; // parado, encolhido
                }
            }
            var dir = _target - t.position; dir.y = 0f;
            if (dir.magnitude < 1f)
            {
                PickWaypoint();
                _anim?.SetFloat("Speed", 0f);
            }
            else
            {
                // não atravessa cruzamento se veículo se aproxima (GTA-style)
                bool waitAtCrossing =
                    Foundation.CityLayout.InIntersectionBand(t.position.x)
                    || Foundation.CityLayout.InIntersectionBand(t.position.z);
                if (waitAtCrossing && _player != null)
                {
                    var rb = _player.GetComponent<Rigidbody>();
                    if (rb != null && rb.linearVelocity.magnitude > 4f
                        && Vector3.Distance(t.position, _player.position) < 18f)
                    {
                        _anim?.SetFloat("Speed", 0f);
                        return; // espera o carro passar
                    }
                }
                // stride sync: velocidade do ciclo casa com o deslocamento
                // (zero foot-sliding) — Walk_Loop stride ~1.35m/s
                t.position += dir.normalized * _speed * Time.deltaTime;
                t.rotation = Quaternion.Slerp(t.rotation,
                    Quaternion.LookRotation(dir.normalized), 6f * Time.deltaTime);
                _anim?.SetFloat("Speed", 1.0f);
                if (NetState != 1) { NetState = 0; NetSpeedNorm = 0.4f; }
                if (_anim != null)
                    _anim.speed = Mathf.Clamp(_speed / 1.35f, 0.5f, 1.8f);
            }
        }
    }
}
