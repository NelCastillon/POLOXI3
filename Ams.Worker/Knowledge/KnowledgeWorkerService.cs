using Ams.Knowledge.Infrastructure.BackgroundProcessing;
using Dapper;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ams.Worker.Knowledge;

public sealed class KnowledgeWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<KnowledgeWorkerService> _logger;
    private readonly string _leaseOwner = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public KnowledgeWorkerService(IServiceProvider serviceProvider, ILogger<KnowledgeWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Insurance Knowledge Center worker started as {LeaseOwner}.", _leaseOwner);

        while (!stoppingToken.IsCancellationRequested)
        {
            var pollSeconds = 30;
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<IKnowledgeBackgroundProcessor>();
                var result = await processor.ProcessBatchAsync(_leaseOwner, stoppingToken);
                pollSeconds = await GetPollSecondsAsync(scope.ServiceProvider, stoppingToken);

                if (result.ImportsProcessed > 0 || result.OutboxMessagesProcessed > 0 || result.Failures > 0)
                {
                    _logger.LogInformation(
                        "Knowledge cycle processed {ImportCount} imports and {MessageCount} semantic messages with {FailureCount} failures.",
                        result.ImportsProcessed,
                        result.OutboxMessagesProcessed,
                        result.Failures);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Knowledge worker polling cycle failed: {Message}", exception.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, pollSeconds)), stoppingToken);
        }
    }

    private static async Task<int> GetPollSecondsAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken)
    {
        var factory = serviceProvider.GetRequiredService<Ams.Knowledge.Infrastructure.Persistence.KnowledgeSqlConnectionFactory>();
        await using var connection = await factory.CreateOpenConnectionAsync(cancellationToken);
        var value = await connection.QuerySingleAsync<string>(new CommandDefinition(
            "SELECT TOP (1) ConfigurationValue FROM knowledge.Configuration WHERE ConfigurationCode = N'WORKER_POLL_SECONDS' AND TenantId IS NULL AND IsActive = 1;",
            cancellationToken: cancellationToken));
        return int.TryParse(value, out var seconds) && seconds > 0 ? seconds : 30;
    }
}
