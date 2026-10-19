using UnityEngine;

namespace Studio.Foundation
{
    /// <summary>
    /// Matemática pura de direção arcade para NEON DISTRICT.
    /// Todas as funções são determinísticas e livres de estado — testáveis em EditMode.
    /// </summary>
    public static class DriveMath
    {
        /// <summary>
        /// Integra a velocidade escalar à frente (m/s) com acelerador, freio e drag.
        /// Freio atua contra o sinal da velocidade; em repouso aplica ré até maxReverse.
        /// </summary>
        public static float IntegrateForwardSpeed(
            float speed, float throttle, float brake,
            float maxSpeed, float maxReverse,
            float accel, float brakeDecel, float drag, float dt)
        {
            if (throttle > 0f)
            {
                speed += throttle * accel * dt;
            }

            if (brake > 0f)
            {
                if (speed > 0.1f)
                {
                    speed -= brake * brakeDecel * dt;
                    if (speed < 0f) speed = 0f;
                }
                else
                {
                    // Ré: pressiona freio parado ou em marcha à ré
                    speed -= brake * accel * 0.85f * dt;
                }
            }

            // Arrasto sempre opõe o movimento
            speed -= speed * drag * dt;

            return Mathf.Clamp(speed, maxReverse, maxSpeed);
        }

        /// <summary>
        /// Fator de atenuação do esterço pela velocidade: esterça mais em baixa,
        /// menos em alta (estabilidade). absSpeed em m/s, refSpeed = velocidade de cruzeiro.
        /// </summary>
        public static float SteeringSpeedFactor(float absSpeed, float refSpeed)
        {
            if (absSpeed < 0.5f) return 0f;
            float t = absSpeed / Mathf.Max(0.01f, refSpeed);
            // Alta resposta em baixa/média (manobras de esquina), amortecimento
            // progressivo acima do cruzeiro (evita rodadas bruscas no asfalto).
            if (t <= 1f) return 1f;
            return Mathf.Lerp(1f, 0.45f, Mathf.Min((t - 1f) * 1.2f, 1f));
        }

        /// <summary>
        /// Taxa de guinada (graus/s) do esterço, com boost em drift.
        /// </summary>
        public static float SteerYawRate(float steerInput, float speed, float maxSteerDegPerSec, float refSpeed, float driftYawBoost, bool drifting)
        {
            float factor = SteeringSpeedFactor(Mathf.Abs(speed), refSpeed);
            float rate = Mathf.Clamp(steerInput, -1f, 1f) * maxSteerDegPerSec * factor;
            if (drifting) rate *= driftYawBoost;
            // Ré inverte o sentido do esterço
            if (speed < 0f) rate = -rate;
            return rate;
        }

        /// <summary>
        /// Coeficiente de amortecimento da velocidade lateral (1/s usado em Exp).
        /// </summary>
        public static float LateralDamping(bool drifting, float grip, float driftGrip)
        {
            return drifting ? driftGrip : grip;
        }

        /// <summary>Ângulo de deslize (graus) entre velocidade e frente do veículo.</summary>
        public static float SlipAngleDeg(Vector3 velocity, Vector3 forward)
        {
            var flat = new Vector3(velocity.x, 0f, velocity.z);
            if (flat.sqrMagnitude < 0.25f) return 0f;
            return Vector3.Angle(new Vector3(forward.x, 0f, forward.z).normalized, flat.normalized);
        }

        /// <summary>m/s → km/h.</summary>
        public static float MsToKmh(float ms) => ms * 3.6f;

        /// <summary>Verdadeiro se |pos - center| está no anel [minR, maxR].</summary>
        public static bool WithinRing(Vector3 pos, Vector3 center, float minR, float maxR)
        {
            float d = Vector3.Distance(pos, center);
            return d >= minR && d <= maxR;
        }

        /// <summary>Envolve ângulo em graus para (-180, 180].</summary>
        public static float WrapAngleDeg(float deg)
        {
            deg %= 360f;
            if (deg > 180f) deg -= 360f;
            if (deg <= -180f) deg += 360f;
            return deg;
        }
    }
}
