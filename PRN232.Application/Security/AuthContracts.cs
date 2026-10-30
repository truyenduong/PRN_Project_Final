using System.ComponentModel.DataAnnotations;

namespace PRN232.Application.Security;

public sealed class LoginRequest
{
    [NotBlank(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "must be a well-formed email address")]
    public string Email { get; set; } = string.Empty;

    [NotBlank(ErrorMessage = "Password is required")]
    public string Password { get; set; } = string.Empty;
}

public sealed class RegisterRequest
{
    [NotBlank(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "must be a well-formed email address")]
    [StringLength(255, ErrorMessage = "Email cannot exceed 255 characters")]
    public string Email { get; set; } = string.Empty;

    [NotBlank(ErrorMessage = "Password is required")]
    [StringLength(255, MinimumLength = 6, ErrorMessage = "Password must be at least 6 characters")]
    public string Password { get; set; } = string.Empty;

    [NotBlank(ErrorMessage = "Name is required")]
    [StringLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string Name { get; set; } = string.Empty;

    public string? Avatar { get; set; }
}

public sealed class RefreshTokenRequest
{
    [NotBlank(ErrorMessage = "Refresh token is required")]
    public string RefreshToken { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [NotBlank(ErrorMessage = "Current password is required")]
    public string CurrentPassword { get; set; } = string.Empty;

    [NotBlank(ErrorMessage = "New password is required")]
    [StringLength(255, MinimumLength = 6, ErrorMessage = "New password must be at least 6 characters")]
    public string NewPassword { get; set; } = string.Empty;

    public string? ConfirmPassword { get; set; }
}

public sealed class NotBlankAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is string text && !string.IsNullOrWhiteSpace(text);
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

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    long ExpiresIn,
    UserResponse User);

public sealed record ApiSuccessResponse<T>(
    bool Success,
    string Code,
    string Message,
    T? Data,
    DateTime Timestamp)
{
    public static ApiSuccessResponse<T> Create(T? data, string message) =>
        new(true, "SUCCESS", message, data, DateTime.Now);
}
