# Backlog

Tickets sized for one PR each, grouped by area. File them as GitHub issues and hand them to coding
agents; each PR should reference its issue with `Closes #N`. The **Watch for** notes are what a
reviewer (human or Vader) should check. They're the mistakes that are easy to make in that change.

## Pricing and risk library (`OptionsDesk.Pricing`)

**P1. American pricing for the chain.** Price puts (and calls on dividend payers) with `BinomialTree`
instead of Black-Scholes in `MarketDataService.Quote`. Keep BS Greeks or bump the tree for them.
*Watch for:* performance (a chain is ~70 tree prices per page load), a tree with too few steps, and
American prices below European.

**P2. Discrete dividends.** Replace the continuous dividend yield with a schedule of cash dividends
(ex-date, amount) per underlying, using the escrowed-dividend model in Black-Scholes.
*Watch for:* dividends after expiry being included, ex-date on the valuation date, schema migration.

**P3. Newton-Raphson implied vol.** Speed up `ImpliedVolatility.Solve` with Newton steps on vega,
falling back to bisection.
*Watch for:* divergence for deep OTM options where vega is near zero; existing round-trip tests must still pass.

**P4. Weekly expiries.** List Friday weeklies for SPY alongside monthlies.
*Watch for:* weeklies that collide with monthlies, holiday Fridays, the Juneteenth case.

**P5. Gamma limit.** Add `MaxUnderlyingGamma` to `RiskLimits` and enforce it in `PreTradeRisk`.
*Watch for:* sign handling (short gamma counts too), config binding, a test per limit.

**P6. Early-exercise / assignment report.** Flag short ITM calls the day before an ex-dividend date
where the dividend exceeds the remaining put value.
*Watch for:* using the wrong side's time value.

**P7. Holiday calendar for 2028.** Add 2028 NYSE holidays, and fail loudly when asked for a date past
the last loaded year.
*Watch for:* Good Friday's date, observed holidays when the date falls on a weekend.

## Backend (`OptionsDesk.Web` services and controllers)

**B1. Authentication.** Add cookie auth with a simple user store; take the trader from the signed-in
user instead of `Desk:DefaultTrader` (see the TODO in `TradesApiController`).
*Watch for:* booking endpoints left anonymous, trader still accepted from the request body.

**B2. Trade amend and cancel.** `PUT /api/trades/{id}` and `DELETE /api/trades/{id}` with an audit
trail of who changed what.
*Watch for:* hard deletes (the blotter must keep history), amends that skip pre-trade risk,
positions not reflecting cancels.

**B3. Account-level limits.** Limits per account (from config), checked alongside desk-wide limits.
*Watch for:* mixing accounts in the delta and vega totals.

**B4. Position cache.** `PortfolioService` replays every trade on every request. Cache positions and
invalidate on booking.
*Watch for:* stale positions after a booking, thread safety, cache keys that ignore valuation date.

**B5. CSV export.** `GET /Trades/Export` and `GET /Positions/Export` download CSV.
*Watch for:* CSV injection (cells starting with `=`, `+`, `-`, `@`), culture-dependent number formats.

**B6. Expired positions.** On the day after expiry, settle option positions: ITM to stock (exercise or
assignment), OTM to zero.
*Watch for:* the multiplier, short vs long, realized P&L on the closing leg.

**B7. Market data admin.** An admin page to update spot and smile parameters per underlying.
*Watch for:* missing authorization, validation (negative vol, spot of 0), antiforgery on the form.

## Frontend (Razor views, TypeScript, CSS)

**F1. Live chain refresh.** Poll the chain every few seconds and update bid/ask in place without a page
reload. Add a `GET /api/chain/{symbol}` endpoint.
*Watch for:* timers that keep running after navigation, re-rendering that drops the click handlers.

**F2. Vol smile chart.** Plot IV against strike for the selected expiry on the chain page (inline SVG
or a small charting library).
*Watch for:* hard-coded axis ranges, puts and calls mixed on one line without a legend.

**F3. Spread ticket.** Let the ticket build a two-leg vertical (buy one strike, sell another) and show
net debit/credit and net Greeks.
*Watch for:* sign errors on the short leg, booking one leg when the other is rejected.

**F4. Keyboard entry.** `B`/`S` to set side, up/down arrows to move the price by one tick
(0.05 above $3, 0.01 below).
*Watch for:* tick logic duplicated from `QuoteBuilder.Tick` instead of shared, shortcuts firing while typing in inputs.

**F5. Positions page Greeks.** Add delta, gamma, vega, theta columns to `/Positions`, with totals.
*Watch for:* per-share vs per-position units in the column headers.

**F6. Dark mode.** Respect `prefers-color-scheme` across all pages.
*Watch for:* P&L colours that fail contrast in dark mode.

## Data (`OptionsDesk.Data`)

**D1. EF Core migrations.** Replace `EnsureCreated` with migrations and apply them at startup.
*Watch for:* the initial migration not matching the current schema, data loss on existing `desk.db` files.

**D2. Trade audit columns.** `CreatedBy`, `ModifiedBy`, `ModifiedAt`, and a soft-delete flag.
*Watch for:* queries that forget to filter soft-deleted rows.

**D3. SQL Server support.** Make the provider configurable (SQLite for dev, SQL Server for prod).
*Watch for:* SQLite-only SQL in check constraints, decimal precision differences.

## DevOps

**O1. Docker Compose.** Compose file with the app and a volume for the database; health check on `/`.
*Watch for:* running as root, secrets in the compose file.

**O2. Code coverage gate.** Publish coverage in CI and fail under 80% for `OptionsDesk.Pricing`.
*Watch for:* excluding files to hit the number.

**O3. Dependabot.** NuGet and GitHub Actions updates, weekly.
*Watch for:* auto-merge on major versions.

**O4. Health checks.** `/health` with a database check, wired into the Dockerfile `HEALTHCHECK`.
*Watch for:* the health endpoint leaking connection strings in its error output.

**O5. Structured logging.** Log every booking and rejection with trade details as structured fields.
*Watch for:* logging the antiforgery token or other secrets.
