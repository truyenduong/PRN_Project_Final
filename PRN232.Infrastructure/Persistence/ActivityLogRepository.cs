using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.ActivityLogs;
using PRN232.Application.Common;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class ActivityLogRepository(AppDbContext dbContext) : IActivityLogRepository
{
    private static readonly IReadOnlyDictionary<string, LambdaExpression> SearchFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["userId"] = (Expression<Func<ActivityLogEntity, long>>)(item => item.UserId),
            ["projectId"] = (Expression<Func<ActivityLogEntity, long?>>)(item => item.ProjectId),
            ["action"] = (Expression<Func<ActivityLogEntity, string>>)(item => item.Action)
        };

    private static readonly IReadOnlyDictionary<string, LambdaExpression> SortFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["id"] = (Expression<Func<ActivityLogEntity, long>>)(item => item.Id),
            ["user.id"] = (Expression<Func<ActivityLogEntity, long>>)(item => item.UserId),
            ["user.name"] = (Expression<Func<ActivityLogEntity, string>>)(item => item.User.Name),
            ["project.id"] = (Expression<Func<ActivityLogEntity, long?>>)(item => item.ProjectId),
            ["project.name"] = (Expression<Func<ActivityLogEntity, string?>>)(item => item.Project!.Name),
            ["action"] = (Expression<Func<ActivityLogEntity, string>>)(item => item.Action),
            ["createdAt"] = (Expression<Func<ActivityLogEntity, DateTime?>>)(item => item.CreatedAt),
            ["updatedAt"] = (Expression<Func<ActivityLogEntity, DateTime?>>)(item => item.UpdatedAt)
        };

    public async Task<(IReadOnlyList<ActivityLogEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken)
    {
        var query = RsqlFilter<ActivityLogEntity>.Apply(
                dbContext.ActivityLogs.AsNoTracking(), search, SearchFields)
            .Include(item => item.User)
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

    public Task<ActivityLogEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.ActivityLogs.AsNoTracking()
            .Include(item => item.User)
            .Include(item => item.Project)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<ActivityLogEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
        dbContext.ActivityLogs
            .Include(item => item.User)
            .Include(item => item.Project)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.ActivityLogs.AnyAsync(item => item.Id == id, cancellationToken);

    public async Task<ActivityLogEntity> SaveAsync(
        ActivityLogEntity activityLog, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(activityLog).State == EntityState.Detached)
        {
            if (activityLog.Id == 0)
            {
                activityLog.User = null!;
                activityLog.Project = null;
                dbContext.ActivityLogs.Add(activityLog);
            }
            else
            {
                dbContext.ActivityLogs.Update(activityLog);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await FindByIdAsync(activityLog.Id, cancellationToken)
            ?? throw new InvalidOperationException("Saved activity log could not be loaded.");
    }

    public async Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
    {
        var activityLog = await dbContext.ActivityLogs.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (activityLog is not null)
        {
            dbContext.ActivityLogs.Remove(activityLog);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
