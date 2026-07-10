// ============================================================================
// ETD.UI - EnemyInfoPanel.cs
// Click/hover enemy inspection panel for in-run tactical readability.
// Put this on a HUD panel and assign TMP/Image references in the Inspector.
// ============================================================================
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using ETD.Core;
using ETD.Enemies;
using ETD.Data;

namespace ETD.UI
{
    [DisallowMultipleComponent]
    public sealed class EnemyInfoPanel : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private GameObject _panel;
        [SerializeField] private Camera _mainCamera;
        [SerializeField] private LayerMask _enemyLayer = ~0;
        [SerializeField] private bool _clickToSelect = true;
        [SerializeField] private bool _hoverPreview = false;
        [SerializeField] private bool _closeOnEmptyClick = false;
        [SerializeField] private float _refreshInterval = 0.08f;

        [Header("Drag")]
        [SerializeField] private bool _enableDragging = true;
        [SerializeField] private RectTransform _dragHandle;
        [SerializeField] private string _dragSaveId = "enemy_info_panel";

        [Header("Text")]
        [SerializeField] private TMP_Text _titleText;
        [SerializeField] private TMP_Text _nameText;
        [SerializeField] private TMP_Text _tierText;
        [SerializeField] private TMP_Text _typeText;
        [SerializeField] private TMP_Text _healthText;
        [SerializeField] private TMP_Text _armorText;
        [SerializeField] private TMP_Text _speedText;
        [SerializeField] private TMP_Text _rewardText;
        [SerializeField] private TMP_Text _statusText;

        [Header("Visuals")]
        [SerializeField] private Image _iconImage;
        [SerializeField] private Image _healthFill;

        [Header("Close")]
        [Tooltip("Assign your own close button here to match the game's UI style. If left empty, a simple runtime 'X' button is created instead.")]
        [SerializeField] private Button _closeButton;

        private EnemyController _selected;
        private int _selectedRuntimeId = -1;
        private float _nextRefreshTime;

        private void Awake()
        {
            if (_panel == null)
                _panel = gameObject;
            if (_mainCamera == null)
                _mainCamera = Camera.main;

            SetText(_titleText, LocalizationManager.Get("ui_enemy_info_title", "Enemy Info"));
            EnsureDraggable();

            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(Hide);
                _closeButton.onClick.AddListener(Hide);
            }
            else
            {
                PanelCloseButton.Ensure(_panel, Hide);
            }

