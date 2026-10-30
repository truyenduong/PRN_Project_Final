using PRN232.Application.Common;
using PRN232.Domain.Entities;

namespace PRN232.Application.Comments;

public interface ICommentRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    Task<(IReadOnlyList<CommentEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken);

    Task<CommentEntity?> FindByIdAsync(long id, CancellationToken cancellationToken);

    Task<CommentEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken);

    Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken);

    Task<CommentEntity> SaveAsync(CommentEntity comment, CancellationToken cancellationToken);

    Task DeleteByIdAsync(long id, CancellationToken cancellationToken);
}
