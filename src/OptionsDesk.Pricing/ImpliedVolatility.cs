namespace OptionsDesk.Pricing;

public sealed class PricingException(string message) : Exception(message);

/// <summary>The vol at which Black-Scholes reproduces a market price.</summary>
public static class ImpliedVolatility
{
    public const double MinVol = 1e-4;
    public const double MaxVol = 5.0;

    public static (double Lower, double Upper) NoArbitrageBounds(OptionRight right, double spot, double strike,
        double t, double rate = 0, double div = 0)
    {
        var forwardSpot = spot * Math.Exp(-div * t);
        var pvStrike = strike * Math.Exp(-rate * t);
        return right == OptionRight.Call
            ? (Math.Max(forwardSpot - pvStrike, 0), forwardSpot)
            : (Math.Max(pvStrike - forwardSpot, 0), pvStrike);
    }

    /// <summary>Bisection on vol. Price is increasing in vol, so it always converges inside the bounds.</summary>
    public static double Solve(OptionRight right, double marketPrice, double spot, double strike, double t,
        double rate = 0, double div = 0)
    {
        if (t <= 0)
            throw new PricingException("option has expired");
        var (lower, upper) = NoArbitrageBounds(right, spot, strike, t, rate, div);
        if (marketPrice <= lower || marketPrice >= upper)
            throw new PricingException($"price {marketPrice} is outside the no-arbitrage range ({lower:F4}, {upper:F4})");
        if (BlackScholes.Price(right, spot, strike, t, MaxVol, rate, div) < marketPrice)
            throw new PricingException($"price {marketPrice} implies a vol above {MaxVol:P0}");

        double lo = MinVol, hi = MaxVol;
        for (var i = 0; i < 200 && hi - lo > 1e-10; i++)
        {
            var mid = (lo + hi) / 2;
            if (BlackScholes.Price(right, spot, strike, t, mid, rate, div) < marketPrice)
                lo = mid;
            else
                hi = mid;
        }
        return (lo + hi) / 2;
    }
}
