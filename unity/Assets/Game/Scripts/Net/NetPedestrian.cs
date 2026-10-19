using System.Collections.Generic;
using Studio.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace Studio.Net
{
    /// <summary>
    /// Réplica de pedestre: assada na prefab *_baked. O servidor roda a IA
    /// (PedestrianSystem/Pedestrian existentes) e difunde estado compacto a
    /// ~8Hz; o cliente NÃO tem componente Pedestrian (é adicionado só no
    /// servidor) — só interpola transform e dirige o Animator por estado.
    /// NetState no Pedestrian: 0 idle/walk,1 flee,2 dead,3 cower.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetPedestrian : NetworkBehaviour
    {
        private struct Snap { public Vector3 P; public float Yaw, Spd, T; }
        private readonly List<Snap> _snaps = new();
        private Animator _anim;
        private Pedestrian _ped;      // só existe no servidor
        private float _nextBc;
        private byte _lastState;

        private void Awake()
        {
            _anim = GetComponentInChildren<Animator>();
            _ped = GetComponent<Pedestrian>(); // nulo no cliente
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                NdNetSession.NetPeds++; Debug.Log($"[NDNET-SRV] ped spawned total={NdNetSession.NetPeds}");
                // relevância: visível a <120m do jogador, some só a >140m
                NetworkObject.CheckObjectVisibility = cid =>
                    NdNetSession.CheckVis(NetworkObjectId, cid,
                        transform.position, 120f, 140f);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) NdNetSession.NetPeds--;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (IsServer)
            {
                if (Time.time < _nextBc) return;
                _nextBc = Time.time + 0.12f;
                float spd = _ped != null ? _ped.NetSpeedNorm : 0f;
                byte st = _ped != null ? _ped.NetState : (byte)0;
                PedStateClientRpc(transform.position,
                    transform.eulerAngles.y, spd, st);
            }
            else Interp();
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void PedStateClientRpc(Vector3 pos, float yaw,
            float speed, byte state)
        {
            _snaps.Add(new Snap { P = pos, Yaw = yaw, Spd = speed, T = Time.time });
            if (_snaps.Count > 12) _snaps.RemoveAt(0);
            if (state != _lastState)
            {
                _lastState = state;
                if (_anim != null)
                {
                    if (state == 2) _anim.SetTrigger("Dead");
                    _anim.SetBool("Cowering", state == 3);
                }
            }
        }

        private void Interp()
        {
            if (_snaps.Count == 0) return;
            float rt = Time.time - 0.12f;
            var b = _snaps[^1];
            Vector3 p = b.P; float yaw = b.Yaw, spd = b.Spd;
            if (_snaps.Count > 1 && rt < b.T)
                for (int i = _snaps.Count - 1; i > 0; i--)
                    if (_snaps[i - 1].T <= rt)
                    {
                        var a = _snaps[i - 1];
                        float f = Mathf.InverseLerp(a.T, b.T, rt);
                        p = Vector3.LerpUnclamped(a.P, b.P, f);
                        yaw = Mathf.LerpAngle(a.Yaw, b.Yaw, f);
                        spd = Mathf.Lerp(a.Spd, b.Spd, f);
                        break;
                    }
            transform.SetPositionAndRotation(p, Quaternion.Euler(0, yaw, 0));
            if (_anim != null)
                _anim.SetFloat("Speed", spd > 0.55f ? 2f : (spd > 0.05f ? 1f : 0f));
        }
    }
}
