using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;

namespace Legal.Application;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// Universal Legal Factor Inventory projector.
//
// Builds the domain-agnostic Factor Inventory from the SAME artifacts the run already produced: the
// validated normalization RegistrationPlan (shared dependencies = global factors, edges = Candidate×
// Factor relationships, candidates = outcomes) and the immutable Matter Context Snapshot (matter-data
// source + actual values + provenance) plus the resolved Domain Pack. It is diagnostic-only, reads
// exclusively from captured state, and makes NO additional LLM call.
//
// Contract fidelity:
//   • One GLOBAL factor identity per shared dependency — never duplicated per candidate. Candidate
//     linkage is expressed through separate Candidate×Factor relationship rows.
//   • The eight-field contract (Name, Source, ActualValue, Availability, VerificationStatus,
//     Requirement, Relationships, MissingInformation) is always populated; absence is explicit
//     (Availability=MISSING / VerificationStatus=UNVERIFIED) — a value is never fabricated.
//   • Availability is derived only from actually-available sources (matter data / domain pack). The
//     dynamic path attaches no per-factor admitted evidence, so verification stays UNVERIFIED.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public sealed partial class IntelligenceWide2Service
{
    private const string FactorSourceMatter = "MATTER_DATA";
    private const string FactorSourceDocument = "UPLOADED_DOCUMENT";
    private const string FactorSourceLlm = "LLM_SEMANTIC";
    private const string FactorSourceDomainPack = "DOMAIN_PACK";

    private const string AvailAvailable = "AVAILABLE";
    private const string AvailMissing = "MISSING";
    private const string AvailIncomplete = "INCOMPLETE";

    private const string VerifSupplied = "SUPPLIED";
    private const string VerifUnverified = "UNVERIFIED";
    private const string VerifVerified = "VERIFIED";

    // Per-run fact-binding validator (DB-backed config resolved by the orchestrator; fail-soft
    // DefaultConfig when the DB load degrades) and optional semantic probe (off by default).
    private PropositionFactBindingValidator? _factBindingValidator;
    private FactBindingSemanticProbe? _factBindingSemanticProbe;

    // Instance entry point: reads captured run state and delegates to the instance-free projector so the
    // projection logic is unit-testable directly from the shared gate fixtures.
    internal WideFactorInventoryDto? BuildFactorInventory(
        IReadOnlyCollection<WideCandidateDto> deliveredCandidates)
    {
        if (_legalNormalization is null || !_legalNormalization.Plan.IsValid)
            return null;

        return ProjectFactorInventory(
            _legalNormalization.Plan,
            _matterContext,
            _resolvedDomainPackId is not null,
            _resolvedDomainPackCode ?? _matterContext?.DomainPackCode,
            deliveredCandidates?.Any(c => !c.IsConstraintViolation) == true,
            _factBindingValidator ?? new PropositionFactBindingValidator(PropositionFactBindingValidator.DefaultConfig()),
            _factBindingSemanticProbe,
            _factorInventoryAdmittedEvidence,
            _factorInventoryProposedAuthorities,
            _factorInventoryChannelContributions);
    }

    // Deterministic, instance-free projection. Accepts only the validated plan + immutable matter context
    // + resolved domain-pack status. There is no provider/router/LLM parameter, which structurally
    // guarantees the inventory cannot trigger an extra live model call.
    internal static WideFactorInventoryDto ProjectFactorInventory(
        LegalDecisionService.LegalDecisionRegistrationPlan plan,
        MatterContextSnapshot? matterContext,
        bool domainPackResolved,
        string? domainPackCode,
        bool anyCandidateDelivered,
        PropositionFactBindingValidator? validator = null,
        FactBindingSemanticProbe? semanticProbe = null,
        IReadOnlyList<PoloxiEvidenceDto>? admittedEvidence = null,
        IReadOnlyList<WideProposedAuthorityDto>? proposedAuthorities = null,
        IReadOnlyList<Features.Intelligence.Decision.Channels.ChannelContributionDto>? channelContributions = null)
    {
        // Fail-soft: with no explicit validator (unit tests / degraded config) apply the embedded
        // DefaultConfig so the deterministic guardrails still run.
        validator ??= new PropositionFactBindingValidator(PropositionFactBindingValidator.DefaultConfig());
        // Candidate id → display title for relationship rows.
        var candidateTitles = plan.Candidates.ToDictionary(
            c => c.RepresentativeSemanticId,
            c => c.DisplayName,
            StringComparer.OrdinalIgnoreCase);

        // Edges grouped per dependency so each global factor lists its relation types + candidate links.
        var edgesByDependency = plan.Edges
            .GroupBy(e => e.DependencyId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.OrdinalIgnoreCase);

        var factors = new List<WideFactorDto>();
        var relationships = new List<WideCandidateFactorRelationDto>();
        var missingCount = 0;
        var rejectedBindingCount = 0;
        var contradictedCount = 0;
        var blockingObligations = new List<string>();

        foreach (var dep in plan.Dependencies)
        {
            edgesByDependency.TryGetValue(dep.DependencyId, out var edges);
            edges ??= [];

            var proposition = string.IsNullOrWhiteSpace(dep.Question) ? dep.Label : dep.Question!;

            // Resolve the actual value from matter data only (never invented). MISSING when no matter
            // field materially matches the factor's label/question.
            var matterMatches = ResolveFactorMatches(dep, matterContext);
            var (actualValue, valueSource, sourceLocation, matchedFieldLabel, matchedProvenance, matchedGroup) = matterMatches.Count == 0
                ? ((string?)null, AvailMissing, (string?)null, (string?)null, MatterFieldProvenance.Unknown, (string?)null)
                : (matterMatches[0].Value, matterMatches[0].ValueSource, matterMatches[0].SourceLocation, matterMatches[0].FieldLabel, matterMatches[0].Provenance, matterMatches[0].GroupName);
            var hasValue = !string.IsNullOrWhiteSpace(actualValue);

            // Cross-source contradiction: the same proposition carries more than one distinct value across
            // matter data / documents. A contradicted proposition is never established regardless of the
            // primary supplied value.
            var contradictions = DetectContradictions(matterMatches);
            var isContradicted = contradictions.Count > 0;

            var relationTypes = edges
                .Select(e => MapRelationVocabulary(e.RelationType))
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var definitionSource = domainPackResolved ? FactorSourceDomainPack : FactorSourceLlm;
            var requirement = BuildRequirement(dep, relationTypes);
            var isRequiredFactor = relationTypes.Any(r => string.Equals(r, "REQUIRED", StringComparison.OrdinalIgnoreCase));

            // ── Proposition-specific fact binding ─────────────────────────────────────────────────────
            // A supplied value is validated BEFORE it is allowed to establish the proposition. A category
            // mismatch (e.g. SettlementStatus=Disbursed → "Liability established") is preserved verbatim in
            // ActualValue but is NOT promoted to an established proposition — it becomes an explicit
            // verification obligation. Availability/VerificationStatus reflect the validated outcome.
            string availability;
            string verification;
            string evidenceState;
            string bindingAdmissibility;
            string? verificationObligation;
            string? missingInformation;
            string source;
            string validationStatus;

            if (hasValue)
            {
                var verdict = validator.Validate(matchedFieldLabel, actualValue, proposition, semanticProbe);
                source = $"{definitionSource}+{valueSource}";
                evidenceState = verdict.EvidenceAdmissionState;
                bindingAdmissibility = verdict.Admissibility;
                verificationObligation = verdict.VerificationObligation;

                if (isContradicted)
                {
                    // Conflicting values across sources: preserve the primary value but never establish the
                    // proposition; force an explicit CONTRADICTED evidence state and blocking obligation.
                    availability = AvailIncomplete;
                    verification = VerifUnverified;
                    evidenceState = PropositionFactBindingValidator.StateContradicted;
                    var conflictObligation = $"Resolve conflicting values for '{dep.Label}': {string.Join(" vs ", contradictions)}.";
                    verificationObligation = conflictObligation;
                    missingInformation = conflictObligation;
                    validationStatus = "CONTRADICTED";
                    contradictedCount++;
                    blockingObligations.Add(conflictObligation);
                }
                else if (verdict.Establishes)
                {
                    // Value legitimately populates the proposition (still SUPPLIED, never VERIFIED here).
                    availability = AvailAvailable;
                    verification = VerifSupplied;
                    missingInformation = null;
                    validationStatus = "VALID";
                }
                else
                {
                    // Rejected or ambiguous: retain the value but the proposition is NOT established.
                    availability = AvailIncomplete;
                    verification = VerifUnverified;
                    missingInformation = verdict.VerificationObligation;
                    validationStatus = AvailIncomplete;
                    if (verdict.Decision == FactBindingDecision.Rejected)
                        rejectedBindingCount++;
                    if (!string.IsNullOrWhiteSpace(verdict.VerificationObligation))
                        blockingObligations.Add(verdict.VerificationObligation!);
                }
            }
            else
            {
                availability = AvailMissing;
                verification = VerifUnverified;
                evidenceState = PropositionFactBindingValidator.StateUnresolved;
                bindingAdmissibility = "NONE";
                verificationObligation = null;
                missingInformation = $"No case-specific value for '{dep.Label}' found in matter data or uploaded documents.";
                source = definitionSource;
                validationStatus = AvailIncomplete;
                // Only ATOMIC leaf factors are decision-material "open questions". PARENT grouping nodes
                // (broad L1/L2 categories) never carry an atomic matter value and must not inflate the
                // missing/open-question count surfaced in the KPI strip and inventory status.
                if (dep.NodeKind != LegalDecisionService.DependencyNodeKind.Parent)
                    missingCount++;
                if (isRequiredFactor)
                    blockingObligations.Add($"Establish required factor '{dep.Label}' — no value in matter data or documents.");
            }

            // ── Actual reference chips (additive transparency) ──────────────────────────────────────────
            // Collect the real, linkable sources that back or relate to THIS factor: the matched matter /
            // profile field (with its own provenance), admitted evidence, proposed/key legal authorities,
            // and decision-channel contributions. References never lower status; a matter/profile field whose
            // provenance is VERIFIED legitimately promotes VerificationStatus to VERIFIED, but ONLY when the
            // value also established the proposition (passed the admission gate) — a rejected/contradicted
            // value is surfaced as a reference without upgrading status, so no gate is weakened.
            var references = new List<WideFactorReferenceDto>();
            var factorEstablished = string.Equals(validationStatus, "VALID", StringComparison.OrdinalIgnoreCase)
                && string.Equals(availability, AvailAvailable, StringComparison.OrdinalIgnoreCase);

            foreach (var match in matterMatches)
            {
                var isDocument = string.Equals(match.GroupName, "AVAILABLE EVIDENCE", StringComparison.OrdinalIgnoreCase);
                references.Add(new WideFactorReferenceDto(
                    KindCode: isDocument ? "DOCUMENT" : "MATTER_FIELD",
                    Label: match.FieldLabel,
                    Reference: $"{match.GroupName}: {match.Value}",
                    NavigationRoute: BuildMatterFieldRoute(matterContext),
                    VerificationState: match.Provenance.ToString().ToUpperInvariant())
                {
                    Excerpt = isDocument ? match.Value : null,
                });
            }

            // Admitted matter-fact evidence that materially matches THIS proposition (already cleared the
            // fact-binding gate upstream). Matched by field-label / proposition term overlap; never fabricated.
            if (admittedEvidence is { Count: > 0 })
            {
                foreach (var ev in admittedEvidence)
                {
                    if (!EvidenceMatchesFactor(ev, dep, matchedFieldLabel))
                        continue;
                    references.Add(new WideFactorReferenceDto(
                        KindCode: "EVIDENCE",
                        Label: string.IsNullOrWhiteSpace(ev.Title) ? dep.Label : ev.Title,
                        Reference: string.IsNullOrWhiteSpace(ev.Excerpt) ? ev.Title : ev.Excerpt,
                        NavigationRoute: string.IsNullOrWhiteSpace(ev.NavigationRoute) ? null : ev.NavigationRoute,
                        VerificationState: "ADMITTED")
                    {
                        DocumentId = ev.SearchDocumentId == Guid.Empty ? null : ev.SearchDocumentId,
                        Excerpt = string.IsNullOrWhiteSpace(ev.Excerpt) ? null : ev.Excerpt,
                    });
                }
            }

            // Proposed legal authorities whose name materially relates to the proposition. VerificationState
            // mirrors the authority's own identity-verification status (VERIFIED/UNVERIFIED) — never upgraded.
            if (proposedAuthorities is { Count: > 0 })
            {
                foreach (var auth in proposedAuthorities)
                {
                    if (!AuthorityMatchesFactor(auth, dep))
                        continue;
                    references.Add(new WideFactorReferenceDto(
                        KindCode: "LEGAL_AUTHORITY",
                        Label: auth.Name,
                        Reference: auth.Relevance,
                        NavigationRoute: null,
                        VerificationState: string.IsNullOrWhiteSpace(auth.VerificationStatus) ? "UNVERIFIED" : auth.VerificationStatus));
                }
            }

            // Decision Channel contributions (Document Evidence, Human Intelligence, Investigation, External
            // Research, Decision Contract, …) persisted against this matter that materially relate to THIS
            // proposition. Purely additive provenance: the contribution's OWN verification state is surfaced
            // verbatim and NEVER upgrades the factor's admission/verification status. Matched by conservative
            // significant-token overlap; never fabricated. Routes to the matter workspace when one exists.
            if (channelContributions is { Count: > 0 })
            {
                foreach (var contribution in channelContributions)
                {
                    if (!ChannelContributionMatchesFactor(contribution, dep))
                        continue;
                    var channelKind = ChannelKindLabel(contribution.ChannelTypeCode);
                    var channelLabel = string.IsNullOrWhiteSpace(contribution.SourceLabel) ? channelKind : contribution.SourceLabel!;
                    references.Add(new WideFactorReferenceDto(
                        KindCode: "CHANNEL",
                        Label: $"{channelKind}: {channelLabel}",
                        Reference: string.IsNullOrWhiteSpace(contribution.VerificationReason) ? contribution.RelationCode : contribution.VerificationReason,
                        NavigationRoute: BuildMatterFieldRoute(matterContext),
                        VerificationState: string.IsNullOrWhiteSpace(contribution.VerificationStateCode) ? "UNVERIFIED" : contribution.VerificationStateCode.ToUpperInvariant())
                    {
                        DocumentId = contribution.LegalDocumentId,
                        DocumentVersionId = contribution.LegalDocumentVersionId,
                        PassageId = contribution.LegalDocumentPassageId,
                    });
                }
            }

            // Promote to VERIFIED when the establishing matter/profile field is itself VERIFIED provenance.
            // Gate preserved: only an already-established (VALID + AVAILABLE) factor may be upgraded.
            if (factorEstablished && matchedProvenance == MatterFieldProvenance.Verified)
            {
                verification = VerifVerified;
            }

            factors.Add(new WideFactorDto(
                FactorName: dep.Label,
                Source: source,
                ActualValue: hasValue ? actualValue : null,
                Availability: availability,
                VerificationStatus: verification,
                Requirement: requirement,
                Relationships: relationTypes,
                MissingInformation: missingInformation,
                FactorId: dep.DependencyId,
                FactorType: dep.Category.ToString().ToUpperInvariant(),
                ValueSource: hasValue ? valueSource : AvailMissing,
                SourceLocation: sourceLocation,
                ValidationStatus: validationStatus,
                NodeKind: dep.NodeKind.ToString().ToUpperInvariant())
            {
                EvidenceAdmissionState = evidenceState,
                CountsTowardEvidenceScore = false, // dynamic path attaches no admitted per-factor evidence yet
                BindingAdmissibility = bindingAdmissibility,
                VerificationObligation = verificationObligation,
                Contradictions = contradictions,
                References = references,
                MatterProvenance = hasValue ? matchedProvenance.ToString().ToUpperInvariant() : null,
            });

            // One relationship row per candidate edge. The same factor may be REQUIRED for one candidate
            // and SUPPORTS another — each edge is preserved distinctly.
            foreach (var edge in edges)
            {
                candidateTitles.TryGetValue(edge.CandidateSemanticId, out var title);
                var relation = MapRelationVocabulary(edge.RelationType);
                var isRequiredEdge = string.Equals(relation, "REQUIRED", StringComparison.OrdinalIgnoreCase);
                // Milestone B: a relationship is VALIDATED only when it carries a semantic rationale (the
                // justification for why the factor matters to THIS candidate). REQUIRED links demand a
                // substantive rationale; a bare edge is a lexical/structural link — retained for
                // transparency, but it must NOT be presented as an established REQUIRED dependency.
                var isValidated = IsRelationValidated(relation, edge.Rationale);
                relationships.Add(new WideCandidateFactorRelationDto(
                    CandidateId: edge.CandidateSemanticId,
                    CandidateTitle: title ?? edge.CandidateSemanticId,
                    FactorId: dep.DependencyId,
                    FactorName: dep.Label,
                    RelationType: relation,
                    IsRequired: isRequiredEdge,
                    Rationale: edge.Rationale)
                {
                    IsValidatedRelation = isValidated,
                });
            }
        }

        var sourceStatus = new WideFactorInventorySourceStatusDto(
            MatterDataLoaded: matterContext is not null && matterContext.HasAnyContext,
            MatterFieldCount: matterContext?.FieldCount ?? 0,
            DomainPackResolved: domainPackResolved,
            DomainPackCode: domainPackCode,
            // The dynamic path does not attach per-factor documents; report corpus reachability honestly
            // rather than fabricating per-factor document links.
            DocumentCorpusStatus: matterContext?.Evidence.Any(f => f.HasValue) == true ? "AVAILABLE" : "NONE",
            EvidenceStatus: anyCandidateDelivered ? "ADMITTED" : "NONE");

        var candidateCount = plan.Candidates.Count;
        var sharedFactorCount = factors.Count(f => f.Relationships.Count > 1
            || relationships.Count(r => string.Equals(r.FactorId, f.FactorId, StringComparison.OrdinalIgnoreCase)) > 1);

        // ── Milestone B: backend REQUIRED-dependency protection (mirrors the UI, but authoritative) ──
        // A factor is "established" only when its value legitimately populated the proposition (VALID).
        var factorById = factors.ToDictionary(f => f.FactorId, f => f, StringComparer.OrdinalIgnoreCase);
        static bool IsEstablished(WideFactorDto f) =>
            string.Equals(f.ValidationStatus, "VALID", StringComparison.OrdinalIgnoreCase)
            && string.Equals(f.Availability, "AVAILABLE", StringComparison.OrdinalIgnoreCase);

        var requiredEdges = relationships.Where(r => r.IsRequired).ToArray();
        // REQUIRED links with no substantive legal/logical basis — never present these as established.
        var unvalidatedRequiredCount = requiredEdges.Count(r => !r.IsValidatedRelation);
        // Validated REQUIRED dependencies that are not yet established (missing/rejected/contradicted/supplied).
        var unsatisfiedRequiredCount = requiredEdges.Count(r =>
            r.IsValidatedRelation
            && !(factorById.TryGetValue(r.FactorId, out var rf) && IsEstablished(rf)));

        if (unvalidatedRequiredCount > 0)
            blockingObligations.Add(
                $"{unvalidatedRequiredCount} REQUIRED dependency relationship(s) lack a validated legal or logical basis — establish the basis before treating them as required.");
        if (unsatisfiedRequiredCount > 0)
            blockingObligations.Add(
                $"{unsatisfiedRequiredCount} REQUIRED dependency(ies) are not established — strong support for other requirements does not satisfy an unresolved requirement.");

        // ── Matter-lifecycle (posture) reconciliation ──────────────────────────────────────────────
        // Independent of per-factor binding: contradictory lifecycle status fields (e.g. a disbursed
        // settlement while the stage still reads active litigation, or an open demand) mean the matter's
        // own posture data is internally inconsistent. We surface this as an explicit blocking obligation
        // so the decision stays provisional until the operational record is reconciled — we never pick a
        // "winning" status or silently normalize the conflict away.
        var postureConflicts = DetectPostureReconciliationConflicts(matterContext);
        foreach (var conflict in postureConflicts)
            blockingObligations.Add(conflict);

        return new WideFactorInventoryDto(
            Factors: factors,
            Relationships: relationships,
            SourceStatus: sourceStatus,
            CandidateCount: candidateCount,
            SharedFactorCount: sharedFactorCount,
            MissingFactorCount: missingCount)
        {
            // Competition may run to surface hypotheses, but an unresolved material dependency or a rejected
            // binding keeps the result provisional — never presented as a verified recommendation.
            IsProvisional = true,
            DecisionReadinessStatus = (blockingObligations.Count > 0 || missingCount > 0 || rejectedBindingCount > 0
                    || contradictedCount > 0 || unvalidatedRequiredCount > 0 || unsatisfiedRequiredCount > 0)
                ? "BLOCKED"
                : "PROVISIONAL",
            RejectedBindingCount = rejectedBindingCount,
            BlockingObligations = blockingObligations,
            ContradictedFactorCount = contradictedCount,
            UnvalidatedRequiredCount = unvalidatedRequiredCount,
            UnsatisfiedRequiredDependencyCount = unsatisfiedRequiredCount,
        };
    }

    // All materially-matching matter values for a factor across every group. Used both to resolve the
    // primary value (first match) and to detect cross-source contradictions (distinct values for the same
    // proposition). Never fabricates: only returns values actually present in matter data / documents.
    private static IReadOnlyList<(string Value, string ValueSource, string SourceLocation, string FieldLabel, MatterFieldProvenance Provenance, string GroupName)> ResolveFactorMatches(
        LegalDecisionService.NormalizedDependency dep,
        MatterContextSnapshot? matterContext)
    {
        var matches = new List<(string, string, string, string, MatterFieldProvenance, string)>();
        if (matterContext is null)
            return matches;

        foreach (var (groupName, fields) in EnumerateMatterGroups(matterContext))
        {
            foreach (var field in fields)
            {
                if (!field.HasValue)
                    continue;
                // Match the field against BOTH the factor's short label AND its fuller proposition/question text.
                // Many real matter values (e.g. "Settlement Status" ↔ "Has the claim already settled?") only
                // overlap with the richer question wording, so testing the label alone left them unbound and the
                // Actual Value showing "—". Still deterministic and conservative: FieldMatchesFactor keeps the
                // significant-token gate, so unrelated fields are not conflated and nothing is fabricated.
                var matchesLabel = FieldMatchesFactor(field.Label, dep.Label);
                var matchesQuestion = !matchesLabel
                    && !string.IsNullOrWhiteSpace(dep.Question)
                    && FieldMatchesFactor(field.Label, dep.Question!);
                if (matchesLabel || matchesQuestion)
                {
                    var source = string.Equals(groupName, "AVAILABLE EVIDENCE", StringComparison.OrdinalIgnoreCase)
                        ? FactorSourceDocument
                        : FactorSourceMatter;
                    matches.Add((field.Value!, source, $"{groupName}:{field.Label}", field.Label, field.Provenance, groupName));
                }
            }
        }
        return matches;
    }

    // Deterministic navigation route for a matched matter/profile field reference chip. Links to the real
    // matter workspace (documents tab for uploaded evidence, profile otherwise). Null when no matter is
    // loaded — never fabricates a route for a matter that does not exist.
    private static string? BuildMatterFieldRoute(MatterContextSnapshot? matterContext)
    {
        if (matterContext is null || matterContext.MatterId == Guid.Empty)
            return null;
        return $"/legal/matters/{matterContext.MatterId}";
    }

    // True when an admitted evidence row materially relates to THIS factor. Uses the same significant-token
    // overlap gate as matter-field matching against the evidence title/excerpt and the factor label/question.
    // No fabrication: an evidence row with no material overlap is simply not attached.
    private static bool EvidenceMatchesFactor(PoloxiEvidenceDto evidence, LegalDecisionService.NormalizedDependency dep, string? matchedFieldLabel)
    {
        var haystack = $"{evidence.Title} {evidence.Excerpt}".Trim();
        if (haystack.Length == 0)
            return false;
        if (!string.IsNullOrWhiteSpace(matchedFieldLabel) && FieldMatchesFactor(haystack, matchedFieldLabel))
            return true;
        if (FieldMatchesFactor(haystack, dep.Label))
            return true;
        return !string.IsNullOrWhiteSpace(dep.Question) && FieldMatchesFactor(haystack, dep.Question!);
    }

    // True when a proposed legal authority materially relates to THIS factor by significant-token overlap of
    // the authority name/relevance against the factor label/question. Authority identity verification is not
    // altered here — only whether the authority is surfaced as a reference for this factor.
    private static bool AuthorityMatchesFactor(WideProposedAuthorityDto authority, LegalDecisionService.NormalizedDependency dep)
    {
        var haystack = $"{authority.Name} {authority.Relevance}".Trim();
        if (haystack.Length == 0)
            return false;
        if (FieldMatchesFactor(haystack, dep.Label))
            return true;
        return !string.IsNullOrWhiteSpace(dep.Question) && FieldMatchesFactor(haystack, dep.Question!);
    }

    // True when a persisted Decision Channel contribution materially relates to THIS factor by significant-token
    // overlap of the contribution's source label / verification reason against the factor label/question. The
    // contribution's own verification state is never altered here — only whether it is surfaced as a channel
    // reference for this factor. No fabrication: a contribution with no material overlap is simply not attached.
    private static bool ChannelContributionMatchesFactor(Features.Intelligence.Decision.Channels.ChannelContributionDto contribution, LegalDecisionService.NormalizedDependency dep)
    {
        var haystack = $"{contribution.SourceLabel} {contribution.VerificationReason}".Trim();
        if (haystack.Length == 0)
            return false;
        if (FieldMatchesFactor(haystack, dep.Label))
            return true;
        return !string.IsNullOrWhiteSpace(dep.Question) && FieldMatchesFactor(haystack, dep.Question!);
    }

    // Human-readable channel label for a persisted contribution's ChannelTypeCode. Falls back to the raw code
    // so an unrecognized/new channel still surfaces its real source rather than being dropped.
    private static string ChannelKindLabel(string? channelTypeCode) => channelTypeCode switch
    {
        "DocumentEvidence" => "Document",
        "HumanIntelligence" => "Human Intelligence",
        "Investigation" => "Investigation",
        "ExternalResearch" => "External Research",
        "DecisionContract" => "Decision Contract",
        "LegalAuthority" => "Legal Authority",
        null or "" => "Channel",
        _ => channelTypeCode
    };
    private static IReadOnlyList<string> DetectContradictions(
        IReadOnlyList<(string Value, string ValueSource, string SourceLocation, string FieldLabel, MatterFieldProvenance Provenance, string GroupName)> matches)
    {
        if (matches.Count < 2)
            return [];

        var distinctValues = matches
            .Select(m => m.Value.Trim())
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (distinctValues.Length < 2)
            return [];

        return matches
            .Where(m => !string.IsNullOrWhiteSpace(m.Value))
            .Select(m => $"{m.SourceLocation} = {m.Value.Trim()}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    // ── Matter lifecycle (posture) reconciliation ──────────────────────────────────────────────────
    // Deterministic cross-field consistency check over the saved matter lifecycle status fields. Unlike
    // DetectContradictions (which compares multiple sources for ONE proposition), this inspects the single
    // authoritative posture record for mutually exclusive states — e.g. a settlement reported as disbursed
    // while litigation is still active, or a settlement disbursed while a demand is still open. It performs
    // NO inference and selects NO winner; it only reports the conflict as a blocking obligation so the
    // decision stays provisional until the operational matter data is reconciled. Returns empty when the
    // posture fields are absent or internally consistent.
    private static IReadOnlyList<string> DetectPostureReconciliationConflicts(MatterContextSnapshot? matterContext)
    {
        if (matterContext is null)
            return [];

        static string? Lookup(MatterContextSnapshot ctx, string label) =>
            ctx.PersonalInjuryProfile
                .Concat(ctx.Decision)
                .FirstOrDefault(f => f.HasValue && string.Equals(f.Label, label, StringComparison.OrdinalIgnoreCase))
                ?.Value?.Trim();

        var stage = Lookup(matterContext, "Current Stage");
        var litigation = Lookup(matterContext, "Litigation Status");
        var demand = Lookup(matterContext, "Demand Status");
        var settlement = Lookup(matterContext, "Settlement Status");

        static bool Mentions(string? value, params string[] tokens) =>
            !string.IsNullOrWhiteSpace(value) && tokens.Any(t => value.Contains(t, StringComparison.OrdinalIgnoreCase));

        var settlementConcluded = Mentions(settlement, "Disbursed", "Settled", "Paid", "Closed");
        var litigationActive = Mentions(litigation, "Active", "Pending", "Discovery", "Trial", "Filed", "Open");
        var demandOpen = Mentions(demand, "Open", "Outstanding", "Pending", "Sent", "Awaiting");
        var stageActive = Mentions(stage, "Discovery", "Litigation", "Trial", "Investigation", "Pre-Suit", "Demand");

        var conflicts = new List<string>();
        if (settlementConcluded && litigationActive)
            conflicts.Add($"Reconcile matter posture: Settlement Status '{settlement}' indicates the matter concluded, but Litigation Status '{litigation}' is still active. Confirm the operational record before relying on either state.");
        if (settlementConcluded && demandOpen)
            conflicts.Add($"Reconcile matter posture: Settlement Status '{settlement}' indicates the matter concluded, but Demand Status '{demand}' is still open. Confirm the operational record before relying on either state.");
        if (settlementConcluded && stageActive)
            conflicts.Add($"Reconcile matter posture: Settlement Status '{settlement}' indicates the matter concluded, but Current Stage '{stage}' reflects an active pre-resolution phase. Confirm the operational record before relying on either state.");
        return conflicts;
    }


    // ── Matter-fact enterprise evidence projection ───────────────────────────────────────────────────
    // Wide2 is knowledge-only (it never grounds branches against AMS capability search), so the scoring
    // pool never saw the saved matter data and every legal decision run reported "0 enterprise evidence".
    // This projects the immutable Matter Context Snapshot into branch-attributed PoloxiEvidenceDto rows so
    // the saved case facts (policy, injuries, bills, documented damages, demands, witnesses, alleged
    // summaries) actually count toward EvidenceSupport / coverage. It is deterministic, adds NO LLM call,
    // and binds NOTHING on its own authority: every (field → branch proposition) pair must clear the SAME
    // PropositionFactBindingValidator gate used by the Factor Inventory. A category mismatch (e.g.
    // SettlementStatus=Disbursed → "a settlement offer was made") is rejected and produces no evidence row,
    // so blocker #3 (status enums are not proof of settlement propositions) is preserved exactly.
    internal static IReadOnlyList<PoloxiEvidenceDto> BuildMatterFactEvidence(
        IReadOnlyCollection<WideBranchRecord> branches,
        MatterContextSnapshot? matterContext,
        PropositionFactBindingValidator? validator,
        FactBindingSemanticProbe? semanticProbe)
    {
        if (matterContext is null || branches.Count == 0)
            return [];
        var materialFacts = matterContext.MaterialFacts();
        if (materialFacts.Count == 0)
            return [];
        validator ??= new PropositionFactBindingValidator(PropositionFactBindingValidator.DefaultConfig());

        var evidence = new List<PoloxiEvidenceDto>();
        var rank = 0;
        foreach (var branch in branches)
        {
            // The branch proposition is its interpretation when present, else its display name.
            var proposition = string.IsNullOrWhiteSpace(branch.Interpretation) ? branch.DisplayName : branch.Interpretation;
            if (string.IsNullOrWhiteSpace(proposition))
                continue;
            foreach (var field in materialFacts)
            {
                if (!field.HasValue)
                    continue;
                // Deterministic guardrail: only consider a fact whose LABEL is lexically relevant to the
                // branch proposition before the semantic gate runs, so the admissibility check is not asked
                // about obviously unrelated field/proposition pairs. This mirrors the Factor Inventory match.
                if (!FieldMatchesFactor(field.Label, proposition) && !FieldMatchesFactor(field.Label, branch.DisplayName))
                    continue;
                var verdict = validator.Validate(field.Label, field.Value, proposition, semanticProbe);
                // ADMITTED only. Rejected / RequiresVerification facts are preserved elsewhere as
                // obligations but must NEVER be credited as grounding evidence — the admission gate is
                // never lowered here.
                if (!verdict.Establishes)
                    continue;
                evidence.Add(new PoloxiEvidenceDto(
                    HierarchyBranchId: branch.WideBranchId,
                    SearchDocumentId: Guid.Empty,
                    EntityTypeCode: "MATTER_FACT",
                    EntityId: matterContext.MatterId,
                    ModuleCode: "LEGAL_MATTER",
                    Title: field.Label,
                    Excerpt: field.Value,
                    NavigationRoute: null,
                    RelevanceScore: 1m,
                    RankNumber: ++rank,
                    MatchedBranches: [branch.DisplayName]));
            }
        }
        return evidence;
    }

    private static IEnumerable<(string GroupName, IReadOnlyList<MatterContextField> Fields)> EnumerateMatterGroups(
        MatterContextSnapshot ctx)
    {
        yield return ("MATTER DECISION", ctx.Decision);
        yield return ("MATTER LEGAL CONTEXT", ctx.LegalScope);
        yield return ("PERSONAL INJURY CONTEXT", ctx.PersonalInjuryProfile);
        yield return ("FACT AND EVIDENCE STATUS", ctx.Facts);
        yield return ("AVAILABLE EVIDENCE", ctx.Evidence);
    }

    // Conservative label match: exact (case-insensitive) or token-containment so "Settlement Status"
    // matches an "Executed Settlement Agreement" factor without collapsing unrelated fields. Never
    // fuzzy-merges on partial single-word overlap alone.
    private static bool FieldMatchesFactor(string fieldLabel, string factorLabel)
    {
        var f = fieldLabel.Trim();
        var g = factorLabel.Trim();
        if (f.Length == 0 || g.Length == 0)
            return false;
        if (string.Equals(f, g, StringComparison.OrdinalIgnoreCase))
            return true;

        var fieldTokens = SignificantTokens(f);
        var factorTokens = SignificantTokens(g);
        if (fieldTokens.Count == 0 || factorTokens.Count == 0)
            return false;

        // Require at least two shared significant tokens (or one when a side is a single significant token)
        // so unrelated legal fields are not conflated.
        var shared = fieldTokens.Intersect(factorTokens, StringComparer.OrdinalIgnoreCase).Count();
        var threshold = Math.Min(fieldTokens.Count, factorTokens.Count) == 1 ? 1 : 2;
        return shared >= threshold;
    }

    private static readonly HashSet<string> FactorStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","of","for","and","or","to","in","is","are","was","were","status","value",
        "any","all","with","on","by","at","this","that","its","their",
    };

    private static IReadOnlyCollection<string> SignificantTokens(string text)
        => text.Split([' ', '\t', '-', '—', ',', '/', '(', ')', ':', ';', '.'],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 2 && !FactorStopWords.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    // Requirement text derived from the factor's proposition + how it relates to candidates. Never
    // fabricated: uses the dependency's own question/label and the actual relation vocabulary.
    private static string BuildRequirement(
        LegalDecisionService.NormalizedDependency dep,
        IReadOnlyCollection<string> relationTypes)
    {
        var basis = string.IsNullOrWhiteSpace(dep.Question) ? dep.Label : dep.Question!;
        if (relationTypes.Any(r => string.Equals(r, "REQUIRED", StringComparison.OrdinalIgnoreCase)))
            return $"Must be established: {basis}";
        if (relationTypes.Count > 0)
            return $"Considered ({string.Join("/", relationTypes)}): {basis}";
        return $"Relevant consideration: {basis}";
    }

    // Milestone B: map the internal proposal-layer relation role (produced by NormalizeRelationType:
    // required/supporting/opposing/conditional/distinguishing/non_applicable) onto the spec's controlled
    // dependency vocabulary. An unknown/blank role becomes UNRESOLVED — it is NEVER silently promoted to
    // SUPPORTS, so a weak or unclassified link cannot overstate support for a candidate.
    private static string MapRelationVocabulary(string? internalRelation)
    {
        var r = (internalRelation ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        return r switch
        {
            "required" => "REQUIRED",
            "supporting" or "supports" => "SUPPORTS",
            "opposing" or "defeats" => "DEFEATS",
            "conditional" => "CONDITIONAL",
            "distinguishing" or "alternative" => "ALTERNATIVE",
            "non_applicable" or "not_applicable" or "na" or "n_a" => "NOT_APPLICABLE",
            "" or "depends_on" or "unknown" => "UNRESOLVED",
            _ => "UNRESOLVED",
        };
    }

    // A REQUIRED dependency is validated only when it carries a SUBSTANTIVE legal/logical rationale — a
    // bare or trivially short rationale is not an authoritative basis. Non-REQUIRED relations keep the
    // lighter presence-of-rationale rule (retained for transparency).
    private static bool IsRelationValidated(string mappedRelation, string? rationale)
    {
        if (string.IsNullOrWhiteSpace(rationale))
            return false;
        if (!string.Equals(mappedRelation, "REQUIRED", StringComparison.OrdinalIgnoreCase))
            return true;
        // REQUIRED: require a rationale with real content (more than a single stub token).
        var significant = rationale.Split(TokenSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Count(t => t.Trim().Length > 2);
        return significant >= 2;
    }

    private static readonly char[] TokenSeparators = [' ', '\t', '-', '\u2014', ',', '/', '(', ')', ':', ';', '.'];
}
