using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Spec de desempenho de um veículo: torque, teto, freio, esterço e
    /// curvas de atrito. Um preset por categoria de carro — aplicado ao
    /// PrometeoCarController e aos WheelColliders. Para calibrar a frota,
    /// ajuste os valores aqui, não espalhados pelo builder.
    /// </summary>
    [System.Serializable]
    public class CarTuning
    {
        public string Name;
        public int AccelerationMultiplier;   // torque/roda = mult × 50 N·m
        public int MaxSpeedKmh;
        public int MaxReverseSpeedKmh;
        public int MaxSteeringDeg;
        public float SteeringSpeed;
        public int BrakeForce;               // brakeTorque por roda
        public int HandbrakeDriftMultiplier;
        public int DecelerationMultiplier;
        public float BodyMassKg;
        public float SidewaysFrontStiffness;
        public float SidewaysRearStiffness;

        public void ApplyTo(PrometeoCarController p)
        {
            p.accelerationMultiplier = AccelerationMultiplier;
            p.maxSpeed = MaxSpeedKmh;
            p.maxReverseSpeed = MaxReverseSpeedKmh;
            p.maxSteeringAngle = MaxSteeringDeg;
            p.steeringSpeed = SteeringSpeed;
            p.brakeForce = BrakeForce;
            p.handbrakeDriftMultiplier = HandbrakeDriftMultiplier;
            p.decelerationMultiplier = DecelerationMultiplier;
            foreach (var wc in p.GetComponentsInChildren<WheelCollider>())
            {
                var fw = wc.forwardFriction;
                fw.extremumSlip = 0.4f; fw.extremumValue = 1.0f;
                fw.asymptoteSlip = 0.8f; fw.asymptoteValue = 0.75f;
                fw.stiffness = 2.0f;
                wc.forwardFriction = fw;
                bool front = wc.name.Contains("_FL") || wc.name.Contains("_FR");
                var sw = wc.sidewaysFriction;
                sw.extremumSlip = front ? 0.25f : 0.28f;
                sw.extremumValue = front ? 1.1f : 1.05f;
                sw.asymptoteSlip = front ? 0.5f : 0.55f;
                sw.asymptoteValue = front ? 0.85f : 0.80f;
                sw.stiffness = front ? SidewaysFrontStiffness : SidewaysRearStiffness;
                wc.sidewaysFriction = sw;
            }
        }
    }

    /// <summary>Presets por categoria de veículo — frota futura.</summary>
    public static class CarTunings
    {
        /// <summary>Super carro do jogador — 900 N·m/roda, arcade GTA.</summary>
        public static readonly CarTuning SuperCar = new()
        {
            Name = "SuperCar",
            AccelerationMultiplier = 18,   // 900 N·m por roda
            MaxSpeedKmh = 140,
            MaxReverseSpeedKmh = 45,
            MaxSteeringDeg = 32,
            SteeringSpeed = 0.85f,
            BrakeForce = 2400,             // ~7.5 m/s²
            HandbrakeDriftMultiplier = 4,
            DecelerationMultiplier = 4,
            BodyMassKg = 1350f,
            SidewaysFrontStiffness = 2.6f,
            SidewaysRearStiffness = 2.4f,
        };

        /// <summary>Sedan civil — torque moderado, freio comum.</summary>
        public static readonly CarTuning Sedan = new()
        {
            Name = "Sedan",
            AccelerationMultiplier = 9,    // 450 N·m por roda
            MaxSpeedKmh = 110,
            MaxReverseSpeedKmh = 35,
            MaxSteeringDeg = 30,
            SteeringSpeed = 0.7f,
            BrakeForce = 2000,
            HandbrakeDriftMultiplier = 3,
            DecelerationMultiplier = 3,
            BodyMassKg = 1500f,
            SidewaysFrontStiffness = 2.4f,
            SidewaysRearStiffness = 2.2f,
        };

        /// <summary>Utilitário/caminhonete — torque baixo, massa alta.</summary>
        public static readonly CarTuning Utility = new()
        {
            Name = "Utility",
            AccelerationMultiplier = 7,    // 350 N·m por roda
            MaxSpeedKmh = 90,
            MaxReverseSpeedKmh = 30,
            MaxSteeringDeg = 28,
            SteeringSpeed = 0.6f,
            BrakeForce = 1800,
            HandbrakeDriftMultiplier = 2,
            DecelerationMultiplier = 3,
            BodyMassKg = 2200f,
            SidewaysFrontStiffness = 2.2f,
            SidewaysRearStiffness = 2.0f,
        };
    }
}
