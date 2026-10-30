using Microsoft.AspNetCore.Mvc;
using PRN232.Application.Common;
using PRN232.Application.Roles;
using PRN232.Application.Security;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/v1/roles")]
public sealed class RolesController(RoleService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiSuccessResponse<PageResponse<RoleResponse>>>> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] string[]? sort = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = PageRequestParser.NormalizePage(page, size);
        var sortOptions = (sort ?? [])
            .Select(ParseSort)
            .ToArray();
        var result = await service.GetPageAsync(
            search, normalizedPage.Page, normalizedPage.Size, sortOptions, cancellationToken);
        var totalPages = (int)Math.Ceiling(result.TotalElements / (double)normalizedPage.Size);
        var data = new PageResponse<RoleResponse>(
            result.Content, normalizedPage.Page, normalizedPage.Size, result.TotalElements, totalPages,
            normalizedPage.Page == 0, normalizedPage.Page >= totalPages - 1);
        return Ok(ApiSuccessResponse<PageResponse<RoleResponse>>.Create(data, "Request successful"));
    }

    private static RoleSort ParseSort(string sort)
    {
        var parts = sort.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length > 2 || string.IsNullOrWhiteSpace(parts[0]))
        {
            throw new InvalidOperationException($"Invalid sort parameter: {sort}");
        }

        var descending = parts.Length == 2
            && string.Equals(parts[1], "desc", StringComparison.OrdinalIgnoreCase);
        if (parts.Length == 2
            && !descending
            && !string.Equals(parts[1], "asc", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Invalid sort direction: {parts[1]}");
        }

        return new RoleSort(parts[0], descending);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<RoleResponse>>> GetById(
        long id, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<RoleResponse>.Create(
            await service.GetRoleByIdAsync(id, cancellationToken), "Request successful"));

    [HttpPost]
    public async Task<ActionResult<ApiSuccessResponse<RoleResponse>>> Create(
        [FromBody] CreateRoleRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created,
            ApiSuccessResponse<RoleResponse>.Create(
                await service.CreateRoleAsync(request, cancellationToken), "Resource created successfully"));

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<RoleResponse>>> Update(
        long id, [FromBody] UpdateRoleRequest request, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<RoleResponse>.Create(
            await service.UpdateRoleAsync(id, request, cancellationToken), "Request successful"));

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<object>>> Delete(
        long id, CancellationToken cancellationToken)
    {
        await service.DeleteRoleAsync(id, cancellationToken);
        return Ok(new ApiSuccessResponse<object>(true, "SUCCESS",
            "Operation completed successfully", null, DateTime.Now));
    }
}
