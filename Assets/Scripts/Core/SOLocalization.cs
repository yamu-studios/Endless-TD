using UnityEngine;

namespace ETD.Core
{
    public static class SOLocalization
    {
        public static string GetName(string key, string fallback)
            => LocalizationManager.Get(key + "_name", fallback);

        public static string GetDesc(string key, string fallback)
            => LocalizationManager.Get(key + "_desc", fallback);

        public static string GetUnlock(string key, string fallback)
            => LocalizationManager.Get(key + "_unlock", fallback);

        public static string GetCondition(string key, string fallback)
            => LocalizationManager.Get(key + "_condition", fallback);

        public static string GetRarity(string key, string fallback)
    => LocalizationManager.Get(key + "_rarity", fallback);
    }
}