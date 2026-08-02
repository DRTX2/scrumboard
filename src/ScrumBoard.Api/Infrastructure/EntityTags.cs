using ScrumBoard.Application.Common;

namespace ScrumBoard.Api.Infrastructure;

internal static class EntityTags
{
    public static string Format(long version) => $"\"{version}\"";

    public static long Require(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("If-Match", out var values)) throw new PreconditionRequiredException();
        var value = values.ToString().Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) value = value[2..];
        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
        if (!long.TryParse(value, out var version) || version < 1)
        {
            throw new BadHttpRequestException("If-Match must contain a valid numeric entity tag.");
        }
        return version;
    }

    public static void Write(HttpResponse response, long version) => response.Headers.ETag = Format(version);
}
