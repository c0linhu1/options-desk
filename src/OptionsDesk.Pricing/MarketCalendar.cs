using System.Collections.Concurrent;
using System.Collections.Frozen;

namespace OptionsDesk.Pricing;

/// <summary>
/// Exchange calendar: business days, T+1 settlement, and listed monthly expiries.
/// Standard monthlies expire on the third Friday; when that Friday is an exchange
/// holiday they expire the business day before.
/// </summary>
public static class MarketCalendar
{
    /// <summary>First year the holiday rules describe: Juneteenth became an NYSE holiday in 2022.</summary>
    public const int FirstSupportedYear = 2022;

    // Unscheduled full-day closures, such as national days of mourning. Add them as the exchange announces them.
    private static readonly FrozenSet<DateOnly> SpecialClosures = new[]
    {
        new DateOnly(2025, 1, 9), // national day of mourning for President Carter
    }.ToFrozenSet();

    private static readonly ConcurrentDictionary<int, FrozenSet<DateOnly>> HolidaysByYear = new();

    /// <summary>
    /// NYSE full-day closures in <paramref name="year"/>, from the exchange's holiday rules plus any special closures.
    /// </summary>
    public static IReadOnlySet<DateOnly> HolidaysIn(int year) => HolidaysByYear.GetOrAdd(year, BuildHolidays);

    public static bool IsHoliday(DateOnly d) => HolidaysIn(d.Year).Contains(d);

    public static bool IsBusinessDay(DateOnly d) =>
        d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && !IsHoliday(d);

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

    /// <summary>Easter Sunday in the Gregorian calendar (anonymous Gregorian algorithm, Meeus/Jones/Butcher).</summary>
    public static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = (h + l - 7 * m + 114) % 31 + 1;
        return new DateOnly(year, month, day);
    }

    private static FrozenSet<DateOnly> BuildHolidays(int year)
    {
        if (year < FirstSupportedYear)
            throw new ArgumentOutOfRangeException(nameof(year), $"the exchange calendar covers {FirstSupportedYear} onward");

        var days = new HashSet<DateOnly>
        {
            NthWeekday(year, 1, DayOfWeek.Monday, 3), // Martin Luther King Jr. Day
            NthWeekday(year, 2, DayOfWeek.Monday, 3), // Washington's Birthday
            EasterSunday(year).AddDays(-2), // Good Friday
            LastWeekday(year, 5, DayOfWeek.Monday), // Memorial Day
            Observed(new DateOnly(year, 6, 19)), // Juneteenth
            Observed(new DateOnly(year, 7, 4)), // Independence Day
            NthWeekday(year, 9, DayOfWeek.Monday, 1), // Labor Day
            NthWeekday(year, 11, DayOfWeek.Thursday, 4), // Thanksgiving
            Observed(new DateOnly(year, 12, 25)), // Christmas
        };

        // New Year's Day moves to Monday when it falls on a Sunday, but when it falls on a Saturday the exchange
        // stays open the Friday before (the last trading day of the year) and there is no closure.
        var newYear = new DateOnly(year, 1, 1);
        if (newYear.DayOfWeek == DayOfWeek.Sunday)
            days.Add(newYear.AddDays(1));
        else if (newYear.DayOfWeek != DayOfWeek.Saturday)
            days.Add(newYear);

        days.UnionWith(SpecialClosures.Where(d => d.Year == year));
        return days.ToFrozenSet();
    }

    /// <summary>Saturday holidays are observed the Friday before, Sunday holidays the Monday after.</summary>
    private static DateOnly Observed(DateOnly d) => d.DayOfWeek switch
    {
        DayOfWeek.Saturday => d.AddDays(-1),
        DayOfWeek.Sunday => d.AddDays(1),
        _ => d,
    };

    private static DateOnly NthWeekday(int year, int month, DayOfWeek weekday, int n)
    {
        var first = new DateOnly(year, month, 1);
        var toWeekday = ((int)weekday - (int)first.DayOfWeek + 7) % 7;
        return first.AddDays(toWeekday + 7 * (n - 1));
    }

    private static DateOnly LastWeekday(int year, int month, DayOfWeek weekday)
    {
        var last = new DateOnly(year, month, DateTime.DaysInMonth(year, month));
        var back = ((int)last.DayOfWeek - (int)weekday + 7) % 7;
        return last.AddDays(-back);
    }
}
