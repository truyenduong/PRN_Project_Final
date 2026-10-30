using Microsoft.AspNetCore.Mvc;
using PRN232.Application.Common;
using PRN232.Application.Security;
using PRN232.Application.Subtasks;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/v1/subtasks")]
public sealed class SubtasksController(SubtaskService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiSuccessResponse<PageResponse<SubtaskResponse>>>> GetPage(
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
        var data = new PageResponse<SubtaskResponse>(
            result.Content, result.Page, result.Size, result.TotalElements, totalPages,
            result.Page == 0, result.Page >= totalPages - 1);
        return Ok(ApiSuccessResponse<PageResponse<SubtaskResponse>>.Create(data, "Request successful"));
    }

    [HttpGet("task/{taskId}")]
    public async Task<ActionResult<ApiSuccessResponse<IReadOnlyList<SubtaskResponse>>>> GetByTaskId(
        long taskId, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<IReadOnlyList<SubtaskResponse>>.Create(
            await service.GetByTaskIdAsync(taskId, cancellationToken), "Request successful"));

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<SubtaskResponse>>> GetById(
        long id, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<SubtaskResponse>.Create(
            await service.GetByIdAsync(id, cancellationToken), "Request successful"));

    [HttpPost]
    public async Task<ActionResult<ApiSuccessResponse<SubtaskResponse>>> Create(
        [FromBody] CreateSubtaskRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created,
            ApiSuccessResponse<SubtaskResponse>.Create(
                await service.CreateAsync(request, cancellationToken), "Resource created successfully"));

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<SubtaskResponse>>> Update(
        long id, [FromBody] UpdateSubtaskRequest request, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<SubtaskResponse>.Create(
            await service.UpdateAsync(id, request, User, cancellationToken), "Request successful"));

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<object>>> Delete(
        long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return Ok(new ApiSuccessResponse<object>(true, "SUCCESS",
            "Operation completed successfully", null, DateTime.Now));
    }
}
