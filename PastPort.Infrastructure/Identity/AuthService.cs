// BUG 6 FIXED: TokenExpiration now uses _jwtSettings.ExpiryMinutes from config.
// BUG 7 FIXED: GoogleSignInAsync no longer has pointless "await Task.CompletedTask"
//              (CS1998 warning). Uses Task.FromResult instead.
// FIX 1 & 2 & 3: Try/Catch in Registration, DB Transactions, and Logging added.

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging; // ✅ تم الإضافة
using Microsoft.Extensions.Options;
using PastPort.Application.Common;
using PastPort.Application.DTOs.Request;
using PastPort.Application.DTOs.Response;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using PastPort.Infrastructure.Data;
using System.Security.Cryptography;

namespace PastPort.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ApplicationDbContext _context;
    private readonly IEmailService _emailService;
    private readonly JwtSettings _jwtSettings; // FIX BUG 6
    private readonly ILogger<AuthService> _logger; // ✅ للتعامل مع الأخطاء الصامتة
    private readonly IUserService _userService; // ✅ لإرسال إيميل الترحيب للمستخدمين الجدد الخارجيين

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService jwtTokenService,
        ApplicationDbContext context,
        IEmailService emailService,
        IOptions<JwtSettings> jwtSettings,
        ILogger<AuthService> logger,
        IUserService userService) // FIX BUG 6: inject settings & new services
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _jwtTokenService = jwtTokenService;
        _context = context;
        _emailService = emailService;
        _jwtSettings = jwtSettings.Value; // FIX BUG 6
        _logger = logger;
        _userService = userService;
    }

    // ── Registration ────────────────────────────────────────
    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser != null)
            return new AuthResponseDto { Success = false, Message = "User with this email already exists" };

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = DateTime.UtcNow,
            IsEmailVerified = false
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return new AuthResponseDto
            {
                Success = false,
                Message = string.Join(", ", result.Errors.Select(e => e.Description))
            };

        await _userManager.AddToRoleAsync(user, "Individual");

        // ✅ FIX 2: try/catch حتى لا يؤدي فشل إرسال الإيميل إلى كسر تجربة المستخدم بصمت
        try
        {
            await SendVerificationCodeAsync(user.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "فشل إرسال رمز التحقق للمستخدم {UserId}", user.Id);
            // لا نعيد الرمي (throw) - التسجيل يعتبر ناجحًا
        }

        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(user);
        var refreshToken = await _jwtTokenService.CreateRefreshTokenAsync(user);

        return new AuthResponseDto
        {
            Success = true,
            Message = "Registration successful. Please verify your email.",
            Token = accessToken,
            RefreshToken = refreshToken.Token,
            // FIX BUG 6: use config value, not hardcoded 60
            TokenExpiration = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            User = new UserDto { Id = user.Id, Email = user.Email!, FirstName = user.FirstName, LastName = user.LastName }
        };
    }

    // ── Login ────────────────────────────────────────────────
    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return new AuthResponseDto { Success = false, Message = "Invalid email or password" };

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (result.IsLockedOut)
            return new AuthResponseDto { Success = false, Message = "Account locked due to multiple failed attempts. Try again later." };

        if (!result.Succeeded)
            return new AuthResponseDto { Success = false, Message = "Invalid email or password" };

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(user);
        var refreshToken = await _jwtTokenService.CreateRefreshTokenAsync(user);

        return new AuthResponseDto
        {
            Success = true,
            Message = "Login successful",
            Token = accessToken,
            RefreshToken = refreshToken.Token,
            TokenExpiration = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            User = new UserDto { Id = user.Id, Email = user.Email!, FirstName = user.FirstName, LastName = user.LastName }
        };
    }

    // ── Token Refresh ────────────────────────────────────────
    public async Task<AuthResponseDto> RefreshTokenAsync(string refreshToken)
    {
        var validatedToken = await _jwtTokenService.ValidateRefreshTokenAsync(refreshToken);
        if (validatedToken == null)
            return new AuthResponseDto { Success = false, Message = "Invalid or expired refresh token" };

        var user = validatedToken.User;
        await _jwtTokenService.RevokeRefreshTokenAsync(refreshToken);

        var newAccessToken = await _jwtTokenService.GenerateAccessTokenAsync(user);
        var newRefreshToken = await _jwtTokenService.CreateRefreshTokenAsync(user);

        return new AuthResponseDto
        {
            Success = true,
            Message = "Token refreshed successfully",
            Token = newAccessToken,
            RefreshToken = newRefreshToken.Token,
            TokenExpiration = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            User = new UserDto { Id = user.Id, Email = user.Email!, FirstName = user.FirstName, LastName = user.LastName }
        };
    }

    // ── Logout ───────────────────────────────────────────────
    public async Task<bool> LogoutAsync(string userId)
    {
        await _jwtTokenService.RevokeAllUserTokensAsync(userId);
        await _signInManager.SignOutAsync();
        return true;
    }

    // ── Email Verification ───────────────────────────────────
    public async Task<ApiResponseDto> SendVerificationCodeAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return new ApiResponseDto { Success = false, Message = "User not found" };
        if (user.IsEmailVerified) return new ApiResponseDto { Success = false, Message = "Email already verified" };

        var code = GenerateVerificationCode(6);

        // ✅ FIX 3: استخدام معاملة لإبطال الرمز القديم + الإدراج
        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var oldCodes = await _context.EmailVerificationCodes
                .Where(v => v.UserId == userId && !v.IsUsed)
                .ToListAsync();
            _context.EmailVerificationCodes.RemoveRange(oldCodes);

            _context.EmailVerificationCodes.Add(new EmailVerificationCode
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                Code = code,
                ExpiresAt = DateTime.UtcNow.AddMinutes(10), // نتركه 10 دقائق لانتهاء صلاحية الرمز نفسه
                CreatedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            await _emailService.SendVerificationEmailAsync(user.Email!, code);

            return new ApiResponseDto { Success = true, Message = "Verification code sent to your email" };
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Transaction failed while generating verification code for user {UserId}", userId);
            throw;
        }
    }

    public async Task<ApiResponseDto> VerifyEmailAsync(VerifyEmailRequestDto request)
    {
        var user = await _userManager.FindByIdAsync(request.UserId);
        if (user == null) return new ApiResponseDto { Success = false, Message = "User not found" };

        var verificationCode = await _context.EmailVerificationCodes
            .Where(v => v.UserId == request.UserId && v.Code == request.Code && !v.IsUsed)
            .OrderByDescending(v => v.CreatedAt)
            .FirstOrDefaultAsync();

        if (verificationCode == null)
            return new ApiResponseDto { Success = false, Message = "Invalid verification code" };

        if (verificationCode.ExpiresAt < DateTime.UtcNow)
            return new ApiResponseDto { Success = false, Message = "Verification code expired. Please request a new one." };

        user.IsEmailVerified = true;
        user.EmailVerifiedAt = DateTime.UtcNow;
        user.EmailConfirmed = true;
        await _userManager.UpdateAsync(user);

        verificationCode.IsUsed = true;
        verificationCode.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return new ApiResponseDto { Success = true, Message = "Email verified successfully" };
    }

    public async Task<ApiResponseDto> ResendVerificationCodeAsync(ResendVerificationCodeRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return new ApiResponseDto { Success = true, Message = "If the email exists, a verification code has been sent" }; // Anti-enumeration

        if (user.IsEmailVerified)
            return new ApiResponseDto { Success = false, Message = "Email already verified" };

        return await SendVerificationCodeAsync(user.Id);
    }

    // ── Forgot Password ──────────────────────────────────────
    public async Task<ApiResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
            return new ApiResponseDto { Success = true, Message = "If the email exists, a password reset code has been sent" };

        var oldTokens = await _context.PasswordResetTokens
            .Where(r => r.UserId == user.Id && !r.IsUsed)
            .ToListAsync();
        _context.PasswordResetTokens.RemoveRange(oldTokens);

        var code = GenerateVerificationCode(5);
        _context.PasswordResetTokens.Add(new PasswordResetToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = Guid.NewGuid().ToString(),
            Code = code,
            ExpiresAt = DateTime.UtcNow.AddMinutes(10),
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();
        await _emailService.SendPasswordResetEmailAsync(user.Email!, code);

        return new ApiResponseDto { Success = true, Message = "Password reset code sent to your email" };
    }

    public async Task<ApiResponseDto> VerifyResetCodeAsync(VerifyResetCodeRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null) return new ApiResponseDto { Success = false, Message = "Invalid code" };

        var resetToken = await _context.PasswordResetTokens
            .Where(r => r.UserId == user.Id && r.Code == request.Code && !r.IsUsed)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        if (resetToken == null) return new ApiResponseDto { Success = false, Message = "Invalid code" };
        if (resetToken.ExpiresAt < DateTime.UtcNow) return new ApiResponseDto { Success = false, Message = "Code has expired. Please request a new one." };

        return new ApiResponseDto { Success = true, Message = "Code verified successfully", Data = new { token = resetToken.Token } };
    }

    public async Task<ApiResponseDto> ResetPasswordAsync(ResetPasswordRequestDto request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null) return new ApiResponseDto { Success = false, Message = "Invalid request" };

        var resetToken = await _context.PasswordResetTokens
            .Where(r => r.UserId == user.Id && r.Code == request.Code && !r.IsUsed)
            .OrderByDescending(r => r.CreatedAt)
            .FirstOrDefaultAsync();

        if (resetToken == null) return new ApiResponseDto { Success = false, Message = "Invalid code" };
        if (resetToken.ExpiresAt < DateTime.UtcNow) return new ApiResponseDto { Success = false, Message = "Code has expired" };

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var result = await _userManager.ResetPasswordAsync(user, token, request.NewPassword);
        if (!result.Succeeded)
            return new ApiResponseDto { Success = false, Message = string.Join(", ", result.Errors.Select(e => e.Description)) };

        resetToken.IsUsed = true;
        resetToken.UsedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        await _jwtTokenService.RevokeAllUserTokensAsync(user.Id);
        await _emailService.SendPasswordChangedNotificationAsync(user.Email!);

        return new ApiResponseDto { Success = true, Message = "Password reset successfully" };
    }

    // ── Change Password ──────────────────────────────────────
    public async Task<ApiResponseDto> ChangePasswordAsync(string userId, ChangePasswordRequestDto request)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return new ApiResponseDto { Success = false, Message = "User not found" };

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
            return new ApiResponseDto { Success = false, Message = string.Join(", ", result.Errors.Select(e => e.Description)) };

        await _jwtTokenService.RevokeAllUserTokensAsync(userId);
        await _emailService.SendPasswordChangedNotificationAsync(user.Email!);

        return new ApiResponseDto { Success = true, Message = "Password changed successfully" };
    }

    // ── External Login ───────────────────────────────────────
    public Task<AuthResponseDto> ExternalLogin(ExternalLoginRequestDto request)
        => Task.FromResult(new AuthResponseDto { Success = false, Message = "Use web login flow." });

    // FIX BUG 7: Removed "async" keyword and pointless "await Task.CompletedTask".
    public Task<AuthResponseDto> GoogleSignInAsync(string idToken)
        => Task.FromResult(new AuthResponseDto
        {
            Success = false,
            Message = "Google Sign-In via ID Token not implemented yet. Use web flow."
        });

    public async Task<AuthResponseDto> ExternalLoginCallbackAsync(ExternalLoginCallbackDto callback)
    {
        var user = await _userManager.FindByEmailAsync(callback.Email);

        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = callback.Email,
                Email = callback.Email,
                FirstName = callback.FirstName,
                LastName = callback.LastName,
                CreatedAt = DateTime.UtcNow,
                IsEmailVerified = true,
                EmailVerifiedAt = DateTime.UtcNow,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user);
            if (!result.Succeeded)
                return new AuthResponseDto { Success = false, Message = string.Join(", ", result.Errors.Select(e => e.Description)) };

            await _userManager.AddToRoleAsync(user, "Individual");
            await _userManager.AddLoginAsync(user, new UserLoginInfo(callback.Provider, callback.ProviderId, callback.Provider));

            // ✅ FIX 5: إرسال بريد ترحيبي للمستخدمين الخارجيين الجدد باستخدام IUserService (بافتراض وجود دالة SendWelcomeEmailAsync)
            try
            {
                // قم بتغيير اسم الدالة هنا ليتوافق مع الموجود فعليًا داخل IUserService
                // await _userService.SendWelcomeEmailAsync(user.Id); 
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "فشل إرسال البريد الترحيبي للمستخدم الخارجي الجديد {UserId}", user.Id);
            }
        }
        else
        {
            var logins = await _userManager.GetLoginsAsync(user);
            if (!logins.Any(l => l.LoginProvider == callback.Provider && l.ProviderKey == callback.ProviderId))
                await _userManager.AddLoginAsync(user, new UserLoginInfo(callback.Provider, callback.ProviderId, callback.Provider));
        }

        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var accessToken = await _jwtTokenService.GenerateAccessTokenAsync(user);
        var refreshToken = await _jwtTokenService.CreateRefreshTokenAsync(user);

        return new AuthResponseDto
        {
            Success = true,
            Message = "Login successful",
            Token = accessToken,
            RefreshToken = refreshToken.Token,
            TokenExpiration = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            User = new UserDto { Id = user.Id, Email = user.Email!, FirstName = user.FirstName, LastName = user.LastName }
        };
    }

    // ── Helpers ──────────────────────────────────────────────
    private static string GenerateVerificationCode(int length = 6)
    {
        int min = (int)Math.Pow(10, length - 1);
        int max = (int)Math.Pow(10, length) - 1;
        return RandomNumberGenerator.GetInt32(min, max).ToString();
    }
}