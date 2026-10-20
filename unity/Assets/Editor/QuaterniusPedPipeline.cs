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

        // ==================================================================
        // PEDESTRES VESTIDOS — modelos npc_csl (roupas originais) com clips
        // UAL retargetados via Mecanim. Esqueleto unificado ASSADO EM DISCO
        // (proibido rebind em runtime — isto é editor-time, uma vez só).
        // ==================================================================

        /// <summary>Batch entry: -executeMethod Studio.Editor.QuaterniusPedPipeline.BakeDressedBatch</summary>
        public static void BakeDressedBatch() => BakeDressedPedestrians();

        /// <summary>
        /// Verificação visual da piloto: renderiza ped_dressed_01f_01 nos
        /// 4 estados-chave (idle/walk/run/death) com log de footY/hipsY/headY.
        /// </summary>
        public static void CaptureDressedBatch() =>
            CaptureDressedPilot(PEDS + "/ped_dressed_01f_01.prefab", "ped_dressed_pilot");

        /// <summary>
        /// DIAGNÓSTICO: renderiza 3 fontes lado a lado sem modificar nada —
        /// (a) prefab vendor intacta, (b) modelo FBX montado, (c) ped_dressed.
        /// Isola se a falha de render é do FBX ou do bake.
        /// </summary>
        public static void DiagRenderBatch()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            ground.GetComponent<Renderer>().sharedMaterial =
                new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { color = new Color(0.25f, 0.25f, 0.3f) };
            var light = new GameObject("L").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);
            var cam = new GameObject("C").AddComponent<Camera>();
            cam.transform.position = new Vector3(0f, 1.3f, 5.5f);
            cam.transform.LookAt(new Vector3(0f, 0.8f, 0f));

            var sources = new (string tag, string path)[]
            {
                ("vendor", "Assets/npc_casual_set_00/Prefabs/npc_csl_00_character_01f_01.prefab"),
                ("fbx", "Assets/npc_casual_set_00/Mesh/npc_csl_00_character_01f.fbx"),
                ("dressed", PEDS + "/ped_dressed_01f_01.prefab"),
            };
            float x = -2.2f;
            foreach (var (tag, path) in sources)
            {
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (src == null) { Debug.LogError("[Diag] ausente: " + path); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                go.transform.position = new Vector3(x, 0f, 0f);
                var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                int en = 0, boneless = 0, nomesh = 0;
                foreach (var s in smrs)
                {
                    if (s.enabled && s.gameObject.activeInHierarchy) en++;
                    if (s.bones == null || s.bones.Length == 0 || s.bones[0] == null) boneless++;
                    if (s.sharedMesh == null) nomesh++;
                }
                var lodg = go.GetComponentInChildren<LODGroup>(true);
                Debug.Log($"[Diag] {tag}: smrs={smrs.Length} enabled={en} " +
                    $"boneless={boneless} nomesh={nomesh} lodgroup={(lodg ? "Y" : "N")}");
                x += 2.2f;
            }
            var rt = new RenderTexture(900, 900, 24);
            cam.targetTexture = rt;
            for (int i = 0; i < 4; i++) cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(900, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); tex.Apply();
            File.WriteAllBytes("../evidence/captures/diag_vendor_fbx_dressed.png", tex.EncodeToPNG());
            Debug.Log("[Diag] captura: diag_vendor_fbx_dressed.png (esq=vendor, meio=fbx, dir=dressed)");
        }

        /// <summary>Batch por prefixo: -executeMethod ...CaptureDressedAllBatch — todas as ped_dressed_*.</summary>
        public static void CaptureDressedAllBatch()
        {
            foreach (var path in Directory.GetFiles(PEDS, "ped_dressed_*.prefab"))
            {
                var name = Path.GetFileNameWithoutExtension(path);
                CaptureDressedPilot(path.Replace('\\', '/'), name);
            }
        }

        private static void CaptureDressedPilot(string prefabPath, string outPrefix)
        {
            // Cena vazia — NUNCA capturar na cena aberta do usuário: os objetos
            // de prova (ground/light/cam) vazavam e estouravam a iluminação.
            UnityEditor.SceneManagement.EditorSceneManager.NewScene(
                UnityEditor.SceneManagement.NewSceneSetup.EmptyScene,
                UnityEditor.SceneManagement.NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) { Debug.LogError("[Dressed] prefab ausente: " + prefabPath); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero;

            var anim = go.GetComponentInChildren<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.applyRootMotion = false;
            Debug.Log($"[DressedCap] {Path.GetFileName(prefabPath)} " +
                $"avatar={(anim.avatar != null ? anim.avatar.name : "NULL")} isHuman={anim.isHuman}");

            const string kk = "Assets/Game/Art/Characters/Quaternius/Animations/UAL1_Standard.fbx";
            AnimationClip Find(string clipName)
            {
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(kk))
                    if (a is AnimationClip c && c.name == clipName)
                        return c;
                return null;
            }
            var clips = new (string tag, AnimationClip clip, float t)[]
            {
                ("idle", Find("Idle_Loop"), 0.5f),
                ("walk", Find("Walk_Loop"), 0.30f),
                ("run", Find("Sprint_Loop") ?? Find("Jog_Fwd_Loop"), 0.35f),
                ("death", Find("Death01"), 0.95f),
            };

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            ground.GetComponent<Renderer>().sharedMaterial =
                new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { color = new Color(0.25f, 0.25f, 0.3f) };
            var light = new GameObject("L").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);
            var cam = new GameObject("C").AddComponent<Camera>();
            cam.transform.position = new Vector3(2.7f, 1.4f, 2.7f);
            cam.transform.LookAt(new Vector3(0f, 0.8f, 0f));

            AnimationMode.StartAnimationMode();
            try
            {
                foreach (var (tag, clip, t) in clips)
                {
                    if (clip == null) { Debug.LogError($"[DressedCap] clip {tag} NULL"); continue; }
                    AnimationMode.SampleAnimationClip(go, clip, t * clip.length);
                    var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                    var head = anim.GetBoneTransform(HumanBodyBones.Head);
                    var foot = anim.GetBoneTransform(HumanBodyBones.LeftFoot);
                    var b = new Bounds(go.transform.position, Vector3.one);
                    foreach (var r in go.GetComponentsInChildren<Renderer>()) b.Encapsulate(r.bounds);
                    Debug.Log($"[DressedCap] {outPrefix}_{tag}: bounds=({b.min.y:F2}..{b.max.y:F2}) " +
                        $"size={b.size} foot={(foot ? foot.position.ToString("F2") : "null")} " +
                        $"hips={(hips ? hips.position.ToString("F2") : "null")} " +
                        $"head={(head ? head.position.ToString("F2") : "null")} " +
                        $"goPos={go.transform.position.ToString("F2")}");
                    // câmera mira no hips real — prova se o corpo saiu do frame
                    if (hips != null)
                    {
                        cam.transform.position = hips.position + new Vector3(2.4f, 0.9f, 2.4f);
                        cam.transform.LookAt(hips.position + Vector3.up * 0.2f);
                    }
                    var rt = new RenderTexture(900, 900, 24);
                    cam.targetTexture = rt;
                    for (int i = 0; i < 3; i++) cam.Render();
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(900, 900, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, 900, 900), 0, 0); tex.Apply();
                    File.WriteAllBytes($"../evidence/captures/{outPrefix}_{tag}.png", tex.EncodeToPNG());
                    RenderTexture.active = null;
                    cam.targetTexture = null;
                    rt.Release();
                }
            }
            finally { AnimationMode.StopAnimationMode(); }
            Object.DestroyImmediate(go);
            Debug.Log($"[DressedCap] capturas salvas: {outPrefix}_*.png");
        }

        /// <summary>
        /// PEDESTRES VESTIDOS v3 — FBX unificado pelo Blender
        /// (tools/art/unify_character_fbx.py + ped_dressed_manifest.json):
        /// bindposes nativas consistentes → skinning funciona sob animação.
        /// Aqui: importa ped_dressed_*.fbx (Humanoid), aplica o MAPA DE
        /// MATERIAIS da variante vendor correspondente (por nome de SMR),
        /// configura o Animator e salva a prefab. Zero cirurgia de esqueleto.
        /// </summary>
        public static void BakeDressedPedestrians()
        {
            var ctrl = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(
                PEDS + "/Pedestrian.controller");
            const string fbxDir = "Assets/Game/Art/Characters/PedDressed";
            const string vendorDir = "Assets/npc_casual_set_00/Prefabs";

            // 1. Reimporta os FBX unificados como Humanoid/CreateFromThisModel
            foreach (var f in Directory.GetFiles(fbxDir, "ped_dressed_*.fbx"))
            {
                var imp = AssetImporter.GetAtPath(f.Replace('\\', '/')) as ModelImporter;
                if (imp == null) continue;
                imp.animationType = ModelImporterAnimationType.Human;
                imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                imp.materialImportMode = ModelImporterMaterialImportMode.None;
                imp.SaveAndReimport();
            }
            AssetDatabase.Refresh();

            foreach (var f in Directory.GetFiles(fbxDir, "ped_dressed_*.fbx"))
            {
                var suffix = Path.GetFileNameWithoutExtension(f)
                    .Substring("ped_dressed_".Length); // 01f_01
                var fbxPath = f.Replace('\\', '/');
                var src = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
                if (src == null) { Debug.LogWarning("[Dressed] fbx ausente: " + fbxPath); continue; }

                // 2. Mapa de materiais da variante vendor (SMR.name → materials)
                var vendorPath = $"{vendorDir}/npc_csl_00_character_{suffix}.prefab";
                var vendor = AssetDatabase.LoadAssetAtPath<GameObject>(vendorPath);
                var matMap = new Dictionary<string, Material[]>();
                if (vendor != null)
                {
                    var vgo = (GameObject)PrefabUtility.InstantiatePrefab(vendor);
                    foreach (var s in vgo.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        matMap[Norm(s.name)] = s.sharedMaterials;
                    Object.DestroyImmediate(vgo);
                }

                // 3. Instancia o FBX limpo, aplica materiais, configura Animator
                var go = (GameObject)PrefabUtility.InstantiatePrefab(src);
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);
                go.name = "ped_dressed_" + suffix;
                int assigned = 0;
                foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (matMap.TryGetValue(Norm(smr.name), out var mats))
                    { smr.sharedMaterials = mats; assigned++; }

                var anim = go.GetComponentInChildren<Animator>();
                if (anim == null) anim = go.AddComponent<Animator>();
                if (!anim.isHuman)
                {
                    Debug.LogError($"[Dressed] {suffix}: avatar NÃO humanoid — abortando");
                    Object.DestroyImmediate(go); continue;
                }
                anim.runtimeAnimatorController = ctrl;
                anim.applyRootMotion = false;
                anim.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                var outp = $"{PEDS}/ped_dressed_{suffix}.prefab";
                PrefabUtility.SaveAsPrefabAsset(go, outp);
                Object.DestroyImmediate(go);
                Debug.Log($"[Dressed] {suffix}: {assigned} SMRs com material vendor → {Path.GetFileName(outp)}");
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Dressed] bake v3 concluído (FBX unificado Blender)");
        }

        /// <summary>Nome normalizado p/ casar SMR vendor↔fbx (minúsculo, sem sufixo lod).</summary>
        private static string Norm(string n)
        {
            n = n.ToLowerInvariant();
            int li = n.LastIndexOf("_lod", System.StringComparison.Ordinal);
            return li >= 0 ? n.Substring(0, li) : n;
        }

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
