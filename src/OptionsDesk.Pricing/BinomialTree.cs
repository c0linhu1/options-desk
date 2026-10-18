namespace OptionsDesk.Pricing;

/// <summary>
/// Cox-Ross-Rubinstein tree for American options, which can be exercised early. Listed US
/// equity options are American, so this is the reference for puts and for calls on dividend
/// payers; Black-Scholes is the fast European approximation.
/// </summary>
public static class BinomialTree
{
    public static double AmericanPrice(OptionRight right, double spot, double strike, double t, double vol,
        double rate = 0, double div = 0, int steps = 400)
    {
        var sign = right == OptionRight.Call ? 1 : -1;
        if (t <= 0 || vol <= 0)
            return Math.Max(sign * (spot - strike), 0);

        var dt = t / steps;
        var up = Math.Exp(vol * Math.Sqrt(dt));
        var down = 1 / up;
        var p = (Math.Exp((rate - div) * dt) - down) / (up - down);
        if (p <= 0 || p >= 1)
            throw new PricingException("tree is unstable for these inputs; use more steps");
        var disc = Math.Exp(-rate * dt);

        var values = new double[steps + 1];
        for (var i = 0; i <= steps; i++)
            values[i] = Math.Max(sign * (spot * Math.Pow(up, steps - 2 * i) - strike), 0);

        for (var n = steps - 1; n >= 0; n--)
        {
            for (var i = 0; i <= n; i++)
            {
                var hold = disc * (p * values[i] + (1 - p) * values[i + 1]);
                var exercise = sign * (spot * Math.Pow(up, n - 2 * i) - strike);
                values[i] = Math.Max(hold, exercise);
            }
        }
        return values[0];
    }
}
