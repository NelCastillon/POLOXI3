using Ams.Application.Abstractions.Persistence;
using Ams.Worker.Automation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Search;

public sealed class SearchProjectionWorkerService(
    IServiceProvider serviceProvider,
    IOptions<WorkerOptions> options,
    ILogger<SearchProjectionWorkerService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Search projection worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ISearchMatchingRepository>();
                var changed = await repository.RefreshProjectionsAsync(stoppingToken);
                if (changed > 0) logger.LogInformation("Search projection worker refreshed {ProjectionCount} projections.", changed);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Search projection refresh failed: {Message}", exception.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(30, options.Value.PollIntervalSeconds)), stoppingToken);
        }
    }
}
