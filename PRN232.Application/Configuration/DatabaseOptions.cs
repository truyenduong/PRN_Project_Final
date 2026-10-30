namespace PRN232.Application.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    public string Schema { get; set; } = "workflow";
}
