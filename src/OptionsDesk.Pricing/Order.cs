namespace OptionsDesk.Pricing;

public enum Side
{
    Buy,
    Sell,
}

public static class SideExtensions
{
    public static int Sign(this Side side) => side == Side.Buy ? 1 : -1;
}

/// <summary>An order. Quantity is shares for stock and contracts for options.</summary>
public sealed record Order
{
    public string Symbol { get; }
    public Side Side { get; }
    public int Quantity { get; }
    public decimal Price { get; }
    public string Trader { get; }
    public string Account { get; }
    public DateOnly TradeDate { get; }
    public int Multiplier { get; }

    public Order(string symbol, Side side, int quantity, decimal price, string trader, string account,
        DateOnly tradeDate, int multiplier = 1)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), "quantity must be positive");
        if (price <= 0)
            throw new ArgumentOutOfRangeException(nameof(price), "price must be positive");
        Symbol = symbol;
        Side = side;
        Quantity = quantity;
        Price = price;
        Trader = trader;
        Account = account;
        TradeDate = tradeDate;
        Multiplier = multiplier;
    }

    public DateOnly SettleDate => MarketCalendar.SettlementDate(TradeDate);

    public decimal Notional => Quantity * Price * Multiplier;
}
