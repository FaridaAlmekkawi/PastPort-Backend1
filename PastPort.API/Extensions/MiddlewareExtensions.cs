using AspNetCoreRateLimit;
using PastPort.API.Hubs;
using PastPort.API.Middlewares;

namespace PastPort.API.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseCustomExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }

    public static IApplicationBuilder UseRequestLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<RequestLoggingMiddleware>();
    }

    public static WebApplication UseMiddlewaresPipeline(this WebApplication app)
    {
        // ==================================================
        // MIDDLEWARE PIPELINE
        // ==================================================
        IWebHostEnvironment env = app.Environment;

        app.UseCustomExceptionHandler();
        app.UseRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }
        else
        {
            app.UseHsts();
        }

        app.UseStaticFiles();

        app.UseRouting();

        // 🟢 --- WebSockets Middleware (يجب أن يكون بعد UseRouting وقبل SignalR) ---
        app.UseWebSockets(new WebSocketOptions
        {
            // How often SignalR sends WebSocket keep-alive pings to Unity.
            KeepAliveInterval = TimeSpan.FromSeconds(15)
        });

        if (!app.Environment.IsDevelopment())
        {
            app.UseIpRateLimiting();
        }

        app.UseCors("AllowAll");

        app.UseAuthentication();
        app.UseAuthorization();

        // 🔥 Swagger
        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("./v1/swagger.json", "PastPort API v1");
            options.RoutePrefix = "swagger";
        });

        app.MapControllers();

        // 🟢 --- Map SignalR Hub ---
        app.MapHub<NpcHub>("/npcHub", options =>
        {
            // Allow large binary frames for audio uploads.
            options.ApplicationMaxBufferSize = 12 * 1024 * 1024; // 12 MB
            options.TransportMaxBufferSize = 12 * 1024 * 1024;

            // Prefer WebSockets for low latency; fall back to Long Polling for
            // environments that block WS (corporate firewalls, etc.).
            options.Transports =
                Microsoft.AspNetCore.Http.Connections.HttpTransportType.WebSockets |
                Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
        });

        return app;
    }
}