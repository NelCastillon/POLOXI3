using Ams.Application.Common.Dtos;
using Ams.Application.Features.Commissions;

namespace Ams.Application.Abstractions.Persistence;

public interface ICommissionAccountingRepository
{
    Task<CommissionAccountingWorkspaceDto> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CarrierCommissionStatementLineDto>> GetStatementLinesAsync(Guid tenantId, Guid statementId, CancellationToken cancellationToken = default);
    Task<CommissionImportResultDto> ImportStatementAsync(ImportCarrierCommissionStatementRequest request, CancellationToken cancellationToken = default);
    Task<CommissionMatchRunResultDto> RunMatchingAsync(RunCommissionMatchingRequest request, CancellationToken cancellationToken = default);
    Task ApproveMatchAsync(ApproveCommissionMatchRequest request, CancellationToken cancellationToken = default);
    Task ResolveExceptionAsync(ResolveCommissionReconciliationExceptionRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Guid>> CreatePayablesAsync(CreateCommissionPayableBatchRequest request, CancellationToken cancellationToken = default);
    Task ApprovePayableAsync(ApproveCommissionPayableRequest request, CancellationToken cancellationToken = default);
    Task<int> SynchronizeExpectedReceivablesAsync(SynchronizeCommissionExpectedReceivablesRequest request, CancellationToken cancellationToken = default);
}
