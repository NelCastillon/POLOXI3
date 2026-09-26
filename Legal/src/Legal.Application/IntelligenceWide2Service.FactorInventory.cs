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
            _factBindingSemanticProbe);
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
        FactBindingSemanticProbe? semanticProbe = null)
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
        var blockingObligations = new List<string>();

        foreach (var dep in plan.Dependencies)
        {
            edgesByDependency.TryGetValue(dep.DependencyId, out var edges);
            edges ??= [];

            var proposition = string.IsNullOrWhiteSpace(dep.Question) ? dep.Label : dep.Question!;

            // Resolve the actual value from matter data only (never invented). MISSING when no matter
            // field materially matches the factor's label/question.
            var (actualValue, valueSource, sourceLocation, matchedFieldLabel) = ResolveFactorValue(dep, matterContext);
            var hasValue = !string.IsNullOrWhiteSpace(actualValue);

            var relationTypes = edges
                .Select(e => e.RelationType)
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

                if (verdict.Establishes)
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
                missingCount++;
                if (isRequiredFactor)
                    blockingObligations.Add($"Establish required factor '{dep.Label}' — no value in matter data or documents.");
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
            });

            // One relationship row per candidate edge. The same factor may be REQUIRED for one candidate
            // and SUPPORTS another — each edge is preserved distinctly.
            foreach (var edge in edges)
            {
                candidateTitles.TryGetValue(edge.CandidateSemanticId, out var title);
                var relation = string.IsNullOrWhiteSpace(edge.RelationType) ? "DEPENDS_ON" : edge.RelationType;
                relationships.Add(new WideCandidateFactorRelationDto(
                    CandidateId: edge.CandidateSemanticId,
                    CandidateTitle: title ?? edge.CandidateSemanticId,
                    FactorId: dep.DependencyId,
                    FactorName: dep.Label,
                    RelationType: relation,
                    IsRequired: string.Equals(relation, "REQUIRED", StringComparison.OrdinalIgnoreCase),
                    Rationale: edge.Rationale));
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
            DecisionReadinessStatus = (blockingObligations.Count > 0 || missingCount > 0 || rejectedBindingCount > 0)
                ? "BLOCKED"
                : "PROVISIONAL",
            RejectedBindingCount = rejectedBindingCount,
            BlockingObligations = blockingObligations,
        };
    }

    // Resolve a factor's actual value from matter data ONLY (never invented). Matches the dependency
    // label/question against the populated matter fields across all groups; returns the field value,
    // its source category, and a source location reference when found.
    private static (string? Value, string ValueSource, string? SourceLocation, string? FieldLabel) ResolveFactorValue(
        LegalDecisionService.NormalizedDependency dep,
        MatterContextSnapshot? matterContext)
    {
        if (matterContext is null)
            return (null, AvailMissing, null, null);

        foreach (var (groupName, fields) in EnumerateMatterGroups(matterContext))
        {
            foreach (var field in fields)
            {
                if (!field.HasValue)
                    continue;
                if (FieldMatchesFactor(field.Label, dep.Label))
                {
                    var source = string.Equals(groupName, "AVAILABLE EVIDENCE", StringComparison.OrdinalIgnoreCase)
                        ? FactorSourceDocument
                        : FactorSourceMatter;
                    return (field.Value, source, $"{groupName}:{field.Label}", field.Label);
                }
            }
        }
        return (null, AvailMissing, null, null);
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
}
