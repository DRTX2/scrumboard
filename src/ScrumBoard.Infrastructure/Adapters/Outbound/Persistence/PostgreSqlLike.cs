namespace ScrumBoard.Infrastructure.Adapters.Outbound.Persistence;

internal static class PostgreSqlLike
{
    public const string EscapeCharacter = "\\";

    public static string ContainsLiteral(string value) =>
        $"%{value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("%", "\\%", StringComparison.Ordinal)
            .Replace("_", "\\_", StringComparison.Ordinal)}%";
}
