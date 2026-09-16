using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Persistence;

// Persistence for the self-contained POLOXI Legal Decision module. All configuration and decision
// state is database-backed (POLOXI.Legal_Decision*); no operational data is hardcoded in code.
public interface ILegalDecisionRepository
{
    Task<DecisionCoreSettings> GetCoreSettingsAsync(CancellationToken cancellationToken = default);
    Task<DecisionV2Settings> GetV2SettingsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionContextDto>> GetContextsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionModelRouteDto>> GetModelRoutesAsync(CancellationToken cancellationToken = default);
    Task<DecisionPromptDefinition?> GetPromptAsync(string promptCode, CancellationToken cancellationToken = default);
    Task PersistSessionAsync(DecisionSessionPersistence session, CancellationToken cancellationToken = default);
    Task<DecisionSessionPersistence?> GetSessionAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);

    // Matter aggregate + cockpit support.
    Task<IReadOnlyCollection<DecisionMatterDto>> GetMattersAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DecisionMatterDto?> GetMatterAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    Task<Guid> CreateMatterAsync(Guid tenantId, Guid userId, DecisionMatterCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionTimelineEventDto>> GetSessionTimelineAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionSessionSummaryDto>> GetMatterSessionsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    Task<bool> UpdateMatterAsync(Guid tenantId, Guid userId, Guid decisionMatterId, DecisionMatterUpdateRequest request, CancellationToken cancellationToken = default);
    Task<bool> UpdateMatterStatusAsync(Guid tenantId, Guid userId, Guid decisionMatterId, string statusCode, CancellationToken cancellationToken = default);
    Task<bool> DeleteMatterAsync(Guid tenantId, Guid userId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    Task<DecisionMatterFacetsDto> GetMatterFacetsAsync(Guid tenantId, CancellationToken cancellationToken = default);

    // POLOXI Legal V2 — dependency-aware decision graph persistence/retrieval.
    Task PersistGraphAsync(DecisionGraphPersistence graph, CancellationToken cancellationToken = default);
    Task<DecisionGraphPersistence?> GetGraphAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
    Task UpdateEdgeVerificationAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionGraphEdgePersistence> edges, CancellationToken cancellationToken = default);

    // POLOXI Legal V2.1 — closed-loop persistence (verification events, recompetitions, research needs, frontier snapshots).
    Task<DecisionV21Settings> GetV21SettingsAsync(CancellationToken cancellationToken = default);
    Task<DecisionDependencyEventPersistence?> GetDependencyEventAsync(Guid tenantId, Guid decisionSessionId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task PersistDependencyEventAsync(DecisionDependencyEventPersistence dependencyEvent, CancellationToken cancellationToken = default);
    Task PersistRecompetitionAsync(DecisionRecompetitionPersistence recompetition, CancellationToken cancellationToken = default);
    Task PersistResearchNeedAsync(DecisionResearchNeedPersistence researchNeed, CancellationToken cancellationToken = default);
    Task PersistFrontierSnapshotAsync(DecisionFrontierSnapshotPersistence snapshot, CancellationToken cancellationToken = default);
    Task<int> CountBranchReopensAsync(Guid tenantId, Guid decisionSessionId, Guid decisionBranchId, CancellationToken cancellationToken = default);
    Task<int> CountResearchNeedsAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
    Task<DecisionRecompetitionPersistence?> GetLatestRecompetitionAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
    Task<DecisionResearchNeedPersistence?> GetLatestOpenResearchNeedAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
    Task UpdateSessionOutcomeAsync(Guid tenantId, Guid userId, Guid decisionSessionId, string statusCode, decimal entropy, decimal margin, Guid? winnerCandidateId, CancellationToken cancellationToken = default);
    Task ReplaceBranchesAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionBranchPersistence> branches, CancellationToken cancellationToken = default);
    Task ReplaceCandidatesAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionCandidatePersistence> candidates, CancellationToken cancellationToken = default);
}
