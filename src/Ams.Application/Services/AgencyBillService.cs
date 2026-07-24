using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Billing;

namespace Ams.Application.Services;

public sealed class AgencyBillService(IAgencyBillRepository repository) : IAgencyBillService
{
    public Task<AgencyBillWorkspaceDto> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetWorkspaceAsync(tenantId, cancellationToken);

    public Task<AgencyBillSynchronizationResultDto> SynchronizeAsync(SynchronizeAgencyBillRequest request, CancellationToken cancellationToken = default)
        => repository.SynchronizeAsync(request, cancellationToken);

    public Task<IReadOnlyList<AgencyBillInstallmentDto>> CreateInstallmentScheduleAsync(CreateAgencyBillInstallmentScheduleRequest request, CancellationToken cancellationToken = default)
        => repository.CreateInstallmentScheduleAsync(request, cancellationToken);

    public Task<Guid> AllocatePaymentAsync(AllocateAgencyBillPaymentRequest request, CancellationToken cancellationToken = default)
        => repository.AllocatePaymentAsync(request, cancellationToken);

    public Task<Guid> ReverseAllocationAsync(ReverseAgencyBillPaymentAllocationRequest request, CancellationToken cancellationToken = default)
        => repository.ReverseAllocationAsync(request, cancellationToken);

    public Task<AgencyBillDelinquencyRunResultDto> RunDelinquencyAsync(RunAgencyBillDelinquencyRequest request, CancellationToken cancellationToken = default)
        => repository.RunDelinquencyAsync(request, cancellationToken);

    public Task<Guid> CreateLateNoticeAsync(CreateAgencyBillLateNoticeRequest request, CancellationToken cancellationToken = default)
        => repository.CreateLateNoticeAsync(request, cancellationToken);

    public Task<Guid> CreateNonPaymentReferralAsync(CreateNonPaymentReferralRequest request, CancellationToken cancellationToken = default)
        => repository.CreateNonPaymentReferralAsync(request, cancellationToken);

    public Task ReviewNonPaymentReferralAsync(ReviewNonPaymentReferralRequest request, CancellationToken cancellationToken = default)
        => repository.ReviewNonPaymentReferralAsync(request, cancellationToken);

    public Task<Guid> CreateReconciliationAsync(CreateAgencyBillReconciliationRequest request, CancellationToken cancellationToken = default)
        => repository.CreateReconciliationAsync(request, cancellationToken);

    public Task<Guid> AddReconciliationLineAsync(AddAgencyBillReconciliationLineRequest request, CancellationToken cancellationToken = default)
        => repository.AddReconciliationLineAsync(request, cancellationToken);

    public Task CompleteReconciliationAsync(CompleteAgencyBillReconciliationRequest request, CancellationToken cancellationToken = default)
        => repository.CompleteReconciliationAsync(request, cancellationToken);

    public Task<Guid> UpsertFinanceCompanyAsync(UpsertFinanceCompanyRequest request, CancellationToken cancellationToken = default)
        => repository.UpsertFinanceCompanyAsync(request, cancellationToken);

    public Task<Guid> CreateFinanceAgreementAsync(CreateFinanceAgreementRequest request, CancellationToken cancellationToken = default)
        => repository.CreateFinanceAgreementAsync(request, cancellationToken);

    public Task UpdateFinanceAgreementFundingAsync(UpdateFinanceAgreementFundingRequest request, CancellationToken cancellationToken = default)
        => repository.UpdateFinanceAgreementFundingAsync(request, cancellationToken);
}
