using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.RunManagement;

namespace Legal.Application.Features.Intelligence.RunManagement;

// Assembles the Run & Assessment Management page view model for a matter. All content is DB-backed:
// matter identity comes from the decision service, runs and the current pointer from the ledger
// repository. When a matter has no runs the page renders an empty ledger rather than fabricated data.
public sealed class AnalysisRunLedgerService(
    IAnalysisRunLedgerRepository ledgerRepository,
    ILegalDecisionService decisionService) : IAnalysisRunLedgerService
{
    public async Task<RunAssessmentPageDto> GetPageAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
    {
        var matters = await decisionService.GetMattersAsync(tenantId, cancellationToken);
        var matter = matters.FirstOrDefault(m => m.DecisionMatterId == matterId);

        var title = matter?.Title ?? "Matter";
        var reference = matter?.MatterTypeCode ?? matter?.PracticeAreaCode;
        var stage = matter?.Posture ?? matter?.StatusCode;

        var runs = await ledgerRepository.GetRunsForMatterAsync(tenantId, matterId, cancellationToken);
        var current = await ledgerRepository.GetCurrentAssessmentAsync(tenantId, matterId, cancellationToken);

        return new RunAssessmentPageDto(matterId, title, reference, stage, current, runs);
    }

    public async Task<bool> DeleteRunAsync(
        Guid tenantId, Guid analysisRunId, Guid? modifiedByUserId, CancellationToken cancellationToken = default)
    {
        var affected = await ledgerRepository.SoftDeleteRunAsync(
            tenantId, analysisRunId, modifiedByUserId, cancellationToken);
        return affected > 0;
    }
}
