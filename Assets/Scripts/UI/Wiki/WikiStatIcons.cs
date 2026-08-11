// ============================================================================
// ETD.UI.Wiki - WikiStatIcons.cs
// The damage / range / fire-rate glyphs the Turrets tab used, in one place so the
// wiki shows the same symbol and colour for the same stat.
//
// Loaded by path rather than assigned in the inspector: wiki pages are plain C#
// objects built by WikiCatalog, with no serialized fields to hang sprites off.
// Results are cached, so the lookup cost is paid once per session.
// ============================================================================
using UnityEngine;

namespace ETD.UI.Wiki
{
    public static class WikiStatIcons
    {
        private const string Folder = "Assets/Art/Textures/Icons/MenuIcons/";

        // Same colours the Turrets tab used, so a stat reads identically in both places.
        public static readonly Color DamageColor = new Color(1f, 0.353f, 0.373f);
        public static readonly Color RangeColor = new Color(0.655f, 1f, 0.310f);
        public static readonly Color SpeedColor = new Color(0.310f, 0.875f, 1f);
        public static readonly Color NeutralColor = new Color(1f, 0.784f, 0.341f);

        private static Sprite _damage, _range, _speed;
        private static bool _loaded;

        public static Sprite Damage { get { EnsureLoaded(); return _damage; } }
        public static Sprite Range { get { EnsureLoaded(); return _range; } }
        public static Sprite Speed { get { EnsureLoaded(); return _speed; } }

        private static void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;

#if UNITY_EDITOR
            _damage = Load("damage_per_second_icon");
            _range = Load("range_icon");
            _speed = Load("fire_rate_icon");
#endif
        }

#if UNITY_EDITOR
        private static Sprite Load(string name)
            => UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(Folder + name + ".png");
#endif

        /// <summary>
        /// Runtime injection point. A build has no AssetDatabase, so WikiIconBinder (a
        /// component on the wiki window) hands the sprites over from serialized fields
        /// during Awake. Editor-only loading above just keeps the editor preview honest
        /// when that component has not run yet.
        /// </summary>
        public static void Bind(Sprite damage, Sprite range, Sprite speed)
        {
            if (damage != null) _damage = damage;
            if (range != null) _range = range;
            if (speed != null) _speed = speed;
            _loaded = true;
        }
    }
}
