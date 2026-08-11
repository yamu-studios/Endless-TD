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
using ETD.Data;
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

        [Header("Tooltip")]
        [Tooltip("Optional. Left empty, a TooltipTrigger is added to the button (or this " +
                 "object) at runtime, so the HUD prefab needs no extra wiring.")]
        [SerializeField] private TooltipTrigger _tooltipTrigger;

        private SpellManager _spellManager;

        // Tooltip content is rebuilt only when a displayed value actually changes.
        // Refresh() runs every frame, and the tooltip's cooldown line only moves once a
        // second, so rebuilding per frame would allocate strings for nothing.
        private SpellData _tooltipSpell;
        private int _tooltipSecondsShown = -1;

        private void Awake()
        {
            EnsureTooltipTrigger();
        }

        private void Start()
        {
            _button?.onClick.AddListener(OnClicked);
        }

        /// <summary>
        /// The trigger lives on the button so the hover area matches the clickable area.
        /// A non-interactable Button still receives pointer-enter events, which is what
        /// lets the tooltip explain the cooldown precisely while the spell is unusable.
        /// </summary>
        private void EnsureTooltipTrigger()
        {
            if (_tooltipTrigger != null)
                return;

            GameObject host = _button != null ? _button.gameObject : gameObject;
            _tooltipTrigger = host.GetComponent<TooltipTrigger>();
            if (_tooltipTrigger == null)
                _tooltipTrigger = host.AddComponent<TooltipTrigger>();
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

            RefreshTooltip(remaining, duration);
        }

        private void RefreshTooltip(float remaining, float duration)
        {
            if (_tooltipTrigger == null)
                return;

            SpellData spell = _spellManager.SelectedSpell;
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));

            if (spell == _tooltipSpell && seconds == _tooltipSecondsShown)
                return;

            _tooltipSpell = spell;
            _tooltipSecondsShown = seconds;

            _tooltipTrigger.SetContent(
                TooltipContentBuilder.FromSpellData(spell, duration, remaining));
            _tooltipTrigger.RefreshIfShowing();
        }
    }
}
