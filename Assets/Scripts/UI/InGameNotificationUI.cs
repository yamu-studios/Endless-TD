// ============================================================================
// ETD.UI - InGameNotificationUI.cs  [NEW]
// Slide-in notification banners when challenges/traits/turrets unlock.
// Shows in game scene. Queues multiple notifications. Auto-dismisses.
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ETD.Core;

namespace ETD.UI
{
    public class InGameNotificationUI : MonoBehaviour
    {
        [Header("Banner")]
        [SerializeField] private GameObject _bannerPanel;
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _descriptionText;
        [SerializeField] private Image _bannerBackground;
        [SerializeField] private Image _iconImage;

        [Header("Animation")]
        [SerializeField] private float _slideInDuration = 0.3f;
        [SerializeField] private float _displayDuration = 3f;
        [SerializeField] private float _slideOutDuration = 0.3f;

        [Header("Colors by type")]
        [SerializeField] private Color _challengeColor = new Color(1f, 0.85f, 0.2f, 0.9f);
        [SerializeField] private Color _traitColor = new Color(0.3f, 0.9f, 0.3f, 0.9f);
        [SerializeField] private Color _turretColor = new Color(1f, 0.5f, 0.2f, 0.9f);

        private readonly Queue<UnlockNotificationEvent> _queue = new();
        private bool _isShowing;
        private RectTransform _bannerRect;
        private Vector2 _hiddenPos;
        private Vector2 _shownPos;

        private void Awake()
        {
            EventBus.Subscribe<UnlockNotificationEvent>(OnUnlockNotification);

            if (_bannerPanel != null)
            {
                _bannerRect = _bannerPanel.GetComponent<RectTransform>();

                // Calculate positions: hidden = off-screen top, shown = visible
                _shownPos = _bannerRect.anchoredPosition;
                _hiddenPos = _shownPos + new Vector2(0, 120f); // above screen

                _bannerRect.anchoredPosition = _hiddenPos;
                _bannerPanel.SetActive(false);
            }
        }

        private void OnUnlockNotification(UnlockNotificationEvent evt)
        {
            _queue.Enqueue(evt);
            if (!_isShowing)
                StartCoroutine(ShowNextNotification());
        }

        private IEnumerator ShowNextNotification()
        {
            _isShowing = true;

            while (_queue.Count > 0)
            {
                var evt = _queue.Dequeue();
                var unlockType = (UnlockType)evt.UnlockType;

                // Set localized content
                string prefix = unlockType switch
                {
                    UnlockType.Challenge => LocalizationManager.Get("notification_challenge_complete", "CHALLENGE COMPLETE"),
                    UnlockType.Trait => LocalizationManager.Get("notification_trait_unlocked", "TRAIT UNLOCKED"),
                    UnlockType.Turret => LocalizationManager.Get("notification_turret_unlocked", "TURRET UNLOCKED"),
                    _ => LocalizationManager.Get("notification_unlocked", "UNLOCKED")
                };

                if (_titleText != null) _titleText.text = LocalizationManager.GetFormat("notification_title_format", "{0}: {1}", prefix, evt.DisplayName);
                if (_descriptionText != null) _descriptionText.text = evt.Description ?? "";

                // Set color
                Color bgColor = unlockType switch
                {
                    UnlockType.Challenge => _challengeColor,
                    UnlockType.Trait => _traitColor,
                    UnlockType.Turret => _turretColor,
                    _ => _challengeColor
                };
                if (_bannerBackground != null) _bannerBackground.color = bgColor;

                // Show banner
                _bannerPanel.SetActive(true);

                // Slide in
                yield return SlideAnimation(_hiddenPos, _shownPos, _slideInDuration);

                // Wait
                yield return new WaitForSecondsRealtime(_displayDuration);

                // Slide out
                yield return SlideAnimation(_shownPos, _hiddenPos, _slideOutDuration);

                _bannerPanel.SetActive(false);

                // Small gap between notifications
                if (_queue.Count > 0)
                    yield return new WaitForSecondsRealtime(0.2f);
            }

            _isShowing = false;
        }

        private IEnumerator SlideAnimation(Vector2 from, Vector2 to, float duration)
        {
            if (_bannerRect == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                _bannerRect.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }
            _bannerRect.anchoredPosition = to;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<UnlockNotificationEvent>(OnUnlockNotification);
        }
    }
}
