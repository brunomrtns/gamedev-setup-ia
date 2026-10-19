using NUnit.Framework;
using Studio.Foundation;
using Studio.Gameplay;
using Studio.Input;
using UnityEditor;
using UnityEngine;

namespace Studio.Tests.EditMode
{
    [TestFixture]
    public class AbyssalBreachGameplayTests
    {
        #region 1. Testes Unitários de Matemática e Lógica (Oxigênio, Balística, Hidrodinâmica)

        [Test]
        public void OxygenMath_NormalSwimVsSprint_DrainsAtExpectedRatesAndClampsToZero()
        {
            float o2Normal = AbyssalMath.ComputeOxygenAfterStep(100f, 100f, isSprinting: false, 1.5f, 3.2f, 10f);
            float o2Sprint = AbyssalMath.ComputeOxygenAfterStep(100f, 100f, isSprinting: true, 1.5f, 3.2f, 10f);
            float o2Clamped = AbyssalMath.ComputeOxygenAfterStep(5f, 100f, isSprinting: true, 1.5f, 3.2f, 10f);

            Assert.AreEqual(85.0f, o2Normal, 0.001f, "Nado normal por 10s a 1.5/s deve resultar em 85% de O2.");
            Assert.AreEqual(68.0f, o2Sprint, 0.001f, "Propulsão rápida (Sprint) por 10s a 3.2/s deve resultar em 68% de O2.");
            Assert.AreEqual(0.0f, o2Clamped, 0.001f, "Oxigênio nunca deve ficar negativo.");
        }

        [Test]
        public void HypoxiaMath_WhenOxygenAboveZero_ReturnsZeroDamage_AndWhenDepleted_ReturnsExpectedDamage()
        {
            float noDamage = AbyssalMath.ComputeHypoxiaDamage(currentOxygen: 12.5f, hypoxiaDamageRate: 12.0f, deltaTime: 2.0f);
            float hypoxiaDamage = AbyssalMath.ComputeHypoxiaDamage(currentOxygen: 0.0f, hypoxiaDamageRate: 12.0f, deltaTime: 2.5f);

            Assert.AreEqual(0.0f, noDamage, 0.001f, "Enquanto houver oxigênio (>0), não deve haver dano de hipóxia.");
            Assert.AreEqual(30.0f, hypoxiaDamage, 0.001f, "Com O2 zerado, 2.5s a 12 HP/s deve causar 30 HP de dano.");
        }

        [Test]
        public void BallisticSpread_WhenAimingADS_DrasticallyReducesSpreadAndRecoilPenalty()
        {
            float hipSpread = AbyssalMath.ComputeBallisticSpreadDegrees(isAimingADS: false, 2.4f, 0.25f, recoilPenaltyDegrees: 1.0f);
            float adsSpread = AbyssalMath.ComputeBallisticSpreadDegrees(isAimingADS: true, 2.4f, 0.25f, recoilPenaltyDegrees: 1.0f);

            Assert.AreEqual(3.4f, hipSpread, 0.001f);
            Assert.AreEqual(0.60f, adsSpread, 0.001f);
            Assert.Less(adsSpread, hipSpread * 0.25f, "A dispersão em mira óptica (ADS) deve ser significativamente menor que em hipfire.");
        }

        [Test]
        public void UnderwaterHydrodynamics_AccelerationAndWaterDrag_BehaveDeterministically()
        {
            Vector3 v0 = Vector3.zero;
            Vector3 vAccel = AbyssalMath.ComputeUnderwaterVelocity(v0, Vector3.forward, targetSpeed: 6.0f, acceleration: 12.0f, waterDrag: 5.0f, deltaTime: 0.25f);
            Assert.AreEqual(3.0f, vAccel.z, 0.001f, "Aceleração hidrodinâmica deve avançar linearmente em direção à velocidade alvo.");

            Vector3 vDecel = AbyssalMath.ComputeUnderwaterVelocity(vAccel, Vector3.zero, targetSpeed: 6.0f, acceleration: 12.0f, waterDrag: 4.0f, deltaTime: 0.5f);
            Assert.AreEqual(1.0f, vDecel.z, 0.001f, "Sem comando de nado, o arrasto da água (waterDrag) deve desacelerar suavemente o mergulhador.");
        }

