using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Studio.Editor
{
    public static class BuildPipelineHelper
    {
        [MenuItem("Studio/Build/Windows Standalone - Neon Rush (.exe)")]
        public static void BuildWindowsMenu()
        {
            string buildPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "builds", "NeonRush_Windows", "NeonRush.exe");
            BuildWindows(buildPath, "Assets/Game/Scenes/NeonRush.unity");
        }

        [MenuItem("Studio/Build/Windows Standalone - Abyssal Breach (.exe)")]
        public static void BuildAbyssalBreachWindowsMenu()
        {
            string buildPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "builds", "AbyssalBreach_Windows", "AbyssalBreach.exe");
            BuildWindows(buildPath, "Assets/Game/Scenes/AbyssalBreach.unity");
        }

        public static void BuildWindowsCommandLine()
        {
            Debug.Log("[StudioBuild] Compilando executável Windows (.exe)...");
            string buildPath = null;
            string scenePath = null;
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-customBuildPath="))
                {
                    buildPath = args[i].Substring("-customBuildPath=".Length);
                }
                else if (args[i].StartsWith("-scenePath="))
                {
                    scenePath = args[i].Substring("-scenePath=".Length);
                }
            }

            if (string.IsNullOrEmpty(buildPath))
            {
                buildPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "builds", "AbyssalBreach_Windows", "AbyssalBreach.exe");
            }

            bool success = BuildWindows(buildPath, scenePath);
            EditorApplication.Exit(success ? 0 : 1);
        }

        public static bool BuildWindows(string outputPath, string explicitScene = null)
        {
            string dir = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string probe = (outputPath ?? "") + "|" + (explicitScene ?? "");
            string product, defaultScene;
            if (probe.IndexOf("Abyssal", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                product = "AbyssalBreach";
                defaultScene = "Assets/Game/Scenes/AbyssalBreach.unity";
            }
            else if (probe.IndexOf("NeonDistrict", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                product = "NeonDistrict";
                defaultScene = "Assets/Game/Scenes/NeonDistrict.unity";
            }
            else
            {
                product = "NeonRush";
                defaultScene = "Assets/Game/Scenes/NeonRush.unity";
            }

            PlayerSettings.companyName = "StudioAI";
            PlayerSettings.productName = product;

            string targetScene = string.IsNullOrEmpty(explicitScene) ? defaultScene : explicitScene;

            string[] scenes = new[] { targetScene };
            Debug.Log($"[StudioBuild] Compilando Windows64: Produto={PlayerSettings.productName}, Cena={targetScene}, Destino={outputPath}");

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            return report.summary.result == BuildResult.Succeeded;
        }

        /// <summary>Build headless Linux Dedicated Server (VPS neon-district).</summary>
        public static void BuildLinuxServerCommandLine()
        {
            string buildPath = null;
            foreach (var a in Environment.GetCommandLineArgs())
                if (a.StartsWith("-customBuildPath="))
                    buildPath = a.Substring("-customBuildPath=".Length);
            if (string.IsNullOrEmpty(buildPath))
                buildPath = Path.Combine(Directory.GetCurrentDirectory(), "..",
                    "builds", "NeonDistrict_Server", "NeonDistrictServer");

            string dir = Path.GetDirectoryName(buildPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            PlayerSettings.companyName = "StudioAI";
            PlayerSettings.productName = "NeonDistrictServer";
            EditorUserBuildSettings.standaloneBuildSubtarget =
                StandaloneBuildSubtarget.Server; // headless dedicado

            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Game/Scenes/NeonDistrict.unity" },
                locationPathName = buildPath,
                target = BuildTarget.StandaloneLinux64,
                subtarget = (int)StandaloneBuildSubtarget.Server,
                options = BuildOptions.None
            };
            Debug.Log($"[StudioBuild] Linux Dedicated Server → {buildPath}");
            var report = BuildPipeline.BuildPlayer(opts);
            EditorUserBuildSettings.standaloneBuildSubtarget =
                StandaloneBuildSubtarget.Player;
            EditorApplication.Exit(
                report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        [MenuItem("Studio/Build/Android Development APK")]
        public static void BuildAndroidDevMenu()
        {
            string buildPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "builds", "game_dev_menu.apk");
            BuildAndroid(buildPath, isDevelopment: true);
        }

        public static void BuildAndroidCommandLine()
        {
            Debug.Log("[StudioBuild] Iniciando compilação Android via linha de comando...");

            string customPath = null;
            string productName = null;
            bool isDevelopment = true;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-customBuildPath="))
                {
                    customPath = args[i].Substring("-customBuildPath=".Length);
                }
                else if (args[i].StartsWith("-customBuildTarget="))
                {
                    string target = args[i].Substring("-customBuildTarget=".Length);
                    isDevelopment = !string.Equals(target, "release", StringComparison.OrdinalIgnoreCase);
                }
                else if (args[i].StartsWith("-productName="))
                {
                    productName = args[i].Substring("-productName=".Length);
                }
            }

            if (string.IsNullOrEmpty(customPath))
            {
                customPath = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "game.apk");
            }

            string dir = Path.GetDirectoryName(customPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            bool success = BuildAndroid(customPath, isDevelopment, productName);
            if (!success)
            {
                EditorApplication.Exit(1);
            }
            else
            {
                EditorApplication.Exit(0);
            }
        }

        public static bool BuildAndroid(string outputPath, bool isDevelopment, string productName = null)
        {
            Debug.Log($"[StudioBuild] Destino do APK: {outputPath}");
            Debug.Log($"[StudioBuild] Modo de desenvolvimento: {isDevelopment}");

            // 1. Configurar PlayerSettings para Android
            EditorUserBuildSettings.buildAppBundle = false; // Gerar APK diretamente
            EditorUserBuildSettings.androidBuildSubtarget = MobileTextureSubtarget.Generic;
            EditorUserBuildSettings.development = isDevelopment;
            EditorUserBuildSettings.connectProfiler = isDevelopment;

            PlayerSettings.companyName = "StudioAI";
            PlayerSettings.productName = string.IsNullOrEmpty(productName) ? "NeonRush" : productName;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android,
                string.IsNullOrEmpty(productName) ? "com.studio.neonrush" : $"com.studio.{productName.Replace(" ", "").ToLowerInvariant()}");
            
            // Requisitos Android modernos
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel25;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel34;
            
            // Para build rápido sem depender de compilação C++ pesada inicial, ARM64 / Mono
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // 2. Coletar cenas
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled && !string.IsNullOrEmpty(s.path) && File.Exists(s.path))
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                // Se nenhuma cena estiver cadastrada no Build Settings, procurar a primeira cena válida
                string[] foundScenes = Directory.GetFiles("Assets", "*.unity", SearchOption.AllDirectories);
                if (foundScenes.Length > 0)
                {
                    scenes = new[] { foundScenes[0].Replace("\\", "/") };
                    Debug.Log($"[StudioBuild] Nenhuma cena ativa no BuildSettings. Usando cena encontrada: {scenes[0]}");
                }
                else
                {
                    Debug.LogError("[StudioBuild] ERRO: Nenhuma cena encontrada no projeto para compilar!");
                    return false;
                }
            }

            Debug.Log($"[StudioBuild] Cenas no build: {string.Join(", ", scenes)}");

            // 3. Executar BuildPlayer
            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = isDevelopment ? (BuildOptions.Development | BuildOptions.AllowDebugging) : BuildOptions.None
            };

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"[StudioBuild] SUCESSO! APK gerado com {summary.totalSize / (1024 * 1024)} MB em {summary.totalTime.TotalSeconds:F1}s.");
                return true;
            }
            else
            {
                Debug.LogError($"[StudioBuild] FALHA na compilação do APK! Status: {summary.result}, Erros: {summary.totalErrors}");
                foreach (var step in report.steps)
                {
                    foreach (var msg in step.messages)
                    {
                        if (msg.type == LogType.Error || msg.type == LogType.Exception)
                        {
                            Debug.LogError($"[StudioBuild Error] {msg.content}");
                        }
                    }
                }
                return false;
            }
        }
    }
}
