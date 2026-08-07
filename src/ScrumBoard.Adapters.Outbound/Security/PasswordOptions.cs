namespace ScrumBoard.Adapters.Outbound.Security;

public sealed class PasswordOptions
{
    public const string SectionName = "Password";
    public string Pepper { get; init; } = string.Empty;
    public int Iterations { get; init; } = 210_000;
    // This opaque verifier has no corresponding account or known plaintext password.
    public string DummyHash { get; init; } =
        "pbkdf2-sha512.210000.HTcy6PJQM6ens1Nn1YfqBA==.9sBaNcROaXZrnt3utbVwmZwhmmbzpc/v3Ai/cxsnWrw=";
}
