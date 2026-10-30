using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Common;
using PRN232.Application.Subtasks;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class SubtaskRepository(AppDbContext dbContext) : ISubtaskRepository
{
    private static readonly IReadOnlyDictionary<string, LambdaExpression> SearchFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["taskId"] = (Expression<Func<SubtaskEntity, long>>)(item => item.TaskId),
            ["assigneeId"] = (Expression<Func<SubtaskEntity, long?>>)(item => item.AssigneeId),
            ["title"] = (Expression<Func<SubtaskEntity, string>>)(item => item.Title),
            ["priority"] = (Expression<Func<SubtaskEntity, string?>>)(item => item.Priority),
            ["isCompleted"] = (Expression<Func<SubtaskEntity, bool?>>)(item => item.IsCompleted)
        };

    private static readonly IReadOnlyDictionary<string, LambdaExpression> SortFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["id"] = (Expression<Func<SubtaskEntity, long>>)(item => item.Id),
            ["task.id"] = (Expression<Func<SubtaskEntity, long>>)(item => item.TaskId),
            ["title"] = (Expression<Func<SubtaskEntity, string>>)(item => item.Title),
            ["description"] = (Expression<Func<SubtaskEntity, string?>>)(item => item.Description),
            ["isCompleted"] = (Expression<Func<SubtaskEntity, bool?>>)(item => item.IsCompleted),
            ["assignee.id"] = (Expression<Func<SubtaskEntity, long?>>)(item => item.AssigneeId),
            ["assignee.name"] = (Expression<Func<SubtaskEntity, string?>>)(item => item.Assignee!.Name),
            ["estimate"] = (Expression<Func<SubtaskEntity, string?>>)(item => item.Estimate),
            ["priority"] = (Expression<Func<SubtaskEntity, string?>>)(item => item.Priority),
            ["dueDate"] = (Expression<Func<SubtaskEntity, DateOnly?>>)(item => item.DueDate),
            ["createdAt"] = (Expression<Func<SubtaskEntity, DateTime?>>)(item => item.CreatedAt),
            ["updatedAt"] = (Expression<Func<SubtaskEntity, DateTime?>>)(item => item.UpdatedAt)
        };

    public async Task<(IReadOnlyList<SubtaskEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken)
    {
        var query = LoadSubtask(RsqlFilter<SubtaskEntity>.Apply(
            dbContext.Subtasks.IgnoreQueryFilters().AsNoTracking(), search, SearchFields));
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

    public async Task<IReadOnlyList<SubtaskEntity>> GetByTaskIdAsync(
        long taskId, CancellationToken cancellationToken) =>
        await LoadSubtask(dbContext.Subtasks.IgnoreQueryFilters().AsNoTracking())
            .Where(item => item.TaskId == taskId)
            .ToListAsync(cancellationToken);

    public Task<SubtaskEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        LoadSubtask(dbContext.Subtasks.IgnoreQueryFilters().AsNoTracking())
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<SubtaskEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
        LoadSubtask(dbContext.Subtasks.IgnoreQueryFilters())
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Subtasks.IgnoreQueryFilters().AnyAsync(item => item.Id == id, cancellationToken);

    public async Task<SubtaskEntity> SaveAsync(SubtaskEntity subtask, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(subtask).State == EntityState.Detached)
        {
            if (subtask.Id == 0)
            {
                subtask.Task = null!;
                subtask.Assignee = null;
                dbContext.Subtasks.Add(subtask);
            }
            else
            {
                dbContext.Subtasks.Attach(subtask);
                dbContext.Entry(subtask).State = EntityState.Modified;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await FindByIdAsync(subtask.Id, cancellationToken)
            ?? throw new InvalidOperationException("Saved subtask could not be loaded.");
    }

    public async Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
    {
        var subtask = await dbContext.Subtasks.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (subtask is not null)
        {
            dbContext.Subtasks.Remove(subtask);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static IQueryable<SubtaskEntity> LoadSubtask(IQueryable<SubtaskEntity> query) =>
        query.Include(item => item.Assignee)
            .Include(item => item.Task)
            .ThenInclude(task => task.Project)
            .ThenInclude(project => project.Owner)
            .Include(item => item.Task)
            .ThenInclude(task => task.Assignee);
}
