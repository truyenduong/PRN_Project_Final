using Google.Apis.Auth;
using Microsoft.Extensions.Options;
using PRN232.Application.Configuration;
using PRN232.Application.Security;
using PRN232.Domain.Exceptions;
using System.Net.Http.Headers;
using System.Text.Json;

namespace PRN232.Infrastructure;

public sealed class GoogleAuthProvider(
    HttpClient httpClient,
    IOptions<GoogleOptions> googleOptions) : IGoogleAuthProvider
{
    private readonly GoogleOptions _options = googleOptions.Value;

    public async Task<GoogleProfile> ValidateIdTokenAsync(
        string idToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var settings = new GoogleJsonWebSignature.ValidationSettings();
            if (!string.IsNullOrEmpty(_options.ClientId))
            {
                settings.Audience = [_options.ClientId];
            }

            var payload = await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
            cancellationToken.ThrowIfCancellationRequested();
            return new GoogleProfile(
                payload.Email,
                payload.EmailVerified,
                payload.Name,
                payload.Picture);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new UnauthorizedException($"Google authentication failed: {exception.Message}");
        }
    }

    public async Task<GoogleProfile> ExchangeAuthorizationCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken)
    {
        using var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code"
        });
        using var tokenResponse = await httpClient.PostAsync(
            "https://oauth2.googleapis.com/token",
            tokenRequest,
            cancellationToken);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            throw new UnauthorizedException("Failed to obtain access token from Google");
        }

        using var tokenDocument = await JsonDocument.ParseAsync(
            await tokenResponse.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        if (!tokenDocument.RootElement.TryGetProperty("access_token", out var accessTokenElement)
            || string.IsNullOrEmpty(accessTokenElement.GetString()))
        {
            throw new UnauthorizedException("Failed to obtain access token from Google");
        }

        using var userRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "https://www.googleapis.com/oauth2/v3/userinfo");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            accessTokenElement.GetString());
        using var userResponse = await httpClient.SendAsync(userRequest, cancellationToken);
        if (!userResponse.IsSuccessStatusCode)
        {
            throw new UnauthorizedException("Failed to obtain user info from Google");
        }

        using var userDocument = await JsonDocument.ParseAsync(
            await userResponse.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);
        var root = userDocument.RootElement;
        if (!root.TryGetProperty("email", out var emailElement)
            || string.IsNullOrEmpty(emailElement.GetString()))
        {
            throw new UnauthorizedException("Failed to obtain user info from Google");
        }

        return new GoogleProfile(
            emailElement.GetString()!,
            root.TryGetProperty("email_verified", out var verified)
                ? verified.GetBoolean()
                : null,
            root.TryGetProperty("name", out var name) ? name.GetString() : null,
            root.TryGetProperty("picture", out var picture) ? picture.GetString() : null);
    }
}
