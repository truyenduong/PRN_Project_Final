using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.Extensions.Options;
using PRN232.Application.Configuration;
using PRN232.Domain.Entities;

namespace PRN232.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IOptions<DatabaseOptions> databaseOptions) : DbContext(options)
{
    public DbSet<UserEntity> Users => Set<UserEntity>();

    public DbSet<ProjectEntity> Projects => Set<ProjectEntity>();

    public DbSet<RoleEntity> Roles => Set<RoleEntity>();

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();

    public DbSet<SubtaskEntity> Subtasks => Set<SubtaskEntity>();

    public DbSet<SprintEntity> Sprints => Set<SprintEntity>();

    public DbSet<ProjectMemberEntity> ProjectMembers => Set<ProjectMemberEntity>();

    public DbSet<CommentEntity> Comments => Set<CommentEntity>();

    public DbSet<AttachmentEntity> Attachments => Set<AttachmentEntity>();

    public DbSet<NotificationEntity> Notifications => Set<NotificationEntity>();

    public DbSet<ActivityLogEntity> ActivityLogs => Set<ActivityLogEntity>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Conventions.Remove(typeof(ForeignKeyIndexConvention));
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(databaseOptions.Value.Schema);

        ConfigureUsers(modelBuilder);
        ConfigureProjects(modelBuilder);
        ConfigureRoles(modelBuilder);
        ConfigureTasks(modelBuilder);
        ConfigureSubtasks(modelBuilder);
        ConfigureSprints(modelBuilder);
        ConfigureProjectMembers(modelBuilder);
        ConfigureComments(modelBuilder);
        ConfigureAttachments(modelBuilder);
        ConfigureNotifications(modelBuilder);
        ConfigureActivityLogs(modelBuilder);

