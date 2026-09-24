using System.Diagnostics;
using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Microsoft.Extensions.Logging;

namespace Legal.Application;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal Decision Intelligence orchestrator (/legal/decision). Implements the Core loop from
// the 1–61 placement map as extensible stages:
//   Intake → Query/Decision Contract → Hierarchy/Candidate Discovery (LLM proposal) →
//   Candidate × Branch Competition → Uncertainty/Entropy → Information Value/ADV → Adaptive
//   Narrowing (branch states + frontier) → Decision-Directed Retrieval/Evidence → Candidate
//   Recompetition → Flip Points → Convergence/Terminal State → Answer Assembly → Persistence/Audit.
// The DECISION is the primary object; POLOXI Core owns the authoritative state and all scoring (§3),
// the LLM only proposes semantics. Everything configurable comes from POLOXI.Legal_Decision* tables.
// ─────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LegalDecisionService(
    ILegalDecisionRepository repository,
    ILegalDecisionAiProvider aiProvider,
    ILegalDecisionRetriever retriever,
    IDependencyPropagationService propagationService,
    ILegalDecisionImpactMapper impactMapper,
    Features.Intelligence.Epistemic.IEpistemicDecisionBridge epistemicBridge,
    Abstractions.Persistence.IDecisionGovernanceRepository governanceRepository,
    Features.Intelligence.Epistemic.IMaterialSignalExtractor materialSignalExtractor,
    Features.Intelligence.Epistemic.IVerifiedDecisionSignalService verifiedSignalService,
    Abstractions.Persistence.IDecisionSupportSignalRepository decisionSupportSignalRepository,
    IIndependentEvidenceVerificationPipeline evidenceVerificationPipeline,
    ILegalDocumentCorpusRepository documentCorpusRepository,
    ILegalMatterContextRetriever matterContextRetriever,
    IDecisionResearchSourceRouter researchSourceRouter,
    IExecutionEnvironment executionEnvironment,
    ILogger<LegalDecisionService> logger) : ILegalDecisionService
{
    private const string DiscoveryPromptCode = "DECISION_DISCOVERY";
    // Branch-first (v2) discovery prompt. Emits a SHARED L1→L3 branch tree + one GLOBAL candidate
    // universe + a candidate×branch competition matrix, matching the Wide/semantic pipeline. Selected
    // only when the Decision.Discovery.BranchFirst.Enabled feature flag is on; v1 stays the default.
    private const string DiscoveryPromptCodeV2 = "DECISION_DISCOVERY_V2";
    private const string AnswerPromptCode = "DECISION_ANSWER";
    private const string GraphPromptCode = "DECISION_GRAPH";
    private const string VerifyPromptCode = "DECISION_VERIFY";
    private const string ResearchNeedPromptCode = "DECISION_RESEARCH_NEED";
    private const int ClarificationPromptBudget = 6000;

    internal static string BuildClarificationPromptContext(
        IEnumerable<DecisionClarificationPersistence> clarifications,
        IReadOnlySet<Guid>? relatedBranchIds = null,
        IReadOnlySet<Guid>? relatedCandidateIds = null,
        IReadOnlySet<Guid>? relatedGraphNodeIds = null,
        IReadOnlySet<Guid>? relatedGraphEdgeIds = null)
    {
        var applicable = clarifications
            .Where(item => item.ScopeCode.Equals("SESSION_LINEAGE", StringComparison.OrdinalIgnoreCase)
                || item.DecisionBranchId is { } branchId && relatedBranchIds?.Contains(branchId) == true
                || item.DecisionCandidateId is { } candidateId && relatedCandidateIds?.Contains(candidateId) == true
                || item.DecisionGraphNodeId is { } nodeId && relatedGraphNodeIds?.Contains(nodeId) == true
                || item.DecisionGraphEdgeId is { } edgeId && relatedGraphEdgeIds?.Contains(edgeId) == true)
            .OrderBy(item => item.CreatedDateUtc)
            .ThenBy(item => item.DecisionClarificationId)
            .ToArray();
        if (applicable.Length == 0)
            return string.Empty;

        var builder = new System.Text.StringBuilder("\n\nEstablished user clarifications (treat as decision context, not as independently verified evidence):");
        foreach (var item in applicable)
        {
            var line = $"\n- {item.Target ?? "detail"}: {item.Answer.Trim()}";
            if (builder.Length + line.Length > ClarificationPromptBudget)
                break;
            builder.Append(line);
        }
        return builder.ToString();
    }

    private static DecisionClarificationPersistence BuildClarificationPersistence(
        DecisionSearchRequest request,
        Guid decisionSessionId,
        DecisionSessionPersistence parentSession)
    {
        var branch = parentSession.Branches.FirstOrDefault(item =>
            string.Equals(item.DisplayName, request.ClarificationTarget, StringComparison.OrdinalIgnoreCase));
        var candidate = parentSession.Candidates.FirstOrDefault(item =>
            string.Equals(item.DisplayName, request.ClarificationTarget, StringComparison.OrdinalIgnoreCase)
            || string.Equals(item.CandidateCode, request.ClarificationTarget, StringComparison.OrdinalIgnoreCase));
        return new DecisionClarificationPersistence(
            Guid.NewGuid(), decisionSessionId, parentSession.DecisionSessionId,
            parentSession.ClarificationQuestion, request.ClarificationTarget?.Trim(), request.ClarificationAnswer!.Trim(),
            branch?.DecisionBranchId, candidate?.DecisionCandidateId, null, null,
            branch is not null || candidate is not null ? "RELATED_DECISION_SCOPE" : "SESSION_LINEAGE",
            request.TenantId, request.UserId, DateTime.UtcNow);
    }

    // Per-session execution guard for the bounded research loop. Concurrent runs (e.g. an inline decide
    // call racing an explicit cockpit "research" action) must not interleave mutations against the same
    // session, so only one loop may hold a session id at a time. In-process is sufficient for now; a
    // distributed guard can replace this later without changing the loop's semantics.
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> ActiveResearchLoops = new();

    public async Task<IReadOnlyCollection<DecisionModelOptionDto>> GetModelsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var routes = await repository.GetModelRoutesAsync(cancellationToken);
        return routes
            .Where(r => !string.IsNullOrWhiteSpace(r.ModelCode))
            .GroupBy(r => r.ModelCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(r => r.Priority).First())
            .Select(r => new DecisionModelOptionDto(r.ModelCode, r.DeploymentName, r.ProviderTypeCode))
            .ToArray();
    }

    // ── Configuration Mode admin surface (DB-backed execution settings per mode) ──────────────────
    public Task<IReadOnlyCollection<DecisionExecutionModeDto>> GetExecutionModesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetExecutionModesAsync(cancellationToken);

    public Task SaveExecutionModeAsync(Guid tenantId, Guid actorUserId, SaveDecisionExecutionModeRequest request, CancellationToken cancellationToken = default)
        => repository.SaveExecutionModeAsync(request, actorUserId, cancellationToken);


    internal static DecisionResearchRecoveryPlan BuildResearchRecoveryPlan(
        DecisionResearchNeedPersistence researchNeed,
        IReadOnlyCollection<EvidenceVerificationResult> verifications,
        string? governingJurisdiction)
    {
        var reasons = verifications
            .SelectMany(result => new[]
            {
                result.PropositionSupport.ReasonCode,
                result.Holding.ReasonCode,
                result.Authority.ReasonCode,
                result.StatementRole.ReasonCode,
            })
            .Where(reason => !string.IsNullOrWhiteSpace(reason))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var proposition = researchNeed.PropositionToResolve?.Trim() ?? string.Empty;
        var jurisdiction = governingJurisdiction?.Trim();
        var authorityKinds = ParseResearchValues(researchNeed.AuthorityKindsJson);

        if (reasons.Any(reason => reason.Contains("JURISDICTION", StringComparison.OrdinalIgnoreCase)))
            return new(DecisionResearchRecoveryDiagnoses.WrongJurisdiction, "TIGHTEN_JURISDICTION_ROUTING",
                proposition, JoinQuery(jurisdiction, proposition, "controlling authority"),
                DecisionResearchSourceClasses.LegalAuthority, researchNeed.ResearchNeedTypeCode, authorityKinds);

        if (reasons.Any(reason => reason.Contains("SOURCE_TYPE", StringComparison.OrdinalIgnoreCase)))
            return new(DecisionResearchRecoveryDiagnoses.WrongSourceClass, "SWITCH_AUTHORITY_KIND",
                proposition, JoinQuery(jurisdiction, proposition, "statute case law interpretation"),
                DecisionResearchSourceClasses.LegalAuthority, researchNeed.ResearchNeedTypeCode,
                authorityKinds.Count > 0 ? authorityKinds : ["STATUTE", "CASE"]);

        if (IsBroadResearchProposition(proposition))
        {
            var narrowed = NarrowResearchProposition(proposition);
            return new(DecisionResearchRecoveryDiagnoses.PropositionTooBroad, "DECOMPOSE_TO_ATOMIC_RULE",
                narrowed, JoinQuery(jurisdiction, narrowed, "statutory text"),
                DecisionResearchSourceClasses.LegalAuthority, DecisionResearchNeedTypes.LegalRule,
                ["STATUTE"]);
        }

        if (reasons.Any(reason => reason.Contains("PROPOSITION", StringComparison.OrdinalIgnoreCase)
                                  || reason.Contains("HOLDING", StringComparison.OrdinalIgnoreCase)
                                  || reason.Contains("UNSUPPORTED", StringComparison.OrdinalIgnoreCase)))
            return new(DecisionResearchRecoveryDiagnoses.IrrelevantPassages, "REFORMULATE_QUERY_WITH_EXACT_PROPOSITION",
                proposition, JoinQuery(jurisdiction, proposition, "holding opinion"),
                DecisionResearchSourceClasses.LegalAuthority, researchNeed.ResearchNeedTypeCode, authorityKinds);

        return new(DecisionResearchRecoveryDiagnoses.NoSupportingAuthority, "BROADEN_BOUNDED_AUTHORITY_SEARCH",
            proposition, JoinQuery(jurisdiction, proposition, "authority"),
            DecisionResearchSourceClasses.LegalAuthority, researchNeed.ResearchNeedTypeCode, authorityKinds);
    }

    private static IReadOnlyCollection<string> ParseResearchValues(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<string[]>(json) ?? []; }
        catch (JsonException) { return []; }
    }

    private static bool IsBroadResearchProposition(string proposition) =>
        proposition.Length > 180
        || proposition.Contains(" distinguishes ", StringComparison.OrdinalIgnoreCase)
        || proposition.Count(character => character == ',') >= 2;

    private static string NarrowResearchProposition(string proposition)
    {
        var marker = proposition.IndexOf(" distinguishes ", StringComparison.OrdinalIgnoreCase);
        return marker > 0 ? proposition[..marker].Trim().TrimEnd('.') : proposition;
    }

    private static string JoinQuery(params string?[] parts)
    {
        var selected = new List<string>();
        foreach (var value in parts.Where(part => !string.IsNullOrWhiteSpace(part)).Select(part => part!.Trim()))
        {
            var normalized = NormalizeQueryText(value);
            if (normalized.Length == 0
                || selected.Any(existing => ContainsQueryPhrase(NormalizeQueryText(existing), normalized)))
                continue;

            selected.RemoveAll(existing => ContainsQueryPhrase(normalized, NormalizeQueryText(existing)));
            selected.Add(value);
        }
        return string.Join(' ', selected);
    }

    private static string NormalizeQueryText(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Trim(' ', '.', ',', ';', ':', '-', '–', '—')
            .ToUpperInvariant();

    private static bool ContainsQueryPhrase(string text, string phrase) =>
        text.Equals(phrase, StringComparison.Ordinal)
        || text.StartsWith($"{phrase} ", StringComparison.Ordinal)
        || text.EndsWith($" {phrase}", StringComparison.Ordinal)
        || text.Contains($" {phrase} ", StringComparison.Ordinal);

    internal static bool IsUserResolvableClarification(DecisionBranchPersistence branch)
    {
        var proposition = string.IsNullOrWhiteSpace(branch.Interpretation)
            ? branch.DisplayName
            : branch.Interpretation;
        var needType = DecisionResearchNeedFactory.ClassifyResearchNeed(proposition);
        return needType is DecisionResearchNeedTypes.MatterFact
            or DecisionResearchNeedTypes.MatterEvidence
            or DecisionResearchNeedTypes.Mixed;
    }

    // Builds an ACTIONABLE clarification for the attorney instead of restating the branch label.
    // The question asks for the specific facts / documents / choice that would resolve the blocking
    // dependency, and states how supplying it moves the competing outcomes. The phrasing adapts to the
    // kind of missing input (matter evidence vs matter fact vs mixed fact+law) so the next execution
    // has something concrete to consume rather than "can you clarify 'X'?".
    internal static string BuildActionableClarification(DecisionBranchPersistence pivot, int competingOutcomeCount)
    {
        var proposition = string.IsNullOrWhiteSpace(pivot.Interpretation)
            ? pivot.DisplayName
            : pivot.Interpretation!.Trim();
        var needType = DecisionResearchNeedFactory.ClassifyResearchNeed(proposition);
        var subject = string.IsNullOrWhiteSpace(pivot.DisplayName) ? proposition : pivot.DisplayName.Trim();

        var effect = competingOutcomeCount > 1
            ? $" This is the pivotal fact separating the {competingOutcomeCount} competing outcomes: supplying it lets the analysis rank them; leaving it open keeps the leading outcome conditional rather than established."
            : " Supplying it lets the analysis promote the leading outcome from conditional to established.";

        var ask = needType switch
        {
            DecisionResearchNeedTypes.MatterEvidence =>
                $"What evidence establishes \u201C{subject}\u201D? Please identify or upload the relevant records, correspondence, declarations, expert reports, or other documents. If it is disputed, identify the disputed items and the competing explanation.",
            DecisionResearchNeedTypes.Mixed =>
                $"What facts and supporting documents establish \u201C{subject}\u201D? Please identify or upload the relevant evidence, and note any legal standard you want applied. If it is disputed, identify the disputed items and the opposing position.",
            _ =>
                $"Can you confirm the facts for \u201C{subject}\u201D? Please state what actually occurred and identify any documents or witnesses that support it. If it is disputed, describe the competing version.",
        };

        return ask + effect;
    }

    private static bool DomainApplicabilityMatches(string? configuredValue, string? matterValue)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
            return true;
        if (string.IsNullOrWhiteSpace(matterValue))
            return false;
        return NormalizeDomainText(configuredValue).Equals(NormalizeDomainText(matterValue), StringComparison.OrdinalIgnoreCase);
    }

    private static int ApplicabilitySpecificity(string? jurisdictionCode, string? matterTypeCode) =>
        (string.IsNullOrWhiteSpace(jurisdictionCode) ? 0 : 1)
        + (string.IsNullOrWhiteSpace(matterTypeCode) ? 0 : 1);

    private static string NormalizeDomainText(string value) =>
        new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    private static string BuildDomainGuardrailProposalContext(
        DecisionDomainPackDto domainPack,
        IReadOnlyCollection<DecisionDomainConceptDto> concepts,
        IReadOnlyCollection<DecisionDomainConceptRelationDto> relations)
    {
        var snapshot = JsonSerializer.Serialize(new
        {
            domainPack.PackCode,
            Policy = "ADVISORY_GUARDRAILS_DYNAMIC_HIERARCHY_PRIMARY",
            Concepts = concepts.Select(concept => new
            {
                concept.ConceptCode,
                concept.DimensionCode,
                concept.Name,
                concept.Description,
                concept.ConceptKindCode,
                concept.SourceClassCode,
                concept.VerificationProfileCode,
                concept.IsRequiredCoverage,
                concept.IsFallbackEligible,
            }),
            Constraints = relations.Select(relation => new
            {
                relation.SourceConceptCode,
                relation.TargetConceptCode,
                relation.RelationTypeCode,
                relation.ConstraintCode,
                relation.Description,
                relation.IsHardConstraint,
            }),
        });

        return "\n\nDOMAIN PACK SEMANTIC GUARDRAILS (database-backed, advisory):\n"
            + snapshot
            + "\nGenerate candidates and branches dynamically for THIS decision. Do not copy this taxonomy as a fixed hierarchy. "
            + "Use applicable concepts to avoid omissions, duplicates, semantic drift, and unsupported assumptions. "
            + "Novel decision-specific branches are allowed. Required coverage means the proposal should cover the concept "
            + "when it is decision-material, not that every concept must become a branch.";
    }

    private static string BuildMatterContextProposalContext(LegalMatterContextResult context)
    {
        var snapshot = JsonSerializer.Serialize(new
        {
            context.SourceRouteCode,
            Policy = "MATTER_CONTEXT_IS_GROUNDING_NOT_AUTHORITATIVE_DECISION_STATE",
            Items = context.Items.Select(item => new
            {
                item.Title,
                item.Text,
                item.SourceReference,
                item.PageNumber,
                item.ExtractionMethodCode,
                item.EvidenceStateCode,
                item.FactStateCode,
                item.IsDecisionAuthoritative,
                item.RelevanceScore,
                item.DocumentTypeCode,
                item.DimensionCode
            })
        });
        return "\n\nMATTER DOCUMENT CONTEXT (retrieved before proposal; provenance preserved):\n"
            + snapshot
            + "\nUse this context only as grounded input. Preserve uncertainty and disputes. Do not treat proposed or alleged items as verified, "
            + "and do not convert retrieved context directly into authoritative POLOXI state.";
    }

    public async Task<IReadOnlyCollection<DecisionContextDto>> GetContextsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await repository.GetContextsAsync(cancellationToken);

    public Task<IReadOnlyCollection<DecisionMatterDto>> GetMattersAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetMattersAsync(tenantId, cancellationToken);

    public Task<IReadOnlyCollection<DecisionMatterDto>> GetMattersAsync(Guid tenantId, bool includeAllTenants, CancellationToken cancellationToken = default)
        => repository.GetMattersAsync(tenantId, includeAllTenants, cancellationToken);

    public Task<DecisionMatterDto?> GetMatterAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => repository.GetMatterAsync(tenantId, decisionMatterId, cancellationToken);

    public Task<Guid> CreateMatterAsync(Guid tenantId, Guid userId, DecisionMatterCreateRequest request, CancellationToken cancellationToken = default)
        => repository.CreateMatterAsync(tenantId, userId, request, cancellationToken);

    public Task<IReadOnlyCollection<DecisionTimelineEventDto>> GetTimelineAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
        => repository.GetSessionTimelineAsync(tenantId, decisionSessionId, cancellationToken);

    public Task<IReadOnlyCollection<DecisionSessionSummaryDto>> GetMatterSessionsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => repository.GetMatterSessionsAsync(tenantId, decisionMatterId, cancellationToken);

    public Task<bool> UpdateMatterAsync(Guid tenantId, Guid userId, Guid decisionMatterId, DecisionMatterUpdateRequest request, CancellationToken cancellationToken = default)
        => repository.UpdateMatterAsync(tenantId, userId, decisionMatterId, request, cancellationToken);

    public Task<bool> UpdateMatterStatusAsync(Guid tenantId, Guid userId, Guid decisionMatterId, string statusCode, CancellationToken cancellationToken = default)
    {
        if (!DecisionMatterStatusCodes.IsValid(statusCode))
            throw new ArgumentException($"'{statusCode}' is not a valid matter status.", nameof(statusCode));
        var normalized = statusCode.ToUpperInvariant();
        return repository.UpdateMatterStatusAsync(tenantId, userId, decisionMatterId, normalized, cancellationToken);
    }

    public Task<bool> DeleteMatterAsync(Guid tenantId, Guid userId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => repository.DeleteMatterAsync(tenantId, userId, decisionMatterId, cancellationToken);

    public Task<DecisionMatterFacetsDto> GetMatterFacetsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetMatterFacetsAsync(tenantId, cancellationToken);

    public Task<DecisionDomainPackDto?> GetDomainPackAsync(Guid tenantId, string packCode, CancellationToken cancellationToken = default)
        => repository.GetDomainPackAsync(tenantId, packCode, cancellationToken);

    public Task<IReadOnlyCollection<DecisionDomainPackDto>> GetDomainPacksAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetDomainPacksAsync(tenantId, cancellationToken);

    // ── Personal Injury (Domain Pack: PERSONAL_INJURY) support ──
    public Task<PersonalInjuryOptionsDto> GetPersonalInjuryOptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetPersonalInjuryOptionsAsync(tenantId, cancellationToken);

    public Task<PersonalInjuryProfileDto?> GetPersonalInjuryProfileAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => repository.GetPersonalInjuryProfileAsync(tenantId, decisionMatterId, cancellationToken);

    public Task SavePersonalInjuryProfileAsync(Guid tenantId, Guid userId, Guid decisionMatterId, PersonalInjuryProfileSaveRequest request, CancellationToken cancellationToken = default)
        => repository.SavePersonalInjuryProfileAsync(tenantId, userId, decisionMatterId, request, cancellationToken);

    public Task<IReadOnlyCollection<PersonalInjuryDecisionTypeDto>> GetPersonalInjuryDecisionTypesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetPersonalInjuryDecisionTypesAsync(tenantId, cancellationToken);

    public Task<IReadOnlyCollection<PersonalInjuryStageDecisionDto>> GetPersonalInjuryStageDecisionMapAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetPersonalInjuryStageDecisionMapAsync(tenantId, cancellationToken);

    public Task<Guid> CreatePersonalInjuryDraftAsync(Guid tenantId, Guid userId, PersonalInjuryMatterDraftCreateRequest request, CancellationToken cancellationToken = default)
        => repository.CreatePersonalInjuryDraftAsync(tenantId, userId, request, cancellationToken);

    public Task<PersonalInjuryMatterDraftDto?> GetPersonalInjuryDraftAsync(Guid tenantId, Guid decisionPIMatterDraftId, CancellationToken cancellationToken = default)
        => repository.GetPersonalInjuryDraftAsync(tenantId, decisionPIMatterDraftId, cancellationToken);

    public Task<bool> MarkPersonalInjuryDraftConfirmedAsync(Guid tenantId, Guid userId, Guid decisionPIMatterDraftId, Guid confirmedMatterId, CancellationToken cancellationToken = default)
        => repository.MarkPersonalInjuryDraftConfirmedAsync(tenantId, userId, decisionPIMatterDraftId, confirmedMatterId, cancellationToken);

    // Transform PI decision context → DecisionSearchRequest, then run the existing POLOXI pipeline
    // unchanged. Domain-pack semantics are advisory; POLOXI Core owns all scoring. The query text is
    // enriched from the PI matter profile so the decision reflects the actual PI posture.
    public async Task<DecisionSearchResponse> DecidePersonalInjuryAsync(
        Guid tenantId, Guid userId, PersonalInjuryDecisionContext context,
        IReadOnlyCollection<string>? grantedPermissions, CancellationToken cancellationToken = default)
    {
        var matter = await repository.GetMatterAsync(tenantId, context.DecisionMatterId, cancellationToken)
            ?? throw new InvalidOperationException($"Personal Injury matter '{context.DecisionMatterId}' was not found.");

        var decisionTypes = await repository.GetPersonalInjuryDecisionTypesAsync(tenantId, cancellationToken);
        var decisionType = decisionTypes.FirstOrDefault(t =>
            string.Equals(t.DecisionTypeCode, context.DecisionTypeCode, StringComparison.OrdinalIgnoreCase));

        var profile = await repository.GetPersonalInjuryProfileAsync(tenantId, context.DecisionMatterId, cancellationToken);

        var query = BuildPersonalInjuryQuery(matter, decisionType, profile, context.Question);

        var request = new DecisionSearchRequest(tenantId, userId, query, CorrelationId: Guid.NewGuid().ToString("N"))
        {
            GrantedPermissions = grantedPermissions?.ToArray() ?? [],
            ModelCode = context.ModelCode,
            ContextCode = string.IsNullOrWhiteSpace(context.ContextCode) ? "LEGAL" : context.ContextCode,
            MatterId = context.DecisionMatterId,
            Mode = context.Mode,
            EnableReplay = context.EnableReplay,
            Posture = matter.Posture,
            MotionTarget = matter.MotionTarget,
            Jurisdiction = ResolveMatterJurisdiction(matter, profile),
            DomainPackCode = string.IsNullOrWhiteSpace(matter.DomainPackCode)
                ? DecisionDomainPackCodes.PersonalInjury
                : matter.DomainPackCode
        };

        return await DecideAsync(request, cancellationToken);
    }

    // Resolves the effective legal jurisdiction / governing law for a matter so AuthorityVerifier can
    // establish authority applicability. Prefers the structured GoverningLaw/State dimensions, then the
    // legacy free-text Jurisdiction, then the PI profile's incident state. Null when nothing is known.
    private static string? ResolveMatterJurisdiction(DecisionMatterDto matter, PersonalInjuryProfileDto? profile)
    {
        if (!string.IsNullOrWhiteSpace(matter.GoverningLaw)) return matter.GoverningLaw!.Trim();
        if (!string.IsNullOrWhiteSpace(matter.State)) return matter.State!.Trim();
        if (!string.IsNullOrWhiteSpace(matter.Jurisdiction)) return matter.Jurisdiction!.Trim();
        if (!string.IsNullOrWhiteSpace(profile?.IncidentState)) return profile!.IncidentState!.Trim();
        return null;
    }

    private static string? NormalizeAuthorityContext(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? ResolveCourtOrForum(DecisionMatterDto matter)
    {
        var values = new[] { matter.CourtSystem, matter.State, matter.CourtLevel, matter.County }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return NormalizeAuthorityContext(string.Join(" · ", values));
    }

    private static LegalAuthorityScope ResolveMatterAuthorityContext(DecisionMatterDto matter) => new()
    {
        GoverningLaw=LegalJurisdictionScope.ResolveGoverningLaw(NormalizeAuthorityContext(matter.GoverningLaw??matter.State??matter.Jurisdiction)),
        CourtSystem=NormalizeAuthorityContext(matter.CourtSystem),
        CourtOrForum=ResolveCourtOrForum(matter),
        SourceCourt=ResolveCourtOrForum(matter),
        CourtLevel=NormalizeAuthorityContext(matter.CourtLevel),
        SubjectMatterJurisdiction=NormalizeAuthorityContext(matter.SubjectMatterJurisdiction),
        PersonalTerritorialJurisdiction=NormalizeAuthorityContext(matter.PersonalTerritorialJurisdiction??matter.State),
        ProceduralLaw=NormalizeAuthorityContext(matter.ProceduralLaw),
        AuthorityCutoffDate=matter.AuthorityCutoffDate,
        StatusCode=LegalAuthorityScopeStatuses.PartiallyResolved,
        ResolutionCode=LegalAuthorityScopeResolutionCodes.MatterContract,
        ProvenanceCode=LegalAuthorityScopeResolutionCodes.MatterContract,
    };

    internal static LegalAuthorityScope ResolveAuthorityScope(
        DecisionResearchNeedPersistence researchNeed,
        DecisionSessionPersistence session)
    {
        var text=$"{researchNeed.IssueLabel} {researchNeed.ResearchQuestion} {researchNeed.PropositionToResolve}";
        var issueScope=ResolveAuthorityIssueScope(researchNeed,text);
        if(issueScope==LegalAuthorityIssueScopes.MatterEvidence)
            return new()
            {
                IssueScopeCode=issueScope,
                AuthorityRoleCode=LegalAuthorityRoles.Controlling,
                SourceTypeCode=DecisionResearchSourceClasses.MatterDocument,
                StatusCode=LegalAuthorityScopeStatuses.NotApplicable,
                ResolutionCode=LegalAuthorityScopeStatuses.NotApplicable,
                ProvenanceCode=LegalAuthorityScopeResolutionCodes.SessionSnapshot,
            };

        var procedural=issueScope==LegalAuthorityIssueScopes.ProceduralLaw;
        var settlement=issueScope==LegalAuthorityIssueScopes.SettlementEnforcement;
        var context=session.AuthorityScope;
        var rawGoverningLaw=NormalizeAuthorityContext(context?.GoverningLaw??session.GoverningLaw??session.MatterJurisdiction);
        var governingLaw=LegalJurisdictionScope.ResolveGoverningLaw(rawGoverningLaw);
        // If the governing-law value was actually a court/forum caption, retain the caption as the
        // court/forum so it is not lost when the sovereign is extracted for provider translation.
        var courtOrForum=NormalizeAuthorityContext(context?.CourtOrForum??session.CourtOrForum);
        if(courtOrForum is null&&LegalJurisdictionScope.LooksLikeCourtCaption(rawGoverningLaw))
            courtOrForum=rawGoverningLaw;
        var proceduralLaw=NormalizeAuthorityContext(context?.ProceduralLaw);
        var territorialJurisdiction=NormalizeAuthorityContext(context?.PersonalTerritorialJurisdiction);
        var conflictingContext=governingLaw is not null&&territorialJurisdiction is not null
            &&!governingLaw.Equals(territorialJurisdiction,StringComparison.OrdinalIgnoreCase)
            &&context?.ResolutionCode!=LegalAuthorityScopeResolutionCodes.RequestedAssumption;
        var resolution=governingLaw is null?LegalAuthorityScopeResolutionCodes.MissingGoverningLaw
            :conflictingContext?LegalAuthorityScopeResolutionCodes.ConflictingLegalContext
            :procedural&&courtOrForum is null?LegalAuthorityScopeResolutionCodes.MissingCourtOrForum
            :procedural&&proceduralLaw is null?LegalAuthorityScopeResolutionCodes.MissingProceduralLaw
            :LegalAuthorityScopeResolutionCodes.SessionSnapshot;
        var status=resolution is LegalAuthorityScopeResolutionCodes.MissingGoverningLaw
            or LegalAuthorityScopeResolutionCodes.ConflictingLegalContext
            or LegalAuthorityScopeResolutionCodes.MissingCourtOrForum
            or LegalAuthorityScopeResolutionCodes.MissingProceduralLaw
            ?LegalAuthorityScopeStatuses.Unresolved
            :courtOrForum is null?LegalAuthorityScopeStatuses.PartiallyResolved:LegalAuthorityScopeStatuses.Resolved;
        return new()
        {
            IssueScopeCode=issueScope,
            AuthorityRoleCode=ResolveAuthorityRole(researchNeed.AuthorityKind),
            GoverningLaw=governingLaw,
            CourtSystem=context?.CourtSystem,
            CourtOrForum=courtOrForum,
            // Provider-native court-slug resolution consumes SourceCourt only. Prefer an explicitly
            // supplied source court, otherwise fall back to the court/forum caption. The governing-law
            // sovereign is intentionally NOT used here so \u0022California\u0022 is never treated as a court id.
            SourceCourt=NormalizeAuthorityContext(context?.SourceCourt)??courtOrForum,
            CourtLevel=context?.CourtLevel,
            SubjectMatterJurisdiction=context?.SubjectMatterJurisdiction,
            PersonalTerritorialJurisdiction=context?.PersonalTerritorialJurisdiction,
            ProceduralLaw=proceduralLaw,
            ProceduralPosture=procedural||settlement?NormalizeAuthorityContext(session.QueryText):null,
            SourceTypeCode=researchNeed.AuthorityKindsJson,
            AuthorityCutoffDate=session.AuthorityCutoffDate,
            StatusCode=status,
            ResolutionCode=resolution,
            ProvenanceCode=context?.ProvenanceCode??LegalAuthorityScopeResolutionCodes.SessionSnapshot,
        };
    }

    private static string ResolveAuthorityRole(string? authorityKind)
    {
        var value=authorityKind?.Trim();
        if(value?.Equals(LegalAuthorityRoles.Persuasive,StringComparison.OrdinalIgnoreCase)==true)
            return LegalAuthorityRoles.Persuasive;
        if(value?.Equals(LegalAuthorityRoles.FederalApplyingStateLaw,StringComparison.OrdinalIgnoreCase)==true)
            return LegalAuthorityRoles.FederalApplyingStateLaw;
        return LegalAuthorityRoles.Controlling;
    }

    private static string ResolveAuthorityIssueScope(DecisionResearchNeedPersistence researchNeed,string text)
    {
        if(researchNeed.SourceClassCode.Equals(DecisionResearchSourceClasses.MatterDocument,StringComparison.OrdinalIgnoreCase)
            || DecisionResearchNeedTypes.RequiresMatterSources(researchNeed.ResearchNeedTypeCode))
            return LegalAuthorityIssueScopes.MatterEvidence;
        if(researchNeed.ResearchNeedTypeCode.Equals(DecisionResearchNeedTypes.ProceduralStandard,StringComparison.OrdinalIgnoreCase))
            return LegalAuthorityIssueScopes.ProceduralLaw;
        if(text.Contains("settlement",StringComparison.OrdinalIgnoreCase)
            || text.Contains("agreement",StringComparison.OrdinalIgnoreCase)
            || text.Contains("contract law",StringComparison.OrdinalIgnoreCase))
            return LegalAuthorityIssueScopes.SettlementEnforcement;
        if(text.Contains("summary judgment",StringComparison.OrdinalIgnoreCase)
            || text.Contains("procedure",StringComparison.OrdinalIgnoreCase)
            || text.Contains("procedural",StringComparison.OrdinalIgnoreCase)
            || text.Contains("motion",StringComparison.OrdinalIgnoreCase)
            || text.Contains("disposition",StringComparison.OrdinalIgnoreCase))
            return LegalAuthorityIssueScopes.ProceduralLaw;
        return LegalAuthorityIssueScopes.SubstantiveLaw;
    }

    private static string BuildPersonalInjuryQuery(
        DecisionMatterDto matter, PersonalInjuryDecisionTypeDto? decisionType,
        PersonalInjuryProfileDto? profile, string? question)
    {
        if (!string.IsNullOrWhiteSpace(question))
            return question!.Trim();

        var sb = new System.Text.StringBuilder();
        var decisionLabel = decisionType?.Name ?? "personal injury decision";
        sb.Append($"For the personal injury matter \"{matter.Title}\", decide: {decisionLabel}.");

        if (profile is not null)
        {
            if (!string.IsNullOrWhiteSpace(profile.IncidentTypeCode))
                sb.Append($" Incident type: {profile.IncidentTypeCode}.");
            if (profile.IncidentDate is { } date)
                sb.Append($" Incident date: {date:yyyy-MM-dd}.");
            if (!string.IsNullOrWhiteSpace(profile.IncidentState))
                sb.Append($" Jurisdiction/state: {profile.IncidentState}.");
            if (!string.IsNullOrWhiteSpace(profile.LiabilitySummary))
                sb.Append($" Liability: {profile.LiabilitySummary}.");
            if (!string.IsNullOrWhiteSpace(profile.InjurySummary))
                sb.Append($" Injuries: {profile.InjurySummary}.");
            if (!string.IsNullOrWhiteSpace(profile.DamagesSummary))
                sb.Append($" Damages: {profile.DamagesSummary}.");
        }

        return sb.ToString();
    }

    public async Task<DecisionSearchResponse> DecideAsync(DecisionSearchRequest request, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        var sessionId = Guid.NewGuid();
        LegalAuthorityScope? matterAuthorityContext=null;
        if (request.MatterId is { } authorityMatterId)
        {
            var authorityMatter = await repository.GetMatterAsync(request.TenantId, authorityMatterId, cancellationToken);
            if (authorityMatter is not null)
            {
                request = request with
                {
                    Jurisdiction = ResolveMatterJurisdiction(authorityMatter, null),
                    GoverningLaw = NormalizeAuthorityContext(authorityMatter.GoverningLaw),
                    CourtOrForum = ResolveCourtOrForum(authorityMatter),
                };
                matterAuthorityContext=ResolveMatterAuthorityContext(authorityMatter);
            }
        }
        var settings = await repository.GetCoreSettingsAsync(cancellationToken);
        var v2Settings = await repository.GetV2SettingsAsync(cancellationToken);
        var useGraph = request.UseDependencyGraph ?? v2Settings.UseDependencyGraphDefault;
        var contextCode = string.IsNullOrWhiteSpace(request.ContextCode) ? DecisionContexts.General : request.ContextCode!.Trim().ToUpperInvariant();
        var events = new List<DecisionEventPersistence>();
        var sequence = 0;
        void Record(string type, string stage, object? payload = null) =>
            events.Add(new DecisionEventPersistence(Guid.NewGuid(), null, sequence++, type, stage, payload is null ? null : JsonSerializer.Serialize(payload), null));

        DecisionSessionPersistence? parentSession = null;
        IReadOnlyCollection<DecisionClarificationPersistence> priorClarifications = [];
        if (request.ParentDecisionSessionId is { } parentSessionId)
        {
            parentSession = await repository.GetSessionAsync(request.TenantId, parentSessionId, cancellationToken);
            if (parentSession is not null)
                priorClarifications = await repository.GetClarificationLineageAsync(
                    request.TenantId, parentSessionId, cancellationToken);
        }

        // Resolve the frozen execution mode ONCE for this decision. A continuation must keep the mode
        // the original session was created under (immutable snapshot), so the parent's ModeCode wins;
        // otherwise the request's Mode is honored, then validated against DB policy and the deployment
        // environment (DEV Logic is rejected server-side in Production).
        var (effectiveMode, modeDefaultModelCode) = await ResolveExecutionModeAsync(
            request.Mode, parentSession?.ModeCode, cancellationToken);
        // When the caller did not pin a model, fall back to the mode's DB-configured default model.
        var routeModelCode = string.IsNullOrWhiteSpace(request.ModelCode) ? modeDefaultModelCode : request.ModelCode;
        // Branch-first discovery (v2): mirrors the /legal/search Wide semantic pipeline. When enabled,
        // discovery loads DECISION_DISCOVERY_V2 (a shared semantic root/branch forest + a scoreless
        // global candidate universe) and adapts it into the same ProposedCandidate shape the legacy
        // candidate-first path produces, so Core scoring/ranking/retrieval stay unchanged. Default off.
        var branchFirstDiscovery = v2Settings.BranchFirstDiscoveryEnabled;
        var discoveryPromptCode = branchFirstDiscovery ? DiscoveryPromptCodeV2 : DiscoveryPromptCode;
        var route = await ResolveRouteAsync(discoveryPromptCode, routeModelCode, cancellationToken);
        // A continued session folds the accumulated clarification lineage into the effective query (§7
        // loop), so every proposal layer re-competes with all applicable disambiguating details in hand.
        var hasClarification = !string.IsNullOrWhiteSpace(request.ClarificationAnswer);
        var hasCounterfactual = !string.IsNullOrWhiteSpace(request.CounterfactualAssumption);
        var effectiveQuery = request.Query;
        var currentClarification = hasClarification && parentSession is not null
            ? BuildClarificationPersistence(request, sessionId, parentSession)
            : null;
        var clarificationContext = BuildClarificationPromptContext(currentClarification is null
            ? priorClarifications
            : priorClarifications.Append(currentClarification));
        effectiveQuery += clarificationContext;
        if (hasClarification)
        {
            if (parentSession is null)
                effectiveQuery += $"\n\nClarification ({request.ClarificationTarget ?? "detail"}): {request.ClarificationAnswer}";
        }
        if (hasCounterfactual)
            effectiveQuery += $"\n\nCounterfactual assumption (treat as established for this analysis): {request.CounterfactualAssumption}";

        DecisionDomainPackDto? domainPack = null;
        IReadOnlyList<DecisionDomainConceptDto> applicableDomainConcepts = [];
        IReadOnlyList<DecisionDomainConceptRelationDto> applicableDomainRelations = [];
        if (!string.IsNullOrWhiteSpace(request.DomainPackCode))
        {
            domainPack = await repository.GetDomainPackAsync(request.TenantId, request.DomainPackCode, cancellationToken);
            DecisionMatterDto? domainMatter = null;
            if (request.MatterId is { } domainMatterId)
                domainMatter = await repository.GetMatterAsync(request.TenantId, domainMatterId, cancellationToken);

            applicableDomainConcepts = (domainPack?.Concepts ?? [])
                .Where(concept => DomainApplicabilityMatches(concept.JurisdictionCode, request.Jurisdiction)
                    && DomainApplicabilityMatches(concept.MatterTypeCode, domainMatter?.MatterTypeCode))
                .GroupBy(concept => concept.ConceptCode, StringComparer.OrdinalIgnoreCase)
                .Select(group => group
                    .OrderByDescending(concept => ApplicabilitySpecificity(concept.JurisdictionCode, concept.MatterTypeCode))
                    .ThenBy(concept => concept.SortOrder)
                    .First())
                .OrderBy(concept => concept.SortOrder)
                .ToArray();
            var applicableCodes = applicableDomainConcepts
                .Select(concept => concept.ConceptCode)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            applicableDomainRelations = (domainPack?.ConceptRelations ?? [])
                .Where(relation => applicableCodes.Contains(relation.SourceConceptCode)
                    && applicableCodes.Contains(relation.TargetConceptCode)
                    && DomainApplicabilityMatches(relation.JurisdictionCode, request.Jurisdiction)
                    && DomainApplicabilityMatches(relation.MatterTypeCode, domainMatter?.MatterTypeCode))
                .OrderBy(relation => relation.SortOrder)
                .ToArray();
        }

        Record("SESSION_STARTED", "INTAKE", new
        {
            request.Query,
            contextCode,
            route.ModelCode,
            request.UsePoloxiEngine,
            hasClarification,
            hasCounterfactual,
            domainPackCode = domainPack?.PackCode,
            domainConceptCount = applicableDomainConcepts.Count,
            domainConstraintCount = applicableDomainRelations.Count,
        });

        if (!request.UsePoloxiEngine)
            return await ComposeDirectAnswerAsync(request, sessionId, contextCode, effectiveQuery,
                currentClarification, events, timer, effectiveMode.ExecutionModeCode, routeModelCode, cancellationToken);

        // ── Additive Decision-Contract completeness PREFLIGHT (§ clarification eligibility gate) ──────
        // Catches an ESSENTIAL missing instruction/fact BEFORE paying for the full discovery/graph/
        // verification pipeline. Off by default and deliberately conservative: it never fires on a
        // continuation that already carries a clarification answer, never on an explicit hypothetical
        // (counterfactual) request, and (per settings) not on "zero documents" alone — a fact-bearing
        // query is still allowed a valid hypothetical analysis. This does NOT replace POLOXI's dynamic
        // clarification logic; ambiguities that only surface after candidate competition are still
        // handled later by the post-competition clarification gate.
        if (settings.Preflight.Enabled && !hasClarification && !hasCounterfactual
            && TryBuildPreflightClarification(settings.Preflight, request, effectiveQuery, out var preflightTarget, out var preflightQuestion))
        {
            Record("PREFLIGHT_CLARIFICATION_GATED", "CLARIFICATION_REQUIRED", new
            {
                cause = "ESSENTIAL_DECISION_CONTRACT_INPUT_MISSING",
                target = preflightTarget,
                hasPosture = !string.IsNullOrWhiteSpace(request.Posture),
                hasMotionTarget = !string.IsNullOrWhiteSpace(request.MotionTarget),
                effectiveQueryLength = effectiveQuery.Length
            });
            return await ComposePreflightClarificationAsync(request, sessionId, contextCode,
                currentClarification, preflightTarget, preflightQuestion, events, timer, effectiveMode.ExecutionModeCode, cancellationToken);
        }

        var retrievalSettings = await documentCorpusRepository.GetRetrievalArchitectureSettingsAsync(cancellationToken);
        LegalMatterContextResult matterContext = new(false, DecisionResearchRouteCodes.NoneDerived, [], 0, 0, "NO_MATTER");
        if (request.MatterId is { } matterId)
        {
            var matterContextTimer = Stopwatch.StartNew();
            matterContext = await matterContextRetriever.RetrieveAsync(
                request.TenantId, request.UserId, matterId, effectiveQuery, retrievalSettings, cancellationToken);
            matterContextTimer.Stop();
            Record("MATTER_CONTEXT_RETRIEVED", "MATTER_CONTEXT", new
            {
                matterContext.Enabled,
                matterContext.SourceRouteCode,
                matterContext.CandidateCount,
                matterContext.FilteredCount,
                returnedCount = matterContext.Items.Count,
                matterContext.NotRunReason
            });
            if (retrievalSettings.TelemetryEnabled)
            {
                await documentCorpusRepository.PersistRetrievalTelemetryAsync(
                    request.TenantId,
                    request.UserId,
                    new DecisionRetrievalTelemetry(
                        Guid.NewGuid(), null, matterId, DecisionRetrievalStages.MatterContext,
                        "MATTER_CONTEXT_RETRIEVED", matterContext.SourceRouteCode, matterContext.Enabled,
                        matterContext.CandidateCount, matterContext.FilteredCount, matterContext.Items.Count,
                        null, "MATTER_DOCUMENT", request.Jurisdiction,
                        JsonSerializer.Serialize(new { matterContext.NotRunReason }),
                        matterContextTimer.ElapsedMilliseconds),
                    cancellationToken);
            }
        }

        // ── Candidate Discovery (LLM proposal only) ─────────────────────────────────────────────
        var discoveryPrompt = await repository.GetPromptAsync(discoveryPromptCode, cancellationToken)
            ?? throw new InvalidOperationException($"The '{discoveryPromptCode}' decision prompt is not configured in POLOXI.Legal_DecisionPrompt.");
        var discoveryUser = discoveryPrompt.UserPromptTemplate
            .Replace("{{QUERY}}", effectiveQuery)
            .Replace("{{CONTEXT}}", contextCode);
        if (domainPack is not null && applicableDomainConcepts.Count > 0)
            discoveryUser += BuildDomainGuardrailProposalContext(domainPack, applicableDomainConcepts, applicableDomainRelations);
        if (matterContext.Items.Count > 0)
            discoveryUser += BuildMatterContextProposalContext(matterContext);
        var discovery = await aiProvider.GenerateAsync(
            new DecisionAiRequest(route, discoveryPromptCode, discoveryPrompt.SystemPrompt, discoveryUser, discoveryPrompt.OutputSchemaJson, request.CorrelationId),
            cancellationToken);
        var llmCalls = 1;
        Record("CANDIDATES_PROPOSED", "DISCOVERY", new { discovery.InputTokenCount, discovery.OutputTokenCount, branchFirst = branchFirstDiscovery });

        // Branch-first (v2) emits the /legal/search Wide semantic shape (semanticRoots + scoreless global
        // candidates) and is adapted into the same ProposedCandidate shape so everything downstream is
        // identical. The legacy candidate-first path parses candidates-own-branches directly.
        var proposal = branchFirstDiscovery
            ? AdaptSemanticProposal(discovery.StructuredOutputJson ?? discovery.Content, settings.MaxCandidates)
            : ParseProposal(discovery.StructuredOutputJson ?? discovery.Content, settings.MaxCandidates);

        // ── Proposal Integrity Gate V2 (shadow-default, disposition-driven targeted recovery) ──────
        // The LLM only PROPOSES a semantic representation; POLOXI decides whether that representation is
        // fit to become authoritative before any downstream reasoning proceeds. The gate ALWAYS computes:
        //   • structural validity (parseable, required fields, ≥2 distinct candidate outcomes),
        //   • shadow-mode semantic diagnostics (query fidelity, interpretation distinctness/coverage,
        //     candidate separability, empty/duplicate interpretations),
        //   • a diagnosed disposition (Accept / Repair / Regenerate / ...).
        //
        // SHADOW MODE is the default (settings.EnableProposalRecovery == false): the disposition is
        // recorded as a PREDICTION only and attempt #1 competes untouched. This lets us establish whether
        // P(good result | gate PASS) is materially higher than P(good result | gate FAIL) before any
        // recovery is enabled.
        //
        // When recovery IS enabled, a non-Accept disposition triggers EXACTLY ONE defect-targeted,
        // diagnosed recovery (REPAIR, not a re-roll — valid structure is preserved). If the recovered
        // proposal passes ⇒ PROPOSAL_INTEGRITY_RECOVERED and it competes. If it still fails ⇒
        // PROPOSAL_INTEGRITY_UNRESOLVED and it MUST NOT compete. Attempt #1 (defects + what changed) is
        // always preserved as recovery telemetry.
        var integrity = EvaluateProposalIntegrity(proposal);
        var diagnostics = BuildProposalIntegrityDiagnostics(effectiveQuery, proposal, integrity);
        var (disposition, defects) = DiagnoseProposal(diagnostics);

        // Decision Integrity Trace (Proposal stage) inputs — a read-only projection of the gate outcome.
        // Tracks the final disposition/attempt/defects so the cockpit can show ACCEPT · Attempt 1 or the
        // recovered path (Attempt 2). Updated below when an ACTIVE-mode recovery runs.
        var proposalTraceDisposition = disposition;
        var proposalTraceAttempt = 1;
        var proposalTraceRecoveryAttempted = false;
        var proposalTraceRecovered = false;
        var proposalTraceDefects = defects;

        // Predicted action lets us reconstruct exactly what ACTIVE mode WOULD have done from SHADOW runs,
        // without rerunning the workload. In shadow mode ActualAction is always CONTINUE_ATTEMPT_1.
        var predictedAction = disposition switch
        {
            ProposalDisposition.Accept => "WOULD_ACCEPT",
            ProposalDisposition.Clarify => "WOULD_STOP",
            ProposalDisposition.Degraded => "WOULD_STOP",
            _ => "WOULD_RECOVER"
        };
        var actualAction = settings.EnableProposalRecovery ? "ACTIVE" : "CONTINUE_ATTEMPT_1";

        Record("PROPOSAL_INTEGRITY_PREDICTION", "DISCOVERY", new
        {
            mode = settings.EnableProposalRecovery ? "ACTIVE" : "SHADOW",
            disposition = disposition.ToString(),
            predictedAction,
            actualAction,
            structuralValid = integrity.IsAcceptable,
            defects
        });

        if (!settings.EnableProposalRecovery)
        {
            // SHADOW MODE: never reject or repair. Log the prediction + diagnostics and proceed with
            // attempt #1 exactly as-is.
            Record("PROPOSAL_INTEGRITY_DIAGNOSTICS", "DISCOVERY", diagnostics);
        }
        else if (disposition != ProposalDisposition.Accept)
        {
            // ACTIVE MODE: the disposition is authoritative and warrants a bounded, targeted recovery.
            var attempt1Defects = defects;
            Record("PROPOSAL_INTEGRITY_FAILED", "DISCOVERY", new { disposition = disposition.ToString(), defects = attempt1Defects, candidateCount = proposal.Count });

            var recoveryUser = discoveryUser + "\n\n" + BuildRecoveryInstruction(attempt1Defects);
            Record("PROPOSAL_RECOVERY_ATTEMPTED", "DISCOVERY", new { disposition = disposition.ToString(), defects = attempt1Defects });
            proposalTraceRecoveryAttempted = true;
            proposalTraceAttempt = 2;
            var recovery = await aiProvider.GenerateAsync(
                new DecisionAiRequest(route, discoveryPromptCode, discoveryPrompt.SystemPrompt, recoveryUser, discoveryPrompt.OutputSchemaJson, request.CorrelationId),
                cancellationToken);
            llmCalls++;
            var recovered = branchFirstDiscovery
                ? AdaptSemanticProposal(recovery.StructuredOutputJson ?? recovery.Content, settings.MaxCandidates)
                : ParseProposal(recovery.StructuredOutputJson ?? recovery.Content, settings.MaxCandidates);
            var recoveredIntegrity = EvaluateProposalIntegrity(recovered);
            var recoveredDiagnostics = BuildProposalIntegrityDiagnostics(effectiveQuery, recovered, recoveredIntegrity);
            var (recoveredDisposition, recoveredDefects) = DiagnoseProposal(recoveredDiagnostics);

            if (recoveredIntegrity.IsAcceptable)
            {
                proposal = recovered;
                integrity = recoveredIntegrity;
                diagnostics = recoveredDiagnostics with
                {
                    RecoveryTriggered = true,
                    RecoveryReason = string.Join(",", attempt1Defects),
                    RecoverySucceeded = true
                };
                Record("PROPOSAL_INTEGRITY_RECOVERED", "DISCOVERY", new
                {
                    attempt1Defects,
                    attempt2Disposition = recoveredDisposition.ToString(),
                    attempt2Defects = recoveredDefects,
                    candidateCount = proposal.Count
                });
                Record("PROPOSAL_INTEGRITY_DIAGNOSTICS", "DISCOVERY", diagnostics);
                proposalTraceRecovered = true;
                proposalTraceDisposition = recoveredDisposition;
                proposalTraceDefects = attempt1Defects;
            }
            else
            {
                Record("PROPOSAL_INTEGRITY_UNRESOLVED", "DISCOVERY", new
                {
                    attempt1Defects,
                    attempt2Defects = recoveredDefects,
                    candidateCount = recovered.Count
                });
                throw new InvalidOperationException(
                    $"The decision proposal failed the integrity gate and could not be recovered (defects: {string.Join(", ", recoveredDefects)}). The proposal is not eligible to compete.");
            }
        }
        else
        {
            // ACTIVE MODE, Accept disposition: no recovery needed.
            Record("PROPOSAL_INTEGRITY_DIAGNOSTICS", "DISCOVERY", diagnostics);
        }

        if (proposal.Count == 0)
            throw new InvalidOperationException("The decision proposal layer returned no candidate outcomes.");

        // Freeze the Proposal-stage trace summary now that the gate has settled (accept or recovered).
        var proposalIntegritySummary = new Features.Intelligence.Decision.DecisionProposalIntegritySummaryDto(
            Mode: settings.EnableProposalRecovery ? "ACTIVE" : "SHADOW",
            Disposition: proposalTraceDisposition.ToString().ToUpperInvariant(),
            Attempt: proposalTraceAttempt,
            RecoveryAttempted: proposalTraceRecoveryAttempted,
            Recovered: proposalTraceRecovered,
            StructurallyValid: integrity.IsAcceptable,
            Defects: proposalTraceDefects?.ToArray() ?? []);

        // ── Candidate × Branch competition + deterministic Core scoring ──────────────────────────
        var candidates = ScoreCandidates(proposal, settings, sessionId, request.TenantId, out var branches, cancellationToken);
        if (applicableDomainConcepts.Count > 0)
        {
            var governance = ApplyDomainGuardrails(
                effectiveQuery, candidates, branches, applicableDomainConcepts, applicableDomainRelations);
            branches = governance.Branches;
            Record("DOMAIN_GUARDRAILS_APPLIED", "COMPETITION", new
            {
                domainPackCode = domainPack?.PackCode,
                governance.MatchedBranchCount,
                governance.NovelBranchCount,
                governance.FallbackBranchCount,
                applicableConceptCount = applicableDomainConcepts.Count,
                applicableConstraintCount = applicableDomainRelations.Count,
                policy = "DYNAMIC_PRIMARY_ADVISORY_GUARDRAILS",
            });
        }
        Record("CANDIDATES_SCORED", "COMPETITION", new { candidateCount = candidates.Count });

        // Bounded adaptive deepening telemetry: emit an observable event only when the deepening pass
        // actually materialized sub-branches (LevelNumber > 1), so a deepened run is traceable and the
        // seeded regression matter mirrors real runtime behavior. Flat runs (the common case) skip it.
        var maxDepthReached = branches.Count == 0 ? 0 : branches.Max(b => b.LevelNumber);
        if (maxDepthReached > 1)
            Record("BRANCH_DEEPENED", "COMPETITION", new
            {
                depth = maxDepthReached,
                deepenedBranchCount = branches.Count(b => b.LevelNumber > 1),
                deepeningFlip = settings.ThresholdDeepeningFlip,
                maxDepth = settings.MaxDepth
            });

        // ── Uncertainty / Entropy (uncertainty signal only, not truth) ──────────────────────────
        var orderedScores = candidates.Select(c => (double)c.CompositeScore).OrderByDescending(x => x).ToArray();
        var distribution = DecisionCoreMath.Distribution(orderedScores);
        var entropy = DecisionCoreMath.NormalizedEntropy(distribution);
        var margin = DecisionCoreMath.Margin(orderedScores);

        // ── Decision-Directed Retrieval / Evidence (§13,§14) ────────────────────────────────────
        var retrieval = new EvidenceRetrievalOutcome([], [], DecisionResearchStates.RequiredPending, "AUTHORITATIVE_RESEARCH_NEED_ROUTING_REQUIRED");
        var evidence = retrieval.Evidence;
        Record("EVIDENCE_RETRIEVAL_DEFERRED", "RETRIEVAL", new
        {
            evidenceCount = evidence.Count,
            researchStatus = retrieval.ResearchStatus,
            failureDetail = retrieval.FailureDetail
        });
        if (retrievalSettings.LegacyUnconditionalRetrievalEnabled)
            Record("LEGACY_RETRIEVAL_BYPASS_IGNORED", "RETRIEVAL", new { reason = "AUTHORITATIVE_RESEARCH_NEED_ROUTING_REQUIRED" });

        // ── Frontier + Flip Points (§11,§30) ────────────────────────────────────────────────────
        var flipPoints = BuildFlipPoints(branches, candidates, sessionId, request.TenantId);

        // ── Convergence / Terminal State (§32,§33,§34) ──────────────────────────────────────────
        var winner = candidates.OrderBy(c => c.RankOrder).FirstOrDefault();
        var frontierOpen = branches.Any(b => b.IsOnFrontier);
        var maxAvailableAdv = branches.Count == 0 ? 0d : branches.Max(b => (double)b.AdvScore);
        var (statusCode, terminalState, reason) = ResolveTerminalState(settings, margin, entropy, frontierOpen, maxAvailableAdv);

        // ── User clarification (§7): an ambiguous, high-entropy tie the engine cannot break by itself is
        // resolved by asking the user once. A session already carrying a clarification answer never re-asks.
        string? clarificationQuestion = null;
        string? clarificationTarget = null;
        if (!hasClarification && statusCode != DecisionStatusCodes.DecisionReady && entropy >= 0.85 && margin < 0.05)
        {
            var pivot = branches.Where(b => b.IsOnFrontier && IsUserResolvableClarification(b))
                .OrderByDescending(b => b.FlipPotential).FirstOrDefault()
                ?? branches.Where(IsUserResolvableClarification).OrderByDescending(b => b.FlipPotential).FirstOrDefault();
            if (pivot is not null)
            {
                statusCode = DecisionStatusCodes.UserClarificationRequired;
                terminalState = DecisionStatusCodes.UserClarificationRequired;
                reason = "AMBIGUOUS_TIE_NEEDS_USER_INPUT";
                clarificationTarget = pivot.DisplayName;
                clarificationQuestion = BuildActionableClarification(pivot, candidates.Count);

                // Gate diagnostics (§7): records WHY the clarification gate fired so a genuine tie can be
                // told apart from thin/unverified evidence at a glance in the timeline. The gate itself is
                // correct either way; these signals point at the upstream cause (evidence vs discrimination).
                var verifiedSourceCount = evidence.Count(e => string.Equals(e.VerificationStatus, DecisionVerificationStates.Verified, StringComparison.OrdinalIgnoreCase));
                var topTwo = candidates.OrderBy(c => c.RankOrder).Take(2).ToArray();
                var pairwiseGap = topTwo.Length == 2 ? Math.Abs((double)(topTwo[0].CompositeScore - topTwo[1].CompositeScore)) : 0d;
                var cause = verifiedSourceCount == 0
                    ? "THIN_EVIDENCE_NO_VERIFIED_SOURCES"
                    : pairwiseGap < 0.01
                        ? "LOW_CANDIDATE_DISCRIMINATION"
                        : "GENUINE_TIE";
                Record("CLARIFICATION_GATED", "CLARIFICATION_REQUIRED", new
                {
                    cause,
                    pivot = pivot.DisplayName,
                    entropy,
                    margin,
                    pairwiseGap,
                    verifiedSourceCount,
                    evidenceCount = evidence.Count,
                    candidateCount = candidates.Count
                });
            }
        }
        // The run always terminates here, but that is NOT the same as the decision converging. A run
        // can complete while the decision remains provisional with research still open. The timeline
        // phase reflects the DECISION state (not the run) so a provisional run is never mislabeled as
        // "CONVERGENCE". This is a presentation label only; it does not affect scoring or the pipeline.
        var terminalPhase = statusCode switch
        {
            DecisionStatusCodes.DecisionReady => "CONVERGENCE",
            DecisionStatusCodes.ProvisionalDecision => "PROVISIONAL_RESEARCH_REMAINS",
            DecisionStatusCodes.UserClarificationRequired => "CLARIFICATION_REQUIRED",
            DecisionStatusCodes.ResearchExhausted => "RESEARCH_EXHAUSTED",
            _ => "TERMINAL_STATE"
        };
        Record("TERMINAL_STATE", terminalPhase, new { statusCode, terminalState, margin, entropy });

        // ── Answer Assembly (§37): composer reads the structured artifact only ──────────────────
        string? finalAnswer = null;
        if (statusCode == DecisionStatusCodes.DecisionReady || statusCode == DecisionStatusCodes.ResearchExhausted || statusCode == DecisionStatusCodes.ProvisionalDecision)
        {
            finalAnswer = await ComposeAnswerAsync(request, effectiveQuery, candidates, branches, flipPoints, margin, entropy, cancellationToken);
            llmCalls++;
            Record("ANSWER_COMPOSED", "ANSWER", null);
        }

        timer.Stop();

        // TELEMETRY SCOPE (persisted LlmCallCount / DurationMs): these capture ONLY the V1 core loop
        // up to session persistence — discovery (1) + optional proposal recovery + optional answer
        // composition. The timer is stopped here, BEFORE the POLOXI Legal V2 dependency-graph pass
        // (RunDependencyGraphAsync: DECISION_GRAPH + DECISION_VERIFY) and the epistemic governance
        // overlay run further below. Those additional Azure AI calls are therefore NOT included in the
        // persisted count/duration, which is why server logs can show more LLM calls and higher wall
        // time than this session row reports. This is intentional scoping (core-loop metrics), not a
        // miscount; the V2/governance work is separately traced in the integrity trace stages.

        var nextAction = BuildNextBestAction(branches, statusCode);
        var readiness = BuildReadiness(candidates, branches, evidence, margin, entropy, statusCode, retrieval.ResearchStatus);

        var persistence = new DecisionSessionPersistence(
            sessionId, request.TenantId, request.UserId, request.Query, contextCode, route.ModelCode, true,
            statusCode, terminalState, reason, winner?.DecisionCandidateId,
            (decimal)0, (decimal)entropy, (decimal)margin,
            branches.Count == 0 ? 0 : branches.Max(b => b.LevelNumber), llmCalls, timer.ElapsedMilliseconds,
            finalAnswer, clarificationQuestion, clarificationTarget, request.CorrelationId,
            candidates, branches, evidence, flipPoints, events)
        {
            MatterId = request.MatterId,
            NextBestActionText = nextAction?.Title,
            NextBestActionImpactCode = nextAction?.ImpactCode,
            NextBestActionRationale = nextAction?.Rationale,
            CounterfactualAssumption = request.CounterfactualAssumption,
            ResearchStatusCode = retrieval.ResearchStatus,
            ResearchFailureDetail = retrieval.FailureDetail,
            ParentDecisionSessionId = parentSession?.DecisionSessionId,
            MatterJurisdiction = NormalizeAuthorityContext(request.Jurisdiction),
            GoverningLaw = NormalizeAuthorityContext(request.GoverningLaw),
            CourtOrForum = NormalizeAuthorityContext(request.CourtOrForum),
            AuthorityCutoffDate = request.AuthorityCutoffDate,
            AuthorityScope = matterAuthorityContext,
            ModeCode = effectiveMode.ExecutionModeCode,
        };
        await repository.PersistSessionAsync(persistence, cancellationToken);
        if (currentClarification is not null)
            await repository.PersistClarificationAsync(currentClarification, cancellationToken);
        var persistedEvidenceVerifications = retrieval.Verifications.Select(v => ToPersistence(
            v, sessionId, request.TenantId, request.UserId, request.MatterId)).ToArray();
        var persistedVerificationsByEvidence = persistedEvidenceVerifications
            .ToDictionary(v => v.DecisionEvidenceId);
        await repository.PersistEvidenceVerificationsAsync(persistedEvidenceVerifications, cancellationToken);

        // ── POLOXI Legal V2 (dependency-aware) — runs only when the per-session toggle is on. It
        // proposes a typed legal dependency graph, runs an INDEPENDENT verifier, propagates any
        // invalidation deterministically, runs the strongest-losing-side gate, and computes the
        // dependency-constrained readiness verdict. Persisted after the session row (FK dependency). ──
        DecisionV2Result? v2 = null;
        string? graphDiagnostic = null;
        Features.Intelligence.Decision.DecisionGovernanceVerdictDto? governanceVerdict = null;
        Features.Intelligence.Decision.DecisionSolverShadowDto? solverShadow = null;
        // Decision Integrity Trace capture (Research + Output Control stages). Populated below when the
        // research loop / output audit run; folded into the response by DecorateTrace at every return.
        Features.Intelligence.Decision.DecisionResearchLoopSummaryDto? researchSummary = null;
        IReadOnlyCollection<Features.Intelligence.Decision.DecisionOutputAuthorizationDto> outputAuthorizations = [];
        Features.Intelligence.Decision.DecisionOutputTransformSummaryDto? outputTransformSummary = null;
        Features.Intelligence.Decision.DecisionOutputClaimExtractionDto? outputClaimExtraction = null;
        Features.Intelligence.Decision.DecisionResearchEligibilityDto? researchEligibility = null;
        {
            try
            {
                v2 = await RunDependencyGraphAsync(request, sessionId, contextCode, effectiveQuery,
                    candidates, branches, v2Settings, winner, cancellationToken);
                await repository.PersistGraphAsync(v2.Persistence, cancellationToken);

                // EA-6/EA-7: advisory epistemic governance overlay. Projects the V2 graph into
                // authoritative EA claims (verify + authority gate), runs readiness + output audit, and
                // records a NON-DESTRUCTIVE governance verdict. Advisory by default: it never changes the
                // V1/V2 verdict; all claims stay visible for reference. Strictly non-blocking.
                try
                {
                    var epistemicContext = new Features.Intelligence.Epistemic.EpistemicDecisionContext
                    {
                        SessionId = sessionId,
                        TenantId = request.TenantId,
                        MatterId = request.MatterId,
                        ActorUserId = request.UserId,
                        ProposedByModel = route.ModelCode,
                        PromptRunId = request.CorrelationId,
                        Nodes = v2.Nodes.ToArray(),
                        Edges = v2.Edges.ToArray(),
                        FinalAnswer = persistence.FinalAnswer,
                    };
                    var governance = await epistemicBridge.ProjectAndGovernAsync(epistemicContext, cancellationToken);
                    if (governance.Executed)
                    {
                        outputClaimExtraction = new Features.Intelligence.Decision.DecisionOutputClaimExtractionDto(
                            governance.ClaimExtraction.Attempted,
                            governance.ClaimExtraction.SourceLength,
                            governance.ClaimExtraction.SubstantiveAnswer,
                            governance.ClaimExtraction.ClaimsReturned,
                            governance.ClaimExtraction.StatusCode,
                            governance.ClaimExtraction.FailureReason);
                        governanceVerdict = await PersistGovernanceVerdictAsync(
                            request, sessionId, v2, governance, cancellationToken);
                        logger.LogInformation(
                            "EA-7 governance for session {SessionId}: {Projected} claim(s), {Authorized} authorized, ready={Ready}, output-clean={Clean}, mode={Mode}, override={Override}.",
                            sessionId, governance.ProjectedClaimCount, governance.AuthorizedClaimCount,
                            governance.Readiness?.IsReady, governance.OutputAudit?.IsClean,
                            Features.Intelligence.Epistemic.EpistemicOverrideModes.ToCode(governance.OverrideMode),
                            governance.OverrideApplied);

                        // Track exactly which claims the enforcement pass located and rewrote in the prose,
                        // plus required/applied counts, so the Decision Integrity Trace reflects the real
                        // transformation outcome instead of assuming every non-ALLOW claim was applied.
                        var appliedProseClaimIds = new HashSet<Guid>();
                        var proseRequired = 0;
                        var proseApplied = 0;
                        var postTransformClean = true;



                        // ── Authoritative output-claim audit enforcement ─────────────────────────
                        // The audit is no longer advisory: if the composed answer surfaces material
                        // claims POLOXI never authorized, the answer MUST be restated as a provisional,
                        // pending-research hypothesis (not an asserted legal conclusion). Rewrite the
                        // in-memory record AND the persisted row so the DB and returned response agree.
                        if (governance.OutputAudit is { Enforced: true, IsClean: false } audit
                            && !string.IsNullOrWhiteSpace(persistence.FinalAnswer))
                        {
                            var enforcement = EnforceOutputAudit(persistence.FinalAnswer, audit);
                            persistence = persistence with { FinalAnswer = enforcement.Answer };
                            await repository.UpdateSessionAnswerAsync(
                                request.TenantId, request.UserId, sessionId, enforcement.Answer, cancellationToken);
                            appliedProseClaimIds = enforcement.AppliedClaimIds.ToHashSet();
                            proseRequired = enforcement.RequiredCount;
                            proseApplied = enforcement.AppliedCount;
                            postTransformClean = !enforcement.UnauthorizedAssertionsRemain;
                            var qualifyCount = audit.Authorizations.Count(a => a.Disposition == Features.Intelligence.Epistemic.OutputClaimDisposition.Qualify);
                            var suppressCount = audit.Authorizations.Count(a => a.Disposition == Features.Intelligence.Epistemic.OutputClaimDisposition.Suppress);
                            var correctCount = audit.Authorizations.Count(a => a.Disposition == Features.Intelligence.Epistemic.OutputClaimDisposition.Correct);
                            logger.LogInformation(
                                "Output audit ENFORCED for session {SessionId}: {Qualify} QUALIFY, {Suppress} SUPPRESS, {Correct} CORRECT; prose transforms {Applied}/{Required} applied, post-transform {Clean} ({Violations} unauthorized, {Unknown} unknown claim(s)).",
                                sessionId, qualifyCount, suppressCount, correctCount, proseApplied, proseRequired,
                                postTransformClean ? "CLEAN" : "ATTENTION", audit.Violations.Count, audit.UnknownClaimIds.Count);
                        }

                        // Decision Integrity Trace (Output Control / Final Audit): capture the per-claim
                        // authorizations from the audit so the cockpit can show ALLOW/QUALIFY/SUPPRESS/
                        // CORRECT dispositions and which prose was ACTUALLY transformed. ProseTransformed
                        // reflects the real applied set from the enforcement pass — never an assumption
                        // based on disposition alone.
                        if (governance.OutputAudit is { } outputAudit)
                        {
                            var claimsById = governance.InvolvedClaims.ToDictionary(c => c.ClaimId);
                            var ledgerById = governance.ClaimLedger.ToDictionary(c => c.OutputClaimId);
                            outputAuthorizations = outputAudit.Authorizations
                                .Select(a =>
                                {
                                    claimsById.TryGetValue(a.ClaimId, out var claim);
                                    ledgerById.TryGetValue(a.ClaimId, out var ledger);
                                    var verification = retrieval.Verifications
                                        .Where(v => claim?.SourceBranchId is null || v.DecisionBranchId == claim.SourceBranchId)
                                        .FirstOrDefault(v => v.IsDecisionAuthorized);
                                    return new Features.Intelligence.Decision.DecisionOutputAuthorizationDto(
                                        a.ClaimId, a.ClaimText,
                                        Features.Intelligence.Epistemic.ClaimCodes.ToCode(a.VerificationState),
                                        Features.Intelligence.Epistemic.ClaimCodes.ToCode(a.DecisionAuthority),
                                        Features.Intelligence.Epistemic.OutputClaimDispositions.ToCode(a.Disposition),
                                        a.IsForeign, a.Reason,
                                        ProseTransformed: appliedProseClaimIds.Contains(a.ClaimId))
                                    {
                                        SourceBranchId = claim?.SourceBranchId,
                                        SourceCandidateId = claim?.SourceCandidateId,
                                        DecisionEvidenceId = verification?.DecisionEvidenceId,
                                        DecisionEvidenceVerificationId = verification is null
                                            ? null
                                            : persistedVerificationsByEvidence.GetValueOrDefault(verification.DecisionEvidenceId)?.DecisionEvidenceVerificationId,
                                        SourceSnapshotId = verification?.SourceSnapshot?.SourceSnapshotId,
                                        PassageRef = verification?.SourceSnapshot?.PassageRef,
                                        IsMaterial = ledger?.IsMaterial ?? true,
                                        MappingState = ledger?.MappingState.ToString().ToUpperInvariant() ?? "MAPPED",
                                        SourcePropositionId = ledger?.SourcePropositionId ?? claim?.ClaimId,
                                        MappingReasonCode = ledger?.ReasonCode,
                                    };
                                })
                                .ToArray();
                            await repository.PersistOutputClaimProvenanceAsync(outputAuthorizations.Select(a =>
                                new DecisionOutputClaimProvenancePersistence(
                                    Guid.NewGuid(), sessionId, a.ClaimId, a.SourceBranchId, a.SourceCandidateId,
                                    a.DecisionEvidenceId, a.DecisionEvidenceAttachmentId,
                                    a.DecisionEvidenceVerificationId, a.SourceSnapshotId, a.PassageRef,
                                    a.ClaimText, a.IsMaterial, a.MappingState, a.SourcePropositionId,
                                    a.MappingReasonCode,
                                    a.Disposition, request.TenantId, request.UserId)).ToArray(), cancellationToken);
                            outputTransformSummary = new Features.Intelligence.Decision.DecisionOutputTransformSummaryDto(
                                proseRequired, proseApplied, postTransformClean);
                        }
                    }
                }
                catch (Exception epistemicEx)
                {
                    logger.LogWarning(epistemicEx, "EA-7 epistemic governance failed for session {SessionId}; decision unaffected.", sessionId);
                }

                // Verified Decision Signals (advisory): extract material support signals from the V2
                // graph, persist them, and project into domain-neutral branch deltas via the verified
                // signal service. Strictly non-blocking — it never changes the V1/V2 verdict.
                try
                {
                    var extractionContext = new Features.Intelligence.Epistemic.MaterialSignalExtractionContext
                    {
                        SessionId = sessionId,
                        TenantId = request.TenantId,
                        MatterId = request.MatterId,
                        ActorUserId = request.UserId,
                        ProposedByModel = route.ModelCode,
                        PromptRunId = request.CorrelationId,
                        Nodes = v2.Nodes.ToArray(),
                        Edges = v2.Edges.ToArray(),
                    };
                    var supportSignals = materialSignalExtractor.Extract(extractionContext);
                    foreach (var signal in supportSignals)
                    {
                        await decisionSupportSignalRepository.UpsertAsync(
                            new Features.Intelligence.Epistemic.DecisionSupportSignalPersistence(
                                signal.SignalId, signal.DecisionSessionId, signal.MatterId,
                                signal.Statement, signal.NormalizedStatement,
                                Features.Intelligence.Epistemic.DecisionSupportSignalCodes.ToCode(signal.Origin),
                                Features.Intelligence.Epistemic.DecisionSupportSignalCodes.ToCode(signal.VerificationState),
                                signal.RequiresVerification, signal.VerificationStrength, signal.DecisionImpact,
                                signal.SourceBranchId, signal.SourceCandidateId, signal.ProposedByModel,
                                signal.PromptRunId, signal.VerificationReason, request.TenantId, request.UserId),
                            cancellationToken);
                    }

                    var projected = verifiedSignalService.Project(supportSignals);
                    logger.LogInformation(
                        "Verified Decision Signals for session {SessionId}: {Extracted} signal(s) extracted, {Projected} branch delta(s) projected.",
                        sessionId, supportSignals.Count, projected.Count);

                    // ── B3 Hallucination Solver ─────────────────────────────────────────────────
                    // When the DB-backed toggle is ON, feed the projected verified-signal deltas into
                    // the authoritative Candidate × Branch recompetition engine so unsupported material
                    // propositions contribute zero positive support and the ranking recompetes on
                    // verified evidence only. POLOXI stays the sole scorer — the graph only supplies
                    // deltas. Strictly non-blocking and gated OFF by default to preserve ASPEN_B2.
                    // Non-destructive by design: the recompeted result is ALWAYS produced as a shadow
                    // "what-if" snapshot (SolverShadow) so the cockpit can show BOTH the original (B2)
                    // decision and the verified-evidence-only recompetition side by side. The DB-backed
                    // toggle decides which one is authoritative: ON = Enforced (recompeted result replaces
                    // the returned/persisted decision); OFF = Advisory (original stays authoritative, the
                    // shadow is annotation-only, reproducing ASPEN_B2).
                    if (projected.Count > 0)
                    {
                        var enforced = v2Settings.ApplyVerifiedSignalsToRanking;
                        var modeCode = enforced ? "Enforced" : "Advisory";
                        var b3Events = new List<DecisionEventPersistence>();
                        var b3Sequence = 0;
                        void RecordB3(string type, string stage, object? payload = null) =>
                            b3Events.Add(new DecisionEventPersistence(Guid.NewGuid(), null, b3Sequence++, type, stage,
                                payload is null ? null : JsonSerializer.Serialize(payload), null));

                        var materialCount = supportSignals.Count;
                        var verifiedCount = supportSignals.Count(s => s.VerificationState == Features.Intelligence.Epistemic.DecisionSupportVerificationState.Supported);
                        var contradictedCount = supportSignals.Count(s => s.VerificationState == Features.Intelligence.Epistemic.DecisionSupportVerificationState.Contradicted);
                        RecordB3("MATERIAL_SUPPORT_IDENTIFIED", "SOLVER", new { materialCount, projectedDeltaCount = projected.Count });
                        RecordB3("SUPPORT_VERIFIED", "SOLVER", new { verifiedCount, contradictedCount, unverifiedCount = materialCount - verifiedCount - contradictedCount });

                        // Loop-safety: only frontier branches may be reopened by the solver pass.
                        var reopenAllowed = new HashSet<Guid>(branches.Where(b => b.IsOnFrontier).Select(b => b.DecisionBranchId));

                        var recompete = Features.Intelligence.Decision.Core.DecisionRecompetition.Run(
                            candidates, branches, projected, settings, reopenAllowed);

                        RecordB3("VERIFIED_SIGNALS_APPLIED", "SOLVER", new
                        {
                            mode = modeCode,
                            appliedDeltaCount = projected.Count,
                            reopenedBranchCount = recompete.ReopenedBranchCount
                        });

                        // Recompute terminal state + winner from the recompeted result.
                        var b3FrontierOpen = recompete.Branches.Any(b => b.IsOnFrontier);
                        var b3MaxAdv = recompete.Branches.Count == 0 ? 0d : recompete.Branches.Max(b => (double)b.AdvScore);
                        var (b3Status, b3TerminalState, b3Reason) = ResolveTerminalState(settings, recompete.CurrentMargin, recompete.CurrentEntropy, b3FrontierOpen, b3MaxAdv);
                        var b3ReasonCode = recompete.WinnerChanged ? "WINNER_FLIP" : "SUPPORT_CHANGED";

                        // Always build the shadow "what-if" snapshot so both outcomes are visible.
                        solverShadow = new Features.Intelligence.Decision.DecisionSolverShadowDto(
                            modeCode, enforced, b3Status, b3TerminalState,
                            recompete.PreviousWinnerId, recompete.CurrentWinnerId, recompete.WinnerChanged,
                            (decimal)recompete.PreviousEntropy, (decimal)recompete.CurrentEntropy,
                            (decimal)recompete.PreviousMargin, (decimal)recompete.CurrentMargin,
                            projected.Count, recompete.ReopenedBranchCount,
                            recompete.Candidates.Select(c => new DecisionCandidateDto(c.DecisionCandidateId, c.CandidateCode, c.DisplayName, c.Outcome, c.LegalSupport, c.FactSupport, c.EvidenceSupport, c.AuthoritySupport, c.Verification, c.Uncertainty, c.Discrimination, c.RankingImpact, c.Diversity, c.RedundancyPenalty, c.CompositeScore, c.DecisionSupportCeiling, c.RankOrder, c.IsWinner, c.IsEliminated)).ToArray(),
                            recompete.Branches.Select(MapBranch).ToArray());

                        // The shadow snapshot is always built (above) so both rankings are visible.
                        // The authoritative Legal_DecisionRecompetition row is written ONLY in Enforced
                        // mode (below), because that table is the closed-loop source of truth that the
                        // read-back (GetLatestRecompetitionAsync -> LastRecompetition) projects on reload.
                        // Advisory mode must NOT write it, or a reload would show a closed-loop flip that
                        // never actually changed the returned decision. Advisory keeps its full audit via
                        // the SOLVER session events recorded here.
                        RecordB3("CANDIDATES_RECOMPETED", "SOLVER", new
                        {
                            mode = modeCode,
                            winnerChanged = recompete.WinnerChanged,
                            previousEntropy = recompete.PreviousEntropy,
                            currentEntropy = recompete.CurrentEntropy,
                            previousMargin = recompete.PreviousMargin,
                            currentMargin = recompete.CurrentMargin,
                            reasonCode = b3ReasonCode
                        });

                        if (enforced)
                        {
                            // Enforced mode: promote the recompeted result to the authoritative decision.
                            // Persist the recompeted state and refresh the in-memory response so DB and
                            // response never diverge (BuildResponse reads persistence/readiness/nextAction).
                            await repository.ReplaceCandidatesAsync(request.TenantId, request.UserId, sessionId, recompete.Candidates, cancellationToken);
                            await repository.ReplaceBranchesAsync(request.TenantId, request.UserId, sessionId, recompete.Branches, cancellationToken);
                            await repository.UpdateSessionOutcomeAsync(request.TenantId, request.UserId, sessionId, b3Status,
                                (decimal)recompete.CurrentEntropy, (decimal)recompete.CurrentMargin, recompete.CurrentWinnerId, cancellationToken);

                            await repository.PersistRecompetitionAsync(new Features.Intelligence.Decision.DecisionRecompetitionPersistence(
                                Guid.NewGuid(), sessionId, request.TenantId, request.UserId, null,
                                recompete.PreviousWinnerId, recompete.CurrentWinnerId, recompete.WinnerChanged,
                                (decimal)recompete.PreviousEntropy, (decimal)recompete.CurrentEntropy,
                                (decimal)recompete.PreviousMargin, (decimal)recompete.CurrentMargin, recompete.ReopenedBranchCount,
                                JsonSerializer.Serialize(candidates.Select(c => new { c.CandidateCode, c.RankOrder, c.CompositeScore })),
                                JsonSerializer.Serialize(recompete.Candidates.Select(c => new { c.CandidateCode, c.RankOrder, c.CompositeScore })),
                                b3ReasonCode), cancellationToken);

                            nextAction = BuildNextBestAction(recompete.Branches, b3Status);
                            readiness = BuildReadiness(recompete.Candidates, recompete.Branches, evidence, recompete.CurrentMargin, recompete.CurrentEntropy, b3Status, retrieval.ResearchStatus);
                            persistence = persistence with
                            {
                                StatusCode = b3Status,
                                TerminalStateCode = b3TerminalState,
                                TerminationReason = b3Reason,
                                WinnerCandidateId = recompete.CurrentWinnerId,
                                CandidateEntropy = (decimal)recompete.CurrentEntropy,
                                DecisionMargin = (decimal)recompete.CurrentMargin,
                                Candidates = recompete.Candidates,
                                Branches = recompete.Branches,
                                NextBestActionText = nextAction?.Title,
                                NextBestActionImpactCode = nextAction?.ImpactCode,
                                NextBestActionRationale = nextAction?.Rationale
                            };
                        }

                        await repository.AppendSessionEventsAsync(request.TenantId, request.UserId, sessionId, b3Events, cancellationToken);

                        logger.LogInformation(
                            "B3 solver ({Mode}) for session {SessionId}: applied {Applied} delta(s), winnerChanged={WinnerChanged}, entropy {PrevEntropy:F3}->{CurEntropy:F3}, margin {PrevMargin:F3}->{CurMargin:F3}.",
                            modeCode, sessionId, projected.Count, recompete.WinnerChanged,
                            recompete.PreviousEntropy, recompete.CurrentEntropy, recompete.PreviousMargin, recompete.CurrentMargin);
                    }
                }
                catch (Exception signalEx)
                {
                    logger.LogWarning(signalEx, "Verified Decision Signals failed for session {SessionId}; decision unaffected.", sessionId);
                }
            }
            catch (Exception ex)
            {
                // V2 is advisory; a graph/verifier failure must never block the V1 decision (§ optional).
                logger.LogWarning(ex, "V2 dependency graph failed for session {SessionId}; returning V1 result.", sessionId);
                v2 = null;
                graphDiagnostic = ex.Message;
            }
        }

        // ── POLOXI Bounded Research Loop (in-request) ───────────────────────────────────────────────
        // When the research-loop flag is ON and this run produced a dependency graph, drive the autonomous
        // Retrieval → Verification → Promotion → Recompetition loop in-request until an explicit STOP
        // condition. Default OFF preserves the shadow baseline; failures are advisory and never block the
        // decision. The read-back below reflects any state the loop committed to the session.
        // INVARIANT (§14): the eligibility snapshot is captured on EVERY path (including graph-off and a
        // settings-load fault), so a NotRun research stage can NEVER be generic — it always names the exact
        // blocking prerequisite. The loop runs only when the snapshot is Eligible.
        {
            // Frontier telemetry is authoritative from the competed branches, independent of loop settings.
            var frontierBranches = persistence.Branches
                .Where(b => b.IsOnFrontier)
                .ToList();
            var frontierCount = frontierBranches.Count;
            var highestFrontierIv = frontierCount == 0 ? 0m : frontierBranches.Max(b => b.InformationValue);

            if (!useGraph || v2 is null)
            {
                // Graph prerequisite missing — the loop cannot run without a dependency graph to research.
                researchEligibility = new Features.Intelligence.Decision.DecisionResearchEligibilityDto(
                    EnabledSetting: false, UseGraph: useGraph, V2Available: v2 is not null,
                    Eligible: false,
                    NotRunReason: !useGraph
                        ? Features.Intelligence.Decision.DecisionResearchNotRunReasons.GraphDisabled
                        : Features.Intelligence.Decision.DecisionResearchNotRunReasons.V2Unavailable,
                    FrontierCount: frontierCount, HighestFrontierInformationValue: highestFrontierIv);
            }
            else
            {
                // Load loop settings under their own guard so a settings-load fault yields an explicit
                // SETTINGS_UNAVAILABLE reason rather than a swallowed null eligibility.
                DecisionResearchLoopSettings? researchLoop = null;
                try
                {
                    researchLoop = await repository.GetResearchLoopSettingsAsync(cancellationToken);
                }
                catch (Exception settingsEx)
                {
                    logger.LogWarning(settingsEx, "Research loop settings load failed for session {SessionId}; loop skipped.", sessionId);
                }

                if (researchLoop is null)
                {
                    researchEligibility = new Features.Intelligence.Decision.DecisionResearchEligibilityDto(
                        EnabledSetting: false, UseGraph: true, V2Available: true, Eligible: false,
                        NotRunReason: Features.Intelligence.Decision.DecisionResearchNotRunReasons.SettingsUnavailable,
                        FrontierCount: frontierCount, HighestFrontierInformationValue: highestFrontierIv);
                }
                else
                {
                    var minFrontierIv = (decimal)researchLoop.MinFrontierInformationValue;
                    var retrievalBudget = researchLoop.MaxRetrievals > 0;

                    // Reason precedence: setting off → no frontier → frontier below threshold → eligible.
                    var loopReason = !researchLoop.Enabled
                        ? Features.Intelligence.Decision.DecisionResearchNotRunReasons.SettingDisabled
                        : frontierCount == 0
                            ? Features.Intelligence.Decision.DecisionResearchNotRunReasons.NoFrontier
                        : highestFrontierIv < minFrontierIv
                            ? Features.Intelligence.Decision.DecisionResearchNotRunReasons.FrontierBelowThreshold
                        : Features.Intelligence.Decision.DecisionResearchNotRunReasons.Eligible;

                    var loopEligible = loopReason == Features.Intelligence.Decision.DecisionResearchNotRunReasons.Eligible;

                    researchEligibility = new Features.Intelligence.Decision.DecisionResearchEligibilityDto(
                        EnabledSetting: researchLoop.Enabled, UseGraph: true, V2Available: true,
                        Eligible: loopEligible, NotRunReason: loopReason,
                        FrontierCount: frontierCount, HighestFrontierInformationValue: highestFrontierIv,
                        MinFrontierInformationValue: minFrontierIv, RetrievalBudget: retrievalBudget);

                    if (loopEligible)
                    {
                        try
                        {
                            var loopResult = await RunResearchLoopAsync(request.TenantId, request.UserId, sessionId, cancellationToken);
                            logger.LogInformation(
                                "Research loop (in-request) for session {SessionId}: {Rounds} round(s), {Retrievals} retrieval(s), stop={StopReason}.",
                                sessionId, loopResult.RoundsExecuted, loopResult.TotalRetrievals, loopResult.StopReason);
                            researchSummary = BuildResearchLoopSummary(loopResult);
                            // The loop committed authoritative state via ApplyVerificationChangeAsync; return
                            // the refreshed session so DB and response agree.
                            var refreshed = await GetSessionResultAsync(request.TenantId, sessionId, cancellationToken);
                            if (refreshed is not null)
                                return DecorateTrace(refreshed, proposalIntegritySummary, researchSummary,
                                    researchEligibility, outputAuthorizations, outputTransformSummary,
                                    outputClaimExtraction, timer.ElapsedMilliseconds);
                        }
                        catch (Exception loopEx)
                        {
                            logger.LogWarning(loopEx, "Research loop (in-request) failed for session {SessionId}; decision unaffected.", sessionId);
                            // The invocation itself is authoritative execution evidence: project a FAILED
                            // research stage instead of leaving the summary null (which the trace could only
                            // report as an observability gap, never as INVOKED).
                            researchSummary = new Features.Intelligence.Decision.DecisionResearchLoopSummaryDto(
                                Enabled: true, RoundsExecuted: 0, RoundsCommitted: 0, RoundsRolledBack: 0,
                                TotalRetrievals: 0, StopReason: DecisionResearchLoopStopReasons.RoundFailed,
                                Rounds: [], Failure: new DecisionResearchFailureDto(
                                    RoundNumber: 0, Stage: "LOOP_INVOCATION",
                                    ExceptionType: loopEx.GetType().Name, Reason: loopEx.Message,
                                    AuthoritativeStateChanged: false));
                        }
                    }
                }
            }
        }

        var responseWithVerifications = await HydrateEvidenceVerificationsAsync(
            BuildResponse(persistence, nextAction, readiness, useGraph, v2, governanceVerdict, graphDiagnostic, solverShadow),
            request.TenantId, sessionId, cancellationToken);
        return await HydrateVerifiedSignalsAsync(
            DecorateTrace(
                responseWithVerifications,
                proposalIntegritySummary, researchSummary, researchEligibility, outputAuthorizations,
                outputTransformSummary, outputClaimExtraction, timer.ElapsedMilliseconds),
            request.TenantId, sessionId, cancellationToken);
    }

    // ── Decision Integrity Trace assembly ────────────────────────────────────────────────────────
    // Fold the live-run trace inputs onto a response and project the DecisionIntegrityTrace. This is a
    // pure, deterministic projection of authoritative state — it never changes the verdict fields.
    private static DecisionSearchResponse DecorateTrace(
        DecisionSearchResponse response,
        Features.Intelligence.Decision.DecisionProposalIntegritySummaryDto? proposalIntegrity,
        Features.Intelligence.Decision.DecisionResearchLoopSummaryDto? researchSummary,
        Features.Intelligence.Decision.DecisionResearchEligibilityDto? researchEligibility,
        IReadOnlyCollection<Features.Intelligence.Decision.DecisionOutputAuthorizationDto> outputAuthorizations,
        Features.Intelligence.Decision.DecisionOutputTransformSummaryDto? outputTransformSummary,
        Features.Intelligence.Decision.DecisionOutputClaimExtractionDto? outputClaimExtraction,
        long totalDurationMs)
    {
        // Decision state version: baseline 1 + one increment per committed authoritative mutation. A
        // recompetition and each committed research round are authoritative mutations; a rolled-back
        // round is deliberately NOT counted, so a fault preserves the prior version.
        var version = 1L;
        if (response.LastRecompetition is not null)
            version += 1;
        if (researchSummary is not null)
            version += researchSummary.RoundsCommitted;

        var phaseTimings = BuildPhaseTimings(response, totalDurationMs);

        var enriched = response with
        {
            ProposalIntegrity = proposalIntegrity,
            ResearchSummary = researchSummary,
            ResearchEligibility = researchEligibility,
            OutputAuthorizations = outputAuthorizations,
            OutputTransformSummary = outputTransformSummary,
            OutputClaimExtraction = outputClaimExtraction,
            DecisionStateVersion = version,
            PhaseTimings = phaseTimings,
        };

        return enriched with
        {
            IntegrityTrace = Features.Intelligence.Decision.DecisionIntegrityTraceProjector.Project(enriched),
        };
    }

    // Read-back decoration: project the trace from PERSISTED state only. The live-run-only inputs
    // (proposal-integrity gate summary, in-request research-loop audit) are not persisted, so they are
    // null/empty here and their stages report NotRun — but every persisted stage (Verification,
    // Promotion, Propagation, Recompetition, Frontier, Output Control, Final Audit) still projects, so
    // a reloaded or closed-loop-updated session shows the same trace panel as the live run.
    private static DecisionSearchResponse DecorateTraceReadback(DecisionSearchResponse response) =>
        DecorateTrace(response, proposalIntegrity: null, researchSummary: null,
            researchEligibility: null, outputAuthorizations: [], outputTransformSummary: null,
            outputClaimExtraction: null, totalDurationMs: response.DurationMilliseconds);

    // Derive per-phase timings from the recorded session-stage events when available, otherwise report
    // only the measured total. Purely a Diagnostics surface — never gates anything.
    private static IReadOnlyCollection<Features.Intelligence.Decision.DecisionPhaseTimingDto> BuildPhaseTimings(
        DecisionSearchResponse response, long totalDurationMs)
    {
        var timings = new List<Features.Intelligence.Decision.DecisionPhaseTimingDto>
        {
            new("Total", totalDurationMs > 0 ? totalDurationMs : response.DurationMilliseconds),
        };
        return timings;
    }

    // Summarize a research-loop result for the Research stage: committed vs rolled-back rounds are
    // derived from the per-round audit (a round whose narrative recorded a rollback is not committed).
    private static Features.Intelligence.Decision.DecisionResearchLoopSummaryDto BuildResearchLoopSummary(
        DecisionResearchLoopResultDto loopResult)
    {
        var rolledBack = string.Equals(loopResult.StopReason,
            DecisionResearchLoopStopReasons.RoundFailed, StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var committed = Math.Max(0, loopResult.RoundsExecuted - rolledBack);
        return new Features.Intelligence.Decision.DecisionResearchLoopSummaryDto(
            loopResult.Enabled, loopResult.RoundsExecuted, committed, rolledBack,
            loopResult.TotalRetrievals, loopResult.StopReason, loopResult.Rounds, loopResult.Failure);
    }


    // ── Direct LLM answer path (POLOXI Engine off) ──
    private async Task<Features.Intelligence.Decision.DecisionGovernanceVerdictDto?> PersistGovernanceVerdictAsync(
        DecisionSearchRequest request,
        Guid sessionId,
        DecisionV2Result v2,
        Features.Intelligence.Epistemic.EpistemicGovernanceResult governance,
        CancellationToken cancellationToken)
    {
        var v2Ready = v2.ReadinessVerdict?.Satisfied ?? true;
        var eaReady = governance.EaReady;
        var outputClean = governance.OutputAudit?.IsClean ?? true;
        // Effective readiness is the V2 verdict unless a stronger mode actually downgraded it.
        var effectiveReady = governance.OverrideApplied ? false : v2Ready;

        var blockers = governance.Readiness?.Blockers.Select(b => b.Reason).ToArray() ?? [];
        var violations = governance.OutputAudit?.Violations.Select(v => v.Reason).ToArray() ?? [];
        var claims = governance.InvolvedClaims
            .Select(c => new Features.Intelligence.Decision.GovernanceClaimDto(
                c.ClaimId, c.Text,
                Features.Intelligence.Epistemic.ClaimCodes.ToCode(c.VerificationState),
                Features.Intelligence.Epistemic.ClaimCodes.ToCode(c.DecisionAuthority),
                c.IsAuthorized, c.IsEssential, c.Annotation))
            .ToArray();
        var narrative = governance.AuditNarrative.ToArray();
        var modeCode = Features.Intelligence.Epistemic.EpistemicOverrideModes.ToCode(governance.OverrideMode);

        var persistence = new Abstractions.Persistence.DecisionGovernanceVerdictPersistence(
            Guid.NewGuid(), sessionId, request.MatterId, modeCode,
            v2Ready, eaReady, governance.Readiness?.Enforced ?? false, outputClean, effectiveReady,
            governance.OverrideApplied, governance.ProjectedClaimCount, governance.AuthorizedClaimCount,
            blockers.Length, violations.Length,
            JsonSerializer.Serialize(blockers), JsonSerializer.Serialize(violations),
            JsonSerializer.Serialize(claims), JsonSerializer.Serialize(narrative),
            request.TenantId, request.UserId);

        await governanceRepository.UpsertVerdictAsync(persistence, cancellationToken);

        return new Features.Intelligence.Decision.DecisionGovernanceVerdictDto(
            modeCode, v2Ready, eaReady, outputClean, effectiveReady, governance.OverrideApplied,
            governance.ProjectedClaimCount, governance.AuthorizedClaimCount,
            blockers, violations, claims, narrative);
    }

    private async Task<DecisionSearchResponse> ComposeDirectAnswerAsync(
        DecisionSearchRequest request,
        Guid sessionId,
        string contextCode,
        string effectiveQuery,
        DecisionClarificationPersistence? clarification,
        List<DecisionEventPersistence> events,
        Stopwatch timer,
        string modeCode,
        string? routeModelCode,
        CancellationToken cancellationToken)
    {
        var route = await ResolveRouteAsync(AnswerPromptCode, routeModelCode, cancellationToken);
        var answerPrompt = await repository.GetPromptAsync(AnswerPromptCode, cancellationToken)
            ?? throw new InvalidOperationException($"The '{AnswerPromptCode}' decision prompt is not configured.");
        var user = answerPrompt.UserPromptTemplate.Replace("{{ARTIFACT}}", "{}").Replace("{{QUERY}}", effectiveQuery);
        var result = await aiProvider.GenerateAsync(new DecisionAiRequest(route, "DECISION_ANSWER", answerPrompt.SystemPrompt, user, null, request.CorrelationId), cancellationToken);
        timer.Stop();
        var persistence = new DecisionSessionPersistence(
            sessionId, request.TenantId, request.UserId, request.Query, contextCode, route.ModelCode, false,
            DecisionStatusCodes.DecisionReady, DecisionStatusCodes.DecisionReady, "LLM_ONLY", null,
            0, 0, 0, 0, 1, timer.ElapsedMilliseconds, result.Content, null, null, request.CorrelationId,
            [], [], [], [], events)
        {
            MatterId = request.MatterId,
            CounterfactualAssumption = request.CounterfactualAssumption,
            ParentDecisionSessionId = clarification?.ParentDecisionSessionId,
            MatterJurisdiction = NormalizeAuthorityContext(request.Jurisdiction),
            GoverningLaw = NormalizeAuthorityContext(request.GoverningLaw),
            CourtOrForum = NormalizeAuthorityContext(request.CourtOrForum),
            AuthorityCutoffDate = request.AuthorityCutoffDate,
            ModeCode = modeCode,
        };
        await repository.PersistSessionAsync(persistence, cancellationToken);
        if (clarification is not null)
            await repository.PersistClarificationAsync(clarification, cancellationToken);
        return BuildResponse(persistence, null, []);
    }

    // Deterministic Decision-Contract completeness test used by the additive preflight gate. Returns
    // true (with an actionable target/question) ONLY when an ESSENTIAL input is missing per settings.
    // Conservative by design: today the single essential-input rule is "no explicit procedural relief/
    // instruction" (both Posture and MotionTarget empty). When RequireFactsWhenProcedureMissing is on,
    // the gate additionally requires the query to be fact-thin, so a hypothetical fact-bearing question
    // is never blocked.
    internal static bool TryBuildPreflightClarification(
        DecisionPreflightSettings preflight,
        DecisionSearchRequest request,
        string effectiveQuery,
        out string clarificationTarget,
        out string clarificationQuestion)
    {
        clarificationTarget = string.Empty;
        clarificationQuestion = string.Empty;

        if (preflight.RequireProceduralInstruction)
        {
            var hasProceduralInstruction = !string.IsNullOrWhiteSpace(request.Posture)
                || !string.IsNullOrWhiteSpace(request.MotionTarget);
            if (!hasProceduralInstruction)
            {
                var factsSupplied = (effectiveQuery?.Trim().Length ?? 0) >= preflight.MinimumFactsQueryLength;
                // Only gate when facts are also thin (unless the tenant opted to gate on procedure alone).
                if (!preflight.RequireFactsWhenProcedureMissing || !factsSupplied)
                {
                    clarificationTarget = "Requested procedural relief / decision instruction";
                    clarificationQuestion =
                        "Before this analysis runs, what decision or procedural relief should it resolve? "
                        + "Please identify the specific motion, procedural vehicle, and the party who bears the "
                        + "burden (for example, \u201Cwhether to grant defendant\u2019s motion for summary judgment\u201D). "
                        + "This defines the decision contract: without it the analysis cannot frame the competing "
                        + "outcomes or the applicable burden, so any leading outcome would be conditional rather than established.";
                    return true;
                }
            }
        }

        return false;
    }

    // Lightweight short-circuit return for the preflight gate: persists a USER_CLARIFICATION_REQUIRED
    // session carrying the actionable question WITHOUT running discovery/graph/verification, so the run
    // does not pay for a decision that cannot become ready. Mirrors ComposeDirectAnswerAsync's shape.
    private async Task<DecisionSearchResponse> ComposePreflightClarificationAsync(
        DecisionSearchRequest request,
        Guid sessionId,
        string contextCode,
        DecisionClarificationPersistence? clarification,
        string clarificationTarget,
        string clarificationQuestion,
        List<DecisionEventPersistence> events,
        Stopwatch timer,
        string modeCode,
        CancellationToken cancellationToken)
    {
        timer.Stop();
        var nextAction = new DecisionNextActionDto(
            $"Provide the missing information: {clarificationTarget}",
            "VERY HIGH",
            "The decision contract is incomplete: an essential instruction is missing that only you can supply. "
            + "Providing it lets the analysis frame the competing outcomes and the applicable burden before any "
            + "expensive research runs. This is a clarification need, not a researchable legal question.",
            null, 0m, 0m);
        var persistence = new DecisionSessionPersistence(
            sessionId, request.TenantId, request.UserId, request.Query, contextCode, null, true,
            DecisionStatusCodes.UserClarificationRequired, DecisionStatusCodes.UserClarificationRequired,
            "ESSENTIAL_DECISION_CONTRACT_INPUT_MISSING", null,
            0, 0, 0, 0, 0, timer.ElapsedMilliseconds,
            null, clarificationQuestion, clarificationTarget, request.CorrelationId,
            [], [], [], [], events)
        {
            MatterId = request.MatterId,
            NextBestActionText = nextAction.Title,
            NextBestActionImpactCode = nextAction.ImpactCode,
            NextBestActionRationale = nextAction.Rationale,
            CounterfactualAssumption = request.CounterfactualAssumption,
            ResearchStatusCode = DecisionResearchStates.NotNeeded,
            ParentDecisionSessionId = clarification?.ParentDecisionSessionId,
            MatterJurisdiction = NormalizeAuthorityContext(request.Jurisdiction),
            GoverningLaw = NormalizeAuthorityContext(request.GoverningLaw),
            CourtOrForum = NormalizeAuthorityContext(request.CourtOrForum),
            AuthorityCutoffDate = request.AuthorityCutoffDate,
            ModeCode = modeCode,
        };
        await repository.PersistSessionAsync(persistence, cancellationToken);
        if (clarification is not null)
            await repository.PersistClarificationAsync(clarification, cancellationToken);
        return BuildResponse(persistence, nextAction, []);
    }

    // ── Read-back: project a persisted session (and its V2 graph, if any) into a response ──────────
    // Lets the cockpit open a saved/seeded session (e.g. the 0215 vertical slice) without re-running
    // the LLM. The V2 fields are populated only when a persisted dependency graph exists.
    public async Task<DecisionSearchResponse?> GetSessionResultAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
    {
        var session = await repository.GetSessionAsync(tenantId, decisionSessionId, cancellationToken);
        if (session is null)
            return null;

        // Rehydrate Next Best Action metrics from the authoritative current frontier. The persisted
        // session stores its display text/rationale but not branch id, IV, or flip potential; emitting
        // zeroes here made the cockpit disagree with the branch table and persisted ResearchNeed.
        var currentFrontierTarget = session.Branches
            .Where(b => b.IsOnFrontier || string.Equals(b.BranchStateCode, DecisionBranchStates.Active, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(b => b.InformationValue)
            .ThenByDescending(b => b.FlipPotential)
            .FirstOrDefault();
        var nextAction = session.NextBestActionText is null ? null : new DecisionNextActionDto(
            session.NextBestActionText, session.NextBestActionImpactCode ?? "MEDIUM",
            session.NextBestActionRationale ?? string.Empty, currentFrontierTarget?.DecisionBranchId,
            currentFrontierTarget?.InformationValue ?? 0m, currentFrontierTarget?.FlipPotential ?? 0m);
        // Research status is persisted (0262); use it directly so rehydrated readiness reports the same
        // "why" (RETRIEVAL_FAILED vs SEARCH_NO_RESULTS vs RETRIEVED vs NOT_NEEDED) as the live run.
        // Older sessions predating the column fall back to a best-effort status derived from evidence.
        var rehydratedResearchStatus = session.ResearchStatusCode
            ?? (session.Evidence.Count > 0
                ? DecisionResearchStates.Retrieved
                : DecisionResearchStates.SearchNoResults);
        var readiness = BuildReadiness(
            session.Candidates, session.Branches, session.Evidence,
            (double)session.DecisionMargin, (double)session.CandidateEntropy, session.StatusCode, rehydratedResearchStatus);

        var graph = await repository.GetGraphAsync(tenantId, decisionSessionId, cancellationToken);
        if (graph is null)
            return DecorateTraceReadback(await HydrateVerifiedSignalsAsync(
                await HydrateEvidenceVerificationsAsync(
                    await HydrateClosedLoopAsync(BuildResponse(session, nextAction, readiness), tenantId, decisionSessionId, cancellationToken),
                    tenantId, decisionSessionId, cancellationToken),
                tenantId, decisionSessionId, cancellationToken));

        var nodeDtos = graph.Nodes
            .Select(n => new DecisionGraphNodeDto(n.NodeId, n.NodeKind, n.NodeCode, n.DisplayName, n.Statement, n.Support, n.IsEssential, n.IsSatisfied, n.VerificationStatus, n.SortOrder))
            .ToArray();
        var edgeDtos = graph.Edges
            .Select(e => new DecisionGraphEdgeDto(e.EdgeId, e.RelationCode, e.SourceNodeKind, e.SourceNodeId, e.TargetNodeKind, e.TargetNodeId, e.SupportWeight, e.Materiality, e.IsEssential, e.IsDispositive, e.VerificationStatus, e.VerificationNotes, e.PropagatedStateCode))
            .ToArray();

        DecisionLosingSideTestDto? losingDto = graph.LosingSideTest is { } l
            ? new DecisionLosingSideTestDto(l.ChallengerCandidateId, l.StrongestCaseSummary, l.ChallengerStrength, l.WinnerStrength, l.WinnerSurvived)
            : null;

        var blockers = string.IsNullOrWhiteSpace(graph.ReadinessBlockersJson)
            ? Array.Empty<string>()
            : (JsonSerializer.Deserialize<string[]>(graph.ReadinessBlockersJson) ?? []);
        var predicate = blockers.Length == 0
            ? new[] { new DecisionReadinessItemDto("Dependency-constrained readiness", graph.ReadinessSatisfied, graph.ReadinessSatisfied ? "All essential dependencies satisfied and verified." : null) }
            : blockers.Select(b => new DecisionReadinessItemDto(b, false, null)).ToArray();
        var verdict = new DecisionReadinessVerdictDto(graph.ReadinessSatisfied, blockers, predicate);

        var v2 = new DecisionV2Result(graph, nodeDtos, edgeDtos, losingDto, verdict);
        return DecorateTraceReadback(await HydrateVerifiedSignalsAsync(
            await HydrateEvidenceVerificationsAsync(
                await HydrateClosedLoopAsync(BuildResponse(session, nextAction, readiness, usedDependencyGraph: true, v2), tenantId, decisionSessionId, cancellationToken),
                tenantId, decisionSessionId, cancellationToken),
            tenantId, decisionSessionId, cancellationToken));
    }

    private async Task<DecisionSearchResponse> HydrateEvidenceVerificationsAsync(
        DecisionSearchResponse response, Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken)
    {
        try
        {
            var verifications = await repository.GetEvidenceVerificationsAsync(tenantId, decisionSessionId, cancellationToken);
            if (verifications.Count == 0)
                return response;

            var dtos = verifications.Select(v => new DecisionEvidenceVerificationDto(
                v.DecisionEvidenceVerificationId,
                v.DecisionEvidenceId,
                v.DecisionBranchId,
                v.SourceTypeCode,
                v.ProfileCode,
                v.DispositionCode,
                v.IsVerified,
                v.IsDecisionAuthorized,
                string.IsNullOrWhiteSpace(v.BlockingReasonsJson)
                    ? []
                    : JsonSerializer.Deserialize<string[]>(v.BlockingReasonsJson) ?? [],
                v.EvaluatedDateUtc,
                v.Factors.Select(f => new DecisionEvidenceVerificationFactorDto(
                    f.FactorCode, f.StateCode, f.ReasonCode, f.Reason, f.VerifiedValue,
                    f.SourceRef, f.SupportingPassage, f.VerificationMethod)).ToArray())
            {
                MechanicalVerificationCount = v.MechanicalVerificationCount,
                SemanticVerificationCount = v.SemanticVerificationCount,
                PoloxiDeepeningCount = v.PoloxiDeepeningCount,
                CacheHitCount = v.CacheHitCount,
                InputTokenCount = v.InputTokenCount,
                OutputTokenCount = v.OutputTokenCount,
                LatencyMilliseconds = v.LatencyMilliseconds,
            }).ToArray();

            return response with { EvidenceVerifications = dtos };
        }
        catch (Exception verificationEx)
        {
            logger.LogWarning(verificationEx,
                "Evidence verification readback failed for session {SessionId}; decision unaffected.", decisionSessionId);
            return response;
        }
    }

    // Hydrate the persisted V2.1 closed-loop readback (last recompetition + latest open research
    // need) so a page reload or an idempotent replay shows the same closed-loop state.
    private async Task<DecisionSearchResponse> HydrateClosedLoopAsync(
        DecisionSearchResponse response, Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken)
    {
        var recompetition = await repository.GetLatestRecompetitionAsync(tenantId, decisionSessionId, cancellationToken);
        var researchNeed = await repository.GetLatestOpenResearchNeedAsync(tenantId, decisionSessionId, cancellationToken);
        if (recompetition is null && researchNeed is null)
            return response;

        var recompetitionDto = recompetition is null ? null : new DecisionRecompetitionDto(
            recompetition.DecisionRecompetitionId, recompetition.PreviousWinnerCandidateId, recompetition.CurrentWinnerCandidateId,
            recompetition.WinnerChanged, recompetition.PreviousEntropy, recompetition.CurrentEntropy,
            recompetition.PreviousMargin, recompetition.CurrentMargin, recompetition.ReopenedBranchCount,
            recompetition.ReasonCode, DateTime.UtcNow);

        var researchNeedDto = researchNeed is null ? null : new DecisionResearchNeedDto(
            researchNeed.DecisionResearchNeedId, researchNeed.DecisionBranchId, researchNeed.IssueLabel, researchNeed.PropositionToResolve,
            researchNeed.AuthorityKind, researchNeed.RequiredEvidenceKind, researchNeed.WhyDecisionRelevant, researchNeed.ExpectedDiscrimination,
            researchNeed.CurrentUncertainty, researchNeed.InformationValue, researchNeed.FalsificationCondition, researchNeed.StatusCode)
        {
            ResearchNeedTypeCode = researchNeed.ResearchNeedTypeCode,
        };

        return response with { LastRecompetition = recompetitionDto, PendingResearchNeed = researchNeedDto };
    }

    // Hydrate the advisory Verified Decision Signals for a session (read-only). Non-blocking: any
    // failure returns the response unchanged so the decision surface is never affected.
    private async Task<DecisionSearchResponse> HydrateVerifiedSignalsAsync(
        DecisionSearchResponse response, Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken)
    {
        try
        {
            var signals = await decisionSupportSignalRepository.GetBySessionAsync(decisionSessionId, tenantId, cancellationToken);
            if (signals.Count == 0)
                return response;

            var dtos = signals
                .Select(s => new DecisionSupportSignalDto(
                    s.SignalId, s.Statement, s.OriginCode, s.VerificationStateCode, s.RequiresVerification,
                    s.VerificationStrength, s.DecisionImpact, s.SourceBranchId, s.SourceCandidateId, s.VerificationReason))
                .ToArray();

            return response with { VerifiedSignals = dtos };
        }
        catch (Exception signalEx)
        {
            logger.LogWarning(signalEx, "Verified Decision Signals readback failed for session {SessionId}; decision unaffected.", decisionSessionId);
            return response;
        }
    }

    // ── POLOXI Legal V2.1 — synchronous closed loop
    // verification change → dependency propagation → domain-neutral signals → Candidate×Branch
    // recompetition → frontier/IV recalculation → ResearchNeed → readiness/audit. POLOXI stays the
    // sole scorer: the graph only supplies signals; DecisionRecompetition + DecisionCoreMath re-rank.
    public async Task<DecisionClosedLoopResultDto> ApplyVerificationChangeAsync(
        Guid tenantId, Guid userId, Guid decisionSessionId, DecisionVerificationChangeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await repository.GetSessionAsync(tenantId, decisionSessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Decision session {decisionSessionId} was not found.");

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"{request.EdgeId:N}:{request.NewStatus}"
            : request.IdempotencyKey!;

        // Idempotency: a retried event must not run the loop twice (§36).
        var existing = await repository.GetDependencyEventAsync(tenantId, decisionSessionId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            var already = await GetSessionResultAsync(tenantId, decisionSessionId, cancellationToken)
                ?? throw new InvalidOperationException("Session result unavailable after idempotent replay.");
            return new DecisionClosedLoopResultDto(
                decisionSessionId, Applied: false, AlreadyProcessed: true,
                DependencyImpact.Empty, already.LastRecompetition, already.PendingResearchNeed, already,
                ["This verification change was already processed; returning the existing decision state."]);
        }

        var v21 = await repository.GetV21SettingsAsync(cancellationToken);
        var v2Settings = await repository.GetV2SettingsAsync(cancellationToken);
        var coreSettings = await repository.GetCoreSettingsAsync(cancellationToken);

        var graph = await repository.GetGraphAsync(tenantId, decisionSessionId, cancellationToken)
            ?? throw new InvalidOperationException("This session has no dependency graph; the closed loop requires V2 graph data.");

        var audit = new List<string>();

        // 1) Deterministic dependency propagation (graph produces impact only).
        var outcome = v21.UseDependencyPropagation
            ? propagationService.Apply(graph, request.EdgeId, request.NewStatus, v2Settings.PropagationMaxDepth)
            : new DependencyPropagationOutcome(DependencyImpact.Empty, DependencyPropagationService.BuildModel(graph), null, EdgeFound: true);

        if (!outcome.EdgeFound)
            throw new InvalidOperationException($"Edge {request.EdgeId} was not found in the session graph.");

        var impact = outcome.Impact;
        audit.Add($"Edge {request.EdgeId} verification changed {outcome.PreviousStatus ?? "UNKNOWN"} → {request.NewStatus}; {impact.AffectedBranchIds.Count} branch(es) and {impact.AffectedCandidateIds.Count} candidate(s) affected.");

        // 2) Persist the edge verification change (authoritative graph state).
        var changedEdge = graph.Edges.First(e => e.EdgeId == request.EdgeId);
        await repository.UpdateEdgeVerificationAsync(tenantId, userId, decisionSessionId,
            [changedEdge with { VerificationStatus = request.NewStatus, VerificationNotes = request.Notes }], cancellationToken);

        // 3) Persist the dependency event (idempotency + audit substrate).
        var dependencyEventId = Guid.NewGuid();
        await repository.PersistDependencyEventAsync(new DecisionDependencyEventPersistence(
            dependencyEventId, decisionSessionId, tenantId, userId, session.MatterId, request.EdgeId, idempotencyKey,
            outcome.PreviousStatus, request.NewStatus, JsonSerializer.Serialize(impact),
            impact.AffectedBranchIds.Count, impact.AffectedCandidateIds.Count, impact.RecompetitionRequired), cancellationToken);

        DecisionRecompetitionDto? recompetitionDto = null;
        DecisionResearchNeedDto? researchNeedDto = null;

        // 4) Candidate×Branch recompetition — POLOXI re-scores (only when enabled and requested).
        if (request.RunClosedLoop && v21.UseGraphDrivenRecompetition && impact.RecompetitionRequired)
        {
            var signals = impactMapper.Map(impact, graph, session.Branches.ToList(), session.Candidates.ToList());
            if (signals.Count > 0)
            {
                // Loop-safety: only branches under the reopen cap may be reopened this session.
                var reopenAllowed = new HashSet<Guid>();
                foreach (var bid in signals.Where(s => s.ReopenRequested && s.BranchId is not null).Select(s => s.BranchId!.Value).Distinct())
                {
                    var reopens = await repository.CountBranchReopensAsync(tenantId, decisionSessionId, bid, cancellationToken);
                    if (reopens < v21.LoopMaxReopensPerBranch)
                        reopenAllowed.Add(bid);
                }

                var result = DecisionRecompetition.Run(
                    session.Candidates.ToList(), session.Branches.ToList(), signals, coreSettings, reopenAllowed);

                var infoGain = Math.Abs(result.CurrentEntropy - result.PreviousEntropy);
                audit.Add($"Recompetition: entropy {result.PreviousEntropy:F3}→{result.CurrentEntropy:F3}, margin {result.PreviousMargin:F3}→{result.CurrentMargin:F3}, {result.ReopenedBranchCount} branch(es) reopened.");
                if (result.WinnerChanged)
                    audit.Add("Leadership flip: the dependency change overturned the previous winning candidate.");
                if (infoGain < v21.LoopNoInformationGainEpsilon && !result.WinnerChanged)
                    audit.Add("No material information gain from this recompetition (below epsilon).");

                // Persist re-ranked candidates + branch/frontier state (POLOXI authoritative state).
                await repository.ReplaceCandidatesAsync(tenantId, userId, decisionSessionId, result.Candidates, cancellationToken);
                await repository.ReplaceBranchesAsync(tenantId, userId, decisionSessionId, result.Branches, cancellationToken);
                await repository.UpdateSessionOutcomeAsync(tenantId, userId, decisionSessionId, session.StatusCode,
                    (decimal)result.CurrentEntropy, (decimal)result.CurrentMargin, result.CurrentWinnerId, cancellationToken);

                var recompetitionId = Guid.NewGuid();
                var reasonCode = result.WinnerChanged ? "WINNER_FLIP" : "SUPPORT_CHANGED";
                await repository.PersistRecompetitionAsync(new DecisionRecompetitionPersistence(
                    recompetitionId, decisionSessionId, tenantId, userId, dependencyEventId,
                    result.PreviousWinnerId, result.CurrentWinnerId, result.WinnerChanged,
                    (decimal)result.PreviousEntropy, (decimal)result.CurrentEntropy,
                    (decimal)result.PreviousMargin, (decimal)result.CurrentMargin, result.ReopenedBranchCount,
                    JsonSerializer.Serialize(session.Candidates.Select(c => new { c.CandidateCode, c.RankOrder, c.CompositeScore })),
                    JsonSerializer.Serialize(result.Candidates.Select(c => new { c.CandidateCode, c.RankOrder, c.CompositeScore })),
                    reasonCode), cancellationToken);

                recompetitionDto = new DecisionRecompetitionDto(
                    recompetitionId, result.PreviousWinnerId, result.CurrentWinnerId, result.WinnerChanged,
                    (decimal)result.PreviousEntropy, (decimal)result.CurrentEntropy,
                    (decimal)result.PreviousMargin, (decimal)result.CurrentMargin, result.ReopenedBranchCount,
                    reasonCode, DateTime.UtcNow);

                // 5) Frontier / Information-Value snapshot (when graph frontier signals are enabled).
                if (v21.UseGraphFrontierSignals)
                {
                    var openFrontier = result.Branches.Where(b => b.IsOnFrontier).ToList();
                    var top = openFrontier.OrderByDescending(b => b.InformationValue).FirstOrDefault();
                    await repository.PersistFrontierSnapshotAsync(new DecisionFrontierSnapshotPersistence(
                        Guid.NewGuid(), decisionSessionId, tenantId, userId, recompetitionId,
                        (decimal)result.CurrentEntropy, (decimal)result.CurrentMargin, openFrontier.Count,
                        top?.DecisionBranchId, top?.InformationValue ?? 0m,
                        JsonSerializer.Serialize(openFrontier.Select(b => new { b.BranchCode, b.InformationValue, b.FlipPotential }))), cancellationToken);
                    audit.Add($"Frontier recalculated: {openFrontier.Count} open branch(es) remain.");
                }

                // 6) Outcome-directed ResearchNeed from the highest-IV frontier (bounded per session).
                var researchCount = await repository.CountResearchNeedsAsync(tenantId, decisionSessionId, cancellationToken);
                if (researchCount < v21.LoopMaxResearchActions)
                {
                    var frontierNeed = DecisionResearchNeedFactory.Create(
                        result.Branches, impact, decisionSessionId, tenantId, userId, session.MatterId, dependencyEventId);
                    if (frontierNeed is not null)
                    {
                        var targetBranch = result.Branches.First(branch => branch.DecisionBranchId == frontierNeed.DecisionBranchId);
                        var semanticNeed = await GenerateResearchNeedAsync(
                            session with { Candidates = result.Candidates, Branches = result.Branches },
                            targetBranch,
                            frontierNeed,
                            cancellationToken);
                        var need = semanticNeed.SelectedNeed;
                        if (need is null)
                        {
                            audit.Add($"Research semantic proposal unresolved; retrieval was not authorized: {semanticNeed.Reason}");
                        }
                        else
                        {
                            foreach (var plannedNeed in semanticNeed.Needs)
                                await repository.PersistResearchNeedAsync(plannedNeed, cancellationToken);
                            researchNeedDto = new DecisionResearchNeedDto(
                                need.DecisionResearchNeedId, need.DecisionBranchId, need.IssueLabel, need.PropositionToResolve,
                                need.AuthorityKind, need.RequiredEvidenceKind, need.WhyDecisionRelevant, need.ExpectedDiscrimination,
                                need.CurrentUncertainty, need.InformationValue, need.FalsificationCondition, need.StatusCode)
                            {
                                ResearchNeedTypeCode = need.ResearchNeedTypeCode,
                            };
                            audit.Add($"Next investigation selected: {need.IssueLabel}.");
                        }
                    }
                }
                else
                {
                    audit.Add("Research action budget for this session is exhausted; no new ResearchNeed generated.");
                }
            }
            else
            {
                audit.Add("Impact produced no signals that map to authoritative branches/candidates; no recompetition run.");
            }
        }
        else if (!impact.RecompetitionRequired)
        {
            audit.Add("Dependency change did not require a recompetition (no essential dependency crossed a threshold).");
        }

        var decision = await GetSessionResultAsync(tenantId, decisionSessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session result unavailable after closed-loop execution.");
        decision = decision with { LastRecompetition = recompetitionDto, PendingResearchNeed = researchNeedDto };

        return new DecisionClosedLoopResultDto(
            decisionSessionId, Applied: true, AlreadyProcessed: false,
            impact, recompetitionDto, researchNeedDto, decision, audit);
    }

    // POLOXI Bounded Research Loop (§13/§14/§18). Repeatedly: pick the highest-Information-Value frontier
    // branch, retrieve external evidence for it, verify + promote (lifecycle §14), map that to the branch's
    // supporting graph edge, and drive the existing single-iteration closed loop (ApplyVerificationChangeAsync
    // → propagation → recompetition → frontier/IV recalculation). Every round is budget-checked; the loop
    // halts on the FIRST satisfied stop condition and reports it explicitly. This is a bounded convergence
    // engine, never "research until ready".
    public async Task<DecisionResearchLoopResultDto> RunResearchLoopAsync(
        Guid tenantId, Guid userId, Guid decisionSessionId, CancellationToken cancellationToken = default)
    {
        var loop = await repository.GetResearchLoopSettingsAsync(cancellationToken);
        var retrievalArchitecture = await documentCorpusRepository.GetRetrievalArchitectureSettingsAsync(cancellationToken);
        var rounds = new List<DecisionResearchRoundDto>();

        // DIAGNOSTIC (Research Loop Execution Diagnosis) — Boundary A: RunResearchLoopAsync entered.
        // Records that invocation reached the method and the budgets it will run under, so a run that
        // reports INVOKED but no telemetry can be traced to the exact deterministic stop below.
        logger.LogInformation(
            "RESEARCHLOOP A/ENTER session={SessionId} enabled={Enabled} maxRounds={MaxRounds} maxRetrievals={MaxRetrievals} minFrontierIV={MinFrontierIV} epsilon={Epsilon} seedRetriever={SeedRetriever}",
            decisionSessionId, loop.Enabled, loop.MaxRounds, loop.MaxRetrievals,
            loop.MinFrontierInformationValue, loop.NoStateChangeEpsilon, loop.UseSeedRetriever);

        async Task<DecisionResearchLoopResultDto> DoneAsync(
            int executed, int retrievals, string stopReason, DecisionResearchFailureDto? failure = null)
        {
            // DIAGNOSTIC — Boundary C: single exit funnel. Every return path lands here, so this is the
            // authoritative record of RoundsExecuted, retrievals, and the deterministic StopReason.
            logger.LogInformation(
                "RESEARCHLOOP C/EXIT session={SessionId} roundsExecuted={Executed} retrievals={Retrievals} stopReason={StopReason}",
                decisionSessionId, executed, retrievals, stopReason);

            var decisionNow = await GetSessionResultAsync(tenantId, decisionSessionId, cancellationToken)
                ?? throw new InvalidOperationException("Session result unavailable after research loop.");
            return new DecisionResearchLoopResultDto(
                decisionSessionId, loop.Enabled, executed, retrievals, stopReason, rounds, decisionNow, failure);
        }

        // Feature flag OFF preserves the shadow baseline: no autonomous research is performed.
        if (!loop.Enabled)
            return await DoneAsync(0, 0, DecisionResearchLoopStopReasons.LoopDisabled);

        // Concurrency guard: refuse to start a second loop for a session already running one. This keeps
        // the inline-decide path and the explicit cockpit action from interleaving mutations mid-round.
        if (!ActiveResearchLoops.TryAdd(decisionSessionId, 0))
        {
            logger.LogInformation("Research loop already running for session {SessionId}; skipping concurrent run.", decisionSessionId);
            return await DoneAsync(0, 0, DecisionResearchLoopStopReasons.AlreadyRunning);
        }

        try
        {
            return await RunResearchLoopCoreAsync(tenantId, userId, decisionSessionId, loop, retrievalArchitecture, rounds, DoneAsync, cancellationToken);
        }
        finally
        {
            ActiveResearchLoops.TryRemove(decisionSessionId, out _);
        }
    }

    // Core loop body, invoked only while the per-session guard is held.
    private async Task<DecisionResearchLoopResultDto> RunResearchLoopCoreAsync(
        Guid tenantId, Guid userId, Guid decisionSessionId, DecisionResearchLoopSettings loop,
        DecisionRetrievalArchitectureSettings retrievalArchitecture,
        List<DecisionResearchRoundDto> rounds,
        Func<int, int, string, DecisionResearchFailureDto?, Task<DecisionResearchLoopResultDto>> DoneAsync,
        CancellationToken cancellationToken)
    {
        var totalRetrievals = 0;
        var roundNumber = 0;
        var edgesVerifiedThisRun = new HashSet<Guid>();
        var preRoundStage = "INITIALIZATION";

        DecisionSessionPersistence session;
        DecisionGraphPersistence graph;
        string contextCode;
        try
        {
            session = await repository.GetSessionAsync(tenantId, decisionSessionId, cancellationToken)
                ?? throw new InvalidOperationException($"Decision session {decisionSessionId} was not found.");

            // The loop needs a dependency graph: verified evidence is promoted by verifying a graph EDGE, so
            // recompetition can propagate the change deterministically. No graph → nothing to close the loop on.
            var loadedGraph = await repository.GetGraphAsync(tenantId, decisionSessionId, cancellationToken);
            if (loadedGraph is null)
                return await DoneAsync(0, 0, DecisionResearchLoopStopReasons.NoGraph, null);
            graph = loadedGraph;

            contextCode = string.IsNullOrWhiteSpace(session.ContextCode)
                ? DecisionContexts.General
                : session.ContextCode!.Trim().ToUpperInvariant();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return await DoneAsync(0, 0, DecisionResearchLoopStopReasons.RoundFailed,
                new DecisionResearchFailureDto(0, preRoundStage, ex.GetType().Name, ex.Message, false));
        }

        // Prefer the immutable session snapshot. Reloading the matter is only a compatibility fallback for
        // sessions created before the authority-context snapshot columns existed.
        var matterJurisdiction = NormalizeAuthorityContext(session.MatterJurisdiction);
        var governingLaw = NormalizeAuthorityContext(session.GoverningLaw);
        var courtOrForum = NormalizeAuthorityContext(session.CourtOrForum);
        var authorityCutoffDate = session.AuthorityCutoffDate;
        if (matterJurisdiction is null && session.MatterId is { } researchMatterId)
        {
            var researchMatter = await repository.GetMatterAsync(tenantId, researchMatterId, cancellationToken);
            if (researchMatter is not null)
            {
                matterJurisdiction = ResolveMatterJurisdiction(researchMatter, null);
                governingLaw = NormalizeAuthorityContext(researchMatter.GoverningLaw);
                courtOrForum = ResolveCourtOrForum(researchMatter);
            }
        }

        while (true)
        {
            // Stop: round budget.
            if (roundNumber >= loop.MaxRounds)
                return await DoneAsync(roundNumber, totalRetrievals, DecisionResearchLoopStopReasons.MaxRounds, null);

            // Stop: cumulative retrieval budget.
            if (totalRetrievals >= loop.MaxRetrievals)
                return await DoneAsync(roundNumber, totalRetrievals, DecisionResearchLoopStopReasons.RetrievalBudget, null);

            DecisionSessionPersistence current;
            DecisionBranchPersistence target;
            DecisionResearchNeedPersistence researchNeed;
            DecisionGraphEdgePersistence? dependencyPath;
            DecisionResearchTransformationDto? researchTransformation = null;
            try
            {
                preRoundStage = "FRONTIER_SELECTION";

                // Re-read current authoritative state each round (previous round mutated it).
                current = await repository.GetSessionAsync(tenantId, decisionSessionId, cancellationToken)
                    ?? throw new InvalidOperationException("Session state unavailable mid research loop.");

                // Stop: already converged.
                if (string.Equals(current.StatusCode, DecisionStatusCodes.DecisionReady, StringComparison.OrdinalIgnoreCase))
                    return await DoneAsync(roundNumber, totalRetrievals, DecisionResearchLoopStopReasons.DecisionReady, null);

                // Highest-IV open frontier branch above the minimum worth-researching threshold.
                var selectedTarget = current.Branches
                    .Where(b => b.IsOnFrontier)
                    .OrderByDescending(b => b.InformationValue)
                    .FirstOrDefault();
                if (selectedTarget is null || (double)selectedTarget.InformationValue < loop.MinFrontierInformationValue)
                    return await DoneAsync(roundNumber, totalRetrievals, DecisionResearchLoopStopReasons.FrontierBelowThreshold, null);
                target = selectedTarget;

                preRoundStage = "RESEARCH_NEED_SELECTION";
                var frontierNeed = DecisionResearchNeedFactory.Create(
                    current.Branches.ToList(), DependencyImpact.Empty, decisionSessionId, tenantId,
                    userId, current.MatterId, dependencyEventId: null)
                    ?? throw new InvalidOperationException("The selected frontier did not produce a research need.");
                var semanticNeed = await GenerateResearchNeedAsync(current, target, frontierNeed, cancellationToken);
                researchTransformation = semanticNeed.Transformation;
                if (semanticNeed.SelectedNeed is null)
                    return await DoneAsync(roundNumber, totalRetrievals,
                        DecisionResearchLoopStopReasons.ResearchNeedUnresolved,
                        new DecisionResearchFailureDto(roundNumber, preRoundStage, "RESEARCHABILITY_GATE",
                            semanticNeed.Reason ?? "No source-resolvable research leaf passed the bounded researchability gate.", false)
                        {
                            Transformation = semanticNeed.Transformation,
                        });
                researchNeed = semanticNeed.SelectedNeed with
                {
                    MatterJurisdiction = matterJurisdiction,
                    GoverningLaw = governingLaw,
                    CourtOrForum = courtOrForum,
                    AuthorityCutoffDate = authorityCutoffDate,
                };
                researchNeed = researchNeed with { AuthorityScope = ResolveAuthorityScope(researchNeed,session) };
                foreach (var plannedNeed in semanticNeed.Needs)
                {
                    var scopedNeed=plannedNeed with
                    {
                        MatterJurisdiction = matterJurisdiction,
                        GoverningLaw = governingLaw,
                        CourtOrForum = courtOrForum,
                        AuthorityCutoffDate = authorityCutoffDate,
                    };
                    await repository.PersistResearchNeedAsync(scopedNeed with
                    {
                        AuthorityScope=ResolveAuthorityScope(scopedNeed,session),
                    }, cancellationToken);
                }

                // Resolve causality independently from research eligibility. Missing typed lineage must not
                // prevent retrieval; it only means a verified result cannot yet be propagated through V2.
                preRoundStage = "DEPENDENCY_PATH_RESOLUTION";
                var currentGraph = await repository.GetGraphAsync(tenantId, decisionSessionId, cancellationToken) ?? graph;
                dependencyPath = ResolveDependencyPathForBranch(
                    currentGraph, target.DecisionBranchId, edgesVerifiedThisRun);
                researchTransformation = researchTransformation with
                {
                    PropositionId = ResolvePropositionId(currentGraph, dependencyPath),
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                return await DoneAsync(roundNumber, totalRetrievals, DecisionResearchLoopStopReasons.RoundFailed,
                    new DecisionResearchFailureDto(
                        roundNumber, preRoundStage, ex.GetType().Name, ex.Message,
                        AuthoritativeStateChanged: false));
            }

            roundNumber++;
            var narrative = new List<string>();
            var proposition = researchNeed.PropositionToResolve
                ?? throw new InvalidOperationException("The accepted research need has no proposition.");
            if (researchNeed.SourceClassCode.Equals(DecisionResearchSourceClasses.LegalAuthority, StringComparison.OrdinalIgnoreCase)
                && string.IsNullOrWhiteSpace(researchNeed.MatterJurisdiction))
            {
                return await DoneAsync(roundNumber, totalRetrievals,
                    DecisionResearchLoopStopReasons.ResearchNeedUnresolved,
                    new DecisionResearchFailureDto(roundNumber, "AUTHORITY_CONTEXT_RESOLUTION",
                        "AUTHORITY_JURISDICTION_NOT_ESTABLISHED",
                        "Legal-authority research was deferred because the authoritative decision contract does not establish matter jurisdiction.", false)
                    {
                        Transformation = researchTransformation,
                    });
            }
            var searchQuery = string.IsNullOrWhiteSpace(researchNeed.SearchQuery)
                ? proposition
                : researchNeed.SearchQuery;
            // DIAGNOSTIC — Boundary B: a research round is actually starting. If A and C are logged but B is
            // never reached, the loop stopped on a pre-round guard (StopReason in the C record explains which).
            logger.LogInformation(
                "RESEARCHLOOP B/ROUND-START session={SessionId} round={Round} branch={BranchCode} targetIV={TargetIV} dependencyPath={EdgeId}",
                decisionSessionId, roundNumber, target.BranchCode, target.InformationValue, dependencyPath?.EdgeId);
            narrative.Add($"Round {roundNumber}: researching highest-IV frontier branch '{target.DisplayName}' (IV {target.InformationValue:F3}).");

            // ── Round consistency (round-level commit) ──────────────────────────────────────────────
            // A round is retrieve → verify → prepare edge change → recompetition. If any of those faults
            // BEFORE the closed-loop commit, we must NOT continue the loop on partially-mutated state:
            // stop with RESEARCH_ROUND_FAILED and leave the previous authoritative decision intact. The
            // per-edge commit inside ApplyVerificationChangeAsync is the atomic boundary for the round.
            var currentStage = "RETRIEVAL";
            try
            {
                // 1) Route the accepted need to its authoritative source boundary, then retrieve.
                IReadOnlyCollection<DecisionRetrievedSource> sources;
                DecisionRetrievalResult? retrievalDiagnostics = null;
                LegalAuthorityRetrievalResult? authorityRetrieval = null;
                var route = researchSourceRouter.Route(
                    researchNeed,
                    researchNeed.MatterJurisdiction,
                    researchNeed.AuthorityCutoffDate?.ToDateTime(TimeOnly.MinValue));
                var retrievalTimer = Stopwatch.StartNew();
                if (!route.RetrievalRequired)
                {
                    logger.LogInformation(
                        "Research need {ResearchNeedId} uses route {RouteCode}; no source retrieval was invoked.",
                        researchNeed.DecisionResearchNeedId, route.RouteCode);
                    sources = [];
                    narrative.Add($"Research need route {route.RouteCode}: {route.Reason}");
                }
                else if (route.RouteCode == DecisionResearchRouteCodes.MatterCorpus)
                {
                    if (current.MatterId is not { } matterId)
                    {
                        sources = [];
                        narrative.Add("Matter-corpus retrieval was selected, but the decision session has no matter.");
                    }
                    else
                    {
                        var matterItems = await documentCorpusRepository.SearchRoutedMatterContextAsync(
                            tenantId, matterId, searchQuery, route.DocumentTypeCodes, 5, cancellationToken);
                        sources = matterItems.Select(item => new DecisionRetrievedSource(item.SourceReference, item.Title, item.Text)
                        {
                            SourceType = EvidenceSourceType.MatterDocument,
                            SourceProvider = "LEGAL_MATTER_CORPUS",
                            SourceVersion = item.LegalDocumentVersionId?.ToString(),
                            ProviderIdentityVerified = item.LegalDocumentId.HasValue && item.LegalDocumentVersionId.HasValue
                        }).ToArray();
                    }
                }
                else try
                {
                    authorityRetrieval=await retriever.RetrieveResearchNeedAsync(new(
                        tenantId,decisionSessionId,researchNeed,route.Jurisdiction,
                        route.AuthorityCutoffDate is { } cutoff?DateOnly.FromDateTime(cutoff):DateOnly.MaxValue,
                        Math.Max(1,loop.MaxRetrievals-totalRetrievals),5),cancellationToken);
                    if(authorityRetrieval.Plan.LegalSearchPlanId==Guid.Empty)
                    {
                        authorityRetrieval=null;
                        retrievalDiagnostics=await retriever.RetrieveWithDiagnosticsAsync(
                            new DecisionRetrievalRequest(DecisionContexts.Legal,searchQuery,5)
                            {
                                TenantId=tenantId,
                                Jurisdiction=route.Jurisdiction,
                                AuthorityKinds=route.AuthorityKinds,
                                AuthorityCutoffDate=route.AuthorityCutoffDate,
                            },cancellationToken);
                        sources=retrievalDiagnostics.Sources;
                    }
                    else
                    {
                        await repository.PersistLegalResearchExecutionAsync(authorityRetrieval.Plan,authorityRetrieval,cancellationToken);
                        researchTransformation = researchTransformation with
                        {
                            SearchPlanId = authorityRetrieval.Plan.LegalSearchPlanId,
                        };
                        sources=authorityRetrieval.Authorities.Select(authority=>authority.ToRetrievedSource()).ToArray();
                        retrievalDiagnostics=new(sources,authorityRetrieval.ProviderAttempts.Sum(attempt=>attempt.RawResultCount),0,0,
                            authorityRetrieval.ProviderAttempts.Select(attempt=>new LegalProviderRetrievalDiagnostic(
                                attempt.ProviderCode,true,attempt.Outcome.ToString().ToUpperInvariant(),attempt.RawResultCount,
                                attempt.ReturnedCount,attempt.Detail)).ToArray());
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Research loop retrieval failed for session {SessionId}, branch {BranchCode}.", decisionSessionId, target.BranchCode);
                    sources = [];
                }
                retrievalTimer.Stop();
                if (route.RetrievalRequired)
                    totalRetrievals++;
                if (retrievalArchitecture.TelemetryEnabled)
                {
                    await documentCorpusRepository.PersistRetrievalTelemetryAsync(
                        tenantId, userId,
                        new DecisionRetrievalTelemetry(
                            Guid.NewGuid(), decisionSessionId, current.MatterId, DecisionRetrievalStages.DecisionResearch,
                            "RESEARCH_SOURCE_ROUTED", route.RouteCode, true,
                            retrievalDiagnostics?.RawResultCount ?? sources.Count,
                            (retrievalDiagnostics?.AuthorityKindFilteredCount ?? 0) + (retrievalDiagnostics?.AuthorityDateFilteredCount ?? 0),
                            sources.Count, route.ResearchNeedTypeCode, route.SourceClassCode,
                            route.Jurisdiction, JsonSerializer.Serialize(new
                            {
                                targetBranchId = target.DecisionBranchId,
                                target.BranchCode,
                                researchNeed.DecisionResearchNeedId,
                                researchNeed.ResearchKey,
                                researchNeed.ResearchQuestion,
                                proposition,
                                searchQuery,
                                route.Reason,
                                route.AuthorityKinds,
                                route.DocumentTypeCodes,
                                providers = retrievalDiagnostics?.Providers ?? [],
                                rawResultCount = retrievalDiagnostics?.RawResultCount ?? sources.Count,
                                authorityKindFilteredCount = retrievalDiagnostics?.AuthorityKindFilteredCount ?? 0,
                                authorityDateFilteredCount = retrievalDiagnostics?.AuthorityDateFilteredCount ?? 0,
                                finalResultCount = sources.Count,
                                retrievalOutcome = authorityRetrieval?.Outcome.ToString().ToUpperInvariant(),
                                attempts = authorityRetrieval?.ProviderAttempts.Select(attempt => new
                                {
                                    attempt.LegalSearchPlanId,
                                    attempt.LegalSearchOperationId,
                                    operationKind = attempt.OperationKind?.ToString().ToUpperInvariant(),
                                    attempt.Query,
                                    attempt.ProviderCode,
                                    outcome = attempt.Outcome.ToString().ToUpperInvariant(),
                                    attempt.RawResultCount,
                                    attempt.ReturnedCount,
                                    attempt.RecoveryActionCode,
                                    attempt.Detail,
                                    attempt.DurationMilliseconds,
                                }) ?? [],
                                configuredRoutingFlag = retrievalArchitecture.Stage3AuthoritativeRoutingEnabled,
                                routingMandatory = true
                            }),
                            retrievalTimer.ElapsedMilliseconds),
                        cancellationToken);
                }

                // Retrieval is the prerequisite for verification and candidate recompetition. A round
                // with no usable source must stop here; continuing would create empty evidence rows and
                // allow competition to appear to progress without any retrieved authority.
                if (route.RetrievalRequired && sources.Count == 0)
                {
                    var retrievalOutcome = authorityRetrieval?.Outcome.ToString().ToUpperInvariant()
                        ?? AggregateLegacyRetrievalOutcome(retrievalDiagnostics, sources.Count);
                    var retrievalDetail = authorityRetrieval?.ProviderAttempts
                        .Where(attempt => !string.IsNullOrWhiteSpace(attempt.Detail))
                        .Select(attempt => $"{attempt.ProviderCode}: {attempt.Detail}")
                        .FirstOrDefault()
                        ?? retrievalDiagnostics?.Providers
                            .Where(provider => !string.IsNullOrWhiteSpace(provider.Detail))
                            .Select(provider => $"{provider.ProviderCode}: {provider.Detail}")
                            .FirstOrDefault()
                        ?? "No configured retrieval provider returned a usable authority passage.";

                    logger.LogWarning(
                        "Research loop stopped before verification because retrieval returned no usable sources for session {SessionId}, branch {BranchCode}. Outcome={Outcome} Detail={Detail}",
                        decisionSessionId, target.BranchCode, retrievalOutcome, retrievalDetail);

                    return await DoneAsync(roundNumber, totalRetrievals,
                        DecisionResearchLoopStopReasons.RetrievalNoResults,
                        new DecisionResearchFailureDto(
                            roundNumber, currentStage, "RETRIEVAL_NO_RESULTS",
                            $"Retrieval returned no usable evidence. {retrievalDetail}",
                            AuthoritativeStateChanged: false)
                        {
                            Transformation = researchTransformation,
                        });
                }

                // Persist every attempt before verification. The attachment is explicitly non-authoritative
                // until the verifier finalizes its support state below.
                var retrievedEvidence = sources
                    .Select(s => new DecisionEvidencePersistence(
                        Guid.NewGuid(), target.DecisionBranchId, s.SourceRef, s.Title, s.Snippet,
                        0m, 0m, 0m, 0m, 0m, 0m, DecisionVerificationStates.Unverified)
                    {
                        SupportedObjective = proposition
                    })
                    .ToList();
                var atomicPropositionId = researchNeed.AtomicPropositionId == Guid.Empty
                    ? researchNeed.DecisionResearchNeedId
                    : researchNeed.AtomicPropositionId;
                var proposedAttachments = sources.Zip(retrievedEvidence, (source, evidenceItem) =>
                    new DecisionEvidenceAttachmentPersistence(
                        Guid.NewGuid(), decisionSessionId, tenantId, userId, current.MatterId,
                        researchNeed.DecisionResearchNeedId, target.DecisionBranchId, evidenceItem.DecisionEvidenceId,
                        proposition, DecisionEvidenceAttachmentStates.ProposedSupportFor,
                        dependencyPath?.EdgeId, IsAuthoritative: false, AssessmentReason: null)
                    {
                        AtomicPropositionId = source.AtomicPropositionId == Guid.Empty
                            ? atomicPropositionId
                            : source.AtomicPropositionId,
                        LegalSearchPlanId = source.LegalSearchPlanId == Guid.Empty ? null : source.LegalSearchPlanId,
                        NormalizedAuthorityId = source.NormalizedAuthorityId,
                        NormalizedAuthorityIdentity = source.NormalizedAuthorityIdentity,
                        PassageRef = source.PassageIdentity,
                        PropositionSelectionScore = source.PropositionSelectionScore,
                        PropositionSelectionRank = source.PropositionSelectionRank,
                    }).ToList();
                await repository.PersistResearchEvidenceAsync(
                    tenantId, userId, decisionSessionId, retrievedEvidence, cancellationToken);
                await repository.PersistEvidenceAttachmentsAsync(proposedAttachments, cancellationToken);

                // 2) Verify each retrieved source through the lifecycle ladder; promote only VERIFIED material.
                currentStage = "VERIFICATION";
                var verificationResults = new List<EvidenceVerificationResult>(sources.Count);
                var verified = new List<DecisionEvidencePersistence>(sources.Count);
                var recoveryAttempts = new List<DecisionRetrievalAttemptDto>();
                DecisionResearchRecoveryDto? recovery = null;
                foreach (var (source, persisted) in sources.Zip(retrievedEvidence))
                {
                    var result = await evidenceVerificationPipeline.VerifyAsync(new EvidenceVerificationRequest(
                        persisted.DecisionEvidenceId, target.DecisionBranchId, proposition,
                        source.SourceRef, source.Title, source.Snippet, source.SourceType,
                        source.Jurisdiction, source.AuthorityDate, DecisionMaterial: true)
                    {
                        SourceProvider = source.SourceProvider,
                        SourceVersion = source.SourceVersion,
                        ProviderIdentityVerified = source.ProviderIdentityVerified,
                        GoverningJurisdiction = researchNeed.MatterJurisdiction,
                    }, cancellationToken);
                    verificationResults.Add(result);
                    verified.Add(ToEvidencePersistence(source, proposition, target.DecisionBranchId, result));
                }
                if (verificationResults.Count > 0
                    && verificationResults.All(result => !result.IsDecisionAuthorized)
                    && route.RouteCode == DecisionResearchRouteCodes.LegalAuthority
                    && totalRetrievals < loop.MaxRetrievals)
                {
                    var recoveryPlan = BuildResearchRecoveryPlan(
                        researchNeed, verificationResults, researchNeed.MatterJurisdiction);
                    var recoveryNeedId = Guid.NewGuid();
                    var recoveryNeed = researchNeed with
                    {
                        DecisionResearchNeedId = recoveryNeedId,
                        AtomicPropositionId = recoveryNeedId,
                        PropositionToResolve = recoveryPlan.Proposition,
                        ResearchNeedTypeCode = recoveryPlan.ResearchNeedTypeCode,
                        SourceClassCode = recoveryPlan.SourceClassCode,
                        SearchQuery = recoveryPlan.Query,
                        AuthorityKindsJson = JsonSerializer.Serialize(recoveryPlan.AuthorityKinds),
                        ParentResearchKey = researchNeed.ResearchKey,
                        ResearchKey = $"{researchNeed.ResearchKey ?? researchNeed.DecisionResearchNeedId.ToString("N")}.recovery",
                        SemanticProposalStatusCode = "RECOVERY",
                        SemanticProposalReasonCode = recoveryPlan.DiagnosisCode,
                        StatusCode = "OPEN",
                    };
                    await repository.PersistResearchNeedAsync(recoveryNeed, cancellationToken);
                    narrative.Add($"Verification feedback diagnosed {recoveryPlan.DiagnosisCode}; bounded recovery action {recoveryPlan.ActionCode}.");

                    var recoveryRoute = researchSourceRouter.Route(
                        recoveryNeed,
                        recoveryNeed.MatterJurisdiction,
                        recoveryNeed.AuthorityCutoffDate?.ToDateTime(TimeOnly.MinValue));
                    var recoveryResult = await retriever.RetrieveResearchNeedAsync(new(
                        tenantId, decisionSessionId, recoveryNeed, recoveryRoute.Jurisdiction,
                        recoveryRoute.AuthorityCutoffDate is { } recoveryCutoff
                            ? DateOnly.FromDateTime(recoveryCutoff)
                            : DateOnly.MaxValue,
                        1, 5), cancellationToken);
                    totalRetrievals++;
                    if (recoveryResult.Plan.LegalSearchPlanId != Guid.Empty)
                        await repository.PersistLegalResearchExecutionAsync(recoveryResult.Plan, recoveryResult, cancellationToken);

                    recoveryAttempts.AddRange(recoveryResult.ProviderAttempts.Select(attempt => new DecisionRetrievalAttemptDto(
                        attempt.LegalSearchPlanId, attempt.LegalSearchOperationId,
                        attempt.OperationKind?.ToString().ToUpperInvariant(), attempt.Query ?? recoveryPlan.Query,
                        attempt.ProviderCode, attempt.Outcome.ToString().ToUpperInvariant(), attempt.RawResultCount,
                        attempt.ReturnedCount, attempt.RecoveryActionCode, attempt.Detail, attempt.DurationMilliseconds)));
                    var recoverySources = recoveryResult.Authorities.Select(authority => authority.ToRetrievedSource()).ToArray();
                    var recoveryEvidence = recoverySources.Select(source => new DecisionEvidencePersistence(
                        Guid.NewGuid(), target.DecisionBranchId, source.SourceRef, source.Title, source.Snippet,
                        0m, 0m, 0m, 0m, 0m, 0m, DecisionVerificationStates.Unverified)
                    {
                        SupportedObjective = recoveryPlan.Proposition,
                    }).ToList();
                    var recoveryAttachments = recoverySources.Zip(recoveryEvidence, (source, evidenceItem) =>
                        new DecisionEvidenceAttachmentPersistence(
                            Guid.NewGuid(), decisionSessionId, tenantId, userId, current.MatterId,
                            recoveryNeed.DecisionResearchNeedId, target.DecisionBranchId, evidenceItem.DecisionEvidenceId,
                            recoveryPlan.Proposition, DecisionEvidenceAttachmentStates.ProposedSupportFor,
                            dependencyPath?.EdgeId, IsAuthoritative: false, AssessmentReason: null)
                        {
                            AtomicPropositionId = source.AtomicPropositionId == Guid.Empty
                                ? recoveryNeed.AtomicPropositionId
                                : source.AtomicPropositionId,
                            LegalSearchPlanId = source.LegalSearchPlanId == Guid.Empty ? null : source.LegalSearchPlanId,
                            NormalizedAuthorityId = source.NormalizedAuthorityId,
                            NormalizedAuthorityIdentity = source.NormalizedAuthorityIdentity,
                            PassageRef = source.PassageIdentity,
                            PropositionSelectionScore = source.PropositionSelectionScore,
                            PropositionSelectionRank = source.PropositionSelectionRank,
                        }).ToList();
                    await repository.PersistResearchEvidenceAsync(
                        tenantId, userId, decisionSessionId, recoveryEvidence, cancellationToken);
                    await repository.PersistEvidenceAttachmentsAsync(recoveryAttachments, cancellationToken);

                    var recoveryVerificationResults = new List<EvidenceVerificationResult>(recoverySources.Length);
                    foreach (var (source, persisted) in recoverySources.Zip(recoveryEvidence))
                    {
                        var result = await evidenceVerificationPipeline.VerifyAsync(new EvidenceVerificationRequest(
                            persisted.DecisionEvidenceId, target.DecisionBranchId, recoveryPlan.Proposition,
                            source.SourceRef, source.Title, source.Snippet, source.SourceType,
                            source.Jurisdiction, source.AuthorityDate, DecisionMaterial: true)
                        {
                            SourceProvider = source.SourceProvider,
                            SourceVersion = source.SourceVersion,
                            ProviderIdentityVerified = source.ProviderIdentityVerified,
                            GoverningJurisdiction = recoveryNeed.MatterJurisdiction,
                        }, cancellationToken);
                        recoveryVerificationResults.Add(result);
                        verified.Add(ToEvidencePersistence(source, recoveryPlan.Proposition, target.DecisionBranchId, result));
                    }
                    verificationResults.AddRange(recoveryVerificationResults);
                    retrievedEvidence.AddRange(recoveryEvidence);
                    proposedAttachments.AddRange(recoveryAttachments);
                    var recoveryAuthorized = recoveryVerificationResults.Count(result => result.IsDecisionAuthorized);
                    recovery = new DecisionResearchRecoveryDto(
                        true, recoveryPlan.DiagnosisCode, recoveryPlan.ActionCode, proposition,
                        recoveryPlan.Proposition, recoveryPlan.Query, recoverySources.Length,
                        recoveryVerificationResults.Count, recoveryAuthorized,
                        recoveryAuthorized > 0 ? "RECOVERED" : "UNRESOLVED_RESEARCH_GAP");
                    narrative.Add(recoveryAuthorized > 0
                        ? $"Bounded recovery produced {recoveryAuthorized} decision-authorized source(s)."
                        : "Bounded recovery exhausted without decision-authorized support; the research gap remains unresolved.");

                    var recoveryVerifiedPropositions = recoveryResult.Authorities.Zip(recoveryVerificationResults)
                        .Where(pair => pair.Second.IsDecisionAuthorized)
                        .Select(pair => new VerifiedLegalProposition(
                            recoveryNeed.DecisionResearchNeedId, target.DecisionBranchId,
                            recoveryPlan.Proposition, pair.First, pair.Second))
                        .ToArray();
                    await repository.PersistVerifiedLegalPropositionsAsync(
                        tenantId, decisionSessionId, recoveryVerifiedPropositions, cancellationToken);
                }
                var anyVerified = verified.Any(e =>
                    string.Equals(e.VerificationStatus, DecisionVerificationStates.Verified, StringComparison.OrdinalIgnoreCase));
                var anyContradicted = verified.Any(e =>
                    string.Equals(e.VerificationStatus, DecisionVerificationStates.Invalidated, StringComparison.OrdinalIgnoreCase));
                await repository.UpdateResearchEvidenceAsync(
                    tenantId, userId, decisionSessionId, verified, cancellationToken);
                var persistedVerifications = verificationResults.Select(v => ToPersistence(
                    v, decisionSessionId, tenantId, userId, current.MatterId)).ToArray();
                await repository.PersistEvidenceVerificationsAsync(persistedVerifications, cancellationToken);
                var verificationByEvidence = persistedVerifications.ToDictionary(v => v.DecisionEvidenceId);
                if(authorityRetrieval is not null)
                {
                    var verifiedPropositions=authorityRetrieval.Authorities.Zip(verificationResults)
                        .Where(pair=>pair.Second.IsDecisionAuthorized)
                        .Select(pair=>new VerifiedLegalProposition(
                            researchNeed.DecisionResearchNeedId,target.DecisionBranchId,proposition,pair.First,pair.Second))
                        .ToArray();
                    await repository.PersistVerifiedLegalPropositionsAsync(
                        tenantId,decisionSessionId,verifiedPropositions,cancellationToken);
                }

                // 3) The verified evidence resolves the supporting edge: VERIFIED strengthens the dependency,
                //    a contradiction invalidates it; anything else leaves it unverified (still an open frontier).
                var newStatus = anyVerified
                    ? DecisionVerificationStates.Verified
                    : anyContradicted
                        ? DecisionVerificationStates.Invalidated
                        : DecisionVerificationStates.Unverified;

                var lifecycleState = anyVerified
                    ? DecisionEvidenceLifecycleStates.Verified
                    : anyContradicted
                        ? DecisionEvidenceLifecycleStates.Contradicted
                        : verified.Count == 0
                            ? DecisionEvidenceLifecycleStates.RetrievalFailed
                            : DecisionEvidenceLifecycleStates.Unsupported;

                var finalizedAttachments = proposedAttachments.Zip(verificationResults, (attachment, verification) =>
                {
                    var state = verification.IsDecisionAuthorized
                        ? DecisionEvidenceAttachmentStates.SupportedBy
                        : verification.Disposition == EvidenceSupportDisposition.Contradicted
                            ? DecisionEvidenceAttachmentStates.ContradictedBy
                            : verification.Disposition == EvidenceSupportDisposition.PartiallySupported
                                ? DecisionEvidenceAttachmentStates.PartiallySupportedBy
                                : DecisionEvidenceAttachmentStates.Unsupported;
                    return attachment with
                    {
                        SupportStateCode = state,
                        IsAuthoritative = verification.IsDecisionAuthorized,
                        DecisionEvidenceVerificationId = verificationByEvidence[verification.DecisionEvidenceId].DecisionEvidenceVerificationId,
                        SourceSnapshotId = verification.SourceSnapshot?.SourceSnapshotId,
                        PassageRef = verification.SourceSnapshot?.PassageRef ?? attachment.PassageRef,
                        AssessmentReason = verification.BlockingReasons.Count == 0
                            ? "All required independent verification factors passed."
                            : string.Join("; ", verification.BlockingReasons)
                    };
                }).ToList();
                await repository.UpdateEvidenceAttachmentsAsync(finalizedAttachments, cancellationToken);

                narrative.Add($"Retrieved {sources.Count} source(s); evidence lifecycle → {lifecycleState}.");

                var entropyBefore = current.CandidateEntropy;
                var winnerChanged = false;
                var dependencyStateChanged = false;
                var recompetitionTriggered = false;
                var impactOutcome = "NO_VERIFIED_DECISION_SIGNAL";
                string? impactDetail = "No independently verified proposition support was admitted.";

                // 4) Drive the existing single-iteration closed loop with this edge verification change. A
                //    unique idempotency key per round guarantees each research round applies exactly once.
                //    This is the round's COMMIT point (propagation + recompetition persisted together).
                var authorityChanged = !string.Equals(
                        newStatus, DecisionVerificationStates.Unverified, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(
                        dependencyPath?.VerificationStatus, DecisionVerificationStates.Verified, StringComparison.OrdinalIgnoreCase);
                if (dependencyPath is not null && authorityChanged)
                {
                    currentStage = "COMMIT";
                    edgesVerifiedThisRun.Add(dependencyPath.EdgeId);
                    var changeRequest = new DecisionVerificationChangeRequest(
                        dependencyPath.EdgeId, newStatus,
                        Notes: $"Autonomous research loop round {roundNumber}: {proposition}",
                        IdempotencyKey: $"RESEARCHLOOP:{decisionSessionId:N}:{roundNumber}:{dependencyPath.EdgeId:N}",
                        RunClosedLoop: true);

                    var loopResult = await ApplyVerificationChangeAsync(tenantId, userId, decisionSessionId, changeRequest, cancellationToken);
                    winnerChanged = loopResult.Recompetition?.WinnerChanged ?? false;
                    dependencyStateChanged = loopResult.Impact.AffectedBranchIds.Count>0||loopResult.Impact.ChangedEdgeIds.Count>0;
                    recompetitionTriggered = loopResult.Recompetition is not null;
                    impactOutcome = winnerChanged
                        ? "WINNER_CHANGED"
                        : dependencyStateChanged
                            ? "DEPENDENCY_CHANGED_WINNER_STABLE"
                            : "VERIFIED_SIGNAL_NO_STATE_CHANGE";
                    impactDetail = loopResult.AuditNarrative.LastOrDefault();
                    foreach (var line in loopResult.AuditNarrative)
                        narrative.Add(line);
                }
                else if (!string.Equals(newStatus, DecisionVerificationStates.Unverified, StringComparison.OrdinalIgnoreCase))
                {
                    narrative.Add("Authoritative evidence was retained, but no typed dependency path was available for propagation.");
                    impactOutcome = "VERIFIED_SIGNAL_NO_DEPENDENCY_PATH";
                    impactDetail = "Independent verification authorized the proposition, but no typed dependency path was available.";
                }
                else
                {
                    narrative.Add("No authoritative evidence support was produced this round.");
                    if (recovery is { OutcomeCode: "UNRESOLVED_RESEARCH_GAP" })
                    {
                        impactOutcome = "UNRESOLVED_RESEARCH_GAP";
                        impactDetail = $"{recovery.DiagnosisCode}: bounded recovery '{recovery.ActionCode}' produced no decision-authorized evidence.";
                    }
                }

                currentStage = "POST_COMMIT_READBACK";
                var after = await repository.GetSessionAsync(tenantId, decisionSessionId, cancellationToken)
                    ?? throw new InvalidOperationException("Session state unavailable after research round.");
                var entropyAfter = after.CandidateEntropy;
                var nextRecommendedFrontier = SelectFrontier(after.Branches, loop.MinFrontierInformationValue);
                await repository.PersistLegalDecisionImpactAsync(tenantId,decisionSessionId,new(
                    researchNeed.DecisionResearchNeedId,target.DecisionBranchId,
                    finalizedAttachments.Any(attachment=>attachment.IsAuthoritative),dependencyStateChanged,
                    recompetitionTriggered,winnerChanged,impactOutcome,impactDetail),cancellationToken);

                var verificationDispositionCounts = verified
                    .GroupBy(e => e.LifecycleState ?? e.VerificationStatus, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);
                var attachmentStateCounts = finalizedAttachments
                    .GroupBy(a => a.SupportStateCode, StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

                rounds.Add(new DecisionResearchRoundDto(
                    roundNumber, target.DecisionBranchId, target.DisplayName, target.InformationValue,
                    dependencyPath?.EdgeId, newStatus, lifecycleState,
                    sources.Count + (recovery?.SourcesRetrieved ?? 0), winnerChanged,
                    entropyBefore, entropyAfter, narrative)
                {
                    PropositionToResolve = researchNeed.PropositionToResolve,
                    SourcesEvaluated = verified.Count,
                    VerificationDispositionCounts = verificationDispositionCounts,
                    AttachmentStateCounts = attachmentStateCounts,
                    AuthoritativeChanges = finalizedAttachments.Count(a => a.IsAuthoritative),
                    Transformation = researchTransformation,
                    NextRecommendedFrontierBranchId = nextRecommendedFrontier?.DecisionBranchId,
                    NextRecommendedFrontierBranchCode = nextRecommendedFrontier?.BranchCode,
                    NextRecommendedFrontierLabel = nextRecommendedFrontier?.DisplayName,
                    RetrievalOutcomeCode = authorityRetrieval?.Outcome.ToString().ToUpperInvariant()
                        ?? AggregateLegacyRetrievalOutcome(retrievalDiagnostics, sources.Count),
                    RetrievalAttempts = (authorityRetrieval?.ProviderAttempts.Select(attempt => new DecisionRetrievalAttemptDto(
                        attempt.LegalSearchPlanId,
                        attempt.LegalSearchOperationId,
                        attempt.OperationKind?.ToString().ToUpperInvariant(),
                        attempt.Query ?? searchQuery,
                        attempt.ProviderCode,
                        attempt.Outcome.ToString().ToUpperInvariant(),
                        attempt.RawResultCount,
                        attempt.ReturnedCount,
                        attempt.RecoveryActionCode,
                        attempt.Detail,
                        attempt.DurationMilliseconds)).ToArray()
                        ?? retrievalDiagnostics?.Providers.Select(provider => new DecisionRetrievalAttemptDto(
                            null,null,null,searchQuery,provider.ProviderCode,provider.OutcomeCode,
                            provider.RawResultCount,provider.ReturnedCount,"LEGACY_PATH_NO_RECOVERY_METADATA",
                            provider.Detail,0)).ToArray()
                        ?? []).Concat(recoveryAttempts).ToArray(),
                    Recovery = recovery,
                    AtomicPropositionId = researchNeed.AtomicPropositionId == Guid.Empty
                        ? researchNeed.DecisionResearchNeedId
                        : researchNeed.AtomicPropositionId,
                    SearchPlanId = authorityRetrieval?.Plan.LegalSearchPlanId,
                    AuthorityScope = researchNeed.AuthorityScope,
                    SelectedPassages = sources.Select(source => new DecisionRetrievedPassageLineageDto(
                        source.SourceRef, source.PassageIdentity, source.SourceProvider,
                        source.PropositionSelectionScore, source.PropositionSelectionRank,
                        source.NormalizedAuthorityId)).ToArray(),
                });

                // Stop: no material state change (|Δentropy| below epsilon AND no winner flip).
                var entropyDelta = Math.Abs((double)(entropyAfter - entropyBefore));
                if (!winnerChanged && entropyDelta < loop.NoStateChangeEpsilon)
                    return await DoneAsync(roundNumber, totalRetrievals, DecisionResearchLoopStopReasons.NoStateChange, null);
            }
            catch (Exception roundEx) when (roundEx is not OperationCanceledException)
            {
                // Round faulted before/at commit. Do NOT advance the loop on partially-mutated state:
                // record the failed round and stop, preserving the previous authoritative decision.
                logger.LogWarning(roundEx, "Research loop round {Round} failed for session {SessionId}; stopping with prior state preserved.", roundNumber, decisionSessionId);
                narrative.Add($"Round {roundNumber} failed before commit: {roundEx.Message}. Prior decision state preserved.");
                rounds.Add(new DecisionResearchRoundDto(
                    roundNumber, target.DecisionBranchId, target.DisplayName, target.InformationValue,
                    dependencyPath?.EdgeId, DecisionVerificationStates.Unverified, DecisionEvidenceLifecycleStates.VerificationFailed,
                    0, false, current.CandidateEntropy, current.CandidateEntropy, narrative));
                var failure = new DecisionResearchFailureDto(
                    roundNumber, currentStage, roundEx.GetType().Name, roundEx.Message,
                    AuthoritativeStateChanged: false);
                return await DoneAsync(
                    roundNumber, totalRetrievals, DecisionResearchLoopStopReasons.RoundFailed, failure);
            }
        }
    }

    // Resolve the closest typed structural dependency path for a frontier branch. Structural verification
    // does not gate research: VERIFIED here means the proposed graph relationship is coherent, not that
    // external evidence has established it. Explicit branch lineage wins; otherwise node lineage supplies
    // the deterministic bridge. No label/string matching is permitted.
    private static DecisionGraphEdgePersistence? ResolveDependencyPathForBranch(
        DecisionGraphPersistence graph, Guid branchId, HashSet<Guid> excludedEdgeIds)
    {
        bool Available(DecisionGraphEdgePersistence e) => !excludedEdgeIds.Contains(e.EdgeId);

        var branchEdge = graph.Edges
            .Where(e => e.SourceBranchId == branchId && Available(e))
            .OrderByDescending(e => e.IsEssential)
            .ThenByDescending(e => e.Materiality)
            .ThenBy(e => e.EdgeId)
            .FirstOrDefault();
        if (branchEdge is not null)
            return branchEdge;

        var branchNodeIds = graph.Nodes
            .Where(n => n.SourceBranchId == branchId)
            .Select(n => n.NodeId)
            .ToHashSet();
        if (branchNodeIds.Count == 0)
            return null;

        return graph.Edges
            .Where(e => Available(e)
                && (branchNodeIds.Contains(e.SourceNodeId) || branchNodeIds.Contains(e.TargetNodeId)))
            .OrderByDescending(e => e.IsEssential)
            .ThenByDescending(e => e.Materiality)
            .ThenBy(e => e.EdgeId)
            .FirstOrDefault();
    }

    private static string AggregateLegacyRetrievalOutcome(
        DecisionRetrievalResult? diagnostics,
        int sourceCount)
    {
        if(sourceCount>0)return "RESULTS_FOUND";
        if(diagnostics is null||diagnostics.Providers.Count==0)return "NO_DIAGNOSTICS";
        var codes=diagnostics.Providers.Select(provider=>provider.OutcomeCode.ToUpperInvariant()).ToArray();
        if(codes.Any(code=>code.Contains("PARSE")||code.Contains("EXTRACT")))return "PARSING_FAILURE";
        if(diagnostics.RawResultCount>0)return "FILTERED_OUT";
        if(codes.Any(code=>code.Contains("FAIL")||code.Contains("ERROR")||code.Contains("TIMEOUT")))return "PROVIDER_FAILURE";
        if(codes.All(code=>code.Contains("DISABLED")||code.Contains("COVERAGE")))return "COVERAGE_GAP";
        return "NO_RESULTS";
    }

    private static Guid? ResolvePropositionId(
        DecisionGraphPersistence graph,
        DecisionGraphEdgePersistence? dependencyPath)
    {
        if (dependencyPath is null)
            return null;

        return graph.Nodes.FirstOrDefault(node =>
            node.NodeId == dependencyPath.SourceNodeId &&
            node.NodeKind.Equals(DecisionGraphNodeKinds.Proposition, StringComparison.OrdinalIgnoreCase))?.NodeId
            ?? graph.Nodes.FirstOrDefault(node =>
                node.NodeId == dependencyPath.TargetNodeId &&
                node.NodeKind.Equals(DecisionGraphNodeKinds.Proposition, StringComparison.OrdinalIgnoreCase))?.NodeId;
    }

    private static DecisionBranchPersistence? SelectFrontier(
        IReadOnlyCollection<DecisionBranchPersistence> branches,
        double minimumInformationValue) => branches
            .Where(branch => branch.IsOnFrontier && (double)branch.InformationValue >= minimumInformationValue)
            .OrderByDescending(branch => branch.InformationValue)
            .ThenByDescending(branch => branch.AdvScore)
            .FirstOrDefault();

    private async Task<DecisionModelRouteDto> ResolveRouteAsync(string featureCode, string? modelCode, CancellationToken cancellationToken)
    {
        var routes = await repository.GetModelRoutesAsync(cancellationToken);
        return DecisionModelRouteSelector.Select(routes, featureCode, modelCode);
    }

    // Resolves the frozen execution mode for a decision session. Continuations inherit the parent's
    // ModeCode (immutable snapshot); new sessions use the requested mode. The resolved mode is validated
    // against DB-backed policy and the deployment environment: DEV Logic (IsProductionAllowed = false)
    // is rejected server-side in a Production deployment even if the request/UI selects it.
    private async Task<(DecisionExecutionModeDto Mode, string? DefaultModelCode)> ResolveExecutionModeAsync(
        DecisionExecutionMode requestedMode, string? inheritedModeCode, CancellationToken cancellationToken)
    {
        var modes = await repository.GetExecutionModesAsync(cancellationToken);
        if (modes.Count == 0)
            throw new InvalidOperationException("No decision execution modes are configured (POLOXI.Legal_DecisionExecutionMode).");

        var desiredCode = !string.IsNullOrWhiteSpace(inheritedModeCode)
            ? inheritedModeCode!.Trim().ToUpperInvariant()
            : requestedMode == DecisionExecutionMode.Prod ? "PROD" : "DEV";

        var mode = modes.FirstOrDefault(m => string.Equals(m.ExecutionModeCode, desiredCode, StringComparison.OrdinalIgnoreCase))
            ?? modes.FirstOrDefault(m => string.Equals(m.ExecutionModeCode, "PROD", StringComparison.OrdinalIgnoreCase))
            ?? modes.First();

        if (!mode.IsProductionAllowed && executionEnvironment.IsProduction)
        {
            var prod = modes.FirstOrDefault(m => m.IsProductionAllowed)
                ?? throw new InvalidOperationException(
                    $"Execution mode '{mode.ExecutionModeCode}' is not permitted in the '{executionEnvironment.EnvironmentName}' environment and no production-allowed mode is configured.");
            logger.LogWarning(
                "Execution mode {Requested} is not allowed in {Environment}; falling back to {Fallback}.",
                mode.ExecutionModeCode, executionEnvironment.EnvironmentName, prod.ExecutionModeCode);
            mode = prod;
        }

        var defaultModelCode = string.IsNullOrWhiteSpace(mode.DefaultModelCode) ? null : mode.DefaultModelCode;
        return (mode, defaultModelCode);
    }

    private async Task<(IReadOnlyList<DecisionResearchNeedPersistence> Needs, DecisionResearchNeedPersistence? SelectedNeed, string? Reason, DecisionResearchTransformationDto Transformation)> GenerateResearchNeedAsync(
        DecisionSessionPersistence session,
        DecisionBranchPersistence frontier,
        DecisionResearchNeedPersistence frontierNeed,
        CancellationToken cancellationToken)
    {
        var prompt = await repository.GetPromptAsync(ResearchNeedPromptCode, cancellationToken)
            ?? throw new InvalidOperationException($"The '{ResearchNeedPromptCode}' decision prompt is not configured.");
        var route = await ResolveRouteAsync(ResearchNeedPromptCode, session.ModelCode, cancellationToken);
        var candidates = JsonSerializer.Serialize(session.Candidates.Select(candidate => new
        {
            candidate.CandidateCode,
            candidate.DisplayName,
            candidate.Outcome,
        }));
        var frontierArtifact = JsonSerializer.Serialize(new
        {
            frontier.DecisionBranchId,
            frontier.BranchCode,
            frontier.DisplayName,
            frontier.Interpretation,
            frontier.InformationValue,
            frontier.FlipPotential,
            frontier.EvidenceAvailability,
        });
        var clarificationLineage = await repository.GetClarificationLineageAsync(
            session.TenantId, session.DecisionSessionId, cancellationToken);
        var clarificationContext = BuildClarificationPromptContext(
            clarificationLineage,
            new HashSet<Guid> { frontier.DecisionBranchId },
            session.Candidates.Select(candidate => candidate.DecisionCandidateId).ToHashSet());
        var userPrompt = prompt.UserPromptTemplate
            .Replace("{{QUERY}}", session.QueryText + clarificationContext)
            .Replace("{{CANDIDATES}}", candidates)
            .Replace("{{FRONTIER}}", frontierArtifact);
        var gate = new DecisionResearchabilityGate();
        var attempts = new List<DecisionResearchTransformationAttemptDto>(2);

        DecisionResearchTransformationDto Transformation(
            string? selectedResearchKey = null,
            string? selectionReason = null,
            string? searchQuery = null,
            Guid? selectedResearchNeedId = null) => new(
                frontier.DecisionBranchId,
                frontier.BranchCode,
                frontier.DisplayName,
                attempts.ToArray(),
                selectedResearchKey,
                selectionReason,
                searchQuery)
            {
                AttemptedResearchNeedId = frontierNeed.DecisionResearchNeedId,
                SelectedResearchNeedId = selectedResearchNeedId,
            };

        for (var attempt = 1; attempt <= 2; attempt++)
        {
            DecisionAiResult response;
            try
            {
                response = await aiProvider.GenerateAsync(new DecisionAiRequest(
                    route, ResearchNeedPromptCode, prompt.SystemPrompt, userPrompt,
                    prompt.OutputSchemaJson, session.CorrelationId ?? Guid.NewGuid().ToString("N")), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                attempts.Add(new DecisionResearchTransformationAttemptDto(
                    attempt, route.ModelCode, "MODEL_CALL_FAILED", 0, "UNRESOLVED",
                    [$"MODEL_CALL_FAILED:{ex.GetType().Name}"], []));
                logger.LogWarning(ex,
                    "Research need transformation model call failed for session {SessionId}, branch {BranchCode}, attempt {Attempt}.",
                    session.DecisionSessionId, frontier.BranchCode, attempt);
                return ([], null, $"UNRESOLVED: MODEL_CALL_FAILED:{ex.GetType().Name}", Transformation());
            }

            var rawProposal = response.StructuredOutputJson ?? response.Content;
            var parseSucceeded = TryParseResearchSemanticProposal(rawProposal, out var proposal,
                out var outputClassification);
            var outputIntegrityFailure = !parseSucceeded
                || outputClassification != DecisionModelOutputClassifications.StructuredProposal;
            // Deterministically self-heal recoverable MATTER-DOCUMENT retrieval fields (SearchQuery /
            // SearchConcepts) before the gate evaluates the proposal. This never invents evidence and never
            // touches legal-authority leaves; it only stops one matter leaf's missing search expression from
            // blocking an independently valid legal-authority leaf from reaching retrieval.
            proposal = DecisionResearchNeedContractNormalizer.Normalize(proposal);
            var evaluation = gate.Evaluate(proposal);
            if (evaluation.IsAcceptable)
            {
                var selectedLeaf = evaluation.ResearchableLeaves
                    .Where(item => item.SourceClass.Equals(DecisionResearchSourceClasses.LegalAuthority, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => item.CandidateDiscrimination.Count)
                    .ThenBy(item => item.ResearchKey, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault()
                    ?? evaluation.ResearchableLeaves
                        .OrderByDescending(item => item.CandidateDiscrimination.Count)
                        .ThenBy(item => item.ResearchKey, StringComparer.OrdinalIgnoreCase)
                        .First();
                var proposalStatus = attempt == 1 ? "ACCEPTED" : "REPAIRED";
                var proposalReason = attempt == 1 ? null : "BOUNDED_RESEARCHABILITY_REPAIR_SUCCEEDED";
                var needs = proposal.Leaves
                    .Select(leaf => DecisionResearchNeedFactory.CreateFromLeaf(
                        frontierNeed, leaf, proposalStatus, proposalReason,
                        leaf.ResearchKey.Equals(selectedLeaf.ResearchKey, StringComparison.OrdinalIgnoreCase) ? "OPEN" : "PLANNED"))
                    .ToArray();
                var selectedNeed = needs.Single(need =>
                    need.ResearchKey!.Equals(selectedLeaf.ResearchKey, StringComparison.OrdinalIgnoreCase));
                attempts.Add(BuildResearchTransformationAttempt(
                    attempt, route.ModelCode, "COMPLETED", "ACCEPT", proposal, evaluation));
                logger.LogInformation(
                    "Research need transformation accepted for session {SessionId}, branch {BranchCode}, attempt {Attempt}, leaves={Leaves}, selected={SelectedResearchKey}, sourceClass={SourceClass}.",
                    session.DecisionSessionId, frontier.BranchCode, attempt, proposal.Leaves.Count,
                    selectedLeaf.ResearchKey, selectedLeaf.SourceClass);
                return (needs, selectedNeed, null, Transformation(
                    selectedLeaf.ResearchKey,
                    "LEGAL_AUTHORITY_PREFERRED_THEN_CANDIDATE_DISCRIMINATION",
                    selectedLeaf.SearchQuery,
                    selectedNeed.DecisionResearchNeedId));
            }

            var disposition = outputIntegrityFailure
                ? DecisionResearchNeedDisposition.Repair
                : parseSucceeded
                ? DecisionResearchNeedRepairPlanner.Diagnose(evaluation)
                : DecisionResearchNeedDisposition.Repair;
            var status = outputIntegrityFailure ? "MODEL_OUTPUT_INTEGRITY_FAILURE" : "GATE_REJECTED";
            var defects = !outputIntegrityFailure
                ? evaluation.Defects
                : ["MODEL_OUTPUT_INTEGRITY_FAILURE", outputClassification, .. evaluation.Defects];
            attempts.Add(BuildResearchTransformationAttempt(
                attempt, route.ModelCode, status, disposition.ToString().ToUpperInvariant(), proposal,
                evaluation with { Defects = defects }, outputClassification,
                outputClassification == DecisionModelOutputClassifications.ModelReturnedClarificationQuestion
                    ? "The model returned a clarification question instead of the required structured Decision Contract."
                    : "The model response did not satisfy the required structured Decision Contract."));
            logger.LogInformation(
                "Research need transformation rejected for session {SessionId}, branch {BranchCode}, attempt {Attempt}, status={Status}, outputClassification={OutputClassification}, disposition={Disposition}, leaves={Leaves}, defects={Defects}.",
                session.DecisionSessionId, frontier.BranchCode, attempt, status, disposition,
                outputClassification, proposal.Leaves.Count, string.Join("; ", defects));

            // Do not spend the bounded repair attempt on an unrecoverable structural failure.
            if (attempt == 1 && disposition != DecisionResearchNeedDisposition.Unresolved)
            {
                userPrompt += DecisionResearchNeedRepairPlanner.BuildDirective(disposition, defects);
                continue;
            }

            // Partial progression: the bounded repair did not fully satisfy the gate, but valid researchable
            // leaves exist and every residual defect is confined to the derived/application synthesis leaf.
            // Progress retrieval with the valid legal-authority/matter leaves rather than discarding them; the
            // defective application leaf is preserved as UNRESOLVED (no fabricated evidence or conclusion).
            if (parseSucceeded && evaluation.CanProgressWithResearchableLeaves)
            {
                var researchableKeys = evaluation.ResearchableLeaves
                    .Select(item => item.ResearchKey)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var selectedLeaf = evaluation.ResearchableLeaves
                    .Where(item => item.SourceClass.Equals(DecisionResearchSourceClasses.LegalAuthority, StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(item => item.CandidateDiscrimination.Count)
                    .ThenBy(item => item.ResearchKey, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault()
                    ?? evaluation.ResearchableLeaves
                        .OrderByDescending(item => item.CandidateDiscrimination.Count)
                        .ThenBy(item => item.ResearchKey, StringComparer.OrdinalIgnoreCase)
                        .First();
                var needs = proposal.Leaves
                    .Select(leaf =>
                    {
                        var isResearchable = researchableKeys.Contains(leaf.ResearchKey);
                        var statusCode = isResearchable
                            ? (leaf.ResearchKey.Equals(selectedLeaf.ResearchKey, StringComparison.OrdinalIgnoreCase) ? "OPEN" : "PLANNED")
                            : "UNRESOLVED";
                        var leafStatus = isResearchable ? "PARTIAL_REPAIRED" : "UNRESOLVED_DEPENDENCY";
                        return DecisionResearchNeedFactory.CreateFromLeaf(
                            frontierNeed, leaf, leafStatus, "APPLICATION_LEAF_UNRESOLVED_RESEARCHABLE_LEAVES_PROGRESSED", statusCode);
                    })
                    .ToArray();
                var selectedNeed = needs.Single(need =>
                    need.ResearchKey!.Equals(selectedLeaf.ResearchKey, StringComparison.OrdinalIgnoreCase));
                logger.LogInformation(
                    "Research need transformation progressed with researchable leaves for session {SessionId}, branch {BranchCode}, attempt {Attempt}, selected={SelectedResearchKey}, unresolvedApplicationDefects={Defects}.",
                    session.DecisionSessionId, frontier.BranchCode, attempt, selectedLeaf.ResearchKey,
                    string.Join("; ", evaluation.ApplicationLeafDefects));
                return (needs, selectedNeed, null, Transformation(
                    selectedLeaf.ResearchKey,
                    "RESEARCHABLE_LEAVES_PROGRESSED_APPLICATION_UNRESOLVED",
                    selectedLeaf.SearchQuery,
                    selectedNeed.DecisionResearchNeedId));
            }

            return ([], null, $"{disposition.ToString().ToUpperInvariant()}: {string.Join("; ", defects)}", Transformation());
        }

        return ([], null, "RESEARCHABILITY_GATE_UNRESOLVED", Transformation());
    }

    private static bool TryParseResearchSemanticProposal(
        string json,
        out DecisionResearchSemanticProposal proposal,
        out string outputClassification)
    {
        try
        {
            proposal = JsonSerializer.Deserialize<DecisionResearchSemanticProposal>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? new DecisionResearchSemanticProposal();
            outputClassification = proposal.Leaves.Count == 0
                ? ClassifyEmptyResearchProposal(json)
                : DecisionModelOutputClassifications.StructuredProposal;
            return true;
        }
        catch (JsonException)
        {
            proposal = new DecisionResearchSemanticProposal();
            outputClassification = ClassifyEmptyResearchProposal(json);
            return false;
        }
    }

    private static string ClassifyEmptyResearchProposal(string? raw)
    {
        if (!string.IsNullOrWhiteSpace(raw)
            && (raw.Contains("clarif", StringComparison.OrdinalIgnoreCase)
                || raw.Contains("question", StringComparison.OrdinalIgnoreCase)
                || raw.Contains('?')))
            return DecisionModelOutputClassifications.ModelReturnedClarificationQuestion;
        return DecisionModelOutputClassifications.IncompleteStructuredProposal;
    }

    private static DecisionResearchTransformationAttemptDto BuildResearchTransformationAttempt(
        int attempt,
        string modelCode,
        string status,
        string disposition,
        DecisionResearchSemanticProposal proposal,
        DecisionResearchabilityResult evaluation,
        string outputClassification = DecisionModelOutputClassifications.StructuredProposal,
        string? blockingReason = null)
    {
        var researchableKeys = evaluation.ResearchableLeaves
            .Select(leaf => leaf.ResearchKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var leaves = proposal.Leaves.Select(leaf =>
        {
            var rejected = evaluation.Defects.Any(defect =>
                defect.StartsWith($"{leaf.ResearchKey}:", StringComparison.OrdinalIgnoreCase));
            var gateStatus = rejected ? "REJECT"
                : researchableKeys.Contains(leaf.ResearchKey) ? "ACCEPT"
                : leaf.Researchable ? "NOT_SELECTED"
                : "ACCEPT_AS_DERIVED";
            return new DecisionResearchTransformationLeafDto(
                leaf.ResearchKey,
                leaf.ResearchNeedType,
                leaf.Proposition,
                leaf.SourceClass,
                leaf.Researchable,
                gateStatus,
                leaf.SearchQuery,
                leaf.Requires);
        }).ToArray();

        return new DecisionResearchTransformationAttemptDto(
            attempt, modelCode, status, proposal.Leaves.Count, disposition,
            evaluation.Defects, leaves)
        {
            OutputClassification = outputClassification,
            BlockingReason = blockingReason,
        };
    }

    // Deterministic integrity check for a parsed proposal. The LLM proposes structure but cannot be
    // trusted to always produce a well-formed, non-degenerate candidate set. A proposal is acceptable
    // only when it yields at least two distinct, well-named candidate outcomes — otherwise competition
    // has nothing meaningful to discriminate between.
    private static ProposalIntegrityResult EvaluateProposalIntegrity(IReadOnlyList<ProposedCandidate> proposal)
    {
        if (proposal.Count == 0)
            return new ProposalIntegrityResult(false, "no candidates parsed");

        if (proposal.Any(c => string.IsNullOrWhiteSpace(c.DisplayName) || string.IsNullOrWhiteSpace(c.Outcome)))
            return new ProposalIntegrityResult(false, "candidate missing displayName or outcome");

        var distinctOutcomes = proposal
            .Select(c => c.Outcome.Trim().ToLowerInvariant())
            .Distinct()
            .Count();
        if (distinctOutcomes < 2)
            return new ProposalIntegrityResult(false, "fewer than two distinct candidate outcomes");

        return new ProposalIntegrityResult(true, "ok");
    }

    // Computes structural + SHADOW-MODE semantic diagnostics for a parsed proposal. Semantic dimensions
    // (query fidelity, interpretation distinctness/coverage, candidate separability, empty/duplicate
    // interpretations) are recorded for correlation analysis ONLY — they never reject a run in V1.
    // This lets us test P(good result | healthy interpretations) vs P(good result | weak interpretations)
    // before any semantic signal is promoted to an authoritative recovery trigger.
    private static ProposalIntegrityDiagnostics BuildProposalIntegrityDiagnostics(
        string query, IReadOnlyList<ProposedCandidate> proposal, ProposalIntegrityResult integrity)
    {
        var candidateCount = proposal.Count;
        var distinctCandidateCount = proposal
            .Select(c => (c.Outcome ?? string.Empty).Trim().ToLowerInvariant())
            .Where(o => o.Length > 0)
            .Distinct()
            .Count();

        var interpretations = proposal
            .SelectMany(c => c.Branches ?? Array.Empty<ProposedBranch>())
            .Select(b => b.Interpretation ?? string.Empty)
            .ToList();
        var interpretationCount = interpretations.Count;
        var emptyInterpretationCount = interpretations.Count(i => string.IsNullOrWhiteSpace(i));
        var normalizedInterpretations = interpretations
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim().ToLowerInvariant())
            .ToList();
        var distinctInterpretations = normalizedInterpretations.Distinct().Count();
        var duplicateInterpretationCount = normalizedInterpretations.Count - distinctInterpretations;

        double interpretationDistinctness = normalizedInterpretations.Count == 0
            ? 0d
            : (double)distinctInterpretations / normalizedInterpretations.Count;

        // Query fidelity: fraction of salient query terms echoed across the proposal text. Coarse by
        // design — a shadow signal, not an authoritative measure of relevance.
        var queryTerms = Tokenize(query);
        var proposalText = string.Join(" ",
            proposal.Select(c => $"{c.DisplayName} {c.Outcome}")
                .Concat(interpretations));
        var proposalTerms = Tokenize(proposalText).ToHashSet();
        double queryFidelity = queryTerms.Count == 0
            ? 0d
            : (double)queryTerms.Count(t => proposalTerms.Contains(t)) / queryTerms.Count;

        // Interpretation coverage: interpretations present relative to candidates (are candidates
        // actually reasoned about, or bare outcomes?).
        double interpretationCoverage = candidateCount == 0
            ? 0d
            : (double)(interpretationCount - emptyInterpretationCount) / candidateCount;

        // Candidate separability: distinct outcomes relative to candidate count (1.0 ⇒ all distinct).
        double candidateSeparability = candidateCount == 0
            ? 0d
            : (double)distinctCandidateCount / candidateCount;

        return new ProposalIntegrityDiagnostics(
            StructuralValid: integrity.IsAcceptable,
            CandidateCount: candidateCount,
            DistinctCandidateCount: distinctCandidateCount,
            InterpretationCount: interpretationCount,
            EmptyInterpretationCount: emptyInterpretationCount,
            DuplicateInterpretationCount: duplicateInterpretationCount,
            QueryFidelity: Math.Round(queryFidelity, 4),
            InterpretationDistinctness: Math.Round(interpretationDistinctness, 4),
            InterpretationCoverage: Math.Round(interpretationCoverage, 4),
            CandidateSeparability: Math.Round(candidateSeparability, 4),
            RecoveryTriggered: false,
            RecoveryReason: null,
            RecoverySucceeded: null);
    }

    // Diagnoses concrete defects from the diagnostics and maps them to a disposition. Structural failures
    // are hard defects (Regenerate). Semantic weaknesses are soft defects (Repair) and, in shadow mode,
    // are recorded but never rejected. Returns Accept when the proposal is structurally valid and shows
    // no soft defects. The disposition is advisory in shadow mode and authoritative only when recovery is
    // enabled.
    private static (ProposalDisposition Disposition, IReadOnlyList<string> Defects) DiagnoseProposal(ProposalIntegrityDiagnostics d)
    {
        var defects = new List<string>();

        if (!d.StructuralValid)
        {
            if (d.CandidateCount == 0)
                defects.Add("NO_CANDIDATES");
            else if (d.DistinctCandidateCount < 2)
                defects.Add("LOW_CANDIDATE_SEPARABILITY");
            else
                defects.Add("INCOMPLETE_CANDIDATE_FIELDS");
            return (ProposalDisposition.Regenerate, defects);
        }

        if (d.DuplicateInterpretationCount > 0)
            defects.Add("DUPLICATE_INTERPRETATIONS");
        if (d.EmptyInterpretationCount > 0)
            defects.Add("EMPTY_INTERPRETATIONS");
        if (d.InterpretationCount == 0)
            defects.Add("MISSING_INTERPRETATIONS");
        else if (d.InterpretationCoverage < 1.0d)
            defects.Add("INCOMPLETE_INTERPRETATION_COVERAGE");
        if (d.InterpretationCount > 0 && d.InterpretationDistinctness < 0.5d)
            defects.Add("LOW_INTERPRETATION_DISTINCTNESS");
        if (d.CandidateSeparability < 1.0d)
            defects.Add("WEAK_CANDIDATE_SEPARABILITY");
        if (d.QueryFidelity < 0.25d)
            defects.Add("QUERY_DRIFT");

        return defects.Count == 0
            ? (ProposalDisposition.Accept, defects)
            : (ProposalDisposition.Repair, defects);
    }

    // Builds a defect-targeted recovery instruction. This is REPAIR, not a re-roll: it preserves valid
    // existing structure and asks only for correction of the diagnosed defects.
    private static string BuildRecoveryInstruction(IReadOnlyList<string> defects)
    {
        var lines = new List<string>
        {
            "The previous proposal was rejected by the Proposal Integrity Gate.",
            "Preserve the original query meaning and all valid existing structure. Correct ONLY the defects below:"
        };
        foreach (var defect in defects)
        {
            lines.Add(defect switch
            {
                "NO_CANDIDATES" => "- Provide candidate outcomes; the proposal contained none.",
                "LOW_CANDIDATE_SEPARABILITY" => "- Provide at least two materially distinct candidate outcomes.",
                "INCOMPLETE_CANDIDATE_FIELDS" => "- Every candidate must have a non-empty displayName and outcome.",
                "DUPLICATE_INTERPRETATIONS" => "- Replace redundant interpretations with materially distinct ones.",
                "EMPTY_INTERPRETATIONS" => "- Fill in every empty interpretation with substantive reasoning.",
                "MISSING_INTERPRETATIONS" => "- Add interpretations that explain how each candidate is reasoned about.",
                "INCOMPLETE_INTERPRETATION_COVERAGE" => "- Ensure each candidate is supported by at least one interpretation.",
                "LOW_INTERPRETATION_DISTINCTNESS" => "- Increase the distinctness of interpretations; they are too similar.",
                "WEAK_CANDIDATE_SEPARABILITY" => "- Make candidate outcomes clearly distinguishable from one another.",
                "QUERY_DRIFT" => "- Realign the proposal with the original question; it has drifted off-topic.",
                _ => $"- Correct: {defect}."
            });
        }
        lines.Add("Respond with valid JSON only, matching the required schema.");
        return string.Join("\n", lines);
    }

    private static IReadOnlyList<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();
        return text
            .Split(new[] { ' ', '\t', '\n', '\r', '.', ',', ';', ':', '!', '?', '(', ')', '"', '\'', '/', '\\', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.ToLowerInvariant())
            .Where(t => t.Length > 3)
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyList<ProposedCandidate> ParseProposal(string json, int maxCandidates)
    {
        var results = new List<ProposedCandidate>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("candidates", out var candidatesNode) || candidatesNode.ValueKind != JsonValueKind.Array)
                return results;
            foreach (var c in candidatesNode.EnumerateArray())
            {
                var branches = new List<ProposedBranch>();
                if (c.TryGetProperty("branches", out var branchNode) && branchNode.ValueKind == JsonValueKind.Array)
                    foreach (var b in branchNode.EnumerateArray())
                        branches.Add(ParseBranch(b));
                results.Add(new ProposedCandidate(
                    GetString(c, "displayName"), GetString(c, "outcome"),
                    GetNumber(c, "legalSupport"), GetNumber(c, "factSupport"), GetNumber(c, "evidenceSupport"),
                    GetNumber(c, "authoritySupport"), GetNumber(c, "verification"),
                    GetNumber(c, "discrimination"), GetNumber(c, "rankingImpact"), branches));
                if (results.Count >= maxCandidates)
                    break;
            }
        }
        catch (JsonException)
        {
            // Non-JSON proposal ⇒ no candidates; the caller surfaces this as a failed proposal.
        }
        return results;
    }

    // Branch-first (v2) adapter — mirrors IntelligenceWideService's semantic-to-legacy adaptation for
    // /legal/search. DECISION_DISCOVERY_V2 emits the Wide semantic shape: a SHARED "semanticRoots"
    // forest (each root carries nested "children" branches) plus one GLOBAL "candidates" universe that
    // carries NO scores. To keep all downstream Core scoring/ranking/retrieval identical to the legacy
    // path, this flattens the shared forest into ProposedBranch objects ONCE and attaches that SAME
    // shared branch set to EVERY candidate (a branch is a shared axis of competition, owned by no
    // candidate). Because the LLM emits no numbers, branch/candidate signals are seeded neutrally
    // (0.5) — honest "unknown until evidence", since Core derives the real scores downstream from
    // retrieved evidence, exactly as the Wide pipeline does.
    private static IReadOnlyList<ProposedCandidate> AdaptSemanticProposal(string json, int maxCandidates)
    {
        var results = new List<ProposedCandidate>();
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // 1) Flatten the SHARED semantic root/branch forest into a neutral ProposedBranch tree.
            var sharedBranches = new List<ProposedBranch>();
            if (root.TryGetProperty("semanticRoots", out var rootsNode) && rootsNode.ValueKind == JsonValueKind.Array)
                foreach (var r in rootsNode.EnumerateArray())
                    sharedBranches.Add(AdaptSemanticBranch(r));

            // 2) Read the GLOBAL candidate universe (scoreless). Seed neutral support and attach the
            //    same shared branch forest to each candidate so Core competes them on identical axes.
            if (root.TryGetProperty("candidates", out var candidatesNode) && candidatesNode.ValueKind == JsonValueKind.Array)
                foreach (var c in candidatesNode.EnumerateArray())
                {
                    var resolution = GetString(c, "resolution");
                    var display = string.IsNullOrWhiteSpace(GetString(c, "candidateType"))
                        ? resolution
                        : $"{GetString(c, "candidateType")}: {resolution}";
                    var rationale = GetString(c, "rationaleSummary");
                    results.Add(new ProposedCandidate(
                        string.IsNullOrWhiteSpace(display) ? resolution : display,
                        string.IsNullOrWhiteSpace(rationale) ? resolution : rationale,
                        LegalSupport: 0.5d, FactSupport: 0.5d, EvidenceSupport: 0.5d,
                        AuthoritySupport: 0.5d, Verification: 0.5d,
                        Discrimination: 0.5d, RankingImpact: 0.5d,
                        Branches: sharedBranches));
                    if (results.Count >= maxCandidates)
                        break;
                }
        }
        catch (JsonException)
        {
            // Non-JSON proposal ⇒ no candidates; the caller surfaces this as a failed proposal.
        }
        return results;
    }

    // Recursively adapts a Wide semantic root/branch node into a neutral ProposedBranch. Roots and
    // branches share the same "children" recursion; a root's identifying text comes from "label",
    // interpretation from "semanticQuestion"/"interpretation". Numeric signals are seeded neutrally
    // because the branch-first contract intentionally emits no scores.
    private static ProposedBranch AdaptSemanticBranch(JsonElement node)
    {
        var children = new List<ProposedBranch>();
        if (node.TryGetProperty("children", out var childNode) && childNode.ValueKind == JsonValueKind.Array)
            foreach (var child in childNode.EnumerateArray())
                children.Add(AdaptSemanticBranch(child));
        var interpretation = GetString(node, "interpretation");
        if (string.IsNullOrWhiteSpace(interpretation))
            interpretation = GetString(node, "semanticQuestion");
        return new ProposedBranch(
            GetString(node, "label"), interpretation,
            DecisionRelevance: 0.5d, FlipPotential: 0.5d, EvidenceAvailability: 0.5d,
            children);
    }

    // a coarse branch with a "subBranches" (or "branches") array of decisive sub-questions; these feed
    // the bounded adaptive-deepening pass. When absent, Children is empty and nothing deepens.
    private static ProposedBranch ParseBranch(JsonElement b)
    {
        var children = new List<ProposedBranch>();
        if ((b.TryGetProperty("subBranches", out var childNode) || b.TryGetProperty("branches", out childNode))
            && childNode.ValueKind == JsonValueKind.Array)
            foreach (var child in childNode.EnumerateArray())
                children.Add(ParseBranch(child));
        return new ProposedBranch(
            GetString(b, "displayName"), GetString(b, "interpretation"),
            GetNumber(b, "decisionRelevance"), GetNumber(b, "flipPotential"), GetNumber(b, "evidenceAvailability"),
            children);
    }

    private List<DecisionCandidatePersistence> ScoreCandidates(IReadOnlyList<ProposedCandidate> proposal, DecisionCoreSettings settings, Guid sessionId, Guid tenantId, out List<DecisionBranchPersistence> branches, CancellationToken cancellationToken)
    {
        branches = [];
        var scored = new List<DecisionCandidatePersistence>();
        var index = 0;
        foreach (var p in proposal)
        {
            var composite = DecisionCoreMath.CompositeScore(p.LegalSupport, p.FactSupport, p.EvidenceSupport, p.AuthoritySupport, p.Verification);
            var ceiling = DecisionCoreMath.CertaintyCeiling(p.LegalSupport, p.FactSupport, p.EvidenceSupport, p.AuthoritySupport);
            // Redundancy penalty / diversity vs the other proposed candidates (§8).
            var redundancy = proposal.Where(o => !ReferenceEquals(o, p)).Select(o => DecisionCoreMath.Similarity(p.DisplayName + " " + p.Outcome, o.DisplayName + " " + o.Outcome)).DefaultIfEmpty(0d).Max();
            var candidateId = Guid.NewGuid();
            var uncertainty = 1d - p.Verification;
            scored.Add(new DecisionCandidatePersistence(
                candidateId, $"C{index + 1}", p.DisplayName, p.Outcome,
                (decimal)DecisionCoreMath.Clamp01(p.LegalSupport), (decimal)DecisionCoreMath.Clamp01(p.FactSupport),
                (decimal)DecisionCoreMath.Clamp01(p.EvidenceSupport), (decimal)DecisionCoreMath.Clamp01(p.AuthoritySupport),
                (decimal)DecisionCoreMath.Clamp01(p.Verification), (decimal)DecisionCoreMath.Clamp01(uncertainty),
                (decimal)DecisionCoreMath.Clamp01(p.Discrimination), (decimal)DecisionCoreMath.Clamp01(p.RankingImpact),
                (decimal)DecisionCoreMath.Clamp01(1d - redundancy), (decimal)DecisionCoreMath.Clamp01(redundancy),
                (decimal)Math.Min(composite, ceiling), (decimal)ceiling, 0, false, false));

            var branchIndex = 0;
            foreach (var b in p.Branches)
            {
                MaterializeBranch(b, settings, branches, parentBranchId: null, parentCode: $"C{index + 1}", level: 1, sortSeed: branchIndex);
                branchIndex++;
            }
            index++;
        }

        // Rank + mark winner (§28 strongest-losing-side handled by ranking; single pass for the slice).
        var ranked = scored.OrderByDescending(c => c.CompositeScore).ToList();
        for (var i = 0; i < ranked.Count; i++)
            ranked[i] = ranked[i] with { RankOrder = i + 1, IsWinner = i == 0 };
        return ranked;
    }

    // Bounded adaptive deepening (§ deepening loop). Scores a proposed branch, appends it to the flat
    // persistence list, then — only when the branch is genuinely worth deepening — recurses into its
    // proposed sub-branches. The gate is deterministic and mirrors POLOXI frontier semantics:
    //   deepen iff the branch is on the frontier, its FlipPotential >= ThresholdDeepeningFlip, the next
    //   level is still <= MaxDepth, and the LLM actually proposed sub-branches.
    // When no sub-branches were proposed (the common case), this behaves identically to the prior flat
    // single-pass materialization, so existing sessions and golden masters are unaffected.
    internal static void MaterializeBranch(
        ProposedBranch b, DecisionCoreSettings settings, List<DecisionBranchPersistence> branches,
        Guid? parentBranchId, string parentCode, int level, int sortSeed)
    {
        var u = 1d - b.EvidenceAvailability;
        var iv = DecisionCoreMath.InformationValue(settings, u, b.DecisionRelevance, b.FlipPotential, b.EvidenceAvailability, novelty: 1d, redundancyPenalty: 0d);
        const double cost = 1d;
        var adv = DecisionCoreMath.LegalAdv(iv, b.DecisionRelevance, b.FlipPotential, cost);
        var onFrontier = DecisionCoreMath.IsOnFrontier(settings, DecisionBranchStates.Active, b.DecisionRelevance, b.FlipPotential);
        var branchId = Guid.NewGuid();
        var branchCode = $"{parentCode}.B{sortSeed + 1}";
        branches.Add(new DecisionBranchPersistence(
            branchId, parentBranchId, level, branchCode, b.DisplayName, b.Interpretation,
            DecisionBranchStates.Active, (decimal)iv, (decimal)DecisionCoreMath.Clamp01(b.DecisionRelevance),
            (decimal)DecisionCoreMath.Clamp01(b.FlipPotential), (decimal)DecisionCoreMath.Clamp01(b.EvidenceAvailability),
            (decimal)adv, (decimal)cost, onFrontier, onFrontier ? null : "BELOW_FRONTIER_THRESHOLD", sortSeed));

        // Deepening gate: bounded by MaxDepth, driven by frontier membership and flip potential.
        var shouldDeepen = b.Children.Count > 0
            && onFrontier
            && b.FlipPotential >= (double)settings.ThresholdDeepeningFlip
            && level < settings.MaxDepth;
        if (!shouldDeepen)
            return;

        var childIndex = 0;
        foreach (var child in b.Children)
        {
            MaterializeBranch(child, settings, branches, parentBranchId: branchId, parentCode: branchCode, level: level + 1, sortSeed: childIndex);
            childIndex++;
        }
    }

    internal sealed record DomainGuardrailGovernanceResult(
        List<DecisionBranchPersistence> Branches,
        int MatchedBranchCount,
        int NovelBranchCount,
        int FallbackBranchCount);

    internal static DomainGuardrailGovernanceResult ApplyDomainGuardrails(
        string query,
        IReadOnlyCollection<DecisionCandidatePersistence> candidates,
        IReadOnlyCollection<DecisionBranchPersistence> dynamicBranches,
        IReadOnlyCollection<DecisionDomainConceptDto> concepts,
        IReadOnlyCollection<DecisionDomainConceptRelationDto> relations)
    {
        var governed = new List<DecisionBranchPersistence>(dynamicBranches.Count + concepts.Count);
        var matchedCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var matchedCount = 0;
        var novelCount = 0;

        foreach (var branch in dynamicBranches)
        {
            var branchText = $"{branch.DisplayName} {branch.Interpretation}";
            var match = concepts
                .Select(concept => new
                {
                    Concept = concept,
                    Score = DomainConceptMatchScore(
                        branchText,
                        $"{concept.Name} {concept.Description} {concept.DimensionCode}"),
                })
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Concept.SortOrder)
                .FirstOrDefault();

            // A concept match enriches provenance only; it never changes branch scores/state/frontier.
            if (match is not null && match.Score >= 0.34d)
            {
                matchedCodes.Add(match.Concept.ConceptCode);
                matchedCount++;
                var constrained = relations.Any(relation => relation.IsHardConstraint
                    && (relation.SourceConceptCode.Equals(match.Concept.ConceptCode, StringComparison.OrdinalIgnoreCase)
                        || relation.TargetConceptCode.Equals(match.Concept.ConceptCode, StringComparison.OrdinalIgnoreCase)));
                governed.Add(branch with
                {
                    GenerationOriginCode = DecisionBranchGenerationOrigins.DynamicLlmEnriched,
                    DecisionDomainConceptId = match.Concept.DecisionDomainConceptId,
                    DomainConceptCode = match.Concept.ConceptCode,
                    GuardrailMatchScore = (decimal)DecisionCoreMath.Clamp01(match.Score),
                    GuardrailActionCode = constrained
                        ? DecisionGuardrailActions.ConstraintEnriched
                        : DecisionGuardrailActions.ConceptMatched,
                    GuardrailVersion = match.Concept.VersionNumber,
                });
            }
            else
            {
                novelCount++;
                governed.Add(branch with
                {
                    GenerationOriginCode = DecisionBranchGenerationOrigins.DynamicLlm,
                    GuardrailActionCode = DecisionGuardrailActions.NovelAccepted,
                });
            }
        }

        // Fallback coverage is deliberately narrow and dormant: only required concepts that are strongly
        // relevant to the actual query and absent from every dynamic branch are recorded. They do not enter
        // Candidate × Branch competition, IV, frontier, retrieval, or ranking unless later reopened by POLOXI.
        var fallbackCount = 0;
        var fallbackParentCode = candidates.OrderBy(candidate => candidate.RankOrder).FirstOrDefault()?.CandidateCode ?? "DOMAIN";
        foreach (var concept in concepts
            .Where(concept => concept.IsRequiredCoverage && concept.IsFallbackEligible && !matchedCodes.Contains(concept.ConceptCode))
            .OrderBy(concept => concept.SortOrder))
        {
            var queryFit = DomainConceptMatchScore(query, $"{concept.Name} {concept.Description} {concept.DimensionCode}");
            if (queryFit < 0.42d)
                continue;

            fallbackCount++;
            governed.Add(new DecisionBranchPersistence(
                Guid.NewGuid(), null, 1, $"{fallbackParentCode}.GF{fallbackCount}", concept.Name,
                concept.Description, DecisionBranchStates.Dormant,
                InformationValue: 0m, DecisionRelevance: 0m, FlipPotential: 0m, EvidenceAvailability: 0m,
                AdvScore: 0m, Cost: 1m, IsOnFrontier: false,
                StopReason: "DOMAIN_FALLBACK_NOT_ACTIVATED", SortOrder: 10000 + concept.SortOrder)
            {
                GenerationOriginCode = DecisionBranchGenerationOrigins.DomainFallback,
                DecisionDomainConceptId = concept.DecisionDomainConceptId,
                DomainConceptCode = concept.ConceptCode,
                GuardrailMatchScore = (decimal)DecisionCoreMath.Clamp01(queryFit),
                GuardrailActionCode = DecisionGuardrailActions.FallbackAdded,
                GuardrailVersion = concept.VersionNumber,
            });
        }

        return new DomainGuardrailGovernanceResult(governed, matchedCount, novelCount, fallbackCount);
    }

    private static double DomainConceptMatchScore(string left, string right)
    {
        var semanticSimilarity = DecisionCoreMath.Similarity(left, right);
        var leftTokens = DomainMatchTokens(left);
        var rightTokens = DomainMatchTokens(right);
        if (leftTokens.Count == 0 || rightTokens.Count == 0)
            return semanticSimilarity;

        var overlap = leftTokens.Count(token => rightTokens.Contains(token));
        var coverage = (double)overlap / Math.Min(leftTokens.Count, rightTokens.Count);
        return Math.Max(semanticSimilarity, coverage);
    }

    private static HashSet<string> DomainMatchTokens(string value)
    {
        string[] stopWords = ["THE", "AND", "OR", "OF", "TO", "A", "AN", "IS", "ARE", "FOR", "IN", "ON", "WITH", "WHETHER"];
        var stops = stopWords.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return value
            .Split([' ', '\t', '\r', '\n', '-', '/', '.', ',', ';', ':', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Select(NormalizeDomainText)
            .Where(token => token.Length >= 4 && !stops.Contains(token))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    // Explicit retrieval outcome so retrieval failure is a first-class decision signal, not a log-only
    // event. ResearchStatus is one of DecisionResearchStates; FailureDetail is populated only on failure.
    internal sealed record EvidenceRetrievalOutcome(
        List<DecisionEvidencePersistence> Evidence,
        List<EvidenceVerificationResult> Verifications,
        string ResearchStatus,
        string? FailureDetail);

    private static DecisionEvidencePersistence ToEvidencePersistence(
        DecisionRetrievedSource source,
        string objective,
        Guid? branchId,
        EvidenceVerificationResult verification)
    {
        var status = verification.Disposition == EvidenceSupportDisposition.Contradicted
            ? DecisionVerificationStates.Invalidated
            : verification.IsDecisionAuthorized
                ? DecisionVerificationStates.Verified
                : DecisionVerificationStates.Unverified;
        var lifecycle = verification.IsDecisionAuthorized
            ? DecisionEvidenceLifecycleStates.Verified
            : verification.Disposition switch
            {
                EvidenceSupportDisposition.PartiallySupported => DecisionEvidenceLifecycleStates.PartiallySupported,
                EvidenceSupportDisposition.Unsupported => DecisionEvidenceLifecycleStates.Unsupported,
                EvidenceSupportDisposition.Contradicted => DecisionEvidenceLifecycleStates.Contradicted,
                EvidenceSupportDisposition.Error => DecisionEvidenceLifecycleStates.VerificationFailed,
                _ => DecisionEvidenceLifecycleStates.Unverifiable,
            };
        var propositionFit = verification.PropositionSupport.State == PropositionSupportState.Supported ? 1m
            : verification.PropositionSupport.State == PropositionSupportState.PartiallySupported ? 0.5m : 0m;

        return new DecisionEvidencePersistence(
            verification.DecisionEvidenceId, branchId, source.SourceRef, source.Title, source.Snippet,
            verification.Identity.State == VerificationCheckState.Passed ? 1m : 0m,
            verification.Citation.State == VerificationCheckState.Passed ? 1m : 0m,
            verification.Holding.State is VerificationCheckState.Passed or VerificationCheckState.NotApplicable ? 1m : 0m,
            verification.Authority.State is VerificationCheckState.Passed or VerificationCheckState.NotApplicable ? 1m : 0m,
            propositionFit,
            verification.IsVerified ? 1m : 0m,
            status)
        {
            SupportedObjective = objective,
            SupportingPassage = verification.IsDecisionAuthorized ? verification.PropositionSupport.SupportingPassage : null,
            LifecycleState = lifecycle,
        };
    }

    private static DecisionEvidenceVerificationPersistence ToPersistence(
        EvidenceVerificationResult verification,
        Guid decisionSessionId,
        Guid tenantId,
        Guid? actorUserId,
        Guid? matterId)
    {
        var runId = Guid.NewGuid();
        var factors = new[]
        {
            ToFactor(runId, EvidenceVerificationFactor.Identity, verification.Identity),
            ToFactor(runId, EvidenceVerificationFactor.Provenance, verification.Provenance),
            ToFactor(runId, EvidenceVerificationFactor.Citation, verification.Citation),
            ToFactor(runId, EvidenceVerificationFactor.Passage, verification.Passage),
            ToFactor(runId, verification.PropositionSupport),
            ToFactor(runId, EvidenceVerificationFactor.StatementRole, verification.StatementRole),
            ToFactor(runId, EvidenceVerificationFactor.Holding, verification.Holding),
            ToFactor(runId, EvidenceVerificationFactor.Authority, verification.Authority),
        };
        return new DecisionEvidenceVerificationPersistence(
            runId, verification.DecisionEvidenceId, decisionSessionId, verification.DecisionBranchId,
            EvidenceSourceTypeCodes.ToCode(verification.SourceType), verification.Profile.ProfileCode,
            EvidenceVerificationCodes.Disposition(verification.Disposition), verification.IsVerified,
            verification.IsDecisionAuthorized, JsonSerializer.Serialize(verification.BlockingReasons),
            matterId, tenantId, actorUserId, verification.EvaluatedAt.UtcDateTime, factors)
        {
            RetrievedCount = verification.Telemetry.RetrievedCount,
            PreScreenRejectedCount = verification.Telemetry.PreScreenRejectedCount,
            SourceSnapshotId = verification.SourceSnapshot?.SourceSnapshotId,
            SourceContentHash = verification.SourceSnapshot?.ContentHash,
            PassageHash = verification.SourceSnapshot?.PassageHash,
            SourceProvider = verification.SourceSnapshot?.SourceProvider,
            SourceVersion = verification.SourceSnapshot?.SourceVersion,
            SourceRef = verification.SourceSnapshot?.SourceRef,
            PassageRef = verification.SourceSnapshot?.PassageRef,
            ExtractionVersion = verification.SourceSnapshot?.ExtractionVersion,
            ProfileVersion = verification.Profile.Version,
            MechanicalVerificationCount = verification.Telemetry.MechanicalVerificationCount,
            SemanticVerificationCount = verification.Telemetry.SemanticVerificationCount,
            PoloxiDeepeningCount = verification.Telemetry.PoloxiDeepeningCount,
            CacheHitCount = verification.Telemetry.CacheHitCount,
            InputTokenCount = verification.Telemetry.InputTokens,
            OutputTokenCount = verification.Telemetry.OutputTokens,
            LatencyMilliseconds = verification.Telemetry.TotalLatencyMilliseconds,
        };
    }

    private static DecisionEvidenceVerificationFactorPersistence ToFactor(
        Guid runId,
        EvidenceVerificationFactor factor,
        VerificationCheckResult result) => new(
            Guid.NewGuid(), EvidenceVerificationCodes.Factor(factor), EvidenceVerificationCodes.State(result.State),
            result.ReasonCode, result.Reason, result.VerifiedValue, result.SourceRef, result.SupportingPassage,
            result.VerificationMethod, null, null, result.EvaluatedAt.UtcDateTime)
        {
            PassageRef = result.PassageRef,
            VerifierId = result.VerifierId,
            VerifierVersion = result.VerifierVersion,
        };

    private static DecisionEvidenceVerificationFactorPersistence ToFactor(
        Guid runId,
        PropositionSupportResult result) => new(
            Guid.NewGuid(), EvidenceVerificationCodes.Factor(EvidenceVerificationFactor.PropositionSupport),
            EvidenceVerificationCodes.State(result.State), result.ReasonCode, result.Reason, null, null,
            result.SupportingPassage, result.VerificationMethod,
            JsonSerializer.Serialize(result.SupportedComponents), JsonSerializer.Serialize(result.UnsupportedComponents),
            result.EvaluatedAt.UtcDateTime)
        {
            PassageRef = result.PassageRef,
            VerifierId = result.VerifierId,
            VerifierVersion = result.VerifierVersion,
        };

    // Evidence Verification/Promotion stage (§14). A retrieved source is NOT verified evidence by
    // default. This stage derives each factor from OBSERVABLE properties of the retrieved source and
    // then decides VerificationStatus with a RULE/STATE-BASED gate — not the multiplicative product,
    // which is mathematically unsound as an authoritative decision (e.g. 0.85^5 ≈ 0.444 would fail a
    // 0.5 threshold). The VerificationValue is still computed and persisted, but only as a diagnostic.
    // Promotion to VERIFIED requires that every identity-class factor is satisfied AND the source
    // demonstrably supports the objective (proposition fit above a support floor).
    internal static DecisionEvidencePersistence VerifyRetrievedSource(DecisionRetrievedSource source, string objective, Guid? branchId)
    {
        // Identity: the source is usable at all (has a resolvable reference and a title).
        var hasSourceRef = !string.IsNullOrWhiteSpace(source.SourceRef);
        var hasTitle = !string.IsNullOrWhiteSpace(source.Title);
        var identity = hasSourceRef ? 1.0 : 0.0;

        // Citation/authority identity: a resolvable, non-empty citation reference is present.
        var citation = hasSourceRef && hasTitle ? 1.0 : hasSourceRef ? 0.6 : 0.0;

        // Relevant passage located: the retrieved snippet carries usable content that is INDEPENDENT of
        // the source title. A snippet that merely echoes the title establishes IDENTITY, not proposition
        // support (§14) — e.g. a bare "Proposed Rule on Overtime Pay" result whose passage is its own
        // title. Topical relevance must never be mistaken for a located supporting passage, so a
        // title-echo snippet fails the PASSAGE_LOCATED rung exactly as an empty snippet does.
        var hasRawSnippet = !string.IsNullOrWhiteSpace(source.Snippet);
        var passageEchoesTitle = DecisionCoreMath.PassageEchoesTitle(source.Title, source.Snippet);
        var hasPassage = hasRawSnippet && !passageEchoesTitle;
        var holding = hasPassage ? 1.0 : 0.0;

        // Authority weight: proxied by passage substance (a substantive passage carries more weight
        // than a bare stub). Retained as a graded diagnostic; not a gate on its own.
        var passageLength = source.Snippet?.Trim().Length ?? 0;
        var weight = hasPassage ? Math.Clamp(0.5 + passageLength / 400.0, 0.5, 1.0) : 0.0;

        // Claim ↔ passage support: content-token overlap (stopwords/short tokens removed, minimum shared
        // content tokens required) between the objective and the retrieved passage. This rejects false
        // support manufactured by incidental function-word overlap (e.g. an unrelated title sharing
        // "that"/"for" with the objective) — the defect that let an executive-order source verify against
        // an uncompensated-overtime proposition. Support is measured ONLY against an independently-located
        // passage: when the snippet just echoes the title there is no passage to support anything, so fit
        // is 0 regardless of topical anchor overlap (the "Proposed Rule on Overtime Pay" false positive).
        var propositionFit = hasPassage
            ? DecisionCoreMath.Clamp01(DecisionCoreMath.PropositionSupport(objective, source.Snippet))
            : 0.0;

        var verificationValue = DecisionCoreMath.EvidenceVerificationValue(
            identity, citation, holding, weight, propositionFit);

        // ── Explicit lifecycle ladder (§14) ──────────────────────────────────────────────────────
        // Walk the discrete verification rungs in order. The FIRST rung that cannot be cleared fixes a
        // terminal state; only a fully-climbed ladder reaches VERIFIED (positive authority). This
        // replaces the multiplicative gate — each rung is auditable and answers "why not trusted yet?".
        const double propositionSupportFloor = 0.10;
        const double propositionContradictionCeiling = 0.02; // effectively no lexical support at all
        var lifecycle = ClassifyEvidenceLifecycle(
            retrieved: true,
            identityVerified: hasSourceRef,
            citationVerified: hasSourceRef,
            passageLocated: hasPassage,
            propositionFit: propositionFit,
            supportFloor: propositionSupportFloor,
            contradictionCeiling: propositionContradictionCeiling);

        // Persisted status stays schema-compatible (VERIFIED / UNVERIFIED / INVALIDATED).
        var status = DecisionEvidenceLifecycleStates.ToPersistedStatus(lifecycle);
        var isVerified = DecisionEvidenceLifecycleStates.GrantsPositiveAuthority(lifecycle);

        return new DecisionEvidencePersistence(
            Guid.NewGuid(), branchId, source.SourceRef, source.Title, source.Snippet,
            (decimal)identity, (decimal)citation, (decimal)holding, (decimal)weight, (decimal)propositionFit,
            (decimal)verificationValue, status)
        {
            // Provenance (§14): a VERIFIED source records WHAT it was verified against and WHICH passage
            // established support, so the claim ↔ source ↔ passage ↔ verification chain is traceable.
            SupportedObjective = objective,
            SupportingPassage = isVerified ? source.Snippet : null,
            LifecycleState = lifecycle
        };
    }

    // Deterministic evidence lifecycle classifier (§14). Climbs the rungs in order; the first unmet
    // rung yields a terminal failure state. Only when every rung clears do we reach VERIFIED. Holding
    // and authority rungs are treated as satisfied when proposition support clears comfortably, since
    // this heuristic verifier cannot independently confirm a holding — that keeps promotion honest
    // (support-established) while leaving room for a stronger verifier to gate those rungs explicitly.
    internal static string ClassifyEvidenceLifecycle(
        bool retrieved,
        bool identityVerified,
        bool citationVerified,
        bool passageLocated,
        double propositionFit,
        double supportFloor,
        double contradictionCeiling)
    {
        if (!retrieved)
            return DecisionEvidenceLifecycleStates.RetrievalFailed;
        if (!identityVerified)
            return DecisionEvidenceLifecycleStates.Unverifiable;   // cannot establish the source is real
        if (!citationVerified)
            return DecisionEvidenceLifecycleStates.Unverifiable;   // no resolvable citation to rely on
        if (!passageLocated)
            return DecisionEvidenceLifecycleStates.Unsupported;    // nothing to support the objective with

        // Passage exists — assess how it relates to the objective.
        if (propositionFit <= contradictionCeiling)
            return DecisionEvidenceLifecycleStates.Unsupported;    // passage present but no support relation
        if (propositionFit < supportFloor)
            return DecisionEvidenceLifecycleStates.PartiallySupported; // some overlap, below the required bar

        // Proposition support cleared → passage supports the objective. This heuristic verifier does not
        // independently confirm holding/authority rungs, so a support-established source is promoted to
        // VERIFIED. A stronger verifier can override to HOLDING_VERIFIED/AUTHORITY_VALIDATED or downgrade.
        return DecisionEvidenceLifecycleStates.Verified;
    }

    internal static List<DecisionFlipPointPersistence> BuildFlipPoints(IReadOnlyList<DecisionBranchPersistence> branches, IReadOnlyList<DecisionCandidatePersistence> candidates, Guid sessionId, Guid tenantId)
    {
        // FlipsWinner is defined STRICTLY from candidate identity: a branch can only flip the winner
        // if the candidate it belongs to is different from the current winner. A high-flip-potential
        // branch that belongs to the winner itself is important but is NOT a winner flip (it must never
        // render as "Winner: Deny → for Deny"). Never derive FlipsWinner from FlipPotential/ADV/text.
        var winner = candidates.OrderBy(c => c.RankOrder).FirstOrDefault();
        var winnerCode = winner?.CandidateCode;
        return branches
            .Where(b => b.IsOnFrontier && b.FlipPotential > 0)
            .OrderByDescending(b => b.FlipPotential)
            .Take(6)
            .Select(b =>
            {
                // A branch belongs to a candidate via its code prefix ("<CandidateCode>.<...>").
                var branchCandidateCode = b.BranchCode.Split('.', 2)[0];
                var targetCandidate = candidates.SingleOrDefault(candidate =>
                    candidate.CandidateCode.Equals(branchCandidateCode, StringComparison.OrdinalIgnoreCase));
                var belongsToWinner = winnerCode is not null
                    && targetCandidate?.DecisionCandidateId == winner?.DecisionCandidateId;
                // A flip that changes the winner is, at minimum, a swap of the top two candidates,
                // i.e. an ordinal rank displacement of 1. RankDelta must never be 0 when the winner
                // changes, otherwise "Δrank 0 · flips winner" is self-contradictory. A branch owned by
                // the winner can never be a winner flip regardless of how high its flip potential is.
                var winnerChanges = targetCandidate is not null && !belongsToWinner && b.FlipPotential >= 0.5m;
                var rankDelta = winnerChanges ? 1 : 0;
                // Reserve "could change the outcome" for modeled winner flips. When the ranking does
                // not move (RankDelta 0), the honest statement is that resolving the branch reinforces
                // the current winner rather than overturning it — never claim an outcome change.
                var description = winnerChanges
                    ? $"Resolving '{b.DisplayName}' could change the outcome."
                    : $"Resolving '{b.DisplayName}' strengthens the current winner but does not change candidate ranking.";
                return new DecisionFlipPointPersistence(
                    Guid.NewGuid(), b.DecisionBranchId,
                    description,
                    b.Cost, winnerChanges, rankDelta)
                {
                    TargetCandidateId = targetCandidate?.DecisionCandidateId,
                    TargetCandidateCode = targetCandidate?.CandidateCode,
                    PolarityCode = winnerChanges ? "TOWARD_TARGET_CANDIDATE"
                        : belongsToWinner ? "REINFORCES_CURRENT_WINNER"
                        : "UNRESOLVED",
                };
            })
            .ToList();
    }

    // Deterministic confidence ceiling for the natural-language composer. The composer may explain the
    // decision state but must never sound more confident than it. Language such as "clear" or "decisive"
    // is only warranted when the margin is wide AND uncertainty is contained; a narrow margin or high
    // entropy caps the wording at "narrow" regardless of which outcome leads.
    internal static string DescribeConfidence(double margin, double entropy)
    {
        if (margin >= 0.15 && entropy < 0.60)
            return "clear — the leader clearly separates from the alternatives; you may state a firm conclusion";
        if (margin >= 0.10 && entropy < 0.75)
            return "moderate — the leader has a meaningful but not decisive edge; avoid the word 'clear'";
        return "narrow — the current leader holds only a narrow advantage over the competing outcome; do not use words like 'clear', 'decisive', or 'strong'";
    }

    // Terminal-state classifier. Core invariant (§32-34):
    //   RESEARCH_EXHAUSTED ⇒ no executable, sufficiently valuable research action remains.
    // Exhaustion is decided ONLY by whether the frontier still offers ADV above the configured floor.
    internal static (string StatusCode, string TerminalState, string Reason) ResolveTerminalState(DecisionCoreSettings settings, double margin, double entropy, bool frontierOpen, double maxAvailableAdv)
    {
        // Genuinely converged: no critical frontier, a clear margin, and contained uncertainty.
        if (!frontierOpen && margin > 0.10 && entropy < 0.60)
            return (DecisionStatusCodes.DecisionReady, DecisionStatusCodes.DecisionReady, "NO_CRITICAL_FRONTIER_AND_CLEAR_MARGIN");

        // A high-value research action still exists on the frontier: research is NOT exhausted.
        // Emit a provisional (leading-outcome) decision so we compose an honest answer while
        // signalling that the highest-value investigation remains open.
        var executableResearchRemains = frontierOpen && maxAvailableAdv >= settings.ThresholdResearchExhaustionAdv;
        if (executableResearchRemains)
            return (DecisionStatusCodes.ProvisionalDecision, DecisionStatusCodes.ProvisionalDecision, "LEADING_OUTCOME_WITH_OPEN_HIGH_VALUE_FRONTIER");

        // No frontier action clears the value floor ⇒ nothing worthwhile left to investigate.
        if (maxAvailableAdv < settings.ThresholdResearchExhaustionAdv)
            return (DecisionStatusCodes.ResearchExhausted, DecisionStatusCodes.ResearchExhausted, "MAX_AVAILABLE_ADV_BELOW_THRESHOLD");

        // Frontier closed with no high-value action but uncertainty not fully contained: converged single pass.
        return (DecisionStatusCodes.DecisionReady, DecisionStatusCodes.DecisionReady, "CONVERGED_SINGLE_PASS");
    }

    private async Task<string?> ComposeAnswerAsync(DecisionSearchRequest request, string effectiveQuery, IReadOnlyList<DecisionCandidatePersistence> candidates, IReadOnlyList<DecisionBranchPersistence> branches, IReadOnlyList<DecisionFlipPointPersistence> flipPoints, double margin, double entropy, CancellationToken cancellationToken)
    {
        var route = await ResolveRouteAsync(AnswerPromptCode, request.ModelCode, cancellationToken);
        var answerPrompt = await repository.GetPromptAsync(AnswerPromptCode, cancellationToken);
        if (answerPrompt is null)
            return null;
        var artifact = new
        {
            winner = candidates.FirstOrDefault(c => c.IsWinner)?.DisplayName,
            margin,
            entropy,
            // Authoritative confidence ceiling: the composer must never sound more confident than the
            // structured decision state. This descriptor is derived deterministically from margin and
            // entropy so language like "clear" is only permitted when the numbers actually support it.
            confidence = DescribeConfidence(margin, entropy),
            candidates = candidates.Select(c => new { c.DisplayName, c.Outcome, c.CompositeScore, c.RankOrder }),
            frontier = branches.Where(b => b.IsOnFrontier).Select(b => new { b.DisplayName, b.FlipPotential, b.DecisionRelevance }),
            flipPoints = flipPoints.Select(f => new { f.Description, f.ChangeCost, f.WinnerChanges })
        };
        var user = answerPrompt.UserPromptTemplate
            .Replace("{{ARTIFACT}}", JsonSerializer.Serialize(artifact))
            .Replace("{{QUERY}}", effectiveQuery);
        var result = await aiProvider.GenerateAsync(new DecisionAiRequest(route, "DECISION_ANSWER", answerPrompt.SystemPrompt, user, null, request.CorrelationId), cancellationToken);
        return result.Content;
    }

    // ── Authoritative output-claim audit enforcement (§34/§35) ─────────────────────────────────────
    // The output audit is no longer advisory: when POLOXI's governance overlay reports that the answer
    // surfaces material claims it never authorized, the composed answer must NOT stand as an asserted
    // legal conclusion. Rather than suppress the reasoning entirely (which would hide POLOXI's thinking),
    // we RESTATE it — the original prose is preserved but reframed as an explicitly provisional,
    // pending-research hypothesis, and every unauthorized material claim is disclosed as unverified.
    // Result of an enforcement pass: the rewritten answer plus the authoritative record of which claims
    // were actually located and rewritten in the prose (versus merely required).
    private sealed record OutputEnforcementResult(
        string Answer,
        IReadOnlyList<Guid> AppliedClaimIds,
        int RequiredCount,
        int AppliedCount)
    {
        public bool UnauthorizedAssertionsRemain => AppliedCount < RequiredCount;
    }

    private static OutputEnforcementResult EnforceOutputAudit(string? finalAnswer, Features.Intelligence.Epistemic.OutputClaimAuditResult audit)
    {
        var unauthorized = audit.Violations.Count;
        var unknown = audit.UnknownClaimIds.Count;

        var preface =
            "⚠ PROVISIONAL — UNVERIFIED. This is POLOXI's current leading hypothesis, not an evidence-backed "
            + "legal conclusion. POLOXI's output audit found "
            + $"{unauthorized} material claim(s) that are not yet verified"
            + (unknown > 0 ? $" and {unknown} claim(s) with unestablished provenance" : string.Empty)
            + ". These claims may NOT be relied upon as established until external evidence is retrieved and "
            + "verified. The reasoning below is reproduced for transparency but must be read as pending research.";

        if (string.IsNullOrWhiteSpace(finalAnswer))
        {
            var required = audit.Authorizations.Count(a =>
                a.Disposition != Features.Intelligence.Epistemic.OutputClaimDisposition.Allow
                && !string.IsNullOrWhiteSpace(a.ClaimText));
            return new OutputEnforcementResult(preface, [], required, 0);
        }

        // Rewrite the prose itself so each qualified/suppressed/corrected claim is reconciled with its
        // POLOXI disposition in place — the reader never sees an unauthorized assertion standing as fact,
        // even before consulting the manifest below. The detailed result reports exactly which claims were
        // located and rewritten so the trace can flag any that could not be applied.
        var transform = Features.Intelligence.Epistemic.OutputProseTransformer.TransformDetailed(
            finalAnswer, audit.Authorizations);

        var answer = preface + "\n\n" + transform.Text;
        return new OutputEnforcementResult(
            answer, transform.AppliedClaimIds, transform.RequiredCount, transform.AppliedCount);
    }

    // Render the POLOXI-decided claim dispositions (QUALIFY/SUPPRESS/CORRECT) as an explicit manifest.
    // ALLOW claims need no annotation. Returns empty when nothing requires restatement.
    private static string BuildDispositionManifest(
        IReadOnlyList<Features.Intelligence.Epistemic.OutputClaimAuthorization> authorizations)
    {
        var actionable = authorizations
            .Where(a => a.Disposition != Features.Intelligence.Epistemic.OutputClaimDisposition.Allow)
            .ToList();
        if (actionable.Count == 0)
            return string.Empty;

        var lines = new List<string> { "POLOXI claim authorization (composer must honor these dispositions):" };
        foreach (var a in actionable)
        {
            var code = Features.Intelligence.Epistemic.OutputClaimDispositions.ToCode(a.Disposition);
            var text = a.Disposition switch
            {
                Features.Intelligence.Epistemic.OutputClaimDisposition.Qualify =>
                    string.IsNullOrWhiteSpace(a.ClaimText)
                        ? "unresolved proposition — state as uncertainty, not as fact."
                        : $"\"{Truncate(a.ClaimText)}\" — state as uncertainty (unresolved), not as fact.",
                Features.Intelligence.Epistemic.OutputClaimDisposition.Suppress =>
                    string.IsNullOrWhiteSpace(a.ClaimText)
                        ? "unsupported/foreign assertion — must not appear as an assertion."
                        : $"\"{Truncate(a.ClaimText)}\" — unsupported/foreign; must not appear as an assertion.",
                Features.Intelligence.Epistemic.OutputClaimDisposition.Correct =>
                    string.IsNullOrWhiteSpace(a.ClaimText)
                        ? "contradicted by authoritative state — remove or correct."
                        : $"\"{Truncate(a.ClaimText)}\" — contradicted by authoritative state; remove or correct.",
                _ => a.Reason,
            };
            lines.Add($"  • [{code}] {text}");
        }

        return string.Join("\n", lines);
    }

    private static string Truncate(string value, int max = 160)
        => value.Length <= max ? value : value[..max].TrimEnd() + "…";

    private static DecisionSearchResponse BuildResponse(DecisionSessionPersistence p, DecisionNextActionDto? nextAction, IReadOnlyCollection<DecisionReadinessItemDto> readiness,
        bool usedDependencyGraph = false, DecisionV2Result? v2 = null, Features.Intelligence.Decision.DecisionGovernanceVerdictDto? governanceVerdict = null, string? graphDiagnostic = null, Features.Intelligence.Decision.DecisionSolverShadowDto? solverShadow = null)
        => new(
            p.DecisionSessionId, p.QueryText, p.StatusCode, p.TerminalStateCode, p.TerminationReason,
            p.DepthReached, p.LlmCallCount, p.CandidateEntropy, p.DecisionMargin, p.ContractCompleteness,
            p.FinalAnswer, p.WinnerCandidateId,
            p.Candidates.Select(c => new DecisionCandidateDto(c.DecisionCandidateId, c.CandidateCode, c.DisplayName, c.Outcome, c.LegalSupport, c.FactSupport, c.EvidenceSupport, c.AuthoritySupport, c.Verification, c.Uncertainty, c.Discrimination, c.RankingImpact, c.Diversity, c.RedundancyPenalty, c.CompositeScore, c.DecisionSupportCeiling, c.RankOrder, c.IsWinner, c.IsEliminated)).ToArray(),
            p.Branches.Select(MapBranch).ToArray(),
            p.Evidence.Select(e => new DecisionEvidenceDto(e.DecisionEvidenceId, e.DecisionBranchId, e.SourceRef, e.SourceTitle, e.Snippet, e.VerificationValue, e.VerificationStatus)
            {
                SupportedObjective = e.SupportedObjective,
                SupportingPassage = e.SupportingPassage,
                LifecycleState = e.LifecycleState
            }).ToArray(),
            p.FlipPoints.Select(f => new DecisionFlipPointDto(f.DecisionFlipPointId, f.DecisionBranchId, f.Description, f.ChangeCost, f.WinnerChanges, f.RankDelta)
            {
                TargetCandidateId = f.TargetCandidateId,
                TargetCandidateCode = f.TargetCandidateCode,
                PolarityCode = f.PolarityCode,
            }).ToArray(),
            p.DurationMs)
        {
            ClarificationQuestion = p.ClarificationQuestion,
            ClarificationTarget = p.ClarificationTarget,
            MatterId = p.MatterId,
            CounterfactualAssumption = p.CounterfactualAssumption,
            NextBestAction = nextAction,
            Readiness = readiness,
            DomainGuardrails = BuildDomainGuardrailSummary(p.Branches),
            UsedDependencyGraph = usedDependencyGraph,
            GraphDiagnostic = graphDiagnostic,
            GraphNodes = v2?.Nodes ?? [],
            GraphEdges = v2?.Edges ?? [],
            LosingSideTest = v2?.LosingSideTest,
            ReadinessVerdict = v2?.ReadinessVerdict,
            GovernanceVerdict = governanceVerdict,
            SolverShadow = solverShadow
        };

    private static DecisionBranchDto MapBranch(DecisionBranchPersistence branch) =>
        new(branch.DecisionBranchId, branch.ParentDecisionBranchId, branch.LevelNumber, branch.BranchCode,
            branch.DisplayName, branch.Interpretation, branch.BranchStateCode, branch.InformationValue,
            branch.DecisionRelevance, branch.FlipPotential, branch.EvidenceAvailability, branch.AdvScore,
            branch.IsOnFrontier, branch.StopReason, branch.SortOrder)
        {
            GenerationOriginCode = branch.GenerationOriginCode,
            DecisionDomainConceptId = branch.DecisionDomainConceptId,
            DomainConceptCode = branch.DomainConceptCode,
            GuardrailMatchScore = branch.GuardrailMatchScore,
            GuardrailActionCode = branch.GuardrailActionCode,
            GuardrailVersion = branch.GuardrailVersion,
        };

    private static DecisionDomainGuardrailSummaryDto? BuildDomainGuardrailSummary(
        IReadOnlyCollection<DecisionBranchPersistence> branches)
    {
        var governed = branches.Where(branch => !string.IsNullOrWhiteSpace(branch.GuardrailActionCode)).ToArray();
        if (governed.Length == 0)
            return null;

        return new DecisionDomainGuardrailSummaryDto(
            DynamicBranchCount: branches.Count(branch => !string.Equals(branch.GenerationOriginCode,
                DecisionBranchGenerationOrigins.DomainFallback, StringComparison.OrdinalIgnoreCase)),
            EnrichedBranchCount: branches.Count(branch => string.Equals(branch.GenerationOriginCode,
                DecisionBranchGenerationOrigins.DynamicLlmEnriched, StringComparison.OrdinalIgnoreCase)),
            NovelBranchCount: branches.Count(branch => string.Equals(branch.GuardrailActionCode,
                DecisionGuardrailActions.NovelAccepted, StringComparison.OrdinalIgnoreCase)),
            DormantFallbackCount: branches.Count(branch => string.Equals(branch.GenerationOriginCode,
                DecisionBranchGenerationOrigins.DomainFallback, StringComparison.OrdinalIgnoreCase)),
            AppliedConceptCodes: branches
                .Where(branch => !string.IsNullOrWhiteSpace(branch.DomainConceptCode))
                .Select(branch => branch.DomainConceptCode!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(code => code, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            PolicyCode: "DYNAMIC_PRIMARY_ADVISORY_GUARDRAILS");
    }

    // ── Next Best Action: pick the open (frontier / active) branch with the highest information value.
    // Impact class scales with flip potential — a branch that can overturn the winner is VERY HIGH. ──
    private static DecisionNextActionDto? BuildNextBestAction(IReadOnlyCollection<DecisionBranchPersistence> branches, string statusCode)
    {
        var candidate = branches
            .Where(b => b.IsOnFrontier || string.Equals(b.BranchStateCode, DecisionBranchStates.Active, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(b => b.InformationValue)
            .ThenByDescending(b => b.FlipPotential)
            .FirstOrDefault();

        // Clarification takes priority: when the run is blocked on an ESSENTIAL matter-specific fact or
        // document the attorney must supply, the next action is to provide it (a user-clarification need),
        // NOT a researchable legal question. Prefer the highest-flip pivot the user can actually resolve so
        // the panel and the clarification prompt point at the same blocking dependency.
        if (statusCode == DecisionStatusCodes.UserClarificationRequired)
        {
            var pivot = branches.Where(b => b.IsOnFrontier && IsUserResolvableClarification(b))
                .OrderByDescending(b => b.FlipPotential).FirstOrDefault()
                ?? branches.Where(IsUserResolvableClarification).OrderByDescending(b => b.FlipPotential).FirstOrDefault()
                ?? candidate;
            if (pivot is not null)
                return new DecisionNextActionDto(
                    $"Provide the missing information: {pivot.DisplayName}",
                    "VERY HIGH",
                    $"The decision is blocked on a matter-specific input only you can supply. Identify or upload the facts/documents that establish '{pivot.DisplayName}'. This is a clarification need, not a researchable legal question \u2014 resolving it lets the competing outcomes be ranked.",
                    pivot.DecisionBranchId,
                    pivot.InformationValue,
                    pivot.FlipPotential);
        }

        // When the decision is ready, or nothing remains on the frontier, surface a
        // confirm/monitor action instead of hiding the panel so the attorney always has a
        // clear next step (e.g. lock in the conclusion, monitor for new facts).
        if (statusCode == DecisionStatusCodes.DecisionReady || candidate is null)
            return new DecisionNextActionDto(
                "Confirm and monitor the current conclusion",
                "LOW",
                "The decision is ready: no open item on the frontier can currently overturn the winner. Lock in the conclusion and monitor for new facts or authority that could reopen the analysis.",
                candidate?.DecisionBranchId,
                candidate?.InformationValue ?? 0m,
                candidate?.FlipPotential ?? 0m);

        var impact = candidate.FlipPotential switch
        {
            >= 0.66m => "VERY HIGH",
            >= 0.40m => "HIGH",
            >= 0.20m => "MEDIUM",
            _ => "LOW"
        };
        var rationale = candidate.FlipPotential >= 0.40m
            ? $"Resolving '{candidate.DisplayName}' could overturn the current winner; it has the highest remaining information value on the decision frontier."
            : $"'{candidate.DisplayName}' carries the highest remaining information value and best reduces residual uncertainty.";
        return new DecisionNextActionDto(
            $"Investigate {candidate.DisplayName}",
            impact,
            rationale,
            candidate.DecisionBranchId,
            candidate.InformationValue,
            candidate.FlipPotential);
    }

    // ── Decision readiness checklist: shown separately from outcome strength. Each item is derived
    // deterministically from the current authoritative state (not an LLM opinion). ──
    private static IReadOnlyCollection<DecisionReadinessItemDto> BuildReadiness(
        IReadOnlyCollection<DecisionCandidatePersistence> candidates,
        IReadOnlyCollection<DecisionBranchPersistence> branches,
        IReadOnlyCollection<DecisionEvidencePersistence> evidence,
        double margin, double entropy, string statusCode, string researchStatus)
    {
        var winner = candidates.OrderBy(c => c.RankOrder).FirstOrDefault();
        var alternative = candidates.OrderBy(c => c.RankOrder).Skip(1).FirstOrDefault();
        // Authoritative verified-source rule: VerificationStatus == VERIFIED decides verified support.
        // The multiplicative VerificationValue factors are retained only as diagnostics and must not
        // define readiness (see §7 clarification-gate diagnostics at line ~188 which use the same rule).
        var verifiedEvidence = evidence.Count(e => string.Equals(e.VerificationStatus, DecisionVerificationStates.Verified, StringComparison.OrdinalIgnoreCase));
        // Single authoritative high-impact frontier count (POLOXI owns the frontier). V2 readiness
        // consumes this same definition so the two panels can never disagree.
        var openFrontier = CountHighImpactFrontier(branches);
        // Winner-separation is decided deterministically on the UNROUNDED margin against a single
        // authoritative threshold. The detail line exposes higher precision so a value that rounds to
        // "0.05" in a two-decimal display can never look like it contradicts the separation label
        // (e.g. 0.0497 vs 0.052 both display as "0.05" but sit on opposite sides of the threshold).
        const double separationThreshold = 0.05;
        var marginSeparates = margin >= separationThreshold;
        var uncertaintyContained = entropy < 0.85;
        // Evidence readiness reports WHY it failed using the explicit research state (§13), so a user can
        // tell "we searched and found nothing" from "our retrieval operation failed" from "nothing was
        // retrieved that could be verified" rather than always seeing a bare "0 verified source(s)".
        var evidenceDetail = verifiedEvidence > 0
            ? $"{verifiedEvidence} verified source(s)"
            : researchStatus switch
            {
                DecisionResearchStates.RetrievalFailed => "Retrieval operation failed; no external evidence could be verified",
                DecisionResearchStates.SearchNoResults => "Search returned no sources to verify",
                DecisionResearchStates.RequiredPending => "Research required but unresolved; authoritative Research Need retrieval has not produced verified evidence",
                DecisionResearchStates.NotNeeded => "No external research was required for this decision",
                _ => "Retrieved sources did not meet verification requirements (0 verified)"
            };
        return new[]
        {
            new DecisionReadinessItemDto("A leading outcome is identified", winner is not null, winner?.DisplayName),
            new DecisionReadinessItemDto(
                marginSeparates ? "Winner separates from the alternative" : "Winner does not clearly separate from the alternative",
                marginSeparates, $"Decision margin {margin:0.####} (threshold {separationThreshold:0.####})"),
            new DecisionReadinessItemDto(
                uncertaintyContained ? "Uncertainty is contained" : "Uncertainty is not contained",
                uncertaintyContained, $"Candidate entropy {entropy:0.####}"),
            new DecisionReadinessItemDto("Strongest opposition considered", alternative is not null, alternative?.DisplayName),
            new DecisionReadinessItemDto("Supporting evidence verified", verifiedEvidence > 0, evidenceDetail),
            new DecisionReadinessItemDto(
                openFrontier == 0 ? "No high-impact unresolved dependency" : $"{openFrontier} high-impact unresolved dependency",
                openFrontier == 0,
                openFrontier == 0 ? null : "Resolve open frontier branches before final reliance")
        };
    }

    // The single authoritative "high-impact open frontier" definition. POLOXI owns the decision
    // frontier; both the ordinary readiness panel and the V2 dependency-readiness gate consume this
    // same count so they can never independently reconstruct (and disagree about) frontier state.
    internal static int CountHighImpactFrontier(IReadOnlyCollection<DecisionBranchPersistence> branches)
        => branches.Count(b => b.IsOnFrontier && b.FlipPotential >= 0.40m);

    // ── POLOXI Legal V2 — dependency-aware decision graph orchestration ──────────────────────────
    // Proposes a typed graph (DECISION_GRAPH), runs an INDEPENDENT verifier (DECISION_VERIFY),
    // propagates invalidation deterministically in Core, runs the strongest-losing-side gate, and
    // computes the dependency-constrained readiness verdict. Everything scored/decided here is Core.
    // Builds a compact posture context line from the structured matter posture fields, injected
    // into the DECISION_GRAPH proposal so the typed graph reflects the decision being asked NOW.
    private static string BuildPostureContext(DecisionSearchRequest request)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(request.Posture))
            parts.Add($"Procedural posture: {request.Posture.Trim()}.");
        if (!string.IsNullOrWhiteSpace(request.MotionTarget))
            parts.Add($"Motion target: {request.MotionTarget.Trim()}.");
        return parts.Count == 0 ? string.Empty : "Decision posture context — " + string.Join(" ", parts);
    }

    private async Task<DecisionV2Result> RunDependencyGraphAsync(
        DecisionSearchRequest request,
        Guid sessionId,
        string contextCode,
        string effectiveQuery,
        IReadOnlyList<DecisionCandidatePersistence> candidates,
        IReadOnlyList<DecisionBranchPersistence> branches,
        DecisionV2Settings v2Settings,
        DecisionCandidatePersistence? winner,
        CancellationToken cancellationToken)
    {
        // 1. Graph proposal (LLM proposes typed nodes/edges; Core assigns identity and owns state).
        var graphRoute = await ResolveRouteAsync(GraphPromptCode, request.ModelCode, cancellationToken);
        var graphPrompt = await repository.GetPromptAsync(GraphPromptCode, cancellationToken)
            ?? throw new InvalidOperationException($"The '{GraphPromptCode}' decision prompt is not configured.");
        var candidateArtifact = JsonSerializer.Serialize(new
        {
            candidates = candidates.Select(c => new { code = c.CandidateCode, c.DisplayName, c.Outcome, c.CompositeScore }),
            branches = branches
                .Where(static branch => branch.IsExecutable)
                .Select(b => new { b.BranchCode, b.DisplayName, b.Interpretation, b.FlipPotential })
        });
        // Structured posture context (from the matter) grounds the proposal in the decision being
        // asked NOW: which motion, and what it targets. Appended only; empty when not supplied.
        var postureContext = BuildPostureContext(request);
        var graphQuery = string.IsNullOrEmpty(postureContext) ? effectiveQuery : $"{effectiveQuery}\n\n{postureContext}";
        var graphUser = graphPrompt.UserPromptTemplate
            .Replace("{{QUERY}}", graphQuery)
            .Replace("{{CONTEXT}}", contextCode)
            .Replace("{{ARTIFACT}}", candidateArtifact);
        var graphResult = await aiProvider.GenerateAsync(
            new DecisionAiRequest(graphRoute, "DECISION_GRAPH", graphPrompt.SystemPrompt, graphUser, graphPrompt.OutputSchemaJson, request.CorrelationId),
            cancellationToken);

        var (model, nodeCodeToId, edgeCodeToId) = ParseGraphProposal(graphResult.StructuredOutputJson ?? graphResult.Content, candidates);
        if (model.Nodes.Count == 0)
            throw new InvalidOperationException("The V2 graph proposal returned no nodes.");

        // 2. Independent verification: a distinct role assesses each edge; Core applies the verdicts.
        var verifyRoute = await ResolveRouteAsync(VerifyPromptCode, request.ModelCode, cancellationToken);
        var verifyPrompt = await repository.GetPromptAsync(VerifyPromptCode, cancellationToken)
            ?? throw new InvalidOperationException($"The '{VerifyPromptCode}' decision prompt is not configured.");
        var edgeArtifact = JsonSerializer.Serialize(new
        {
            edges = model.Edges.Select(e => new
            {
                edgeCode = edgeCodeToId.First(kv => kv.Value == e.Id).Key,
                e.Relation,
                source = NodeLabel(model, e.SourceId),
                target = NodeLabel(model, e.TargetId),
                e.IsEssential,
                e.IsDispositive
            })
        });
        var verifyUser = verifyPrompt.UserPromptTemplate
            .Replace("{{ARTIFACT}}", edgeArtifact)
            .Replace("{{QUERY}}", effectiveQuery);
        var verifyResult = await aiProvider.GenerateAsync(
            new DecisionAiRequest(verifyRoute, "DECISION_VERIFY", verifyPrompt.SystemPrompt, verifyUser, verifyPrompt.OutputSchemaJson, request.CorrelationId),
            cancellationToken);
        ApplyVerification(verifyResult.StructuredOutputJson ?? verifyResult.Content, model, edgeCodeToId);

        // 3. Deterministic Core: recompute node support, then propagate any invalidation downstream.
        foreach (var node in model.Nodes.Values)
            DecisionGraph.RecomputeSupport(model, node);
        DecisionGraph.PropagateInvalidation(model, v2Settings.PropagationMaxDepth);

        // 4. Strongest-losing-side gate: winner must survive the strongest permissible opposing candidate.
        var losingSide = BuildLosingSideTest(candidates, winner);

        // 5. Dependency-constrained readiness verdict (hard gate; margin/entropy are not inputs).
        var openHighImpact = CountHighImpactFrontier(branches);
        var verdict = DecisionGraph.EvaluateReadiness(
            model,
            winnerExists: winner is not null,
            authorityVerifiedFractionRequired: v2Settings.ReadinessMinAuthorityVerified,
            maxHighImpactFrontier: v2Settings.ReadinessMaxHighImpactFrontier,
            openHighImpactFrontierCount: openHighImpact,
            losingSide: losingSide,
            losingSideMargin: v2Settings.ReadinessLosingSideMargin);

        // 6. Map the working model back to persistence + DTOs.
        var nodeSnapshots = model.Nodes.Values
            .OrderBy(n => n.Kind).ThenBy(n => n.SortOrder)
            .Select(n => new DecisionGraphNodePersistence(
                n.Id, n.Kind, n.Code, n.DisplayName, n.Statement, (decimal)n.Support,
                n.IsEssential, n.IsSatisfied, n.VerificationStatus, n.SortOrder)
            {
                CandidateId = n.Kind == DecisionGraphNodeKinds.Strategy ? ResolveStrategyCandidate(n, candidates) : null,
                AuthorityRef = n.AuthorityRef,
                BurdenedParty = n.BurdenedParty,
                StandardOfProof = n.StandardOfProof
            })
            .ToArray();
        var edgeSnapshots = model.Edges
            .Select(e => new DecisionGraphEdgePersistence(
                e.Id, e.Relation, e.SourceKind, e.SourceId, e.TargetKind, e.TargetId,
                (decimal)e.SupportWeight, (decimal)e.Materiality, e.IsEssential, e.IsDispositive,
                e.VerificationStatus, null, e.PropagatedStateCode))
            .ToArray();

        DecisionLosingSideTestPersistence? losingPersistence = losingSide is null ? null : new(
            Guid.NewGuid(), winner?.DecisionCandidateId,
            losingSide.ChallengerCandidateId, losingSide.StrongestCaseSummary,
            losingSide.ChallengerStrength, losingSide.WinnerStrength, losingSide.WinnerSurvived);

        var blockersJson = JsonSerializer.Serialize(verdict.Blockers);
        var persistence = new DecisionGraphPersistence(
            sessionId, request.TenantId, request.UserId, verdict.Satisfied, blockersJson,
            nodeSnapshots, edgeSnapshots, losingPersistence);

        var nodeDtos = nodeSnapshots
            .Select(n => new DecisionGraphNodeDto(n.NodeId, n.NodeKind, n.NodeCode, n.DisplayName, n.Statement, n.Support, n.IsEssential, n.IsSatisfied, n.VerificationStatus, n.SortOrder))
            .ToArray();
        var edgeDtos = edgeSnapshots
            .Select(e => new DecisionGraphEdgeDto(e.EdgeId, e.RelationCode, e.SourceNodeKind, e.SourceNodeId, e.TargetNodeKind, e.TargetNodeId, e.SupportWeight, e.Materiality, e.IsEssential, e.IsDispositive, e.VerificationStatus, e.VerificationNotes, e.PropagatedStateCode))
            .ToArray();

        return new DecisionV2Result(persistence, nodeDtos, edgeDtos, losingSide, verdict);
    }

    // Parse the DECISION_GRAPH proposal into a Core working model, assigning deterministic ids.
    private static (DecisionGraph.Model Model, Dictionary<string, Guid> NodeCodeToId, Dictionary<string, Guid> EdgeCodeToId) ParseGraphProposal(string json, IReadOnlyList<DecisionCandidatePersistence> candidates)
    {
        var model = new DecisionGraph.Model();
        var nodeCodeToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var edgeCodeToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        // Seed candidate nodes so strategy→candidate edges have a valid target.
        var candidateCodeToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in candidates)
        {
            model.Nodes[c.DecisionCandidateId] = new DecisionGraph.Node
            {
                Id = c.DecisionCandidateId,
                Kind = DecisionGraphNodeKinds.Candidate,
                Code = c.CandidateCode,
                DisplayName = c.DisplayName,
                Support = (double)c.CompositeScore,
                IsSatisfied = c.IsWinner,
                VerificationStatus = DecisionVerificationStates.Unverified
            };
            candidateCodeToId[c.CandidateCode] = c.DecisionCandidateId;
            nodeCodeToId[c.CandidateCode] = c.DecisionCandidateId;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var sort = 0;
            if (root.TryGetProperty("nodes", out var nodesNode) && nodesNode.ValueKind == JsonValueKind.Array)
                foreach (var n in nodesNode.EnumerateArray())
                {
                    var code = GetString(n, "code");
                    if (string.IsNullOrWhiteSpace(code) || nodeCodeToId.ContainsKey(code))
                        continue;
                    var kind = MapNodeKind(GetString(n, "kind"));
                    if (kind is null)
                        continue;
                    var id = Guid.NewGuid();
                    var node = new DecisionGraph.Node
                    {
                        Id = id,
                        Kind = kind,
                        Code = code,
                        DisplayName = string.IsNullOrWhiteSpace(GetString(n, "displayName")) ? code : GetString(n, "displayName"),
                        Statement = GetString(n, "statement"),
                        Support = DecisionCoreMath.Clamp01(GetNumber(n, "support")),
                        IsEssential = GetBool(n, "isEssential"),
                        VerificationStatus = DecisionVerificationStates.Unverified,
                        SortOrder = sort++,
                        AuthorityRef = NullIfBlank(GetString(n, "authorityRef")),
                        BurdenedParty = NullIfBlank(GetString(n, "burdenedParty")),
                        StandardOfProof = NullIfBlank(GetString(n, "standardOfProof"))
                    };

                    // Normalize → validate → repair-if-bounded → drop-invalid. The LLM proposes; POLOXI
                    // governs: a malformed node must never reach SQL as a constraint violation. A burden
                    // rule requires a BurdenedParty; recover it from an obvious source or drop the node.
                    if (!TryNormalizeAndValidateNode(node))
                    {
                        sort--;
                        continue;
                    }

                    model.Nodes[id] = node;
                    nodeCodeToId[code] = id;
                }

            if (root.TryGetProperty("edges", out var edgesNode) && edgesNode.ValueKind == JsonValueKind.Array)
                foreach (var e in edgesNode.EnumerateArray())
                {
                    var sourceCode = GetString(e, "sourceCode");
                    var targetCode = GetString(e, "targetCode");
                    if (!nodeCodeToId.TryGetValue(sourceCode, out var sourceId) || !nodeCodeToId.TryGetValue(targetCode, out var targetId))
                        continue;
                    var relation = MapRelation(GetString(e, "relation"));
                    if (relation is null)
                        continue;
                    var id = Guid.NewGuid();
                    model.Edges.Add(new DecisionGraph.Edge
                    {
                        Id = id,
                        Relation = relation,
                        SourceKind = model.Nodes[sourceId].Kind,
                        SourceId = sourceId,
                        TargetKind = model.Nodes[targetId].Kind,
                        TargetId = targetId,
                        SupportWeight = DecisionCoreMath.Clamp01(GetNumberOrDefault(e, "supportWeight", 0.6)),
                        Materiality = DecisionCoreMath.Clamp01(GetNumberOrDefault(e, "materiality", 0.5)),
                        IsEssential = GetBool(e, "isEssential"),
                        IsDispositive = GetBool(e, "isDispositive"),
                        VerificationStatus = DecisionVerificationStates.Unverified
                    });
                    edgeCodeToId[$"{sourceCode}->{targetCode}"] = id;
                }
        }
        catch (JsonException)
        {
            // Malformed proposal ⇒ empty graph; the caller treats this as a V2 failure and falls back.
        }

        return (model, nodeCodeToId, edgeCodeToId);
    }

    private static void ApplyVerification(string json, DecisionGraph.Model model, Dictionary<string, Guid> edgeCodeToId)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("assessments", out var assessments) || assessments.ValueKind != JsonValueKind.Array)
                return;
            foreach (var a in assessments.EnumerateArray())
            {
                var edgeCode = GetString(a, "edgeCode");
                if (!edgeCodeToId.TryGetValue(edgeCode, out var edgeId))
                    continue;
                var status = GetString(a, "status").Trim().ToUpperInvariant();
                var edge = model.Edges.FirstOrDefault(e => e.Id == edgeId);
                if (edge is null)
                    continue;
                edge.VerificationStatus = status == DecisionVerificationStates.Invalidated
                    ? DecisionVerificationStates.Invalidated
                    : DecisionVerificationStates.Verified;
            }
        }
        catch (JsonException)
        {
            // No verification opinion ⇒ edges stay UNVERIFIED; readiness will block on material authority.
        }
    }

    // Strongest-losing-side: the strongest non-winner candidate becomes the challenger; the winner
    // survives only if its composite strength exceeds the challenger's (Core owns the comparison).
    private static DecisionLosingSideTestDto? BuildLosingSideTest(IReadOnlyList<DecisionCandidatePersistence> candidates, DecisionCandidatePersistence? winner)
    {
        if (winner is null)
            return null;
        var challenger = candidates
            .Where(c => c.DecisionCandidateId != winner.DecisionCandidateId)
            .OrderByDescending(c => c.CompositeScore)
            .FirstOrDefault();
        var winnerStrength = winner.CompositeScore;
        var challengerStrength = challenger?.CompositeScore ?? 0m;
        var survived = winnerStrength > challengerStrength;
        var summary = challenger is null
            ? "No opposing candidate; winner is uncontested."
            : $"Strongest opposing outcome '{challenger.DisplayName}' ({challenger.Outcome}); winner leads by {winnerStrength - challengerStrength:0.00}.";
        return new DecisionLosingSideTestDto(challenger?.DecisionCandidateId, summary, challengerStrength, winnerStrength, survived);
    }

    private static string NodeLabel(DecisionGraph.Model model, Guid nodeId)
        => model.Nodes.TryGetValue(nodeId, out var n) ? $"{n.Kind}:{n.DisplayName}" : nodeId.ToString();

    private static Guid? ResolveStrategyCandidate(DecisionGraph.Node strategy, IReadOnlyList<DecisionCandidatePersistence> candidates)
        => null;

    // Trims blank strings to null so optional attributes never persist as empty-but-not-null noise.
    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // Governance boundary for a single proposed node: normalize its kind-specific attributes, then
    // validate/repair. Returns false when the node cannot be made valid and must be dropped so it
    // never reaches SQL as a NOT NULL constraint violation (bounded local failure — the rest of the
    // graph is unaffected). Today the only hard requirement is Burden.BurdenedParty.
    private static bool TryNormalizeAndValidateNode(DecisionGraph.Node node)
    {
        if (node.Kind != DecisionGraphNodeKinds.Burden)
            return true;

        node.StandardOfProof = NullIfBlank(node.StandardOfProof);
        node.BurdenedParty = NullIfBlank(node.BurdenedParty);
        if (!string.IsNullOrWhiteSpace(node.BurdenedParty))
            return true;

        // Bounded repair: a burden rule's burdened party is often stated in the display name
        // ("Movant bears the burden…") or the statement. Recover it from an obvious source.
        var recovered = NullIfBlank(node.DisplayName);
        if (recovered is null && !string.IsNullOrWhiteSpace(node.Statement))
            recovered = NullIfBlank(node.Statement);

        if (recovered is null)
            return false; // still invalid → drop the node rather than persist a NULL burdened party.

        // BurdenedParty is NVARCHAR(120); truncate the recovered value to fit the column.
        node.BurdenedParty = recovered.Length > 120 ? recovered[..120] : recovered;
        return true;
    }

    private static string? MapNodeKind(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        "fact" => DecisionGraphNodeKinds.Fact,
        "proposition" => DecisionGraphNodeKinds.Proposition,
        "element" => DecisionGraphNodeKinds.Element,
        "strategy" => DecisionGraphNodeKinds.Strategy,
        "burden" => DecisionGraphNodeKinds.Burden,
        "procedure" => DecisionGraphNodeKinds.Procedure,
        _ => null
    };

    private static string? MapRelation(string relation) => relation.Trim().ToUpperInvariant() switch
    {
        DecisionGraphRelations.Supports => DecisionGraphRelations.Supports,
        DecisionGraphRelations.Requires => DecisionGraphRelations.Requires,
        DecisionGraphRelations.Satisfies => DecisionGraphRelations.Satisfies,
        DecisionGraphRelations.Establishes => DecisionGraphRelations.Establishes,
        DecisionGraphRelations.DependsOn => DecisionGraphRelations.DependsOn,
        DecisionGraphRelations.Contradicts => DecisionGraphRelations.Contradicts,
        _ => null
    };

    private static bool GetBool(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static double GetNumberOrDefault(JsonElement element, string name, double fallback)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : fallback;

    private sealed record DecisionV2Result(
        DecisionGraphPersistence Persistence,
        IReadOnlyCollection<DecisionGraphNodeDto> Nodes,
        IReadOnlyCollection<DecisionGraphEdgeDto> Edges,
        DecisionLosingSideTestDto? LosingSideTest,
        DecisionReadinessVerdictDto ReadinessVerdict);

    private static string GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static double GetNumber(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : 0d;

    private enum ProposalDisposition { Accept, Repair, Expand, Regenerate, Clarify, Degraded }

    // Three fundamentally different terminal situations, preserved in the domain model even though only
    // UnresolvedModelFailure is routed today (as the hard stop). Kept distinct so that, once shadow data
    // shows which defect classes deserve which outcome, routing can be added without redefining meaning:
    //   • UnresolvedModelFailure  : the model failed to produce an acceptable representation despite
    //                               bounded recovery. NOT the user's fault.
    //   • ClarificationRequired   : the query lacks information necessary to resolve the representation;
    //                               regeneration cannot safely fix it (e.g. QUERY_UNDERSPECIFIED persists).
    //   • DegradedProposal        : a usable but incomplete representation — remaining structure is
    //                               sufficient for controlled continuation without pretending the missing
    //                               component was resolved.
    private enum ProposalTerminalSituation { UnresolvedModelFailure, ClarificationRequired, DegradedProposal }
    private sealed record ProposalIntegrityResult(bool IsAcceptable, string Reason);
    private sealed record ProposalIntegrityDiagnostics(
        bool StructuralValid,
        int CandidateCount,
        int DistinctCandidateCount,
        int InterpretationCount,
        int EmptyInterpretationCount,
        int DuplicateInterpretationCount,
        double QueryFidelity,
        double InterpretationDistinctness,
        double InterpretationCoverage,
        double CandidateSeparability,
        bool RecoveryTriggered,
        string? RecoveryReason,
        bool? RecoverySucceeded);
    private sealed record ProposedCandidate(string DisplayName, string Outcome, double LegalSupport, double FactSupport, double EvidenceSupport, double AuthoritySupport, double Verification, double Discrimination, double RankingImpact, IReadOnlyList<ProposedBranch> Branches);
    internal sealed record ProposedBranch(string DisplayName, string Interpretation, double DecisionRelevance, double FlipPotential, double EvidenceAvailability, IReadOnlyList<ProposedBranch> Children);
}
