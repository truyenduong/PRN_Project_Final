using PRN232.Application.Dashboard;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Tests;

public sealed class DashboardMigrationTests
{
    [Fact]
    public async Task GetUserDashboardAsync_MapsDirectDatabaseMetrics()
    {
        var repository = new FakeDashboardRepository
        {
            UserData = new UserDashboardData(
                3, 12, 2, new Dictionary<string, long> { ["pending"] = 4, ["completed"] = 8 })
        };

        var result = await new DashboardService(repository)
            .GetUserDashboardAsync(19, CancellationToken.None);

        Assert.Equal(19, result.UserId);
        Assert.Equal(3, result.TotalProjects);
        Assert.Equal(12, result.TotalAssignedTasks);
        Assert.Equal(2, result.OverdueTasks);
        Assert.Equal(8, result.TasksByStatus["completed"]);
    }

    [Fact]
    public async Task GetProjectDashboardAsync_UsesJavaRoundingAndFirstActiveSprintData()
    {
        var repository = new FakeDashboardRepository
        {
            Project = new ProjectEntity
            {
                Id = 7,
                Name = "Roadmap",
                Description = "Plan",
                Owner = new UserEntity { Id = 8, Name = "Owner" }
            },
            ProjectData = new ProjectDashboardData(
                4, 32, 1, 3, "Sprint 1", new Dictionary<string, long> { ["completed"] = 1 })
        };

        var result = await new DashboardService(repository)
            .GetProjectDashboardAsync(7, CancellationToken.None);

        Assert.Equal(3.13, result.CompletionRate);
        Assert.Equal("Sprint 1", result.ActiveSprintName);
        Assert.Equal("Owner", result.OwnerName);
        Assert.Equal(4, result.TotalMembers);
        Assert.Equal(32, result.TotalTasks);
    }

    [Fact]
    public async Task GetProjectDashboardAsync_UsesZeroCompletionWhenThereAreNoTasks()
    {
        var repository = new FakeDashboardRepository
        {
            Project = new ProjectEntity { Id = 7, Name = "Empty" },
            ProjectData = new ProjectDashboardData(0, 0, 0, 0, null,
                new Dictionary<string, long>())
        };

        var result = await new DashboardService(repository)
            .GetProjectDashboardAsync(7, CancellationToken.None);

        Assert.Equal(0, result.CompletionRate);
    }

    [Fact]
    public async Task GetProjectDashboardAsync_UsesJavaNotFoundMessage()
    {
        var exception = await Assert.ThrowsAsync<NotFoundException>(() =>
            new DashboardService(new FakeDashboardRepository())
                .GetProjectDashboardAsync(71, CancellationToken.None));

        Assert.Equal("Project not found with id: 71", exception.Message);
    }

    private sealed class FakeDashboardRepository : IDashboardRepository
    {
        public UserDashboardData UserData { get; set; } =
            new(0, 0, 0, new Dictionary<string, long>());

        public ProjectEntity? Project { get; set; }

        public ProjectDashboardData ProjectData { get; set; } =
            new(0, 0, 0, 0, null, new Dictionary<string, long>());

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<UserDashboardData> GetUserDashboardDataAsync(
            long userId, DateOnly today, CancellationToken cancellationToken) =>
            Task.FromResult(UserData);

        public Task<ProjectEntity?> FindProjectForDashboardAsync(
            long projectId, CancellationToken cancellationToken) =>
            Task.FromResult(Project);

        public Task<ProjectDashboardData> GetProjectDashboardDataAsync(
            long projectId, DateOnly today, CancellationToken cancellationToken) =>
            Task.FromResult(ProjectData);
    }
}
