namespace PRN232.Domain.Entities;

public sealed class SubtaskEntity : BaseEntity
{
    public long Id { get; set; }

    public long TaskId { get; set; }

    public TaskEntity Task { get; set; } = null!;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool? IsCompleted { get; set; } = false;

    public long? AssigneeId { get; set; }

    public UserEntity? Assignee { get; set; }

    public string? Estimate { get; set; }

    public string? Priority { get; set; } = "MEDIUM";

    public DateOnly? DueDate { get; set; }
}
