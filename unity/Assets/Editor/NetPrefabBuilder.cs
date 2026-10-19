using System.IO;
using Studio.Gameplay;
using Studio.Net;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// Gera Assets/Game/Resources/Net/NetPlayerFoot.prefab a partir de um
    /// prefab de pedestre *_baked (humanóide real, não placeholder):
    /// + CharacterController + OnFootMotor + NetworkObject + NetPlayerFoot.
    /// Uso: -executeMethod Studio.Editor.NetPrefabBuilder.BuildNetFootPrefab
    /// </summary>
    public static class NetPrefabBuilder
    {
        public static void BuildNetFootPrefab()
        {
            var peds = Resources.LoadAll<GameObject>("Art/Pedestrians");
            GameObject src = null;
            foreach (var p in peds)
                if (p.name.EndsWith("_baked")) { src = p; break; }
            if (src == null && peds.Length > 0) src = peds[0];
            if (src == null)
            {
                Debug.LogError("[NETPFX] nenhum ped prefab em Resources/Art/Pedestrians");
                EditorApplication.Exit(1); return;
            }

            var go = Object.Instantiate(src);
            go.name = "NetPlayerFoot";

            // remove colisores/rigidbodies de NPC (prefab pode trazer)
            foreach (var c in go.GetComponentsInChildren<Collider>(true))
                if (!(c is CharacterController)) Object.DestroyImmediate(c);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true))
                Object.DestroyImmediate(rb);
            foreach (var mb in go.GetComponentsInChildren<MonoBehaviour>(true))
            {   // remove scripts de IA de pedestre herdados do prefab
                if (mb != null && mb.GetType().Namespace == "Studio.Gameplay"
                    && mb.GetType().Name.Contains("Pedestrian"))
                    Object.DestroyImmediate(mb);
            }

            var cc = go.AddComponent<CharacterController>();
            cc.height = OnFootMotor.HUMAN_HEIGHT;
            cc.center = new Vector3(0, OnFootMotor.HUMAN_HEIGHT * 0.5f, 0);
            cc.radius = 0.35f; cc.slopeLimit = 50f; cc.stepOffset = 0.35f;
            go.AddComponent<OnFootMotor>();
            go.AddComponent<NetworkObject>();
            go.AddComponent<NetPlayerFoot>();

            Directory.CreateDirectory("Assets/Game/Resources/Net");
            var saved = PrefabUtility.SaveAsPrefabAsset(
                go, "Assets/Game/Resources/Net/NetPlayerFoot.prefab");
            Object.DestroyImmediate(go);
            Debug.Log(saved != null
                ? $"[NETPFX] NetPlayerFoot.prefab salvo (src={src.name})"
                : "[NETPFX] FALHOU");
            EditorApplication.Exit(saved != null ? 0 : 1);
        }

        /// <summary>
        /// Etapa C2: assa NetworkObject+NetPedestrian nas prefabs *_baked e
        /// NetworkObject+NetTrafficSync nas prefabs de veículo civil (as
        /// serializadas no TrafficManager da cena NeonDistrict).
        /// Uso: -executeMethod Studio.Editor.NetPrefabBuilder.BakeNetEntities
        /// </summary>
        public static void BakeNetEntities()
        {
            int n = 0;
            // pedestres
            foreach (var p in Resources.LoadAll<GameObject>("Art/Pedestrians"))
            {
                if (!p.name.EndsWith("_baked")) continue;
                string path = AssetDatabase.GetAssetPath(p);
                var root = PrefabUtility.LoadPrefabContents(path);
                if (root.GetComponent<NetworkObject>() == null)
                {
                    root.AddComponent<NetworkObject>();
                    root.AddComponent<NetPedestrian>();
                    PrefabUtility.SaveAsPrefabAsset(root, path); n++;
                }
                PrefabUtility.UnloadPrefabContents(root);
            }
            // tráfego: prefabs reais do TrafficManager da cena
            var scene = EditorSceneManager.OpenScene(
                "Assets/Game/Scenes/NeonDistrict.unity");
            var tm = Object.FindFirstObjectByType<TrafficManager>();
            if (tm != null && tm.VehiclePrefabs != null)
                foreach (var v in tm.VehiclePrefabs)
                {
                    if (v == null) continue;
                    string path = AssetDatabase.GetAssetPath(v);
                    if (string.IsNullOrEmpty(path)) continue;
                    var root = PrefabUtility.LoadPrefabContents(path);
                    if (root.GetComponent<NetworkObject>() == null)
                    {
                        root.AddComponent<NetworkObject>();
                        root.AddComponent<NetTrafficSync>();
                        PrefabUtility.SaveAsPrefabAsset(root, path); n++;
                    }
                    PrefabUtility.UnloadPrefabContents(root);
                }
            Debug.Log($"[NETPFX] BakeNetEntities: {n} prefabs assados");
            EditorApplication.Exit(0);
        }
    }
}
