using PRN232.Application.Comments;
using PRN232.Application.Common;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Tests;

public sealed class CommentMigrationTests
{
    [Fact]
    public async Task CreateAsync_PreservesJavaIdOnlyUserMapping()
    {
        var repository = new FakeCommentRepository();

        var response = await new CommentService(repository).CreateAsync(
            new CreateCommentRequest { TaskId = 12, UserId = 7, Content = "Looks good" },
            CancellationToken.None);

        var saved = Assert.Single(repository.Comments);
        Assert.Equal(12, saved.TaskId);
        Assert.Equal(7, saved.UserId);
        Assert.Null(response.UserName);
    }

    [Fact]
    public async Task UpdateAsync_IgnoresNullContentAndReturnsRelatedUserName()
    {
        var repository = new FakeCommentRepository();
        repository.Comments.Add(new CommentEntity
        {
            Id = 4,
            TaskId = 12,
            UserId = 7,
            User = new UserEntity { Id = 7, Name = "Ada" },
            Content = "Original"
        });
        var service = new CommentService(repository);

        var updated = await service.UpdateAsync(
            4, new UpdateCommentRequest { Content = "Edited" }, CancellationToken.None);
        var unchanged = await service.UpdateAsync(4, new UpdateCommentRequest(), CancellationToken.None);

        Assert.Equal("Edited", updated.Content);
        Assert.Equal("Ada", updated.UserName);
        Assert.Equal("Edited", unchanged.Content);
    }

    [Fact]
    public async Task GetPageAsync_PassesRsqlAndUsesRequestedPageShape()
    {
        var repository = new FakeCommentRepository();

        var result = await new CommentService(repository).GetPageAsync(
            "taskId==12;userId==7", 2, 5, [], CancellationToken.None);

        Assert.Equal("taskId==12;userId==7", repository.LastSearch);
        Assert.Equal(2, result.Page);
        Assert.Equal(5, result.Size);
        Assert.Equal(9, result.TotalElements);
    }

    [Fact]
    public async Task GetByIdAsync_UsesJavaNotFoundMessage()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            new CommentService(new FakeCommentRepository())
                .GetByIdAsync(21, CancellationToken.None));

        Assert.Equal("Comment not found with id: 21", exception.Message);
    }

    private sealed class FakeCommentRepository : ICommentRepository
    {
        public List<CommentEntity> Comments { get; } = [];
        public string? LastSearch { get; private set; }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<(IReadOnlyList<CommentEntity> Content, long TotalElements)> GetPageAsync(
            string? search, int page, int size, IReadOnlyList<PageSort> sort,
            CancellationToken cancellationToken)
        {
            LastSearch = search;
            return Task.FromResult(((IReadOnlyList<CommentEntity>)Comments, 9L));
        }

        public Task<CommentEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Comments.SingleOrDefault(item => item.Id == id));

        public Task<CommentEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Comments.Any(item => item.Id == id));

        public Task<CommentEntity> SaveAsync(CommentEntity comment, CancellationToken cancellationToken)
        {
            if (comment.Id == 0)
            {
                comment.Id = Comments.Count + 1;
                Comments.Add(comment);
            }

            return Task.FromResult(comment);
        }

        public Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
        {
            Comments.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }
    }
}
