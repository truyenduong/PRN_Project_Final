using Microsoft.AspNetCore.Mvc;
using PRN232.Application.Common;
using PRN232.Application.Dashboard;
using PRN232.Application.Security;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/v1/dashboard")]
public sealed class DashboardController(
    DashboardService service,
    ICurrentUserAccessor currentUserAccessor) : ControllerBase
{
    [HttpGet("user")]
    public async Task<ActionResult<ApiSuccessResponse<UserDashboardResponse>>> GetMyDashboard(
        CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<UserDashboardResponse>.Create(
            await service.GetUserDashboardAsync(
                currentUserAccessor.GetCurrentUser(User).Id, cancellationToken),
            "Request successful"));

    [HttpGet("project/{projectId}")]
    public async Task<ActionResult<ApiSuccessResponse<ProjectDashboardResponse>>> GetProjectDashboard(
        long projectId, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<ProjectDashboardResponse>.Create(
            await service.GetProjectDashboardAsync(projectId, cancellationToken),
            "Request successful"));
}
