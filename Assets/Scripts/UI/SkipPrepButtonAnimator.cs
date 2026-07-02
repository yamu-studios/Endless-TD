// ============================================================================
// ETD.UI - SkipPrepButtonAnimator.cs  [NEW]
// Attach to the SkipPrep button's RectTransform parent.
// Slides down into view when prep phase starts, up out of view when it ends.
// ============================================================================
using System.Collections;
using UnityEngine;
using ETD.Core;

namespace ETD.UI
{
    public class SkipPrepButtonAnimator : MonoBehaviour
    {
        [SerializeField] private RectTransform _buttonRect;
        [SerializeField] private float _slideDistance = 80f;   // pixels to slide
        [SerializeField] private float _slideDuration = 0.25f;
        [SerializeField] private AnimationCurve _curve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        private Vector2 _shownPos;
        private Vector2 _hiddenPos;
        private Coroutine _slideCoroutine;

        bool shouldSlide;

        private void Awake()
        {
            if (_buttonRect == null)
                _buttonRect = GetComponent<RectTransform>();

            _shownPos  = _buttonRect.anchoredPosition;
            _hiddenPos = _shownPos + Vector2.up * _slideDistance; // hidden above

            // Start hidden
            _buttonRect.anchoredPosition = _hiddenPos;
            //gameObject.SetActive(false);

            EventBus.Subscribe<PrepPhaseStartedEvent>(OnPrepStarted);
            EventBus.Subscribe<WaveStartedEvent>(OnWaveStarted);
        }

        private void OnPrepStarted(PrepPhaseStartedEvent evt)
        {
            gameObject.SetActive(true);
            Slide(_hiddenPos, _shownPos);
        }

        private void OnEnable()
        {
            if (shouldSlide) Slide(_hiddenPos, _shownPos);
        }


        private void OnWaveStarted(WaveStartedEvent evt)
        {
            Slide(_shownPos, _hiddenPos, hideAfter: false);
        }

        private void Slide(Vector2 from, Vector2 to, bool hideAfter = false)
        {
            shouldSlide = true;
            if (_slideCoroutine != null) StopCoroutine(_slideCoroutine);
            if (!gameObject.activeInHierarchy) return;
            _slideCoroutine = StartCoroutine(SlideCoroutine(from, to, hideAfter));
        }

        private IEnumerator SlideCoroutine(Vector2 from, Vector2 to, bool hideAfter)
        {
            float elapsed = 0f;
            while (elapsed < _slideDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = _curve.Evaluate(Mathf.Clamp01(elapsed / _slideDuration));
                _buttonRect.anchoredPosition = Vector2.Lerp(from, to, t);
                yield return null;
            }
            _buttonRect.anchoredPosition = to;
            shouldSlide = false;
            if (hideAfter) gameObject.SetActive(false);
            _slideCoroutine = null;
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<PrepPhaseStartedEvent>(OnPrepStarted);
            EventBus.Unsubscribe<WaveStartedEvent>(OnWaveStarted);
        }
    }
}
