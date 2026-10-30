namespace PRN232.Application.Configuration;

public sealed class FrontendOptions
{
    public const string SectionName = "Frontend";

    public string Url { get; set; } = "http://localhost:3000";
}
