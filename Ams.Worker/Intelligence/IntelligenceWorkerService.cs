namespace Ams.Worker.Intelligence;

public sealed class IntelligenceWorkerService(IServiceProvider services,ILogger<IntelligenceWorkerService> logger):BackgroundService
{
    private readonly string _leaseOwner=$"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var nextEvaluationUtc=DateTime.MinValue;
        var nextSearchIndexUtc=DateTime.MinValue;
        var nextPlatformUtc=DateTime.MinValue;
        while(!stoppingToken.IsCancellationRequested)
        {
            var delay=TimeSpan.FromSeconds(10);
            try
            {
                using var scope=services.CreateScope();var processor=scope.ServiceProvider.GetRequiredService<IntelligenceWorkerProcessor>();var settings=await processor.GetSettingsAsync(stoppingToken);delay=TimeSpan.FromSeconds(settings.PollIntervalSeconds);var recommendationCount=await processor.ProcessRecommendationsAsync(_leaseOwner,settings,stoppingToken);if(DateTime.UtcNow>=nextEvaluationUtc){var evaluationCount=await processor.ProcessEvaluationsAsync(_leaseOwner,settings,stoppingToken);nextEvaluationUtc=DateTime.UtcNow.AddMinutes(settings.EvaluationIntervalMinutes);if(evaluationCount>0)logger.LogInformation("Completed {EvaluationCount} intelligence evaluation runs.",evaluationCount);}if(DateTime.UtcNow>=nextSearchIndexUtc){var indexed=await processor.SynchronizeSearchIndexAsync(stoppingToken);nextSearchIndexUtc=DateTime.UtcNow.AddMinutes(settings.SearchIndexIntervalMinutes);if(indexed>0)logger.LogInformation("Synchronized {IndexedCount} changed records into permission-aware intelligence search.",indexed);}if(DateTime.UtcNow>=nextPlatformUtc){var projected=await processor.SynchronizePlatformProjectionsAsync(stoppingToken);var decisionDiscovery=await processor.SynchronizeDecisionAndDiscoveryAsync(stoppingToken);var business=await processor.SynchronizeBusinessIntelligenceAsync(stoppingToken);var processed=await processor.ProcessPlatformWorkAsync(_leaseOwner,settings,stoppingToken);nextPlatformUtc=DateTime.UtcNow.AddSeconds(settings.PlatformIntervalSeconds);if(projected+decisionDiscovery+business+processed>0)logger.LogInformation("Processed {PlatformCount} intelligence pillar projection and work changes.",projected+decisionDiscovery+business+processed);}if(recommendationCount>0)logger.LogInformation("Completed {RecommendationCount} recommendation work items.",recommendationCount);
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogError(ex,"Enterprise intelligence worker cycle failed.");}
            await Task.Delay(delay,stoppingToken);
        }
    }
}
