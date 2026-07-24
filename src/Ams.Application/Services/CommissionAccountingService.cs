using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Commissions;

namespace Ams.Application.Services;

public sealed class CommissionAccountingService(ICommissionAccountingRepository repository) : ICommissionAccountingService
{
    public Task<CommissionAccountingWorkspaceDto> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetWorkspaceAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<CarrierCommissionStatementLineDto>> GetStatementLinesAsync(Guid tenantId, Guid statementId, CancellationToken cancellationToken = default)
        => repository.GetStatementLinesAsync(tenantId, statementId, cancellationToken);

    public Task<CommissionImportResultDto> ImportStatementAsync(ImportCarrierCommissionStatementRequest request, CancellationToken cancellationToken = default)
        => repository.ImportStatementAsync(request, cancellationToken);

    public Task<CommissionMatchRunResultDto> RunMatchingAsync(RunCommissionMatchingRequest request, CancellationToken cancellationToken = default)
        => repository.RunMatchingAsync(request, cancellationToken);

    public Task ApproveMatchAsync(ApproveCommissionMatchRequest request, CancellationToken cancellationToken = default)
        => repository.ApproveMatchAsync(request, cancellationToken);

    public Task ResolveExceptionAsync(ResolveCommissionReconciliationExceptionRequest request, CancellationToken cancellationToken = default)
        => repository.ResolveExceptionAsync(request, cancellationToken);

    public Task<IReadOnlyList<Guid>> CreatePayablesAsync(CreateCommissionPayableBatchRequest request, CancellationToken cancellationToken = default)
        => repository.CreatePayablesAsync(request, cancellationToken);

    public Task ApprovePayableAsync(ApproveCommissionPayableRequest request, CancellationToken cancellationToken = default)
        => repository.ApprovePayableAsync(request, cancellationToken);

    public Task<int> SynchronizeExpectedReceivablesAsync(SynchronizeCommissionExpectedReceivablesRequest request, CancellationToken cancellationToken = default)
        => repository.SynchronizeExpectedReceivablesAsync(request, cancellationToken);
}
