using Microsoft.AspNetCore.Mvc;
using PRN232.Application.Common;
using PRN232.Application.Security;
using PRN232.Application.Users;
using UserResponse = PRN232.Application.Users.UserResponse;

namespace PRN232.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(UsersService service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<ApiSuccessResponse<PageResponse<UserResponse>>>> GetPage(
        [FromQuery] string? search,
        [FromQuery] int page = 0,
        [FromQuery] int size = 20,
        [FromQuery] string[]? sort = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPage = PageRequestParser.NormalizePage(page, size);
        var result = await service.GetPageAsync(
            search, normalizedPage.Page, normalizedPage.Size, PageRequestParser.ParseSort(sort), cancellationToken);
        return Ok(ApiSuccessResponse<PageResponse<UserResponse>>.Create(
            ToPage(result), "Request successful"));
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<UserResponse>>> GetById(
        long id, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<UserResponse>.Create(
            await service.GetUserByIdAsync(id, cancellationToken), "Request successful"));

    [HttpPost]
    public async Task<ActionResult<ApiSuccessResponse<UserResponse>>> Create(
        [FromBody] CreateUserRequest request, CancellationToken cancellationToken) =>
        StatusCode(StatusCodes.Status201Created,
            ApiSuccessResponse<UserResponse>.Create(
                await service.CreateUserAsync(request, cancellationToken), "Resource created successfully"));

    [HttpPut("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<UserResponse>>> Update(
        long id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken) =>
        Ok(ApiSuccessResponse<UserResponse>.Create(
            await service.UpdateUserAsync(id, request, cancellationToken), "Request successful"));

    [HttpDelete("{id}")]
    public async Task<ActionResult<ApiSuccessResponse<object>>> Delete(
        long id, CancellationToken cancellationToken)
    {
        await service.DeleteUserAsync(id, cancellationToken);
        return Ok(new ApiSuccessResponse<object>(true, "SUCCESS",
            "Operation completed successfully", null, DateTime.Now));
    }

    private static PageResponse<UserResponse> ToPage(UserPageResult result)
    {
        var totalPages = result.Size <= 0
            ? 0
            : (int)Math.Ceiling(result.TotalElements / (double)result.Size);
        return new PageResponse<UserResponse>(
            result.Content, result.Page, result.Size, result.TotalElements, totalPages,
            result.Page == 0, result.Page >= totalPages - 1);
    }
}
