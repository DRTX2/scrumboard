using ScrumBoard.Adapters.Outbound.Configuration;
using ScrumBoard.Adapters.Outbound.Security;

namespace ScrumBoard.UnitTests.Infrastructure;

public sealed class SecurityOptionsValidationTests
{
    [Fact]
    public void JwtOptions_WithoutSigningKey_FailValidationWithoutThrowing()
    {
        var result = new JwtOptionsValidator().Validate(null, new JwtOptions
        {
            Issuer = "issuer",
            Audience = "audience",
            SigningKey = string.Empty
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("SigningKey", StringComparison.Ordinal));
    }

    [Fact]
    public void PasswordOptions_WithoutPepper_FailValidationWithoutThrowing()
    {
        var result = new PasswordOptionsValidator().Validate(null, new PasswordOptions());

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("Pepper", StringComparison.Ordinal));
    }

    [Fact]
    public void PasswordOptions_WithMalformedDummyHash_FailValidationWithoutThrowing()
    {
        var result = new PasswordOptionsValidator().Validate(null, new PasswordOptions
        {
            Pepper = "a-sufficiently-long-pepper",
            DummyHash = "not-a-password-hash"
        });

        Assert.True(result.Failed);
        Assert.Contains(result.Failures, failure => failure.Contains("DummyHash", StringComparison.Ordinal));
    }
}
