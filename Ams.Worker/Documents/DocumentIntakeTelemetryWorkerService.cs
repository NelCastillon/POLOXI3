using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.DocumentIntake;

namespace Ams.Worker.Documents;

public sealed class DocumentIntakeTelemetryWorkerService(IServiceProvider services,ILogger<DocumentIntakeTelemetryWorkerService> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            var delay=TimeSpan.FromMinutes(5);
            try
            {
                using var scope=services.CreateScope();var repository=scope.ServiceProvider.GetRequiredService<IDocumentIntakeOperationsRepository>();var settings=await repository.GetSettingsAsync(null,stoppingToken);delay=TimeSpan.FromMinutes(Math.Clamp(settings.TelemetrySnapshotIntervalMinutes,1,60));if(settings.TelemetryEnabled){var snapshot=await repository.CaptureTelemetrySnapshotAsync(stoppingToken);await repository.EvaluateSlosAsync(snapshot,stoppingToken);DocumentIntakeTelemetry.QueueDepth.Record(snapshot.QueueDepth);DocumentIntakeTelemetry.OldestQueuedAge.Record(snapshot.OldestQueuedAgeSeconds);DocumentIntakeTelemetry.DeadLetterDepth.Record(snapshot.DeadLetterCount);if(snapshot.DeadLetterCount>0)logger.LogWarning("Document intake has {DeadLetterCount} dead-lettered work items and queue depth {QueueDepth}.",snapshot.DeadLetterCount,snapshot.QueueDepth);}
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogError(ex,"Document intake telemetry and SLO cycle failed.");}
            await Task.Delay(delay,stoppingToken);
        }
    }
}
