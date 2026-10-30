namespace PRN232.Domain.Entities;

public sealed class RoleEntity : BaseEntity
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;
}
