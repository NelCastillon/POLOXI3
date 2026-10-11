using Legal.Application.Features.Intelligence.Citator;

namespace Legal.Application.Abstractions.Intelligence;

// Read side of the Citator. Resolves recorded treatments for cited authorities (tenant-scoped,
// non-deleted). Returns raw treatment rows; the service computes the validity flag.
public interface ICitatorRepository
{
    // All recorded treatments for the given cited-authority identities (case-insensitive match).
    Task<IReadOnlyList<AuthorityTreatmentDto>> GetTreatmentsAsync(
        Guid tenantId,
        IReadOnlyCollection<string> citedAuthorityIdentities,
        CancellationToken cancellationToken = default);

    // The DB-backed flag-level vocabulary (code → display/color/severity), the source of truth for
    // how the computed validity flag is presented.
    Task<IReadOnlyList<CitationFlagLevelDto>> GetFlagLevelsAsync(
        CancellationToken cancellationToken = default);

    // ── Management (write) side ──

    // The DB-backed, active treatment-code vocabulary a user picks from when recording a treatment.
    Task<IReadOnlyList<CitationTreatmentCodeDto>> GetTreatmentCodesAsync(
        CancellationToken cancellationToken = default);

    // All recorded (non-deleted) treatments for a single cited-authority identity, newest first.
    Task<IReadOnlyList<AuthorityTreatmentDto>> ListTreatmentsAsync(
        Guid tenantId,
        string citedAuthorityIdentity,
        CancellationToken cancellationToken = default);

    // Creates (when CitationTreatmentId is null) or updates one treatment row. FlagLevelCode is the
    // flag implied by the treatment code, resolved by the caller. Returns the row id.
    Task<Guid> UpsertTreatmentAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid? citationTreatmentId,
        string flagLevelCode,
        SaveCitationTreatmentRequest request,
        CancellationToken cancellationToken = default);

    // Soft-deletes a treatment row (IsDeleted = 1); never a physical delete. Returns true if a row changed.
    Task<bool> SoftDeleteTreatmentAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid citationTreatmentId,
        CancellationToken cancellationToken = default);
}
