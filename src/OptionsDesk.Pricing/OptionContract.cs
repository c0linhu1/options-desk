using System.Globalization;
using System.Text.RegularExpressions;

namespace OptionsDesk.Pricing;

public enum OptionRight
{
    Call,
    Put,
}

/// <summary>
/// A listed equity option. The OCC symbol is the root padded to 6 characters, the expiry as
/// YYMMDD, C or P, and the strike times 1000 as 8 digits: "AAPL  261218C00200000" is the
/// AAPL Dec 18 2026 200 call.
/// </summary>
public sealed partial record OptionContract
{
    public string Underlying { get; }
    public DateOnly Expiry { get; }
    public decimal Strike { get; }
    public OptionRight Right { get; }
    public int Multiplier { get; }

    public OptionContract(string underlying, DateOnly expiry, decimal strike, OptionRight right, int multiplier = 100)
    {
        if (string.IsNullOrWhiteSpace(underlying) || underlying.Length > 6)
            throw new ArgumentException($"bad underlying '{underlying}'", nameof(underlying));
        if (strike <= 0)
            throw new ArgumentOutOfRangeException(nameof(strike), "strike must be positive");
        Underlying = underlying.ToUpperInvariant();
        Expiry = expiry;
        Strike = strike;
        Right = right;
        Multiplier = multiplier;
    }

    public string Symbol =>
        $"{Underlying,-6}{Expiry:yyMMdd}{(Right == OptionRight.Call ? 'C' : 'P')}{(long)Math.Round(Strike * 1000):D8}";

    public static bool IsOptionSymbol(string symbol) => OccPattern().IsMatch(symbol.Trim().ToUpperInvariant());

    public static OptionContract Parse(string symbol)
    {
        var m = OccPattern().Match(symbol.Trim().ToUpperInvariant());
        if (!m.Success)
            throw new FormatException($"not an OCC option symbol: '{symbol}'");
        var expiry = DateOnly.ParseExact(m.Groups[2].Value, "yyMMdd", CultureInfo.InvariantCulture);
        var right = m.Groups[3].Value == "C" ? OptionRight.Call : OptionRight.Put;
        return new OptionContract(m.Groups[1].Value, expiry, long.Parse(m.Groups[4].Value) / 1000m, right);
    }

    public decimal Intrinsic(decimal spot) =>
        Right == OptionRight.Call ? Math.Max(spot - Strike, 0) : Math.Max(Strike - spot, 0);

    public override string ToString() => Symbol;

    [GeneratedRegex(@"^([A-Z0-9.]{1,6})\s*(\d{6})([CP])(\d{8})$")]
    private static partial Regex OccPattern();
}
