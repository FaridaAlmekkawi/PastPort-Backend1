using PastPort.API.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);


// ==================================================
// 2. SERVICES
// ==================================================
builder.AddLoggerService();
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

app.UseMiddlewaresPipeline();

await app.SeedDataAsync();

Log.Information("PastPort API Starting...");

app.Run();