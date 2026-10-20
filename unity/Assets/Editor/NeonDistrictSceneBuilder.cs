using System.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using Studio.Cameras;
using Studio.Foundation;
using Studio.Gameplay;
using Studio.Input;
using Studio.UI;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Studio.Editor
{
    /// <summary>
    /// Scene Builder canônico do NEON DISTRICT: FREE RIDE.
    /// Monta um distrito ~5x5 quarteirões (grid 6x6 vias) com o pack Demo City
    /// (Versatile Studio), avenida principal, praça central, tráfego civil por
    /// waypoints, carro SportCar do jogador, atmosfera dusk e HUD mobile.
    /// Converte materiais vendor Built-in RP → URP Lit de forma idempotente.
    /// </summary>
    public static class NeonDistrictSceneBuilder
    {
        private const string SCENE_PATH = "Assets/Game/Scenes/NeonDistrict.unity";
        private const string MAT_DIR = "Assets/Game/Materials/NeonDistrict";
        private const string CITY_DIR = "Assets/Versatile Studio Assets/Demo City By Versatile Studio";
        private const string CITY_PREFABS = CITY_DIR + "/Prefabs";
        private const string CITY_TEX = CITY_DIR + "/Textures";
        private const string TRAFFIC_PREFABS = "Assets/Awbmecreations/Mobile Optimize-Free Low Poly Cars/Prefabs";
        private const string PLAYER_CAR_PREFAB = "Assets/SportCar/Prefabs/SportCar_1.prefab";

        // Grade: 6 linhas de rua em cada eixo → 25 quarteirões de 40x40m
        private const int LINES = 6;
        private const float PITCH = 52f;
        // Old Brick District — extensão leste (MegaKit Boston/NYC).
        // Ruas N-S novas em x=182 e x=234; quarteirões em x=156 e x=208.
        private const float OBD_X0 = 156f, OBD_X1 = 208f;
        private const float OBD_XS0 = 182f, OBD_XS1 = 234f;   // street lines N-S novas
        private const float OBD_EAST_EDGE = 260f;             // fim dirigível
        private const float OBD_ROAD_END = 284f;              // fim visual das ruas
        private const float STREET_W = 12f;
        private const float AVENUE_W = 16f;
        private const float STREET_HALF = STREET_W * 0.5f;
        private const float AVENUE_HALF = AVENUE_W * 0.5f;
        private const float LANE_OFF = 2.75f;
        private const float AVENUE_LANE = 4f;
        private const int AVENUE_J = 3; // zs[3] = +26 — Avenida Neon (E-W)
        private const float CITY_SPAN = 284f;
        private const float ROAD_UV_V_PER_M = 1f / 16f; // repete marcações a cada ~16m

        private static readonly string[] TRAFFIC_PREFAB_NAMES =
        {
            "Classic Car_9", "Hatchback Car_15", "Monster Truck_12",
            "N Van_10", "N_Muscle Car_10", "Pick Up_11", "Sport Car_39",
        };

        private const string MEGAKIT_PREFABS = "Assets/Game/Prefabs/Modular/DowntownMegaKit";
        // Prédios pré-montados do Downtown MegaKit (Boston/NYC, 3m-grid)
        private static readonly string[] MEGAKIT_POOL =
        {
            "Building_Small_1", "Building_Medium_2_001", "Building_Large_2",
        };
        // PILOTO: 2 quarteirões adjacentes à Avenida Neon (z=+26, lado norte)
        private static bool IsDowntownPilot(int bi, int bj) =>
            (bi == 1 && bj == 3) || (bi == 3 && bj == 3);

        private static readonly string[] DOWNTOWN_POOL =
        {
            "office_building_1", "office_building_2", "office_building_3", "office_building_4",
            "office_building_1_with_base", "office_building_3_with_base",
        };
        private static readonly string[] MID_POOL =
        {
            "mid_house_1", "mid_house_2", "mid_house_3", "mid_house_4", "mid_house_5",
            "mid_house_1_2", "mid_house_4_2", "office_building_2_bgr",
        };
        private static readonly string[] EDGE_POOL =
        {
            "small_house_1", "small_house_2", "small_house_3",
            "factory_building_small", "mid_house_1", "mid_house_3",
        };

        [MenuItem("Studio/Neon District/Build Neon District Scene")]
        public static void BuildSceneMenu()
        {
            BuildNeonDistrictScene(SCENE_PATH);
        }

        /// <summary>
        /// Abre a cena NeonDistrict e entra em Play Mode (uso: editor GUI via
        /// -executeMethod Studio.Editor.NeonDistrictSceneBuilder.PlayNeonDistrict).
        /// delayCall garante execução após a inicialização completa do editor.
        /// </summary>
        [MenuItem("Studio/Neon District/Play Neon District")]
        public static void PlayNeonDistrict()
        {
            EditorApplication.delayCall += () =>
            {
                var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
                EditorApplication.EnterPlaymode();
            };
        }

        public static void RunBuildSceneBatch()
        {
            // DynamicsManager drifta pra Script(2) — física morre sem isso
            DynamicsManagerSettingsFix();
            Debug.Log("[NeonDistrictSceneBuilder] Iniciando montagem da cena NeonDistrict em batchmode...");
            bool success = BuildNeonDistrictScene(SCENE_PATH);
            if (success)
            {
                Debug.Log("[NeonDistrictSceneBuilder] Cena NeonDistrict criada com sucesso!");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[NeonDistrictSceneBuilder] Falha ao construir cena NeonDistrict!");
                EditorApplication.Exit(1);
            }
        }

        public static void CaptureNeonDistrictAnglesBatch()
        {
            Debug.Log("[NeonDistrictSceneBuilder] Reconstruindo cena e capturando ângulos do Neon District...");
            BuildNeonDistrictScene(SCENE_PATH);

            var cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            if (cam == null)
            {
                Debug.LogError("[NeonDistrictSceneBuilder] Nenhuma câmera encontrada na cena!");
                EditorApplication.Exit(1);
                return;
            }

            var chase = cam.GetComponent<ChaseCamera>();
            if (chase != null) chase.enabled = false;

            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            // 1. Avenida Neon ao entardecer (vista ao longo da avenida)
            PlaceCam(cam, new Vector3(-40f, 4.5f, 14f), new Vector3(60f, 1f, 26f));
            RenderAndSave(cam, Path.Combine(outDir, "neon_district_01_avenue_dusk.png"));

            // 2. Chase do carro do jogador na avenida
            var player = GameObject.Find("PlayerCar");
            var pp = player != null ? player.transform.position : new Vector3(-60f, 0f, 22f);
            PlaceCam(cam, pp + new Vector3(-7.5f, 3.8f, 0f), pp + new Vector3(6f, 1f, 0f));
            RenderAndSave(cam, Path.Combine(outDir, "neon_district_02_player_chase.png"));

            // 3. Praça Central com prédios ao fundo
            PlaceCam(cam, new Vector3(-20f, 9f, -20f), new Vector3(8f, 2f, 8f));
            RenderAndSave(cam, Path.Combine(outDir, "neon_district_03_central_plaza.png"));

            // 4. Cruzamento com tráfego (veículos spawnados em Play não existem em Edit;
            //    o enquadramento cobre as vias, zebras e postes)
            PlaceCam(cam, new Vector3(38f, 10f, -14f), new Vector3(26f, 0f, 26f));
            RenderAndSave(cam, Path.Combine(outDir, "neon_district_04_intersection.png"));

            // 5. Vista aérea do distrito (grid 5x5)
            PlaceCam(cam, new Vector3(-140f, 90f, -150f), new Vector3(0f, 0f, 0f));
            RenderAndSave(cam, Path.Combine(outDir, "neon_district_05_district_aerial.png"));

            // 6. Rua lateral residencial com postes acesos
            PlaceCam(cam, new Vector3(-104f, 3.2f, -60f), new Vector3(-104f, 2f, 20f));
            RenderAndSave(cam, Path.Combine(outDir, "neon_district_06_side_street.png"));

            // 7. HUD (canvas renderiza em ScreenSpaceOverlay na última captura)
            PlaceCam(cam, pp + new Vector3(-7.5f, 3.8f, 0f), pp + new Vector3(6f, 1f, 0f));
            RenderAndSave(cam, Path.Combine(outDir, "neon_district_07_hud.png"));

            // 8. Lateral do carro — checagem de rodas vs. chão
            PlaceCam(cam, pp + new Vector3(0f, 1.0f, 5.5f), pp + new Vector3(0f, 0.4f, 0f));
            RenderAndSave(cam, Path.Combine(outDir, "neon_district_08_car_side.png"));

            Debug.Log("[NeonDistrictSceneBuilder] 8 capturas renderizadas em evidence/captures/!");
            EditorApplication.Exit(0);
        }

        private static void PlaceCam(Camera cam, Vector3 pos, Vector3 lookAt)
        {
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(lookAt - pos, Vector3.up);
        }

        /// <summary>Corrige m_SimulationMode no DynamicsManager (drift de migração).</summary>
        private static void DynamicsManagerSettingsFix()
        {
            string path = "ProjectSettings/DynamicsManager.asset";
            string s = System.IO.File.ReadAllText(path);
            if (s.Contains("m_SimulationMode: 2"))
            {
                System.IO.File.WriteAllText(path, s.Replace("m_SimulationMode: 2", "m_SimulationMode: 0"));
                Debug.Log("[Builder] DynamicsManager: m_SimulationMode 2→0 corrigido");
            }
        }

        /// <summary>
        /// Instala as variantes danificadas (FBX gerados por damage_vehicle.py)
        /// como filhos inativos do root — o VehicleDamageController faz swap.
        /// </summary>
        private static void InstallDamageVariants(GameObject root, string baseName)
        {
            foreach (var v in new[] { "wrecked_severe", "wrecked_burned" })
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>(
                    $"Assets/Game/Art/Vehicles/Variants/{baseName}__{v}.fbx");
                if (p == null) { Debug.LogWarning($"[Builder] variante ausente: {baseName}__{v}"); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
                go.name = $"__{v}";
                go.transform.SetParent(root.transform, false);
                // mesma escala do modelo intacto (variante saiu do mesmo FBX)
                go.transform.localScale = Vector3.one;
                // Material URP garantido nos renderers da variante — o material
                // embutido do FBX renderizava translúcido (carro "fantasma")
                var baseR = root.GetComponentInChildren<Renderer>();
                var baseMat = baseR != null ? baseR.sharedMaterial : null;
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                {
                    if (baseMat != null)
                    {
                        var m = new Material(baseMat);
                        m.SetFloat("_Surface", 0f); // opaco, sempre
                        if (v == "wrecked_burned")
                        {
                            m.SetColor("_BaseColor", new Color(0.03f, 0.03f, 0.035f, 1f));
                            m.SetFloat("_Smoothness", 0.08f);
                        }
                        else m.SetColor("_BaseColor", baseMat.color * 0.55f);
                        r.sharedMaterial = m;
                    }
                }
                go.SetActive(false);
            }
        }

        /// <summary>Diagnóstico: estado do RB do carro + teste de translação real na cena.</summary>
        public static void DiagnoseCarBatch()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            var car = GameObject.Find("PlayerCar");
            if (car == null) { Debug.Log("[DIAG] PlayerCar não achado"); return; }
            var rb = car.GetComponent<Rigidbody>();
            Debug.Log($"[DIAG] rb kin={rb.isKinematic} constraints={rb.constraints} mass={rb.mass} com={rb.centerOfMass} useGrav={rb.useGravity} pos={car.transform.position}");

            // O que colide com o carro no spawn?
            var cols = Physics.OverlapBox(car.transform.position, new Vector3(2f, 1f, 3f), car.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            foreach (var c in cols)
                if (!c.transform.IsChildOf(car.transform))
                    Debug.Log($"[DIAG] OVERLAP: {c.name} (layer {c.gameObject.layer}, trigger={c.isTrigger})");

            // Aplica velocidade direta e simula — o corpo anda?
            Physics.autoSimulation = false;
            rb.WakeUp();
            rb.linearVelocity = new Vector3(8f, 0f, 0f);
            var p0 = car.transform.position;
            for (int i = 0; i < 50; i++) Physics.Simulate(0.02f);
            var p1 = car.transform.position;
            Debug.Log($"[DIAG] moveu {Vector3.Distance(p0, p1):F2}m em 1s ({p0} -> {p1}) vel={rb.linearVelocity}");
        }

        /// <summary>
        /// AnimatorController dos pedestres com clips Mecanim Humanoid REAIS
        /// (3D Game Kit/Ellen — retargeta pro avatar humanoid dos NPCs).
        /// </summary>
        /// <summary>Entry público p/ batch: regenera só o controller dos peds.</summary>
        public static void BuildPedAnimator()
        {
            EnsurePedestrianAnimator();
        }

        /// <summary>Auditoria Mecanim do prefab unificado (batch).</summary>
        public static void DiagnosePedestrian()
        {
            var go = PrefabUtility.LoadPrefabContents(
                "Assets/Game/Resources/Art/Pedestrians/npc_csl_00_character_01f_01_baked.prefab");
            var anim = go.GetComponentInChildren<Animator>();
            Debug.Log($"[DIAG] Controller: {anim?.runtimeAnimatorController?.name}");
            Debug.Log($"[DIAG] Avatar: {anim?.avatar?.name} | IsValid: " +
                $"{anim?.avatar?.isValid} | IsHuman: {anim?.avatar?.isHuman}");
            if (anim.avatar != null && anim.avatar.isHuman)
            {
                var hd = anim.avatar.humanDescription;
                var legs = System.Array.FindAll(hd.human,
                    h => h.humanName.Contains("Leg")
                        || h.humanName.Contains("Foot")
                        || h.humanName.Contains("Hips"));
                Debug.Log($"[DIAG] ossos de perna/hips mapeados: {legs.Length}");
                foreach (var b in legs)
                    Debug.Log($"  -> {b.humanName} => {b.boneName}");
            }
            foreach (var hb in (HumanBodyBones[])System.Enum.GetValues(
                    typeof(HumanBodyBones)))
            {
                if (hb == HumanBodyBones.LastBone) continue;
                var t = anim.GetBoneTransform(hb);
                if (t == null)
                    Debug.Log($"[DIAG] bone NÃO resolvido: {hb}");
            }
            var smrs = go.GetComponentsInChildren<SkinnedMeshRenderer>();
            Debug.Log($"[DIAG] total SMRs: {smrs.Length}");
            foreach (var smr in smrs)
            {
                var rb = smr.rootBone;
                int onAvatar = 0;
                var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                Transform skel = hips;
                while (skel != null && skel.parent != go.transform)
                    skel = skel.parent;
                foreach (var b in smr.bones)
                    if (b != null && skel != null
                        && b.IsChildOf(skel)) onAvatar++;
                if (onAvatar < smr.bones.Length)
                    Debug.LogWarning($"[SMR] {smr.name}: {onAvatar}/{smr.bones.Length} bones no esqueleto do avatar, root={rb?.name}");
            }
            PrefabUtility.UnloadPrefabContents(go);
        }

        /// <summary>
        /// Pipeline definitivo de pedestres: modelos Quaternius (esqueleto
        /// humanóide limpo, clips UAL reais). Substituída pela etapa Quaternius —
        /// a pipeline antiga npc_csl_*_baked ficou obsoleta.
        /// </summary>
        public static void BakeUnifiedPedestrians()
        {
            QuaterniusPedPipeline.RunPipeline();
        }

        /// <summary>
        /// Knight.fbx (KayKit Adventures, CC0): rig humanóide universal com
        /// TODOS os clips embutidos como takes (Idle, Walking_A, Running_A,
        /// Death_A/B, Hit_A/B...). Proporções humanas — retargeta limpo.
        /// </summary>
        private static void ReimportKayKitAnims()
        {
            const string path = "Assets/Game/Art/Animations/KayKit/Knight.fbx";
            var imp = AssetImporter.GetAtPath(path) as ModelImporter;
            if (imp == null) { Debug.LogError("[KayKit] Knight.fbx ausente"); return; }
            imp.animationType = ModelImporterAnimationType.Human;
            imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            var defs = imp.defaultClipAnimations;
            if (defs.Length == 0)
                defs = new[] { new ModelImporterClipAnimation
                    { name = "Take001", takeName = "Take 001" } };
            foreach (var d in defs)
            {
                bool isDeath = d.takeName.StartsWith("Death")
                    || d.takeName.StartsWith("Dying");
                bool isHold = d.takeName.StartsWith("Sit")
                    || d.takeName.StartsWith("Lie");
                bool loop = !isDeath && !isHold
                    && !d.takeName.StartsWith("Jump_")
                    && !d.takeName.StartsWith("PickUp");
                d.name = d.takeName;
                d.loopTime = loop;
                // root motion extraído — applyRootMotion=false no Animator
                d.lockRootPositionXZ = false;
                d.lockRootRotation = false;
                d.lockRootHeightY = false;
                d.keepOriginalPositionY = true;
                d.keepOriginalPositionXZ = true;
                d.heightFromFeet = true;
            }
            imp.clipAnimations = defs;
            imp.SaveAndReimport();
            foreach (var a in AssetDatabase.LoadAllAssetsAtPath(path))
                if (a is AnimationClip c && !c.name.StartsWith("__preview"))
                    Debug.Log($"[KayKit] clip '{c.name}' len={c.length:F2}s");
        }

        private static void EnsurePedestrianAnimator()
        {
            const string ctrlPath =
                "Assets/Game/Resources/Art/Pedestrians/Pedestrian.controller";
            const string kk = "Assets/Game/Art/Animations/KayKit/Knight.fbx";
            ReimportKayKitAnims();
            AnimationClip Find(string clipName)
            {
                foreach (var a in AssetDatabase.LoadAllAssetsAtPath(kk))
                    if (a is AnimationClip c && c.name == clipName)
                        return c;
                return null;
            }
            var idle = Find("Idle");
            var walk = Find("Walking_A") ?? Find("Walking_B");
            var run = Find("Running_A") ?? Find("Running_B");
            var death = Find("Death_B") ?? Find("Death_A");
            var cower = Find("Hit_A");
            if (idle == null || walk == null || run == null || death == null)
            {
                Debug.LogError("[Builder] clips KayKit ausentes em " + kk);
                return;
            }
            if (AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(ctrlPath)
                != null)
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
            Debug.Log("[Builder] Pedestrian.controller: clips reais KayKit");
        }

        /// <summary>
        /// Calçadas densas: hidrantes, lixeiras, bancos, ATMs e placas do
        /// POLYGON pack — todos BreakableProp (física real, destrutível).
        /// </summary>
        private static void PopulateStreetProps(Transform parent)
        {
            string P = "Assets/POLYGON city pack/Prefabs";
            var seeds = new (string path, float y, float chance)[]
            {
                ($"{P}/Props/Hydrant prefab.prefab", 0f, 1f),
                ($"{P}/Props/Bin prefab.prefab", 0f, 1f),
                ($"{P}/Props/Big_trash_bin prefab.prefab", 0f, 0.5f),
                ($"{P}/Props/Bench 2 prefab.prefab", 0f, 0.6f),
                ($"{P}/Props/ATM_prefab.prefab", 0f, 0.3f),
                ($"{P}/traffic signs/stop sign.prefab", 0f, 0.5f),
                ($"{P}/traffic signs/parking sign.prefab", 0f, 0.5f),
            };
            var rand = new System.Random(42);
            int n = 0;
            for (int i = 0; i < 90; i++)
            {
                bool alongNS = rand.Next(2) == 0;
                int street = rand.Next(Studio.Foundation.CityLayout.LINES);
                int side = rand.Next(2) == 0 ? -1 : 1;
                // posição ao longo da via — pula bandas de cruzamento
                float t = -130f + (float)rand.NextDouble() * 260f;
                if (Studio.Foundation.CityLayout.InIntersectionBand(t)) continue;
                var seed = seeds[rand.Next(seeds.Length)];
                if ((float)rand.NextDouble() > seed.chance) continue;
                var pf = AssetDatabase.LoadAssetAtPath<GameObject>(seed.path);
                if (pf == null) continue;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(pf);
                go.name = $"StreetProp_{n++}";
                go.transform.SetParent(parent, false);
                var sp = Studio.Foundation.CityLayout.SidewalkPoint(alongNS, street, side, t);
                go.transform.position = new Vector3(sp.x, seed.y, sp.z);
                go.transform.rotation = Quaternion.Euler(0, (float)rand.NextDouble() * 360f, 0);
                UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, 0);
                // Lixeiras/bancos leves: rb dinâmico 15–30 kg — empurráveis pelo
                // para-choque. Postes/placas/hidrantes: BreakableProp (tombam).
                bool light = seed.path.Contains("Bin") || seed.path.Contains("Bench");
                if (go.GetComponentInChildren<Collider>() == null)
                    ColliderFitter.FitProp(go, System.IO.Path.GetFileNameWithoutExtension(seed.path), go.transform);
                var prb = go.AddComponent<Rigidbody>();
                if (light)
                {
                    prb.mass = 15f + (float)rand.NextDouble() * 15f;
                    go.AddComponent<Studio.Gameplay.ShootableProp>();
                }
                else
                {
                    prb.isKinematic = true; prb.mass = 60f;
                    go.AddComponent<Studio.Gameplay.BreakableProp>();
                }
            }
            Debug.Log($"[Builder] street props: {n}");
        }

        /// PROVA VISUAL: carro danificado (severe) + carcaça queimada (exploded)
        /// lado a lado com bala perdida — simula TakeDamage em batch.
        /// </summary>
        public static void CaptureWreckedBatch()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            var ground = GameObject.Find("Ground_Collider_System");
            var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{TRAFFIC_PREFABS}/Hatchback Car_15.prefab");
            var severe = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab);
            severe.transform.position = new Vector3(-63f, 0.1f, 22f);
            var burned = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab);
            burned.transform.position = new Vector3(-55f, 0.1f, 22f);
            var d1 = severe.AddComponent<Studio.Gameplay.VehicleDamageController>();
            var d2 = burned.AddComponent<Studio.Gameplay.VehicleDamageController>();
            // Força Awake (find children) + aplica dano
            severe.SetActive(false); severe.SetActive(true);
            burned.SetActive(false); burned.SetActive(true);
            var awake = typeof(Studio.Gameplay.VehicleDamageController)
                .GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            awake?.Invoke(d1, null); awake?.Invoke(d2, null);
            d1.TakeDamage(70f);   // → Severe (35% restam)
            d2.TakeDamage(100f);  // → Exploded
            Physics.autoSimulation = false;
            for (int i = 0; i < 20; i++) Physics.Simulate(0.02f);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.transform.position = new Vector3(-59f, 3.2f, 14f);
            cam.transform.rotation = Quaternion.LookRotation(new Vector3(-59f, 0.8f, 22f) - cam.transform.position);
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            cam.Render();
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
            tex.Apply();
            System.IO.File.WriteAllBytes("../evidence/captures/neon_district_vehicle_damaged.png", tex.EncodeToPNG());
            Debug.Log("[Capture] neon_district_vehicle_damaged.png salvo");
        }

        /// <summary>
        /// 3 evidências do briefing VFX: carro fumegando (Severe/Burning),
        /// momento da explosão, e carcaça queimada com fogos residuais.
        /// </summary>
        public static void CaptureVfxPipelineBatch()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            var carPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{TRAFFIC_PREFABS}/Hatchback Car_15.prefab");
            var awake = typeof(Studio.Gameplay.VehicleDamageController)
                .GetMethod("Awake", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            Studio.Gameplay.VehicleDamageController Spawn(float x, float dmg)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(carPrefab);
                go.transform.position = new Vector3(x, 0.1f, 22f);
                var d = go.AddComponent<Studio.Gameplay.VehicleDamageController>();
                awake?.Invoke(d, null);
                d.TakeDamage(dmg);
                return d;
            }

            var cam = new GameObject("Cam").AddComponent<Camera>();
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);

            void Shot(GameObject car, string file, float delaySim)
            {
                Physics.autoSimulation = false;
                for (int i = 0; i < (int)(delaySim / 0.02f); i++) Physics.Simulate(0.02f);
                cam.transform.position = car.transform.position + new Vector3(0f, 2.6f, -6.5f);
                cam.transform.rotation = Quaternion.LookRotation(
                    car.transform.position + Vector3.up * 0.7f - cam.transform.position);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                tex.Apply();
                System.IO.File.WriteAllBytes($"../evidence/captures/{file}", tex.EncodeToPNG());
                Debug.Log($"[Capture] {file} salvo");
            }

            var smoke = Spawn(-70f, 70f);   // Severe→Burning: fumaça/fogo
            Shot(smoke.gameObject, "vehicle_damage_smoke.png", 0.6f);
            var blast = Spawn(-60f, 99f);   // Burning
            blast.TakeDamage(10f);          // → Explode (blast visual imediato)
            Shot(blast.gameObject, "vehicle_explosion_blast.png", 0.05f);
            var wreck = Spawn(-50f, 100f);  // Exploded: carcaça
            Shot(wreck.gameObject, "vehicle_wreck_burned.png", 1.5f);
        }

        /// <summary>PROVA: renderiza o prefab WFXMR_Explosion isolado — valida se
        /// a explosão renderiza texturizada em batch (diagnóstico URP).</summary>
        public static void CaptureExplosionBatch()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/JMO Assets/WarFX/_Effects (Mobile)/Explosions/WFXMR_Explosion.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = new Vector3(-60f, 1f, 22f);
            foreach (var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                ps.Simulate(0.5f, true, false);
            var cam = new GameObject("Cam").AddComponent<Camera>();
            cam.transform.position = go.transform.position + new Vector3(0f, 3f, -8f);
            cam.transform.rotation = Quaternion.LookRotation(go.transform.position - cam.transform.position);
            cam.backgroundColor = new Color(0.05f, 0.05f, 0.08f); cam.clearFlags = CameraClearFlags.SolidColor;
            var rt = new RenderTexture(960, 540, 24); cam.targetTexture = rt;
            cam.Render();
            var tex = new Texture2D(960, 540, TextureFormat.RGB24, false);
            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes("../evidence/captures/explosion_iso.png", tex.EncodeToPNG());
            Debug.Log("[Capture] explosion_iso.png salvo");
        }

        /// <summary>
        /// PROVA VISUAL: spawna um poste, quebra via BreakableProp, simula a
        /// física em batch (Physics.Simulate) e renderiza o poste tombado.
        /// Executar via -executeMethod CaptureBrokenPoleBatch.
        /// </summary>
        public static void CaptureBrokenPoleBatch()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(40f, 1f, 40f);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CITY_PREFABS}/lamp_pole_single_orange.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero;

            ColliderFitter.FitProp(go, "lamp_pole_single_orange", go.transform);
            var prb = go.AddComponent<Rigidbody>();
            prb.isKinematic = true; prb.mass = 60f;
            var bp = go.AddComponent<Studio.Gameplay.BreakableProp>();
            bp.Break(new Vector3(0.6f, 0f, 1f).normalized, new Vector3(0f, 0.6f, 0.3f), 20000f);

            Physics.autoSimulation = false;
            for (int i = 0; i < 160; i++) Physics.Simulate(1f / 50f);

            var light = new GameObject("ProofLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.4f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.4f, 0.4f, 0.45f);

            var cam = new GameObject("ProofCam").AddComponent<Camera>();
            cam.transform.position = new Vector3(4.5f, 2.2f, -5f);
            cam.transform.LookAt(new Vector3(0f, 0.7f, 0.5f));
            RenderAndSave(cam, "../evidence/captures/neon_district_pole_broken.png");
            Debug.Log("[NeonDistrictSceneBuilder] Prova de poste quebrado capturada.");
        }

        /// <summary>
        /// PROVA VISUAL dos pedestres: instancia prefab real npc_csl_*, aplica o
        /// Pedestrian.controller (clips Ellen Humanoid) e captura cada estado.
        /// Loga hash do estado, bounds e altura do pé pra validação honesta.
        /// </summary>
        public static void CapturePedAnims()
        {
            EnsurePedestrianAnimator();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "ProofGround";
            ground.transform.position = new Vector3(0f, -0.5f, 0f);
            ground.transform.localScale = new Vector3(30f, 1f, 30f);
            var gm = ground.GetComponent<Renderer>();
            if (gm != null)
                gm.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
                { color = new Color(0.25f, 0.25f, 0.3f) };

            var light = new GameObject("ProofLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.6f;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Game/Resources/Art/Pedestrians/quat_male_a.prefab");
            if (prefab == null)
                prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                    "Assets/Game/Resources/Art/Pedestrians/npc_csl_00_character_01f_01_baked.prefab");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.transform.position = Vector3.zero;

            var anim = go.GetComponentInChildren<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            anim.applyRootMotion = false;
            Debug.Log($"[PedCap] avatar={(anim.avatar != null ? anim.avatar.name : "NULL")} " +
                $"isHuman={anim.isHuman}");

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
                ("walk2", Find("Walk_Loop"), 0.80f),
                ("run", Find("Sprint_Loop") ?? Find("Jog_Fwd_Loop"), 0.35f),
                ("death", Find("Death01"), 0.95f),
                ("cower", Find("Crouch_Idle_Loop") ?? Find("Hit_Chest"), 0.6f),
            };

            var cam = new GameObject("ProofCam").AddComponent<Camera>();
            // vista 3/4: passada (eixo Z) fica visível, não comprimida
            cam.transform.position = new Vector3(2.7f, 1.4f, 2.7f);
            cam.transform.LookAt(new Vector3(0f, 0.8f, 0f));

            // Amostra cada clip direto via AnimationMode (editor-time preview
            // oficial — Animator.Update não escreve pose fora de play mode).
            AnimationMode.StartAnimationMode();
            try
            {
                foreach (var (tag, clip, t) in clips)
                {
                    if (clip == null) { Debug.LogError($"[PedCap] clip {tag} NULL"); continue; }
                    float time = t * clip.length;
                    AnimationMode.SampleAnimationClip(go, clip, time);
                    var foot = FindDeep(go.transform, "LeftFoot");
                    var hips = anim.GetBoneTransform(HumanBodyBones.Hips);
                    var head = anim.GetBoneTransform(HumanBodyBones.Head);
                    var rends = go.GetComponentsInChildren<Renderer>();
                    var b = new Bounds(go.transform.position, Vector3.one);
                    foreach (var r in rends) b.Encapsulate(r.bounds);
                    Debug.Log($"[PedCap] {tag}: clip={clip.name} len={clip.length:F2}s " +
                        $"t={time:F2} bounds=({b.min.y:F2}..{b.max.y:F2}) " +
                        $"footY={(foot != null ? foot.position.y : -999):F3} " +
                        $"hipsY={(hips != null ? hips.position.y : -999):F3} " +
                        $"headY={(head != null ? head.position.y : -999):F3}");
                    RenderAndSave(cam, $"../evidence/captures/ped_{tag}.png");
                }
            }
            finally { AnimationMode.StopAnimationMode(); }
            Debug.Log("[PedCap] capturas salvas em evidence/captures/ped_*.png");
        }

        /// <summary>
        /// Calibração completa PROMETEO+GTA na cena já construída: curvas de
        /// atrito dos WheelCols + parâmetros do controller (idem ao builder).
        /// </summary>
        public static void TunePrometeoBatch()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);
            var prom = UnityEngine.Object.FindAnyObjectByType<PrometeoCarController>();
            if (prom == null) { Debug.LogError("[Tune] PrometeoCarController não achado"); return; }
            Studio.Gameplay.CarTunings.SuperCar.ApplyTo(prom);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[Tune] PROMETEO calibrado: grip+brake+steer aplicados");
        }

        /// <summary>Capturas do briefing: rodas viradas + pássaros visíveis.</summary>
        public static void CaptureWheelsAndBirdsBatch()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

            // --- Rodas dianteiras viradas: força o esterço visual do carro ---
            var car = UnityEngine.Object.FindAnyObjectByType<Studio.Gameplay.PlayerCarController>();
            if (car != null)
            {
                var t = typeof(Studio.Gameplay.PlayerCarController);
                var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance;
                t.GetMethod("Awake", flags)?.Invoke(car, null); // _rb precisa existir
                var fld = t.GetField("_visualSteerDeg", flags);
                var upd = t.GetMethod("UpdateWheelVisuals", flags);
                fld?.SetValue(car, 32f); // alvo já atingido
                upd?.Invoke(car, new object[] { 1f, 0.016f });
                var cam = new GameObject("WheelCam").AddComponent<Camera>();
                cam.transform.position = car.transform.position
                    + car.transform.forward * 3.2f + car.transform.right * 2.4f + Vector3.up * 1.3f;
                cam.transform.LookAt(car.transform.position + car.transform.forward * 1.4f + Vector3.up * 0.35f);
                RenderAndSave(cam, "../evidence/captures/wheels_steering_turn.png");
                UnityEngine.Object.DestroyImmediate(cam.gameObject);
            }

            // --- Pássaros: força spawn dos bandos + enquadra praça/avenida ---
            var bs = UnityEngine.Object.FindAnyObjectByType<Studio.Gameplay.BirdSystem>();
            if (bs != null)
            {
                var st = bs.GetType().GetMethod("Start",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                st?.Invoke(bs, null);
                Physics.autoSimulation = false;
                for (int i = 0; i < 30; i++)
                {
                    var u = bs.GetType().GetMethod("Update",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    u?.Invoke(bs, null);
                }
            }
            var bcam = new GameObject("BirdCam").AddComponent<Camera>();
            bcam.transform.position = new Vector3(-40f, 4f, 26f);
            bcam.transform.rotation = Quaternion.Euler(6f, 90f, 0f);
            bcam.farClipPlane = 200f;
            RenderAndSave(bcam, "../evidence/captures/birds_visible_sky.png");
            UnityEngine.Object.DestroyImmediate(bcam.gameObject);
            Debug.Log("[Capture] wheels_steering_turn.png + birds_visible_sky.png salvos");
        }

        private static Transform FindDeep(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
            return null;
        }

        private static void RenderAndSave(Camera cam, string filePath)
        {
            int width = 1280;
            int height = 720;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;

            // Warm-up de shaders URP: 3 frames dummy evitam o frame magenta em batchmode
            for (int i = 0; i < 3; i++) cam.Render();
            cam.Render();

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();

            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);

            byte[] bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            File.WriteAllBytes(filePath, bytes);
            Debug.Log($"[NeonDistrictSceneBuilder] Captura salva: {Path.GetFileName(filePath)} ({bytes.Length / 1024} KB)");
        }

        // ====================================================================
        // Construção principal
        // ====================================================================

        public static bool BuildNeonDistrictScene(string scenePath)
        {
            string sceneDir = Path.GetDirectoryName(scenePath);
            if (!Directory.Exists(sceneDir)) Directory.CreateDirectory(sceneDir);
            if (!Directory.Exists(MAT_DIR)) Directory.CreateDirectory(MAT_DIR);

            // 1. Conversão idempotente dos materiais vendor Built-in → URP Lit
            int converted = ConvertVendorMaterialsToUrp();
            Debug.Log($"[NeonDistrictSceneBuilder] Materiais vendor convertidos para URP Lit: {converted}");

            // 2. Texturas PBR do pack Demo City (wrap Repeat obrigatório para UV > 1)
            var texRoadStreet = LoadTex("asphalt_2_tracks");
            var texRoadStreetN = LoadTex("asphalt_2_tracks_nm");
            var texRoadStreetOc = LoadTex("asphalt_2_tracks_oc");
            var texRoadAvenue = LoadTex("asphalt_4_tracks");
            var texRoadAvenueN = LoadTex("asphalt_4_tracks_nm");
            var texRoadAvenueOc = LoadTex("asphalt_4_tracks_oc");
            var texIntersection = LoadTex("asphalt_square");
            var texIntersectionN = LoadTex("asphalt_square_nm");
            var texIntersectionOc = LoadTex("asphalt_square_oc");
            var texSidewalk = LoadTex("sideway_tile");
            var texSidewalkN = LoadTex("sideway_tile_nm");
            var texSidewalkOc = LoadTex("sideway_tile_oc");
            var texCrosswalk = LoadTex("asphalt_extra");
            var texCrosswalkN = LoadTex("asphalt_extra_nm");
            foreach (var t in new Texture2D[] { texRoadStreet, texRoadStreetN, texRoadStreetOc,
                texRoadAvenue, texRoadAvenueN, texRoadAvenueOc, texIntersection, texIntersectionN,
                texIntersectionOc, texSidewalk, texSidewalkN, texSidewalkOc, texCrosswalk, texCrosswalkN })
                EnsureRepeatWrap(t);

            // 3. Materiais URP do jogo (todos com _BaseMap — Quality Gate V2)
            var matStreet = GetOrCreateMaterial("Mat_ND_Road_Street", Color.white, Color.black, texRoadStreet, texRoadStreetN);
            var matAvenue = GetOrCreateMaterial("Mat_ND_Road_Avenue", Color.white, Color.black, texRoadAvenue, texRoadAvenueN);
            var matIntersection = GetOrCreateMaterial("Mat_ND_Intersection", new Color(0.92f, 0.92f, 0.95f), Color.black, texIntersection, texIntersectionN);
            var matSidewalk = GetOrCreateMaterial("Mat_ND_Sidewalk", Color.white, Color.black, texSidewalk, texSidewalkN, new Vector2(48f, 48f));
            var matCrosswalk = GetOrCreateMaterial("Mat_ND_Crosswalk", Color.white, Color.black, texCrosswalk, texCrosswalkN);
            var matSkid = GetOrCreateMaterial("Mat_FX_SkidMark", new Color(0.06f, 0.06f, 0.07f), Color.black, texIntersection);
            var matFar = GetOrCreateMaterial("Mat_ND_GroundFar", new Color(0.16f, 0.13f, 0.16f), Color.black, texIntersection, texIntersectionN, new Vector2(60f, 60f));
            var matSkybox = GetOrCreateSkybox();

            // 4. Cena limpa
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 5. Atmosfera dusk
            RenderSettings.skybox = matSkybox;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.32f, 0.20f, 0.38f);
            RenderSettings.ambientEquatorColor = new Color(0.42f, 0.22f, 0.22f);
            RenderSettings.ambientGroundColor = new Color(0.10f, 0.07f, 0.14f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.28f, 0.13f, 0.18f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0045f;

            var sunObj = new GameObject("Directional Light (Dusk Sun)");
            var sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.62f, 0.38f);
            sun.intensity = 1.15f;
            sun.shadows = LightShadows.Soft;
            sunObj.AddComponent<UniversalAdditionalLightData>();
            sunObj.transform.rotation = Quaternion.Euler(14f, -118f, 0f); // sol baixo âmbar a poente
            RenderSettings.sun = sun;

            // 6. Vias, calçadas, cruzamentos e zebras
            var envRoot = new GameObject("Environment");
            var roadsRoot = new GameObject("Roads");
            roadsRoot.transform.SetParent(envRoot.transform);
            BuildRoadNetwork(roadsRoot.transform, matStreet, matAvenue, matIntersection, matSidewalk, matCrosswalk);

            // Colisor-mãe do chão (topo em y=0, coplanar às vias)
            var groundColObj = new GameObject("Ground_Collider_System");
            groundColObj.transform.SetParent(envRoot.transform);
            var groundCol = groundColObj.AddComponent<BoxCollider>();
            groundCol.center = new Vector3(0f, -0.05f, 0f);
            groundCol.size = new Vector3(CITY_SPAN + 20f + 320f, 0.1f, CITY_SPAN + 20f); // +OBD leste

            // 7. Quarteirões com prédios + praça central
            var blocksRoot = new GameObject("City_Blocks");
            blocksRoot.transform.SetParent(envRoot.transform);
            BuildBlocks(blocksRoot.transform);
            BuildOldBrickDistrict(envRoot.transform);
            BuildOBD_ArtPass(envRoot.transform);
            BuildPlaza(blocksRoot.transform);
            BuildSkylineRing(blocksRoot.transform, matFar);

            // 8. Props de rua (postes, árvores)
            var propsRoot = new GameObject("Street_Props");
            propsRoot.transform.SetParent(envRoot.transform);
            BuildStreetProps(propsRoot.transform);

            // 9. Barreiras invisíveis do distrito
            BuildBoundsBlockers(envRoot.transform);

            // 10. Sistemas (_GameSystems primeiro na hierarquia)
            var systemObj = new GameObject("_GameSystems");
            systemObj.AddComponent<DriveInputAdapter>();
            systemObj.AddComponent<ProceduralAudioService>();
            systemObj.AddComponent<Studio.Gameplay.RadioSystem>();
            var pedSys = systemObj.AddComponent<Studio.Gameplay.PedestrianSystem>();
            systemObj.AddComponent<Studio.Gameplay.BirdSystem>();
            var dayNight = systemObj.AddComponent<Studio.Foundation.DayNightCycle>();
            dayNight.Setup(sun, matSkybox, null);
            var netObj = new GameObject("TrafficNetwork");
            netObj.transform.SetParent(systemObj.transform);
            var network = netObj.AddComponent<TrafficNetwork>();
            network.SetEdges(BuildTrafficEdges());
            var gameMgr = systemObj.AddComponent<NeonDistrictGameManager>();
            var trafficMgr = systemObj.AddComponent<TrafficManager>();

            // 11. Carro do jogador (SportCar_1 convertido, física arcade)
            var playerObj = BuildPlayerCar(matSkid);

            // 12. Câmera chase + pós-processamento
            var chaseCamObj = BuildCamera(playerObj.transform);
            BuildPostVolume();

            // 12b. Sistemas a pé (OnFootMotor + AK-47 + pickups + alvos)
            BuildOnFootSystems(playerObj, systemObj.transform, chaseCamObj.GetComponent<Camera>());

            // 13. HUD mobile (paisagem)
            BuildUICanvas(playerObj.GetComponent<PlayerCarController>(), systemObj.GetComponent<DriveInputAdapter>());
            pedSys.SetPlayer(playerObj.transform);
            QuaterniusPedPipeline.RunPipeline(); // ped/prefabs/controller definitivos
            PopulateStreetProps(envRoot.transform);

            // 14. Wiring entre sistemas via SerializedObject
            var tmSo = new SerializedObject(trafficMgr);
            tmSo.FindProperty("_network").objectReferenceValue = network;
            tmSo.FindProperty("_player").objectReferenceValue = playerObj.transform;
            InstallTrafficVariants();
            var prefabs = LoadTrafficPrefabs();

            // VfxLibrary: registra prefabs War FX no pool (impacto/fogo/explosão)
            var libGo = new GameObject("VfxLibrary");
            var lib = libGo.AddComponent<Studio.Foundation.VfxLibrary>();
            string fx = "Assets/JMO Assets/WarFX/_Effects (Mobile)";
            lib.impactMetal = AssetDatabase.LoadAssetAtPath<GameObject>($"{fx}/Bullet Impacts/WFXMR_BImpact Metal.prefab");
            lib.fireSmall = AssetDatabase.LoadAssetAtPath<GameObject>($"{fx}/Fire/Black Smoke/WFXMR_Fire Natural (Black Smoke).prefab");
            // Explosão UNIFICADA: o mesmo prefab War FX validado para batida
            // e tiros — o variant Mirza gerava quadrados pretos (shader próprio).
            lib.explosion = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{fx}/Explosions/WFXMR_Explosion.prefab")
                ?? AssetDatabase.LoadAssetAtPath<GameObject>($"{fx}/Explosions/WFXMR_Explosion Simple.prefab");
            lib.residualSmoke = AssetDatabase.LoadAssetAtPath<GameObject>($"{fx}/Explosions/WFXMR_ExplosiveSmoke Small.prefab");
            var arr = tmSo.FindProperty("_vehiclePrefabs");
            arr.arraySize = prefabs.Count;
            for (int i = 0; i < prefabs.Count; i++)
                arr.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            tmSo.ApplyModifiedPropertiesWithoutUndo();

            var gmSo = new SerializedObject(gameMgr);
            gmSo.FindProperty("_player").objectReferenceValue = playerObj.GetComponent<PlayerCarController>();
            gmSo.FindProperty("_network").objectReferenceValue = network;
            gmSo.ApplyModifiedPropertiesWithoutUndo();

            // 15. Orientação mobile: paisagem
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;

            // 16. Salvar e registrar
            bool saved = EditorSceneManager.SaveScene(scene, scenePath);
            if (!saved)
            {
                Debug.LogError($"[NeonDistrictSceneBuilder] Não foi possível salvar a cena em: {scenePath}");
                return false;
            }

            RegisterSceneInBuildSettings(scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        // ====================================================================
        #region Conversão URP de materiais vendor

        /// <summary>
        /// Converte in-loco (idempotente) os .mat Built-in RP dos packs usados
        /// para URP Lit preservando albedo/normal/occlusion/emissão/cor.
        /// </summary>
        private static int ConvertVendorMaterialsToUrp()
        {
            var urp = Shader.Find("Universal Render Pipeline/Lit");
            if (urp == null) { Debug.LogError("[NeonDistrictSceneBuilder] URP Lit shader não encontrado!"); return 0; }

            var scanDirs = new[]
            {
                CITY_DIR + "/Materials",
                CITY_DIR + "/Models/Materials",
                "Assets/SportCar",
                "Assets/Weapons_ChamferZone",
                "Assets/Package",
                "Assets/Game/Art/Vehicles/Variants",
                "Assets/POLYGON city pack",
                "Assets/npc_casual_set_00",
                "Assets/Zacxophone",
            };

            int converted = 0;
            foreach (var dir in scanDirs)
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { dir }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (mat == null || mat.shader == urp) continue;

                    // Captura propriedades built-in antes de trocar o shader
                    Texture texMain = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                    Color color = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                    Texture bump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                    Texture occ = mat.HasProperty("_OcclusionMap") ? mat.GetTexture("_OcclusionMap") : null;
                    Texture emisMap = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
                    Color emisCol = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
                    bool emissive = mat.IsKeywordEnabled("_EMISSION") && emisCol.maxColorComponent > 0.01f;
                    float gloss = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.35f;
                    float metal = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;

                    mat.shader = urp;
                    if (texMain != null) mat.SetTexture("_BaseMap", texMain);
                    else if (mat.GetTexture("_BaseMap") == null) mat.SetTexture("_BaseMap", GetOrCreateWhiteTex());
                    mat.SetColor("_BaseColor", color);
                    if (bump != null)
                    {
                        mat.SetTexture("_BumpMap", bump);
                        mat.EnableKeyword("_NORMALMAP");
                    }
                    if (occ != null) mat.SetTexture("_OcclusionMap", occ);
                    if (emissive || emisMap != null)
                    {
                        mat.EnableKeyword("_EMISSION");
                        if (emisMap != null) mat.SetTexture("_EmissionMap", emisMap);
                        mat.SetColor("_EmissionColor", emisCol.maxColorComponent > 0.01f ? emisCol : emisCol + new Color(0.8f, 0.8f, 0.8f));
                    }
                    mat.SetFloat("_Smoothness", gloss);
                    mat.SetFloat("_Metallic", metal);

                    // Transparência só quando o alfa é real (vidro): alfa ~0
                    // nos FBX do Blender significa "sem info" — NÃO é vidro
                    if (color.a > 0.15f && color.a < 0.98f)
                    {
                        mat.SetFloat("_Surface", 1f);
                        mat.SetFloat("_Blend", 0f);
                        mat.SetFloat("_AlphaClip", 0f);
                        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    }
                    else
                    {
                        mat.SetFloat("_Surface", 0f);
                        mat.SetFloat("_AlphaClip", 0f);
                    }

                    EditorUtility.SetDirty(mat);
                    converted++;
                }
            }
            // Reparo: força opaco nos .mat das variantes (carcaça não é vidro;
            // conversão antiga marcou transparente por alfa~0 do Blender)
            foreach (var guid in AssetDatabase.FindAssets("t:Material",
                new[] { "Assets/Game/Art/Vehicles/Variants" }))
            {
                var m = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (m == null) continue;
                if (m.HasProperty("_Surface") && m.GetFloat("_Surface") > 0.5f)
                { m.SetFloat("_Surface", 0f); m.DisableKeyword("_SURFACE_TYPE_TRANSPARENT"); EditorUtility.SetDirty(m); }
            }

            // Passe extra: materiais de PARTÍCULA (War FX/CFX + Mirza Beig) → URP
            converted += ConvertParticleMaterialsToUrp("Assets/JMO Assets");
            converted += ConvertParticleMaterialsToUrp("Assets/Mirza Beig");

            AssetDatabase.SaveAssets();
            return converted;
        }

        /// <summary>
        /// Converte materiais de partícula Built-in (Particles/Additive,
        /// Alpha Blended etc.) para URP Particles/Unlit preservando textura,
        /// cor e o modo de blend (Add=chamas/faísca, Alpha=fumaça).
        /// </summary>
        private static int ConvertParticleMaterialsToUrp(string dir)
        {
            var shaderAdd = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shaderAdd == null) shaderAdd = Shader.Find("Universal Render Pipeline/Particles/Simple Lit");
            if (shaderAdd == null) return 0;
            int n = 0;
            foreach (var guid in AssetDatabase.FindAssets("t:Material", new[] { dir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) continue;
                string cur = mat.shader != null ? mat.shader.name : "";
                // Blend correto pelo NOME do material (Mult=multiply, Add=additive,
                // resto=alpha) — re-aplica SEMPRE, mesmo se já convertido
                string mn = System.IO.Path.GetFileNameWithoutExtension(path);
                float blend = mn.Contains("Add") || mn.Contains("Flame") || mn.Contains("Fire")
                            || mn.Contains("Spark") || mn.Contains("Glow") ? 2f : 0f;
                bool ok = cur.Contains("Particles/Unlit") || cur.Contains("Particles/Simple Lit");
                if (ok && mat.GetTexture("_BaseMap") != null && mat.HasProperty("_Blend")
                    && Mathf.Approximately(mat.GetFloat("_Blend"), blend)) continue;
                var tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex")
                        : mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : null;
                if (tex == null) tex = GetOrCreateWhiteTex(); // nunca quadro preto
                var col = mat.HasProperty("_TintColor") ? mat.GetColor("_TintColor")
                        : mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                mat.shader = shaderAdd;
                mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", col);
                if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
                if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", blend);
                EditorUtility.SetDirty(mat);
                n++;
            }
            return n;
        }

        #endregion

        #region Construção de vias

        private static void BuildRoadNetwork(Transform parent, Material matStreet, Material matAvenue,
            Material matIntersection, Material matSidewalk, Material matCrosswalk)
        {
            // Plano-mãe de calçada (pavimento) cobrindo todo o distrito
            var ground = CreateRoadQuad("Sidewalk_Base", CITY_SPAN + 16f, CITY_SPAN + 16f,
                matSidewalk, 0.25f, parent);
            ground.transform.position = new Vector3(0f, -0.08f, 0f);

            // Ruas N-S (compridas em Z) e E-W (compridas em X)
            for (int i = 0; i < LINES; i++)
            {
                float x = StreetLine(i);
                var ns = CreateRoadQuad($"Street_NS_{i}", STREET_W, CITY_SPAN, matStreet, ROAD_UV_V_PER_M, parent);
                ns.transform.position = new Vector3(x, 0f, 0f);
            }
            for (int j = 0; j < LINES; j++)
            {
                float z = StreetLine(j);
                bool avenue = (j == AVENUE_J);
                var ew = CreateRoadQuad(avenue ? "Avenue_Neon_EW" : $"Street_EW_{j}",
                    avenue ? AVENUE_W : STREET_W, CITY_SPAN, avenue ? matAvenue : matStreet, ROAD_UV_V_PER_M, parent);
                ew.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                ew.transform.position = new Vector3(0f, 0f, z);
            }

            // Cruzamentos (asfalto liso levemente acima das vias)
            for (int i = 0; i < LINES; i++)
            {
                for (int j = 0; j < LINES; j++)
                {
                    var cross = CreateRoadQuad($"Intersection_{i}_{j}",
                        (j == AVENUE_J ? AVENUE_W : STREET_W) + 1.2f, STREET_W + 1.2f, matIntersection, 0.12f, parent);
                    cross.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                    cross.transform.position = new Vector3(StreetLine(i), 0.06f, StreetLine(j));
                }
            }

            // Faixas de pedestres nas 4 aproximações do cruzamento da praça/avenida
            BuildCrosswalks(parent, matCrosswalk);
        }

        /// <summary>Zebras no cruzamento (xs[3]=26, zs[3]=26) junto à praça.</summary>
        private static void BuildCrosswalks(Transform parent, Material mat)
        {
            float cx = StreetLine(3);
            float cz = StreetLine(AVENUE_J);
            float strip = 3.2f;

            // Atravessa a avenida (E-W): estreita em X (3.2), cobre a largura em Z (16)
            // Após yaw 90°, o "width" local vira extensão em Z e o "length" em X.
            for (int side = -1; side <= 1; side += 2)
            {
                var cw = CreateRoadQuad($"Crosswalk_Avenue_{side}", AVENUE_W, strip, mat, 0f, parent);
                cw.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                cw.transform.position = new Vector3(cx + side * (AVENUE_HALF + 0.6f + strip * 0.5f), 0.11f, cz);
                CropUVToZebra(cw);
            }
            // Atravessa a rua N-S: estreita em Z (3.2), cobre a largura em X (12)
            for (int side = -1; side <= 1; side += 2)
            {
                var cw = CreateRoadQuad($"Crosswalk_Street_{side}", STREET_W, strip, mat, 0f, parent);
                cw.transform.position = new Vector3(cx, 0.11f, cz + side * (STREET_HALF + 0.6f + strip * 0.5f));
                CropUVToZebra(cw);
            }
        }

        /// <summary>Recorta o UV para a metade zebra do asphalt_extra (u ∈ [0.5,1]).</summary>
        private static void CropUVToZebra(GameObject quad)
        {
            var mf = quad.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            var mesh = UnityEngine.Object.Instantiate(mf.sharedMesh);
            var uv = mesh.uv;
            for (int i = 0; i < uv.Length; i++)
                uv[i] = new Vector2(0.5f + uv[i].x * 0.5f, uv[i].y);
            mesh.uv = uv;
            mf.sharedMesh = mesh;
        }

        /// <summary>
        /// Quad horizontal (XZ) com UV: u ∈ [0,1] na largura, v = length * vPerM.
        /// Se vPerM = 0, v ∈ [0,1] (para recorte manual posterior).
        /// </summary>
        private static GameObject CreateRoadQuad(string name, float width, float length,
            Material mat, float vPerM, Transform parent)
        {
            float hw = width * 0.5f;
            float hl = length * 0.5f;
            float vMax = vPerM > 0f ? length * vPerM : 1f;

            var mesh = new Mesh { name = $"SM_ND_{name}" };
            mesh.vertices = new[]
            {
                new Vector3(-hw, 0f, -hl), new Vector3(hw, 0f, -hl),
                new Vector3(-hw, 0f, hl), new Vector3(hw, 0f, hl),
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(0f, vMax), new Vector2(1f, vMax),
            };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        #endregion

        #region Quarteirões, praça e props

        private static void BuildBlocks(Transform parent)
        {
            // Centros dos 25 quarteirões: (i-2)*52 → -104..104
            for (int bi = 0; bi < 5; bi++)
            {
                for (int bj = 0; bj < 5; bj++)
                {
                    if (bi == 2 && bj == 2) continue; // praça central
                    float cx = (bi - 2) * PITCH;
                    float cz = (bj - 2) * PITCH;

                    int ring = Mathf.Max(Mathf.Abs(bi - 2), Mathf.Abs(bj - 2)); // 0/1 centro, 2 borda
                    var pool = ring <= 1 ? DOWNTOWN_POOL : (ring == 2 ? EDGE_POOL : MID_POOL);
                    var poolFinal = ring <= 1 && (bi + bj) % 2 == 0 ? MID_POOL : pool;
                    float targetH = ring == 0 ? 26f : ring == 1 ? 17f : 10f;

                    // 2x2 lotes por quarteirão
                    for (int lx = -1; lx <= 1; lx += 2)
                    {
                        for (int lz = -1; lz <= 1; lz += 2)
                        {
                            int seed = bi * 73856093 ^ bj * 19349663 ^ (lx + 2) * 83492791 ^ (lz + 2) * 29712149;
                            seed = Mathf.Abs(seed);
                            // Nem todo lote é ocupado (respiros urbanos)
                            if (seed % 7 == 0) continue;

                            var lot = new Vector3(cx + lx * 9.5f, 0f, cz + lz * 9.5f);
                            bool pilot = IsDowntownPilot(bi, bj);
                            var usePool = pilot ? MEGAKIT_POOL : poolFinal;
                            var prefabName = usePool[seed % usePool.Length];
                            float yaw = (seed % 4) * 90f;
                            float h = targetH * (0.85f + (seed % 40) / 100f);
                            SpawnBuilding(parent, prefabName, lot, yaw, h,
                                $"Bldg_{bi}_{bj}_{lx}_{lz}",
                                prefabDir: pilot ? MEGAKIT_PREFABS : null);
                        }
                    }
                }
            }
        }

        private static void SpawnBuilding(Transform parent, string prefabName, Vector3 pos, float yaw,
            float targetHeight, string name, bool withCollider = true, string prefabDir = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
                $"{prefabDir ?? CITY_PREFABS}/{prefabName}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[NeonDistrictSceneBuilder] Prefab de prédio ausente: {prefabName}");
                return;
            }

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            var bounds = ProceduralMeshLibrary.ComputeBounds(go);
            // Escala pela altura, limitado à pegada do lote (~16m)
            float s = Mathf.Min(
                targetHeight / Mathf.Max(0.01f, bounds.size.y),
                16.5f / Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z)));
            go.transform.localScale *= s;
            bounds = ProceduralMeshLibrary.ComputeBounds(go);
            go.transform.position += new Vector3(0f, -bounds.min.y, 0f);
            bounds = ProceduralMeshLibrary.ComputeBounds(go);

            if (!withCollider) return;
            // Colisor COMPOSTO por renderer — prédios com rampa/recorte não
            // criam mais colisão invisível no espaço aberto (regra ColliderFitter §5)
            ColliderFitter.FitCompoundBuilding(go, parent);
        }

        /// <summary>
        /// OLD BRICK DISTRICT — cidade vizinha ao leste (MegaKit Boston/NYC).
        /// 2 colunas × 5 fileiras de quarteirões (x=156,208), malha viária
        /// completa (ruas E-W estendidas + N-S em 182/234), calçadas/props do kit.
        /// </summary>
        private static void BuildOldBrickDistrict(Transform parent)
        {
            var root = new GameObject("OldBrickDistrict");
            root.transform.SetParent(parent);
            var matStreet = AssetDatabase.LoadAssetAtPath<Material>(
                MAT_DIR + "/Mat_ND_Road_Street.mat");
            var matWalk = AssetDatabase.LoadAssetAtPath<Material>(
                MAT_DIR + "/Mat_ND_Sidewalk.mat");

            // -- Malha viária: E-W estendidas (x 134→284) + N-S em 182/234 --
            float ewLen = OBD_ROAD_END - 134f, ewCx = 134f + ewLen * 0.5f;
            for (int j = 0; j < LINES; j++)
            {
                var r = CreateRoadQuad($"OBD_Street_EW_{j}", STREET_W, ewLen,
                    matStreet, ROAD_UV_V_PER_M, root.transform);
                r.transform.position = new Vector3(ewCx, 0.045f, StreetLine(j));
                r.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            }
            foreach (var sx in new[] { OBD_XS0, OBD_XS1 })
            {
                var r = CreateRoadQuad($"OBD_Street_NS_{(int)sx}", STREET_W,
                    CITY_SPAN + 20f, matStreet, ROAD_UV_V_PER_M, root.transform);
                r.transform.position = new Vector3(sx, 0.045f, 0f);
            }

            // -- Calçada base contínua sob o distrito (dirigível/caminhável) --
            var walk = CreateRoadQuad("OBD_Sidewalk_Base",
                OBD_ROAD_END - 128f, CITY_SPAN + 12f, matWalk, 0f, root.transform);
            walk.transform.position = new Vector3(128f + (OBD_ROAD_END - 128f) * 0.5f, 0.02f, 0f);

            // -- Quarteirões: 2×2 lotes, MEGAKIT_POOL, escala p/ lote ~16m --
            for (int bj = 0; bj < 5; bj++)
            {
                float cz = (bj - 2) * PITCH;
                foreach (float cx in new[] { OBD_X0, OBD_X1 })
                {
                    int col = cx < 190f ? 0 : 1;
                    for (int lx = -1; lx <= 1; lx += 2)
                        for (int lz = -1; lz <= 1; lz += 2)
                        {
                            int seed = col * 512461 ^ bj * 19349663
                                ^ (lx + 2) * 83492791 ^ (lz + 2) * 29712149;
                            seed = Mathf.Abs(seed);
                            var lot = new Vector3(cx + lx * 9.5f, 0f, cz + lz * 9.5f);
                            var pn = MEGAKIT_POOL[seed % MEGAKIT_POOL.Length];
                            float h = 19f * (0.85f + (seed % 40) / 100f);
                            SpawnBuilding(root.transform, pn, lot,
                                (seed % 4) * 90f, h,
                                $"OBD_Bldg_{col}_{bj}_{lx}_{lz}",
                                prefabDir: MEGAKIT_PREFABS);
                        }
                }
            }

            // -- Props do kit nas esquinas dos quarteirões OBD --
            string[] cornerProps = { "Prop_Bollard", "Sidewalk_Planter", "Prop_Planter_Single" };
            for (int bj = 0; bj < 5; bj++)
                foreach (float cx in new[] { OBD_X0, OBD_X1 })
                    for (int c = 0; c < 4; c++)
                    {
                        var pn = cornerProps[(bj + c) % cornerProps.Length];
                        var p = AssetDatabase.LoadAssetAtPath<GameObject>(
                            $"{MEGAKIT_PREFABS}/{pn}.prefab");
                        if (p == null) continue;
                        var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
                        go.transform.SetParent(root.transform);
                        go.transform.position = new Vector3(
                            cx + (c % 2 == 0 ? -17f : 17f), 0f,
                            (bj - 2) * PITCH + (c < 2 ? -17f : 17f));
                        go.transform.rotation = Quaternion.Euler(0f, c * 90f, 0f);
                        ColliderFitter.FitProp(go, pn, root.transform);
                    }
            Debug.Log("[NeonDistrictSceneBuilder] Old Brick District construído");
        }

        // ==================== ART PASS — Old Brick District ====================
        private const string POLY_PROPS = "Assets/POLYGON city pack/Prefabs/Props";
        private const string POLY_LETTERS = "Assets/POLYGON city pack/Prefabs/Letters";
        private const string POLY_SIGNS = "Assets/POLYGON city pack/Prefabs/traffic signs";
        private const string BIRDS_DIR = "Assets/Game/Resources/Art/Birds";
        private const string CP_PREFABS = "Assets/Game/Prefabs/Modular/CityPack";

        /// <summary>
        /// ART PASS OBD: calçadas do kit por quarteirão, decals de via nas
        /// interseções, densidade de props por quarteirão (luz quente de bairro
        /// velho), letreiros de térreo, praça + canto industrial, janelas acesas.
        /// </summary>
        private static void BuildOBD_ArtPass(Transform parent)
        {
            var root = new GameObject("OBD_ArtPass");
            root.transform.SetParent(parent);
            var rng = new System.Random(4242);
            GameObject Spawn(string dir, string name, Vector3 pos, float yaw)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{dir}/{name}.prefab");
                if (p == null) return null;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(p);
                go.transform.SetParent(root.transform);
                go.transform.position = pos;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                return go;
            }

            var cols = new[] { OBD_X0, OBD_X1 };   // 156, 208
            var stX = new[] { OBD_XS0, OBD_XS1 };  // ruas N-S 182, 234

            // ---- 2.2 Calçadas + decals + drains/manholes ----
            foreach (var cx in cols) for (int bj = 0; bj < 5; bj++)
            {
                float cz = (bj - 2) * PITCH;
                // calçada perímetro: linha de peças 3m nas 2 faces de rua
                for (int k = -6; k <= 6; k++)
                {
                    Spawn(MEGAKIT_PREFABS, "Sidewalk_Straight_3m",
                        new Vector3(cx + k * 3f, 0.03f, cz - 20f), 0f);
                    Spawn(MEGAKIT_PREFABS, "Sidewalk_Straight_3m",
                        new Vector3(cx + k * 3f, 0.03f, cz + 20f), 180f);
                }
            }
            // decals nas interseções das ruas N-S novas × E-W
            foreach (var sx in stX) for (int j = 0; j < LINES; j++)
            {
                float z = StreetLine(j);
                Spawn(MEGAKIT_PREFABS, "Decal_Crosswalk", new Vector3(sx, 0.07f, z - STREET_HALF - 1.5f), 0f);
                Spawn(MEGAKIT_PREFABS, "Decal_Crosswalk", new Vector3(sx, 0.07f, z + STREET_HALF + 1.5f), 180f);
                Spawn(MEGAKIT_PREFABS, "Decal_Stop", new Vector3(sx - STREET_HALF - 2f, 0.07f, z - 3f), 90f);
                Spawn(MEGAKIT_PREFABS, "Decal_DoubleYellow_Straight",
                    new Vector3(sx, 0.07f, z + (j % 2 == 0 ? 26f : -26f) * 0.5f), 0f);
                Spawn(MEGAKIT_PREFABS, "Prop_ManholeCover",
                    new Vector3(sx + 3f, 0.07f, z + 4f), 0f);
                Spawn(MEGAKIT_PREFABS, "Prop_Drain", new Vector3(sx - 5f, 0.07f, z - 5f), 0f);
            }
            // crosswalks nas E-W estendidas
            for (int j = 0; j < LINES; j++)
                foreach (var sx in stX)
                    Spawn(MEGAKIT_PREFABS, "Decal_Crosswalk_Wide",
                        new Vector3(sx - STREET_HALF - 2f, 0.07f, StreetLine(j)), 90f);

            // ---- 2.3 Densidade por quarteirão ----
            string[] lamps = { "lamp_pole_single_orange", "lamp_pole_single_white" };
            string[] trees = { "tree_1", "tree_2", "tree_3" };
            string[] signs = { "platt sign 1 prefab", "platt sign 2 prefab",
                "platt sign 3 prefab", "platt sign 4 prefab", "platt sign 5 prefab" };
            string[] storefronts = { "DELI", "BAR", "PAWN", "LAUNDRY", "LIQUOR", "CAFE" };

            foreach (var cx in cols) for (int bj = 0; bj < 5; bj++)
            {
                float cz = (bj - 2) * PITCH;
                int seed = Mathf.Abs((int)cx * 7919 ^ bj * 104729);

                // postes luz quente: 2 por lado N/S
                for (int side = -1; side <= 1; side += 2)
                    for (int k = 0; k < 2; k++)
                    {
                        var go = Spawn(CITY_PREFABS, lamps[(seed + k) % 2],
                            new Vector3(cx - 14f + k * 28f, 0f, cz + side * 18f),
                            side > 0 ? 180f : 0f);
                        if (go != null) ColliderFitter.FitProp(go, "lamp_pole", root.transform);
                    }
                // árvores 6-10 no perímetro
                int nTree = 6 + seed % 5;
                for (int k = 0; k < nTree; k++)
                {
                    float ang = k * (360f / nTree);
                    var go = Spawn(CITY_PREFABS, trees[(seed + k) % trees.Length],
                        new Vector3(cx + 17f * Mathf.Sin(ang * Mathf.Deg2Rad), 0f,
                                    cz + 17f * Mathf.Cos(ang * Mathf.Deg2Rad)),
                        (seed + k) * 47f % 360f);
                }
                // hidrante, bancos, bins, parking meter
                var hyd = Spawn(POLY_PROPS, "Hydrant prefab",
                    new Vector3(cx - 17f, 0f, cz - 17f), 0f);
                if (hyd != null) ColliderFitter.FitProp(hyd, "hydrant", root.transform);
                Spawn(POLY_PROPS, "Bench 2 prefab",
                    new Vector3(cx + 15f, 0f, cz - 15f), 270f);
                Spawn(POLY_PROPS, "Bin prefab",
                    new Vector3(cx + 15.5f, 0f, cz - 13f), 0f);
                Spawn(POLY_PROPS, "Big_trash_bin prefab",
                    new Vector3(cx - 16f, 0f, cz + 15f), 0f);
                Spawn(POLY_PROPS, "parking meter prefab",
                    new Vector3(cx - 8f, 0f, cz - 18.5f), 0f);
                Spawn(POLY_PROPS, "parking meter prefab",
                    new Vector3(cx + 8f, 0f, cz - 18.5f), 0f);
                if ((seed & 1) == 0)
                    Spawn(POLY_PROPS, "Mail_box prefab",
                        new Vector3(cx + 17f, 0f, cz + 10f), 270f);
                if ((seed & 3) == 1)
                    Spawn(POLY_PROPS, "Bus stop prefab",
                        new Vector3(cx - 12f, 0f, cz - 18.5f), 0f);
                // semáforo decorativo na esquina SE do quarteirão
                var tl = Spawn(POLY_SIGNS, $"Traffic light {(seed % 4) + 1} Prefab",
                    new Vector3(cx + 19f, 0f, cz + 19f), 180f);
                if (tl != null) ColliderFitter.FitProp(tl, "traffic_light", root.transform);
                // letreiro de loja no térreo virado p/ rua sul
                var signWord = storefronts[seed % storefronts.Length];
                float lx = cx - signWord.Length * 0.9f;
                foreach (var ch in signWord)
                {
                    var l = Spawn(POLY_LETTERS, $"Letter_{ch}_Prefab",
                        new Vector3(lx, 4.2f, cz - 20.2f), 180f);
                    lx += 1.8f;
                }
            }

            // ---- 2.4 Praça do bairro (quarteirão col 0, bj 2 — centro (156,0)) ----
            var plazaCP = Spawn(CP_PREFABS, "floor_park", new Vector3(OBD_X0, 0.05f, 0f), 0f);
            for (int k = 0; k < 6; k++)
                Spawn(POLY_PROPS, "Bench 2 prefab",
                    new Vector3(OBD_X0 - 12f + k * 5f, 0f, k % 2 == 0 ? -8f : 8f),
                    k % 2 == 0 ? 0f : 180f);
            for (int k = 0; k < 8; k++)
            {
                float ang = k * 45f * Mathf.Deg2Rad;
                Spawn(CITY_PREFABS, trees[k % 3],
                    new Vector3(OBD_X0 + 14f * Mathf.Sin(ang), 0f, 14f * Mathf.Cos(ang)),
                    k * 60f);
            }
            if (Spawn(POLY_PROPS, "statue_obelisk", new Vector3(OBD_X0, 0f, 0f), 0f) == null)
                Spawn(POLY_PROPS, "Pot_tree prefab", new Vector3(OBD_X0, 0f, 0f), 0f);

            // ---- 2.4 Canto industrial (borda leste, quarteirão col 1 bj 4) ----
            float ix = OBD_X1, iz = -104f;
            string[] industrial = { "container_blue", "container_red", "container_green",
                "container_white", "construction_barrier", "fence_a", "wall_industrial", "tractor" };
            for (int k = 0; k < industrial.Length; k++)
                Spawn(CP_PREFABS, industrial[k],
                    new Vector3(ix - 12f + (k % 4) * 8f, 0f, iz + (k / 4) * 10f), (k * 37) % 360);

            // ---- 2.5 Janelas acesas (matriz inteira: OBD + pilotos) ----
            ApplyLitWindows();

            // ---- Etapa 3: pássaros sobre praça + OBD ----
            for (int k = 0; k < 3; k++)
                Spawn(BIRDS_DIR, "01BirdPrefab",
                    new Vector3(OBD_X0 - 10f + k * 10f, 15f + k * 3f, 10f), 0f);

            Debug.Log("[NeonDistrictSceneBuilder] OBD Art Pass aplicado");
        }

        /// <summary>Janelas acesas: 4 variantes emissivas âmbar nos slots de
        /// interior/vidro dos prédios do mapa inteiro (~50%, seed fixa).</summary>
        private static void ApplyLitWindows()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            var variants = new Material[4];
            var warm = new Color(1f, 0.85f, 0.63f); // #FFD9A0
            for (int i = 0; i < 4; i++)
            {
                var path = $"{MAT_DIR}/Mat_WinLit_{i}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(shader);
                    var src = AssetDatabase.LoadAssetAtPath<Material>(
                        $"Assets/Game/Materials/DowntownMegaKit/Mat_DT_lit_interior_{(i % 2) + 1}.mat");
                    if (src != null) { m.CopyPropertiesFromMaterial(src); m.shader = shader; }
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", warm * (0.6f + i * 0.5f));
                    AssetDatabase.CreateAsset(m, path);
                }
                variants[i] = m;
            }
            int lit = 0, total = 0;
            var rng = new System.Random(777);
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.transform.root.name.Contains("Bldg") &&
                    !r.name.Contains("Building")) continue;
                var arr = r.sharedMaterials;
                bool touched = false;
                bool litB = rng.Next(100) < 75; // v3: 70-80% prédios acesos
                for (int i = 0; i < arr.Length; i++)
                {
                    var n = arr[i] != null ? arr[i].name.ToLowerInvariant() : "";
                    if (n.Contains("interior") || n.Contains("glass") || n.Contains("window"))
                    { total++; touched = true; if (litB) arr[i] = variants[rng.Next(4)]; }
                }
                if (touched) { r.sharedMaterials = arr; if (litB) lit++; }
            }
            Debug.Log($"[ArtPass] janelas acesas: {lit} prédios ({total} slots)");
        }

        /// <summary>
        /// A1 — auditoria de invasão de via: para cada prédio, AABB XZ vs
        /// corredores (rua 12m + calçada 3m; avenida 16m + calçada). Invasor é
        /// re-encaixado ao quarteirão (encolhe/descola da via).
        /// </summary>
        public static void AuditBuildingFootprints()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Game/Scenes/NeonDistrict.unity");
            var roads = new System.Collections.Generic.List<(float lo, float hi, bool isX)>();
            for (int i = 0; i < LINES; i++)
            {
                float s = StreetLine(i);
                float hw = (i == AVENUE_J ? AVENUE_W : STREET_W) * 0.5f + 3f; // rua+calçada
                roads.Add((s - hw, s + hw, false)); // faixa Z de rua E-W
                roads.Add((s - hw, s + hw, true));  // faixa X de rua N-S
            }
            roads.Add((OBD_XS0 - 9f, OBD_XS0 + 9f, true));
            roads.Add((OBD_XS1 - 9f, OBD_XS1 + 9f, true));

            int fixed_ = 0, total = 0;
            var log = new System.Text.StringBuilder();
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Bldg_") && !t.name.StartsWith("OBD_Bldg_")) continue;
                var rs = t.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;
                total++;
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                bool invades = false;
                foreach (var (lo, hi, isX) in roads)
                {
                    float mn = isX ? b.min.x : b.min.z, mx = isX ? b.max.x : b.max.z;
                    if (mn < hi && mx > lo) { invades = true; break; }
                }
                if (!invades) continue;
                // corrige: encolhe 8% e desloca pro centro do quarteirão mais próximo
                t.localScale *= 0.92f;
                float bx = Mathf.Round(t.position.x / PITCH) * PITCH;
                float bz = Mathf.Round(t.position.z / PITCH) * PITCH;
                var toCenter = new Vector3(bx, 0f, bz) - t.position;
                toCenter.y = 0f;
                if (toCenter.magnitude > 0.1f)
                    t.position += toCenter.normalized * Mathf.Min(toCenter.magnitude * 0.15f, 4f);
                fixed_++;
                log.Append($"{t.name} ");
            }
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[A1-Audit] {fixed_}/{total} prédios corrigidos: {log}");
        }

        /// <summary>
        /// A3 — cidade acesa: cabeça emissiva + poça de luz em todos os postes
        /// (âmbar OBD / branco-rosa downtown) + letreiros neon Letter_* em ≥60%
        /// dos prédios do centro (horizontal/vertical/marquee).
        /// </summary>
        public static void ApplyCityLights()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Game/Scenes/NeonDistrict.unity");
            var shader = Shader.Find("Universal Render Pipeline/Lit");

            Material LampMat(Color c, string nm)
            {
                var path = $"{MAT_DIR}/{nm}.mat";
                var m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null)
                {
                    m = new Material(shader);
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", c * 2.2f);
                    m.SetColor("_BaseColor", c * 0.15f);
                    AssetDatabase.CreateAsset(m, path);
                }
                return m;
            }
            var amberLamp = LampMat(new Color(1f, 0.8f, 0.5f), "Mat_Lamp_Amber");
            var neonPink = LampMat(new Color(1f, 0.2f, 0.7f), "Mat_Lamp_Pink");
            var neonWhite = LampMat(new Color(0.9f, 0.95f, 1f), "Mat_Lamp_White");
            var neonCyan = LampMat(new Color(0.2f, 0.9f, 1f), "Mat_Neon_Cyan");
            var poolMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            poolMat.color = new Color(1f, 0.75f, 0.4f, 0.18f);
            poolMat.SetFloat("_Surface", 1f);
            poolMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            poolMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);

            int lampsDone = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var n = t.name.ToLowerInvariant();
                if (!n.Contains("lamp_pole") && !n.Contains("street_lamp") && !n.Contains("street lamp"))
                    continue;
                var rs = t.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                float topY = b.max.y;
                float cx = b.center.x, cz = b.center.z;
                bool obd = t.position.x > 134f;
                if (t.Find("LampGlow") != null) continue; // idempotente
                var headMat = obd ? amberLamp
                    : n.Contains("blue") ? neonCyan
                    : n.Contains("pink") ? neonPink : neonWhite;
                // cabeça emissiva no topo do poste
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "LampGlow";
                head.transform.position = new Vector3(cx, topY - 0.15f, cz);
                head.transform.localScale = Vector3.one * 0.55f;
                head.GetComponent<Renderer>().sharedMaterial = headMat;
                var col = head.GetComponent<Collider>();
                if (col != null) UnityEngine.Object.DestroyImmediate(col);
                head.transform.SetParent(t);
                // poça de luz no chão
                var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pool.name = "LightPool";
                pool.transform.position = new Vector3(cx, 0.06f, cz);
                pool.transform.localScale = new Vector3(6f, 0.015f, 6f);
                pool.GetComponent<Renderer>().sharedMaterial = poolMat;
                var col2 = pool.GetComponent<Collider>();
                if (col2 != null) UnityEngine.Object.DestroyImmediate(col2);
                pool.transform.SetParent(t);
                lampsDone++;
                // Point Light real SÓ na avenida (|z-26|<10) — teste de budget
                if (Mathf.Abs(t.position.z - StreetLine(AVENUE_J)) < 10f && t.position.x < 134f)
                {
                    var pl = new GameObject("LampPointLight");
                    pl.transform.SetParent(t);
                    pl.transform.position = new Vector3(cx, topY - 0.4f, cz);
                    var li = pl.AddComponent<Light>();
                    li.type = LightType.Point;
                    li.intensity = 2.4f;
                    li.range = 11f;
                    li.color = headMat.GetColor("_EmissionColor");
                    li.shadows = LightShadows.None; // shadow map custa caro
                }
            }

            // Point Lights reais SÓ na avenida — passe separado (postes já tinham LampGlow)
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var n = t.name.ToLowerInvariant();
                if (!n.Contains("lamp_pole") && !n.Contains("street_lamp") && !n.Contains("street lamp"))
                    continue;
                if (t.Find("LampPointLight") != null) continue;
                if (Mathf.Abs(t.position.z - StreetLine(AVENUE_J)) > 10f || t.position.x > 134f) continue;
                var glow = t.Find("LampGlow");
                var pl = new GameObject("LampPointLight");
                pl.transform.SetParent(t);
                pl.transform.position = glow != null ? glow.position + Vector3.down * 0.3f : t.position;
                var li = pl.AddComponent<Light>();
                li.type = LightType.Point;
                li.intensity = 2.4f;
                li.range = 11f;
                li.color = glow != null ? glow.GetComponent<Renderer>().sharedMaterial.GetColor("_EmissionColor")
                                        : new Color(1f, 0.85f, 0.6f);
                li.shadows = LightShadows.None;
            }
            // Neon do centro: words emissivos nos prédios downtown (x<134)
            string[] words = { "MOTEL", "BAR", "CLUB", "HOTEL", "DINER", "PAWN",
                               "LIQUOR", "DELI", "CASINO", "24H" };
            var rng = new System.Random(909);
            int signs = 0, bldgs = 0;
            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!t.name.StartsWith("Bldg_")) continue;
                var rs = t.GetComponentsInChildren<Renderer>(true);
                if (rs.Length == 0) continue;
                if (t.Find("NeonSign") != null) continue; // já tem letreiro
                var b = rs[0].bounds;
                foreach (var r in rs) b.Encapsulate(r.bounds);
                // _bgr são fundo de skyline — não contam na meta de ≥60%
                if (t.name.Contains("_bgr")) continue;
                bldgs++;
                if (rng.Next(100) >= 75) continue; // ~75% dos elegíveis
                var word = words[rng.Next(words.Length)];
                int fmt = rng.Next(3); // 0=horizontal térreo, 1=vertical, 2=marquee
                float y0 = fmt == 1 ? b.max.y * 0.35f : fmt == 2 ? 3.4f : 4.4f;
                var mat = rng.Next(2) == 0 ? neonPink : neonCyan;
                // face sul do prédio (voltada p/ rua)
                float zFace = b.min.z - 0.3f;
                var signRoot = new GameObject("NeonSign");
                signRoot.transform.SetParent(t);
                if (fmt == 1) // vertical suspenso
                {
                    for (int k = 0; k < word.Length; k++)
                    {
                        var lp = AssetDatabase.LoadAssetAtPath<GameObject>(
                            $"{POLY_LETTERS}/Letter_{word[k]}_Prefab.prefab");
                        if (lp == null) break;
                        var g = (GameObject)PrefabUtility.InstantiatePrefab(lp);
                        g.transform.position = new Vector3(b.center.x, y0 + k * 1.9f, zFace);
                        g.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                        g.transform.localScale = Vector3.one * 1.4f;
                        foreach (var rr in g.GetComponentsInChildren<Renderer>())
                            rr.sharedMaterial = mat;
                        g.transform.SetParent(signRoot.transform);
                    }
                }
                else // horizontal térreo / marquee
                {
                    float lx = b.center.x - word.Length * 0.7f;
                    foreach (var ch in word)
                    {
                        var lp = AssetDatabase.LoadAssetAtPath<GameObject>(
                            $"{POLY_LETTERS}/Letter_{ch}_Prefab.prefab");
                        if (lp == null) break;
                        var g = (GameObject)PrefabUtility.InstantiatePrefab(lp);
                        g.transform.position = new Vector3(lx, y0, zFace);
                        g.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
                        g.transform.localScale = Vector3.one * 1.2f;
                        foreach (var rr in g.GetComponentsInChildren<Renderer>())
                            rr.sharedMaterial = mat;
                        g.transform.SetParent(signRoot.transform);
                        lx += 1.4f;
                    }
                }
                signs++;
            }
            // Controller dia/noite (ambiente) — dusk permanente => aceso por default
            if (UnityEngine.Object.FindFirstObjectByType<Studio.Foundation.CityLightingController>() == null)
            {
                var ctrl = new GameObject("CityLightingController");
                ctrl.AddComponent<Studio.Foundation.CityLightingController>();
            }
            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[A3] postes acesos={lampsDone} | neon: {signs}/{bldgs} prédios");
        }

        /// <summary>A1 prova: render ortográfico topo com corredores de via em
        /// overlay vermelho — qualquer prédio sobre via fica visível.</summary>
        public static void CaptureAuditTopDown()
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Game/Scenes/NeonDistrict.unity");
            var overlay = new GameObject("AuditOverlay");
            var matRed = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            matRed.color = new Color(1f, 0f, 0f, 0.45f);
            matRed.SetFloat("_Surface", 1f); // transparent
            matRed.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            matRed.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            for (int i = 0; i < LINES; i++)
            {
                float s = StreetLine(i);
                float hw = (i == AVENUE_J ? AVENUE_W : STREET_W) * 0.5f + 3f;
                var q1 = GameObject.CreatePrimitive(PrimitiveType.Cube); // E-W
                q1.transform.SetParent(overlay.transform);
                q1.transform.position = new Vector3(80f, 0.5f, s);
                q1.transform.localScale = new Vector3(600f, 0.1f, hw * 2f);
                q1.GetComponent<Renderer>().sharedMaterial = matRed;
                var q2 = GameObject.CreatePrimitive(PrimitiveType.Cube); // N-S
                q2.transform.SetParent(overlay.transform);
                q2.transform.position = new Vector3(s, 0.5f, 0f);
                q2.transform.localScale = new Vector3(hw * 2f, 0.1f, 600f);
                q2.GetComponent<Renderer>().sharedMaterial = matRed;
            }
            foreach (var sx in new[] { OBD_XS0, OBD_XS1 })
            {
                var q = GameObject.CreatePrimitive(PrimitiveType.Cube);
                q.transform.SetParent(overlay.transform);
                q.transform.position = new Vector3(sx, 0.5f, 0f);
                q.transform.localScale = new Vector3(18f, 0.1f, 600f);
                q.GetComponent<Renderer>().sharedMaterial = matRed;
            }
            var cam = new GameObject("AuditCam").AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = 230f;
            cam.transform.position = new Vector3(80f, 300f, 0f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            var rt = new RenderTexture(1280, 900, 24);
            cam.targetTexture = rt;
            for (int i = 0; i < 6; i++) cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 900), 0, 0); tex.Apply();
            File.WriteAllBytes("../evidence/captures/audit_topdown.png", tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(overlay);
            UnityEngine.Object.DestroyImmediate(cam.gameObject);
            Debug.Log("[A1-Audit] captura: audit_topdown.png");
        }

        /// <summary>A4: diversidade de piso por zona — avenida própria (4Lane+faixas),
        /// OBD desgastado, praças em patches Street_Asphalt. ≤2 materiais novos.</summary>
        public static void ApplyRoadSurfaces()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH);
            const string MEGA = "Assets/Game/Prefabs/Modular/DowntownMegaKit";

            // Material base existente das ruas (pega de uma via real)
            Material baseMat = null;
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (r.name.StartsWith("Street_EW_")) { baseMat = r.sharedMaterial; break; }
            if (baseMat == null) { Debug.LogError("[A4] material de rua base não achado"); return; }

            Material Tint(Color c, string nm)
            {
                var m = new Material(baseMat) { color = c, name = nm };
                AssetDatabase.CreateAsset(m, $"{MAT_DIR}/{nm}.mat");
                return m;
            }
            var matAvenue = Tint(new Color(0.16f, 0.14f, 0.20f), "Mat_Road_Avenue"); // asfalto escuro azulado
            var matOBD    = Tint(new Color(0.34f, 0.28f, 0.24f), "Mat_Road_OBD");    // desgastado marrom-cinza

            int retinted = 0;
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var n = r.name;
                if (n == "Avenue_Neon_EW") { r.sharedMaterial = matAvenue; retinted++; }
                else if (n.StartsWith("OBD_Street_") ||
                         (n.StartsWith("Intersection_") && r.bounds.center.x > 134f))
                { r.sharedMaterial = matOBD; retinted++; }
            }

            // Marcações de 4Lane na avenida: double-yellow no eixo + broken lines nas faixas
            var dy  = AssetDatabase.LoadAssetAtPath<GameObject>($"{MEGA}/Decal_DoubleYellow_Straight.prefab");
            var bl  = AssetDatabase.LoadAssetAtPath<GameObject>($"{MEGA}/Decal_BrokenLine_Straight.prefab");
            int marks = 0;
            for (float x = -120f; x <= 120f; x += 12f)
            {
                if (dy != null)
                {
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(dy);
                    g.transform.position = new Vector3(x, 0.02f, StreetLine(AVENUE_J));
                    marks++;
                }
                foreach (var zoff in new[] { -4f, 4f })
                    if (bl != null)
                    {
                        var g = (GameObject)PrefabUtility.InstantiatePrefab(bl);
                        g.transform.position = new Vector3(x, 0.02f, StreetLine(AVENUE_J) + zoff);
                        marks++;
                    }
            }

            // Patches de asfalto (pré-req da rotatória + praças) — nos 4 cantos do Central Plaza
            var ap = AssetDatabase.LoadAssetAtPath<GameObject>($"{MEGA}/Street_Asphalt_9x9.prefab");
            if (ap != null)
                foreach (var p in new[] { new Vector3(-26f, 0.01f, -26f), new Vector3(26f, 0.01f, -26f),
                                        new Vector3(-26f, 0.01f, 26f), new Vector3(26f, 0.01f, 26f) })
                {
                    var g = (GameObject)PrefabUtility.InstantiatePrefab(ap);
                    g.name = "AsphaltPatch_Plaza";
                    g.transform.position = p;
                    var c = g.GetComponentInChildren<Collider>();
                    if (c != null) UnityEngine.Object.DestroyImmediate(c);
                }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[A4] retinted={retinted} | faixas avenida={marks} | patches=4");
        }

        /// <summary>FASE B: boulevard curvo pro leste + rotatória + diagonal.
        /// Grid atual INTACTO — só adiciona. Sem tráfego IA (TrafficNetwork é grid-only).</summary>
        public static void BuildCurvedBoulevard()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH);
            const string MEGA = "Assets/Game/Prefabs/Modular/DowntownMegaKit";
            if (GameObject.Find("CurvedBoulevard") != null) { Debug.Log("[B] já existe"); return; }
            var root = new GameObject("CurvedBoulevard");

            GameObject Place(string prefab, Vector3 pos, float yaw, string nm)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{MEGA}/{prefab}.prefab");
                if (p == null) { Debug.LogWarning($"[B] falta {prefab}"); return null; }
                var g = (GameObject)PrefabUtility.InstantiatePrefab(p);
                g.name = nm;
                g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
                g.transform.SetParent(root.transform);
                return g;
            }

            int n = 0;
            // 1) Rotatória na ponta leste da avenida (x≈290, z=26): anel de curvas + miolo
            var rCenter = new Vector3(292f, 0f, StreetLine(AVENUE_J));
            for (int k = 0; k < 4; k++)
            {
                float yaw = k * 90f;
                var rad = yaw * Mathf.Deg2Rad;
                Place("Street_Curve_2Lane",
                      rCenter + new Vector3(Mathf.Cos(rad) * 10f, 0.01f, Mathf.Sin(rad) * 10f),
                      yaw, $"Roundabout_Ring_{k}");
            }
            Place("Street_Asphalt_9x9", rCenter + Vector3.up * 0.015f, 0f, "Roundabout_Center");
            Place("Street_Asphalt_9x9", rCenter + new Vector3(0f, 0.015f, 0f), 0f, "Roundabout_Pad");

            // 2) Boulevard curvo: arco saindo da rotatória pro nordeste (rumo ao Green Hills)
            //    Segmentos Street_2Lane tangenciais a um arco — lê como curva, dirigível.
            float R = 55f;
            var c = new Vector3(292f, 0f, 26f + R); // centro do arco ao norte da rotatória
            for (float a = 200f; a >= 115f; a -= 9f)
            {
                var rad = a * Mathf.Deg2Rad;
                var pos = new Vector3(c.x + Mathf.Cos(rad) * R, 0.01f, c.z + Mathf.Sin(rad) * R);
                float tangent = Mathf.Atan2(-Mathf.Cos(rad), -Mathf.Sin(rad)) * Mathf.Rad2Deg;
                Place("Street_2Lane", pos, tangent + 90f, $"Blvd_Arc_{(int)a}");
                n++;
            }

            // 3) Conexão diagonal: (284,78) → rotatória norte, chord reto inclinado
            for (int k = 0; k <= 6; k++)
            {
                float t = k / 6f;
                var pos = Vector3.Lerp(new Vector3(284f, 0.01f, 78f),
                                       new Vector3(300f, 0.01f, 40f), t);
                Place("Street_2Lane", pos, 24f, $"Diagonal_A_{k}");
            }

            // 4) Decals: double-yellow nas curvas + crosswalk onde encontra a calçada do OBD
            for (float a = 195f; a >= 120f; a -= 18f)
            {
                var rad = a * Mathf.Deg2Rad;
                var pos = new Vector3(c.x + Mathf.Cos(rad) * R, 0.03f, c.z + Mathf.Sin(rad) * R);
                float tangent = Mathf.Atan2(-Mathf.Cos(rad), -Mathf.Sin(rad)) * Mathf.Rad2Deg;
                Place("Decal_Curve_2Lane_Stripe", pos, tangent + 90f, $"Decal_Arc_{(int)a}");
            }
            Place("Decal_Crosswalk", new Vector3(284f, 0.03f, 26f), 90f, "XWalk_BlvdEntry");
            Place("Decal_Stop", new Vector3(280f, 0.03f, 26f), 90f, "Stop_BlvdEntry");
            Place("Decal_Curve_4LaneShort_DoubleYellow",
                  new Vector3(292f, 0.03f, 14f), 0f, "Roundabout_Yellow_S");

            UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
            Debug.Log($"[B] boulevard: arco={n} segs + rotatória + diagonal — sem IA (grid-only)");
        }

        /// <summary>A3 prova: aérea do centro no dusk + street-level poste aceso.</summary>
        public static void CaptureLitCity()
        {
            var scene = EditorSceneManager.OpenScene(SCENE_PATH);
            Shot(new Vector3(0f, 130f, -60f), Quaternion.Euler(55f, 0f, 0f),
                 "../evidence/captures/aerial_neon_dusk.png");
            // poste aceso de perto na avenida (x=10, z=26 avenida)
            Shot(new Vector3(12f, 3.5f, 20f), Quaternion.Euler(10f, 30f, 0f),
                 "../evidence/captures/street_lamp_closeup.png");
            Debug.Log("[A3] capturas: aerial_neon_dusk + street_lamp_closeup");
        }

        private static void Shot(Vector3 pos, Quaternion rot, string path)
        {
            var camGo = new GameObject("ShotCam");
            var cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 55f;
            cam.transform.SetPositionAndRotation(pos, rot);
            var rt = new RenderTexture(1280, 720, 24);
            cam.targetTexture = rt;
            for (int i = 0; i < 8; i++) cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(camGo);
            RenderTexture.active = null;
        }

        /// <summary>Anel de prédios _bgr (background) além do distrito — fecha o horizonte.</summary>
        private static void BuildSkylineRing(Transform parent, Material matFar)
        {
            // Chão distante escuro cobrindo até além do horizonte
            var far = CreateRoadQuad("FarGround", 1600f, 1600f, matFar, 0f, parent);
            far.transform.position = new Vector3(0f, -0.15f, 0f);

            var bgrPool = new[] { "office_building_1_bgr", "office_building_2_bgr",
                "office_building_3_bgr", "office_building_4_bgr",
                "factory_building_big_bgr", "factory_building_small_bgr" };
            int seed = 7;
            for (float a = 0f; a < 360f; a += 24f)
            {
                float rad = a * Mathf.Deg2Rad;
                float dist = 175f + (seed % 3) * 25f;
                var pos = new Vector3(Mathf.Cos(rad) * dist, 0f, Mathf.Sin(rad) * dist);
                // Evita sobrepor a grade de ruas (aneis externos apenas)
                if (Mathf.Abs(pos.x) < 150f && Mathf.Abs(pos.z) < 150f) { seed++; continue; }
                SpawnBuilding(parent, bgrPool[seed % bgrPool.Length], pos,
                    (seed * 43) % 360f, 22f + (seed % 4) * 11f, $"Skyline_{(int)a}", withCollider: false);
                seed++;
            }
        }

        private static void BuildPlaza(Transform parent)
        {
            var plaza = new GameObject("Central_Plaza");
            plaza.transform.SetParent(parent, false);
            plaza.transform.position = Vector3.zero;

            // Anel de hedges no perímetro (4 lados, 4 peças por lado)
            for (int side = 0; side < 4; side++)
            {
                for (int k = -1; k <= 1; k++)
                {
                    float along = k * 11f;
                    var p = side switch
                    {
                        0 => new Vector3(along, 0f, -17f),
                        1 => new Vector3(along, 0f, 17f),
                        2 => new Vector3(-17f, 0f, along),
                        _ => new Vector3(17f, 0f, along),
                    };
                    float yaw = side < 2 ? 0f : 90f;
                    SpawnProp(plaza.transform, "hedge_with_base", p, yaw, $"Plaza_Hedge_{side}_{k}", 0f);
                }
            }

            // Árvores nos cantos + centro-sul
            SpawnProp(plaza.transform, "tree_1", new Vector3(-14f, 0f, -14f), 0f, "Plaza_Tree_1");
            SpawnProp(plaza.transform, "tree_2", new Vector3(14f, 0f, -14f), 40f, "Plaza_Tree_2");
            SpawnProp(plaza.transform, "tree_3", new Vector3(-14f, 0f, 14f), 200f, "Plaza_Tree_3");
            SpawnProp(plaza.transform, "tree_2", new Vector3(14f, 0f, 14f), 130f, "Plaza_Tree_4");
            SpawnProp(plaza.transform, "tree_1", new Vector3(0f, 0f, -6f), 80f, "Plaza_Tree_5");

            // Bancos voltados ao centro
            SpawnProp(plaza.transform, "bench", new Vector3(-6f, 0f, 3f), 90f, "Plaza_Bench_1");
            SpawnProp(plaza.transform, "bench", new Vector3(6f, 0f, 3f), -90f, "Plaza_Bench_2");
            SpawnProp(plaza.transform, "bench", new Vector3(-6f, 0f, -3f), 90f, "Plaza_Bench_3");
            SpawnProp(plaza.transform, "bench", new Vector3(6f, 0f, -3f), -90f, "Plaza_Bench_4");

            // Iluminação da praça: postes baixos com luz ativa
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var rad = a * Mathf.Deg2Rad;
                SpawnProp(plaza.transform, "lamp_pole_small",
                    new Vector3(Mathf.Cos(rad) * 12f, 0f, Mathf.Sin(rad) * 12f), a + 180f, $"Plaza_Lamp_{i}", 1f);
            }
        }

        private static void BuildStreetProps(Transform parent)
        {
            var lampPool = new[] { "lamp_pole_single_orange", "lamp_pole_single_white",
                "lamp_pole_single_pink", "lamp_pole_single_blue" };
            var dualPool = new[] { "lamp_pole_dual_orange", "lamp_pole_dual_white",
                "lamp_pole_dual_pink" };
            var treePool = new[] { "tree_1", "tree_2", "tree_3" };

            int lampIdx = 0;

            // Postes simples nas ruas N-S e E-W (exceto avenida): a cada ~52m,
            // alternando lado; Light ativa só a cada 3º (orçamento de luzes)
            for (int i = 0; i < LINES; i++)
            {
                for (int k = -2; k <= 2; k++)
                {
                    float along = k * 50f + ((i % 2) * 26f);
                    // N-S: poste na calçada leste da rua i
                    var p1 = new Vector3(StreetLine(i) + STREET_HALF + 1.1f, 0f, along);
                    SpawnProp(parent, lampPool[lampIdx % lampPool.Length], p1, 180f,
                        $"Lamp_NS_{i}_{k}", lampIdx % 3 == 0 ? 1f : 0f);
                    lampIdx++;
                    // E-W: poste na calçada norte da rua i
                    var p2 = new Vector3(along, 0f, StreetLine(i) - STREET_HALF - 1.1f);
                    SpawnProp(parent, lampPool[lampIdx % lampPool.Length], p2, 0f,
                        $"Lamp_EW_{i}_{k}", lampIdx % 3 == 0 ? 1f : 0f);
                    lampIdx++;
                }
            }

            // Postes duplos no canteiro da Avenida Neon (a cada ~26m)
            int dualIdx = 0;
            for (float x = -117f; x <= 117f; x += 26f)
            {
                SpawnProp(parent, dualPool[dualIdx % dualPool.Length],
                    new Vector3(x, 0f, StreetLine(AVENUE_J)), 90f,
                    $"Avenue_Dual_{(int)x}", dualIdx % 2 == 0 ? 1f : 0f);
                dualIdx++;
            }

            // Árvores nas calçadas (esparsas, deterministic)
            for (int i = 0; i < LINES; i++)
            {
                for (int k = -2; k <= 2; k++)
                {
                    if ((i + k) % 2 == 0) continue;
                    float along = k * 47f + 13f;
                    SpawnProp(parent, treePool[Mathf.Abs(i * 3 + k) % treePool.Length],
                        new Vector3(StreetLine(i) - STREET_HALF - 1.3f, 0f, along), (i * 61 + k * 37) % 360f,
                        $"Tree_NS_{i}_{k}");
                    SpawnProp(parent, treePool[Mathf.Abs(i * 5 + k) % treePool.Length],
                        new Vector3(along, 0f, StreetLine(i) + STREET_HALF + 1.3f), (i * 53 + k * 41) % 360f,
                        $"Tree_EW_{i}_{k}");
                }
            }
        }

        /// <summary>
        /// Instancia prop do pack Demo City; lightFactor > 0 mantém Lights do
        /// prefab ativas, 0 desativa (orçamento mobile).
        /// </summary>
        private static GameObject SpawnProp(Transform parent, string prefabName, Vector3 pos,
            float yaw, string name, float lightFactor = -1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CITY_PREFABS}/{prefabName}.prefab");
            if (prefab == null)
            {
                Debug.LogWarning($"[NeonDistrictSceneBuilder] Prop ausente: {prefabName}");
                return null;
            }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            go.name = name;
            go.transform.SetParent(parent, true);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            if (lightFactor >= 0f)
            {
                foreach (var l in go.GetComponentsInChildren<Light>(true))
                    l.enabled = lightFactor > 0f;
            }

            // Colisor via ColliderFitter (perfil por arquétipo: poste/árvore = só
            // tronco/haste; barreiras = AABB completo). Root-level, world-aligned.
            if (prefabName.StartsWith("lamp_pole") || prefabName.StartsWith("tree")
                || prefabName.StartsWith("bench") || prefabName.StartsWith("hedge")
                || prefabName.StartsWith("concrete_block") || prefabName.StartsWith("fence"))
            {
                // Colisor FILHO do prop → o conjunto tomba junto (breakables)
                bool breakable = prefabName.StartsWith("lamp_pole");
                bool draggable = prefabName.StartsWith("bench");
                var colObj = ColliderFitter.FitProp(go, prefabName,
                    breakable || draggable ? go.transform : parent);
                if (breakable || draggable)
                    UnityEditor.GameObjectUtility.SetStaticEditorFlags(go, 0);
                if (breakable)
                {
                    // Poste: rb kinematic na raiz + BreakableProp — ao quebrar
                    // o prop INTEIRO tomba (colisor+renderer caem juntos)
                    var rb = go.AddComponent<Rigidbody>();
                    rb.isKinematic = true; rb.mass = 60f;
                    go.AddComponent<Studio.Gameplay.BreakableProp>();
                }
                else if (draggable)
                {
                    // Banco leve: rb dinâmico 22 kg — empurrado pelo para-choque
                    var rb = go.AddComponent<Rigidbody>();
                    rb.mass = 22f; rb.isKinematic = false;
                }
            }
            return go;
        }

        private static void BuildBoundsBlockers(Transform parent)
        {
            const int IGNORE_RAYCAST = 2; // bala passa (não deixa marca no ar)
            float half = CITY_SPAN * 0.5f + 2f;
            var defs = new (string n, Vector3 c, Vector3 s)[]
            {
                ("Bounds_Blocker_N", new Vector3(0f, 5f, half), new Vector3(CITY_SPAN + 10f, 10f, 1f)),
                ("Bounds_Blocker_S", new Vector3(0f, 5f, -half), new Vector3(CITY_SPAN + 10f, 10f, 1f)),
                ("Bounds_Blocker_E", new Vector3(OBD_ROAD_END + 8f, 5f, 0f), new Vector3(1f, 10f, CITY_SPAN + 10f)),
                ("Bounds_Blocker_W", new Vector3(-half, 5f, 0f), new Vector3(1f, 10f, CITY_SPAN + 10f)),
            };
            foreach (var (n, c, s) in defs)
            {
                var go = new GameObject(n);
                go.layer = IGNORE_RAYCAST; // colisão física sim, raycast/tiro não
                go.transform.SetParent(parent, false);
                go.transform.position = c;
                var col = go.AddComponent<BoxCollider>();
                col.size = s;
            }
        }

        #endregion

        #region Rede de tráfego

        private static List<TrafficEdge> BuildTrafficEdges()
        {
            // xs estendido ao leste p/ cobrir o Old Brick District (ruas N-S
            // em 182/234) — BuildGrid agora aceita xs/zs assimétricos.
            var xs = new float[LINES + 2];
            var zs = new float[LINES];
            for (int i = 0; i < LINES; i++) { xs[i] = StreetLine(i); zs[i] = StreetLine(i); }
            xs[LINES] = OBD_XS0; xs[LINES + 1] = OBD_XS1;
            var edges = TrafficNetworkBuilder.BuildGrid(
                xs, zs, STREET_HALF, LANE_OFF, AVENUE_J, AVENUE_HALF, AVENUE_LANE);
            Debug.Log($"[NeonDistrictSceneBuilder] Rede de tráfego: {edges.Count} arestas");
            return edges;
        }

        /// <summary>Instala variantes danificadas nos prefabs de tráfego (edit+sava).</summary>
        private static void InstallTrafficVariants()
        {
            foreach (var n in TRAFFIC_PREFAB_NAMES)
            {
                string path = $"{TRAFFIC_PREFABS}/{n}.prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                InstallDamageVariants(root, n);
                PrefabUtility.SaveAsPrefabAsset(root, path);
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static List<GameObject> LoadTrafficPrefabs()
        {
            var list = new List<GameObject>();
            foreach (var n in TRAFFIC_PREFAB_NAMES)
            {
                var p = AssetDatabase.LoadAssetAtPath<GameObject>($"{TRAFFIC_PREFABS}/{n}.prefab");
                if (p != null) list.Add(p);
                else Debug.LogWarning($"[NeonDistrictSceneBuilder] Prefab de tráfego ausente: {n}");
            }
            return list;
        }

        #endregion

        #region Carro do jogador, câmera, pós e HUD

        private static GameObject BuildPlayerCar(Material matSkid)
        {
            var wrapper = new GameObject("PlayerCar");
            wrapper.tag = GameConstants.TAG_PLAYER;
            // Spawn na faixa leste da Avenida Neon (z = 26 - 4 = 22, heading +X)
            var spawnPos = new Vector3(-60f, 0.15f, StreetLine(AVENUE_J) - AVENUE_LANE);
            wrapper.transform.SetPositionAndRotation(spawnPos, Quaternion.Euler(0f, 90f, 0f));

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PLAYER_CAR_PREFAB);
            GameObject model = null;
            if (prefab != null)
            {
                model = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                model.name = "SportCar_Model";
                model.transform.SetParent(wrapper.transform, false);
                model.transform.localPosition = Vector3.zero;
                model.transform.localRotation = Quaternion.identity;

                // Escala para ~4.5m de comprimento e recentraliza base no y=0
                var bounds = ProceduralMeshLibrary.ComputeBounds(model);
                float s = 4.5f / Mathf.Max(0.01f, Mathf.Max(bounds.size.x, bounds.size.z));
                model.transform.localScale *= s;
                bounds = ProceduralMeshLibrary.ComputeBounds(model);
                model.transform.position += wrapper.transform.position - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            }
            else
            {
                Debug.LogWarning("[NeonDistrictSceneBuilder] SportCar_1.prefab ausente — fallback de chassis kit");
                model = ProceduralMeshLibrary.CreateKitInstance("raceCarRed", "SportCar_Model", wrapper.transform, spawnPos);
            }

            var carBounds = ProceduralMeshLibrary.ComputeBounds(wrapper);

            // Física arcade
            var rb = wrapper.AddComponent<Rigidbody>();
            rb.mass = 1350f;
            rb.linearDamping = 0.05f;
            rb.angularDamping = 3f;

            var car = wrapper.AddComponent<PlayerCarController>();
            var vdcPlayer = wrapper.AddComponent<Studio.Gameplay.VehicleDamageController>();
            vdcPlayer.ConfigureHealth(350f); // player: 350 HP (tráfego: 140)
            InstallDamageVariants(wrapper, "SportCar_1");

            // Rodas do modelo (FL/FR esterçam, todas giram)
            Transform FindWheel(string n)
            {
                var t = wrapper.transform.Find($"SportCar_Model/{n}");
                if (t == null)
                {
                    foreach (var c in wrapper.GetComponentsInChildren<Transform>(true))
                        if (c.name == n) return c;
                }
                return t;
            }

            var fl = FindWheel("FL");
            var fr = FindWheel("FR");
            var rl = FindWheel("RL");
            var rr = FindWheel("RR");

            // O prefab SportCar_1 tem mounts de roda vazios — instanciar Sport_Wheel neles.
            // Diâmetro da roda = 2× a altura do mount acima da base do carro (roda
            // assenta no chão, não atravessa).
            var wheelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/SportCar/Prefabs/Wheels/Sport/Sport_Wheel_1.prefab");
            float wheelRadius = 0.30f;
            float carBaseY = carBounds.min.y; // base real do modelo (spawnPos.y)
            // Diâmetro visual: roda esportiva ~14% do comprimento — e levanta o
            // carro para que a roda encoste no chão (mount costuma ser baixo).
            float targetDiam = Mathf.Clamp(carBounds.size.x * 0.14f, 0.45f, 0.85f);
            float maxMountH = 0f;
            foreach (var mount in new[] { fl, fr, rl, rr })
                if (mount != null) maxMountH = Mathf.Max(maxMountH, mount.position.y - carBaseY);
            float lift = Mathf.Max(0f, targetDiam * 0.5f - maxMountH);
            if (lift > 0f) wrapper.transform.position += new Vector3(0f, lift, 0f);

            foreach (var mount in new[] { fl, fr, rl, rr })
            {
                if (mount == null || wheelPrefab == null) continue;
                var w = (GameObject)PrefabUtility.InstantiatePrefab(wheelPrefab);
                w.transform.SetParent(mount, false);
                w.transform.localPosition = Vector3.zero;
                w.transform.localRotation = Quaternion.identity;
                var wb = ProceduralMeshLibrary.ComputeBounds(w);
                float srcD = Mathf.Max(wb.size.x, wb.size.y, wb.size.z);
                w.transform.localScale = w.transform.localScale * (targetDiam / Mathf.Max(0.01f, srcD));
            }
            wheelRadius = targetDiam * 0.5f;

            // Colisor criado DEPOIS das rodas: localB inclui as rodas → base do
            // colisor assenta no fundo delas (rodas tocam chão, belly nunca raspa).
            var localB = ColliderFitter.ComputeLocalBounds(wrapper, wrapper.transform);
            ColliderFitter.FitVehicleCompound(wrapper, localB);
            foreach (var c in wrapper.GetComponentsInChildren<Collider>(true))
                c.material = GetOrCreatePhysicsMaterial("Physic_Car");

            // ── FÍSICA FLUIDA PROMETEO (WheelColliders + PrometeoCarController)
            // Fachada: PlayerCarController só injeta input; PROMETEO dirige.
            WheelCollider WheelCol(string name, Transform mount, bool front)
            {
                var go = new GameObject("WheelCol_" + name);
                go.transform.SetParent(wrapper.transform, false);
                // Z do collider DEVE alinhar ao forward do carro — o wrapper nasce
                // rotacionado 90°, sem isso o carro "anda de lado"
                go.transform.localRotation = Quaternion.identity;
                go.transform.position = mount != null ? mount.position
                    : wrapper.transform.position;
                var wc = go.AddComponent<WheelCollider>();
                wc.radius = wheelRadius;
                wc.mass = 45f;
                wc.suspensionDistance = 0.25f;
                var spring = wc.suspensionSpring;
                spring.spring = 32000f; spring.damper = 4200f;
                wc.suspensionSpring = spring;
                // Forward: tração/frenagem firme (stiffness 2.0 sustenta 1350kg)
                var fw = wc.forwardFriction;
                fw.extremumSlip = 0.4f; fw.extremumValue = 1.0f;
                fw.asymptoteSlip = 0.8f; fw.asymptoteValue = 0.75f;
                fw.stiffness = 2.0f;
                wc.forwardFriction = fw;
                // Sideways: mordida frontal (2.6) mata o deslize de sabão na
                // entrada da curva; traseira (2.4) segura sem escorregar
                var sw = wc.sidewaysFriction;
                if (front)
                {
                    sw.extremumSlip = 0.25f; sw.extremumValue = 1.1f;
                    sw.asymptoteSlip = 0.5f; sw.asymptoteValue = 0.85f;
                    sw.stiffness = 2.6f;
                }
                else
                {
                    sw.extremumSlip = 0.28f; sw.extremumValue = 1.05f;
                    sw.asymptoteSlip = 0.55f; sw.asymptoteValue = 0.80f;
                    sw.stiffness = 2.4f;
                }
                wc.sidewaysFriction = sw;
                return wc;
            }
            var prom = wrapper.AddComponent<PrometeoCarController>();
            Studio.Gameplay.CarTunings.SuperCar.ApplyTo(prom); // spec do carro
            prom.bodyMassCenter = new Vector3(0f, -0.35f, 0f);
            prom.useExternalInput = true;   // input via DriveInputAdapter→fachada
            prom.useEffects = false;
            prom.useUI = false;
            prom.useSounds = false;
            prom.frontLeftMesh = fl != null ? fl.gameObject : null;
            prom.frontRightMesh = fr != null ? fr.gameObject : null;
            prom.rearLeftMesh = rl != null ? rl.gameObject : null;
            prom.rearRightMesh = rr != null ? rr.gameObject : null;
            prom.frontLeftCollider = WheelCol("FL", fl, true);
            prom.frontRightCollider = WheelCol("FR", fr, true);
            prom.rearLeftCollider = WheelCol("RL", rl, false);
            prom.rearRightCollider = WheelCol("RR", rr, false);

            var carSo = new SerializedObject(car);
            var wr = carSo.FindProperty("_wheelRadius");
            if (wr != null) wr.floatValue = wheelRadius;
            var steerArr = carSo.FindProperty("_steerWheels");
            steerArr.arraySize = 2;
            steerArr.GetArrayElementAtIndex(0).objectReferenceValue = fl;
            steerArr.GetArrayElementAtIndex(1).objectReferenceValue = fr;
            var driveArr = carSo.FindProperty("_driveWheels");
            driveArr.arraySize = 4;
            driveArr.GetArrayElementAtIndex(0).objectReferenceValue = fl;
            driveArr.GetArrayElementAtIndex(1).objectReferenceValue = fr;
            driveArr.GetArrayElementAtIndex(2).objectReferenceValue = rl;
            driveArr.GetArrayElementAtIndex(3).objectReferenceValue = rr;

            // Trails de drift nas rodas traseiras
            var trailArr = carSo.FindProperty("_skidTrails");
            trailArr.arraySize = 2;
            for (int i = 0; i < 2; i++)
            {
                var wheel = i == 0 ? rl : rr;
                var trailObj = new GameObject($"SkidTrail_{i}");
                trailObj.transform.SetParent(wrapper.transform, false);
                trailObj.transform.position = wheel != null
                    ? new Vector3(wheel.position.x, 0.04f, wheel.position.z)
                    : wrapper.transform.position - wrapper.transform.forward * 1.4f + Vector3.up * 0.04f;
                var tr = trailObj.AddComponent<TrailRenderer>();
                tr.sharedMaterial = matSkid;
                tr.time = 7f;
                tr.minVertexDistance = 0.18f;
                tr.startWidth = 0.26f;
                tr.endWidth = 0.05f;
                tr.emitting = false;
                trailArr.GetArrayElementAtIndex(i).objectReferenceValue = tr;
            }
            carSo.ApplyModifiedPropertiesWithoutUndo();

            // Faróis (2 spots quentes) + lanternas traseiras nos cantos via bounds LOCAIS
            float frontZ = localB.center.z + localB.size.z * 0.5f;
            float rearZ = localB.center.z - localB.size.z * 0.5f;
            float halfW = localB.size.x * 0.5f;
            for (int side = -1; side <= 1; side += 2)
            {
                var hl = new GameObject($"Headlight_{side}");
                hl.transform.SetParent(wrapper.transform, false);
                hl.transform.localPosition = new Vector3(side * halfW * 0.6f, carBounds.size.y * 0.45f, frontZ + 0.05f);
                var l = hl.AddComponent<Light>();
                l.type = LightType.Spot;
                l.color = new Color(1f, 0.85f, 0.6f);
                l.intensity = 6f;
                l.range = 32f;
                l.spotAngle = 55f;
                l.shadows = LightShadows.None;

                var tl = new GameObject($"TailLight_{side}");
                tl.transform.SetParent(wrapper.transform, false);
                tl.transform.localPosition = new Vector3(side * halfW * 0.62f, carBounds.size.y * 0.42f, rearZ - 0.05f);
                var tlLight = tl.AddComponent<Light>();
                tlLight.type = LightType.Point;
                tlLight.color = new Color(1f, 0.08f, 0.12f);
                tlLight.intensity = 1.4f;
                tlLight.range = 3.5f;
            }

            return wrapper;
        }

        private static GameObject BuildCamera(Transform target)
        {
            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.fieldOfView = 62f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 600f;
            var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camObj.AddComponent<AudioListener>();

            var chase = camObj.AddComponent<ChaseCamera>();
            chase.SetTarget(target);
            var so = new SerializedObject(chase);
            so.FindProperty("_target").objectReferenceValue = target;
            so.ApplyModifiedPropertiesWithoutUndo();

            camObj.transform.position = target.position + new Vector3(-8f, 3.6f, 0f);
            camObj.transform.rotation = Quaternion.LookRotation(target.position + Vector3.up * 1.1f - camObj.transform.position);
            return camObj;
        }

        private static void BuildPostVolume()
        {
            var volumeObj = new GameObject("Global_PostProcessing_Volume");
            var volume = volumeObj.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            if (!Directory.Exists("Assets/Game/Settings")) Directory.CreateDirectory("Assets/Game/Settings");
            AssetDatabase.CreateAsset(profile, "Assets/Game/Settings/NeonDistrict_VolumeProfile.asset");
            volume.profile = profile;

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(1.0f);
            bloom.intensity.Override(0.9f);
            bloom.scatter.Override(0.55f);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.28f);
            vignette.smoothness.Override(0.4f);
        }

        private static void BuildUICanvas(PlayerCarController car, DriveInputAdapter adapter)
        {
            var canvasObj = new GameObject("UICanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f); // paisagem
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventObj = new GameObject("EventSystem");
                eventObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            var hud = canvasObj.AddComponent<NeonDistrictHUD>();

            // Painel superior esquerdo: velocímetro + fps
            var speedText = CreateText(canvasObj.transform, "SpeedText", "0 km/h", 64,
                TextAnchor.UpperLeft, new Vector2(-880f, 490f), new Color(0f, 0.95f, 1f), new Vector2(500f, 120f));
            var fpsText = CreateText(canvasObj.transform, "FpsText", "60 fps", 30,
                TextAnchor.UpperLeft, new Vector2(-880f, 430f), new Color(0.7f, 1f, 0.7f, 0.8f), new Vector2(400f, 60f));

            var driftText = CreateText(canvasObj.transform, "DriftIndicator", "DRIFT", 48,
                TextAnchor.MiddleCenter, new Vector2(0f, -380f), new Color(1f, 0.5f, 0.1f), new Vector2(300f, 90f));
            driftText.gameObject.SetActive(false);

            // Sprites do pack "Virtual Buttons" (Asset Store) — fallback procedural
            const string VB_SPRITES = "Assets/VirtualButtonsForUnity/VirtualButtonsForUnity/Sprites";
            var btnSprite = AssetDatabase.LoadAssetAtPath<Sprite>(VB_SPRITES + "/circle-border.png")
                            ?? GetOrCreateButtonSprite();
            var iconRecenter = AssetDatabase.LoadAssetAtPath<Sprite>(VB_SPRITES + "/joystick.png")
                               ?? GetOrCreateRecenterIcon();
            var accent = new Color(0f, 0.85f, 1f);

            // Botões touch (layout paisagem): ◄► à esquerda, gas/freio/drift à direita
            var left = CreateStyledButton(canvasObj.transform, "Btn_SteerLeft", "◄", new Vector2(-830f, -420f),
                new Vector2(170f, 150f), new Color(0.1f, 0.16f, 0.28f, 0.62f), accent, btnSprite);
            var right = CreateStyledButton(canvasObj.transform, "Btn_SteerRight", "►", new Vector2(-600f, -420f),
                new Vector2(170f, 150f), new Color(0.1f, 0.16f, 0.28f, 0.62f), accent, btnSprite);
            var brake = CreateStyledButton(canvasObj.transform, "Btn_Brake", "BRAKE", new Vector2(600f, -420f),
                new Vector2(190f, 150f), new Color(0.4f, 0.08f, 0.1f, 0.62f), new Color(1f, 0.35f, 0.35f), btnSprite);
            var gas = CreateStyledButton(canvasObj.transform, "Btn_Gas", "GAS", new Vector2(830f, -420f),
                new Vector2(190f, 150f), new Color(0.06f, 0.3f, 0.18f, 0.62f), new Color(0.3f, 1f, 0.55f), btnSprite);
            var drift = CreateStyledButton(canvasObj.transform, "Btn_Drift", "DRIFT", new Vector2(715f, -235f),
                new Vector2(190f, 110f), new Color(0.3f, 0.14f, 0.03f, 0.62f), new Color(1f, 0.65f, 0.2f), btnSprite);

            // Recenter: botão pequeno com ícone circular (sem texto)
            var recenter = new GameObject("Btn_Recenter");
            recenter.transform.SetParent(canvasObj.transform, false);
            var rr = recenter.AddComponent<RectTransform>();
            rr.anchorMin = rr.anchorMax = new Vector2(0.5f, 0.5f);
            rr.anchoredPosition = new Vector2(-715f, -235f);
            rr.sizeDelta = new Vector2(110f, 110f);
            var rimg = recenter.AddComponent<Image>();
            rimg.sprite = btnSprite;
            rimg.type = Image.Type.Sliced;
            rimg.color = new Color(0.1f, 0.14f, 0.24f, 0.55f);
            recenter.AddComponent<Button>();
            var riObj = new GameObject("Icon");
            riObj.transform.SetParent(recenter.transform, false);
            var rir = riObj.AddComponent<RectTransform>();
            rir.anchorMin = Vector2.zero; rir.anchorMax = Vector2.one; rir.sizeDelta = Vector2.zero;
            var ri = riObj.AddComponent<Image>();
            ri.sprite = iconRecenter;
            ri.color = accent;

            left.gameObject.AddComponent<DriveTouchButton>();
            SetControl(left.gameObject, DriveInputAdapter.DriveControl.SteerLeft);
            SetControl(right.gameObject, DriveInputAdapter.DriveControl.SteerRight);
            SetControl(brake.gameObject, DriveInputAdapter.DriveControl.Brake);
            SetControl(gas.gameObject, DriveInputAdapter.DriveControl.Gas);
            SetControl(drift.gameObject, DriveInputAdapter.DriveControl.Drift);

            // Rádio GTA: UM botão ♪ no topo — toque=próxima, segurar=liga/desliga
            var radioBtn = CreateStyledButton(canvasObj.transform, "Btn_Radio", "♪", new Vector2(0f, 0f),
                new Vector2(120f, 78f), new Color(0.08f, 0.1f, 0.18f, 0.6f), accent, btnSprite);
            var rRect = radioBtn.GetComponent<RectTransform>();
            rRect.anchorMin = rRect.anchorMax = new Vector2(0.5f, 1f);
            rRect.anchoredPosition = new Vector2(0f, -55f);
            UnityEngine.Object.DestroyImmediate(radioBtn.GetComponent<Button>()); // touch direto
            radioBtn.AddComponent<Studio.Gameplay.RadioTouchButton>();

            var recenterBtn = recenter.GetComponent<Button>();
            if (adapter != null)
            {
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
                    recenterBtn.onClick, adapter.TriggerRecenter);
            }

            var so = new SerializedObject(hud);
            so.FindProperty("_player").objectReferenceValue = car;
            so.FindProperty("_speedText").objectReferenceValue = speedText;
            so.FindProperty("_fpsText").objectReferenceValue = fpsText;
            so.FindProperty("_driftIndicator").objectReferenceValue = driftText.gameObject;
            so.ApplyModifiedPropertiesWithoutUndo();

            // --- v2: HUD a pé (DEPOIS do Apply — SerializedObject stale reverteria) ---
            BuildOnFootHud(canvasObj, adapter, hud, speedText, btnSprite);
        }


        /// <summary>HUD v2: ammo, CANS, crosshair, botões a pé e joystick/look zone.</summary>
        private static void BuildOnFootHud(GameObject canvasObj, DriveInputAdapter adapter,
            NeonDistrictHUD hud, Text speedText, Sprite btnSprite)
        {
            var accent = new Color(0f, 0.9f, 1f);

            // Textos a pé
            var ammoText = CreateText(canvasObj.transform, "AmmoText", "30/90", 44,
                TextAnchor.LowerRight, new Vector2(830f, -160f), accent, new Vector2(300f, 90f));
            var cansText = CreateText(canvasObj.transform, "CansText", "CANS: 0/18", 36,
                TextAnchor.UpperRight, new Vector2(830f, 490f), new Color(1f, 0.4f, 1f), new Vector2(320f, 70f));

            // Crosshair central
            var cross = new GameObject("Crosshair");
            cross.transform.SetParent(canvasObj.transform, false);
            var cr = cross.AddComponent<RectTransform>();
            cr.anchorMin = cr.anchorMax = new Vector2(0.5f, 0.5f);
            cr.sizeDelta = new Vector2(36f, 36f);
            foreach (var (n, sz) in new[] { ("H", new Vector2(36f, 3f)), ("V", new Vector2(3f, 36f)) })
            {
                var bar = new GameObject(n);
                bar.transform.SetParent(cross.transform, false);
                var img = bar.AddComponent<Image>();
                img.color = new Color(0f, 1f, 1f, 0.85f);
                ((RectTransform)bar.transform).sizeDelta = sz;
            }
            cross.SetActive(false);

            // Grupo controles de direção existente
            var driveGroup = new GameObject("DrivingControls");
            driveGroup.transform.SetParent(canvasObj.transform, false);
            driveGroup.AddComponent<RectTransform>();
            foreach (var n in new[] { "Btn_SteerLeft", "Btn_SteerRight", "Btn_Brake", "Btn_Gas", "Btn_Drift", "Btn_Recenter" })
            {
                var t = canvasObj.transform.Find(n);
                if (t != null) t.SetParent(driveGroup.transform, false);
            }

            // Grupo controles a pé
            var footGroup = new GameObject("OnFootControls");
            footGroup.transform.SetParent(canvasObj.transform, false);
            footGroup.AddComponent<RectTransform>();

            // Joystick esquerdo
            var joy = CreateStyledButton(footGroup.transform, "Btn_Joystick", "", new Vector2(-720f, -320f),
                new Vector2(240f, 240f), new Color(0.1f, 0.14f, 0.26f, 0.35f), accent, btnSprite);
            UnityEngine.Object.DestroyImmediate(joy.GetComponent<Button>());
            joy.AddComponent<VirtualJoystick>();
            var knob = new GameObject("Knob");
            knob.transform.SetParent(joy.transform, false);
            var kr = knob.AddComponent<RectTransform>();
            kr.sizeDelta = new Vector2(90f, 90f);
            var ki = knob.AddComponent<Image>();
            ki.sprite = btnSprite; ki.color = new Color(0f, 0.85f, 1f, 0.7f);

            // Zona de look (direita inteira)
            var look = new GameObject("LookZone");
            look.transform.SetParent(footGroup.transform, false);
            var lz = look.AddComponent<RectTransform>();
            lz.anchorMin = new Vector2(0.5f, 0f); lz.anchorMax = Vector2.one;
            lz.offsetMin = lz.offsetMax = Vector2.zero;
            var li = look.AddComponent<Image>();
            li.color = new Color(0f, 0f, 0f, 0f); // invisível mas raycastável
            look.AddComponent<TouchLookZone>();

            // Botões FIRE / ADS / RELOAD / ENTER-EXIT
            var fire = CreateStyledButton(footGroup.transform, "Btn_Fire", "FIRE", new Vector2(830f, -320f),
                new Vector2(200f, 170f), new Color(0.45f, 0.08f, 0.12f, 0.7f), new Color(1f, 0.4f, 0.4f), btnSprite);
            var ads = CreateStyledButton(footGroup.transform, "Btn_Ads", "ADS", new Vector2(590f, -320f),
                new Vector2(150f, 130f), new Color(0.1f, 0.2f, 0.45f, 0.7f), accent, btnSprite);
            var reload = CreateStyledButton(footGroup.transform, "Btn_Reload", "RELOAD", new Vector2(830f, -120f),
                new Vector2(170f, 110f), new Color(0.3f, 0.2f, 0.05f, 0.7f), new Color(1f, 0.8f, 0.3f), btnSprite);
            var enterExit = CreateStyledButton(footGroup.transform, "Btn_EnterExit", "CARRO", new Vector2(0f, -400f),
                new Vector2(200f, 110f), new Color(0.05f, 0.3f, 0.25f, 0.7f), new Color(0.4f, 1f, 0.8f), btnSprite);
            var jump = CreateStyledButton(footGroup.transform, "Btn_Jump", "JUMP", new Vector2(590f, -160f),
                new Vector2(150f, 110f), new Color(0.12f, 0.12f, 0.4f, 0.7f), accent, btnSprite);
            var crouch = CreateStyledButton(footGroup.transform, "Btn_Crouch", "CROUCH", new Vector2(590f, -20f),
                new Vector2(150f, 90f), new Color(0.12f, 0.12f, 0.4f, 0.7f), accent, btnSprite);

            SetControl(fire.gameObject, DriveInputAdapter.DriveControl.Fire);
            SetControl(jump.gameObject, DriveInputAdapter.DriveControl.Jump);
            SetControl(crouch.gameObject, DriveInputAdapter.DriveControl.Crouch);
            SetControl(ads.gameObject, DriveInputAdapter.DriveControl.Ads);
            SetControl(reload.gameObject, DriveInputAdapter.DriveControl.Reload);
            var eeBtn = enterExit.GetComponent<Button>();
            if (adapter != null)
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(eeBtn.onClick, adapter.TriggerEnterExit);
            // Enter/Exit também acessível dirigindo
            var driveEE = CreateStyledButton(canvasObj.transform, "Btn_EnterExitDrive", "SAIR CARRO", new Vector2(0f, -400f),
                new Vector2(200f, 110f), new Color(0.05f, 0.3f, 0.25f, 0.7f), new Color(0.4f, 1f, 0.8f), btnSprite);
            if (adapter != null)
                UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
                    driveEE.GetComponent<Button>().onClick, adapter.TriggerEnterExit);
            driveEE.transform.SetParent(driveGroup.transform, false);

            footGroup.SetActive(false);

            var healthText = CreateText(canvasObj.transform, "HealthText", "HP 100", 40,
                TextAnchor.UpperLeft, new Vector2(-820f, -60f), new Color(0.3f, 1f, 0.6f), new Vector2(240f, 80f));

            var weapon = UnityEngine.Object.FindAnyObjectByType<Ak47Weapon>(FindObjectsInactive.Include);
            hud.BindOnFoot(weapon, ammoText, cansText, cross, driveGroup, footGroup);
            hud.BindHealth(healthText);
        }

        private static GameObject CreateStyledButton(Transform parent, string name, string label,
            Vector2 pos, Vector2 size, Color bgColor, Color textColor, Sprite sprite)
        {
            var bObj = new GameObject(name);
            bObj.transform.SetParent(parent, false);
            var rect = bObj.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var img = bObj.AddComponent<Image>();
            img.sprite = sprite;
            img.type = Image.Type.Sliced;
            img.color = bgColor;
            bObj.AddComponent<Button>();
            var t = CreateText(bObj.transform, "Label", label,
                Mathf.Min(64, (int)(size.y * 0.42f)), TextAnchor.MiddleCenter, Vector2.zero, textColor, size);
            t.fontStyle = FontStyle.Bold;
            return bObj;
        }

        private static Sprite PngToSprite(string path, Vector4 border)
        {
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            imp.textureType = TextureImporterType.Sprite;
            imp.spriteImportMode = SpriteImportMode.Single;
            imp.spriteBorder = border;
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        private static Sprite GetOrCreateButtonSprite()
        {
            const string dir = "Assets/Game/Art/UI";
            const string path = dir + "/T_UI_BtnRound.png";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var ex = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (ex != null) return ex;
            const int S = 96, R = 26;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = Mathf.Max(R - x, x - (S - 1 - R), 0f);
                    float dy = Mathf.Max(R - y, y - (S - 1 - R), 0f);
                    tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(R - Mathf.Sqrt(dx * dx + dy * dy))));
                }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            return PngToSprite(path, new Vector4(28f, 28f, 28f, 28f));
        }

        private static Sprite GetOrCreateRecenterIcon()
        {
            const string dir = "Assets/Game/Art/UI";
            const string path = dir + "/T_UI_IconRecenter.png";
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var ex = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (ex != null) return ex;
            const int S = 64;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var clear = new Color(0, 0, 0, 0);
            for (int i = 0; i < S * S; i++) tex.SetPixel(i % S, i / S, clear);
            Vector2 c = new Vector2(S / 2f, S / 2f);
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    var d = new Vector2(x, y) - c;
                    float r = d.magnitude;
                    if (r > 18f && r < 24f && Mathf.Atan2(d.y, d.x) < 2.4f) // arco 270°
                        tex.SetPixel(x, y, Color.white);
                }
            // ponta da seta no fim do arco
            for (int dy = -8; dy <= 8; dy++)
                for (int dx = 0; dx < 10; dx++)
                    if (dx + Mathf.Abs(dy) < 9) tex.SetPixel(40 + dx, 36 + dy, Color.white);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            return PngToSprite(path, Vector4.zero);
        }

        private static void SetControl(GameObject btnObj, DriveInputAdapter.DriveControl control)
        {
            var dtb = btnObj.GetComponent<DriveTouchButton>();
            if (dtb == null) dtb = btnObj.AddComponent<DriveTouchButton>();
            var so = new SerializedObject(dtb);
            var prop = so.FindProperty("_control");
            prop.enumValueIndex = (int)control;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Text CreateText(Transform parent, string name, string text, int fontSize,
            TextAnchor alignment, Vector2 pos, Color color, Vector2 size)
        {
            var tObj = new GameObject(name);
            tObj.transform.SetParent(parent, false);
            var rect = tObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var t = tObj.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = color;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static GameObject CreateButton(Transform parent, string name, string label,
            Vector2 pos, Vector2 size, Color color, int fontSize)
        {
            var bObj = new GameObject(name);
            bObj.transform.SetParent(parent, false);
            var rect = bObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;

            var img = bObj.AddComponent<Image>();
            img.color = color;
            bObj.AddComponent<Button>();

            var t = CreateText(bObj.transform, "Label", label, fontSize, TextAnchor.MiddleCenter, Vector2.zero, Color.white, size);
            t.rectTransform.sizeDelta = size;
            return bObj;
        }

        #endregion


        /// <summary>
        /// NEON DISTRICT v2: rig a pé (cápsula 1.75m + FPS cam + AK-47), pickups
        /// de munição, latas-alvo e componentes IShootable no mundo reativo.
        /// </summary>
        private static GameObject BuildOnFootSystems(GameObject playerObj, Transform systemObj, Camera mainCam)
        {
            // --- Rig a pé ---
            var foot = new GameObject("PlayerOnFoot");
            var cc = foot.AddComponent<CharacterController>();
            var motor = foot.AddComponent<OnFootMotor>();
            var eye = new GameObject("Eye");
            eye.transform.SetParent(foot.transform, false);
            eye.transform.localPosition = new Vector3(0f, OnFootMotor.EYE_HEIGHT, 0f);
            var fpsCamGo = new GameObject("FpsCamera");
            fpsCamGo.transform.SetParent(eye.transform, false);
            var fpsCam = fpsCamGo.AddComponent<Camera>();
            fpsCam.fieldOfView = 65f;
            fpsCam.nearClipPlane = 0.05f;
            fpsCamGo.AddComponent<UniversalAdditionalCameraData>();
            fpsCam.enabled = false;
            fpsCamGo.tag = "Untagged"; // câmera principal continua sendo a chase
            const int WEAPON_LAYER = 3;
            fpsCam.cullingMask &= ~(1 << WEAPON_LAYER); // mundo sem a arma

            // Câmera overlay da arma (URP camera stack) — FOV próprio 50°
            var wcamGo = new GameObject("WeaponCamera");
            wcamGo.transform.SetParent(eye.transform, false);
            var wcam = wcamGo.AddComponent<Camera>();
            wcam.fieldOfView = 50f;
            wcam.nearClipPlane = 0.01f;
            wcam.farClipPlane = 2.5f;
            wcam.cullingMask = 1 << WEAPON_LAYER;
            wcam.clearFlags = CameraClearFlags.Depth;
            var wcamData = wcamGo.AddComponent<UniversalAdditionalCameraData>();
            wcamData.renderType = CameraRenderType.Overlay;
            fpsCamGo.GetComponent<UniversalAdditionalCameraData>().cameraStack.Add(wcam);

            // --- AK-47 viewmodel ---
            var holder = new GameObject("WeaponHolder");
            holder.transform.SetParent(eye.transform, false);
            holder.transform.localPosition = new Vector3(0.22f, -0.22f, 0.45f);
            var akmPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Weapons_ChamferZone/AKM/WPN_AKM.FBX");
            if (akmPrefab != null)
            {
                var akm = (GameObject)PrefabUtility.InstantiatePrefab(akmPrefab);
                PrefabUtility.UnpackPrefabInstance(akm, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                akm.transform.SetParent(holder.transform, false);
                akm.transform.localPosition = Vector3.zero;
                akm.transform.localRotation = Quaternion.identity;
                var wb = ColliderFitter.ComputeBounds(akm);
                float s = 0.62f / Mathf.Max(0.05f, wb.size.x + wb.size.z);
                akm.transform.localScale = Vector3.one * s;
                foreach (var t in akm.GetComponentsInChildren<Transform>(true))
                    t.gameObject.layer = WEAPON_LAYER;
                // Munição solta do modelo (calibers) flutua acima da arma — desliga
                foreach (var r in akm.GetComponentsInChildren<Renderer>(true))
                    if (r.sharedMaterial != null && r.sharedMaterial.name.Contains("calibers"))
                        r.enabled = false;
            }
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(holder.transform, false);
            muzzle.transform.localPosition = new Vector3(0f, 0.05f, 0.6f);
            var texBase = LoadTex("asphalt_square");

            // Quad de muzzle flash (emissivo, layer arma, desligado por padrão)
            var matFlash = GetOrCreateUnlitMaterial("Mat_FX_MuzzleFlash", new Color(1f, 0.72f, 0.25f));
            var quad = ProceduralMeshLibrary.CreatePart("MuzzleFlashQuad", ProceduralMeshLibrary.Plane,
                muzzle.transform, new Vector3(0f, 0f, 0.06f), Quaternion.identity, Vector3.one * 0.055f, matFlash);
            quad.layer = WEAPON_LAYER;
            var quadR = quad.GetComponent<MeshRenderer>();
            quadR.enabled = false;

            var weapon = foot.AddComponent<Ak47Weapon>();
            weapon.Bind(holder.transform, muzzle.transform, fpsCam);
            weapon.SetFlashQuad(quadR);
            foot.AddComponent<Studio.Gameplay.PlayerHealth>();

            // Pools de estojo/decal em root de cena (NUNCA filhos do player —
            // senão "grudam na câmera" ao mover)
            var poolRoot = new GameObject("FPPools");
            poolRoot.transform.SetParent(systemObj.transform, false);
            var matBrass = GetOrCreateUnlitMaterial("Mat_FX_Brass", new Color(0.85f, 0.62f, 0.18f));
            var casings = new Transform[10];
            for (int i = 0; i < casings.Length; i++)
            {
                var c = ProceduralMeshLibrary.CreatePart($"Casing_{i}", ProceduralMeshLibrary.Capsule,
                    poolRoot.transform, Vector3.zero, Quaternion.identity, new Vector3(0.012f, 0.035f, 0.012f), matBrass);
                casings[i] = c.transform;
                c.SetActive(false);
            }
            weapon.SetCasingPool(casings);
            var matDecal = GetOrCreateUnlitMaterial("Mat_FX_Decal", new Color(0.10f, 0.09f, 0.08f), GetOrCreateBulletHoleTex(), true);
            var decals = new Transform[24];
            for (int i = 0; i < decals.Length; i++)
            {
                var d = ProceduralMeshLibrary.CreatePart($"Decal_{i}", ProceduralMeshLibrary.Plane,
                    poolRoot.transform, Vector3.zero, Quaternion.identity, Vector3.one * 0.07f, matDecal);
                decals[i] = d.transform;
                d.SetActive(false);
            }
            weapon.SetDecalPool(decals);

            // Pool de tracers (8 LineRenderers finos emissivos)
            var tracerMat = GetOrCreateUnlitMaterial("Mat_FX_Tracer", new Color(1f, 0.65f, 0.15f));
            var tracers = new LineRenderer[8];
            for (int i = 0; i < tracers.Length; i++)
            {
                var t = new GameObject($"Tracer_{i}");
                t.transform.SetParent(foot.transform, false);
                var lr = t.AddComponent<LineRenderer>();
                lr.positionCount = 2;
                lr.startWidth = lr.endWidth = 0.025f;
                lr.sharedMaterial = tracerMat;
                lr.enabled = false;
                tracers[i] = lr;
            }
            weapon.SetTracerPool(tracers);
            foot.SetActive(false);

            // --- ModeManager ---
            var mgr = systemObj.gameObject.AddComponent<NeonDistrictModeManager>();
            mgr.Bind(playerObj.GetComponent<PlayerCarController>(), motor, mainCam, fpsCam);
            playerObj.AddComponent<ShootableSurface>().surface = ShootableSurface.Surface.Metal;

            // --- Pickups de munição (neon, respawn) ---
            var matPickup = GetOrCreateMaterial("Mat_ND_AmmoPickup", new Color(0.1f, 0.9f, 0.4f), new Color(0.2f, 1f, 0.5f) * 3f, texBase);
            var rng = new System.Random(4242);
            int pickups = 0;
            for (int i = 0; i < 8; i++)
            {
                float x = StreetLine(rng.Next(0, LINES)) + (rng.Next(2) == 0 ? -9f : 9f);
                float z = StreetLine(rng.Next(0, LINES)) + (rng.Next(2) == 0 ? -9f : 9f);
                var p = ProceduralMeshLibrary.CreatePart($"AmmoPickup_{i}",
                    ProceduralMeshLibrary.Octahedron, null, new Vector3(x, 0.8f, z),
                    Quaternion.identity, Vector3.one * 0.35f, matPickup);
                p.AddComponent<AmmoPickup>();
                pickups++;
            }

            // --- Latas-alvo (praça + calçadas) ---
            var matCan = GetOrCreateMaterial("Mat_ND_Can", new Color(0.9f, 0.2f, 0.9f), new Color(1f, 0.3f, 1f) * 3f, texBase);
            var canPositions = new List<Vector3>();
            for (int i = 0; i < 10; i++) // praça central
                canPositions.Add(new Vector3(-20f + rng.Next(0, 40), 0.12f, -20f + rng.Next(0, 40)));
            for (int i = 0; i < 8; i++) // calçadas aleatórias
                canPositions.Add(new Vector3(StreetLine(rng.Next(0, LINES)) + 8f, 0.12f, StreetLine(rng.Next(0, LINES)) + 8f));
            int cans = 0;
            foreach (var cp in canPositions)
            {
                var c = ProceduralMeshLibrary.CreatePart($"Can_{cans}", ProceduralMeshLibrary.Cylinder,
                    null, cp, Quaternion.identity, new Vector3(0.16f, 0.12f, 0.16f), matCan);
                var col = c.AddComponent<CapsuleCollider>();
                col.radius = 0.09f; col.height = 0.24f;
                c.AddComponent<ShootableCan>();
                cans++;
            }
            mgr.CansTotal = cans;

            // --- Mundo reativo: postes apagam, bancos/blocos saltam ---
            foreach (var go in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var n = go.name;
                if (n.StartsWith("Lamp_") && go.GetComponent<ShootableLamp>() == null)
                    go.gameObject.AddComponent<ShootableLamp>();
                else if ((n.StartsWith("Bench_") || n.StartsWith("concrete_block"))
                    && go.GetComponent<IShootable>() == null && go.GetComponent<Collider>() != null)
                    go.gameObject.AddComponent<ShootableProp>();
            }

            return foot;
        }

        #region Helpers gerais

        private static float StreetLine(int i) => (i - (LINES - 1) * 0.5f) * PITCH;


        /// <summary>Textura branca 4x4 para materiais sem albedo (validador exige _BaseMap).</summary>
        private static Texture2D GetOrCreateWhiteTex()
        {
            const string dir = "Assets/Game/Art/UI";
            const string path = dir + "/T_White.png";
            var ex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (ex != null) return ex;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var t = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            for (int i = 0; i < 16; i++) t.SetPixel(i % 4, i / 4, Color.white);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Material URP Unlit de cor sólida (tracer, flash, decal).</summary>
        private static Material GetOrCreateUnlitMaterial(string name, Color color,
            Texture2D baseMap = null, bool transparent = false)
        {
            string path = $"{MAT_DIR}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = name;
                AssetDatabase.CreateAsset(mat, path);
            }
            else mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            mat.SetColor("_Color", color);
            if (baseMap != null) mat.SetTexture("_BaseMap", baseMap);
            if (transparent)
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetOverrideTag("RenderType", "Transparent");
                mat.renderQueue = 3000;
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>
        /// Textura procedural de marca de bala: splat radial escuro com
        /// rachaduras irregulares, alpha decaindo nas bordas (parece buraco).
        /// </summary>
        private static Texture2D GetOrCreateBulletHoleTex()
        {
            const string dir = "Assets/Game/Art/UI";
            const string path = dir + "/T_FX_BulletHole.png";
            var ex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (ex != null) return ex;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            const int S = 128;
            var rng = new System.Random(7);
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            // rachaduras: 6 linhas radiais irregulares
            var cracks = new System.Collections.Generic.List<(float ang, float len, float wob)>();
            for (int i = 0; i < 7; i++)
                cracks.Add(((float)rng.NextDouble() * Mathf.PI * 2f, 0.35f + (float)rng.NextDouble() * 0.5f, 0.2f));
            for (int y = 0; y < S; y++)
                for (int x = 0; x < S; x++)
                {
                    float dx = (x - S / 2f) / (S / 2f), dy = (y - S / 2f) / (S / 2f);
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float ang = Mathf.Atan2(dy, dx);
                    // splat central escuro + bordas irregulares
                    float alpha = Mathf.Clamp01(1f - r * 1.8f);
                    float crackA = 0f;
                    foreach (var (ca, cl, wob) in cracks)
                    {
                        float diff = Mathf.Abs(Mathf.DeltaAngle(ang * Mathf.Rad2Deg, ca * Mathf.Rad2Deg)) / 180f;
                        if (diff < 0.04f && r < cl)
                            crackA = Mathf.Max(crackA, (1f - diff / 0.04f) * Mathf.Clamp01(cl - r + wob * r));
                    }
                    float a = Mathf.Clamp01(alpha * alpha + crackA * 0.9f);
                    // borda da marca levemente mais clara (poeira ao redor)
                    float shade = Mathf.Lerp(1f, 0.4f, Mathf.Clamp01(a * 1.5f));
                    tex.SetPixel(x, y, new Color(shade * 0.12f, shade * 0.11f, shade * 0.10f, a));
                }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (imp != null) { imp.alphaIsTransparency = true; imp.SaveAndReimport(); }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }


        /// <summary>
        /// Complete City Pack: estátua na praça (centerpiece), postes reais na
        /// avenida, flyover como viaduto cruzando a avenida e canopies nas
        /// fachadas — todos com colisão exata (MeshCollider) ou perfil de haste.
        /// </summary>
        private static void BuildCityPackExtras(Transform parent)
        {
            var rng = new System.Random(777);
            const string M = "Assets/Package/Model/";

            // --- Estátua na praça central ---
            var statue = SpawnPackModel(parent, M + "Statue satnd_a.FBX", new Vector3(0f, 0f, 0f), 4.5f, "Statue_Plaza");
            if (statue != null) ColliderFitter.FitCompoundBuilding(statue, parent, 0.3f);

            // --- Street lights do pack nas calçadas da avenida (a cada ~40m) ---
            for (int i = 0; i < 8; i++)
            {
                float x = -120f + i * 34f;
                foreach (int side in new[] { -1, 1 })
                {
                    var l = SpawnPackModel(parent, M + "Street_light.FBX",
                        new Vector3(x, 0f, 26f + side * 9f), 7f, $"StreetLight_{i}_{side}");
                    if (l != null) ColliderFitter.FitProp(l, "street_light", parent);
                }
            }

            // --- Flyover: viaduto cruzando a avenida no extremo oeste ---
            var fly = SpawnPackModel(parent, M + "Flyover.FBX", new Vector3(-118f, 0f, 26f), 22f, "Flyover_West");
            if (fly != null)
            {
                fly.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
                ColliderFitter.FitCompoundBuilding(fly, parent, 1.5f);
            }

            // --- Canopies/bannars nas fachadas da avenida (vida de rua) ---
            for (int i = 0; i < 6; i++)
            {
                float x = -90f + i * 34f;
                SpawnPackModel(parent, M + "canopi.FBX", new Vector3(x, 0f, 26f + 9.5f), 3.2f, $"Canopy_{i}");
                var b = SpawnPackModel(parent, M + "bannar.FBX", new Vector3(x + 8f, 0f, 26f - 9.5f), 4f, $"Banner_{i}");
                if (b != null) ColliderFitter.FitProp(b, "sign", parent);
            }
        }

        /// <summary>Instancia modelo do pack, normaliza altura e retorna (ou null).</summary>
        private static GameObject SpawnPackModel(Transform parent, string path, Vector3 pos, float targetHeight, string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var b = ColliderFitter.ComputeBounds(go);
            float s = targetHeight / Mathf.Max(0.01f, b.size.y);
            go.transform.localScale *= s;
            b = ColliderFitter.ComputeBounds(go);
            go.transform.position += new Vector3(0f, pos.y - b.min.y, 0f);
            return go;
        }

        /// <summary>PhysicMaterial sem fricção pro carro deslizar (drift funciona).</summary>
        private static PhysicsMaterial GetOrCreatePhysicsMaterial(string name)
        {
            var path = $"{MAT_DIR}/{name}.physicMaterial";
            var mat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);
            if (mat == null)
            {
                mat = new PhysicsMaterial(name);
                AssetDatabase.CreateAsset(mat, path);
            }
            mat.dynamicFriction = 0f;
            mat.staticFriction = 0f;
            mat.bounciness = 0.15f;
            mat.frictionCombine = PhysicsMaterialCombine.Minimum;
            mat.bounceCombine = PhysicsMaterialCombine.Average;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>Força wrapMode=Repeat na textura (UVs das vias ultrapassam 1).</summary>
        private static void EnsureRepeatWrap(Texture2D tex)
        {
            if (tex == null) return;
            var imp = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(tex)) as TextureImporter;
            if (imp != null && imp.wrapMode != TextureWrapMode.Repeat)
            {
                imp.wrapMode = TextureWrapMode.Repeat;
                imp.SaveAndReimport();
            }
        }

        private static Texture2D LoadTex(string name)
        {
            return AssetDatabase.LoadAssetAtPath<Texture2D>($"{CITY_TEX}/{name}.png");
        }

        private static void ApplyPackSkybox()
        {
            var sky = AssetDatabase.LoadAssetAtPath<Material>("Assets/Package/Skybox/Skybox Night.mat");
            if (sky == null) return;
            var shader = Shader.Find("Skybox/Panoramic");
            if (shader != null) sky.shader = shader;
            RenderSettings.skybox = sky;
            Debug.Log("[NeonDistrictSceneBuilder] Skybox noturno do Complete City Pack aplicado");
        }

        private static Material GetOrCreateSkybox()
        {
            string path = $"{MAT_DIR}/Mat_Skybox_NeonDusk.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Skybox/Procedural");
            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = "Mat_Skybox_NeonDusk";
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (shader != null) mat.shader = shader;

            mat.SetFloat("_SunSize", 0.06f);
            mat.SetFloat("_SunSizeConvergence", 3.5f);
            mat.SetFloat("_AtmosphereThickness", 1.35f);
            mat.SetColor("_SkyTint", new Color(0.45f, 0.2f, 0.5f));
            mat.SetColor("_GroundColor", new Color(0.08f, 0.05f, 0.1f));
            mat.SetFloat("_Exposure", 1.15f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material GetOrCreateMaterial(string name, Color baseColor, Color emissionColor,
            Texture2D baseMap = null, Texture2D normalMap = null, Vector2 tiling = default)
        {
            string path = $"{MAT_DIR}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null)
            {
                mat = new Material(shader);
                mat.name = name;
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_BaseColor", baseColor);
            if (baseMap != null)
            {
                mat.SetTexture("_BaseMap", baseMap);
                mat.SetTextureScale("_BaseMap", tiling != Vector2.zero ? tiling : Vector2.one);
            }
            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.EnableKeyword("_NORMALMAP");
                if (tiling != Vector2.zero) mat.SetTextureScale("_BumpMap", tiling);
            }

            if (emissionColor != Color.black)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void RegisterSceneInBuildSettings(string scenePath)
        {
            var currentScenes = EditorBuildSettings.scenes;
            bool exists = false;
            foreach (var s in currentScenes)
            {
                if (string.Equals(s.path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    exists = true;
                    break;
                }
            }

            if (!exists)
            {
                var newScenes = new EditorBuildSettingsScene[currentScenes.Length + 1];
                newScenes[0] = new EditorBuildSettingsScene(scenePath, true);
                for (int i = 0; i < currentScenes.Length; i++)
                {
                    newScenes[i + 1] = currentScenes[i];
                }
                EditorBuildSettings.scenes = newScenes;
                Debug.Log($"[NeonDistrictSceneBuilder] Cena registrada como Scene[0]: {scenePath}");
            }
        }

        #endregion
    }
}
