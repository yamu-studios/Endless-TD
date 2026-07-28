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

        public WikiRow(string label, string value, string note = null)
        {
            Label = label;
            Value = value;
            Note = note;
        }
    }

    public sealed class WikiSection
    {
        public string Heading;
        public string Body;
        public readonly List<WikiRow> Rows = new();

        public WikiSection(string heading, string body = null)
        {
            Heading = heading;
            Body = body;
        }

        public WikiSection Row(string label, string value, string note = null)
        {
            Rows.Add(new WikiRow(label, value, note));
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
