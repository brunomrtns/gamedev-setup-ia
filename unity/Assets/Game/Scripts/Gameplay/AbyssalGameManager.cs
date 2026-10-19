using System;
using Studio.Foundation;
using Studio.Input;
using UnityEngine;

namespace Studio.Gameplay
{
    /// <summary>
    /// Gerenciador Central de Partida e Missão para "Abyssal Breach" (Jogo 2).
    /// Coordena os estados StartMenu, Playing, Victory (Extraction Complete) e GameOver (Hypoxia/Suit Breach),
    /// integrando o motor FPS subaquático, sistema de oxigênio, armas, coletáveis e cápsula de extração.
    /// </summary>
    public class AbyssalGameManager : MonoBehaviour
    {
        private static AbyssalGameManager _instance;
        public static AbyssalGameManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindAnyObjectByType<AbyssalGameManager>();
                }
                return _instance;
            }
        }

        [Header("Player Systems")]
        [SerializeField] private AbyssalFirstPersonMotor _playerMotor;
        [SerializeField] private OxygenSurvivalSystem _survivalSystem;
        [SerializeField] private AbyssalWeaponController _weaponController;
        [SerializeField] private AbyssalExtractionPod _extractionPod;

        [Header("Mission Telemetry")]
        [SerializeField] private GameState _currentState = GameState.StartMenu;
        [SerializeField] private int _coresCollected;
        [SerializeField] private int _totalCoresInMission = 3;
        [SerializeField] private int _oxygenTanksCollected;
        [SerializeField] private int _dronesDestroyed;
        [SerializeField] private int _score;

        public GameState CurrentState => _currentState;
        public AbyssalFirstPersonMotor PlayerMotor => _playerMotor;
        public OxygenSurvivalSystem SurvivalSystem => _survivalSystem;
        public AbyssalWeaponController WeaponController => _weaponController;
        public AbyssalExtractionPod ExtractionPod => _extractionPod;

        public int CoresCollected => _coresCollected;
        public int TotalCoresInMission => _totalCoresInMission;
        public int OxygenTanksCollected => _oxygenTanksCollected;
        public int DronesDestroyed => _dronesDestroyed;
        public int Score => _score;

        public event Action<GameState> OnStateChanged;
        public event Action OnTelemetryUpdated;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            EnsureReferences();
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
            }
        }

        private void Start()
        {
            EnsureReferences();
            var adapter = AbyssalInputAdapter.Instance;
            if (adapter != null)
            {
                adapter.OnRestart += RestartMission;
            }
            SetState(GameState.StartMenu);
            if (_playerMotor != null) _playerMotor.SetMovementActive(false);
            if (_survivalSystem != null) _survivalSystem.SetSurvivalActive(false);
            NotifyTelemetry();
        }

        private void Update()
        {
            if (_currentState == GameState.Playing)
            {
                StepMissionLoop(0f);
            }
        }

        public void EnsureReferences()
        {
            if (_playerMotor == null) _playerMotor = FindAnyObjectByType<AbyssalFirstPersonMotor>();
            if (_playerMotor != null)
            {
                if (_survivalSystem == null) _survivalSystem = _playerMotor.GetComponent<OxygenSurvivalSystem>();
                if (_weaponController == null) _weaponController = _playerMotor.GetComponent<AbyssalWeaponController>();
            }
            if (_extractionPod == null) _extractionPod = FindAnyObjectByType<AbyssalExtractionPod>();
        }

        public void SetPlayerSystems(
            AbyssalFirstPersonMotor motor,
            OxygenSurvivalSystem survival,
            AbyssalWeaponController weapons,
            AbyssalExtractionPod pod = null)
        {
            _playerMotor = motor;
            _survivalSystem = survival;
            _weaponController = weapons;
            if (pod != null) _extractionPod = pod;
        }

        /// <summary>
        /// Avança de maneira determinística todos os subsistemas da missão (Motor FPS, Sobrevivência O2, Armas, Coletáveis e Extração).
        /// </summary>
        public void StepMissionLoop(float deltaTime)
        {
            EnsureReferences();
            if (_currentState != GameState.Playing) return;

            if (deltaTime > 0f)
            {
                if (_playerMotor != null)
                {
                    _playerMotor.StepSimulation(deltaTime);
                }
                if (_survivalSystem != null)
                {
                    bool sprinting = _playerMotor != null && _playerMotor.IsSprinting;
                    _survivalSystem.StepSurvival(deltaTime, sprinting);
                }
                if (_weaponController != null)
                {
                    _weaponController.StepWeapons(deltaTime);
                }
            }

            // Verificar proximidade de coletáveis e cápsula de extração
            var collectibles = FindObjectsByType<AbyssalCollectible>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            foreach (var c in collectibles)
            {
                if (c != null && !c.IsCollected)
                {
                    c.CheckPlayerProximity();
                }
            }

            if (_extractionPod != null && !_extractionPod.IsExtracted)
            {
                _extractionPod.CheckPlayerDocking();
            }

            RecalculateScore(extracted: _currentState == GameState.Victory);
            NotifyTelemetry();
        }

        public void StartGame()
        {
            EnsureReferences();
            SetState(GameState.Playing);
            if (_playerMotor != null) _playerMotor.SetMovementActive(true);
            if (_survivalSystem != null) _survivalSystem.SetSurvivalActive(true);
            NotifyTelemetry();
        }

        public void RegisterEnergyCoreCollected()
        {
            _coresCollected++;
            RecalculateScore(false);
            NotifyTelemetry();
        }

        public void RegisterOxygenTankCollected()
        {
            _oxygenTanksCollected++;
            RecalculateScore(false);
            NotifyTelemetry();
        }

        public void RegisterDroneDestroyed()
        {
            _dronesDestroyed++;
            RecalculateScore(false);
            NotifyTelemetry();
        }

        public void CompleteExtraction()
        {
            if (_currentState != GameState.Playing) return;

            RecalculateScore(extracted: true);
            SetState(GameState.Victory);
            if (_playerMotor != null) _playerMotor.SetMovementActive(false);
            if (_survivalSystem != null) _survivalSystem.SetSurvivalActive(false);
            NotifyTelemetry();
        }

        public void GameOver()
        {
            if (_currentState != GameState.Playing) return;

            RecalculateScore(extracted: false);
            SetState(GameState.GameOver);
            if (_playerMotor != null) _playerMotor.SetMovementActive(false);
            if (_survivalSystem != null) _survivalSystem.SetSurvivalActive(false);
            NotifyTelemetry();
        }

        public void RestartMission()
        {
            EnsureReferences();
            _coresCollected = 0;
            _oxygenTanksCollected = 0;
            _dronesDestroyed = 0;
            _score = 0;

            if (_playerMotor != null) _playerMotor.ResetPosition();
            if (_survivalSystem != null) _survivalSystem.ResetVitals();
            if (_weaponController != null) _weaponController.ResetWeapons();
            if (_extractionPod != null) _extractionPod.ResetPod();

            var collectibles = FindObjectsByType<AbyssalCollectible>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in collectibles) c.ResetCollectible();

            var drones = FindObjectsByType<AbyssalThreatDrone>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var d in drones) d.ResetDrone();

            StartGame();
        }

        public void SetState(GameState state)
        {
            _currentState = state;
            OnStateChanged?.Invoke(_currentState);
        }

        private void RecalculateScore(bool extracted)
        {
            float remO2 = _survivalSystem != null ? _survivalSystem.CurrentOxygen : 0f;
            _score = AbyssalMath.ComputeExtractionScore(_coresCollected, _dronesDestroyed, remO2, extracted);
        }

        private void NotifyTelemetry()
        {
            OnTelemetryUpdated?.Invoke();
        }
    }
}
