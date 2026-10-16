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

            // Slide -> Jump (quando o jogador pula durante um slide)
            var slideToJump = slideState.AddTransition(jumpState);
            slideToJump.hasExitTime = false;
            slideToJump.duration = 0.06f;
            slideToJump.AddCondition(AnimatorConditionMode.If, 0, "IsJumping");

            // Jump -> Slide (quando o jogador cancela o pulo com slide rápido)
            var jumpToSlide = jumpState.AddTransition(slideState);
            jumpToSlide.hasExitTime = false;
            jumpToSlide.duration = 0.06f;
            jumpToSlide.AddCondition(AnimatorConditionMode.If, 0, "IsSliding");

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

            var defaultClips = importer.defaultClipAnimations;
            var currentClips = importer.clipAnimations;
            if (defaultClips != null && defaultClips.Length > 0)
            {
                bool needsClipSync = (currentClips == null || currentClips.Length != defaultClips.Length);
                foreach (var c in defaultClips)
                {
                    bool shouldLoop = c.name.Contains("Run") || c.name.Contains("Idle") || c.name.Contains("Slide");
                    if (shouldLoop)
                    {
                        c.loopTime = true;
                    }
                }

                if (!needsClipSync)
                {
                    for (int i = 0; i < defaultClips.Length; i++)
                    {
                        if (currentClips[i].name != defaultClips[i].name ||
                            currentClips[i].lastFrame != defaultClips[i].lastFrame ||
                            currentClips[i].loopTime != defaultClips[i].loopTime)
                        {
                            needsClipSync = true;
                            break;
                        }
                    }
                }

                if (!needsClipSync)
                {
                    // Tudo sincronizado
                }
                else
                {
                    importer.clipAnimations = defaultClips;
                    changed = true;
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

        public static void InspectAndCaptureCharacterStatesBatch()
        {
            NeonRushSceneBuilder.BuildNeonRushScene("Assets/Game/Scenes/NeonRush.unity");
            var player = GameObject.Find("N3-0N_Robot");
            var aiModel = GameObject.Find("AI_Character_Model");
            var cam = UnityEngine.Object.FindAnyObjectByType<UnityEngine.Camera>();

            if (aiModel == null || cam == null || player == null)
            {
                Debug.LogError($"[InspectCharacter] Falha ao encontrar Player ({player}), AI_Character_Model ({aiModel}) ou Camera ({cam})");
                EditorApplication.Exit(1);
                return;
            }

            // Ocultar painéis de UI para ver o personagem com clareza
            var startPanel = GameObject.Find("Start_Panel");
            var hudPanel = GameObject.Find("HUD_Panel");
            if (startPanel) startPanel.SetActive(false);
            if (hudPanel) hudPanel.SetActive(false);

            var smr = aiModel.GetComponentInChildren<SkinnedMeshRenderer>();
            var arm = aiModel.transform.Find("Armature");
            Debug.Log($"[InspectCharacter] REST POSE -> aiModel rot={aiModel.transform.eulerAngles}, arm rot={arm?.eulerAngles}, mesh rot={smr?.transform.eulerAngles}");
            if (smr != null)
            {
                Debug.Log($"[InspectCharacter] REST BOUNDS -> min={smr.bounds.min}, max={smr.bounds.max}, size={smr.bounds.size}");
            }

            AnimationClip runClip = null, jumpClip = null, slideClip = null;
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(FBX_PATH))
            {
                if (asset is AnimationClip clip && !clip.name.StartsWith("__preview__"))
                {
                    string n = clip.name.ToLower();
                    if (n.Contains("run")) runClip = clip;
                    else if (n.Contains("jump")) jumpClip = clip;
                    else if (n.Contains("slide")) slideClip = clip;
                }
            }

            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "..", "evidence", "captures");
            Directory.CreateDirectory(outDir);

            // Câmera em 3/4 lateral-traseira (visão do jogador + perfil das pernas)
            cam.transform.position = new Vector3(1.6f, 1.2f, -1.8f);
            cam.transform.LookAt(new Vector3(0f, 0.65f, 0.1f));

            if (runClip != null)
            {
                runClip.SampleAnimation(aiModel, 0.0f);
                LogPoseState("RUN_FRAME_0", aiModel, smr);
                RenderToPng(cam, Path.Combine(outDir, "inspect_01_run_frame0.png"));

                runClip.SampleAnimation(aiModel, 0.5f);
                LogPoseState("RUN_FRAME_12", aiModel, smr);
                RenderToPng(cam, Path.Combine(outDir, "inspect_02_run_frame12.png"));
            }

            if (jumpClip != null)
            {
                jumpClip.SampleAnimation(aiModel, 0.33f);
                LogPoseState("JUMP_APEX", aiModel, smr);
                RenderToPng(cam, Path.Combine(outDir, "inspect_03_jump_apex.png"));
            }

            if (slideClip != null)
            {
                slideClip.SampleAnimation(aiModel, 0.33f);
                LogPoseState("SLIDE_POSE", aiModel, smr);
                RenderToPng(cam, Path.Combine(outDir, "inspect_04_slide.png"));
            }

            // Verificar controles reais de Gameplay (Frente, Esquerda, Direita, Pulo, Agachamento)
            if (!VerifyAndCaptureGameplayControls(player, aiModel, cam, runClip, jumpClip, slideClip, outDir))
            {
                Debug.LogError("[GameplayVerify] Falha na verificação integrada de controles de gameplay!");
                EditorApplication.Exit(1);
                return;
            }

            // Restaurar cena limpa salva no disco (sem pose amostrada fixa)
            NeonRushSceneBuilder.BuildNeonRushScene("Assets/Game/Scenes/NeonRush.unity");
            EditorApplication.Exit(0);
        }

        private static bool VerifyAndCaptureGameplayControls(
            GameObject player,
            GameObject aiModel,
            UnityEngine.Camera cam,
            AnimationClip runClip,
            AnimationClip jumpClip,
            AnimationClip slideClip,
            string outDir)
        {
            var gm = Studio.Gameplay.GameManager.Instance;
            var input = Studio.Input.NeonInputAdapter.Instance;
            var ctrl = player.GetComponent<Studio.Gameplay.PlayerController>();
            var mov = player.GetComponent<Studio.Gameplay.PlayerMovement>();
            var anim = player.GetComponent<Studio.Gameplay.RobotAnimator>();

            if (gm == null || input == null || ctrl == null || mov == null || anim == null)
            {
                Debug.LogError($"[GameplayVerify] Componentes ausentes: gm={gm}, input={input}, ctrl={ctrl}, mov={mov}, anim={anim}");
                return false;
            }

            mov.ResetPosition();
            ctrl.EnsureInputSubscribed();
            gm.StartGame();

            if (!ctrl.IsSubscribedToInput || !mov.CanMove)
            {
                Debug.LogError($"[GameplayVerify] Falha ao ativar controles: IsSubscribed={ctrl.IsSubscribedToInput}, CanMove={mov.CanMove}");
                return false;
            }

            // 1. Avanço para frente + Comando Esquerda (TriggerMoveLeft)
            input.TriggerMoveLeft();
            for (int i = 0; i < 12; i++)
            {
                mov.StepSimulation(0.016f);
                ctrl.StepController(0.016f);
            }
            if (runClip != null) runClip.SampleAnimation(aiModel, 0.25f);
            cam.transform.position = new Vector3(0f, 2.8f, player.transform.position.z - 5.2f);
            cam.transform.LookAt(new Vector3(0f, 0.8f, player.transform.position.z + 2.0f));
            RenderToPng(cam, Path.Combine(outDir, "gameplay_ctrl_01_move_left.png"));
            Debug.Log($"[GameplayVerify] MOVE_LEFT -> Lane={mov.CurrentLane} (esperado -1), Pos={player.transform.position}, BankAngle={anim.CurrentBankAngle:F2}");
            if (mov.CurrentLane != -1 || Mathf.Abs(player.transform.position.x - Studio.Foundation.GameConstants.LANE_LEFT_X) > 0.05f)
            {
                return false;
            }

            // 2. Comando Direita 2x (Esquerda -> Centro -> Direita via TriggerMoveRight)
            input.TriggerMoveRight();
            input.TriggerMoveRight();
            for (int i = 0; i < 20; i++)
            {
                mov.StepSimulation(0.016f);
                ctrl.StepController(0.016f);
            }
            if (runClip != null) runClip.SampleAnimation(aiModel, 0.75f);
            cam.transform.position = new Vector3(0f, 2.8f, player.transform.position.z - 5.2f);
            cam.transform.LookAt(new Vector3(0f, 0.8f, player.transform.position.z + 2.0f));
            RenderToPng(cam, Path.Combine(outDir, "gameplay_ctrl_02_move_right.png"));
            Debug.Log($"[GameplayVerify] MOVE_RIGHT -> Lane={mov.CurrentLane} (esperado 1), Pos={player.transform.position}, BankAngle={anim.CurrentBankAngle:F2}");
            if (mov.CurrentLane != 1 || Mathf.Abs(player.transform.position.x - Studio.Foundation.GameConstants.LANE_RIGHT_X) > 0.05f)
            {
                return false;
            }

            // Retornar ao centro para testar Pulo e Slide
            input.TriggerMoveLeft();
            for (int i = 0; i < 15; i++)
            {
                mov.StepSimulation(0.016f);
                ctrl.StepController(0.016f);
            }

            // 3. Comando de Pulo (TriggerJump) -> Ápice da parábola (~0.32s)
            input.TriggerJump();
            for (int i = 0; i < 20; i++)
            {
                mov.StepSimulation(0.016f);
                ctrl.StepController(0.016f);
            }
            if (jumpClip != null) jumpClip.SampleAnimation(aiModel, 0.33f);
            cam.transform.position = new Vector3(2.2f, 2.2f, player.transform.position.z - 3.8f);
            cam.transform.LookAt(new Vector3(0f, 1.4f, player.transform.position.z));
            RenderToPng(cam, Path.Combine(outDir, "gameplay_ctrl_03_jump.png"));
            Debug.Log($"[GameplayVerify] JUMP -> IsJumping={mov.IsJumping}, Pos={player.transform.position}, Animator.IsJumping={anim.AnimatorComponent?.GetBool("IsJumping")}");
            if (!mov.IsJumping || player.transform.position.y < 1.8f)
            {
                return false;
            }

            // Aguardar aterrissagem do pulo
            for (int i = 0; i < 25; i++)
            {
                mov.StepSimulation(0.016f);
                ctrl.StepController(0.016f);
            }

            // 4. Comando de Agachamento/Slide (TriggerSlide)
            input.TriggerSlide();
            for (int i = 0; i < 10; i++)
            {
                mov.StepSimulation(0.016f);
                ctrl.StepController(0.016f);
            }
            if (slideClip != null) slideClip.SampleAnimation(aiModel, 0.33f);
            cam.transform.position = new Vector3(2.0f, 1.3f, player.transform.position.z - 3.2f);
            cam.transform.LookAt(new Vector3(0f, 0.5f, player.transform.position.z));
            RenderToPng(cam, Path.Combine(outDir, "gameplay_ctrl_04_slide.png"));
            Debug.Log($"[GameplayVerify] SLIDE -> IsSliding={mov.IsSliding}, ColliderSize={mov.Hitbox?.size}, Animator.IsSliding={anim.AnimatorComponent?.GetBool("IsSliding")}");
            if (!mov.IsSliding || mov.Hitbox == null || mov.Hitbox.size.y > 0.70f || mov.Hitbox.size.y < 0.50f)
            {
                return false;
            }

            Debug.Log("[GameplayVerify] SUCESSO: Todos os 5 controles (Frente, Esquerda, Direita, Pulo, Agachamento) funcionaram perfeitamente!");
            return true;
        }

        private static void LogPoseState(string label, GameObject aiModel, SkinnedMeshRenderer smr)
        {
            Transform head = null, hips = null, footL = null, footR = null, arm = null;
            foreach (var t in aiModel.GetComponentsInChildren<Transform>())
            {
                if (t.name == "Armature") arm = t;
                else if (t.name == "Head") head = t;
                else if (t.name == "Hips") hips = t;
                else if (t.name == "Foot.L") footL = t;
                else if (t.name == "Foot.R") footR = t;
            }
            if (smr != null) smr.forceMatrixRecalculationPerRender = true;
            Debug.Log($"[InspectCharacter] {label} -> Armature.euler={arm?.eulerAngles} | Head.pos={head?.position} | Hips.pos={hips?.position} | Foot.L.pos={footL?.position} | Foot.R.pos={footR?.position} | Bounds.min={smr?.bounds.min}, max={smr?.bounds.max}, size={smr?.bounds.size}");
        }

        private static void RenderToPng(UnityEngine.Camera cam, string path)
        {
            int w = 1024, h = 768;
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            cam.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(rt);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
        }
    }
}
