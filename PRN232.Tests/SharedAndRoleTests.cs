using System.Security.Claims;
using PRN232.Application.Common;
using PRN232.Application.Roles;
using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;
using PRN232.Infrastructure.Persistence;
using System.Linq.Expressions;

namespace PRN232.Tests;

public sealed class SharedAndRoleTests
{
    [Fact]
    public void CurrentUserAccessor_UsesUserIdAndEmailClaims()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("userId", "42"),
            new Claim("email", "person@example.com")
        ]));

        var user = new CurrentUserAccessor().GetCurrentUser(principal);

        Assert.Equal(42, user.Id);
        Assert.Equal("person@example.com", user.Email);
    }

    [Fact]
    public void CurrentUserAccessor_RejectsMissingIdentity()
    {
        Assert.Throws<UnauthorizedException>(() =>
            new CurrentUserAccessor().GetCurrentUser(new ClaimsPrincipal()));
    }

    [Fact]
    public void RoleMatcher_IsCaseInsensitiveWithoutRemovingRolePrefix()
    {
        Assert.True(RoleMatcher.HasAny(["role_admin"], "ROLE_ADMIN"));
        Assert.False(RoleMatcher.HasAny(["ADMIN"], "ROLE_ADMIN"));
        Assert.Equal(RoleEnums.User, RoleEnumExtensions.FromString("USER"));
        Assert.Equal(RoleEnums.User, RoleEnumExtensions.FromString("user"));
        Assert.Equal(RoleEnums.ProjectManager, RoleEnumExtensions.FromString("project_manager"));
        Assert.Null(RoleEnumExtensions.FromString("ProjectManager"));
    }

    [Fact]
    public void RsqlFilter_AppliesWhitelistedEqualityAndBooleanOperators()
    {
        var roles = new[]
        {
            new RoleEntity { Name = "ADMIN" },
            new RoleEntity { Name = "USER" },
            new RoleEntity { Name = "VIEWER" }
        }.AsQueryable();
        IReadOnlyDictionary<string, LambdaExpression> fields =
            new Dictionary<string, LambdaExpression>
            {
                ["name"] = (Expression<Func<RoleEntity, string>>)(role => role.Name)
            };

        var result = RsqlFilter<RoleEntity>.Apply(roles, "name==ADMIN,name==USER", fields).ToArray();

        Assert.Equal(2, result.Length);
    }

    [Fact]
    public void RsqlFilter_RejectsUnknownFieldsWithDedicatedError()
    {
        IReadOnlyDictionary<string, LambdaExpression> fields =
            new Dictionary<string, LambdaExpression>
            {
                ["name"] = (Expression<Func<RoleEntity, string>>)(role => role.Name)
            };

        Assert.Throws<InvalidRsqlException>(() =>
            RsqlFilter<RoleEntity>.Apply(
                new[] { new RoleEntity { Name = "ADMIN" } }.AsQueryable(),
                "password==secret",
                fields));
    }

    [Fact]
    public void RsqlFilter_ReportsJavaCompatibleUnknownFieldMessage()
    {
        IReadOnlyDictionary<string, LambdaExpression> fields =
            new Dictionary<string, LambdaExpression>
            {
                ["name"] = (Expression<Func<RoleEntity, string>>)(role => role.Name)
            };

        var exception = Assert.Throws<InvalidRsqlException>(() =>
            RsqlFilter<RoleEntity>.Apply(
                new[] { new RoleEntity { Name = "ADMIN" } }.AsQueryable(),
                "privateField==value",
                fields));

        Assert.Equal("Unknown or unsearchable field 'privateField' in RSQL query", exception.Message);
    }

    [Fact]
    public void RsqlFilter_SupportsInForNullableProperties()
    {
        var tasks = new[]
        {
            new TaskEntity { AssigneeId = 2 },
            new TaskEntity { AssigneeId = 5 },
            new TaskEntity { AssigneeId = null }
        }.AsQueryable();
        IReadOnlyDictionary<string, LambdaExpression> fields =
            new Dictionary<string, LambdaExpression>
            {
                ["assigneeId"] = (Expression<Func<TaskEntity, long?>>)(task => task.AssigneeId)
            };

        var result = RsqlFilter<TaskEntity>.Apply(tasks, "assigneeId=in=(2,5)", fields).ToArray();

        Assert.Equal(2, result.Length);
    }

    [Fact]
    public void RsqlFilter_MatchesSqlNullSemanticsForNullableInOutAndNotEqual()
    {
        var tasks = new[]
        {
            new TaskEntity { AssigneeId = 2 },
            new TaskEntity { AssigneeId = 5 },
            new TaskEntity { AssigneeId = null }
        }.AsQueryable();
        IReadOnlyDictionary<string, LambdaExpression> fields =
            new Dictionary<string, LambdaExpression>
            {
                ["assigneeId"] = (Expression<Func<TaskEntity, long?>>)(task => task.AssigneeId)
            };

        Assert.Equal([5L], RsqlFilter<TaskEntity>.Apply(tasks, "assigneeId=out=(2)", fields)
            .Select(task => task.AssigneeId!.Value).ToArray());
        Assert.Equal([5L], RsqlFilter<TaskEntity>.Apply(tasks, "assigneeId!=2", fields)
            .Select(task => task.AssigneeId!.Value).ToArray());
        Assert.Single(RsqlFilter<TaskEntity>.Apply(tasks, "assigneeId==null", fields));
        Assert.Empty(RsqlFilter<TaskEntity>.Apply(tasks, "assigneeId=in=(null)", fields));
        Assert.Empty(RsqlFilter<TaskEntity>.Apply(tasks, "assigneeId=out=(2,null)", fields));
    }

    [Theory]
    [InlineData("user@example.com", true)]
    [InlineData("invalid", false)]
    [InlineData("+12345678901", true)]
    [InlineData("123", false)]
    public void ValidationUtils_MatchesJavaPatterns(string value, bool expected)
    {
        Assert.Equal(expected, ValidationUtils.IsEmail(value) || ValidationUtils.IsPhone(value));
    }

    [Fact]
    public async Task RoleService_CreateRejectsExactDuplicateName()
    {
        var repository = new FakeRoleRepository();
        repository.Roles.Add(new RoleEntity { Id = 1, Name = "ADMIN" });
        var service = new RoleService(repository);

        var exception = await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateRoleAsync(new CreateRoleRequest("ADMIN"), CancellationToken.None));

        Assert.Equal("Role already exists with name: ADMIN", exception.Message);
    }

    [Fact]
    public async Task RoleService_UpdateSetsNameWhenOnlyCasingChanges()
    {
        var repository = new FakeRoleRepository();
        repository.Roles.Add(new RoleEntity { Id = 1, Name = "ADMIN" });
        var service = new RoleService(repository);

        var result = await service.UpdateRoleAsync(1, new UpdateRoleRequest("admin"), CancellationToken.None);

        Assert.Equal("admin", result.Name);
    }

    private sealed class FakeRoleRepository : IRoleRepository
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

        public Task<RoleEntity?> FindByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(Roles.SingleOrDefault(role => role.Name == name));

        public Task<bool> ExistsByIdAsync(long id, CancellationToken cancellationToken) =>
            Task.FromResult(Roles.Any(role => role.Id == id));

        public Task<bool> ExistsByNameAsync(string name, CancellationToken cancellationToken) =>
            Task.FromResult(Roles.Any(role => role.Name == name));

        public Task<RoleEntity> SaveAsync(RoleEntity role, CancellationToken cancellationToken)
        {
            if (role.Id == 0)
            {
                role.Id = Roles.Count + 1;
                Roles.Add(role);
            }

            return Task.FromResult(role);
        }

        public Task DeleteByIdAsync(long id, CancellationToken cancellationToken)
        {
            Roles.RemoveAll(role => role.Id == id);
            return Task.CompletedTask;
        }
    }
}
