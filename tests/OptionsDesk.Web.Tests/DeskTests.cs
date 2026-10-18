using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using OptionsDesk.Pricing;
using OptionsDesk.Web.Services;

namespace OptionsDesk.Web.Tests;

public class PageTests : IDisposable
{
    private readonly DeskFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("/", "AAPL")]
    [InlineData("/Chain/AAPL", "Trade ticket")]
    [InlineData("/Positions", "Positions")]
    [InlineData("/Trades", "Trades")]
    [InlineData("/Risk", "Risk")]
    public async Task PagesRender(string url, string expected)
    {
        var response = await _factory.CreateClient().GetAsync(url);
        response.EnsureSuccessStatusCode();
        Assert.Contains(expected, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task UnknownUnderlyingIs404()
    {
        var response = await _factory.CreateClient().GetAsync("/Chain/NOPE");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task UnlistedExpiryIs400()
    {
        var response = await _factory.CreateClient().GetAsync("/Chain/AAPL?expiry=2026-10-15");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CompiledTypeScriptIsServed()
    {
        var response = await _factory.CreateClient().GetAsync("/js/ticket.js");
        response.EnsureSuccessStatusCode();
        Assert.Contains("bookTrade", await response.Content.ReadAsStringAsync());
    }
}

public class ApiTests : IDisposable
{
    private static readonly DateOnly Front = MarketCalendar.NextMonthlyExpiries(DeskFactory.AsOf, 1)[0];
    private readonly DeskFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static string Call(decimal strike, string underlying = "AAPL") =>
        new OptionContract(underlying, Front, strike, OptionRight.Call).Symbol;

    private decimal Mid(string symbol)
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<MarketDataService>().Resolve(symbol).Mid;
    }

    [Fact]
    public async Task PricingSolvesForImpliedVolAndBack()
    {
        var client = _factory.CreateClient();
        var atVol = await client.PostAsJsonAsync("/api/pricing", new { symbol = Call(230), vol = 0.3 });
        var priced = await atVol.Content.ReadFromJsonAsync<JsonElement>();
        var price = priced.GetProperty("price").GetDecimal();

        var fromPrice = await client.PostAsJsonAsync("/api/pricing", new { symbol = Call(230), price });
        var solved = await fromPrice.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0.3, solved.GetProperty("vol").GetDouble(), 6);
    }

    [Fact]
    public async Task PricingRejectsStockSymbols()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/pricing", new { symbol = "AAPL" });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task BookingRequiresAntiforgeryToken()
    {
        var response = await _factory.CreateClient().BookAsync("AAPL", "Buy", 100, 228.50m);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task BookOptionAndStockThenSeePositionsAndRisk()
    {
        var client = await _factory.TradingClientAsync();
        var call = Call(230);

        var booked = await client.BookAsync(call, "Buy", 10, Mid(call));
        Assert.Equal(HttpStatusCode.Created, booked.StatusCode);
        var result = await booked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("2026-10-15", result.GetProperty("settleDate").GetString());

        Assert.Equal(HttpStatusCode.Created, (await client.BookAsync("AAPL", "Sell", 500, 228.50m)).StatusCode);

        using var scope = _factory.Services.CreateScope();
        var portfolio = scope.ServiceProvider.GetRequiredService<PortfolioService>();
        var positions = portfolio.Positions();
        Assert.Equal(2, positions.Count);
        Assert.Contains(positions, p => p.Symbol == call && p.Quantity == 10 && p.Multiplier == 100);

        var aapl = portfolio.ByUnderlying(portfolio.Exposures())["AAPL"];
        Assert.InRange(aapl.Delta, -500 + 10 * 100 * 0.3, -500 + 10 * 100 * 0.7); // long calls, short stock
        Assert.True(aapl.Vega > 0);

        var risk = await client.GetStringAsync("/Risk?underlying=AAPL");
        Assert.Contains("AAPL scenario P&amp;L", risk);
    }

    [Fact]
    public async Task FatFingerIsRejected()
    {
        var client = await _factory.TradingClientAsync();
        var call = Call(230);
        var response = await client.BookAsync(call, "Buy", 10, Mid(call) * 2);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("from the mid", body.GetProperty("problems")[0].GetString());
    }

    [Fact]
    public async Task UnlistedExpiryIsRejected()
    {
        var client = await _factory.TradingClientAsync();
        var bogus = new OptionContract("AAPL", new DateOnly(2026, 10, 30), 230, OptionRight.Call).Symbol;
        var response = await client.BookAsync(bogus, "Buy", 1, 5m);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task ScenarioGridIsZeroAtTheOrigin()
    {
        var client = await _factory.TradingClientAsync();
        var call = Call(230);
        await client.BookAsync(call, "Buy", 10, Mid(call));

        using var scope = _factory.Services.CreateScope();
        var grid = scope.ServiceProvider.GetRequiredService<PortfolioService>().Scenarios("AAPL");
        var origin = grid.Pnl[Array.IndexOf(PortfolioService.VolShocks, 0.0)][Array.IndexOf(PortfolioService.SpotShocks, 0.0)];
        Assert.Equal(0, origin, 6);
        Assert.True(grid.Pnl[1][^1] > 0, "long calls make money when spot rallies");
    }

    [Theory]
    [InlineData("2026-10-15", "in the future")]
    [InlineData("2026-10-13", "before the earliest allowed date")]
    [InlineData("2026-10-11", "not a business day")]
    public async Task TradeDateMustBeToday(string tradeDate, string expected)
    {
        var client = await _factory.TradingClientAsync();
        var response = await client.BookAsync("AAPL", "Buy", 100, 228.50m, DateOnly.Parse(tradeDate));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(expected, body.GetProperty("problems")[0].GetString());
    }

    [Fact]
    public async Task TodaysDateIsAccepted()
    {
        var client = await _factory.TradingClientAsync();
        var response = await client.BookAsync("AAPL", "Buy", 100, 228.50m, DeskFactory.AsOf);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }
}
