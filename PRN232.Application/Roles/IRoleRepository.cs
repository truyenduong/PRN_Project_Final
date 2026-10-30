using PRN232.Domain.Entities;

namespace PRN232.Application.Roles;

public interface IRoleRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
    Task<(IReadOnlyList<RoleEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<RoleSort> sort, CancellationToken cancellationToken);
    Task<RoleEntity?> FindByIdAsync(long id, CancellationToken cancellationToken);
    Task<RoleEntity?> FindByNameAsync(string name, CancellationToken cancellationToken);
    Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken);
    Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken);
    Task<RoleEntity> SaveAsync(RoleEntity role, CancellationToken cancellationToken);
    Task DeleteByIdAsync(long id, CancellationToken cancellationToken);
}
