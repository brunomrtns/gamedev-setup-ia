using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Studio.Editor
{
    /// <summary>
    /// Pipeline definitivo de pedestres — Quaternius CC0:
    /// modelos Superhero_*_FullBody (Humanoid, avatar próprio) + clips da
    /// Universal Animation Library (UAL1/UAL2, Humanoid CopyFromOther).
    /// Esqueleto limpo: 1 armature + SMRs nativos — ZERO rebind/ossos manuais.
    /// </summary>
    public static class QuaterniusPedPipeline
    {
        private const string DIR = "Assets/Game/Art/Characters/Quaternius";
        private const string MODELS = DIR + "/Models";
        private const string ANIMS = DIR + "/Animations";
        private const string TEXS = DIR + "/Textures";
        private const string MATS = DIR + "/Materials";
        private const string PEDS = "Assets/Game/Resources/Art/Pedestrians";

        private static readonly string[] UAL_PATHS =
        {
            ANIMS + "/UAL1_Standard.fbx",
            ANIMS + "/UAL2_Standard.fbx",
        };

        /// <summary>Idempotente — chamado pelo scene builder e por -executeMethod.</summary>
        public static void RunPipeline()
        {
            ImportModels();
            var avatar = ImportAnimations();
            var mats = CreateMaterials();
            BuildPrefabs(mats);
            RebuildController();
            AssetDatabase.SaveAssets();
            Debug.Log("[Quaternius] pipeline concluído — prefabs quat_* + controller");
        }

        /// <summary>Ponto de entrada batch: -executeMethod Studio.Editor.QuaterniusPedPipeline.RunPipelineBatch</summary>
        public static void RunPipelineBatch() => RunPipeline();

        // ------------------------------------------------------------------
        // 1. Modelos base: Humanoid + CreateFromThisModel
        // ------------------------------------------------------------------
        private static void ImportModels()
        {
            foreach (var f in Directory.GetFiles(MODELS, "*.fbx"))
            {
                var path = f.Replace('\\', '/');
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) continue;
                bool isBody = path.Contains("Superhero") || path.Contains("Mannequin");
                if (isBody)
                {
                    imp.animationType = ModelImporterAnimationType.Human;
                    imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                }
                else
                {
                    // cabelos/sobrancelhas rigged to head bone — sem avatar próprio
                    imp.animationType = ModelImporterAnimationType.None;
                }
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.SaveAndReimport();
            }
            Debug.Log("[Quaternius] modelos importados (Humanoid nos corpos)");
        }

        // ------------------------------------------------------------------
        // 2. Animações UAL: Humanoid + CopyFromOther (avatar do modelo base)
        // ------------------------------------------------------------------
        private static Avatar ImportAnimations()
        {
            Avatar avatar = null;
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(
                MODELS + "/Superhero_Male_FullBody.fbx"))
                if (a is Avatar av) { avatar = av; break; }
            if (avatar == null)
            {
                Debug.LogError("[Quaternius] avatar do modelo base não encontrado");
                return null;
            }
            foreach (var path in UAL_PATHS)
            {
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (imp == null) { Debug.LogWarning("[Quaternius] ausente: " + path); continue; }
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
                imp.sourceAvatar = avatar;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                var defs = imp.defaultClipAnimations;
                foreach (var d in defs)
                {
                    // "Armature|Walk_Loop" → "Walk_Loop"
                    var tn = d.takeName;
                    int bar = tn.LastIndexOf('|');
                    d.name = bar >= 0 ? tn.Substring(bar + 1) : tn;
                    bool loop = d.name.EndsWith("_Loop");
                    d.loopTime = loop;
                    // bake into pose: pos Y + orientação fixos (in-place)
                    d.lockRootHeightY = true;
                    d.lockRootRotation = true;
                    d.keepOriginalPositionY = true;
                    d.keepOriginalPositionXZ = true;
                    d.heightFromFeet = true;
                }
                imp.clipAnimations = defs;
                imp.SaveAndReimport();
                int n = AssetDatabase.LoadAllAssetsAtPath(path)
                    .Count(a => a is AnimationClip && !a.name.StartsWith("__preview"));
                Debug.Log($"[Quaternius] {Path.GetFileName(path)}: {n} clips importados");
            }
            return avatar;
        }

        // ------------------------------------------------------------------
        // 3. Materiais URP Lit com texturas do pack
        // ------------------------------------------------------------------
        private static Dictionary<string, Material> CreateMaterials()
        {
            if (!Directory.Exists(MATS)) Directory.CreateDirectory(MATS);
            // normal maps: forçar TextureType=NormalMap
            foreach (var f in Directory.GetFiles(TEXS, "*_Normal.png"))
            {
                var p = f.Replace('\\', '/');
                var ti = AssetImporter.GetAtPath(p) as TextureImporter;
                if (ti != null && ti.textureType != TextureImporterType.NormalMap)
                { ti.textureType = TextureImporterType.NormalMap; ti.SaveAndReimport(); }
            }
            var mats = new Dictionary<string, Material>();
            Material Mat(string key, string texName, string normalName = null)
            {
                var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TEXS}/{texName}");
                if (tex != null) m.SetTexture("_BaseMap", tex);
                if (normalName != null)
                {
                    var n = AssetDatabase.LoadAssetAtPath<Texture2D>($"{TEXS}/{normalName}");
                    if (n != null) { m.SetTexture("_BumpMap", n); m.EnableKeyword("_NORMALMAP"); }
                }
                m.SetFloat("_Smoothness", 0.35f);
                AssetDatabase.CreateAsset(m, $"{MATS}/Mat_Quat_{key}.mat");
                mats[key] = m;
                return m;
            }
            Mat("Male_Light", "T_Superhero_Male_Ligh.png", "T_Superhero_Male_Normal.png");
            Mat("Male_Dark", "T_Superhero_Male_Dark.png", "T_Superhero_Male_Normal.png");
            Mat("Female_Light", "T_Superhero_Female_Light_BaseColor.png", "T_Superhero_Female_Normal.png");
            Mat("Female_Dark", "T_Superhero_Female_Dark_BaseColor.png", "T_Superhero_Female_Normal.png");
            Mat("Hair_1", "T_Hair_1_BaseColor.png", "T_Hair_1_Normal.png");
            Mat("Hair_2", "T_Hair_2_BaseColor.png", "T_Hair_2_Normal.png");
            Mat("Eye", "T_Eye_Brown.png", "T_Eye_Normal.png");
            return mats;
        }

        // ------------------------------------------------------------------
        // 4. Prefabs quat_* em Resources/Art/Pedestrians
        // ------------------------------------------------------------------
        private static void BuildPrefabs(Dictionary<string, Material> mats)
        {
            var defs = new (string name, string model, string matKey, string hair, string hairMat)[]
            {
                ("quat_male_a", "Superhero_Male_FullBody.fbx", "Male_Light", null, null),
                ("quat_male_b", "Superhero_Male_FullBody.fbx", "Male_Dark", "Hair_Buzzed.fbx", "Hair_1"),
                ("quat_male_c", "Superhero_Male_FullBody.fbx", "Male_Light", "Hair_Beard.fbx", "Hair_2"),
                ("quat_female_a", "Superhero_Female_FullBody.fbx", "Female_Light", "Hair_Buns.fbx", "Hair_1"),
                ("quat_female_b", "Superhero_Female_FullBody.fbx", "Female_Dark", "Hair_Long.fbx", "Hair_2"),
            };
            foreach (var (name, model, matKey, hair, hairMat) in defs)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>($"{MODELS}/{model}");
                if (src == null) { Debug.LogError("[Quaternius] modelo ausente: " + model); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                go.name = name;
                var body = mats[matKey];
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    var m = smr.name.Contains("Eye") ? mats["Eye"]
                        : smr.name.Contains("Hair") ? mats["Hair_1"] : body;
                    var arr = smr.sharedMaterials;
                    for (int i = 0; i < arr.Length; i++) arr[i] = m;
                    smr.sharedMaterials = arr;
                }
                // cabelo rigged-to-head: instancia sob o osso da cabeça
                if (hair != null)
                {
                    var head = FindBone(go.transform, "Head");
                    var hp = AssetDatabase.LoadAssetAtPath<GameObject>($"{MODELS}/{hair}");
                    if (head != null && hp != null)
                    {
                        var h = (GameObject)PrefabUtility.InstantiatePrefab(hp);
                        PrefabUtility.UnpackPrefabInstance(h, PrefabUnpackMode.Completely,
                            InteractionMode.AutomatedAction);
                        h.transform.SetParent(head, false);
                        h.transform.localPosition = Vector3.zero;
                        h.transform.localRotation = Quaternion.identity;
                        foreach (var smr in h.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        {
                            var arr = smr.sharedMaterials;
                            for (int i = 0; i < arr.Length; i++) arr[i] = mats[hairMat];
                            smr.sharedMaterials = arr;
                        }
                    }
                }
                // AlwaysAnimate: sem isso o Animator omite transform updates
                // fora da visão da câmera (ossos congelam em testes/long shot)
                var anim = go.GetComponentInChildren<Animator>(true);
                if (anim != null) anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                var outp = $"{PEDS}/{name}.prefab";
                PrefabUtility.SaveAsPrefabAsset(go, outp);
                Object.DestroyImmediate(go);
                Debug.Log("[Quaternius] prefab: " + outp);
            }
        }

        private static Transform FindBone(Transform root, string boneName)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == boneName || t.name.EndsWith(boneName)) return t;
            return null;
        }

        // ------------------------------------------------------------------
        // 5. Pedestrian.controller com clips UAL reais
        // ------------------------------------------------------------------
        private static void RebuildController()
        {
            const string ctrlPath = PEDS + "/Pedestrian.controller";
            AnimationClip Find(string clipName)
            {
                foreach (var p in UAL_PATHS)
                    foreach (var a in AssetDatabase.LoadAllAssetsAtPath(p))
                        if (a is AnimationClip c && c.name == clipName
                            && !c.name.StartsWith("__preview"))
                            return c;
                return null;
            }
            var idle = Find("Idle_Loop") ?? Find("Idle");
            var walk = Find("Walk_Loop") ?? Find("Walk");
            var run = Find("Sprint_Loop") ?? Find("Jog_Fwd_Loop");
            var death = Find("Death01") ?? Find("Death_A") ?? Find("Death");
            var cower = Find("Crouch_Idle_Loop") ?? Find("Hit_Chest") ?? Find("Hit_A");
            if (idle == null || walk == null || run == null || death == null)
            {
                Debug.LogError($"[Quaternius] clips faltando: idle={idle} walk={walk} run={run} death={death}");
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath) != null)
                AssetDatabase.DeleteAsset(ctrlPath);
            var ctrl = AnimatorController.CreateAnimatorControllerAtPath(ctrlPath);
            ctrl.AddParameter("Speed", AnimatorControllerParameterType.Float);
            ctrl.AddParameter("Dead", AnimatorControllerParameterType.Trigger);
            ctrl.AddParameter("Cowering", AnimatorControllerParameterType.Bool);
            var sm = ctrl.layers[0].stateMachine;
            var sIdle = sm.AddState("Idle"); sIdle.motion = idle;
            var sWalk = sm.AddState("Walk"); sWalk.motion = walk;
            var sRun = sm.AddState("Run"); sRun.motion = run;
            var sDeath = sm.AddState("Death"); sDeath.motion = death;
            var sCower = sm.AddState("Cower");
            sCower.motion = cower != null ? cower : idle;
            sm.defaultState = sIdle;
            AnimatorStateTransition T(AnimatorState a, AnimatorState b,
                AnimatorConditionMode m, float th, string p, float dur)
            {
                var tr = a.AddTransition(b); tr.AddCondition(m, th, p);
                tr.hasExitTime = false; tr.duration = dur; return tr;
            }
            T(sIdle, sWalk, AnimatorConditionMode.Greater, 0.1f, "Speed", 0.2f);
            T(sWalk, sIdle, AnimatorConditionMode.Less, 0.1f, "Speed", 0.2f);
            T(sWalk, sRun, AnimatorConditionMode.Greater, 1.2f, "Speed", 0.15f);
            T(sRun, sWalk, AnimatorConditionMode.Less, 1.0f, "Speed", 0.15f);
            var tc = sm.AddAnyStateTransition(sCower);
            tc.AddCondition(AnimatorConditionMode.If, 0f, "Cowering");
            tc.hasExitTime = false; tc.duration = 0.25f; tc.canTransitionToSelf = false;
            T(sCower, sIdle, AnimatorConditionMode.IfNot, 0f, "Cowering", 0.3f);
            var td = sm.AddAnyStateTransition(sDeath);
            td.AddCondition(AnimatorConditionMode.If, 0f, "Dead");
            td.hasExitTime = false; td.duration = 0.1f; td.canTransitionToSelf = false;
            Debug.Log("[Quaternius] Pedestrian.controller reconstruído com clips UAL");
        }
    }
}
