using Studio.Foundation;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Cápsula Submarina de Extração (Sub-Pod): objetivo final da operação na fenda oceânica.
    /// Quando o mergulhador entra no raio de acoplamento, conclui a extração com sucesso.
    /// </summary>
    public class AbyssalExtractionPod : MonoBehaviour
    {
        [SerializeField] private float _dockingRadius = 4.2f;
        [SerializeField] private Transform _beaconRing;

        private bool _isExtracted;
        public bool IsExtracted => _isExtracted;
        public float DockingRadius => _dockingRadius;

        private void Update()
        {
            if (_beaconRing != null)
            {
                _beaconRing.Rotate(0f, 65f * Time.deltaTime, 0f, Space.Self);
            }

            CheckPlayerDocking();
        }

        public void CheckPlayerDocking()
        {
            if (_isExtracted) return;
            var gm = AbyssalGameManager.Instance;
            if (gm == null || gm.CurrentState != GameState.Playing || gm.PlayerMotor == null) return;

            float dist = Vector3.Distance(transform.position, gm.PlayerMotor.transform.position);
            if (dist <= _dockingRadius)
            {
                TriggerExtraction();
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isExtracted) return;
            if (other.GetComponentInParent<AbyssalFirstPersonMotor>() != null)
            {
                TriggerExtraction();
            }
        }

        public void TriggerExtraction()
        {
            if (_isExtracted) return;
            _isExtracted = true;
            ProceduralAudioService.Instance?.PlayVictory();
            AbyssalGameManager.Instance?.CompleteExtraction();
        }

        public void ResetPod()
        {
            _isExtracted = false;
        }
    }
}
