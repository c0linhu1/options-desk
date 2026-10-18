using Microsoft.AspNetCore.Mvc;
using OptionsDesk.Web.Models;
using OptionsDesk.Web.Services;

namespace OptionsDesk.Web.Controllers;

public class PositionsController(PortfolioService portfolio) : Controller
{
    public IActionResult Index() => View(new PositionsViewModel(portfolio.Positions()));
}
