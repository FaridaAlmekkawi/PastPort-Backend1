using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PastPort.Domain.Entities;
using PastPort.Domain.Enums;
using PastPort.Infrastructure.Data;

namespace PastPort.API.Controllers;

[Authorize]
[ApiController]
[Route("api/experiences")]
public class ExperiencesController(ApplicationDbContext context) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User identity not found.");

    [HttpPost]
    public async Task<IActionResult> StartExperience([FromBody] StartExperienceRequest request)
    {
        if (!ModelState.IsValid) return BadRequest(ModelState);

        var vrSessionGuid = Guid.NewGuid();
        var vrSessionId = vrSessionGuid.ToString();
        var vrSession = new VrSession
        {
            Id = vrSessionGuid,
            SessionId = vrSessionId,
            UserId = UserId,
            Status = VrSessionStatus.Pending,
            Civilization = request.Civilization,
            YearRange = string.Empty,
            LocationOldName = request.LocationOldName,
            RoleOrName = request.RoleOrName,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(4)
        };

        var experience = new UserExperience
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            SceneId = request.SceneId,
            Civilization = request.Civilization,
            YearRange = string.Empty,
            LocationOldName = request.LocationOldName,
            RoleOrName = request.RoleOrName,
            Goal = request.Goal,
            VrSessionId = vrSessionId,
            Status = ExperienceStatus.Started,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };

        context.VrSessions.Add(vrSession);
        context.UserExperiences.Add(experience);
        await context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetExperience), new { id = experience.Id }, new
        {
            success = true,
            data = ToResponse(experience)
        });
    }

    [HttpGet]
    public async Task<IActionResult> GetMyExperiences([FromQuery] ExperienceStatus? status = null)
    {
        var query = context.UserExperiences
            .Include(x => x.Scene)
            .Where(x => x.UserId == UserId);

        if (status.HasValue)
            query = query.Where(x => x.Status == status.Value);

        var experiences = await query
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return Ok(new { success = true, data = experiences.Select(ToResponse) });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetExperience(Guid id)
    {
        var experience = await context.UserExperiences
            .Include(x => x.Scene)
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);

        return experience is null
            ? NotFound(new { message = "Experience not found" })
            : Ok(new { success = true, data = ToResponse(experience) });
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> CompleteExperience(Guid id)
    {
        var experience = await context.UserExperiences
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);

        if (experience is null)
            return NotFound(new { message = "Experience not found" });

        experience.Status = ExperienceStatus.Completed;
        experience.CompletedAt = DateTime.UtcNow;
        experience.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(new { success = true, data = ToResponse(experience) });
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> CancelExperience(Guid id)
    {
        var experience = await context.UserExperiences
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId);

        if (experience is null)
            return NotFound(new { message = "Experience not found" });

        experience.Status = ExperienceStatus.Cancelled;
        experience.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(new { success = true, data = ToResponse(experience) });
    }

    private static ExperienceResponse ToResponse(UserExperience experience) => new(
        experience.Id,
        experience.SceneId,
        experience.Scene?.Title,
        experience.Civilization,
        experience.YearRange,
        experience.LocationOldName,
        experience.RoleOrName,
        experience.Goal,
        experience.VrSessionId,
        experience.Status.ToString(),
        experience.StartedAt,
        experience.CompletedAt,
        experience.CreatedAt);
}

public record StartExperienceRequest(
    Guid? SceneId,
    string Civilization,
    string LocationOldName,
    string? RoleOrName,
    string Goal);

public record ExperienceResponse(
    Guid Id,
    Guid? SceneId,
    string? SceneTitle,
    string Civilization,
    string YearRange,
    string LocationOldName,
    string? RoleOrName,
    string Goal,
    string? VrSessionId,
    string Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    DateTime CreatedAt);
