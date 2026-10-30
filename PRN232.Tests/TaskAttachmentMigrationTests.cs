using System.Security.Claims;
using PRN232.Application.Attachments;
using PRN232.Application.Common;
using PRN232.Application.ProjectMembers;
using PRN232.Application.Tasks;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Tests;

public sealed class TaskAttachmentMigrationTests
{
    [Fact]
    public async Task TaskCreate_GeneratesCodeAndNotifiesOnlyWhenAssigned()
    {
        var repository = new FakeTaskRepository();
        repository.Projects.Add(new ProjectEntity { Id = 5, Name = "Project", OwnerId = 11 });
        var notifier = new FakeAssignmentNotifier();
        var service = CreateTaskService(repository, notifier);

        var created = await service.CreateAsync(new CreateTaskRequest
        {
            ProjectId = 5,
            AssigneeId = 17,
            Title = "Review"
        }, CancellationToken.None);

        Assert.Matches("^TASK-[0-9]{1,5}$", created.TaskCode);
        Assert.False(created.IsDeleted);
        Assert.Equal([created.Id], notifier.TaskAssignments);

        await service.CreateAsync(new CreateTaskRequest
        {
            ProjectId = 5,
            Title = "Unassigned"
        }, CancellationToken.None);
        Assert.Equal([created.Id], notifier.TaskAssignments);
    }

    [Theory]
    [InlineData(null, "isDeleted==false")]
    [InlineData("", "isDeleted==false")]
    [InlineData("status==pending", "status==pending;isDeleted==false")]
    [InlineData("isDeleted==true", "isDeleted==true")]
    [InlineData("IsDeleted==true", "IsDeleted==true;isDeleted==false")]
    public async Task TaskGetPage_AppliesJavaSoftDeleteSearchDefault(string? search, string expectedSearch)
    {
        var repository = new FakeTaskRepository();
        await CreateTaskService(repository, new FakeAssignmentNotifier())
            .GetPageAsync(search, 0, 20, [], CancellationToken.None);

        Assert.Equal(expectedSearch, repository.LastSearch);
    }

    [Fact]
    public async Task TaskUpdate_AllowsProjectOwnerAndNotifiesNewAssigneeOnce()
    {
        var repository = CreateRepositoryWithTask(assigneeId: 17);
        var notifier = new FakeAssignmentNotifier();
        var service = CreateTaskService(repository, notifier);
        var owner = Principal(11);

        var updated = await service.UpdateAsync(
            3, new UpdateTaskRequest { Status = "IN_PROGRESS", AssigneeId = 19 },
            owner, CancellationToken.None);
        await service.UpdateAsync(
            3, new UpdateTaskRequest { Title = "Title only" },
            owner, CancellationToken.None);

        Assert.Equal("IN_PROGRESS", updated.Status);
        Assert.Equal(19, updated.AssigneeId);
        Assert.Equal([3L], notifier.TaskAssignments);
    }

