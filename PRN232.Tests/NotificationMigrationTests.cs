using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using PRN232.Application;
using PRN232.Application.Common;
using PRN232.Application.Notifications;
using PRN232.Application.Tasks;
using PRN232.Application.Attachments;
using PRN232.Application.Comments;
using PRN232.Application.Subtasks;
using PRN232.Application.Dashboard;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;
using PRN232.Infrastructure;
using PRN232.Infrastructure.Persistence;

namespace PRN232.Tests;

public sealed class NotificationMigrationTests
{
    [Fact]
    public async Task CreateAsync_UsesDefaultUnreadStateAndMatchesIdOnlyJavaMapper()
    {
        var repository = new FakeNotificationRepository();
        repository.Users.Add(new UserEntity { Id = 3, Name = "Lin" });

        var response = await new NotificationService(repository).CreateAsync(
            new CreateNotificationRequest
            {
                UserId = 3,
                Type = "TASK_ASSIGNED:18",
                Content = "You have been assigned a task: \"Review\""
            },
            CancellationToken.None);

        var saved = Assert.Single(repository.Notifications);
        Assert.False(saved.IsRead);
        Assert.Null(response.UserName);
        Assert.Equal("TASK_ASSIGNED:18", response.Type);
    }

    [Fact]
    public async Task UpdateAsync_IgnoresNullAndDoesNotChangeRecipientOrType()
    {
        var repository = new FakeNotificationRepository();
        repository.Users.Add(new UserEntity { Id = 3, Name = "Lin" });
        repository.Notifications.Add(new NotificationEntity
        {
            Id = 8,
            UserId = 3,
            Type = "TASK_ASSIGNED:18",
            Content = "Original",
            IsRead = false,
            User = repository.Users[0]
        });
        var service = new NotificationService(repository);

        var updated = await service.UpdateAsync(
            8, new UpdateNotificationRequest { Content = "Changed", IsRead = true }, CancellationToken.None);
        var unchanged = await service.UpdateAsync(
            8, new UpdateNotificationRequest(), CancellationToken.None);

        Assert.Equal("Changed", updated.Content);
        Assert.True(updated.IsRead);
        Assert.Equal("Changed", unchanged.Content);
        Assert.True(unchanged.IsRead);
        Assert.Equal(3, repository.Notifications[0].UserId);
        Assert.Equal("TASK_ASSIGNED:18", repository.Notifications[0].Type);
    }

    [Fact]
    public async Task AssignmentNotifier_LogsAndSuppressesPersistenceFailures()
    {
        var notifier = new AssignmentNotifier(
            new FailingDbContextFactory(),
            NullLogger<AssignmentNotifier>.Instance);

        await notifier.NotifyTaskAssignmentAsync(1, CancellationToken.None);
        await notifier.NotifySubtaskAssignmentAsync(2, CancellationToken.None);
    }

    [Fact]
    public void AssignmentNotifierAndScopedContext_AreRegistered()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:WorkflowHub"] =
                    "Host=localhost;Database=workflow;Username=workflow;Password=workflow",
                ["Database:Schema"] = "workflow"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddApplication(configuration);
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<AssignmentNotifier>(scope.ServiceProvider.GetRequiredService<IAssignmentNotifier>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<TaskService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AttachmentService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<CommentService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<SubtaskService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<DashboardService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<AppDbContext>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ICommentRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISubtaskRepository>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IDashboardRepository>());
    }

    [Fact]
    public async Task GetByIdAsync_UsesJavaNotFoundMessage()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            new NotificationService(new FakeNotificationRepository())
                .GetByIdAsync(9, CancellationToken.None));

        Assert.Equal("Notification not found with id: 9", exception.Message);
    }

    private sealed class FakeNotificationRepository : INotificationRepository
    {
        public List<NotificationEntity> Notifications { get; } = [];
        public List<UserEntity> Users { get; } = [];

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<(IReadOnlyList<NotificationEntity> Content, long TotalElements)> GetPageAsync(
            string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<NotificationEntity>)Notifications, (long)Notifications.Count));

        public Task<NotificationEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Notifications.SingleOrDefault(item => item.Id == id));

        public Task<NotificationEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Notifications.Any(item => item.Id == id));

        public Task<NotificationEntity> SaveAsync(
            NotificationEntity notification, CancellationToken cancellationToken)
        {
            if (notification.Id == 0)
            {
                notification.Id = Notifications.Count + 1;
                Notifications.Add(notification);
            }

            notification.User = Users.Single(user => user.Id == notification.UserId);
            return Task.FromResult(notification);
        }

        public Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
        {
            Notifications.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class FailingDbContextFactory : IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() =>
            throw new InvalidOperationException("The notification database is unavailable.");

        public Task<AppDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromException<AppDbContext>(
                new InvalidOperationException("The notification database is unavailable."));
    }
}
