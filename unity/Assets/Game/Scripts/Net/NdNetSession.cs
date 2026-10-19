using System;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Studio.Net
{
    /// <summary>
    /// Bootstrap de rede do NEON DISTRICT (Netcode for GameObjects + UDP).
    /// Modos via CLI: -ndserver (dedicado), -ndhost, -ndclient=IP[:PORTA].
    /// Sem argumentos → single-player intocado. Em modo rede, o rig SP da cena
    /// (PlayerCar/OnFoot/ModeManager/câmeras) é desativado e o NetworkManager
    /// spawna o NetPlayerCar por cliente — servidor autoritativo.
    /// </summary>
    public static class NdNetSession
    {
        public const int DefaultPort = 7777;
        public static bool NetActive { get; private set; }
        public static bool IsDedicatedServer { get; private set; }
        public static bool AutoDrive { get; private set; }
        public static string ServerIp = "127.0.0.1";
        public static int ServerPort = DefaultPort;

        /// <summary>Registro servidor: clientId → entidade a pé do jogador.</summary>
        public static readonly Dictionary<ulong, NetPlayerFoot> Feet = new();
        /// <summary>Contadores de entidades replicadas (servidor).</summary>
        public static int NetPeds, NetTraf;
        /// <summary>Cache de histerese de visibilidade (NO,clientId)→visível.</summary>
        public static readonly Dictionary<(ulong, ulong), bool>
            _visCache = new();

        /// <summary>Distância do jogador do cliente <paramref name="cid"/>
        /// à posição — usa o pé se a pé, senão o carro (fallback 9999).</summary>
        public static float DistanceToPlayer(ulong cid, Vector3 pos)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || !nm.IsServer) return 0f;
            Vector3 p;
            if (Feet.TryGetValue(cid, out var f) && f != null &&
                f.IsOnFoot.Value) p = f.transform.position;
            else if (nm.ConnectedClients.TryGetValue(cid, out var cc) &&
                     cc.PlayerObject != null) p = cc.PlayerObject.transform.position;
            else return 9999f;
            return Vector3.Distance(p, pos);
        }

        /// <summary>Visibilidade com histerese: entra a &lt;enter m,
        /// sai só a &gt;exit m (evita flicker de spawn/despawn na borda).</summary>
        public static bool CheckVis(ulong id, ulong cid,
            Vector3 pos, float enter, float exit)
        {
            float d = DistanceToPlayer(cid, pos);
            var k = (id, cid);
            bool vis = _visCache.TryGetValue(k, out var v) && v
                ? d < exit : d < enter;
            _visCache[k] = vis;
            return vis;
        }

        public static event Action OnNetModeReady;

        /// <summary>Parse args uma vez; chamado pelo hook de scene load.</summary>
        public static void InitFromArgs()
        {
            var args = Environment.GetCommandLineArgs();
            bool wantsClient = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-ndserver" || args[i] == "-dedicatedServer")
                    IsDedicatedServer = true;
                else if (args[i] == "-ndhost")
                    wantsClient = true; // host = server+client local
                else if (args[i].StartsWith("-ndclient="))
                {
                    wantsClient = true;
                    var v = args[i].Substring("-ndclient=".Length).Split(':');
                    ServerIp = v[0];
                    if (v.Length > 1) int.TryParse(v[1], out ServerPort);
                }
                else if (args[i] == "-ndautodrive")
                    AutoDrive = true;
                else if (args[i] == "-ndport" && i + 1 < args.Length)
                    int.TryParse(args[i + 1], out ServerPort);
            }
            // batchmode = servidor dedicado SÓ se nenhum modo cliente/host foi pedido
            if (Application.isBatchMode && !wantsClient) IsDedicatedServer = true;
            NetActive = IsDedicatedServer || wantsClient;
            if (!NetActive) return;

            Debug.Log($"[NDNET] modo={(IsDedicatedServer ? "server" : "client")} " +
                $"destino={ServerIp}:{ServerPort}");
        }

        /// <summary>Desativa o rig single-player serializado na cena.</summary>
        public static void DisableSpRig()
        {
            var car = UnityEngine.Object.FindFirstObjectByType<
                Studio.Gameplay.PlayerCarController>();
            var motor = UnityEngine.Object.FindFirstObjectByType<
                Studio.Gameplay.OnFootMotor>(FindObjectsInactive.Include);
            var mode = UnityEngine.Object.FindFirstObjectByType<
                Studio.Gameplay.NeonDistrictModeManager>();
            if (car != null) car.gameObject.SetActive(false);
            if (motor != null) motor.gameObject.SetActive(false);
            if (mode != null) mode.enabled = false;
            Debug.Log("[NDNET] rig SP desativado (modo rede)");
        }

        /// <summary>Monta NetworkManager + transport UDP e inicia.</summary>
        public static NetworkManager StartNet()
        {
            if (!NetActive) return null;
            var prefab = Resources.Load<GameObject>("Net/NetPlayerCar");
            if (prefab == null)
            {
                Debug.LogError("[NDNET] NetPlayerCar prefab ausente em Resources/Net");
                return null;
            }
            try
            {
                var go = new GameObject("_NdNet");
                UnityEngine.Object.DontDestroyOnLoad(go);
                var utp = go.AddComponent<UnityTransport>();
                // servidor escuta em todas as interfaces; cliente aponta pro IP
                utp.SetConnectionData(
                    IsDedicatedServer ? "0.0.0.0" : ServerIp,
                    (ushort)ServerPort);
                var nm = go.AddComponent<NetworkManager>();
                if (nm.NetworkConfig == null)
                    nm.NetworkConfig = new NetworkConfig();
                nm.NetworkConfig.NetworkTransport = utp;
                nm.NetworkConfig.PlayerPrefab = prefab;

                // prefab a pé: registrar nos DOIS lados (senão o cliente não
                // consegue instanciar a replica); spawn é só no servidor
                var footPf = Resources.Load<GameObject>("Net/NetPlayerFoot");
                if (footPf != null) nm.AddNetworkPrefab(footPf);
                // registra prefabs de ped/tráfego (variantes com NB assado)
                foreach (var p in Resources.LoadAll<GameObject>("Art/Pedestrians"))
                    if (p.GetComponent<NetworkObject>() != null)
                        nm.AddNetworkPrefab(p);
                var tm = UnityEngine.Object.FindFirstObjectByType<
                    Studio.Gameplay.TrafficManager>();
                if (tm != null)
                    foreach (var v in tm.VehiclePrefabs)
                        if (v != null && v.GetComponent<NetworkObject>() != null)
                            nm.AddNetworkPrefab(v);

                // cliente: IA local desligada; servidor: hooks p/ Spawn
                bool srv = IsDedicatedServer;
                Studio.Gameplay.PedestrianSystem.NetworkClientMode = !srv;
                Studio.Gameplay.TrafficManager.NetworkClientMode = !srv;
                Studio.Gameplay.PedestrianSystem.OnPedSpawned =
                    srv ? go => go.GetComponent<NetworkObject>()?.Spawn()
                        : (System.Action<GameObject>)null;
                Studio.Gameplay.TrafficManager.OnTrafficSpawned =
                    srv ? go => go.GetComponent<NetworkObject>()?.Spawn()
                        : (System.Action<GameObject>)null;
                Studio.Gameplay.TrafficManager.OnTrafficDespawned =
                    srv ? go => go.GetComponent<NetworkObject>()?.Despawn()
                        : (System.Action<GameObject>)null;

                if (IsDedicatedServer && footPf != null)
                    nm.OnClientConnectedCallback += cid =>
                    {
                        var f = UnityEngine.Object.Instantiate(footPf,
                            new Vector3(0, -50f, 0), Quaternion.identity);
                        f.GetComponent<NetworkObject>().SpawnWithOwnership(cid);
                        Debug.Log($"[NDNET-SRV] foot spawned for client {cid}");
                    };

                bool ok = IsDedicatedServer ? nm.StartServer() : nm.StartClient();
                Debug.Log(ok ? $"[NDNET] {(IsDedicatedServer ? "server" : "client")} ON :{ServerPort}"
                             : "[NDNET] FALHA ao iniciar");
                if (!ok) return null;
                OnNetModeReady?.Invoke();
                return nm;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[NDNET] StartNet falhou: {e}");
                return null;
            }
        }
    }
}