            Hide();
        }

        private void Update()
        {
            HandlePointerSelection();

            if (_selected == null)
                return;

            if (!_selected.gameObject.activeInHierarchy || _selected.IsDead || _selected.RuntimeId != _selectedRuntimeId)
            {
                Hide();
                return;
            }

            if (Time.unscaledTime >= _nextRefreshTime)
            {
                _nextRefreshTime = Time.unscaledTime + Mathf.Max(0.02f, _refreshInterval);
                Refresh();
            }
        }

        private void HandlePointerSelection()
        {
            if (IsPointerOverUI())
                return;

            if (_closeOnEmptyClick && UnityEngine.Input.GetMouseButtonDown(1))
            {
                Hide();
                return;
            }

            if (_hoverPreview && TryGetEnemyUnderCursor(out EnemyController hoverEnemy))
                Select(hoverEnemy);

            if (!_clickToSelect || !UnityEngine.Input.GetMouseButtonDown(0))
                return;

            if (TryGetEnemyUnderCursor(out EnemyController clickedEnemy))
                Select(clickedEnemy);

            // Enemy and turret panels are independent now. Left-clicking a turret
            // or empty world space must not close this panel.
        }

        private bool TryGetEnemyUnderCursor(out EnemyController enemy)
        {
            enemy = null;
            Camera cam = _mainCamera != null ? _mainCamera : Camera.main;
            if (cam == null)
                return false;

            Ray ray = cam.ScreenPointToRay(UnityEngine.Input.mousePosition);
            if (!Physics.Raycast(ray, out RaycastHit hit, 1000f, _enemyLayer, QueryTriggerInteraction.Collide))
                return false;

            enemy = hit.collider.GetComponentInParent<EnemyController>();
            return enemy != null && !enemy.IsDead;
        }

        private void Select(EnemyController enemy)
        {
            if (enemy == null)
                return;

            _selected = enemy;
            _selectedRuntimeId = enemy.RuntimeId;
            if (_panel != null)
                _panel.SetActive(true);

            EventBus.Publish(new EnemySelectedEvent { EnemyId = enemy.InstanceId });
            Refresh();
        }

        private void Refresh()
        {
            if (_selected == null || _selected.Data == null)
                return;

            EnemyData data = _selected.Data;
            string key = string.IsNullOrWhiteSpace(data.LocalizationKey) ? data.Id : data.LocalizationKey;
            string displayName = SOLocalization.GetName(key, data.DisplayName);

            SetText(_nameText, displayName);
            SetText(_tierText, FormatPair("ui_tier", "Tier", LocalizeTier(_selected.Tier)));
            SetText(_typeText, FormatPair("ui_type", "Type", LocalizeType(data.Type)));
            // v8: these text fields now show only the value. The row/icon already communicates the stat.
            SetText(_armorText, _selected.Armor.ToString("0.#"));
            SetText(_speedText, _selected.MoveSpeed.ToString("0.##"));
            // Show the wave/tier-scaled rewards this kill will actually pay,
            // not the base asset values.
            SetText(_rewardText, string.Format("{0}: {1}  {2}: {3:0}",
                LocalizationManager.Get("ui_gold", "Gold"),
                EnemyController.ComputeGoldReward(data, _selected.Tier, _selected.WaveNumber),
                LocalizationManager.Get("ui_xp", "XP"),
                EnemyController.ComputeXPReward(data, _selected.WaveNumber)));

            float hp = Mathf.Max(0f, _selected.CurrentHealth);
            float maxHp = Mathf.Max(1f, _selected.MaxHealth);
            // v10: the health bar already shows current HP visually. The stat row
            // shows only the enemy's full/base max HP for cleaner reading.
            SetText(_healthText, FormatNumber(maxHp));

            if (_healthFill != null)
                _healthFill.fillAmount = Mathf.Clamp01(hp / maxHp);

            if (_iconImage != null)
            {
                _iconImage.sprite = data.Icon;
                _iconImage.enabled = data.Icon != null;
            }

            SetText(_statusText, BuildStatusText(_selected));
        }

        private void EnsureDraggable()
        {
            if (!_enableDragging || _panel == null)
                return;

            var panelGo = _panel;
            var draggable = panelGo.GetComponent<DraggableUIPanel>();
            if (draggable == null)
                draggable = panelGo.AddComponent<DraggableUIPanel>();

            draggable.Configure(_dragSaveId, _dragHandle);
        }

        private void Hide()
        {
            _selected = null;
            _selectedRuntimeId = -1;
            if (_panel != null)
                _panel.SetActive(false);
            EventBus.Publish(new EnemyDeselectedEvent());
        }

        private static string BuildStatusText(EnemyController enemy)
        {
            if (enemy == null)
                return string.Empty;

            string status = string.Empty;
            string burnLabel = LocalizationManager.Get("status_burn", "Burn");
            if (enemy.BurnStackCount > 1)
                burnLabel += " x" + enemy.BurnStackCount;
            AppendStatus(ref status, enemy.HasStatus(StatusEffectType.Burn), burnLabel);
            AppendStatus(ref status, enemy.HasStatus(StatusEffectType.Slow), LocalizationManager.Get("status_slow", "Slow"));
            AppendStatus(ref status, enemy.HasStatus(StatusEffectType.Freeze), LocalizationManager.Get("status_freeze", "Freeze"));
            AppendStatus(ref status, enemy.IsStealth && !enemy.IsRevealed, LocalizationManager.Get("status_stealth", "Stealth"));
            AppendStatus(ref status, enemy.IsStealth && enemy.IsRevealed, LocalizationManager.Get("status_revealed", "Revealed"));

            if (string.IsNullOrEmpty(status))
                status = LocalizationManager.Get("status_none", "No status");

            return status;
        }

        private static void AppendStatus(ref string current, bool condition, string value)
        {
            if (!condition)
                return;
            if (!string.IsNullOrEmpty(current))
                current += "  •  ";
            current += value;
        }

        private static string LocalizeTier(EnemyTier tier)
        {
            return tier switch
            {
                EnemyTier.Elite => LocalizationManager.Get("enemy_tier_elite", "Elite"),
                EnemyTier.Boss => LocalizationManager.Get("enemy_tier_boss", "Boss"),
                _ => LocalizationManager.Get("enemy_tier_normal", "Normal")
            };
        }

        private static string LocalizeType(EnemyType type)
        {
            string key = "enemy_type_" + type.ToString().ToLowerInvariant();
            return LocalizationManager.Get(key, type.ToString());
        }

        private static string FormatPair(string labelKey, string fallback, string value)
        {
            return LocalizationManager.Get(labelKey, fallback) + ": " + value;
        }

        private static string FormatNumber(float value)
        {
            // Canonical formatter (K/M/B/T/Qa..Dc, then scientific; NaN/Inf safe).
            return ETD.Core.NumberFormat.Compact(value);
        }

        private static void SetText(TMP_Text text, string value)
        {
            if (text != null)
                text.text = value;
        }

        private static bool IsPointerOverUI()
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        }
    }
}
