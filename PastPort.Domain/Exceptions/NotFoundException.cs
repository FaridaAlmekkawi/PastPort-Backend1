namespace PastPort.Domain.Exceptions;

/// <summary>
/// Exception thrown when a requested entity cannot be found in the data store.
/// Caught by <see cref="API.Middlewares.ExceptionHandlingMiddleware"/> and
/// mapped to an HTTP 404 Not Found response.
/// </summary>
public class NotFoundException : Exception
{
    /// <summary>
    /// Initializes a new instance with a custom error message.
    /// </summary>
    /// <param name="message">The error message describing what was not found.</param>
    public NotFoundException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance with the entity name and key,
    /// producing a standardized message: "{entityName} with id '{key}' was not found."
    /// </summary>
    /// <param name="entityName">The name of the entity type (e.g., "Character").</param>
    /// <param name="key">The identifier that was searched for.</param>
    public NotFoundException(string entityName, object key)
        : base($"{entityName} with id '{key}' was not found.") { }
}