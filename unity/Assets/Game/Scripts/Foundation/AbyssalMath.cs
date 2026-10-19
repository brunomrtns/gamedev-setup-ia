using UnityEngine;

namespace Studio.Foundation
{
    /// <summary>
    /// Funções matemáticas puras e determinísticas para o Jogo 2: "Abyssal Breach"
    /// (Movimentação Hidrodinâmica Subaquática, Consumo de Oxigênio/Hipóxia, Balística ADS e Telemetria).
    /// </summary>
    public static class AbyssalMath
    {
        public const float DEFAULT_MAX_OXYGEN = 100.0f;
        public const float DEFAULT_MAX_HEALTH = 100.0f;
        public const float NORMAL_OXYGEN_DRAIN_PER_SEC = 1.5f;
        public const float SPRINT_OXYGEN_DRAIN_PER_SEC = 3.2f;
        public const float HYPOXIA_DAMAGE_PER_SEC = 12.0f;

        public const float DEFAULT_FOV = 65.0f;
        public const float ADS_FOV = 46.0f;
        public const float HIPFIRE_SPREAD_DEG = 2.4f;
        public const float ADS_SPREAD_DEG = 0.25f;

        public const float BASE_OCEAN_DEPTH_METERS = -245.0f;
        public const int REQUIRED_CORES_FOR_BONUS = 3;

        /// <summary>
        /// Calcula o novo nível de oxigênio após um intervalo deltaTime, considerando nado normal vs propulsão rápida (Sprint).
        /// </summary>
        public static float ComputeOxygenAfterStep(
            float currentOxygen,
            float maxOxygen,
            bool isSprinting,
            float normalDrainRate,
            float sprintDrainRate,
            float deltaTime)
        {
            if (deltaTime <= 0f) return Mathf.Clamp(currentOxygen, 0f, maxOxygen);
            float drainRate = isSprinting ? sprintDrainRate : normalDrainRate;
            float nextOxygen = currentOxygen - (drainRate * deltaTime);
            return Mathf.Clamp(nextOxygen, 0f, maxOxygen);
        }

        /// <summary>
        /// Calcula o dano de hipóxia (asfixia subaquática) quando o medidor de oxigênio está zerado.
        /// </summary>
        public static float ComputeHypoxiaDamage(float currentOxygen, float hypoxiaDamageRate, float deltaTime)
        {
            if (currentOxygen > 0.0001f || deltaTime <= 0f)
            {
                return 0f;
            }
            return Mathf.Max(0f, hypoxiaDamageRate * deltaTime);
        }

        /// <summary>
        /// Calcula o ângulo de dispersão balística (em graus) para o rifle de plasma submarino.
        /// Em modo ADS (mira óptica), a dispersão é drasticamente reduzida.
        /// </summary>
        public static float ComputeBallisticSpreadDegrees(
            bool isAimingADS,
            float hipSpreadDegrees = HIPFIRE_SPREAD_DEG,
            float adsSpreadDegrees = ADS_SPREAD_DEG,
            float recoilPenaltyDegrees = 0f)
        {
            float baseSpread = isAimingADS ? adsSpreadDegrees : hipSpreadDegrees;
            float recoilFactor = isAimingADS ? 0.35f : 1.0f;
            return Mathf.Clamp(baseSpread + Mathf.Max(0f, recoilPenaltyDegrees) * recoilFactor, 0f, 15.0f);
        }

        /// <summary>
        /// Retorna o Field of View (FOV) alvo da câmera de acordo com o estado de mira (ADS).
        /// </summary>
        public static float ComputeTargetCameraFov(
            bool isAimingADS,
            float defaultFov = DEFAULT_FOV,
            float adsFov = ADS_FOV)
        {
            return isAimingADS ? adsFov : defaultFov;
        }

        /// <summary>
        /// Calcula a velocidade hidrodinâmica 3D com aceleração direcional e arrasto viscoso da água.
        /// </summary>
        public static Vector3 ComputeUnderwaterVelocity(
            Vector3 currentVelocity,
            Vector3 wishDirection,
            float targetSpeed,
            float acceleration,
            float waterDrag,
            float deltaTime)
        {
            if (deltaTime <= 0f) return currentVelocity;

            Vector3 clampedWish = wishDirection.sqrMagnitude > 1f ? wishDirection.normalized : wishDirection;
            Vector3 desiredVelocity = clampedWish * Mathf.Max(0f, targetSpeed);

            if (clampedWish.sqrMagnitude > 0.0001f)
            {
                return Vector3.MoveTowards(currentVelocity, desiredVelocity, acceleration * deltaTime);
            }
            else
            {
                return Vector3.MoveTowards(currentVelocity, Vector3.zero, waterDrag * deltaTime);
            }
        }

        /// <summary>
        /// Calcula o deslocamento vertical de flutuabilidade hidrostática passiva (buoyancy).
        /// </summary>
        public static float ComputeBuoyancyVerticalVelocity(float timeSeconds, float amplitude = 0.35f, float frequency = 1.6f)
        {
            return Mathf.Sin(timeSeconds * frequency) * amplitude;
        }

        /// <summary>
        /// Calcula a profundidade oceânica exibida no HUD do capacete (em metros negativos abaixo da superfície).
        /// </summary>
        public static float ComputeDepthMeters(float worldY, float baseDepthMeters = BASE_OCEAN_DEPTH_METERS)
        {
            return baseDepthMeters + worldY;
        }

        /// <summary>
        /// Calcula o azimute da bússola tática [0, 360) a partir do vetor forward horizontal.
        /// </summary>
        public static float ComputeCompassHeadingDegrees(Vector3 forward)
        {
            Vector3 flat = new Vector3(forward.x, 0f, forward.z);
            if (flat.sqrMagnitude < 0.0001f) return 0f;
            float angle = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            if (angle < 0f) angle += 360f;
            return angle;
        }

        /// <summary>
        /// Calcula a pontuação tática total da operação subaquática.
        /// </summary>
        public static int ComputeExtractionScore(int coresCollected, int dronesDestroyed, float remainingOxygen, bool extracted)
        {
            int corePoints = Mathf.Max(0, coresCollected) * 250;
            int dronePoints = Mathf.Max(0, dronesDestroyed) * 300;
            int oxygenBonus = extracted ? Mathf.RoundToInt(Mathf.Clamp(remainingOxygen, 0f, 100f) * 5f) : 0;
            int extractionBonus = extracted ? 500 : 0;
            return corePoints + dronePoints + oxygenBonus + extractionBonus;
        }
    }
}
