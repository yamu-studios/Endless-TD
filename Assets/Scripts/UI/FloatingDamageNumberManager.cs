// ============================================================================
// ETD.UI - FloatingDamageNumberManager.cs
// Event-driven pooled floating damage numbers. Add this to your in-game HUD
// Canvas; it listens to EnemyDamagedEvent published by EnemyController.
// ============================================================================
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using ETD.Core;

namespace ETD.UI
{
    [DisallowMultipleComponent]
    public sealed class FloatingDamageNumberManager : MonoBehaviour
    {
        private struct PendingDamage
        {
            public float Amount;
            public Vector3 Position;
            public bool Critical;
            public bool KillingBlow;
            public int Kind;
            public float ReleaseAt;
        }

        [Header("References")]
        [SerializeField] private Canvas _canvas;
        [SerializeField] private RectTransform _container;
        [Tooltip("Camera that renders enemies/turrets. Required for Screen Space Overlay canvases too.")]
        [SerializeField] private Camera _worldCamera;
        [SerializeField] private FloatingDamageNumber _prefab;

        [Header("Pooling")]
        [SerializeField, Min(0)] private int _prewarm = 48;
        [SerializeField, Min(1)] private int _maxActive = 140;

        [Header("Readability")]
        [SerializeField] private float _minimumDamageToShow = 0.5f;
        [SerializeField] private float _mergeWindow = 0.075f;
        [SerializeField] private float _worldOffsetY = 0.35f;
        [SerializeField] private bool _showBurnTicks = false;
        [SerializeField] private bool _showLaserTicks = true;

        private readonly Queue<FloatingDamageNumber> _pool = new();
        private readonly List<FloatingDamageNumber> _active = new(160);
        private readonly Dictionary<int, float> _lastSpawnTimeByRuntimeId = new();
        private readonly Dictionary<int, PendingDamage> _pendingByRuntimeId = new();
        private readonly List<int> _pendingKeysToFlush = new(32);

        // SaveSystem.Load can touch disk/json. Do not call it for every damage event.
        private bool _damageNumbersEnabled = true;
        private float _nextPreferenceRefreshTime;
        private const float PreferenceRefreshInterval = 0.25f;

        private void Awake()
        {
            if (_canvas == null)
                _canvas = GetComponentInParent<Canvas>();
            if (_worldCamera == null)
                _worldCamera = Camera.main;

            EnsureContainer();
            Prewarm();
            RefreshPreferenceCache(true);
        }

        private void OnEnable()
        {
            RefreshPreferenceCache(true);
            EventBus.Subscribe<EnemyDamagedEvent>(OnEnemyDamaged);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnemyDamagedEvent>(OnEnemyDamaged);
        }

        private void LateUpdate()
        {
            RefreshPreferenceCache(false);

            if (!_damageNumbersEnabled)
            {
                ClearAllDamageNumbers();
                return;
            }

            float now = Time.unscaledTime;

            // Drive every live number from this single loop instead of one Unity
            // Update() per instance (profiler: 99 separate Update calls cost more
            // in invocation overhead than the actual visual math).
            for (int i = _active.Count - 1; i >= 0; i--)
            {
                FloatingDamageNumber number = _active[i];
                if (number == null)
                {
                    RemoveActiveAt(i);
                    continue;
                }

                if (number.ManagedUpdate(now))
                {
                    RemoveActiveAt(i);
                    Recycle(number);
                }
            }

            if (_pendingByRuntimeId.Count == 0)
                return;

            _pendingKeysToFlush.Clear();

            foreach (var pair in _pendingByRuntimeId)
            {
                if (now >= pair.Value.ReleaseAt)
                    _pendingKeysToFlush.Add(pair.Key);
            }

            for (int i = 0; i < _pendingKeysToFlush.Count; i++)
            {
                int runtimeId = _pendingKeysToFlush[i];
                if (!_pendingByRuntimeId.TryGetValue(runtimeId, out PendingDamage pending))
                    continue;

                _pendingByRuntimeId.Remove(runtimeId);
                Spawn(runtimeId, pending.Amount, pending.Position, pending.Critical, pending.Kind, pending.KillingBlow);
            }
        }

        private void OnEnemyDamaged(EnemyDamagedEvent evt)
        {
            if (!_damageNumbersEnabled)
                return;

            if (evt.Amount < _minimumDamageToShow)
                return;

            DamageNumberKind kind = (DamageNumberKind)evt.DamageKind;
            if (!_showBurnTicks && kind == DamageNumberKind.Burn)
                return;
            if (!_showLaserTicks && kind == DamageNumberKind.Laser)
                return;

            int runtimeId = evt.RuntimeId != 0 ? evt.RuntimeId : evt.EnemyId;
            float now = Time.unscaledTime;
            Vector3 position = evt.WorldPosition + Vector3.up * _worldOffsetY;

            if (_lastSpawnTimeByRuntimeId.TryGetValue(runtimeId, out float lastSpawn) &&
                now - lastSpawn < _mergeWindow)
            {
                if (_pendingByRuntimeId.TryGetValue(runtimeId, out PendingDamage pending))
                {
                    pending.Amount += evt.Amount;
                    pending.Position = position;
                    pending.Critical |= evt.IsCritical;
                    pending.KillingBlow |= evt.IsKillingBlow;
                    pending.Kind = evt.IsCritical ? (int)DamageNumberKind.Critical : evt.DamageKind;
                    pending.ReleaseAt = Mathf.Min(pending.ReleaseAt, lastSpawn + _mergeWindow);
                    _pendingByRuntimeId[runtimeId] = pending;
                }
                else
                {
                    _pendingByRuntimeId[runtimeId] = new PendingDamage
                    {
                        Amount = evt.Amount,
                        Position = position,
                        Critical = evt.IsCritical,
                        KillingBlow = evt.IsKillingBlow,
                        Kind = evt.DamageKind,
                        ReleaseAt = lastSpawn + _mergeWindow
                    };
                }

                return;
            }

            Spawn(runtimeId, evt.Amount, position, evt.IsCritical, evt.DamageKind, evt.IsKillingBlow);
        }

