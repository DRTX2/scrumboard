namespace ScrumBoard.Domain.Primitives;

internal static class Guard
{
    public static string Required(string? value, string name, int maxLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            throw new DomainException("required_value", $"El campo {name} es obligatorio.");
        }

        if (normalized.Length > maxLength)
        {
            throw new DomainException("value_too_long", $"El campo {name} no puede superar {maxLength} caracteres.");
        }

        return normalized;
    }

    public static string? Optional(string? value, int maxLength)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > maxLength)
        {
            throw new DomainException("value_too_long", $"El valor no puede superar {maxLength} caracteres.");
        }

        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    public static Guid Required(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new DomainException("required_identifier", $"El identificador {name} es obligatorio.");
        }

        return value;
    }

    public static TEnum Defined<TEnum>(TEnum value, string name) where TEnum : struct, Enum
    {
        if (!Enum.IsDefined(value))
        {
            throw new DomainException("invalid_enum_value", $"El valor de {name} no es válido.");
        }

        return value;
    }

    public static long Positive(long value, string name)
    {
        if (value < 1)
        {
            throw new DomainException("invalid_position", $"El valor de {name} debe ser mayor que cero.");
        }

        return value;
    }
}
