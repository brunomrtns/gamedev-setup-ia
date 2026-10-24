// _v38_restore_road_mats.cs — eval_file (edit mode): restaura os materiais
// canonicos de via nos 13 pisos trocados pelo FixWhiteTrees (v3.6).
// Street_EW/NS -> Mat_ND_Road_Street (asphalt_2_tracks, Demo City)
// Crosswalk_Street_* -> Mat_ND_Crosswalk
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
    "Assets/Game/Scenes/NeonDistrict.unity",
    UnityEditor.SceneManagement.OpenSceneMode.Single);
var road = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(
    "Assets/Game/Materials/NeonDistrict/Mat_ND_Road_Street.mat");
var cross = UnityEditor.AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(
    "Assets/Game/Materials/NeonDistrict/Mat_ND_Crosswalk.mat");
int n = 0;
foreach (var r in UnityEngine.Object.FindObjectsByType<UnityEngine.MeshRenderer>(
             UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None))
{
    var name = r.gameObject.name;
    UnityEngine.Material want = null;
    if (name.StartsWith("Street_EW_") || name.StartsWith("Street_NS_")) want = road;
    else if (name.StartsWith("Crosswalk_Street_")) want = cross;
    if (want == null || r.sharedMaterial == want) continue;
    var mats = r.sharedMaterials;
    mats[0] = want;
    r.sharedMaterials = mats;
    n++;
}
UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
UnityEngine.Debug.Log($"[v38-mats] restaurados={n} (esperado 13) cena salva");
