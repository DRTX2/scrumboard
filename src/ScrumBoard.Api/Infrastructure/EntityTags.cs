namespace ScrumBoard.Api.Infrastructure;

internal static class EntityTags
{
    public static string Format(long version) => $"\"{version}\"";

    public static long Require(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("If-Match", out var values)) throw new EntityTagRequiredException();
        if (values.Count != 1)
        {
            throw new BadHttpRequestException("If-Match debe contener una etiqueta de entidad numérica válida.");
        }

        var value = values[0]?.Trim();
        if (value is null || value.Length < 3 || value[0] != '"' || value[^1] != '"')
        {
            throw new BadHttpRequestException("If-Match debe contener una etiqueta de entidad numérica válida.");
        }

        var digits = value.AsSpan(1, value.Length - 2);
        foreach (var character in digits)
        {
            if (character is < '0' or > '9')
            {
                throw new BadHttpRequestException("If-Match debe contener una etiqueta de entidad numérica válida.");
            }
        }

        if (!long.TryParse(digits, out var version) || version < 1)
        {
            throw new BadHttpRequestException("If-Match debe contener una etiqueta de entidad numérica válida.");
        }

        return version;
    }

    public static void Write(HttpResponse response, long version) => response.Headers.ETag = Format(version);
}
