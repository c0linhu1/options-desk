namespace OptionsDesk.Pricing;

/// <summary>
/// Per-share value and Greeks, quoted the way the desk reads them: vega per 1 vol point,
/// theta per calendar day, rho per 1% move in rates.
/// </summary>
public sealed record Greeks(double Price, double Delta, double Gamma, double Vega, double Theta, double Rho);

/// <summary>
/// Black-Scholes-Merton for European options on a stock with a continuous dividend yield.
/// Vol and rates are decimals (0.25 = 25%), time is in years.
/// </summary>
public static class BlackScholes
{
    public static double Price(OptionRight right, double spot, double strike, double t, double vol,
        double rate = 0, double div = 0) => Greeks(right, spot, strike, t, vol, rate, div).Price;

    public static Greeks Greeks(OptionRight right, double spot, double strike, double t, double vol,
        double rate = 0, double div = 0)
    {
        if (spot <= 0 || strike <= 0)
            throw new ArgumentOutOfRangeException(nameof(spot), "spot and strike must be positive");
        if (vol < 0 || t < 0)
            throw new ArgumentOutOfRangeException(nameof(vol), "vol and time can't be negative");

        var call = right == OptionRight.Call;
        var dfRate = Math.Exp(-rate * t);
        var dfDiv = Math.Exp(-div * t);

        if (t == 0 || vol == 0)
        {
            // No optionality left: the discounted forward intrinsic value, with a step delta.
            var forwardValue = spot * dfDiv - strike * dfRate;
            var value = Math.Max(call ? forwardValue : -forwardValue, 0);
            var itm = call ? forwardValue > 0 : forwardValue < 0;
            var stepDelta = itm ? (call ? dfDiv : -dfDiv) : 0;
            return new Greeks(value, stepDelta, 0, 0, 0, 0);
        }

        var sqrtT = Math.Sqrt(t);
        var d1 = (Math.Log(spot / strike) + (rate - div + 0.5 * vol * vol) * t) / (vol * sqrtT);
        var d2 = d1 - vol * sqrtT;
        var pdf = Normal.Pdf(d1);
        var gamma = dfDiv * pdf / (spot * vol * sqrtT);
        var vega = spot * dfDiv * pdf * sqrtT;
        var decay = -spot * dfDiv * pdf * vol / (2 * sqrtT);

        double price, delta, theta, rho;
        if (call)
        {
            price = spot * dfDiv * Normal.Cdf(d1) - strike * dfRate * Normal.Cdf(d2);
            delta = dfDiv * Normal.Cdf(d1);
            theta = decay - rate * strike * dfRate * Normal.Cdf(d2) + div * spot * dfDiv * Normal.Cdf(d1);
            rho = strike * t * dfRate * Normal.Cdf(d2);
        }
        else
        {
            price = strike * dfRate * Normal.Cdf(-d2) - spot * dfDiv * Normal.Cdf(-d1);
            delta = -dfDiv * Normal.Cdf(-d1);
            theta = decay + rate * strike * dfRate * Normal.Cdf(-d2) - div * spot * dfDiv * Normal.Cdf(-d1);
            rho = -strike * t * dfRate * Normal.Cdf(-d2);
        }
        return new Greeks(price, delta, gamma, vega / 100, theta / 365, rho / 100);
    }
}

public static class Normal
{
    public static double Pdf(double x) => Math.Exp(-0.5 * x * x) / Math.Sqrt(2 * Math.PI);

    /// <summary>Standard normal CDF, Hart's double-precision algorithm (West, 2005). Error ~1e-15.</summary>
    public static double Cdf(double x)
    {
        var z = Math.Abs(x);
        double tail;
        if (z > 37)
        {
            tail = 0;
        }
        else
        {
            var e = Math.Exp(-z * z / 2);
            if (z < 7.07106781186547)
            {
                var n = ((((((0.0352624965998911 * z + 0.700383064443688) * z + 6.37396220353165) * z
                    + 33.912866078383) * z + 112.079291497871) * z + 221.213596169931) * z + 220.206867912376);
                var d = (((((((0.0883883476483184 * z + 1.75566716318264) * z + 16.064177579207) * z
                    + 86.7807322029461) * z + 296.564248779674) * z + 637.333633378831) * z
                    + 793.826512519948) * z + 440.413735824752);
                tail = e * n / d;
            }
            else
            {
                var b = z + 0.65;
                b = z + 4 / b;
                b = z + 3 / b;
                b = z + 2 / b;
                b = z + 1 / b;
                tail = e / b / 2.506628274631;
            }
        }
        return x > 0 ? 1 - tail : tail;
    }
}
