using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.Configuration;
using PRN232.Application.Common;
using PRN232.Application.Roles;
using PRN232.Application.Security;
using PRN232.Application.Users;
using PRN232.Application.Projects;
using PRN232.Application.ProjectMembers;
using PRN232.Application.ActivityLogs;
using PRN232.Application.Notifications;
using PRN232.Application.Sprints;
using PRN232.Application.Tasks;
using PRN232.Application.Attachments;
using PRN232.Application.Comments;
using PRN232.Application.Subtasks;
using PRN232.Application.Dashboard;
using System.Text;

namespace PRN232.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<DatabaseOptions>().BindConfiguration(DatabaseOptions.SectionName);
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .Validate(options => Encoding.UTF8.GetByteCount(options.SecretKey) >= 32,
                "Security:Jwt:SecretKey must contain at least 32 UTF-8 bytes.")
            .Validate(options => options.AccessTokenExpiration > 0 && options.RefreshTokenExpiration > 0,
                "JWT expirations must be positive milliseconds.")
            .ValidateOnStart();
        services.AddOptions<CorsOptions>().BindConfiguration(CorsOptions.SectionName);
        services.AddOptions<GoogleOptions>().BindConfiguration(GoogleOptions.SectionName);
        services.AddOptions<FrontendOptions>().BindConfiguration(FrontendOptions.SectionName);
        services.AddOptions<RedisOptions>().BindConfiguration(RedisOptions.SectionName);
        services.AddOptions<KafkaOptions>().BindConfiguration(KafkaOptions.SectionName);
        services.AddOptions<PaginationOptions>().BindConfiguration(PaginationOptions.SectionName);
        services.AddScoped<JwtTokenService>();
        services.AddSingleton<TokenBlacklist>();
        services.AddScoped<AuthService>();
        services.AddScoped<ICurrentUserAccessor, CurrentUserAccessor>();
        services.AddScoped<RoleService>();
        services.AddScoped<UsersService>();
        services.AddScoped<ProjectService>();
        services.AddScoped<ProjectMemberService>();
        services.AddScoped<ActivityLogService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<SprintService>();
        services.AddScoped<TaskService>();
        services.AddScoped<AttachmentService>();
        services.AddScoped<CommentService>();
        services.AddScoped<SubtaskService>();
        services.AddScoped<DashboardService>();

        return services;
    }
}
