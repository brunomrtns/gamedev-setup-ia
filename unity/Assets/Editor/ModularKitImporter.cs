using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Studio.Editor
{
    /// <summary>
    /// ModularKitImporter V2 — Gera materiais URP Lit (PBR) e prefabs catalogados a partir dos
    /// kits modulares CC0 (Assets/Game/Art/Environment/Modular) e texturas Poly Haven
    /// (Assets/Game/Art/Common/Textures). Quality Gate V2: nenhuma cena pode usar geometria
    /// primitiva nao catalogada no lugar destes prefabs.
    /// </summary>
    public static class ModularKitImporter
    {
        private const string ModularRoot = "Assets/Game/Art/Environment/Modular";
        private const string CommonTexturesRoot = "Assets/Game/Art/Common/Textures";
        private const string MaterialsDir = "Assets/Game/Materials/Modular";
        private const string PrefabsDir = "Assets/Game/Prefabs/Modular";
        private const string ReportPath = "../evidence/art/modular-import-report.json";

        public static void RunImportBatch()
        {
            var report = ImportAll();
            Debug.Log($"[ModularKitImporter] Concluido: materiais={report.materialCount} prefabs={report.prefabCount} erros={report.errors.Count}");
            EditorApplication.Exit(report.errors.Count == 0 && report.prefabCount > 0 ? 0 : 1);
        }

        [Serializable]
        public class ImportReport
        {
            public string generatedAt = DateTime.UtcNow.ToString("o");
            public int materialCount;
            public int prefabCount;
            public List<string> materials = new List<string>();
            public List<string> prefabs = new List<string>();
            public List<string> errors = new List<string>();
        }

        public static ImportReport ImportAll()
        {
            var report = new ImportReport();
            Directory.CreateDirectory(MaterialsDir);
            Directory.CreateDirectory(PrefabsDir);
            AssetDatabase.Refresh();
            // ---- 1. Materiais PBR Poly Haven (familias 1024; variantes _512 ficam como alternativos)
            foreach (var famDir in Directory.GetDirectories(CommonTexturesRoot).OrderBy(d => d))
            {
                var famName = Path.GetFileName(famDir);
                if (famName.EndsWith("_512")) continue;
                try
                {
                    var mat = CreatePbrMaterial(famName, famDir);
                    if (mat != null) report.materials.Add(mat);
                }
                catch (Exception e)
                {
                    report.errors.Add($"Material {famName}: {e.Message}");
                }
            }

            // ---- 2. Material base por kit (colormap do kit quando existir, senao MetalPlate PBR)
            var fallbackMat = LoadOrCreateFallback("Mat_Modular_GenericSciFi", "MetalPlate");

            // ---- 3. Prefabs por peca modular
            foreach (var kitDir in Directory.GetDirectories(ModularRoot).OrderBy(d => d))
            {
                var kitName = Path.GetFileName(kitDir);
                var modelsDir = Path.Combine(kitDir, "Models");
                if (!Directory.Exists(modelsDir)) continue;
                var kitMat = CreateKitMaterial(kitName, kitDir) ?? fallbackMat;

                foreach (var modelFile in Directory.GetFiles(modelsDir, "*.fbx")
                             .Concat(Directory.GetFiles(modelsDir, "*.obj"))
                             .Concat(Directory.GetFiles(modelsDir, "*.glb"))
                             .OrderBy(p => p))
                {
                    try
                    {
                        var prefabPath = CreateKitPrefab(modelFile, kitMat);
                        if (!string.IsNullOrEmpty(prefabPath)) report.prefabs.Add(prefabPath);
                    }
                    catch (Exception e)
                    {
                        report.errors.Add($"Prefab {modelFile}: {e.Message}");
                    }
                }
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            WriteReport(report);
            return report;
        }

        private static string ToAssetPath(string fsPath)
        {
            var full = Path.GetFullPath(fsPath).Replace('\\', '/');
            var dataPath = Application.dataPath.Replace('\\', '/');
            if (!full.StartsWith(dataPath)) return null;
            return "Assets" + full.Substring(dataPath.Length);
        }

        private static string CreatePbrMaterial(string famName, string famDir)
        {
            var assetPath = $"{MaterialsDir}/Mat_PBR_{famName}.mat";
            Texture2D FindTex(string pattern)
            {
                var match = Directory.GetFiles(famDir, pattern).FirstOrDefault();
                if (match == null) return null;
                var rel = ToAssetPath(match);
                return rel == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(rel);
            }

            var albedo = FindTex($"{famName}_D.*");
            var normal = FindTex($"{famName}_N.*");
            var metallicSmooth = FindTex($"{famName}_MS.*");
            if (albedo == null)
            {
                Debug.LogWarning($"[ModularKitImporter] Familia {famName} sem Albedo — material ignorado.");
                return null;
            }

            var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, assetPath);
            }

            mat.SetTexture("_BaseMap", albedo);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (metallicSmooth != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallicSmooth);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            }
            mat.SetFloat("_Smoothness", metallicSmooth != null ? 1f : 0.5f);
            mat.SetFloat("_Metallic", metallicSmooth != null ? 1f : 0f);
            mat.SetFloat("_BumpScale", 1f);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(mat);
            return assetPath;
        }

        private static Material LoadOrCreateFallback(string matName, string famName)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/Mat_PBR_{famName}.mat");
            if (existing != null) return existing;
            CreatePbrMaterial(famName, Path.Combine(Application.dataPath, "Game/Art/Common/Textures", famName));
            return AssetDatabase.LoadAssetAtPath<Material>($"{MaterialsDir}/Mat_PBR_{famName}.mat");
        }

        private static Material CreateKitMaterial(string kitName, string kitDir)
        {
            // Colormap principal do kit (paleta CC0 estilizada); fallback para PBR generico
            var texDir = Path.Combine(kitDir, "Textures");
            string colormap = null;
            if (Directory.Exists(texDir))
            {
                colormap = Directory.GetFiles(texDir, "colormap*.png").FirstOrDefault()
                           ?? Directory.GetFiles(texDir, "*.png").OrderBy(f => f.Length).FirstOrDefault();
            }

            var assetPath = $"{MaterialsDir}/Mat_Kit_{kitName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(mat, assetPath);
            }

            if (colormap != null)
            {
                var rel = ToAssetPath(colormap);
                var tex = rel == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(rel);
                if (tex != null)
                {
                    mat.SetTexture("_BaseMap", tex);
                    mat.SetFloat("_Smoothness", 0.35f);
                    mat.SetFloat("_Metallic", 0.1f);
                }
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static string CreateKitPrefab(string modelFile, Material kitMat)
        {
            var modelName = Path.GetFileNameWithoutExtension(modelFile);
            var relModel = ToAssetPath(modelFile);
            if (relModel == null)
            {
                Debug.LogWarning($"[ModularKitImporter] Modelo fora de Assets: {modelFile}");
                return null;
            }
            var prefabPath = $"{PrefabsDir}/{modelName}.prefab";

            var source = AssetDatabase.LoadAssetAtPath<GameObject>(relModel);
            if (source == null)
            {
                Debug.LogWarning($"[ModularKitImporter] Modelo nao carregou: {relModel}");
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
            if (instance == null) instance = UnityEngine.Object.Instantiate(source);

            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterial = kitMat;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            if (instance.GetComponent<Collider>() == null)
            {
                var box = instance.AddComponent<BoxCollider>();
                var bounds = CalculateLocalBounds(instance);
                box.center = bounds.center;
                box.size = bounds.size;
            }

            PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
            UnityEngine.Object.DestroyImmediate(instance);
            return prefabPath;
        }

        private static Bounds CalculateLocalBounds(GameObject go)
        {
            var combined = new Bounds();
            var first = true;
            foreach (var filter in go.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var b = new Bounds(filter.sharedMesh.bounds.center, filter.sharedMesh.bounds.size);
                var lp = filter.transform.localPosition;
                var ls = filter.transform.localScale;
                b.center = new Vector3(b.center.x * ls.x + lp.x, b.center.y * ls.y + lp.y, b.center.z * ls.z + lp.z);
                b.size = Vector3.Scale(b.size, ls);
                if (first) { combined = b; first = false; }
                else combined.Encapsulate(b);
            }
            if (first) combined = new Bounds(Vector3.zero, Vector3.one);
            return combined;
        }

        private static void WriteReport(ImportReport report)
        {
            report.materialCount = report.materials.Count;
            report.prefabCount = report.prefabs.Count;
            var dir = Path.GetDirectoryName(Path.GetFullPath(ReportPath));
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine($"  \"generatedAt\": \"{report.generatedAt}\",");
            sb.AppendLine($"  \"materialCount\": {report.materialCount},");
            sb.AppendLine($"  \"prefabCount\": {report.prefabCount},");
            sb.AppendLine("  \"materials\": [");
            sb.AppendLine(string.Join(",\n", report.materials.Select(m => $"    \"{m}\"")));
            sb.AppendLine("  ],");
            sb.AppendLine("  \"prefabs\": [");
            sb.AppendLine(string.Join(",\n", report.prefabs.Select(p => $"    \"{p}\"")));
            sb.AppendLine("  ],");
            sb.AppendLine("  \"errors\": [");
            sb.AppendLine(string.Join(",\n", report.errors.Select(e => $"    \"{e.Replace("\"", "'")}\"")));
            sb.AppendLine("  ]");
            sb.AppendLine("}");
            File.WriteAllText(ReportPath, sb.ToString());
            Debug.Log($"[ModularKitImporter] Relatorio: {Path.GetFullPath(ReportPath)}");
        }
    }
}