        private void Spawn(int runtimeId, float amount, Vector3 position, bool critical, int kind, bool killingBlow)
        {
            EnsureContainer();

            if (_active.Count >= _maxActive)
                return;

            FloatingDamageNumber number = GetNumber();
            if (number == null)
                return;

            _active.Add(number);
            _lastSpawnTimeByRuntimeId[runtimeId] = Time.unscaledTime;
            number.Play(_container, GetWorldCamera(), GetUiCamera(), position, amount, critical || killingBlow, kind);
        }

        private Camera GetWorldCamera()
        {
            if (_worldCamera != null)
                return _worldCamera;

            if (_canvas != null && _canvas.worldCamera != null)
                return _canvas.worldCamera;

            return Camera.main;
        }

        private Camera GetUiCamera()
        {
            if (_canvas != null && _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
                return null;

            if (_canvas != null && _canvas.worldCamera != null)
                return _canvas.worldCamera;

            return GetWorldCamera();
        }

        private FloatingDamageNumber GetNumber()
        {
            while (_pool.Count > 0)
            {
                FloatingDamageNumber pooled = _pool.Dequeue();
                if (pooled != null)
                    return pooled;
            }

            return CreateNumber();
        }

        private void RemoveActiveAt(int index)
        {
            int lastIndex = _active.Count - 1;
            _active[index] = _active[lastIndex];
            _active.RemoveAt(lastIndex);
        }

        private void Recycle(FloatingDamageNumber number)
        {
            if (number == null)
                return;

            number.gameObject.SetActive(false);
            number.transform.SetParent(_container, false);
            _pool.Enqueue(number);
        }

        private void RefreshPreferenceCache(bool force)
        {
            float now = Time.unscaledTime;
            if (!force && now < _nextPreferenceRefreshTime)
                return;

            _nextPreferenceRefreshTime = now + PreferenceRefreshInterval;
            SaveData save = SaveSystem.Load();
            _damageNumbersEnabled = save == null || save.FloatingDamageNumbersEnabled;
        }

        private void ClearAllDamageNumbers()
        {
            _pendingByRuntimeId.Clear();
            _lastSpawnTimeByRuntimeId.Clear();

            for (int i = _active.Count - 1; i >= 0; i--)
                Recycle(_active[i]);

            _active.Clear();
        }

        private void Prewarm()
        {
            for (int i = 0; i < _prewarm; i++)
            {
                FloatingDamageNumber number = CreateNumber();
                if (number == null)
                    return;
                number.gameObject.SetActive(false);
                _pool.Enqueue(number);
            }
        }

        private FloatingDamageNumber CreateNumber()
        {
            EnsureContainer();

            if (_prefab != null)
            {
                FloatingDamageNumber instance = Instantiate(_prefab, _container);
                instance.gameObject.SetActive(false);
                return instance;
            }

            GameObject go = new("FloatingDamageNumber_Runtime");
            go.transform.SetParent(_container, false);
            RectTransform rect = go.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(140f, 36f);
            rect.localScale = Vector3.one;

            CanvasGroup canvasGroup = go.AddComponent<CanvasGroup>();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = 22f;
            text.fontStyle = FontStyles.Bold;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Overflow;

            FloatingDamageNumber number = go.AddComponent<FloatingDamageNumber>();
            return number;
        }

        private void EnsureContainer()
        {
            if (_container == null)
            {
                GameObject containerObject = new("FloatingDamageNumbers");
                containerObject.transform.SetParent(_canvas != null ? _canvas.transform : transform, false);
                _container = containerObject.AddComponent<RectTransform>();
            }

            // Nested canvas: damage numbers move/fade every frame, which would
            // otherwise mark the entire parent HUD canvas dirty and force a full
            // rebatch per frame (profiler: UGUI.Rendering.UpdateBatches 4.26ms).
            // A nested canvas confines that churn to this container.
            if (_container.GetComponent<Canvas>() == null)
            {
                Canvas nested = _container.gameObject.AddComponent<Canvas>();
                nested.overrideSorting = false;
            }

            _container.anchorMin = Vector2.zero;
            _container.anchorMax = Vector2.one;
            _container.pivot = new Vector2(0.5f, 0.5f);
            _container.offsetMin = Vector2.zero;
            _container.offsetMax = Vector2.zero;
            _container.localScale = Vector3.one;
            _container.SetAsLastSibling();
        }
    }
}
