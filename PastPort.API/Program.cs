using Microsoft.AspNetCore.Identity;
using PastPort.API.Extensions;
using PastPort.API.Hubs;
using PastPort.Domain.Constants;
using AspNetCoreRateLimit;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

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

// ==================================================
// 2. SERVICES
// ==================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services
    .AddDatabaseAndIdentity(builder.Configuration)
    .AddJwtAndAuth(builder.Configuration)
    .AddRateLimitingAndCache(builder.Configuration)
    .AddApplicationDependencies(builder.Configuration)
    .AddSwaggerConfig()
    .AddSignalRConfig(builder.Environment)
    .AddCorsConfig();

// ==================================================
// BUILD
// ==================================================
var app = builder.Build();

// ==================================================
// MIDDLEWARE PIPELINE
// ==================================================

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

// ==================================================
// SEEDING
// ==================================================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in Roles.All)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "حدث خطأ أثناء محاولة الاتصال بقاعدة البيانات في مرحلة الـ Seeding.");
    }
}

Log.Information("PastPort API Starting...");

app.Run();