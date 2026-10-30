using System.ComponentModel.DataAnnotations;

namespace PRN232.Application.Notifications;

public sealed class CreateNotificationRequest
{
    [Required(ErrorMessage = "User ID is required")]
    public long? UserId { get; set; }

    [Required(ErrorMessage = "Type is required")]
    [StringLength(100, ErrorMessage = "Type cannot exceed 100 characters")]
    public string Type { get; set; } = string.Empty;

    [Required(ErrorMessage = "Content is required")]
    public string Content { get; set; } = string.Empty;

    public bool? IsRead { get; set; } = false;
}

public sealed class UpdateNotificationRequest
{
    public string? Content { get; set; }

    public bool? IsRead { get; set; }
}

public sealed record NotificationResponse(
    long Id,
    long UserId,
    string? UserName,
    string Type,
    string Content,
    bool? IsRead,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record NotificationPageResult(
    IReadOnlyList<NotificationResponse> Content,
    int Page,
    int Size,
    long TotalElements);
