using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
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
            public string source_mode;
            public string source_glb;
        }

        [Serializable]
        public class ImportReport
        {
            public string generatedAt = DateTime.UtcNow.ToString("o");
            public int assetsProcessed;
            public List<string> materials = new List<string>();
            public List<string> prefabs = new List<string>();
            public List<string> errors = new List<string>();
        }

        [MenuItem("Studio/Fab3D/Import Generated Assets")]
        public static void MenuRun()
        {
            var report = ImportAll();
            Debug.Log($"[Fab3dImporter] Concluido: assets={report.assetsProcessed} materiais={report.materials.Count} prefabs={report.prefabs.Count} erros={report.errors.Count}");
        }

        /// <summary>Entrada batchmode: Unity -batchmode -quit -executeMethod Studio.Editor.Fab3dImporter.RunImportBatch</summary>
        public static void RunImportBatch()
        {
            var report = ImportAll();
            Debug.Log($"[Fab3dImporter] Concluido: assets={report.assetsProcessed} materiais={report.materials.Count} prefabs={report.prefabs.Count} erros={report.errors.Count}");
            EditorApplication.Exit(report.errors.Count == 0 && report.assetsProcessed > 0 ? 0 : 1);
        }

        public static ImportReport ImportAll()
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
                    ImportOne(Path.GetDirectoryName(manifestPath), report);
                }
                catch (Exception e)
                {
                    report.errors.Add($"{Path.GetDirectoryName(manifestPath)}: {e.Message}");
                }
            }

            WriteReport(report);
            return report;
        }

        private static void ImportOne(string folder, ImportReport report)
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

            // Prefab com material aplicado + colisor por categoria
            GameObject src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (src == null)
            {
                report.errors.Add($"{name}: FBX não importável pelo Unity ({fbxPath})");
                return;
            }
            GameObject go = (GameObject)UnityEngine.Object.Instantiate(src);
            var renderers = go.GetComponentsInChildren<MeshRenderer>(true);
            foreach (var r in renderers)
            {
                r.sharedMaterial = mat;
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
