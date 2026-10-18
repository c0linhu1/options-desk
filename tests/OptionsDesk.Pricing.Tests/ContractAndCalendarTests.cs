using OptionsDesk.Pricing;

namespace OptionsDesk.Pricing.Tests;

public class OptionContractTests
{
    [Fact]
    public void FormatsOccSymbol()
    {
        var c = new OptionContract("AAPL", new DateOnly(2026, 12, 18), 200m, OptionRight.Call);
        Assert.Equal("AAPL  261218C00200000", c.Symbol);
    }

    [Theory]
    [InlineData("AAPL  261218C00200000", "AAPL", 200.0, OptionRight.Call)]
    [InlineData("SPY   270115P00587500", "SPY", 587.5, OptionRight.Put)]
    [InlineData("brk.b 261120c00450000", "BRK.B", 450.0, OptionRight.Call)]
    public void ParsesOccSymbol(string symbol, string underlying, double strike, OptionRight right)
    {
        var c = OptionContract.Parse(symbol);
        Assert.Equal(underlying, c.Underlying);
        Assert.Equal((decimal)strike, c.Strike);
        Assert.Equal(right, c.Right);
        Assert.Equal(c, OptionContract.Parse(c.Symbol));
    }

    [Theory]
    [InlineData("AAPL")]
    [InlineData("AAPL  261218X00200000")]
    [InlineData("TOOLONGX261218C00200000")]
    public void RejectsBadSymbols(string symbol)
    {
        Assert.False(OptionContract.IsOptionSymbol(symbol));
        Assert.Throws<FormatException>(() => OptionContract.Parse(symbol));
    }

    [Fact]
    public void Intrinsic()
    {
        var put = new OptionContract("KO", new DateOnly(2026, 11, 20), 70m, OptionRight.Put);
        Assert.Equal(5m, put.Intrinsic(65m));
        Assert.Equal(0m, put.Intrinsic(75m));
    }
}

public class MarketCalendarTests
{
    [Theory]
    [InlineData(2026, 10, 2026, 10, 16)]
    [InlineData(2026, 11, 2026, 11, 20)]
    [InlineData(2027, 1, 2027, 1, 15)]
    public void MonthlyExpiryIsThirdFriday(int year, int month, int ey, int em, int ed)
    {
        Assert.Equal(new DateOnly(ey, em, ed), MarketCalendar.MonthlyExpiry(year, month));
    }

    [Fact]
    public void HolidayExpiryMovesToThursday()
    {
        // Juneteenth 2026 falls on the third Friday of June.
        Assert.Equal(new DateOnly(2026, 6, 19), MarketCalendar.ThirdFriday(2026, 6));
        Assert.Equal(new DateOnly(2026, 6, 18), MarketCalendar.MonthlyExpiry(2026, 6));
    }

    [Fact]
    public void NextExpiriesSkipTodaysExpiry()
    {
        var expiries = MarketCalendar.NextMonthlyExpiries(new DateOnly(2026, 10, 16), 3);
        Assert.Equal([new DateOnly(2026, 11, 20), new DateOnly(2026, 12, 18), new DateOnly(2027, 1, 15)], expiries);
    }

    [Theory]
    [InlineData(2026, 10, 14, 2026, 10, 15)] // Wed -> Thu
    [InlineData(2026, 10, 16, 2026, 10, 19)] // Fri -> Mon
    [InlineData(2026, 11, 25, 2026, 11, 27)] // skips Thanksgiving
    public void SettlesTPlusOne(int y, int m, int d, int sy, int sm, int sd)
    {
        Assert.Equal(new DateOnly(sy, sm, sd), MarketCalendar.SettlementDate(new DateOnly(y, m, d)));
    }
}
