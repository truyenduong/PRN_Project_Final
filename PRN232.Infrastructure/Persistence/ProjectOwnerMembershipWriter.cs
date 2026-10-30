using Microsoft.EntityFrameworkCore;
using PRN232.Application.Projects;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class ProjectOwnerMembershipWriter(AppDbContext dbContext) : IProjectOwnerMembershipWriter
{
    public async Task AddOwnerAsProjectManagerAsync(
        long projectId, long ownerId, long roleId, CancellationToken cancellationToken)
    {
        dbContext.ProjectMembers.Add(new ProjectMemberEntity
        {
            ProjectId = projectId,
            UserId = ownerId,
            RoleId = roleId
        });
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
