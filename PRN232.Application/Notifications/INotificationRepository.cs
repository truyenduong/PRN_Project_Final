using PRN232.Application.Common;
using PRN232.Domain.Entities;

namespace PRN232.Application.Notifications;

public interface INotificationRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    Task<(IReadOnlyList<NotificationEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken);

    Task<NotificationEntity?> FindByIdAsync(long id, CancellationToken cancellationToken);

    Task<NotificationEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken);

    Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken);

    Task<NotificationEntity> SaveAsync(NotificationEntity notification, CancellationToken cancellationToken);

    Task DeleteByIdAsync(long id, CancellationToken cancellationToken);
}
