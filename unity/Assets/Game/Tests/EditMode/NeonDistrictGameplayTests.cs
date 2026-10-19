using System.Collections.Generic;
using NUnit.Framework;
using Studio.Foundation;
using Studio.Gameplay;
using UnityEngine;

namespace Studio.Tests.EditMode
{
    [TestFixture]
    public class NeonDistrictGameplayTests
    {
        private static List<TrafficEdge> BuildTestNetwork()
        {
            var xs = new float[] { -52f, 0f, 52f };
            var zs = new float[] { -52f, 0f, 52f };
            return TrafficNetworkBuilder.BuildGrid(xs, zs, 6f, 2.75f, -1, 8f, 4f);
        }

        // ---------- DriveMath: powertrain ----------

        [Test]
        public void IntegrateForwardSpeed_FullThrottle_AcceleratesTowardMax()
        {
            float s = 0f;
            for (int i = 0; i < 200; i++)
                s = DriveMath.IntegrateForwardSpeed(s, 1f, 0f, 30f, -9f, 14f, 24f, 0.55f, 0.02f);
            Assert.Greater(s, 15f);
            Assert.LessOrEqual(s, 30f);
        }

        [Test]
        public void IntegrateForwardSpeed_Throttle_ClampsAtMaxSpeed()
        {
            float s = 29f;
            for (int i = 0; i < 100; i++)
                s = DriveMath.IntegrateForwardSpeed(s, 1f, 0f, 30f, -9f, 14f, 24f, 0.55f, 0.02f);
            Assert.LessOrEqual(s, 30.001f);
        }

        [Test]
        public void IntegrateForwardSpeed_BrakeAtSpeed_DeceleratesToZeroNotReverse()
        {
            float s = 15f;
            for (int i = 0; i < 60; i++)
                s = DriveMath.IntegrateForwardSpeed(s, 0f, 1f, 30f, -9f, 14f, 24f, 0.55f, 0.02f);
            Assert.GreaterOrEqual(s, -9.001f);
            // Freio prolongado entra em ré limitada
            Assert.GreaterOrEqual(s, -9.001f);
            Assert.LessOrEqual(s, 0.5f);
        }

        [Test]
        public void IntegrateForwardSpeed_ReverseHeld_ClampsAtMaxReverse()
        {
            float s = 0f;
            for (int i = 0; i < 400; i++)
                s = DriveMath.IntegrateForwardSpeed(s, 0f, 1f, 30f, -9f, 14f, 24f, 0.55f, 0.02f);
            Assert.AreEqual(-9f, s, 0.01f);
        }

        [Test]
        public void IntegrateForwardSpeed_NoInput_DragSlowsCar()
        {
            float s = DriveMath.IntegrateForwardSpeed(20f, 0f, 0f, 30f, -9f, 14f, 24f, 0.55f, 1f);
            Assert.Less(s, 20f);
        }

        // ---------- DriveMath: steering & drift ----------

        [Test]
        public void SteeringSpeedFactor_ZeroSpeed_ReturnsZero()
        {
            Assert.AreEqual(0f, DriveMath.SteeringSpeedFactor(0f, 18f));
        }

        [Test]
        public void SteeringSpeedFactor_AtRefSpeed_IsOne()
        {
            Assert.AreEqual(1f, DriveMath.SteeringSpeedFactor(18f, 18f), 0.001f);
        }

        [Test]
        public void SteerYawRate_Drift_BoostsYaw()
        {
            float normal = DriveMath.SteerYawRate(1f, 15f, 105f, 18f, 1.5f, false);
            float drift = DriveMath.SteerYawRate(1f, 15f, 105f, 18f, 1.5f, true);
            Assert.Greater(drift, normal);
        }

        [Test]
        public void SteerYawRate_Reverse_InvertsSign()
        {
            float fwd = DriveMath.SteerYawRate(1f, 15f, 105f, 18f, 1.5f, false);
            float rev = DriveMath.SteerYawRate(1f, -15f, 105f, 18f, 1.5f, false);
            Assert.AreEqual(-fwd, rev, 0.5f);
        }

