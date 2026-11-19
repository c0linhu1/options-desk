using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OptionsDesk.Data;
using OptionsDesk.Pricing;

namespace OptionsDesk.Web.Services;

public sealed record OptionQuote(
    OptionContract Contract, decimal Bid, decimal Ask, double Theo, double Vol,
    double Delta, double Gamma, double Vega, double Theta)
{
    public decimal Mid => (Bid + Ask) / 2;
}

public sealed record ChainRow(decimal Strike, OptionQuote Call, OptionQuote Put);

/// <summary>
/// A tradable instrument resolved to its underlying, mid, and per-share Greeks. <paramref name="Expiry"/> says
/// how an expired option settled; it's null for anything still trading.
/// </summary>
public sealed record InstrumentQuote(
    Underlying Underlying, OptionContract? Contract, decimal Mid,
    double Delta, double Gamma, double Vega, double Theta, string? Expiry = null)
{
    public bool IsOption => Contract is not null;
    public int Multiplier => Contract?.Multiplier ?? 1;
    public string Symbol => Contract?.Symbol ?? Underlying.Symbol;
}

/// <summary>
/// Synthetic market data: listed expiries, strikes, and two-sided option quotes priced off each
/// underlying's vol smile.
/// </summary>
public class MarketDataService(DeskDbContext db, IValuationClock clock, IOptions<DeskOptions> options)
{
    private readonly DeskOptions _options = options.Value;

    public DateOnly Today => clock.Today;
    public double Rate => _options.RiskFreeRate;

    public IReadOnlyList<Underlying> Underlyings() =>
        db.Underlyings.AsNoTracking().OrderBy(u => u.Symbol).ToList();

    public Underlying GetUnderlying(string symbol) =>
        db.Underlyings.AsNoTracking().SingleOrDefault(u => u.Symbol == symbol.ToUpper())
        ?? throw new DeskException(DeskErrorKind.NotFound, $"unknown underlying {symbol}");

    public IReadOnlyList<DateOnly> ListedExpiries() =>
        MarketCalendar.NextMonthlyExpiries(Today, _options.ExpiriesListed);

    public static decimal StrikeStep(decimal spot) => spot switch
    {
        < 25 => 1m,
        < 100 => 2.5m,
        < 250 => 5m,
        _ => 10m,
    };

    public IReadOnlyList<decimal> Strikes(decimal spot)
    {
        var step = StrikeStep(spot);
        var center = Math.Round(spot / step) * step;
        return Enumerable.Range(-_options.StrikesEachSide, 2 * _options.StrikesEachSide + 1)
            .Select(i => center + i * step)
            .Where(k => k > 0)
            .ToList();
    }

    public double TimeToExpiry(DateOnly expiry) => MarketCalendar.YearFraction(Today, expiry);

    public double SmileVol(Underlying u, decimal strike, DateOnly expiry) =>
        u.Smile.Vol((double)u.Spot, (double)strike, TimeToExpiry(expiry), Rate, u.DividendYield);

    public OptionQuote Quote(Underlying u, OptionContract c)
    {
        var vol = SmileVol(u, c.Strike, c.Expiry);
        var g = BlackScholes.Greeks(c.Right, (double)u.Spot, (double)c.Strike, TimeToExpiry(c.Expiry), vol, Rate,
            u.DividendYield);
        var (bid, ask) = QuoteBuilder.TwoSided(g.Price);
        return new OptionQuote(c, bid, ask, g.Price, vol, g.Delta, g.Gamma, g.Vega, g.Theta);
    }

    public IReadOnlyList<ChainRow> Chain(Underlying u, DateOnly expiry)
    {
        if (!ListedExpiries().Contains(expiry))
            throw new DeskException(DeskErrorKind.Invalid, $"{expiry:yyyy-MM-dd} is not a listed expiry");
        return Strikes(u.Spot)
            .Select(k => new ChainRow(k,
                Quote(u, new OptionContract(u.Symbol, expiry, k, OptionRight.Call)),
                Quote(u, new OptionContract(u.Symbol, expiry, k, OptionRight.Put))))
            .ToList();
    }

    /// <summary>Resolves a stock ticker or OCC option symbol for a new order. Options must be on a listed expiry.</summary>
    public InstrumentQuote Resolve(string symbol) => Resolve(symbol, requireListed: true);

    /// <summary>
    /// Values an instrument the book already holds. Unlike <see cref="Resolve(string)"/> this accepts any expiry,
    /// because a position outlives the listing: an option that has expired is marked at its settlement value.
    /// </summary>
    public InstrumentQuote Value(string symbol) => Resolve(symbol, requireListed: false);

    /// <summary>
    /// What an expired option turned into: +1 for a call and -1 for a put that settled in the money, since those
    /// are exercised into stock bought (or sold) at the strike; 0 for one that expired worthless.
    /// </summary>
    public int ExercisedDirection(Underlying u, OptionContract c) =>
        c.Intrinsic(SettlementPrice(u, c.Expiry)) == 0 ? 0 : c.Right == OptionRight.Call ? 1 : -1;

    /// <summary>
    /// The underlying's price options expiring on <paramref name="expiry"/> settle at. The desk keeps no price
    /// history, so the first time an expired contract is valued, the underlying's price then is recorded as its
    /// settlement and used from then on: an expired option's value must stop following the stock.
    /// </summary>
    public decimal SettlementPrice(Underlying u, DateOnly expiry)
    {
        var recorded = db.Settlements.AsNoTracking()
            .Where(s => s.Underlying == u.Symbol && s.Expiry == expiry)
            .Select(s => (decimal?)s.Price)
            .SingleOrDefault();
        if (recorded is { } price)
            return price;

        // One statement, so two requests settling the same expiry at once can't collide; the first one wins.
        db.Database.ExecuteSql($"""
            INSERT INTO "Settlements" ("Underlying", "Expiry", "Price") VALUES ({u.Symbol}, {expiry}, {u.Spot})
            ON CONFLICT DO NOTHING
            """);
        return db.Settlements.AsNoTracking().Single(s => s.Underlying == u.Symbol && s.Expiry == expiry).Price;
    }

    private InstrumentQuote Resolve(string symbol, bool requireListed)
    {
        if (!OptionContract.IsOptionSymbol(symbol))
        {
            var stock = GetUnderlying(symbol);
            return new InstrumentQuote(stock, null, stock.Spot, 1, 0, 0, 0);
        }

        var contract = OptionContract.Parse(symbol);
        var u = GetUnderlying(contract.Underlying);
        if (requireListed && !ListedExpiries().Contains(contract.Expiry))
            throw new DeskException(DeskErrorKind.Invalid, $"{contract.Expiry:yyyy-MM-dd} is not a listed expiry");

        if (contract.Expiry < Today)
        {
            // Settlement decides whether it was exercised; after that an exercised option is stock bought (or sold)
            // at the strike, so it's worth spot less strike and moves one for one with the stock.
            var direction = ExercisedDirection(u, contract);
            return direction == 0
                ? new InstrumentQuote(u, contract, 0, 0, 0, 0, 0, "expired worthless")
                : new InstrumentQuote(u, contract, direction * (u.Spot - contract.Strike), direction, 0, 0, 0,
                    "exercised");
        }

        var q = Quote(u, contract);
        return new InstrumentQuote(u, contract, q.Mid, q.Delta, q.Gamma, q.Vega, q.Theta);
    }
}
