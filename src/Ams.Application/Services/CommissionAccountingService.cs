using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Commissions;
using Ams.Application.Features.SearchMatching;

namespace Ams.Application.Services;

public sealed class CommissionAccountingService(ICommissionAccountingRepository repository, IEntityMatchingService matchingService) : ICommissionAccountingService
{
    public Task<CommissionAccountingWorkspaceDto> GetWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetWorkspaceAsync(tenantId, cancellationToken);

    public Task<IReadOnlyList<CarrierCommissionStatementLineDto>> GetStatementLinesAsync(Guid tenantId, Guid statementId, CancellationToken cancellationToken = default)
        => repository.GetStatementLinesAsync(tenantId, statementId, cancellationToken);

    public Task<CommissionImportResultDto> ImportStatementAsync(ImportCarrierCommissionStatementRequest request, CancellationToken cancellationToken = default)
        => repository.ImportStatementAsync(request, cancellationToken);

    public async Task<CommissionMatchRunResultDto> RunMatchingAsync(RunCommissionMatchingRequest request, CancellationToken cancellationToken = default)
    {
        var workspace = await repository.GetWorkspaceAsync(request.TenantId, cancellationToken);
        var statement = workspace.Statements.Single(item => item.CarrierCommissionStatementId == request.CarrierCommissionStatementId);
        var lines = await repository.GetStatementLinesAsync(request.TenantId, request.CarrierCommissionStatementId, cancellationToken);
        var expectedById = workspace.ExpectedReceivables.ToDictionary(item => item.CommissionExpectedReceivableId);
        var proposals = new List<ProposedCommissionMatch>();
        foreach (var line in lines.Where(item => item.MatchStatusCode == "Unmatched" && string.IsNullOrWhiteSpace(item.ValidationErrorsJson)))
        {
            var result = await matchingService.FindModuleMatchesAsync(new ModuleMatchRequest
            {
                TenantId = request.TenantId,
                ProfileCode = MatchProfileCodes.CommissionLineReconciliation,
                SourceEntityId = line.CarrierCommissionStatementLineId,
                CorrelationId = $"commission:{request.CarrierCommissionStatementId:N}:{line.CarrierCommissionStatementLineId:N}",
                RequestedByUserId = request.UserId,
                Fields = new Dictionary<string, string?>
                {
                    ["PolicyNumber"] = line.PolicyNumber,
                    ["CarrierId"] = statement.CarrierId?.ToString(),
                    ["InsuredName"] = line.InsuredName,
                    ["PremiumAmount"] = line.PremiumAmount.ToString(System.Globalization.CultureInfo.InvariantCulture)
                }
            }, cancellationToken);
            var candidate = result.Candidates.FirstOrDefault(item => expectedById.TryGetValue(item.EntityId, out var expected)
                && Math.Abs(line.NetAmount - expected.ExpectedCommissionAmount) <= request.AmountTolerance
                && (line.TransactionDate is null || expected.EffectiveDate is null || Math.Abs(line.TransactionDate.Value.DayNumber - expected.EffectiveDate.Value.DayNumber) <= request.DateToleranceDays));
            if (candidate is null) continue;
            var expected = expectedById[candidate.EntityId];
            proposals.Add(new(line.CarrierCommissionStatementLineId, candidate.EntityId, candidate.OverallScore, candidate.ConfidenceBandCode, line.NetAmount, expected.ExpectedCommissionAmount));
        }
        return await repository.SaveSharedMatchesAsync(request, proposals, cancellationToken);
    }

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
