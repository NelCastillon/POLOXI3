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
            try{using var scope=_services.CreateScope();var result=await scope.ServiceProvider.GetRequiredService<IDocumentIntakeProcessor>().ProcessBatchAsync(_leaseOwner,stoppingToken);if(result.Completed+result.Retried+result.Failed>0)_logger.LogInformation("Document intake cycle completed {Completed}, retried {Retried}, and failed {Failed} work items.",result.Completed,result.Retried,result.Failed);}
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){_logger.LogError(ex,"Document intake worker cycle failed.");}
            await Task.Delay(TimeSpan.FromSeconds(10),stoppingToken);
        }
    }
}
