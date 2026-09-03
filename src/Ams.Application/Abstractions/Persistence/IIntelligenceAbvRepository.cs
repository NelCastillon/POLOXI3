using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Persistence;

// Persistence contract for the POLOXI ABV Action Layer (POLOXI.AbvDomainPack + child taxonomy/
// policy/mapping/catalog tables, and the POLOXI.AbvResolution observability table). Domain Packs
// are database-backed configuration — never hardcoded. Tenant rows override global (TenantId NULL).
public interface IIntelligenceAbvRepository
{
    // Loads the effective Domain Pack (intents, urgency policies, owner mappings, actions). When
    // packCode is null/empty the default pack (IsDefault=1) is returned.
    Task<AbvDomainPack> GetDomainPackAsync(Guid tenantId,string? packCode,CancellationToken cancellationToken=default);
    // Persists the ABV resolution outcome for audit/observability; returns the stored row id.
    Task<Guid> RecordResolutionAsync(Guid tenantId,Guid userId,Guid? ambiguityRunId,Guid abvDomainPackId,string? proposedIntentCode,AbvResolutionOutcome outcome,long durationMilliseconds,CancellationToken cancellationToken=default);
}
