// ============================================================
//  UsersController.cs — PastPort.API/Controllers
//
//  GAP 12 FIX: GetUserStats was returning:
//    totalConversations = 0  (hardcoded)
//    joinedDate = DateTime.UtcNow  (always NOW, not real join date)
//  Now queries the real DB values.
//
//  Note: change-password lives here only. AuthController.cs has
//  a duplicate [HttpPost("change-password")] that must be removed
//  (see AuthController.cs fix below).
// ============================================================
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PastPort.Application.DTOs.Request;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using PastPort.Infrastructure.Data;
using System.Security.Claims;

namespace PastPort.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly IAuthService _authService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public UsersController(
        IUserService userService,
        IAuthService authService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _userService = userService;
        _authService = authService;
        _userManager = userManager;
        _context = context;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var profile = await _userService.GetUserProfileAsync(userId);
        if (profile == null) return NotFound(new { message = "User not found" });

        return Ok(new { success = true, data = profile });
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _userService.UpdateUserProfileAsync(userId, request);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// The SINGLE canonical change-password endpoint.
    /// The duplicate in AuthController has been removed.
    /// </summary>
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequestDto request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _authService.ChangePasswordAsync(userId, request);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    [HttpDelete("account")]
    public async Task<IActionResult> DeleteAccount()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _userService.DeleteUserAccountAsync(userId);
        if (!result.Success) return BadRequest(result);

        return Ok(result);
    }

    /// <summary>
    /// GAP 12 FIX: Returns real stats from the database.
    ///
    /// Old (broken) version:
    ///   totalConversations = 0           ← always zero
    ///   joinedDate = DateTime.UtcNow     ← always NOW, never the real date
    ///
    /// New (fixed) version:
    ///   totalConversations = real COUNT from Conversations table
    ///   joinedDate = user.CreatedAt      ← real registration date
    /// </summary>
    [HttpGet("stats")]
    public async Task<IActionResult> GetUserStats()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound(new { message = "User not found" });

        // Real conversation count from DB
        var totalConversations = await _context.Conversations
            .CountAsync(c => c.UserId == userId);

        return Ok(new
        {
            success = true,
            data = new
            {
                totalScenes = 0,                  // Extend when user-owned scenes are added
                totalConversations,                      // FIX: real count
                joinedDate = user.CreatedAt      // FIX: real registration date
            }
        });
    }
}