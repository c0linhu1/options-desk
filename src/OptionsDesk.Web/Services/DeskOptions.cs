namespace OptionsDesk.Web.Services;

public class DeskOptions
{
    public double RiskFreeRate { get; set; } = 0.04;
    public int ExpiriesListed { get; set; } = 4;
    public int StrikesEachSide { get; set; } = 8;
    public string DefaultTrader { get; set; } = "trader1";
    public DateOnly? ValuationDate { get; set; }

    /// <summary>How many business days back a trade may be dated. 0 means trades must be dated today.</summary>
    public int MaxBackdateBusinessDays { get; set; }
}

/// <summary>The date positions are valued and options are priced as of.</summary>
public interface IValuationClock
{
    DateOnly Today { get; }
}

public class SystemValuationClock(Microsoft.Extensions.Options.IOptions<DeskOptions> options) : IValuationClock
{
    public DateOnly Today => options.Value.ValuationDate ?? DateOnly.FromDateTime(DateTime.Today);
}

public enum DeskErrorKind
{
    NotFound,
    Invalid,
}

public class DeskException(DeskErrorKind kind, string message) : Exception(message)
{
    public DeskErrorKind Kind { get; } = kind;
}
