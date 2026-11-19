using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OptionsDesk.Data;
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

    private static string Put(decimal strike) => new OptionContract("AAPL", Front, strike, OptionRight.Put).Symbol;

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

    [Fact]
    public async Task PagesStillRenderAfterAHeldOptionExpires()
    {
        var client = await _factory.TradingClientAsync();
        var itm = Call(200);
        var otm = Call(260);
        Assert.Equal(HttpStatusCode.Created, (await client.BookAsync(itm, "Buy", 5, Mid(itm))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.BookAsync(otm, "Buy", 5, Mid(otm))).StatusCode);

        _factory.Clock.Today = Front.AddDays(3);

        foreach (var url in new[] { "/Positions", "/Risk", "/Risk?underlying=AAPL" })
            (await client.GetAsync(url)).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var positions = scope.ServiceProvider.GetRequiredService<PortfolioService>().Positions();
        Assert.Equal(228.50m - 200m, positions.Single(p => p.Symbol == itm).Mark); // exercised: spot less strike
        Assert.Equal("exercised", positions.Single(p => p.Symbol == itm).Expiry);
        Assert.Equal(0m, positions.Single(p => p.Symbol == otm).Mark);
        Assert.Equal("expired worthless", positions.Single(p => p.Symbol == otm).Expiry);
        Assert.Contains("expired worthless", await client.GetStringAsync("/Positions"));
    }

    [Fact]
    public async Task SettlementDecidesExerciseAndExercisedOptionsMoveWithTheStock()
    {
        var client = await _factory.TradingClientAsync();
        var itmCall = Call(200);
        var otmCall = Call(260);
        var itmPut = Put(250);
        Assert.Equal(HttpStatusCode.Created, (await client.BookAsync(itmCall, "Buy", 5, Mid(itmCall))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.BookAsync(otmCall, "Buy", 5, Mid(otmCall))).StatusCode);
        Assert.Equal(HttpStatusCode.Created, (await client.BookAsync(itmPut, "Buy", 3, Mid(itmPut))).StatusCode);

        _factory.Clock.Today = Front.AddDays(3);
        (await client.GetAsync("/Positions")).EnsureSuccessStatusCode(); // first valuation records the settlement at 228.50

        // The stock rallies through the out-of-the-money strike after expiry.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeskDbContext>();
            db.Underlyings.Single(u => u.Symbol == "AAPL").Spot = 270m;
            db.SaveChanges();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var portfolio = scope.ServiceProvider.GetRequiredService<PortfolioService>();
            var positions = portfolio.Positions();
            Assert.Equal(270m - 200m, positions.Single(p => p.Symbol == itmCall).Mark); // long 500 shares at 200
            Assert.Equal(0m, positions.Single(p => p.Symbol == otmCall).Mark); // worthless at settlement, stays so
            Assert.Equal(250m - 270m, positions.Single(p => p.Symbol == itmPut).Mark); // short 300 shares at 250

            var delta = portfolio.Exposures().ToDictionary(e => e.Symbol, e => e.Delta);
            Assert.Equal(500, delta[itmCall]);
            Assert.Equal(0, delta[otmCall]);
            Assert.Equal(-300, delta[itmPut]);

            var grid = portfolio.Scenarios("AAPL");
            for (var v = 0; v < PortfolioService.VolShocks.Length; v++)
                for (var s = 0; s < PortfolioService.SpotShocks.Length; s++)
                    Assert.Equal(200 * 270 * PortfolioService.SpotShocks[s], grid.Pnl[v][s], 6); // net 200 shares
        }
    }

    [Fact]
    public void DatabasesCreatedBeforeSettlementsGainTheTable()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using var db = new DeskDbContext(new DbContextOptionsBuilder<DeskDbContext>().UseSqlite(connection).Options);
        db.Database.EnsureCreated();
        db.Database.ExecuteSqlRaw("""DROP TABLE "Settlements" """);

        DbSeeder.Seed(db);

        db.Settlements.Add(new Settlement { Underlying = "AAPL", Expiry = new DateOnly(2026, 10, 16), Price = 228.5m });
        db.SaveChanges();
        Assert.Equal(228.5m, db.Settlements.AsNoTracking().Single().Price);
    }

    [Fact]
    public async Task ExpiredOptionsCanNotBeBooked()
    {
        var client = await _factory.TradingClientAsync();
        _factory.Clock.Today = Front.AddDays(3); // the Monday after expiry, so the trade date is a business day
        var response = await client.BookAsync(Call(230), "Buy", 1, 1m);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("not a listed expiry", body.GetProperty("problems")[0].GetString());
    }

    [Theory]
    [InlineData("2026-10-15", "in the future")]
    [InlineData("2026-10-13", "before the earliest allowed date")]
    [InlineData("2026-10-11", "not a business day")]
    [InlineData("2026-09-07", "not a business day")] // Labor Day
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

public class BackdatingTests : IDisposable
{
    // As of Wednesday 2026-10-14, two business days back is Monday 2026-10-12.
    private readonly DeskFactory _factory = new(desk: o => o.MaxBackdateBusinessDays = 2);

    public void Dispose() => _factory.Dispose();

    [Theory]
    [InlineData("2026-10-14")]
    [InlineData("2026-10-12")]
    public async Task TradesWithinTheWindowAreBooked(string tradeDate)
    {
        var client = await _factory.TradingClientAsync();
        var response = await client.BookAsync("AAPL", "Buy", 100, 228.50m, DateOnly.Parse(tradeDate));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task TradesBeforeTheWindowAreRejected()
    {
        var client = await _factory.TradingClientAsync();
        var response = await client.BookAsync("AAPL", "Buy", 100, 228.50m, new DateOnly(2026, 10, 9));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("before the earliest allowed date 2026-10-12", body.GetProperty("problems")[0].GetString());
    }

    [Fact]
    public void ANegativeWindowIsRejectedAtStartup()
    {
        using var factory = new DeskFactory(desk: o => o.MaxBackdateBusinessDays = -1);
        Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
    }
}
