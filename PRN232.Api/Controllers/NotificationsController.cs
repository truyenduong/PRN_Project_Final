using Microsoft.AspNetCore.Mvc;
using PRN232.Application.Common;
using PRN232.Application.Notifications;
using PRN232.Application.Security;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/v1/notifications")]
public sealed class NotificationsController(NotificationService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiSuccessResponse<PageResponse<NotificationResponse>>>> GetPage(
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
        var data = new PageResponse<NotificationResponse>(
            result.Content, result.Page, result.Size, result.TotalElements, totalPages,
            result.Page == 0, result.Page >= totalPages - 1);
        return Ok(ApiSuccessResponse<PageResponse<NotificationResponse>>.Create(data, "Request successful"));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<NotificationResponse>>> GetById(
        long id, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<NotificationResponse>.Create(
            await service.GetByIdAsync(id, cancellationToken), "Request successful"));

    [HttpPost]
    public async Task<ActionResult<ApiSuccessResponse<NotificationResponse>>> Create(
        [FromBody] CreateNotificationRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created,
            ApiSuccessResponse<NotificationResponse>.Create(
                await service.CreateAsync(request, cancellationToken), "Resource created successfully"));

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<NotificationResponse>>> Update(
        long id, [FromBody] UpdateNotificationRequest request, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<NotificationResponse>.Create(
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
