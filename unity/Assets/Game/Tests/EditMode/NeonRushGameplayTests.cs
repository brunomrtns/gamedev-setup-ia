using NUnit.Framework;
using Studio.Foundation;
using Studio.Gameplay;
using Studio.Input;
using UnityEngine;

namespace Studio.Tests.EditMode
{
    [TestFixture]
    public class NeonRushGameplayTests
    {
        [Test]
        public void LaneCalculator_ChangeLeftFromCenter_ReturnsLeftLanePosition()
        {
            int targetLane = -1;
            float laneX = LaneMath.GetLaneX(targetLane);
            Assert.AreEqual(GameConstants.LANE_LEFT_X, laneX, 0.001f);
        }

        [Test]
        public void LaneCalculator_ChangeLeftFromLeftmost_ClampsToLeftLane()
        {
            int invalidLeftLane = -2;
            int clampedLane = LaneMath.ClampLane(invalidLeftLane);
            float laneX = LaneMath.GetLaneX(invalidLeftLane);
            Assert.AreEqual(-1, clampedLane);
            Assert.AreEqual(GameConstants.LANE_LEFT_X, laneX, 0.001f);
        }

        [Test]
        public void LaneCalculator_ChangeRightFromCenter_ReturnsRightLanePosition()
        {
            int targetLane = 1;
            float laneX = LaneMath.GetLaneX(targetLane);
            Assert.AreEqual(GameConstants.LANE_RIGHT_X, laneX, 0.001f);
        }

        [Test]
        public void LaneCalculator_ClampRightmost_ClampsToRightLane()
        {
            int invalidRightLane = 3;
            int clampedLane = LaneMath.ClampLane(invalidRightLane);
            float laneX = LaneMath.GetLaneX(invalidRightLane);
            Assert.AreEqual(1, clampedLane);
            Assert.AreEqual(GameConstants.LANE_RIGHT_X, laneX, 0.001f);
        }

        [Test]
        public void ScoreCalculator_AddEnergyCell_IncreasesScoreByHundred()
        {
            int score = LaneMath.CalculateScore(0f, 3);
            Assert.AreEqual(300, score);
        }

        [Test]
        public void ScoreCalculator_DistanceTraveled_ComputesTenPointsPerMeter()
        {
            int score = LaneMath.CalculateScore(50f, 0);
            Assert.AreEqual(500, score);
        }

        [Test]
        public void ScoreCalculator_CombinedDistanceAndCells_ReturnsCorrectTotal()
        {
            int score = LaneMath.CalculateScore(120.5f, 4);
            Assert.AreEqual(1600, score);
        }

        [Test]
        public void JumpTrajectory_AtHalfDuration_ReachesApexHeight()
        {
            float height = LaneMath.ComputeParabolicJumpHeight(0.5f, 2.2f);
            Assert.AreEqual(2.2f, height, 0.001f);
        }

        [Test]
        public void JumpTrajectory_AtStartAndLanding_ReturnsZeroHeight()
        {
            float startHeight = LaneMath.ComputeParabolicJumpHeight(0.0f, 2.2f);
            float landingHeight = LaneMath.ComputeParabolicJumpHeight(1.0f, 2.2f);
            Assert.AreEqual(0.0f, startHeight, 0.001f);
            Assert.AreEqual(0.0f, landingHeight, 0.001f);
        }

        [Test]
        public void ProgressRatio_ClampsToZeroAndOne()
        {
            float totalLength = 350.0f;
            Assert.AreEqual(0.0f, LaneMath.ComputeProgressRatio(-10f, totalLength), 0.001f);
            Assert.AreEqual(0.5f, LaneMath.ComputeProgressRatio(175f, totalLength), 0.001f);
            Assert.AreEqual(1.0f, LaneMath.ComputeProgressRatio(360f, totalLength), 0.001f);
        }

