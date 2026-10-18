using OptionsDesk.Pricing;

namespace OptionsDesk.Data;

/// <summary>A stock or ETF the desk trades, with the market data its option chain is priced from.</summary>
public class Underlying
{
    public required string Symbol { get; set; }
    public required string Name { get; set; }
    public required string Sector { get; set; }
    public decimal Spot { get; set; }
    public double DividendYield { get; set; }
    public double BaseVol { get; set; }
    public double Skew { get; set; }
    public double Curvature { get; set; }

    public VolSmile Smile => new(BaseVol, Skew, Curvature);
}

/// <summary>A booked trade. Symbol is the stock ticker or the OCC option symbol.</summary>
public class Trade
{
    public int Id { get; set; }
    public required string Symbol { get; set; }
    public required string Underlying { get; set; }
    public Side Side { get; set; }
    public int Quantity { get; set; }
    public decimal Price { get; set; }
    public int Multiplier { get; set; }
    public required string Trader { get; set; }
    public required string Account { get; set; }
    public DateOnly TradeDate { get; set; }
    public DateOnly SettleDate { get; set; }
    public DateTimeOffset BookedAt { get; set; }

    public bool IsOption => Multiplier > 1;
}
