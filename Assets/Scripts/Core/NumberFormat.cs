// ============================================================================
// ETD.Core - NumberFormat.cs
// Single canonical compact number formatter for all UI (damage numbers, health
// bars, gold, score, stats). Handles K/M/B/T/Qa..Dc, then scientific notation,
// and is NaN/Infinity safe so garbage values can never reach the UI.
//
// Uses double internally so very large exponential late-game values stay exact
// enough to format (float damage/HP is widened before formatting).
// ============================================================================
using System;
using System.Globalization;

namespace ETD.Core
{
    public static class NumberFormat
    {
        // tier 0 = no suffix, 1 = K, 2 = M, ... 11 = Dc (1e33). Beyond -> scientific.
        private static readonly string[] Suffixes =
        {
            "", "K", "M", "B", "T", "Qa", "Qi", "Sx", "Sp", "Oc", "No", "Dc"
        };

        /// <summary>
        /// Compact human-readable form: 12, 1.2K, 12.3M, 4.5B, 8.1T, ... then
        /// scientific (1.23e42) past the named suffixes. Never returns NaN/Infinity text.
        /// </summary>
        public static string Compact(double value)
        {
            if (double.IsNaN(value)) return "0";
            if (double.IsPositiveInfinity(value)) return "∞";   // ∞
            if (double.IsNegativeInfinity(value)) return "-∞";

            bool negative = value < 0d;
            double abs = negative ? -value : value;

            string result;
            if (abs < 1000d)
            {
                // Whole numbers as integers; small fractional values keep one decimal.
                result = (abs >= 100d || abs == Math.Floor(abs))
                    ? abs.ToString("0", CultureInfo.InvariantCulture)
                    : abs.ToString("0.#", CultureInfo.InvariantCulture);
            }
            else
            {
                int tier = (int)Math.Floor(Math.Log10(abs) / 3d);
                double scaled = abs / Math.Pow(1000d, tier);

                // Guard rounding at tier boundaries so 999,999 shows "1M", not "1000K".
                // The 3-sig-fig integer format rounds at .5, so bump at 999.5, not 1000.
                if (scaled >= 999.5d) { tier++; scaled /= 1000d; }

                if (tier >= Suffixes.Length)
                {
                    result = abs.ToString("0.##e0", CultureInfo.InvariantCulture);
                }
                else
                {
                    // ~3 significant digits: 1.23K, 12.3M, 123M.
                    string fmt = scaled >= 100d ? "0" : (scaled >= 10d ? "0.#" : "0.##");
                    result = scaled.ToString(fmt, CultureInfo.InvariantCulture) + Suffixes[tier];
                }
            }

            return negative ? "-" + result : result;
        }

        public static string Compact(float value) => Compact((double)value);
        public static string Compact(int value) => Compact((double)value);
        public static string Compact(long value) => Compact((double)value);
    }
}
