namespace PRN232.Domain.Entities;

public sealed class AttachmentEntity : BaseEntity
{
    public long Id { get; set; }

    public long TaskId { get; set; }

    public TaskEntity Task { get; set; } = null!;

    public string FileUrl { get; set; } = string.Empty;

    public string FileName { get; set; } = string.Empty;
}
