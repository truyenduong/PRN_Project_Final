using System.Security.Claims;
using PRN232.Domain.Exceptions;

namespace PRN232.Application.Common;

public sealed record CurrentUser(long Id, string? Email);

public interface ICurrentUserAccessor
{
    CurrentUser GetCurrentUser(ClaimsPrincipal principal);
}

public sealed class CurrentUserAccessor : ICurrentUserAccessor
{
    public CurrentUser GetCurrentUser(ClaimsPrincipal principal)
    {
        var idValue = principal.FindFirst("userId")?.Value ?? principal.FindFirst("sub")?.Value;
        if (!long.TryParse(idValue, out var id))
        {
            throw new UnauthorizedException("Vui lòng đăng nhập để thực hiện thao tác");
        }

        return new CurrentUser(
            id,
            principal.FindFirst("email")?.Value ?? principal.FindFirst("sub")?.Value);
    }
}
