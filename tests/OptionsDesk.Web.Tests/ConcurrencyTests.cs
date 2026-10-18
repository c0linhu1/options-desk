using System.Net;
using OptionsDesk.Pricing;

namespace OptionsDesk.Web.Tests;

public class ConcurrencyTests : IDisposable
{
    // Each order uses 40% of the delta limit, so at most two can ever be booked.
    private readonly DeskFactory _factory = new(fileDatabase: true, limits: new RiskLimits { MaxUnderlyingDelta = 100_000 });

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task SimultaneousOrdersCanNotJointlyBreachALimit()
    {
        var clients = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => _factory.TradingClientAsync()));

        var responses = await Task.WhenAll(clients.Select(c => c.BookAsync("KO", "Buy", 40_000, 69.80m)));

        Assert.Equal(2, responses.Count(r => r.StatusCode == HttpStatusCode.Created));
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.UnprocessableEntity, r.StatusCode));
    }
}
