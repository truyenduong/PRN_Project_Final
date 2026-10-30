using System.Security.Claims;
using PRN232.Application.Common;
using PRN232.Application.ProjectMembers;
using PRN232.Application.Subtasks;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Tests;

public sealed class SubtaskMigrationTests
{
    [Fact]
    public async Task GetByTaskIdAsync_ReturnsTheCompleteUnpagedList()
    {
        var repository = CreateRepository();
        repository.Subtasks.AddRange(
        [
            CreateSubtask(1, assigneeId: null),
            CreateSubtask(2, assigneeId: null)
        ]);

        var result = await CreateService(repository, new FakeProjectMemberRepository(),
            new FakeAssignmentNotifier()).GetByTaskIdAsync(5, CancellationToken.None);

        Assert.Equal(5, repository.LastTaskId);
        Assert.Equal([1L, 2L], result.Select(item => item.Id));
    }

    [Fact]
    public async Task CreateAsync_DefaultsCompletionAndSkipsParentAssigneeNotification()
    {
        var repository = CreateRepository(taskAssigneeId: 17);
        var notifier = new FakeAssignmentNotifier();
        var service = CreateService(repository, new FakeProjectMemberRepository(), notifier);

        var sameAssignee = await service.CreateAsync(new CreateSubtaskRequest
        {
            TaskId = 5,
            Title = "Same person",
            AssigneeId = 17
        }, CancellationToken.None);
        var otherAssignee = await service.CreateAsync(new CreateSubtaskRequest
        {
            TaskId = 5,
            Title = "Different person",
            AssigneeId = 18
        }, CancellationToken.None);

        Assert.False(sameAssignee.IsCompleted);
        Assert.Equal([otherAssignee.Id], notifier.SubtaskAssignments);
    }

    [Fact]
    public async Task UpdateAsync_AllowsTaskOwnerAndNotifiesOnlyNewDistinctAssignee()
    {
        var repository = CreateRepository(taskAssigneeId: 17);
        repository.Subtasks.Add(CreateSubtask(9, assigneeId: 19));
        var notifier = new FakeAssignmentNotifier();
        var service = CreateService(repository, new FakeProjectMemberRepository(), notifier);

        var response = await service.UpdateAsync(
            9,
            new UpdateSubtaskRequest { IsCompleted = true, AssigneeId = 18 },
            Principal(11), CancellationToken.None);

        Assert.True(response.IsCompleted);
        Assert.Equal(18, response.AssigneeId);
        Assert.Equal([9L], notifier.SubtaskAssignments);
    }

    [Fact]
    public async Task UpdateAsync_SkipsNotificationWhenAssigneeMatchesParentTask()
    {
        var repository = CreateRepository(taskAssigneeId: 17);
        repository.Subtasks.Add(CreateSubtask(9, assigneeId: 19));
        var notifier = new FakeAssignmentNotifier();

        await CreateService(repository, new FakeProjectMemberRepository(), notifier)
            .UpdateAsync(9, new UpdateSubtaskRequest { AssigneeId = 17 },
                Principal(11), CancellationToken.None);

        Assert.Empty(notifier.SubtaskAssignments);
    }

    [Theory]
    [InlineData(11L, null, null, false)]
    [InlineData(30L, 30L, null, false)]
    [InlineData(31L, null, 31L, false)]
    [InlineData(40L, null, null, true)]
    public async Task UpdateAsync_AuthorizesOwnerTaskAssigneeSubtaskAssigneeAndAdmin(
        long callerId, long? taskAssigneeId, long? subtaskAssigneeId, bool isAdmin)
    {
        var repository = CreateRepository(taskAssigneeId);
        repository.Subtasks.Add(CreateSubtask(4, subtaskAssigneeId, taskAssigneeId));
        var members = new FakeProjectMemberRepository();
        var principal = Principal(callerId, isAdmin ? "ADMIN" : null);
        var service = CreateService(repository, members, new FakeAssignmentNotifier());

        var response = await service.UpdateAsync(
            4, new UpdateSubtaskRequest { IsCompleted = true }, principal, CancellationToken.None);

        Assert.True(response.IsCompleted);
    }

    [Fact]
    public async Task UpdateAsync_AllowsProjectManagerMember()
    {
        var repository = CreateRepository();
        repository.Subtasks.Add(CreateSubtask(4, assigneeId: 18));
        var members = new FakeProjectMemberRepository();
        members.Members.Add(new ProjectMemberEntity
        {
            ProjectId = 6,
            UserId = 42,
            Role = new RoleEntity { Name = "project_manager" }
        });

        var response = await CreateService(repository, members, new FakeAssignmentNotifier())
            .UpdateAsync(4, new UpdateSubtaskRequest { IsCompleted = true },
                Principal(42), CancellationToken.None);

        Assert.True(response.IsCompleted);
    }

