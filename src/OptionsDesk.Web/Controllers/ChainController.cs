using Microsoft.AspNetCore.Mvc;
using OptionsDesk.Web.Models;
using OptionsDesk.Web.Services;

namespace OptionsDesk.Web.Controllers;

public class ChainController(MarketDataService market) : Controller
{
    [HttpGet("/Chain/{symbol?}")]
    public IActionResult Index(string symbol = "SPY", DateOnly? expiry = null)
    {
        try
        {
            var u = market.GetUnderlying(symbol);
            var expiries = market.ListedExpiries();
            var selected = expiry ?? expiries[0];
            var rows = market.Chain(u, selected);
            return View(new ChainViewModel(u, market.Underlyings(), expiries, selected, market.TimeToExpiry(selected),
                rows));
        }
        catch (DeskException e) when (e.Kind == DeskErrorKind.NotFound)
        {
            return NotFound(e.Message);
        }
        catch (DeskException e)
        {
            return BadRequest(e.Message);
        }
    }
}
