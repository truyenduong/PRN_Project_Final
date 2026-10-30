using Microsoft.EntityFrameworkCore;
using PRN232.Application.Dashboard;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class DashboardRepository(AppDbContext dbContext) : IDashboardRepository
{
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<UserDashboardData> GetUserDashboardDataAsync(
        long userId, DateOnly today, CancellationToken cancellationToken)
    {
        var totalProjects = await dbContext.ProjectMembers.LongCountAsync(
            member => member.UserId == userId, cancellationToken);
        var tasks = dbContext.Tasks.Where(task => task.AssigneeId == userId && task.IsDeleted == false);
        var totalTasks = await tasks.LongCountAsync(cancellationToken);
        var overdueTasks = await tasks.LongCountAsync(
            task => task.DueDate < today && task.Status != "completed", cancellationToken);
        var tasksByStatus = await GetTaskStatusCountsAsync(tasks, cancellationToken);

        return new UserDashboardData(totalProjects, totalTasks, overdueTasks, tasksByStatus);
    }

    public Task<ProjectEntity?> FindProjectForDashboardAsync(
        long projectId, CancellationToken cancellationToken) =>
        dbContext.Projects.AsNoTracking()
            .Include(project => project.Owner)
            .SingleOrDefaultAsync(project => project.Id == projectId, cancellationToken);

    public async Task<ProjectDashboardData> GetProjectDashboardDataAsync(
        long projectId, DateOnly today, CancellationToken cancellationToken)
    {
        var totalMembers = await dbContext.ProjectMembers.LongCountAsync(
            member => member.ProjectId == projectId, cancellationToken);
        var tasks = dbContext.Tasks.Where(task => task.ProjectId == projectId && task.IsDeleted == false);
        var totalTasks = await tasks.LongCountAsync(cancellationToken);
        var completedTasks = await tasks.LongCountAsync(
            task => task.Status != null && EF.Functions.ILike(task.Status, "completed"),
            cancellationToken);
        var overdueTasks = await tasks.LongCountAsync(
            task => task.DueDate < today && task.Status != null
                && !EF.Functions.ILike(task.Status, "completed"),
            cancellationToken);
        var activeSprintName = await dbContext.Sprints.AsNoTracking()
            .Where(sprint => sprint.ProjectId == projectId
                && EF.Functions.ILike(sprint.Status, "ACTIVE"))
            .Select(sprint => sprint.Name)
            .FirstOrDefaultAsync(cancellationToken);
        var tasksByStatus = await GetTaskStatusCountsAsync(tasks, cancellationToken);

        return new ProjectDashboardData(
            totalMembers, totalTasks, completedTasks, overdueTasks, activeSprintName, tasksByStatus);
    }

    private static async Task<IReadOnlyDictionary<string, long>> GetTaskStatusCountsAsync(
        IQueryable<TaskEntity> tasks, CancellationToken cancellationToken)
    {
        var groups = await tasks.GroupBy(task => task.Status)
            .Select(group => new DashboardTaskStatusCount(group.Key, group.LongCount()))
            .ToListAsync(cancellationToken);
        return groups.ToDictionary(group => group.Status!, group => group.Count);
    }
}