        [Test]
        public void SlipAngleDeg_AlignedVelocity_IsZero()
        {
            Assert.AreEqual(0f, DriveMath.SlipAngleDeg(Vector3.forward * 10f, Vector3.forward));
        }

        [Test]
        public void SlipAngleDeg_SidewaysVelocity_IsNinety()
        {
            Assert.AreEqual(90f, DriveMath.SlipAngleDeg(Vector3.right * 10f, Vector3.forward), 0.5f);
        }

        [Test]
        public void MsToKmh_ConvertsCorrectly()
        {
            Assert.AreEqual(36f, DriveMath.MsToKmh(10f), 0.001f);
        }

        // ---------- TrafficEdge: geometria de polilinha ----------

        [Test]
        public void TrafficEdge_Length_SumsSegments()
        {
            var e = new TrafficEdge { points = new[] { Vector3.zero, new Vector3(3f, 0f, 0f), new Vector3(3f, 0f, 4f) } };
            Assert.AreEqual(7f, e.Length, 0.001f);
        }

        [Test]
        public void TrafficEdge_PointAtDistance_LerpsAndClamps()
        {
            var e = new TrafficEdge { points = new[] { Vector3.zero, new Vector3(10f, 0f, 0f) } };
            Assert.AreEqual(new Vector3(5f, 0f, 0f), e.PointAtDistance(5f));
            Assert.AreEqual(new Vector3(10f, 0f, 0f), e.PointAtDistance(50f));
            Assert.AreEqual(Vector3.zero, e.PointAtDistance(-3f));
        }

        [Test]
        public void TrafficEdge_DirectionAtDistance_FollowsSegments()
        {
            var e = new TrafficEdge { points = new[] { Vector3.zero, new Vector3(10f, 0f, 0f), new Vector3(10f, 0f, 10f) } };
            Assert.AreEqual(Vector3.right, e.DirectionAtDistance(5f));
            Assert.AreEqual(Vector3.forward, e.DirectionAtDistance(15f));
        }

        // ---------- TrafficNetworkBuilder: grade dirigida ----------

        [Test]
        public void BuildGrid_AllNextIndices_ResolveToValidEdges()
        {
            var edges = BuildTestNetwork();
            for (int i = 0; i < edges.Count; i++)
            {
                var next = edges[i].next;
                Assert.IsNotNull(next, $"Aresta {i} sem saída (cul-de-sac indevido)");
                Assert.GreaterOrEqual(next.Length, 1, $"Aresta {i} sem continuação");
                foreach (var n in next)
                {
                    Assert.GreaterOrEqual(n, 0);
                    Assert.Less(n, edges.Count);
                }
            }
        }

        [Test]
        public void BuildGrid_RightHandTraffic_LanesOnRightSide()
        {
            var edges = BuildTestNetwork();
            // Northbound na rua x=-52 deve ter x > -52 (faixa da direita)
            bool foundNorth = false;
            bool foundEast = false;
            foreach (var e in edges)
            {
                if (e.points.Length == 2 && Mathf.Approximately(e.points[0].x, -52f + 2.75f)
                    && e.points[0].z < e.points[1].z)
                {
                    foundNorth = true;
                }
                if (e.points.Length == 2 && Mathf.Approximately(e.points[0].z, -52f - 2.75f)
                    && e.points[0].x < e.points[1].x)
                {
                    foundEast = true;
                }
            }
            Assert.IsTrue(foundNorth, "Faixa northbound da direita não encontrada em x=-52+2.75");
            Assert.IsTrue(foundEast, "Faixa eastbound da direita não encontrada em z=-52-2.75");
        }

