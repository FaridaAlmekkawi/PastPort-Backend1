// ✅ FIXED: Program.cs
// المشاكل الأصلية:
// 1. Swagger كان شغّال في كل البيئات (Production, Staging, Development)
//    ده بيكشف كل الـ API surface في Production للمهاجمين.
//    الحل: Swagger بس في Development.
// 2. CORS كان AllowAll في كل البيئات
//    الحل: Production بيستخدم allowed origins من الـ config.
// 3. الـ Log.Information("🚀 PastPort API Starting...") كانت بتتنفذ
//    قبل app.Run() بفترة — يعني الـ API مش بدأت فعلاً لما الـ log يطلع.
//    الحل: نقلناها بعد الـ seeding وقبل app.Run() مباشرة.
// 4. "DefaultFallbackKeyForSafety" كـ JWT secret key خطر في Production
//    لو الـ config مش متعمل صح، الـ app هتشتغل بـ key معروف للكل.
//    الحل: بنعمل validation إن الـ key موجود ومش فاضي في startup.

using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PastPort.API.Extensions;
using PastPort.Application.Common;
using PastPort.Application.Identity;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using PastPort.Domain.Interfaces;
using PastPort.Infrastructure.Data;
using PastPort.Infrastructure.Data.Repositories;
using PastPort.Infrastructure.ExternalServices.AI;
using PastPort.Infrastructure.ExternalServices.Payment;
using PastPort.Infrastructure.ExternalServices.Storage;
using PastPort.Infrastructure.Identity;
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
// 2. SERVICES (DI Container)
// ==================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// --- Database Context
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

// --- JWT & Auth Configuration
var jwtSettingsSection = builder.Configuration.GetSection("JwtSettings");
builder.Services.Configure<JwtSettings>(jwtSettingsSection);
var secretKey = jwtSettingsSection["SecretKey"];

// ✅ FIXED: بدل ما نستخدم "DefaultFallbackKeyForSafety" كـ fallback
// لو الـ secret key مش موجود في الـ config، الـ app مش هتشتغل خالص.
// ده أحسن من إنها تشتغل بـ key معروف للكل ويكون كل token يقدر يتعمل بسهولة.
if (string.IsNullOrWhiteSpace(secretKey))
{
    Log.Fatal("❌ JWT SecretKey is missing from configuration. Application cannot start.");
    throw new InvalidOperationException(
        "JWT SecretKey is not configured. Add JwtSettings:SecretKey to appsettings.");
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
});

// --- Dependency Injection (Repositories & Services)
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
builder.Services.AddSingleton<INpcSessionStore, NpcSessionStore>();

// --- External Configurations
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));
builder.Services.Configure<PayPalSettings>(builder.Configuration.GetSection("PayPal"));
builder.Services.Configure<NpcAISettings>(builder.Configuration.GetSection("NpcAI"));
builder.Services.AddHttpClient<INpcAIService, NpcAIService>();

// --- Swagger Configuration (نسجّله دايماً — بس هنفعّله في Development بس)
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

// ✅ FIXED: CORS policy بناءً على البيئة
// الكود القديم كان AllowAll في كل البيئات — ده خطر في Production
// دلوقتي:
//   - Development: AllowAll (مريح للتطوير)
//   - Production: بيقرأ الـ origins المسموح بيها من الـ config
builder.Services.AddCors(options =>
{
    options.AddPolicy("Development", policy =>
        policy.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader());

    options.AddPolicy("Production", policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? Array.Empty<string>();

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});

// ==================================================
// 3. BUILD APP & DIRECTORIES
// ==================================================
var app = builder.Build();

// إنشاء المجلدات بأمان
var webRoot = app.Environment.WebRootPath
    ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");

string[] directoriesToCreate = {
    Path.Combine(webRoot, "uploads"),
    Path.Combine(webRoot, "assets")
};

foreach (var dirPath in directoriesToCreate)
{
    try
    {
        if (!Directory.Exists(dirPath))
        {
            Directory.CreateDirectory(dirPath);
            Log.Information("📁 Directory created: {Path}", dirPath);
        }
    }
    catch (Exception ex)
    {
        Log.Warning("⚠️ Could not create directory {Path}: {Message}", dirPath, ex.Message);
    }
}

// ==================================================
// 4. MIDDLEWARE PIPELINE
// ==================================================

// 1. Error handling أولاً — عشان يلتقط أي exception من أي middleware تاني
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/error");
    app.UseHsts();
}

// ✅ FIXED: Swagger بس في Development
// الكود القديم كان بيشغّل Swagger في كل البيئات
// ده كان بيكشف كل الـ API endpoints والـ DTOs في Production للمهاجمين
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "PastPort API v1");
        c.RoutePrefix = "swagger";
    });
}

// 3. الأساسيات
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ✅ FIXED: CORS policy بناءً على البيئة
app.UseCors(app.Environment.IsDevelopment() ? "Development" : "Production");

// 4. الحماية — لازم Authentication قبل Authorization
app.UseAuthentication();
app.UseAuthorization();

// 5. الـ Custom Middlewares
app.UseRequestLogging();
app.UseCustomExceptionHandler();

// 6. الروابط
app.MapControllers();

// ==================================================
// 5. SEEDING & RUN
// ==================================================
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        string[] roles = { "Admin", "School", "Museum", "Enterprise", "Individual" };

        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        Log.Information("✅ All roles initialized");
    }
    catch (Exception ex)
    {
        Log.Error(ex, "❌ Seeding failed");
    }
}

// ✅ FIXED: الـ log ده بيظهر وقت الـ startup الفعلي (قبل app.Run() مباشرة)
Log.Information("🚀 PastPort API Starting...");
app.Run();
