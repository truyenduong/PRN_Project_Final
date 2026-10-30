using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Common;
using PRN232.Application.Tasks;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class TaskRepository(AppDbContext dbContext) : ITaskRepository
{
    private static readonly IReadOnlyDictionary<string, LambdaExpression> SearchFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["projectId"] = (Expression<Func<TaskEntity, long>>)(item => item.ProjectId),
            ["assigneeId"] = (Expression<Func<TaskEntity, long?>>)(item => item.AssigneeId),
            ["sprintId"] = (Expression<Func<TaskEntity, long?>>)(item => item.SprintId),
            ["deletedById"] = (Expression<Func<TaskEntity, long?>>)(item => item.DeletedById),
            ["taskCode"] = (Expression<Func<TaskEntity, string?>>)(item => item.TaskCode),
            ["status"] = (Expression<Func<TaskEntity, string?>>)(item => item.Status),
            ["priority"] = (Expression<Func<TaskEntity, string?>>)(item => item.Priority),
            ["isDeleted"] = (Expression<Func<TaskEntity, bool?>>)(item => item.IsDeleted)
        };

    private static readonly IReadOnlyDictionary<string, LambdaExpression> SortFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["id"] = (Expression<Func<TaskEntity, long>>)(item => item.Id),
            ["project.id"] = (Expression<Func<TaskEntity, long>>)(item => item.ProjectId),
            ["project.name"] = (Expression<Func<TaskEntity, string>>)(item => item.Project.Name),
            ["sprint.id"] = (Expression<Func<TaskEntity, long?>>)(item => item.SprintId),
            ["sprint.name"] = (Expression<Func<TaskEntity, string?>>)(item => item.Sprint!.Name),
            ["assignee.id"] = (Expression<Func<TaskEntity, long?>>)(item => item.AssigneeId),
            ["assignee.name"] = (Expression<Func<TaskEntity, string?>>)(item => item.Assignee!.Name),
            ["deletedBy.id"] = (Expression<Func<TaskEntity, long?>>)(item => item.DeletedById),
            ["deletedBy.name"] = (Expression<Func<TaskEntity, string?>>)(item => item.DeletedBy!.Name),
            ["title"] = (Expression<Func<TaskEntity, string>>)(item => item.Title),
            ["description"] = (Expression<Func<TaskEntity, string?>>)(item => item.Description),
            ["estimate"] = (Expression<Func<TaskEntity, string?>>)(item => item.Estimate),
            ["priority"] = (Expression<Func<TaskEntity, string?>>)(item => item.Priority),
            ["status"] = (Expression<Func<TaskEntity, string?>>)(item => item.Status),
            ["dueDate"] = (Expression<Func<TaskEntity, DateOnly?>>)(item => item.DueDate),
            ["taskCode"] = (Expression<Func<TaskEntity, string?>>)(item => item.TaskCode),
            ["isDeleted"] = (Expression<Func<TaskEntity, bool?>>)(item => item.IsDeleted),
            ["createdAt"] = (Expression<Func<TaskEntity, DateTime?>>)(item => item.CreatedAt),
            ["updatedAt"] = (Expression<Func<TaskEntity, DateTime?>>)(item => item.UpdatedAt)
        };

    public async Task<(IReadOnlyList<TaskEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken)
    {
        var query = RsqlFilter<TaskEntity>.Apply(
                dbContext.Tasks.IgnoreQueryFilters().AsNoTracking(), search, SearchFields)
            .Include(item => item.Project)
            .ThenInclude(project => project.Owner)
            .Include(item => item.Sprint)
            .Include(item => item.Assignee)
            .Include(item => item.DeletedBy);
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

    public Task<TaskEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        LoadTask(dbContext.Tasks.IgnoreQueryFilters().AsNoTracking())
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<TaskEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
        LoadTask(dbContext.Tasks.IgnoreQueryFilters())
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Tasks.IgnoreQueryFilters().AnyAsync(item => item.Id == id, cancellationToken);

    public async Task<TaskEntity> SaveAsync(TaskEntity task, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(task).State == EntityState.Detached)
        {
            if (task.Id == 0)
            {
                task.Project = null!;
                task.Sprint = null;
                task.Assignee = null;
                task.DeletedBy = null;
                dbContext.Tasks.Add(task);
            }
            else
            {
                dbContext.Tasks.Attach(task);
                dbContext.Entry(task).State = EntityState.Modified;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await FindByIdAsync(task.Id, cancellationToken)
            ?? throw new InvalidOperationException("Saved task could not be loaded.");
    }

    public async Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
    {
        var task = await dbContext.Tasks.IgnoreQueryFilters()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (task is not null)
        {
            dbContext.Tasks.Remove(task);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<int> DeleteAllTrashTasksByProjectAndUserAsync(
        long projectId, long userId, CancellationToken cancellationToken) =>
        dbContext.Tasks.IgnoreQueryFilters()
            .Where(item => item.ProjectId == projectId
                && item.IsDeleted == true
                && item.DeletedById == userId)
            .ExecuteDeleteAsync(cancellationToken);

    public Task<int> DeleteAllTrashTasksByProjectAsync(
        long projectId, CancellationToken cancellationToken) =>
        dbContext.Tasks.IgnoreQueryFilters()
            .Where(item => item.ProjectId == projectId && item.IsDeleted == true)
            .ExecuteDeleteAsync(cancellationToken);

    private static IQueryable<TaskEntity> LoadTask(IQueryable<TaskEntity> query) =>
        query.Include(item => item.Project)
            .ThenInclude(project => project.Owner)
            .Include(item => item.Sprint)
            .Include(item => item.Assignee)
            .Include(item => item.DeletedBy);
}
