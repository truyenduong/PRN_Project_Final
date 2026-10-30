namespace PRN232.Domain.Entities;

public sealed class ProjectEntity : BaseEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public long OwnerId { get; set; }

    public UserEntity Owner { get; set; } = null!;
}
