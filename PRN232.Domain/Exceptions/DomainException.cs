namespace PRN232.Domain.Exceptions;

public abstract class DomainException(string message) : Exception(message)
{
}

public sealed class BadRequestException(string message) : DomainException(message);

public sealed class InvalidRsqlException(string message) : DomainException(message);

public sealed class UnauthorizedException(string message) : DomainException(message);

public sealed class ForbiddenException(string message) : DomainException(message);

public sealed class NotFoundException(string message) : DomainException(message);

public sealed class ConflictException(string message) : DomainException(message);
