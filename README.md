# Options Desk

An internal-style trading desk application for US equities and listed equity options, built with
ASP.NET Core MVC on .NET 10. Traders browse option chains, book stock and option trades through a
pre-trade risk check, and watch positions, P&L, Greeks, and scenario risk.

Market data is synthetic: each underlying has a spot price and a parametric volatility smile, and
the option chain is priced from it with Black-Scholes. Nothing connects to an exchange or broker.

## Layout

```
src/
  OptionsDesk.Pricing/   Pure C# domain library, no dependencies
    OptionContract.cs      OCC option symbology (parse/format)
    MarketCalendar.cs      Business days, T+1 settlement, monthly expiries (holiday-adjusted)
    BlackScholes.cs        Black-Scholes-Merton price and Greeks, normal CDF
    ImpliedVolatility.cs   Implied vol solver with no-arbitrage bounds
    BinomialTree.cs        CRR tree for American exercise
    VolSmile.cs            Parametric smile and listed quote construction (tick sizes)
    Order.cs, Position.cs  Orders, average-cost P&L
    PreTradeRisk.cs        Limits: size, notional, fat-finger collar, delta, vega
  OptionsDesk.Data/      EF Core (SQLite): underlyings and trades, demo seed data
  OptionsDesk.Web/       ASP.NET Core MVC app
    Controllers/           MVC pages (Home, Chain, Positions, Trades, Risk) and JSON API (Controllers/Api)
    Services/              MarketDataService, PortfolioService, TradeBookingService
    Views/                 Razor .cshtml views
    Scripts/               TypeScript, compiled to wwwroot/js by the build (Microsoft.TypeScript.MSBuild)
tests/
  OptionsDesk.Pricing.Tests/   xUnit: pricing, calendar, symbology, positions, risk
  OptionsDesk.Web.Tests/       xUnit + WebApplicationFactory: pages, API, booking, antiforgery
```

## Running

Requires the .NET 10 SDK. TypeScript compiles during the build; Node is not needed.

```
dotnet run --project src/OptionsDesk.Web
```

The app creates `desk.db` (SQLite) next to the project on first run and seeds eight underlyings.

```
dotnet test                              # all tests
dotnet format --verify-no-changes        # what CI checks
docker build -t options-desk . && docker run -p 8080:8080 options-desk
```

## Pages and endpoints

| Route | What it shows |
|---|---|
| `/` | Underlyings with spot, front-month ATM vol, dividend yield |
| `/Chain/{symbol}?expiry=yyyy-MM-dd` | Option chain (calls / strike / puts) and the trade ticket |
| `/Positions` | Positions with mark, market value, realized and unrealized P&L |
| `/Trades?underlying=&trader=` | Trade blotter |
| `/Risk?underlying=` | Greeks by underlying, limit usage, spot x vol scenario grid |
| `POST /api/pricing` | `{symbol, vol?}` prices an option; `{symbol, price}` solves implied vol |
| `POST /api/trades` | Books a trade after pre-trade risk. Requires the antiforgery token header |

## Conventions

- **Units.** Greeks from `BlackScholes` are per share: vega per 1 vol point, theta per calendar day,
  rho per 1% in rates. Book-level delta is in shares (contracts x 100 x delta); vega and theta are dollars.
- **Money is `decimal`, math is `double`.** Prices, strikes, and P&L are `decimal`; pricing models use `double`.
  Convert at the boundary.
- **Valuation date** comes from `IValuationClock` (set `Desk:ValuationDate` to pin it). Never call
  `DateTime.Now` in pricing or booking code.
- **Warnings are errors** (`Directory.Build.props`), and CI runs `dotnet format --verify-no-changes`.
- Every PR links a ticket (`Closes #N`) and fills in the PR template.

## Backlog

See [docs/BACKLOG.md](docs/BACKLOG.md) for ready-to-file tickets across pricing, backend, frontend,
data, and DevOps.
