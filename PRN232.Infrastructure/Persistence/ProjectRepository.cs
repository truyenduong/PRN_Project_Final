using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Common;
using PRN232.Application.Projects;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Infrastructure.Persistence;

public sealed class ProjectRepository(AppDbContext dbContext) : IProjectRepository
{
    private static readonly IReadOnlyDictionary<string, LambdaExpression> SearchFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["id"] = (Expression<Func<ProjectEntity, long>>)(project => project.Id),
            ["name"] = (Expression<Func<ProjectEntity, string>>)(project => project.Name),
            ["description"] = (Expression<Func<ProjectEntity, string?>>)(project => project.Description),
            ["ownerId"] = (Expression<Func<ProjectEntity, long>>)(project => project.OwnerId),
            ["createdAt"] = (Expression<Func<ProjectEntity, DateTime?>>)(project => project.CreatedAt),
            ["updatedAt"] = (Expression<Func<ProjectEntity, DateTime?>>)(project => project.UpdatedAt)
        };

    private static readonly IReadOnlyDictionary<string, LambdaExpression> SortFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["id"] = (Expression<Func<ProjectEntity, long>>)(project => project.Id),
            ["name"] = (Expression<Func<ProjectEntity, string>>)(project => project.Name),
            ["description"] = (Expression<Func<ProjectEntity, string?>>)(project => project.Description),
            ["createdAt"] = (Expression<Func<ProjectEntity, DateTime?>>)(project => project.CreatedAt),
            ["updatedAt"] = (Expression<Func<ProjectEntity, DateTime?>>)(project => project.UpdatedAt),
            ["owner.name"] = (Expression<Func<ProjectEntity, string?>>)(project => project.Owner.Name)
        };

    public async Task<(IReadOnlyList<ProjectEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken)
    {
        var query = RsqlFilter<ProjectEntity>.Apply(dbContext.Projects.AsNoTracking(), search, SearchFields)
            .Include(project => project.Owner);
        return await EfQueryOrdering.PageAsync(query, page, size, sort, SortFields, cancellationToken);
    }

    public async Task<(IReadOnlyList<ProjectForUserResult> Content, long TotalElements)> GetMyPageAsync(
        long userId, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken)
    {
        ValidatePage(page, size);
        var query = dbContext.Projects.AsNoTracking()
            .Where(project => project.OwnerId == userId
                || dbContext.ProjectMembers.Any(member =>
                    member.ProjectId == project.Id && member.UserId == userId));
        var total = await query.LongCountAsync(cancellationToken);
        var ordered = EfQueryOrdering.Apply(query, sort, SortFields);
        var projects = await ordered.Include(project => project.Owner)
            .Skip(checked(page * size)).Take(size).ToListAsync(cancellationToken);

        var projectIds = projects.Select(project => project.Id).ToArray();
        var memberRoles = projectIds.Length == 0
            ? new Dictionary<long, string?>()
            : await dbContext.ProjectMembers.AsNoTracking()
                .Where(member => member.UserId == userId && projectIds.Contains(member.ProjectId))
                .Select(member => new
                {
                    member.ProjectId,
                    RoleName = member.Role == null ? null : member.Role.Name
                })
                .ToDictionaryAsync(member => member.ProjectId, member => member.RoleName, cancellationToken);

        return (projects.Select(project => new ProjectForUserResult(
            project,
            memberRoles.GetValueOrDefault(project.Id))).ToArray(), total);
    }

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var result = await operation(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return result;
    }

    public Task<ProjectEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Projects.AsNoTracking().Include(project => project.Owner)
            .SingleOrDefaultAsync(project => project.Id == id, cancellationToken);

    public Task<ProjectEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Projects.Include(project => project.Owner)
            .SingleOrDefaultAsync(project => project.Id == id, cancellationToken);

    public Task<long> CountByOwnerIdAsync(long ownerId, CancellationToken cancellationToken) =>
        dbContext.Projects.LongCountAsync(project => project.OwnerId == ownerId, cancellationToken);

    public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
        dbContext.Projects.AnyAsync(project => project.Id == id, cancellationToken);

    public async Task<ProjectEntity> SaveAsync(ProjectEntity project, CancellationToken cancellationToken)
    {
        if (project.Id == 0)
        {
            project.Owner = null!;
            dbContext.Projects.Add(project);
        }
        else if (dbContext.Entry(project).State == EntityState.Detached)
        {
            project.Owner = null!;
            dbContext.Projects.Attach(project);
            dbContext.Entry(project).State = EntityState.Modified;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await dbContext.Projects.AsNoTracking().Include(item => item.Owner)
            .SingleAsync(item => item.Id == project.Id, cancellationToken);
    }

    public async Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
    {
        var project = await dbContext.Projects.FindAsync([id], cancellationToken);
        if (project is not null)
        {
            dbContext.Projects.Remove(project);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static void ValidatePage(int page, int size)
    {
        if (page < 0 || size < 1)
        {
            throw new BadRequestException("Page must be non-negative and size must be positive");
        }
    }
}