        [Test]
        public void ExtractionScore_WithCoresDronesAndOxygenBonus_ComputesAccurateTotal()
        {
            int score = AbyssalMath.ComputeExtractionScore(coresCollected: 3, dronesDestroyed: 2, remainingOxygen: 60f, extracted: true);
            // 3 * 250 (750) + 2 * 300 (600) + 60 * 5 (300) + 500 (extraction) = 2150
            Assert.AreEqual(2150, score);
        }

        #endregion

        #region 2. Testes de Controles e Entrada (Sem Condição de Corrida no Ciclo de Vida)

        [Test]
        public void InputLifecycle_WhenPlayerCreatedBeforeInputAdapter_SubscribesCleanlyWithoutRaceCondition()
        {
            var playerObj = new GameObject("TestDiver_CreatedFirst");
            var motor = playerObj.AddComponent<AbyssalFirstPersonMotor>();
            var weapons = playerObj.AddComponent<AbyssalWeaponController>();

            var sysObj = new GameObject("TestSystems_CreatedAfter");
            var gm = sysObj.AddComponent<AbyssalGameManager>();
            var input = sysObj.AddComponent<AbyssalInputAdapter>();

            try
            {
                gm.SetPlayerSystems(motor, null, weapons);
                gm.StartGame();

                motor.StepSimulation(0.016f);
                weapons.StepWeapons(0.016f);

                Assert.IsTrue(motor.IsSubscribedToInput, "AbyssalFirstPersonMotor deve assinar o AbyssalInputAdapter mesmo quando instanciado antes na cena.");
                Assert.IsTrue(weapons.IsSubscribedToInput, "AbyssalWeaponController deve assinar o AbyssalInputAdapter mesmo quando instanciado antes na cena.");

                input.SetAiming(true);
                Assert.IsTrue(motor.IsAimingADS, "Mudança de ADS no InputAdapter deve refletir imediatamente no motor.");

                int initialAmmo = weapons.CurrentMagazineAmmo;
                input.TriggerFire();
                Assert.AreEqual(initialAmmo - 1, weapons.CurrentMagazineAmmo, "TriggerFire via InputAdapter deve disparar o rifle imediatamente.");
            }
            finally
            {
                Object.DestroyImmediate(playerObj);
                Object.DestroyImmediate(sysObj);
            }
        }

        [Test]
        public void UnderwaterLocomotion_SwimForwardStrafeAndVerticalAscendDescend_MovesPlayerIn3DSpace()
        {
            var sysObj = new GameObject("TestSystems");
            var input = sysObj.AddComponent<AbyssalInputAdapter>();
            var gm = sysObj.AddComponent<AbyssalGameManager>();

            var playerObj = new GameObject("TestDiver");
            playerObj.transform.position = new Vector3(0f, 5.0f, 0f);
            var motor = playerObj.AddComponent<AbyssalFirstPersonMotor>();

            try
            {
                gm.SetPlayerSystems(motor, null, null);
                gm.StartGame();

                // 1. Nado frontal (+Z) + Ascensão vertical (+Y)
                input.SetMoveInput(new Vector2(0f, 1f));
                input.SetVerticalInput(1f);
                for (int i = 0; i < 20; i++)
                {
                    motor.StepSimulation(0.05f);
                }

                Assert.Greater(playerObj.transform.position.z, 3.0f, "Mergulhador deve avançar no eixo +Z.");
                Assert.Greater(playerObj.transform.position.y, 7.0f, "Comando Ascend (+1) deve elevar a profundidade Y do mergulhador.");

                // 2. Mergulho descendente (-Y) + Strafe lateral (+X)
                float peakY = playerObj.transform.position.y;
                input.SetMoveInput(new Vector2(1f, 0f));
                input.SetVerticalInput(-1f);
                for (int i = 0; i < 20; i++)
                {
                    motor.StepSimulation(0.05f);
                }

                Assert.Greater(playerObj.transform.position.x, 2.5f, "Strafe lateral deve deslocar o mergulhador no eixo +X.");
                Assert.Less(playerObj.transform.position.y, peakY - 1.5f, "Comando Descend (-1) deve reduzir a altura Y do mergulhador.");
            }
            finally
            {
                Object.DestroyImmediate(playerObj);
                Object.DestroyImmediate(sysObj);
            }
        }

