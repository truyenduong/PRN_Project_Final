namespace PRN232.Application.Dashboard;

public sealed record UserDashboardResponse(
    long UserId,
    long TotalProjects,
    long TotalAssignedTasks,
    long OverdueTasks,
    IReadOnlyDictionary<string, long> TasksByStatus);

public sealed record ProjectDashboardResponse(
    long ProjectId,
    string ProjectName,
    string? ProjectDescription,
    string? OwnerName,
    long TotalMembers,
    long TotalTasks,
    long CompletedTasks,
    double CompletionRate,
    long OverdueTasks,
    string? ActiveSprintName,
    IReadOnlyDictionary<string, long> TasksByStatus);

public sealed record UserDashboardData(
    long TotalProjects,
    long TotalAssignedTasks,
    long OverdueTasks,
    IReadOnlyDictionary<string, long> TasksByStatus);

public sealed record ProjectDashboardData(
    long TotalMembers,
    long TotalTasks,
    long CompletedTasks,
    long OverdueTasks,
    string? ActiveSprintName,
    IReadOnlyDictionary<string, long> TasksByStatus);

public sealed record DashboardTaskStatusCount(string? Status, long Count);
