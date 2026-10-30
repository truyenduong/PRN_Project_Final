using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Common;
using PRN232.Application.Notifications;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class NotificationRepository(AppDbContext dbContext) : INotificationRepository
{
    private static readonly IReadOnlyDictionary<string, LambdaExpression> SearchFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["userId"] = (Expression<Func<NotificationEntity, long>>)(item => item.UserId),
            ["type"] = (Expression<Func<NotificationEntity, string>>)(item => item.Type),
            ["isRead"] = (Expression<Func<NotificationEntity, bool?>>)(item => item.IsRead)
        };

    private static readonly IReadOnlyDictionary<string, LambdaExpression> SortFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["id"] = (Expression<Func<NotificationEntity, long>>)(item => item.Id),
            ["user.id"] = (Expression<Func<NotificationEntity, long>>)(item => item.UserId),
            ["user.name"] = (Expression<Func<NotificationEntity, string>>)(item => item.User.Name),
            ["type"] = (Expression<Func<NotificationEntity, string>>)(item => item.Type),
            ["content"] = (Expression<Func<NotificationEntity, string>>)(item => item.Content),
            ["isRead"] = (Expression<Func<NotificationEntity, bool?>>)(item => item.IsRead),
            ["createdAt"] = (Expression<Func<NotificationEntity, DateTime?>>)(item => item.CreatedAt),
            ["updatedAt"] = (Expression<Func<NotificationEntity, DateTime?>>)(item => item.UpdatedAt)
        };

    public async Task<(IReadOnlyList<NotificationEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken)
    {
        var query = RsqlFilter<NotificationEntity>.Apply(
                dbContext.Notifications.AsNoTracking(), search, SearchFields)
            .Include(item => item.User);
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

    public Task<NotificationEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Notifications.AsNoTracking()
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<NotificationEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Notifications
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Notifications.AnyAsync(item => item.Id == id, cancellationToken);

    public async Task<NotificationEntity> SaveAsync(
        NotificationEntity notification, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(notification).State == EntityState.Detached)
        {
            if (notification.Id == 0)
            {
                notification.User = null!;
                dbContext.Notifications.Add(notification);
            }
            else
            {
                dbContext.Notifications.Update(notification);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await FindByIdAsync(notification.Id, cancellationToken)
            ?? throw new InvalidOperationException("Saved notification could not be loaded.");
    }

    public async Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
    {
        var notification = await dbContext.Notifications.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (notification is not null)
        {
            dbContext.Notifications.Remove(notification);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
