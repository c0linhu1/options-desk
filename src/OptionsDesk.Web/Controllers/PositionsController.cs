using Microsoft.AspNetCore.Mvc;
using OptionsDesk.Web.Models;
using OptionsDesk.Web.Services;

namespace OptionsDesk.Web.Controllers;

public class PositionsController(PortfolioService portfolio, MarketDataService market) : Controller
{
    public IActionResult Index() => View(new PositionsViewModel(Rows()));

    /// <summary>Positions with P&amp;L and Greeks as CSV.</summary>
    public IActionResult Export()
    {
        var rows = portfolio.Positions().Select(p =>
        {
            var e = portfolio.Exposures().Single(x => x.Symbol == p.Symbol);
            return Csv.Row(p.Symbol, p.Underlying, p.Quantity, p.AveragePrice, p.Mark, p.MarketValue,
                p.UnrealizedPnl, p.RealizedPnl, e.Delta, e.Gamma, e.Vega, e.Theta);
        });
        var csv = Csv.Document(
            "Symbol,Underlying,Quantity,AvgPrice,Mark,MarketValue,UnrealizedPnl,RealizedPnl,Delta,Gamma,Vega,Theta",
            rows);
        return File(csv, "text/csv", $"positions-{market.Today:yyyyMMdd}.csv");
    }

    private IReadOnlyList<PositionGreeksRow> Rows() =>
        portfolio.Positions().Select(p =>
        {
            var q = market.Resolve(p.Symbol);
            double units = p.Quantity * p.Multiplier;
            return new PositionGreeksRow(p, units * q.Delta, p.Quantity * q.Gamma, units * q.Vega, units * q.Theta);
        }).ToList();
}
