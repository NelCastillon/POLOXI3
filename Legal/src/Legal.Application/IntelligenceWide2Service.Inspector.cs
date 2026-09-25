using Legal.Application.Features.Intelligence;

namespace Legal.Application;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// R5 — Legal Decision Processing Inspector builder.
//
// Projects the ACTUAL captured run state of the dynamic PI pipeline into the structured
// WideDecisionInspectorDto so the UI can show, for every processing stage, exactly what happened:
// inputs, required/available/missing factors, validation, processing performed, outputs, and blocking
// reasons. It is diagnostic-only and reads exclusively from state the run already produced — it makes
// no additional LLM call and never reconstructs a stage silently. Where a stage was not reached or a
// value was not captured, it renders an explicit MISSING / NOT_REACHED marker rather than a placeholder.
// Returns null on any run that is not a matter-backed legal EVALUATE run (the inspector is legal-specific).
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed partial class IntelligenceWide2Service
{
    private const string StatusCompleted = "COMPLETED";
    private const string StatusIncomplete = "INCOMPLETE";
    private const string StatusBlocked = "BLOCKED";
    private const string StatusMissing = "MISSING";
    private const string StatusNotReached = "NOT_REACHED";

    internal WideDecisionInspectorDto? BuildDecisionInspector(
        string? answerKindCode,
        IReadOnlyCollection<WideInterpretiveResultDto> interpretiveResults,
        IReadOnlyCollection<WideCandidateDto> deliveredCandidates)
    {
        // The inspector is legal-decision specific. A non-legal / non-EVALUATE run never reaches the gate,
        // so surfacing an inspector there would be misleading — return null and let the UI hide it.
        if (!IsLegalDecisionEvaluationRun)
            return null;

        var executionMode = new WideInspectorExecutionModeDto(
            ExecutionRoute: "IntelligenceWide2Service.SearchDynamicAsync (dynamic Wide2 pipeline)",
            AnswerKind: string.IsNullOrWhiteSpace(answerKindCode) ? null : answerKindCode,
            IsLegalDecisionEvaluationRun: true,
            GateMode: _gateMode ?? StatusNotReached,
            NormalizationMode: _normalizationGateStatus,
            // The dynamic Wide2 hierarchy is always built from the DB-backed WIDE_INTENT (Level 1) and
            // WIDE_HIERARCHY_STEP (Level 2+) prompts — this is a deterministic property of the pipeline,
            // not an inferred value, so reporting the composite prompt key is factual. The concrete
            // version/hash are resolved per tenant by the prompt catalog at dispatch time and are not
            // returned to the caller, so they remain MISSING rather than being fabricated.
            PromptKey: $"{IntelligencePromptCodes.WideIntent} + {IntelligencePromptCodes.WideHierarchyStep}",
            PromptVersion: null,
            PromptHash: null);

        var normalizedCandidates = BuildInspectorCandidates(deliveredCandidates);
        var sharedDependencies = BuildInspectorDependencies();
        var rejected = BuildInspectorRejected();
        var stages = BuildInspectorStages(interpretiveResults, deliveredCandidates, normalizedCandidates, sharedDependencies);

        // Universal Factor Inventory: projected from the SAME normalized plan + matter context. Null-safe;
        // absent when no validated plan exists so the UI hides the panel rather than showing empty data.
        var factorInventory = BuildFactorInventory(deliveredCandidates);

        return new WideDecisionInspectorDto(executionMode, stages, normalizedCandidates, sharedDependencies, rejected)
        {
            FactorInventory = factorInventory,
        };
    }

    // §4 — normalized candidate rows straight from the registration plan. ReachedScoring is TRUE only when
    // the delivered (competed) candidate set actually contains the normalized representative name.
    private IReadOnlyCollection<WideInspectorCandidateDto> BuildInspectorCandidates(
        IReadOnlyCollection<WideCandidateDto> deliveredCandidates)
    {
        if (_legalNormalization is null)
            return [];

        var scored = deliveredCandidates
            .Where(c => !c.IsConstraintViolation)
            .Select(c => c.DisplayName.Trim());
        return ProjectInspectorCandidates(_legalNormalization.Plan, scored);
    }

    // Deterministic, instance-free projection of a validated registration plan into candidate inspector
    // rows. Extracted so it can be unit-tested directly from the shared gate fixtures without building
    // the full service. ReachedScoring is TRUE only when an eligible candidate name is in scoredNames.
    internal static IReadOnlyCollection<WideInspectorCandidateDto> ProjectInspectorCandidates(
        LegalDecisionService.LegalDecisionRegistrationPlan plan,
        IEnumerable<string> scoredNames)
    {
        var scored = new HashSet<string>(
            scoredNames.Select(n => n.Trim()),
            StringComparer.OrdinalIgnoreCase);

        var rows = new List<WideInspectorCandidateDto>();
        foreach (var c in plan.Candidates)
        {
            var decision = c.Decision.ToString();
            var reason = c.Decision switch
            {
                LegalDecisionService.NormalizationDecision.Merge =>
                    $"Merged: shares material legal identity ({c.Identity.Signature}) with {Math.Max(0, c.MergedSemanticIds.Count - 1)} other outcome(s).",
                LegalDecisionService.NormalizationDecision.RequiresReview =>
                    "Requires review: material identity overlap could not be deterministically resolved; withheld from competition.",
                _ =>
                    $"Kept distinct: material legal identity ({c.Identity.Signature}) is uniquely settled.",
            };
            var eligibility = c.EligibleToCompete;
            var reachedScoring = eligibility && scored.Contains(c.DisplayName.Trim());
            rows.Add(new WideInspectorCandidateDto(
                NormalizedCandidateId: c.RepresentativeSemanticId,
                OriginalTitle: c.DisplayName,
                SemanticRole: c.Role.ToString(),
                IdentityDecision: decision,
                MaterialDistinction: c.Identity.Signature,
                OriginMappings: c.MergedSemanticIds.ToArray(),
                OriginatingOutcomeNodeIds: c.OriginatingOutcomeNodeIds.ToArray(),
                CompetitionEligible: eligibility,
                ReachedScoring: reachedScoring,
                DecisionReason: reason));
        }
        return rows;
    }

    // §5 — each shared dependency appears ONCE, with the list of candidate ids that depend on it (never
    // duplicated beneath each outcome). VerificationState reflects that the dynamic pipeline registers
    // dependencies semantically but does not attach verified evidence per-dependency in this path.
    private IReadOnlyCollection<WideInspectorDependencyDto> BuildInspectorDependencies()
    {
        if (_legalNormalization is null)
            return [];
        return ProjectInspectorDependencies(_legalNormalization.Plan);
    }

    // Deterministic, instance-free projection of shared dependencies. Each dependency appears once with
    // the distinct list of candidate ids that depend on it (never duplicated beneath each outcome).
    internal static IReadOnlyCollection<WideInspectorDependencyDto> ProjectInspectorDependencies(
        LegalDecisionService.LegalDecisionRegistrationPlan plan)
    {
        var edgesByDependency = plan.Edges
            .GroupBy(e => e.DependencyId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

        var rows = new List<WideInspectorDependencyDto>();
        foreach (var d in plan.Dependencies)
        {
            edgesByDependency.TryGetValue(d.DependencyId, out var edges);
            var related = (edges ?? [])
                .Select(e => e.CandidateSemanticId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var relationType = (edges ?? []).Select(e => e.RelationType).FirstOrDefault() ?? "DEPENDS_ON";
            rows.Add(new WideInspectorDependencyDto(
                DependencyId: d.DependencyId,
                DependencyType: d.Category.ToString(),
                Proposition: string.IsNullOrWhiteSpace(d.Question) ? d.Label : d.Question!,
                RelatedCandidateIds: related,
                RelationType: relationType,
                VerificationState: "REGISTERED_SEMANTIC (no per-dependency evidence attached on the dynamic path)",
                UnresolvedInformation: string.IsNullOrWhiteSpace(d.Question) ? null : d.Question));
        }
        return rows;
    }

    // Rejected objects are preserved for provenance — never silently dropped (§4/§6).
    private IReadOnlyCollection<WideInspectorRejectedDto> BuildInspectorRejected()
    {
        if (_legalNormalization is null)
            return [];
        return ProjectInspectorRejected(_legalNormalization.Plan);
    }

    internal static IReadOnlyCollection<WideInspectorRejectedDto> ProjectInspectorRejected(
        LegalDecisionService.LegalDecisionRegistrationPlan plan)
        => plan.Rejected
            .Select(r => new WideInspectorRejectedDto(r.ObjectKind, r.Identifier, r.Reason))
            .ToArray();

    // §1 — the eight processing stages, each populated from real captured run state.
    private IReadOnlyCollection<WideInspectorStageDto> BuildInspectorStages(
        IReadOnlyCollection<WideInterpretiveResultDto> interpretiveResults,
        IReadOnlyCollection<WideCandidateDto> deliveredCandidates,
        IReadOnlyCollection<WideInspectorCandidateDto> normalizedCandidates,
        IReadOnlyCollection<WideInspectorDependencyDto> sharedDependencies)
    {
        var stages = new List<WideInspectorStageDto>();
        var gate = _legalNormalization?.GateResult;
        var plan = _legalNormalization?.Plan;

        // 1) Matter Decision Contract.
        var contractFields = _matterContext?.Decision ?? [];
        var available = contractFields.Where(f => f.HasValue).Select(f => f.Label).ToArray();
        var missing = contractFields.Where(f => !f.HasValue).Select(f => f.Label).ToArray();
        stages.Add(new WideInspectorStageDto(
            StageCode: "MATTER_CONTRACT",
            StageName: "Matter Decision Contract",
            Status: _matterContext is null ? StatusMissing : (missing.Length == 0 ? StatusCompleted : StatusIncomplete),
            InputSource: "Matter Context Snapshot (DB-backed, projected into prompts)",
            ActualInput: _matterContext is null ? null : $"Matter {_matterContext.MatterId} · DomainPack {_matterContext.DomainPackCode ?? "(none)"} · {contractFields.Count} decision field(s)",
            RequiredFactors: contractFields.Select(f => f.Label).ToArray(),
            AvailableFactors: available,
            MissingFactors: missing,
            ValidationErrors: _matterContext is null ? ["No matter context loaded — the run proceeded matter-less."] : [],
            ProcessingPerformed: "Loaded the immutable matter snapshot and projected its decision fields into the intent/hierarchy prompts.",
            ActualOutput: _matterContext is null ? null : _matterContextBlock,
            DurationMilliseconds: null,
            BlockingReason: _matterContext is null ? "Matter context unavailable for this tenant/matter." : null));

        // 2) Possible Legal Outcomes (interpretive layer).
        var outcomeItems = interpretiveResults.SelectMany(r => r.Items.Select(i => i.Name.Trim())).Where(n => n.Length > 0).ToArray();
        stages.Add(new WideInspectorStageDto(
            StageCode: "POSSIBLE_OUTCOMES",
            StageName: "Possible Legal Outcomes",
            Status: outcomeItems.Length > 0 ? StatusCompleted : StatusIncomplete,
            InputSource: "Dynamic interpretive narrowing (LLM-proposed outcome alternatives)",
            ActualInput: $"{interpretiveResults.Count} interpretive result set(s)",
            RequiredFactors: ["At least one competing outcome alternative"],
            AvailableFactors: outcomeItems.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
            MissingFactors: outcomeItems.Length == 0 ? ["No competing outcomes were surfaced by the interpretive layer."] : [],
            ValidationErrors: [],
            ProcessingPerformed: "Harvested distinct competing outcome names from the surviving interpretive result sets.",
            ActualOutput: outcomeItems.Length == 0 ? null : string.Join(" · ", outcomeItems.Distinct(StringComparer.OrdinalIgnoreCase)),
            DurationMilliseconds: null,
            BlockingReason: null));

        // 3) Structured Outcome Proposal (projected into the gate).
        var proposedCount = _legalNormalization?.ProposedCandidateCount ?? 0;
        stages.Add(new WideInspectorStageDto(
            StageCode: "STRUCTURED_PROPOSAL",
            StageName: "Structured Outcome Proposal",
            Status: _legalNormalization is null ? StatusMissing : (proposedCount > 0 ? StatusCompleted : StatusIncomplete),
            InputSource: "Gate proposal projection (competing dispositions + supplied matter assertions)",
            ActualInput: _legalNormalization is null ? null : $"{proposedCount} proposed candidate(s), {_legalNormalization.OutcomeNodeCount} outcome node(s)",
            RequiredFactors: ["Structured proposal with distinct outcome nodes and provenance"],
            AvailableFactors: _legalNormalization is null ? [] : [$"ProposedCandidates={proposedCount}", $"OutcomeNodes={_legalNormalization.OutcomeNodeCount}"],
            MissingFactors: _legalNormalization is null ? ["No structured proposal was produced — the gate was not reached."] : [],
            ValidationErrors: [],
            ProcessingPerformed: "Projected the competing dispositions and supplied matter assertions/objectives into the shared gate's ProposedCandidate structure.",
            ActualOutput: _legalNormalization is null ? null : $"Structured proposal with {proposedCount} candidate node(s).",
            DurationMilliseconds: null,
            BlockingReason: _legalNormalization is null ? NormalizationSkipReason() : null));

        // 4) Shared Dependencies.
        stages.Add(new WideInspectorStageDto(
            StageCode: "SHARED_DEPENDENCIES",
            StageName: "Shared Dependencies",
            Status: _legalNormalization is null ? StatusNotReached : (sharedDependencies.Count > 0 ? StatusCompleted : StatusIncomplete),
            InputSource: "NormalizeSharedDependencies over the surviving decision-dependency branch forest",
            ActualInput: _legalNormalization is null ? null : $"{gate!.Diagnostics.SharedDependencies} dependency node(s)",
            RequiredFactors: ["Reusable shared dependencies with candidate relations"],
            AvailableFactors: sharedDependencies.Select(d => d.DependencyId).ToArray(),
            MissingFactors: (_legalNormalization is not null && sharedDependencies.Count == 0) ? ["No shared dependencies were registered."] : [],
            ValidationErrors: [],
            ProcessingPerformed: "Normalized shared dependencies and validated candidate→dependency edges against registered dependency ids.",
            ActualOutput: _legalNormalization is null ? null : $"{sharedDependencies.Count} shared dependency(ies), {gate!.Diagnostics.ValidatedRelationCount} validated relation(s).",
            DurationMilliseconds: null,
            BlockingReason: _legalNormalization is null ? NormalizationSkipReason() : null));

        // 5) Proposal Validation (RegistrationPlan.IsValid).
        var planValid = plan?.IsValid == true;
        var validationErrors = new List<string>();
        if (plan is not null && !plan.IsValid)
        {
            if (plan.Candidates.Count == 0) validationErrors.Add("No normalized candidates.");
            if (plan.Dependencies.Count == 0) validationErrors.Add("No shared dependencies.");
            if (plan.EligibleCandidateSemanticIds.Count == 0) validationErrors.Add("No candidate is eligible to compete (all baseline/procedural/review).");
        }
        stages.Add(new WideInspectorStageDto(
            StageCode: "PROPOSAL_VALIDATION",
            StageName: "Proposal Validation",
            Status: _legalNormalization is null ? StatusNotReached : (planValid ? StatusCompleted : StatusIncomplete),
            InputSource: "LegalDecisionRegistrationPlan validity rules",
            ActualInput: plan is null ? null : $"Candidates={plan.Candidates.Count}, Dependencies={plan.Dependencies.Count}, Eligible={plan.EligibleCandidateSemanticIds.Count}",
            RequiredFactors: ["≥1 candidate", "≥1 dependency", "≥1 competition-eligible candidate"],
            AvailableFactors: plan is null ? [] : [$"Candidates={plan.Candidates.Count}", $"Dependencies={plan.Dependencies.Count}", $"Eligible={plan.EligibleCandidateSemanticIds.Count}"],
            MissingFactors: validationErrors.ToArray(),
            ValidationErrors: validationErrors.ToArray(),
            ProcessingPerformed: "Validated the registration plan is fit to drive Core competition.",
            ActualOutput: plan is null ? null : (planValid ? "RegistrationPlan VALID" : "RegistrationPlan INVALID"),
            DurationMilliseconds: null,
            BlockingReason: _legalNormalization is null ? NormalizationSkipReason() : (planValid ? null : "Plan not valid — degraded to the unchanged candidate universe.")));

        // 6) Candidate Normalization.
        var mergeCount = _legalNormalization is null ? 0 : DecisionCount(_legalNormalization, "Merge");
        var keepDistinctCount = _legalNormalization is null ? 0 : DecisionCount(_legalNormalization, "KeepDistinct");
        var requiresReviewCount = _legalNormalization is null ? 0 : DecisionCount(_legalNormalization, "RequiresReview");
        stages.Add(new WideInspectorStageDto(
            StageCode: "CANDIDATE_NORMALIZATION",
            StageName: "Candidate Normalization",
            Status: _legalNormalization is null ? StatusNotReached : (normalizedCandidates.Count > 0 ? StatusCompleted : StatusIncomplete),
            InputSource: "Deterministic material-identity normalization (MERGE / KEEP_DISTINCT / REQUIRES_REVIEW)",
            ActualInput: gate is null ? null : $"{gate.Diagnostics.RawProposalCount} raw proposal object(s)",
            RequiredFactors: ["Normalized global candidate pool with identity decisions"],
            AvailableFactors: normalizedCandidates.Select(c => c.NormalizedCandidateId).ToArray(),
            MissingFactors: [],
            ValidationErrors: [],
            ProcessingPerformed: gate is null ? null : $"MERGE={mergeCount}, KEEP_DISTINCT={keepDistinctCount}, REQUIRES_REVIEW={requiresReviewCount}.",
            ActualOutput: gate is null ? null : $"{gate.Diagnostics.NormalizedCandidates} normalized candidate(s), {gate.Diagnostics.EligibleCandidateCount} eligible, {gate.Diagnostics.DeferredCandidateCount} deferred.",
            DurationMilliseconds: null,
            BlockingReason: _legalNormalization is null ? NormalizationSkipReason() : null));

        // 7) Core Registration.
        var registeredIds = plan?.EligibleCandidateSemanticIds.ToArray() ?? [];
        stages.Add(new WideInspectorStageDto(
            StageCode: "CORE_REGISTRATION",
            StageName: "Core Registration",
            Status: _legalNormalization is null ? StatusNotReached : (planValid ? StatusCompleted : StatusIncomplete),
            InputSource: "RegistrationPlan → candidate competition basis",
            ActualInput: plan is null ? null : $"Eligible candidate ids: {string.Join(", ", registeredIds)}",
            RequiredFactors: ["Valid RegistrationPlan", "≥1 eligible candidate id"],
            AvailableFactors: registeredIds,
            MissingFactors: planValid ? [] : ["No eligible candidate ids registered for competition."],
            ValidationErrors: [],
            ProcessingPerformed: planValid
                ? $"Registered {registeredIds.Length} eligible representative candidate(s) and fed them into the candidate competition (basis size {_competitionBasis.Count})."
                : "Registration not committed — plan invalid, competition used the unchanged universe.",
            ActualOutput: _normalizationGateStatus,
            DurationMilliseconds: null,
            BlockingReason: _legalNormalization is null ? NormalizationSkipReason() : (planValid ? null : "Plan invalid.")));

        // 8) Core Evaluation and Decision Readiness — distinguishes semantic registration from evidence readiness.
        var scoredIds = _scoredCandidateIds.ToArray();
        var delivered = DeliveredCandidateCount(deliveredCandidates);
        var readinessBlocked = _decisionReadinessStatus is not null && _decisionReadinessStatus.StartsWith("REGISTERED_SCORING_BLOCKED", StringComparison.OrdinalIgnoreCase);
        stages.Add(new WideInspectorStageDto(
            StageCode: "CORE_EVALUATION",
            StageName: "Core Evaluation and Decision Readiness",
            Status: _decisionReadinessStatus is null ? StatusNotReached : (readinessBlocked ? StatusBlocked : (delivered >= 2 ? StatusCompleted : StatusIncomplete)),
            InputSource: "Candidate × Dependency competition (unchanged Core scoring + evidence admission)",
            ActualInput: $"Competition basis {_competitionBasis.Count} candidate(s)",
            RequiredFactors: ["≥2 competing candidates reaching scoring", "Admitted evidence for an authoritative winner"],
            AvailableFactors: scoredIds,
            MissingFactors: readinessBlocked ? ["Admitted evidence to support an authoritative winner."] : [],
            ValidationErrors: [],
            ProcessingPerformed: $"Candidate competition status: {_candidateCompetitionStatus ?? "(none)"}.",
            ActualOutput: _decisionReadinessStatus,
            DurationMilliseconds: null,
            BlockingReason: readinessBlocked
                ? "Semantic registration succeeded but the unchanged evidence/readiness rules prevented an authoritative winner (zero admitted evidence blocks selection)."
                : null));

        return stages;
    }

    private string NormalizationSkipReason()
        => _gateMode is null
            ? StatusNotReached
            : _gateMode.StartsWith("ENABLED", StringComparison.OrdinalIgnoreCase)
                ? (_normalizationGateStatus ?? "Gate enabled but produced no projectable dispositions (fail-soft skip).")
                : $"Gate {_gateMode}.";
}
