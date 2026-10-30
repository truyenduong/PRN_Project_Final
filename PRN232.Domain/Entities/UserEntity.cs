namespace PRN232.Domain.Entities;

public sealed class UserEntity : BaseEntity
{
    public long Id { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string? Avatar { get; set; }

    public string? Status { get; set; } = "active";

    public long? RoleId { get; set; }

    public RoleEntity? Role { get; set; }
}
