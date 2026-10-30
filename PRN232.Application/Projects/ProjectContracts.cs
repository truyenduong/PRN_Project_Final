using System.ComponentModel.DataAnnotations;
using PRN232.Domain.Entities;

namespace PRN232.Application.Projects;

public sealed class CreateProjectRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Owner ID is required")]
    public long? OwnerId { get; set; }
}

public sealed class UpdateProjectRequest
{
    [StringLength(255, ErrorMessage = "Name cannot exceed 255 characters")]
    public string? Name { get; set; }

    public string? Description { get; set; }

    public long? OwnerId { get; set; }
}

public sealed record ProjectResponse(
    long Id,
    string Name,
    string? Description,
    long OwnerId,
    string? OwnerName,
    string? CurrentUserRole,
    DateTime? CreatedAt,
    DateTime? UpdatedAt);

public sealed record ProjectPageResult(
    IReadOnlyList<ProjectResponse> Content,
    int Page,
    int Size,
    long TotalElements);

public sealed record ProjectForUserResult(ProjectEntity Project, string? MemberRoleName);
