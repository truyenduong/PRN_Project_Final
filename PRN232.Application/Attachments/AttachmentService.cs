using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;
using PRN232.Application.Common;

namespace PRN232.Application.Attachments;

public sealed class AttachmentService(IAttachmentRepository repository)
{
    private const string EntityName = "Attachment";

    public Task<AttachmentPageResult> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort,
        CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var result = await repository.GetPageAsync(search, page, size, sort, token);
            return new AttachmentPageResult(
                result.Content.Select(ToResponse).ToArray(), page, size, result.TotalElements);
        }, cancellationToken);

    public Task<AttachmentResponse> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
            ToResponse(await FindOrThrowAsync(id, token)), cancellationToken);

    public Task<AttachmentResponse> CreateAsync(
        CreateAttachmentRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var saved = await repository.SaveAsync(new AttachmentEntity
            {
                TaskId = request.TaskId!.Value,
                FileUrl = request.FileUrl,
                FileName = request.FileName
            }, token);
            return ToResponse(saved);
        }, cancellationToken);

    public Task<AttachmentResponse> UpdateAsync(
        long id, UpdateAttachmentRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var attachment = await repository.FindByIdForUpdateAsync(id, token)
                ?? throw NotFound(id);
            attachment.FileUrl = request.FileUrl ?? attachment.FileUrl;
            attachment.FileName = request.FileName ?? attachment.FileName;
            return ToResponse(await repository.SaveAsync(attachment, token));
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

    private async Task<AttachmentEntity> FindOrThrowAsync(long id, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(id, cancellationToken) ?? throw NotFound(id);

    private static NotFoundException NotFound(long id) =>
        new($"{EntityName} not found with id: {id}");

    private static AttachmentResponse ToResponse(AttachmentEntity attachment) =>
        new(attachment.Id, attachment.TaskId, attachment.FileUrl, attachment.FileName,
            attachment.CreatedAt, attachment.UpdatedAt);
}
