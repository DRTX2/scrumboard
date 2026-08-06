namespace ScrumBoard.Application.Errors;

internal static class InputValidation
{
    public static T Required<T>(T? value, string code, string message) where T : class =>
        value ?? throw new ValidationException(code, message);

    public static void Identifier(Guid value, string name)
    {
        if (value == Guid.Empty)
        {
            throw new ValidationException("invalid_identifier", $"El identificador {name} no es válido.");
        }
    }

    public static void Range(int value, int minimum, int maximum, string name)
    {
        if (value < minimum || value > maximum)
        {
            throw new ValidationException(
                "value_out_of_range",
                $"El valor de {name} debe estar entre {minimum} y {maximum}.");
        }
    }

    public static void Positive(long value, string name)
    {
        if (value < 1)
        {
            throw new ValidationException("value_out_of_range", $"El valor de {name} debe ser mayor que cero.");
        }
    }

    public static void RequiredText(string? value, int maximumLength, string name)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            throw new ValidationException("required_value", $"El campo {name} es obligatorio.");
        }

        if (normalized.Length > maximumLength)
        {
            throw new ValidationException(
                "value_too_long",
                $"El campo {name} no puede superar {maximumLength} caracteres.");
        }
    }

    public static void OptionalText(string? value, int maximumLength, string name)
    {
        if (value?.Trim().Length > maximumLength)
        {
            throw new ValidationException(
                "value_too_long",
                $"El campo {name} no puede superar {maximumLength} caracteres.");
        }
    }

    public static void ProjectDates(DateOnly startDate, DateOnly expectedEndDate)
    {
        if (startDate == default || expectedEndDate == default)
        {
            throw new ValidationException("project_dates_required", "Las fechas de inicio y fin previstas son obligatorias.");
        }

        if (expectedEndDate < startDate)
        {
            throw new ValidationException(
                "invalid_project_dates",
                "La fecha prevista de fin no puede ser anterior a la fecha de inicio.");
        }
    }

    public static string? Search(string? value)
    {
        var normalized = value?.Trim();
        if (normalized?.Length > 200)
        {
            throw new ValidationException("search_too_long", "La búsqueda no puede superar 200 caracteres.");
        }

        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    public static void Defined<TEnum>(TEnum? value, string name) where TEnum : struct, Enum
    {
        if (value is not null && !Enum.IsDefined(value.Value))
        {
            throw new ValidationException("invalid_enum_value", $"El valor de {name} no es válido.");
        }
    }
}
