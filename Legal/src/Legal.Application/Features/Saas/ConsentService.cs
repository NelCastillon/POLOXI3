using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;

namespace Legal.Application.Features.Saas;

// ─────────────────────────────────────────────────────────────────────────────
// Judz.ai Early Access SaaS — Legal clickwrap consent service.
//
// Serves the DB-backed active agreements (Terms of Service + Privacy Policy) to
// the signup surface and records full legal evidence when a user accepts them.
// Unlike login-history writes, consent evidence is authoritative: if a required
// agreement cannot be persisted the caller is told (the signup flow decides how
// to react). One consent row is written per active agreement so each document
// version + content hash the user saw is independently provable.
// ─────────────────────────────────────────────────────────────────────────────
public sealed class ConsentService(ISaasRepository repository) : IConsentService
{
    public Task<IReadOnlyList<LegalAgreementDto>> GetActiveAgreementsAsync(CancellationToken ct = default)
        => repository.GetActiveAgreementsAsync(ct);

    public async Task RecordConsentForActiveAgreementsAsync(
        Guid? userId,
        Guid? tenantId,
        string? email,
        string acceptanceMethod,
        string? ipAddress,
        string? userAgent,
        string? correlationId,
        CancellationToken ct = default)
    {
        var agreements = await repository.GetActiveAgreementsAsync(ct);
        foreach (var agreement in agreements)
        {
            if (!agreement.RequiresConsent)
                continue;

            await repository.RecordConsentAsync(new RecordConsentRequest(
                userId,
                tenantId,
                email,
                agreement.AgreementId,
                agreement.AgreementType,
                agreement.Version,
                agreement.ContentHash,
                acceptanceMethod,
                ipAddress,
                userAgent,
                correlationId), ct);
        }
    }

    public Task<IReadOnlyList<ConsentRecordDto>> GetConsentHistoryAsync(Guid userId, Guid? tenantId, CancellationToken ct = default)
        => repository.ListConsentRecordsForUserAsync(userId, tenantId, ct);
}
