using PRN232.Application.Common;
using PRN232.Domain.Entities;

namespace PRN232.Application.Projects;

public interface IProjectRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
    Task<(IReadOnlyList<ProjectEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken);
    Task<(IReadOnlyList<ProjectForUserResult> Content, long TotalElements)> GetMyPageAsync(
        long userId, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken);
    Task<ProjectEntity?> FindByIdAsync(long id, CancellationToken cancellationToken);
    Task<ProjectEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken);
    Task<long> CountByOwnerIdAsync(long ownerId, CancellationToken cancellationToken);
    Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken);
    Task<ProjectEntity> SaveAsync(ProjectEntity project, CancellationToken cancellationToken);
    Task DeleteByIdAsync(long id, CancellationToken cancellationToken);
}

public interface IProjectOwnerMembershipWriter
{
    Task AddOwnerAsProjectManagerAsync(
        long projectId, long ownerId, long roleId, CancellationToken cancellationToken);
}
