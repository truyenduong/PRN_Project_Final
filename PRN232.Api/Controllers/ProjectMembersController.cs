using Microsoft.AspNetCore.Mvc;
using PRN232.Application.Common;
using PRN232.Application.ProjectMembers;
using PRN232.Application.Security;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/v1/project-members")]
public sealed class ProjectMembersController(ProjectMemberService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiSuccessResponse<PageResponse<ProjectMemberResponse>>>> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] string[]? sort = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = PageRequestParser.NormalizePage(page, size);
        var result = await service.GetPageAsync(
            search, normalizedPage.Page, normalizedPage.Size, PageRequestParser.ParseSort(sort), cancellationToken);
        return Ok(ApiSuccessResponse<PageResponse<ProjectMemberResponse>>.Create(
            ToPage(result), "Request successful"));
    }

    [HttpGet("projects/{projectId}/users/{userId}")]
    public async Task<ActionResult<ApiSuccessResponse<ProjectMemberResponse>>> GetById(
        long projectId, long userId, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<ProjectMemberResponse>.Create(
            await service.GetByIdAsync(projectId, userId, cancellationToken), "Request successful"));

    [HttpPost]
    public async Task<ActionResult<ApiSuccessResponse<ProjectMemberResponse>>> Create(
        [FromBody] CreateProjectMemberRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created,
            ApiSuccessResponse<ProjectMemberResponse>.Create(
                await service.CreateAsync(request, cancellationToken), "Resource created successfully"));

    [HttpPut("projects/{projectId}/users/{userId}")]
    public async Task<ActionResult<ApiSuccessResponse<ProjectMemberResponse>>> Update(
        long projectId, long userId, [FromBody] UpdateProjectMemberRequest request,
        CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<ProjectMemberResponse>.Create(
            await service.UpdateAsync(projectId, userId, request, cancellationToken), "Request successful"));

    [HttpDelete("projects/{projectId}/users/{userId}")]
    public async Task<ActionResult<ApiSuccessResponse<object>>> Delete(
        long projectId, long userId, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(projectId, userId, User, cancellationToken);
        return Ok(new ApiSuccessResponse<object>(true, "SUCCESS",
            "Operation completed successfully", null, DateTime.Now));
    }

    private static PageResponse<ProjectMemberResponse> ToPage(ProjectMemberPageResult result)
    {
        var totalPages = result.Size <= 0
            ? 0
            : (int)Math.Ceiling(result.TotalElements / (double)result.Size);
        return new PageResponse<ProjectMemberResponse>(
            result.Content, result.Page, result.Size, result.TotalElements, totalPages,
            result.Page == 0, result.Page >= totalPages - 1);
    }
}
