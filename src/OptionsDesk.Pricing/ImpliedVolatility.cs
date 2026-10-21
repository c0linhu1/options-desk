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

    /// <summary>Solver stops when a step moves vol by less than this (the old bisection's bracket width).</summary>
    public const double Tolerance = 1e-10;

    private const int MaxNewtonIterations = 100;

    /// <summary>
    /// Solves for implied vol with safeguarded Newton-Raphson (Numerical Recipes' <c>rtsafe</c>): Newton steps on
    /// vega, inside a bracket that always contains the answer, falling back to bisection whenever a Newton step would
    /// leave the bracket or isn't shrinking at least as fast as bisection would.
    /// </summary>
    public static double Solve(OptionRight right, double marketPrice, double spot, double strike, double t,
        double rate = 0, double div = 0) => Solve(right, marketPrice, spot, strike, t, rate, div, out _);

    internal static double Solve(OptionRight right, double marketPrice, double spot, double strike, double t,
        double rate, double div, out int evaluations)
    {
        if (t <= 0)
            throw new PricingException("option has expired");
        var (lower, upper) = NoArbitrageBounds(right, spot, strike, t, rate, div);
        if (marketPrice <= lower || marketPrice >= upper)
            throw new PricingException($"price {marketPrice} is outside the no-arbitrage range ({lower:F4}, {upper:F4})");
        if (BlackScholes.Price(right, spot, strike, t, MaxVol, rate, div) < marketPrice)
            throw new PricingException($"price {marketPrice} implies a vol above {MaxVol:P0}");

        double lo = MinVol, hi = MaxVol;
        var vol = InitialGuess(marketPrice, lower, spot, t, div);
        var step = hi - lo;
        evaluations = 0;
        for (var i = 0; i < MaxNewtonIterations; i++)
        {
            var g = BlackScholes.Greeks(right, spot, strike, t, vol, rate, div);
            evaluations++;
            var diff = g.Price - marketPrice;
            if (diff == 0)
                return vol;

            // Price rises with vol, so a model price above the market means the answer is below this vol.
            if (diff > 0)
                hi = vol;
            else
                lo = vol;

            // Greeks.Vega is per vol point (0.01); Newton needs the derivative per unit of vol.
            var newtonStep = diff / (g.Vega * 100);
            var previousStep = step;
            var next = vol - newtonStep;

            // Written as !(...) so that a NaN or infinite step, when vega underflows to zero, also bisects.
            if (!(next > lo && next < hi) || !(2 * Math.Abs(newtonStep) <= Math.Abs(previousStep)))
                next = (lo + hi) / 2;

            step = next - vol;
            if (Math.Abs(step) < Tolerance)
                return next;
            vol = next;
        }

        // Newton has never needed this many steps in testing; finishing by bisection guarantees termination.
        while (hi - lo > Tolerance)
        {
            var mid = (lo + hi) / 2;
            evaluations++;
            if (BlackScholes.Price(right, spot, strike, t, mid, rate, div) < marketPrice)
                lo = mid;
            else
                hi = mid;
        }
        return (lo + hi) / 2;
    }

    /// <summary>
    /// Brenner-Subrahmanyam: an at-the-money option is worth about S * vol * sqrt(t / 2pi). Applied to the time value
    /// (price above the no-arbitrage lower bound) so in-the-money options don't start with a far-too-high guess.
    /// </summary>
    private static double InitialGuess(double marketPrice, double lowerBound, double spot, double t, double div)
    {
        var guess = Math.Sqrt(2 * Math.PI / t) * (marketPrice - lowerBound) / (spot * Math.Exp(-div * t));
        return Math.Clamp(guess, 0.01, 3.0);
    }
}
