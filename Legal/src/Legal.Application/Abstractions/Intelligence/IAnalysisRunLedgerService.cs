using Legal.Application.Features.Intelligence.RunManagement;

namespace Legal.Application.Abstractions.Intelligence;

// Assembles the Run & Assessment Management page view model for a matter from DB-backed records.
public interface IAnalysisRunLedgerService
{
    Task<RunAssessmentPageDto> GetPageAsync(
        Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    // Soft-deletes a single run and its dependent history for the matter. Tenant-scoped.
    // Returns true when a run was deleted, false when no matching run exists for the tenant.
    Task<bool> DeleteRunAsync(
        Guid tenantId, Guid analysisRunId, Guid? modifiedByUserId, CancellationToken cancellationToken = default);
}
