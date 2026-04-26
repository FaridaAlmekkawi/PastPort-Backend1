using PastPort.Application.DTOs.Request;
using PastPort.Application.DTOs.Response;

namespace PastPort.Application.Interfaces;

/// <summary>
/// Defines the authentication and identity management contract for the PastPort platform.
/// Handles user registration, login, token lifecycle, email verification,
/// password management, and external OAuth provider integration.
/// </summary>
public interface IAuthService
{
    // ── Registration & Login ────────────────────────────────────

    /// <summary>
    /// Registers a new user account and returns JWT credentials.
    /// </summary>
    /// <param name="request">Registration data including email, password, and name.</param>
    /// <returns>An <see cref="AuthResponseDto"/> containing the JWT and refresh token on success.</returns>
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request);

    /// <summary>
    /// Authenticates a user with email/password and returns JWT credentials.
    /// </summary>
    /// <param name="request">Login credentials.</param>
    /// <returns>An <see cref="AuthResponseDto"/> containing the JWT and refresh token on success.</returns>
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request);

    /// <summary>
    /// Exchanges a valid refresh token for a new JWT + refresh token pair.
    /// </summary>
    /// <param name="refreshToken">The current refresh token.</param>
    /// <returns>An <see cref="AuthResponseDto"/> with new credentials.</returns>
    Task<AuthResponseDto> RefreshTokenAsync(string refreshToken);

    /// <summary>
    /// Invalidates the user's refresh token, effectively logging them out.
    /// </summary>
    /// <param name="userId">The ID of the user to log out.</param>
    /// <returns><c>true</c> if logout succeeded; otherwise <c>false</c>.</returns>
    Task<bool> LogoutAsync(string userId);

    // ── Email Verification ──────────────────────────────────────

    /// <summary>
    /// Generates and sends a verification code to the user's email address.
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    Task<ApiResponseDto> SendVerificationCodeAsync(string userId);

    /// <summary>
    /// Verifies the user's email using the code that was sent to them.
    /// </summary>
    /// <param name="request">The email and verification code.</param>
    Task<ApiResponseDto> VerifyEmailAsync(VerifyEmailRequestDto request);

    /// <summary>
    /// Resends the email verification code if the previous one expired.
    /// </summary>
    /// <param name="request">The user's email address.</param>
    Task<ApiResponseDto> ResendVerificationCodeAsync(ResendVerificationCodeRequestDto request);

    // ── Password Reset ──────────────────────────────────────────

    /// <summary>
    /// Initiates the password reset flow by sending a reset code via email.
    /// Always succeeds (returns 200) to prevent email enumeration attacks.
    /// </summary>
    /// <param name="request">The user's email address.</param>
    Task<ApiResponseDto> ForgotPasswordAsync(ForgotPasswordRequestDto request);

    /// <summary>
    /// Validates a password reset code before allowing the user to set a new password.
    /// </summary>
    /// <param name="request">The email and reset code to verify.</param>
    Task<ApiResponseDto> VerifyResetCodeAsync(VerifyResetCodeRequestDto request);

    /// <summary>
    /// Resets the user's password using a previously verified reset code.
    /// </summary>
    /// <param name="request">The email, reset code, and new password.</param>
    Task<ApiResponseDto> ResetPasswordAsync(ResetPasswordRequestDto request);

    // ── Change Password ─────────────────────────────────────────

    /// <summary>
    /// Changes the authenticated user's password (requires current password verification).
    /// </summary>
    /// <param name="userId">The ID of the authenticated user.</param>
    /// <param name="request">The current and new passwords.</param>
    Task<ApiResponseDto> ChangePasswordAsync(string userId, ChangePasswordRequestDto request);

    // ── External Login ──────────────────────────────────────────

    /// <summary>
    /// Initiates an external OAuth login flow for mobile clients.
    /// </summary>
    /// <param name="request">External login provider details.</param>
    Task<AuthResponseDto> ExternalLogin(ExternalLoginRequestDto request);

    /// <summary>
    /// Processes the OAuth callback from an external provider (Google, Facebook, Apple).
    /// Creates or links the user account and returns JWT credentials.
    /// </summary>
    /// <param name="callback">The provider's callback data including email and provider ID.</param>
    Task<AuthResponseDto> ExternalLoginCallbackAsync(ExternalLoginCallbackDto callback);

    /// <summary>
    /// Authenticates a user using a Google ID token (for mobile clients that
    /// handle Google Sign-In client-side and send the token to the backend).
    /// </summary>
    /// <param name="idToken">The Google-issued ID token.</param>
    Task<AuthResponseDto> GoogleSignInAsync(string idToken);
}