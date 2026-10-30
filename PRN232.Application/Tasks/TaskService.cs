using System.Security.Claims;
using PRN232.Application.Common;
using PRN232.Application.ProjectMembers;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Application.Tasks;

public sealed class TaskService(
    ITaskRepository repository,
    IProjectMemberRepository projectMemberRepository,
    ICurrentUserAccessor currentUserAccessor,
    IAssignmentNotifier assignmentNotifier)
{
    private const string EntityName = "Task";
    private const string InvalidTaskStatusAuthentication = "Please log in to perform this action.";
    private const string InvalidTaskStatusAuthority =
        "Only the Project Manager or the person assigned the task has the authority to change the task status or assign";
    private const string EmptyEntireProjectTrashAuthority =
        "Only the Project Manager has the authority to empty the entire project trash bin.";

    public Task<TaskPageResult> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort,
        CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var effectiveSearch = search;
            if (effectiveSearch is null || !effectiveSearch.Contains("isDeleted", StringComparison.Ordinal))
            {
                effectiveSearch = string.IsNullOrEmpty(effectiveSearch)
                    ? "isDeleted==false"
                    : $"{effectiveSearch};isDeleted==false";
            }

            var result = await repository.GetPageAsync(effectiveSearch, page, size, sort, token);
            return new TaskPageResult(result.Content.Select(ToResponse).ToArray(), page, size, result.TotalElements);
        }, cancellationToken);

    public Task<TaskResponse> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
            ToResponse(await FindOrThrowAsync(id, token)), cancellationToken);

    public async Task<TaskResponse> CreateAsync(
        CreateTaskRequest request, CancellationToken cancellationToken)
    {
        var saved = await repository.ExecuteInTransactionAsync(async token =>
        {
            var task = new TaskEntity
            {
                ProjectId = request.ProjectId!.Value,
                SprintId = request.SprintId,
                AssigneeId = request.AssigneeId,
                Title = request.Title,
                Description = request.Description,
                Estimate = request.Estimate,
                Priority = request.Priority,
                Status = request.Status,
                DueDate = request.DueDate,
                TaskCode = string.IsNullOrWhiteSpace(request.TaskCode)
                    ? $"TASK-{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() % 100000}"
                    : request.TaskCode,
                IsDeleted = false
            };
            return await repository.SaveAsync(task, token);
        }, cancellationToken);

        if (saved.AssigneeId is not null)
        {
            await assignmentNotifier.NotifyTaskAssignmentAsync(saved.Id, cancellationToken);
        }

        return ToResponse(saved);
    }

    public async Task<TaskResponse> UpdateAsync(
        long id, UpdateTaskRequest request, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var result = await repository.ExecuteInTransactionAsync(async token =>
        {
            var existing = await FindOrThrowForUpdateAsync(id, token);
            await ValidateBeforeUpdateAsync(request, existing, principal, token);
            var oldAssigneeId = existing.AssigneeId;

            existing.ProjectId = request.ProjectId ?? existing.ProjectId;
            existing.SprintId = request.SprintId ?? existing.SprintId;
            existing.AssigneeId = request.AssigneeId ?? existing.AssigneeId;
            existing.Title = request.Title ?? existing.Title;
            existing.Description = request.Description ?? existing.Description;
            existing.Estimate = request.Estimate ?? existing.Estimate;
            existing.Priority = request.Priority ?? existing.Priority;
            existing.Status = request.Status ?? existing.Status;
            existing.DueDate = request.DueDate ?? existing.DueDate;
            existing.TaskCode = request.TaskCode ?? existing.TaskCode;
            existing.IsDeleted = request.IsDeleted ?? existing.IsDeleted;

            var saved = await repository.SaveAsync(existing, token);
            return (Saved: saved, OldAssigneeId: oldAssigneeId);
        }, cancellationToken);

        if (result.Saved.AssigneeId is long assigneeId && assigneeId != result.OldAssigneeId)
        {
            await assignmentNotifier.NotifyTaskAssignmentAsync(result.Saved.Id, cancellationToken);
        }

        return ToResponse(result.Saved);
    }

    public Task DeleteAsync(long id, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var task = await FindOrThrowForUpdateAsync(id, token);
            var callerId = currentUserAccessor.GetCurrentUser(principal).Id;
            task.IsDeleted = true;
            // Java's ObjectUtils.isEmpty(non-null Long) is false, so its ternary stores null here.
            long? deletedById = callerId;
            task.DeletedById = deletedById is null ? deletedById : null;
            await repository.SaveAsync(task, token);
            return true;
        }, cancellationToken);

    public Task<TaskResponse> RestoreAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var task = await FindOrThrowForUpdateAsync(id, token);
            task.IsDeleted = false;
            task.DeletedById = null;
            return ToResponse(await repository.SaveAsync(task, token));
        }, cancellationToken);

    public Task PermanentDeleteAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            if (!await repository.ExistsByIdAsync(id, token))
            {
                throw NotFound(id);
            }

            await repository.DeleteByIdAsync(id, token);
            return true;
        }, cancellationToken);

    public Task EmptyTrashAsync(
        long projectId, bool all, ClaimsPrincipal principal, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var currentUserId = currentUserAccessor.GetCurrentUser(principal).Id;
            if (!all)
            {
                await repository.DeleteAllTrashTasksByProjectAndUserAsync(projectId, currentUserId, token);
                return true;
            }

            var isAdmin = HasExactAuthority(principal, "ADMIN");
            var isProjectManager = isAdmin
                || await projectMemberRepository.FindByIdAsync(projectId, currentUserId, token)
                    is { Role: not null } membership
                    && RoleEnums.ProjectManager.Matches(membership.Role.Name);
            if (!isProjectManager)
            {
                throw new ForbiddenException(EmptyEntireProjectTrashAuthority);
            }

            await repository.DeleteAllTrashTasksByProjectAsync(projectId, token);
            return true;
        }, cancellationToken);

    private async Task ValidateBeforeUpdateAsync(
        UpdateTaskRequest request, TaskEntity existing, ClaimsPrincipal principal,
        CancellationToken cancellationToken)
    {
        var statusChanged = request.Status is not null
            && !string.Equals(request.Status, existing.Status, StringComparison.OrdinalIgnoreCase);
        var assigneeChanged = request.AssigneeId is not null
            && request.AssigneeId != existing.AssigneeId;
        if (statusChanged || assigneeChanged)
        {
            await ValidateUpdateAuthorityAsync(existing, principal, cancellationToken);
        }
    }

    private async Task ValidateUpdateAuthorityAsync(
        TaskEntity existing, ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (principal.Identity?.IsAuthenticated != true)
        {
            throw new UnauthorizedException(InvalidTaskStatusAuthentication);
        }

        var callerId = TryGetCurrentUserId(principal);
        var roles = GetRoleClaims(principal);
        var isAdmin = roles.Any(role => role is "ROLE_ADMIN" or "ADMIN");
        var isProjectManager = isAdmin;
        if (!isProjectManager && callerId is long userId)
        {
            isProjectManager = existing.Project.OwnerId == userId
                || await projectMemberRepository.FindByIdAsync(
                    existing.ProjectId, userId, cancellationToken)
                    is { Role: not null } membership
                    && RoleEnums.ProjectManager.Matches(membership.Role.Name);
        }

        var isAssignee = callerId is not null && existing.AssigneeId == callerId;
        if (!isProjectManager && !isAssignee)
        {
            throw new ForbiddenException(InvalidTaskStatusAuthority);
        }
    }

    private async Task<TaskEntity> FindOrThrowAsync(long id, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(id, cancellationToken) ?? throw NotFound(id);

    private async Task<TaskEntity> FindOrThrowForUpdateAsync(long id, CancellationToken cancellationToken) =>
        await repository.FindByIdForUpdateAsync(id, cancellationToken) ?? throw NotFound(id);

    private static NotFoundException NotFound(long id) => new($"{EntityName} not found with id: {id}");

    private static long? TryGetCurrentUserId(ClaimsPrincipal principal)
    {
        var value = principal.FindFirst("userId")?.Value ?? principal.FindFirst("sub")?.Value;
        return long.TryParse(value, out var id) ? id : null;
    }

    private static IEnumerable<string> GetRoleClaims(ClaimsPrincipal principal) =>
        principal.Claims
            .Where(claim => claim.Type == "roles" || claim.Type == ClaimTypes.Role)
            .Select(claim => claim.Value);

    private static bool HasExactAuthority(ClaimsPrincipal principal, string authority) =>
        GetRoleClaims(principal).Any(role => string.Equals(role, authority, StringComparison.Ordinal));

    private static TaskResponse ToResponse(TaskEntity task) =>
        new(task.Id, task.ProjectId, task.Project?.Name, task.SprintId, task.Sprint?.Name,
            task.AssigneeId, task.Assignee?.Name, task.Title, task.Description, task.Estimate,
            task.Priority, task.Status, task.DueDate, task.TaskCode, task.IsDeleted,
            task.DeletedById, task.DeletedBy?.Name, task.CreatedAt, task.UpdatedAt);
}
