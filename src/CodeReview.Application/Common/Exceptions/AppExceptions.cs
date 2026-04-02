using FluentValidation.Results;

namespace CodeReview.Application.Common.Exceptions;

public sealed class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IEnumerable<ValidationFailure> failures)
        : base("One or more validation failures occurred.")
    {
        Errors = failures
            .GroupBy(f => f.PropertyName, f => f.ErrorMessage)
            .ToDictionary(g => g.Key, g => g.ToArray());
    }
}

public sealed class NotFoundException : Exception
{
    public NotFoundException(string name, Guid id)
        : base($"Entity '{name}' ({id}) was not found.") { }
}

public sealed class ForbiddenException : Exception
{
    public ForbiddenException() : base("You do not have permission to perform this action.") { }
    public ForbiddenException(string message) : base(message) { }
}

public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
