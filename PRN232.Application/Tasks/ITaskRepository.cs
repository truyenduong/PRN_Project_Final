using PRN232.Application.Common;
using PRN232.Domain.Entities;

namespace PRN232.Application.Tasks;

public interface ITaskRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    Task<(IReadOnlyList<TaskEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken);

    Task<TaskEntity?> FindByIdAsync(long id, CancellationToken cancellationToken);

    Task<TaskEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken);

    Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken);

    Task<TaskEntity> SaveAsync(TaskEntity task, CancellationToken cancellationToken);

    Task DeleteByIdAsync(long id, CancellationToken cancellationToken);

    Task<int> DeleteAllTrashTasksByProjectAndUserAsync(
        long projectId, long userId, CancellationToken cancellationToken);

    Task<int> DeleteAllTrashTasksByProjectAsync(long projectId, CancellationToken cancellationToken);
}
