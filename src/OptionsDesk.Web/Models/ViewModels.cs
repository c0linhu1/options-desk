using OptionsDesk.Data;
using OptionsDesk.Pricing;
using OptionsDesk.Web.Services;

namespace OptionsDesk.Web.Models;

public sealed record UnderlyingSummary(Underlying Underlying, double AtmVol, DateOnly FrontExpiry);

public sealed record DashboardViewModel(DateOnly AsOf, IReadOnlyList<UnderlyingSummary> Underlyings);

public sealed record ChainViewModel(
    Underlying Underlying,
    IReadOnlyList<Underlying> AllUnderlyings,
    IReadOnlyList<DateOnly> Expiries,
    DateOnly Expiry,
    double TimeToExpiry,
    IReadOnlyList<ChainRow> Rows);

/// <summary>A position with its Greeks in book terms: delta and gamma in shares, vega and theta in dollars.</summary>
public sealed record PositionGreeksRow(PositionRow Position, double Delta, double Gamma, double Vega, double Theta);

public sealed record PositionsViewModel(IReadOnlyList<PositionGreeksRow> Rows)
{
    public decimal TotalMarketValue => Rows.Sum(r => r.Position.MarketValue);
    public decimal TotalRealized => Rows.Sum(r => r.Position.RealizedPnl);
    public decimal TotalUnrealized => Rows.Sum(r => r.Position.UnrealizedPnl);
    public double TotalVega => Rows.Sum(r => r.Vega);
    public double TotalTheta => Rows.Sum(r => r.Theta);
}

public sealed record TradesViewModel(IReadOnlyList<Trade> Trades, IReadOnlyList<string> Underlyings,
    string? Underlying, string? Trader);

public sealed record RiskViewModel(
    IReadOnlyList<Exposure> Exposures,
    IReadOnlyDictionary<string, GreekTotals> ByUnderlying,
    GreekTotals Totals,
    RiskLimits Limits,
    ScenarioGrid? Scenarios);

public sealed record PricingRequest(string Symbol, double? Vol = null, decimal? Price = null);

public sealed record PricingResponse(string Symbol, decimal Spot, double TimeToExpiry, double Vol, double Price,
    double Delta, double Gamma, double Vega, double Theta, double Rho);

public record ErrorViewModel(string? RequestId)
{
    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
