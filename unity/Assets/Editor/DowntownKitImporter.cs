using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// Importa o Downtown City MegaKit: materiais por FAMÍLIA de textura
    /// compartilhada (MI_* do FBX → Mat_DT_*) e prefab por peça.
    /// Mesma disciplina do ModularKitImporter: URP Lit + _BaseMap, SRP Batcher.
    /// </summary>
    public static class DowntownKitImporter
    {
        private const string KitDir = "Assets/Game/Art/Environment/Modular/DowntownMegaKit";
        private const string ModelsDir = KitDir + "/Models";
        private const string TexDir = KitDir + "/Textures";
        private const string MatDir = "Assets/Game/Materials/DowntownMegaKit";
        private const string PrefabDir = "Assets/Game/Prefabs/Modular/DowntownMegaKit";

        /// <summary>MI_* do FBX → família de textura (T_&lt;fam&gt;_BaseColor/_Normal).</summary>
        private static string FamilyFor(string slotName)
        {
            var n = slotName.ToLowerInvariant();
            if (n.Contains("fakeinterior_3") || n.Contains("dark_interior")) return "dark_interior";
            if (n.Contains("fakeinterior_1")) return "lit_interior_1";
            if (n.Contains("fakeinterior_2") || n.Contains("fakeinterior_4")) return "lit_interior_2";
            if (n.Contains("fakeinterior") || n.Contains("glass")) return "lit_interior_1";
            if (n.Contains("asphalt")) return "Concrete_Asphalt";
            if (n.Contains("redbrick")) return "RedBrick";
            if (n.Contains("metalconcrete")) return "MetalConcrete";
            if (n.Contains("trim")) return "Trim";
            if (n.Contains("marble") || n.Contains("interiorfloor")) return "MarbleFloor";
            if (n.Contains("interiorwall") || n.Contains("concrete")) return "Concrete";
            if (n.Contains("roof")) return "RoofSlate";
            if (n.Contains("dirt")) return "Dirt";
            if (n.Contains("ornament")) return "Ornaments";
            if (n.Contains("blinds")) return "Blinds";
            if (n.Contains("curtain")) return "Curtains";
            if (n.Contains("decal")) return "Street_Decals";
            return "Concrete";
        }

        /// <summary>Prova visual: os 3 prédios montados + peças representativas.</summary>
        public static void CapturePrefabsBatch()
        {
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.1f, 0f);
            ground.transform.localScale = new Vector3(120f, 0.2f, 120f);
            ground.GetComponent<Renderer>().sharedMaterial =
                new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { color = new Color(0.3f, 0.3f, 0.33f) };
            var light = new GameObject("L").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(45f, -35f, 0f);
            RenderSettings.ambientLight = new Color(0.45f, 0.45f, 0.5f);

            var names = new[]
            {
                "Building_Large_2", "Building_Medium_2_001", "Building_Small_1",
                "Sidewalk_Straight_3m", "Brick_Plain_1", "Street_4WayIntersection",
                "Roof_Metal_1", "Cornice_Brick_1"
            };
            float x = -38f;
            foreach (var n in names)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{PrefabDir}/{n}.prefab");
                if (p == null) { Debug.LogWarning("[MegaKitCap] ausente: " + n); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
                go.transform.position = new Vector3(x, 0f, 0f);
                x += 19f;
            }
            var cam = new GameObject("C").AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 26f, -52f);
            cam.transform.LookAt(new Vector3(0f, 4f, 10f));
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            for (int i = 0; i < 4; i++) cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            File.WriteAllBytes("../evidence/captures/megakit_prefabs.png", tex.EncodeToPNG());
            Debug.Log("[MegaKitCap] captura: megakit_prefabs.png");
        }

        /// <summary>Captura o piloto downtown: câmera na avenida olhando p/ os 2 quarteirões.</summary>
        public static void CapturePilotBatch()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Game/Scenes/NeonDistrict.unity");
            var cam = new GameObject("PilotCam").AddComponent<Camera>();
            // quarteirões pilotos: bi=1,3 bj=3 → centros (±52, +52); avenida em z=+26
            var shots = new (string tag, Vector3 pos, Vector3 look)[]
            {
                ("downtown_pilot_west", new Vector3(-52f, 10f, 26f), new Vector3(-52f, 8f, 52f)),
                ("downtown_pilot_east", new Vector3(52f, 10f, 26f), new Vector3(52f, 8f, 52f)),
                ("downtown_pilot_street", new Vector3(0f, 4f, 26f), new Vector3(52f, 6f, 45f)),
            };
            foreach (var (tag, pos, look) in shots)
            {
                cam.transform.position = pos;
                cam.transform.LookAt(look);
                var rt = new RenderTexture(1280, 720, 24);
                cam.targetTexture = rt;
                for (int i = 0; i < 5; i++) cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes($"../evidence/captures/{tag}.png", tex.EncodeToPNG());
                RenderTexture.active = null; cam.targetTexture = null; rt.Release();
            }
            Debug.Log("[MegaKitCap] pilot capturado: downtown_pilot_*.png");
        }

        /// <summary>Capturas do Old Brick District: aérea + rua + conexão.</summary>
        public static void CaptureDistrictBatch()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Game/Scenes/NeonDistrict.unity");
            var cam = new GameObject("DistrictCam").AddComponent<Camera>();
            var shots = new (string tag, Vector3 pos, Vector3 look)[]
            {
                ("obd_aerial", new Vector3(182f, 160f, 0f), new Vector3(182f, 0f, 0f)),
                ("obd_street_1", new Vector3(156f, 3f, 26f), new Vector3(208f, 8f, 26f)),
                ("obd_street_2", new Vector3(182f, 2.5f, -60f), new Vector3(182f, 15f, 0f)),
                ("obd_connection", new Vector3(120f, 6f, 26f), new Vector3(190f, 8f, 26f)),
            };
            foreach (var (tag, pos, look) in shots)
            {
                cam.transform.position = pos; cam.transform.LookAt(look);
                var rt = new RenderTexture(1280, 720, 24);
                cam.targetTexture = rt;
                for (int i = 0; i < 6; i++) cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes($"../evidence/captures/{tag}.png", tex.EncodeToPNG());
                RenderTexture.active = null; cam.targetTexture = null; rt.Release();
            }
            Debug.Log("[MegaKitCap] distrito: obd_*.png");
        }

        /// <summary>Matriz ANTES/DEPOIS do art pass — posições fixas. prefix="before"|"after".</summary>
        public static void CaptureArtMatrix(string prefix)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Game/Scenes/NeonDistrict.unity");
            var cam = new GameObject("MatrixCam").AddComponent<Camera>();
            var shots = new (string tag, Vector3 pos, Vector3 look)[]
            {
                ("aerial", new Vector3(90f, 210f, -160f), new Vector3(70f, 0f, 20f)),
                ("downtown_1", new Vector3(0f, 3f, -26f), new Vector3(0f, 12f, -52f)),
                ("downtown_2", new Vector3(30f, 3f, 26f), new Vector3(60f, 12f, 26f)),
                ("obd_1", new Vector3(156f, 3f, 26f), new Vector3(208f, 8f, 26f)),
                ("obd_2", new Vector3(182f, 2.5f, -60f), new Vector3(182f, 15f, 0f)),
                ("connection", new Vector3(120f, 6f, 26f), new Vector3(190f, 8f, 26f)),
                ("obd_intersection", new Vector3(176f, 4f, 20f), new Vector3(188f, 8f, 32f)),
            };
            foreach (var (tag, pos, look) in shots)
            {
                cam.transform.position = pos; cam.transform.LookAt(look);
                var rt = new RenderTexture(1280, 720, 24);
                cam.targetTexture = rt;
                for (int i = 0; i < 6; i++) cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
                File.WriteAllBytes($"../evidence/captures/{prefix}_{tag}.png", tex.EncodeToPNG());
                RenderTexture.active = null; cam.targetTexture = null; rt.Release();
            }
            Debug.Log($"[ArtPass] matriz {prefix}: 7 capturas");
        }
        public static void CaptureBefore() => CaptureArtMatrix("before");
        public static void CaptureAfter() => CaptureArtMatrix("after");

        // ==================== CITY PACK ====================
        private const string CP_Models = "Assets/Game/Art/Environment/Modular/CityPack/Models";
        private const string CP_Tex = "Assets/Game/Art/Environment/Modular/CityPack/Textures";
        private const string CP_MatDir = "Assets/Game/Materials/CityPack";
        private const string CP_PrefabDir = "Assets/Game/Prefabs/Modular/CityPack";

        /// <summary>
        /// Prefabs do City Pack: converte os materiais importados do FBX
        /// (Standard shader) p/ URP Lit Mat_CP_*, preservando o atlas correto.
        /// Atlas por nome do material: city_classical/modern/props/veg/road/industrial.
        /// </summary>
        public static void RunCityPackImport()
        {
            Directory.CreateDirectory(CP_MatDir);
            Directory.CreateDirectory(CP_PrefabDir);
            AssetDatabase.Refresh();
            int done = 0;
            var log = new System.Text.StringBuilder();

            // Materiais URP por atlas (dia/normal — sem sufixo night/winter/Autumn)
            Material AtlasMat(string fam)
            {
                var path = $"{CP_MatDir}/Mat_CP_{fam}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{CP_Tex}/city_{fam}.tif");
                    if (tex != null) m.SetTexture("_BaseMap", tex);
                    m.SetFloat("_Smoothness", 0.2f);
                    AssetDatabase.CreateAsset(m, path);
                }
                return m;
            }
            string FamFor(string matName)
            {
                var n = matName.ToLowerInvariant();
                if (n.Contains("classical")) return "classical";
                if (n.Contains("modern")) return "modern";
                if (n.Contains("prop")) return "props";
                if (n.Contains("veg") || n.Contains("tree") || n.Contains("leaf")) return "veg";
                if (n.Contains("road")) return "road";
                if (n.Contains("industrial")) return "industrial";
                if (n.Contains("bannar") || n.Contains("banner")) return "props";
                return "classical"; // prédios clássicos são o default do whitelist
            }

            foreach (var f in Directory.GetFiles(CP_Models, "*.fbx")
                         .Concat(Directory.GetFiles(CP_Models, "*.FBX")).OrderBy(x => x))
            {
                var rel = f.Replace('\\', '/');
                var name = Path.GetFileNameWithoutExtension(rel);
                var imp = AssetImporter.GetAtPath(rel) as ModelImporter;
                if (imp != null && imp.materialImportMode == ModelImporterMaterialImportMode.None)
                { imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription; imp.SaveAndReimport(); }

                var src = AssetDatabase.LoadAssetAtPath<GameObject>(rel);
                if (src == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                go.name = name;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var arr = r.sharedMaterials;
                    for (int i = 0; i < arr.Length; i++)
                        arr[i] = arr[i] != null ? AtlasMat(FamFor(arr[i].name)) : arr[i];
                    r.sharedMaterials = arr;
                }
                PrefabUtility.SaveAsPrefabAsset(go, $"{CP_PrefabDir}/{name}.prefab");
                Object.DestroyImmediate(go);
                done++;
                log.Append($"{name} ");
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[CityPack] {done} prefabs");
        }

        public static void RunImportBatch()
        {
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(PrefabDir);
            AssetDatabase.Refresh();

            // 1. Materiais Mat_DT_<família> com _BaseMap + _BumpMap do set
            var fams = new HashSet<string>();
            var slotNames = new HashSet<string>();
            var mats = new Dictionary<string, Material>();
            Material GetMat(string slot)
            {
                var fam = FamilyFor(slot);
                if (mats.TryGetValue(fam, out var m)) return m;
                var path = $"{MatDir}/Mat_DT_{fam}.mat";
                m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_{fam}_BaseColor.png");
                    if (tex == null) tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_{fam}.png");
                    if (tex != null) m.SetTexture("_BaseMap", tex);
                    var nrm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/T_{fam}_Normal.png");
                    if (nrm != null)
                    {
                        var ti = AssetImporter.GetAtPath($"{TexDir}/T_{fam}_Normal.png") as TextureImporter;
                        if (ti != null && ti.textureType != TextureImporterType.NormalMap)
                        { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
                        m.SetTexture("_BumpMap", nrm);
                        m.EnableKeyword("_NORMALMAP");
                    }
                    m.SetFloat("_Smoothness", fam.Contains("interior") || fam.Contains("Glass") ? 0.6f : 0.25f);
                    AssetDatabase.CreateAsset(m, path);
                }
                fams.Add(fam);
                mats[fam] = m;
                return m;
            }

            // 2. Prefab por FBX — remapeia slots pelo NOME do material importado
            int done = 0;
            foreach (var f in Directory.GetFiles(ModelsDir, "*.fbx").OrderBy(x => x))
            {
                var rel = f.Replace('\\', '/');
                var name = Path.GetFileNameWithoutExtension(rel);
                var imp = AssetImporter.GetAtPath(rel) as ModelImporter;
                if (imp != null && imp.materialImportMode == ModelImporterMaterialImportMode.None)
                {
                    // importar materiais p/ carregar os NOMES MI_* nos slots
                    imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                    imp.SaveAndReimport();
                }

                var src = AssetDatabase.LoadAssetAtPath<GameObject>(rel);
                if (src == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                go.name = name;

                // Remapear por NOME do slot importado (MI_*) → família Mat_DT_*
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    var arr = r.sharedMaterials;
                    for (int i = 0; i < arr.Length; i++)
                    {
                        var sn = arr[i] != null ? arr[i].name : r.name;
                        slotNames.Add(sn);
                        arr[i] = GetMat(sn);
                    }
                    r.sharedMaterials = arr;
                }
                PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
                Object.DestroyImmediate(go);
                done++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log($"[MegaKit] {done} prefabs, famílias: {string.Join(", ", fams)}");
        }
    }
}
