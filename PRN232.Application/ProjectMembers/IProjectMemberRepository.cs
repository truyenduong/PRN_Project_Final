using PRN232.Application.Common;
using PRN232.Domain.Entities;

namespace PRN232.Application.ProjectMembers;

public interface IProjectMemberRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
    Task<(IReadOnlyList<ProjectMemberEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken);
    Task<ProjectMemberEntity?> FindByIdAsync(
        long projectId, long userId, CancellationToken cancellationToken);
    Task<ProjectMemberEntity?> FindByIdForUpdateAsync(
        long projectId, long userId, CancellationToken cancellationToken);
    Task<bool> ExistsByIdAsync(long projectId, long userId, CancellationToken cancellationToken);
    Task<ProjectMemberEntity> SaveAsync(ProjectMemberEntity member, CancellationToken cancellationToken);
    Task DeleteByIdAsync(long projectId, long userId, CancellationToken cancellationToken);
    Task<long> CountProjectsByUserIdAsync(long userId, CancellationToken cancellationToken);
    Task<long> CountMembersByProjectIdAsync(long projectId, CancellationToken cancellationToken);
}
