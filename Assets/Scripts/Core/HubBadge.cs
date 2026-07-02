// ============================================================================
// ETD.UI - HubBadge.cs  [NEW]
// Exclamation mark indicator. Add to hub buttons and individual list items.
// Scales up/down continuously while visible.
// Call SetVisible(false) when player views the item.
// ============================================================================
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace ETD.Core
{
    public class HubBadge : MonoBehaviour
    {
        [SerializeField] private GameObject _badgeRoot;
        [SerializeField] private Image      _badgeImage;
        [SerializeField] private float      _scaleMin    = 0.85f;
        [SerializeField] private float      _scaleMax    = 1.15f;
        [SerializeField] private float      _scalePeriod = 0.7f;

        private Coroutine _animCoroutine;
        private bool _visible;

        private void Awake()
        {
            if (_badgeRoot == null) _badgeRoot = gameObject;
            _badgeRoot.SetActive(false);
        }

        public void SetVisible(bool visible)
        {
            if (_visible == visible) return;
            _visible = visible;
            _badgeRoot.SetActive(visible);

            if (visible)
            {
                if (_animCoroutine != null) StopCoroutine(_animCoroutine);
                _animCoroutine = StartCoroutine(PulseScale());
            }
            else if (_animCoroutine != null)
            {
                StopCoroutine(_animCoroutine);
                _animCoroutine = null;
                _badgeRoot.transform.localScale = Vector3.one;
            }
        }

        private IEnumerator PulseScale()
        {
            var t = _badgeRoot.transform;
            while (true)
            {
                float elapsed = 0f;
                while (elapsed < _scalePeriod)
                {
                    float s = Mathf.Lerp(_scaleMin, _scaleMax,
                        (Mathf.Sin(elapsed / _scalePeriod * Mathf.PI * 2f - Mathf.PI / 2f) + 1f) * 0.5f);
                    t.localScale = Vector3.one * s;
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }
            }
        }

        public bool IsVisible => _visible;
    }

    // ============================================================================
    // Badge type enum
    // ============================================================================
    public enum HubBadgeType { Turret, Trait, Challenge }
}
