using PRN232.Domain.Entities;

namespace PRN232.Application.Security;

public interface IAuthRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken);

    Task<UserEntity?> FindUserByEmailAsync(string email, CancellationToken cancellationToken);

    Task<RoleEntity?> FindDefaultRoleAsync(CancellationToken cancellationToken);

    Task<RoleEntity?> FindRoleByIdAsync(long id, CancellationToken cancellationToken);

    Task AddUserAsync(UserEntity user, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
