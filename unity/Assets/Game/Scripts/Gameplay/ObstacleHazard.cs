using Studio.Foundation;
using UnityEngine;

namespace Studio.Gameplay
{
    public enum HazardType
    {
        LowLaser,   // Exige pulo (Jump)
        HighBeam,   // Exige deslize (Slide)
        FullBlock   // Exige desvio lateral (Dodge)
    }

    public class ObstacleHazard : MonoBehaviour
    {
        [SerializeField] private HazardType _hazardType = HazardType.FullBlock;

        public HazardType Type => _hazardType;

        public void SetHazardType(HazardType type)
        {
            _hazardType = type;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag(GameConstants.TAG_PLAYER) || other.GetComponentInParent<PlayerMovement>() != null)
            {
                var player = other.GetComponentInParent<PlayerController>();
                var movement = other.GetComponentInParent<PlayerMovement>();

                if (movement == null || !movement.CanMove) return;

                bool safe = false;

                switch (_hazardType)
                {
                    case HazardType.LowLaser:
                        // Seguro se o jogador estiver saltando a uma altura mínima
                        if (movement.IsJumping && movement.transform.position.y > 0.6f)
                        {
                            safe = true;
                        }
                        break;

                    case HazardType.HighBeam:
                        // Seguro se o jogador estiver deslizando
                        if (movement.IsSliding)
                        {
                            safe = true;
                        }
                        break;

                    case HazardType.FullBlock:
                    default:
                        // Barricada total é intransponível nesta faixa
                        safe = false;
                        break;
                }

                if (!safe)
                {
                    if (player != null)
                    {
                        player.HandleCollisionWithHazard();
                    }

                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.GameOver();
                    }
                }
            }
        }
    }
}
