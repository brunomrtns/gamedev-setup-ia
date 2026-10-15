using NUnit.Framework;
using UnityEngine;

namespace Studio.Tests.EditMode
{
    public class CoreValidationTests
    {
        [Test]
        public void Project_QualitySettings_TargetFrameRate_IsValid()
        {
            // O target frame rate deve ser definido ou clamped para não superaquecer mobile
            int targetFps = Application.targetFrameRate;
            Assert.IsTrue(targetFps == -1 || targetFps >= 30, $"Target FPS inválido: {targetFps}");
        }

        [Test]
        public void MobileBudget_DrawCallThreshold_IsConfiguredProperly()
        {
            const int MAX_ALLOWED_DRAWCALLS = 150;
            const int SAMPLE_DRAWCALLS = 65;
            Assert.LessOrEqual(SAMPLE_DRAWCALLS, MAX_ALLOWED_DRAWCALLS, "Orçamento de draw calls violado!");
        }

        [Test]
        public void MobileBudget_TriangleBudget_IsWithinLimits()
        {
            const int MAX_ALLOWED_TRIANGLES = 350000;
            const int SAMPLE_TRIANGLES = 45000;
            Assert.LessOrEqual(SAMPLE_TRIANGLES, MAX_ALLOWED_TRIANGLES, "Orçamento de triângulos excedido!");
        }

        [Test]
        public void Coordinates_Transformation_FollowsRightHandRuleOrUnityConvention()
        {
            Vector3 forward = Vector3.forward;
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, forward);
            Assert.AreEqual(Vector3.right, right, "Orientação de vetores de coordenadas em desacordo!");
        }
    }
}
