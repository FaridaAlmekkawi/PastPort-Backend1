using PastPort.API.Middlewares;
using Serilog;

namespace PastPort.API.Extensions;

public static class LoggerServiceExtensions
{
    public static WebApplicationBuilder AddLoggerService(this WebApplicationBuilder builder)
    {
        
        // ==================================================
        // 1. LOGGING (Serilog)
        // ==================================================
        Log.Logger = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/pastport-.txt", rollingInterval: RollingInterval.Day)
        .CreateLogger();

        builder.Host.UseSerilog();
        
        return builder;
    }
}