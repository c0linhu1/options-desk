using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OptionsDesk.Data;
using OptionsDesk.Web.Models;
using OptionsDesk.Web.Services;

namespace OptionsDesk.Web.Controllers;

public class TradesController(DeskDbContext db) : Controller
{
    public IActionResult Index(string? underlying = null, string? trader = null)
    {
        var trades = Filter(underlying, trader, null, null).OrderByDescending(t => t.Id).ToList();
        var underlyings = db.Underlyings.AsNoTracking().OrderBy(u => u.Symbol).Select(u => u.Symbol).ToList();
        return View(new TradesViewModel(trades, underlyings, underlying, trader));
    }

    /// <summary>
    /// The blotter as CSV, with the same filters as the page plus an optional trade-date range.
    /// <paramref name="from"/> and <paramref name="to"/> are both inclusive.
    /// </summary>
    public IActionResult Export(string? underlying = null, string? trader = null, DateOnly? from = null,
        DateOnly? to = null)
    {
        var trades = Filter(underlying, trader, from, to).OrderBy(t => t.Id).ToList();
        var rows = trades.Select(t => Csv.Row(
            t.Id, t.TradeDate, t.SettleDate, t.Symbol, t.Underlying, t.Side.ToString(), t.Quantity, t.Price,
            t.Quantity * t.Price * t.Multiplier, t.Trader, t.Account,
            t.BookedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")));
        var csv = Csv.Document(
            "Id,TradeDate,SettleDate,Symbol,Underlying,Side,Quantity,Price,Notional,Trader,Account,BookedAt", rows);
        return File(csv, "text/csv", $"trades-{DateTime.Now:yyyyMMdd}.csv");
    }

    private IQueryable<Trade> Filter(string? underlying, string? trader, DateOnly? from, DateOnly? to)
    {
        // Trader names are typed by hand on the filter form, so match them case-insensitively.
        // EF can't translate COLLATE NOCASE from LINQ on SQLite; the value is our own trader code, trimmed.
#pragma warning disable EF1003
        var query = string.IsNullOrWhiteSpace(trader)
            ? db.Trades.AsNoTracking()
            : db.Trades.FromSqlRaw("SELECT * FROM Trades WHERE Trader = '" + trader.Trim() + "' COLLATE NOCASE")
                .AsNoTracking();
#pragma warning restore EF1003
        if (!string.IsNullOrWhiteSpace(underlying))
            query = query.Where(t => t.Underlying == underlying.ToUpper());
        if (from is not null)
            query = query.Where(t => t.TradeDate >= from);
        if (to is not null)
            query = query.Where(t => t.TradeDate < to);
        return query;
    }
}
