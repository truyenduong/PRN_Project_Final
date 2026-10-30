using Microsoft.AspNetCore.Mvc;
using PRN232.Application.Common;
using PRN232.Application.Security;
using PRN232.Application.Tasks;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/v1/tasks")]
public sealed class TasksController(TaskService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiSuccessResponse<PageResponse<TaskResponse>>>> GetPage(
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
        var data = new PageResponse<TaskResponse>(
            result.Content, result.Page, result.Size, result.TotalElements, totalPages,
            result.Page == 0, result.Page >= totalPages - 1);
        return Ok(ApiSuccessResponse<PageResponse<TaskResponse>>.Create(data, "Request successful"));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<TaskResponse>>> GetById(
        long id, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<TaskResponse>.Create(
            await service.GetByIdAsync(id, cancellationToken), "Request successful"));

    [HttpPost]
    public async Task<ActionResult<ApiSuccessResponse<TaskResponse>>> Create(
        [FromBody] CreateTaskRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created,
            ApiSuccessResponse<TaskResponse>.Create(
                await service.CreateAsync(request, cancellationToken), "Resource created successfully"));

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<TaskResponse>>> Update(
        long id, [FromBody] UpdateTaskRequest request, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<TaskResponse>.Create(
            await service.UpdateAsync(id, request, User, cancellationToken), "Request successful"));

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<object>>> Delete(
        long id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, User, cancellationToken);
        return Ok(new ApiSuccessResponse<object>(true, "SUCCESS",
            "Operation completed successfully", null, DateTime.Now));
    }

    [HttpPut("{id}/restore")]
    public async Task<ActionResult<ApiSuccessResponse<TaskResponse>>> Restore(
        long id, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<TaskResponse>.Create(
            await service.RestoreAsync(id, cancellationToken), "Request successful"));

    [HttpDelete("{id}/permanent")]
    public async Task<ActionResult<ApiSuccessResponse<object>>> PermanentDelete(
        long id, CancellationToken cancellationToken)
    {
        await service.PermanentDeleteAsync(id, cancellationToken);
        return Ok(new ApiSuccessResponse<object>(true, "SUCCESS",
            "Operation completed successfully", null, DateTime.Now));
    }

    [HttpDelete("projects/{projectId}/trash")]
    public async Task<ActionResult<ApiSuccessResponse<object>>> EmptyTrash(
        long projectId,
        [FromQuery] bool all = false,
        CancellationToken cancellationToken = default)
    {
        await service.EmptyTrashAsync(projectId, all, User, cancellationToken);
        return Ok(new ApiSuccessResponse<object>(true, "SUCCESS",
            "Operation completed successfully", null, DateTime.Now));
    }
}
