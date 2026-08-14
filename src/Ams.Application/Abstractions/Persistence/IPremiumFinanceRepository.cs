using Ams.Application.Common.Dtos;
using Ams.Application.Features.PremiumFinance;

namespace Ams.Application.Abstractions.Persistence;

public interface IPremiumFinanceRepository
{
    Task<PremiumFinanceWorkbenchDto> GetWorkbenchAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PremiumFinanceDetailDto?> GetDetailAsync(Guid tenantId, Guid premiumFinanceRequestId, CancellationToken cancellationToken = default);
    Task<PremiumFinanceSourceDto?> GetSourceAsync(Guid tenantId, string sourceTypeCode, Guid sourceId, CancellationToken cancellationToken = default);
    Task<Guid> CreateRequestAsync(CreatePremiumFinanceRequest request, CancellationToken cancellationToken = default);
    Task UpdateRequestAsync(Guid premiumFinanceRequestId, UpdatePremiumFinanceRequest request, CancellationToken cancellationToken = default);
    Task UpdateRequestStatusAsync(Guid premiumFinanceRequestId, UpdatePremiumFinanceStatusRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AddQuoteOptionAsync(AddPremiumFinanceQuoteOptionRequest request, CancellationToken cancellationToken = default);
    Task SelectQuoteOptionAsync(Guid premiumFinanceRequestId, SelectPremiumFinanceQuoteOptionRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateAgreementAsync(SubmitPremiumFinanceApplicationRequest request, CancellationToken cancellationToken = default);
    Task UpdateAgreementAsync(UpdatePremiumFinanceAgreementRequest request, CancellationToken cancellationToken = default);
    Task ReplacePaymentScheduleAsync(ReplacePremiumFinancePaymentScheduleRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AddActivityAsync(AddPremiumFinanceActivityRequest request, CancellationToken cancellationToken = default);
    Task<Guid> LinkDocumentAsync(LinkPremiumFinanceDocumentRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertProviderAsync(UpsertPremiumFinanceProviderRequest request, CancellationToken cancellationToken = default);
    Task CancelRequestAsync(CancelPremiumFinanceRequest request, CancellationToken cancellationToken = default);
}
