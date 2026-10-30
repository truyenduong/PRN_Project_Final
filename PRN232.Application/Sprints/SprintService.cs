using System.Security.Claims;
using PRN232.Application.Common;
using PRN232.Application.ProjectMembers;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Application.Sprints;

public sealed class SprintService(
    ISprintRepository repository,
    IProjectMemberRepository projectMemberRepository,
    ICurrentUserAccessor currentUserAccessor)
{
    private const string EntityName = "Sprint";

    public Task<SprintPageResult> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort,
        CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var result = await repository.GetPageAsync(search, page, size, sort, token);
            return new SprintPageResult(
                result.Content.Select(ToResponse).ToArray(), page, size, result.TotalElements);
        }, cancellationToken);

    public Task<SprintResponse> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
            ToResponse(await FindOrThrowAsync(id, token)), cancellationToken);

    public Task<SprintResponse> CreateAsync(
        CreateSprintRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var saved = await repository.SaveAsync(new SprintEntity
            {
                ProjectId = request.ProjectId!.Value,
                Name = request.Name,
                Goal = request.Goal,
                StartDate = request.StartDate!.Value,
                EndDate = request.EndDate!.Value,
                Status = request.Status
            }, token);
            return ToResponse(saved) with { ProjectName = null };
        }, cancellationToken);

    public Task<SprintResponse> UpdateAsync(
        long id, UpdateSprintRequest request, ClaimsPrincipal principal,
        CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var sprint = await repository.FindByIdForUpdateAsync(id, token)
                ?? throw NotFound(id);

            var currentUserId = currentUserAccessor.GetCurrentUser(principal).Id;
            var membership = await projectMemberRepository.FindByIdAsync(
                sprint.ProjectId, currentUserId, token);
            var isProjectManager = membership?.Role is not null
                && RoleEnums.ProjectManager.Matches(membership.Role.Name);
            var isProjectOwner = sprint.Project.OwnerId == currentUserId;
            if (!isProjectManager && !isProjectOwner)
            {
                throw new ForbiddenException("Chỉ Project Manager của dự án mới có quyền cập nhật Sprint");
            }

            sprint.ProjectId = request.ProjectId ?? sprint.ProjectId;
            sprint.Name = request.Name ?? sprint.Name;
            sprint.Goal = request.Goal ?? sprint.Goal;
            sprint.StartDate = request.StartDate ?? sprint.StartDate;
            sprint.EndDate = request.EndDate ?? sprint.EndDate;
            sprint.Status = request.Status ?? sprint.Status;
            var response = ToResponse(await repository.SaveAsync(sprint, token));
            return request.ProjectId is null ? response : response with { ProjectName = null };
        }, cancellationToken);

    public Task DeleteAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            if (!await repository.ExistsByIdAsync(id, token))
            {
                throw NotFound(id);
            }

            await repository.DeleteByIdAsync(id, token);
            return true;
        }, cancellationToken);

    private async Task<SprintEntity> FindOrThrowAsync(long id, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(id, cancellationToken) ?? throw NotFound(id);

    private static NotFoundException NotFound(long id) =>
        new($"{EntityName} not found with id: {id}");

    private static SprintResponse ToResponse(SprintEntity sprint) =>
        new(sprint.Id, sprint.ProjectId, sprint.Project?.Name, sprint.Name, sprint.Goal,
            sprint.StartDate, sprint.EndDate, sprint.Status, sprint.CreatedAt, sprint.UpdatedAt);
}
