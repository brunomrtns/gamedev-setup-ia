using System;
using System.IO;
using Studio.Foundation;
using Studio.Gameplay;
using Studio.Input;
using Studio.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Studio.Editor
{
    /// <summary>
    /// Construtor determinístico de cena, materiais URP e capturas de evidência para o Jogo 2:
    /// "Abyssal Breach" (Sub-Aquatic First-Person Survival / Extraction Shooter).
    /// </summary>
    public static class AbyssalBreachSceneBuilder
    {
        public const string SCENE_PATH = "Assets/Game/Scenes/AbyssalBreach.unity";
        public const string MAT_DIR = "Assets/Game/Materials/AbyssalBreach";
        public const string ART_MODELS_DIR = "Assets/Game/Art/AbyssalBreach/Models";
        public const string ART_TEX_DIR = "Assets/Game/Art/AbyssalBreach/Textures";

        [MenuItem("Studio/Abyssal Breach/Build Abyssal Breach Scene")]
        public static void BuildSceneMenu()
        {
            BuildAbyssalBreachScene(SCENE_PATH);
        }

        [MenuItem("Studio/Abyssal Breach/Capture Evidence Screenshots")]
        public static void CaptureMenu()
        {
            CaptureAbyssalBreachAnglesBatch(exitEditor: false);
        }

        public static void RunBuildSceneBatch()
        {
            Debug.Log("[AbyssalBreachSceneBuilder] Iniciando construção da cena AbyssalBreach em batchmode...");
            bool ok = BuildAbyssalBreachScene(SCENE_PATH);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static void RunCaptureAnglesBatch()
        {
            CaptureAbyssalBreachAnglesBatch(exitEditor: true);
        }

        public static void CaptureAbyssalBreachAnglesBatch(bool exitEditor = true)
        {
            Debug.Log("[AbyssalBreachSceneBuilder] Renderizando 6 capturas determinísticas de Abyssal Breach...");
            BuildAbyssalBreachScene(SCENE_PATH);

            var player = GameObject.Find("Abyssal_Diver_Player");
            var motor = player != null ? player.GetComponent<AbyssalFirstPersonMotor>() : null;
            var weapons = player != null ? player.GetComponent<AbyssalWeaponController>() : null;
            var survival = player != null ? player.GetComponent<OxygenSurvivalSystem>() : null;
            var gm = AbyssalGameManager.Instance;
            var hud = UnityEngine.Object.FindAnyObjectByType<AbyssalHUDManager>();
            var cam = Camera.main ?? UnityEngine.Object.FindAnyObjectByType<Camera>();

            if (cam == null || player == null || gm == null)
            {
                Debug.LogError("[AbyssalBreachSceneBuilder] Elementos essenciais ausentes para captura!");
                if (exitEditor) EditorApplication.Exit(1);
                return;
            }

            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            var canvasTf = GameObject.Find("Abyssal_Helmet_Canvas")?.transform;
            var startPanel = canvasTf != null ? canvasTf.Find("Start_Panel")?.gameObject : null;
            var hudPanel = canvasTf != null ? canvasTf.Find("HUD_Panel")?.gameObject : null;
            var victoryPanel = canvasTf != null ? canvasTf.Find("Victory_Panel")?.gameObject : null;
            var gameOverPanel = canvasTf != null ? canvasTf.Find("GameOver_Panel")?.gameObject : null;

            var rifleObj = weapons != null ? weapons.transform.Find("CameraPivot/WeaponHolder/FPS_PlasmaRifle") : null;
            var knifeObj = weapons != null ? weapons.transform.Find("CameraPivot/WeaponHolder/FPS_TacticalKnife") : null;
            var tracerLine = weapons != null ? weapons.GetComponentInChildren<LineRenderer>(true) : null;

            // Warm-up: força compilação de shaders/variantes URP antes da primeira captura (evita frame magenta)
            for (int w = 0; w < 3; w++)
            {
                var wrt = RenderTexture.GetTemporary(256, 144, 24);
                cam.targetTexture = wrt;
                cam.Render();
                cam.targetTexture = null;
                RenderTexture.ReleaseTemporary(wrt);
            }

            // 1. Tela Inicial de Briefing do Capacete (Start Screen + Reef Overview)
            gm.SetState(GameState.StartMenu);
            if (startPanel) startPanel.SetActive(true);
            if (hudPanel) hudPanel.SetActive(false);
            if (victoryPanel) victoryPanel.SetActive(false);
            if (gameOverPanel) gameOverPanel.SetActive(false);
            player.transform.position = new Vector3(0f, 3.4f, 2f);
            player.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
            if (hud != null) hud.RefreshAllUI();
            RenderAndSave(cam, Path.Combine(outDir, "abyssal_breach_01_start_helmet.png"));

            // 2. Gameplay FPS com Rifle Submarino em Primeira Pessoa + Recifes de Corais + HUD Tático
            gm.StartGame();
            if (startPanel) startPanel.SetActive(false);
            if (hudPanel) hudPanel.SetActive(true);
            if (victoryPanel) victoryPanel.SetActive(false);
            if (gameOverPanel) gameOverPanel.SetActive(false);
            player.transform.position = new Vector3(-0.35f, 2.8f, 15f);
            player.transform.rotation = Quaternion.Euler(0f, 2f, 0f);
            weapons.SwitchWeaponSlot(AbyssalWeaponSlot.PlasmaRifle);
            AbyssalInputAdapter.Instance?.SetAiming(false);
            motor.StepSimulation(0.1f);
            weapons.StepWeapons(0.1f);
            if (hud != null) hud.RefreshAllUI();
            RenderAndSave(cam, Path.Combine(outDir, "abyssal_breach_02_fps_rifle_corals.png"));

            // 3. Mira Óptica Ativa (ADS Zoom) + Disparo de Plasma contra Drone Sentinela e Pod Naufragado
            player.transform.position = new Vector3(-0.05f, 2.9f, 19.5f);
            player.transform.rotation = Quaternion.Euler(1.5f, -1.0f, 0f);
            AbyssalInputAdapter.Instance?.SetAiming(true);
            for (int i = 0; i < 10; i++)
            {
                motor.StepSimulation(0.05f);
                weapons.StepWeapons(0.05f);
            }
            weapons.TryFirePlasmaRifle();
            if (tracerLine != null)
            {
                tracerLine.enabled = true;
                tracerLine.SetPosition(0, cam.transform.position + cam.transform.right * 0.02f - cam.transform.up * 0.06f + cam.transform.forward * 0.55f);
                tracerLine.SetPosition(1, new Vector3(0.55f, 3.2f, 24.0f));
            }
            if (hud != null) hud.RefreshAllUI();
            RenderAndSave(cam, Path.Combine(outDir, "abyssal_breach_03_ads_aiming_drone.png"));
            if (tracerLine != null) tracerLine.enabled = false;

            // 4. Arma Secundária: Faca Tática de Mergulho (Melee / Coleta de Núcleo e Tanque de O2)
            AbyssalInputAdapter.Instance?.SetAiming(false);
            weapons.SwitchWeaponSlot(AbyssalWeaponSlot.TacticalKnife);
            player.transform.position = new Vector3(0.2f, 2.8f, 52.5f);
            player.transform.rotation = Quaternion.Euler(2f, -4f, 0f);
            for (int i = 0; i < 8; i++)
            {
                motor.StepSimulation(0.05f);
                weapons.StepWeapons(0.05f);
            }
            survival.StepSurvival(15f, false); // Mostrar consumo parcial de O2 (78%)
            gm.RegisterEnergyCoreCollected();
            if (hud != null) hud.RefreshAllUI();
            RenderAndSave(cam, Path.Combine(outDir, "abyssal_breach_04_tactical_knife_melee.png"));

            // 5. Cápsula de Extração Submarina (Sub-Pod) + Tela de Vitória (Extraction Complete)
            weapons.SwitchWeaponSlot(AbyssalWeaponSlot.PlasmaRifle);
            player.transform.position = new Vector3(0f, 3.6f, 102f);
            player.transform.rotation = Quaternion.Euler(4f, 0f, 0f);
            gm.RegisterEnergyCoreCollected();
            gm.RegisterEnergyCoreCollected();
            gm.RegisterDroneDestroyed();
            gm.CompleteExtraction();
            if (hudPanel) hudPanel.SetActive(false);
            if (victoryPanel) victoryPanel.SetActive(true);
            if (hud != null) hud.RefreshAllUI();
            RenderAndSave(cam, Path.Combine(outDir, "abyssal_breach_05_extraction_victory.png"));

            // 6. Tela de Derrota por Hipóxia / Violação do Traje (Game Over)
            gm.SetState(GameState.Playing);
            survival.StepSurvival(80f, true); // Zera O2 e aplica hipóxia
            gm.GameOver();
            if (victoryPanel) victoryPanel.SetActive(false);
            if (gameOverPanel) gameOverPanel.SetActive(true);
            player.transform.position = new Vector3(0f, 2.5f, 70f);
            player.transform.rotation = Quaternion.Euler(12f, 15f, -8f);
            if (hud != null) hud.RefreshAllUI();
            RenderAndSave(cam, Path.Combine(outDir, "abyssal_breach_06_hypoxia_gameover.png"));

            // Restaurar cena limpa para estado inicial antes de salvar
            BuildAbyssalBreachScene(SCENE_PATH);

            Debug.Log("[AbyssalBreachSceneBuilder] Todas as 6 capturas salvas em evidence/captures/!");
            if (exitEditor) EditorApplication.Exit(0);
        }

        private static void RenderAndSave(Camera cam, string filePath)
        {
            Canvas.ForceUpdateCanvases();

            int width = 1280;
            int height = 720;
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

            byte[] bytes = tex.EncodeToPNG();
            UnityEngine.Object.DestroyImmediate(tex);

            File.WriteAllBytes(filePath, bytes);
            Debug.Log($"[AbyssalBreachSceneBuilder] Captura salva: {Path.GetFileName(filePath)} ({bytes.Length / 1024} KB)");
        }

        public static bool BuildAbyssalBreachScene(string scenePath)
        {
            string sceneDir = Path.GetDirectoryName(scenePath);
            if (!string.IsNullOrEmpty(sceneDir) && !Directory.Exists(sceneDir)) Directory.CreateDirectory(sceneDir);
            if (!Directory.Exists(MAT_DIR)) Directory.CreateDirectory(MAT_DIR);

            AssetDatabase.Refresh();

            // 1. Carregar Texturas PBR Reais Extraídas do Vault e Pacotes Nativos
            var texCoral1D = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Coral_01_D.png");
            var texCoral1N = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Coral_01_N.png");
            var texCoral2D = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Coral_02_D.png");
            var texCoral3D = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Coral_03_D.png");
            var texRockD = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Abyssal_Rock_D.png");
            var texRockN = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Abyssal_Rock_N.png");
            var texSeabedD = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Abyssal_Seabed_D.png");
            var texSeabedN = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Abyssal_Seabed_N.png");
            var texRifleDiff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Assault_Rifle_B_Diff.png");
            var texRifleNorm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Assault_Rifle_B_Norm.png");
            var texDroneColormap = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_SentinelDrone_Colormap.png");
            var texKnifeDiff = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Knife_01_D.png");
            var texKnifeNorm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Knife_01_N.png");
            var texFishAlbedo = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Fish_Albedo.png");
            var texFishNorm = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_Fish_Normal.png");
            var texCaustics = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ART_TEX_DIR}/T_OceanSurface_Caustics.png");

            // 2. Criar Materiais URP Lit com Texturas PBR Autênticas (Sem emissões ofuscantes)
            var matSeabed = CreateUrpMaterial("Mat_Abyssal_Seabed", Color.white, Color.black, texSeabedD, texSeabedN, 0.15f, 0.40f, new Vector2(16f, 60f));
            var matRockWall = CreateUrpMaterial("Mat_Abyssal_Rock", Color.white, Color.black, texRockD, texRockN, 0.20f, 0.45f, new Vector2(4f, 12f));
            var matCoralCyan = CreateUrpMaterial("Mat_Coral_BiolumCyan", Color.white, Color.black, texCoral1D, texCoral1N, 0.15f, 0.65f);
            var matCoralMagenta = CreateUrpMaterial("Mat_Coral_BiolumMagenta", Color.white, Color.black, texCoral2D, texCoral1N, 0.15f, 0.65f);
            var matCoralAmber = CreateUrpMaterial("Mat_Coral_BiolumAmber", Color.white, Color.black, texCoral3D, texCoral1N, 0.15f, 0.65f);
            var matFish = CreateUrpMaterial("Mat_Barramundi_Fish", Color.white, Color.black, texFishAlbedo, texFishNorm, 0.1f, 0.85f);
            var matRifle = CreateUrpMaterial("Mat_FPS_PlasmaRifle", Color.white, Color.black, texRifleDiff, texRifleNorm, 0.75f, 0.65f);
            var matRifleGlow = CreateUrpMaterial("Mat_FPS_PlasmaGlow", new Color(0.0f, 0.95f, 1.0f), new Color(0.0f, 2.8f, 3.2f), null, null, 0.2f, 0.9f);
            var matKnife = CreateUrpMaterial("Mat_FPS_TacticalKnife", Color.white, Color.black, texKnifeDiff, texKnifeNorm, 0.85f, 0.85f);
            var matPodHull = CreateUrpMaterial("Mat_SunkenPod_Hull", Color.white, Color.black, texRockD, texRockN, 0.65f, 0.65f, new Vector2(2f, 2f));
            var matDroneHull = CreateUrpMaterial("Mat_SubDrone_Hull", Color.white, Color.black, texDroneColormap, null, 0.75f, 0.78f);
            var matDroneEye = CreateUrpMaterial("Mat_SubDrone_Eye", new Color(1.0f, 0.15f, 0.2f), new Color(3.2f, 0.2f, 0.3f), null, null, 0.1f, 0.95f);
            var matOxygenTank = CreateUrpMaterial("Mat_OxygenTank", new Color(0.1f, 0.95f, 0.75f), new Color(0.1f, 2.0f, 1.4f), null, null, 0.4f, 0.85f);
            var matEnergyCore = CreateUrpMaterial("Mat_DataCore", new Color(1.0f, 0.82f, 0.15f), new Color(2.6f, 1.8f, 0.2f), null, null, 0.5f, 0.92f);
            var matExtractionPod = CreateUrpMaterial("Mat_ExtractionSubPod", Color.white, new Color(0.1f, 1.2f, 1.6f), texCaustics, null, 0.6f, 0.85f, new Vector2(4f, 4f));
            var matOceanCanopy = CreateUrpMaterial("Mat_OceanSurfaceCanopy", new Color(0.05f, 0.48f, 0.68f), new Color(0.08f, 0.65f, 0.95f), texCaustics, null, 0.0f, 0.9f, new Vector2(12f, 35f));

            // 3. Carregar Modelos 3D Reais (Kenney FPS Kit, Khronos Fish PBR, Corais e Pods)
            var fbxRifle = AssetDatabase.LoadAssetAtPath<GameObject>($"{ART_MODELS_DIR}/SM_Assault_Rifle_B.fbx");
            var fbxDrone = AssetDatabase.LoadAssetAtPath<GameObject>($"{ART_MODELS_DIR}/SM_Sentinel_Drone.fbx");
            var fbxFish = AssetDatabase.LoadAssetAtPath<GameObject>($"{ART_MODELS_DIR}/BarramundiFish.fbx");
            var fbxKnife = AssetDatabase.LoadAssetAtPath<GameObject>($"{ART_MODELS_DIR}/SM_Tactical_Knife.fbx");
            var fbxCoralBrain = AssetDatabase.LoadAssetAtPath<GameObject>($"{ART_MODELS_DIR}/SM_Coral_Brain.fbx");
            var fbxCoralTube = AssetDatabase.LoadAssetAtPath<GameObject>($"{ART_MODELS_DIR}/SM_Coral_TubeCluster.fbx");
            var fbxCoralStaghorn = AssetDatabase.LoadAssetAtPath<GameObject>($"{ART_MODELS_DIR}/SM_Coral_Staghorn.fbx");
            var fbxRockArch = AssetDatabase.LoadAssetAtPath<GameObject>($"{ART_MODELS_DIR}/SM_Abyssal_RockArch.fbx");
            var fbxSunkenPod = AssetDatabase.LoadAssetAtPath<GameObject>($"{ART_MODELS_DIR}/SM_Sunken_Pod.fbx");
            var fbxExtractionPod = AssetDatabase.LoadAssetAtPath<GameObject>($"{ART_MODELS_DIR}/SM_Extraction_SubPod.fbx");

            // 4. Criar Cena Limpa e Configurar Atmosfera Subaquática URP
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.12f, 0.32f, 0.46f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = new Color(0.03f, 0.16f, 0.26f);
            RenderSettings.fogDensity = 0.0055f;

            var sunObj = new GameObject("Directional Light (Ocean Sunbeams)");
            var sunLight = sunObj.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = new Color(0.40f, 0.88f, 1.0f);
            sunLight.intensity = 1.70f;
            sunLight.shadows = LightShadows.Soft;
            sunObj.AddComponent<UniversalAdditionalLightData>();
            sunObj.transform.rotation = Quaternion.Euler(58f, -22f, 0f);

            // 5. Construir Relevo Submarino, Paredões da Fenda, Recifes de Corais e Fauna Marinha
            var envRoot = new GameObject("Environment_AbyssalTrench");

            var seabed = ProceduralMeshLibrary.CreatePart(
                "Abyssal_Seabed_Floor",
                ProceduralMeshLibrary.Box,
                envRoot.transform,
                new Vector3(0f, -0.5f, 65f),
                Quaternion.identity,
                new Vector3(42f, 1f, 160f),
                matSeabed);
            seabed.AddComponent<BoxCollider>();

            ProceduralMeshLibrary.CreatePart(
                "Ocean_Surface_CausticCanopy",
                ProceduralMeshLibrary.Box,
                envRoot.transform,
                new Vector3(0f, 22f, 65f),
                Quaternion.identity,
                new Vector3(48f, 0.4f, 160f),
                matOceanCanopy);

            // Paredões rochosos laterais e formações de corais ao longo da fenda
            for (int z = 6; z <= 120; z += 8)
            {
                // Paredões da trincheira — rochas modulares do kit CC0 (V2) com
                // material geológico PBR do Abyssal (textura + normal map)
                CreateTrenchWallRock(envRoot.transform, $"Trench_Wall_L_{z}",
                    new Vector3(-14.5f - (z % 3), 0f, z),
                    new Vector3(6f, 14f + (z % 5), 9f), z, matRockWall);

                CreateTrenchWallRock(envRoot.transform, $"Trench_Wall_R_{z}",
                    new Vector3(14.5f + (z % 3), 0f, z),
                    new Vector3(6f, 14f + ((z + 2) % 5), 9f), z + 7, matRockWall);

                // Recifes de corais posicionados verticalmente em escalas biológicas reais (2.5m - 3.5m)
                SpawnModelOrFallback(
                    fbxCoralTube,
                    PrimitiveType.Cylinder,
                    $"Coral_TubeCluster_L_{z}",
                    envRoot.transform,
                    new Vector3(-2.3f + ((z * 7) % 5) * 0.08f, 0.0f, z),
                    Quaternion.Euler(0f, z * 23f, 0f),
                    Vector3.one * (80f + (z % 3) * 12f),
                    (z % 16 == 0) ? matCoralCyan : matCoralMagenta);

                SpawnModelOrFallback(
                    fbxCoralStaghorn,
                    PrimitiveType.Cylinder,
                    $"Coral_Staghorn_R_{z}",
                    envRoot.transform,
                    new Vector3(2.2f - ((z * 11) % 5) * 0.08f, 0.0f, z + 2.0f),
                    Quaternion.Euler(0f, -z * 31f, 0f),
                    Vector3.one * (90f + ((z + 1) % 3) * 15f),
                    (z % 16 == 0) ? matCoralAmber : matCoralCyan);

                if (z % 16 == 6)
                {
                    SpawnModelOrFallback(
                        fbxCoralBrain,
                        PrimitiveType.Sphere,
                        $"Coral_Brain_Mound_{z}",
                        envRoot.transform,
                        new Vector3((z % 32 == 6) ? -1.8f : 1.8f, 0.0f, z + 3.5f),
                        Quaternion.Euler(0f, z * 15f, 0f),
                        Vector3.one * 85f,
                        matCoralAmber);
                }

                // Arcos rochosos abissais a cada 32m
                if (z == 22 || z == 54 || z == 86)
                {
                    SpawnModelOrFallback(
                        fbxRockArch,
                        PrimitiveType.Cube,
                        $"Abyssal_RockArch_{z}",
                        envRoot.transform,
                        new Vector3(0f, 0f, z),
                        Quaternion.identity,
                        Vector3.one * 110f,
                        matRockWall);

                    CreateBiolumLight(envRoot.transform, $"Reef_GlowLight_{z}", new Vector3(0f, 5.0f, z - 2f), new Color(0.0f, 0.9f, 1.0f), 3.0f, 22f);
                }
            }

            // Cardumes de Peixes Marinhos (Barramundi Fish PBR) nadando na fenda em diversas profundidades diretamente no campo de visão
            var faunaRoot = new GameObject("Environment_MarineFauna");
            faunaRoot.transform.SetParent(envRoot.transform);

            SpawnFishSchool(faunaRoot.transform, fbxFish, matFish, new Vector3(0.2f, 3.2f, 6.5f), 5, 1.3f, 175f);
            SpawnFishSchool(faunaRoot.transform, fbxFish, matFish, new Vector3(-0.6f, 2.8f, 17.5f), 5, 1.4f, 25f);
            SpawnFishSchool(faunaRoot.transform, fbxFish, matFish, new Vector3(0.8f, 3.2f, 18.5f), 4, 1.2f, -50f);
            SpawnFishSchool(faunaRoot.transform, fbxFish, matFish, new Vector3(-0.3f, 3.1f, 22.5f), 4, 1.2f, -30f);
            SpawnFishSchool(faunaRoot.transform, fbxFish, matFish, new Vector3(0.4f, 2.7f, 54.5f), 5, 1.4f, 190f);
            SpawnFishSchool(faunaRoot.transform, fbxFish, matFish, new Vector3(0.5f, 3.4f, 85.0f), 5, 1.5f, 45f);
            SpawnFishSchool(faunaRoot.transform, fbxFish, matFish, new Vector3(-0.4f, 3.7f, 107.0f), 4, 1.3f, -75f);

            // 6. Sistemas Globais (_GameSystems: Input, Audio, AbyssalGameManager)
            var sysObj = new GameObject("_GameSystems");
            sysObj.AddComponent<AbyssalInputAdapter>();
            sysObj.AddComponent<ProceduralAudioService>();
            var gameMgr = sysObj.AddComponent<AbyssalGameManager>();

            // 7. Elementos de Gameplay (Pods Naufragados, Núcleos de Dados, Cilindros de O2, Drones e Cápsula de Extração)
            var gameplayRoot = new GameObject("Gameplay_Elements");

            // Pods Naufragados + Núcleos de Dados + Drones Sentinelas
            float[] podZPositions = { 25f, 58f, 88f };
            for (int i = 0; i < podZPositions.Length; i++)
            {
                float pz = podZPositions[i];
                float px = (i % 2 == 0) ? -2.2f : 2.2f;

                SpawnModelOrFallback(
                    fbxSunkenPod,
                    PrimitiveType.Capsule,
                    $"Sunken_Scientific_Pod_{i + 1}",
                    gameplayRoot.transform,
                    new Vector3(px, 0.0f, pz),
                    Quaternion.Euler(0f, 35f * (i + 1), 0f),
                    Vector3.one * 1.0f,
                    matPodHull);

                // Núcleo de Energia/Dados ao lado do Pod
                CreateCollectibleObject(
                    gameplayRoot.transform,
                    $"EnergyCore_{i + 1}",
                    AbyssalCollectibleType.EnergyCore,
                    new Vector3(px * 0.4f, 2.4f, pz - 1.5f),
                    matEnergyCore);

                // Drone Sentinela Hostil patrulhando o Pod
                CreateThreatDroneObject(
                    gameplayRoot.transform,
                    $"Abyssal_ThreatDrone_{i + 1}",
                    fbxDrone,
                    new Vector3(-px * 0.4f, 3.1f, pz - 1.0f),
                    matDroneHull,
                    matDroneEye);
            }

            // Cilindros de Oxigênio (O2 Tanks) distribuídos estrategicamente
            float[] o2ZPositions = { 16f, 42f, 72f, 98f };
            for (int i = 0; i < o2ZPositions.Length; i++)
            {
                CreateCollectibleObject(
                    gameplayRoot.transform,
                    $"OxygenTank_{i + 1}",
                    AbyssalCollectibleType.OxygenTank,
                    new Vector3((i % 2 == 0) ? 1.2f : -1.2f, 2.6f, o2ZPositions[i]),
                    matOxygenTank);
            }

            // Cápsula Submarina de Extração em Z = 115m
            var extractionPod = CreateExtractionPodObject(
                gameplayRoot.transform,
                fbxExtractionPod,
                new Vector3(0f, 0.0f, 115f),
                matExtractionPod,
                matCoralCyan);

            // 8. Criar Mergulhador em Primeira Pessoa (Abyssal_Diver_Player) + Rig de Armas (Rifle & Faca)
            var playerObj = CreateFirstPersonDiverPlayer(fbxRifle, fbxKnife, matRifle, matRifleGlow, matKnife);
            var motor = playerObj.GetComponent<AbyssalFirstPersonMotor>();
            var survival = playerObj.GetComponent<OxygenSurvivalSystem>();
            var weapons = playerObj.GetComponent<AbyssalWeaponController>();
            var mainCam = playerObj.GetComponentInChildren<Camera>();

            gameMgr.SetPlayerSystems(motor, survival, weapons, extractionPod);
            var gmSo = new SerializedObject(gameMgr);
            gmSo.FindProperty("_playerMotor").objectReferenceValue = motor;
            gmSo.FindProperty("_survivalSystem").objectReferenceValue = survival;
            gmSo.FindProperty("_weaponController").objectReferenceValue = weapons;
            gmSo.FindProperty("_extractionPod").objectReferenceValue = extractionPod;
            gmSo.ApplyModifiedProperties();

            // 9. Volume Global URP (Bloom, ACES Tonemapping, Vignette)
            var volumeObj = new GameObject("Global_PostProcessing_Volume");
            var volume = volumeObj.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            if (!Directory.Exists("Assets/Game/Settings")) Directory.CreateDirectory("Assets/Game/Settings");
            string profilePath = "Assets/Game/Settings/AbyssalBreach_VolumeProfile.asset";
            if (AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.VolumeProfile>(profilePath) != null)
            {
                AssetDatabase.DeleteAsset(profilePath);
            }
            AssetDatabase.CreateAsset(profile, profilePath);
            volume.profile = profile;

            var bloom = profile.Add<Bloom>(true);
            bloom.threshold.Override(0.82f);
            bloom.intensity.Override(1.35f);
            bloom.scatter.Override(0.68f);

            var tonemapping = profile.Add<Tonemapping>(true);
            tonemapping.mode.Override(TonemappingMode.ACES);

            var vignette = profile.Add<Vignette>(true);
            vignette.intensity.Override(0.34f);
            vignette.smoothness.Override(0.42f);

            // 10. Construir HUD Tático do Capacete de Mergulho
            CreateHelmetHUDCanvas(sysObj, mainCam);

            // 11. Salvar Cena e Registrar nos Build Settings
            bool saved = EditorSceneManager.SaveScene(scene, scenePath);
            if (!saved)
            {
                Debug.LogError($"[AbyssalBreachSceneBuilder] Falha ao salvar cena em {scenePath}");
                return false;
            }

            RegisterSceneInBuildSettings(scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log($"[AbyssalBreachSceneBuilder] Cena AbyssalBreach salva com sucesso em: {scenePath}");
            return true;
        }

        #region Helpers de Construção

        private static Material CreateUrpMaterial(
            string name,
            Color baseColor,
            Color emissionColor,
            Texture2D baseMap,
            Texture2D normalMap,
            float metallic,
            float smoothness,
            Vector2 tiling = default)
        {
            string path = $"{MAT_DIR}/{name}.mat";
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.name = name;
            mat.SetColor("_BaseColor", baseColor);
            mat.color = baseColor;
            if (baseMap != null)
            {
                mat.SetTexture("_BaseMap", baseMap);
                if (tiling != Vector2.zero)
                {
                    mat.SetTextureScale("_BaseMap", tiling);
                }
                else
                {
                    mat.SetTextureScale("_BaseMap", Vector2.one);
                }
            }
            if (normalMap != null)
            {
                mat.SetTexture("_BumpMap", normalMap);
                mat.EnableKeyword("_NORMALMAP");
                if (tiling != Vector2.zero)
                {
                    mat.SetTextureScale("_BumpMap", tiling);
                }
            }
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);

            if (emissionColor.maxColorComponent > 0.01f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emissionColor);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                mat.DisableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", Color.black);
            }

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SpawnFishSchool(
            Transform parent,
            GameObject fbxFish,
            Material matFish,
            Vector3 center,
            int count,
            float spreadRadius,
            float swimHeading)
        {
            if (fbxFish == null) return;
            var schoolRoot = new GameObject($"Fish_School_Z{(int)center.z}");
            schoolRoot.transform.SetParent(parent);
            schoolRoot.transform.position = center;

            for (int i = 0; i < count; i++)
            {
                float angle = (i / (float)count) * Mathf.PI * 2f;
                float r = (i == 0) ? 0f : spreadRadius * (0.6f + 0.4f * Mathf.Sin(i * 1.9f));
                Vector3 offset = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle * 2.5f) * 0.4f, Mathf.Sin(angle) * r);

                var fish = UnityEngine.Object.Instantiate(fbxFish, schoolRoot.transform);
                fish.name = $"Barramundi_Fish_{i + 1}";
                fish.transform.localPosition = offset;
                float rotJitter = Mathf.Sin(i * 2.7f) * 12f;
                fish.transform.localRotation = Quaternion.Euler(0f, swimHeading + rotJitter, 0f) * Quaternion.Euler(-90f, 180f, 0f);
                float scaleJitter = 135f + (i % 3) * 15f;
                fish.transform.localScale = Vector3.one * scaleJitter;

                foreach (var renderer in fish.GetComponentsInChildren<Renderer>())
                {
                    renderer.sharedMaterial = matFish;
                }
                foreach (var col in fish.GetComponentsInChildren<Collider>())
                {
                    UnityEngine.Object.DestroyImmediate(col);
                }
            }
        }

        // Pool determinístico de rochas modulares (SpaceKit/FantasyTown CC0) para os paredões
        private static readonly string[] ROCK_POOL =
        {
            "rock_largeA", "rock_largeB", "rock_crystalsLargeA",
            "rock_crystalsLargeB", "rock-wide", "rock-large",
        };

        private static void CreateTrenchWallRock(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 targetSize,
            int seed,
            Material rockMat)
        {
            var prefabName = ROCK_POOL[seed % ROCK_POOL.Length];
            float yaw = (seed * 53) % 360f;
            var rock = ProceduralMeshLibrary.CreateKitInstance(prefabName, name, parent, position, yaw);
            if (rock != null)
            {
                ProceduralMeshLibrary.ScaleToFit(rock, targetSize, baseY: -0.6f);
                foreach (var r in rock.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterial = rockMat;
                }
                return;
            }

            // Fallback: malha procedural própria (nunca primitiva nativa)
            var fb = ProceduralMeshLibrary.CreatePart(
                name, ProceduralMeshLibrary.Box, parent,
                position + new Vector3(0f, targetSize.y * 0.5f, 0f),
                Quaternion.identity, targetSize, rockMat);
            fb.AddComponent<BoxCollider>();
        }

        private static GameObject SpawnModelOrFallback(
            GameObject fbxPrefab,
            PrimitiveType fallbackType,
            string name,
            Transform parent,
            Vector3 position,
            Quaternion rotation,
            Vector3 scale,
            Material material)
        {
            GameObject instance;
            if (fbxPrefab != null)
            {
                instance = UnityEngine.Object.Instantiate(fbxPrefab, parent);
            }
            else
            {
                // Fallback V2: malha procedural própria equivalente (nunca primitiva nativa)
                instance = ProceduralMeshLibrary.CreatePart(
                    name,
                    ProceduralMeshLibrary.ForPrimitive(fallbackType),
                    parent,
                    position,
                    rotation,
                    scale,
                    material);
            }

            instance.name = name;
            if (parent != null)
            {
                instance.transform.localPosition = position;
                instance.transform.localRotation = rotation;
            }
            else
            {
                instance.transform.position = position;
                instance.transform.rotation = rotation;
            }
            instance.transform.localScale = scale;

            foreach (var r in instance.GetComponentsInChildren<Renderer>())
            {
                r.sharedMaterial = material;
            }
            foreach (var c in instance.GetComponentsInChildren<Collider>())
            {
                UnityEngine.Object.DestroyImmediate(c);
            }

            return instance;
        }

        private static void CreateBiolumLight(Transform parent, string name, Vector3 pos, Color color, float intensity, float range)
        {
            var lObj = new GameObject(name);
            lObj.transform.SetParent(parent);
            lObj.transform.position = pos;
            var light = lObj.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            lObj.AddComponent<UniversalAdditionalLightData>();
        }

        private static GameObject CreateCollectibleObject(
            Transform parent,
            string name,
            AbyssalCollectibleType type,
            Vector3 position,
            Material mat)
        {
            PrimitiveType prim = type == AbyssalCollectibleType.OxygenTank ? PrimitiveType.Cylinder : PrimitiveType.Sphere;
            var obj = ProceduralMeshLibrary.CreatePart(
                name,
                ProceduralMeshLibrary.ForPrimitive(prim),
                parent,
                position,
                Quaternion.identity,
                type == AbyssalCollectibleType.OxygenTank
                    ? new Vector3(0.45f, 0.55f, 0.45f)
                    : new Vector3(0.75f, 0.75f, 0.75f),
                mat);

            var col = type == AbyssalCollectibleType.OxygenTank
                ? (Collider)obj.AddComponent<CapsuleCollider>()
                : obj.AddComponent<SphereCollider>();
            col.isTrigger = true;

            var collectible = obj.AddComponent<AbyssalCollectible>();
            collectible.Configure(type);
            var so = new SerializedObject(collectible);
            so.FindProperty("_collectibleType").enumValueIndex = (int)type;
            so.ApplyModifiedProperties();

            return obj;
        }

        private static AbyssalThreatDrone CreateThreatDroneObject(
            Transform parent,
            string name,
            GameObject fbxDrone,
            Vector3 position,
            Material hullMat,
            Material eyeMat)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent);
            root.transform.position = position;

            var sphereCol = root.AddComponent<SphereCollider>();
            sphereCol.radius = 1.1f;

            var meshObj = SpawnModelOrFallback(
                fbxDrone,
                PrimitiveType.Sphere,
                "Drone_Mesh",
                root.transform,
                position,
                Quaternion.Euler(0f, 180f, 0f),
                Vector3.one * 1.5f,
                hullMat);
            meshObj.transform.localPosition = Vector3.zero;

            ProceduralMeshLibrary.CreatePart(
                "Sensor_Eye_Glow",
                ProceduralMeshLibrary.Sphere,
                root.transform,
                new Vector3(0f, 0.05f, 0.45f),
                Quaternion.identity,
                new Vector3(0.25f, 0.25f, 0.20f),
                eyeMat);

            var drone = root.AddComponent<AbyssalThreatDrone>();
            return drone;
        }

        private static AbyssalExtractionPod CreateExtractionPodObject(
            Transform parent,
            GameObject fbxPod,
            Vector3 position,
            Material podMat,
            Material ringMat)
        {
            var root = new GameObject("Abyssal_Extraction_SubPod");
            root.transform.SetParent(parent);
            root.transform.position = position;

            var col = root.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(6.5f, 7.0f, 6.5f);
            col.center = new Vector3(0f, 3.5f, 0f);

            var meshObj = SpawnModelOrFallback(
                fbxPod,
                PrimitiveType.Cylinder,
                "SubPod_Structure",
                root.transform,
                position,
                Quaternion.identity,
                Vector3.one * 1.0f,
                podMat);
            meshObj.transform.localPosition = Vector3.zero;

            // Anel de sinalização — toro procedural do pipeline V2 (eixo +Z, já vertical)
            var beaconRing = ProceduralMeshLibrary.CreatePart(
                "Extraction_Beacon_Ring",
                ProceduralMeshLibrary.Torus,
                root.transform,
                new Vector3(0f, 3.2f, -1.8f),
                Quaternion.identity,
                new Vector3(5.2f, 5.2f, 5.2f),
                ringMat);

            CreateBiolumLight(root.transform, "Extraction_Beacon_Light", position + new Vector3(0f, 4.0f, -2.5f), new Color(0.1f, 1.0f, 0.85f), 4.5f, 24f);

            var pod = root.AddComponent<AbyssalExtractionPod>();
            var so = new SerializedObject(pod);
            so.FindProperty("_beaconRing").objectReferenceValue = beaconRing.transform;
            so.ApplyModifiedProperties();

            return pod;
        }

        private static GameObject CreateFirstPersonDiverPlayer(
            GameObject fbxRifle,
            GameObject fbxKnife,
            Material matRifle,
            Material matRifleGlow,
            Material matKnife)
        {
            var player = new GameObject("Abyssal_Diver_Player");
            player.tag = "Player";
            player.transform.position = new Vector3(0f, 3.2f, 0f);

            var col = player.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.45f;
            col.center = new Vector3(0f, 0.2f, 0f);

            var rb = player.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            var motor = player.AddComponent<AbyssalFirstPersonMotor>();
            var survival = player.AddComponent<OxygenSurvivalSystem>();
            var weapons = player.AddComponent<AbyssalWeaponController>();

            // Camera Pivot & FPS Camera
            var cameraPivot = new GameObject("CameraPivot");
            cameraPivot.transform.SetParent(player.transform);
            cameraPivot.transform.localPosition = new Vector3(0f, 0.65f, 0f);
            cameraPivot.transform.localRotation = Quaternion.identity;

            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            camObj.transform.SetParent(cameraPivot.transform);
            camObj.transform.localPosition = Vector3.zero;
            camObj.transform.localRotation = Quaternion.identity;

            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.02f, 0.14f, 0.24f);
            cam.fieldOfView = AbyssalMath.DEFAULT_FOV;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 260f;
            var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camObj.AddComponent<AudioListener>();

            // Lanterna tática acoplada ao capacete (ilumina o rifle em 1ª pessoa e os corais próximos)
            var headlampObj = new GameObject("Diver_Headlamp");
            headlampObj.transform.SetParent(cameraPivot.transform);
            headlampObj.transform.localPosition = new Vector3(0f, 0.12f, 0.15f);
            var headlamp = headlampObj.AddComponent<Light>();
            headlamp.type = LightType.Point;
            headlamp.color = new Color(0.65f, 0.95f, 1.0f);
            headlamp.intensity = 2.2f;
            headlamp.range = 18f;
            headlampObj.AddComponent<UniversalAdditionalLightData>();

            // Weapon Holder em Primeira Pessoa
            var weaponHolder = new GameObject("WeaponHolder");
            weaponHolder.transform.SetParent(cameraPivot.transform);
            weaponHolder.transform.localPosition = Vector3.zero;
            weaponHolder.transform.localRotation = Quaternion.identity;

            // 1. Rifle de Plasma Submarino (FPS_PlasmaRifle) com modelo 3D texturizado autêntico
            var rifleRoot = new GameObject("FPS_PlasmaRifle");
            rifleRoot.transform.SetParent(weaponHolder.transform);
            rifleRoot.transform.localPosition = new Vector3(0.20f, -0.20f, 0.44f);
            rifleRoot.transform.localRotation = Quaternion.identity;

            var rifleMesh = SpawnModelOrFallback(
                fbxRifle,
                PrimitiveType.Cube,
                "Assault_Rifle_B_Mesh",
                rifleRoot.transform,
                Vector3.zero,
                Quaternion.Euler(-90f, 0f, 0f),
                Vector3.one * 55f,
                matRifle);
            rifleMesh.transform.localPosition = Vector3.zero;
            rifleMesh.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);

            var muzzlePoint = new GameObject("MuzzlePoint");
            muzzlePoint.transform.SetParent(rifleRoot.transform);
            muzzlePoint.transform.localPosition = new Vector3(0f, 0.02f, 0.52f);

            var tracerObj = new GameObject("PlasmaTracerLine");
            tracerObj.transform.SetParent(rifleRoot.transform);
            var lineRenderer = tracerObj.AddComponent<LineRenderer>();
            lineRenderer.sharedMaterial = matRifleGlow;
            lineRenderer.startWidth = 0.035f;
            lineRenderer.endWidth = 0.012f;
            lineRenderer.positionCount = 2;
            lineRenderer.enabled = false;

            // 2. Faca Tática de Mergulho (FPS_TacticalKnife) com mapas PBR 2K reais
            var knifeRoot = new GameObject("FPS_TacticalKnife");
            knifeRoot.transform.SetParent(weaponHolder.transform);
            knifeRoot.transform.localPosition = new Vector3(0.18f, -0.16f, 0.40f);
            knifeRoot.transform.localRotation = Quaternion.identity;

            var knifeMesh = SpawnModelOrFallback(
                fbxKnife,
                PrimitiveType.Cube,
                "Tactical_Knife_Mesh",
                knifeRoot.transform,
                Vector3.zero,
                Quaternion.Euler(-75f, -20f, 15f),
                Vector3.one * 58f,
                matKnife);
            knifeMesh.transform.localPosition = Vector3.zero;
            knifeMesh.transform.localRotation = Quaternion.Euler(-75f, -20f, 15f);

            knifeRoot.SetActive(false);

            // Vincular SerializedProperties
            motor.SetCameraReferences(cameraPivot.transform, cam);
            var motorSo = new SerializedObject(motor);
            motorSo.FindProperty("_cameraPivot").objectReferenceValue = cameraPivot.transform;
            motorSo.FindProperty("_fpsCamera").objectReferenceValue = cam;
            motorSo.ApplyModifiedProperties();

            weapons.SetWeaponReferences(weaponHolder.transform, rifleRoot.transform, knifeRoot.transform, muzzlePoint.transform, lineRenderer);
            var weapSo = new SerializedObject(weapons);
            weapSo.FindProperty("_weaponHolder").objectReferenceValue = weaponHolder.transform;
            weapSo.FindProperty("_rifleModel").objectReferenceValue = rifleRoot.transform;
            weapSo.FindProperty("_knifeModel").objectReferenceValue = knifeRoot.transform;
            weapSo.FindProperty("_muzzlePoint").objectReferenceValue = muzzlePoint.transform;
            weapSo.FindProperty("_plasmaTracerLine").objectReferenceValue = lineRenderer;
            weapSo.ApplyModifiedProperties();

            return player;
        }

        private static void CreateHelmetHUDCanvas(GameObject systemObj, Camera mainCam)
        {
            var canvasObj = new GameObject("Abyssal_Helmet_Canvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = mainCam;
            canvas.planeDistance = 0.22f;

            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            // 1. HUD_Panel (Visor Tático de Mergulho)
            var hudPanel = CreatePanel(canvasObj.transform, "HUD_Panel", new Color(0f, 0f, 0f, 0f));

            // Barra Superior de Telemetria (Profundidade, Bússola, Núcleos)
            var topBar = CreateElement(hudPanel.transform, "Helmet_TopBezel", new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(1220f, 44f));
            topBar.AddComponent<Image>().color = new Color(0.01f, 0.09f, 0.16f, 0.78f);
            var depthCompassText = CreateText(topBar.transform, "DepthCompass_Text", font, "DEPTH: -241.8m   |   HDG: 000°   |   EXTRACTION POD: 115m", 15, new Color(0.2f, 0.95f, 1f), TextAnchor.MiddleLeft, new Vector2(-150f, 0f), new Vector2(820f, 36f));
            var missionCoresText = CreateText(topBar.transform, "MissionCores_Text", font, "DATA CORES: 0/3   |   DRONES NEUTRALIZED: 0   |   SCORE: 0000", 15, new Color(1f, 0.85f, 0.25f), TextAnchor.MiddleRight, new Vector2(150f, 0f), new Vector2(820f, 36f));

            // Painel Inferior Esquerdo (Oxigênio O2 + Integridade HP)
            var vitalsBox = CreateElement(hudPanel.transform, "Helmet_VitalsBox", new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(210f, 68f), new Vector2(360f, 96f));
            vitalsBox.AddComponent<Image>().color = new Color(0.01f, 0.09f, 0.16f, 0.80f);

            var oxygenText = CreateText(vitalsBox.transform, "Oxygen_Text", font, "O2 SUPPLY: 100%", 15, new Color(0f, 0.95f, 1f), TextAnchor.MiddleLeft, new Vector2(0f, 24f), new Vector2(320f, 24f));
            var o2BarBg = CreateElement(vitalsBox.transform, "O2_Bar_BG", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 6f), new Vector2(320f, 12f));
            o2BarBg.AddComponent<Image>().color = new Color(0.05f, 0.2f, 0.28f, 0.9f);
            var o2FillObj = CreateElement(o2BarBg.transform, "O2_Bar_Fill", new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var oxygenFillBar = o2FillObj.AddComponent<Image>();
            oxygenFillBar.color = new Color(0f, 0.92f, 1f, 0.95f);
            oxygenFillBar.type = Image.Type.Filled;
            oxygenFillBar.fillMethod = Image.FillMethod.Horizontal;
            oxygenFillBar.fillAmount = 1.0f;

            var healthText = CreateText(vitalsBox.transform, "Health_Text", font, "SUIT INTEGRITY: 100%", 15, new Color(0.25f, 1f, 0.72f), TextAnchor.MiddleLeft, new Vector2(0f, -16f), new Vector2(320f, 24f));
            var hpBarBg = CreateElement(vitalsBox.transform, "HP_Bar_BG", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -34f), new Vector2(320f, 10f));
            hpBarBg.AddComponent<Image>().color = new Color(0.05f, 0.2f, 0.18f, 0.9f);
            var hpFillObj = CreateElement(hpBarBg.transform, "HP_Bar_Fill", new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var healthFillBar = hpFillObj.AddComponent<Image>();
            healthFillBar.color = new Color(0.2f, 1f, 0.65f, 0.95f);
            healthFillBar.type = Image.Type.Filled;
            healthFillBar.fillMethod = Image.FillMethod.Horizontal;
            healthFillBar.fillAmount = 1.0f;

            // Painel Inferior Direito (Munição + Arma Ativa)
            var weaponBox = CreateElement(hudPanel.transform, "Helmet_WeaponBox", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-220f, 68f), new Vector2(380f, 96f));
            weaponBox.AddComponent<Image>().color = new Color(0.01f, 0.09f, 0.16f, 0.80f);
            var ammoText = CreateText(weaponBox.transform, "Ammo_Text", font, "AMMO: 24 / 096", 20, new Color(0.2f, 0.98f, 1f), TextAnchor.MiddleRight, new Vector2(0f, 16f), new Vector2(340f, 30f));
            var weaponModeText = CreateText(weaponBox.transform, "WeaponMode_Text", font, "PLASMA RIFLE [MK-IV]  |  HIP-FIRE / SWIM", 13, new Color(0.65f, 0.9f, 1f), TextAnchor.MiddleRight, new Vector2(0f, -18f), new Vector2(340f, 24f));

            // Retículo Central Dinâmico
            var reticleText = CreateText(hudPanel.transform, "Dynamic_Reticle_Text", font, "(  +  )", 22, new Color(0f, 0.95f, 1f, 0.85f), TextAnchor.MiddleCenter, Vector2.zero, new Vector2(160f, 50f));

            // 2. Start_Panel (Briefing Tático)
            var startPanel = CreatePanel(canvasObj.transform, "Start_Panel", new Color(0.01f, 0.06f, 0.12f, 0.72f));
            CreateText(startPanel.transform, "Title_Text", font, "ABYSSAL BREACH", 44, new Color(0f, 0.95f, 1f), TextAnchor.MiddleCenter, new Vector2(0f, 90f), new Vector2(800f, 70f));
            CreateText(startPanel.transform, "Subtitle_Text", font, "SUB-AQUATIC TACTICAL EXTRACTION & SURVIVAL FPS", 18, new Color(0.6f, 0.92f, 1f), TextAnchor.MiddleCenter, new Vector2(0f, 35f), new Vector2(800f, 40f));
            CreateText(startPanel.transform, "Controls_Text", font, "[WASD] SWIM   |   [SPACE / C] ASCEND & DESCEND   |   [MOUSE2 / Q] OPTICAL ADS\n[MOUSE1 / F] FIRE PLASMA RIFLE   |   [V / TAB] TACTICAL DIVE KNIFE   |   [R] RELOAD", 15, new Color(0.85f, 0.96f, 1f), TextAnchor.MiddleCenter, new Vector2(0f, -35f), new Vector2(900f, 70f));
            CreateText(startPanel.transform, "Prompt_Text", font, ">>> PRESS [WASD] OR [FIRE] TO DIVE INTO TRENCH <<<", 20, new Color(0.2f, 1f, 0.7f), TextAnchor.MiddleCenter, new Vector2(0f, -115f), new Vector2(800f, 40f));

            // 3. Victory_Panel (Extração Concluída)
            var victoryPanel = CreatePanel(canvasObj.transform, "Victory_Panel", new Color(0.01f, 0.14f, 0.18f, 0.80f));
            CreateText(victoryPanel.transform, "Victory_Title", font, "EXTRACTION SUCCESSFUL", 42, new Color(0.2f, 1f, 0.75f), TextAnchor.MiddleCenter, new Vector2(0f, 70f), new Vector2(850f, 65f));
            var victorySummaryText = CreateText(victoryPanel.transform, "Victory_Summary", font, "SUB-POD DOCKED — ALL BIOMETRICS STABLE\nDATA CORES: 3/3  |  DRONES CLEARED: 3  |  FINAL SCORE: 2450", 18, Color.white, TextAnchor.MiddleCenter, new Vector2(0f, -10f), new Vector2(850f, 80f));
            CreateText(victoryPanel.transform, "Victory_RestartPrompt", font, "PRESS [ENTER] TO RE-DEPLOY", 16, new Color(0f, 0.9f, 1f), TextAnchor.MiddleCenter, new Vector2(0f, -95f), new Vector2(600f, 40f));

            // 4. GameOver_Panel (Hipóxia / Falha na Missão)
            var gameOverPanel = CreatePanel(canvasObj.transform, "GameOver_Panel", new Color(0.18f, 0.02f, 0.05f, 0.82f));
            CreateText(gameOverPanel.transform, "GameOver_Title", font, "MISSION FAILED — SIGNAL LOST", 42, new Color(1f, 0.25f, 0.25f), TextAnchor.MiddleCenter, new Vector2(0f, 70f), new Vector2(850f, 65f));
            var gameOverReasonText = CreateText(gameOverPanel.transform, "GameOver_Reason", font, "CAUSE: CRITICAL HYPOXIA (OXYGEN DEPLETED IN TRENCH)", 18, new Color(1f, 0.8f, 0.8f), TextAnchor.MiddleCenter, new Vector2(0f, -10f), new Vector2(850f, 60f));
            CreateText(gameOverPanel.transform, "GameOver_RestartPrompt", font, "PRESS [ENTER] FOR EMERGENCY RESUSCITATION", 16, new Color(1f, 0.6f, 0.4f), TextAnchor.MiddleCenter, new Vector2(0f, -95f), new Vector2(600f, 40f));

            startPanel.SetActive(true);
            hudPanel.SetActive(false);
            victoryPanel.SetActive(false);
            gameOverPanel.SetActive(false);

            var hudMgr = systemObj.AddComponent<AbyssalHUDManager>();
            hudMgr.ConfigureReferences(
                startPanel,
                hudPanel,
                victoryPanel,
                gameOverPanel,
                oxygenText,
                oxygenFillBar,
                healthText,
                healthFillBar,
                ammoText,
                weaponModeText,
                depthCompassText,
                missionCoresText,
                reticleText,
                victorySummaryText,
                gameOverReasonText);

            var hudSo = new SerializedObject(hudMgr);
            hudSo.FindProperty("_startPanel").objectReferenceValue = startPanel;
            hudSo.FindProperty("_hudPanel").objectReferenceValue = hudPanel;
            hudSo.FindProperty("_victoryPanel").objectReferenceValue = victoryPanel;
            hudSo.FindProperty("_gameOverPanel").objectReferenceValue = gameOverPanel;
            hudSo.FindProperty("_oxygenText").objectReferenceValue = oxygenText;
            hudSo.FindProperty("_oxygenFillBar").objectReferenceValue = oxygenFillBar;
            hudSo.FindProperty("_healthText").objectReferenceValue = healthText;
            hudSo.FindProperty("_healthFillBar").objectReferenceValue = healthFillBar;
            hudSo.FindProperty("_ammoText").objectReferenceValue = ammoText;
            hudSo.FindProperty("_weaponModeText").objectReferenceValue = weaponModeText;
            hudSo.FindProperty("_depthCompassText").objectReferenceValue = depthCompassText;
            hudSo.FindProperty("_missionCoresText").objectReferenceValue = missionCoresText;
            hudSo.FindProperty("_reticleCrosshairText").objectReferenceValue = reticleText;
            hudSo.FindProperty("_victorySummaryText").objectReferenceValue = victorySummaryText;
            hudSo.FindProperty("_gameOverReasonText").objectReferenceValue = gameOverReasonText;
            hudSo.ApplyModifiedProperties();
        }

        private static GameObject CreatePanel(Transform parent, string name, Color bgColor)
        {
            var panel = new GameObject(name);
            panel.transform.SetParent(parent, false);
            var rt = panel.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = panel.AddComponent<Image>();
            img.color = bgColor;
            return panel;
        }

        private static GameObject CreateElement(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = anchoredPos;
            rt.sizeDelta = sizeDelta;
            return go;
        }

        private static Text CreateText(Transform parent, string name, Font font, string content, int fontSize, Color color, TextAnchor align, Vector2 anchoredPos, Vector2 sizeDelta)
        {
            var go = CreateElement(parent, name, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), anchoredPos, sizeDelta);
            var txt = go.AddComponent<Text>();
            txt.font = font;
            txt.text = content;
            txt.fontSize = fontSize;
            txt.fontStyle = FontStyle.Bold;
            txt.color = color;
            txt.alignment = align;
            txt.horizontalOverflow = HorizontalWrapMode.Overflow;
            txt.verticalOverflow = VerticalWrapMode.Overflow;
            return txt;
        }

        private static void RegisterSceneInBuildSettings(string scenePath)
        {
            var current = EditorBuildSettings.scenes;
            foreach (var s in current)
            {
                if (string.Equals(s.path, scenePath, StringComparison.OrdinalIgnoreCase))
                    return;
            }
            var next = new EditorBuildSettingsScene[current.Length + 1];
            Array.Copy(current, next, current.Length);
            next[current.Length] = new EditorBuildSettingsScene(scenePath, true);
            EditorBuildSettings.scenes = next;
        }

        #endregion
    }
}