        [Test]
        public void Gameplay_InputAdapterSubscribedEvenWhenPlayerCreatedBeforeSystems()
        {
            // Reproduz exatamente a condição em que o Player era instanciado antes do _GameSystems na cena
            var playerObj = new GameObject("TestPlayer");
            var col = playerObj.AddComponent<BoxCollider>();
            col.size = new Vector3(1.2f, 1.4f, 1.2f);
            col.center = new Vector3(0f, 0.7f, 0f);
            var mov = playerObj.AddComponent<PlayerMovement>();
            var ctrl = playerObj.AddComponent<PlayerController>();

            var sysObj = new GameObject("TestSystems");
            var gm = sysObj.AddComponent<GameManager>();
            var input = sysObj.AddComponent<NeonInputAdapter>();

            try
            {
                gm.SetPlayer(mov);
                gm.StartGame();
                ctrl.StepController(0.016f);

                Assert.IsTrue(ctrl.IsSubscribedToInput, "PlayerController deve assinar o NeonInputAdapter mesmo quando criado antes na cena.");
                Assert.IsTrue(mov.CanMove, "PlayerMovement deve estar ativo após StartGame().");

                input.TriggerMoveLeft();
                Assert.AreEqual(-1, mov.CurrentLane, "Comando MoveLeft deve alterar a faixa do jogador para -1.");
            }
            finally
            {
                Object.DestroyImmediate(playerObj);
                Object.DestroyImmediate(sysObj);
            }
        }

        [Test]
        public void Gameplay_MoveLeftAndRightCommands_ShiftLanesAndBankCharacter()
        {
            var sysObj = new GameObject("TestSystems");
            var input = sysObj.AddComponent<NeonInputAdapter>();
            var gm = sysObj.AddComponent<GameManager>();

            var playerObj = new GameObject("TestPlayer");
            var col = playerObj.AddComponent<BoxCollider>();
            col.size = new Vector3(1.2f, 1.4f, 1.2f);
            var mov = playerObj.AddComponent<PlayerMovement>();
            var ctrl = playerObj.AddComponent<PlayerController>();
            var visualRoot = new GameObject("VisualRoot");
            visualRoot.transform.SetParent(playerObj.transform);
            var anim = playerObj.AddComponent<RobotAnimator>();
            anim.Initialize(visualRoot.transform);

            try
            {
                gm.SetPlayer(mov);
                gm.StartGame();
                ctrl.EnsureInputSubscribed();

                // 1. Comando Esquerda (Centro 0 -> Esquerda -1)
                input.TriggerMoveLeft();
                Assert.AreEqual(-1, mov.CurrentLane);
                for (int i = 0; i < 15; i++)
                {
                    mov.StepSimulation(0.016f);
                    ctrl.StepController(0.016f);
                }
                Assert.AreEqual(GameConstants.LANE_LEFT_X, playerObj.transform.position.x, 0.05f, "Jogador deve alcançar a coordenada X da faixa esquerda.");
                Assert.Greater(playerObj.transform.position.z, 2.0f, "Jogador deve avançar continuamente para frente (+Z).");

                // 2. Comando Direita 2x (Esquerda -1 -> Centro 0 -> Direita +1)
                input.TriggerMoveRight();
                Assert.AreEqual(0, mov.CurrentLane);
                input.TriggerMoveRight();
                Assert.AreEqual(1, mov.CurrentLane);
                for (int i = 0; i < 25; i++)
                {
                    mov.StepSimulation(0.016f);
                    ctrl.StepController(0.016f);
                }
                Assert.AreEqual(GameConstants.LANE_RIGHT_X, playerObj.transform.position.x, 0.05f, "Jogador deve alcançar a coordenada X da faixa direita.");
            }
            finally
            {
                Object.DestroyImmediate(playerObj);
                Object.DestroyImmediate(sysObj);
            }
        }

