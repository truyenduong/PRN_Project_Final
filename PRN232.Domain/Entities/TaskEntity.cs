namespace PRN232.Domain.Entities;

public sealed class TaskEntity : BaseEntity
{
    public long Id { get; set; }

    public long ProjectId { get; set; }

    public ProjectEntity Project { get; set; } = null!;

    public long? SprintId { get; set; }

    public SprintEntity? Sprint { get; set; }

    public long? AssigneeId { get; set; }

    public UserEntity? Assignee { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Priority { get; set; }

    public string? Status { get; set; } = "pending";

    public DateOnly? DueDate { get; set; }

    public string? TaskCode { get; set; }

    public bool? IsDeleted { get; set; } = false;

    public string? Estimate { get; set; }

    public long? DeletedById { get; set; }

    public UserEntity? DeletedBy { get; set; }
}
