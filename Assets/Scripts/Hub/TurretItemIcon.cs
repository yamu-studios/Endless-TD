using ETD.Core;
using ETD.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ETD.Hub
{ // =========================================================================
    // TURRET ICON ITEM
    // =========================================================================
    public class TurretIconItem : MonoBehaviour
    {
        [SerializeField] private Image _icon;
        [SerializeField] private Image _border;
        [SerializeField] private Button _button;
        [SerializeField] private HubBadge _badge;

        public TurretData Data { get; private set; }
        public bool IsUnlocked { get; private set; }
        private Color _sel, _def;

        public void Setup(TurretData data, bool unlocked,
            Color sel, Color def, System.Action<TurretIconItem> onClick)
        {
            Data = data; IsUnlocked = unlocked; _sel = sel; _def = def;
            if (_icon != null) { _icon.sprite = data.Icon; _icon.color = unlocked ? Color.white : new Color(0.3f, 0.3f, 0.3f); }
            SetBorderSelected(false);
            if (_button == null) _button = GetComponent<Button>();
            _button?.onClick.RemoveAllListeners();
            _button?.onClick.AddListener(() => onClick?.Invoke(this));
            _badge?.SetVisible(HubBadgeRegistry.IsNew(HubBadgeType.Turret, data.Id));
        }

        public void SetBorderSelected(bool sel)
        {
            if (_border != null) _border.color = sel ? _sel : _def;
        }

        public void HideBadge()
        {
            _badge?.SetVisible(false);
        }
    }

    // =========================================================================
    // TURRET EVOLUTION PATH UI — 3 separate stat texts
    // =========================================================================
    [System.Serializable]
    public class TurretEvolutionPathUI
    {
        [Header("Container")]
        public GameObject container;

        [Header("Header")]
        public Image icon;
        public TMP_Text nameText;
        public TMP_Text descText;

        [Header("Stats (3 separate texts)")]
        public TMP_Text damageText;
        public TMP_Text rangeText;
        public TMP_Text speedText;

        public void Setup(TurretEvolutionData evo, TurretData baseTurret,
            string dmgLabel, string rngLabel, string spdLabel, string noVal,string path)
        {
            if (container != null) container.SetActive(evo != null);
            if (evo == null) return;

            // Icon & name
            if (icon != null) { icon.sprite = evo.Icon; icon.enabled = evo.Icon != null; }
            if (nameText != null) nameText.text = SOLocalization.GetName("turret_"+baseTurret.LocalizationKey+"_"+path, evo.Name);
            if (descText != null) descText.text = SOLocalization.GetDesc("turret_" + baseTurret.LocalizationKey + "_" + path, evo.Description); ;

            // Damage
            if (damageText != null)
            {
                string val = evo.DamageOverride >= 0
                    ? $"{evo.DamageOverride:F0}"
                    : $"{baseTurret.Damage:F0}";
                damageText.text = $"{dmgLabel}: {val}";
            }

            // Range
            if (rangeText != null)
            {
                string val = evo.RangeOverride >= 0
                    ? $"{evo.RangeOverride:F1}"
                    : $"{baseTurret.Range:F1}";
                rangeText.text = $"{rngLabel}: {val}";
            }

            // Speed
            if (speedText != null)
            {
                bool hasSpeed = baseTurret.Type != TurretType.Support
                             && baseTurret.Type != TurretType.Radar
                             && !baseTurret.IsContinuousBeam;

                if (!hasSpeed)
                {
                    speedText.text = $"{spdLabel}: {noVal}";
                }
                else
                {
                    float interval = evo.AttackIntervalOverride >= 0
                        ? evo.AttackIntervalOverride
                        : baseTurret.AttackInterval;
                    string val = interval > 0 ? $"{1f / interval:F2}/s" : noVal;
                    speedText.text = $"{spdLabel}: {val}";
                }
            }
        }
    }
}

