using System.ComponentModel.DataAnnotations;

namespace PRN232.Application.ActivityLogs;

public sealed class CreateActivityLogRequest
{
    [Required(ErrorMessage = "User ID is required")]
    public long? UserId { get; set; }

    public long? ProjectId { get; set; }

    [Required(ErrorMessage = "Action is required")]
    [StringLength(255, ErrorMessage = "Action cannot exceed 255 characters")]
    public string Action { get; set; } = string.Empty;
}

public sealed class UpdateActivityLogRequest
{
    [StringLength(255, ErrorMessage = "Action cannot exceed 255 characters")]
    public string? Action { get; set; }
}

public sealed record ActivityLogResponse(
    long Id,
    long UserId,
    string? UserName,
    long? ProjectId,
    string? ProjectName,
    string Action,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record ActivityLogPageResult(
    IReadOnlyList<ActivityLogResponse> Content,
    int Page,
    int Size,
    long TotalElements);
