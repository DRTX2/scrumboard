using System.Globalization;
using ScrumBoard.Infrastructure.Adapters.Outbound.Reporting;

namespace ScrumBoard.UnitTests.Adapters.Reporting;

public sealed class ReportDateFormatterTests
{
    [Fact]
    public void Format_UsesRequiredCalendarAndLocalTimestampFormats()
    {
        var timestamp = new DateTimeOffset(2026, 8, 5, 23, 47, 0, TimeSpan.FromHours(5));

        Assert.Equal("05/08/2026", ReportDateFormatter.Format(new DateOnly(2026, 8, 5)));
        Assert.Equal(timestamp.ToLocalTime().ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            ReportDateFormatter.Format(timestamp));
    }
}
