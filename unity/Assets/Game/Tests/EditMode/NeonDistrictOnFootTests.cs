using NUnit.Framework;
using Studio.Gameplay;
using Studio.Input;
using UnityEngine;

namespace Studio.Tests.EditMode
{
    /// <summary>Testes do Capítulo 2 — modo a pé, munição, alvos e modo.</summary>
    public class NeonDistrictOnFootTests
    {
        private GameObject _root;

        [SetUp]
        public void SetUp() { _root = new GameObject("TestRoot"); }

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
        }

        [Test]
        public void OnFootMotor_CapsuleScale_IsHumanCanonical()
        {
            var go = new GameObject("P");
            var cc = go.AddComponent<CharacterController>();
            OnFootMotor.ApplyCanonicalScale(cc); // Awake chama isso em runtime
            Assert.AreEqual(1.75f, cc.height, 0.001f, "Humano canônico 1.75m");
            Assert.AreEqual(0.35f, cc.radius, 0.001f);
            Assert.AreEqual(1.65f, OnFootMotor.EYE_HEIGHT, 0.001f, "Câmera FPS a 1.65m");
            Object.DestroyImmediate(go);
        }

        [Test]
        public void ModeManager_RegisterCanHit_IncrementsCounter()
        {
            var mgr = _root.AddComponent<NeonDistrictModeManager>();
            mgr.CansTotal = 18;
            int hits = -1, total = -1;
            mgr.OnCansChanged += (h, t) => { hits = h; total = t; };
            mgr.RegisterCanHit();
            mgr.RegisterCanHit();
            Assert.AreEqual(2, mgr.CansHit);
            Assert.AreEqual(2, hits);
            Assert.AreEqual(18, total);
        }

        [Test]
        public void ModeManager_DefaultMode_IsDriving()
        {
            var mgr = _root.AddComponent<NeonDistrictModeManager>();
            Assert.AreEqual(NeonDistrictModeManager.Mode.Driving, mgr.Current);
        }

        [Test]
        public void InputAdapter_VirtualFootControls_ReadThrough()
        {
            var adapter = _root.AddComponent<DriveInputAdapter>();
            adapter.SetVirtualControl(DriveInputAdapter.DriveControl.Fire, true);
            adapter.SetVirtualControl(DriveInputAdapter.DriveControl.Ads, true);
            // Update não roda no EditMode sem Play — mas o estado virtual é lido direto
            Assert.IsTrue(adapter.FireHeld);
            Assert.IsTrue(adapter.AdsHeld);
            adapter.SetFootMove(new Vector2(0.5f, 1f));
            Assert.AreEqual(new Vector2(0.5f, 1f), adapter.FootMove);
        }

        [Test]
        public void ShootableCan_HasRigidbody_AfterAddComponent()
        {
            var can = new GameObject("Can");
            var s = can.AddComponent<ShootableCan>();
            // Awake roda ao adicionar: garante rigidbody p/ salto
            Assert.IsNotNull(can.GetComponent<Rigidbody>());
            Object.DestroyImmediate(can);
        }

        [Test]
        public void TrafficVehicle_ImplementsIShootable()
        {
            var v = new GameObject("T");
            var tv = v.AddComponent<TrafficVehicle>();
            Assert.IsTrue(tv is IShootable);
            Object.DestroyImmediate(v);
        }

        [Test]
        public void Ak47Weapon_InitialAmmo_Is30()
        {
            var w = _root.AddComponent<Ak47Weapon>();
            Assert.AreEqual(30, w.Ammo);
            Assert.AreEqual(90, w.Reserve);
        }

        [Test]
        public void Ak47Weapon_AddAmmo_AccumulatesReserve()
        {
            var w = _root.AddComponent<Ak47Weapon>();
            w.AddAmmo(30);
            Assert.AreEqual(120, w.Reserve);
        }

        [Test]
        public void OnFootMotor_CrouchScale_IsHalfHeight()
        {
            Assert.AreEqual(1.0f, OnFootMotor.CROUCH_HEIGHT, 0.001f, "Crouch = 1.0m");
            Assert.AreEqual(0.95f, OnFootMotor.CROUCH_EYE, 0.001f);
        }

        [Test]
        public void InputAdapter_ConsumeJump_OnlyOnce()
        {
            var adapter = _root.AddComponent<DriveInputAdapter>();
            adapter.SetVirtualControl(DriveInputAdapter.DriveControl.Jump, true);
            // ConsumeJump limpa o virtual — primeiro true, segundo false
            Assert.IsTrue(adapter.ConsumeJump());
            Assert.IsFalse(adapter.ConsumeJump());
        }

        [Test]
        public void InputAdapter_ConsumeCrouch_OnlyOnce()
        {
            var adapter = _root.AddComponent<DriveInputAdapter>();
            adapter.SetVirtualControl(DriveInputAdapter.DriveControl.Crouch, true);
            Assert.IsTrue(adapter.ConsumeCrouch());
            Assert.IsFalse(adapter.ConsumeCrouch());
        }

        [Test]
        public void Ak47Weapon_TracerPool_StateInitialized()
        {
            var w = _root.AddComponent<Ak47Weapon>();
            var pool = new LineRenderer[4];
            for (int i = 0; i < 4; i++)
            {
                var t = new GameObject($"tr{i}");
                t.transform.SetParent(_root.transform);
                pool[i] = t.AddComponent<LineRenderer>();
            }
            w.SetTracerPool(pool);
            w.SetCasingPool(new Transform[6]);
            w.SetDecalPool(new Transform[8]);
            Assert.Pass("Pools aceitos sem exceção");
        }
    }
}
