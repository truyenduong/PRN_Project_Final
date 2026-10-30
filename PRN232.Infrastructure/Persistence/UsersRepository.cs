using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Common;
using PRN232.Application.Users;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class UsersRepository(AppDbContext dbContext) : IUsersRepository
{
    private static readonly IReadOnlyDictionary<string, LambdaExpression> SearchFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["id"] = (Expression<Func<UserEntity, long>>)(user => user.Id),
            ["email"] = (Expression<Func<UserEntity, string>>)(user => user.Email),
            ["name"] = (Expression<Func<UserEntity, string>>)(user => user.Name),
            ["avatar"] = (Expression<Func<UserEntity, string?>>)(user => user.Avatar),
            ["status"] = (Expression<Func<UserEntity, string?>>)(user => user.Status),
            ["createdAt"] = (Expression<Func<UserEntity, DateTime?>>)(user => user.CreatedAt),
            ["updatedAt"] = (Expression<Func<UserEntity, DateTime?>>)(user => user.UpdatedAt)
        };

    private static readonly IReadOnlyDictionary<string, LambdaExpression> SortFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
        ["id"] = (Expression<Func<UserEntity, long>>)(user => user.Id),
        ["email"] = (Expression<Func<UserEntity, string>>)(user => user.Email),
        ["name"] = (Expression<Func<UserEntity, string>>)(user => user.Name),
        ["avatar"] = (Expression<Func<UserEntity, string?>>)(user => user.Avatar),
        ["status"] = (Expression<Func<UserEntity, string?>>)(user => user.Status),
        ["createdAt"] = (Expression<Func<UserEntity, DateTime?>>)(user => user.CreatedAt),
        ["updatedAt"] = (Expression<Func<UserEntity, DateTime?>>)(user => user.UpdatedAt),
        ["role.name"] = (Expression<Func<UserEntity, string?>>)(user => user.Role!.Name)
        };

    public async Task<(IReadOnlyList<UserEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken)
    {
        var query = RsqlFilter<UserEntity>.Apply(dbContext.Users.AsNoTracking(), search, SearchFields)
            .Include(user => user.Role);
        return await EfQueryOrdering.PageAsync(
            query, page, size, sort, SortFields, cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task<UserEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<UserEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Users.Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<UserEntity?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Users.AsNoTracking().Include(user => user.Role)
            .SingleOrDefaultAsync(user => user.Email == email, cancellationToken);

    public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.Id == id, cancellationToken);

    public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken);

    public Task<RoleEntity?> FindRoleByNameAsync(string name, CancellationToken cancellationToken) =>
        dbContext.Roles.AsNoTracking().SingleOrDefaultAsync(role => role.Name == name, cancellationToken);

    public async Task<UserEntity> SaveAsync(UserEntity user, CancellationToken cancellationToken)
    {
        if (user.Id == 0)
        {
            user.Role = null;
            dbContext.Users.Add(user);
        }
        else if (dbContext.Entry(user).State == EntityState.Detached)
        {
            dbContext.Users.Attach(user);
            dbContext.Entry(user).State = EntityState.Modified;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await dbContext.Users.AsNoTracking().Include(item => item.Role)
            .SingleAsync(item => item.Id == user.Id, cancellationToken);
    }

    public async Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.FindAsync([id], cancellationToken);
        if (user is not null)
        {
            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
