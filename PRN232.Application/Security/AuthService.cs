using PRN232.Domain.Entities;
using PRN232.Domain.Exceptions;

namespace PRN232.Application.Security;

public sealed class AuthService(
    IAuthRepository repository,
    JwtTokenService tokens,
    TokenBlacklist blacklist,
    IGoogleAuthProvider googleAuthProvider)
{
    public Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async transactionToken =>
        {
            var user = await repository.FindUserByEmailAsync(request.Email, transactionToken);
            if (user is null)
            {
                throw new UnauthorizedException("Invalid email or password");
            }

            var matches = VerifyPassword(request.Password, user.Password);
            var passwordUpgraded = false;
            if (!matches && request.Password == user.Password)
            {
                user.Password = BCrypt.Net.BCrypt.HashPassword(request.Password, 10);
                matches = true;
                passwordUpgraded = true;
            }

            if (!matches)
            {
                throw new UnauthorizedException("Invalid email or password");
            }

            EnsureAccountActive(user);
            if (passwordUpgraded)
            {
                await repository.SaveChangesAsync(transactionToken);
            }

            return CreateAuthResponse(user, tokens.CreateRefreshToken(user));
        }, cancellationToken);

    public Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async transactionToken =>
        {
            if (await repository.FindUserByEmailAsync(request.Email, transactionToken) is not null)
            {
                throw new ConflictException($"Email already exists: {request.Email}");
            }

            var role = await repository.FindDefaultRoleAsync(transactionToken)
                ?? throw new NotFoundException("User not found with id: null");
            var user = new UserEntity
            {
                Email = request.Email,
                Password = BCrypt.Net.BCrypt.HashPassword(request.Password, 10),
                Name = request.Name,
                Avatar = request.Avatar,
                Status = "active",
                RoleId = role.Id,
                Role = role
            };
            await repository.AddUserAsync(user, transactionToken);
            await repository.SaveChangesAsync(transactionToken);
            return ToUserResponse(user);
        }, cancellationToken);

    public Task<AuthResponse> LoginGoogleAsync(string idToken, CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async transactionToken =>
        {
            var profile = await googleAuthProvider.ValidateIdTokenAsync(idToken, transactionToken);
            return await LoginGoogleProfileAsync(profile, transactionToken);
        }, cancellationToken);

    public Task<AuthResponse> LoginGoogleByAuthorizationCodeAsync(
        string code,
        string redirectUri,
        CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async transactionToken =>
        {
            var profile = await googleAuthProvider.ExchangeAuthorizationCodeAsync(
                code,
                redirectUri,
                transactionToken);
            return await LoginGoogleProfileAsync(profile, transactionToken);
        }, cancellationToken);

    public Task<AuthResponse> RefreshAsync(
        RefreshTokenRequest request,
        CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async transactionToken =>
        {
            if (!tokens.TryReadToken(request.RefreshToken, out var claims)
                || blacklist.Contains(request.RefreshToken))
            {
                throw new UnauthorizedException("Invalid or expired refresh token");
            }

            var user = await repository.FindUserByEmailAsync(claims.Email, transactionToken)
                ?? throw new UnauthorizedException("User not found for refresh token");
            EnsureAccountActive(user);
            return CreateAuthResponse(user, request.RefreshToken);
        }, cancellationToken);

    public Task ChangePasswordAsync(
        string email,
        ChangePasswordRequest request,
        CancellationToken cancellationToken) =>
        repository.ExecuteInTransactionAsync(async transactionToken =>
        {
            var user = await repository.FindUserByEmailAsync(email, transactionToken)
                ?? throw new NotFoundException($"User not found with id: {email}");
            var matches = VerifyPassword(request.CurrentPassword, user.Password);
            if (!matches && request.CurrentPassword == user.Password)
            {
                matches = true;
            }

            if (!matches)
            {
                throw new BadRequestException("Current password is incorrect");
            }

            if (VerifyPassword(request.NewPassword, user.Password) || request.NewPassword == user.Password)
            {
                throw new BadRequestException("New password cannot be the same as current password");
            }

            if (!string.IsNullOrWhiteSpace(request.ConfirmPassword)
                && request.NewPassword != request.ConfirmPassword)
            {
                throw new BadRequestException("Password confirmation does not match");
            }

            user.Password = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, 10);
            await repository.SaveChangesAsync(transactionToken);
            return true;
        }, cancellationToken);

    public void Logout(string? authorizationHeader)
    {
        const string prefix = "Bearer ";
        if (authorizationHeader?.StartsWith(prefix, StringComparison.Ordinal) != true)
        {
            return;
        }

        var token = authorizationHeader[prefix.Length..].Trim();
        if (token.Length > 0 && tokens.TryReadToken(token, out var claims))
        {
            blacklist.Add(token, DateTimeOffset.FromUnixTimeSeconds(claims.ExpiresAtSeconds));
        }
    }

    public bool IsBlacklisted(string token) => blacklist.Contains(token);

    private async Task<AuthResponse> LoginGoogleProfileAsync(
        GoogleProfile profile,
        CancellationToken cancellationToken)
    {
        if (profile.EmailVerified == false)
        {
            throw new UnauthorizedException("Google email is not verified");
        }

        var user = await repository.FindUserByEmailAsync(profile.Email, cancellationToken);
        var isNewUser = user is null;
        if (user is null)
        {
            var role = await repository.FindDefaultRoleAsync(cancellationToken)
                ?? await repository.FindRoleByIdAsync(2, cancellationToken);
            user = new UserEntity
            {
                Email = profile.Email,
                Name = !string.IsNullOrWhiteSpace(profile.Name)
                    ? profile.Name
                    : profile.Email.Split('@')[0],
                Avatar = profile.Picture,
                Status = "active",
                RoleId = role?.Id,
                Role = role,
                Password = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString(), 10)
            };
            await repository.AddUserAsync(user, cancellationToken);
        }

        EnsureAccountActive(user);
        var updated = false;
        if (!string.IsNullOrEmpty(profile.Picture) && profile.Picture != user.Avatar)
        {
            user.Avatar = profile.Picture;
            updated = true;
        }

        if (!string.IsNullOrEmpty(profile.Name) && profile.Name != user.Name)
        {
            user.Name = profile.Name;
            updated = true;
        }

        if (updated || isNewUser)
        {
            await repository.SaveChangesAsync(cancellationToken);
        }

        return CreateAuthResponse(user, tokens.CreateRefreshToken(user));
    }

    private AuthResponse CreateAuthResponse(UserEntity user, string refreshToken) =>
        new(
            tokens.CreateAccessToken(user),
            refreshToken,
            "Bearer",
            tokens.AccessTokenExpiration,
            ToUserResponse(user));

    private static bool VerifyPassword(string suppliedPassword, string storedPassword) =>
        storedPassword.StartsWith("$2a$", StringComparison.Ordinal)
        || storedPassword.StartsWith("$2b$", StringComparison.Ordinal)
        || storedPassword.StartsWith("$2y$", StringComparison.Ordinal)
            ? BCrypt.Net.BCrypt.Verify(suppliedPassword, storedPassword)
            : false;

    private static UserResponse ToUserResponse(UserEntity user) =>
        new(
            user.Id,
            user.Email,
            user.Name,
            user.Avatar,
            user.Status,
            user.RoleId,
            user.Role?.Name,
            user.CreatedAt,
            user.UpdatedAt);

    private static void EnsureAccountActive(UserEntity user)
    {
        if (string.Equals(user.Status, "inactive", StringComparison.OrdinalIgnoreCase)
            || string.Equals(user.Status, "suspended", StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException($"Account is {user.Status}");
        }
    }
}
