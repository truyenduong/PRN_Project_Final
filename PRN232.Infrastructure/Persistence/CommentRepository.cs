using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Comments;
using PRN232.Application.Common;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class CommentRepository(AppDbContext dbContext) : ICommentRepository
{
    private static readonly IReadOnlyDictionary<string, LambdaExpression> SearchFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["taskId"] = (Expression<Func<CommentEntity, long>>)(item => item.TaskId),
            ["userId"] = (Expression<Func<CommentEntity, long>>)(item => item.UserId)
        };

    private static readonly IReadOnlyDictionary<string, LambdaExpression> SortFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["id"] = (Expression<Func<CommentEntity, long>>)(item => item.Id),
            ["task.id"] = (Expression<Func<CommentEntity, long>>)(item => item.TaskId),
            ["user.id"] = (Expression<Func<CommentEntity, long>>)(item => item.UserId),
            ["user.name"] = (Expression<Func<CommentEntity, string?>>)(item => item.User.Name),
            ["content"] = (Expression<Func<CommentEntity, string>>)(item => item.Content),
            ["createdAt"] = (Expression<Func<CommentEntity, DateTime?>>)(item => item.CreatedAt),
            ["updatedAt"] = (Expression<Func<CommentEntity, DateTime?>>)(item => item.UpdatedAt)
        };

    public async Task<(IReadOnlyList<CommentEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken)
    {
        var query = RsqlFilter<CommentEntity>.Apply(dbContext.Comments.AsNoTracking(), search, SearchFields)
            .Include(item => item.Task)
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

    public Task<CommentEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        LoadComment(dbContext.Comments.AsNoTracking())
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<CommentEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
        LoadComment(dbContext.Comments)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Comments.AnyAsync(item => item.Id == id, cancellationToken);

    public async Task<CommentEntity> SaveAsync(CommentEntity comment, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(comment).State == EntityState.Detached)
        {
            if (comment.Id == 0)
            {
                comment.Task = null!;
                comment.User = null!;
                dbContext.Comments.Add(comment);
            }
            else
            {
                dbContext.Comments.Attach(comment);
                dbContext.Entry(comment).State = EntityState.Modified;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await FindByIdAsync(comment.Id, cancellationToken)
            ?? throw new InvalidOperationException("Saved comment could not be loaded.");
    }

    public async Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
    {
        var comment = await dbContext.Comments.SingleOrDefaultAsync(
            item => item.Id == id, cancellationToken);
        if (comment is not null)
        {
            dbContext.Comments.Remove(comment);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static IQueryable<CommentEntity> LoadComment(IQueryable<CommentEntity> query) =>
        query.Include(item => item.Task).Include(item => item.User);
}
