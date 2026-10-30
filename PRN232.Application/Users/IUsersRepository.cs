using PRN232.Domain.Entities;
using PRN232.Application.Common;

namespace PRN232.Application.Users;

public interface IUsersRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);
    Task<(IReadOnlyList<UserEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken);
    Task<UserEntity?> FindByIdAsync(long id, CancellationToken cancellationToken);
    Task<UserEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken);
    Task<UserEntity?> FindByEmailAsync(string email, CancellationToken cancellationToken);
    Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken);
    Task<RoleEntity?> FindRoleByNameAsync(string name, CancellationToken cancellationToken);
    Task<UserEntity> SaveAsync(UserEntity user, CancellationToken cancellationToken);
    Task DeleteByIdAsync(long id, CancellationToken cancellationToken);
}
