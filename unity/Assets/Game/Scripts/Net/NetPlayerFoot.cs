using System.Collections.Generic;
using Studio.Gameplay;
using Studio.Input;
using Unity.Netcode;
using UnityEngine;

namespace Studio.Net
{
    /// <summary>
    /// Jogador a pé em rede. Mesmo modelo do NetPlayerCar: dono prediz local
    /// (CharacterController é kinematic — bem mais determinístico que PhysX),
    /// servidor é autoridade (UseNetInput no OnFootMotor), remotos interpolam
    /// snapshots 100ms no passado. Visibilidade por IsOnFoot (NetworkVariable):
    /// false = jogador está dentro de um veículo (modelo escondido).
    /// Enter/exit validados no servidor via RequestEnter/Exit em NetPlayerCar.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    [RequireComponent(typeof(CharacterController))]
    public class NetPlayerFoot : NetworkBehaviour
    {
        [SerializeField] private float _inputRate = 30f;
        [SerializeField] private float _stateRate = 20f;
        [SerializeField] private float _interpDelay = 0.10f;
        [SerializeField] private float _enterDistance = 3.0f;

        public readonly NetworkVariable<bool> IsOnFoot = new(false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private struct Snap { public Vector3 P; public float Yaw, Spd, T; }
        private readonly List<Snap> _snaps = new();

        private OnFootMotor _motor;
        private CharacterController _cc;
        private Animator _anim;
        private Renderer[] _rends;
        private uint _nextSeq, _lastSeq;
        private float _nextSend, _nextBroadcast, _logAt, _nextInputOk;
        private Vector3 _blendErr;
        private Vector2 _pendMove; private float _pendYaw;
        private bool _pendSprint, _pendJump, _pendCrouch;

        private void Awake()
        {
            _motor = GetComponent<OnFootMotor>();
            _cc = GetComponent<CharacterController>();
            _anim = GetComponentInChildren<Animator>();
            _rends = GetComponentsInChildren<Renderer>(true);
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            IsOnFoot.OnValueChanged += OnFootChanged;
            ApplyVisibility(IsOnFoot.Value);
            if (IsServer)
            {
                NdNetSession.Feet[OwnerClientId] = this;
                if (_motor != null) { _motor.UseNetInput = true; _motor.Activate(null); }
            }
            else if (IsOwner)
            {
                if (_motor != null) _motor.enabled = false; // até ficar a pé
                var cam = Camera.main;
                if (cam != null) cam.gameObject.SetActive(true);
            }
            else if (_motor != null) _motor.enabled = false;
            Debug.Log($"[NDNET] foot spawn owner={OwnerClientId} srv={IsServer} own={IsOwner}");
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) NdNetSession.Feet.Remove(OwnerClientId);
            IsOnFoot.OnValueChanged -= OnFootChanged;
            base.OnNetworkDespawn();
        }

        private float _onFootSince;

        private void OnFootChanged(bool prev, bool now)
        {
            ApplyVisibility(now);
            if (now) _onFootSince = Time.time;
            if (IsOwner) UpdateOwnerCamera(now);
        }

        /// <summary>Modelo + física só existem quando o jogador está a pé.</summary>
        private void ApplyVisibility(bool on)
        {
            foreach (var r in _rends) if (r != null) r.enabled = on;
            if (_cc != null) _cc.enabled = on;
            if (IsOwner && _motor != null)
            { _motor.enabled = on; if (on) _motor.Activate(null); }
        }

        private void UpdateOwnerCamera(bool onFoot)
        {
            var cam = Camera.main; if (cam == null) return;
            var chase = cam.GetComponent<Studio.Cameras.ChaseCamera>();
            if (chase == null) return;
            if (onFoot) chase.SetTarget(transform);
            else
            {
                var car = FindMyCar();
                if (car != null) chase.SetTarget(car.transform);
            }
        }

