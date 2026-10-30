using PRN232.Domain.Entities;

namespace PRN232.Application.Dashboard;

public interface IDashboardRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    Task<UserDashboardData> GetUserDashboardDataAsync(
        long userId, DateOnly today, CancellationToken cancellationToken);

    Task<ProjectEntity?> FindProjectForDashboardAsync(
        long projectId, CancellationToken cancellationToken);

    Task<ProjectDashboardData> GetProjectDashboardDataAsync(
        long projectId, DateOnly today, CancellationToken cancellationToken);
}
