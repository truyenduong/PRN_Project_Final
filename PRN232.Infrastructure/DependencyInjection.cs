using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using PRN232.Application.Configuration;
using PRN232.Application.Roles;
using PRN232.Application.Security;
using PRN232.Application.Users;
using PRN232.Application.Projects;
using PRN232.Application.ProjectMembers;
using PRN232.Application.ActivityLogs;
using PRN232.Application.Common;
using PRN232.Application.Notifications;
using PRN232.Application.Sprints;
using PRN232.Application.Tasks;
using PRN232.Application.Attachments;
using PRN232.Application.Comments;
using PRN232.Application.Subtasks;
using PRN232.Application.Dashboard;
using PRN232.Infrastructure.Persistence;

namespace PRN232.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContextFactory<AppDbContext>((serviceProvider, options) =>
        {
            var connectionString = configuration.GetConnectionString("WorkflowHub")
                ?? throw new InvalidOperationException("ConnectionStrings:WorkflowHub is not configured.");

            options
                .UseNpgsql(connectionString)
                .UseSnakeCaseNamingConvention();
        });
        services.AddScoped<IAuthRepository, AuthRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IUsersRepository, UsersRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IProjectOwnerMembershipWriter, ProjectOwnerMembershipWriter>();
        services.AddScoped<IProjectMemberRepository, ProjectMemberRepository>();
        services.AddScoped<IActivityLogRepository, ActivityLogRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<ISprintRepository, SprintRepository>();
        services.AddScoped<ITaskRepository, TaskRepository>();
        services.AddScoped<IAttachmentRepository, AttachmentRepository>();
        services.AddScoped<ICommentRepository, CommentRepository>();
        services.AddScoped<ISubtaskRepository, SubtaskRepository>();
        services.AddScoped<IDashboardRepository, DashboardRepository>();
        services.AddScoped<IAssignmentNotifier, AssignmentNotifier>();
        services.AddHttpClient<IGoogleAuthProvider, GoogleAuthProvider>();

        return services;
    }
}
