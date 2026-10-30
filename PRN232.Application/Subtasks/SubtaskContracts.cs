using System.ComponentModel.DataAnnotations;
using PRN232.Application.Security;

namespace PRN232.Application.Subtasks;

public sealed class CreateSubtaskRequest
{
    [Required(ErrorMessage = "Task ID is required")]
    public long? TaskId { get; set; }

    [NotBlank(ErrorMessage = "Title is required")]
    [StringLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
    public string Title { get; set; } = string.Empty;

    [StringLength(400, ErrorMessage = "Description cannot exceed 400 characters")]
    public string? Description { get; set; }

    public bool? IsCompleted { get; set; }

    public long? AssigneeId { get; set; }

    [StringLength(50, ErrorMessage = "Estimate cannot exceed 50 characters")]
    public string? Estimate { get; set; }

    [StringLength(50, ErrorMessage = "Priority cannot exceed 50 characters")]
    public string? Priority { get; set; }

    public DateOnly? DueDate { get; set; }
}

public sealed class UpdateSubtaskRequest
{
    [StringLength(255, ErrorMessage = "Title cannot exceed 255 characters")]
    public string? Title { get; set; }

    [StringLength(400, ErrorMessage = "Description cannot exceed 400 characters")]
    public string? Description { get; set; }

    public bool? IsCompleted { get; set; }

    public long? AssigneeId { get; set; }

    [StringLength(50, ErrorMessage = "Estimate cannot exceed 50 characters")]
    public string? Estimate { get; set; }

    [StringLength(50, ErrorMessage = "Priority cannot exceed 50 characters")]
    public string? Priority { get; set; }

    public DateOnly? DueDate { get; set; }
}

public sealed record SubtaskResponse(
    long Id,
    long TaskId,
    string Title,
    string? Description,
    bool? IsCompleted,
    long? AssigneeId,
    string? AssigneeName,
    string? Estimate,
    string? Priority,
    DateOnly? DueDate,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record SubtaskPageResult(
    IReadOnlyList<SubtaskResponse> Content,
    int Page,
    int Size,
    long TotalElements);
