using Studio.Gameplay;
using Studio.Input;
using UnityEngine;
using UnityEngine.UI;

namespace Studio.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject _startPanel;
        [SerializeField] private GameObject _hudPanel;
        [SerializeField] private GameObject _gameOverPanel;
        [SerializeField] private GameObject _victoryPanel;

        [Header("HUD Elements")]
        [SerializeField] private Text _cellsText;
        [SerializeField] private Text _scoreText;
        [SerializeField] private Slider _progressBar;

        [Header("Game Over Elements")]
        [SerializeField] private Text _gameOverScoreText;
        [SerializeField] private Text _gameOverCellsText;

        [Header("Victory Elements")]
        [SerializeField] private Text _victoryScoreText;
        [SerializeField] private Text _victoryCellsText;

        [Header("Buttons")]
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _victoryRestartButton;

        [Header("On-Screen Mobile Controls")]
        [SerializeField] private Button _btnLeft;
        [SerializeField] private Button _btnRight;
        [SerializeField] private Button _btnJump;
        [SerializeField] private Button _btnSlide;

        private void Start()
        {
            if (_startButton != null)
                _startButton.onClick.AddListener(OnStartClicked);

            if (_retryButton != null)
                _retryButton.onClick.AddListener(OnRestartClicked);

            if (_victoryRestartButton != null)
                _victoryRestartButton.onClick.AddListener(OnRestartClicked);

            // Hook mobile buttons to input adapter
            if (_btnLeft != null)
                _btnLeft.onClick.AddListener(() => NeonInputAdapter.Instance?.TriggerMoveLeft());

            if (_btnRight != null)
                _btnRight.onClick.AddListener(() => NeonInputAdapter.Instance?.TriggerMoveRight());

            if (_btnJump != null)
                _btnJump.onClick.AddListener(() => NeonInputAdapter.Instance?.TriggerJump());

            if (_btnSlide != null)
                _btnSlide.onClick.AddListener(() => NeonInputAdapter.Instance?.TriggerSlide());

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged += HandleStateChanged;
                GameManager.Instance.OnScoreUpdated += HandleScoreUpdated;
                HandleStateChanged(GameManager.Instance.CurrentState);
            }
        }

        private void OnDestroy()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnStateChanged -= HandleStateChanged;
                GameManager.Instance.OnScoreUpdated -= HandleScoreUpdated;
            }
        }

        private void Update()
        {
            if (GameManager.Instance == null) return;

            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null)
            {
                if (GameManager.Instance.CurrentState == GameState.StartMenu)
                {
                    if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                    {
                        OnStartClicked();
                    }
                }
                else if (GameManager.Instance.CurrentState == GameState.GameOver || GameManager.Instance.CurrentState == GameState.Victory)
                {
                    if (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.rKey.wasPressedThisFrame)
                    {
                        OnRestartClicked();
                    }
                }
            }
        }

        private void HandleStateChanged(GameState state)
        {
            if (_startPanel != null) _startPanel.SetActive(state == GameState.StartMenu);
            if (_hudPanel != null) _hudPanel.SetActive(state == GameState.Playing);
            if (_gameOverPanel != null) _gameOverPanel.SetActive(state == GameState.GameOver);
            if (_victoryPanel != null) _victoryPanel.SetActive(state == GameState.Victory);

            if (state == GameState.GameOver && GameManager.Instance != null)
            {
                if (_gameOverScoreText != null) _gameOverScoreText.text = $"SCORE: {GameManager.Instance.Score}";
                if (_gameOverCellsText != null) _gameOverCellsText.text = $"CELLS: {GameManager.Instance.EnergyCellsCollected}";
            }
            else if (state == GameState.Victory && GameManager.Instance != null)
            {
                if (_victoryScoreText != null) _victoryScoreText.text = $"FINAL SCORE: {GameManager.Instance.Score}";
                if (_victoryCellsText != null) _victoryCellsText.text = $"ENERGY CELLS: {GameManager.Instance.EnergyCellsCollected}";
            }
        }

        private void HandleScoreUpdated(int cells, int score, float progressRatio)
        {
            if (_cellsText != null) _cellsText.text = $"CELLS: {cells}";
            if (_scoreText != null) _scoreText.text = $"SCORE: {score}";
            if (_progressBar != null) _progressBar.value = progressRatio;
        }

        private void OnStartClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.StartGame();
            }
        }

        private void OnRestartClicked()
        {
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
            }
        }
    }
}
