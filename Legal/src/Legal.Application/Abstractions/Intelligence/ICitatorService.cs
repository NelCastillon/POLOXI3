using Legal.Application.Features.Intelligence.Citator;

namespace Legal.Application.Abstractions.Intelligence;

// Citator application service. Computes a good-law validity flag for each requested authority by
// taking the worst flag among its recorded treatments. Authorities with no citator data resolve to
// the "Unknown" flag. Advisory only — never blocks a decision.
public interface ICitatorService
{
    // Returns one validity result per requested authority identity (always one per input, including
    // Unknown for authorities without treatment data).
    Task<IReadOnlyList<AuthorityValidityDto>> GetValidityAsync(
        Guid tenantId,
        IReadOnlyCollection<string> authorityIdentities,
        CancellationToken cancellationToken = default);

    // ── Management ──

    // The DB-backed treatment-code vocabulary a user picks from when recording a treatment.
    Task<IReadOnlyList<CitationTreatmentCodeDto>> GetTreatmentCodesAsync(
        CancellationToken cancellationToken = default);

    // All recorded treatments for a single cited authority (newest first), for the management UI.
    Task<IReadOnlyList<AuthorityTreatmentDto>> ListTreatmentsAsync(
        Guid tenantId,
        string citedAuthorityIdentity,
        CancellationToken cancellationToken = default);

    // Records or updates one treatment. Validates the treatment code against the DB vocabulary and
    // derives the implied validity flag from it. Returns the treatment row id.
    Task<Guid> SaveTreatmentAsync(
        Guid tenantId,
        Guid actorUserId,
        SaveCitationTreatmentRequest request,
        CancellationToken cancellationToken = default);

    // Soft-deletes a treatment row. Returns true if a row was changed.
    Task<bool> DeleteTreatmentAsync(
        Guid tenantId,
        Guid actorUserId,
        Guid citationTreatmentId,
        CancellationToken cancellationToken = default);
}
