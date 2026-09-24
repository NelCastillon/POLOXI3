using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Tests.Intelligence;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// In-memory ILegalDecisionRepository used by the research-round rollback tests. It serves a single
// seeded session + graph and COUNTS every authoritative mutation so a test can assert that a faulted
// round performed no partial writes. Read paths return the current in-memory state; mutating paths
// both record a call and update the in-memory session so a successful round is observable. Methods the
// research-loop path never touches return benign defaults (they are not exercised by these tests).
// ────────────────────────────────────────────────────────────────────────────────────────────────
internal sealed class RecordingDecisionRepository : ILegalDecisionRepository
{
    public RecordingDecisionRepository(
        DecisionSessionPersistence session,
        DecisionGraphPersistence graph,
        DecisionPromptDefinition? prompt = null,
        IReadOnlyCollection<DecisionModelRouteDto>? routes = null)
    {
        Session = session;
        _graph = graph;
        Prompt = prompt;
        Routes = routes ?? [];
    }

    public DecisionSessionPersistence Session { get; set; }
    private DecisionGraphPersistence _graph;
    public DecisionPromptDefinition? Prompt { get; set; }
    public IReadOnlyCollection<DecisionModelRouteDto> Routes { get; set; }

    // Mutation counters (the transaction-protection assertions read these).
    public int UpdateEdgeVerificationCount { get; private set; }
    public int PersistDependencyEventCount { get; private set; }
    public int PersistRecompetitionCount { get; private set; }
    public int PersistResearchNeedCount { get; private set; }
    public int PersistFrontierSnapshotCount { get; private set; }
    public int ReplaceBranchesCount { get; private set; }
    public int ReplaceCandidatesCount { get; private set; }
    public int UpdateSessionOutcomeCount { get; private set; }
    public int PersistResearchEvidenceCount { get; private set; }
    public int UpdateResearchEvidenceCount { get; private set; }
    public int PersistEvidenceAttachmentCount { get; private set; }
    public int UpdateEvidenceAttachmentCount { get; private set; }
    public List<DecisionEvidencePersistence> ResearchEvidence { get; } = [];
    public List<DecisionEvidenceAttachmentPersistence> EvidenceAttachments { get; } = [];
    public List<DecisionEvidenceVerificationPersistence> EvidenceVerifications { get; } = [];
    public List<DecisionOutputClaimProvenancePersistence> OutputClaimProvenance { get; } = [];
    public List<DecisionDependencyEventPersistence> DependencyEvents { get; } = [];
    public List<DecisionRecompetitionPersistence> Recompetitions { get; } = [];
    public List<DecisionFrontierSnapshotPersistence> FrontierSnapshots { get; } = [];
    public List<DecisionResearchNeedPersistence> ResearchNeeds { get; } = [];
    public List<LegalAuthorityRetrievalResult> LegalResearchExecutions { get; } = [];
    public List<LegalDecisionImpactResult> LegalDecisionImpacts { get; } = [];
    public List<VerifiedLegalProposition> VerifiedLegalPropositions { get; } = [];
    public List<DecisionClarificationPersistence> Clarifications { get; } = [];

