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

    // POLOXI Bounded Research Loop (§13/§14/§18) — the AUTONOMOUS closed loop. Repeatedly selects the
    // highest-Information-Value frontier item, retrieves + verifies external evidence for it, promotes
    // only VERIFIED material, and drives the existing single-iteration closed loop (propagation →
    // Candidate×Branch recompetition → frontier/IV/readiness recalculation) until an explicit STOP
    // condition (DecisionReady, budget exhausted, frontier below threshold, or no material change).
    // Bounded convergence engine, never "research until ready". Default OFF until validated end to end.
    Task<DecisionResearchLoopResultDto> RunResearchLoopAsync(
        Guid tenantId, Guid userId, Guid decisionSessionId, CancellationToken cancellationToken = default);
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

    // Domain Pack (practice-area domain semantics) retrieval.
    Task<DecisionDomainPackDto?> GetDomainPackAsync(Guid tenantId, string packCode, CancellationToken cancellationToken = default);

    // ── Personal Injury (Domain Pack: PERSONAL_INJURY) support ──
    Task<PersonalInjuryOptionsDto> GetPersonalInjuryOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<PersonalInjuryProfileDto?> GetPersonalInjuryProfileAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default);
    Task SavePersonalInjuryProfileAsync(Guid tenantId, Guid userId, Guid decisionMatterId, PersonalInjuryProfileSaveRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PersonalInjuryDecisionTypeDto>> GetPersonalInjuryDecisionTypesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PersonalInjuryStageDecisionDto>> GetPersonalInjuryStageDecisionMapAsync(Guid tenantId, CancellationToken cancellationToken = default);

    // Generate-New-Matter draft / provenance (extraction wired later).
    Task<Guid> CreatePersonalInjuryDraftAsync(Guid tenantId, Guid userId, PersonalInjuryMatterDraftCreateRequest request, CancellationToken cancellationToken = default);
    Task<PersonalInjuryMatterDraftDto?> GetPersonalInjuryDraftAsync(Guid tenantId, Guid decisionPIMatterDraftId, CancellationToken cancellationToken = default);
    Task<bool> MarkPersonalInjuryDraftConfirmedAsync(Guid tenantId, Guid userId, Guid decisionPIMatterDraftId, Guid confirmedMatterId, CancellationToken cancellationToken = default);

    // PI Decision Intelligence: transform PI context into the existing decision pipeline (Core unchanged).
    Task<DecisionSearchResponse> DecidePersonalInjuryAsync(Guid tenantId, Guid userId, PersonalInjuryDecisionContext context, IReadOnlyCollection<string>? grantedPermissions, CancellationToken cancellationToken = default);
}
