using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Intelligence;

// Write side of the Run & Assessment Management execution ledger. Records a completed decision
// execution as a persisted AnalysisRun (with real, response-derived steps) so the ledger reflects
// actual history. Best-effort by contract: a failure here must never break the decision result.
public interface IAnalysisRunLedgerWriter
{
    // Records one decision execution as a run for the matter. Returns the new run id, or null when the
    // decision is not matter-scoped (ad-hoc) and therefore not recorded in the matter ledger.
    Task<Guid?> RecordDecisionRunAsync(
        DecisionSearchRequest request,
        DecisionSearchResponse response,
        CancellationToken cancellationToken = default);

    // Records one POLOXI Wide (decision cockpit) execution as a run for the matter. Returns the new run
    // id, or null when the execution is not matter-scoped (ad-hoc) and therefore not recorded in the
    // matter ledger. Best-effort by contract: a failure here must never break the search result.
    Task<Guid?> RecordWideRunAsync(
        WideSearchRequest request,
        WideSearchResponse response,
        CancellationToken cancellationToken = default);
}
