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

public sealed record PositionsViewModel(IReadOnlyList<PositionRow> Positions)
{
    public decimal TotalMarketValue => Positions.Sum(p => p.MarketValue);
    public decimal TotalRealized => Positions.Sum(p => p.RealizedPnl);
    public decimal TotalUnrealized => Positions.Sum(p => p.UnrealizedPnl);
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