        [Test]
        public void WeaponSystem_AimADS_FireReloadAndSwitchToTacticalKnife_WorksDeterministically()
        {
            var sysObj = new GameObject("TestSystems");
            var input = sysObj.AddComponent<AbyssalInputAdapter>();
            var gm = sysObj.AddComponent<AbyssalGameManager>();

            var playerObj = new GameObject("TestDiver");
            var camObj = new GameObject("TestCam");
            camObj.transform.SetParent(playerObj.transform);
            var cam = camObj.AddComponent<Camera>();
            var motor = playerObj.AddComponent<AbyssalFirstPersonMotor>();
            motor.SetCameraReferences(camObj.transform, cam);
            var weapons = playerObj.AddComponent<AbyssalWeaponController>();

            try
            {
                gm.SetPlayerSystems(motor, null, weapons);
                gm.StartGame();
                motor.EnsureInputSubscribed();
                weapons.EnsureInputSubscribed();

                // 1. Ativar ADS e verificar zoom do FOV e redução da dispersão
                float hipSpread = weapons.CurrentSpreadDegrees;
                input.SetAiming(true);
                for (int i = 0; i < 15; i++)
                {
                    motor.StepSimulation(0.05f);
                    weapons.StepWeapons(0.05f);
                }
                Assert.AreEqual(AbyssalMath.ADS_FOV, cam.fieldOfView, 0.5f, "Em modo ADS, a câmera deve reduzir o FOV para o zoom óptico (46°).");
                Assert.Less(weapons.CurrentSpreadDegrees, hipSpread, "Em modo ADS, a dispersão balística deve ser reduzida.");

                // 2. Disparar 3 projéteis de plasma
                for (int i = 0; i < 3; i++)
                {
                    input.TriggerFire();
                    weapons.StepWeapons(0.20f);
                }
                Assert.AreEqual(21, weapons.CurrentMagazineAmmo, "Após 3 disparos, o carregador deve cair de 24 para 21.");

                // 3. Recarregar e concluir o ciclo de recarga
                input.TriggerReload();
                Assert.IsTrue(weapons.IsReloading, "Comando Reload deve iniciar o estado IsReloading.");
                weapons.StepWeapons(1.6f);
                Assert.IsFalse(weapons.IsReloading, "Após 1.6s, a recarga deve concluir.");
                Assert.AreEqual(24, weapons.CurrentMagazineAmmo, "O carregador deve voltar a 24 munições.");
                Assert.AreEqual(93, weapons.ReserveAmmo, "A reserva deve descontar as 3 munições transferidas (96 -> 93).");

                // 4. Trocar para a Faca Tática e executar ataque Melee
                input.TriggerSwitchWeapon();
                Assert.AreEqual(AbyssalWeaponSlot.TacticalKnife, weapons.ActiveSlot, "SwitchWeapon deve alternar para a Faca Tática.");
                input.TriggerMelee();
                Assert.AreEqual(1, weapons.TotalMeleeAttacks, "TriggerMelee deve contabilizar o golpe corpo a corpo.");
            }
            finally
            {
                Object.DestroyImmediate(playerObj);
                Object.DestroyImmediate(sysObj);
            }
        }

        #endregion

        #region 3. Testes de Integração de Gameplay (Combate, Coleta, Oxigênio e Extração)

