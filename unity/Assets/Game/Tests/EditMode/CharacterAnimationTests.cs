using NUnit.Framework;
using Studio.Editor;
using Studio.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Studio.Tests.EditMode
{
    [TestFixture]
    public class CharacterAnimationTests
    {
        private const string FBX_PATH = "Assets/Game/Art/Characters/RobotAI/robot_character.fbx";
        private const string CONTROLLER_PATH = "Assets/Game/Art/Characters/RobotAI/Robot_AnimatorController.controller";
        private const string MATERIAL_PATH = "Assets/Game/Materials/NeonRush/Mat_RobotAI.mat";

        [Test]
        public void Character_RiggedFBX_ExistsAndHasHumanoidBones()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FBX_PATH);
            Assert.IsNotNull(fbx, $"O asset FBX não foi encontrado em: {FBX_PATH}");

            // Verificar se possui Armature e ossos essenciais de locomoção
            var armature = fbx.transform.Find("Armature");
            Assert.IsNotNull(armature, "O modelo FBX deve possuir um nó de Armature (esqueleto).");

            var hips = armature.Find("Hips") ?? armature.Find("Root/Hips");
            Assert.IsNotNull(hips, "O esqueleto deve possuir um osso Hips (quadril).");

            // Verificar presença de pernas
            var leftLeg = fbx.GetComponentsInChildren<Transform>();
            bool hasLeftLeg = false;
            bool hasRightLeg = false;
            foreach (var t in leftLeg)
            {
                if (t.name.Contains("UpperLeg.L") || t.name.Contains("Leg.L")) hasLeftLeg = true;
                if (t.name.Contains("UpperLeg.R") || t.name.Contains("Leg.R")) hasRightLeg = true;
            }

            Assert.IsTrue(hasLeftLeg, "O esqueleto do personagem deve possuir o osso UpperLeg.L para mover a perna esquerda.");
            Assert.IsTrue(hasRightLeg, "O esqueleto do personagem deve possuir o osso UpperLeg.R para mover a perna direita.");
        }

        [Test]
        public void Character_AnimatorController_IsConfiguredWithRunCycle()
        {
            var controller = CharacterPipelineEditorHelper.SetupRobotAnimatorController();
            Assert.IsNotNull(controller, "O AnimatorController deve ser configurado com sucesso.");

            var animController = controller as AnimatorController;
            Assert.IsNotNull(animController, "O asset deve ser do tipo AnimatorController.");

            var sm = animController.layers[0].stateMachine;
            Assert.IsNotNull(sm.defaultState, "O AnimatorController deve possuir um estado padrão.");
            Assert.AreEqual("RunCycle", sm.defaultState.name, "O estado padrão de animação deve ser RunCycle.");
            Assert.IsNotNull(sm.defaultState.motion, "O estado RunCycle deve possuir um AnimationClip associado.");
        }

        [Test]
        public void Character_Material_HasDedicatedEmissionMapAndPBR()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(MATERIAL_PATH);
            if (mat != null)
            {
                bool hasKeyword = mat.IsKeywordEnabled("_EMISSION") ||
                    mat.IsKeywordEnabled(new UnityEngine.Rendering.LocalKeyword(mat.shader, "_EMISSION"));
                var emissionMap = mat.GetTexture("_EmissionMap");
                var emissionColor = mat.GetColor("_EmissionColor");

                Assert.IsTrue(hasKeyword || emissionMap != null || emissionColor != Color.black, 
                    "O material deve ter a emissão configurada.");
                Assert.IsNotNull(emissionMap, "O material deve ter uma textura _EmissionMap dedicada para não lavar o modelo em azul sólido.");
                Assert.Greater(mat.GetFloat("_Smoothness"), 0.5f, "O material deve ter brilho PBR configurado.");
            }
        }

        [Test]
        public void Character_Posture_StandsUprightAndAnimatesLegsInAllStates()
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FBX_PATH);
            Assert.IsNotNull(fbx, "O FBX do personagem deve existir.");

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

            Assert.IsNotNull(runClip, "O FBX deve conter o clipe RunCycle.");
            Assert.IsNotNull(jumpClip, "O FBX deve conter o clipe Jump.");
            Assert.IsNotNull(slideClip, "O FBX deve conter o clipe Slide.");

            var instance = Object.Instantiate(fbx, Vector3.zero, Quaternion.identity);
            try
            {
                Transform head = null, hips = null, footL = null, footR = null;
                foreach (var t in instance.GetComponentsInChildren<Transform>())
                {
                    if (t.name == "Head") head = t;
                    else if (t.name == "Hips") hips = t;
                    else if (t.name == "Foot.L") footL = t;
                    else if (t.name == "Foot.R") footR = t;
                }

                Assert.IsNotNull(head, "Osso Head não encontrado.");
                Assert.IsNotNull(hips, "Osso Hips não encontrado.");
                Assert.IsNotNull(footL, "Osso Foot.L não encontrado.");
                Assert.IsNotNull(footR, "Osso Foot.R não encontrado.");

                // 1. Verificar RunCycle Frame 0 (Postura ereta + passada inicial)
                runClip.SampleAnimation(instance, 0.0f);
                Assert.Greater(head.position.y, hips.position.y + 0.35f, "O personagem deve estar EM PÉ no RunCycle (Head.y significativamente acima de Hips.y).");
                Assert.Greater(hips.position.y, footL.position.y + 0.15f, "O quadril deve estar acima dos pés no RunCycle.");
                Assert.Less(Mathf.Abs(head.position.z - hips.position.z), 0.35f, "O personagem não pode estar deitado no eixo Z.");

                float footL_z0 = footL.position.z;
                float footR_z0 = footR.position.z;

                // 2. Verificar RunCycle Frame 12 (0.5s - inversão completa da passada das pernas)
                runClip.SampleAnimation(instance, 0.5f);
                Assert.Greater(head.position.y, hips.position.y + 0.35f, "O personagem deve permanecer EM PÉ no meio do ciclo de corrida.");
                float footL_z12 = footL.position.z;
                float footR_z12 = footR.position.z;

                float strideDeltaL = Mathf.Abs(footL_z12 - footL_z0);
                float strideDeltaR = Mathf.Abs(footR_z12 - footR_z0);
                Assert.Greater(strideDeltaL, 0.25f, "A perna esquerda deve se movimentar ativamente para frente e para trás durante a corrida.");
                Assert.Greater(strideDeltaR, 0.25f, "A perna direita deve se movimentar ativamente para frente e para trás durante a corrida.");

                // 3. Verificar Jump (Ápice em 0.33s - elevação vertical e postura em pé)
                jumpClip.SampleAnimation(instance, 0.33f);
                Assert.Greater(head.position.y, hips.position.y + 0.35f, "O personagem deve permanecer em pé durante o salto.");
                Assert.Greater(footL.position.y, 0.12f, "Os pés devem sair do chão durante o ápice do salto.");

                // 4. Verificar Slide (0.33s - agachamento para passar sob obstáculos mantendo postura coerente)
                slideClip.SampleAnimation(instance, 0.33f);
                Assert.Greater(head.position.y, hips.position.y + 0.25f, "Mesmo agachado no Slide, a cabeça deve estar acima do quadril.");
                Assert.Less(head.position.y, 0.90f, "No Slide, a cabeça deve abaixar para passar sob vigas altas.");
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }
    }
}
