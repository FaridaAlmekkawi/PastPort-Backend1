using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PastPort.Application.DTOs.Request;
using PastPort.Application.Interfaces;
using PastPort.Domain.Entities;
using PastPort.Infrastructure.Data;
using System.Security.Claims;
using System.Linq;

namespace PastPort.API.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _context;

    public UsersController(
        IUserService userService,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context)
    {
        _userService = userService;
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
        // FIX 3: Standardize validation errors
        if (!ModelState.IsValid) return ValidationError();

        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var result = await _userService.UpdateUserProfileAsync(userId, request);
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

    [HttpGet("stats")]
    public async Task<IActionResult> GetUserStats()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return Unauthorized();

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound(new { message = "User not found" });

        // FIX 1: Implement real stats from DB
        var totalConversations = await _context.Conversations
            .CountAsync(c => c.UserId == userId);

        return Ok(new
        {
            success = true,
            data = new
            {
                totalConversations,
                joinedDate = user.CreatedAt
            }
        });
    }

    // FIX 3: Helper method to standardize validation error responses
    private IActionResult ValidationError() =>
        BadRequest(new
        {
            success = false,
            message = "Validation failed",
            errors = ModelState
                .Where(x => x.Value?.Errors.Any() == true)
                .ToDictionary(
                    kvp => kvp.Key,
                    kvp => kvp.Value!.Errors.Select(e => e.ErrorMessage))
        });
}