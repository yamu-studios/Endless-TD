// ============================================================================
// ETD.Core - HubBadgeRegistry.cs  [NEW]
// Static registry — tracks which turrets/traits/challenges are "new" and
// haven't been viewed yet. Lives in Core so any window can read/write it.
// Persists via SaveData. Reset on run start.
// ============================================================================
using System.Collections.Generic;
using ETD.Core;

namespace ETD.Core
{
    public static class HubBadgeRegistry
    {
        // Runtime sets (loaded from save on hub open)
        private static HashSet<string> _newTurrets    = new();
        private static HashSet<string> _newTraits     = new();
        private static HashSet<string> _newChallenges = new();

        // =================================================================
        // LOAD / SAVE
        // =================================================================

        public static void LoadFromSave()
        {
            var save = SaveSystem.Load();
            _newTurrets    = new HashSet<string>(save.LastRunNewTurretIds    ?? System.Array.Empty<string>());
            _newTraits     = new HashSet<string>(save.LastRunNewTraitIds     ?? System.Array.Empty<string>());
            _newChallenges = new HashSet<string>(save.LastRunCompletedChallengeIds ?? System.Array.Empty<string>());

            // Remove already-viewed items
            var viewed = new HashSet<string>(save.ViewedNewTurretIds    ?? System.Array.Empty<string>());
            _newTurrets.ExceptWith(viewed);

            viewed = new HashSet<string>(save.ViewedNewTraitIds ?? System.Array.Empty<string>());
            _newTraits.ExceptWith(viewed);

            viewed = new HashSet<string>(save.ViewedCompletedChallengeIds ?? System.Array.Empty<string>());
            _newChallenges.ExceptWith(viewed);
        }

        // =================================================================
        // QUERY
        // =================================================================

        public static bool IsNew(HubBadgeType type, string id) => GetSet(type).Contains(id);

        public static bool AnyNew(HubBadgeType type) => GetSet(type).Count > 0;

        // =================================================================
        // MARK VIEWED — called when player opens a window or scrolls to item
        // =================================================================

        public static void MarkViewed(HubBadgeType type, string id)
        {
            if (!GetSet(type).Contains(id)) return;
            GetSet(type).Remove(id);

            // Persist to save so badge stays gone after re-opening hub
            var save = SaveSystem.Load();
            var list = new List<string>();

            switch (type)
            {
                case HubBadgeType.Turret:
                    list = new List<string>(save.ViewedNewTurretIds ?? System.Array.Empty<string>());
                    if (!list.Contains(id)) list.Add(id);
                    save.ViewedNewTurretIds = list.ToArray();
                    break;
                case HubBadgeType.Trait:
                    list = new List<string>(save.ViewedNewTraitIds ?? System.Array.Empty<string>());
                    if (!list.Contains(id)) list.Add(id);
                    save.ViewedNewTraitIds = list.ToArray();
                    break;
                case HubBadgeType.Challenge:
                    list = new List<string>(save.ViewedCompletedChallengeIds ?? System.Array.Empty<string>());
                    if (!list.Contains(id)) list.Add(id);
                    save.ViewedCompletedChallengeIds = list.ToArray();
                    break;
            }
            SaveSystem.Save(save);
        }

        public static void MarkAllViewed(HubBadgeType type)
        {
            var ids = new List<string>(GetSet(type));
            foreach (var id in ids) MarkViewed(type, id);
        }

        // =================================================================
        // REGISTER NEW ITEMS (called by ChallengeTracker / RunManager on unlock)
        // =================================================================

        public static void RegisterNew(HubBadgeType type, string id)
        {
            GetSet(type).Add(id);

            var save = SaveSystem.Load();
            List<string> list;
            switch (type)
            {
                case HubBadgeType.Turret:
                    list = new List<string>(save.LastRunNewTurretIds ?? System.Array.Empty<string>());
                    if (!list.Contains(id)) list.Add(id);
                    save.LastRunNewTurretIds = list.ToArray();
                    break;
                case HubBadgeType.Trait:
                    list = new List<string>(save.LastRunNewTraitIds ?? System.Array.Empty<string>());
                    if (!list.Contains(id)) list.Add(id);
                    save.LastRunNewTraitIds = list.ToArray();
                    break;
                case HubBadgeType.Challenge:
                    list = new List<string>(save.LastRunCompletedChallengeIds ?? System.Array.Empty<string>());
                    if (!list.Contains(id)) list.Add(id);
                    save.LastRunCompletedChallengeIds = list.ToArray();
                    break;
            }
            SaveSystem.Save(save);
        }

        // =================================================================
        // CLEAR (call at run START to reset last-run data)
        // =================================================================

        public static void ClearLastRunData()
        {
            _newTurrets.Clear();
            _newTraits.Clear();
            _newChallenges.Clear();

            var save = SaveSystem.Load();
            save.LastRunNewTurretIds            = System.Array.Empty<string>();
            save.LastRunNewTraitIds             = System.Array.Empty<string>();
            save.LastRunCompletedChallengeIds   = System.Array.Empty<string>();
            save.ViewedNewTurretIds             = System.Array.Empty<string>();
            save.ViewedNewTraitIds              = System.Array.Empty<string>();
            save.ViewedCompletedChallengeIds    = System.Array.Empty<string>();
            SaveSystem.Save(save);
        }

        private static HashSet<string> GetSet(HubBadgeType type) => type switch
        {
            HubBadgeType.Turret    => _newTurrets,
            HubBadgeType.Trait     => _newTraits,
            HubBadgeType.Challenge => _newChallenges,
            _ => _newChallenges
        };
    }
}
