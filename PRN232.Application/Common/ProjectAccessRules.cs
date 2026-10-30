namespace PRN232.Application.Common;

public static class ProjectAccessRules
{
    public static bool IsAdmin(IEnumerable<string> roles, string expectedLiteral) =>
        RoleMatcher.HasRole(roles, expectedLiteral);

    public static bool IsOwner(long? ownerId, long userId) => ownerId == userId;

    public static bool IsPmMember(string? memberRoleName) =>
        RoleEnums.ProjectManager.Matches(memberRoleName);

    public static bool IsTaskAssignee(long? assigneeId, long userId) => assigneeId == userId;

    public static bool IsSubtaskAssignee(long? assigneeId, long userId) => assigneeId == userId;
}

public readonly record struct ProjectMemberKey(long ProjectId, long UserId);
