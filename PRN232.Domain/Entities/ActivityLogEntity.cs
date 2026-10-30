namespace PRN232.Domain.Entities;

public sealed class ActivityLogEntity : BaseEntity
{
    public long Id { get; set; }

    public long UserId { get; set; }

    public UserEntity User { get; set; } = null!;

    public long? ProjectId { get; set; }

    public ProjectEntity? Project { get; set; }

    public string Action { get; set; } = string.Empty;
}
