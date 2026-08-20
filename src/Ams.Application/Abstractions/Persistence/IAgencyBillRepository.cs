using Ams.Application.Common.Dtos;
using Ams.Application.Features.Billing;

namespace Ams.Application.Abstractions.Persistence;

public interface IAgencyBillRepository
{
    Task<AgencyBillWorkspaceDto> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<AgencyBillSynchronizationResultDto> SynchronizeAsync(SynchronizeAgencyBillRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AgencyBillInstallmentDto>> CreateInstallmentScheduleAsync(CreateAgencyBillInstallmentScheduleRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AllocatePaymentAsync(AllocateAgencyBillPaymentRequest request, CancellationToken cancellationToken = default);
    Task<Guid> ReverseAllocationAsync(ReverseAgencyBillPaymentAllocationRequest request, CancellationToken cancellationToken = default);
    Task<AgencyBillDelinquencyRunResultDto> RunDelinquencyAsync(RunAgencyBillDelinquencyRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateLateNoticeAsync(CreateAgencyBillLateNoticeRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateNonPaymentReferralAsync(CreateNonPaymentReferralRequest request, CancellationToken cancellationToken = default);
    Task ReviewNonPaymentReferralAsync(ReviewNonPaymentReferralRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateReconciliationAsync(CreateAgencyBillReconciliationRequest request, CancellationToken cancellationToken = default);
    Task<Guid> AddReconciliationLineAsync(AddAgencyBillReconciliationLineRequest request, CancellationToken cancellationToken = default);
    Task CompleteReconciliationAsync(CompleteAgencyBillReconciliationRequest request, CancellationToken cancellationToken = default);
    Task<Guid> UpsertFinanceCompanyAsync(UpsertFinanceCompanyRequest request, CancellationToken cancellationToken = default);
    Task<Guid> CreateFinanceAgreementAsync(CreateFinanceAgreementRequest request, CancellationToken cancellationToken = default);
    Task UpdateFinanceAgreementFundingAsync(UpdateFinanceAgreementFundingRequest request, CancellationToken cancellationToken = default);
}
