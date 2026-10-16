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
    }
}
