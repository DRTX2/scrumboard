using Microsoft.Extensions.Options;
using Npgsql;

namespace ScrumBoard.Infrastructure.Configuration;

internal sealed class DatabaseOptionsValidator : IValidateOptions<DatabaseOptions>
{
    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Database))
        {
            return ValidateOptionsResult.Fail("ConnectionStrings:Database is required.");
        }

        try
        {
            var connectionString = new NpgsqlConnectionStringBuilder(options.Database);
            if (string.IsNullOrWhiteSpace(connectionString.Host) || string.IsNullOrWhiteSpace(connectionString.Database))
            {
                return ValidateOptionsResult.Fail("ConnectionStrings:Database must include Host and Database.");
            }
        }
        catch (ArgumentException exception)
        {
            return ValidateOptionsResult.Fail($"ConnectionStrings:Database is invalid: {exception.Message}");
        }

        return ValidateOptionsResult.Success;
    }
}
