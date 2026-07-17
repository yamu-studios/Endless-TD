using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using ETD.Core;

namespace ETD.Data
{
    [CreateAssetMenu(fileName = "GameDatabase", menuName = "ETD/Game Database")]
    public class GameDatabase : ScriptableObject
    {
        [Header("Enemies")]
        public EnemyData[] Enemies;

        [Header("Turrets")]
        public TurretData[] Turrets;

        [Header("Traits")]
        public TraitData[] Traits;

        [Header("Spec Cards")]
        public SpecCardData[] SpecCards;

        [Header("Spells")]
        public SpellData[] Spells;

        [Header("Challenges")]
        public ChallengeData[] Challenges;

        [Header("Dynamic Tiles")]
        public DynamicTileData[] DynamicTiles;

        [Header("Grade Colors")]
        public Color[] GradeColors;

        // Lookup caches
        private Dictionary<string, EnemyData> _enemyLookup;
        private Dictionary<string, TurretData> _turretLookup;
        private Dictionary<string, TraitData> _traitLookup;
        private Dictionary<string, SpecCardData> _specCardLookup;
        private Dictionary<string, SpellData> _spellLookup;
        private Dictionary<DynamicTileType, DynamicTileData> _tileLookup;

        public ChallengeData GetChallenge(string id)
        {
            if (Challenges == null) return null;
            foreach (var c in Challenges)
                if (c != null && c.Id == id) return c;
            return null;
        }

        public EnemyData GetEnemy(string id)
        {
            _enemyLookup ??= BuildStringLookup(Enemies, e => e.Id, "Enemies");
            return TryGet(_enemyLookup, id, "Enemy");
        }

        public TurretData GetTurret(string id)
        {
            _turretLookup ??= BuildStringLookup(Turrets, t => t.Id, "Turrets");
            return TryGet(_turretLookup, id, "Turret");
        }

        public TraitData GetTrait(string id)
        {
            _traitLookup ??= BuildStringLookup(Traits, t => t.Id, "Traits");
            return TryGet(_traitLookup, id, "Trait");
        }

        public SpecCardData GetSpecCard(string id)
        {
            _specCardLookup ??= BuildStringLookup(SpecCards, s => s.Id, "SpecCards");
            return TryGet(_specCardLookup, id, "SpecCard");
        }

        public SpellData GetSpell(string id)
        {
            _spellLookup ??= BuildStringLookup(Spells, s => s.Id, "Spells");
            return TryGet(_spellLookup, id, "Spell");
        }

        public DynamicTileData GetDynamicTile(DynamicTileType type)
        {
            _tileLookup ??= BuildEnumLookup(DynamicTiles, t => t.TileType, "DynamicTiles");
            return _tileLookup.TryGetValue(type, out var data) ? data : null;
        }

        public DynamicTileData[] GetTilesByCategory(TileSpecialty category)
        {
            if (DynamicTiles == null) return System.Array.Empty<DynamicTileData>();
            return DynamicTiles.Where(t => t != null && t.Category == category).ToArray();
        }

        public EnemyData[] GetEnemiesByType(EnemyType type)
        {
            if (Enemies == null) return System.Array.Empty<EnemyData>();
            return Enemies.Where(e => e != null && e.Type == type).ToArray();
        }

        public TurretData[] GetTurretsByType(TurretType type)
        {
            if (Turrets == null) return System.Array.Empty<TurretData>();
            return Turrets.Where(t => t != null && t.Type == type).ToArray();
        }

        public SpecCardData[] GetSpecCardsByRarity(SpecCardRarity rarity)
        {
            if (SpecCards == null) return System.Array.Empty<SpecCardData>();
            return SpecCards.Where(s => s != null && s.Rarity == rarity).ToArray();
        }

        public void ClearLookupCaches()
        {
            _enemyLookup = null;
            _turretLookup = null;
            _traitLookup = null;
            _specCardLookup = null;
            _tileLookup = null;
        }

        private TData TryGet<TData>(Dictionary<string, TData> lookup, string id, string label)
            where TData : ScriptableObject
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning($"GameDatabase.Get{label} called with empty id.", this);
                return null;
            }

            if (lookup != null && lookup.TryGetValue(id, out var data))
                return data;

            Debug.LogWarning($"GameDatabase could not find {label} with id '{id}'.", this);
            return null;
        }

        private Dictionary<string, TData> BuildStringLookup<TData>(
            TData[] items,
            System.Func<TData, string> getId,
            string label)
            where TData : ScriptableObject
        {
            var lookup = new Dictionary<string, TData>();

            if (items == null)
            {
                Debug.LogError($"GameDatabase.{label} array is null.", this);
                return lookup;
            }

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    Debug.LogError($"GameDatabase.{label}[{i}] is null.", this);
                    continue;
                }

                string id = getId(item);
                if (string.IsNullOrWhiteSpace(id))
                {
                    Debug.LogError($"GameDatabase.{label}[{i}] '{item.name}' has an empty Id.", item);
                    continue;
                }

                if (lookup.TryGetValue(id, out var existing))
                {
                    Debug.LogError(
                        $"GameDatabase.{label} has duplicate Id '{id}' used by '{existing.name}' and '{item.name}'. Keeping first entry.",
                        item);
                    continue;
                }

                lookup.Add(id, item);
            }

            return lookup;
        }

        private Dictionary<TKey, TData> BuildEnumLookup<TKey, TData>(
            TData[] items,
            System.Func<TData, TKey> getKey,
            string label)
            where TData : ScriptableObject
        {
            var lookup = new Dictionary<TKey, TData>();

            if (items == null)
            {
                Debug.LogError($"GameDatabase.{label} array is null.", this);
                return lookup;
            }

            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                if (item == null)
                {
                    Debug.LogError($"GameDatabase.{label}[{i}] is null.", this);
                    continue;
                }

                TKey key = getKey(item);
                if (lookup.TryGetValue(key, out var existing))
                {
                    Debug.LogError(
                        $"GameDatabase.{label} has duplicate key '{key}' used by '{existing.name}' and '{item.name}'. Keeping first entry.",
                        item);
                    continue;
                }

                lookup.Add(key, item);
            }

            return lookup;
        }
    }
}
