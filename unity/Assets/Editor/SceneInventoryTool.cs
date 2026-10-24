using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// Inventario estrutural de cena POR GEOMETRIA (regra A6.6 do handoff:
    /// auditoria nunca filtra por nome â€” nome Ã© sugestÃ£o, fÃ­sica Ã© realidade).
    /// Percorre TODOS os transforms (inclusive inativos) e emite JSON com
    /// posicao de mundo, bounds de renderer, colliders (tipo/bounds/trigger),
    /// rigidbody e origem do prefab. Base para diffs entre versoes de cena.
    ///
    /// Uso (eval_file no editor vivo):
    ///   Studio.Editor.SceneInventoryTool.Dump("Assets/Game/Scenes/NeonDistrict.unity",
    ///       "C:/abs/path/evidence/qa/inv_<tag>.json");
    /// </summary>
    public static class SceneInventoryTool
    {
        private static readonly System.Globalization.CultureInfo CULT =
            System.Globalization.CultureInfo.InvariantCulture;

        public static void Dump(string scenePath, string outAbsJson)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var sb = new StringBuilder(1 << 22);
            sb.Append('[');
            int count = 0;
            foreach (var t in Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var go = t.gameObject;
                if (count > 0) sb.Append(',');
                sb.Append('{');
                AppendKV(sb, "path", GetPath(t));
                AppendKV(sb, "name", go.name);
                AppendKV(sb, "src", PrefabUtility.GetCorrespondingObjectFromSource(go)?.name ?? "");
                sb.Append("\"active\":").Append(go.activeSelf ? 1 : 0).Append(',');
                sb.Append("\"layer\":").Append(go.layer).Append(',');
                var p = t.position;
                sb.Append("\"pos\":[").Append(p.x.ToString("F3", CULT)).Append(',')
                  .Append(p.y.ToString("F3", CULT)).Append(',')
                  .Append(p.z.ToString("F3", CULT)).Append(']');

                var r = go.GetComponent<Renderer>();
                if (r != null)
                {
                    var b = r.bounds;
                    sb.Append(",\"rend\":{\"enabled\":").Append(r.enabled ? 1 : 0)
                      .Append(",\"b\":[").Append(b.center.x.ToString("F3", CULT)).Append(',')
                      .Append(b.center.y.ToString("F3", CULT)).Append(',').Append(b.center.z.ToString("F3", CULT)).Append(',')
                      .Append(b.size.x.ToString("F3", CULT)).Append(',').Append(b.size.y.ToString("F3", CULT)).Append(',')
                      .Append(b.size.z.ToString("F3", CULT)).Append("],\"mats\":\"");
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (i > 0) sb.Append('|');
                        sb.Append(mats[i] != null ? mats[i].name : "NULL");
                    }
                    sb.Append("\"}");
                }

                var cols = go.GetComponents<Collider>();
                if (cols.Length > 0)
                {
                    sb.Append(",\"cols\":[");
                    for (int i = 0; i < cols.Length; i++)
                    {
                        var c = cols[i];
                        if (i > 0) sb.Append(',');
                        var cb = c.bounds;
                        sb.Append("{\"t\":\"").Append(c.GetType().Name)
                          .Append("\",\"on\":").Append(c.enabled ? 1 : 0)
                          .Append(",\"trig\":").Append(c.isTrigger ? 1 : 0)
                          .Append(",\"b\":[").Append(cb.center.x.ToString("F3", CULT)).Append(',')
                          .Append(cb.center.y.ToString("F3", CULT)).Append(',').Append(cb.center.z.ToString("F3", CULT)).Append(',')
                          .Append(cb.size.x.ToString("F3", CULT)).Append(',').Append(cb.size.y.ToString("F3", CULT)).Append(',')
                          .Append(cb.size.z.ToString("F3", CULT)).Append("]}");
                    }
                    sb.Append(']');
                }

                if (go.GetComponent<Rigidbody>() != null) sb.Append(",\"rb\":1");
                sb.Append('}');
                count++;
            }
            sb.Append(']');
            System.IO.File.WriteAllText(outAbsJson, sb.ToString());
            Debug.Log($"[inventory] {scenePath} objetos={count} -> {outAbsJson} (cena aberta: {scene.path})");
        }

        private static void AppendKV(StringBuilder sb, string k, string v)
        {
            sb.Append('"').Append(k).Append("\":\"").Append(Escape(v)).Append("\",");
        }

        private static string GetPath(Transform t)
        {
            var sb = new StringBuilder(t.name);
            var cur = t.parent;
            while (cur != null) { sb.Insert(0, cur.name + "/"); cur = cur.parent; }
            return sb.ToString();
        }

        private static string Escape(string s)
        {
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
