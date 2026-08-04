using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;

namespace Ams.Worker.Documents;

public sealed class DocumentIntakeRetentionWorkerService(IServiceProvider services,ILogger<DocumentIntakeRetentionWorkerService> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            var delay=TimeSpan.FromHours(1);
            try
            {
                using var scope=services.CreateScope();
                var repository=scope.ServiceProvider.GetRequiredService<IDocumentIntakeOperationsRepository>();
                var settings=await repository.GetSettingsAsync(null,stoppingToken);
                delay=TimeSpan.FromMinutes(Math.Clamp(settings.PayloadRetentionWorkerIntervalMinutes,1,1440));
                var store=scope.ServiceProvider.GetRequiredService<IDocumentStorageService>();
                foreach(var payload in await repository.LeaseExpiredPayloadsAsync(settings.PayloadPurgeBatchSize,stoppingToken))
                {
                    try
                    {
                        await store.DeleteAsync(payload.StorageReference,stoppingToken);
                        await repository.RecordPayloadAccessAsync(payload.TenantId,payload.IntakeSessionId,payload.StorageReference,"PURGE","WORKER","DocumentIntakeRetentionWorker","Apply expired raw payload retention.","SUCCEEDED",null,stoppingToken);
                        await repository.CompletePayloadPurgeAsync(payload.TenantId,payload.PayloadGovernanceId,true,"Retention period expired.",stoppingToken);
                    }
                    catch(Exception ex)
                    {
                        await repository.CompletePayloadPurgeAsync(payload.TenantId,payload.PayloadGovernanceId,false,ex.Message,stoppingToken);
                        logger.LogError(ex,"Failed to purge intake payload {PayloadId} at {StorageReference}.",payload.PayloadGovernanceId,payload.StorageReference);
                    }
                }
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogError(ex,"Document intake payload retention cycle failed.");}
            await Task.Delay(delay,stoppingToken);
        }
    }
}
