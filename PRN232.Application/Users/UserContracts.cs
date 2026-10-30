using System.ComponentModel.DataAnnotations;

namespace PRN232.Application.Users;

public sealed class CreateUserRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Email must be valid")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required")]
    [StringLength(255, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Name is required")]
    [StringLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string Name { get; set; } = string.Empty;

    public string? Avatar { get; set; }

    public string? Status { get; set; }
}

public sealed class UpdateUserRequest
{
    [EmailAddress(ErrorMessage = "Email must be valid")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string? Email { get; set; }

    [StringLength(255, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
    public string? Password { get; set; }

    [StringLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string? Name { get; set; }

    public string? Avatar { get; set; }

    public string? Status { get; set; }

    public long? RoleId { get; set; }
}

public sealed record UserResponse(
    long Id,
    string Email,
    string Name,
    string? Avatar,
    string? Status,
    long? RoleId,
    string? RoleName,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record UserPageResult(
    IReadOnlyList<UserResponse> Content,
    int Page,
    int Size,
    long TotalElements);
