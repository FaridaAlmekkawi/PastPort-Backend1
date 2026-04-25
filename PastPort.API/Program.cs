using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PastPort.API.Extensions;
using PastPort.Application.Common;
using PastPort.Infrastructure.Identity;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using PastPort.Domain.Interfaces;
using PastPort.Infrastructure.Data;
using PastPort.Infrastructure.Data.Repositories;
using PastPort.Infrastructure.ExternalServices.AI;
using PastPort.Infrastructure.ExternalServices.Payment;
using PastPort.Infrastructure.ExternalServices.Storage;
using AspNetCoreRateLimit;
using Serilog;
using PastPort.Application.Services;
using PastPort.API.Hubs;

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

// --- DB
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        b => b.MigrationsAssembly("PastPort.Infrastructure")));

// --- Identity
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = true;
    options.Password.RequiredLength = 8;
    options.User.RequireUniqueEmail = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// --- JWT & Authentication
var jwtSettingsSection = builder.Configuration.GetSection("JwtSettings");
builder.Services.Configure<JwtSettings>(jwtSettingsSection);
var secretKey = jwtSettingsSection["SecretKey"];

if (string.IsNullOrWhiteSpace(secretKey))
{
    Log.Fatal("JWT SecretKey is missing");
    throw new InvalidOperationException("JWT SecretKey is missing");
}

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtSettingsSection["Issuer"],
        ValidAudience = jwtSettingsSection["Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey))
    };
})
.AddGoogle(options =>
{
    options.ClientId = builder.Configuration["Authentication:Google:ClientId"]
        ?? throw new InvalidOperationException("Google ClientId missing");
    options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]
        ?? throw new InvalidOperationException("Google ClientSecret missing");
    Log.Information("✅ Google Authentication enabled");
})
.AddFacebook(options =>
{
    options.AppId = builder.Configuration["Authentication:Facebook:AppId"]
        ?? throw new InvalidOperationException("Facebook AppId missing");
    options.AppSecret = builder.Configuration["Authentication:Facebook:AppSecret"]
        ?? throw new InvalidOperationException("Facebook AppSecret missing");
    Log.Information("✅ Facebook Authentication enabled");
});

// 🟢 --- Rate Limiting & Memory Cache (تم تحديث الـ MemoryCache هنا) ---
builder.Services.AddMemoryCache(options =>
{
    options.SizeLimit = 10_000; // max concurrent sessions
    options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
    options.CompactionPercentage = 0.20; // remove 20% oldest when limit hit
});
builder.Services.Configure<IpRateLimitOptions>(builder.Configuration.GetSection("IpRateLimiting"));
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// --- DI
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<ISceneRepository, SceneRepository>();
builder.Services.AddScoped<ICharacterRepository, CharacterRepository>();
builder.Services.AddScoped<IConversationRepository, ConversationRepository>();
builder.Services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
builder.Services.AddScoped<IAssetRepository, AssetRepository>();
builder.Services.AddScoped<IPaymentRepository, PaymentRepository>();

builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<ISceneService, SceneService>();
builder.Services.AddScoped<ICharacterService, CharacterService>();
builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<ISubscriptionService, SubscriptionService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IPaymentService, PayPalService>();
builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
builder.Services.AddScoped<IAIConversationService, MockAIConversationService>();

// --- External configs
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<PayPalSettings>(builder.Configuration.GetSection("PayPal"));
builder.Services.Configure<NpcAISettings>(builder.Configuration.GetSection("NpcAI"));

// 🟢 --- NPC AI SERVICE (تم تغييره من HttpClient إلى Scoped للـ WebSockets) ---
builder.Services.AddScoped<INpcAIService, NpcAIService>();

// --- Redis
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
{
    var redisConnString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
    var options = StackExchange.Redis.ConfigurationOptions.Parse(redisConnString);
    options.AbortOnConnectFail = false;
    return StackExchange.Redis.ConnectionMultiplexer.Connect(options);
});

builder.Services.AddSingleton<INpcSessionStore, RedisNpcSessionStore>();

// 🟢 --- SIGNALR SERVICES ---
builder.Services.AddSignalR(options =>
{
    // How long a connection may be idle before the server probes it.
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);

    // Max inbound message size from Unity (audio + overhead).
    options.MaximumReceiveMessageSize = 11 * 1024 * 1024; // 11 MB

    // Enable detailed errors in development for Unity debugging.
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();

    // Allow streaming return values
    options.StreamBufferCapacity = 20;
})
.AddMessagePackProtocol(options =>
{
    // MessagePack is more efficient than JSON for binary audio data.
    options.SerializerOptions =
        MessagePack.MessagePackSerializerOptions.Standard
            .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray);
});

// --- Swagger
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo { Title = "PastPort API", Version = "v1" });

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "Bearer",
        In = ParameterLocation.Header,
        Description = "Enter JWT token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

// --- CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

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

// ❌ تم إيقاف السطر ده لأنه بيوقع السيرفر على استضافتك
// app.UseHttpsRedirection(); 

app.UseStaticFiles();

app.UseRouting();

// 🟢 --- WebSockets Middleware (يجب أن يكون بعد UseRouting وقبل SignalR) ---
app.UseWebSockets(new WebSocketOptions
{
    // How often SignalR sends WebSocket keep-alive pings to Unity.
    KeepAliveInterval = TimeSpan.FromSeconds(15)
});

app.UseIpRateLimiting();

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
// SEEDING (✅ تم إضافة الحماية هنا)
// ==================================================
using (var scope = app.Services.CreateScope())
{
    try
    {
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        string[] roles = { "Admin", "School", "Museum", "Enterprise", "Individual" };

        foreach (var role in roles)
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