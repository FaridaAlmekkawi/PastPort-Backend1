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
using AspNetCoreRateLimit; // تم الإضافة عشان الـ Rate Limiting
using Serilog;
using PastPort.Application.Services;

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

// --- JWT & Authentication (FIX 1: تم ربط Google و Facebook بشكل صحيح)
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

// --- Rate Limiting (FIX 3: تم إضافة إعدادات الـ Rate Limit)
builder.Services.AddMemoryCache();
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
builder.Services.AddScoped<IAIConversationService, MockAIConversationService>(); // خلي بالك لو عندك NpcAIService حقيقي الأفضل تستخدمه

// --- External configs
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<PayPalSettings>(builder.Configuration.GetSection("PayPal"));
builder.Services.Configure<NpcAISettings>(builder.Configuration.GetSection("NpcAI"));
builder.Services.AddHttpClient<INpcAIService, NpcAIService>();

// ✅ التعديل الجديد: إعداد Redis وربط الـ Session Store الجديد
builder.Services.AddSingleton<StackExchange.Redis.IConnectionMultiplexer>(sp =>
    StackExchange.Redis.ConnectionMultiplexer.Connect(
        builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379"));

// ربط الإنترفيس بالكلاس الجديد اللي بيستخدم Redis
builder.Services.AddSingleton<INpcSessionStore, RedisNpcSessionStore>();
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
    options.AddPolicy("Development", p =>
        p.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    options.AddPolicy("Production", p =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];

        p.WithOrigins(origins)
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
// MIDDLEWARE PIPELINE (FIX 2: ترتيب الـ Middleware)
// ==================================================

// 1. Exception Handler MUST be FIRST
app.UseCustomExceptionHandler();

// 2. Request Logging SECOND (عشان يسجل كل الـ Requests حتى اللي بتضرب Errors)
app.UseRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    // لو مش Development ممكن نستخدم Hsts
    app.UseHsts();
}

// 🔥 FIX CORS Headers (مهم للاستضافة)
app.Use(async (context, next) =>
{
    context.Response.Headers["Access-Control-Allow-Origin"] = "*";
    await next();
});

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// 3. Rate Limiting Middleware (يُفضل يكون بعد الـ Routing وقبل الـ Auth)
app.UseIpRateLimiting();

app.UseCors(app.Environment.IsDevelopment() ? "Development" : "Production");

app.UseAuthentication();
app.UseAuthorization();

// 🔥 Swagger FIX
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("v1/swagger.json", "PastPort API V1");
    c.RoutePrefix = "swagger";
});

app.MapControllers();

// ==================================================
// SEEDING
// ==================================================
using (var scope = app.Services.CreateScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    string[] roles = { "Admin", "School", "Museum", "Enterprise", "Individual" };

    foreach (var role in roles)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));
    }
}

Log.Information("PastPort API Starting...");

app.Run();