using System.ComponentModel.DataAnnotations;

namespace PRN232.Application.Roles;

public sealed record CreateRoleRequest(
    [property: Required(ErrorMessage = "Role name is required")]
    [property: StringLength(100, ErrorMessage = "Role name cannot exceed 100 characters")]
    string Name);

public sealed record UpdateRoleRequest(
    [property: StringLength(100, ErrorMessage = "Role name cannot exceed 100 characters")]
    string? Name);

public sealed record RoleResponse(long Id, string Name, DateTime? CreatedAt, DateTime? UpdatedAt);

public sealed record RoleSort(string Property, bool Descending);

public sealed record RolePageResult(
    IReadOnlyList<RoleResponse> Content,
    int Page,
    int Size,
    long TotalElements);
