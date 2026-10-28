using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Intelligence;

// Read/write access to the persisted full POLOXI Wide (Decision2) result captured per analysis run.
// The write side stores the ENTIRE WideSearchResponse so Analysis History can reopen the exact
// Decision2 Display read-only; the read side reconstructs the response verbatim. All tenant-scoped.
public interface IWideRunResultRepository
{
    // Reconstructs the saved WideSearchResponse for one analysis run, or null when no Wide result was
    // captured for that run (e.g. legacy runs recorded before full result capture existed).
    Task<WideSearchResponse?> GetByAnalysisRunAsync(
        Guid tenantId, Guid analysisRunId, CancellationToken cancellationToken = default);

    // Persists the Next Best Action result onto the captured Wide run so reopening the saved run
    // renders the full NBA panel without a fresh LLM (INTELLIGENCE_WIDE_ANSWER) call.
    Task SaveNextBestActionAsync(
        Guid tenantId, Guid analysisRunId, NextBestActionResult nextBestAction,
        CancellationToken cancellationToken = default);

    // Reads the persisted Next Best Action result for one analysis run, or null when none was captured
    // (legacy runs recorded before NBA persistence existed).
    Task<NextBestActionResult?> GetNextBestActionByAnalysisRunAsync(
        Guid tenantId, Guid analysisRunId, CancellationToken cancellationToken = default);
}
