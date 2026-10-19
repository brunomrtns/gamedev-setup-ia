using System.Collections.Generic;
using Studio.Gameplay;
using Unity.Netcode;
using UnityEngine;

namespace Studio.Net
{
    /// <summary>
    /// Réplica de veículo civil: assada na prefab de tráfego. IA/física só no
    /// servidor (TrafficVehicle normal lá); cliente só interpola pos/rot.
    /// Estado byte = CrashState (0 Routing,1 PhysicsFree,2 Wrecked).
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetTrafficSync : NetworkBehaviour
    {
        private struct Snap { public Vector3 P; public Quaternion R; public float T; }
        private readonly List<Snap> _snaps = new();
        private TrafficVehicle _tv;
        private float _nextBc;

        private void Awake() { _tv = GetComponent<TrafficVehicle>(); }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            if (IsServer)
            {
                NdNetSession.NetTraf++; Debug.Log($"[NDNET-SRV] traf spawned total={NdNetSession.NetTraf}");
                NetworkObject.CheckObjectVisibility = cid =>
                    NdNetSession.CheckVis(NetworkObjectId, cid,
                        transform.position, 130f, 160f);
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer) NdNetSession.NetTraf--;
            base.OnNetworkDespawn();
        }

        private void Update()
        {
            if (IsServer)
            {
                if (Time.time < _nextBc) return;
                _nextBc = Time.time + 0.1f;
                byte st = (byte)(_tv != null ? (int)_tv.State : 0);
                TrafStateClientRpc(transform.position, transform.rotation, st);
            }
            else Interp();
        }

        [Rpc(SendTo.NotServer, Delivery = RpcDelivery.Unreliable)]
        private void TrafStateClientRpc(Vector3 pos, Quaternion rot, byte st)
        {
            _snaps.Add(new Snap { P = pos, R = rot, T = Time.time });
            if (_snaps.Count > 12) _snaps.RemoveAt(0);
        }

        private void Interp()
        {
            if (_snaps.Count == 0) return;
            float rt = Time.time - 0.12f;
            var b = _snaps[^1];
            Vector3 p = b.P; Quaternion r = b.R;
            if (_snaps.Count > 1 && rt < b.T)
                for (int i = _snaps.Count - 1; i > 0; i--)
                    if (_snaps[i - 1].T <= rt)
                    {
                        var a = _snaps[i - 1];
                        float f = Mathf.InverseLerp(a.T, b.T, rt);
                        p = Vector3.LerpUnclamped(a.P, b.P, f);
                        r = Quaternion.Slerp(a.R, b.R, f);
                        break;
                    }
            transform.SetPositionAndRotation(p, r);
        }
    }
}
