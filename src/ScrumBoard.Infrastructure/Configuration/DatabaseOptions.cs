namespace ScrumBoard.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "ConnectionStrings";
    public string Database { get; init; } = string.Empty;
}
