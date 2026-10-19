using System.Globalization;
using System.Text;

namespace OptionsDesk.Web.Services;

/// <summary>Minimal RFC 4180 CSV writer for the blotter and positions exports.</summary>
public static class Csv
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    /// <summary>Quotes a text field when it contains a delimiter, quote, or line break.</summary>
    public static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return "";
        if (value.IndexOfAny([',', '"', '\n', '\r']) < 0)
            return value;
        return '"' + value.Replace("\"", "\"\"") + '"';
    }

    /// <summary>
    /// One CSV line. Text goes through <see cref="Field"/>; numbers and dates never contain delimiters,
    /// so they're written as-is: money to 2 decimals, other numbers round-trip.
    /// </summary>
    public static string Row(params object?[] values)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < values.Length; i++)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append(values[i] switch
            {
                null => "",
                string s => Field(s),
                decimal d => d.ToString("N2", Invariant),
                double x => x.ToString("R", Invariant),
                DateOnly date => date.ToString("yyyy-MM-dd", Invariant),
                IFormattable f => f.ToString(null, Invariant),
                var other => Field(other.ToString()),
            });
        }
        return sb.ToString();
    }

    public static byte[] Document(string header, IEnumerable<string> rows)
    {
        var sb = new StringBuilder(header).Append("\r\n");
        foreach (var row in rows)
            sb.Append(row).Append("\r\n");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }
}
