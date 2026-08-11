// ============================================================================
// ETD.UI.Wiki - WikiModels.cs  [NEW]
// Display model for the in-game wiki. Pages emit sections of prose + labelled
// rows; the window prefab renders them without knowing what page it is showing.
//
// Every number in a page is COMPUTED from ETD.Data.TurretStatMath /
// BalanceConstants / the turret assets — never authored into a loc string — so
// rebalancing can't leave the wiki lying. Prose lives in loc keys with {0}
// placeholders filled by LocalizationManager.GetFormat.
// ============================================================================
using System.Collections.Generic;
using ETD.Core;

namespace ETD.UI.Wiki
{
    /// <summary>One labelled value inside a section, e.g. "Burn stack cap" / "5".</summary>
    public readonly struct WikiRow
    {
        public readonly string Label;
        public readonly string Value;
        /// <summary>Optional clarifier shown under the row, for rules a number can't carry.</summary>
        public readonly string Note;

        /// <summary>
        /// When set, the row renders as an icon plus its value instead of a text label —
        /// a damage glyph reads faster than the word "damage" and needs no translation.
        /// The Label is kept regardless and used as the accessible/tooltip name.
        /// </summary>
        public readonly UnityEngine.Sprite Icon;

        /// <summary>Tint for the icon, so damage/range/speed keep their established colours.</summary>
        public readonly UnityEngine.Color IconColor;

        /// <summary>
        /// Renders as a heading inside the section rather than a labelled value — used for
        /// the level markers above each row of stats. Kept explicit instead of inferring it
        /// from an empty Value, which would make the rendering depend on a coincidence.
        /// </summary>
        public readonly bool IsSubheading;

        public WikiRow(string label, string value, string note = null, bool isSubheading = false)
        {
            Label = label;
            Value = value;
            Note = note;
            Icon = null;
            IconColor = UnityEngine.Color.white;
            IsSubheading = isSubheading;
        }

        public WikiRow(UnityEngine.Sprite icon, UnityEngine.Color iconColor,
                       string label, string value, string note = null)
        {
            Label = label;
            Value = value;
            Note = note;
            Icon = icon;
            IconColor = iconColor;
            IsSubheading = false;
        }
    }

    /// <summary>
    /// A turret's forms shown side by side — base, Path A, Path B, Tier 2. Rendered as one
    /// strip so a player can see at a glance what the tower becomes, which is the thing the
    /// old Turrets tab communicated and a wall of text cannot.
    /// </summary>
    public readonly struct WikiIconEntry
    {
        public readonly UnityEngine.Sprite Icon;
        public readonly string Caption;
        public readonly string Name;

        /// <summary>
        /// Which position in the strip this form occupies: 0 base, 1 Evolution A,
        /// 2 Evolution B, 3 Evolution C. Fixed rather than sequential so a turret with a
        /// missing form leaves that slot empty instead of shifting the others along and
        /// mislabelling every icon after it.
        /// </summary>
        public readonly int Slot;

        public WikiIconEntry(UnityEngine.Sprite icon, string caption, string name, int slot)
        {
            Icon = icon;
            Caption = caption;
            Name = name;
            Slot = slot;
        }
    }

    public sealed class WikiSection
    {
        public string Heading;
        public string Body;
        public readonly List<WikiRow> Rows = new();

        /// <summary>
        /// Draws the whole section in the alert colour. Used for a locked turret's unlock
        /// requirement, which has to read as a gate rather than as one more stat — it sits
        /// directly above the forms strip, where anything in the normal palette blends into
        /// the page and gets scrolled past.
        /// </summary>
        public bool IsAlert;

        public WikiSection(string heading, string body = null, bool isAlert = false)
        {
            Heading = heading;
            Body = body;
            IsAlert = isAlert;
        }

        /// <summary>Icons for this section, rendered as a strip above its rows. Empty for
        /// text-only sections.</summary>
        public readonly List<WikiIconEntry> Icons = new();

        public WikiSection Row(string label, string value, string note = null)
        {
            Rows.Add(new WikiRow(label, value, note));
            return this;
        }

