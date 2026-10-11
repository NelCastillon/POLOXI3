using Legal.Application.Features.Intelligence;

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
}
