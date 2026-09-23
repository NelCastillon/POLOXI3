using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Abstractions.Intelligence;

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
    Task<IReadOnlyCollection<DecisionPromptConfigurationDto>> GetPromptConfigurationsAsync(CancellationToken cancellationToken = default);
    Task SavePromptConfigurationAsync(Guid actorUserId, SaveDecisionPromptConfigurationRequest request, CancellationToken cancellationToken = default);
    Task PersistSessionAsync(DecisionSessionPersistence session, CancellationToken cancellationToken = default);
    Task PersistClarificationAsync(DecisionClarificationPersistence clarification, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionClarificationPersistence>> GetClarificationLineageAsync(Guid tenantId, Guid parentDecisionSessionId, CancellationToken cancellationToken = default);
    // Appends additional timeline events to an already-persisted session (e.g. the B3 solver's
    // post-persistence recompetition stages). Sequence numbers continue from the session's events.
    Task AppendSessionEventsAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionEventPersistence> events, CancellationToken cancellationToken = default);
    Task<DecisionSessionPersistence?> GetSessionAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
    Task PersistResearchEvidenceAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionEvidencePersistence> evidence, CancellationToken cancellationToken = default);
    Task UpdateResearchEvidenceAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionEvidencePersistence> evidence, CancellationToken cancellationToken = default);
    Task PersistEvidenceVerificationsAsync(IReadOnlyCollection<DecisionEvidenceVerificationPersistence> verifications, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DecisionEvidenceVerificationPersistence>> GetEvidenceVerificationsAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
    Task PersistOutputClaimProvenanceAsync(IReadOnlyCollection<DecisionOutputClaimProvenancePersistence> provenance, CancellationToken cancellationToken = default);

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

    // Domain Pack (practice-area domain semantics) retrieval.
    Task<DecisionDomainPackDto?> GetDomainPackAsync(Guid tenantId, string packCode, CancellationToken cancellationToken = default);

    // ── Personal Injury (Domain Pack: PERSONAL_INJURY) profile + child aggregates + options ──
    Task<PersonalInjuryOptionsDto> GetPersonalInjuryOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PersonalInjuryProfileDto?> GetPersonalInjuryProfileAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    Task SavePersonalInjuryProfileAsync(Guid tenantId, Guid userId, Guid decisionMatterId, PersonalInjuryProfileSaveRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PersonalInjuryDecisionTypeDto>> GetPersonalInjuryDecisionTypesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PersonalInjuryStageDecisionDto>> GetPersonalInjuryStageDecisionMapAsync(Guid tenantId, CancellationToken cancellationToken = default);

    // Generate-New-Matter draft / provenance persistence (extraction wired later).
    Task<Guid> CreatePersonalInjuryDraftAsync(Guid tenantId, Guid userId, PersonalInjuryMatterDraftCreateRequest request, CancellationToken cancellationToken = default);
    Task<PersonalInjuryMatterDraftDto?> GetPersonalInjuryDraftAsync(Guid tenantId, Guid decisionPIMatterDraftId, CancellationToken cancellationToken = default);
    Task<bool> MarkPersonalInjuryDraftConfirmedAsync(Guid tenantId, Guid userId, Guid decisionPIMatterDraftId, Guid confirmedMatterId, CancellationToken cancellationToken = default);

    // POLOXI Legal V2 — dependency-aware decision graph persistence/retrieval.
    Task PersistGraphAsync(DecisionGraphPersistence graph, CancellationToken cancellationToken = default);
    Task<DecisionGraphPersistence?> GetGraphAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
    Task UpdateEdgeVerificationAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionGraphEdgePersistence> edges, CancellationToken cancellationToken = default);

    // POLOXI Legal V2.1 — closed-loop persistence (verification events, recompetitions, research needs, frontier snapshots).
    Task<DecisionV21Settings> GetV21SettingsAsync(CancellationToken cancellationToken = default);
    Task<DecisionResearchLoopSettings> GetResearchLoopSettingsAsync(CancellationToken cancellationToken = default);
    Task<DecisionDependencyEventPersistence?> GetDependencyEventAsync(Guid tenantId, Guid decisionSessionId, string idempotencyKey, CancellationToken cancellationToken = default);
    Task PersistDependencyEventAsync(DecisionDependencyEventPersistence dependencyEvent, CancellationToken cancellationToken = default);
    Task PersistRecompetitionAsync(DecisionRecompetitionPersistence recompetition, CancellationToken cancellationToken = default);
    Task PersistResearchNeedAsync(DecisionResearchNeedPersistence researchNeed, CancellationToken cancellationToken = default);
    Task PersistLegalResearchExecutionAsync(LegalSearchPlan plan, LegalAuthorityRetrievalResult result, CancellationToken cancellationToken = default);
    Task PersistVerifiedLegalPropositionsAsync(Guid tenantId,Guid decisionSessionId,IReadOnlyCollection<VerifiedLegalProposition> propositions,CancellationToken cancellationToken=default);
    Task PersistLegalDecisionImpactAsync(Guid tenantId, Guid decisionSessionId, LegalDecisionImpactResult impact, CancellationToken cancellationToken = default);
    Task PersistEvidenceAttachmentsAsync(IReadOnlyCollection<DecisionEvidenceAttachmentPersistence> attachments, CancellationToken cancellationToken = default);
    Task UpdateEvidenceAttachmentsAsync(IReadOnlyCollection<DecisionEvidenceAttachmentPersistence> attachments, CancellationToken cancellationToken = default);
    Task PersistFrontierSnapshotAsync(DecisionFrontierSnapshotPersistence snapshot, CancellationToken cancellationToken = default);
    Task<int> CountBranchReopensAsync(Guid tenantId, Guid decisionSessionId, Guid decisionBranchId, CancellationToken cancellationToken = default);
    Task<int> CountResearchNeedsAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
    Task<DecisionRecompetitionPersistence?> GetLatestRecompetitionAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
    Task<DecisionResearchNeedPersistence?> GetLatestOpenResearchNeedAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default);
    Task UpdateSessionOutcomeAsync(Guid tenantId, Guid userId, Guid decisionSessionId, string statusCode, decimal entropy, decimal margin, Guid? winnerCandidateId, CancellationToken cancellationToken = default);
    Task UpdateSessionAnswerAsync(Guid tenantId, Guid userId, Guid decisionSessionId, string? finalAnswer, CancellationToken cancellationToken = default);
    Task ReplaceBranchesAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionBranchPersistence> branches, CancellationToken cancellationToken = default);
    Task ReplaceCandidatesAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionCandidatePersistence> candidates, CancellationToken cancellationToken = default);
}
