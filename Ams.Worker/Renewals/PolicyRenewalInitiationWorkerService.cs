using Ams.Application.Abstractions.Services;
using Ams.Application.Features.RenewalRetention;
using Ams.Worker.Automation;
using Microsoft.Extensions.Options;

namespace Ams.Worker.Renewals;

public sealed class PolicyRenewalInitiationWorkerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerOptions _options;
    private readonly ILogger<PolicyRenewalInitiationWorkerService> _logger;

    public PolicyRenewalInitiationWorkerService(
        IServiceProvider serviceProvider,
        IOptions<WorkerOptions> options,
        ILogger<PolicyRenewalInitiationWorkerService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Policy renewal initiation worker started with {PollIntervalSeconds}s polling interval.", _options.RenewalInitiationPollIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IRenewalRetentionService>();
                var result = await service.InitiateEligibleAsync(
                    new InitiateEligibleRenewalsRequest(null, "ExpirationWorker", null, _options.MaxRenewalInitiationsPerPoll),
                    stoppingToken);

                if (result.CreatedCases > 0)
                {
                    _logger.LogInformation("Renewal initiation created {CreatedCases} case(s) from {EligiblePolicyTerms} eligible policy term(s).", result.CreatedCases, result.EligiblePolicyTerms);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Policy renewal initiation polling cycle failed: {Message}", ex.Message);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(_options.RenewalInitiationPollIntervalSeconds, 60, 86400)), stoppingToken);
        }
    }
}