        private NetPlayerCar FindMyCar()
        {
            foreach (var c in FindObjectsByType<NetPlayerCar>(
                         FindObjectsSortMode.None))
                if (c.OwnerClientId == OwnerClientId) return c;
            return null;
        }

        /// <summary>Servidor: teleporta o pé pra saída do carro e ativa.</summary>
        public void SetOnFootServer(bool on, Vector3 pos, float yaw)
        {
            if (on)
            {
                if (_cc != null) _cc.enabled = false;
                transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
                if (_cc != null) _cc.enabled = true;
            }
            IsOnFoot.Value = on;
        }

        // ---------------- RPCs ----------------

        /// <summary>Input do dono → servidor (30Hz, unreliable).</summary>
        [Rpc(SendTo.Server, Delivery = RpcDelivery.Unreliable, RequireOwnership = true)]
        private void FootInputServerRpc(uint seq, Vector2 move, float yaw,
            bool sprint, bool jump, bool crouch)
        {
            if (!IsOnFoot.Value) return; // a pé só aceita input quando a pé
            if (Time.time < _nextInputOk) return;
            _nextInputOk = Time.time + 1f / 60f;
            if (seq <= _lastSeq && _lastSeq - seq > 512u) return;
            if (!float.IsFinite(move.x) || !float.IsFinite(move.y) ||
                !float.IsFinite(yaw)) return;
            _lastSeq = seq;
            if (_motor == null) return;
            _motor.NetMove = Vector2.ClampMagnitude(move, 1f);
            _motor.NetYaw = Mathf.Repeat(yaw, 360f);
            _motor.NetSprint = sprint;
            if (jump) _motor.NetJump = true;
            if (crouch) _motor.NetCrouch = true;
        }

        /// <summary>Dono pede pra entrar num veículo próximo.</summary>
        [Rpc(SendTo.Server, RequireOwnership = true)]
        private void EnterCarServerRpc(ulong carNetId)
        {
            if (!IsOnFoot.Value) return;
            if (!NetworkManager.SpawnManager.SpawnedObjects
                    .TryGetValue(carNetId, out var nobj)) return;
            var car = nobj.GetComponent<NetPlayerCar>();
            if (car == null) return;
            if (Vector3.Distance(transform.position, car.transform.position)
                    > _enterDistance + 1.5f) return; // tolerância p/ lag
            if (car.OccupantClientId.Value != ulong.MaxValue) return; // ocupado
            car.OccupantClientId.Value = OwnerClientId;
            IsOnFoot.Value = false;
            Debug.Log($"[NDNET-SRV] enter car {carNetId} by {OwnerClientId}");
        }

        /// <summary>Estado autoritativo → clientes (20Hz, unreliable).</summary>
        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void FootStateClientRpc(Vector3 pos, float yaw,
            float speedNorm, uint ackSeq)
        {
            if (IsOwner)
            {
                var err = pos - transform.position; err.y = 0f;
                float mag = err.magnitude;
                if (mag > 2.0f)
                {   // divergência: snap autoritativo
                    bool was = _cc != null && _cc.enabled;
                    if (was) _cc.enabled = false;
                    transform.SetPositionAndRotation(
                        pos, Quaternion.Euler(0, yaw, 0));
                    if (was) _cc.enabled = true;
                    if (_motor != null) _motor.Face(Quaternion.Euler(0, yaw, 0));
                    _blendErr = Vector3.zero;
                }
                else if (mag > 0.1f) _blendErr += err * 0.5f;
            }
            else
            {
                _snaps.Add(new Snap { P = pos, Yaw = yaw, Spd = speedNorm, T = Time.time });
                if (_snaps.Count > 20) _snaps.RemoveAt(0);
            }
        }

        // ---------------- loop ----------------

