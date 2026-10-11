using Legal.Application.Features.Intelligence.RunManagement;

namespace Legal.Application.Abstractions.Intelligence;

// Read access to the Run & Assessment Management execution ledger. All queries are tenant-scoped.
public interface IAnalysisRunLedgerRepository
{
    // The runs for a matter, newest first, each with ordered steps and attempts.
    Task<IReadOnlyList<AnalysisRunDto>> GetRunsForMatterAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    // The guarded current-assessment pointer for the matter, or null when none is published.
    Task<CurrentAssessmentDto?> GetCurrentAssessmentAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    // Soft-deletes a single run and its dependent history (steps, attempts, input snapshots,
    // current-assessment pointer, and saved Wide result) for the matter. Tenant-scoped, write-once.
    // Returns the number of run rows affected (0 when the run does not exist for the tenant).
    Task<int> SoftDeleteRunAsync(
        Guid tenantId, Guid analysisRunId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
