namespace CodeReview.Domain.Exceptions;

public sealed class DomainException : Exception
{
    public DomainException(string message) : base(message) { }
    public DomainException(string message, Exception inner) : base(message, inner) { }
}

public sealed class NotFoundException : Exception
{
    public NotFoundException(string entity, Guid id)
        : base($"{entity} with id '{id}' was not found.") { }
}

public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Access denied.") : base(message) { }
}
