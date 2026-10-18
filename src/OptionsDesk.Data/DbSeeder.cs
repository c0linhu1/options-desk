namespace OptionsDesk.Data;

/// <summary>Demo underlyings. Spots and vol parameters are illustrative, not live market data.</summary>
public static class DbSeeder
{
    public static IReadOnlyList<Underlying> DemoUnderlyings() =>
    [
        New("AAPL", "Apple Inc.", "Technology", 228.50m, 0.0045, 0.24, -0.10, 0.06),
        New("MSFT", "Microsoft Corp.", "Technology", 432.10m, 0.0072, 0.22, -0.09, 0.05),
        New("NVDA", "NVIDIA Corp.", "Technology", 138.40m, 0.0003, 0.48, -0.08, 0.08),
        New("TSLA", "Tesla Inc.", "Consumer", 251.20m, 0.0, 0.55, -0.04, 0.10),
        New("JPM", "JPMorgan Chase & Co.", "Financials", 221.75m, 0.0210, 0.21, -0.11, 0.05),
        New("XOM", "Exxon Mobil Corp.", "Energy", 118.30m, 0.0330, 0.23, -0.07, 0.04),
        New("KO", "Coca-Cola Co.", "Staples", 69.80m, 0.0280, 0.15, -0.08, 0.04),
        New("SPY", "SPDR S&P 500 ETF", "Index", 584.60m, 0.0125, 0.15, -0.16, 0.05),
    ];

    /// <summary>Creates the schema and loads the demo underlyings if the database is empty.</summary>
    public static void Seed(DeskDbContext db)
    {
        db.Database.EnsureCreated();
        if (db.Underlyings.Any())
            return;
        db.Underlyings.AddRange(DemoUnderlyings());
        db.SaveChanges();
    }

    private static Underlying New(string symbol, string name, string sector, decimal spot, double div,
        double baseVol, double skew, double curvature) => new()
        {
            Symbol = symbol,
            Name = name,
            Sector = sector,
            Spot = spot,
            DividendYield = div,
            BaseVol = baseVol,
            Skew = skew,
            Curvature = curvature,
        };
}
