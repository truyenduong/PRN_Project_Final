using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Application.Roles;

public sealed class RoleService(IRoleRepository repository)
{
    public async Task<RolePageResult> GetPageAsync(
        string? search, int page, int size, IReadOnlyList<RoleSort> sort, CancellationToken cancellationToken)
    {
        return await repository.ExecuteInTransactionAsync(async token =>
        {
            var result = await repository.GetPageAsync(search, page, size, sort, token);
            return new RolePageResult(
                result.Content.Select(ToResponse).ToArray(),
                page,
                size,
                result.TotalElements);
        }, cancellationToken);
    }

    public Task<RoleResponse> GetRoleByIdAsync(long id, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
            ToResponse(await FindOrThrowAsync(id, token)), cancellationToken);

    public Task<RoleResponse> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            if (await repository.ExistsByNameAsync(request.Name, token))
            {
                throw new ConflictException($"Role already exists with name: {request.Name}");
            }

            var saved = await repository.SaveAsync(new RoleEntity { Name = request.Name }, token);
            return ToResponse(saved);
        }, cancellationToken);

    public Task<RoleResponse> UpdateRoleAsync(
        long id, UpdateRoleRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async token =>
        {
            var existing = await FindOrThrowAsync(id, token);
            if (request.Name is not null
                && !string.Equals(request.Name, existing.Name, StringComparison.OrdinalIgnoreCase)
                && await repository.ExistsByNameAsync(request.Name, token))
            {
                throw new ConflictException($"Role already exists with name: {request.Name}");
            }

            existing.Name = request.Name ?? existing.Name;

            return ToResponse(await repository.SaveAsync(existing, token));
        }, cancellationToken);

    public async Task DeleteRoleAsync(long id, CancellationToken cancellationToken)
    {
        await repository.ExecuteInTransactionAsync(async token =>
        {
            if (!await repository.ExistsByIdAsync(id, token))
            {
                throw NotFound(id);
            }

            await repository.DeleteByIdAsync(id, token);
            return true;
        }, cancellationToken);
    }

    private async Task<RoleEntity> FindOrThrowAsync(long id, CancellationToken cancellationToken) =>
        await repository.FindByIdAsync(id, cancellationToken) ?? throw NotFound(id);

    private static NotFoundException NotFound(long id) => new($"Role not found with id: {id}");

    private static RoleResponse ToResponse(RoleEntity role) =>
        new(role.Id, role.Name, role.CreatedAt, role.UpdatedAt);
}
