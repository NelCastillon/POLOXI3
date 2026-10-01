using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Decision;

// Composes the DB-backed Domain Pack (concepts) with the migration-0373 entity/event taxonomy,
// synonym terminology, and evidence-type→signal map into a single ResolvedDomainPack. Fail-soft:
// a null/blank pack code or an absent pack yields an empty ResolvedDomainPack so callers (channels
// and the semantic interpreter) degrade to their generic behavior without failing. Advisory config
// only — POLOXI Core reasoning is unchanged.
public sealed class DomainPackResolver(ILegalDecisionRepository decisionRepository) : IDomainPackResolver
{
    private static readonly ResolvedDomainPack Empty = new(
        string.Empty, string.Empty, [], [], [], [], []);

    public async Task<ResolvedDomainPack> ResolveAsync(Guid tenantId, string? packCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(packCode))
            return Empty;

        var code = packCode.Trim();
        var pack = await decisionRepository.GetDomainPackAsync(tenantId, code, cancellationToken);
        if (pack is null)
            return Empty;

        var entityTypes = await decisionRepository.GetDomainEntityTypesAsync(tenantId, code, cancellationToken);
        var eventTypes = await decisionRepository.GetDomainEventTypesAsync(tenantId, code, cancellationToken);
        var terms = await decisionRepository.GetDomainTermsAsync(tenantId, code, cancellationToken);
        var signalMap = await decisionRepository.GetDomainSignalMapAsync(tenantId, code, cancellationToken);

        return new ResolvedDomainPack(
            pack.PackCode,
            pack.PracticeAreaCode,
            pack.Concepts,
            entityTypes,
            eventTypes,
            terms,
            signalMap);
    }
}
