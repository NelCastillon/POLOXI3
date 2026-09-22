using Legal.Application.Abstractions.Intelligence;

namespace Legal.Api.Services;

public sealed class LegalDocumentSearchProjectionHostedService(
    IServiceProvider services,
    ILogger<LegalDocumentSearchProjectionHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = 0;
            try
            {
                using var scope = services.CreateScope();
                processed = await scope.ServiceProvider.GetRequiredService<ILegalDocumentSearchProjectionDispatcher>()
                    .ProcessBatchAsync(20, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Legal document search projection iteration failed.");
            }

            await Task.Delay(processed > 0 ? TimeSpan.FromMilliseconds(500) : TimeSpan.FromSeconds(15), stoppingToken);
        }
    }
}
