using Studio.Input;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Orquestrador leve do NEON DISTRICT: configura clamp de framerate e
    /// sleep-timeout mobile, recoloca o carro na via mais próxima (R/RECENTER)
    /// e mantém referências centrais de cena.
    /// </summary>
    public class NeonDistrictGameManager : MonoBehaviour
    {
        [SerializeField] private PlayerCarController _player;
        [SerializeField] private TrafficNetwork _network;

        public PlayerCarController Player => _player;

        private void Awake()
        {
            // Guarda definitiva: DynamicsManager.asset regrava m_SimulationMode=2
            // (migração de versão do Unity) — sem isso a física NUNCA simula
            // (carro gira mas não anda, impulsos não resolvem). Impõe em runtime.
            Physics.simulationMode = SimulationMode.FixedUpdate;

            // Gestão térmica/bateria (docs/standards/mobile-performance.md)
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.NeverSleep;
        }

        private void Start()
        {
            if (DriveInputAdapter.Instance != null)
            {
                DriveInputAdapter.Instance.OnRecenter += RecenterPlayer;
            }
        }

        private void OnDestroy()
        {
            if (DriveInputAdapter.Instance != null)
            {
                DriveInputAdapter.Instance.OnRecenter -= RecenterPlayer;
            }
        }

        /// <summary>Recoloca o carro na pose de via mais próxima (desencalhar/flip).</summary>
        public void RecenterPlayer()
        {
            if (_player == null || _network == null) return;
            if (_network.TryGetNearestPose(_player.transform.position, out var pos, out var dir))
            {
                _player.ResetToPose(pos + Vector3.up * 0.4f, Quaternion.LookRotation(dir));
            }
        }
    }
}
