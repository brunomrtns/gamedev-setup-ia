// _v38_remove_junk_colliders.cs â€” eval_file (edit mode): remove APENAS os
// 361 colisores-lixo inventariados no M1 (6 nomes exatos, nenhum outro
// _BoundsCollider legitimo e tocado). Conta por nome e salva a cena.
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
    "Assets/Game/Scenes/NeonDistrict.unity",
    UnityEditor.SceneManagement.OpenSceneMode.Single);
string[] alvos =
{
    "Decal_DoubleYellow_Straight_BoundsCollider",
    "Decal_Crosswalk_BoundsCollider",
    "Decal_Crosswalk_Wide_BoundsCollider",
    "Decal_Stop_BoundsCollider",
    "Sidewalk_Straight_3m_BoundsCollider",
    "AsphaltPatch_Plaza_BoundsCollider",
};
var contagem = new System.Collections.Generic.Dictionary<string,int>();
var doomed = new System.Collections.Generic.List<UnityEngine.GameObject>();
foreach (var t in UnityEngine.Object.FindObjectsByType<UnityEngine.Transform>(
             UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
    if (System.Array.IndexOf(alvos, t.name) < 0) continue;
    if (!contagem.ContainsKey(t.name)) contagem[t.name] = 0;
    contagem[t.name]++;
    doomed.Add(t.gameObject);
}
int total = doomed.Count;
foreach (var g in doomed) UnityEngine.Object.DestroyImmediate(g);
foreach (var kv in contagem)
    UnityEngine.Debug.Log($"[v38-rm] {kv.Key} = {kv.Value}");
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log($"[v38-rm] TOTAL removidos={total} (esperado 361) cena salva");
