namespace PRN232.Application.Configuration;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 6379;

    public string Password { get; set; } = string.Empty;

    public bool SslEnabled { get; set; }

    public int TimeoutMilliseconds { get; set; } = 5_000;

    public int CacheTimeToLiveMilliseconds { get; set; } = 60_000;
}
