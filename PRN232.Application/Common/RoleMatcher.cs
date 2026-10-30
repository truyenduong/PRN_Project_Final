namespace PRN232.Application.Common;

public static class RoleMatcher
{
    public static bool HasRole(IEnumerable<string> actualRoles, string expectedLiteral) =>
        actualRoles.Contains(expectedLiteral, StringComparer.OrdinalIgnoreCase);

    public static bool HasAny(IEnumerable<string> actualRoles, params string[] acceptedRoles)
    {
        var actual = actualRoles.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return acceptedRoles.Any(actual.Contains);
    }
}
