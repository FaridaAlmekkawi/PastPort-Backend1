using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PastPort.Domain.Entities;
using PastPort.Infrastructure.Data;

namespace PastPort.API.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationsController(ApplicationDbContext context) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User identity not found.");

    [HttpGet]
    public async Task<IActionResult> GetMyNotifications([FromQuery] bool unreadOnly = false)
    {
        var query = context.Notifications.Where(n => n.UserId == UserId);

        if (unreadOnly)
            query = query.Where(n => !n.IsRead);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync();

        return Ok(new { success = true, data = notifications.Select(ToResponse) });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount()
    {
        var count = await context.Notifications
            .CountAsync(n => n.UserId == UserId && !n.IsRead);

        return Ok(new { success = true, data = new { unreadCount = count } });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> CreateNotification([FromBody] CreateNotificationRequest request)
    {
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = request.UserId,
            Title = request.Title,
            Body = request.Body,
            Type = string.IsNullOrWhiteSpace(request.Type) ? "general" : request.Type,
            ActionUrl = request.ActionUrl,
            CreatedAt = DateTime.UtcNow
        };

        context.Notifications.Add(notification);
        await context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetMyNotifications), new { }, new
        {
            success = true,
            data = ToResponse(notification)
        });
    }

    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkAsRead(Guid id)
    {
        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);

        if (notification is null)
            return NotFound(new { message = "Notification not found" });

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(new { success = true, data = ToResponse(notification) });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var notifications = await context.Notifications
            .Where(n => n.UserId == UserId && !n.IsRead)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        return Ok(new { success = true, data = new { updated = notifications.Count } });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteNotification(Guid id)
    {
        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == id && n.UserId == UserId);

        if (notification is null)
            return NotFound(new { message = "Notification not found" });

        context.Notifications.Remove(notification);
        await context.SaveChangesAsync();

        return Ok(new { success = true });
    }

    private static NotificationResponse ToResponse(Notification notification) => new(
        notification.Id,
        notification.Title,
        notification.Body,
        notification.Type,
        notification.ActionUrl,
        notification.IsRead,
        notification.ReadAt,
        notification.CreatedAt);
}

public record CreateNotificationRequest(
    string UserId,
    string Title,
    string Body,
    string? Type,
    string? ActionUrl);

public record NotificationResponse(
    Guid Id,
    string Title,
    string Body,
    string Type,
    string? ActionUrl,
    bool IsRead,
    DateTime? ReadAt,
    DateTime CreatedAt);