        private void FixedUpdate()
        {
            if (IsServer)
            {
                if (IsOnFoot.Value && Time.time > _nextBroadcast)
                {
                    _nextBroadcast = Time.time + 1f / _stateRate;
                    FootStateClientRpc(transform.position,
                        transform.eulerAngles.y,
                        _motor != null ? _motor.SpeedNorm : 0f, _lastSeq);
                }
                if (Time.time > _logAt)
                {
                    _logAt = Time.time + 5f;
                    Debug.Log($"[NDNET-SRV] foot owner={OwnerClientId} " +
                        $"onFoot={IsOnFoot.Value} pos={transform.position:F1}");
                }
                return;
            }

            if (IsOwner)
            {
                if (!IsOnFoot.Value) return;
                // entrada
                if (Time.time >= _nextSend)
                {
                    _nextSend = Time.time + 1f / _inputRate;
                    var inp = DriveInputAdapter.Instance;
                    Vector2 mv = inp != null ? inp.FootMove : Vector2.zero;
                    if (NdNetSession.AutoDrive)
                        // sai à esquerda do carro; andar p/ direita se aproxima
                        mv = new Vector2(0.45f, 0.08f);
                    float yaw = transform.eulerAngles.y;
                    bool sp = inp != null && inp.SprintHeld;
                    SubmitInputWithEdges(mv, yaw, sp);
                    // enter car: E perto de um carro livre
                    if (inp != null && inp.ConsumeEnterExit())
                        TryEnterNearestCar();
                    // autoteste C: re-entra ~6s depois de sair (perto do carro)
                    if (NdNetSession.AutoDrive && _onFootSince > 0f &&
                        Time.time - _onFootSince > 6f)
                        TryEnterNearestCar();
                }
                // reconciliação suave
                if (_blendErr.sqrMagnitude > 0.0001f)
                {
                    bool was = _cc != null && _cc.enabled;
                    if (was) _cc.enabled = false;
                    transform.position += _blendErr *
                        Mathf.Min(1f, 4f * Time.fixedDeltaTime);
                    _blendErr *= 1f - Mathf.Min(1f, 4f * Time.fixedDeltaTime);
                    if (was) _cc.enabled = true;
                }
            }
            else if (IsOnFoot.Value)
            {
                InterpRemote();
            }
        }

        private void SubmitInputWithEdges(Vector2 mv, float yaw, bool sprint)
        {
            var inp = DriveInputAdapter.Instance;
            bool jump = inp != null && inp.PeekJump();
            bool crouch = inp != null && inp.PeekCrouch();
            FootInputServerRpc(_nextSeq++, mv, yaw, sprint, jump, crouch);
        }

        private void TryEnterNearestCar()
        {
            NetPlayerCar best = null; float bd = _enterDistance + 0.5f;
            foreach (var c in FindObjectsByType<NetPlayerCar>(
                         FindObjectsSortMode.None))
            {
                if (c.OccupantClientId.Value != ulong.MaxValue) continue;
                float d = Vector3.Distance(transform.position, c.transform.position);
                if (d < bd) { bd = d; best = c; }
            }
            if (best != null)
                EnterCarServerRpc(best.NetworkObjectId);
        }

        private void InterpRemote()
        {
            if (_snaps.Count == 0) return;
            float rt = Time.time - _interpDelay;
            var last = _snaps[^1];
            Vector3 p = last.P; float yaw = last.Yaw; float spd = last.Spd;
            if (_snaps.Count > 1 && rt < last.T)
                for (int i = _snaps.Count - 1; i > 0; i--)
                    if (_snaps[i - 1].T <= rt)
                    {
                        var a = _snaps[i - 1]; var b = _snaps[i];
                        float f = Mathf.InverseLerp(a.T, b.T, rt);
                        p = Vector3.LerpUnclamped(a.P, b.P, f);
                        yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, f);
                        spd = Mathf.Lerp(a.Spd, b.Spd, f);
                        break;
                    }
            transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
            if (_anim != null) _anim.SetFloat("Speed", spd >= 0.55f ? 2f : (spd > 0.05f ? 1f : 0f));
        }
    }
}
