namespace PRN232.Application.Common;

public enum RoleEnums
{
    Admin,
    User,
    ProjectManager,
    Developer,
    Tester,
    Viewer
}

public static class RoleEnumExtensions
{
    public static string RoleName(this RoleEnums role) => role switch
    {
        RoleEnums.Admin => "ADMIN",
        RoleEnums.User => "User",
        RoleEnums.ProjectManager => "PROJECT_MANAGER",
        RoleEnums.Developer => "DEVELOPER",
        RoleEnums.Tester => "TESTER",
        RoleEnums.Viewer => "VIEWER",
        _ => throw new ArgumentOutOfRangeException(nameof(role))
    };

    public static bool Matches(this RoleEnums role, string? name) =>
        name is not null
        && (string.Equals(role.RoleName(), name, StringComparison.OrdinalIgnoreCase)
            || string.Equals(ToJavaConstant(role), name, StringComparison.OrdinalIgnoreCase));

    public static RoleEnums? FromString(string? name)
    {
        foreach (var role in Enum.GetValues<RoleEnums>())
        {
            if (role.Matches(name))
            {
                return role;
            }
        }

        return null;
    }

    private static string ToJavaConstant(RoleEnums role) =>
        role == RoleEnums.ProjectManager ? "PROJECT_MANAGER" : role.ToString().ToUpperInvariant();
}
