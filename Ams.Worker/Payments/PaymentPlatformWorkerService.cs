using Ams.Application.Abstractions.Services;
using Ams.Worker.Automation;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Payments;

public sealed class PaymentPlatformWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<PaymentPlatformWorkerService> _logger;

    public PaymentPlatformWorkerService(IServiceProvider serviceProvider, IOptions<WorkerOptions> options, ILogger<PaymentPlatformWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AMS payment platform worker started with {PollIntervalSeconds}s polling interval.", _options.PollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IPaymentPlatformService>();

                var retryCount = await service.ProcessDueRetriesAsync(_options.MaxPaymentRetriesPerPoll, stoppingToken);
                var settlementCount = await service.PollSettlementsAsync(_options.MaxPaymentSettlementCredentialsPerPoll, stoppingToken);

                if (retryCount > 0 || settlementCount > 0)
                {
                    _logger.LogInformation("Payment platform worker processed {RetryCount} retries and {SettlementCount} settlement batches.", retryCount, settlementCount);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment platform worker polling cycle failed: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Max(10, _options.PaymentPollIntervalSeconds)), stoppingToken);
        }
    }
}
