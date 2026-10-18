using System.Globalization;

namespace OptionsDesk.Web.Models;

/// <summary>Display formatting shared by the views. Culture-independent so every desk sees the same numbers.</summary>
public static class Fmt
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public static string Money(decimal x) => x < 0 ? $"-${(-x).ToString("N0", Us)}" : $"${x.ToString("N0", Us)}";

    public static string Money(double x) => Money((decimal)x);

    public static string Price(decimal x) => x.ToString("F2", Us);

    public static string Pct(double x, int decimals = 1) => (x * 100).ToString($"F{decimals}", Us) + "%";

    public static string Num(double x, int decimals = 0) => x.ToString($"N{decimals}", Us);

    public static string Greek(double x) => x.ToString("F3", Us);

    /// <summary>"pos" or "neg" for colouring P&amp;L.</summary>
    public static string Sign(decimal x) => x > 0 ? "pos" : x < 0 ? "neg" : "";

    public static string Sign(double x) => Sign((decimal)x);
}
