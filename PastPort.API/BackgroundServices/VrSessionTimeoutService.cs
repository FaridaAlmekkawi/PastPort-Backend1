using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PastPort.Domain.Entities;
using PastPort.Infrastructure.Data;

namespace PastPort.API.BackgroundServices;

public class VrSessionTimeoutService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<VrSessionTimeoutService> _logger;

    public VrSessionTimeoutService(IServiceProvider serviceProvider, ILogger<VrSessionTimeoutService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VR Session Timeout Service starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckActiveSessionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while checking active VR sessions.");
            }

            // Wait 1 minute before checking again
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }

        _logger.LogInformation("VR Session Timeout Service stopping.");
    }

    private async Task CheckActiveSessionsAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        var threshold = DateTime.UtcNow.AddMinutes(-2);

        // Find active sessions that haven't sent a heartbeat in the last 2 minutes
        var timedOutSessions = await context.VrSessions
            .Where(s => s.Status == VrSessionStatus.Active && (s.LastHeartbeat == null || s.LastHeartbeat < threshold))
            .ToListAsync();

        if (timedOutSessions.Any())
        {
            _logger.LogInformation("Found {Count} VR sessions that have timed out. Setting status to Disconnected.", timedOutSessions.Count);

            foreach (var session in timedOutSessions)
            {
                session.Status = VrSessionStatus.Disconnected;
            }

            await context.SaveChangesAsync();
        }
    }
}
