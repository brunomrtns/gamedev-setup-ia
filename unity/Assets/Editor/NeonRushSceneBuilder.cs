using System;
using System.IO;
using Studio.Cameras;
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
    public static class NeonRushSceneBuilder
    {
        private const string SCENE_PATH = "Assets/Game/Scenes/NeonRush.unity";
        private const string MAT_DIR = "Assets/Game/Materials/NeonRush";

        [MenuItem("Studio/Neon Rush/Build Neon Rush Scene")]
        public static void BuildSceneMenu()
        {
            BuildNeonRushScene(SCENE_PATH);
        }

        public static void RunBuildSceneBatch()
        {
            Debug.Log("[NeonRushSceneBuilder] Iniciando montagem da cena NeonRush em batchmode...");
            bool success = BuildNeonRushScene(SCENE_PATH);
            if (success)
            {
                Debug.Log("[NeonRushSceneBuilder] Cena NeonRush criada com sucesso!");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[NeonRushSceneBuilder] Falha ao construir cena NeonRush!");
                EditorApplication.Exit(1);
            }
        }

        public static void CaptureNeonRushAnglesBatch()
        {
            Debug.Log("[NeonRushSceneBuilder] Iniciando captura de ângulos e telas de Neon Rush...");
            EditorSceneManager.OpenScene(SCENE_PATH, OpenSceneMode.Single);

            var cam = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Camera>();
            if (cam == null)
            {
                Debug.LogError("[NeonRushSceneBuilder] Nenhuma câmera encontrada na cena!");
                EditorApplication.Exit(1);
                return;
            }

            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures");
            if (!Directory.Exists(outDir)) Directory.CreateDirectory(outDir);

            var startPanel = GameObject.Find("Start_Panel");
            var hudPanel = GameObject.Find("HUD_Panel");
            var gameOverPanel = GameObject.Find("GameOver_Panel");
            var victoryPanel = GameObject.Find("Victory_Panel");

            // 1. Tela Inicial
            if (startPanel) startPanel.SetActive(true);
            if (hudPanel) hudPanel.SetActive(false);
            if (gameOverPanel) gameOverPanel.SetActive(false);
            if (victoryPanel) victoryPanel.SetActive(false);
            cam.transform.position = new Vector3(0f, 3.2f, -6.5f);
            cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            RenderAndSave(cam, Path.Combine(outDir, "neon_rush_01_start_screen.png"));

            // 2. Gameplay em Andamento
            if (startPanel) startPanel.SetActive(false);
            if (hudPanel) hudPanel.SetActive(true);
            cam.transform.position = new Vector3(0f, 3.2f, -6.5f);
            cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            RenderAndSave(cam, Path.Combine(outDir, "neon_rush_02_gameplay.png"));

            // 3. Obstáculo e Interação (Laser Hurdle + Célula Aérea em Z = 50m)
            cam.transform.position = new Vector3(0f, 3.5f, 41f);
            cam.transform.rotation = Quaternion.Euler(15f, 0f, 0f);
            RenderAndSave(cam, Path.Combine(outDir, "neon_rush_03_obstacles_hurdles.png"));

            // 4. Portal de Extração e Skyline Final (Z = 350m)
            cam.transform.position = new Vector3(0f, 4.2f, 332f);
            cam.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            RenderAndSave(cam, Path.Combine(outDir, "neon_rush_04_extraction_portal.png"));

            // 5. Tela de Derrota (Game Over)
            if (hudPanel) hudPanel.SetActive(false);
            if (gameOverPanel) gameOverPanel.SetActive(true);
            cam.transform.position = new Vector3(0f, 3.2f, 120f);
            cam.transform.rotation = Quaternion.Euler(18f, 0f, 0f);
            RenderAndSave(cam, Path.Combine(outDir, "neon_rush_05_game_over.png"));

            // 6. Tela de Vitória
            if (gameOverPanel) gameOverPanel.SetActive(false);
            if (victoryPanel) victoryPanel.SetActive(true);
            cam.transform.position = new Vector3(0f, 4.2f, 332f);
            cam.transform.rotation = Quaternion.Euler(12f, 0f, 0f);
            RenderAndSave(cam, Path.Combine(outDir, "neon_rush_06_victory.png"));

            Debug.Log("[NeonRushSceneBuilder] Todas as 6 capturas foram renderizadas com sucesso em evidence/captures/!");
            EditorApplication.Exit(0);
        }

        private static void RenderAndSave(UnityEngine.Camera cam, string filePath)
        {
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
            Debug.Log($"[NeonRushSceneBuilder] Captura salva: {Path.GetFileName(filePath)} ({bytes.Length / 1024} KB)");
        }

        public static bool BuildNeonRushScene(string scenePath)
        {
            // 1. Assegurar diretórios
            string sceneDir = Path.GetDirectoryName(scenePath);
            if (!Directory.Exists(sceneDir)) Directory.CreateDirectory(sceneDir);
            if (!Directory.Exists(MAT_DIR)) Directory.CreateDirectory(MAT_DIR);

            // 2. Criar ou carregar Materiais URP
            var matTrack = GetOrCreateMaterial("Mat_Track", new Color(0.05f, 0.05f, 0.08f), Color.black);
            var matLaneCyan = GetOrCreateMaterial("Mat_Lane_Cyan", new Color(0f, 0.8f, 1f), new Color(0f, 1.2f, 2.0f));
            var matLaneMagenta = GetOrCreateMaterial("Mat_Lane_Magenta", new Color(1f, 0.1f, 0.6f), new Color(2f, 0.2f, 1.2f));
            var matLaserHurdle = GetOrCreateMaterial("Mat_Laser_Hurdle", new Color(1f, 0.05f, 0.2f), new Color(2.5f, 0.1f, 0.5f));
            var matHighBeam = GetOrCreateMaterial("Mat_High_Beam", new Color(1f, 0.6f, 0.05f), new Color(2.2f, 1.3f, 0.1f));
            var matBlocker = GetOrCreateMaterial("Mat_Blocker", new Color(0.12f, 0.12f, 0.18f), new Color(0.3f, 0.1f, 0.4f));
            var matCell = GetOrCreateMaterial("Mat_EnergyCell", new Color(0f, 1f, 0.9f), new Color(0f, 2.5f, 2.2f));
            var matRobotBody = GetOrCreateMaterial("Mat_RobotBody", new Color(0.12f, 0.16f, 0.24f), Color.black);
            var matRobotVisor = GetOrCreateMaterial("Mat_RobotVisor", new Color(0f, 0.9f, 1f), new Color(0f, 2f, 2.5f));
            var matRobotThruster = GetOrCreateMaterial("Mat_RobotThruster", new Color(0.08f, 0.08f, 0.12f), new Color(0f, 1.5f, 2.2f));
            var matPortal = GetOrCreateMaterial("Mat_Portal", new Color(0.9f, 0.1f, 0.7f), new Color(2.5f, 0.2f, 1.8f));
            var matBuilding = GetOrCreateMaterial("Mat_Building", new Color(0.04f, 0.04f, 0.07f), new Color(0.1f, 0.05f, 0.2f));
            var matWindowStrip = GetOrCreateMaterial("Mat_WindowStrip", new Color(0.0f, 0.7f, 0.9f), new Color(0f, 1.2f, 1.6f));

            // 3. Criar nova cena limpa
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 4. Configurar Iluminação & Atmosfera
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.08f, 0.06f, 0.15f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.03f, 0.02f, 0.08f);
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.006f;

            // Luz Direcional
            var lightObj = new GameObject("Directional Light (Night Sky)");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(0.4f, 0.5f, 0.85f);
            light.intensity = 0.9f;
            light.shadows = LightShadows.Soft;
            lightObj.AddComponent<UniversalAdditionalLightData>();
            lightObj.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            // 5. Construir Pista e Cenário
            var environmentRoot = new GameObject("Environment");

            // Asfalto da Pista principal
            var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = "Track_Road";
            road.transform.SetParent(environmentRoot.transform);
            road.transform.position = new Vector3(0f, -0.25f, 175f);
            road.transform.localScale = new Vector3(7.5f, 0.5f, 360f);
            road.GetComponent<Renderer>().sharedMaterial = matTrack;

            // Linhas de Demarcação de Faixas (Neon Strips)
            CreateLaneStrip("Lane_Divider_Left", -1.1f, matLaneCyan, environmentRoot.transform);
            CreateLaneStrip("Lane_Divider_Right", 1.1f, matLaneCyan, environmentRoot.transform);
            CreateLaneStrip("GuardRail_Left", -3.6f, matLaneMagenta, environmentRoot.transform, 0.4f);
            CreateLaneStrip("GuardRail_Right", 3.6f, matLaneMagenta, environmentRoot.transform, 0.4f);

            // Edifícios e Estruturas Futuristas ao Longo do Percurso
            var buildingsRoot = new GameObject("City_Skyline");
            buildingsRoot.transform.SetParent(environmentRoot.transform);

            for (int z = 20; z <= 340; z += 25)
            {
                // Prédio Esquerdo
                float hLeft = 18f + ((z * 7) % 22);
                CreateBuilding(buildingsRoot.transform, new Vector3(-9.5f, hLeft * 0.5f - 0.25f, z), new Vector3(5f, hLeft, 12f), matBuilding, matWindowStrip);

                // Prédio Direito
                float hRight = 20f + ((z * 13) % 24);
                CreateBuilding(buildingsRoot.transform, new Vector3(9.5f, hRight * 0.5f - 0.25f, z), new Vector3(5f, hRight, 12f), matBuilding, matWindowStrip);

                // Arcos Estruturais Neo-Góticos a cada 50m
                if (z % 50 == 0)
                {
                    CreateNeoArch(buildingsRoot.transform, z, matBuilding, matLaneMagenta);
                }
            }

            // 6. Criar Obstáculos e Coletáveis
            var gameplayElementsRoot = new GameObject("Gameplay_Elements");

            // Z = 25m: Células de introdução (faixa central)
            CreateEnergyCellSequence(gameplayElementsRoot.transform, 0f, 25f, 4);

            // Z = 50m: Laser Baixo (Faixa Centro)
            CreateLowLaserHurdle(gameplayElementsRoot.transform, 0f, 50f, matLaserHurdle);
            CreateEnergyCell(gameplayElementsRoot.transform, new Vector3(0f, 2.3f, 50f), matCell); // Recompensa por pular!

            // Z = 75m: Células na faixa esquerda
            CreateEnergyCellSequence(gameplayElementsRoot.transform, GameConstants.LANE_LEFT_X, 70f, 3);

            // Z = 95m: Viga Alta (Faixa Esquerda)
            CreateHighBeamHazard(gameplayElementsRoot.transform, GameConstants.LANE_LEFT_X, 95f, matHighBeam);

            // Z = 120m: Monólito Bloqueador (Faixa Centro)
            CreateFullBlockHazard(gameplayElementsRoot.transform, GameConstants.LANE_CENTER_X, 120f, matBlocker, matLaserHurdle);

            // Z = 145m: Células na faixa direita
            CreateEnergyCellSequence(gameplayElementsRoot.transform, GameConstants.LANE_RIGHT_X, 140f, 3);

            // Z = 165m: Laser Baixo (Faixa Direita)
            CreateLowLaserHurdle(gameplayElementsRoot.transform, GameConstants.LANE_RIGHT_X, 165f, matLaserHurdle);
            CreateEnergyCell(gameplayElementsRoot.transform, new Vector3(GameConstants.LANE_RIGHT_X, 2.3f, 165f), matCell);

            // Z = 190m: Viga Alta (Faixa Centro)
            CreateHighBeamHazard(gameplayElementsRoot.transform, GameConstants.LANE_CENTER_X, 190f, matHighBeam);

            // Z = 220m: Bloqueio Esquerda + Direita (Centro Aberto)
            CreateFullBlockHazard(gameplayElementsRoot.transform, GameConstants.LANE_LEFT_X, 220f, matBlocker, matLaserHurdle);
            CreateFullBlockHazard(gameplayElementsRoot.transform, GameConstants.LANE_RIGHT_X, 220f, matBlocker, matLaserHurdle);
            CreateEnergyCellSequence(gameplayElementsRoot.transform, GameConstants.LANE_CENTER_X, 215f, 4);

            // Z = 250m: Viga Alta Centro + Bloqueio Esquerda
            CreateHighBeamHazard(gameplayElementsRoot.transform, GameConstants.LANE_CENTER_X, 250f, matHighBeam);
            CreateFullBlockHazard(gameplayElementsRoot.transform, GameConstants.LANE_LEFT_X, 250f, matBlocker, matLaserHurdle);

            // Z = 280m: Laser Baixo Direita + Centro
            CreateLowLaserHurdle(gameplayElementsRoot.transform, GameConstants.LANE_RIGHT_X, 280f, matLaserHurdle);
            CreateLowLaserHurdle(gameplayElementsRoot.transform, GameConstants.LANE_CENTER_X, 280f, matLaserHurdle);
            CreateEnergyCellSequence(gameplayElementsRoot.transform, GameConstants.LANE_LEFT_X, 275f, 3);

            // Z = 310m: Viga Alta nas 3 Faixas (Deslize Obrigatório!)
            CreateHighBeamHazard(gameplayElementsRoot.transform, GameConstants.LANE_LEFT_X, 310f, matHighBeam);
            CreateHighBeamHazard(gameplayElementsRoot.transform, GameConstants.LANE_CENTER_X, 310f, matHighBeam);
            CreateHighBeamHazard(gameplayElementsRoot.transform, GameConstants.LANE_RIGHT_X, 310f, matHighBeam);

            // Z = 330m: Reta final com células de energia até o portal
            CreateEnergyCellSequence(gameplayElementsRoot.transform, GameConstants.LANE_CENTER_X, 330f, 5);

            // 7. Criar Portal de Extração em Z = 350m
            CreateExtractionPortalObject(gameplayElementsRoot.transform, 350f, matPortal, matLaneCyan);

            // 8. Criar Singletons de Sistema PRIMEIRO na hierarquia (_GameSystems: Input, Audio, GameManager)
            // Garante que NeonInputAdapter.Awake() e GameManager.Awake() rodem antes do PlayerController.OnEnable()
            var systemObj = new GameObject("_GameSystems");
            var inputAdapter = systemObj.AddComponent<NeonInputAdapter>();
            systemObj.AddComponent<ProceduralAudioService>();
            var gameMgr = systemObj.AddComponent<GameManager>();

            // 9. Criar o Droide N3-0N (Player)
            var playerObj = CreatePlayerRobot(matRobotBody, matRobotVisor, matRobotThruster);
            var playerMovement = playerObj.GetComponent<PlayerMovement>();
            gameMgr.SetPlayer(playerMovement);
            var gmSo = new SerializedObject(gameMgr);
            gmSo.FindProperty("_playerMovement").objectReferenceValue = playerMovement;
            gmSo.ApplyModifiedProperties();

            // 10. Criar Câmera de Acompanhamento
            var camObj = new GameObject("Main Camera");
            camObj.tag = "MainCamera";
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.03f, 0.09f);
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 500f;
            var camData = camObj.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camObj.AddComponent<AudioListener>();

            var followCam = camObj.AddComponent<SmoothFollowCamera>();
            followCam.SetTarget(playerObj.transform);
            var camSo = new SerializedObject(followCam);
            camSo.FindProperty("_target").objectReferenceValue = playerObj.transform;
            camSo.ApplyModifiedProperties();
            camObj.transform.position = new Vector3(0f, 3.2f, -6.5f);
            camObj.transform.rotation = Quaternion.Euler(18f, 0f, 0f);

            // Volume Global de Pós-Processamento URP (Bloom, ACES, Vinheta)
            var volumeObj = new GameObject("Global_PostProcessing_Volume");
            var volume = volumeObj.AddComponent<UnityEngine.Rendering.Volume>();
            volume.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<UnityEngine.Rendering.VolumeProfile>();
            if (!Directory.Exists("Assets/Game/Settings")) Directory.CreateDirectory("Assets/Game/Settings");
            AssetDatabase.CreateAsset(profile, "Assets/Game/Settings/NeonRush_VolumeProfile.asset");
            volume.profile = profile;

            var bloom = profile.Add<UnityEngine.Rendering.Universal.Bloom>(true);
            bloom.threshold.Override(0.85f);
            bloom.intensity.Override(1.6f);
            bloom.scatter.Override(0.7f);

            var tonemapping = profile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
            tonemapping.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.ACES);

            var vignette = profile.Add<UnityEngine.Rendering.Universal.Vignette>(true);
            vignette.intensity.Override(0.32f);
            vignette.smoothness.Override(0.45f);

            // 11. Criar UI Canvas & Telas
            CreateUICanvas(systemObj, gameMgr);

            // 12. Salvar Cena e Registrar nos Build Settings
            bool saved = EditorSceneManager.SaveScene(scene, scenePath);
            if (!saved)
            {
                Debug.LogError($"[NeonRushSceneBuilder] Não foi possível salvar a cena em: {scenePath}");
                return false;
            }

            RegisterSceneInBuildSettings(scenePath);

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        #region Helpers de Construção de Objetos

        private static Material GetOrCreateMaterial(string name, Color baseColor, Color emissionColor)
        {
            string path = $"{MAT_DIR}/{name}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                mat = new Material(shader);
                mat.SetColor("_BaseColor", baseColor);

                if (emissionColor != Color.black)
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emissionColor);
                }

                AssetDatabase.CreateAsset(mat, path);
            }
            return mat;
        }

        private static void CreateLaneStrip(string name, float x, Material mat, Transform parent, float height = 0.05f)
        {
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = name;
            strip.transform.SetParent(parent);
            strip.transform.position = new Vector3(x, height * 0.5f, 175f);
            strip.transform.localScale = new Vector3(0.08f, height, 360f);
            strip.GetComponent<Renderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(strip.GetComponent<Collider>());
        }

        private static void CreateBuilding(Transform parent, Vector3 pos, Vector3 scale, Material matBuilding, Material matWindow)
        {
            var bldg = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bldg.name = "Building";
            bldg.transform.SetParent(parent);
            bldg.transform.position = pos;
            bldg.transform.localScale = scale;
            bldg.GetComponent<Renderer>().sharedMaterial = matBuilding;
            UnityEngine.Object.DestroyImmediate(bldg.GetComponent<Collider>());

            // Faixa de janela luminosa
            var strip = GameObject.CreatePrimitive(PrimitiveType.Cube);
            strip.name = "Window_Band";
            strip.transform.SetParent(bldg.transform);
            strip.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            strip.transform.localScale = new Vector3(1.02f, 0.05f, 1.02f);
            strip.GetComponent<Renderer>().sharedMaterial = matWindow;
            UnityEngine.Object.DestroyImmediate(strip.GetComponent<Collider>());
        }

        private static void CreateNeoArch(Transform parent, float z, Material matStructure, Material matNeon)
        {
            var arch = new GameObject($"NeoArch_{z}");
            arch.transform.SetParent(parent);
            arch.transform.position = new Vector3(0f, 0f, z);

            // Pilar Esquerdo
            var pLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pLeft.transform.SetParent(arch.transform);
            pLeft.transform.localPosition = new Vector3(-4.5f, 4f, 0f);
            pLeft.transform.localScale = new Vector3(0.8f, 8f, 0.8f);
            pLeft.GetComponent<Renderer>().sharedMaterial = matStructure;

            // Pilar Direito
            var pRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pRight.transform.SetParent(arch.transform);
            pRight.transform.localPosition = new Vector3(4.5f, 4f, 0f);
            pRight.transform.localScale = new Vector3(0.8f, 8f, 0.8f);
            pRight.GetComponent<Renderer>().sharedMaterial = matStructure;

            // Viga Superior
            var topBeam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topBeam.transform.SetParent(arch.transform);
            topBeam.transform.localPosition = new Vector3(0f, 8f, 0f);
            topBeam.transform.localScale = new Vector3(9.8f, 0.8f, 0.8f);
            topBeam.GetComponent<Renderer>().sharedMaterial = matNeon;
        }

        private static void CreateLowLaserHurdle(Transform parent, float x, float z, Material mat)
        {
            var hurdle = new GameObject($"LaserHurdle_{z}_{x}");
            hurdle.transform.SetParent(parent);
            hurdle.transform.position = new Vector3(x, 0.35f, z);

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.transform.SetParent(hurdle.transform);
            beam.transform.localPosition = Vector3.zero;
            beam.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            beam.transform.localScale = new Vector3(0.2f, 1.0f, 0.2f);
            beam.GetComponent<Renderer>().sharedMaterial = mat;

            // Colisor de gatilho
            var col = hurdle.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2.0f, 0.7f, 0.5f);
            col.center = new Vector3(0f, 0f, 0f);

            var hazard = hurdle.AddComponent<ObstacleHazard>();
            hazard.SetHazardType(HazardType.LowLaser);
            hurdle.tag = GameConstants.TAG_OBSTACLE;
        }

        private static void CreateHighBeamHazard(Transform parent, float x, float z, Material mat)
        {
            var highHazard = new GameObject($"HighBeam_{z}_{x}");
            highHazard.transform.SetParent(parent);
            highHazard.transform.position = new Vector3(x, 1.8f, z);

            var beam = GameObject.CreatePrimitive(PrimitiveType.Cube);
            beam.transform.SetParent(highHazard.transform);
            beam.transform.localPosition = Vector3.zero;
            beam.transform.localScale = new Vector3(2.1f, 0.4f, 0.4f);
            beam.GetComponent<Renderer>().sharedMaterial = mat;

            var col = highHazard.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(2.1f, 0.8f, 0.5f);

            var hazard = highHazard.AddComponent<ObstacleHazard>();
            hazard.SetHazardType(HazardType.HighBeam);
            highHazard.tag = GameConstants.TAG_OBSTACLE;
        }

        private static void CreateFullBlockHazard(Transform parent, float x, float z, Material matBlock, Material matGlow)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = $"FullBlock_{z}_{x}";
            block.transform.SetParent(parent);
            block.transform.position = new Vector3(x, 1.25f, z);
            block.transform.localScale = new Vector3(2.0f, 2.5f, 1.0f);
            block.GetComponent<Renderer>().sharedMaterial = matBlock;

            // Faixa luminosa no topo
            var stripe = GameObject.CreatePrimitive(PrimitiveType.Cube);
            stripe.transform.SetParent(block.transform);
            stripe.transform.localPosition = new Vector3(0f, 0.45f, 0f);
            stripe.transform.localScale = new Vector3(1.05f, 0.15f, 1.05f);
            stripe.GetComponent<Renderer>().sharedMaterial = matGlow;
            UnityEngine.Object.DestroyImmediate(stripe.GetComponent<Collider>());

            var col = block.GetComponent<BoxCollider>();
            col.isTrigger = true;

            var hazard = block.AddComponent<ObstacleHazard>();
            hazard.SetHazardType(HazardType.FullBlock);
            block.tag = GameConstants.TAG_OBSTACLE;
        }

        private static void CreateEnergyCell(Transform parent, Vector3 pos, Material mat)
        {
            var cell = new GameObject($"EnergyCell_{pos.z}_{pos.x}");
            cell.transform.SetParent(parent);
            cell.transform.position = pos;

            // Octaedro estilizado usando duas pirâmides ou cubo rotacionado
            var core = GameObject.CreatePrimitive(PrimitiveType.Cube);
            core.transform.SetParent(cell.transform);
            core.transform.localPosition = Vector3.zero;
            core.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);
            core.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            core.GetComponent<Renderer>().sharedMaterial = mat;
            UnityEngine.Object.DestroyImmediate(core.GetComponent<Collider>());

            // Trigger
            var col = cell.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 0.6f;

            cell.AddComponent<EnergyCellCollectible>();
            cell.tag = GameConstants.TAG_COLLECTIBLE;
        }

        private static void CreateEnergyCellSequence(Transform parent, float x, float startZ, int count)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"{MAT_DIR}/Mat_EnergyCell.mat");
            for (int i = 0; i < count; i++)
            {
                CreateEnergyCell(parent, new Vector3(x, 0.8f, startZ + i * 3.5f), mat);
            }
        }

        private static void CreateExtractionPortalObject(Transform parent, float z, Material matPortal, Material matVortex)
        {
            var portalObj = new GameObject("Extraction_Portal");
            portalObj.transform.SetParent(parent);
            portalObj.transform.position = new Vector3(0f, 3.5f, z);
            portalObj.tag = GameConstants.TAG_PORTAL;

            // Anel Externo Hexagonal/Circular
            var outer = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            outer.name = "Portal_OuterRing";
            outer.transform.SetParent(portalObj.transform);
            outer.transform.localPosition = Vector3.zero;
            outer.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            outer.transform.localScale = new Vector3(8.5f, 0.3f, 8.5f);
            outer.GetComponent<Renderer>().sharedMaterial = matPortal;
            UnityEngine.Object.DestroyImmediate(outer.GetComponent<Collider>());

            // Vórtice Central
            var inner = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inner.name = "Portal_Vortex";
            inner.transform.SetParent(portalObj.transform);
            inner.transform.localPosition = Vector3.zero;
            inner.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            inner.transform.localScale = new Vector3(6.5f, 0.1f, 6.5f);
            inner.GetComponent<Renderer>().sharedMaterial = matVortex;
            UnityEngine.Object.DestroyImmediate(inner.GetComponent<Collider>());

            // Trigger de Conclusão
            var col = portalObj.AddComponent<BoxCollider>();
            col.isTrigger = true;
            col.size = new Vector3(8f, 7f, 2f);
            col.center = Vector3.zero;

            portalObj.AddComponent<ExtractionPortal>();
        }

        private static GameObject CreatePlayerRobot(Material matBody, Material matVisor, Material matThruster)
        {
            var player = new GameObject("N3-0N_Robot");
            player.tag = GameConstants.TAG_PLAYER;
            player.transform.position = Vector3.zero;

            // Colisor Físico
            var col = player.AddComponent<BoxCollider>();
            col.size = new Vector3(1.2f, 1.4f, 1.2f);
            col.center = new Vector3(0f, 0.7f, 0f);

            var rb = player.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;

            // Movimento e Controle
            var movement = player.AddComponent<PlayerMovement>();
            var controller = player.AddComponent<PlayerController>();

            // Visual Root (para animação, banking e posicionamento)
            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(player.transform);
            visualRoot.transform.localPosition = Vector3.zero; // Pés perfeitamente nivelados no chão Y = 0

            GameObject visorObj = null;
            GameObject thrusterL = null;
            GameObject thrusterR = null;
            Animator characterAnimator = null;

            // Prioridade de carregamento de modelo:
            // 1. FBX bípede riggado com esqueleto e animações
            // 2. OBJ normalizado em eixos canônicos
            // 3. OBJ bruto
            string aiFbxPath = "Assets/Game/Art/Characters/RobotAI/robot_character.fbx";
            string aiNormObjPath = "Assets/Game/Art/Characters/RobotAI/robot_normalized.obj";
            string aiRawObjPath = "Assets/Game/Art/Characters/RobotAI/robot_mesh.obj";

            string aiDiffusePath = "Assets/Game/Art/Characters/RobotAI/robot_diffuse.png";
            string aiEmissionPath = "Assets/Game/Art/Characters/RobotAI/robot_emission_mask.png";
            string aiSmoothnessPath = "Assets/Game/Art/Characters/RobotAI/robot_smoothness.png";
            string aiRawTexPath = "Assets/Game/Art/Characters/RobotAI/robot_texture.png";

            GameObject characterModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(aiFbxPath);
            if (characterModelPrefab == null) characterModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(aiNormObjPath);
            if (characterModelPrefab == null) characterModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(aiRawObjPath);

            var aiDiffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(aiDiffusePath) ?? AssetDatabase.LoadAssetAtPath<Texture2D>(aiRawTexPath);
            var aiEmission = AssetDatabase.LoadAssetAtPath<Texture2D>(aiEmissionPath);

            if (characterModelPrefab != null)
            {
                var aiRobot = UnityEngine.Object.Instantiate(characterModelPrefab, visualRoot.transform);
                aiRobot.name = "AI_Character_Model";
                aiRobot.transform.localPosition = Vector3.zero;
                aiRobot.transform.localRotation = Quaternion.identity;
                aiRobot.transform.localScale = Vector3.one;

                characterAnimator = aiRobot.GetComponentInChildren<Animator>();
                if (characterAnimator == null)
                {
                    characterAnimator = aiRobot.AddComponent<Animator>();
                }
                var animController = CharacterPipelineEditorHelper.SetupRobotAnimatorController();
                if (characterAnimator != null && animController != null)
                {
                    characterAnimator.runtimeAnimatorController = animController;
                    characterAnimator.applyRootMotion = false;
                    characterAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                }

                // Configurar Material URP Lit PBR com Máscara de Emissão
                // Cura o Problema D: Sem emissão global descontrolada; emissão aplicada apenas onde o mapa dita
                string matPath = $"{MAT_DIR}/Mat_RobotAI.mat";
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                var matAI = new Material(shader);
                matAI.name = "Mat_RobotAI";
                matAI.SetColor("_BaseColor", Color.white);
                if (aiDiffuse != null) matAI.SetTexture("_BaseMap", aiDiffuse);
                matAI.SetFloat("_Metallic", 0.55f);
                matAI.SetFloat("_Smoothness", 0.70f);

                if (aiEmission != null)
                {
                    matAI.EnableKeyword("_EMISSION");
                    matAI.SetKeyword(new UnityEngine.Rendering.LocalKeyword(shader, "_EMISSION"), true);
                    matAI.SetTexture("_EmissionMap", aiEmission);
                    matAI.SetColor("_EmissionColor", Color.white * 2.0f);
                    matAI.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    matAI.DisableKeyword("_EMISSION");
                    matAI.SetKeyword(new UnityEngine.Rendering.LocalKeyword(shader, "_EMISSION"), false);
                    matAI.SetColor("_EmissionColor", Color.black);
                }

                AssetDatabase.CreateAsset(matAI, matPath);
                EditorUtility.SetDirty(matAI);

                foreach (var r in aiRobot.GetComponentsInChildren<Renderer>())
                {
                    r.sharedMaterial = matAI;
                }
                foreach (var c in aiRobot.GetComponentsInChildren<Collider>())
                {
                    UnityEngine.Object.DestroyImmediate(c);
                }

                visorObj = aiRobot;
                thrusterL = null;
                thrusterR = null;
            }
            else
            {
                // Fallback para geometria básica procedural
                var chassis = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                chassis.name = "Chassis";
                chassis.transform.SetParent(visualRoot.transform);
                chassis.transform.localPosition = new Vector3(0f, 0.5f, 0f);
                chassis.transform.localScale = new Vector3(1.0f, 0.85f, 0.95f);
                chassis.GetComponent<Renderer>().sharedMaterial = matBody;
                UnityEngine.Object.DestroyImmediate(chassis.GetComponent<Collider>());

                visorObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                visorObj.name = "Sensor_Visor";
                visorObj.transform.SetParent(visualRoot.transform);
                visorObj.transform.localPosition = new Vector3(0f, 0.65f, 0.42f);
                visorObj.transform.localScale = new Vector3(0.55f, 0.2f, 0.2f);
                visorObj.GetComponent<Renderer>().sharedMaterial = matVisor;
                UnityEngine.Object.DestroyImmediate(visorObj.GetComponent<Collider>());

                thrusterL = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                thrusterL.name = "Thruster_Left";
                thrusterL.transform.SetParent(visualRoot.transform);
                thrusterL.transform.localPosition = new Vector3(-0.55f, 0.25f, -0.1f);
                thrusterL.transform.localScale = new Vector3(0.28f, 0.35f, 0.28f);
                thrusterL.GetComponent<Renderer>().sharedMaterial = matThruster;
                UnityEngine.Object.DestroyImmediate(thrusterL.GetComponent<Collider>());

                thrusterR = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                thrusterR.name = "Thruster_Right";
                thrusterR.transform.SetParent(visualRoot.transform);
                thrusterR.transform.localPosition = new Vector3(0.55f, 0.25f, -0.1f);
                thrusterR.transform.localScale = new Vector3(0.28f, 0.35f, 0.28f);
                thrusterR.GetComponent<Renderer>().sharedMaterial = matThruster;
                UnityEngine.Object.DestroyImmediate(thrusterR.GetComponent<Collider>());
            }

            // Animador
            var anim = player.AddComponent<RobotAnimator>();
            anim.Initialize(visualRoot.transform, characterAnimator, isHoverDrone: false, baseY: 0.0f);
            var so = new SerializedObject(anim);
            so.FindProperty("_visualRoot").objectReferenceValue = visualRoot.transform;
            if (characterAnimator != null) so.FindProperty("_animator").objectReferenceValue = characterAnimator;
            if (thrusterL != null) so.FindProperty("_leftThruster").objectReferenceValue = thrusterL.transform;
            if (thrusterR != null) so.FindProperty("_rightThruster").objectReferenceValue = thrusterR.transform;
            if (visorObj != null) so.FindProperty("_sensorVisor").objectReferenceValue = visorObj.transform;
            so.ApplyModifiedProperties();

            var movSo = new SerializedObject(movement);
            movSo.FindProperty("_boxCollider").objectReferenceValue = col;
            movSo.ApplyModifiedProperties();

            var ctrlSo = new SerializedObject(controller);
            ctrlSo.FindProperty("_movement").objectReferenceValue = movement;
            ctrlSo.FindProperty("_animator").objectReferenceValue = anim;
            ctrlSo.ApplyModifiedProperties();

            return player;
        }

        private static void CreateUICanvas(GameObject systemObj, GameManager gameMgr)
        {
            var canvasObj = new GameObject("UICanvas");
            var canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObj.AddComponent<GraphicRaycaster>();

            // EventSystem com suporte nativo ao novo Input System
            if (UnityEngine.Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var eventObj = new GameObject("EventSystem");
                eventObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            var uiMgr = canvasObj.AddComponent<UIManager>();

            // --- 1. HUD Panel ---
            var hudPanel = CreatePanel(canvasObj.transform, "HUD_Panel", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, 0f), new Vector2(1080f, 250f));
            var cellsText = CreateText(hudPanel.transform, "CellsText", "CELLS: 0", 42, TextAnchor.MiddleLeft, new Vector2(40f, -60f));
            var scoreText = CreateText(hudPanel.transform, "ScoreText", "SCORE: 0", 42, TextAnchor.MiddleRight, new Vector2(-40f, -60f));

            // Slider de Progresso
            var sliderObj = new GameObject("ProgressSlider");
            sliderObj.transform.SetParent(hudPanel.transform, false);
            var sliderRect = sliderObj.AddComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0.5f, 1f);
            sliderRect.anchorMax = new Vector2(0.5f, 1f);
            sliderRect.anchoredPosition = new Vector2(0f, -120f);
            sliderRect.sizeDelta = new Vector2(600f, 24f);

            var slider = sliderObj.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;

            var bgImg = sliderObj.AddComponent<Image>();
            bgImg.color = new Color(0.1f, 0.1f, 0.2f, 0.8f);

            var fillArea = new GameObject("FillArea");
            fillArea.transform.SetParent(sliderObj.transform, false);
            var fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fillRect = fill.AddComponent<RectTransform>();
            fillRect.sizeDelta = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.color = new Color(0f, 0.9f, 1f);
            slider.fillRect = fillRect;

            // --- 2. Start Panel ---
            var startPanel = CreateCenterPanel(canvasObj.transform, "Start_Panel", new Vector2(900f, 1100f), new Color(0.04f, 0.04f, 0.09f, 0.92f));
            CreateText(startPanel.transform, "Title", "NEON RUSH", 72, TextAnchor.MiddleCenter, new Vector2(0f, 380f), new Color(0f, 1f, 1f));
            CreateText(startPanel.transform, "SubTitle", "SECTOR 7 COLLAPSE — EXTRACTION RUN", 28, TextAnchor.MiddleCenter, new Vector2(0f, 310f), new Color(0.8f, 0.4f, 1f));
            CreateText(startPanel.transform, "Instructions", "CONTROLS:\n• SWIPE LEFT/RIGHT or A/D to Change Lanes\n• SWIPE UP or W/SPACE to Jump Hurdles\n• SWIPE DOWN or S to Slide Under Beams\n• Collect Energy Cells & Reach the Portal!", 32, TextAnchor.MiddleCenter, new Vector2(0f, 80f), Color.white);
            var startBtn = CreateButton(startPanel.transform, "StartButton", "START MISSION", new Vector2(0f, -320f), new Vector2(480f, 110f), new Color(0f, 0.7f, 0.9f));

            // --- 3. GameOver Panel ---
            var gameOverPanel = CreateCenterPanel(canvasObj.transform, "GameOver_Panel", new Vector2(900f, 900f), new Color(0.12f, 0.02f, 0.04f, 0.94f));
            CreateText(gameOverPanel.transform, "Title", "CRITICAL OVERLOAD", 68, TextAnchor.MiddleCenter, new Vector2(0f, 280f), new Color(1f, 0.1f, 0.3f));
            var goScoreText = CreateText(gameOverPanel.transform, "Score", "SCORE: 0", 42, TextAnchor.MiddleCenter, new Vector2(0f, 120f), Color.white);
            var goCellsText = CreateText(gameOverPanel.transform, "Cells", "CELLS: 0", 38, TextAnchor.MiddleCenter, new Vector2(0f, 30f), new Color(0f, 1f, 1f));
            var retryBtn = CreateButton(gameOverPanel.transform, "RetryButton", "RETRY MISSION", new Vector2(0f, -220f), new Vector2(480f, 110f), new Color(0.9f, 0.2f, 0.3f));

            // --- 4. Victory Panel ---
            var victoryPanel = CreateCenterPanel(canvasObj.transform, "Victory_Panel", new Vector2(900f, 900f), new Color(0.02f, 0.08f, 0.12f, 0.94f));
            CreateText(victoryPanel.transform, "Title", "EXTRACTION COMPLETE!", 64, TextAnchor.MiddleCenter, new Vector2(0f, 280f), new Color(0.2f, 1f, 0.5f));
            var vicScoreText = CreateText(victoryPanel.transform, "Score", "FINAL SCORE: 0", 42, TextAnchor.MiddleCenter, new Vector2(0f, 120f), Color.white);
            var vicCellsText = CreateText(victoryPanel.transform, "Cells", "ENERGY CELLS: 0", 38, TextAnchor.MiddleCenter, new Vector2(0f, 30f), new Color(0f, 1f, 1f));
            var vicBtn = CreateButton(victoryPanel.transform, "VictoryRestartButton", "PLAY AGAIN", new Vector2(0f, -220f), new Vector2(480f, 110f), new Color(0.1f, 0.8f, 0.5f));

            // --- 5. Mobile On-Screen Buttons (Bottom) ---
            var mobileControls = CreatePanel(canvasObj.transform, "MobileControls", new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 120f), new Vector2(1080f, 200f));
            var btnLeft = CreateButton(mobileControls.transform, "BtnLeft", "◄ LEFT", new Vector2(-360f, 0f), new Vector2(200f, 130f), new Color(0.2f, 0.2f, 0.35f, 0.6f));
            var btnRight = CreateButton(mobileControls.transform, "BtnRight", "RIGHT ►", new Vector2(-120f, 0f), new Vector2(200f, 130f), new Color(0.2f, 0.2f, 0.35f, 0.6f));
            var btnJump = CreateButton(mobileControls.transform, "BtnJump", "▲ JUMP", new Vector2(140f, 0f), new Vector2(200f, 130f), new Color(0f, 0.6f, 0.8f, 0.7f));
            var btnSlide = CreateButton(mobileControls.transform, "BtnSlide", "▼ SLIDE", new Vector2(380f, 0f), new Vector2(200f, 130f), new Color(0.8f, 0.4f, 0.1f, 0.7f));

            // Injetar referências no UIManager via SerializedObject
            var so = new SerializedObject(uiMgr);
            so.FindProperty("_startPanel").objectReferenceValue = startPanel;
            so.FindProperty("_hudPanel").objectReferenceValue = hudPanel;
            so.FindProperty("_gameOverPanel").objectReferenceValue = gameOverPanel;
            so.FindProperty("_victoryPanel").objectReferenceValue = victoryPanel;

            so.FindProperty("_cellsText").objectReferenceValue = cellsText;
            so.FindProperty("_scoreText").objectReferenceValue = scoreText;
            so.FindProperty("_progressBar").objectReferenceValue = slider;

            so.FindProperty("_gameOverScoreText").objectReferenceValue = goScoreText;
            so.FindProperty("_gameOverCellsText").objectReferenceValue = goCellsText;

            so.FindProperty("_victoryScoreText").objectReferenceValue = vicScoreText;
            so.FindProperty("_victoryCellsText").objectReferenceValue = vicCellsText;

            so.FindProperty("_startButton").objectReferenceValue = startBtn;
            so.FindProperty("_retryButton").objectReferenceValue = retryBtn;
            so.FindProperty("_victoryRestartButton").objectReferenceValue = vicBtn;

            so.FindProperty("_btnLeft").objectReferenceValue = btnLeft;
            so.FindProperty("_btnRight").objectReferenceValue = btnRight;
            so.FindProperty("_btnJump").objectReferenceValue = btnJump;
            so.FindProperty("_btnSlide").objectReferenceValue = btnSlide;

            so.ApplyModifiedProperties();
        }

        private static GameObject CreatePanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var p = new GameObject(name);
            p.transform.SetParent(parent, false);
            var rect = p.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            return p;
        }

        private static GameObject CreateCenterPanel(Transform parent, string name, Vector2 size, Color color)
        {
            var p = new GameObject(name);
            p.transform.SetParent(parent, false);
            var rect = p.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = size;

            var img = p.AddComponent<Image>();
            img.color = color;
            return p;
        }

        private static Text CreateText(Transform parent, string name, string text, int fontSize, TextAnchor alignment, Vector2 pos, Color? color = null)
        {
            var tObj = new GameObject(name);
            tObj.transform.SetParent(parent, false);
            var rect = tObj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(850f, fontSize * 3f);

            var t = tObj.AddComponent<Text>();
            t.text = text;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.fontSize = fontSize;
            t.alignment = alignment;
            t.color = color ?? Color.white;
            return t;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 pos, Vector2 size, Color color)
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

            var btn = bObj.AddComponent<Button>();

            var t = CreateText(bObj.transform, "Label", label, 36, TextAnchor.MiddleCenter, Vector2.zero, Color.white);
            t.rectTransform.sizeDelta = size;

            return btn;
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
                newScenes[0] = new EditorBuildSettingsScene(scenePath, true); // NeonRush como cena 0 principal
                for (int i = 0; i < currentScenes.Length; i++)
                {
                    newScenes[i + 1] = currentScenes[i];
                }
                EditorBuildSettings.scenes = newScenes;
                Debug.Log($"[NeonRushSceneBuilder] Cena registrada como Scene[0] no BuildSettings: {scenePath}");
            }
        }

        #endregion
    }
}
