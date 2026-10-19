using UnityEditor;
using UnityEngine;

namespace Studio.Editor
{
    public static class ModelVertexInspector
    {
        public static void Inspect()
        {
            InspectModel("BarramundiFish", "Assets/Game/Art/AbyssalBreach/Models/BarramundiFish.fbx");
            InspectModel("SM_Assault_Rifle_B", "Assets/Game/Art/AbyssalBreach/Models/SM_Assault_Rifle_B.fbx");
            InspectModel("SM_Tactical_Knife", "Assets/Game/Art/AbyssalBreach/Models/SM_Tactical_Knife.fbx");
            InspectModel("SM_Sentinel_Drone", "Assets/Game/Art/AbyssalBreach/Models/SM_Sentinel_Drone.fbx");
            EditorApplication.Exit(0);
        }

        private static void InspectModel(string tag, string path)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null)
            {
                Debug.LogError($"[{tag}] Arquivo não encontrado: {path}");
                return;
            }

            var mf = go.GetComponentInChildren<MeshFilter>();
            if (mf == null || mf.sharedMesh == null)
            {
                Debug.LogError($"[{tag}] MeshFilter ausente em {path}");
                return;
            }

            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var b = mesh.bounds;

            Vector3 minV = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 maxV = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int i = 0; i < verts.Length; i++)
            {
                minV = Vector3.Min(minV, verts[i]);
                maxV = Vector3.Max(maxV, verts[i]);
            }

            Debug.Log($"[{tag}] Vertices={verts.Length}, BoundsSize=({b.size.x:F4}, {b.size.y:F4}, {b.size.z:F4}), Min=({minV.x:F4}, {minV.y:F4}, {minV.z:F4}), Max=({maxV.x:F4}, {maxV.y:F4}, {maxV.z:F4})");
        }
    }
}
