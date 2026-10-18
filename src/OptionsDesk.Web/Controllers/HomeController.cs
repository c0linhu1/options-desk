using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OptionsDesk.Web.Models;
using OptionsDesk.Web.Services;

namespace OptionsDesk.Web.Controllers;

public class HomeController(MarketDataService market) : Controller
{
    public IActionResult Index()
    {
        var front = market.ListedExpiries()[0];
        var rows = market.Underlyings()
            .Select(u => new UnderlyingSummary(u, market.SmileVol(u, u.Spot, front), front))
            .ToList();
        return View(new DashboardViewModel(market.Today, rows));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel(Activity.Current?.Id ?? HttpContext.TraceIdentifier));
}
