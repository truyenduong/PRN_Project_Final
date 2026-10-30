using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PRN232.Application.Configuration;
using PRN232.Application.Security;
using System.Net;
using System.Text.Json;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController(
    AuthService authService,
    IOptions<GoogleOptions> googleOptions,
    IOptions<FrontendOptions> frontendOptions,
    JsonSerializerOptions jsonOptions) : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("health")]
    public IActionResult Health() =>
        Ok(ApiSuccessResponse<string>.Create("OK", "Backend service is awake and healthy"));

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(request, cancellationToken);
        return Ok(ApiSuccessResponse<AuthResponse>.Create(result, "Login successful"));
    }

    [AllowAnonymous]
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await authService.RegisterAsync(request, cancellationToken);
        return StatusCode(
            StatusCodes.Status201Created,
            ApiSuccessResponse<UserResponse>.Create(result, "Resource created successfully"));
    }

    [AllowAnonymous]
    [HttpPost("refresh-token")]
    public async Task<IActionResult> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(request, cancellationToken);
        return Ok(ApiSuccessResponse<AuthResponse>.Create(result, "Token refreshed successfully"));
    }

    [AllowAnonymous]
    [HttpPost("login-google")]
    public async Task<IActionResult> LoginGoogle(
        GoogleLoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginGoogleAsync(request.IdToken, cancellationToken);
        return Ok(ApiSuccessResponse<AuthResponse>.Create(result, "Request successful"));
    }

    [AllowAnonymous]
    [HttpGet("google/login")]
    public IActionResult GoogleLogin([FromQuery(Name = "client_url")] string? clientUrl)
    {
        var target = GetTrustedFrontendOrigin(clientUrl)
            ?? GetTrustedFrontendOrigin(Request.Headers.Referer.ToString())
            ?? GetTrustedFrontendOrigin(frontendOptions.Value.Url)
            ?? throw new InvalidOperationException("Frontend:Url must be an absolute HTTP or HTTPS URL.");
        var callbackUri = $"{GetBaseUrl()}/api/v1/auth/google/callback";
        var state = WebUtility.UrlEncode(target);
        var url = "https://accounts.google.com/o/oauth2/v2/auth"
            + $"?client_id={googleOptions.Value.ClientId}"
            + $"&redirect_uri={WebUtility.UrlEncode(callbackUri)}"
            + "&response_type=code"
            + $"&scope={WebUtility.UrlEncode("email profile")}"
            + "&access_type=offline"
            + "&prompt=consent"
            + $"&state={state}";
        return Redirect(url);
    }

    [AllowAnonymous]
    [HttpGet("google/callback")]
    public async Task<IActionResult> GoogleCallback(
        [FromQuery] string code,
        [FromQuery] string? state,
        CancellationToken cancellationToken)
    {
        var decodedState = string.IsNullOrWhiteSpace(state)
            ? null
            : Uri.UnescapeDataString(state.Replace('+', ' '));
        var targetFrontend = GetTrustedFrontendOrigin(decodedState)
            ?? GetTrustedFrontendOrigin(frontendOptions.Value.Url)
            ?? throw new InvalidOperationException("Frontend:Url must be an absolute HTTP or HTTPS URL.");

        try
        {
            var callbackUri = $"{GetBaseUrl()}/api/v1/auth/google/callback";
            var auth = await authService.LoginGoogleByAuthorizationCodeAsync(
                code,
                callbackUri,
                cancellationToken);
            var userJson = JsonSerializer.Serialize(auth.User, jsonOptions);
            var redirectUrl = targetFrontend
                + "/?token=" + WebUtility.UrlEncode(auth.AccessToken)
                + "&refreshToken=" + WebUtility.UrlEncode(auth.RefreshToken)
                + "&user=" + WebUtility.UrlEncode(userJson);
            return Redirect(redirectUrl);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return Redirect(targetFrontend + "/?error=" + WebUtility.UrlEncode(exception.Message));
        }
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    public IActionResult Logout()
    {
        authService.Logout(Request.Headers.Authorization.ToString());
        return Ok(ApiSuccessResponse<object>.Create(null, "Logout successful"));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(
        ChangePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var email = User.FindFirst("sub")?.Value;
        if (string.IsNullOrEmpty(email))
        {
            throw new PRN232.Domain.Exceptions.UnauthorizedException(
                "Authentication required to change password");
        }

        await authService.ChangePasswordAsync(email, request, cancellationToken);
        return Ok(ApiSuccessResponse<object>.Create(null, "Password changed successfully"));
    }

    private string GetBaseUrl()
    {
        var scheme = Request.Scheme;
        var host = Request.Host;
        if (host.Port == (scheme == "https" ? 443 : 80))
        {
            host = new HostString(host.Host);
        }

        return $"{scheme}://{host}";
    }

    private string? GetTrustedFrontendOrigin(string? candidate)
    {
        if (!Uri.TryCreate(frontendOptions.Value.Url, UriKind.Absolute, out var configured)
            || !IsHttpUrl(configured))
        {
            return null;
        }

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var requested)
            && IsHttpUrl(requested)
            && Uri.Compare(
                configured,
                requested,
                UriComponents.SchemeAndServer,
                UriFormat.SafeUnescaped,
                StringComparison.OrdinalIgnoreCase) == 0)
        {
            return requested.GetLeftPart(UriPartial.Authority);
        }

        if (string.IsNullOrWhiteSpace(candidate)
            || !Uri.TryCreate(candidate, UriKind.Absolute, out _))
        {
            return configured.GetLeftPart(UriPartial.Authority);
        }

        return null;
    }

    private static bool IsHttpUrl(Uri uri) =>
        string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
        || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}

public sealed class GoogleLoginRequest
{
    [NotBlank(ErrorMessage = "Google ID token is required")]
    public string IdToken { get; set; } = string.Empty;
}
