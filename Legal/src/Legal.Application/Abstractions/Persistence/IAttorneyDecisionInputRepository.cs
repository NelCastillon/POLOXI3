using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Persistence;

// Read-only persistence for the Attorney Decision Input (Human Intelligence) surface.
// Phase 1: DB-backed reads only. Write/preview/commit/scoring paths are intentionally not exposed yet.
public interface IAttorneyDecisionInputRepository
{
    Task<MatterHumanIntelligenceDto> GetMatterHumanIntelligenceAsync(
        Guid tenantId,
        Guid matterId,
        CancellationToken cancellationToken = default);
}
