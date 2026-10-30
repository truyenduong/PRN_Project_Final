namespace PRN232.Application.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Security:Jwt";

    public string SecretKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "WorkFlowHub";

    public long AccessTokenExpiration { get; set; } = 1_800_000;

    public long RefreshTokenExpiration { get; set; } = 604_800_000;
}
