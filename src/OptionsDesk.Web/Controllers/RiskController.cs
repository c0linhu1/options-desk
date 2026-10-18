using Microsoft.AspNetCore.Mvc;
using OptionsDesk.Pricing;
using OptionsDesk.Web.Models;
using OptionsDesk.Web.Services;

namespace OptionsDesk.Web.Controllers;

public class RiskController(PortfolioService portfolio, RiskLimits limits) : Controller
{
    public IActionResult Index(string? underlying = null)
    {
        var exposures = portfolio.Exposures();
        var byUnderlying = portfolio.ByUnderlying(exposures);
        var selected = underlying ?? byUnderlying.Keys.FirstOrDefault();
        var scenarios = selected is null ? null : portfolio.Scenarios(selected);
        return View(new RiskViewModel(exposures, byUnderlying, GreekTotals.Sum(exposures), limits, scenarios));
    }
}
