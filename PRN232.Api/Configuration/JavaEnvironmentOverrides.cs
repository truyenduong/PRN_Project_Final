using Npgsql;
using System.Globalization;

namespace PRN232.Api.Configuration;

public static class JavaEnvironmentOverrides
{
    public static void Apply(IConfiguration configuration)
    {
        ApplyDatabase(configuration);
        SetIfPresent(configuration, "JWT_SECRET_KEY", "Security:Jwt:SecretKey");
        SetIfPresent(configuration, "GOOGLE_CLIENT_ID", "Google:ClientId");
        SetIfPresent(configuration, "GOOGLE_CLIENT_SECRET", "Google:ClientSecret");
        SetIfPresent(configuration, "FRONTEND_URL", "Frontend:Url");
        SetIfPresent(configuration, "REDIS_HOST", "Redis:Host");
        SetIfPresent(configuration, "REDIS_PASSWORD", "Redis:Password");
        SetIfPresent(configuration, "KAFKA_BOOTSTRAP_SERVERS", "Kafka:BootstrapServers");
        SetIfPresent(configuration, "KAFKA_SECURITY_PROTOCOL", "Kafka:SecurityProtocol");
        SetIfPresent(configuration, "KAFKA_SASL_MECHANISM", "Kafka:SaslMechanism");
        SetIfPresent(configuration, "KAFKA_SASL_JAAS_CONFIG", "Kafka:SaslJaasConfig");
        SetIfPresent(configuration, "KAFKA_CONSUMER_GROUP", "Kafka:ConsumerGroup");

        ApplyLong(configuration, "JWT_ACCESS_TOKEN_EXPIRATION", "Security:Jwt:AccessTokenExpiration");
        ApplyLong(configuration, "JWT_REFRESH_TOKEN_EXPIRATION", "Security:Jwt:RefreshTokenExpiration");
        ApplyInteger(configuration, "REDIS_PORT", "Redis:Port");
        ApplyBoolean(configuration, "REDIS_SSL_ENABLED", "Redis:SslEnabled");

        var serverPort = Environment.GetEnvironmentVariable("SERVER_PORT");
        if (!string.IsNullOrWhiteSpace(serverPort))
        {
            configuration["Urls"] = $"http://0.0.0.0:{serverPort}";
        }
    }

    private static void ApplyDatabase(IConfiguration configuration)
    {
        var jdbcUrl = Environment.GetEnvironmentVariable("DB_URL");
        var username = Environment.GetEnvironmentVariable("DB_USERNAME");
        var password = Environment.GetEnvironmentVariable("DB_PASSWORD");
        var connectionString = configuration.GetConnectionString("WorkflowHub");

        if (string.IsNullOrWhiteSpace(jdbcUrl)
            && string.IsNullOrWhiteSpace(username)
            && string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        var connection = new NpgsqlConnectionStringBuilder(connectionString);
        if (!string.IsNullOrWhiteSpace(jdbcUrl))
        {
            const string jdbcPrefix = "jdbc:";
            var postgresUrl = jdbcUrl.StartsWith(jdbcPrefix, StringComparison.OrdinalIgnoreCase)
                ? jdbcUrl[jdbcPrefix.Length..]
                : jdbcUrl;

            if (!Uri.TryCreate(postgresUrl, UriKind.Absolute, out var uri)
                || !string.Equals(uri.Scheme, "postgresql", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("DB_URL must be a PostgreSQL JDBC URL.");
            }

            connection.Host = uri.Host;
            connection.Port = uri.IsDefaultPort ? 5432 : uri.Port;
            connection.Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
        }

        if (!string.IsNullOrWhiteSpace(username))
        {
            connection.Username = username;
        }

        if (password is not null)
        {
            connection.Password = password;
        }

        configuration["ConnectionStrings:WorkflowHub"] = connection.ConnectionString;
    }

    private static void SetIfPresent(
        IConfiguration configuration,
        string environmentVariable,
        string configurationKey,
        bool overwrite = true)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        if (!string.IsNullOrWhiteSpace(value)
            && (overwrite || string.IsNullOrWhiteSpace(configuration[configurationKey])))
        {
            configuration[configurationKey] = value;
        }
    }

    private static void ApplyLong(IConfiguration configuration, string environmentVariable, string configurationKey)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        if (value is null)
        {
            return;
        }

        if (!long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            throw new InvalidOperationException($"{environmentVariable} must be an integer number of milliseconds.");
        }

        configuration[configurationKey] = value;
    }

    private static void ApplyInteger(IConfiguration configuration, string environmentVariable, string configurationKey)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        if (value is null)
        {
            return;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
        {
            throw new InvalidOperationException($"{environmentVariable} must be an integer.");
        }

        configuration[configurationKey] = value;
    }

    private static void ApplyBoolean(IConfiguration configuration, string environmentVariable, string configurationKey)
    {
        var value = Environment.GetEnvironmentVariable(environmentVariable);
        if (value is null)
        {
            return;
        }

        if (!bool.TryParse(value, out _))
        {
            throw new InvalidOperationException($"{environmentVariable} must be true or false.");
        }

        configuration[configurationKey] = value;
    }
}
