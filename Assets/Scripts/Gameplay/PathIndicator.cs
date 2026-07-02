// ============================================================================
// ETD.Gameplay - PathIndicator.cs  [NEW]
// Shows directional arrow sprites along the enemy path.
// Triggers on: game start, PathRecalculatedEvent.
// Arrows fade in quickly, hold briefly, then fade out.
// ============================================================================
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Grid;
using ETD.Pathfinding;

namespace ETD.Gameplay
{
    public class PathIndicator : MonoBehaviour
    {
        [Header("Arrow Visuals")]
        [Tooltip("Sprite used for each path tile arrow")]
        [SerializeField] private Sprite _arrowSprite;
        [SerializeField] private Color  _arrowColor      = new Color(0.3f, 0.9f, 1f, 0.85f);
        [SerializeField] private float  _arrowSize       = 0.55f;
        [SerializeField] private float  _yOffset         = 0.05f;

        [Header("Animation")]
        [SerializeField] private float _fadeInDuration   = 0.2f;
        [SerializeField] private float _holdDuration     = 2.5f;
        [SerializeField] private float _fadeOutDuration  = 0.6f;
        [SerializeField] private bool  _keepAlwaysVisible = false; // for tutorial mode

        [Header("References")]
        [SerializeField] private AStarPathfinder _pathfinder;
        [SerializeField] private GridSystem _grid;

        private readonly List<SpriteRenderer> _arrows = new();
        private Coroutine _animCoroutine;

        private void Awake()
        {
            EventBus.Subscribe<PathRecalculatedEvent>(OnPathRecalculated);
        }

        private void Start()
        {
            // Show path immediately on game start
            ShowPath();
        }

        private void OnPathRecalculated(PathRecalculatedEvent evt)
        {
            ShowPath();
        }

        // =================================================================
        // SHOW PATH
        // =================================================================

        public void ShowPath()
        {
            if (_pathfinder == null || _grid == null) return;

            if (_animCoroutine != null) StopCoroutine(_animCoroutine);

            ClearArrows();

            var path = _pathfinder.CurrentPath;
            if (path == null || path.Count < 2) return;

            // Create one arrow per path tile (except last tile)
            for (int i = 0; i < path.Count - 1; i++)
            {
                Vector3 worldPos = _grid.GridToWorld(path[i]);
                worldPos.y += _yOffset;

                Vector3 nextWorld = _grid.GridToWorld(path[i + 1]);
                Vector3 dir       = (nextWorld - _grid.GridToWorld(path[i])).normalized;

                float angle = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;
                // Rotate to point in movement direction (arrow points up by default)
                Quaternion rotation = Quaternion.Euler(90f, angle, 0f);

                var go = new GameObject($"PathArrow_{i}");
                go.transform.SetParent(transform);
                go.transform.position = worldPos;
                go.transform.rotation = rotation;

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite        = _arrowSprite;
                sr.color         = new Color(_arrowColor.r, _arrowColor.g, _arrowColor.b, 0f);
                sr.sortingOrder  = 1;
                go.transform.localScale = Vector3.one * _arrowSize;

                _arrows.Add(sr);
            }

            if (_keepAlwaysVisible)
                _animCoroutine = StartCoroutine(FadeIn());
            else
                _animCoroutine = StartCoroutine(ShowAndFade());
        }

        // =================================================================
        // TOGGLE (called by tutorial or settings)
        // =================================================================

        public void SetAlwaysVisible(bool visible)
        {
            _keepAlwaysVisible = visible;
            if (visible)
                ShowPath();
        }

        // =================================================================
        // ANIMATION
        // =================================================================

        private IEnumerator ShowAndFade()
        {
            // Fade in
            yield return Fade(0f, 1f, _fadeInDuration);
            // Hold
            yield return new WaitForSecondsRealtime(_holdDuration);
            // Fade out
            yield return Fade(1f, 0f, _fadeOutDuration);
        }

        private IEnumerator FadeIn()
        {
            yield return Fade(0f, 1f, _fadeInDuration);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float alpha = Mathf.Lerp(from, to, elapsed / duration);
                foreach (var sr in _arrows)
                    if (sr != null)
                        sr.color = new Color(_arrowColor.r, _arrowColor.g, _arrowColor.b, alpha);
                yield return null;
            }
        }

        private void ClearArrows()
        {
            foreach (var sr in _arrows)
                if (sr != null) Destroy(sr.gameObject);
            _arrows.Clear();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<PathRecalculatedEvent>(OnPathRecalculated);
        }
    }
}
