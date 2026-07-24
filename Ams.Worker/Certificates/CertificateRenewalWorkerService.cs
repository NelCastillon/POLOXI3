using Ams.Application.Abstractions.Services;
using Ams.Worker.Automation;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Certificates;

public sealed class CertificateRenewalWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<CertificateRenewalWorkerService> _logger;

    public CertificateRenewalWorkerService(
        IServiceProvider serviceProvider,
        IOptions<WorkerOptions> options,
        ILogger<CertificateRenewalWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(60, _options.CertificateRenewalPollIntervalSeconds));
        var batchSize = Math.Clamp(_options.MaxCertificateRenewalsPerPoll, 1, 250);
        _logger.LogInformation(
            "AMS certificate renewal worker started with {PollIntervalSeconds}s polling and batch size {BatchSize}.",
            interval.TotalSeconds,
            batchSize);

        using var timer = new PeriodicTimer(interval);
        do
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ICertificateWorkflowService>();
                var processed = await service.ProcessDueRenewalsAsync(batchSize, stoppingToken);
                if (processed > 0)
                {
                    _logger.LogInformation(
                        "Certificate renewal worker created {ProcessedCount} auditable renewal requests.",
                        processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Certificate renewal polling cycle failed: {Message}", ex.Message);
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}