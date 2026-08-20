using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.PremiumFinance;

namespace Ams.Infrastructure.Services;

public sealed class ManualPremiumFinanceProvider : IPremiumFinanceProvider
{
    public string ProviderKey => "Manual";

    public Task<PremiumFinanceProviderResultDto> GetQuoteAsync(PremiumFinanceSourceDto source, Guid financeCompanyId, CancellationToken cancellationToken = default)
        => ResultAsync("ManualActionRequired", "Obtain terms from the provider and record each option in AgencyBinder.");

    public Task<PremiumFinanceProviderResultDto> SubmitApplicationAsync(SubmitPremiumFinanceApplicationRequest request, CancellationToken cancellationToken = default)
        => ResultAsync("ManuallyRecorded", "The externally submitted application will be tracked in AgencyBinder.", true, request.ProviderApplicationReference);

    public Task<PremiumFinanceProviderResultDto> GetApplicationStatusAsync(Guid tenantId, Guid financeAgreementId, CancellationToken cancellationToken = default)
        => ResultAsync("ManualActionRequired", "Confirm application status with the provider and record the update.");

    public Task<PremiumFinanceProviderResultDto> GetAgreementAsync(Guid tenantId, Guid financeAgreementId, CancellationToken cancellationToken = default)
        => ResultAsync("ManualActionRequired", "Obtain the agreement from the provider and link the document.");

    public Task<PremiumFinanceProviderResultDto> GetPaymentScheduleAsync(Guid tenantId, Guid financeAgreementId, CancellationToken cancellationToken = default)
        => ResultAsync("ManualActionRequired", "Obtain the payment schedule from the provider and synchronize it.");

    public Task<PremiumFinanceProviderResultDto> GetAccountStatusAsync(Guid tenantId, Guid financeAgreementId, CancellationToken cancellationToken = default)
        => ResultAsync("ManualActionRequired", "Confirm account status with the provider and record the update.");

    public Task<PremiumFinanceProviderResultDto> GetPayoffAsync(Guid tenantId, Guid financeAgreementId, CancellationToken cancellationToken = default)
        => ResultAsync("ManualActionRequired", "Obtain a payoff statement from the provider and record its amount and good-through date.");

    public Task<PremiumFinanceProviderResultDto> CancelRequestAsync(CancelPremiumFinanceRequest request, CancellationToken cancellationToken = default)
        => ResultAsync("ManuallyRecorded", "The external provider cancellation must be completed outside AgencyBinder.", true);

    private static Task<PremiumFinanceProviderResultDto> ResultAsync(string status, string message, bool successful = false, string? reference = null)
        => Task.FromResult(new PremiumFinanceProviderResultDto(successful, status, reference, message, [], null, []));
}

public sealed class PremiumFinanceProviderResolver(IEnumerable<IPremiumFinanceProvider> providers) : IPremiumFinanceProviderResolver
{
    private readonly IReadOnlyDictionary<string, IPremiumFinanceProvider> _providers = providers.ToDictionary(x => x.ProviderKey, StringComparer.OrdinalIgnoreCase);

    public IPremiumFinanceProvider Resolve(string? providerKey)
    {
        var key = string.IsNullOrWhiteSpace(providerKey) ? "Manual" : providerKey;
        return _providers.TryGetValue(key, out var provider)
            ? provider
            : throw new InvalidOperationException($"Premium finance provider adapter '{key}' is not registered.");
    }
}
