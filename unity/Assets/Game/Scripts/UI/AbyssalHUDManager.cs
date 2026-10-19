using Studio.Foundation;
using Studio.Gameplay;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.UI
{
    /// <summary>
    /// Interface Tática do Capacete de Mergulho (Diving Helmet HUD) para "Abyssal Breach".
    /// Exibe oxigênio (O2), integridade do traje (HP), munição e estado da arma (ADS/Reload/Knife),
    /// bússola, profundidade oceânica, núcleos recuperados e retículo de mira dinâmico.
    /// </summary>
    public class AbyssalHUDManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject _startPanel;
        [SerializeField] private GameObject _hudPanel;
        [SerializeField] private GameObject _victoryPanel;
        [SerializeField] private GameObject _gameOverPanel;

        [Header("Helmet Telemetry Texts")]
        [SerializeField] private Text _oxygenText;
        [SerializeField] private Image _oxygenFillBar;
        [SerializeField] private Text _healthText;
        [SerializeField] private Image _healthFillBar;
        [SerializeField] private Text _ammoText;
        [SerializeField] private Text _weaponModeText;
        [SerializeField] private Text _depthCompassText;
        [SerializeField] private Text _missionCoresText;
        [SerializeField] private Text _reticleCrosshairText;
        [SerializeField] private Text _victorySummaryText;
        [SerializeField] private Text _gameOverReasonText;

        private AbyssalGameManager _gameManager;
        private float _hitMarkerTimer;

        private void Start()
        {
            BindToGameManager();
            RefreshAllUI();
        }

        public void BindToGameManager()
        {
            _gameManager = AbyssalGameManager.Instance;
            if (_gameManager != null)
            {
                _gameManager.OnStateChanged -= HandleStateChanged;
                _gameManager.OnStateChanged += HandleStateChanged;
                _gameManager.OnTelemetryUpdated -= RefreshAllUI;
                _gameManager.OnTelemetryUpdated += RefreshAllUI;

                if (_gameManager.WeaponController != null)
                {
                    _gameManager.WeaponController.OnTargetHit -= HandleTargetHit;
                    _gameManager.WeaponController.OnTargetHit += HandleTargetHit;
                }
            }
        }

        public void ConfigureReferences(
            GameObject startPanel,
            GameObject hudPanel,
            GameObject victoryPanel,
            GameObject gameOverPanel,
            Text oxygenText,
            Image oxygenFillBar,
            Text healthText,
            Image healthFillBar,
            Text ammoText,
            Text weaponModeText,
            Text depthCompassText,
            Text missionCoresText,
            Text reticleCrosshairText,
            Text victorySummaryText,
            Text gameOverReasonText)
        {
            _startPanel = startPanel;
            _hudPanel = hudPanel;
            _victoryPanel = victoryPanel;
            _gameOverPanel = gameOverPanel;
            _oxygenText = oxygenText;
            _oxygenFillBar = oxygenFillBar;
            _healthText = healthText;
            _healthFillBar = healthFillBar;
            _ammoText = ammoText;
            _weaponModeText = weaponModeText;
            _depthCompassText = depthCompassText;
            _missionCoresText = missionCoresText;
            _reticleCrosshairText = reticleCrosshairText;
            _victorySummaryText = victorySummaryText;
            _gameOverReasonText = gameOverReasonText;
        }

        private void Update()
        {
            if (_hitMarkerTimer > 0f)
            {
                _hitMarkerTimer = Mathf.Max(0f, _hitMarkerTimer - Time.deltaTime);
            }
            RefreshAllUI();
        }

        private void HandleTargetHit(bool destroyed)
        {
            _hitMarkerTimer = 0.22f;
            RefreshAllUI();
        }

        private void HandleStateChanged(GameState state)
        {
            if (_startPanel != null) _startPanel.SetActive(state == GameState.StartMenu);
            if (_hudPanel != null) _hudPanel.SetActive(state == GameState.Playing);
            if (_victoryPanel != null) _victoryPanel.SetActive(state == GameState.Victory);
            if (_gameOverPanel != null) _gameOverPanel.SetActive(state == GameState.GameOver);
            RefreshAllUI();
        }

        public void RefreshAllUI()
        {
            if (_gameManager == null) _gameManager = AbyssalGameManager.Instance;
            if (_gameManager == null) return;

            var survival = _gameManager.SurvivalSystem;
            var weapons = _gameManager.WeaponController;
            var motor = _gameManager.PlayerMotor;

            if (survival != null)
            {
                if (_oxygenText != null)
                {
                    string warn = survival.IsLowOxygen ? " [CRITICAL LOW]" : "";
                    _oxygenText.text = $"O2 SUPPLY: {survival.CurrentOxygen:0}%{warn}";
                    _oxygenText.color = survival.IsLowOxygen ? new Color(1f, 0.25f, 0.25f) : new Color(0f, 0.95f, 1f);
                }
                if (_oxygenFillBar != null)
                {
                    _oxygenFillBar.fillAmount = survival.OxygenRatio;
                }

                if (_healthText != null)
                {
                    _healthText.text = $"SUIT INTEGRITY: {survival.CurrentHealth:0}%";
                }
                if (_healthFillBar != null)
                {
                    _healthFillBar.fillAmount = survival.HealthRatio;
                }
            }

            if (weapons != null)
            {
                if (_ammoText != null)
                {
                    if (weapons.ActiveSlot == AbyssalWeaponSlot.PlasmaRifle)
                    {
                        _ammoText.text = $"AMMO: {weapons.CurrentMagazineAmmo:00} / {weapons.ReserveAmmo:000}";
                    }
                    else
                    {
                        _ammoText.text = "MELEE: TACTICAL BLADE READY";
                    }
                }

                if (_weaponModeText != null)
                {
                    string slotName = weapons.ActiveSlot == AbyssalWeaponSlot.PlasmaRifle
                        ? "PLASMA RIFLE [MK-IV]"
                        : "TACTICAL DIVE KNIFE";
                    string modeTag = weapons.IsReloading
                        ? "RELOADING..."
                        : (weapons.IsAimingADS ? "OPTICAL ADS [ZOOM 1.4x]" : "HIP-FIRE / SWIM");
                    _weaponModeText.text = $"{slotName}  |  {modeTag}";
                }

                if (_reticleCrosshairText != null)
                {
                    if (_hitMarkerTimer > 0f)
                    {
                        _reticleCrosshairText.text = "X";
                        _reticleCrosshairText.color = new Color(1f, 0.3f, 0.2f, 1f);
                    }
                    else if (weapons.IsAimingADS)
                    {
                        _reticleCrosshairText.text = "[ + ]";
                        _reticleCrosshairText.color = new Color(0.15f, 1f, 0.65f, 0.95f);
                    }
                    else
                    {
                        _reticleCrosshairText.text = "(  +  )";
                        _reticleCrosshairText.color = new Color(0f, 0.95f, 1f, 0.85f);
                    }
                }
            }

            if (motor != null && _depthCompassText != null)
            {
                float depth = motor.CurrentDepthMeters;
                float hdg = motor.CurrentHeadingDegrees;
                float distToPod = _gameManager.ExtractionPod != null
                    ? Vector3.Distance(motor.transform.position, _gameManager.ExtractionPod.transform.position)
                    : 0f;
                _depthCompassText.text = $"DEPTH: {depth:0.0}m   |   HDG: {hdg:000}°   |   EXTRACTION POD: {distToPod:0}m";
            }

            if (_missionCoresText != null)
            {
                _missionCoresText.text = $"DATA CORES: {_gameManager.CoresCollected}/{_gameManager.TotalCoresInMission}   |   DRONES NEUTRALIZED: {_gameManager.DronesDestroyed}   |   SCORE: {_gameManager.Score:0000}";
            }

            if (_victorySummaryText != null && _gameManager.CurrentState == GameState.Victory)
            {
                _victorySummaryText.text = $"EXTRACTION COMPLETE — SUB-POD DOCKED\nDATA CORES: {_gameManager.CoresCollected}/{_gameManager.TotalCoresInMission}  |  DRONES CLEARED: {_gameManager.DronesDestroyed}  |  FINAL SCORE: {_gameManager.Score}";
            }

            if (_gameOverReasonText != null && _gameManager.CurrentState == GameState.GameOver)
            {
                bool hypoxia = survival != null && survival.IsHypoxic;
                _gameOverReasonText.text = hypoxia
                    ? "CAUSE: CRITICAL HYPOXIA (OXYGEN DEPLETED IN TRENCH)"
                    : "CAUSE: DIVING SUIT BREACH (HULL INTEGRITY FAILED)";
            }
        }
    }
}
