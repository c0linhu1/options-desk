using OptionsDesk.Pricing;

namespace OptionsDesk.Pricing.Tests;

public class PositionTests
{
    [Fact]
    public void AverageCostAndRealizedPnlUseMultiplier()
    {
        var p = new Position("AAPL  261218C00200000", 100);
        p.Apply(Side.Buy, 10, 5.00m);
        p.Apply(Side.Buy, 10, 6.00m);
        Assert.Equal(5.50m, p.AveragePrice);

        p.Apply(Side.Sell, 5, 7.00m);
        Assert.Equal(5 * 1.50m * 100, p.RealizedPnl);
        Assert.Equal(15, p.Quantity);
        Assert.Equal(15 * 0.50m * 100, p.UnrealizedPnl(6.00m));
    }

    [Fact]
    public void ShortCoverAndFlip()
    {
        var p = new Position("AAPL");
        p.Apply(Side.Sell, 100, 230m);
        p.Apply(Side.Buy, 150, 225m); // covers 100 at a $5 profit, then 50 long at 225
        Assert.Equal(500m, p.RealizedPnl);
        Assert.Equal(50, p.Quantity);
        Assert.Equal(225m, p.AveragePrice);
    }

    [Fact]
    public void FlatPositionResetsAverage()
    {
        var p = new Position("KO");
        p.Apply(Side.Buy, 100, 70m);
        p.Apply(Side.Sell, 100, 71m);
        Assert.Equal(0, p.Quantity);
        Assert.Equal(0m, p.AveragePrice);
        Assert.Equal(100m, p.RealizedPnl);
    }
}

public class PreTradeRiskTests
{
    private static readonly RiskLimits Limits = new()
    {
        MaxOrderContracts = 100,
        MaxOrderShares = 10_000,
        MaxOrderNotional = 1_000_000,
        MaxUnderlyingDelta = 20_000,
        MaxPortfolioVega = 10_000,
        PriceCollarPercent = 10,
    };

    private static Order OptionOrder(int quantity = 50, decimal price = 5.00m, Side side = Side.Buy) =>
        new("AAPL  261218C00230000", side, quantity, price, "trader1", "ACCT1", new DateOnly(2026, 10, 14), 100);

    private static RiskContext Ctx(double underlyingDelta = 0, double portfolioVega = 0) =>
        new(Mid: 5.00m, DeltaPerUnit: 0.5, VegaPerUnit: 0.4, underlyingDelta, portfolioVega, IsOption: true);

    [Fact]
    public void CleanOrderPasses() => Assert.Empty(PreTradeRisk.Check(OptionOrder(), Ctx(), Limits));

    [Fact]
    public void QuantityLimit() => AssertSingle(PreTradeRisk.Check(OptionOrder(quantity: 150), Ctx(), Limits), "contracts");

    [Fact]
    public void FatFinger() => AssertSingle(PreTradeRisk.Check(OptionOrder(price: 6.00m), Ctx(), Limits), "from the mid");

    [Fact]
    public void DeltaLimit() => AssertSingle(PreTradeRisk.Check(OptionOrder(), Ctx(underlyingDelta: 18_000), Limits), "delta");

    [Fact]
    public void VegaLimit() => AssertSingle(PreTradeRisk.Check(OptionOrder(), Ctx(portfolioVega: 9_000), Limits), "vega");

    [Fact]
    public void SellingAgainstTheBookReducesRisk()
    {
        Assert.Empty(PreTradeRisk.Check(OptionOrder(side: Side.Sell), Ctx(underlyingDelta: 19_000, portfolioVega: 9_900), Limits));
    }

    [Fact]
    public void CheapOptionsGetAMinimumCollar()
    {
        var penny = new Order("AAPL  261218C00300000", Side.Buy, 10, 0.12m, "t", "a", new DateOnly(2026, 10, 14), 100);
        var ctx = Ctx() with { Mid = 0.05m };
        Assert.Empty(PreTradeRisk.Check(penny, ctx, Limits));
    }

    private static void AssertSingle(IReadOnlyList<string> problems, string expected)
    {
        var problem = Assert.Single(problems);
        Assert.Contains(expected, problem);
    }
}
