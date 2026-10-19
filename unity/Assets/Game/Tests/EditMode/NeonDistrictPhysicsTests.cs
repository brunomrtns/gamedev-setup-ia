using NUnit.Framework;
using Studio.Gameplay;
using UnityEngine;

namespace Studio.Tests.EditMode
{
    /// <summary>
    /// Testes de física/combate (physics-and-combat.md): quebra de props
    /// por limiar de impulso e convergência do cano no centro da câmera.
    /// </summary>
    public class NeonDistrictPhysicsTests
    {
        // --- Classificação de impulso do tráfego ---
        [Test]
        public void ClassifyImpulse_Boundaries()
        {
            Assert.AreEqual(TrafficVehicle.ImpactClass.None,
                TrafficVehicle.ClassifyImpulse(3999f, 4000f, 20000f));
            Assert.AreEqual(TrafficVehicle.ImpactClass.Soft,
                TrafficVehicle.ClassifyImpulse(12000f, 4000f, 20000f));
            Assert.AreEqual(TrafficVehicle.ImpactClass.Severe,
                TrafficVehicle.ClassifyImpulse(25000f, 4000f, 20000f));
        }

        // --- Re-roteamento: aresta mais próxima ---
        [Test]
        public void FindNearestEdge_ProjectsOntoSegment()
        {
            var go = new GameObject("net");
            var net = go.AddComponent<TrafficNetwork>();
            net.SetEdges(new System.Collections.Generic.List<TrafficEdge>
            {
                new TrafficEdge { points = new[] { Vector3.zero, new Vector3(10f, 0f, 0f) } },
                new TrafficEdge { points = new[] { new Vector3(0f, 0f, 50f), new Vector3(10f, 0f, 50f) } },
            });
            Assert.IsTrue(net.FindNearestEdge(new Vector3(5f, 0f, 3f), out int edge, out float dist));
            Assert.AreEqual(0, edge);
            Assert.AreEqual(5f, dist, 0.01f);
            Assert.IsTrue(net.FindNearestEdge(new Vector3(8f, 0f, 40f), out edge, out dist));
            Assert.AreEqual(1, edge);
            Assert.AreEqual(8f, dist, 0.01f);
            Object.DestroyImmediate(go);
        }

        // --- Zonas de dano veicular ---
        [Test]
        public void ZoneMultiplier_HoodTankChassis()
        {
            Assert.AreEqual(1.5f, VehicleDamageController.ZoneMultiplier("Col_Hood"), 0.01f);
            Assert.AreEqual(2.0f, VehicleDamageController.ZoneMultiplier("Col_Tank"), 0.01f);
            Assert.AreEqual(1.0f, VehicleDamageController.ZoneMultiplier("Col_Chassis"), 0.01f);
            Assert.AreEqual(1.0f, VehicleDamageController.ZoneMultiplier(null), 0.01f);
        }

        [Test]
        public void ExplosionFalloff_QuadraticDecay()
        {
            Assert.AreEqual(0f, VehicleDamageController.ExplosionFalloff(9f, 9f));
            Assert.AreEqual(0f, VehicleDamageController.ExplosionFalloff(15f, 9f));
            Assert.AreEqual(1f, VehicleDamageController.ExplosionFalloff(0f, 9f));
            Assert.AreEqual(0.25f, VehicleDamageController.ExplosionFalloff(4.5f, 9f), 0.01f);
        }

        /// <summary>Tiro único de fuzil não explode o carro no primeiro impacto.</summary>
        [Test]
        public void SingleShot_NeverExplodes()
        {
            // dano de 1 tiro no capô (pior zona): 12*1.5=18 → ainda Intact/Light
            var s = VehicleDamageController.StateForHealth(100f - 18f, 100f);
            Assert.AreNotEqual(VehicleDamageController.DamageState.Exploded, s);
            Assert.AreNotEqual(VehicleDamageController.DamageState.Burning, s);
        }

        // --- Estados de dano veicular ---
        [Test]
        public void VehicleDamage_StateForHealth_Transitions()
        {
            Assert.AreEqual(VehicleDamageController.DamageState.Intact,
                VehicleDamageController.StateForHealth(100f, 100f));
            Assert.AreEqual(VehicleDamageController.DamageState.Light,
                VehicleDamageController.StateForHealth(70f, 100f));
            Assert.AreEqual(VehicleDamageController.DamageState.Severe,
                VehicleDamageController.StateForHealth(45f, 100f));
            Assert.AreEqual(VehicleDamageController.DamageState.Burning,
                VehicleDamageController.StateForHealth(15f, 100f));
            Assert.AreEqual(VehicleDamageController.DamageState.Exploded,
                VehicleDamageController.StateForHealth(0f, 100f));
        }

        // --- Quebra de prop por limiar ---
        [Test]
        public void BreakableProp_ShouldBreak_RespectsThreshold()
        {
            Assert.IsFalse(BreakableProp.ShouldBreak(5999f, 6000f));
            Assert.IsTrue(BreakableProp.ShouldBreak(6000f, 6000f));
            Assert.IsTrue(BreakableProp.ShouldBreak(30000f, 6000f));
        }

        [Test]
        public void BreakableProp_StumpScale_Clamped()
        {
            Assert.AreEqual(0.05f, BreakableProp.StumpScaleY(8f, 0.35f), 0.001f);
            Assert.AreEqual(0.35f, BreakableProp.StumpScaleY(1f, 0.35f), 0.001f);
            Assert.AreEqual(0.6f, BreakableProp.StumpScaleY(0.5f, 0.35f), 0.001f);
        }

        // --- Convergência do cano no centro da câmera ---
        [Test]
        public void ComputeMuzzleDir_PointsAtTarget()
        {
            var muzzle = new Vector3(0.2f, 1.4f, 0.5f);
            var target = new Vector3(0f, 2f, 40f);
            var dir = Ak47Weapon.ComputeMuzzleDir(muzzle, target);
            Assert.AreEqual(1f, dir.magnitude, 0.0001f);
            var reach = muzzle + dir * Vector3.Distance(muzzle, target);
            Assert.AreEqual(target.x, reach.x, 0.001f);
            Assert.AreEqual(target.y, reach.y, 0.001f);
            Assert.AreEqual(target.z, reach.z, 0.001f);
        }

        [Test]
        public void ComputeMuzzleDir_Degenerate_ReturnsForward()
        {
            var p = new Vector3(1f, 1f, 1f);
            Assert.AreEqual(Vector3.forward, Ak47Weapon.ComputeMuzzleDir(p, p));
        }
    }
}
