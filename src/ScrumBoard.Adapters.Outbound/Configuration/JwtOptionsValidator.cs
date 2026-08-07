using Microsoft.Extensions.Options;
using ScrumBoard.Adapters.Outbound.Security;

namespace ScrumBoard.Adapters.Outbound.Configuration;

internal sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        var failures = new List<string>();
        if (string.IsNullOrWhiteSpace(options.Issuer)) failures.Add("Jwt:Issuer is required.");
        if (string.IsNullOrWhiteSpace(options.Audience)) failures.Add("Jwt:Audience is required.");
        if (string.IsNullOrWhiteSpace(options.SigningKey) || options.SigningKey.Length < 32)
        {
            failures.Add("Jwt:SigningKey must contain at least 32 characters.");
        }

        if (options.LifetimeMinutes is < 5 or > 120)
        {
            failures.Add("Jwt:LifetimeMinutes must be between 5 and 120.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
