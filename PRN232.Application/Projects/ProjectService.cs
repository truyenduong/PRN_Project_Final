using System.Security.Claims;
using PRN232.Application.Common;
using PRN232.Application.Roles;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Application.Projects;

public sealed class ProjectService(
    IProjectRepository repository,
    IProjectOwnerMembershipWriter ownerMembershipWriter,
    IRoleRepository roleRepository,
    ICurrentUserAccessor currentUserAccessor)
{
    public Task<ProjectPageResult> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var result = await repository.GetPageAsync(search, page, size, sort, token);
            return new ProjectPageResult(result.Content.Select(ToResponse).ToArray(), page, size, result.TotalElements);
        }, cancellationToken);

    public Task<ProjectResponse> GetProjectByIdAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
            ToResponse(await FindOrThrowAsync(id, token)), cancellationToken);

    public Task<ProjectResponse> CreateProjectAsync(
        CreateProjectRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            if (await repository.CountByOwnerIdAsync(request.OwnerId!.Value, token) > 5)
            {
                throw new BadRequestException("The owner has created more than five projects");
            }

            var role = await roleRepository.FindByNameAsync(RoleEnums.ProjectManager.RoleName(), token)
                ?? throw new NotFoundException("PROJECT_MANAGER not found with id: null");

            var project = await repository.SaveAsync(new ProjectEntity
            {
                Name = request.Name,
                Description = request.Description,
                OwnerId = request.OwnerId.Value
            }, token);

            await ownerMembershipWriter.AddOwnerAsProjectManagerAsync(
                project.Id, project.OwnerId, role.Id, token);

            return ToResponse(project);
        }, cancellationToken);

    public Task<ProjectResponse> UpdateProjectAsync(
        long id, UpdateProjectRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var existing = await repository.FindByIdForUpdateAsync(id, token) ?? throw NotFound(id);
            if (request.Name is not null) existing.Name = request.Name;
            if (request.Description is not null) existing.Description = request.Description;
            if (request.OwnerId is not null)
            {
                existing.OwnerId = request.OwnerId.Value;
            }
            return ToResponse(await repository.SaveAsync(existing, token));
        }, cancellationToken);

    public Task DeleteProjectAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            if (!await repository.ExistsByIdAsync(id, token))
            {
                throw NotFound(id);
            }

            await repository.DeleteByIdAsync(id, token);
            return true;
        }, cancellationToken);

    public async Task<ProjectPageResult> GetMyProjectAsync(
        ClaimsPrincipal principal, int page, int size, IReadOnlyList<PageSort> sort,
        CancellationToken cancellationToken)
    {
        var userId = currentUserAccessor.GetCurrentUser(principal).Id;
        var result = await repository.GetMyPageAsync(userId, page, size, sort, cancellationToken);
        var content = result.Content.Select(item =>
        {
            var response = ToResponse(item.Project);
            var roleName = string.IsNullOrEmpty(item.MemberRoleName)
                && item.Project.OwnerId == userId
                ? RoleEnums.ProjectManager.RoleName()
                : item.MemberRoleName;
            return response with { CurrentUserRole = roleName };
        }).ToArray();
        return new ProjectPageResult(content, page, size, result.TotalElements);
    }

    private async Task<ProjectEntity> FindOrThrowAsync(long id, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(id, cancellationToken) ?? throw NotFound(id);

    private static NotFoundException NotFound(long id) => new($"Project not found with id: {id}");

    private static ProjectResponse ToResponse(ProjectEntity project) =>
        new(project.Id, project.Name, project.Description, project.OwnerId, project.Owner?.Name,
            null, project.CreatedAt, project.UpdatedAt);
}
