using System.ComponentModel.DataAnnotations;

namespace PRN232.Application.ProjectMembers;

public sealed class CreateProjectMemberRequest
{
    [Required(ErrorMessage = "Project ID is required")]
    public long? ProjectId { get; set; }

    [Required(ErrorMessage = "User ID is required")]
    public long? UserId { get; set; }

    public long? RoleId { get; set; }
}

public sealed class UpdateProjectMemberRequest
{
    public long? RoleId { get; set; }
}

public sealed record ProjectMemberResponse(
    long ProjectId,
    string? ProjectName,
    long UserId,
    string? UserName,
    long? RoleId,
    string? RoleName,
    DateTime? CreatedAt,
    DateTime? UpdatedAt)
{
    public string? UserEmail { get; init; }
}

public sealed record ProjectMemberPageResult(
    IReadOnlyList<ProjectMemberResponse> Content,
    int Page,
    int Size,
    long TotalElements);
