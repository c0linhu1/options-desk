namespace OptionsDesk.Pricing;

/// <summary>Delta is in share equivalents (contracts x multiplier x delta). Vega is dollars per vol point.</summary>
public sealed record RiskLimits
{
    public int MaxOrderContracts { get; init; } = 500;
    public int MaxOrderShares { get; init; } = 50_000;
    public decimal MaxOrderNotional { get; init; } = 5_000_000;
    public double MaxUnderlyingDelta { get; init; } = 100_000;
    public double MaxPortfolioVega { get; init; } = 50_000;
    public decimal PriceCollarPercent { get; init; } = 10;
    public decimal MinCollar { get; init; } = 0.10m; // cheap options get at least this much room
}

/// <summary>What the risk check needs to know about the order's instrument and the book.</summary>
/// <param name="DeltaPerUnit">Per share: 1 for stock, the option's delta for an option.</param>
/// <param name="VegaPerUnit">Per share: 0 for stock.</param>
public sealed record RiskContext(
    decimal Mid,
    double DeltaPerUnit,
    double VegaPerUnit,
    double UnderlyingDelta,
    double PortfolioVega,
    bool IsOption);

/// <summary>Pre-trade checks. An order that breaks any limit is rejected before booking.</summary>
public static class PreTradeRisk
{
    public static IReadOnlyList<string> Check(Order order, RiskContext ctx, RiskLimits limits)
    {
        var problems = new List<string>();

        var maxQuantity = ctx.IsOption ? limits.MaxOrderContracts : limits.MaxOrderShares;
        var unit = ctx.IsOption ? "contracts" : "shares";
        if (order.Quantity > maxQuantity)
            problems.Add($"order of {order.Quantity:N0} {unit} exceeds the {maxQuantity:N0} limit");

        if (order.Notional > limits.MaxOrderNotional)
            problems.Add($"order notional ${order.Notional:N0} exceeds the ${limits.MaxOrderNotional:N0} limit");

        var collar = Math.Max(ctx.Mid * limits.PriceCollarPercent / 100, limits.MinCollar);
        if (Math.Abs(order.Price - ctx.Mid) > collar)
            problems.Add($"price {order.Price} is more than {collar:F2} from the mid {ctx.Mid:F2}");

        var units = order.Side.Sign() * order.Quantity * order.Multiplier;
        var newDelta = ctx.UnderlyingDelta + units * ctx.DeltaPerUnit;
        if (Math.Abs(newDelta) > limits.MaxUnderlyingDelta)
            problems.Add($"delta would be {newDelta:N0} shares, over the {limits.MaxUnderlyingDelta:N0} limit");

        var newVega = ctx.PortfolioVega + units * ctx.VegaPerUnit;
        if (Math.Abs(newVega) > limits.MaxPortfolioVega)
            problems.Add($"portfolio vega would be ${newVega:N0}, over the ${limits.MaxPortfolioVega:N0} limit");

        return problems;
    }
}
