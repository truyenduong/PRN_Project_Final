using System.ComponentModel.DataAnnotations;

namespace PRN232.Application.Sprints;

public sealed class CreateSprintRequest
{
    [Required(ErrorMessage = "Project ID is required")]
    public long? ProjectId { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [StringLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string Name { get; set; } = string.Empty;

    public string? Goal { get; set; }

    [Required(ErrorMessage = "Start date is required")]
    public DateOnly? StartDate { get; set; }

    [Required(ErrorMessage = "End date is required")]
    public DateOnly? EndDate { get; set; }

    [Required(ErrorMessage = "Status is required")]
    [StringLength(30, ErrorMessage = "Status cannot exceed 30 characters")]
    public string Status { get; set; } = string.Empty;
}

public sealed class UpdateSprintRequest
{
    public long? ProjectId { get; set; }

    [StringLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string? Name { get; set; }

    public string? Goal { get; set; }

    public DateOnly? StartDate { get; set; }

    public DateOnly? EndDate { get; set; }

    [StringLength(30, ErrorMessage = "Status cannot exceed 30 characters")]
    public string? Status { get; set; }
}

public sealed record SprintResponse(
    long Id,
    long ProjectId,
    string? ProjectName,
    string Name,
    string? Goal,
    DateOnly StartDate,
    DateOnly EndDate,
    string Status,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record SprintPageResult(
    IReadOnlyList<SprintResponse> Content,
    int Page,
    int Size,
    long TotalElements);
