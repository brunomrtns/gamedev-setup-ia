using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Studio.Editor
{
    public static class CharacterPipelineEditorHelper
    {
        public const string FBX_PATH = "Assets/Game/Art/Characters/RobotAI/robot_character.fbx";
        public const string CONTROLLER_PATH = "Assets/Game/Art/Characters/RobotAI/Robot_AnimatorController.controller";

        [MenuItem("Studio/Art Pipeline/Setup Robot Character Animator")]
        public static void SetupRobotCharacterMenu()
        {
            var controller = SetupRobotAnimatorController();
            if (controller != null)
            {
                Debug.Log($"[CharacterPipelineEditorHelper] AnimatorController configurado com sucesso em: {CONTROLLER_PATH}");
            }
        }

        public static RuntimeAnimatorController SetupRobotAnimatorController()
        {
            if (!File.Exists(FBX_PATH))
            {
                Debug.LogWarning($"[CharacterPipelineEditorHelper] FBX não encontrado em: {FBX_PATH}");
                return null;
            }

            // 1. Configurar ModelImporter para loop e nomes limpos de animação
            ConfigureFbxModelImporter(FBX_PATH);

            // 2. Extrair os AnimationClips do FBX
            AnimationClip runClip = null;
            AnimationClip jumpClip = null;
            AnimationClip slideClip = null;

            var allSubAssets = AssetDatabase.LoadAllAssetsAtPath(FBX_PATH);
            foreach (var asset in allSubAssets)
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    string cName = clip.name.ToLower();
                    if (cName.Contains("run")) runClip = clip;
                    else if (cName.Contains("jump")) jumpClip = clip;
                    else if (cName.Contains("slide")) slideClip = clip;
                }
            }

            Debug.Log($"[CharacterPipelineEditorHelper] Clips detectados no FBX - Run: {runClip?.name}, Jump: {jumpClip?.name}, Slide: {slideClip?.name}");

            // 3. Criar ou Obter AnimatorController
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(CONTROLLER_PATH);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(CONTROLLER_PATH);
            }

            // Parâmetros de controle
            EnsureParameter(controller, "IsJumping", AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, "IsSliding", AnimatorControllerParameterType.Bool);

            var stateMachine = controller.layers[0].stateMachine;

            // Limpar estados existentes para recriar de forma limpa
            foreach (var state in stateMachine.states)
            {
                stateMachine.RemoveState(state.state);
            }

            // Criar Estados
            var runState = stateMachine.AddState("RunCycle");
            runState.motion = runClip;

            var jumpState = stateMachine.AddState("Jump");
            jumpState.motion = jumpClip;

            var slideState = stateMachine.AddState("Slide");
            slideState.motion = slideClip;

            stateMachine.defaultState = runState;

            // Transições
            // Run -> Jump
            var runToJump = runState.AddTransition(jumpState);
            runToJump.hasExitTime = false;
            runToJump.duration = 0.08f;
            runToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");

            // Jump -> Run
            var jumpToRun = jumpState.AddTransition(runState);
            jumpToRun.hasExitTime = false;
            jumpToRun.duration = 0.12f;
            jumpToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsJumping");

            // Run -> Slide
            var runToSlide = runState.AddTransition(slideState);
            runToSlide.hasExitTime = false;
            runToSlide.duration = 0.08f;
            runToSlide.AddCondition(AnimatorConditionMode.If, 0, "IsSliding");

            // Slide -> Run
            var slideToRun = slideState.AddTransition(runState);
            slideToRun.hasExitTime = false;
            slideToRun.duration = 0.12f;
            slideToRun.AddCondition(AnimatorConditionMode.IfNot, 0, "IsSliding");

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            return controller;
        }

        private static void ConfigureFbxModelImporter(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null) return;

            bool changed = false;

            if (importer.animationType != ModelImporterAnimationType.Generic && importer.animationType != ModelImporterAnimationType.Human)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                changed = true;
            }

            var clips = importer.defaultClipAnimations;
            if (clips != null && clips.Length > 0)
            {
                foreach (var c in clips)
                {
                    if (c.name.Contains("Run") || c.name.Contains("RunCycle"))
                    {
                        if (!c.loopTime)
                        {
                            c.loopTime = true;
                            changed = true;
                        }
                    }
                }
                if (changed)
                {
                    importer.clipAnimations = clips;
                }
            }

            if (changed)
            {
                importer.SaveAndReimport();
            }
        }

        private static void EnsureParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
        {
            foreach (var p in controller.parameters)
            {
                if (p.name == paramName) return;
            }
            controller.AddParameter(paramName, type);
        }
    }
}
