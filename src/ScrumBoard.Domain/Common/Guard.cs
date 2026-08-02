namespace ScrumBoard.Domain.Common;

internal static class Guard
{
    public static string Required(string value, string name, int maxLength)
    {
        var normalized = value.Trim();
        if (normalized.Length is 0)
        {
            throw new DomainException("required_value", $"{name} is required.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException("value_too_long", $"{name} cannot exceed {maxLength} characters.");
        }

        return normalized;
    }

    public static string? Optional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > maxLength)
        {
            throw new DomainException("value_too_long", $"Value cannot exceed {maxLength} characters.");
        }

        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
