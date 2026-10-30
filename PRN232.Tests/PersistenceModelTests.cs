using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PRN232.Application.Configuration;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Persistence;

namespace PRN232.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public void Model_MapsAllExistingWorkflowTablesAndTimestampColumns()
    {
        using var context = CreateContext();

        var expectedTables = new Dictionary<Type, string>
        {
            [typeof(UserEntity)] = "users",
            [typeof(ProjectEntity)] = "projects",
            [typeof(RoleEntity)] = "roles",
            [typeof(TaskEntity)] = "tasks",
            [typeof(SubtaskEntity)] = "subtasks",
            [typeof(SprintEntity)] = "sprints",
            [typeof(ProjectMemberEntity)] = "project_members",
            [typeof(CommentEntity)] = "comments",
            [typeof(AttachmentEntity)] = "attachments",
            [typeof(NotificationEntity)] = "notifications",
            [typeof(ActivityLogEntity)] = "activity_logs"
        };

        foreach (var (clrType, tableName) in expectedTables)
        {
            var entityType = context.Model.FindEntityType(clrType);
            Assert.NotNull(entityType);
            Assert.Equal(tableName, entityType!.GetTableName());
            Assert.Equal("workflow", entityType.GetSchema());
            Assert.Equal("timestamp without time zone", entityType.FindProperty(nameof(BaseEntity.CreatedAt))!.GetColumnType());
            Assert.Equal("timestamp without time zone", entityType.FindProperty(nameof(BaseEntity.UpdatedAt))!.GetColumnType());
        }

        Assert.All(context.Model.GetEntityTypes().Where(entityType => entityType.ClrType != typeof(UserEntity)),
            entityType => Assert.Empty(entityType.GetIndexes()));
        Assert.Contains(context.Model.FindEntityType(typeof(UserEntity))!.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "users_email_key");
    }

    [Fact]
    public void Model_MapsSerialSequencesCompositeKeyAndSoftDeleteFilter()
    {
        using var context = CreateContext();

        var user = context.Model.FindEntityType(typeof(UserEntity))!;
        Assert.Equal("integer", user.FindProperty(nameof(UserEntity.Id))!.GetColumnType());
        Assert.Equal("nextval('workflow.users_id_seq'::regclass)", user.FindProperty(nameof(UserEntity.Id))!.GetDefaultValueSql());
        Assert.Contains(user.GetIndexes(), index =>
            index.IsUnique && index.GetDatabaseName() == "users_email_key");

        var subtask = context.Model.FindEntityType(typeof(SubtaskEntity))!;
        Assert.Equal("bigint", subtask.FindProperty(nameof(SubtaskEntity.Id))!.GetColumnType());
        Assert.Equal("nextval('workflow.subtasks_id_seq'::regclass)", subtask.FindProperty(nameof(SubtaskEntity.Id))!.GetDefaultValueSql());

        var projectMember = context.Model.FindEntityType(typeof(ProjectMemberEntity))!;
        Assert.Equal(
            new[] { nameof(ProjectMemberEntity.ProjectId), nameof(ProjectMemberEntity.UserId) },
            projectMember.FindPrimaryKey()!.Properties.Select(property => property.Name));

        var task = context.Model.FindEntityType(typeof(TaskEntity))!;
        Assert.NotNull(task.GetQueryFilter());
    }

    [Fact]
    public void ForeignKeys_UseDeleteActionsFromSqlMigrations()
    {
        using var context = CreateContext();

        var projectMember = context.Model.FindEntityType(typeof(ProjectMemberEntity))!;
        Assert.Contains(projectMember.GetForeignKeys(), foreignKey =>
            foreignKey.GetConstraintName() == "project_members_project_id_fkey"
            && foreignKey.DeleteBehavior == DeleteBehavior.Cascade);
        Assert.Contains(projectMember.GetForeignKeys(), foreignKey =>
            foreignKey.GetConstraintName() == "project_members_role_id_fkey"
            && foreignKey.DeleteBehavior == DeleteBehavior.SetNull);

        var task = context.Model.FindEntityType(typeof(TaskEntity))!;
        Assert.Contains(task.GetForeignKeys(), foreignKey =>
            foreignKey.GetConstraintName() == "fk_workflow_task_workflow_sprint"
            && foreignKey.DeleteBehavior == DeleteBehavior.SetNull);
        Assert.Equal("bigint", task.FindProperty(nameof(TaskEntity.DeletedById))!.GetColumnType());
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=workflowhub_model_test;Username=unused;Password=unused")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, Options.Create(new DatabaseOptions()));
    }
}
