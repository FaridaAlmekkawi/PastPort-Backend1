using System.Text;
using AspNetCoreRateLimit;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using PastPort.Application.Common;
using PastPort.Application.Interfaces;
using PastPort.Application.Services;
using PastPort.Domain.Entities;
using PastPort.Domain.Interfaces;
using PastPort.Infrastructure.Data;
using PastPort.Infrastructure.Data.Repositories;
using PastPort.Infrastructure.ExternalServices.AI;
using PastPort.Infrastructure.ExternalServices.Payment;
using PastPort.Infrastructure.ExternalServices.Storage;
using PastPort.Infrastructure.Identity;
using PastPort.Infrastructure.Services;
using Mapster;
using MapsterMapper;
using Serilog;
using System.Reflection;

namespace PastPort.API.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabaseAndIdentity(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly("PastPort.Infrastructure")));

        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
        {
            options.Password.RequireDigit = true;
            options.Password.RequiredLength = 8;
            options.User.RequireUniqueEmail = true;
        })
        .AddEntityFrameworkStores<ApplicationDbContext>()
        .AddDefaultTokenProviders();

        return services;
    }

    public static IServiceCollection AddJwtAndAuth(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSettingsSection = configuration.GetSection("JwtSettings");
        services.Configure<JwtSettings>(jwtSettingsSection);
        var secretKey = jwtSettingsSection["SecretKey"];

        if (string.IsNullOrWhiteSpace(secretKey))
        {
            Log.Fatal("JWT SecretKey is missing");
            throw new InvalidOperationException("JWT SecretKey is missing");
        }

        services.AddAuthentication(options =>
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
            options.ClientId = configuration["Authentication:Google:ClientId"]
                ?? throw new InvalidOperationException("Google ClientId missing");
            options.ClientSecret = configuration["Authentication:Google:ClientSecret"]
                ?? throw new InvalidOperationException("Google ClientSecret missing");
            Log.Information("✅ Google Authentication enabled");
        })
        .AddFacebook(options =>
        {
            options.AppId = configuration["Authentication:Facebook:AppId"]
                ?? throw new InvalidOperationException("Facebook AppId missing");
            options.AppSecret = configuration["Authentication:Facebook:AppSecret"]
                ?? throw new InvalidOperationException("Facebook AppSecret missing");
            Log.Information("✅ Facebook Authentication enabled");
        });

        return services;
    }

    public static IServiceCollection AddRateLimitingAndCache(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache(options =>
        {
            options.ExpirationScanFrequency = TimeSpan.FromMinutes(5);
        });

        services.Configure<IpRateLimitOptions>(configuration.GetSection("IpRateLimiting"));
        services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
        services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
        services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
        services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
        services.AddInMemoryRateLimiting();

        return services;
    }

    public static IServiceCollection AddApplicationDependencies(this IServiceCollection services, IConfiguration configuration)
    {
        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<ISceneRepository, SceneRepository>();
        services.AddScoped<ICharacterRepository, CharacterRepository>();
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<ISubscriptionRepository, SubscriptionRepository>();
        services.AddScoped<IAssetRepository, AssetRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        // Services
        services.AddScoped<IJwtTokenService, JwtTokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ISceneService, SceneService>();
        services.AddScoped<ICharacterService, CharacterService>();
        services.AddScoped<IConversationService, ConversationService>();
        services.AddScoped<ISubscriptionService, SubscriptionService>();
        services.AddScoped<IEmailService, EmailService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IPaymentService, PayPalService>();
        services.AddScoped<IFileStorageService, LocalFileStorageService>();
        services.AddScoped<IAIConversationService, MockAIConversationService>();
        if (configuration.GetValue<bool>("NpcAI:UseMock"))
        {
            services.AddScoped<INpcAIService, MockNpcAIService>();
        }
        else
        {
            services.AddScoped<INpcAIService, NpcAIService>();
        }
        services.AddSingleton<ICacheService, CacheService>();

        // Settings
        services.Configure<EmailSettings>(configuration.GetSection("EmailSettings"));
        services.Configure<PayPalSettings>(configuration.GetSection("PayPal"));
        services.Configure<NpcAISettings>(configuration.GetSection("NpcAI"));
        
        // Mapster
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(Assembly.Load("PastPort.Application"));
        services.AddSingleton(config);
        services.AddScoped<IMapper, ServiceMapper>();

        return services;
    }

    public static IServiceCollection AddSwaggerConfig(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
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

        return services;
    }

    public static IServiceCollection AddSignalRConfig(this IServiceCollection services, IWebHostEnvironment environment)
    {
        services.AddSignalR(options =>
        {
            options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
            options.KeepAliveInterval = TimeSpan.FromSeconds(15);
            options.MaximumReceiveMessageSize = 11 * 1024 * 1024;
            options.EnableDetailedErrors = environment.IsDevelopment();
            options.StreamBufferCapacity = 20;
        })
        .AddMessagePackProtocol(options =>
        {
            options.SerializerOptions =
                MessagePack.MessagePackSerializerOptions.Standard
                    .WithCompression(MessagePack.MessagePackCompression.Lz4BlockArray);
        });

        return services;
    }

    public static IServiceCollection AddCorsConfig(this IServiceCollection services)
    {
        services.AddCors(options =>
        {
            options.AddPolicy("AllowAll", policy =>
            {
                policy.SetIsOriginAllowed(_ => true)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        return services;
    }
}
