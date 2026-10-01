using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Intelligence;

// The formal DomainPack abstraction the Decision Channels and the document semantic interpreter
// resolve through. It composes the DB-backed pack (concepts) with the migration-0373 entity/event
// taxonomy, synonym terminology, and evidence-type→POLOXI TargetSignal/Relation map into a single,
// tenant-scoped, cached ResolvedDomainPack. It carries DOMAIN SEMANTICS ONLY — no numeric scores —
// so POLOXI Core remains the sole owner of candidate competition. Resolution is fail-soft: when no
// pack code is supplied or the pack is absent, an empty ResolvedDomainPack is returned.
public interface IDomainPackResolver
{
    Task<ResolvedDomainPack> ResolveAsync(Guid tenantId, string? packCode, CancellationToken cancellationToken = default);
}
