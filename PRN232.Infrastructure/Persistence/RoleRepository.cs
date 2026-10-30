using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using PRN232.Application.Roles;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class RoleRepository(AppDbContext dbContext) : IRoleRepository
{
    private static readonly IReadOnlyDictionary<string, LambdaExpression> SearchFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["name"] = (Expression<Func<RoleEntity, string>>)(role => role.Name)
        };

    public async Task<(IReadOnlyList<RoleEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<RoleSort> sort, CancellationToken cancellationToken)
    {
        if (page < 0 || size < 1)
        {
            throw new ArgumentOutOfRangeException(page < 0 ? nameof(page) : nameof(size));
        }

        var query = RsqlFilter<RoleEntity>.Apply(dbContext.Roles.AsNoTracking(), search, SearchFields);
        var total = await query.LongCountAsync(cancellationToken);
        foreach (var (sortItem, index) in sort.Select((item, index) => (item, index)))
        {
            var propertyName = sortItem.Property.Length == 0
                ? sortItem.Property
                : char.ToUpperInvariant(sortItem.Property[0]) + sortItem.Property[1..];
            var property = typeof(RoleEntity).GetProperty(propertyName)
                ?? throw new ArgumentException($"Unsupported sort field: {sortItem.Property}");
            var parameter = Expression.Parameter(typeof(RoleEntity), "role");
            var selector = Expression.Lambda(Expression.Property(parameter, property), parameter);
            var method = index == 0
                ? sortItem.Descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy)
                : sortItem.Descending ? nameof(Queryable.ThenByDescending) : nameof(Queryable.ThenBy);
            var expression = Expression.Call(
                typeof(Queryable),
                method,
                [typeof(RoleEntity), property.PropertyType],
                query.Expression,
                Expression.Quote(selector));
            query = query.Provider.CreateQuery<RoleEntity>(expression);
        }

        var content = await query.Skip(page * size).Take(size).ToListAsync(cancellationToken);
        return (content, total);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task<RoleEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Roles.AsNoTracking().SingleOrDefaultAsync(role => role.Id == id, cancellationToken);

    public Task<RoleEntity?> FindByNameAsync(string name, CancellationToken cancellationToken) =>
        dbContext.Roles.AsNoTracking().SingleOrDefaultAsync(role => role.Name == name, cancellationToken);

    public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Roles.AnyAsync(role => role.Id == id, cancellationToken);

    public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken) =>
        dbContext.Roles.AnyAsync(role => role.Name == name, cancellationToken);

    public async Task<RoleEntity> SaveAsync(RoleEntity role, CancellationToken cancellationToken)
    {
        if (role.Id == 0)
        {
            dbContext.Roles.Add(role);
        }
        else
        {
            dbContext.Roles.Update(role);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return role;
    }

    public async Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.FindAsync([id], cancellationToken);
        if (role is not null)
        {
            dbContext.Roles.Remove(role);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
