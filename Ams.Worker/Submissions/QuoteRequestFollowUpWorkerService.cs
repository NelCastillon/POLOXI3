using Ams.Application.Abstractions.Persistence;
using Ams.Worker.Automation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Submissions;

public sealed class QuoteRequestFollowUpWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<QuoteRequestFollowUpWorkerService> _logger;

    public QuoteRequestFollowUpWorkerService(IServiceProvider serviceProvider, IOptions<WorkerOptions> options, ILogger<QuoteRequestFollowUpWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AMS quote request follow-up worker started with {PollIntervalSeconds}s polling interval.", _options.QuoteRequestFollowUpPollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ISubmissionRepository>();
                var updated = await repository.SynchronizeOverdueMarketRequestsAsync(stoppingToken);

                if (updated > 0)
                {
                    _logger.LogInformation("Quote request follow-up worker synchronized {MarketRequestCount} overdue market requests.", updated);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Quote request follow-up worker polling cycle failed: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(60, _options.QuoteRequestFollowUpPollIntervalSeconds)), stoppingToken);
        }
    }
}
