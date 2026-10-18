namespace OptionsDesk.Pricing;

/// <summary>
/// Exchange calendar: business days, T+1 settlement, and listed monthly expiries.
/// Standard monthlies expire on the third Friday; when that Friday is an exchange
/// holiday they expire the business day before.
/// </summary>
public static class MarketCalendar
{
    // NYSE full-day closures. Extend each year.
    public static readonly IReadOnlySet<DateOnly> Holidays = new HashSet<DateOnly>
    {
        new(2026, 1, 1), new(2026, 1, 19), new(2026, 2, 16), new(2026, 4, 3), new(2026, 5, 25),
        new(2026, 6, 19), new(2026, 7, 3), new(2026, 9, 7), new(2026, 11, 26), new(2026, 12, 25),
        new(2027, 1, 1), new(2027, 1, 18), new(2027, 2, 15), new(2027, 3, 26), new(2027, 5, 31),
        new(2027, 6, 18), new(2027, 7, 5), new(2027, 9, 6), new(2027, 11, 25), new(2027, 12, 24),
    };

    public static bool IsBusinessDay(DateOnly d) =>
        d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !Holidays.Contains(d);

    /// <summary>US equities and listed options both settle T+1.</summary>
    public static DateOnly SettlementDate(DateOnly tradeDate, int days = 1)
    {
        var d = tradeDate;
        while (days > 0)
        {
            d = d.AddDays(1);
            if (IsBusinessDay(d))
                days--;
        }
        return d;
    }

    public static DateOnly ThirdFriday(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var toFriday = ((int)DayOfWeek.Friday - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(toFriday + 14);
    }

    public static DateOnly MonthlyExpiry(int year, int month)
    {
        var d = ThirdFriday(year, month);
        while (!IsBusinessDay(d))
            d = d.AddDays(-1);
        return d;
    }

    /// <summary>The next <paramref name="count"/> monthly expiries strictly after asOf.</summary>
    public static IReadOnlyList<DateOnly> NextMonthlyExpiries(DateOnly asOf, int count)
    {
        var expiries = new List<DateOnly>(count);
        var month = new DateOnly(asOf.Year, asOf.Month, 1);
        while (expiries.Count < count)
        {
            var d = MonthlyExpiry(month.Year, month.Month);
            if (d > asOf)
                expiries.Add(d);
            month = month.AddMonths(1);
        }
        return expiries;
    }

    /// <summary>Calendar time to expiry in years, ACT/365.</summary>
    public static double YearFraction(DateOnly asOf, DateOnly expiry) =>
        Math.Max(expiry.DayNumber - asOf.DayNumber, 0) / 365.0;
}