        [Test]
        public void GameplayIntegration_FullExtractionLoop_SwimCollectO2AndCoresDestroyDroneAndExtract()
        {
            var sysObj = new GameObject("TestSystems");
            var input = sysObj.AddComponent<AbyssalInputAdapter>();
            var gm = sysObj.AddComponent<AbyssalGameManager>();

            var playerObj = new GameObject("TestDiver");
            playerObj.transform.position = new Vector3(0f, 3.0f, 0f);
            var motor = playerObj.AddComponent<AbyssalFirstPersonMotor>();
            var survival = playerObj.AddComponent<OxygenSurvivalSystem>();
            var weapons = playerObj.AddComponent<AbyssalWeaponController>();

            // Drone hostil em Z = 12m (diretamente na linha de tiro +Z)
            var droneObj = new GameObject("HostileDrone");
            droneObj.transform.position = new Vector3(0f, 3.0f, 12f);
            droneObj.AddComponent<SphereCollider>().radius = 0.8f;
            var drone = droneObj.AddComponent<AbyssalThreatDrone>();

            // Tanque de O2 em Z = 15m e Núcleo de Energia em Z = 20m
            var o2Obj = new GameObject("OxygenTank");
            o2Obj.transform.position = new Vector3(0f, 3.0f, 15f);
            var o2Collectible = o2Obj.AddComponent<AbyssalCollectible>();
            o2Collectible.Configure(AbyssalCollectibleType.OxygenTank, 35f);

            var coreObj = new GameObject("EnergyCore");
            coreObj.transform.position = new Vector3(0f, 3.0f, 20f);
            var coreCollectible = coreObj.AddComponent<AbyssalCollectible>();
            coreCollectible.Configure(AbyssalCollectibleType.EnergyCore);

            // Cápsula de Extração em Z = 28m
            var podObj = new GameObject("ExtractionPod");
            podObj.transform.position = new Vector3(0f, 3.0f, 28f);
            var extractionPod = podObj.AddComponent<AbyssalExtractionPod>();

            try
            {
                gm.SetPlayerSystems(motor, survival, weapons, extractionPod);
                gm.StartGame();

                // 1. Disparar 3 tiros de rifle contra o Drone Sentinela (3 x 35 = 105 dano >= 100 HP)
                input.SetAiming(true);
                for (int s = 0; s < 3; s++)
                {
                    weapons.TryFirePlasmaRifle();
                    weapons.StepWeapons(0.20f);
                }
                Assert.IsTrue(drone.IsDestroyed, "Drone hostil deve ser destruído após 3 impactos do rifle de plasma.");
                Assert.AreEqual(1, gm.DronesDestroyed, "AbyssalGameManager deve registrar 1 drone neutralizado.");

                // 2. Avançar nadando em Sprint até a Cápsula de Extração (Z = 28m), coletando O2 e Núcleo pelo caminho
                input.SetAiming(false);
                input.SetSprint(true);
                input.SetMoveInput(new Vector2(0f, 1f));

                for (int step = 0; step < 120; step++)
                {
                    gm.StepMissionLoop(0.05f);
                    if (gm.CurrentState == GameState.Victory)
                        break;
                }

                Assert.IsTrue(o2Collectible.IsCollected, "O cilindro de oxigênio no caminho deve ter sido coletado.");
                Assert.IsTrue(coreCollectible.IsCollected, "O núcleo de energia no caminho deve ter sido coletado.");
                Assert.AreEqual(1, gm.CoresCollected, "O contador de núcleos deve ser incrementado.");
                Assert.AreEqual(GameState.Victory, gm.CurrentState, "Ao alcançar a cápsula de extração, o estado deve mudar para Victory.");
                Assert.Greater(gm.Score, 1000, "A pontuação final de extração deve contabilizar núcleo, drone, bônus de O2 e extração.");
            }
            finally
            {
                Object.DestroyImmediate(playerObj);
                Object.DestroyImmediate(droneObj);
                Object.DestroyImmediate(o2Obj);
                Object.DestroyImmediate(coreObj);
                Object.DestroyImmediate(podObj);
                Object.DestroyImmediate(sysObj);
            }
        }

        [Test]
        public void AbyssalVaultAssets_ConvertedModelsAndTextures_ExistInProject()
        {
            string[] requiredModels =
            {
                "Assets/Game/Art/AbyssalBreach/Models/SM_Assault_Rifle_B.fbx",
                "Assets/Game/Art/AbyssalBreach/Models/SM_Tactical_Knife.fbx",
                "Assets/Game/Art/AbyssalBreach/Models/SM_Coral_Brain.fbx",
                "Assets/Game/Art/AbyssalBreach/Models/SM_Coral_TubeCluster.fbx",
                "Assets/Game/Art/AbyssalBreach/Models/SM_Coral_Staghorn.fbx",
                "Assets/Game/Art/AbyssalBreach/Models/SM_Extraction_SubPod.fbx",
                "Assets/Game/Art/AbyssalBreach/Models/SM_SubDrone_Hostile.fbx"
            };

            foreach (var modelPath in requiredModels)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                Assert.IsNotNull(asset, $"Modelo 3D convertido deve existir no projeto Unity: {modelPath}");
            }

            string[] requiredTextures =
            {
                "Assets/Game/Art/AbyssalBreach/Textures/T_Coral_01_D.png",
                "Assets/Game/Art/AbyssalBreach/Textures/T_Assault_Rifle_B_Diff.png",
                "Assets/Game/Art/AbyssalBreach/Textures/T_Knife_01_D.png",
                "Assets/Game/Art/AbyssalBreach/Textures/T_OceanSurface_Caustics.png"
            };

            foreach (var texPath in requiredTextures)
            {
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);
                Assert.IsNotNull(tex, $"Textura PBR extraída do Vault deve existir no projeto Unity: {texPath}");
            }
        }

        #endregion
    }
}
