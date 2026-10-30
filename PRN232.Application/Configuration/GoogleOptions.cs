namespace PRN232.Application.Configuration;

public sealed class GoogleOptions
{
    public const string SectionName = "Google";

    public string ClientId { get; set; } = string.Empty;

    public string ClientSecret { get; set; } = string.Empty;
}
