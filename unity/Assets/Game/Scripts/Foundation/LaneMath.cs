using UnityEngine;

namespace Studio.Foundation
{
    public static class LaneMath
    {
        public static float GetLaneX(int laneIndex)
        {
            // laneIndex: -1 (Left), 0 (Center), +1 (Right)
            int clamped = Mathf.Clamp(laneIndex, -1, 1);
            return clamped * GameConstants.LANE_WIDTH;
        }

        public static int ClampLane(int laneIndex)
        {
            return Mathf.Clamp(laneIndex, -1, 1);
        }

        public static float ComputeParabolicJumpHeight(float normalizedTime, float maxHeight)
        {
            // normalizedTime in [0, 1]
            float t = Mathf.Clamp01(normalizedTime);
            // Parabola: 4 * t * (1 - t) reaches maximum of 1 at t = 0.5
            return 4.0f * t * (1.0f - t) * maxHeight;
        }

        public static int CalculateScore(float distanceCovered, int cellsCollected)
        {
            int distancePoints = Mathf.Max(0, Mathf.FloorToInt(distanceCovered)) * GameConstants.POINTS_PER_METER;
            int cellPoints = Mathf.Max(0, cellsCollected) * GameConstants.ENERGY_CELL_SCORE;
            return distancePoints + cellPoints;
        }

        public static float ComputeProgressRatio(float currentZ, float totalLength)
        {
            if (totalLength <= 0.001f) return 1.0f;
            return Mathf.Clamp01(currentZ / totalLength);
        }
    }
}
