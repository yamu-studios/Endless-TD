// ============================================================================
// ETD.UI - UIButtonSound.cs
// Add to any Button to play click/hover sounds via AudioManager.
// Can also be added once to a parent and it auto-registers all child buttons.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ETD.Core;

namespace ETD.UI
{
    public class UIButtonSound : MonoBehaviour
    {
        [Header("Sounds (leave null to use GameSoundConfig defaults)")]
        [SerializeField] private AudioClip _clickSound;
        [SerializeField] private AudioClip _hoverSound;

        [Header("Click")]
        [SerializeField] private float _clickVolume = 1f;
        [SerializeField] private float _clickPitch = 1f;

        [Header("Hover")]
        [SerializeField] private bool _playHoverSound = true;
        [SerializeField] private float _hoverVolume = 0.75f;
        [SerializeField] private float _hoverPitch = 1f;
        [SerializeField] private float _hoverCooldown = 0.05f;

        [Header("Auto-register child buttons")]
        [SerializeField] private bool _includeChildren = true;
        [SerializeField] private bool _includeInactiveButtons = true;
        [SerializeField] private bool _ignoreNonInteractableButtons = true;

        // Compatibility with your old serialized fields.
        [SerializeField, HideInInspector] private float _volume = 1f;
        [SerializeField, HideInInspector] private float _pitch = 1f;

        private readonly HashSet<Button> _registeredButtons = new HashSet<Button>();
        private float _lastHoverTime = -999f;

        private void Awake()
        {
            // Migrate old values if this component already existed in your scene.
            if (_clickVolume <= 0f && _volume > 0f)
                _clickVolume = _volume;

            if (_clickPitch <= 0f && _pitch > 0f)
                _clickPitch = _pitch;

            RegisterButtons();
        }

        private void OnEnable()
        {
            RegisterButtons();
        }

        private void RegisterButtons()
        {
            RegisterButton(GetComponent<Button>());

            if (!_includeChildren)
                return;

            var childButtons = GetComponentsInChildren<Button>(_includeInactiveButtons);
            foreach (var btn in childButtons)
                RegisterButton(btn);
        }

        private void RegisterButton(Button btn)
        {
            if (btn == null || _registeredButtons.Contains(btn))
                return;

            _registeredButtons.Add(btn);
            btn.onClick.AddListener(() => PlayClick(btn));

            var emitter = btn.GetComponent<ButtonSoundEmitter>();
            if (emitter == null)
                emitter = btn.gameObject.AddComponent<ButtonSoundEmitter>();

            emitter.Bind(this, btn);
        }

        private void PlayClick(Button btn)
        {
            if (!CanPlayFor(btn))
                return;

            var clip = _clickSound;

            if (clip == null && GameSoundConfig.Instance != null)
                clip = GameSoundConfig.Instance.ButtonClick;

            PlayUISound(clip, _clickVolume, _clickPitch);
        }

        private void PlayHover(Button btn)
        {
            if (!_playHoverSound || !CanPlayFor(btn))
                return;

            if (_hoverCooldown > 0f && Time.unscaledTime - _lastHoverTime < _hoverCooldown)
                return;

            _lastHoverTime = Time.unscaledTime;

            var clip = _hoverSound;

            if (clip == null && GameSoundConfig.Instance != null)
                clip = GameSoundConfig.Instance.ButtonHover;

            PlayUISound(clip, _hoverVolume, _hoverPitch);
        }

        private bool CanPlayFor(Button btn)
        {
            if (btn == null || !isActiveAndEnabled)
                return false;

            if (_ignoreNonInteractableButtons && !btn.interactable)
                return false;

            return true;
        }

        private static void PlayUISound(AudioClip clip, float volume, float pitch)
        {
            if (clip == null || AudioManager.Instance == null)
                return;

            AudioManager.Instance.PlaySFX(clip, SoundCategory.UI, volume, pitch);
        }

        private sealed class ButtonSoundEmitter : MonoBehaviour, IPointerEnterHandler
        {
            private UIButtonSound _owner;
            private Button _button;

            public void Bind(UIButtonSound owner, Button button)
            {
                _owner = owner;
                _button = button;
            }

            public void OnPointerEnter(PointerEventData eventData)
            {
                _owner?.PlayHover(_button);
            }
        }
    }
}