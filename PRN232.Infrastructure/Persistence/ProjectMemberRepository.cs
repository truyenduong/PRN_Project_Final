using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using PRN232.Application.Common;
using PRN232.Application.ProjectMembers;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class ProjectMemberRepository(AppDbContext dbContext) : IProjectMemberRepository
{
    private static readonly IReadOnlyDictionary<string, LambdaExpression> SearchFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["projectId"] = (Expression<Func<ProjectMemberEntity, long>>)(member => member.ProjectId),
            ["userId"] = (Expression<Func<ProjectMemberEntity, long>>)(member => member.UserId),
            ["roleId"] = (Expression<Func<ProjectMemberEntity, long?>>)(member => member.RoleId),
            ["roleName"] = (Expression<Func<ProjectMemberEntity, string?>>)(member => member.Role!.Name)
        };

    private static readonly IReadOnlyDictionary<string, LambdaExpression> SortFields =
        new Dictionary<string, LambdaExpression>(StringComparer.Ordinal)
        {
            ["id.projectId"] = (Expression<Func<ProjectMemberEntity, long>>)(member => member.ProjectId),
            ["id.userId"] = (Expression<Func<ProjectMemberEntity, long>>)(member => member.UserId),
            ["createdAt"] = (Expression<Func<ProjectMemberEntity, DateTime?>>)(member => member.CreatedAt),
            ["updatedAt"] = (Expression<Func<ProjectMemberEntity, DateTime?>>)(member => member.UpdatedAt),
            ["project.name"] = (Expression<Func<ProjectMemberEntity, string?>>)(member => member.Project.Name),
            ["user.name"] = (Expression<Func<ProjectMemberEntity, string?>>)(member => member.User.Name),
            ["role.name"] = (Expression<Func<ProjectMemberEntity, string?>>)(member => member.Role!.Name)
        };

    public async Task<(IReadOnlyList<ProjectMemberEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken)
    {
        var query = RsqlFilter<ProjectMemberEntity>.Apply(
                dbContext.ProjectMembers.AsNoTracking(), search, SearchFields)
            .Include(member => member.Project)
            .Include(member => member.User)
            .Include(member => member.Role);
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

    public Task<ProjectMemberEntity?> FindByIdAsync(
        long projectId, long userId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AsNoTracking()
            .Include(member => member.Project)
            .Include(member => member.User)
            .Include(member => member.Role)
            .SingleOrDefaultAsync(
                member => member.ProjectId == projectId && member.UserId == userId, cancellationToken);

    public Task<ProjectMemberEntity?> FindByIdForUpdateAsync(
        long projectId, long userId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers
            .Include(member => member.Project)
            .Include(member => member.User)
            .Include(member => member.Role)
            .SingleOrDefaultAsync(
                member => member.ProjectId == projectId && member.UserId == userId, cancellationToken);

    public Task<bool> ExistsByIdAsync(long projectId, long userId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.AnyAsync(
            member => member.ProjectId == projectId && member.UserId == userId, cancellationToken);

    public async Task<ProjectMemberEntity> SaveAsync(
        ProjectMemberEntity member, CancellationToken cancellationToken)
    {
        if (dbContext.Entry(member).State == EntityState.Detached)
        {
            var existing = await dbContext.ProjectMembers.SingleOrDefaultAsync(
                item => item.ProjectId == member.ProjectId && item.UserId == member.UserId, cancellationToken);
            if (existing is null)
            {
                member.Project = null!;
                member.User = null!;
                member.Role = null;
                dbContext.ProjectMembers.Add(member);
            }
            else
            {
                existing.RoleId = member.RoleId;
                member = existing;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await FindByIdAsync(member.ProjectId, member.UserId, cancellationToken)
            ?? throw new InvalidOperationException("Saved project member could not be loaded.");
    }

    public async Task DeleteByIdAsync(long projectId, long userId, CancellationToken cancellationToken)
    {
        var member = await dbContext.ProjectMembers.SingleOrDefaultAsync(
            item => item.ProjectId == projectId && item.UserId == userId, cancellationToken);
        if (member is not null)
        {
            dbContext.ProjectMembers.Remove(member);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public Task<long> CountProjectsByUserIdAsync(long userId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.LongCountAsync(member => member.UserId == userId, cancellationToken);

    public Task<long> CountMembersByProjectIdAsync(long projectId, CancellationToken cancellationToken) =>
        dbContext.ProjectMembers.LongCountAsync(member => member.ProjectId == projectId, cancellationToken);
}
