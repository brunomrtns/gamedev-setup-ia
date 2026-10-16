using System;
using Studio.Foundation;
using UnityEngine;

namespace Studio.Gameplay
{
    public enum GameState
    {
        StartMenu,
        Playing,
        GameOver,
        Victory
    }

    public class GameManager : MonoBehaviour
    {
        private static GameManager _instance;
        public static GameManager Instance => _instance;

        [Header("References")]
        [SerializeField] private PlayerMovement _playerMovement;

        [Header("State")]
        [SerializeField] private GameState _currentState = GameState.StartMenu;
        [SerializeField] private int _score = 0;
        [SerializeField] private int _energyCellsCollected = 0;
        [SerializeField] private float _distanceCovered = 0f;
        [SerializeField] private float _trackTotalLength = GameConstants.TRACK_TOTAL_LENGTH;

        public GameState CurrentState => _currentState;
        public int Score => _score;
        public int EnergyCellsCollected => _energyCellsCollected;
        public float DistanceCovered => _distanceCovered;
        public float TrackTotalLength => _trackTotalLength;

        public event Action<GameState> OnStateChanged;
        public event Action<int, int, float> OnScoreUpdated; // (cells, score, progressRatio)

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        private void Start()
        {
            SetState(GameState.StartMenu);
            if (_playerMovement != null)
            {
                _playerMovement.SetMovementActive(false);
            }
        }

        private void Update()
        {
            if (_currentState == GameState.Playing && _playerMovement != null)
            {
                _distanceCovered = _playerMovement.transform.position.z;
                _score = LaneMath.CalculateScore(_distanceCovered, _energyCellsCollected);
                float progress = LaneMath.ComputeProgressRatio(_distanceCovered, _trackTotalLength);

                OnScoreUpdated?.Invoke(_energyCellsCollected, _score, progress);
            }
        }

        public void StartGame()
        {
            SetState(GameState.Playing);
            if (_playerMovement != null)
            {
                _playerMovement.SetMovementActive(true);
            }
        }

        public void CollectEnergyCell()
        {
            if (_currentState != GameState.Playing) return;

            _energyCellsCollected++;
            _score = LaneMath.CalculateScore(_distanceCovered, _energyCellsCollected);
            float progress = LaneMath.ComputeProgressRatio(_distanceCovered, _trackTotalLength);

            OnScoreUpdated?.Invoke(_energyCellsCollected, _score, progress);
        }

        public void GameOver()
        {
            if (_currentState != GameState.Playing) return;

            SetState(GameState.GameOver);
            if (_playerMovement != null)
            {
                _playerMovement.SetMovementActive(false);
            }
        }

        public void Victory()
        {
            if (_currentState != GameState.Playing) return;

            SetState(GameState.Victory);
            if (_playerMovement != null)
            {
                _playerMovement.SetMovementActive(false);
            }
        }

        public void RestartGame()
        {
            _score = 0;
            _energyCellsCollected = 0;
            _distanceCovered = 0f;

            // Reset collectibles
            var collectibles = FindObjectsByType<EnergyCellCollectible>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var c in collectibles)
            {
                c.ResetCollectible();
            }

            var portal = FindAnyObjectByType<ExtractionPortal>();
            if (portal != null) portal.ResetPortal();

            if (_playerMovement != null)
            {
                _playerMovement.ResetPosition();
            }

            StartGame();
        }

        public void SetState(GameState newState)
        {
            _currentState = newState;
            OnStateChanged?.Invoke(_currentState);
        }

        public void SetPlayer(PlayerMovement player)
        {
            _playerMovement = player;
        }
    }
}
