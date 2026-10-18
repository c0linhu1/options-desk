using OptionsDesk.Data;
using OptionsDesk.Pricing;

namespace OptionsDesk.Web.Services;

public sealed record TradeTicket(string Symbol, Side Side, int Quantity, decimal Price, string Account,
    DateOnly? TradeDate = null);

public sealed record BookingResult(bool Booked, int? TradeId, string Symbol, DateOnly? SettleDate,
    IReadOnlyList<string> Problems)
{
    public static BookingResult Rejected(string symbol, IReadOnlyList<string> problems) =>
        new(false, null, symbol, null, problems);
}

/// <summary>Validates a ticket, runs pre-trade risk against the current book, and books it.</summary>
public class TradeBookingService(DeskDbContext db, MarketDataService market, PortfolioService portfolio,
    RiskLimits limits, TimeProvider time)
{
    public BookingResult Book(TradeTicket ticket, string trader)
    {
        var instrument = market.Resolve(ticket.Symbol);
        var symbol = instrument.Symbol;
        var tradeDate = ticket.TradeDate ?? market.Today;
        if (instrument.Contract is { } c && tradeDate > c.Expiry)
            return BookingResult.Rejected(symbol, [$"{symbol} expired on {c.Expiry:yyyy-MM-dd}"]);

        Order order;
        try
        {
            order = new Order(symbol, ticket.Side, ticket.Quantity, ticket.Price, trader, ticket.Account, tradeDate,
                instrument.Multiplier);
        }
        catch (ArgumentOutOfRangeException e)
        {
            return BookingResult.Rejected(symbol, [e.Message]);
        }

        var exposures = portfolio.Exposures();
        var ctx = new RiskContext(
            instrument.Mid, instrument.Delta, instrument.Vega,
            UnderlyingDelta: exposures.Where(e => e.Underlying == instrument.Underlying.Symbol).Sum(e => e.Delta),
            PortfolioVega: exposures.Sum(e => e.Vega),
            instrument.IsOption);
        var problems = PreTradeRisk.Check(order, ctx, limits);
        if (problems.Count > 0)
            return BookingResult.Rejected(symbol, problems);

        var trade = new Trade
        {
            Symbol = symbol,
            Underlying = instrument.Underlying.Symbol,
            Side = order.Side,
            Quantity = order.Quantity,
            Price = order.Price,
            Multiplier = order.Multiplier,
            Trader = trader,
            Account = order.Account,
            TradeDate = order.TradeDate,
            SettleDate = order.SettleDate,
            BookedAt = time.GetUtcNow(),
        };
        db.Trades.Add(trade);
        db.SaveChanges();
        return new BookingResult(true, trade.Id, symbol, trade.SettleDate, []);
    }
}
