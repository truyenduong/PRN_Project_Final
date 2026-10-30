using PRN232.Application.Common;
using PRN232.Domain.Entities;

namespace PRN232.Application.Subtasks;

public interface ISubtaskRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    Task<(IReadOnlyList<SubtaskEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken);

    Task<IReadOnlyList<SubtaskEntity>> GetByTaskIdAsync(long taskId, CancellationToken cancellationToken);

    Task<SubtaskEntity?> FindByIdAsync(long id, CancellationToken cancellationToken);

    Task<SubtaskEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken);

    Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken);

    Task<SubtaskEntity> SaveAsync(SubtaskEntity subtask, CancellationToken cancellationToken);

    Task DeleteByIdAsync(long id, CancellationToken cancellationToken);
}
