using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;

namespace ETD.Hub
{
    // =========================================================================
    // LEADERBOARD — always visible in hub, shows top 10 scores + meta currency
    // =========================================================================
    public class LeaderboardUI : MonoBehaviour
    {
        [SerializeField] private Transform _entryContainer;
        [SerializeField] private GameObject _entryPrefab;
        [SerializeField] private TMP_Text _metaCurrencyText;

        private void OnEnable()
        {
            Refresh();
        }

        public void Refresh()
        {
            var save = SaveSystem.Load();

            if (_metaCurrencyText != null)
                _metaCurrencyText.text = save.MetaCurrency.ToString();

            if (_entryContainer != null)
            {
                foreach (Transform child in _entryContainer)
                    Destroy(child.gameObject);

                for (int i = 0; i < save.TopScores.Length; i++)
                {
                    if (save.TopScores[i] == 0) continue;
                    var go = Instantiate(_entryPrefab, _entryContainer);
                    var text = go.GetComponent<TMP_Text>();
                    if (text != null)
                        text.text = $"#{i + 1}  {FormatScore(save.TopScores[i])}";
                }
            }
        }

        private static string FormatScore(int n)
        {
            if (n >= 1000000) return $"{n / 1000000f:F1}M";
            if (n >= 1000) return $"{n / 1000f:F1}K";
            return n.ToString();
        }
    }

   
}
