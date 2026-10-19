using Studio.Foundation;
using UnityEngine;

namespace Studio.Gameplay
{
    public enum AbyssalCollectibleType
    {
        OxygenTank,
        EnergyCore,
        AmmoCrate
    }

    /// <summary>
    /// Coletáveis subaquáticos na Fenda Mariana-X:
    /// - OxygenTank: Restaura +35 de Oxigênio e +10 de Integridade do Traje.
    /// - EnergyCore: Núcleo de dados/energia de pods naufragados (objetivo de extração).
    /// - AmmoCrate: Munição reserva (+24 células de plasma).
    /// </summary>
    public class AbyssalCollectible : MonoBehaviour
    {
        [SerializeField] private AbyssalCollectibleType _collectibleType = AbyssalCollectibleType.OxygenTank;
        [SerializeField] private float _oxygenRestoreAmount = 35.0f;
        [SerializeField] private int _ammoRestoreAmount = 24;
        [SerializeField] private float _collectionRadius = 2.4f;

        private Vector3 _basePosition;
        private bool _isCollected;

        public AbyssalCollectibleType CollectibleType => _collectibleType;
        public bool IsCollected => _isCollected;

        private void Awake()
        {
            _basePosition = transform.position;
        }

        public void Configure(AbyssalCollectibleType type, float oxygenRestore = 35f, int ammoRestore = 24)
        {
            _collectibleType = type;
            _oxygenRestoreAmount = oxygenRestore;
            _ammoRestoreAmount = ammoRestore;
        }

        private void Update()
        {
            if (_isCollected) return;

            transform.Rotate(0f, 75f * Time.deltaTime, 0f, Space.World);
            float bob = Mathf.Sin(Time.time * 2.5f + _basePosition.z * 0.4f) * 0.14f;
            transform.position = _basePosition + new Vector3(0f, bob, 0f);

            CheckPlayerProximity();
        }

        public void CheckPlayerProximity()
        {
            if (_isCollected) return;
            var gm = AbyssalGameManager.Instance;
            if (gm == null || gm.PlayerMotor == null) return;

            float dist = Vector3.Distance(transform.position, gm.PlayerMotor.transform.position);
            if (dist <= _collectionRadius)
            {
                Collect(gm.PlayerMotor.gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (_isCollected) return;
            if (other.GetComponentInParent<AbyssalFirstPersonMotor>() != null)
            {
                Collect(other.GetComponentInParent<AbyssalFirstPersonMotor>().gameObject);
            }
        }

        public bool Collect(GameObject collector)
        {
            if (_isCollected || collector == null) return false;

            _isCollected = true;
            gameObject.SetActive(false);

            var survival = collector.GetComponent<OxygenSurvivalSystem>();
            var weapons = collector.GetComponent<AbyssalWeaponController>();
            var gm = AbyssalGameManager.Instance;

            switch (_collectibleType)
            {
                case AbyssalCollectibleType.OxygenTank:
                    if (survival != null)
                    {
                        survival.AddOxygen(_oxygenRestoreAmount);
                        survival.HealSuit(10f);
                    }
                    if (gm != null) gm.RegisterOxygenTankCollected();
                    break;

                case AbyssalCollectibleType.EnergyCore:
                    ProceduralAudioService.Instance?.PlayCollect();
                    if (gm != null) gm.RegisterEnergyCoreCollected();
                    break;

                case AbyssalCollectibleType.AmmoCrate:
                    ProceduralAudioService.Instance?.PlayReload();
                    if (weapons != null) weapons.AddReserveAmmo(_ammoRestoreAmount);
                    break;
            }

            return true;
        }

        public void ResetCollectible()
        {
            _isCollected = false;
            transform.position = _basePosition;
            gameObject.SetActive(true);
        }
    }
}
