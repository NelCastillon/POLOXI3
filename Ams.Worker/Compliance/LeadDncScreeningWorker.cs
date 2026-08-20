using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Leads;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Ams.Worker.Compliance;

public sealed class LeadDncScreeningWorker : BackgroundService
{
    private const string ScopeCode = "Platform";
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<LeadDncScreeningWorker> _logger;

    public LeadDncScreeningWorker(IServiceProvider serviceProvider, ILogger<LeadDncScreeningWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Lead DNC screening worker started.");
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = TimeSpan.FromMinutes(5);
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var configuration = scope.ServiceProvider.GetRequiredService<IConfigurationService>();
                var enabledSetting = await configuration.GetByKeyAsync("Dnc.ScreeningWorker.Enabled", ScopeCode, null, stoppingToken);
                var intervalSetting = await configuration.GetByKeyAsync("Dnc.ScreeningWorker.PollIntervalSeconds", ScopeCode, null, stoppingToken);
                delay = TimeSpan.FromSeconds(ParseInt(intervalSetting?.SettingValue, intervalSetting?.DefaultValue, 300, 15, 86400));

                if (!ParseBool(enabledSetting?.SettingValue, enabledSetting?.DefaultValue))
                {
                    await Task.Delay(delay, stoppingToken);
                    continue;
                }

                var batchSetting = await configuration.GetByKeyAsync("Dnc.ScreeningWorker.BatchSize", ScopeCode, null, stoppingToken);
                var providerSetting = await configuration.GetByKeyAsync("Dnc.ScreeningWorker.ProviderCode", ScopeCode, null, stoppingToken);
                var providerCode = providerSetting?.SettingValue?.Trim();
                var providers = scope.ServiceProvider.GetServices<IPhoneScreeningProvider>();
                var provider = providers.FirstOrDefault(p => string.Equals(p.ProviderCode, providerCode, StringComparison.OrdinalIgnoreCase));
                if (provider is null)
                {
                    _logger.LogError("DNC screening is enabled but provider code {ProviderCode} has no registered implementation.", providerCode);
                    await Task.Delay(delay, stoppingToken);
                    continue;
                }

                var leadService = scope.ServiceProvider.GetRequiredService<ILeadService>();
                var batchSize = ParseInt(batchSetting?.SettingValue, batchSetting?.DefaultValue, 100, 1, 1000);
                var due = await leadService.GetDuePhoneScreeningsAsync(batchSize, stoppingToken);
                foreach (var phone in due)
                {
                    await ScreenPhoneAsync(leadService, provider, phone.PhoneComplianceProfileId, phone.TenantId, phone.NormalizedPhoneNumber, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lead DNC screening cycle failed: {Message}", ex.Message);
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task ScreenPhoneAsync(ILeadService leadService, IPhoneScreeningProvider provider, Guid profileId, Guid tenantId, string normalizedPhoneNumber, CancellationToken cancellationToken)
    {
        try
        {
            var result = await provider.ScreenAsync(tenantId, normalizedPhoneNumber, cancellationToken);
            await leadService.RecordPhoneScreeningAsync(new RecordPhoneScreeningRequest
            {
                TenantId = tenantId,
                PhoneComplianceProfileId = profileId,
                ProviderCode = provider.ProviderCode,
                RegistryCode = result.RegistryCode,
                JurisdictionCode = result.JurisdictionCode,
                ResultCode = result.ResultCode,
                ScreenedDateUtc = result.ScreenedDateUtc,
                ValidThroughDateUtc = result.ValidThroughDateUtc,
                ProviderReference = result.ProviderReference,
                RawResponseHash = result.RawResponseHash,
                ErrorDetails = result.ErrorDetails
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DNC screening failed for profile {PhoneComplianceProfileId}: {Message}", profileId, ex.Message);
            await leadService.RecordPhoneScreeningAsync(new RecordPhoneScreeningRequest
            {
                TenantId = tenantId,
                PhoneComplianceProfileId = profileId,
                ProviderCode = provider.ProviderCode,
                RegistryCode = "Provider",
                ResultCode = "Failed",
                ScreenedDateUtc = DateTime.UtcNow,
                ErrorDetails = ex.Message
            }, cancellationToken);
        }
    }

    private static bool ParseBool(string? value, string? defaultValue)
        => bool.TryParse(value ?? defaultValue, out var parsed) && parsed;

    private static int ParseInt(string? value, string? defaultValue, int fallback, int minimum, int maximum)
        => Math.Clamp(int.TryParse(value ?? defaultValue, out var parsed) ? parsed : fallback, minimum, maximum);
}
