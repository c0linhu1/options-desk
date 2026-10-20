using OptionsDesk.Pricing;

namespace OptionsDesk.Pricing.Tests;

public class HolidayCalendarTests
{
    // NYSE's published full-day closures for each year.
    public static TheoryData<int, string[]> PublishedClosures => new()
    {
        { 2022, ["2022-01-17", "2022-02-21", "2022-04-15", "2022-05-30", "2022-06-20", "2022-07-04", "2022-09-05", "2022-11-24", "2022-12-26"] },
        { 2023, ["2023-01-02", "2023-01-16", "2023-02-20", "2023-04-07", "2023-05-29", "2023-06-19", "2023-07-04", "2023-09-04", "2023-11-23", "2023-12-25"] },
        { 2024, ["2024-01-01", "2024-01-15", "2024-02-19", "2024-03-29", "2024-05-27", "2024-06-19", "2024-07-04", "2024-09-02", "2024-11-28", "2024-12-25"] },
        { 2025, ["2025-01-01", "2025-01-09", "2025-01-20", "2025-02-17", "2025-04-18", "2025-05-26", "2025-06-19", "2025-07-04", "2025-09-01", "2025-11-27", "2025-12-25"] },
        { 2026, ["2026-01-01", "2026-01-19", "2026-02-16", "2026-04-03", "2026-05-25", "2026-06-19", "2026-07-03", "2026-09-07", "2026-11-26", "2026-12-25"] },
        { 2027, ["2027-01-01", "2027-01-18", "2027-02-15", "2027-03-26", "2027-05-31", "2027-06-18", "2027-07-05", "2027-09-06", "2027-11-25", "2027-12-24"] },
        { 2028, ["2028-01-17", "2028-02-21", "2028-04-14", "2028-05-29", "2028-06-19", "2028-07-04", "2028-09-04", "2028-11-23", "2028-12-25"] },
    };

    [Theory]
    [MemberData(nameof(PublishedClosures))]
    public void MatchesPublishedClosures(int year, string[] expected)
    {
        Assert.Equal(expected.Select(DateOnly.Parse).Order(), MarketCalendar.HolidaysIn(year).Order());
    }

    [Theory]
    [InlineData(2000, 4, 23)]
    [InlineData(2008, 3, 23)]
    [InlineData(2011, 4, 24)]
    [InlineData(2019, 4, 21)]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2028, 4, 16)]
    [InlineData(2038, 4, 25)]
    public void ComputesEaster(int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), MarketCalendar.EasterSunday(year));
    }

    [Fact]
    public void SaturdayNewYearIsNotObserved()
    {
        // Jan 1 2022 and Jan 1 2028 are Saturdays; the exchange stays open the Friday before.
        Assert.DoesNotContain(new DateOnly(2027, 12, 31), MarketCalendar.HolidaysIn(2027));
        Assert.True(MarketCalendar.IsBusinessDay(new DateOnly(2027, 12, 31)));
        Assert.DoesNotContain(MarketCalendar.HolidaysIn(2028), d => d.Month == 1 && d.Day <= 3);
    }

    [Fact]
    public void GoodFridayMovesTheMonthlyExpiryToThursday()
    {
        // April 2022 options expired on Thursday the 14th because Good Friday fell on the third Friday.
        Assert.Equal(new DateOnly(2022, 4, 15), MarketCalendar.ThirdFriday(2022, 4));
        Assert.Equal(new DateOnly(2022, 4, 14), MarketCalendar.MonthlyExpiry(2022, 4));
    }

    [Fact]
    public void SettlementSkipsHolidaysAfter2027()
    {
        // Previously the hardcoded list ended in 2027, so Good Friday 2028 counted as a business day.
        Assert.Equal(new DateOnly(2028, 4, 17), MarketCalendar.SettlementDate(new DateOnly(2028, 4, 13)));
    }

    [Fact]
    public void YearsBeforeTheRulesApplyAreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => MarketCalendar.HolidaysIn(2021));
        Assert.Throws<ArgumentOutOfRangeException>(() => MarketCalendar.IsBusinessDay(new DateOnly(2021, 12, 31)));
    }
}
