using System.Globalization;

namespace ScrumBoard.Adapters.Outbound.Reporting;

internal static class ReportDateFormatter
{
    public static string Format(DateOnly value) => value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Format(DateTimeOffset value) =>
        value.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}