        [Test]
        public void Gameplay_JumpCommand_ElevatesPlayerOverLowLaserAndLandsCleanly()
        {
            var sysObj = new GameObject("TestSystems");
            var input = sysObj.AddComponent<NeonInputAdapter>();
            var gm = sysObj.AddComponent<GameManager>();

            var playerObj = new GameObject("TestPlayer");
            var mov = playerObj.AddComponent<PlayerMovement>();
            var ctrl = playerObj.AddComponent<PlayerController>();

            try
            {
                gm.SetPlayer(mov);
                gm.StartGame();
                ctrl.EnsureInputSubscribed();

                // Acionar comando de pulo
                input.TriggerJump();
                Assert.IsTrue(mov.IsJumping, "Comando TriggerJump deve iniciar o estado IsJumping.");

                // Avançar até o ápice do salto (~0.325s)
                mov.StepSimulation(GameConstants.JUMP_DURATION * 0.5f);
                Assert.Greater(playerObj.transform.position.y, 2.0f, "No ápice do salto, a altura Y deve superar 2.0m (acima do LowLaser de 0.6m).");

                // Concluir duração do salto e verificar retorno ao solo Y=0
                mov.StepSimulation(GameConstants.JUMP_DURATION * 0.6f);
                Assert.IsFalse(mov.IsJumping, "Após a duração do salto, IsJumping deve voltar para false.");
                Assert.AreEqual(0.0f, playerObj.transform.position.y, 0.01f, "Jogador deve aterrissar de volta em Y=0.");
            }
            finally
            {
                Object.DestroyImmediate(playerObj);
                Object.DestroyImmediate(sysObj);
            }
        }

        [Test]
        public void Gameplay_SlideCommand_ShrinksColliderUnderHighBeamAndRestoresAfterDuration()
        {
            var sysObj = new GameObject("TestSystems");
            var input = sysObj.AddComponent<NeonInputAdapter>();
            var gm = sysObj.AddComponent<GameManager>();

            var playerObj = new GameObject("TestPlayer");
            var col = playerObj.AddComponent<BoxCollider>();
            col.size = new Vector3(1.2f, 1.4f, 1.2f);
            col.center = new Vector3(0f, 0.7f, 0f);
            var mov = playerObj.AddComponent<PlayerMovement>();
            var ctrl = playerObj.AddComponent<PlayerController>();

            try
            {
                gm.SetPlayer(mov);
                gm.StartGame();
                ctrl.EnsureInputSubscribed();

                // Acionar comando de slide/agachamento
                input.TriggerSlide();
                Assert.IsTrue(mov.IsSliding, "Comando TriggerSlide deve iniciar o estado IsSliding.");
                Assert.Less(col.size.y, 0.70f, "Durante o Slide, o BoxCollider deve reduzir sua altura para 45% (0.63m) para passar sob vigas altas.");

                // Avançar além da duração do slide
                mov.StepSimulation(GameConstants.SLIDE_DURATION + 0.05f);
                Assert.IsFalse(mov.IsSliding, "Após a duração do slide, IsSliding deve voltar para false.");
                Assert.AreEqual(1.4f, col.size.y, 0.01f, "O BoxCollider deve restaurar sua altura original de 1.4m após o slide.");
            }
            finally
            {
                Object.DestroyImmediate(playerObj);
                Object.DestroyImmediate(sysObj);
            }
        }

        [Test]
        public void Gameplay_FirstMovementCommandOnStartMenu_AutomaticallyStartsGameAndExecutesMove()
        {
            var sysObj = new GameObject("TestSystems");
            var input = sysObj.AddComponent<NeonInputAdapter>();
            var gm = sysObj.AddComponent<GameManager>();

            var playerObj = new GameObject("TestPlayer");
            var mov = playerObj.AddComponent<PlayerMovement>();
            var ctrl = playerObj.AddComponent<PlayerController>();

            try
            {
                gm.SetPlayer(mov);
                gm.SetState(GameState.StartMenu);
                mov.SetMovementActive(false);
                ctrl.EnsureInputSubscribed();

                // Mesmo no StartMenu sem clicar no botão Start, pressionar Esquerda deve iniciar a partida e mudar de faixa
                input.TriggerMoveLeft();

                Assert.AreEqual(GameState.Playing, gm.CurrentState, "O primeiro comando de movimento no StartMenu deve iniciar o jogo automaticamente.");
                Assert.IsTrue(mov.CanMove, "PlayerMovement deve ficar ativo imediatamente.");
                Assert.AreEqual(-1, mov.CurrentLane, "O comando de movimento acionado deve ser executado imediatamente.");
            }
            finally
            {
                Object.DestroyImmediate(playerObj);
                Object.DestroyImmediate(sysObj);
            }
        }
    }
}
