using Studio.Foundation;
using UnityEngine;

namespace Studio.Gameplay
{
    public class EnergyCellCollectible : MonoBehaviour
    {
        [SerializeField] private float _rotationSpeed = 120.0f;
        [SerializeField] private float _bobFrequency = 3.0f;
        [SerializeField] private float _bobAmplitude = 0.15f;

        private Vector3 _basePosition;
        private float _timer = 0f;
        private bool _isCollected = false;

        private void Start()
        {
            _basePosition = transform.position;
            // Deslocamento de fase aleatório para não sincronizar perfeitamente
            _timer = (transform.position.z * 0.1f) % (Mathf.PI * 2f);
        }

        private void Update()
        {
            if (_isCollected) return;

            float dt = Time.deltaTime;
            _timer += dt * _bobFrequency;

            // Rotação contínua
            transform.Rotate(0f, _rotationSpeed * dt, 0f, Space.World);

            // Flutuação vertical suave
            Vector3 pos = _basePosition;
            pos.y += Mathf.Sin(_timer) * _bobAmplitude;
            transform.position = pos;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isCollected) return;

            if (other.CompareTag(GameConstants.TAG_PLAYER) || other.GetComponentInParent<PlayerMovement>() != null)
            {
                _isCollected = true;

                if (ProceduralAudioService.Instance != null)
                {
                    ProceduralAudioService.Instance.PlayCollect();
                }

                if (GameManager.Instance != null)
                {
                    GameManager.Instance.CollectEnergyCell();
                }

                // Desativa o coletável
                gameObject.SetActive(false);
            }
        }

        public void ResetCollectible()
        {
            _isCollected = false;
            gameObject.SetActive(true);
        }
    }
}
