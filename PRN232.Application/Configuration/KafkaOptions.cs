namespace PRN232.Application.Configuration;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    public string BootstrapServers { get; set; } = "localhost:9092";

    public string SecurityProtocol { get; set; } = "PLAINTEXT";

    public string SaslMechanism { get; set; } = "SCRAM-SHA-256";

    public string SaslJaasConfig { get; set; } = string.Empty;

    public string ConsumerGroup { get; set; } = "workflowhub-notification-group";
}
