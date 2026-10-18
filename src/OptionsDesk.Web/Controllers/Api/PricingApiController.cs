using Microsoft.AspNetCore.Mvc;
using OptionsDesk.Pricing;
using OptionsDesk.Web.Models;
using OptionsDesk.Web.Services;

namespace OptionsDesk.Web.Controllers.Api;

[ApiController]
[Route("api/pricing")]
public class PricingApiController(MarketDataService market) : ControllerBase
{
    /// <summary>Price an option at a vol (default: the smile vol), or solve for implied vol from a price.</summary>
    [HttpPost]
    public ActionResult<PricingResponse> Price(PricingRequest req)
    {
        if (req.Vol is not null && req.Price is not null)
            return Problem("give vol or price, not both", statusCode: 422);
        try
        {
            var inst = market.Resolve(req.Symbol);
            if (inst.Contract is not { } c)
                return Problem("pricing needs an option symbol", statusCode: 422);

            var u = inst.Underlying;
            var t = market.TimeToExpiry(c.Expiry);
            var vol = req.Price is { } price
                ? ImpliedVolatility.Solve(c.Right, (double)price, (double)u.Spot, (double)c.Strike, t, market.Rate,
                    u.DividendYield)
                : req.Vol ?? market.SmileVol(u, c.Strike, c.Expiry);
            var g = BlackScholes.Greeks(c.Right, (double)u.Spot, (double)c.Strike, t, vol, market.Rate,
                u.DividendYield);
            return new PricingResponse(c.Symbol, u.Spot, t, vol, g.Price, g.Delta, g.Gamma, g.Vega, g.Theta, g.Rho);
        }
        catch (DeskException e) when (e.Kind == DeskErrorKind.NotFound)
        {
            return Problem(e.Message, statusCode: 404);
        }
        catch (Exception e) when (e is DeskException or PricingException or FormatException or ArgumentException)
        {
            return Problem(e.Message, statusCode: 422);
        }
    }
}
