using Microsoft.EntityFrameworkCore;
using PRN232.Application.Security;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class AuthRepository(AppDbContext dbContext) : IAuthRepository
{
    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task<UserEntity?> FindUserByEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Users
            .Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<RoleEntity?> FindDefaultRoleAsync(CancellationToken cancellationToken) =>
        dbContext.Roles.SingleOrDefaultAsync(role => role.Name == "User", cancellationToken);

    public Task<RoleEntity?> FindRoleByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Roles.SingleOrDefaultAsync(role => role.Id == id, cancellationToken);

    public Task AddUserAsync(UserEntity user, CancellationToken cancellationToken) =>
        dbContext.Users.AddAsync(user, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
