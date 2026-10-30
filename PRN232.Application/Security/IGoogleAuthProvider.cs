namespace PRN232.Application.Security;

public sealed record GoogleProfile(string Email, bool? EmailVerified, string? Name, string? Picture);

public interface IGoogleAuthProvider
{
    Task<GoogleProfile> ValidateIdTokenAsync(string idToken, CancellationToken cancellationToken);

    Task<GoogleProfile> ExchangeAuthorizationCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken);
}
