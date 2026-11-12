using Cube.Api.Modules.Notifications.Features;
using Cube.Api.Modules.News;
using Microsoft.EntityFrameworkCore;
namespace Cube.Api;
// Only local in-app reminders. External outbox records remain awaiting configuration.
public sealed class NotificationWorker(IServiceScopeFactory scopes, TimeProvider clock, ILogger<NotificationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { using var scope = scopes.CreateScope(); await scope.ServiceProvider.GetRequiredService<NotificationScheduler>().Tick(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "In-app notification scheduling failed"); }
            try { await Task.Delay(TimeSpan.FromMinutes(1), clock, stoppingToken); } catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
}
