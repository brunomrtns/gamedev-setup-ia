// _v38_push_off_road.cs — eval_file (edit mode): empurra postes/árvores/
// parquímetros para FORA do corredor de via (push perpendicular mínimo,
// borda + 0,5 m de folga). Não toca em: Avenue_Dual (medianas quebráveis,
// design), Bounds_Blocker_* (bordas do mapa), prédios, carros, peds.
// Família da dívida v3.6: postes na pista (máscara errada do FIX-1).
// Nota: colliders FitProp de árvore são IRMÃOS do root (sob Street_Props) —
// root e collider são movidos JUNTOS pelo mesmo delta.
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
    "Assets/Game/Scenes/NeonDistrict.unity",
    UnityEditor.SceneManagement.OpenSceneMode.Single);

float[] ewLines = { -130f, -78f, -26f, 26f, 78f, 130f };
float[] nsLines = { -130f, -78f, -26f, 26f, 78f, 130f, 182f, 234f };
const float X_MIN = -147f, X_MAX = 284f;

bool IsFamily(string n) =>
    n.StartsWith("Lamp_") || n.StartsWith("lamp_pole_") ||
    n.StartsWith("Tree_") || n.StartsWith("parking meter");

var processed = new System.Collections.Generic.HashSet<UnityEngine.Transform>();
int moved = 0;
var log = new System.Text.StringBuilder();

foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(
             UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
    if (t.name.EndsWith("_BoundsCollider")) continue;   // movemos o ROOT
    if (!IsFamily(t.name)) continue;
    if (t.name.StartsWith("Avenue_Dual")) continue;      // mediana (design)
    if (t.name.Contains("Blocker")) continue;

    // sobe até o ancestral mais externo da mesma família
    var root = t;
    while (root.parent != null && IsFamily(root.parent.name)) root = root.parent;
    if (!processed.Add(root)) continue;

    // acha o colisor: filho, IRMÃO (padrão FitProp tree) ou descendente
    UnityEngine.Transform colGo = root.Find(root.name + "_BoundsCollider");
    if (colGo == null && root.parent != null)
        colGo = root.parent.Find(root.name + "_BoundsCollider");
    UnityEngine.Collider c;
    if (colGo != null) c = colGo.GetComponent<UnityEngine.Collider>();
    else c = root.GetComponentInChildren<UnityEngine.Collider>();
    if (c == null) continue;
    var b = c.bounds;
    if (b.center.y - b.extents.y > 3f) continue;

    float bestDx = 0f, bestDz = 0f, bestMag = float.MaxValue;
    string bestWhich = null;
    for (int j = 0; j < ewLines.Length; j++)
    {
        float hw = j == 3 ? 8f : 6f;
        var z0 = ewLines[j];
        if (b.center.x < X_MIN || b.center.x > X_MAX) continue;
        if (b.center.z - b.extents.z >= z0 + hw || b.center.z + b.extents.z <= z0 - hw) continue;
        float side = b.center.z >= z0 ? 1f : -1f;
        float dz = z0 + side * (hw + b.extents.z + 0.5f) - b.center.z;
        if (Mathf.Abs(dz) < bestMag) { bestMag = Mathf.Abs(dz); bestDz = dz; bestDx = 0f; bestWhich = "EW_z" + z0; }
    }
    foreach (var x0 in nsLines)
    {
        const float hw = 6f;
        if (b.center.x - b.extents.x >= x0 + hw || b.center.x + b.extents.x <= x0 - hw) continue;
        float side = b.center.x >= x0 ? 1f : -1f;
        float dx = x0 + side * (hw + b.extents.x + 0.5f) - b.center.x;
        if (Mathf.Abs(dx) < bestMag) { bestMag = Mathf.Abs(dx); bestDx = dx; bestDz = 0f; bestWhich = "NS_x" + x0; }
    }
    if (bestWhich == null || bestMag < 0.05f) continue;

    var p = root.position;
    var np = new UnityEngine.Vector3(p.x + bestDx, p.y, p.z + bestDz);
    root.position = np;
    if (colGo != null && colGo.parent != root)
        colGo.position += new UnityEngine.Vector3(bestDx, 0f, bestDz);
    moved++;
    log.AppendLine($"[v38-push] {root.name} @({p.x:F1},{p.z:F1}) -> ({np.x:F1},{np.z:F1}) via {bestWhich} ({bestMag:F1}m)");
}
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log(log.ToString());
UnityEngine.Debug.Log($"[v38-push] TOTAL movidos={moved} cena salva");
