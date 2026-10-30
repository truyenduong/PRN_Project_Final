using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;
using PRN232.Application.Common;

namespace PRN232.Application.ActivityLogs;

public sealed class ActivityLogService(IActivityLogRepository repository)
{
    private const string EntityName = "ActivityLog";

    public Task<ActivityLogPageResult> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort,
        CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var result = await repository.GetPageAsync(search, page, size, sort, token);
            return new ActivityLogPageResult(
                result.Content.Select(ToResponse).ToArray(), page, size, result.TotalElements);
        }, cancellationToken);

    public Task<ActivityLogResponse> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
            ToResponse(await FindOrThrowAsync(id, token)), cancellationToken);

    public Task<ActivityLogResponse> CreateAsync(
        CreateActivityLogRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var saved = await repository.SaveAsync(new ActivityLogEntity
            {
                UserId = request.UserId!.Value,
                ProjectId = request.ProjectId,
                Action = request.Action
            }, token);
            return ToResponse(saved) with { UserName = null, ProjectName = null };
        }, cancellationToken);

    public Task<ActivityLogResponse> UpdateAsync(
        long id, UpdateActivityLogRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var activityLog = await repository.FindByIdForUpdateAsync(id, token)
                ?? throw NotFound(id);
            activityLog.Action = request.Action ?? activityLog.Action;
            return ToResponse(await repository.SaveAsync(activityLog, token));
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

    private async Task<ActivityLogEntity> FindOrThrowAsync(long id, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(id, cancellationToken) ?? throw NotFound(id);

    private static NotFoundException NotFound(long id) =>
        new($"{EntityName} not found with id: {id}");

    private static ActivityLogResponse ToResponse(ActivityLogEntity activityLog) =>
        new(activityLog.Id, activityLog.UserId, activityLog.User?.Name, activityLog.ProjectId,
            activityLog.Project?.Name, activityLog.Action, activityLog.CreatedAt, activityLog.UpdatedAt);
}
