using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;
using PRN232.Application.Common;

namespace PRN232.Application.Notifications;

public sealed class NotificationService(INotificationRepository repository)
{
    private const string EntityName = "Notification";

    public Task<NotificationPageResult> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort,
        CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var result = await repository.GetPageAsync(search, page, size, sort, token);
            return new NotificationPageResult(
                result.Content.Select(ToResponse).ToArray(), page, size, result.TotalElements);
        }, cancellationToken);

    public Task<NotificationResponse> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
            ToResponse(await FindOrThrowAsync(id, token)), cancellationToken);

    public Task<NotificationResponse> CreateAsync(
        CreateNotificationRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var saved = await repository.SaveAsync(new NotificationEntity
            {
                UserId = request.UserId!.Value,
                Type = request.Type,
                Content = request.Content,
                IsRead = request.IsRead
            }, token);
            return ToResponse(saved) with { UserName = null };
        }, cancellationToken);

    public Task<NotificationResponse> UpdateAsync(
        long id, UpdateNotificationRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var notification = await repository.FindByIdForUpdateAsync(id, token)
                ?? throw NotFound(id);
            notification.Content = request.Content ?? notification.Content;
            notification.IsRead = request.IsRead ?? notification.IsRead;
            return ToResponse(await repository.SaveAsync(notification, token));
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

    private async Task<NotificationEntity> FindOrThrowAsync(long id, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(id, cancellationToken) ?? throw NotFound(id);

    private static NotFoundException NotFound(long id) =>
        new($"{EntityName} not found with id: {id}");

    private static NotificationResponse ToResponse(NotificationEntity notification) =>
        new(notification.Id, notification.UserId, notification.User?.Name, notification.Type,
            notification.Content, notification.IsRead, notification.CreatedAt, notification.UpdatedAt);
}
