using PRN232.Application.Common;
using PRN232.Domain.Entities;

namespace PRN232.Application.Attachments;

public interface IAttachmentRepository
{
    Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken);

    Task<(IReadOnlyList<AttachmentEntity> Content, long TotalElements)> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken);

    Task<AttachmentEntity?> FindByIdAsync(long id, CancellationToken cancellationToken);

    Task<AttachmentEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken);

    Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken);

    Task<AttachmentEntity> SaveAsync(AttachmentEntity attachment, CancellationToken cancellationToken);

    Task DeleteByIdAsync(long id, CancellationToken cancellationToken);
}