    // ── Settings (loop path reads these) ──────────────────────────────────────────────────────────
    public Task<DecisionCoreSettings> GetCoreSettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new DecisionCoreSettings(
            0.20, 0.25, 0.25, 0.15, 0.10, 0.05,
            0.35, 0.25, 0.15, 0.10, 0.30, 0.40,
            4, 24, 8));

    public Task<DecisionV2Settings> GetV2SettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new DecisionV2Settings(
            UseDependencyGraphDefault: true, MaterialityThreshold: 0.30, ReadinessMinAuthorityVerified: 0.50,
            ReadinessLosingSideMargin: 0.10, PropagationMaxDepth: 8, ReadinessMaxHighImpactFrontier: 3));

    public Task<IReadOnlyCollection<DecisionExecutionModeDto>> GetExecutionModesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DecisionExecutionModeDto>>(new[]
        {
            new DecisionExecutionModeDto("DEV", "Dev Logic", null, "gpt-4.1-mini", AllowReplay: true, IsProductionAllowed: false, SortOrder: 1, IsActive: true),
            new DecisionExecutionModeDto("PROD", "Prod Logic", null, null, AllowReplay: false, IsProductionAllowed: true, SortOrder: 2, IsActive: true),
        });

    public Task SaveExecutionModeAsync(SaveDecisionExecutionModeRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<DecisionV21Settings> GetV21SettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new DecisionV21Settings(
            UseDependencyPropagation: true, UseGraphDrivenRecompetition: true, UseGraphFrontierSignals: true,
            LoopMaxReopensPerBranch: 3, LoopMaxResearchActions: 5, LoopNoInformationGainEpsilon: 0.001,
            BenchmarkEnabled: false));

    public Task<DecisionResearchLoopSettings> GetResearchLoopSettingsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(new DecisionResearchLoopSettings(
            Enabled: true, MaxRounds: 1, MaxRetrievals: 5, MinFrontierInformationValue: 0.10,
            NoStateChangeEpsilon: 0.0001, UseSeedRetriever: false));

    // ── Session / graph reads ─────────────────────────────────────────────────────────────────────
    public Task<DecisionSessionPersistence?> GetSessionAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
        => Task.FromResult<DecisionSessionPersistence?>(Session);

    public Task<DecisionGraphPersistence?> GetGraphAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
        => Task.FromResult<DecisionGraphPersistence?>(_graph);

    public Task PersistResearchEvidenceAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionEvidencePersistence> evidence, CancellationToken cancellationToken = default)
    {
        PersistResearchEvidenceCount += evidence.Count;
        ResearchEvidence.AddRange(evidence);
        return Task.CompletedTask;
    }

    public Task PersistClarificationAsync(DecisionClarificationPersistence clarification, CancellationToken cancellationToken = default)
    {
        Clarifications.Add(clarification);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<DecisionClarificationPersistence>> GetClarificationLineageAsync(
        Guid tenantId, Guid parentDecisionSessionId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DecisionClarificationPersistence>>(
            Clarifications.Where(item => item.TenantId == tenantId).OrderBy(item => item.CreatedDateUtc).ToArray());

    public Task PersistVerifiedLegalPropositionsAsync(Guid tenantId,Guid decisionSessionId,IReadOnlyCollection<VerifiedLegalProposition> propositions,CancellationToken cancellationToken=default)
    {
        VerifiedLegalPropositions.AddRange(propositions);
        return Task.CompletedTask;
    }

    public Task PersistEvidenceVerificationsAsync(IReadOnlyCollection<DecisionEvidenceVerificationPersistence> verifications, CancellationToken cancellationToken = default)
    {
        foreach (var verification in verifications)
        {
            EvidenceVerifications.RemoveAll(v => v.DecisionEvidenceId == verification.DecisionEvidenceId);
            EvidenceVerifications.Add(verification);
        }
        return Task.CompletedTask;
    }

    public Task<IReadOnlyCollection<DecisionEvidenceVerificationPersistence>> GetEvidenceVerificationsAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DecisionEvidenceVerificationPersistence>>(EvidenceVerifications.ToArray());

    public Task PersistOutputClaimProvenanceAsync(IReadOnlyCollection<DecisionOutputClaimProvenancePersistence> provenance, CancellationToken cancellationToken = default)
    {
        OutputClaimProvenance.AddRange(provenance);
        return Task.CompletedTask;
    }

    public Task UpdateResearchEvidenceAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionEvidencePersistence> evidence, CancellationToken cancellationToken = default)
    {
        UpdateResearchEvidenceCount += evidence.Count;
        foreach (var item in evidence)
        {
            var index = ResearchEvidence.FindIndex(x => x.DecisionEvidenceId == item.DecisionEvidenceId);
            if (index >= 0)
                ResearchEvidence[index] = item;
        }
        return Task.CompletedTask;
    }

    public Task<DecisionDependencyEventPersistence?> GetDependencyEventAsync(Guid tenantId, Guid decisionSessionId, string idempotencyKey, CancellationToken cancellationToken = default)
        => Task.FromResult<DecisionDependencyEventPersistence?>(null);

    public Task<int> CountBranchReopensAsync(Guid tenantId, Guid decisionSessionId, Guid decisionBranchId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<int> CountResearchNeedsAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
        => Task.FromResult(0);

    public Task<DecisionRecompetitionPersistence?> GetLatestRecompetitionAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
        => Task.FromResult<DecisionRecompetitionPersistence?>(null);

    public Task<DecisionResearchNeedPersistence?> GetLatestOpenResearchNeedAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
        => Task.FromResult<DecisionResearchNeedPersistence?>(null);

    // ── Authoritative mutations (counted; update in-memory state so a successful round is observable) ─
    public Task UpdateEdgeVerificationAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionGraphEdgePersistence> edges, CancellationToken cancellationToken = default)
    {
        UpdateEdgeVerificationCount++;
        var updated = _graph.Edges.ToList();
        foreach (var e in edges)
        {
            var i = updated.FindIndex(x => x.EdgeId == e.EdgeId);
            if (i >= 0) updated[i] = e;
        }
        _graph = _graph with { Edges = updated };
        return Task.CompletedTask;
    }

    public Task PersistEvidenceAttachmentsAsync(IReadOnlyCollection<DecisionEvidenceAttachmentPersistence> attachments, CancellationToken cancellationToken = default)
    {
        PersistEvidenceAttachmentCount += attachments.Count;
        EvidenceAttachments.AddRange(attachments);
        return Task.CompletedTask;
    }

    public Task UpdateEvidenceAttachmentsAsync(IReadOnlyCollection<DecisionEvidenceAttachmentPersistence> attachments, CancellationToken cancellationToken = default)
    {
        UpdateEvidenceAttachmentCount += attachments.Count;
        foreach (var attachment in attachments)
        {
            var index = EvidenceAttachments.FindIndex(x => x.DecisionEvidenceAttachmentId == attachment.DecisionEvidenceAttachmentId);
            if (index >= 0)
                EvidenceAttachments[index] = attachment;
        }
        return Task.CompletedTask;
    }

    public Task PersistDependencyEventAsync(DecisionDependencyEventPersistence dependencyEvent, CancellationToken cancellationToken = default)
    {
        PersistDependencyEventCount++;
        DependencyEvents.Add(dependencyEvent);
        return Task.CompletedTask;
    }

    public Task PersistRecompetitionAsync(DecisionRecompetitionPersistence recompetition, CancellationToken cancellationToken = default)
    {
        PersistRecompetitionCount++;
        Recompetitions.Add(recompetition);
        return Task.CompletedTask;
    }

    public Task PersistResearchNeedAsync(DecisionResearchNeedPersistence researchNeed, CancellationToken cancellationToken = default)
    {
        PersistResearchNeedCount++;
        ResearchNeeds.Add(researchNeed);
        return Task.CompletedTask;
    }

    public Task PersistFrontierSnapshotAsync(DecisionFrontierSnapshotPersistence snapshot, CancellationToken cancellationToken = default)
    {
        PersistFrontierSnapshotCount++;
        FrontierSnapshots.Add(snapshot);
        return Task.CompletedTask;
    }

    public Task ReplaceBranchesAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionBranchPersistence> branches, CancellationToken cancellationToken = default)
    {
        ReplaceBranchesCount++;
        Session = Session with { Branches = branches.ToArray() };
        return Task.CompletedTask;
    }

    public Task ReplaceCandidatesAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionCandidatePersistence> candidates, CancellationToken cancellationToken = default)
    {
        ReplaceCandidatesCount++;
        Session = Session with { Candidates = candidates.ToArray() };
        return Task.CompletedTask;
    }

    public Task UpdateSessionOutcomeAsync(Guid tenantId, Guid userId, Guid decisionSessionId, string statusCode, decimal entropy, decimal margin, Guid? winnerCandidateId, CancellationToken cancellationToken = default)
    {
        UpdateSessionOutcomeCount++;
        Session = Session with
        {
            StatusCode = statusCode,
            CandidateEntropy = entropy,
            DecisionMargin = margin,
            WinnerCandidateId = winnerCandidateId,
        };
        return Task.CompletedTask;
    }

    public Task UpdateSessionAnswerAsync(Guid tenantId, Guid userId, Guid decisionSessionId, string? finalAnswer, CancellationToken cancellationToken = default)
    {
        Session = Session with { FinalAnswer = finalAnswer };
        return Task.CompletedTask;
    }

    // ── Not exercised by the rollback tests ───────────────────────────────────────────────────────
    public Task<IReadOnlyCollection<DecisionContextDto>> GetContextsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DecisionContextDto>>([]);

    public Task<IReadOnlyCollection<DecisionModelRouteDto>> GetModelRoutesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Routes);

    public Task SaveModelRouteAsync(Guid actorUserId, SaveDecisionModelRouteRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<DecisionPromptDefinition?> GetPromptAsync(string promptCode, CancellationToken cancellationToken = default)
        => Task.FromResult(Prompt is not null && string.Equals(Prompt.PromptCode, promptCode, StringComparison.Ordinal)
            ? Prompt
            : null);

    public Task<IReadOnlyCollection<DecisionPromptConfigurationDto>> GetPromptConfigurationsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DecisionPromptConfigurationDto>>([]);

    public Task SavePromptConfigurationAsync(Guid actorUserId, SaveDecisionPromptConfigurationRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyCollection<DecisionSettingDto>> GetSettingsAsync(string? keyPrefix = null, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DecisionSettingDto>>([]);

    public Task SaveSettingAsync(Guid actorUserId, SaveDecisionSettingRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PersistSessionAsync(DecisionSessionPersistence session, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task PersistLegalResearchExecutionAsync(LegalSearchPlan plan, LegalAuthorityRetrievalResult result, CancellationToken cancellationToken = default)
    {
        LegalResearchExecutions.Add(result);
        return Task.CompletedTask;
    }

    public Task PersistLegalDecisionImpactAsync(Guid tenantId, Guid decisionSessionId, LegalDecisionImpactResult impact, CancellationToken cancellationToken = default)
    {
        LegalDecisionImpacts.Add(impact);
        return Task.CompletedTask;
    }

    public Task AppendSessionEventsAsync(Guid tenantId, Guid userId, Guid decisionSessionId, IReadOnlyCollection<DecisionEventPersistence> events, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyCollection<DecisionMatterDto>> GetMattersAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DecisionMatterDto>>([]);

    public Task<IReadOnlyCollection<DecisionMatterDto>> GetMattersAsync(Guid tenantId, bool includeAllTenants, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DecisionMatterDto>>([]);

    public Task<DecisionMatterDto?> GetMatterAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => Task.FromResult<DecisionMatterDto?>(null);

    public Task<Guid> CreateMatterAsync(Guid tenantId, Guid userId, DecisionMatterCreateRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(Guid.NewGuid());

    public Task<IReadOnlyCollection<DecisionTimelineEventDto>> GetSessionTimelineAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DecisionTimelineEventDto>>([]);

    public Task<IReadOnlyCollection<DecisionSessionSummaryDto>> GetMatterSessionsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DecisionSessionSummaryDto>>([]);

    public Task<bool> UpdateMatterAsync(Guid tenantId, Guid userId, Guid decisionMatterId, DecisionMatterUpdateRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> UpdateMatterStatusAsync(Guid tenantId, Guid userId, Guid decisionMatterId, string statusCode, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<bool> DeleteMatterAsync(Guid tenantId, Guid userId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    public Task<DecisionMatterFacetsDto> GetMatterFacetsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("Matter facets are not used on the research-loop path.");

    public Task<DecisionDomainPackDto?> GetDomainPackAsync(Guid tenantId, string packCode, CancellationToken cancellationToken = default)
        => Task.FromResult<DecisionDomainPackDto?>(null);

    public Task<IReadOnlyCollection<DecisionDomainPackDto>> GetDomainPacksAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<DecisionDomainPackDto>>([]);

    public Task PersistGraphAsync(DecisionGraphPersistence graph, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    // ── Personal Injury (Domain Pack) — not exercised by the research-loop tests ──
    public Task<PersonalInjuryOptionsDto> GetPersonalInjuryOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult(new PersonalInjuryOptionsDto());

    public Task<PersonalInjuryProfileDto?> GetPersonalInjuryProfileAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => Task.FromResult<PersonalInjuryProfileDto?>(null);

    public Task SavePersonalInjuryProfileAsync(Guid tenantId, Guid userId, Guid decisionMatterId, PersonalInjuryProfileSaveRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<IReadOnlyCollection<PersonalInjuryDecisionTypeDto>> GetPersonalInjuryDecisionTypesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<PersonalInjuryDecisionTypeDto>>([]);

    public Task<IReadOnlyCollection<PersonalInjuryStageDecisionDto>> GetPersonalInjuryStageDecisionMapAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyCollection<PersonalInjuryStageDecisionDto>>([]);

    public Task<Guid> CreatePersonalInjuryDraftAsync(Guid tenantId, Guid userId, PersonalInjuryMatterDraftCreateRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(Guid.NewGuid());

    public Task<PersonalInjuryMatterDraftDto?> GetPersonalInjuryDraftAsync(Guid tenantId, Guid decisionPIMatterDraftId, CancellationToken cancellationToken = default)
        => Task.FromResult<PersonalInjuryMatterDraftDto?>(null);

    public Task<bool> MarkPersonalInjuryDraftConfirmedAsync(Guid tenantId, Guid userId, Guid decisionPIMatterDraftId, Guid confirmedMatterId, CancellationToken cancellationToken = default)
        => Task.FromResult(true);

    // ── Configuration CRUD (prompts + Domain Pack children) — not exercised by the research-loop tests ──
    public Task CreatePromptConfigurationAsync(Guid tenantId, Guid actorUserId, CreateDecisionPromptConfigurationRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeletePromptConfigurationAsync(Guid actorUserId, string promptCode, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveDomainPackDimensionAsync(Guid tenantId, Guid actorUserId, string packCode, SaveDomainPackDimensionRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteDomainPackDimensionAsync(Guid tenantId, Guid actorUserId, string packCode, string dimensionCode, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveDomainPackEvidenceTypeAsync(Guid tenantId, Guid actorUserId, string packCode, SaveDomainPackEvidenceTypeRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteDomainPackEvidenceTypeAsync(Guid tenantId, Guid actorUserId, string packCode, string evidenceTypeCode, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveDomainPackVerificationProfileAsync(Guid tenantId, Guid actorUserId, string packCode, SaveDomainPackVerificationProfileRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteDomainPackVerificationProfileAsync(Guid tenantId, Guid actorUserId, string packCode, string profileCode, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveDomainPackMatterTypeAsync(Guid tenantId, Guid actorUserId, string packCode, SaveDomainPackMatterTypeRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteDomainPackMatterTypeAsync(Guid tenantId, Guid actorUserId, string packCode, string matterTypeCode, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveDomainPackConceptAsync(Guid tenantId, Guid actorUserId, string packCode, SaveDomainPackConceptRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteDomainPackConceptAsync(Guid tenantId, Guid actorUserId, string packCode, string conceptCode, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task SaveDomainPackConceptRelationAsync(Guid tenantId, Guid actorUserId, string packCode, SaveDomainPackConceptRelationRequest request, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task DeleteDomainPackConceptRelationAsync(Guid tenantId, Guid actorUserId, string packCode, string sourceConceptCode, string targetConceptCode, string relationTypeCode, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
