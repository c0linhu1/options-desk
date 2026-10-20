using OptionsDesk.Pricing;

namespace OptionsDesk.Pricing.Tests;

public class BlackScholesTests
{
    [Fact]
    public void MatchesTextbookValues()
    {
        // Hull: S=100, K=100, T=1, vol=20%, r=5%
        Assert.Equal(10.4506, BlackScholes.Price(OptionRight.Call, 100, 100, 1, 0.2, 0.05), 4);
        Assert.Equal(5.5735, BlackScholes.Price(OptionRight.Put, 100, 100, 1, 0.2, 0.05), 4);
    }

    [Theory]
    [InlineData(100, 90, 0.5, 0.3, 0.04, 0.02)]
    [InlineData(50, 60, 0.1, 0.6, 0.01, 0.0)]
    [InlineData(500, 500, 2.0, 0.15, 0.05, 0.015)]
    public void SatisfiesPutCallParity(double s, double k, double t, double vol, double r, double q)
    {
        var call = BlackScholes.Price(OptionRight.Call, s, k, t, vol, r, q);
        var put = BlackScholes.Price(OptionRight.Put, s, k, t, vol, r, q);
        Assert.Equal(s * Math.Exp(-q * t) - k * Math.Exp(-r * t), call - put, 10);
    }

    [Theory]
    [InlineData(OptionRight.Call)]
    [InlineData(OptionRight.Put)]
    public void GreeksMatchFiniteDifferences(OptionRight right)
    {
        double s = 100, k = 105, t = 0.75, vol = 0.25, r = 0.04, q = 0.01;
        var g = BlackScholes.Greeks(right, s, k, t, vol, r, q);
        double P(double s_, double t_, double v_, double r_) => BlackScholes.Price(right, s_, k, t_, v_, r_, q);

        Assert.Equal((P(s + 0.01, t, vol, r) - P(s - 0.01, t, vol, r)) / 0.02, g.Delta, 6);
        Assert.Equal((P(s + 0.01, t, vol, r) - 2 * P(s, t, vol, r) + P(s - 0.01, t, vol, r)) / 1e-4, g.Gamma, 4);
        Assert.Equal((P(s, t, vol + 0.0001, r) - P(s, t, vol - 0.0001, r)) / 0.0002 / 100, g.Vega, 6);
        Assert.Equal((P(s, t - 1 / 365.0, vol, r) - P(s, t, vol, r)), g.Theta, 3);
        Assert.Equal((P(s, t, vol, r + 0.0001) - P(s, t, vol, r - 0.0001)) / 0.0002 / 100, g.Rho, 6);
    }

    [Fact]
    public void ExpiredOptionIsWorthIntrinsic()
    {
        var call = BlackScholes.Greeks(OptionRight.Call, 110, 100, 0, 0.3);
        Assert.Equal(10, call.Price, 10);
        Assert.Equal(1, call.Delta);
        Assert.Equal(0, BlackScholes.Price(OptionRight.Put, 110, 100, 0, 0.3));
    }

    [Theory]
    [InlineData(0, 100, 1, 0.2, "spot")]
    [InlineData(-5, 100, 1, 0.2, "spot")]
    [InlineData(100, 0, 1, 0.2, "strike")]
    [InlineData(100, -5, 1, 0.2, "strike")]
    [InlineData(100, 100, 1, -0.1, "vol")]
    [InlineData(100, 100, -1, 0.2, "t")]
    public void RejectsBadInputsNamingTheArgument(double spot, double strike, double t, double vol, string param)
    {
        var e = Assert.Throws<ArgumentOutOfRangeException>(
            () => BlackScholes.Greeks(OptionRight.Call, spot, strike, t, vol));
        Assert.Equal(param, e.ParamName);
    }

    [Fact]
    public void AcceptsZeroVolAndZeroTime()
    {
        Assert.Equal(0, BlackScholes.Price(OptionRight.Call, 100, 100, 1, 0));
        Assert.Equal(10, BlackScholes.Price(OptionRight.Call, 110, 100, 0, 0.2), 10);
    }

    [Fact]
    public void NormalCdfIsAccurate()
    {
        Assert.Equal(0.5, Normal.Cdf(0), 15);
        Assert.Equal(0.975002104851780, Normal.Cdf(1.96), 13);
        Assert.Equal(1 - Normal.Cdf(1.3), Normal.Cdf(-1.3), 15);
    }
}

public class ImpliedVolatilityTests
{
    [Theory]
    [InlineData(OptionRight.Call, 100, 0.05)]
    [InlineData(OptionRight.Call, 120, 0.35)]
    [InlineData(OptionRight.Put, 80, 0.6)]
    [InlineData(OptionRight.Put, 100, 1.5)]
    public void RoundTripsThroughPrice(OptionRight right, double strike, double vol)
    {
        var price = BlackScholes.Price(right, 100, strike, 0.5, vol, 0.04, 0.01);
        Assert.Equal(vol, ImpliedVolatility.Solve(right, price, 100, strike, 0.5, 0.04, 0.01), 8);
    }

    [Fact]
    public void RejectsPricesBelowIntrinsic()
    {
        Assert.Throws<PricingException>(() => ImpliedVolatility.Solve(OptionRight.Call, 9.0, 110, 100, 0.5));
    }

    [Fact]
    public void RejectsExpiredOptions()
    {
        Assert.Throws<PricingException>(() => ImpliedVolatility.Solve(OptionRight.Call, 1, 100, 100, 0));
    }
}

public class BinomialTreeTests
{
    [Fact]
    public void AmericanCallWithoutDividendsEqualsEuropean()
    {
        var european = BlackScholes.Price(OptionRight.Call, 100, 100, 1, 0.2, 0.05);
        Assert.Equal(european, BinomialTree.AmericanPrice(OptionRight.Call, 100, 100, 1, 0.2, 0.05), 2);
    }

    [Fact]
    public void AmericanPutIsWorthMoreThanEuropean()
    {
        var european = BlackScholes.Price(OptionRight.Put, 100, 110, 1, 0.2, 0.08);
        var american = BinomialTree.AmericanPrice(OptionRight.Put, 100, 110, 1, 0.2, 0.08);
        Assert.True(american > european + 0.1, $"american {american} vs european {european}");
        Assert.True(american >= 10, "never worth less than exercising now");
    }
}
