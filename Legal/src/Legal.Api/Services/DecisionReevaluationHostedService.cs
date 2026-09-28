using Legal.Application.Abstractions.Intelligence;

namespace Legal.Api.Services;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Continuous Decision Integrity — Phase 2 Reevaluation worker.
//
// Polls for material PROCESSED matter-change events and drives the automatic reevaluation trigger
// (IDecisionReevaluationDispatcher). Mirrors the search-projection worker: short delay when work was
// found, longer idle delay otherwise. Fail-soft: one bad iteration is logged and retried, never crashes
// the host. The dispatcher owns claim/idempotency; this service only paces it.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class DecisionReevaluationHostedService(
    IServiceProvider services,
    ILogger<DecisionReevaluationHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                using var scope = services.CreateScope();
                processed = await scope.ServiceProvider.GetRequiredService<IDecisionReevaluationDispatcher>()
                    .ProcessBatchAsync(20, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Decision reevaluation iteration failed.");
            }

            await Task.Delay(processed > 0 ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
