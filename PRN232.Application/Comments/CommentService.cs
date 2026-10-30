using PRN232.Application.Common;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Application.Comments;

public sealed class CommentService(ICommentRepository repository)
{
    private const string EntityName = "Comment";

    public Task<CommentPageResult> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort,
        CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var result = await repository.GetPageAsync(search, page, size, sort, token);
            return new CommentPageResult(
                result.Content.Select(ToResponse).ToArray(), page, size, result.TotalElements);
        }, cancellationToken);

    public Task<CommentResponse> GetByIdAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
            ToResponse(await FindOrThrowAsync(id, token)), cancellationToken);

    public Task<CommentResponse> CreateAsync(
        CreateCommentRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var saved = await repository.SaveAsync(new CommentEntity
            {
                TaskId = request.TaskId!.Value,
                UserId = request.UserId!.Value,
                Content = request.Content
            }, token);
            return ToResponse(saved) with { UserName = null };
        }, cancellationToken);

    public Task<CommentResponse> UpdateAsync(
        long id, UpdateCommentRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var comment = await repository.FindByIdForUpdateAsync(id, token)
                ?? throw NotFound(id);
            comment.Content = request.Content ?? comment.Content;
            return ToResponse(await repository.SaveAsync(comment, token));
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

    private async Task<CommentEntity> FindOrThrowAsync(long id, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(id, cancellationToken) ?? throw NotFound(id);

    private static NotFoundException NotFound(long id) =>
        new($"{EntityName} not found with id: {id}");

    private static CommentResponse ToResponse(CommentEntity comment) =>
        new(comment.Id, comment.TaskId, comment.UserId, comment.User?.Name, comment.Content,
            comment.CreatedAt, comment.UpdatedAt);
}
