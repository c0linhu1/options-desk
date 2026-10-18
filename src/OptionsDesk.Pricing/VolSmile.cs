namespace OptionsDesk.Pricing;

/// <summary>
/// Parametric smile: vol = base + skew * m + curvature * m^2, where m = ln(K / F) / sqrt(t) is
/// moneyness scaled by time, so the smile flattens for longer expiries.
/// </summary>
public sealed record VolSmile(double BaseVol, double Skew, double Curvature)
{
    public const double MinVol = 0.05;

    public double Vol(double spot, double strike, double t, double rate, double div)
    {
        var forward = spot * Math.Exp((rate - div) * t);
        var m = Math.Log(strike / forward) / Math.Sqrt(Math.Max(t, 1 / 365.0));
        return Math.Max(BaseVol + Skew * m + Curvature * m * m, MinVol);
    }
}

/// <summary>Turns a theoretical value into a listed two-sided quote.</summary>
public static class QuoteBuilder
{
    /// <summary>Penny pilot style: $0.05 at or above $3, $0.01 below.</summary>
    public static decimal Tick(decimal price) => price >= 3 ? 0.05m : 0.01m;

    public static (decimal Bid, decimal Ask) TwoSided(double theo)
    {
        var value = (decimal)theo;
        var half = Math.Max(0.02m * value, 0.025m);
        var tick = Tick(value);
        var bid = Math.Floor((value - half) / tick) * tick;
        var ask = Math.Ceiling((value + half) / tick) * tick;
        return (Math.Max(bid, 0), Math.Max(ask, tick));
    }
}
