using Microsoft.AspNetCore.Mvc;
using PRN232.Application.Common;
using PRN232.Application.Projects;
using PRN232.Application.Security;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/v1/projects")]
public sealed class ProjectsController(ProjectService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiSuccessResponse<PageResponse<ProjectResponse>>>> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] string[]? sort = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = PageRequestParser.NormalizePage(page, size);
        var result = await service.GetPageAsync(
            search, normalizedPage.Page, normalizedPage.Size, PageRequestParser.ParseSort(sort), cancellationToken);
        return Ok(ApiSuccessResponse<PageResponse<ProjectResponse>>.Create(
            ToPage(result), "Request successful"));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<ProjectResponse>>> GetById(
        long id, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<ProjectResponse>.Create(
            await service.GetProjectByIdAsync(id, cancellationToken), "Request successful"));

    [HttpPost]
    public async Task<ActionResult<ApiSuccessResponse<ProjectResponse>>> Create(
        [FromBody] CreateProjectRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created,
            ApiSuccessResponse<ProjectResponse>.Create(
                await service.CreateProjectAsync(request, cancellationToken), "Resource created successfully"));

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<ProjectResponse>>> Update(
        long id, [FromBody] UpdateProjectRequest request, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<ProjectResponse>.Create(
            await service.UpdateProjectAsync(id, request, cancellationToken), "Request successful"));

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<object>>> Delete(
        long id, CancellationToken cancellationToken)
    {
        await service.DeleteProjectAsync(id, cancellationToken);
        return Ok(new ApiSuccessResponse<object>(true, "SUCCESS",
            "Operation completed successfully", null, DateTime.Now));
    }

    [HttpGet("my-project")]
    public async Task<ActionResult<ApiSuccessResponse<PageResponse<ProjectResponse>>>> GetMyProjects(
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] string[]? sort = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = PageRequestParser.NormalizePage(page, size);
        var result = await service.GetMyProjectAsync(
            User, normalizedPage.Page, normalizedPage.Size, PageRequestParser.ParseSort(sort), cancellationToken);
        return Ok(ApiSuccessResponse<PageResponse<ProjectResponse>>.Create(
            ToPage(result), "Request successful"));
    }

    private static PageResponse<ProjectResponse> ToPage(ProjectPageResult result)
    {
        var totalPages = result.Size <= 0
            ? 0
            : (int)Math.Ceiling(result.TotalElements / (double)result.Size);
        return new PageResponse<ProjectResponse>(
            result.Content, result.Page, result.Size, result.TotalElements, totalPages,
            result.Page == 0, result.Page >= totalPages - 1);
    }
}
