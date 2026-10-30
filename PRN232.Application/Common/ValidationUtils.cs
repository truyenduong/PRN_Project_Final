using System.Text.RegularExpressions;
using PRN232.Domain.Exceptions;

namespace PRN232.Application.Common;

public static partial class ValidationUtils
{
    public static bool IsEmail(string? value) => Matches(EmailPattern(), value);
    public static bool IsPhone(string? value) => Matches(PhonePattern(), value);
    public static bool IsUsername(string? value) => Matches(UsernamePattern(), value);
    public static bool IsPassword(string? value) => Matches(PasswordPattern(), value);
    public static bool IsUuid(string? value) => Matches(UuidPattern(), value);

    public static void RequireNonNull(object? value, string fieldName)
    {
        if (value is null)
        {
            throw new BadRequestException($"'{fieldName}' must not be null");
        }
    }

    public static void RequireNotBlank(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new BadRequestException($"'{fieldName}' must not be blank");
        }
    }

    public static void RequireValidEmail(string? value)
    {
        RequireNotBlank(value, "email");
        if (!IsEmail(value))
        {
            throw new BadRequestException($"'{value}' is not a valid email address");
        }
    }

    public static void RequireValidPhone(string? value)
    {
        RequireNotBlank(value, "phone");
        if (!IsPhone(value))
        {
            throw new BadRequestException($"'{value}' is not a valid phone number");
        }
    }

    public static void RequireValidPassword(string? value)
    {
        RequireNotBlank(value, "password");
        if (!IsPassword(value))
        {
            throw new BadRequestException(
                "Password must be at least 8 characters and contain uppercase, lowercase, digit, and special character");
        }
    }

    public static void RequireValidUsername(string? value)
    {
        RequireNotBlank(value, "username");
        if (!IsUsername(value))
        {
            throw new BadRequestException(
                $"'{value}' is not a valid username (3-30 chars, letters/digits/underscores/hyphens, must start with a letter)");
        }
    }

    public static Guid RequireValidUuid(string? value, string fieldName)
    {
        RequireNotBlank(value, fieldName);
        if (!Guid.TryParse(value, out var guid))
        {
            throw new BadRequestException($"'{fieldName}' must be a valid UUID, got: {value}");
        }

        return guid;
    }

    public static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new BadRequestException(message);
        }
    }

    private static bool Matches(Regex pattern, string? value) =>
        value is not null && pattern.IsMatch(value);

    [GeneratedRegex(@"^[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}$")]
    private static partial Regex EmailPattern();

    [GeneratedRegex(@"^\+?[1-9]\d{6,14}$")]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"^[a-zA-Z][a-zA-Z0-9_\-]{2,29}$")]
    private static partial Regex UsernamePattern();

    [GeneratedRegex(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[@$!%*?&])[A-Za-z\d@$!%*?&]{8,}$")]
    private static partial Regex PasswordPattern();

    [GeneratedRegex(@"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$")]
    private static partial Regex UuidPattern();
}
