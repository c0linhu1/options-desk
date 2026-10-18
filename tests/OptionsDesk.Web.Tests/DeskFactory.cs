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
using OptionsDesk.Pricing;
using OptionsDesk.Web.Services;

namespace OptionsDesk.Web.Tests;

/// <summary>
/// The app on a private database, valued as of a fixed date. The default is one shared in-memory
/// connection; pass <c>fileDatabase: true</c> for a temp file where each request gets its own
/// connection, as in production (needed to exercise concurrent requests).
/// </summary>
public sealed partial class DeskFactory : WebApplicationFactory<Program>
{
    public static readonly DateOnly AsOf = new(2026, 10, 14);

    private readonly SqliteConnection? _connection;
    private readonly string? _dbFile;
    private readonly RiskLimits? _limits;

    public DeskFactory(bool fileDatabase = false, RiskLimits? limits = null)
    {
        _limits = limits;
        if (fileDatabase)
        {
            _dbFile = Path.Combine(Path.GetTempPath(), $"desk-test-{Guid.NewGuid():N}.db");
            return;
        }
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<DeskDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<DeskDbContext>>();
            if (_connection is not null)
                services.AddDbContext<DeskDbContext>(o => o.UseSqlite(_connection));
            else
                services.AddDbContext<DeskDbContext>(o => o.UseSqlite($"Data Source={_dbFile}"));
            if (_limits is not null)
            {
                services.RemoveAll<RiskLimits>();
                services.AddSingleton(_limits);
            }
            services.RemoveAll<IValuationClock>();
            services.AddSingleton<IValuationClock>(new FixedClock(AsOf));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (!disposing)
            return;
        _connection?.Dispose();
        if (_dbFile is not null)
        {
            SqliteConnection.ClearAllPools();
            File.Delete(_dbFile);
        }
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

    private sealed class FixedClock(DateOnly today) : IValuationClock
    {
        public DateOnly Today => today;
    }
}

public static class HttpExtensions
{
    public static Task<HttpResponseMessage> BookAsync(this HttpClient client, string symbol, string side, int quantity,
        decimal price) =>
        client.PostAsJsonAsync("/api/trades", new { symbol, side, quantity, price, account = "ACCT1" });
}
