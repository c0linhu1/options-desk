using Microsoft.EntityFrameworkCore;
using OptionsDesk.Data;
using OptionsDesk.Pricing;

namespace OptionsDesk.Web.Services;

public sealed record PositionRow(
    string Symbol, string Underlying, int Quantity, int Multiplier, decimal AveragePrice, decimal Mark,
    decimal MarketValue, decimal RealizedPnl, decimal UnrealizedPnl);

/// <summary>Greeks of one position in book terms: delta in shares, vega and theta in dollars.</summary>
public sealed record Exposure(string Symbol, string Underlying, int Quantity, double Delta, double Gamma,
    double Vega, double Theta);

public sealed record GreekTotals(double Delta, double Gamma, double Vega, double Theta)
{
    public static GreekTotals Sum(IEnumerable<Exposure> rows)
    {
        var list = rows.ToList();
        return new GreekTotals(list.Sum(r => r.Delta), list.Sum(r => r.Gamma), list.Sum(r => r.Vega),
            list.Sum(r => r.Theta));
    }
}

public sealed record ScenarioGrid(string Underlying, decimal Spot, IReadOnlyList<double> SpotShocks,
    IReadOnlyList<double> VolShocks, double[][] Pnl);

public class PortfolioService(DeskDbContext db, MarketDataService market)
{
    public static readonly double[] SpotShocks = [-0.10, -0.05, -0.02, 0, 0.02, 0.05, 0.10];
    public static readonly double[] VolShocks = [-0.05, 0, 0.05];

    private sealed record BookEntry(Position Position, string Underlying);

    private Dictionary<string, BookEntry> Book()
    {
        var book = new Dictionary<string, BookEntry>();
        foreach (var t in db.Trades.AsNoTracking().OrderBy(t => t.Id))
        {
            if (!book.TryGetValue(t.Symbol, out var entry))
                book[t.Symbol] = entry = new BookEntry(new Position(t.Symbol, t.Multiplier), t.Underlying);
            entry.Position.Apply(t.Side, t.Quantity, t.Price);
        }
        return book;
    }

    public IReadOnlyList<PositionRow> Positions() =>
        Book().Select(kv =>
        {
            var (p, underlying) = (kv.Value.Position, kv.Value.Underlying);
            var mark = market.Value(kv.Key).Mid;
            return new PositionRow(kv.Key, underlying, p.Quantity, p.Multiplier, p.AveragePrice, mark,
                p.MarketValue(mark), p.RealizedPnl, p.UnrealizedPnl(mark));
        }).OrderBy(r => r.Underlying).ThenBy(r => r.Symbol).ToList();

    public IReadOnlyList<Exposure> Exposures() =>
        Book().Where(kv => kv.Value.Position.Quantity != 0).Select(kv =>
        {
            var p = kv.Value.Position;
            var q = market.Value(kv.Key);
            double units = p.Quantity * p.Multiplier;
            return new Exposure(kv.Key, kv.Value.Underlying, p.Quantity, units * q.Delta, units * q.Gamma,
                units * q.Vega, units * q.Theta);
        }).OrderBy(e => e.Underlying).ThenBy(e => e.Symbol).ToList();

    public IReadOnlyDictionary<string, GreekTotals> ByUnderlying(IEnumerable<Exposure> exposures) =>
        exposures.GroupBy(e => e.Underlying).ToDictionary(g => g.Key, GreekTotals.Sum);

    /// <summary>P&amp;L of every position on this underlying under a grid of spot and vol shocks.</summary>
    public ScenarioGrid Scenarios(string symbol)
    {
        var u = market.GetUnderlying(symbol);
        var legs = Book().Where(kv => kv.Value.Underlying == u.Symbol && kv.Value.Position.Quantity != 0)
            .Select(kv => (Symbol: kv.Key, kv.Value.Position)).ToList();

        double Value(double spot, double volShift)
        {
            var total = 0.0;
            foreach (var (s, p) in legs)
            {
                if (!OptionContract.IsOptionSymbol(s))
                {
                    total += p.Quantity * spot;
                    continue;
                }
                var c = OptionContract.Parse(s);
                var vol = Math.Max(market.SmileVol(u, c.Strike, c.Expiry) + volShift, VolSmile.MinVol);
                var price = BlackScholes.Price(c.Right, spot, (double)c.Strike, market.TimeToExpiry(c.Expiry), vol,
                    market.Rate, u.DividendYield);
                total += p.Quantity * c.Multiplier * price;
            }
            return total;
        }

        var spot0 = (double)u.Spot;
        var baseValue = Value(spot0, 0);
        var grid = VolShocks.Select(dv => SpotShocks.Select(ds => Value(spot0 * (1 + ds), dv) - baseValue).ToArray())
            .ToArray();
        return new ScenarioGrid(u.Symbol, u.Spot, SpotShocks, VolShocks, grid);
    }
}
