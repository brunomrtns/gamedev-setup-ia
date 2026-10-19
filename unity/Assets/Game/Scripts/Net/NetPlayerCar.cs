using System.Collections.Generic;
using Studio.Gameplay;
using Studio.Input;
using Unity.Netcode;
using UnityEngine;

namespace Studio.Net
{
    /// <summary>
    /// Carro de jogador em rede. Autoridade no SERVIDOR com predição no dono:
    /// - Dono: simula localmente (PlayerCarController ligado, rb dinâmico) e
    ///   envia inputs sequenciados por RPC unreliable a ~30Hz.
    /// - Servidor: aplica inputs (campos Net*/UseNetInput), simula física real,
    ///   e difunde estado autoritativo (pos/rot/fwd/lastSeq) a ~20Hz.
    /// - Dono reconcilia: erro pequeno → blend suave; erro grande → snap +
    ///   desconta inputs já confirmados (PhysX não é determinístico entre
    ///   máquinas — replay exato não é garantido; correção controlada).
    /// - Clientes remotos: buffer de snapshots + interpolação 100ms no passado.
    /// Segurança: RPC exige ownership, inputs são clampados, rate-limit por
    /// conexão, e o servidor nunca aceita posição do cliente.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetPlayerCar : NetworkBehaviour
    {
        [Header("Rede")]
        [SerializeField] private float _inputRate = 30f;
        [SerializeField] private float _stateRate = 20f;
        [SerializeField] private float _interpDelay = 0.10f;
        [SerializeField] private float _snapErrorMeters = 3.0f;
        [SerializeField] private float _blendPerSecond = 4.0f;

        private struct InputCmd { public uint Seq; public float S, T, B; public bool D; }
        private struct Snap { public Vector3 P; public Quaternion R; public float F; public float T; }

        private PlayerCarController _car;
        private Rigidbody _rb;

        // servidor
        private uint _lastSeq;
        private float _nextBroadcast;
        private float _nextInputOk;       // rate limit
        private int _rateViolations;
        private float _serverLogAt;

        // dono (predição)
        private readonly List<InputCmd> _pending = new();
        private uint _nextSeq;
        private Vector3 _blendErr;        // erro residual p/ correção suave
        private float _nextSend;
        private bool _autoExited;

        // remotos (interpolação)
        private readonly List<Snap> _snaps = new();

        // ocupação do veículo (infra p/ enter/exit multiplayer)
        public readonly NetworkVariable<ulong> OccupantClientId =
            new(ulong.MaxValue, NetworkVariableReadPermission.Everyone,
                NetworkVariableWritePermission.Server);

        private void Awake()
        {
            _car = GetComponent<PlayerCarController>();
            _rb = GetComponent<Rigidbody>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                if (_car != null) _car.UseNetInput = true;
                OccupantClientId.Value = OwnerClientId;
                // separa spawn por cliente (mesmo ponto = interpenetração)
                var spawn = transform.position
                    + Vector3.right * (OwnerClientId % 4) * 6f
                    + Vector3.forward * (OwnerClientId / 4) * 6f;
                if (_car != null) _car.ResetToPose(spawn, Quaternion.identity);
            }
            else if (!IsOwner)
            {
                // remoto: interpolação de snapshots, rb cinemático
                if (_car != null) _car.enabled = false;
                if (_rb != null) _rb.isKinematic = true;
            }
            else
            {
                // DONO: predição — controlador ativo, rb dinâmico simulando
                if (_car != null) { _car.enabled = true; _car.UseNetInput = false; }
                if (!NdNetSession.IsDedicatedServer) BindLocalCamera();
            }
            Debug.Log($"[NDNET] car spawn owner={OwnerClientId} " +
                $"srv={IsServer} own={IsOwner}");
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();
            _snaps.Clear(); _pending.Clear();
        }

