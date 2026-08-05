using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using ScrumBoard.Application.Ports.Outbound;

namespace ScrumBoard.Infrastructure.Adapters.Outbound.Security;

public sealed class Pbkdf2PasswordHasher(IOptions<PasswordOptions> options) : IPasswordHasher
{
    private readonly PasswordOptions _options = options.Value;

    public bool Verify(string password, string encodedHash)
    {
        try
        {
            var segments = encodedHash.Split('.', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 4 || segments[0] != "pbkdf2-sha512") return false;
            var iterations = int.Parse(segments[1], System.Globalization.CultureInfo.InvariantCulture);
            var salt = Convert.FromBase64String(segments[2]);
            var expected = Convert.FromBase64String(segments[3]);
            var actual = Derive(password, _options.Pepper, salt, iterations, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    public static string HashWithSalt(string password, string pepper, byte[] salt, int iterations = 210_000)
    {
        var hash = Derive(password, pepper, salt, iterations, 32);
        return $"pbkdf2-sha512.{iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    private static byte[] Derive(string password, string pepper, byte[] salt, int iterations, int length) =>
        Rfc2898DeriveBytes.Pbkdf2(
            Encoding.UTF8.GetBytes(password + pepper),
            salt,
            iterations,
            HashAlgorithmName.SHA512,
            length);
}
