using PRN232.Application.Common;
using PRN232.Domain.Entities;

namespace PRN232.Application.ActivityLogs;

public interface IActivityLogRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    Task<(IReadOnlyList<ActivityLogEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken);

    Task<ActivityLogEntity?> FindByIdAsync(long id, CancellationToken cancellationToken);

    Task<ActivityLogEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken);

    Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken);

    Task<ActivityLogEntity> SaveAsync(ActivityLogEntity activityLog, CancellationToken cancellationToken);

    Task DeleteByIdAsync(long id, CancellationToken cancellationToken);
}
