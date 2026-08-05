using Ams.Application.Abstractions.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ams.Worker.Communications;

public sealed class NotificationDeliveryWorkerService(
    IServiceProvider serviceProvider,
    ILogger<NotificationDeliveryWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var leaseOwner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
        logger.LogInformation("Notification Platform delivery worker started as {LeaseOwner}.", leaseOwner);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<INotificationDeliveryService>();
                var processed = await service.ProcessQueuedAsync(leaseOwner, 25, TimeSpan.FromMinutes(5), stoppingToken);
                if (processed > 0) logger.LogInformation("Notification Platform processed {Count} queued email deliveries.", processed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Notification Platform delivery cycle failed: {Message}", ex.Message); }
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