        private void BindLocalCamera()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var chase = cam.GetComponent<Studio.Cameras.ChaseCamera>();
            if (chase != null) chase.SetTarget(transform);
        }

        // ---------------- servidor ----------------

        /// <summary>Intenção do dono → autoridade. Seq monotônica, clamps, rate-limit.</summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, RequireOwnership = true)]
        private void SubmitInputServerRpc(uint seq, float steer,
            float throttle, float brake, bool drift)
        {
            // rate limit: ~60Hz máximo; spam descartado
            if (Time.time < _nextInputOk)
            {
                if (++_rateViolations > 120)
                    Debug.LogWarning($"[NDNET-SRV] rate spam owner={OwnerClientId}");
                return;
            }
            _nextInputOk = Time.time + 1f / 60f;
            if (seq <= _lastSeq && _lastSeq - seq > 512u) return; // antigo demais
            if (!float.IsFinite(steer) || !float.IsFinite(throttle) ||
                !float.IsFinite(brake)) return;
            _lastSeq = seq;
            if (_car == null) return;
            _car.NetSteer = Mathf.Clamp(steer, -1f, 1f);
            _car.NetThrottle = Mathf.Clamp01(throttle);
            _car.NetBrake = Mathf.Clamp01(brake);
            _car.NetDrift = drift;
        }

        // ---------------- dono (predição + reconciliação) ----------------

        private void FixedUpdate()
        {
            if (IsServer)
            {
                // broadcast de estado autoritativo a ~20Hz + heartbeat log
                if (NetworkManager != null && Time.time > _nextBroadcast)
                {
                    _nextBroadcast = Time.time + 1f / _stateRate;
                    StateClientRpc(transform.position, transform.rotation,
                        _car != null ? _car.ForwardSpeed : 0f, _lastSeq);
                }
                if (Time.time > _serverLogAt)
                {
                    _serverLogAt = Time.time + 5f;
                    Debug.Log($"[NDNET-SRV] car owner={OwnerClientId} " +
                        $"pos={transform.position:F1} " +
                        $"fwd={(_car != null ? _car.ForwardSpeed : 0f):F1}m/s " +
                        $"seq={_lastSeq}");
                }
                return;
            }

            if (IsOwner)
            {
                if (Time.time >= _nextSend)
                {
                    _nextSend = Time.time + 1f / _inputRate;
                    float s, t, b; bool d;
                    if (NdNetSession.AutoDrive)
                    { s = Mathf.Sin(Time.time * 0.35f); t = 0.7f; b = 0f; d = false; }
                    else
                    {
                        var inp = DriveInputAdapter.Instance;
                        s = inp != null ? inp.Steer : 0f;
                        t = inp != null ? inp.Throttle : 0f;
                        b = inp != null ? inp.Brake : 0f;
                        d = inp != null && inp.DriftHeld;
                    }
                    var cmd = new InputCmd { Seq = _nextSeq++, S = s, T = t, B = b, D = d };
                    _pending.Add(cmd);
                    SubmitInputServerRpc(cmd.Seq, s, t, b, d);
                    // sair do carro: E quando ocupa o assento
                    var inp2 = DriveInputAdapter.Instance;
                    if (OccupantClientId.Value == OwnerClientId &&
                        inp2 != null && inp2.ConsumeEnterExit())
                        RequestExitServerRpc();
                    // autoteste C: sai do carro após ~8s dirigindo
                    if (NdNetSession.AutoDrive &&
                        OccupantClientId.Value == OwnerClientId &&
                        Time.timeSinceLevelLoad > 8f && !_autoExited)
                    {
                        _autoExited = true;
                        RequestExitServerRpc();
                    }
                }
                // reconciliação suave: consome erro residual sem snap visual
                if (_blendErr.sqrMagnitude > 0.0001f && _rb != null)
                {
                    var step = _blendErr * Mathf.Min(1f, _blendPerSecond * Time.fixedDeltaTime);
                    _rb.MovePosition(_rb.position + step);
                    _blendErr -= step;
                }
            }
            else
            {
                InterpolateRemote();
            }
        }

        /// <summary>Estado autoritativo do servidor → todos os clientes.</summary>
        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void StateClientRpc(Vector3 pos, Quaternion rot,
            float fwdSpeed, uint ackSeq)
        {
            if (IsOwner)
            {
                // reconciliação: descarta inputs confirmados, mede erro
                while (_pending.Count > 0 && _pending[0].Seq <= ackSeq)
                    _pending.RemoveAt(0);
                var err = pos - transform.position;
                err.y = 0f;
                float mag = err.magnitude;
                if (mag > _snapErrorMeters)
                {
                    // divergência real (colisão/teleporte legítimo): snap
                    if (_rb != null) { _rb.position = pos; _rb.rotation = rot; }
                    transform.SetPositionAndRotation(pos, rot);
                    if (_car != null) _car.ResetToPose(pos, rot);
                    _blendErr = Vector3.zero;
                }
                else if (mag > 0.15f)
                    _blendErr += err * 0.5f; // correção parcial → suave
            }
            else
            {
                _snaps.Add(new Snap { P = pos, R = rot, F = fwdSpeed, T = Time.time });
                if (_snaps.Count > 20) _snaps.RemoveAt(0);
            }
        }

        /// <summary>Interpolação 100ms no passado entre snapshots do servidor.</summary>
        private void InterpolateRemote()
        {
            if (_snaps.Count == 0) return;
            float rt = Time.time - _interpDelay;
            if (_snaps.Count == 1 || rt >= _snaps[^1].T)
            {
                var last = _snaps[^1];
                transform.SetPositionAndRotation(last.P, last.R);
                return;
            }
            for (int i = _snaps.Count - 1; i > 0; i--)
            {
                if (_snaps[i - 1].T <= rt)
                {
                    var a = _snaps[i - 1]; var b = _snaps[i];
                    float f = Mathf.InverseLerp(a.T, b.T, rt);
                    transform.SetPositionAndRotation(
                        Vector3.LerpUnclamped(a.P, b.P, f),
                        Quaternion.Slerp(a.R, b.R, f));
                    return;
                }
            }
            var first = _snaps[0];
            transform.SetPositionAndRotation(first.P, first.R);
        }

        // ---------------- enter/exit (infra, Etapa B) ----------------

        /// <summary>Pedido de saída → servidor valida, libera assento,
        /// teleporta a entidade a pé do jogador pro lado do carro.</summary>
        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void RequestExitServerRpc(RpcParams p = default)
        {
            ulong cid = p.Receive.SenderClientId;
            if (OccupantClientId.Value != cid) return; // não é o ocupante
            OccupantClientId.Value = ulong.MaxValue;   // assento livre
            // para o carro
            if (_car != null) { _car.NetThrottle = 0f; _car.NetSteer = 0f; }
            Vector3 exitPos = transform.position - transform.right * 1.6f
                + Vector3.up * 0.4f;
            if (NdNetSession.Feet.TryGetValue(cid, out var foot) && foot != null)
                foot.SetOnFootServer(true, exitPos, transform.eulerAngles.y);
            Debug.Log($"[NDNET-SRV] exit car {NetworkObjectId} by {cid}");
        }


    }
}