    [Fact]
    public async Task TaskUpdate_AllowsCurrentAssigneeAndRejectsUnrelatedUser()
    {
        var repository = CreateRepositoryWithTask(assigneeId: 17);
        var service = CreateTaskService(repository, new FakeAssignmentNotifier());

        var response = await service.UpdateAsync(
            3, new UpdateTaskRequest { Status = "DONE" }, Principal(17), CancellationToken.None);
        Assert.Equal("DONE", response.Status);

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UpdateAsync(3, new UpdateTaskRequest { Status = "BLOCKED" },
                Principal(25), CancellationToken.None));
        Assert.Equal(
            "Only the Project Manager or the person assigned the task has the authority to change the task status or assign",
            exception.Message);
    }

    [Fact]
    public async Task TaskUpdate_AdminAliasesAndProjectManagerMemberCanChangeStatus()
    {
        var repository = CreateRepositoryWithTask(assigneeId: 17);
        var members = new FakeProjectMemberRepository();
        members.Members.Add(new ProjectMemberEntity
        {
            ProjectId = 5,
            UserId = 25,
            Role = new RoleEntity { Name = "project_manager" }
        });
        var service = CreateTaskService(repository, new FakeAssignmentNotifier(), members);

        await service.UpdateAsync(3, new UpdateTaskRequest { Status = "ADMIN-ALLOWED" },
            Principal(22, "ROLE_ADMIN"), CancellationToken.None);
        var response = await service.UpdateAsync(3, new UpdateTaskRequest { Status = "PM-ALLOWED" },
            Principal(25), CancellationToken.None);

        Assert.Equal("PM-ALLOWED", response.Status);
    }

    [Fact]
    public async Task TaskDeletePreservesNullDeletedByAndRestoreClearsTrashFlag()
    {
        var repository = CreateRepositoryWithTask(assigneeId: 17);
        var service = CreateTaskService(repository, new FakeAssignmentNotifier());

        await service.DeleteAsync(3, Principal(22), CancellationToken.None);
        var deleted = Assert.Single(repository.Tasks);
        Assert.True(deleted.IsDeleted);
        Assert.Null(deleted.DeletedById);

        var restored = await service.RestoreAsync(3, CancellationToken.None);
        Assert.False(restored.IsDeleted);
        Assert.Null(restored.DeletedById);
        Assert.Equal(0, repository.DeleteAllByProjectAndUserCount);
    }

    [Fact]
    public async Task TaskEmptyTrash_UsesUserFilterAndRequiresAdminOrPmForAll()
    {
        var repository = CreateRepositoryWithTask(assigneeId: 17);
        var members = new FakeProjectMemberRepository();
        var service = CreateTaskService(repository, new FakeAssignmentNotifier(), members);

        await service.EmptyTrashAsync(5, false, Principal(22), CancellationToken.None);
        Assert.Equal((5L, 22L), repository.LastTrashUserDelete);

        var ownerDenied = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.EmptyTrashAsync(5, true, Principal(11), CancellationToken.None));
        Assert.Equal("Only the Project Manager has the authority to empty the entire project trash bin.",
            ownerDenied.Message);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.EmptyTrashAsync(5, true, Principal(22, "ROLE_ADMIN"), CancellationToken.None));
        await service.EmptyTrashAsync(5, true, Principal(22, "ADMIN"), CancellationToken.None);
        Assert.Equal(1, repository.DeleteAllTrashTasksByProjectCount);
    }

    [Fact]
    public async Task TaskPermanentDeleteChecksExistenceAndPhysicallyDeletes()
    {
        var repository = CreateRepositoryWithTask(assigneeId: null);
        var service = CreateTaskService(repository, new FakeAssignmentNotifier());

        await service.PermanentDeleteAsync(3, CancellationToken.None);

        Assert.Empty(repository.Tasks);
    }

    [Fact]
    public async Task AttachmentCrud_PreservesTaskReferenceAndIgnoresNullUpdates()
    {
        var repository = new FakeAttachmentRepository();
        var service = new AttachmentService(repository);

        var created = await service.CreateAsync(new CreateAttachmentRequest
        {
            TaskId = 9,
            FileUrl = "https://example.test/file",
            FileName = "design.txt"
        }, CancellationToken.None);
        var updated = await service.UpdateAsync(
            created.Id, new UpdateAttachmentRequest { FileName = "updated.txt" }, CancellationToken.None);
        var unchangedUrl = await service.UpdateAsync(
            created.Id, new UpdateAttachmentRequest(), CancellationToken.None);

        Assert.Equal(9, updated.TaskId);
        Assert.Equal("updated.txt", updated.FileName);
        Assert.Equal("https://example.test/file", unchangedUrl.FileUrl);
        await service.DeleteAsync(created.Id, CancellationToken.None);
        Assert.Empty(repository.Attachments);
    }

    private static TaskService CreateTaskService(
        FakeTaskRepository repository,
        FakeAssignmentNotifier notifier,
        FakeProjectMemberRepository? members = null) =>
        new(repository, members ?? new FakeProjectMemberRepository(),
            new PrincipalCurrentUserAccessor(), notifier);

    private static FakeTaskRepository CreateRepositoryWithTask(long? assigneeId)
    {
        var repository = new FakeTaskRepository();
        var project = new ProjectEntity
        {
            Id = 5,
            Name = "Project",
            OwnerId = 11,
            Owner = new UserEntity { Id = 11, Name = "Owner" }
        };
        repository.Projects.Add(project);
        repository.Tasks.Add(new TaskEntity
        {
            Id = 3,
            ProjectId = 5,
            Project = project,
            AssigneeId = assigneeId,
            Assignee = assigneeId is null ? null : new UserEntity { Id = assigneeId.Value, Name = "Assignee" },
            Title = "Task",
            Status = "PENDING",
            IsDeleted = false
        });
        return repository;
    }

    private static ClaimsPrincipal Principal(long userId, params string[] roles) =>
        new(new ClaimsIdentity(
            [new Claim("sub", userId.ToString()), .. roles.Select(role => new Claim("roles", role))],
            "test"));

    private sealed class FakeTaskRepository : ITaskRepository
    {
        public List<TaskEntity> Tasks { get; } = [];
        public List<ProjectEntity> Projects { get; } = [];
        public string? LastSearch { get; private set; }
        public (long ProjectId, long UserId)? LastTrashUserDelete { get; private set; }
        public int DeleteAllByProjectAndUserCount { get; private set; }
        public int DeleteAllTrashTasksByProjectCount { get; private set; }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<(IReadOnlyList<TaskEntity> Content, long TotalElements)> GetPageAsync(
            string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken)
        {
            LastSearch = search;
            return Task.FromResult(((IReadOnlyList<TaskEntity>)Tasks, (long)Tasks.Count));
        }

        public Task<TaskEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Tasks.SingleOrDefault(task => task.Id == id));

        public Task<TaskEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Tasks.Any(task => task.Id == id));

        public Task<TaskEntity> SaveAsync(TaskEntity task, CancellationToken cancellationToken)
        {
            if (task.Id == 0)
            {
                task.Id = Tasks.Count == 0 ? 1 : Tasks.Max(item => item.Id) + 1;
                task.Project = Projects.Single(project => project.Id == task.ProjectId);
                Tasks.Add(task);
            }
            else
            {
                var existing = Tasks.Single(item => item.Id == task.Id);
                existing.ProjectId = task.ProjectId;
                existing.SprintId = task.SprintId;
                existing.AssigneeId = task.AssigneeId;
                existing.Title = task.Title;
                existing.Description = task.Description;
                existing.Estimate = task.Estimate;
                existing.Priority = task.Priority;
                existing.Status = task.Status;
                existing.DueDate = task.DueDate;
                existing.TaskCode = task.TaskCode;
                existing.IsDeleted = task.IsDeleted;
                existing.DeletedById = task.DeletedById;
                task = existing;
            }

            if (task.AssigneeId is long assigneeId && task.Assignee?.Id != assigneeId)
            {
                task.Assignee = new UserEntity { Id = assigneeId, Name = $"User {assigneeId}" };
            }

            return Task.FromResult(task);
        }

        public Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
        {
            Tasks.RemoveAll(task => task.Id == id);
            return Task.CompletedTask;
        }

        public Task<int> DeleteAllTrashTasksByProjectAndUserAsync(
            long projectId, long userId, CancellationToken cancellationToken)
        {
            LastTrashUserDelete = (projectId, userId);
            DeleteAllByProjectAndUserCount++;
            return Task.FromResult(0);
        }

        public Task<int> DeleteAllTrashTasksByProjectAsync(
            long projectId, CancellationToken cancellationToken)
        {
            DeleteAllTrashTasksByProjectCount++;
            return Task.FromResult(0);
        }
    }

    private sealed class FakeProjectMemberRepository : IProjectMemberRepository
    {
        public List<ProjectMemberEntity> Members { get; } = [];

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);
        public Task<(IReadOnlyList<ProjectMemberEntity> Content, long TotalElements)> GetPageAsync(
            string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken) =>
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

    private sealed class PrincipalCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser GetCurrentUser(ClaimsPrincipal principal)
        {
            var value = principal.FindFirst("userId")?.Value ?? principal.FindFirst("sub")?.Value;
            if (!long.TryParse(value, out var userId))
            {
                throw new UnauthorizedException("Vui lòng đăng nhập để thực hiện thao tác");
            }

            return new CurrentUser(userId, null);
        }
    }

    private sealed class FakeAssignmentNotifier : IAssignmentNotifier
    {
        public List<long> TaskAssignments { get; } = [];
        public Task NotifyTaskAssignmentAsync(long taskId, CancellationToken cancellationToken)
        {
            TaskAssignments.Add(taskId);
            return Task.CompletedTask;
        }

        public Task NotifySubtaskAssignmentAsync(long subtaskId, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeAttachmentRepository : IAttachmentRepository
    {
        public List<AttachmentEntity> Attachments { get; } = [];

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);
        public Task<(IReadOnlyList<AttachmentEntity> Content, long TotalElements)> GetPageAsync(
            string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<AttachmentEntity>)Attachments, (long)Attachments.Count));
        public Task<AttachmentEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Attachments.SingleOrDefault(item => item.Id == id));
        public Task<AttachmentEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);
        public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Attachments.Any(item => item.Id == id));
        public Task<AttachmentEntity> SaveAsync(
            AttachmentEntity attachment, CancellationToken cancellationToken)
        {
            if (attachment.Id == 0)
            {
                attachment.Id = Attachments.Count + 1;
                Attachments.Add(attachment);
            }
            else
            {
                var existing = Attachments.Single(item => item.Id == attachment.Id);
                existing.FileUrl = attachment.FileUrl;
                existing.FileName = attachment.FileName;
                attachment = existing;
            }

            return Task.FromResult(attachment);
        }
        public Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
        {
            Attachments.RemoveAll(item => item.Id == id);
            return Task.CompletedTask;
        }
    }
}
