using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Documents;
using Ams.Worker.Automation;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Documents;

public sealed class ESignDispatchWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<ESignDispatchWorkerService> _logger;
    private readonly string _workerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    public ESignDispatchWorkerService(
        IServiceProvider serviceProvider,
        IOptions<WorkerOptions> options,
        ILogger<ESignDispatchWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AMS e-sign dispatch worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IESignRepository>();
                var workItems = await repository.ClaimDispatchesAsync(
                    _workerId,
                    Math.Clamp(_options.MaxESignDispatchesPerPoll, 1, 100),
                    TimeSpan.FromMinutes(Math.Clamp(_options.ESignDispatchClaimLeaseMinutes, 1, 120)),
                    stoppingToken);

                if (workItems.Count > 0)
                {
                    var provider = scope.ServiceProvider.GetRequiredService<IESignEnvelopeProvider>();
                    var storage = scope.ServiceProvider.GetRequiredService<IDocumentStorageService>();
                    foreach (var workItem in workItems)
                        await ProcessAsync(repository, provider, storage, workItem, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "E-sign dispatch polling cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, _options.ESignDispatchPollIntervalSeconds)), stoppingToken);
        }
    }

    private async Task ProcessAsync(
        IESignRepository repository,
        IESignEnvelopeProvider provider,
        IDocumentStorageService storage,
        ESignDispatchWorkItem workItem,
        CancellationToken cancellationToken)
    {
        try
        {
            var download = await storage.DownloadAsync(workItem.StoragePath, cancellationToken)
                ?? throw new ESignProviderException("DOCUMENT_NOT_FOUND", "The document content could not be found in storage.", false);
            await using var content = download.Content;
            var result = await provider.SendAsync(workItem, content, cancellationToken);
            await repository.CompleteDispatchAsync(workItem, result, cancellationToken);
            _logger.LogInformation(
                "E-sign request {ESignRequestId} dispatched as DocuSign envelope {EnvelopeId}.",
                workItem.ESignRequestId,
                result.ProviderEnvelopeId);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ESignProviderException exception)
        {
            await repository.FailDispatchAsync(
                workItem,
                new ESignDispatchFailure(exception.ErrorCode, exception.Message, exception.IsRetryable, exception.RetryAtUtc),
                cancellationToken);
            _logger.LogWarning(exception, "E-sign request {ESignRequestId} dispatch failed with {ErrorCode}.", workItem.ESignRequestId, exception.ErrorCode);
        }
        catch (Exception exception)
        {
            await repository.FailDispatchAsync(
                workItem,
                new ESignDispatchFailure("DISPATCH_ERROR", exception.Message, true),
                cancellationToken);
            _logger.LogError(exception, "E-sign request {ESignRequestId} dispatch failed unexpectedly.", workItem.ESignRequestId);
        }
    }
}
