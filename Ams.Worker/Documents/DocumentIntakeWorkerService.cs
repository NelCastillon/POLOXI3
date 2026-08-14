namespace Ams.Worker.Documents;

public sealed class DocumentIntakeWorkerService : BackgroundService
{
    private readonly IServiceProvider _services;private readonly ILogger<DocumentIntakeWorkerService> _logger;private readonly string _leaseOwner=$"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";
    public DocumentIntakeWorkerService(IServiceProvider services,ILogger<DocumentIntakeWorkerService> logger){_services=services;_logger=logger;}
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AI document intake worker started as {LeaseOwner}.",_leaseOwner);
        while(!stoppingToken.IsCancellationRequested)
        {
            var pollInterval=TimeSpan.FromSeconds(10);
            try{using var scope=_services.CreateScope();var settings=await scope.ServiceProvider.GetRequiredService<Ams.Application.Abstractions.Persistence.IDocumentIntakeOperationsRepository>().GetSettingsAsync(null,stoppingToken);pollInterval=TimeSpan.FromSeconds(Math.Clamp(settings.WorkerPollIntervalSeconds,1,3600));var result=await scope.ServiceProvider.GetRequiredService<IDocumentIntakeProcessor>().ProcessBatchAsync(_leaseOwner,settings.WorkerBatchSize,TimeSpan.FromSeconds(Math.Clamp(settings.LeaseDurationSeconds,30,3600)),stoppingToken);if(result.Completed+result.Retried+result.Failed>0)_logger.LogInformation("Document intake cycle completed {Completed}, retried {Retried}, and failed {Failed} work items.",result.Completed,result.Retried,result.Failed);}
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){_logger.LogError(ex,"Document intake worker cycle failed.");}
            await Task.Delay(pollInterval,stoppingToken);
        }
    }
}
