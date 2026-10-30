namespace PRN232.Application.Configuration;

public sealed class CorsOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOriginPatterns { get; set; } = [];

    public string[] AllowedMethods { get; set; } = [];

    public string[] AllowedHeaders { get; set; } = [];

    public string[] ExposedHeaders { get; set; } = [];

    public bool AllowCredentials { get; set; } = true;
}
