using System.Net;
using System.Text.Json;
using PastPort.Domain.Exceptions;

namespace PastPort.API.Middlewares;

/// <summary>
/// Global exception handling middleware that intercepts all unhandled exceptions
/// thrown during request processing and converts them into standardized JSON
/// error responses. Prevents stack trace leakage in production environments.
/// </summary>
/// <remarks>
/// <para>
/// This middleware is registered early in the ASP.NET Core pipeline in <c>Program.cs</c>
/// and wraps all subsequent middleware and controller execution in a try-catch block.
/// </para>
/// <para>
/// Exception-to-status-code mapping:
/// <list type="bullet">
///   <item><see cref="UnauthorizedAccessException"/> → 401 Unauthorized</item>
///   <item><see cref="NotFoundException"/> → 404 Not Found</item>
///   <item><see cref="ValidationException"/> → 400 Bad Request</item>
///   <item><see cref="ArgumentException"/> → 400 Bad Request</item>
///   <item><see cref="InvalidOperationException"/> → 400 Bad Request</item>
///   <item>All others → 500 Internal Server Error</item>
/// </list>
/// </para>
/// </remarks>
public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    /// <summary>
    /// Invokes the next middleware in the pipeline, catching any unhandled exceptions.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unhandled exception occurred");
            await HandleExceptionAsync(context, ex);
        }
    }

    /// <summary>
    /// Maps the exception to an appropriate HTTP status code and writes
    /// a JSON response body with error details and an ISO 8601 timestamp.
    /// </summary>
    /// <param name="context">The current HTTP context.</param>
    /// <param name="exception">The unhandled exception.</param>
    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException =>
                (HttpStatusCode.Unauthorized, "Unauthorized access"),

            NotFoundException =>
                (HttpStatusCode.NotFound, exception.Message),

            ValidationException =>
                (HttpStatusCode.BadRequest, exception.Message),

            ArgumentException =>
                (HttpStatusCode.BadRequest, "Invalid request parameters"),

            InvalidOperationException =>
                (HttpStatusCode.BadRequest, "The operation could not be completed"),

            _ =>
                (HttpStatusCode.InternalServerError, "An error occurred while processing your request")
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            error = message,
            statusCode = (int)statusCode,
            timestamp = DateTime.UtcNow.ToString("O")
        };

        var jsonResponse = JsonSerializer.Serialize(response);
        return context.Response.WriteAsync(jsonResponse);
    }
}