using PRN232.Application.Common;
using PRN232.Domain.Entities;

namespace PRN232.Application.Sprints;

public interface ISprintRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    Task<(IReadOnlyList<SprintEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken);

    Task<SprintEntity?> FindByIdAsync(long id, CancellationToken cancellationToken);

    Task<SprintEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken);

    Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken);

    Task<SprintEntity> SaveAsync(SprintEntity sprint, CancellationToken cancellationToken);

    Task DeleteByIdAsync(long id, CancellationToken cancellationToken);
}
