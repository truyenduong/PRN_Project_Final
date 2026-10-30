namespace PRN232.Domain.Entities;

public sealed class ProjectMemberEntity : BaseEntity
{
    public long ProjectId { get; set; }

    public ProjectEntity Project { get; set; } = null!;

    public long UserId { get; set; }

    public UserEntity User { get; set; } = null!;

    public long? RoleId { get; set; }

    public RoleEntity? Role { get; set; }
}
