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

        [Header("Name Colors")]
        [SerializeField] private Color _defaultNameColor = Color.white;
        [Tooltip("Name colour for the local player's own row, so they can spot themselves.")]
        [SerializeField] private Color _localPlayerNameColor = new Color(1f, 0.84f, 0.2f);
        [SerializeField] private FontStyles _localPlayerFontStyle = FontStyles.Bold;

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
            SetName(entry.PlayerName, entry.IsLocalPlayer);
            SetScore(entry.Score);
            SetAvatar(entry.AvatarTexture);
            SetBackground(highlight ? _playerColor : _defaultColor);
        }

        // =================================================================
        // LOCAL FALLBACK ENTRY
        // =================================================================

        public void SetLocalEntry(int rank, LeaderboardEntry entry)
        {
            SetRank(rank);
            SetName(LocalizationManager.Get("leaderboard_you", "You"), isLocalPlayer: true);
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

            SetName(LocalizationManager.Get("leaderboard_not_ranked_yet", "Not Ranked Yet"), isLocalPlayer: true);
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

        /// <summary>
        /// Sets the name, tinting it when the row belongs to the local player so their
        /// entry is findable at a glance in a long list. The row background already
        /// highlights, but that reads weakly next to the neighbouring rows.
        /// </summary>
        private void SetName(string playerName, bool isLocalPlayer = false)
        {
            if (_nameText == null) return;

            _nameText.text = string.IsNullOrWhiteSpace(playerName) ? _emptyText : playerName;
            _nameText.color = isLocalPlayer ? _localPlayerNameColor : _defaultNameColor;
            _nameText.fontStyle = isLocalPlayer ? _localPlayerFontStyle : FontStyles.Normal;
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
