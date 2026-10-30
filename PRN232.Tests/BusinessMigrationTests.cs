using System.Security.Claims;
using PRN232.Application.Common;
using PRN232.Application.ProjectMembers;
using PRN232.Application.Projects;
using PRN232.Application.Roles;
using PRN232.Application.Users;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Tests;

public sealed class BusinessMigrationTests
{
    [Fact]
    public async Task UsersService_CreateUsesDefaultRoleAndSpringCompatibleBcrypt()
    {
        var repository = new FakeUsersRepository();
        repository.Roles.Add(new RoleEntity { Id = 7, Name = "User" });
        var service = new UsersService(repository);

        var response = await service.CreateUserAsync(new CreateUserRequest
        {
            Email = "person@example.com",
            Password = "secret123",
            Name = "Person"
        }, CancellationToken.None);

        var saved = Assert.Single(repository.Users);
        Assert.Equal(7, saved.RoleId);
        Assert.True(BCrypt.Net.BCrypt.Verify("secret123", saved.Password));
        Assert.Equal("User", response.RoleName);
        Assert.Null(saved.Status);
        Assert.DoesNotContain("Password", typeof(UserResponse).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public async Task UsersService_UpdateTreatsEmailCasingAsSameAndPreservesRequestedCasing()
    {
        var repository = new FakeUsersRepository();
        repository.Users.Add(new UserEntity
        {
            Id = 2,
            Email = "person@example.com",
            Password = "existing-hash",
            Name = "Person"
        });
        var service = new UsersService(repository);

        var response = await service.UpdateUserAsync(2, new UpdateUserRequest
        {
            Email = "PERSON@example.com"
        }, CancellationToken.None);

        Assert.Equal("PERSON@example.com", response.Email);
        Assert.Equal(0, repository.EmailExistenceChecks);
    }

    [Fact]
    public async Task UsersService_CreateReportsDuplicateEmail()
    {
        var repository = new FakeUsersRepository();
        repository.Users.Add(new UserEntity { Id = 1, Email = "person@example.com" });

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            new UsersService(repository).CreateUserAsync(new CreateUserRequest
            {
                Email = "person@example.com",
                Password = "secret123",
                Name = "Person"
            }, CancellationToken.None));

        Assert.Equal("Email already exists: person@example.com", exception.Message);
    }

    [Fact]
    public async Task ProjectService_CreateSavesProjectThenOwnerMembershipInOrder()
    {
        var calls = new List<string>();
        var repository = new FakeProjectRepository(calls) { OwnerProjectCount = 2 };
        var roles = new FakeRoleRepository(calls);
        roles.Roles.Add(new RoleEntity { Id = 9, Name = "PROJECT_MANAGER" });
        var writer = new FakeOwnerMembershipWriter(calls);
        var service = new ProjectService(
            repository, writer, roles, new FixedCurrentUserAccessor(4));

        var result = await service.CreateProjectAsync(new CreateProjectRequest
        {
            Name = "Migration",
            OwnerId = 4,
            Description = "Description"
        }, CancellationToken.None);

        Assert.Equal(["count-owner-projects", "find-manager-role", "save-project", "add-owner-membership"], calls);
        Assert.Equal(9, writer.RoleId);
        Assert.Equal(result.Id, writer.ProjectId);
        Assert.Equal(4, writer.OwnerId);
        Assert.Null(result.CurrentUserRole);
    }

    [Fact]
    public async Task ProjectService_CreateRejectsOnlyWhenOwnerAlreadyHasMoreThanFiveProjects()
    {
        var calls = new List<string>();
        var repository = new FakeProjectRepository(calls) { OwnerProjectCount = 6 };
        var service = new ProjectService(
            repository, new FakeOwnerMembershipWriter(calls),
            new FakeRoleRepository(calls), new FixedCurrentUserAccessor(4));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.CreateProjectAsync(new CreateProjectRequest { Name = "No", OwnerId = 4 }, CancellationToken.None));

        Assert.Equal("The owner has created more than five projects", exception.Message);
        Assert.Equal(["count-owner-projects"], calls);
    }

