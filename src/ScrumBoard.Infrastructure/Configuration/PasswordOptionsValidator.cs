using Microsoft.Extensions.Options;
using ScrumBoard.Infrastructure.Adapters.Outbound.Security;

namespace ScrumBoard.Infrastructure.Configuration;

internal sealed class PasswordOptionsValidator : IValidateOptions<PasswordOptions>
{
    public ValidateOptionsResult Validate(string? name, PasswordOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Pepper) || options.Pepper.Length < 16)
        {
            failures.Add("Password:Pepper must contain at least 16 characters.");
        }

        if (options.Iterations < 100_000)
        {
            failures.Add("Password:Iterations must be at least 100000.");
        }

        if (!IsValidDummyHash(options.DummyHash, options.Iterations))
        {
            failures.Add("Password:DummyHash must be a valid PBKDF2-SHA512 hash using Password:Iterations.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static bool IsValidDummyHash(string encodedHash, int configuredIterations)
    {
        if (string.IsNullOrWhiteSpace(encodedHash)) return false;

        try
        {
            var segments = encodedHash.Split('.');
            return segments is ["pbkdf2-sha512", _, _, _]
                   && int.TryParse(
                       segments[1],
                       System.Globalization.NumberStyles.None,
                       System.Globalization.CultureInfo.InvariantCulture,
                       out var iterations)
                   && iterations == configuredIterations
                   && Convert.FromBase64String(segments[2]).Length == 16
                   && Convert.FromBase64String(segments[3]).Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
