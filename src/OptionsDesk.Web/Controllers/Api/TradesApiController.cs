using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OptionsDesk.Web.Services;

namespace OptionsDesk.Web.Controllers.Api;

[ApiController]
[Route("api/trades")]
[AutoValidateAntiforgeryToken]
public class TradesApiController(TradeBookingService booking, IOptions<DeskOptions> options) : ControllerBase
{
    [HttpPost]
    public ActionResult<BookingResult> Book(TradeTicket ticket)
    {
        try
        {
            // TODO: take the trader from the signed-in user once the desk has authentication.
            var result = booking.Book(ticket, options.Value.DefaultTrader);
            return result.Booked ? StatusCode(201, result) : UnprocessableEntity(result);
        }
        catch (DeskException e) when (e.Kind == DeskErrorKind.NotFound)
        {
            return NotFound(BookingResult.Rejected(ticket.Symbol, [e.Message]));
        }
        catch (Exception e) when (e is DeskException or FormatException or ArgumentException)
        {
            return UnprocessableEntity(BookingResult.Rejected(ticket.Symbol, [e.Message]));
        }
    }
}
