using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Studio.Editor
{
    public static class StudioAutomation
    {
        [MenuItem("Studio/Scenes/Create Calibration Scene")]
        public static void CreateCalibrationSceneMenu()
        {
            CreateCalibrationScene("Assets/Game/Scenes/Calibration.unity");
        }

        public static void RunSceneCreationBatch()
        {
            Debug.Log("[StudioAutomation] Executando criação de cena em batchmode...");
            string scenePath = "Assets/Game/Scenes/Calibration.unity";
            
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-scenePath="))
                {
                    scenePath = args[i].Substring("-scenePath=".Length);
                }
            }

            bool success = CreateCalibrationScene(scenePath);
            if (success)
            {
                Debug.Log($"[StudioAutomation] Cena criada com sucesso em: {scenePath}");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[StudioAutomation] Falha ao criar a cena!");
                EditorApplication.Exit(1);
            }
        }

        public static bool CreateCalibrationScene(string scenePath)
        {
            string dir = Path.GetDirectoryName(scenePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // 1. Criar nova cena limpa
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 2. Criar Câmera Principal
            var cameraObj = new GameObject("Main Camera");
            cameraObj.tag = "MainCamera";
            var camera = cameraObj.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.fieldOfView = 60f;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 1000f;
            cameraObj.AddComponent<UniversalAdditionalCameraData>();
            cameraObj.AddComponent<AudioListener>();
            cameraObj.transform.position = new Vector3(0, 3, -7);
            cameraObj.transform.rotation = Quaternion.Euler(20, 0, 0);

            // 3. Criar Luz Direcional (Sol URP)
            var lightObj = new GameObject("Directional Light");
            var light = lightObj.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.0f, 0.95f, 0.85f);
            light.intensity = 1.2f;
            light.shadows = LightShadows.Soft;
            var lightData = lightObj.AddComponent<UniversalAdditionalLightData>();
            lightObj.transform.rotation = Quaternion.Euler(50, -30, 0);

            var matCalib = AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Materials/Modular/Mat_PBR_ConcretePanel.mat");

            // 4. Criar Chão (Ground) — malha procedural V2 (sem primitivas nativas)
            var ground = ProceduralMeshLibrary.CreatePart(
                "Ground",
                ProceduralMeshLibrary.Plane,
                null,
                Vector3.zero,
                Quaternion.identity,
                new Vector3(5f, 1f, 5f),
                matCalib);
            ground.AddComponent<MeshCollider>();

            // 5. Criar Esfera de Calibração (Calibration Target)
            var sphere = ProceduralMeshLibrary.CreatePart(
                "Calibration_Sphere",
                ProceduralMeshLibrary.Sphere,
                null,
                new Vector3(0f, 1.0f, 0f),
                Quaternion.identity,
                Vector3.one * 1.5f,
                matCalib);
            sphere.AddComponent<SphereCollider>();

            // 6. Criar Cubo de Referência
            var cube = ProceduralMeshLibrary.CreatePart(
                "Reference_Cube",
                ProceduralMeshLibrary.Box,
                null,
                new Vector3(2.5f, 0.5f, 0f),
                Quaternion.identity,
                Vector3.one,
                matCalib);
            cube.AddComponent<BoxCollider>();

            // 7. Salvar Cena
            bool saved = EditorSceneManager.SaveScene(scene, scenePath);
            if (!saved)
            {
                Debug.LogError($"[StudioAutomation] Não foi possível salvar a cena em {scenePath}");
                return false;
            }

            // 8. Registrar cena nos Build Settings
            var currentScenes = EditorBuildSettings.scenes;
            bool alreadyInBuild = false;
            foreach (var s in currentScenes)
            {
                if (string.Equals(s.path, scenePath, StringComparison.OrdinalIgnoreCase))
                {
                    alreadyInBuild = true;
                    break;
                }
            }

            if (!alreadyInBuild)
            {
                var newScenes = new EditorBuildSettingsScene[currentScenes.Length + 1];
                Array.Copy(currentScenes, newScenes, currentScenes.Length);
                newScenes[currentScenes.Length] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = newScenes;
                Debug.Log($"[StudioAutomation] Cena adicionada ao BuildSettings: {scenePath}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return true;
        }

        public static void CaptureScreenshotBatch()
        {
            string outPath = null;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-screenshotPath="))
                {
                    outPath = args[i].Substring("-screenshotPath=".Length);
                }
            }

            if (string.IsNullOrEmpty(outPath))
            {
                outPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures", "calibration_view.png");
            }

            string scenePath = "Assets/Game/Scenes/Calibration.unity";
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-scenePath="))
                {
                    scenePath = args[i].Substring("-scenePath=".Length);
                }
            }

            if (File.Exists(scenePath))
            {
                EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            }

            string dir = Path.GetDirectoryName(outPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Renderizar câmera ativa para RenderTexture e salvar PNG
            var cam = Camera.main;
            if (cam == null)
            {
                cam = UnityEngine.Object.FindAnyObjectByType<Camera>();
            }

            if (cam != null)
            {
                // Warm-up: força compilação de shaders/variantes URP antes da captura
                // real (sem isso o primeiro frame batchmode renderiza em magenta).
                for (int w = 0; w < 3; w++)
                {
                    var wrt = RenderTexture.GetTemporary(256, 144, 24);
                    cam.targetTexture = wrt;
                    cam.Render();
                    cam.targetTexture = null;
                    RenderTexture.ReleaseTemporary(wrt);
                }

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

                File.WriteAllBytes(outPath, bytes);
                Debug.Log($"[StudioAutomation] Screenshot salvo com sucesso em: {outPath}");
                EditorApplication.Exit(0);
            }
            else
            {
                Debug.LogError("[StudioAutomation] Nenhuma câmera encontrada para renderizar screenshot!");
                EditorApplication.Exit(1);
            }
        }
    }
}
