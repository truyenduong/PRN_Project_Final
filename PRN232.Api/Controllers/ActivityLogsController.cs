using Microsoft.AspNetCore.Mvc;
using PRN232.Api.Contracts;
using PRN232.Application.ActivityLogs;
using PRN232.Application.Common;
using PRN232.Application.Security;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/v1/activity-logs")]
public sealed class ActivityLogsController(ActivityLogService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiSuccessResponse<PageResponse<ActivityLogResponse>>>> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] string[]? sort = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = PageRequestParser.NormalizePage(page, size);
        var result = await service.GetPageAsync(
            search, normalizedPage.Page, normalizedPage.Size, PageRequestParser.ParseSort(sort), cancellationToken);
        var totalPages = result.Size <= 0
            ? 0
            : (int)Math.Ceiling(result.TotalElements / (double)result.Size);
        var data = new PageResponse<ActivityLogResponse>(
            result.Content, result.Page, result.Size, result.TotalElements, totalPages,
            result.Page == 0, result.Page >= totalPages - 1);
        return Ok(ApiSuccessResponse<PageResponse<ActivityLogResponse>>.Create(data, "Request successful"));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<ActivityLogResponse>>> GetById(
        long id, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<ActivityLogResponse>.Create(
            await service.GetByIdAsync(id, cancellationToken), "Request successful"));

    [HttpPost]
    public async Task<ActionResult<ApiSuccessResponse<ActivityLogResponse>>> Create(
        [FromBody] CreateActivityLogRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created,
            ApiSuccessResponse<ActivityLogResponse>.Create(
                await service.CreateAsync(request, cancellationToken), "Resource created successfully"));

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<ActivityLogResponse>>> Update(
        long id, [FromBody] UpdateActivityLogRequest request, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<ActivityLogResponse>.Create(
            await service.UpdateAsync(id, request, cancellationToken), "Request successful"));

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<object>>> Delete(
        long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return Ok(new ApiSuccessResponse<object>(true, "SUCCESS",
            "Operation completed successfully", null, DateTime.Now));
    }
}