    [Fact]
    public async Task UpdateAsync_RejectsUnrelatedUser()
    {
        var repository = CreateRepository(taskAssigneeId: 30);
        repository.Subtasks.Add(CreateSubtask(4, assigneeId: 31));
        var service = CreateService(
            repository, new FakeProjectMemberRepository(), new FakeAssignmentNotifier());

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UpdateAsync(4, new UpdateSubtaskRequest { IsCompleted = true },
                Principal(40), CancellationToken.None));
    }

    [Fact]
    public async Task UpdateAsync_PreservesJavaNullAssigneeDereference()
    {
        var repository = CreateRepository();
        repository.Subtasks.Add(CreateSubtask(4, assigneeId: null));

        await Assert.ThrowsAsync<NullReferenceException>(() =>
            CreateService(repository, new FakeProjectMemberRepository(), new FakeAssignmentNotifier())
                .UpdateAsync(4, new UpdateSubtaskRequest { AssigneeId = 19 },
                    Principal(11), CancellationToken.None));
    }

    private static FakeSubtaskRepository CreateRepository(long? taskAssigneeId = null)
    {
        var repository = new FakeSubtaskRepository
        {
            TaskEntity = new TaskEntity
            {
                Id = 5,
                ProjectId = 6,
                AssigneeId = taskAssigneeId,
                Project = new ProjectEntity { Id = 6, OwnerId = 11, Name = "Project" }
            }
        };
        return repository;
    }

    private static SubtaskEntity CreateSubtask(long id, long? assigneeId, long? taskAssigneeId = 17) =>
        new()
        {
            Id = id,
            TaskId = 5,
            Task = new TaskEntity
            {
                Id = 5,
                ProjectId = 6,
                AssigneeId = taskAssigneeId,
                Project = new ProjectEntity { Id = 6, OwnerId = 11, Name = "Project" }
            },
            Title = "Subtask",
            IsCompleted = false,
            AssigneeId = assigneeId,
            Assignee = assigneeId is long userId ? new UserEntity { Id = userId, Name = "Assignee" } : null
        };

    private static SubtaskService CreateService(
        FakeSubtaskRepository repository, IProjectMemberRepository members,
        IAssignmentNotifier notifier) =>
        new(repository, members, notifier);

    private static ClaimsPrincipal Principal(long userId, string? role = null)
    {
        var claims = new List<Claim> { new("userId", userId.ToString()) };
        if (role is not null)
        {
            claims.Add(new Claim("roles", role));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));
    }

    private sealed class FakeSubtaskRepository : ISubtaskRepository
    {
        public List<SubtaskEntity> Subtasks { get; } = [];
        public TaskEntity TaskEntity { get; set; } = null!;
        public long? LastTaskId { get; private set; }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<(IReadOnlyList<SubtaskEntity> Content, long TotalElements)> GetPageAsync(
            string? search, int page, int size, IReadOnlyList<PageSort> sort,
            CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<SubtaskEntity>)Subtasks, (long)Subtasks.Count));

        public Task<IReadOnlyList<SubtaskEntity>> GetByTaskIdAsync(
            long taskId, CancellationToken cancellationToken)
        {
            LastTaskId = taskId;
            return Task.FromResult((IReadOnlyList<SubtaskEntity>)Subtasks
                .Where(item => item.TaskId == taskId).ToArray());
        }

        public Task<SubtaskEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Subtasks.SingleOrDefault(item => item.Id == id));

        public Task<SubtaskEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Subtasks.Any(item => item.Id == id));

        public Task<SubtaskEntity> SaveAsync(SubtaskEntity subtask, CancellationToken cancellationToken)
        {
            if (subtask.Id == 0)
            {
                subtask.Id = Subtasks.Count + 1;
                Subtasks.Add(subtask);
            }

            subtask.Task = TaskEntity;
            if (subtask.AssigneeId is long assigneeId)
            {
                subtask.Assignee = new UserEntity { Id = assigneeId, Name = "Assignee" };
            }

            return Task.FromResult(subtask);
        }

        public Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
        {
            Subtasks.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProjectMemberRepository : IProjectMemberRepository
    {
        public List<ProjectMemberEntity> Members { get; } = [];

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<(IReadOnlyList<ProjectMemberEntity> Content, long TotalElements)> GetPageAsync(
            string? search, int page, int size, IReadOnlyList<PageSort> sort,
            CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<ProjectMemberEntity>)Members, (long)Members.Count));

        public Task<ProjectMemberEntity?> FindByIdAsync(
            long projectId, long userId, CancellationToken cancellationToken) =>
            Task.FromResult(Members.SingleOrDefault(
                item => item.ProjectId == projectId && item.UserId == userId));

        public Task<ProjectMemberEntity?> FindByIdForUpdateAsync(
            long projectId, long userId, CancellationToken cancellationToken) =>
            FindByIdAsync(projectId, userId, cancellationToken);

        public Task<bool> ExistsByIdAsync(long projectId, long userId, CancellationToken cancellationToken) =>
            Task.FromResult(Members.Any(item => item.ProjectId == projectId && item.UserId == userId));

        public Task<ProjectMemberEntity> SaveAsync(
            ProjectMemberEntity member, CancellationToken cancellationToken) =>
            Task.FromResult(member);

        public Task DeleteByIdAsync(long projectId, long userId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task<long> CountProjectsByUserIdAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult((long)Members.Count(item => item.UserId == userId));

        public Task<long> CountMembersByProjectIdAsync(long projectId, CancellationToken cancellationToken) =>
            Task.FromResult((long)Members.Count(item => item.ProjectId == projectId));
    }

    private sealed class FakeAssignmentNotifier : IAssignmentNotifier
    {
        public List<long> SubtaskAssignments { get; } = [];

        public Task NotifyTaskAssignmentAsync(long taskId, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task NotifySubtaskAssignmentAsync(long subtaskId, CancellationToken cancellationToken)
        {
            SubtaskAssignments.Add(subtaskId);
            return Task.CompletedTask;
        }
    }
}