        [Test]
        public void BuildGrid_LaneEdges_AlwaysHaveExitAndInteriorHasThree()
        {
            var edges = BuildTestNetwork();
            int laneEdges = 0, threeExit = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].points.Length == 2) // lane edges são retas de 2 pontos
                {
                    laneEdges++;
                    Assert.GreaterOrEqual(edges[i].next.Length, 1,
                        $"Lane edge {i} sem saída (cul-de-sac)");
                    if (edges[i].next.Length == 3) threeExit++;
                }
            }
            Assert.Greater(laneEdges, 0);
            Assert.Greater(threeExit, 0, "Cruzamento interior deve oferecer reto/esq/dir");
        }

        [Test]
        public void BuildGrid_Connectors_HaveSingleContinuation()
        {
            var edges = BuildTestNetwork();
            int connectors = 0;
            for (int i = 0; i < edges.Count; i++)
            {
                if (edges[i].points.Length == 4) // conectores têm 4 pontos
                {
                    connectors++;
                    Assert.AreEqual(1, edges[i].next.Length,
                        $"Conector {i} deve levar a uma única lane edge");
                }
            }
            Assert.Greater(connectors, 0);
        }

        // ---------- TrafficNetwork component ----------

        [Test]
        public void PickNext_DeterministicWithSeed()
        {
            var go = new GameObject("TestNet");
            var net = go.AddComponent<TrafficNetwork>();
            net.SetEdges(BuildTestNetwork());
            var rng1 = new System.Random(42);
            var rng2 = new System.Random(42);
            for (int i = 0; i < 8; i++)
            {
                int e = net.PickNext(0, rng1);
                int e2 = net.PickNext(0, rng2);
                Assert.AreEqual(e, e2);
                Assert.GreaterOrEqual(e, 0);
            }
            Object.DestroyImmediate(go);
        }

        /// <summary>
        /// Regressão: domain reload em play mode zera campos não serializáveis
        /// (System.Random). PickNext com rng null deve tratar como dead-end,
        /// nunca lançar NullReferenceException (bug real: NRE em runtime).
        /// </summary>
        [Test]
        public void PickNext_NullRng_ReturnsDeadEnd()
        {
            var go = new GameObject("TestNet");
            var net = go.AddComponent<TrafficNetwork>();
            net.SetEdges(BuildTestNetwork());
            Assert.AreEqual(-1, net.PickNext(0, null));
            Object.DestroyImmediate(go);
        }

        [Test]
        public void TryGetNearestPose_SnapsToLane()
        {
            var go = new GameObject("TestNet");
            var net = go.AddComponent<TrafficNetwork>();
            net.SetEdges(BuildTestNetwork());
            // Ponto entre a rua x=-26 e x=26: deve cair numa faixa próxima
            bool ok = net.TryGetNearestPose(new Vector3(0f, 0f, 0f), out var pos, out var dir);
            Assert.IsTrue(ok);
            Assert.Less(Vector3.Distance(pos, new Vector3(0f, 0f, 0f)), 40f);
            Assert.Greater(dir.sqrMagnitude, 0.5f);
            Object.DestroyImmediate(go);
        }

        // ---------- DriveInputAdapter: controles virtuais ----------

        [Test]
        public void DriveInputAdapter_VirtualControls_SetAndClear()
        {
            var go = new GameObject("TestInput");
            var adapter = go.AddComponent<Studio.Input.DriveInputAdapter>();
            adapter.SetVirtualControl(Studio.Input.DriveInputAdapter.DriveControl.Gas, true);
            adapter.SetVirtualControl(Studio.Input.DriveInputAdapter.DriveControl.Drift, true);
            Assert.IsTrue(adapter.IsVirtualHeld(Studio.Input.DriveInputAdapter.DriveControl.Gas));
            Assert.IsTrue(adapter.IsVirtualHeld(Studio.Input.DriveInputAdapter.DriveControl.Drift));
            adapter.ClearVirtual();
            Assert.IsFalse(adapter.IsVirtualHeld(Studio.Input.DriveInputAdapter.DriveControl.Gas));
            Assert.IsFalse(adapter.IsVirtualHeld(Studio.Input.DriveInputAdapter.DriveControl.Drift));
            Object.DestroyImmediate(go);
        }
    }
}
