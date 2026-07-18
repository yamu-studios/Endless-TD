// ============================================================================
// ETD.UI - SpellCastButton.cs  [NEW]
// v1.0 active spell system in-run HUD button (see [[etd-v1-full-release]]
// Phase 4). Binds directly to SpellManager.TryCast()/CooldownRemaining —
// backend was already fully wired, this was the last missing piece. Hidden
// entirely when no spell is selected (fresh saves / pre-v1.0 saves default to "").
// ============================================================================
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;
using ETD.Gameplay;

namespace ETD.UI
{
    public class SpellCastButton : MonoBehaviour
    {
        [Tooltip("Root object hidden entirely when no spell is selected. Leave empty to hide this GameObject instead.")]
        [SerializeField] private GameObject _root;
        [SerializeField] private Button _button;
        [SerializeField] private Image _icon;
        [Tooltip("Optional: an Image with Image Type = Filled, driven as a cooldown radial. Shown only while on cooldown.")]
        [SerializeField] private Image _cooldownFill;
        [SerializeField] private TMP_Text _cooldownText;

        private SpellManager _spellManager;

        private void Start()
        {
            _button?.onClick.AddListener(OnClicked);
        }

        private void OnEnable()
        {
            ServiceLocator.TryGet(out _spellManager);
            Refresh();
        }

        private void Update()
        {
            if (_spellManager == null)
                ServiceLocator.TryGet(out _spellManager);

            Refresh();
        }

        private void OnClicked()
        {
            _spellManager?.TryCast();
        }

        private void Refresh()
        {
            bool hasSpell = _spellManager != null && _spellManager.SelectedSpell != null;

            var rootObj = _root != null ? _root : gameObject;
            if (rootObj.activeSelf != hasSpell)
                rootObj.SetActive(hasSpell);

            if (!hasSpell) return;

            if (_icon != null)
                _icon.sprite = _spellManager.SelectedSpell.Icon;

            bool ready = _spellManager.IsReady;
            if (_button != null)
                _button.interactable = ready;

            float duration = _spellManager.CooldownDuration;
            float remaining = _spellManager.CooldownRemaining;
            float fill = duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;

            if (_cooldownFill != null)
            {
                _cooldownFill.gameObject.SetActive(!ready);
                _cooldownFill.fillAmount = fill;
            }

            if (_cooldownText != null)
                _cooldownText.text = ready ? "" : Mathf.CeilToInt(remaining).ToString();
        }
    }
}
