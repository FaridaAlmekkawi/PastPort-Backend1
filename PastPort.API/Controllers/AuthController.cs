// ============================================================
//  AuthController.cs — PastPort.API/Controllers
//
//  GAP 13 FIX: Removed the duplicate [HttpPost("change-password")]
//  endpoint that was identical to the one in UsersController.
//  Having both caused ambiguous routing and split responsibility.
//  The canonical location is UsersController (user profile management).
//  AuthController handles auth flows only: register, login, tokens,
//  email verification, password reset, external login.
// ============================================================
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PastPort.Application.DTOs.Request;
using PastPort.Application.Interfaces;
using System.Security.Claims;

namespace PastPort.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController(IAuthService authService) : ControllerBase
{
    // ── Registration & Login ─────────────────────────────────

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await authService.RegisterAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await authService.LoginAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("refresh-token")]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        var result = await authService.RefreshTokenAsync(request.RefreshToken);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var result = await authService.LogoutAsync(userId);
        if (!result) return BadRequest(new { message = "Logout failed" });
        return Ok(new { message = "Logged out successfully" });
    }

    // ── Email Verification ───────────────────────────────────

    [Authorize]
    [HttpPost("send-verification-code")]
    public async Task<IActionResult> SendVerificationCode()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();
        var result = await authService.SendVerificationCodeAsync(userId);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("verify-email")]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await authService.VerifyEmailAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("resend-verification-code")]
    public async Task<IActionResult> ResendVerificationCode([FromBody] ResendVerificationCodeRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await authService.ResendVerificationCodeAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // ── Password Reset ───────────────────────────────────────

    [HttpPost("forgot-password")]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await authService.ForgotPasswordAsync(request);
        return Ok(result); // Always 200 to prevent email enumeration
    }

    [HttpPost("verify-reset-code")]
    public async Task<IActionResult> VerifyResetCode([FromBody] VerifyResetCodeRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await authService.VerifyResetCodeAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);
        var result = await authService.ResetPasswordAsync(request);
        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    // GAP 13 FIX: [HttpPost("change-password")] REMOVED from this controller.
    // It lives exclusively in UsersController to avoid duplicate routes.
    // See: PastPort.API/Controllers/UsersController.cs

    // ── External Login (Web flows) ───────────────────────────

    [HttpGet("external-login/google")]
    public IActionResult GoogleLogin([FromQuery] string returnUrl = "/")
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth",
            new { provider = "Google", returnUrl });
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUrl }, "Google");
    }

    [HttpGet("external-login/facebook")]
    public IActionResult FacebookLogin([FromQuery] string returnUrl = "/")
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth",
            new { provider = "Facebook", returnUrl });
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUrl }, "Facebook");
    }

    [HttpGet("external-login/apple")]
    public IActionResult AppleLogin([FromQuery] string returnUrl = "/")
    {
        var redirectUrl = Url.Action(nameof(ExternalLoginCallback), "Auth",
            new { provider = "Apple", returnUrl });
        return Challenge(new AuthenticationProperties { RedirectUri = redirectUrl }, "Apple");
    }

    [HttpGet("external-login-callback")]
    public async Task<IActionResult> ExternalLoginCallback(
        [FromQuery] string provider,
        [FromQuery] string returnUrl = "/")
    {
        var info = await HttpContext.AuthenticateAsync(provider);
        if (!info.Succeeded)
            return BadRequest(new { message = "External authentication failed" });

        var email = info.Principal?.FindFirst(ClaimTypes.Email)?.Value;
        var firstName = info.Principal?.FindFirst(ClaimTypes.GivenName)?.Value ?? "";
        var lastName = info.Principal?.FindFirst(ClaimTypes.Surname)?.Value ?? "";
        var providerId = info.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(providerId))
            return BadRequest(new { message = "Could not retrieve user information from provider" });

        var result = await authService.ExternalLoginCallbackAsync(new ExternalLoginCallbackDto
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            ProviderId = providerId,
            Provider = provider
        });

        if (!result.Success) return BadRequest(result);
        return Ok(result);
    }

    [HttpPost("external-login/mobile")]
    public Task<IActionResult> MobileExternalLogin([FromBody] ExternalLoginRequestDto request)
        => Task.FromResult<IActionResult>(
            BadRequest(new { message = "Use web login flow or implement token verification per provider." }));
}