using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OptionsDesk.Data;
using OptionsDesk.Web.Models;

namespace OptionsDesk.Web.Controllers;

public class TradesController(DeskDbContext db) : Controller
{
    public IActionResult Index(string? underlying = null, string? trader = null)
    {
        var query = db.Trades.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(underlying))
            query = query.Where(t => t.Underlying == underlying.ToUpper());
        if (!string.IsNullOrWhiteSpace(trader))
            query = query.Where(t => t.Trader == trader);
        var trades = query.OrderByDescending(t => t.Id).ToList();
        var underlyings = db.Underlyings.AsNoTracking().OrderBy(u => u.Symbol).Select(u => u.Symbol).ToList();
        return View(new TradesViewModel(trades, underlyings, underlying, trader));
    }
}
