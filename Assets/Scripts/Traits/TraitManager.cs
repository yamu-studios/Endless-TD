// ============================================================================
// ETD.Traits - TraitManager.cs
// Manages active traits during a run. Provides bonus queries.
// ============================================================================
using System.Collections.Generic;
using UnityEngine;
using ETD.Core;
using ETD.Data;

namespace ETD.Traits
{
    public class TraitManager : MonoBehaviour
    {
        private readonly List<TraitData> _activeTraits = new();
        private GameDatabase _db;

        public IReadOnlyList<TraitData> ActiveTraits => _activeTraits;

        public static TraitManager Instance;
        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
            }
            else if (Instance == null)
                Instance = this;
        }
        public void Initialize(GameDatabase db, List<string> activeTraitIds)
        {
            _db = db;
            _activeTraits.Clear();

            for (int i = 0; i < activeTraitIds.Count; i++)
            {
                var trait = db.GetTrait(activeTraitIds[i]);
                if (trait != null)
                    _activeTraits.Add(trait);
            }

            ServiceLocator.Register(this);
        }

        public List<TraitData> GetActiveTraits()
        {
            return _activeTraits;
        }

        public float GetTotalBonus(TraitEffectType effectType)
        {
            float total = 0f;
            for (int i = 0; i < _activeTraits.Count; i++)
            {
                if (_activeTraits[i].EffectType == effectType)
                    total += _activeTraits[i].EffectValue;
            }
            return total;
        }

        public bool HasTrait(string traitId)
        {
            for (int i = 0; i < _activeTraits.Count; i++)
            {
                if (_activeTraits[i].Id == traitId)
                    return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            ServiceLocator.Unregister<TraitManager>();
        }
    }
}