        /// <summary>Heading row inside a section, e.g. the level above a bar of stats.</summary>
        public WikiSection Subheading(string label)
        {
            Rows.Add(new WikiRow(label, "", null, true));
            return this;
        }

        /// <summary>Row headed by a stat glyph rather than a word. See <see cref="WikiRow.Icon"/>.</summary>
        public WikiSection StatRow(UnityEngine.Sprite icon, UnityEngine.Color iconColor,
                                   string label, string value, string note = null)
        {
            Rows.Add(new WikiRow(icon, iconColor, label, value, note));
            return this;
        }

        public WikiSection IconEntry(UnityEngine.Sprite icon, string caption, string name, int slot)
        {
            Icons.Add(new WikiIconEntry(icon, caption, name, slot));
            return this;
        }
    }

    /// <summary>
    /// A wiki page. <paramref name="mods"/> is null in the Hub (no active run), in
    /// which case pages show base values; during a run it is the live modifier set,
    /// so pages can additionally show what the player's current build actually does.
    /// </summary>
    public interface IWikiPage
    {
        string Id { get; }
        string Title { get; }
        IReadOnlyList<WikiSection> BuildSections(IRunStatModifiers mods);
    }

    /// <summary>
    /// Extra presentation a page can offer the list on the left. Optional: a page that
    /// does not implement it is listed as a plain label under the default category, so
    /// the mechanic pages did not have to change to gain grouping.
    /// </summary>
    public interface IWikiListEntry
    {
        /// <summary>Heading this page is filed under. Pages are grouped in the order the
        /// catalog returns them, so the first category listed is the one players land on.</summary>
        string Category { get; }

        /// <summary>Shown beside the title in the list. Null for text-only pages.</summary>
        UnityEngine.Sprite Icon { get; }

        /// <summary>Drawn dimmed with a lock marker. The point of listing locked entries at
        /// all is that players can see what they have not unlocked yet and why.</summary>
        bool IsLocked { get; }
    }

    /// <summary>Shared number formatting so every page reads consistently.</summary>
    public static class WikiFormat
    {
        // Stat numbers are formatted culture-invariantly so a decimal point stays a
        // point in every language. Without this, ToString() follows the player's OS
        // locale and the same stat renders as "2.00" or "2,00" depending on machine,
        // which makes shared builds and screenshots disagree.
        private static readonly System.Globalization.CultureInfo Invariant =
            System.Globalization.CultureInfo.InvariantCulture;

        private static string L(string key, string fallback) => LocalizationManager.Get(key, fallback);

        /// <summary>0.25 -> "25%".</summary>
        public static string Percent(float fraction, int decimals = 0)
            => (fraction * 100f).ToString("F" + decimals, Invariant) + "%";

        /// <summary>0.25 -> "+25%", -0.1 -> "-10%".</summary>
        public static string PercentSigned(float fraction, int decimals = 0)
            => (fraction >= 0f ? "+" : "") + (fraction * 100f).ToString("F" + decimals, Invariant) + "%";

        /// <summary>1.5 -> "x1.50".</summary>
        public static string Multiplier(float value) => "x" + value.ToString("0.00", Invariant);

        /// <summary>
        /// Multiplier for values that reach endless-run scale. Two decimals stop being
        /// information once the number passes a thousand — "x974044.50" is harder to read
        /// than "x974K" and implies a precision the curve does not have.
        /// </summary>
        public static string MultiplierLarge(float value)
            => value >= 1000f ? "x" + NumberFormat.Compact(value) : Multiplier(value);

        public static string Number(float value, int decimals = 1)
            => value.ToString("F" + decimals, Invariant);

        public static string PerLevel(float fraction)
            => LocalizationManager.GetFormat("wiki_per_level", "{0} per level", PercentSigned(fraction, 2));

        public static string Seconds(float value)
            => LocalizationManager.GetFormat("wiki_seconds", "{0}s", value.ToString("0.##", Invariant));

        /// <summary>Label for values that only exist while a run is active.</summary>
        public static string YourBuildLabel => L("wiki_your_build", "Your current build");

        public static string BaseLabel => L("wiki_base", "Base");
    }
}
