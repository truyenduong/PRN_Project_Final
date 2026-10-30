using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Attachments;
using PRN232.Application.Common;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class AttachmentRepository(AppDbContext dbContext) : IAttachmentRepository
{
    private static readonly IReadOnlyDictionary<string, LambdaExpression> SearchFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["taskId"] = (Expression<Func<AttachmentEntity, long>>)(item => item.TaskId),
            ["fileName"] = (Expression<Func<AttachmentEntity, string>>)(item => item.FileName)
        };

    private static readonly IReadOnlyDictionary<string, LambdaExpression> SortFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["id"] = (Expression<Func<AttachmentEntity, long>>)(item => item.Id),
            ["task.id"] = (Expression<Func<AttachmentEntity, long>>)(item => item.TaskId),
            ["fileUrl"] = (Expression<Func<AttachmentEntity, string>>)(item => item.FileUrl),
            ["fileName"] = (Expression<Func<AttachmentEntity, string>>)(item => item.FileName),
            ["createdAt"] = (Expression<Func<AttachmentEntity, DateTime?>>)(item => item.CreatedAt),
            ["updatedAt"] = (Expression<Func<AttachmentEntity, DateTime?>>)(item => item.UpdatedAt)
        };

    public async Task<(IReadOnlyList<AttachmentEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken)
    {
        var query = RsqlFilter<AttachmentEntity>.Apply(dbContext.Attachments.AsNoTracking(), search, SearchFields);
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

    public Task<AttachmentEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Attachments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<AttachmentEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Attachments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Attachments.AnyAsync(item => item.Id == id, cancellationToken);

    public async Task<AttachmentEntity> SaveAsync(
        AttachmentEntity attachment, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(attachment).State == EntityState.Detached)
        {
            if (attachment.Id == 0)
            {
                attachment.Task = null!;
                dbContext.Attachments.Add(attachment);
            }
            else
            {
                dbContext.Attachments.Attach(attachment);
                dbContext.Entry(attachment).State = EntityState.Modified;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await FindByIdAsync(attachment.Id, cancellationToken)
            ?? throw new InvalidOperationException("Saved attachment could not be loaded.");
    }

    public async Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
    {
        var attachment = await dbContext.Attachments.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (attachment is not null)
        {
            dbContext.Attachments.Remove(attachment);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
