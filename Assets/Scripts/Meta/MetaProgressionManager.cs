// ============================================================================
// ETD.Meta - MetaProgressionManager.cs
// Handles shop purchases, challenge tracking, permanent upgrades.
// Used in Hub scene.
// ============================================================================
using UnityEngine;
using ETD.Core;
using ETD.Data;

namespace ETD.Meta
{
    public class MetaProgressionManager : MonoBehaviour
    {
        [SerializeField] private GameDatabase _database;

        private SaveData _save;

        public int MetaCurrency => _save.MetaCurrency;

        private void Awake()
        {
            _save = SaveSystem.Load();
            ServiceLocator.Register(this);
        }


        private void Update()
        {
            if (Input.GetKey(KeyCode.F)) AddMeta();
        }
        // === SHOP ===

        public bool BuyTraitSlot(int cost)
        {
            if (_save.TraitSlotCount >= GameConstants.MAX_TRAIT_SLOTS_LIMIT) return false;
            if (!SpendMetaCurrency(cost)) return false;
            _save.TraitSlotCount++;
            SaveSystem.Save(_save);
            return true;
        }

        public bool UpgradeTrait(string traitId, int cost)
        {
            if (!SpendMetaCurrency(cost)) return false;
            // Mark trait as upgraded in save data
            SaveSystem.Save(_save);
            return true;
        }

      
        public bool UpgradeTurretBase(string turretId, int cost)
        {
            if (!SpendMetaCurrency(cost)) return false;
            SaveSystem.Save(_save);
            return true;
        }

      

        // === CHALLENGES ===

        public bool IsChallengeCompleted(string challengeId)
        {
            for (int i = 0; i < _save.CompletedChallengeIds.Length; i++)
            {
                if (_save.CompletedChallengeIds[i] == challengeId) return true;
            }
            return false;
        }

        public void CompleteChallenge(ChallengeData challenge)
        {
            if (IsChallengeCompleted(challenge.Id)) return;

            var list = new System.Collections.Generic.List<string>(_save.CompletedChallengeIds);
            list.Add(challenge.Id);
            _save.CompletedChallengeIds = list.ToArray();

            ApplyChallengeReward(challenge);
            SaveSystem.Save(_save);
        }

        private void ApplyChallengeReward(ChallengeData challenge)
        {
            switch (challenge.RewardType)
            {
                //case ChallengeRewardType.MaxHPBonus:
                //    _save.PermanentBonuses[0] += challenge.RewardValue;
                //    break;
                //case ChallengeRewardType.PermanentXPBonus:
                //    _save.PermanentBonuses[1] += challenge.RewardValue;
                //    break;
                //case ChallengeRewardType.PermanentGoldBonus:
                //    _save.PermanentBonuses[2] += challenge.RewardValue;
                //    break;
                //case ChallengeRewardType.PermanentDamageBonus:
                //    _save.PermanentBonuses[3] += challenge.RewardValue;
                //    break;
                //case ChallengeRewardType.MetaCurrency:
                //    SaveSystem.AddMetaCurrency((int)challenge.RewardValue);
                //    break;
                //case ChallengeRewardType.StartingGoldBonus:
                //    _save.PermanentBonuses[4] += challenge.RewardValue;
                //    break;
            }
        }

        // === UNLOCKS ===

        public bool IsTraitUnlocked(string traitId)
        {
            var trait = _database.GetTrait(traitId);
            if (trait != null && trait.IsUnlockedByDefault) return true;
            return System.Array.IndexOf(_save.UnlockedTraitIds, traitId) >= 0;
        }

        

        public bool IsTurretUnlocked(string turretId)
        {
            var turret = _database.GetTurret(turretId);
            if (turret != null && turret.IsUnlockedByDefault) return true;
            return System.Array.IndexOf(_save.UnlockedTurretIds, turretId) >= 0;
        }

        //private bool SpendMeta(int amount)
        //{
        //    if (_save.MetaCurrency < amount) return false;
        //    _save.MetaCurrency -= amount;
        //    EventBus.Publish(new MetaCurrencyChangedEvent
        //    {
        //        Current = _save.MetaCurrency,
        //        Delta = -amount
        //    });
        //    return true;
        //}
        public bool SpendMetaCurrency(int amount)
        {
            var save = SaveSystem.Load();
            if (save.MetaCurrency < amount) return false;
            save.MetaCurrency -= amount;
            SaveSystem.Save(save);
            EventBus.Publish(new MetaCurrencyChangedEvent
            {
                Current = save.MetaCurrency,
                Delta = -amount
            });
            return true;
        }

        private void AddMeta()
        {
            var save = SaveSystem.Load();
            save.MetaCurrency += 500;
            SaveSystem.Save(save);
            EventBus.Publish(new MetaCurrencyChangedEvent
            {
                Current = save.MetaCurrency,
                Delta = +500
            });

        }
        private void OnDestroy()
        {
            ServiceLocator.Unregister<MetaProgressionManager>();
        }
    }
}
