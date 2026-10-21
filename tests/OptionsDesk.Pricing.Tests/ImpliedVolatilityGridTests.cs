using OptionsDesk.Pricing;

namespace OptionsDesk.Pricing.Tests;

/// <summary>
/// Checks the Newton solver across a wide grid against the previous bisection solver, kept here as a reference.
/// </summary>
public class ImpliedVolatilityGridTests
{
    private const double Spot = 100;

    private static readonly double[] Strikes = [1, 5, 20, 50, 80, 95, 99.9, 100, 100.1, 105, 120, 200, 500, 2000, 10000];
    private static readonly double[] Times = [0.5 / 365, 1 / 365.0, 2 / 365.0, 7 / 365.0, 30 / 365.0, 0.25, 1, 3, 10, 30];
    private static readonly double[] Vols = [0.0002, 0.005, 0.01, 0.05, 0.2, 0.6, 1.5, 3, 4.5, 4.99];
    private static readonly (double Rate, double Div)[] Carry = [(0, 0), (0.04, 0.01), (0.08, 0.03), (-0.01, 0), (0.05, 0.12), (0.2, 0)];

    private sealed record Case(OptionRight Right, double Strike, double T, double Vol, double Rate, double Div, double Price);

    /// <summary>Every grid point whose model price is one the solver accepts (strictly inside the no-arbitrage range).</summary>
    private static IEnumerable<Case> Grid(double[] strikes, double[] vols)
    {
        foreach (var right in new[] { OptionRight.Call, OptionRight.Put })
            foreach (var k in strikes)
                foreach (var t in Times)
                    foreach (var vol in vols)
                        foreach (var (rate, div) in Carry)
                        {
                            var price = BlackScholes.Price(right, Spot, k, t, vol, rate, div);
                            var (lower, upper) = ImpliedVolatility.NoArbitrageBounds(right, Spot, k, t, rate, div);
                            if (price <= lower || price >= upper)
                                continue;
                            if (BlackScholes.Price(right, Spot, k, t, ImpliedVolatility.MaxVol, rate, div) < price)
                                continue;
                            yield return new Case(right, k, t, vol, rate, div, price);
                        }
    }

    /// <summary>The solver this PR replaces.</summary>
    private static double ReferenceBisection(Case c)
    {
        double lo = ImpliedVolatility.MinVol, hi = ImpliedVolatility.MaxVol;
        for (var i = 0; i < 200 && hi - lo > 1e-10; i++)
        {
            var mid = (lo + hi) / 2;
            if (BlackScholes.Price(c.Right, Spot, c.Strike, c.T, mid, c.Rate, c.Div) < c.Price)
                lo = mid;
            else
                hi = mid;
        }
        return (lo + hi) / 2;
    }

    [Fact]
    public void ReproducesPricesAndAgreesWithBisectionAcrossTheGrid()
    {
        var cases = Grid(Strikes, Vols).ToList();
        Assert.True(cases.Count > 10_000, $"grid has {cases.Count} cases");

        foreach (var c in cases)
        {
            var vol = ImpliedVolatility.Solve(c.Right, c.Price, Spot, c.Strike, c.T, c.Rate, c.Div);
            Assert.InRange(vol, ImpliedVolatility.MinVol, ImpliedVolatility.MaxVol);

            var repriced = BlackScholes.Price(c.Right, Spot, c.Strike, c.T, vol, c.Rate, c.Div);
            Assert.True(Math.Abs(repriced - c.Price) <= 1e-8 * Math.Max(1, c.Price), $"{c}: repriced {repriced}");

            // Where vega is meaningful, implied vol is well defined: both solvers must recover it.
            var vegaPerUnitVol = BlackScholes.Greeks(c.Right, Spot, c.Strike, c.T, c.Vol, c.Rate, c.Div).Vega * 100;
            if (vegaPerUnitVol > 1e-3)
            {
                Assert.True(Math.Abs(vol - c.Vol) < 1e-8, $"{c}: solved {vol}");
                Assert.True(Math.Abs(vol - ReferenceBisection(c)) < 1e-8, $"{c}: bisection {ReferenceBisection(c)}");
            }
        }
    }

    [Fact]
    public void IsNeverSlowerThanBisectionForStrikesAndVolsADeskQuotes()
    {
        // Strikes 0.5x to 2x spot, vols 5% to 150%, prices of at least a cent. Bisection always takes 36 evaluations.
        var cases = Grid([50, 80, 95, 99.9, 100, 100.1, 105, 120, 200], [0.05, 0.2, 0.6, 1.5])
            .Where(c => c.Price >= 0.01)
            .ToList();
        var evaluations = cases.Select(c =>
        {
            ImpliedVolatility.Solve(c.Right, c.Price, Spot, c.Strike, c.T, c.Rate, c.Div, out var n);
            return n;
        }).ToList();

        Assert.True(evaluations.Max() <= 36, $"worst case {evaluations.Max()} evaluations");
        Assert.True(evaluations.Average() < 10, $"mean {evaluations.Average():F1} evaluations");
    }

    [Fact]
    public void HandlesVolsBelowTheSolverFloor()
    {
        // An at-the-money option worth 1e-12 implies a vol far under MinVol (at MinVol it's worth about 0.003),
        // so the answer settles on the floor, as it did with bisection.
        var vol = ImpliedVolatility.Solve(OptionRight.Call, 1e-12, Spot, 100, 0.5);
        Assert.InRange(vol, ImpliedVolatility.MinVol, ImpliedVolatility.MinVol + 1e-9);
    }
}
