using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using PRN232.Api.Contracts;
using PRN232.Api.Configuration;
using PRN232.Api.Middleware;
using PRN232.Api.Serialization;
using PRN232.Application;
using PRN232.Application.Configuration;
using PRN232.Application.Security;
using PRN232.Infrastructure;
using Serilog;
using System.Text;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
JavaEnvironmentOverrides.Apply(builder.Configuration);
var jsonOptions = JacksonJsonOptions.Create();

builder.Host.UseSerilog((context, loggerConfiguration) =>
    loggerConfiguration
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console());

builder.Services.AddSingleton(jsonOptions);
builder.Services
    .AddControllers()
    .AddJsonOptions(options => JacksonJsonOptions.Configure(options.JsonSerializerOptions));
builder.Services.Configure<ApiBehaviorOptions>(options =>
    options.InvalidModelStateResponseFactory = GlobalExceptionHandler.CreateValidationResponse);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddApplication(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var corsOptions = builder.Configuration
    .GetSection(CorsOptions.SectionName)
    .Get<CorsOptions>() ?? new CorsOptions
    {
        AllowedOriginPatterns = ["*"],
        AllowedMethods = ["GET", "POST", "PUT", "DELETE", "OPTIONS", "PATCH", "HEAD"],
        AllowedHeaders = ["*"],
        ExposedHeaders = ["Authorization", "Content-Type"],
        AllowCredentials = true
    };
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
{
    policy.SetIsOriginAllowed(origin =>
            corsOptions.AllowedOriginPatterns.Contains("*", StringComparer.Ordinal)
            || corsOptions.AllowedOriginPatterns.Any(pattern =>
                string.Equals(pattern, origin, StringComparison.Ordinal)))
        .WithMethods(corsOptions.AllowedMethods)
        .WithHeaders(corsOptions.AllowedHeaders)
        .WithExposedHeaders(corsOptions.ExposedHeaders);

    if (corsOptions.AllowCredentials)
    {
        policy.AllowCredentials();
    }
}));

var jwtOptions = builder.Configuration
    .GetSection(JwtOptions.SectionName)
    .Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwtOptions.SecretKey) < 32)
{
    throw new InvalidOperationException(
        "Configure Security:Jwt:SecretKey or JWT_SECRET_KEY with at least 32 UTF-8 bytes.");
}

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SecretKey)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            NameClaimType = "sub",
            RoleClaimType = "roles",
            ClockSkew = TimeSpan.Zero
        };
        options.Events = new JwtBearerEvents
        {
            OnChallenge = async context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(
                    ApiErrorResponse.Create(
                        "UNAUTHORIZED",
                        "Authentication required: Full authentication is required to access this resource",
                        context.Request.Path),
                    jsonOptions,
                    context.HttpContext.RequestAborted);
            },
            OnForbidden = async context =>
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(
                    ApiErrorResponse.Create("FORBIDDEN", "Access denied: Access Denied", context.Request.Path),
                    jsonOptions,
                    context.HttpContext.RequestAborted);
            },
            OnTokenValidated = context =>
            {
                const string bearerPrefix = "Bearer ";
                var authorizationHeader = context.Request.Headers.Authorization.ToString();
                var rawToken = authorizationHeader.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
                    ? authorizationHeader[bearerPrefix.Length..].Trim()
                    : null;
                var blacklist = context.HttpContext.RequestServices.GetRequiredService<TokenBlacklist>();
                if (rawToken is not null && blacklist.Contains(rawToken))
                {
                    context.Fail("Token has been revoked");
                }

                return Task.CompletedTask;
            }
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    AddRolePolicy(options, "Admin", "ADMIN", "ROLE_ADMIN");
    AddRolePolicy(options, "User", "User", "USER", "ROLE_USER");
    AddRolePolicy(options, "ProjectManager", "PROJECT_MANAGER", "ROLE_PROJECT_MANAGER");
    AddRolePolicy(options, "Developer", "DEVELOPER", "ROLE_DEVELOPER");
    AddRolePolicy(options, "Tester", "TESTER", "ROLE_TESTER");
    AddRolePolicy(options, "Viewer", "VIEWER", "ROLE_VIEWER");
});

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages(context =>
    StatusCodeErrorWriter.WriteAsync(
        context,
        app.Services.GetRequiredService<JsonSerializerOptions>()));
app.UseSerilogRequestLogging();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static void AddRolePolicy(AuthorizationOptions options, string policyName, params string[] roleNames) =>
    options.AddPolicy(policyName, policy => policy.RequireRole(roleNames));

public partial class Program
{
}
