using Ams.Application.Common.Dtos;
using Ams.Application.Features.PremiumFinance;

namespace Ams.Application.Abstractions.Services;

public interface IPremiumFinanceProvider
{
    string ProviderKey { get; }
    Task<PremiumFinanceProviderResultDto> GetQuoteAsync(PremiumFinanceSourceDto source, Guid financeCompanyId, CancellationToken cancellationToken = default);
    Task<PremiumFinanceProviderResultDto> SubmitApplicationAsync(SubmitPremiumFinanceApplicationRequest request, CancellationToken cancellationToken = default);
    Task<PremiumFinanceProviderResultDto> GetApplicationStatusAsync(Guid tenantId, Guid financeAgreementId, CancellationToken cancellationToken = default);
    Task<PremiumFinanceProviderResultDto> GetAgreementAsync(Guid tenantId, Guid financeAgreementId, CancellationToken cancellationToken = default);
    Task<PremiumFinanceProviderResultDto> GetPaymentScheduleAsync(Guid tenantId, Guid financeAgreementId, CancellationToken cancellationToken = default);
    Task<PremiumFinanceProviderResultDto> GetAccountStatusAsync(Guid tenantId, Guid financeAgreementId, CancellationToken cancellationToken = default);
    Task<PremiumFinanceProviderResultDto> GetPayoffAsync(Guid tenantId, Guid financeAgreementId, CancellationToken cancellationToken = default);
    Task<PremiumFinanceProviderResultDto> CancelRequestAsync(CancelPremiumFinanceRequest request, CancellationToken cancellationToken = default);
}

public interface IPremiumFinanceProviderResolver
{
    IPremiumFinanceProvider Resolve(string? providerKey);
}
