using System.Security.Claims;
using PRN232.Application.Common;
using PRN232.Application.ProjectMembers;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Application.Subtasks;

public sealed class SubtaskService(
    ISubtaskRepository repository,
    IProjectMemberRepository projectMemberRepository,
    IAssignmentNotifier assignmentNotifier)
{
    private const string EntityName = "Subtask";
    private const string InvalidTaskStatusAuthentication = "Please log in to perform this action.";
    private const string InvalidTaskStatusAuthority =
        "Only the Project Manager or the person assigned the task has the authority to change the task status or assign";

    public Task<SubtaskPageResult> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort,
        CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var result = await repository.GetPageAsync(search, page, size, sort, token);
            return new SubtaskPageResult(
                result.Content.Select(ToResponse).ToArray(), page, size, result.TotalElements);
        }, cancellationToken);

    public Task<IReadOnlyList<SubtaskResponse>> GetByTaskIdAsync(
        long taskId, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
            (IReadOnlyList<SubtaskResponse>)(await repository.GetByTaskIdAsync(taskId, token))
                .Select(ToResponse).ToArray(), cancellationToken);

    public Task<SubtaskResponse> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
            ToResponse(await FindOrThrowAsync(id, token)), cancellationToken);

    public async Task<SubtaskResponse> CreateAsync(
        CreateSubtaskRequest request, CancellationToken cancellationToken)
    {
        var saved = await repository.ExecuteInTransactionAsync(async token =>
            await repository.SaveAsync(new SubtaskEntity
            {
                TaskId = request.TaskId!.Value,
                Title = request.Title,
                Description = request.Description,
                IsCompleted = request.IsCompleted ?? false,
                AssigneeId = request.AssigneeId,
                Estimate = request.Estimate,
                Priority = request.Priority,
                DueDate = request.DueDate
            }, token), cancellationToken);

        if (saved.AssigneeId is not null && saved.AssigneeId != saved.Task.AssigneeId)
        {
            await assignmentNotifier.NotifySubtaskAssignmentAsync(saved.Id, cancellationToken);
        }

        return ToResponse(saved);
    }

    public async Task<SubtaskResponse> UpdateAsync(
        long id, UpdateSubtaskRequest request, ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var result = await repository.ExecuteInTransactionAsync(async token =>
        {
            var subtask = await repository.FindByIdForUpdateAsync(id, token)
                ?? throw NotFound(id);
            var oldAssigneeId = subtask.AssigneeId;
            await ValidateBeforeUpdateAsync(request, subtask, principal, token);

            subtask.Title = request.Title ?? subtask.Title;
            subtask.Description = request.Description ?? subtask.Description;
            subtask.IsCompleted = request.IsCompleted ?? subtask.IsCompleted;
            subtask.AssigneeId = request.AssigneeId ?? subtask.AssigneeId;
            subtask.Estimate = request.Estimate ?? subtask.Estimate;
            subtask.Priority = request.Priority ?? subtask.Priority;
            subtask.DueDate = request.DueDate ?? subtask.DueDate;

            return (Saved: await repository.SaveAsync(subtask, token), OldAssigneeId: oldAssigneeId);
        }, cancellationToken);

        if (result.Saved.AssigneeId is long assigneeId
            && assigneeId != result.OldAssigneeId
            && assigneeId != result.Saved.Task.AssigneeId)
        {
            await assignmentNotifier.NotifySubtaskAssignmentAsync(result.Saved.Id, cancellationToken);
        }

        return ToResponse(result.Saved);
    }

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

    private async Task ValidateBeforeUpdateAsync(
        UpdateSubtaskRequest request, SubtaskEntity subtask, ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var completionChanged = request.IsCompleted is not null
            && request.IsCompleted != subtask.IsCompleted;

        // Java dereferences the existing assignee here; preserve its null-assignee failure.
        var assigneeChanged = request.AssigneeId is not null
            && request.AssigneeId != subtask.Assignee!.Id;

        if (completionChanged || assigneeChanged)
        {
            await ValidateUpdateAuthorityAsync(subtask, principal, cancellationToken);
        }
    }

    private async Task ValidateUpdateAuthorityAsync(
        SubtaskEntity subtask, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedException(InvalidTaskStatusAuthentication);
        }

        var callerId = TryGetCurrentUserId(principal);
        var roles = principal.Claims
            .Where(claim => claim.Type == "roles" || claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value);
        var isAdmin = roles.Any(role => role is "ROLE_ADMIN" or "ADMIN");
        var task = subtask.Task;
        var isProjectManager = isAdmin;

        if (!isProjectManager && callerId is long userId)
        {
            isProjectManager = task.Project.OwnerId == userId
                || await projectMemberRepository.FindByIdAsync(
                    task.ProjectId, userId, cancellationToken)
                    is { Role: not null } membership
                    && RoleEnums.ProjectManager.Matches(membership.Role.Name);
        }

        var isTaskAssignee = callerId is not null && task.AssigneeId == callerId;
        var isSubtaskAssignee = callerId is not null && subtask.AssigneeId == callerId;
        if (!isProjectManager && !isTaskAssignee && !isSubtaskAssignee)
        {
            throw new ForbiddenException(InvalidTaskStatusAuthority);
        }
    }

    private async Task<SubtaskEntity> FindOrThrowAsync(long id, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(id, cancellationToken) ?? throw NotFound(id);

    private static long? TryGetCurrentUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst("userId")?.Value ?? principal.FindFirst("sub")?.Value;
        return long.TryParse(value, out var id) ? id : null;
    }

    private static NotFoundException NotFound(long id) =>
        new($"{EntityName} not found with id: {id}");

    private static SubtaskResponse ToResponse(SubtaskEntity subtask) =>
        new(subtask.Id, subtask.TaskId, subtask.Title, subtask.Description, subtask.IsCompleted,
            subtask.AssigneeId, subtask.Assignee?.Name, subtask.Estimate, subtask.Priority,
            subtask.DueDate, subtask.CreatedAt, subtask.UpdatedAt);
}
