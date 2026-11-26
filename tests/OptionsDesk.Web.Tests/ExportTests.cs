using System.Net;
using Microsoft.Extensions.DependencyInjection;
using OptionsDesk.Pricing;
using OptionsDesk.Web.Services;

namespace OptionsDesk.Web.Tests;

public class CsvTests
{
    [Theory]
    [InlineData("AAPL", "AAPL")]
    [InlineData("Smith, J", "\"Smith, J\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData("", "")]
    public void QuotesFieldsThatNeedIt(string value, string expected) => Assert.Equal(expected, Csv.Field(value));

    [Fact]
    public void WritesMixedRow()
    {
        Assert.Equal("AAPL,10,5.25,2026-10-14", Csv.Row("AAPL", 10, 5.25m, new DateOnly(2026, 10, 14)));
    }

    [Fact]
    public void NullsAreEmpty() => Assert.Equal("a,,b", Csv.Row("a", null, "b"));
}

public class ExportTests : IDisposable
{
    private static readonly DateOnly Front = MarketCalendar.NextMonthlyExpiries(DeskFactory.AsOf, 1)[0];
    private readonly DeskFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private decimal Mid(string symbol)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<MarketDataService>().Resolve(symbol).Mid;
    }

    [Fact]
    public async Task TradesExportHasHeaderAndOneLinePerTrade()
    {
        var client = await _factory.TradingClientAsync();
        await client.BookAsync("KO", "Buy", 100, 69.80m);
        await client.BookAsync("AAPL", "Sell", 50, 228.50m);

        var response = await client.GetAsync("/Trades/Export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/csv", response.Content.Headers.ContentType?.MediaType);

        var lines = (await response.Content.ReadAsStringAsync()).TrimEnd().Split("\r\n");
        Assert.StartsWith("Id,TradeDate,SettleDate,Symbol", lines[0]);
        Assert.Equal(3, lines.Length);
        Assert.Contains(lines, l => l.Contains(",KO,"));
    }

    [Fact]
    public async Task TradesExportFiltersByTraderAndDate()
    {
        var client = await _factory.TradingClientAsync();
        await client.BookAsync("KO", "Buy", 100, 69.80m);

        var mine = await client.GetStringAsync($"/Trades/Export?trader=TRADER1&from={DeskFactory.AsOf:yyyy-MM-dd}");
        var none = await client.GetStringAsync("/Trades/Export?trader=someone-else");
        Assert.Equal(2, mine.TrimEnd().Split("\r\n").Length);
        Assert.Single(none.TrimEnd().Split("\r\n"));
    }

    [Fact]
    public async Task PositionsPageShowsGreeks()
    {
        var client = await _factory.TradingClientAsync();
        var call = new OptionContract("AAPL", Front, 230, OptionRight.Call).Symbol;
        await client.BookAsync(call, "Buy", 10, Mid(call));

        var html = await client.GetStringAsync("/Positions");
        Assert.Contains("Gamma", html);

        using var scope = _factory.Services.CreateScope();
        var market = scope.ServiceProvider.GetRequiredService<MarketDataService>();
        var q = market.Resolve(call);
        Assert.InRange(q.Delta, 0.3, 0.7);
        Assert.Contains($"data-value=\"{10 * 100 * q.Delta}\"", html);
        Assert.Contains($"data-value=\"{10 * q.Gamma}\"", html);
    }

    [Fact]
    public async Task PositionsExport()
    {
        var client = await _factory.TradingClientAsync();
        var call = new OptionContract("AAPL", Front, 230, OptionRight.Call).Symbol;
        await client.BookAsync(call, "Buy", 10, Mid(call));
        await client.BookAsync("AAPL", "Sell", 500, 228.50m);

        var response = await client.GetAsync("/Positions/Export");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var lines = (await response.Content.ReadAsStringAsync()).TrimEnd().Split("\r\n");
        Assert.Equal(3, lines.Length);
        Assert.EndsWith("Delta,Gamma,Vega,Theta", lines[0]);
    }
}
