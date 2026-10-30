using System.ComponentModel.DataAnnotations;
using PRN232.Application.Security;

namespace PRN232.Application.Comments;

public sealed class CreateCommentRequest
{
    [Required(ErrorMessage = "Task ID is required")]
    public long? TaskId { get; set; }

    [Required(ErrorMessage = "User ID is required")]
    public long? UserId { get; set; }

    [NotBlank(ErrorMessage = "Content is required")]
    public string Content { get; set; } = string.Empty;
}

public sealed class UpdateCommentRequest
{
    public string? Content { get; set; }
}

public sealed record CommentResponse(
    long Id,
    long TaskId,
    long UserId,
    string? UserName,
    string Content,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record CommentPageResult(
    IReadOnlyList<CommentResponse> Content,
    int Page,
    int Size,
    long TotalElements);
