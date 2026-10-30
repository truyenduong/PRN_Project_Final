using System.Security.Claims;
using PRN232.Application.Common;
using PRN232.Application.Projects;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Application.ProjectMembers;

public sealed class ProjectMemberService(
    IProjectMemberRepository repository,
    IProjectRepository projectRepository,
    ICurrentUserAccessor currentUserAccessor)
{
    public Task<ProjectMemberPageResult> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var result = await repository.GetPageAsync(search, page, size, sort, token);
            return new ProjectMemberPageResult(
                result.Content.Select(ToResponse).ToArray(), page, size, result.TotalElements);
        }, cancellationToken);

    public Task<ProjectMemberResponse> GetByIdAsync(
        long projectId, long userId, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
            ToResponse(await FindOrThrowAsync(projectId, userId, token)), cancellationToken);

    public Task<ProjectMemberResponse> CreateAsync(
        CreateProjectMemberRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var member = await repository.SaveAsync(new ProjectMemberEntity
            {
                ProjectId = request.ProjectId!.Value,
                UserId = request.UserId!.Value,
                RoleId = request.RoleId
            }, token);
            return ToResponse(member);
        }, cancellationToken);

    public Task<ProjectMemberResponse> UpdateAsync(
        long projectId, long userId, UpdateProjectMemberRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var member = await repository.FindByIdForUpdateAsync(projectId, userId, token)
                ?? throw NotFound(projectId, userId);
            if (request.RoleId is not null) member.RoleId = request.RoleId;
            return ToResponse(await repository.SaveAsync(member, token));
        }, cancellationToken);

    public Task DeleteAsync(
        long projectId, long userId, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            if (!await repository.ExistsByIdAsync(projectId, userId, token))
            {
                throw NotFound(projectId, userId);
            }

            var currentUserId = currentUserAccessor.GetCurrentUser(principal).Id;
            if (currentUserId.Equals(userId))
            {
                throw new BadRequestException("You cannot remove yourself from the project.");
            }

            var project = await projectRepository.FindByIdAsync(projectId, token)
                ?? throw new NotFoundException($"Project not found with id={projectId}");
            if (project.OwnerId == userId)
            {
                throw new BadRequestException("Unable to remove Owner from project");
            }

            var isAdmin = HasExactAdminAuthority(principal);
            var isProjectManager = isAdmin
                || ProjectAccessRules.IsOwner(project.OwnerId, currentUserId)
                || await IsProjectManagerMemberAsync(projectId, currentUserId, token);
            if (!isProjectManager)
            {
                throw new ForbiddenException("Only the project manager has the authority to remove members.");
            }

            await repository.DeleteByIdAsync(projectId, userId, token);
            return true;
        }, cancellationToken);

    private async Task<bool> IsProjectManagerMemberAsync(
        long projectId, long userId, CancellationToken cancellationToken)
    {
        var callerMembership = await repository.FindByIdAsync(projectId, userId, cancellationToken);
        return callerMembership?.Role is not null
            && RoleEnums.ProjectManager.Matches(callerMembership.Role.Name);
    }

    private async Task<ProjectMemberEntity> FindOrThrowAsync(
        long projectId, long userId, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(projectId, userId, cancellationToken)
        ?? throw NotFound(projectId, userId);

    private static NotFoundException NotFound(long projectId, long userId) =>
        new($"ProjectMember not found for projectId={projectId}, userId={userId}");

    private static bool HasExactAdminAuthority(ClaimsPrincipal principal) =>
        principal.Claims.Any(claim =>
            (claim.Type == "roles" || claim.Type == ClaimTypes.Role)
            && claim.Value == "ADMIN");

    private static ProjectMemberResponse ToResponse(ProjectMemberEntity member) =>
        new(member.ProjectId, member.Project?.Name, member.UserId, member.User?.Name,
            member.RoleId, member.Role?.Name, member.CreatedAt, member.UpdatedAt)
        {
            UserEmail = member.User?.Email
        };
}
