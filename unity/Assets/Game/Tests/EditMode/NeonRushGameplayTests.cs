using NUnit.Framework;
using Studio.Foundation;
using UnityEngine;

namespace Studio.Tests.EditMode
{
    [TestFixture]
    public class NeonRushGameplayTests
    {
        [Test]
        public void LaneCalculator_ChangeLeftFromCenter_ReturnsLeftLanePosition()
        {
            // Arrange
            int targetLane = -1;

            // Act
            float laneX = LaneMath.GetLaneX(targetLane);

            // Assert
            Assert.AreEqual(GameConstants.LANE_LEFT_X, laneX, 0.001f);
        }

        [Test]
        public void LaneCalculator_ChangeLeftFromLeftmost_ClampsToLeftLane()
        {
            // Arrange
            int invalidLeftLane = -2;

            // Act
            int clampedLane = LaneMath.ClampLane(invalidLeftLane);
            float laneX = LaneMath.GetLaneX(invalidLeftLane);

            // Assert
            Assert.AreEqual(-1, clampedLane);
            Assert.AreEqual(GameConstants.LANE_LEFT_X, laneX, 0.001f);
        }

        [Test]
        public void LaneCalculator_ChangeRightFromCenter_ReturnsRightLanePosition()
        {
            // Arrange
            int targetLane = 1;

            // Act
            float laneX = LaneMath.GetLaneX(targetLane);

            // Assert
            Assert.AreEqual(GameConstants.LANE_RIGHT_X, laneX, 0.001f);
        }

        [Test]
        public void LaneCalculator_ClampRightmost_ClampsToRightLane()
        {
            // Arrange
            int invalidRightLane = 3;

            // Act
            int clampedLane = LaneMath.ClampLane(invalidRightLane);
            float laneX = LaneMath.GetLaneX(invalidRightLane);

            // Assert
            Assert.AreEqual(1, clampedLane);
            Assert.AreEqual(GameConstants.LANE_RIGHT_X, laneX, 0.001f);
        }

        [Test]
        public void ScoreCalculator_AddEnergyCell_IncreasesScoreByHundred()
        {
            // Arrange
            float distance = 0f;
            int cells = 3;

            // Act
            int score = LaneMath.CalculateScore(distance, cells);

            // Assert
            Assert.AreEqual(300, score);
        }

        [Test]
        public void ScoreCalculator_DistanceTraveled_ComputesTenPointsPerMeter()
        {
            // Arrange
            float distance = 50f;
            int cells = 0;

            // Act
            int score = LaneMath.CalculateScore(distance, cells);

            // Assert
            Assert.AreEqual(500, score);
        }

        [Test]
        public void ScoreCalculator_CombinedDistanceAndCells_ReturnsCorrectTotal()
        {
            // Arrange
            float distance = 120.5f;
            int cells = 4;

            // Act
            int score = LaneMath.CalculateScore(distance, cells);

            // Assert (120m * 10 = 1200 + 4 * 100 = 400 -> 1600)
            Assert.AreEqual(1600, score);
        }

        [Test]
        public void JumpTrajectory_AtHalfDuration_ReachesApexHeight()
        {
            // Arrange
            float normalizedTime = 0.5f;
            float maxHeight = 2.2f;

            // Act
            float height = LaneMath.ComputeParabolicJumpHeight(normalizedTime, maxHeight);

            // Assert (4 * 0.5 * 0.5 * 2.2 = 2.2)
            Assert.AreEqual(maxHeight, height, 0.001f);
        }

        [Test]
        public void JumpTrajectory_AtStartAndLanding_ReturnsZeroHeight()
        {
            // Act
            float startHeight = LaneMath.ComputeParabolicJumpHeight(0.0f, 2.2f);
            float landingHeight = LaneMath.ComputeParabolicJumpHeight(1.0f, 2.2f);

            // Assert
            Assert.AreEqual(0.0f, startHeight, 0.001f);
            Assert.AreEqual(0.0f, landingHeight, 0.001f);
        }

        [Test]
        public void ProgressRatio_ClampsToZeroAndOne()
        {
            // Arrange
            float totalLength = 350.0f;

            // Act & Assert
            Assert.AreEqual(0.0f, LaneMath.ComputeProgressRatio(-10f, totalLength), 0.001f);
            Assert.AreEqual(0.5f, LaneMath.ComputeProgressRatio(175f, totalLength), 0.001f);
            Assert.AreEqual(1.0f, LaneMath.ComputeProgressRatio(360f, totalLength), 0.001f);
        }
    }
}
