using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// Fab3dImporter — Importa assets gerados pela ponte Fábrica 3D (ADR-005) a partir de
    /// Assets/Game/Art/Generated/<nome>/ (FBX canônico + texturas + fab3d_manifest.json).
    /// Cria texturas ASTC por categoria, material URP Lit (BaseColor -> _BaseMap;
    /// MetallicRoughness -> _MetallicGlossMap com B->metallic, 1-G->smoothness), prefab
    /// com colisor e relatório em evidence/art/fab3d-import-report.json.
    /// Quality Gate V2: mesma disciplina do ModularKitImporter (sem primitivas, material com _BaseMap).
    /// </summary>
    public static class Fab3dImporter
    {
        private const string GeneratedRoot = "Assets/Game/Art/Generated";
        private const string MaterialsDir = "Assets/Game/Materials/Fab3D";
        private const string PrefabsDir = "Assets/Game/Prefabs/Fab3D";
        private const string ReportPath = "../evidence/art/fab3d-import-report.json";
        private static readonly string[] UalAnimationPaths =
        {
            "Assets/Game/Art/Characters/Quaternius/Animations/UAL1_Standard.fbx",
            "Assets/Game/Art/Characters/Quaternius/Animations/UAL2_Standard.fbx",
        };

        /// <summary>Espelho exato de fab3d_manifest.json (campos snake_case vêm do fab3d_bridge.py).</summary>
        [Serializable]
        public class Fab3dManifest
        {
            public string asset_name;
            public string category;
            public string preset;
            public int texture_size;
            public string astc;
            public string collider;
            public int max_tris;
            public int tris_final;
            public bool rigged;
            public string source_mode;
            public string source_glb;
        }

        [Serializable]
        public class ImportReport
        {
            public string generatedAt = DateTime.UtcNow.ToString("o");
            public int assetsProcessed;
            public int animationsImported;
            public int humanoidAvatarsValid;
            public int ualClipsBound;
            public List<string> materials = new List<string>();
            public List<string> prefabs = new List<string>();
            public List<string> captures = new List<string>();
            public List<string> errors = new List<string>();
        }

        [MenuItem("Studio/Fab3D/Import Generated Assets")]
        public static void MenuRun()
        {
            var report = ImportAll(autoCapture: true);
            Debug.Log($"[Fab3dImporter] Concluido: assets={report.assetsProcessed} materiais={report.materials.Count} prefabs={report.prefabs.Count} captures={report.captures.Count} erros={report.errors.Count}");
        }

        /// <summary>Entrada batchmode: Unity -batchmode -quit -executeMethod Studio.Editor.Fab3dImporter.RunImportBatch</summary>
        public static void RunImportBatch()
        {
            bool skipCapture = Environment.GetCommandLineArgs().Any(a => a == "-skipPreviewCapture");
            var report = ImportAll(autoCapture: !skipCapture);
            Debug.Log($"[Fab3dImporter] Concluido: assets={report.assetsProcessed} materiais={report.materials.Count} prefabs={report.prefabs.Count} captures={report.captures.Count} erros={report.errors.Count}");
            EditorApplication.Exit(report.errors.Count == 0 && report.assetsProcessed > 0 ? 0 : 1);
        }

        /// <summary>Captura evidencia visual de um prefab Fab3D com nome datado em evidence/captures/ sem alterar a cena em disco.</summary>
        public static void CapturePreviewBatch()
        {
            string assetName = null;
            foreach (var arg in Environment.GetCommandLineArgs())
            {
                if (arg.StartsWith("-fab3dAsset="))
                    assetName = arg.Substring("-fab3dAsset=".Length);
            }
            if (string.IsNullOrEmpty(assetName))
            {
                var prefabs = Directory.Exists(PrefabsDir) ? Directory.GetFiles(PrefabsDir, "*.prefab") : Array.Empty<string>();
                if (prefabs.Length == 0)
                {
                    Debug.LogError("[Fab3dImporter] Nenhum prefab em " + PrefabsDir);
                    EditorApplication.Exit(1);
                    return;
                }
                assetName = Path.GetFileNameWithoutExtension(prefabs[0]);
            }

            string datedOutPath = CapturePreviewForAsset(assetName, out string err);
            if (string.IsNullOrEmpty(datedOutPath))
            {
                Debug.LogError("[Fab3dImporter] " + err);
                EditorApplication.Exit(1);
                return;
            }
            EditorApplication.Exit(0);
        }

        public static string CapturePreviewForAsset(string assetName, out string error)
        {
            error = null;
            string prefabPath = $"{PrefabsDir}/{assetName}.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                error = "Prefab nao encontrado: " + prefabPath;
                return null;
            }

            // Lei de Higiene 6a: captura de ferramenta/gate usa SEMPRE Calibration.unity (ou cena neutra). Zero cenario de outros jogos.
            string calibScene = "Assets/Game/Scenes/Calibration.unity";
            Vector3 pos = Vector3.zero;
            if (File.Exists(calibScene))
            {
                EditorSceneManager.OpenScene(calibScene, OpenSceneMode.Single);
                pos = new Vector3(0f, 0.0f, 0f);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                pos = Vector3.zero;
            }

            GameObject inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            inst.transform.position = pos;
            inst.transform.rotation = Quaternion.Euler(0f, 35f, 0f);

            var renderers = inst.GetComponentsInChildren<Renderer>(true);
            Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(pos + Vector3.up, Vector3.one);
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                cam = new GameObject("Fab3dPreviewCam").AddComponent<Camera>();
            }
            float dist = Mathf.Max(b.size.magnitude * 1.45f, 3.0f);
            cam.transform.position = b.center + new Vector3(-dist * 0.65f, b.size.y * 0.55f, dist * 0.85f);
            cam.transform.LookAt(b.center + Vector3.up * (b.size.y * 0.05f));

            // Warm-up URP (3 frames)
            for (int w = 0; w < 3; w++)
            {
                var wrt = RenderTexture.GetTemporary(256, 144, 24);
                cam.targetTexture = wrt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(wrt);
            }

            int width = 1280, height = 720;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);

            byte[] pngBytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);
            UnityEngine.Object.DestroyImmediate(inst);

            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures");
            Directory.CreateDirectory(outDir);
            string stamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");
            string datedPath = Path.Combine(outDir, $"fab3d_{assetName}_{stamp}.png");
            string latestPath = Path.Combine(outDir, $"fab3d_{assetName}.png");
            File.WriteAllBytes(datedPath, pngBytes);
            File.WriteAllBytes(latestPath, pngBytes);

            Debug.Log($"[Fab3dImporter] Captura datada salva em: {datedPath} (bounds={b.size})");
            return datedPath;
        }

        public static ImportReport ImportAll(bool autoCapture = true)
        {
            var report = new ImportReport();
            EnsureFolder("Assets/Game/Materials");
            EnsureFolder(MaterialsDir);
            EnsureFolder("Assets/Game/Prefabs");
            EnsureFolder(PrefabsDir);
            AssetDatabase.Refresh();

            if (!AssetDatabase.IsValidFolder(GeneratedRoot))
            {
                report.errors.Add($"Pasta ausente: {GeneratedRoot}");
                WriteReport(report);
                return report;
            }

            var manifests = Directory.GetFiles(GeneratedRoot, "fab3d_manifest.json", SearchOption.AllDirectories);
            if (manifests.Length == 0)
            {
                report.errors.Add("Nenhum fab3d_manifest.json encontrado (rode fab3d_bridge.py primeiro)");
                WriteReport(report);
                return report;
            }

            foreach (var manifestPath in manifests)
            {
                try
                {
                    ImportOne(Path.GetDirectoryName(manifestPath), report, autoCapture);
                }
                catch (Exception e)
                {
                    report.errors.Add($"{Path.GetDirectoryName(manifestPath)}: {e.Message}");
                }
            }

            WriteReport(report);
            return report;
        }

        private static void ImportOne(string folder, ImportReport report, bool autoCapture = true)
        {
            var manifest = JsonUtility.FromJson<Fab3dManifest>(File.ReadAllText(Path.Combine(folder, "fab3d_manifest.json")));
            string name = manifest.asset_name;
            string fbxPath = $"{folder}/{name}.fbx".Replace('\\', '/');
            string baseColorPath = $"{folder}/{name}_basecolor.png".Replace('\\', '/');
            string mrPath = $"{folder}/{name}_mr.png".Replace('\\', '/');

            if (!File.Exists(fbxPath))
            {
                report.errors.Add($"{name}: FBX ausente ({fbxPath})");
                return;
            }

            Texture2D baseTex = ConfigureTexture(baseColorPath, manifest, srgb: true, readable: false);
            if (baseTex == null)
            {
                report.errors.Add($"{name}: BaseColor ausente ({baseColorPath})");
                return;
            }

            Texture2D metallicGloss = null;
            if (File.Exists(mrPath))
            {
                metallicGloss = BuildMetallicGloss(mrPath, manifest, MaterialsDir, name);
            }
            else
            {
                Debug.LogWarning($"[Fab3dImporter] {name}: MR ausente, material só com Albedo");
            }

            // Material URP Lit canônico (Quality Gate V2: _BaseMap obrigatório)
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null)
            {
                report.errors.Add($"{name}: shader URP Lit não encontrado");
                return;
            }
            var mat = new Material(lit);
            mat.SetTexture("_BaseMap", baseTex);
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Metallic", metallicGloss != null ? 1f : 0.05f);
            mat.SetFloat("_Smoothness", 1f);
            if (metallicGloss != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallicGloss);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            string matPath = $"{MaterialsDir}/Mat_Fab3D_{name}.mat";
            AssetDatabase.CreateAsset(mat, matPath);
            report.materials.Add(matPath);

            // Configura rig/animacao quando personagem rigado (Fase 5 + Fase 5.1-A Humanoid + UAL)
            bool isHumanoidValid = false;
            if (manifest.rigged)
            {
                var modelImp = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
                if (modelImp != null)
                {
                    modelImp.animationType = ModelImporterAnimationType.Human;
                    modelImp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                    modelImp.importAnimation = true;
                    modelImp.SaveAndReimport();

                    var avatar = AssetDatabase.LoadAssetAtPath<Avatar>(fbxPath);
                    if (avatar != null && avatar.isValid && avatar.isHuman)
                    {
                        isHumanoidValid = true;
                        report.humanoidAvatarsValid++;
                    }
                    else
                    {
                        Debug.LogWarning($"[Fab3dImporter] {name}: Avatar Humanoid invalido — fallback para Generic");
                        modelImp.animationType = ModelImporterAnimationType.Generic;
                        modelImp.SaveAndReimport();
                    }
                }
                foreach (var sub in AssetDatabase.LoadAllAssetsAtPath(fbxPath))
                {
                    if (sub is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                        report.animationsImported++;
                }
            }

            // Prefab com material aplicado (MeshRenderer + SkinnedMeshRenderer) + colisor por categoria
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (src == null)
            {
                report.errors.Add($"{name}: FBX não importável pelo Unity ({fbxPath})");
                return;
            }
            GameObject go = (GameObject)UnityEngine.Object.Instantiate(src);
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = mat;
            }

            // Se Humanoid valido, conecta AnimatorController com clips Quaternius UAL (Fase 5.1-A)
            if (isHumanoidValid)
            {
                var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
                animator.avatar = AssetDatabase.LoadAssetAtPath<Avatar>(fbxPath);
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                int ualCount;
                var ctrl = EnsureUalHumanoidController(out ualCount);
                if (ctrl != null)
                {
                    animator.runtimeAnimatorController = ctrl;
                    report.ualClipsBound = ualCount;
                }
            }

            if (manifest.collider == "box" && renderers.Length > 0)
            {
                var bounds = renderers[0].bounds;
                for (int i = 1; i < renderers.Length; i++)
                {
                    bounds.Encapsulate(renderers[i].bounds);
                }
                var box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(bounds.center);
                box.size = bounds.size;
            }

            string prefabPath = $"{PrefabsDir}/{name}.prefab";
            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            UnityEngine.Object.DestroyImmediate(go);
            report.prefabs.Add(prefabPath);
            report.assetsProcessed++;

            if (autoCapture)
            {
                AssetDatabase.SaveAssets();
                string datedCap = CapturePreviewForAsset(name, out string capErr);
                if (!string.IsNullOrEmpty(datedCap))
                {
                    report.captures.Add(datedCap);
                }
                else
                {
                    report.errors.Add($"{name}: falha na captura automatica pos-import ({capErr})");
                }
            }

            Debug.Log($"[Fab3dImporter] {name}: cat={manifest.category} tris={manifest.tris_final}/{manifest.max_tris} tex={manifest.texture_size}px {manifest.astc} collider={manifest.collider}");
        }

        private static Texture2D ConfigureTexture(string path, Fab3dManifest manifest, bool srgb, bool readable)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
            if (importer == null)
            {
                return null;
            }
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = srgb;
            importer.mipmapEnabled = true;
            importer.isReadable = readable;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.maxTextureSize = manifest.texture_size;
            var android = new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = manifest.texture_size,
                format = manifest.astc == "ASTC_4x4" ? TextureImporterFormat.ASTC_4x4 : TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.Compressed,
            };
            importer.SetPlatformTextureSettings(android);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path.Replace('\\', '/'));
        }

        /// <summary>Converte o PNG MetallicRoughness (G=roughness, B=metallic) para o formato
        /// Unity MetallicGloss (R=metallic, A=smoothness=1-roughness) e importa como asset linear.</summary>
        private static Texture2D BuildMetallicGloss(string mrPath, Fab3dManifest manifest, string outDir, string name)
        {
            Texture2D mr = ConfigureTexture(mrPath, manifest, srgb: false, readable: true);
            if (mr == null)
            {
                return null;
            }
            Color32[] px = mr.GetPixels32();
            var mapped = new Color32[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                mapped[i] = new Color32(px[i].b, 0, 0, (byte)(255 - px[i].g));
            }
            var ms = new Texture2D(mr.width, mr.height, TextureFormat.RGBA32, mipChain: false, linear: true);
            ms.SetPixels32(mapped);
            ms.Apply();

            string outPath = $"{outDir}/{name}_metallicsmooth.png";
            File.WriteAllBytes(outPath, ms.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(ms);
            AssetDatabase.ImportAsset(outPath);

            var imp = (TextureImporter)AssetImporter.GetAtPath(outPath);
            imp.sRGBTexture = false;
            imp.mipmapEnabled = false;
            imp.textureCompression = TextureImporterCompression.Compressed;
            imp.maxTextureSize = manifest.texture_size;
            imp.SaveAndReimport();

            // MR não precisa mais ficar readable em runtime
            var mrImp = (TextureImporter)AssetImporter.GetAtPath(mrPath.Replace('\\', '/'));
            mrImp.isReadable = false;
            mrImp.SaveAndReimport();

            return AssetDatabase.LoadAssetAtPath<Texture2D>(outPath);
        }

        private static RuntimeAnimatorController EnsureUalHumanoidController(out int clipsBound)
        {
            clipsBound = 0;
            const string ctrlPath = PrefabsDir + "/Fab3D_Humanoid_UAL.controller";

            AnimationClip FindUal(params string[] names)
            {
                foreach (var target in names)
                {
                    foreach (var p in UalAnimationPaths)
                    {
                        if (!File.Exists(p)) continue;
                        foreach (var a in AssetDatabase.LoadAllAssetsAtPath(p))
                        {
                            if (a is AnimationClip c && c.name == target && !c.name.StartsWith("__preview"))
                                return c;
                        }
                    }
                }
                return null;
            }

            var idle = FindUal("Idle_Loop", "Idle");
            var walk = FindUal("Walk_Loop", "Walk");
            var run = FindUal("Sprint_Loop", "Jog_Fwd_Loop", "Run_Loop");
            var jump = FindUal("Jump_Start", "Jump_Loop", "Jump");
            var hit = FindUal("Hit_Chest", "Hit_A", "Crouch_Idle_Loop");

            var found = new List<(string state, AnimationClip clip)>();
            if (idle != null) found.Add(("Idle", idle));
            if (walk != null) found.Add(("Walk", walk));
            if (run != null) found.Add(("Run", run));
            if (jump != null) found.Add(("Jump", jump));
            if (hit != null) found.Add(("Hit", hit));
            clipsBound = found.Count;
            if (clipsBound == 0) return null;

            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath) != null)
                AssetDatabase.DeleteAsset(ctrlPath);

            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Jump", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Hit", AnimatorControllerParameterType.Trigger);
            var sm = ctrl.layers[0].stateMachine;
            AnimatorState defaultState = null;
            foreach (var (stateName, clip) in found)
            {
                var st = sm.AddState(stateName);
                st.motion = clip;
                if (defaultState == null) defaultState = st;
            }
            sm.defaultState = defaultState;
            AssetDatabase.SaveAssets();
            return ctrl;
        }

        private static void EnsureFolder(string path)
        {
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = $"{current}/{parts[i]}";
                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, parts[i]);
                }
                current = next;
            }
        }

        private static void WriteReport(ImportReport report)
        {
            Directory.CreateDirectory("../evidence/art");
            File.WriteAllText(ReportPath, JsonUtility.ToJson(report, prettyPrint: true));
        }
    }
}
