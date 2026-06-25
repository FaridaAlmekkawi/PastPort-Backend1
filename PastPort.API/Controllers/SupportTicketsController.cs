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
[Route("api/support")]
public class SupportTicketsController(ApplicationDbContext context) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User identity not found.");

    [HttpPost("tickets")]
    public async Task<IActionResult> CreateTicket([FromBody] CreateSupportTicketRequest request)
    {
        var ticket = new SupportTicket
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Subject = request.Subject,
            Message = request.Message,
            Status = SupportTicketStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        context.SupportTickets.Add(ticket);
        await context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetTicket), new { id = ticket.Id }, new
        {
            success = true,
            data = ToResponse(ticket)
        });
    }

    [HttpGet("tickets")]
    public async Task<IActionResult> GetMyTickets()
    {
        var tickets = await context.SupportTickets
            .Where(t => t.UserId == UserId)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();

        return Ok(new { success = true, data = tickets.Select(ToResponse) });
    }

    [HttpGet("tickets/{id:guid}")]
    public async Task<IActionResult> GetTicket(Guid id)
    {
        var ticket = await context.SupportTickets
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == UserId);

        return ticket is null
            ? NotFound(new { message = "Support ticket not found" })
            : Ok(new { success = true, data = ToResponse(ticket) });
    }

    [HttpPut("tickets/{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateTicket(Guid id, [FromBody] UpdateSupportTicketRequest request)
    {
        var ticket = await context.SupportTickets.FirstOrDefaultAsync(t => t.Id == id);

        if (ticket is null)
            return NotFound(new { message = "Support ticket not found" });

        ticket.Status = request.Status;
        ticket.AdminResponse = request.AdminResponse;
        ticket.UpdatedAt = DateTime.UtcNow;
        ticket.ResolvedAt = request.Status == SupportTicketStatus.Resolved
            ? DateTime.UtcNow
            : ticket.ResolvedAt;

        await context.SaveChangesAsync();
        return Ok(new { success = true, data = ToResponse(ticket) });
    }

    private static SupportTicketResponse ToResponse(SupportTicket ticket) => new(
        ticket.Id,
        ticket.Subject,
        ticket.Message,
        ticket.Status.ToString(),
        ticket.AdminResponse,
        ticket.CreatedAt,
        ticket.UpdatedAt,
        ticket.ResolvedAt);
}

public record CreateSupportTicketRequest(string Subject, string Message);

public record UpdateSupportTicketRequest(SupportTicketStatus Status, string? AdminResponse);

public record SupportTicketResponse(
    Guid Id,
    string Subject,
    string Message,
    string Status,
    string? AdminResponse,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? ResolvedAt);
