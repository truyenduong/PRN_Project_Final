using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PRN232.Api.Controllers;
using PRN232.Application.Configuration;
using PRN232.Application.Security;
using PRN232.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PRN232.Tests;

public sealed class AuthenticationTests
{
    private const string Secret = "workflowhub-secret-key-must-be-at-least-256-bits-long-for-hmac-sha256-security!";

    [Fact]
    public void DotNetToken_IsAcceptedByJwtBearerCompatibleValidator()
    {
        var service = CreateTokenService();
        var user = new UserEntity
        {
            Id = 15,
            Email = "person@example.com",
            Name = "Person",
            Role = new RoleEntity { Id = 2, Name = "User" }
        };

        var token = service.CreateAccessToken(user);
        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256]
        };
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        var principal = handler.ValidateToken(token, parameters, out _);

        Assert.Equal("person@example.com", principal.FindFirst("sub")?.Value);
        Assert.Equal("15", principal.FindFirst("userId")?.Value);
        Assert.Equal("User", principal.FindFirst("roles")?.Value);
        Assert.True(service.TryReadToken(token, out var claims));
        Assert.Equal("person@example.com", claims.Email);
    }

    [Fact]
    public void JavaStyleHs256Token_IsAcceptedByDotNetTokenReader()
    {
        var service = CreateTokenService();
        var token = CreateJavaStyleToken(
            "person@example.com",
            15,
            DateTimeOffset.UtcNow.AddMinutes(2).ToUnixTimeSeconds(),
            ["ADMIN"]);

        Assert.True(service.TryReadToken(token, out var claims));
        Assert.Equal("person@example.com", claims.Email);
        Assert.Equal(15, claims.UserId);
        Assert.Equal(["ADMIN"], claims.Roles);
    }

    [Fact]
    public void ExpiredToken_IsRejected()
    {
        var service = CreateTokenService();
        var token = service.CreateToken(
            new UserEntity { Id = 1, Email = "person@example.com" },
            -60_000,
            []);

        Assert.False(service.TryReadToken(token, out _));
    }

    [Fact]
    public void SpringCompatibleBCrypt2aHash_IsVerified()
    {
        var springPasswordHash = BCrypt.Net.BCrypt.HashPassword("password", 10);

        Assert.StartsWith("$2a$", springPasswordHash, StringComparison.Ordinal);
        Assert.True(BCrypt.Net.BCrypt.Verify("password", springPasswordHash));
    }

    [Fact]
    public void DotNetBCryptHash_UsesSpringCompatibleFormat()
    {
        var hash = BCrypt.Net.BCrypt.HashPassword("Password123", 10);

        Assert.StartsWith("$2a$", hash, StringComparison.Ordinal);
        Assert.True(BCrypt.Net.BCrypt.Verify("Password123", hash));
    }

    [Fact]
    public void GoogleLogin_RejectsUntrustedClientUrl()
    {
        var controller = new AuthController(
            null!,
            Options.Create(new GoogleOptions { ClientId = "client-id" }),
            Options.Create(new FrontendOptions { Url = "https://app.example" }),
            new JsonSerializerOptions());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.Request.Scheme = "https";
        controller.HttpContext.Request.Host = new HostString("api.example");

        var result = Assert.IsType<RedirectResult>(controller.GoogleLogin("https://attacker.example"));

        Assert.DoesNotContain("attacker.example", result.Url, StringComparison.Ordinal);
        Assert.Contains("state=https%3A%2F%2Fapp.example", result.Url, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GoogleCallback_RejectsUntrustedStateOrigin()
    {
        var controller = new AuthController(
            null!,
            Options.Create(new GoogleOptions { ClientId = "client-id" }),
            Options.Create(new FrontendOptions { Url = "https://app.example" }),
            new JsonSerializerOptions());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        controller.HttpContext.Request.Scheme = "https";
        controller.HttpContext.Request.Host = new HostString("api.example");

        var result = Assert.IsType<RedirectResult>(await controller.GoogleCallback(
            "auth-code",
            "https%3A%2F%2Fattacker.example",
            CancellationToken.None));

        Assert.StartsWith("https://app.example/?error=", result.Url, StringComparison.Ordinal);
        Assert.DoesNotContain("attacker.example", result.Url, StringComparison.Ordinal);
    }

    private static JwtTokenService CreateTokenService() =>
        new(Options.Create(new JwtOptions
        {
            SecretKey = Secret,
            Issuer = "WorkFlowHub",
            AccessTokenExpiration = 1_800_000,
            RefreshTokenExpiration = 604_800_000
        }));

    private static string CreateJavaStyleToken(
        string email,
        long userId,
        long expiration,
        IReadOnlyList<string> roles)
    {
        var header = Base64UrlEncode("""{"alg":"HS256","typ":"JWT"}"""u8);
        var payload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object?>
        {
            ["sub"] = email,
            ["iss"] = "WorkFlowHub",
            ["iat"] = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            ["exp"] = expiration,
            ["email"] = email,
            ["userId"] = userId,
            ["roles"] = roles
        }));
        var unsignedToken = $"{header}.{payload}";
        var signature = HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(Secret),
            Encoding.ASCII.GetBytes(unsignedToken));
        return $"{unsignedToken}.{Base64UrlEncode(signature)}";
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
