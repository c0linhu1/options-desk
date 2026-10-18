using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OptionsDesk.Data;
using OptionsDesk.Web.Services;

namespace OptionsDesk.Web.Tests;

/// <summary>The app on a private in-memory database, valued as of a fixed date.</summary>
public sealed partial class DeskFactory : WebApplicationFactory<Program>
{
    public static readonly DateOnly AsOf = new(2026, 10, 14);

    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    /// <summary>The valuation date the app sees. Tests can move it forward.</summary>
    public MutableClock Clock { get; } = new(AsOf);

    public DeskFactory() => _connection.Open();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DeskDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<DeskDbContext>>();
            services.AddDbContext<DeskDbContext>(o => o.UseSqlite(_connection));
            services.RemoveAll<IValuationClock>();
            services.AddSingleton<IValuationClock>(Clock);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }

    /// <summary>A client that has loaded the chain page, so it holds the antiforgery cookie and token.</summary>
    public async Task<HttpClient> TradingClientAsync()
    {
        var client = CreateClient();
        var html = await client.GetStringAsync("/Chain/AAPL");
        var token = TokenPattern().Match(html).Groups[1].Value;
        client.DefaultRequestHeaders.Add("RequestVerificationToken", token);
        return client;
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenPattern();

    public sealed class MutableClock(DateOnly today) : IValuationClock
    {
        public DateOnly Today { get; set; } = today;
    }
}

public static class HttpExtensions
{
    public static Task<HttpResponseMessage> BookAsync(this HttpClient client, string symbol, string side, int quantity,
        decimal price, DateOnly? tradeDate = null) =>
        client.PostAsJsonAsync("/api/trades", new { symbol, side, quantity, price, account = "ACCT1", tradeDate });
}
