using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using MapsterMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using PastPort.Application.Common;
using PastPort.Application.DTOs.Request;
using PastPort.Application.DTOs.Response;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using PastPort.Infrastructure.Data;
using PastPort.Infrastructure.Identity;
using Xunit;

namespace PastPort.Tests.Services;

public class AuthServiceTests : IDisposable
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection;
    private readonly ApplicationDbContext _context;
    private readonly Mock<UserManager<ApplicationUser>> _userManager;
    private readonly Mock<SignInManager<ApplicationUser>> _signInManager;
    private readonly Mock<IJwtTokenService> _jwtTokenService = new();
    private readonly Mock<IEmailService> _emailService = new();
    private readonly Mock<IUserService> _userService = new();
    private readonly Mock<IMapper> _mapper = new();
    private readonly IOptions<JwtSettings> _jwtOptions;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _connection = new Microsoft.Data.Sqlite.SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using (var command = _connection.CreateCommand())
        {
            command.CommandText = "PRAGMA foreign_keys = OFF;";
            command.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        _context = new ApplicationDbContext(options);
        _context.Database.EnsureCreated();


        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _userManager = new Mock<UserManager<ApplicationUser>>(userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var contextAccessor = new Mock<IHttpContextAccessor>();
        var userClaimsPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _signInManager = new Mock<SignInManager<ApplicationUser>>(
            _userManager.Object, contextAccessor.Object, userClaimsPrincipalFactory.Object, null!, null!, null!, null!);

        _jwtOptions = Microsoft.Extensions.Options.Options.Create(new JwtSettings
        {
            SecretKey = "super-secret-key-that-is-long-enough",
            Issuer = "PastPort",
            Audience = "PastPort",
            ExpiryMinutes = 60
        });

        _sut = new AuthService(
            _userManager.Object,
            _signInManager.Object,
            _jwtTokenService.Object,
            _context,
            _emailService.Object,
            _jwtOptions,
            new Mock<ILogger<AuthService>>().Object,
            _userService.Object,
            _mapper.Object);
    }

    public void Dispose()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
    }

    [Fact]
    public async Task RegisterAsync_WhenUserExists_ReturnsFailure()
    {
        var request = new RegisterRequestDto { Email = "test@example.com" };
        _userManager.Setup(x => x.FindByEmailAsync(request.Email)).ReturnsAsync(new ApplicationUser());

        var result = await _sut.RegisterAsync(request);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("User with this email already exists");
    }

    [Fact]
    public async Task RegisterAsync_HappyPath_ReturnsSuccess()
    {
        var request = new RegisterRequestDto 
        { 
            Email = "new@example.com", 
            Password = "Password123!",
            FirstName = "Test",
            LastName = "User"
        };
        _userManager.Setup(x => x.FindByEmailAsync(request.Email)).ReturnsAsync((ApplicationUser?)null);
        _userManager.Setup(x => x.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .Callback<ApplicationUser, string>((u, p) => _context.Users.Add(u))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.AddToRoleAsync(It.IsAny<ApplicationUser>(), "Individual"))
            .ReturnsAsync(IdentityResult.Success);
        _userManager.Setup(x => x.FindByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) => _context.Users.Find(id) ?? new ApplicationUser { Id = id, Email = request.Email });

        _jwtTokenService.Setup(x => x.GenerateAccessTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync("access-token");
        _jwtTokenService.Setup(x => x.CreateRefreshTokenAsync(It.IsAny<ApplicationUser>()))
            .ReturnsAsync(new RefreshToken { Token = "refresh-token" });

        var result = await _sut.RegisterAsync(request);

        result.Success.Should().BeTrue();
        result.Token.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
        _emailService.Verify(x => x.SendVerificationEmailAsync(request.Email, It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_InvalidCredentials_ReturnsFailure()
    {
        var request = new LoginRequestDto { Email = "test@example.com", Password = "wrong" };
        _userManager.Setup(x => x.FindByEmailAsync(request.Email)).ReturnsAsync(new ApplicationUser());
        _signInManager.Setup(x => x.CheckPasswordSignInAsync(It.IsAny<ApplicationUser>(), request.Password, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.Failed);

        var result = await _sut.LoginAsync(request);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid email or password");
    }

    [Fact]
    public async Task LoginAsync_LockedOut_ReturnsFailure()
    {
        var request = new LoginRequestDto { Email = "test@example.com", Password = "any" };
        _userManager.Setup(x => x.FindByEmailAsync(request.Email)).ReturnsAsync(new ApplicationUser());
        _signInManager.Setup(x => x.CheckPasswordSignInAsync(It.IsAny<ApplicationUser>(), request.Password, true))
            .ReturnsAsync(Microsoft.AspNetCore.Identity.SignInResult.LockedOut);

        var result = await _sut.LoginAsync(request);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("locked");
    }

    [Fact]
    public async Task VerifyEmailAsync_InvalidCode_ReturnsFailure()
    {
        var userId = Guid.NewGuid().ToString();
        var request = new VerifyEmailRequestDto { UserId = userId, Code = "123456" };
        _userManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(new ApplicationUser { Id = userId });

        var result = await _sut.VerifyEmailAsync(request);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid verification code");
    }

    [Fact]
    public async Task VerifyEmailAsync_ValidCode_ReturnsSuccess()
    {
        var userId = Guid.NewGuid().ToString();
        var code = "123456";
        var user = new ApplicationUser { Id = userId, Email = "test@example.com" };
        
        _context.EmailVerificationCodes.Add(new EmailVerificationCode
        {
            UserId = userId,
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        _userManager.Setup(x => x.FindByIdAsync(userId)).ReturnsAsync(user);
        _userManager.Setup(x => x.UpdateAsync(user)).ReturnsAsync(IdentityResult.Success);

        var result = await _sut.VerifyEmailAsync(new VerifyEmailRequestDto { UserId = userId, Code = code });

        result.Success.Should().BeTrue();
        user.IsEmailVerified.Should().BeTrue();
    }
}
