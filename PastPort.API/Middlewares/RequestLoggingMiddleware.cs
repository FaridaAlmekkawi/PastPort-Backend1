using Serilog;

namespace PastPort.API.Middlewares;

public class RequestLoggingMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var startTime = DateTime.UtcNow;

        Log.Information("Request: {Method} {Path} started at {StartTime}",
            context.Request.Method,
            context.Request.Path,
            startTime);

        await next(context);

        var duration = DateTime.UtcNow - startTime;

        Log.Information("Request: {Method} {Path} completed in {Duration}ms with status {StatusCode}",
            context.Request.Method,
            context.Request.Path,
            duration.TotalMilliseconds,
            context.Response.StatusCode);
    }
}