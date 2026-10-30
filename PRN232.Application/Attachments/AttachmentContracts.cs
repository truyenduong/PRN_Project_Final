using System.ComponentModel.DataAnnotations;
using PRN232.Application.Security;

namespace PRN232.Application.Attachments;

public sealed class CreateAttachmentRequest
{
    [Required(ErrorMessage = "Task ID is required")]
    public long? TaskId { get; set; }

    [NotBlank(ErrorMessage = "File URL is required")]
    public string FileUrl { get; set; } = string.Empty;

    [NotBlank(ErrorMessage = "File name is required")]
    [StringLength(255, ErrorMessage = "File name cannot exceed 255 characters")]
    public string FileName { get; set; } = string.Empty;
}

public sealed class UpdateAttachmentRequest
{
    public string? FileUrl { get; set; }

    [StringLength(255, ErrorMessage = "File name cannot exceed 255 characters")]
    public string? FileName { get; set; }
}

public sealed record AttachmentResponse(
    long Id,
    long TaskId,
    string FileUrl,
    string FileName,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record AttachmentPageResult(
    IReadOnlyList<AttachmentResponse> Content,
    int Page,
    int Size,
    long TotalElements);
