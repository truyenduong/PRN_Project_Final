using Microsoft.Extensions.Options;
using PRN232.Application.Configuration;
using PRN232.Domain.Entities;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PRN232.Application.Security;

public sealed class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    public long AccessTokenExpiration => _options.AccessTokenExpiration;

    public string CreateAccessToken(UserEntity user) =>
        CreateToken(user, _options.AccessTokenExpiration, [user.Role?.Name ?? "User"]);

    public string CreateRefreshToken(UserEntity user) =>
        CreateToken(user, _options.RefreshTokenExpiration, []);

    public string CreateToken(UserEntity user, long expirationMilliseconds, IReadOnlyList<string> roles)
    {
        var now = DateTimeOffset.UtcNow;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
        {
            ["sub"] = user.Email,
            ["iss"] = "WorkFlowHub",
            ["iat"] = now.ToUnixTimeSeconds(),
            ["exp"] = now.AddMilliseconds(expirationMilliseconds).ToUnixTimeSeconds(),
            ["email"] = user.Email,
            ["userId"] = user.Id,
            ["roles"] = roles
        });

        var encodedHeader = Base64UrlEncode("""{"alg":"HS256","typ":"JWT"}"""u8);
        var encodedPayload = Base64UrlEncode(payload);
        var unsignedToken = $"{encodedHeader}.{encodedPayload}";
        var signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(_options.SecretKey),
            Encoding.ASCII.GetBytes(unsignedToken));
        return $"{unsignedToken}.{Base64UrlEncode(signature)}";
    }

    public bool TryReadToken(string token, out JwtTokenClaims claims)
    {
        claims = default!;
        var parts = token.Split('.');
        if (parts.Length != 3)
        {
            return false;
        }

        try
        {
            using var header = JsonDocument.Parse(Base64UrlDecode(parts[0]));
            if (!header.RootElement.TryGetProperty("alg", out var algorithm)
                || algorithm.GetString() != "HS256")
            {
                return false;
            }

            var unsignedToken = $"{parts[0]}.{parts[1]}";
            var expectedSignature = HMACSHA256.HashData(
                Encoding.UTF8.GetBytes(_options.SecretKey),
                Encoding.ASCII.GetBytes(unsignedToken));
            var suppliedSignature = Base64UrlDecode(parts[2]);
            if (suppliedSignature.Length != expectedSignature.Length
                || !CryptographicOperations.FixedTimeEquals(suppliedSignature, expectedSignature))
            {
                return false;
            }

            using var payload = JsonDocument.Parse(Base64UrlDecode(parts[1]));
            var root = payload.RootElement;
            var email = root.TryGetProperty("sub", out var subject) ? subject.GetString() : null;
            if (string.IsNullOrEmpty(email)
                || !root.TryGetProperty("exp", out var expiration)
                || !expiration.TryGetInt64(out var expirationSeconds)
                || expirationSeconds <= DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            {
                return false;
            }

            var roles = root.TryGetProperty("roles", out var rolesElement)
                && rolesElement.ValueKind == JsonValueKind.Array
                ? rolesElement.EnumerateArray()
                    .Where(role => role.ValueKind == JsonValueKind.String)
                    .Select(role => role.GetString()!)
                    .ToArray()
                : [];
            var userId = root.TryGetProperty("userId", out var idElement)
                && idElement.TryGetInt64(out var id)
                ? id
                : 0;
            claims = new JwtTokenClaims(email, userId, expirationSeconds, roles);
            return true;
        }
        catch (Exception exception) when (
            exception is FormatException or JsonException or ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> value) =>
        Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
        return Convert.FromBase64String(base64);
    }
}

public sealed record JwtTokenClaims(string Email, long UserId, long ExpiresAtSeconds, IReadOnlyList<string> Roles);
