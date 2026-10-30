using System.ComponentModel.DataAnnotations;
using PRN232.Application.Security;

namespace PRN232.Application.Tasks;

public sealed class CreateTaskRequest
{
    [Required(ErrorMessage = "Project ID must not be empty")]
    public long? ProjectId { get; set; }

    public long? SprintId { get; set; }

    public long? AssigneeId { get; set; }

    [NotBlank(ErrorMessage = "Title must not be empty")]
    [StringLength(255, ErrorMessage = "Title has invalid length or format")]
    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    [StringLength(50, ErrorMessage = "Estimate has invalid length or format")]
    public string? Estimate { get; set; }

    [StringLength(50, ErrorMessage = "Priority has invalid length or format")]
    public string? Priority { get; set; }

    [StringLength(50, ErrorMessage = "Status has invalid length or format")]
    public string? Status { get; set; }

    public DateOnly? DueDate { get; set; }

    [StringLength(50, ErrorMessage = "Task code has invalid length or format")]
    public string? TaskCode { get; set; }
}

public sealed class UpdateTaskRequest
{
    public long? ProjectId { get; set; }

    public long? SprintId { get; set; }

    public long? AssigneeId { get; set; }

    [StringLength(255, ErrorMessage = "Title has invalid length or format")]
    public string? Title { get; set; }

    public string? Description { get; set; }

    [StringLength(50, ErrorMessage = "Estimate has invalid length or format")]
    public string? Estimate { get; set; }

    [StringLength(50, ErrorMessage = "Priority has invalid length or format")]
    public string? Priority { get; set; }

    [StringLength(50, ErrorMessage = "Status has invalid length or format")]
    public string? Status { get; set; }

    public DateOnly? DueDate { get; set; }

    [StringLength(50, ErrorMessage = "Task code has invalid length or format")]
    public string? TaskCode { get; set; }

    public bool? IsDeleted { get; set; }
}

public sealed record TaskResponse(
    long Id,
    long ProjectId,
    string? ProjectName,
    long? SprintId,
    string? SprintName,
    long? AssigneeId,
    string? AssigneeName,
    string Title,
    string? Description,
    string? Estimate,
    string? Priority,
    string? Status,
    DateOnly? DueDate,
    string? TaskCode,
    bool? IsDeleted,
    long? DeletedById,
    string? DeletedByName,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record TaskPageResult(
    IReadOnlyList<TaskResponse> Content,
    int Page,
    int Size,
    long TotalElements);
