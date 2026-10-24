UnityEditor.AssetDatabase.Refresh();
Studio.Editor.SceneInventoryTool.Dump("Assets/Game/Scenes/NeonDistrict.unity",
    @"C:/Users/Bruno/workspace/gamedev-setup-ia/evidence/qa/v38/inv_head.json");
Studio.Editor.SceneInventoryTool.Dump("Assets/Game/Scenes/_v38_pre.unity",
    @"C:/Users/Bruno/workspace/gamedev-setup-ia/evidence/qa/v38/inv_pre.json");
UnityEditor.AssetDatabase.DeleteAsset("Assets/Game/Scenes/_v38_pre.unity");
var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Scenes/NeonDistrict.unity",
    UnityEditor.SceneManagement.OpenSceneMode.Single);
UnityEngine.Debug.Log("[v38] redumps done scene=" + scene.path);
