using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Veículo de tráfego ambiente: segue a polilinha da aresta corrente por
    /// arc-length (Rigidbody cinemático), freia ao detectar obstáculo frontal
    /// e troca de aresta nos cruzamentos.
    ///
    /// Máquina de estados de colisão (padrão physics-and-combat.md):
    ///   Routing     — rota normal, rb cinemático (carro é parede para o player)
    ///   PhysicsFree — impacto leve/médio: rb vira dinâmico, absorve o impulso,
    ///                 assenta e re-roteia para a aresta mais próxima
    ///   Wrecked     — impacto severo: sai do tráfego, fica acidentado na via
    /// </summary>
    public class TrafficVehicle : MonoBehaviour, IShootable
    {
        public enum ImpactClass { None, Soft, Severe }
        public enum CrashState { Routing, PhysicsFree, Wrecked }

        [SerializeField] private float _accel = 5f;
        [SerializeField] private float _brakeDecel = 11f;
        [SerializeField] private float _rayDistance = 8f;
        [SerializeField] private float _rayHeight = 0.55f;
        [SerializeField] private float _rayForwardOffset = 1.9f;
        [SerializeField] private float _turnLerpSpeed = 8f;

        [Header("Impacto")]
        // massa do player ~1350kg: 4k N·s ≈ toque a 3m/s; 20k ≈ pancada a 15m/s
        [SerializeField] private float _softImpulse = 4000f;
        [SerializeField] private float _severeImpulse = 20000f;
        [SerializeField] private float _settleSpeed = 0.8f;
        [SerializeField] private float _settleTime = 1.2f;
        [SerializeField] private float _rerouteMaxDist = 25f;

        private static readonly RaycastHit[] _hits = new RaycastHit[6];

        private TrafficNetwork _network;
        private TrafficManager _manager;
        private System.Random _rng;
        private Rigidbody _rb;
        private int _edge;
        private float _dist;
        private float _speed;
        private float _cruiseSpeed;
        private float _settleTimer;
        private bool _shotBrake; // buzinou e travou após levar tiro

        public int CurrentEdge => _edge;
        public float Speed => _speed;
        public CrashState State { get; private set; }

        /// <summary>Classificação pura do impacto — testável em EditMode.</summary>
        public static ImpactClass ClassifyImpulse(float impulse, float softThresh, float severeThresh)
        {
            if (impulse >= severeThresh) return ImpactClass.Severe;
            if (impulse >= softThresh) return ImpactClass.Soft;
            return ImpactClass.None;
        }

        /// <summary>Mundo reativo: ao levar tiro, buzina e freia definitivamente.</summary>
        public void OnShot(RaycastHit hit)
        {
            if (!_shotBrake)
            {
                _shotBrake = true;
                Foundation.ProceduralAudioService.Instance?.PlayHornReal();
            }
        }

        public void Initialize(TrafficNetwork network, TrafficManager manager, System.Random rng, int startEdge, float startDist, float cruiseSpeed)
        {
            _network = network;
            _manager = manager;
            _rng = rng;
            _edge = startEdge;
            _dist = startDist;
            _cruiseSpeed = cruiseSpeed;
            _speed = cruiseSpeed;
            _rb = GetComponent<Rigidbody>();

            var e = _network.GetEdge(_edge);
            transform.position = e.PointAtDistance(_dist);
            transform.rotation = Quaternion.LookRotation(e.DirectionAtDistance(_dist));
        }

        private void Update()
        {
            if (State == CrashState.Wrecked || _network == null || _edge < 0) return;
            if (State == CrashState.PhysicsFree) { UpdatePhysicsFree(); return; }

            float dt = Time.deltaTime;
            var edge = _network.GetEdge(_edge);

            Vector3 pos = transform.position;
            Vector3 dir = edge.DirectionAtDistance(_dist);

            // Obstáculo frontal: raycast a partir do para-choque dianteiro
            float targetSpeed = _shotBrake ? 0f : _cruiseSpeed;
            var origin = pos + dir * _rayForwardOffset + Vector3.up * _rayHeight;
            int hitCount = Physics.RaycastNonAlloc(origin, dir, _hits, _rayDistance, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.MaxValue;
            for (int i = 0; i < hitCount; i++)
            {
                var h = _hits[i];
                if (h.transform == transform || h.transform.IsChildOf(transform)) continue;
                if (h.distance < nearest) nearest = h.distance;
            }
            if (nearest < _rayDistance)
            {
                // Escala a velocidade alvo com a distância livre (para ~2m antes)
                targetSpeed = _cruiseSpeed * Mathf.Clamp01((nearest - 1.6f) / (_rayDistance - 1.6f));
            }

            float rate = targetSpeed > _speed ? _accel : _brakeDecel;
            _speed = Mathf.MoveTowards(_speed, targetSpeed, rate * dt);

            _dist += _speed * dt;
            float edgeLen = edge.Length;
            int guard = 0;
            while (_dist >= edgeLen && guard++ < 8)
            {
                _dist -= edgeLen;
                // Domain reload em play (recompile) zera campos não
                // serializáveis: _network/_edge sobrevivem, _rng vira null.
                _rng ??= new System.Random(GetInstanceID());
                int next = _network.PickNext(_edge, _rng);
                if (next < 0)
                {
                    _manager?.NotifyDeadEnd(this);
                    return;
                }
                _edge = next;
                edge = _network.GetEdge(_edge);
                edgeLen = edge.Length;
            }

            Vector3 targetPos = edge.PointAtDistance(_dist);
            Vector3 targetDir = edge.DirectionAtDistance(_dist);
            if (_rb != null)
            {
                _rb.MovePosition(targetPos);
                _rb.MoveRotation(Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(targetDir), _turnLerpSpeed * dt));
            }
            else
            {
                transform.position = targetPos;
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(targetDir), _turnLerpSpeed * dt);
            }
        }

        private float _lastThudAt = -99f;

        private void OnCollisionEnter(Collision collision)
        {
            if (State == CrashState.Wrecked) return;
            float impulse = collision.impulse.magnitude;
            var cls = ClassifyImpulse(impulse, _softImpulse, _severeImpulse);
            if (cls == ImpactClass.None) return;

            // Áudio 3D filtrado: só impactos relevantes, cooldown por veículo,
            // sem som de raspinha constante da IA
            float relV = collision.relativeVelocity.magnitude;
            if (relV > 4f && Time.time - _lastThudAt > 0.5f)
            {
                _lastThudAt = Time.time;
                Foundation.ProceduralAudioService.Instance?.PlayCollisionThud3D(
                    transform.position, Mathf.Clamp01(impulse / 8000f));
            }
            if (cls == ImpactClass.Severe) { EnterWrecked(collision); return; }
            EnterPhysicsFree(collision);
        }

        /// <summary>Impacto leve/médio: libera física, absorve o impulso no contato.</summary>
        private void EnterPhysicsFree(Collision collision)
        {
            if (_rb == null) return;
            _rb.isKinematic = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.linearDamping = 0.6f;
            _rb.angularDamping = 2f;
            _rb.mass = 1400f; // mais pesado que o player: FPS não empurra
            if (collision.contactCount > 0)
            {
                var c = collision.GetContact(0);
                // 35% do impulso (o player já absorveu parte) com teto ~15 m/s
                float applied = Mathf.Min(collision.impulse.magnitude * 0.35f, _rb.mass * 15f);
                _rb.AddForceAtPosition(c.normal * applied, c.point, ForceMode.Impulse);
            }
            _speed = 0f;
            _settleTimer = 0f;
            State = CrashState.PhysicsFree;
        }

        /// <summary>Impacto severo: acidentado permanente — fora da rota, físico.</summary>
        private void EnterWrecked(Collision collision)
        {
            if (_rb == null) return;
            _rb.isKinematic = false;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.linearDamping = 1.2f;
            _rb.angularDamping = 2.5f;
            _rb.mass = 3500f; // pesado: FPS/CharacterController não consegue empurrar
            if (collision.contactCount > 0)
            {
                var c = collision.GetContact(0);
                float applied = Mathf.Min(collision.impulse.magnitude * 0.35f, _rb.mass * 12f);
                _rb.AddForceAtPosition(c.normal * applied, c.point, ForceMode.Impulse);
            }
            _speed = 0f;
            State = CrashState.Wrecked;
        }

        /// <summary>Assentou → volta pra aresta mais próxima e retoma a rota.</summary>
        private void UpdatePhysicsFree()
        {
            float dt = Time.deltaTime;
            // Teto de velocidade: o carro solto escorrega, nunca sai voando
            if (_rb.linearVelocity.magnitude > 16f)
                _rb.linearVelocity = _rb.linearVelocity.normalized * 16f;
            bool settled = _rb.linearVelocity.magnitude < _settleSpeed
                        && _rb.angularVelocity.magnitude < 1.2f;
            _settleTimer = settled ? _settleTimer + dt : 0f;
            if (_settleTimer < _settleTime) return;

            if (_network.FindNearestEdge(transform.position, out int edgeIdx, out float d)
                && Vector3.Distance(transform.position, _network.GetEdge(edgeIdx).PointAtDistance(d)) < _rerouteMaxDist)
            {
                _edge = edgeIdx;
                _dist = d;
                var e = _network.GetEdge(_edge);
                var snap = e.PointAtDistance(_dist);
                // Não teleporta pra cima de nada (player/tráfego) — senão a
                // depenetração explode e o contato constante mata o player
                foreach (var col in Physics.OverlapSphere(snap, 2.4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (!col.transform.IsChildOf(transform) && !col.isTrigger)
                    {
                        _settleTimer = 0f; // espera a vaga liberar
                        return;
                    }
                }
                transform.position = snap;
                transform.rotation = Quaternion.LookRotation(e.DirectionAtDistance(_dist));
                _rb.isKinematic = true;
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
                _speed = 0f;
                State = CrashState.Routing;
            }
            else
            {
                // Não achou via perto → fica acidentado (fora do tráfego)
                State = CrashState.Wrecked;
            }
        }

        /// <summary>Distância ao ponto de referência (player) para spawn/despawn.</summary>
        public float DistanceTo(Vector3 p) => Vector3.Distance(transform.position, p);
    }
}
