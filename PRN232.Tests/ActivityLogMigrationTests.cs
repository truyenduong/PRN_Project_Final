using PRN232.Application.ActivityLogs;
using PRN232.Application.Common;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Tests;

public sealed class ActivityLogMigrationTests
{
    [Fact]
    public async Task CreateAsync_PreservesReferencesAndMatchesIdOnlyJavaMapper()
    {
        var repository = new FakeActivityLogRepository();
        repository.Users.Add(new UserEntity { Id = 7, Name = "Ada" });
        repository.Projects.Add(new ProjectEntity { Id = 12, Name = "Migration", OwnerId = 7 });

        var response = await new ActivityLogService(repository).CreateAsync(
            new CreateActivityLogRequest { UserId = 7, ProjectId = 12, Action = "created task" },
            CancellationToken.None);

        var saved = Assert.Single(repository.ActivityLogs);
        Assert.Equal(7, saved.UserId);
        Assert.Equal(12, saved.ProjectId);
        Assert.Null(response.UserName);
        Assert.Null(response.ProjectName);
    }

    [Fact]
    public async Task UpdateAsync_OnlyChangesActionAndIgnoresNull()
    {
        var repository = new FakeActivityLogRepository();
        repository.Users.Add(new UserEntity { Id = 7, Name = "Ada" });
        repository.Projects.Add(new ProjectEntity { Id = 12, Name = "Migration", OwnerId = 7 });
        repository.ActivityLogs.Add(new ActivityLogEntity
        {
            Id = 4,
            UserId = 7,
            ProjectId = 12,
            Action = "before",
            User = new UserEntity { Id = 7, Name = "Ada" },
            Project = new ProjectEntity { Id = 12, Name = "Migration", OwnerId = 7 }
        });
        var service = new ActivityLogService(repository);

        var updated = await service.UpdateAsync(
            4, new UpdateActivityLogRequest { Action = "after" }, CancellationToken.None);
        var unchanged = await service.UpdateAsync(
            4, new UpdateActivityLogRequest(), CancellationToken.None);

        Assert.Equal("after", updated.Action);
        Assert.Equal("after", unchanged.Action);
        Assert.Equal(7, repository.ActivityLogs[0].UserId);
        Assert.Equal(12, repository.ActivityLogs[0].ProjectId);
    }

    [Fact]
    public async Task GetByIdAsync_UsesJavaNotFoundMessage()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            new ActivityLogService(new FakeActivityLogRepository())
                .GetByIdAsync(22, CancellationToken.None));

        Assert.Equal("ActivityLog not found with id: 22", exception.Message);
    }

    private sealed class FakeActivityLogRepository : IActivityLogRepository
    {
        public List<ActivityLogEntity> ActivityLogs { get; } = [];
        public List<UserEntity> Users { get; } = [];
        public List<ProjectEntity> Projects { get; } = [];

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<(IReadOnlyList<ActivityLogEntity> Content, long TotalElements)> GetPageAsync(
            string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<ActivityLogEntity>)ActivityLogs, (long)ActivityLogs.Count));

        public Task<ActivityLogEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(ActivityLogs.SingleOrDefault(item => item.Id == id));

        public Task<ActivityLogEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(ActivityLogs.Any(item => item.Id == id));

        public Task<ActivityLogEntity> SaveAsync(
            ActivityLogEntity activityLog, CancellationToken cancellationToken)
        {
            if (activityLog.Id == 0)
            {
                activityLog.Id = ActivityLogs.Count + 1;
                ActivityLogs.Add(activityLog);
            }

            activityLog.User = Users.Single(user => user.Id == activityLog.UserId);
            activityLog.Project = activityLog.ProjectId is null
                ? null
                : Projects.Single(project => project.Id == activityLog.ProjectId);
            return Task.FromResult(activityLog);
        }

        public Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
        {
            ActivityLogs.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }
    }
}
