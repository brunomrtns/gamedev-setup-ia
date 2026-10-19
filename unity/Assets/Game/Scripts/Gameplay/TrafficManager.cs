using System.Collections.Generic;
using Studio.Foundation;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Gerencia o ciclo de vida do tráfego civil: mantém ~N veículos ativos,
    /// spawna em arestas dentro de um anel ao redor do player e despawn
    /// além do raio máximo (mobile-first, deterministic RNG semeado).
    /// </summary>
    public class TrafficManager : MonoBehaviour
    {
        /// <summary>Cliente de rede: tráfego simulado no servidor —
        /// este sistema se desativa; réplicas chegam via Studio.Net.</summary>
        public static bool NetworkClientMode;
        /// <summary>Hooks p/ net code (Spawn/Despawn via NetworkObject).</summary>
        public static System.Action<GameObject> OnTrafficSpawned;
        public static System.Action<GameObject> OnTrafficDespawned;

        [Header("References")]
        [SerializeField] private TrafficNetwork _network;
        [SerializeField] private Transform _player;
        [SerializeField] private GameObject[] _vehiclePrefabs;

        [Header("Population")]
        [SerializeField] private int _targetVehicles = 14;
        [SerializeField] private float _spawnMinRadius = 28f;
        [SerializeField] private float _spawnMaxRadius = 70f;
        [SerializeField] private float _despawnRadius = 80f;
        [SerializeField] private float _spawnInterval = 0.4f;
        [SerializeField] private float _minVehicleGap = 6f;

        [Header("Cruise")]
        [SerializeField] private float _cruiseMin = 7f;
        [SerializeField] private float _cruiseMax = 10f;

        private readonly List<TrafficVehicle> _active = new List<TrafficVehicle>();
        private System.Random _rng = new System.Random(20261016);
        private float _spawnTimer;

        public int ActiveCount => _active.Count;
        public Transform Player => _player;
        public GameObject[] VehiclePrefabs => _vehiclePrefabs;

        private void Update()
        {
            if (NetworkClientMode) { enabled = false; return; } // IA só no servidor
            if (_network == null || _player == null || _vehiclePrefabs == null || _vehiclePrefabs.Length == 0)
                return;

            Vector3 pp = _player.position;

            // Despawn por raio máximo (iteração reversa para remoção segura)
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var v = _active[i];
                // Carros fora de rota (empurrados/acidentados) NUNCA são
                // reciclados — persistem como obstáculo (padrão GTA)
                if (v != null && v.State != TrafficVehicle.CrashState.Routing) continue;
                if (v == null || v.DistanceTo(pp) > _despawnRadius)
                {
                    if (v != null) { OnTrafficDespawned?.Invoke(v.gameObject); Destroy(v.gameObject); }
                    _active.RemoveAt(i);
                }
            }

            _spawnTimer -= Time.deltaTime;
            if (_active.Count < _targetVehicles && _spawnTimer <= 0f)
            {
                TrySpawnVehicle(pp);
                _spawnTimer = _spawnInterval;
            }
        }

        /// <summary>Chamado por TrafficVehicle quando a aresta não tem saída.</summary>
        public void NotifyDeadEnd(TrafficVehicle v)
        {
            // Fora de rota: não remove — vira obstáculo estático
            if (v != null && v.State != TrafficVehicle.CrashState.Routing) return;
            _active.Remove(v);
            if (v != null) Destroy(v.gameObject);
        }

        /// <summary>Remove todos os veículos (uso em testes/captura).</summary>
        public void ClearTraffic()
        {
            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i] != null) Destroy(_active[i].gameObject);
            }
            _active.Clear();
        }

        /// <summary>Verifica se a aresta passa pelo anel de spawn ao redor de pos.</summary>
        public bool EdgeInSpawnRing(int edgeIndex, Vector3 center)
        {
            var e = _network.GetEdge(edgeIndex);
            var mid = e.PointAtDistance(e.Length * 0.5f);
            return DriveMath.WithinRing(mid, center, _spawnMinRadius, _spawnMaxRadius);
        }

        private void TrySpawnVehicle(Vector3 playerPos)
        {
            int attempts = 10;
            while (attempts-- > 0)
            {
                int edgeIndex = _rng.Next(_network.EdgeCount);
                var edge = _network.GetEdge(edgeIndex);
                if (edge.Length < 8f) continue;

                var mid = edge.PointAtDistance(edge.Length * 0.5f);
                if (!DriveMath.WithinRing(mid, playerPos, _spawnMinRadius, _spawnMaxRadius)) continue;

                float dist = (float)_rng.NextDouble() * (edge.Length - 4f) + 2f;
                var pos = edge.PointAtDistance(dist);

                // Evita spawn colado em outro veículo ou no player
                bool blocked = Vector3.Distance(pos, playerPos) < _minVehicleGap + 2f;
                if (!blocked)
                {
                    for (int i = 0; i < _active.Count; i++)
                    {
                        if (_active[i] != null && Vector3.Distance(_active[i].transform.position, pos) < _minVehicleGap)
                        {
                            blocked = true;
                            break;
                        }
                    }
                }
                if (blocked) continue;

                var prefab = _vehiclePrefabs[_rng.Next(_vehiclePrefabs.Length)];
                var go = Instantiate(prefab, pos, Quaternion.identity);
                go.name = $"Traffic_{prefab.name}_{_active.Count}";
                OnTrafficSpawned?.Invoke(go); // net hook (no-op fora de rede)

                var rb = go.GetComponent<Rigidbody>();
                if (rb == null) rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true; // vira dinâmico sob impacto (TrafficVehicle)
                rb.mass = 950f;
                rb.centerOfMass = new Vector3(0f, -0.3f, 0f);
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
                var vdc = go.AddComponent<VehicleDamageController>();
                vdc.ConfigureHealth(140f); // tráfego: 140 HP (player: 350)

                // Colisores compostos (chassi+cabine): silhueta real pra tiros/colisão
                foreach (var old in go.GetComponentsInChildren<Collider>(true))
                    Object.Destroy(old);
                var renderers = go.GetComponentsInChildren<Renderer>();
                if (renderers.Length > 0)
                {
                    var wb = renderers[0].bounds;
                    for (int r = 1; r < renderers.Length; r++) wb.Encapsulate(renderers[r].bounds);
                    var ctr = go.transform.InverseTransformPoint(wb.center);
                    var sz = go.transform.InverseTransformVector(wb.size);
                    sz = new Vector3(Mathf.Abs(sz.x), Mathf.Abs(sz.y), Mathf.Abs(sz.z));
                    bool zLong = sz.z >= sz.x;
                    float L = zLong ? sz.z : sz.x, W = zLong ? sz.x : sz.z, H = sz.y;
                    float baseY = ctr.y - H * 0.5f + H * 0.06f;
                    void Zone(float lenFrac, float lenCenter, float widFrac, float botFrac, float hFrac, string n)
                    {
                        var bc = new GameObject(n).AddComponent<BoxCollider>();
                        bc.transform.SetParent(go.transform, false);
                        float sl = L * lenFrac, sw = W * widFrac, sh = H * hFrac;
                        float off = (lenCenter - 0.5f) * L;
                        bc.size = zLong ? new Vector3(sw, sh, sl) : new Vector3(sl, sh, sw);
                        bc.center = zLong ? new Vector3(ctr.x, baseY + H * botFrac + sh * 0.5f, ctr.z + off)
                                          : new Vector3(ctr.x + off, baseY + H * botFrac + sh * 0.5f, ctr.z);
                    }
                    Zone(1.0f, 0.5f, 0.82f, 0.0f, 0.45f, "Col_Chassis");
                    Zone(0.42f, 0.52f, 0.68f, 0.42f, 0.52f, "Col_Cabin");
                    Zone(0.28f, 0.86f, 0.78f, 0.0f, 0.52f, "Col_Hood");
                    Zone(0.16f, 0.06f, 0.60f, 0.0f, 0.40f, "Col_Tank");
                }

                var cruise = Mathf.Lerp(_cruiseMin, _cruiseMax, (float)_rng.NextDouble());
                var vehicle = go.AddComponent<TrafficVehicle>();
                vehicle.Initialize(_network, this, _rng, edgeIndex, dist, cruise);
                _active.Add(vehicle);
                return;
            }
        }
    }
}
