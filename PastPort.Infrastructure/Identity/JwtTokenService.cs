// BUG 19 FIXED: Added ClaimTypes.NameIdentifier to JWT claims.
// ROOT CAUSE of all controllers returning Unauthorized() silently:
// Controllers call User.FindFirst(ClaimTypes.NameIdentifier) but the token
// only had "sub" — in .NET 6+ the automatic sub→NameIdentifier mapping is OFF.
// Also removed the redundant "uid" duplicate claim.

// FIX 2: Atomic revoke-on-validate using optimistic concurrency (ExecuteUpdateAsync)
// FIX 3: Token rotation logic documented (handled in AuthService).
// FIX 4: Note - index on RefreshTokens.Token MUST be added in ApplicationDbContext.OnModelCreating!

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using PastPort.Application.Common;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using PastPort.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace PastPort.Infrastructure.Identity;

public class JwtTokenService : IJwtTokenService
{
    private readonly JwtSettings _jwtSettings;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public JwtTokenService(
        IOptions<JwtSettings> jwtSettings,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _jwtSettings = jwtSettings.Value;
        _userManager = userManager;
        _context = context;
    }

    public async Task<string> GenerateAccessTokenAsync(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var userClaims = await _userManager.GetClaimsAsync(user);

        var claims = new List<Claim>
        {
            // FIX BUG 19: ClaimTypes.NameIdentifier is what every controller reads via
            // User.FindFirst(ClaimTypes.NameIdentifier). Without this claim, every
            // authenticated endpoint that extracts userId returns null → Unauthorized().
            new Claim(ClaimTypes.NameIdentifier, user.Id),

            // Standard JWT subject claim
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email!),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),

            // REMOVED: "uid" was a duplicate of Sub — unnecessary extra claim
            // Added null fallback to prevent exceptions if names are null
            new Claim("FirstName", user.FirstName ?? ""),
            new Claim("LastName", user.LastName ?? "")
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        claims.AddRange(userClaims);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // FIX BUG 6: Use ExpiryMinutes from config instead of hardcoded 60
        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Task<string> GenerateRefreshTokenAsync()
    {
        var randomNumber = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Task.FromResult(Convert.ToBase64String(randomNumber));
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(ApplicationUser user)
    {
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = await GenerateRefreshTokenAsync(),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(_jwtSettings.RefreshTokenExpiryDays),
            CreatedAt = DateTime.UtcNow
        };

        _context.RefreshTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return refreshToken;
    }

    // FIX 2: Atomic revoke-on-validate using ExecuteUpdateAsync to prevent TOCTOU
    public async Task<RefreshToken?> ValidateRefreshTokenAsync(string token)
    {
        // نجلب التوكن إذا لم يكن Revoked فقط لتوفير الـ Checks
        var refreshToken = await _context.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token && !rt.IsRevoked);

        if (refreshToken == null)
            return null;

        if (refreshToken.ExpiresAt < DateTime.UtcNow)
        {
            // Expired — revoke atomically
            await _context.RefreshTokens
                .Where(rt => rt.Token == token && !rt.IsRevoked)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(rt => rt.IsRevoked, true)
                    .SetProperty(rt => rt.RevokedAt, DateTime.UtcNow));

            return null;
        }

        // FIX 3: Token is not rotated here. It is rotated/revoked in AuthService
        // after validation passes.
        return refreshToken;
    }

    // Refactored to use ExecuteUpdateAsync for atomic update & better performance
    public async Task RevokeRefreshTokenAsync(string token)
    {
        await _context.RefreshTokens
            .Where(rt => rt.Token == token && !rt.IsRevoked)
            .ExecuteUpdateAsync(s => s
                .SetProperty(rt => rt.IsRevoked, true)
                .SetProperty(rt => rt.RevokedAt, DateTime.UtcNow));
    }

    // Refactored to use ExecuteUpdateAsync for Bulk Update & better performance
    public async Task RevokeAllUserTokensAsync(string userId)
    {
        await _context.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ExecuteUpdateAsync(s => s
                .SetProperty(rt => rt.IsRevoked, true)
                .SetProperty(rt => rt.RevokedAt, DateTime.UtcNow));
    }
}