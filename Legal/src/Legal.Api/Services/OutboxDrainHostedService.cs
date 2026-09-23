using Legal.Application.Abstractions.Services;

namespace Legal.Api.Services;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Outbox drain worker (Phase B).
//
// Periodically resolves a scoped IOutboxDispatcher and processes a batch of
// pending transactional emails. Runs in the API host so invitation emails are
// delivered shortly after the inviting transaction commits. Failures are logged
// and retried by the dispatcher's backoff logic; the loop never crashes the host.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class OutboxDrainHostedService(
    IServiceProvider services,
    ILogger<OutboxDrainHostedService> logger) : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan BusyDelay = TimeSpan.FromMilliseconds(500);
    private const int BatchSize = 20;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                using var scope = services.CreateScope();
                var dispatcher = scope.ServiceProvider.GetRequiredService<IOutboxDispatcher>();
                processed = await dispatcher.ProcessBatchAsync(BatchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Outbox drain iteration failed.");
            }

            try
            {
                await Task.Delay(processed > 0 ? BusyDelay : IdleDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
