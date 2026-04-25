using System.Net;
using System.Text.Json;
using PastPort.Domain.Exceptions; // تأكد من إضافة هذا الـ using

namespace PastPort.API.Middlewares;

public class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
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

    private static Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        // استخدام switch expression لتحديد حالة الكود والرسالة بشكل نظيف
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

            // منع تسريب تفاصيل الخطأ الداخلية في الـ Production
            InvalidOperationException =>
                (HttpStatusCode.BadRequest, "The operation could not be completed"),

            _ =>
                (HttpStatusCode.InternalServerError, "An error occurred while processing your request")
        };

        // من أفضل الممارسات تحديد الـ ContentType قبل الـ StatusCode
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var response = new
        {
            error = message,
            statusCode = (int)statusCode,
            timestamp = DateTime.UtcNow.ToString("O") // تنسيق ISO 8601 للوقت
        };

        var jsonResponse = JsonSerializer.Serialize(response);
        return context.Response.WriteAsync(jsonResponse);
    }
}