    [Fact]
    public async Task ProjectService_MyProjectUsesMemberRoleAndOwnerFallback()
    {
        var repository = new FakeProjectRepository([])
        {
            MyPage =
            [
                new ProjectForUserResult(
                    new ProjectEntity { Id = 1, Name = "Owned", OwnerId = 4 }, null),
                new ProjectForUserResult(
                    new ProjectEntity { Id = 2, Name = "Member", OwnerId = 8 }, "DEVELOPER")
            ]
        };
        var service = new ProjectService(
            repository, new FakeOwnerMembershipWriter([]), new FakeRoleRepository([]),
            new FixedCurrentUserAccessor(4));

        var result = await service.GetMyProjectAsync(
            new ClaimsPrincipal(), 0, 20, [], CancellationToken.None);

        Assert.Equal("PROJECT_MANAGER", result.Content[0].CurrentUserRole);
        Assert.Equal("DEVELOPER", result.Content[1].CurrentUserRole);
    }

    [Fact]
    public async Task ProjectMemberDeleteChecksSelfBeforeLoadingProject()
    {
        var calls = new List<string>();
        var members = new FakeProjectMemberRepository(calls);
        members.Members.Add(new ProjectMemberEntity { ProjectId = 5, UserId = 11 });
        var projects = new FakeProjectRepository(calls);
        var service = new ProjectMemberService(members, projects, new FixedCurrentUserAccessor(11, calls));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.DeleteAsync(5, 11, new ClaimsPrincipal(), CancellationToken.None));

