using PRN232.Domain.Exceptions;

namespace PRN232.Application.Dashboard;

public sealed class DashboardService(IDashboardRepository repository)
{
    public Task<UserDashboardResponse> GetUserDashboardAsync(
        long userId, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var data = await repository.GetUserDashboardDataAsync(
                userId, DateOnly.FromDateTime(DateTime.Today), token);
            return new UserDashboardResponse(
                userId, data.TotalProjects, data.TotalAssignedTasks, data.OverdueTasks, data.TasksByStatus);
        }, cancellationToken);

    public Task<ProjectDashboardResponse> GetProjectDashboardAsync(
        long projectId, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var project = await repository.FindProjectForDashboardAsync(projectId, token)
                ?? throw new NotFoundException($"Project not found with id: {projectId}");
            var data = await repository.GetProjectDashboardDataAsync(
                projectId, DateOnly.FromDateTime(DateTime.Today), token);
            var completionRate = data.TotalTasks > 0
                ? Math.Floor(data.CompletedTasks / (double)data.TotalTasks * 10_000 + 0.5) / 100
                : 0;

            return new ProjectDashboardResponse(
                projectId, project.Name, project.Description, project.Owner?.Name,
                data.TotalMembers, data.TotalTasks, data.CompletedTasks, completionRate,
                data.OverdueTasks, data.ActiveSprintName, data.TasksByStatus);
        }, cancellationToken);
}
