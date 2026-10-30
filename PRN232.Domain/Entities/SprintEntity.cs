namespace PRN232.Domain.Entities;

public sealed class SprintEntity : BaseEntity
{
    public long Id { get; set; }

    public long ProjectId { get; set; }

    public ProjectEntity Project { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string? Goal { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string Status { get; set; } = string.Empty;
}