        base.OnModelCreating(modelBuilder);
    }

    public override int SaveChanges()
    {
        ApplyAuditValuesAndNormalizeTimestamps();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyAuditValuesAndNormalizeTimestamps();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplyAuditValuesAndNormalizeTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditValuesAndNormalizeTimestamps();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private static void ConfigureUsers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<UserEntity>();
        ConfigureAudit(entity, subtaskTable: false);
        entity.ToTable("users", "workflow");
        entity.HasKey(x => x.Id).HasName("users_pkey");
        ConfigureSerialId(entity, "id", "users_id_seq", "integer");
        entity.Property(x => x.Email).HasColumnName("email").HasColumnType("character varying(255)").IsRequired();
        entity.HasIndex(x => x.Email).IsUnique().HasDatabaseName("users_email_key");
        entity.Property(x => x.Password).HasColumnName("password").HasColumnType("character varying(255)").IsRequired();
        entity.Property(x => x.Name).HasColumnName("name").HasColumnType("character varying(255)").IsRequired();
        entity.Property(x => x.Avatar).HasColumnName("avatar").HasColumnType("text");
        entity.Property(x => x.Status).HasColumnName("status").HasColumnType("character varying(50)")
            .HasDefaultValueSql("'active'").HasSentinel("active");
        entity.Property(x => x.RoleId).HasColumnName("role_id").HasColumnType("integer");
        entity.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .HasConstraintName("users_role_id_fkey")
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureProjects(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProjectEntity>();
        ConfigureAudit(entity, subtaskTable: false);
        entity.ToTable("projects", "workflow");
        entity.HasKey(x => x.Id).HasName("projects_pkey");
        ConfigureSerialId(entity, "id", "projects_id_seq", "integer");
        entity.Property(x => x.Name).HasColumnName("name").HasColumnType("character varying(255)").IsRequired();
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        entity.Property(x => x.OwnerId).HasColumnName("owner_id").HasColumnType("integer").IsRequired();
        entity.HasOne(x => x.Owner)
            .WithMany()
            .HasForeignKey(x => x.OwnerId)
            .HasConstraintName("projects_owner_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureRoles(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<RoleEntity>();
        ConfigureAudit(entity, subtaskTable: false);
        entity.ToTable("roles", "workflow");
        entity.HasKey(x => x.Id).HasName("roles_pkey");
        ConfigureSerialId(entity, "id", "roles_id_seq", "integer");
        entity.Property(x => x.Name).HasColumnName("name").HasColumnType("character varying(100)").IsRequired();
        entity.HasAlternateKey(x => x.Name).HasName("roles_name_key");
    }

    private static void ConfigureTasks(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<TaskEntity>();
        ConfigureAudit(entity, subtaskTable: false);
        entity.ToTable("tasks", "workflow");
        entity.HasKey(x => x.Id).HasName("tasks_pkey");
        ConfigureSerialId(entity, "id", "tasks_id_seq", "integer");
        entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("integer").IsRequired();
        entity.Property(x => x.SprintId).HasColumnName("sprint_id").HasColumnType("integer");
        entity.Property(x => x.AssigneeId).HasColumnName("assignee_id").HasColumnType("integer");
        entity.Property(x => x.Title).HasColumnName("title").HasColumnType("character varying(255)").IsRequired();
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("text");
        entity.Property(x => x.Priority).HasColumnName("priority").HasColumnType("character varying(50)");
        entity.Property(x => x.Status).HasColumnName("status").HasColumnType("character varying(50)").HasDefaultValueSql("'pending'");
        entity.Property(x => x.DueDate).HasColumnName("due_date").HasColumnType("date");
        entity.Property(x => x.TaskCode).HasColumnName("task_code").HasColumnType("character varying(50)");
        entity.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasColumnType("boolean").HasDefaultValue(false);
        entity.Property(x => x.Estimate).HasColumnName("estimate").HasColumnType("character varying(50)");
        entity.Property(x => x.DeletedById).HasColumnName("deleted_by_id").HasColumnType("bigint");

        entity.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .HasConstraintName("tasks_project_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Sprint)
            .WithMany()
            .HasForeignKey(x => x.SprintId)
            .HasConstraintName("fk_workflow_task_workflow_sprint")
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(x => x.Assignee)
            .WithMany()
            .HasForeignKey(x => x.AssigneeId)
            .HasConstraintName("tasks_assignee_id_fkey")
            .OnDelete(DeleteBehavior.SetNull);
        entity.HasOne(x => x.DeletedBy)
            .WithMany()
            .HasForeignKey(x => x.DeletedById)
            .HasConstraintName("tasks_deleted_by_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
        entity.HasQueryFilter(x => x.IsDeleted != true);
    }

    private static void ConfigureSubtasks(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SubtaskEntity>();
        ConfigureAudit(entity, subtaskTable: true);
        entity.ToTable("subtasks", "workflow");
        entity.HasKey(x => x.Id).HasName("subtasks_pkey");
        ConfigureSerialId(entity, "id", "subtasks_id_seq", "bigint");
        entity.Property(x => x.TaskId).HasColumnName("task_id").HasColumnType("bigint").IsRequired();
        entity.Property(x => x.Title).HasColumnName("title").HasColumnType("character varying(255)").IsRequired();
        entity.Property(x => x.Description).HasColumnName("description").HasColumnType("character varying(400)");
        entity.Property(x => x.IsCompleted).HasColumnName("is_completed").HasColumnType("boolean").HasDefaultValue(false);
        entity.Property(x => x.AssigneeId).HasColumnName("assignee_id").HasColumnType("bigint");
        entity.Property(x => x.Estimate).HasColumnName("estimate").HasColumnType("character varying(50)");
        entity.Property(x => x.Priority).HasColumnName("priority").HasColumnType("character varying(50)").HasDefaultValueSql("'MEDIUM'");
        entity.Property(x => x.DueDate).HasColumnName("due_date").HasColumnType("date");
        entity.HasOne(x => x.Task)
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .HasConstraintName("subtasks_task_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Assignee)
            .WithMany()
            .HasForeignKey(x => x.AssigneeId)
            .HasConstraintName("subtasks_assignee_id_fkey")
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureSprints(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<SprintEntity>();
        ConfigureAudit(entity, subtaskTable: false);
        entity.ToTable("sprints", "workflow");
        entity.HasKey(x => x.Id).HasName("sprints_pkey");
        ConfigureSerialId(entity, "id", "sprints_id_seq", "integer");
        entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("integer").IsRequired();
        entity.Property(x => x.Name).HasColumnName("name").HasColumnType("character varying(255)").IsRequired();
        entity.Property(x => x.Goal).HasColumnName("goal").HasColumnType("text");
        entity.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("date").IsRequired();
        entity.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("date").IsRequired();
        entity.Property(x => x.Status).HasColumnName("status").HasColumnType("character varying(30)").IsRequired();
        entity.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .HasConstraintName("fk_workflow_sprint_workflow_project")
            .OnDelete(DeleteBehavior.NoAction);
    }

    private static void ConfigureProjectMembers(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ProjectMemberEntity>();
        ConfigureAudit(entity, subtaskTable: false);
        entity.ToTable("project_members", "workflow");
        entity.HasKey(x => new { x.ProjectId, x.UserId }).HasName("project_members_pkey");
        entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("integer");
        entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("integer");
        entity.Property(x => x.RoleId).HasColumnName("role_id").HasColumnType("integer");
        entity.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .HasConstraintName("project_members_project_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("project_members_user_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Role)
            .WithMany()
            .HasForeignKey(x => x.RoleId)
            .HasConstraintName("project_members_role_id_fkey")
            .OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigureComments(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<CommentEntity>();
        ConfigureAudit(entity, subtaskTable: false);
        entity.ToTable("comments", "workflow");
        entity.HasKey(x => x.Id).HasName("comments_pkey");
        ConfigureSerialId(entity, "id", "comments_id_seq", "integer");
        entity.Property(x => x.TaskId).HasColumnName("task_id").HasColumnType("integer").IsRequired();
        entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("integer").IsRequired();
        entity.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        entity.HasOne(x => x.Task)
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .HasConstraintName("comments_task_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("comments_user_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureAttachments(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<AttachmentEntity>();
        ConfigureAudit(entity, subtaskTable: false);
        entity.ToTable("attachments", "workflow");
        entity.HasKey(x => x.Id).HasName("attachments_pkey");
        ConfigureSerialId(entity, "id", "attachments_id_seq", "integer");
        entity.Property(x => x.TaskId).HasColumnName("task_id").HasColumnType("integer").IsRequired();
        entity.Property(x => x.FileUrl).HasColumnName("file_url").HasColumnType("text").IsRequired();
        entity.Property(x => x.FileName).HasColumnName("file_name").HasColumnType("character varying(255)").IsRequired();
        entity.HasOne(x => x.Task)
            .WithMany()
            .HasForeignKey(x => x.TaskId)
            .HasConstraintName("attachments_task_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureNotifications(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<NotificationEntity>();
        ConfigureAudit(entity, subtaskTable: false);
        entity.ToTable("notifications", "workflow");
        entity.HasKey(x => x.Id).HasName("notifications_pkey");
        ConfigureSerialId(entity, "id", "notifications_id_seq", "integer");
        entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("integer").IsRequired();
        entity.Property(x => x.Type).HasColumnName("type").HasColumnType("character varying(100)").IsRequired();
        entity.Property(x => x.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        entity.Property(x => x.IsRead).HasColumnName("is_read").HasColumnType("boolean").HasDefaultValue(false);
        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("notifications_user_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureActivityLogs(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ActivityLogEntity>();
        ConfigureAudit(entity, subtaskTable: false);
        entity.ToTable("activity_logs", "workflow");
        entity.HasKey(x => x.Id).HasName("activity_logs_pkey");
        ConfigureSerialId(entity, "id", "activity_logs_id_seq", "integer");
        entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("integer").IsRequired();
        entity.Property(x => x.ProjectId).HasColumnName("project_id").HasColumnType("integer");
        entity.Property(x => x.Action).HasColumnName("action").HasColumnType("character varying(255)").IsRequired();
        entity.HasOne(x => x.User)
            .WithMany()
            .HasForeignKey(x => x.UserId)
            .HasConstraintName("activity_logs_user_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
        entity.HasOne(x => x.Project)
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .HasConstraintName("activity_logs_project_id_fkey")
            .OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureAudit<TEntity>(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity, bool subtaskTable)
        where TEntity : BaseEntity
    {
        entity.Property(x => x.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired(!subtaskTable)
            .ValueGeneratedOnAdd();
        entity.Property(x => x.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp without time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired(!subtaskTable)
            .ValueGeneratedOnAdd();
    }

    private static void ConfigureSerialId<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity,
        string columnName,
        string sequenceName,
        string columnType)
        where TEntity : class
    {
        entity.Property<long>("Id")
            .HasColumnName(columnName)
            .HasColumnType(columnType)
            .HasDefaultValueSql($"nextval('workflow.{sequenceName}'::regclass)")
            .ValueGeneratedOnAdd();
    }

    private void ApplyAuditValuesAndNormalizeTimestamps()
    {
        var now = ToTimestampWithoutTimeZone(DateTime.UtcNow);

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is BaseEntity entity)
            {
                if (entry.State == EntityState.Added)
                {
                    entity.CreatedAt = ToTimestampWithoutTimeZone(entity.CreatedAt ?? now);
                    entity.UpdatedAt = ToTimestampWithoutTimeZone(now);
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                    entity.UpdatedAt = ToTimestampWithoutTimeZone(now);
                }
            }

            foreach (var property in entry.Properties)
            {
                if (property.Metadata.ClrType == typeof(DateTime)
                    || property.Metadata.ClrType == typeof(DateTime?))
                {
                    if (property.CurrentValue is DateTime value)
                    {
                        property.CurrentValue = ToTimestampWithoutTimeZone(value);
                    }
                }
            }
        }
    }

    private static DateTime ToTimestampWithoutTimeZone(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => DateTime.SpecifyKind(value, DateTimeKind.Unspecified),
            DateTimeKind.Local => DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Unspecified),
            _ => value
        };
}
