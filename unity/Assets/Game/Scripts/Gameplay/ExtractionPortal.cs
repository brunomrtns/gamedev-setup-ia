using Studio.Foundation;
using UnityEngine;

namespace Studio.Gameplay
{
    public class ExtractionPortal : MonoBehaviour
    {
        [SerializeField] private Transform _outerRing;
        [SerializeField] private Transform _innerRing;
        [SerializeField] private float _outerRingSpeed = 40.0f;
        [SerializeField] private float _innerRingSpeed = -70.0f;

        private bool _triggered = false;

        private void Update()
        {
            float dt = Time.deltaTime;
            if (_outerRing != null) _outerRing.Rotate(0f, 0f, _outerRingSpeed * dt);
            if (_innerRing != null) _innerRing.Rotate(0f, 0f, _innerRingSpeed * dt);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;

            if (other.CompareTag(GameConstants.TAG_PLAYER) || other.GetComponentInParent<PlayerMovement>() != null)
            {
                _triggered = true;

                if (ProceduralAudioService.Instance != null)
                {
                    ProceduralAudioService.Instance.PlayVictory();
                }

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.Victory();
                }
            }
        }

        public void ResetPortal()
        {
            _triggered = false;
        }
    }
}
