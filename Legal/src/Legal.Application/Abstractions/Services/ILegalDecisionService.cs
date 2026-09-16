using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Services;

// Self-contained POLOXI Legal Decision Intelligence service (/legal/decision). Runs the Core
// decision-state loop and returns the structured decision artifact (§35). Separate from
// IIntelligenceWideService so the two modules evolve independently.
public interface ILegalDecisionService
{
    Task<DecisionSearchResponse> DecideAsync(DecisionSearchRequest request, CancellationToken cancellationToken = default);
    Task<DecisionSearchResponse?> GetSessionResultAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);

    // POLOXI Legal V2.1 — synchronous closed loop: apply one edge verification change and (optionally)
    // run dependency propagation → Candidate×Branch recompetition → frontier/IV recalculation →
    // ResearchNeed generation. POLOXI remains the sole scorer; the graph only emits signals.
    Task<DecisionClosedLoopResultDto> ApplyVerificationChangeAsync(
        Guid tenantId, Guid userId, Guid decisionSessionId, DecisionVerificationChangeRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionModelOptionDto>> GetModelsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionContextDto>> GetContextsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    // Matter dashboard / cockpit support.
    Task<IReadOnlyCollection<DecisionMatterDto>> GetMattersAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DecisionMatterDto?> GetMatterAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    Task<Guid> CreateMatterAsync(Guid tenantId, Guid userId, DecisionMatterCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionTimelineEventDto>> GetTimelineAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionSessionSummaryDto>> GetMatterSessionsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    Task<bool> UpdateMatterAsync(Guid tenantId, Guid userId, Guid decisionMatterId, DecisionMatterUpdateRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateMatterStatusAsync(Guid tenantId, Guid userId, Guid decisionMatterId, string statusCode, CancellationToken cancellationToken = default);
    Task<bool> DeleteMatterAsync(Guid tenantId, Guid userId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    Task<DecisionMatterFacetsDto> GetMatterFacetsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
