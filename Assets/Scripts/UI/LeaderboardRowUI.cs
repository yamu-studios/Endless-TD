using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;

namespace ETD.UI
{
    public class LeaderboardRowUI : MonoBehaviour
    {
        [Header("Fields")]
        [SerializeField] private TMP_Text _rankText;
        [SerializeField] private RawImage _avatarImage;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _scoreText;

        [Header("Row Styling")]
        [SerializeField] private Image _rowBackground;
        [SerializeField] private Color _defaultColor = new Color(0.12f, 0.12f, 0.14f, 0.9f);
        [SerializeField] private Color _playerColor = new Color(0.15f, 0.35f, 0.55f, 0.95f);
        [SerializeField] private Color _separatorColor = new Color(0.5f, 0.4f, 0.1f, 0.9f);

        [Header("Default Avatar")]
        [SerializeField] private Texture2D _defaultAvatar;

        [Header("Rank Colors")]
        [SerializeField] private Color _goldColor = new Color(1f, 0.84f, 0f);
        [SerializeField] private Color _silverColor = new Color(0.75f, 0.75f, 0.75f);
        [SerializeField] private Color _bronzeColor = new Color(0.8f, 0.5f, 0.2f);
        [SerializeField] private Color _defaultRankColor = Color.white;

        [Header("Empty Text")]
        [SerializeField] private string _emptyText = "—";

        // =================================================================
        // UNIFIED ENTRY
        // =================================================================

        public void SetEntry(LeaderboardEntry entry, bool isPlayerRow = false)
        {
            if (entry == null)
            {
                SetNotRanked();
                return;
            }

            bool highlight = entry.IsLocalPlayer || isPlayerRow;

            SetRank(entry.Rank, isSeparator: isPlayerRow && !entry.IsLocalPlayer);
            SetName(entry.PlayerName);
            SetScore(entry.Score);
            SetAvatar(entry.AvatarTexture);
            SetBackground(highlight ? _playerColor : _defaultColor);
        }

        // =================================================================
        // STEAM ENTRY
        // =================================================================

        public void SetSteamEntry(SteamLeaderboardEntry entry, bool isPlayerRow = false)
        {
            bool highlight = entry.IsLocalPlayer || isPlayerRow;

            SetRank(entry.Rank, isSeparator: isPlayerRow && !entry.IsLocalPlayer);
            SetName(entry.PlayerName);
            SetScore(entry.Score);
            SetAvatar(entry.Avatar);
            SetBackground(highlight ? _playerColor : _defaultColor);
        }

        // =================================================================
        // LOCAL FALLBACK ENTRY
        // =================================================================

        public void SetLocalEntry(int rank, LeaderboardEntry entry)
        {
            SetRank(rank);
            SetName(LocalizationManager.Get("leaderboard_you", "You"));
            SetScore(entry.SurvivedWave);
            SetAvatar(null);
            SetBackground(_defaultColor);
        }

        // =================================================================
        // EMPTY / NOT RANKED
        // =================================================================

        public void SetEmpty(int rank)
        {
            SetRank(rank);
            SetName(_emptyText);
            SetScore(0);
            SetAvatar(null);
            SetBackground(_defaultColor);
        }

        public void SetNotRanked()
        {
            if (_rankText != null)
            {
                _rankText.text = "?";
                _rankText.color = _defaultRankColor;
            }

            SetName(LocalizationManager.Get("leaderboard_not_ranked_yet", "Not Ranked Yet"));
            SetScore(0);
            SetAvatar(null);
            SetBackground(_playerColor);
        }

        // =================================================================
        // HELPERS
        // =================================================================

        private void SetRank(int rank, bool isSeparator = false)
        {
            if (_rankText == null) return;

            _rankText.text = $"#{rank}";
            _rankText.color = isSeparator ? _separatorColor
                : rank == 1 ? _goldColor
                : rank == 2 ? _silverColor
                : rank == 3 ? _bronzeColor
                : _defaultRankColor;
        }

        private void SetName(string playerName)
        {
            if (_nameText != null)
                _nameText.text = string.IsNullOrWhiteSpace(playerName) ? _emptyText : playerName;
        }

        private void SetScore(int score)
        {
            if (_scoreText != null)
                _scoreText.text = score > 0 ? score.ToString() : _emptyText;
        }

        private void SetAvatar(Texture2D tex)
        {
            if (_avatarImage == null) return;
            _avatarImage.texture = tex != null ? tex : _defaultAvatar;
        }

        private void SetBackground(Color color)
        {
            if (_rowBackground != null) _rowBackground.color = color;
        }
    }
}
