// ============================================================================
// ETD.UI - GameSpeedController.cs  [NEW]
// Toggles game speed: 1x → 1.5x → 2x → 3x → 1x
// Displays current speed on a button. Uses Time.timeScale.
// Resets to 1x when paused or game over.
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;

namespace ETD.UI
{
    public class GameSpeedController : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] private Button _speedButton;
        [SerializeField] private TMP_Text _speedText;

        [Header("Settings")]
        [SerializeField] private float[] _speedOptions = { 1f, 1.5f, 2f, 3f };

        private int _currentIndex;
        private bool _isPaused;

        private void Awake()
        {
            _currentIndex = 0;

            if (_speedButton != null)
                _speedButton.onClick.AddListener(CycleSpeed);

            EventBus.Subscribe<GamePausedEvent>(OnPaused);
            EventBus.Subscribe<GameOverEvent>(OnGameOver);

            UpdateUI();
        }

        private void CycleSpeed()
        {
            if (_isPaused) return;

            _currentIndex = (_currentIndex + 1) % _speedOptions.Length;
            Time.timeScale = _speedOptions[_currentIndex];
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (_speedText != null)
            {
                float speed = _speedOptions[_currentIndex];
                // Show clean text: "1x", "1.5x", "2x", "3x"
                _speedText.text = speed % 1 == 0
                    ? $"{speed:F0}x"
                    : $"{speed:F1}x";
            }
        }

        /// <summary>
        /// Set a specific speed index (0-based). Called externally if needed.
        /// </summary>
        public void SetSpeed(int index)
        {
            _currentIndex = Mathf.Clamp(index, 0, _speedOptions.Length - 1);
            if (!_isPaused)
                Time.timeScale = _speedOptions[_currentIndex];
            UpdateUI();
        }

        /// <summary>
        /// Reset to 1x speed.
        /// </summary>
        public void ResetSpeed()
        {
            _currentIndex = 0;
            Time.timeScale = 1f;
            UpdateUI();
        }

        public float CurrentSpeed => _speedOptions[_currentIndex];

        private void OnPaused(GamePausedEvent evt)
        {
            //_isPaused = GameManager.Instance.CurrentState == GameState.Paused;

            bool isBlockingModal = GameManager.Instance?.CurrentState == GameState.Paused
                               || GameManager.Instance?.CurrentState == GameState.LevelUp
                               || GameManager.Instance?.CurrentState == GameState.EvolveChoice;

            _isPaused = isBlockingModal;

            if (isBlockingModal)
                Time.timeScale = 0f;
            else
                Time.timeScale = _speedOptions[_currentIndex]; // restore player speed

            //if (_isPaused)
            //{
            //    Time.timeScale = 0f;
            //}
            //else
            //{
            //    // Restore previous speed
            //    Time.timeScale = _speedOptions[_currentIndex];
            //}
        }

        private void OnGameOver(GameOverEvent evt)
        {
            _currentIndex = 0;
            Time.timeScale = 0f; // freeze on game over
            UpdateUI();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<GamePausedEvent>(OnPaused);
            EventBus.Unsubscribe<GameOverEvent>(OnGameOver);

            // Safety: restore time scale
            Time.timeScale = 1f;
        }
    }
}
