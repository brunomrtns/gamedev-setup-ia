using Studio.Gameplay;
using Studio.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.UI
{
    /// <summary>
    /// HUD minimalista do NEON DISTRICT: velocímetro km/h, FPS counter (debug)
    /// e indicador de drift. Atualiza textos em cadência fixa (~10 Hz) para
    /// minimizar alocação por frame.
    /// </summary>
    public class NeonDistrictHUD : MonoBehaviour
    {
        [SerializeField] private PlayerCarController _player;
        [SerializeField] private Ak47Weapon _weapon;
        [SerializeField] private Text _speedText;
        [SerializeField] private Text _fpsText;
        [SerializeField] private GameObject _driftIndicator;
        [SerializeField] private Text _ammoText;
        [SerializeField] private Text _cansText;
        [SerializeField] private Text _healthText;
        [SerializeField] private GameObject _crosshair;
        [SerializeField] private GameObject _driveControlsGroup;
        [SerializeField] private GameObject _footControlsGroup;
        [SerializeField] private float _refreshInterval = 0.1f;

        private float _refreshTimer;
        private float _fpsSmoothed = 60f;
        private readonly string[] _kmhCache = new string[301];
        private bool _cacheInit;
        private bool _showPos;
        private OnFootMotor _motor;

        private void Awake()
        {
            for (int i = 0; i < _kmhCache.Length; i++) _kmhCache[i] = i.ToString() + " km/h";
            _cacheInit = true;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _fpsSmoothed = Mathf.Lerp(_fpsSmoothed, 1f / Mathf.Max(dt, 0.0001f), 5f * dt);

            if (UnityEngine.Input.GetKeyDown(KeyCode.F10)) _showPos = !_showPos;

            if (_driftIndicator != null && _player != null)
            {
                _driftIndicator.SetActive(_player.IsDrifting);
            }

            // Modo a pé: crosshair + alterna grupos de controles touch
            var mgr = NeonDistrictModeManager.Instance;
            bool onFoot = mgr != null && mgr.Current == NeonDistrictModeManager.Mode.OnFoot;
            if (_crosshair != null)
            {
                bool ads = _weapon != null && _weapon.IsAds;
                _crosshair.SetActive(onFoot && !ads); // em ADS, a mira da arma é o centro
                if (onFoot && !ads)
                {
                    // abre com movimento/tiro, fecha parado — feedback de precisão
                    float spread = 1f;
                    if (_motor == null) _motor = FindAnyObjectByType<OnFootMotor>();
                    if (_motor != null) spread += _motor.SpeedNorm * 1.2f;
                    if (_weapon != null && DriveInputAdapter.Instance != null && DriveInputAdapter.Instance.FireHeld) spread += 0.8f;
                    _crosshair.transform.localScale = Vector3.Lerp(
                        _crosshair.transform.localScale, Vector3.one * spread, dt * 8f);
                }
            }
            if (_driveControlsGroup != null && _driveControlsGroup.activeSelf == onFoot)
                _driveControlsGroup.SetActive(!onFoot);
            if (_footControlsGroup != null && _footControlsGroup.activeSelf != onFoot)
                _footControlsGroup.SetActive(onFoot);

            _refreshTimer -= dt;
            if (_refreshTimer > 0f) return;
            _refreshTimer = _refreshInterval;

            if (_speedText != null && _player != null)
            {
                int kmh = Mathf.Clamp(Mathf.RoundToInt(_player.SpeedKmh), 0, _kmhCache.Length - 1);
                _speedText.text = _cacheInit ? _kmhCache[kmh] : kmh + " km/h";
            }
            if (_fpsText != null)
            {
                string fps = Mathf.RoundToInt(_fpsSmoothed) + " fps";
                if (_showPos)
                {
                    Vector3 pos = onFoot && _motor != null ? _motor.transform.position
                        : _player != null ? _player.transform.position : Vector3.zero;
                    fps += $" | {pos.x:F1},{pos.z:F1}";
                }
                _fpsText.text = fps;
            }
            if (_ammoText != null && _weapon != null)
            {
                _ammoText.text = _weapon.IsReloading ? "RELOAD" : _weapon.Ammo + "/" + _weapon.Reserve;
            }
            if (_cansText != null && mgr != null)
            {
                _cansText.text = "CANS: " + mgr.CansHit + "/" + mgr.CansTotal;
            }
            if (_healthText != null)
            {
                var ph = PlayerHealth.Instance;
                if (ph != null)
                {
                    int hp = Mathf.CeilToInt(ph.Health);
                    _healthText.text = "HP " + hp;
                    _healthText.color = hp > 60 ? new Color(0.3f, 1f, 0.6f)
                        : hp > 30 ? new Color(1f, 0.8f, 0.2f)
                        : new Color(1f, 0.25f, 0.2f);
                }
            }
        }

        public void SetPlayer(PlayerCarController player) => _player = player;

        /// <summary>Wiring direto do modo a pé (chamado pelo builder).</summary>
        public void BindOnFoot(Ak47Weapon weapon, Text ammo, Text cans,
            GameObject crosshair, GameObject driveGroup, GameObject footGroup)
        {
            _weapon = weapon;
            _ammoText = ammo;
            _cansText = cans;
            _crosshair = crosshair;
            _driveControlsGroup = driveGroup;
            _footControlsGroup = footGroup;
        }

        /// <summary>Wiring do texto de vida (chamado pelo builder).</summary>
        public void BindHealth(Text health) => _healthText = health;
    }
}
