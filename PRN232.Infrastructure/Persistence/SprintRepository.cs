using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Common;
using PRN232.Application.Sprints;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class SprintRepository(AppDbContext dbContext) : ISprintRepository
{
    private static readonly IReadOnlyDictionary<string, LambdaExpression> SearchFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["projectId"] = (Expression<Func<SprintEntity, long>>)(item => item.ProjectId),
            ["name"] = (Expression<Func<SprintEntity, string>>)(item => item.Name),
            ["status"] = (Expression<Func<SprintEntity, string>>)(item => item.Status),
            ["startDate"] = (Expression<Func<SprintEntity, DateOnly>>)(item => item.StartDate),
            ["endDate"] = (Expression<Func<SprintEntity, DateOnly>>)(item => item.EndDate)
        };

    private static readonly IReadOnlyDictionary<string, LambdaExpression> SortFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["id"] = (Expression<Func<SprintEntity, long>>)(item => item.Id),
            ["project.id"] = (Expression<Func<SprintEntity, long>>)(item => item.ProjectId),
            ["project.name"] = (Expression<Func<SprintEntity, string>>)(item => item.Project.Name),
            ["name"] = (Expression<Func<SprintEntity, string>>)(item => item.Name),
            ["goal"] = (Expression<Func<SprintEntity, string?>>)(item => item.Goal),
            ["startDate"] = (Expression<Func<SprintEntity, DateOnly>>)(item => item.StartDate),
            ["endDate"] = (Expression<Func<SprintEntity, DateOnly>>)(item => item.EndDate),
            ["status"] = (Expression<Func<SprintEntity, string>>)(item => item.Status),
            ["createdAt"] = (Expression<Func<SprintEntity, DateTime?>>)(item => item.CreatedAt),
            ["updatedAt"] = (Expression<Func<SprintEntity, DateTime?>>)(item => item.UpdatedAt)
        };

    public async Task<(IReadOnlyList<SprintEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken)
    {
        var query = RsqlFilter<SprintEntity>.Apply(
                dbContext.Sprints.AsNoTracking(), search, SearchFields)
            .Include(item => item.Project);
        return await EfQueryOrdering.PageAsync(query, page, size, sort, SortFields, cancellationToken);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task<SprintEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Sprints.AsNoTracking()
            .Include(item => item.Project)
            .ThenInclude(project => project.Owner)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<SprintEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Sprints
            .Include(item => item.Project)
            .ThenInclude(project => project.Owner)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Sprints.AnyAsync(item => item.Id == id, cancellationToken);

    public async Task<SprintEntity> SaveAsync(SprintEntity sprint, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(sprint).State == EntityState.Detached)
        {
            if (sprint.Id == 0)
            {
                sprint.Project = null!;
                dbContext.Sprints.Add(sprint);
            }
            else
            {
                dbContext.Sprints.Update(sprint);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await FindByIdAsync(sprint.Id, cancellationToken)
            ?? throw new InvalidOperationException("Saved sprint could not be loaded.");
    }

    public async Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
    {
        var sprint = await dbContext.Sprints.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (sprint is not null)
        {
            dbContext.Sprints.Remove(sprint);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
