using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Automation;

public sealed class AutomationWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<AutomationWorkerService> _logger;

    public AutomationWorkerService(IServiceProvider serviceProvider, IOptions<WorkerOptions> options, ILogger<AutomationWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AMS automation worker started with {PollIntervalSeconds}s polling interval.", _options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var orchestrator = scope.ServiceProvider.GetRequiredService<AutomationJobOrchestrator>();
                var nowUtc = DateTime.UtcNow;

                await orchestrator.EnqueueDueSchedulesAsync(nowUtc, _options.MaxDueSchedulesPerPoll, stoppingToken);
                await orchestrator.ExecuteQueuedRunsAsync(_options.MaxQueuedRunsPerPoll, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Automation worker polling cycle failed: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.PollIntervalSeconds)), stoppingToken);
        }
    }
}
