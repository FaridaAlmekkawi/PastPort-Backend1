// ✅ FIXED: ExternalAuthController.cs
// المشكلة الأصلية: FacebookCallback كان بيستدعي GoogleCallback مباشرة
// ده مشكلة لأن:
// 1. لو Google logic اتغير، Facebook هيتأثر بدون قصد
// 2. الـ external login info بيتحمل بناءً على الـ provider الحالي —
//    لو Facebook redirect وصل لـ GoogleCallback، ممكن الـ info يكون null
//    أو يرجع provider غلط.
// الحل: استخرجنا الـ shared logic في private method واحدة (HandleExternalLoginResult)
// وكل provider عنده callback مستقل بيستدعي الـ helper ده.

using Microsoft.AspNetCore.Authentication.Facebook;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using System.Security.Claims;

namespace PastPort.API.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ExternalAuthController : ControllerBase
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly ILogger<ExternalAuthController> _logger;

    public ExternalAuthController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        IJwtTokenService jwtTokenService,
        ILogger<ExternalAuthController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _jwtTokenService = jwtTokenService;
        _logger = logger;
    }

    /// <summary>
    /// Initiate Google Login — يعمل redirect لـ Google OAuth
    /// </summary>
    [HttpGet("google")]
    public IActionResult GoogleLogin([FromQuery] string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(GoogleCallback), "ExternalAuth", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(
            GoogleDefaults.AuthenticationScheme, redirectUrl);
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Google Login Callback — Google بيرجع هنا بعد الموافقة
    /// </summary>
    [HttpGet("google-callback")]
    public async Task<IActionResult> GoogleCallback(string? returnUrl = null)
    {
        // ✅ كل provider بيستدعي GetExternalLoginInfoAsync بنفسه
        // عشان الـ info بيتحدد من الـ cookie اللي ASP.NET Core بيخزنه
        // بناءً على الـ provider اللي عمل الـ challenge
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogWarning("Failed to load Google external login info");
            return BadRequest(new { error = "Error loading Google login information" });
        }

        var result = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);

        // ✅ FIXED: استخدمنا الـ shared helper بدل تكرار الكود
        return await HandleExternalLoginResult(result, info);
    }

    /// <summary>
    /// Initiate Facebook Login — يعمل redirect لـ Facebook OAuth
    /// </summary>
    [HttpGet("facebook")]
    public IActionResult FacebookLogin([FromQuery] string? returnUrl = null)
    {
        var redirectUrl = Url.Action(nameof(FacebookCallback), "ExternalAuth", new { returnUrl });
        var properties = _signInManager.ConfigureExternalAuthenticationProperties(
            FacebookDefaults.AuthenticationScheme, redirectUrl);
        return Challenge(properties, FacebookDefaults.AuthenticationScheme);
    }

    /// <summary>
    /// Facebook Login Callback — Facebook بيرجع هنا بعد الموافقة
    /// </summary>
    [HttpGet("facebook-callback")]
    public async Task<IActionResult> FacebookCallback(string? returnUrl = null)
    {
        // ✅ FIXED: الكود القديم كان:
        //    return await GoogleCallback(returnUrl);
        // ده كان بيعمل GetExternalLoginInfoAsync داخل Google context
        // مش Facebook context — ممكن يرجع null أو provider غلط.
        // دلوقتي كل callback مستقل ويحمّل الـ info بتاعه هو.
        var info = await _signInManager.GetExternalLoginInfoAsync();
        if (info == null)
        {
            _logger.LogWarning("Failed to load Facebook external login info");
            return BadRequest(new { error = "Error loading Facebook login information" });
        }

        var result = await _signInManager.ExternalLoginSignInAsync(
            info.LoginProvider,
            info.ProviderKey,
            isPersistent: false,
            bypassTwoFactor: true);

        return await HandleExternalLoginResult(result, info);
    }

    // ==================== Private Helper ====================

    /// <summary>
    /// ✅ FIXED: Shared logic لكل الـ external providers
    /// بدل تكرار نفس الكود في كل callback، استخرجناه هنا.
    /// لو محتاج تضيف provider جديد (Apple, Twitter, etc.)،
    /// callback بتاعه بيستدعي الـ method دي بس.
    /// </summary>
    private async Task<IActionResult> HandleExternalLoginResult(
        Microsoft.AspNetCore.Identity.SignInResult result,
        ExternalLoginInfo info)
    {
        // الحالة 1: المستخدم موجود بالفعل → generate tokens مباشرة
        if (result.Succeeded)
        {
            var existingUser = await _userManager.FindByLoginAsync(
                info.LoginProvider,
                info.ProviderKey);

            if (existingUser != null)
            {
                existingUser.LastLoginAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(existingUser);

                var token = await _jwtTokenService.GenerateAccessTokenAsync(existingUser);
                var refreshToken = await _jwtTokenService.CreateRefreshTokenAsync(existingUser);

                _logger.LogInformation(
                    "Existing user logged in via {Provider}: {Email}",
                    info.LoginProvider,
                    existingUser.Email);

                return Ok(new
                {
                    success = true,
                    token,
                    refreshToken = refreshToken.Token,
                    user = new
                    {
                        existingUser.Id,
                        existingUser.Email,
                        existingUser.FirstName,
                        existingUser.LastName
                    }
                });
            }
        }

        // الحالة 2: مستخدم جديد → إنشاء حساب وربطه بالـ provider
        var email = info.Principal.FindFirstValue(ClaimTypes.Email);
        var firstName = info.Principal.FindFirstValue(ClaimTypes.GivenName) ?? "";
        var lastName = info.Principal.FindFirstValue(ClaimTypes.Surname) ?? "";

        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("External provider {Provider} did not return an email", info.LoginProvider);
            return BadRequest(new { error = "Email not provided by external provider" });
        }

        // تحقق لو في user بنفس الـ email بس مش مربوط بالـ provider ده
        var userByEmail = await _userManager.FindByEmailAsync(email);
        if (userByEmail != null)
        {
            // ربط الـ provider بالحساب الموجود
            await _userManager.AddLoginAsync(userByEmail, info);

            var token = await _jwtTokenService.GenerateAccessTokenAsync(userByEmail);
            var refreshToken = await _jwtTokenService.CreateRefreshTokenAsync(userByEmail);

            return Ok(new
            {
                success = true,
                token,
                refreshToken = refreshToken.Token,
                user = new
                {
                    userByEmail.Id,
                    userByEmail.Email,
                    userByEmail.FirstName,
                    userByEmail.LastName
                }
            });
        }

        // إنشاء حساب جديد خالص
        var newUser = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            EmailConfirmed = true,
            IsEmailVerified = true,
            EmailVerifiedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await _userManager.CreateAsync(newUser);
        if (!createResult.Succeeded)
        {
            _logger.LogError(
                "Failed to create user from {Provider}: {Errors}",
                info.LoginProvider,
                string.Join(", ", createResult.Errors.Select(e => e.Description)));

            return BadRequest(new
            {
                error = "Failed to create user",
                errors = createResult.Errors.Select(e => e.Description)
            });
        }

        await _userManager.AddLoginAsync(newUser, info);
        await _userManager.AddToRoleAsync(newUser, "Individual");
        await _signInManager.SignInAsync(newUser, isPersistent: false);

        _logger.LogInformation(
            "New user created via {Provider}: {Email}",
            info.LoginProvider,
            email);

        var newToken = await _jwtTokenService.GenerateAccessTokenAsync(newUser);
        var newRefreshToken = await _jwtTokenService.CreateRefreshTokenAsync(newUser);

        return Ok(new
        {
            success = true,
            token = newToken,
            refreshToken = newRefreshToken.Token,
            user = new
            {
                newUser.Id,
                newUser.Email,
                newUser.FirstName,
                newUser.LastName
            }
        });
    }
}