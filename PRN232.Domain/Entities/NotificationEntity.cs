namespace PRN232.Domain.Entities;

public sealed class NotificationEntity : BaseEntity
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public UserEntity User { get; set; } = null!;

    public string Type { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public bool? IsRead { get; set; } = false;
}
