using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;
using PRN232.Application.Common;

namespace PRN232.Application.Users;

public sealed class UsersService(IUsersRepository repository)
{
    private const string UserName = "User";

    public Task<UserPageResult> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<PageSort> sort, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var result = await repository.GetPageAsync(search, page, size, sort, token);
            return new UserPageResult(
                result.Content.Select(ToResponse).ToArray(), page, size, result.TotalElements);
        }, cancellationToken);

    public Task<UserResponse> GetUserByIdAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
            ToResponse(await FindOrThrowAsync(id, token)), cancellationToken);

    public Task<UserResponse> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            if (await repository.ExistsByEmailAsync(request.Email, token))
            {
                throw new ConflictException($"Email already exists: {request.Email}");
            }

            var role = await repository.FindRoleByNameAsync(UserName, token)
                ?? throw new NotFoundException("User not found with id: null");

            var saved = await repository.SaveAsync(new UserEntity
            {
                Email = request.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password, 10),
                Name = request.Name,
                Avatar = request.Avatar,
                Status = request.Status,
                RoleId = role.Id,
                Role = null
            }, token);
            return ToResponse(saved);
        }, cancellationToken);

    public Task<UserResponse> UpdateUserAsync(
        long id, UpdateUserRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var existing = await repository.FindByIdForUpdateAsync(id, token) ?? throw NotFound(id);
            if (request.Email is not null
                && !string.Equals(request.Email, existing.Email, StringComparison.OrdinalIgnoreCase)
                && await repository.ExistsByEmailAsync(request.Email, token))
            {
                throw new ConflictException($"Email already exists: {request.Email}");
            }

            if (request.Email is not null) existing.Email = request.Email;
            if (request.Password is not null) existing.Password = request.Password;
            if (request.Name is not null) existing.Name = request.Name;
            if (request.Avatar is not null) existing.Avatar = request.Avatar;
            if (request.Status is not null) existing.Status = request.Status;
            if (request.RoleId is not null)
            {
                existing.RoleId = request.RoleId;
            }

            if (!string.IsNullOrWhiteSpace(request.Password))
            {
                existing.Password = BCrypt.Net.BCrypt.HashPassword(request.Password, 10);
            }

            return ToResponse(await repository.SaveAsync(existing, token));
        }, cancellationToken);

    public Task DeleteUserAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            if (!await repository.ExistsByIdAsync(id, token))
            {
                throw NotFound(id);
            }

            await repository.DeleteByIdAsync(id, token);
            return true;
        }, cancellationToken);

    private async Task<UserEntity> FindOrThrowAsync(long id, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(id, cancellationToken) ?? throw NotFound(id);

    private static NotFoundException NotFound(long id) => new($"User not found with id: {id}");

    private static UserResponse ToResponse(UserEntity user) =>
        new(user.Id, user.Email, user.Name, user.Avatar, user.Status,
            user.RoleId, user.Role?.Name, user.CreatedAt, user.UpdatedAt);
}
