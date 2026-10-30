using System.Security.Claims;
using PRN232.Application.Common;
using PRN232.Application.ProjectMembers;
using PRN232.Application.Sprints;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Tests;

public sealed class SprintMigrationTests
{
    [Fact]
    public async Task UpdateAsync_AllowsProjectOwnerAndRetainsJavaProjectIdUpdateBehavior()
    {
        var repository = CreateRepository();
        var service = new SprintService(
            repository, new FakeProjectMemberRepository(), new FixedCurrentUserAccessor(11));

        var response = await service.UpdateAsync(
            4,
            new UpdateSprintRequest { ProjectId = 99, Name = "Updated" },
            new ClaimsPrincipal(),
            CancellationToken.None);

        Assert.Equal(99, response.ProjectId);
        Assert.Null(response.ProjectName);
        Assert.Equal("Updated", response.Name);
    }

    [Fact]
    public async Task UpdateAsync_AllowsProjectManagerMember()
    {
        var repository = CreateRepository();
        var members = new FakeProjectMemberRepository();
        members.Members.Add(new ProjectMemberEntity
        {
            ProjectId = 5,
            UserId = 17,
            Role = new RoleEntity { Name = "project_manager" }
        });
        var service = new SprintService(repository, members, new FixedCurrentUserAccessor(17));

        var response = await service.UpdateAsync(
            4, new UpdateSprintRequest { Status = "ACTIVE" },
            new ClaimsPrincipal(), CancellationToken.None);

        Assert.Equal("ACTIVE", response.Status);
    }

    [Fact]
    public async Task UpdateAsync_DoesNotAllowAdminBypass()
    {
        var repository = CreateRepository();
        var members = new FakeProjectMemberRepository();
        members.Members.Add(new ProjectMemberEntity
        {
            ProjectId = 5,
            UserId = 19,
            Role = new RoleEntity { Name = "DEVELOPER" }
        });
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("roles", "ADMIN")], "test"));
        var service = new SprintService(repository, members, new FixedCurrentUserAccessor(19));

        var exception = await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.UpdateAsync(4, new UpdateSprintRequest { Name = "Rejected" },
                principal, CancellationToken.None));

        Assert.Equal("Chỉ Project Manager của dự án mới có quyền cập nhật Sprint", exception.Message);
        Assert.Equal("Planning", repository.Sprints[0].Name);
    }

    [Fact]
    public async Task CreateAndDeleteAsync_DoNotCheckPermissions()
    {
        var repository = CreateRepository();
        var members = new FakeProjectMemberRepository();
        var service = new SprintService(repository, members, new ThrowingCurrentUserAccessor());

        var created = await service.CreateAsync(new CreateSprintRequest
        {
            ProjectId = 5,
            Name = "Unrestricted",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 14),
            Status = "PLANNED"
        }, CancellationToken.None);
        await service.DeleteAsync(created.Id, CancellationToken.None);

        Assert.Empty(members.Members);
        Assert.Equal(4, Assert.Single(repository.Sprints).Id);
    }

    private static FakeSprintRepository CreateRepository()
    {
        var repository = new FakeSprintRepository();
        repository.Projects.Add(new ProjectEntity
        {
            Id = 5,
            Name = "Current",
            OwnerId = 11
        });
        repository.Projects.Add(new ProjectEntity
        {
            Id = 99,
            Name = "Destination",
            OwnerId = 99
        });
        repository.Sprints.Add(new SprintEntity
        {
            Id = 4,
            ProjectId = 5,
            Project = repository.Projects[0],
            Name = "Planning",
            StartDate = new DateOnly(2026, 10, 1),
            EndDate = new DateOnly(2026, 10, 14),
            Status = "PLANNED"
        });
        return repository;
    }

    private sealed class FakeSprintRepository : ISprintRepository
    {
        public List<SprintEntity> Sprints { get; } = [];
        public List<ProjectEntity> Projects { get; } = [];

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<(IReadOnlyList<SprintEntity> Content, long TotalElements)> GetPageAsync(
            string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<SprintEntity>)Sprints, (long)Sprints.Count));

        public Task<SprintEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Sprints.SingleOrDefault(item => item.Id == id));

        public Task<SprintEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
            FindByIdAsync(id, cancellationToken);

        public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Sprints.Any(item => item.Id == id));

        public Task<SprintEntity> SaveAsync(SprintEntity sprint, CancellationToken cancellationToken)
        {
            if (sprint.Id == 0)
            {
                sprint.Id = Sprints.Count + 1;
                Sprints.Add(sprint);
            }

            sprint.Project = Projects.Single(project => project.Id == sprint.ProjectId);
            return Task.FromResult(sprint);
        }

        public Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
        {
            Sprints.RemoveAll(sprint => sprint.Id == id);
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

    private sealed class FixedCurrentUserAccessor(long userId) : ICurrentUserAccessor
    {
        public CurrentUser GetCurrentUser(ClaimsPrincipal principal) => new(userId, null);
    }

    private sealed class ThrowingCurrentUserAccessor : ICurrentUserAccessor
    {
        public CurrentUser GetCurrentUser(ClaimsPrincipal principal) =>
            throw new InvalidOperationException("The create/delete flow must not resolve the caller.");
    }
}
