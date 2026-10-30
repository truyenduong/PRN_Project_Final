namespace PRN232.Domain.Entities;

public sealed class CommentEntity : BaseEntity
{
    public long Id { get; set; }

    public long TaskId { get; set; }

    public TaskEntity Task { get; set; } = null!;

    public long UserId { get; set; }

    public UserEntity User { get; set; } = null!;

    public string Content { get; set; } = string.Empty;
}