        Assert.Equal("You cannot remove yourself from the project.", exception.Message);
        Assert.Equal(["exists-target", "get-current-user"], calls);
    }

    [Fact]
    public async Task ProjectMemberResponse_IncludesUserEmailWhenAvailable()
    {
        var members = new FakeProjectMemberRepository([]);
        members.Members.Add(new ProjectMemberEntity
        {
            ProjectId = 5,
            UserId = 11,
            User = new UserEntity
            {
                Id = 11,
                Email = "member@example.com",
                Name = "Member"
            }
        });
        var service = new ProjectMemberService(
            members, new FakeProjectRepository([]), new FixedCurrentUserAccessor(11));

        var response = await service.GetByIdAsync(5, 11, CancellationToken.None);

        Assert.Equal("member@example.com", response.UserEmail);
    }

    [Fact]
    public async Task ProjectMemberDeleteChecksOwnerBeforeCallerAuthorization()
    {
        var calls = new List<string>();
        var members = new FakeProjectMemberRepository(calls);
        members.Members.Add(new ProjectMemberEntity { ProjectId = 5, UserId = 11 });
        var projects = new FakeProjectRepository(calls);
        projects.Projects.Add(new ProjectEntity { Id = 5, OwnerId = 11 });
        var service = new ProjectMemberService(members, projects, new FixedCurrentUserAccessor(12, calls));

        var exception = await Assert.ThrowsAsync<BadRequestException>(() =>
            service.DeleteAsync(5, 11, new ClaimsPrincipal(), CancellationToken.None));

        Assert.Equal("Unable to remove Owner from project", exception.Message);
        Assert.Equal(["exists-target", "get-current-user", "get-project"], calls);
    }

    [Fact]
    public async Task ProjectMemberDeleteAllowsProjectManagerAndPreservesCheckOrder()
    {
        var calls = new List<string>();
        var members = new FakeProjectMemberRepository(calls);
        members.Members.Add(new ProjectMemberEntity { ProjectId = 5, UserId = 11 });
        members.Members.Add(new ProjectMemberEntity
        {
            ProjectId = 5,
            UserId = 12,
            Role = new RoleEntity { Name = "project_manager" }
        });
        var projects = new FakeProjectRepository(calls);
        projects.Projects.Add(new ProjectEntity { Id = 5, OwnerId = 99 });
        var service = new ProjectMemberService(members, projects, new FixedCurrentUserAccessor(12, calls));

        await service.DeleteAsync(5, 11, new ClaimsPrincipal(), CancellationToken.None);

        Assert.Equal(["exists-target", "get-current-user", "get-project", "get-caller-membership", "delete-member"], calls);
    }

    [Fact]
    public async Task ProjectMemberDeleteUsesExactAdminAuthorityLiteral()
    {
        var calls = new List<string>();
        var members = new FakeProjectMemberRepository(calls);
        members.Members.Add(new ProjectMemberEntity { ProjectId = 5, UserId = 11 });
        var projects = new FakeProjectRepository(calls);
        projects.Projects.Add(new ProjectEntity { Id = 5, OwnerId = 99 });
        var service = new ProjectMemberService(members, projects, new FixedCurrentUserAccessor(12, calls));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("roles", "ADMIN")], "test"));

        await service.DeleteAsync(5, 11, principal, CancellationToken.None);

        Assert.Equal(["exists-target", "get-current-user", "get-project", "delete-member"], calls);
    }

    [Fact]
    public async Task ProjectMemberDeleteDoesNotTreatRolePrefixAsAdminForThisJavaCheck()
    {
        var calls = new List<string>();
        var members = new FakeProjectMemberRepository(calls);
        members.Members.Add(new ProjectMemberEntity { ProjectId = 5, UserId = 11 });
        var projects = new FakeProjectRepository(calls);
        projects.Projects.Add(new ProjectEntity { Id = 5, OwnerId = 99 });
        var service = new ProjectMemberService(members, projects, new FixedCurrentUserAccessor(12, calls));
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim("roles", "ROLE_ADMIN")], "test"));

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            service.DeleteAsync(5, 11, principal, CancellationToken.None));

        Assert.Equal(
            ["exists-target", "get-current-user", "get-project", "get-caller-membership"],
            calls);
    }

    [Fact]
    public async Task ProjectMemberCreatePreservesSpringSaveBehaviorForExistingCompositeKey()
    {
        var calls = new List<string>();
        var repository = new FakeProjectMemberRepository(calls);
        repository.Members.Add(new ProjectMemberEntity
        {
            ProjectId = 5,
            UserId = 11,
            RoleId = 2,
            CreatedAt = DateTime.UtcNow
        });
        var service = new ProjectMemberService(
            repository, new FakeProjectRepository(calls), new FixedCurrentUserAccessor(99, calls));

        var response = await service.CreateAsync(new CreateProjectMemberRequest
        {
            ProjectId = 5,
            UserId = 11,
            RoleId = 3
        }, CancellationToken.None);

        Assert.Single(repository.Members);
        Assert.Equal(3, response.RoleId);
        Assert.Empty(calls);
    }

    private sealed class FakeUsersRepository : IUsersRepository
    {
        public List<UserEntity> Users { get; } = [];
        public List<RoleEntity> Roles { get; } = [];
        public int EmailExistenceChecks { get; private set; }

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<(IReadOnlyList<UserEntity> Content, long TotalElements)> GetPageAsync(
            string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<UserEntity>)Users, (long)Users.Count));

        public Task<UserEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Users.SingleOrDefault(user => user.Id == id));

        public Task<UserEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Users.SingleOrDefault(user => user.Id == id));

        public Task<UserEntity?> FindByEmailAsync(string email, CancellationToken cancellationToken) =>
            Task.FromResult(Users.SingleOrDefault(user => user.Email == email));

        public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Users.Any(user => user.Id == id));

        public Task<bool> ExistsByEmailAsync(string email, CancellationToken cancellationToken)
        {
            EmailExistenceChecks++;
            return Task.FromResult(Users.Any(user => user.Email == email));
        }

        public Task<RoleEntity?> FindRoleByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(Roles.SingleOrDefault(role => role.Name == name));

        public Task<UserEntity> SaveAsync(UserEntity user, CancellationToken cancellationToken)
        {
            if (user.Id == 0)
            {
                user.Id = Users.Count + 1;
                Users.Add(user);
            }

            user.Role = Roles.SingleOrDefault(role => role.Id == user.RoleId);
            return Task.FromResult(user);
        }

        public Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
        {
            Users.RemoveAll(user => user.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRoleRepository(List<string> calls) : IRoleRepository
    {
        public List<RoleEntity> Roles { get; } = [];

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<(IReadOnlyList<RoleEntity> Content, long TotalElements)> GetPageAsync(
            string? search, int page, int size, IReadOnlyList<RoleSort> sort, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<RoleEntity>)Roles, (long)Roles.Count));

        public Task<RoleEntity?> FindByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Roles.SingleOrDefault(role => role.Id == id));

        public Task<RoleEntity?> FindByNameAsync(string name, CancellationToken cancellationToken)
        {
            calls.Add("find-manager-role");
            return Task.FromResult(Roles.SingleOrDefault(role => role.Name == name));
        }

        public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Roles.Any(role => role.Id == id));

        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(Roles.Any(role => role.Name == name));

        public Task<RoleEntity> SaveAsync(RoleEntity role, CancellationToken cancellationToken) =>
            Task.FromResult(role);

        public Task DeleteByIdAsync(long id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeProjectRepository(List<string> calls) : IProjectRepository
    {
        public List<ProjectEntity> Projects { get; } = [];
        public long OwnerProjectCount { get; init; }
        public IReadOnlyList<ProjectForUserResult> MyPage { get; init; } = [];

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<(IReadOnlyList<ProjectEntity> Content, long TotalElements)> GetPageAsync(
            string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<ProjectEntity>)Projects, (long)Projects.Count));

        public Task<(IReadOnlyList<ProjectForUserResult> Content, long TotalElements)> GetMyPageAsync(
            long userId, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken) =>
            Task.FromResult((MyPage, (long)MyPage.Count));

        public Task<ProjectEntity?> FindByIdAsync(long id, CancellationToken cancellationToken)
        {
            calls.Add("get-project");
            return Task.FromResult(Projects.SingleOrDefault(project => project.Id == id));
        }

        public Task<ProjectEntity?> FindByIdForUpdateAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Projects.SingleOrDefault(project => project.Id == id));

        public Task<long> CountByOwnerIdAsync(long ownerId, CancellationToken cancellationToken)
        {
            calls.Add("count-owner-projects");
            return Task.FromResult(OwnerProjectCount);
        }

        public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Projects.Any(project => project.Id == id));

        public Task<ProjectEntity> SaveAsync(ProjectEntity project, CancellationToken cancellationToken)
        {
            calls.Add("save-project");
            project.Id = Projects.Count + 1;
            project.Owner = new UserEntity { Id = project.OwnerId, Name = "Owner" };
            Projects.Add(project);
            return Task.FromResult(project);
        }

        public Task DeleteByIdAsync(long id, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeOwnerMembershipWriter(List<string> calls) : IProjectOwnerMembershipWriter
    {
        public long ProjectId { get; private set; }
        public long OwnerId { get; private set; }
        public long RoleId { get; private set; }

        public Task AddOwnerAsProjectManagerAsync(
            long projectId, long ownerId, long roleId, CancellationToken cancellationToken)
        {
            calls.Add("add-owner-membership");
            ProjectId = projectId;
            OwnerId = ownerId;
            RoleId = roleId;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeProjectMemberRepository(List<string> calls) : IProjectMemberRepository
    {
        public List<ProjectMemberEntity> Members { get; } = [];

        public Task<T> ExecuteInTransactionAsync<T>(
            Func<CancellationToken, Task<T>> operation, CancellationToken cancellationToken) =>
            operation(cancellationToken);

        public Task<(IReadOnlyList<ProjectMemberEntity> Content, long TotalElements)> GetPageAsync(
            string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken) =>
            Task.FromResult(((IReadOnlyList<ProjectMemberEntity>)Members, (long)Members.Count));

        public Task<ProjectMemberEntity?> FindByIdAsync(
            long projectId, long userId, CancellationToken cancellationToken)
        {
            calls.Add("get-caller-membership");
            return Task.FromResult(Members.SingleOrDefault(
                member => member.ProjectId == projectId && member.UserId == userId));
        }

        public Task<ProjectMemberEntity?> FindByIdForUpdateAsync(
            long projectId, long userId, CancellationToken cancellationToken) =>
            Task.FromResult(Members.SingleOrDefault(
                member => member.ProjectId == projectId && member.UserId == userId));

        public Task<bool> ExistsByIdAsync(long projectId, long userId, CancellationToken cancellationToken)
        {
            calls.Add("exists-target");
            return Task.FromResult(Members.Any(
                member => member.ProjectId == projectId && member.UserId == userId));
        }

        public Task<ProjectMemberEntity> SaveAsync(ProjectMemberEntity member, CancellationToken cancellationToken)
        {
            var existing = Members.SingleOrDefault(item =>
                item.ProjectId == member.ProjectId && item.UserId == member.UserId);
            if (existing is not null)
            {
                existing.RoleId = member.RoleId;
                return Task.FromResult(existing);
            }

            Members.Add(member);
            return Task.FromResult(member);
        }

        public Task DeleteByIdAsync(long projectId, long userId, CancellationToken cancellationToken)
        {
            calls.Add("delete-member");
            Members.RemoveAll(member => member.ProjectId == projectId && member.UserId == userId);
            return Task.CompletedTask;
        }

        public Task<long> CountProjectsByUserIdAsync(long userId, CancellationToken cancellationToken) =>
            Task.FromResult((long)Members.Count(member => member.UserId == userId));

        public Task<long> CountMembersByProjectIdAsync(long projectId, CancellationToken cancellationToken) =>
            Task.FromResult((long)Members.Count(member => member.ProjectId == projectId));
    }

    private sealed class FixedCurrentUserAccessor(long userId, List<string>? calls = null) : ICurrentUserAccessor
    {
        public CurrentUser GetCurrentUser(ClaimsPrincipal principal)
        {
            calls?.Add("get-current-user");
            return new CurrentUser(userId, null);
        }
    }
}
