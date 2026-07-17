// ============================================================================
// ETD.Enemies - EnemyStatusVFX.cs  [NEW - replaces EnemyVFXConfig status part]
// Each status effect is a DISABLED child GameObject on the enemy prefab.
// They get activated when status applies and deactivated when it expires.
// No pooling — just SetActive(true/false).
//
// PREFAB SETUP:
//   Enemy_Basic
//   ├── Model
//   ├── EnemyController
//   ├── EnemyStatusVFX         ← this component
//   ├── FrostStatusVFX_GO      [child, disabled] ← drag into Frost Status field
//   ├── BurnStatusVFX_GO       [child, disabled] ← drag into Burn Status field
//   ├── LightningStatusVFX_GO  [child, disabled] ← drag into Lightning Status field
//   ├── BuffStatusVFX_GO       [child, disabled] ← drag into Buff Status field
//   └── DebuffStatusVFX_GO     [child, disabled] ← drag into Debuff Status field
// ============================================================================
using UnityEngine;
using ETD.Core;

namespace ETD.Enemies
{
    public class EnemyStatusVFX : MonoBehaviour
    {
        [Header("Status Effect VFX (disabled child GOs on this prefab)")]
        [SerializeField] private GameObject _frostStatusVFX;
        [SerializeField] private GameObject _burnStatusVFX;
        [SerializeField] private GameObject _lightningStatusVFX;
        [SerializeField] private GameObject _laserStatusVFX;
        [SerializeField] private GameObject _armorBreakStatusVFX;
        [SerializeField] private GameObject _weakenStatusVFX;
        [SerializeField] private GameObject _poisonStatusVFX;
        [SerializeField] private GameObject _exposeStatusVFX;
        [SerializeField] private GameObject _buffStatusVFX;
        [SerializeField] private GameObject _debuffStatusVFX;

        // =================================================================
        // CALLED BY EnemyController when status applied / removed / expired
        // =================================================================

        public void OnStatusApplied(StatusEffectType type)
        {
            GetVFX(type)?.SetActive(true);
        }

        public void OnStatusRemoved(StatusEffectType type)
        {
            GetVFX(type)?.SetActive(false);
        }

        public void ClearAll()
        {
            _frostStatusVFX?.SetActive(false);
            _burnStatusVFX?.SetActive(false);
            _lightningStatusVFX?.SetActive(false);
            //_buffStatusVFX?.SetActive(false);
            _debuffStatusVFX?.SetActive(false);
            _laserStatusVFX?.SetActive(false);
            _armorBreakStatusVFX?.SetActive(false);
            _weakenStatusVFX?.SetActive(false);
            _poisonStatusVFX?.SetActive(false);
            _exposeStatusVFX?.SetActive(false);
        }

        private GameObject GetVFX(StatusEffectType type) => type switch
        {
            StatusEffectType.Slow    => _frostStatusVFX,
            StatusEffectType.Freeze  => _frostStatusVFX,
            StatusEffectType.Burn    => _burnStatusVFX,
            StatusEffectType.ChainLightning => _lightningStatusVFX,
            StatusEffectType.HPPercentReduce => _laserStatusVFX,
            StatusEffectType.ArmorBreak => _armorBreakStatusVFX,
            StatusEffectType.Weaken => _weakenStatusVFX,
            StatusEffectType.Poison => _poisonStatusVFX,
            StatusEffectType.Expose => _exposeStatusVFX,
            _ => null
        };

        // Called from support turret — pass true for buff, false for debuff
        public void SetBuffVFX(bool active) => _buffStatusVFX?.SetActive(active);
        public void SetDebuffVFX(bool active) => _debuffStatusVFX?.SetActive(active);
    }
}
