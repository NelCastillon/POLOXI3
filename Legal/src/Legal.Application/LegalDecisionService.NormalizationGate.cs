using System.Text.Json;

namespace Legal.Application;

// ───────────────────────────────────────────────────────────────────────────────────────────────
// Outcome–Dependency Normalization Gate (pre-Core, LEGAL_DECISION / EVALUATE only).
//
// This is the missing semantic-to-Core integration boundary. The R2 proposal layer only PROPOSES a
// dual-hierarchy semantic representation (outcomeProposalHierarchy + semanticRoots + a global candidate
// pool + explicit candidateBranchRelations). Before POLOXI Core competes/scores anything, this gate
// deterministically:
//   1. CLASSIFIES every candidate by its semantic role (substantive/conditional resolution, baseline
//      state, procedural pathway, procedural consequence, dependency proposition).
//   2. NORMALIZES candidate identity on MATERIAL LEGAL IDENTITY (legal family + disposition polarity +
//      distinctive qualifier tokens with antonym-conflict detection) — NOT raw title similarity —
//      producing a MERGE / KEEP_DISTINCT / REQUIRES_REVIEW decision per candidate and preserving/
//      unioning originatingOutcomeNodeIds across merges.
//   3. NORMALIZES the shared semanticRoots forest into reusable typed dependency identities
//      (legal / factual / economic / evidentiary / procedural).
//   4. VALIDATES only the EXPLICIT candidate→dependency relations (never manufactures missing edges).
//   5. Produces a LegalDecisionRegistrationPlan and validates it before any Core registration.
//
// It NEVER assigns scores, evidence, verdicts, branch states, or information value — POLOXI Core remains
// authoritative for all of that. The gate returns a normalized proposal + a remapped enrichment (merged
// candidate ids rewritten) so the existing scoring/materialization path is unchanged.
// ───────────────────────────────────────────────────────────────────────────────────────────────
public sealed partial class LegalDecisionService
{
    // ── Semantic role of a proposed candidate (§3 classification). ──────────────────────────────
    internal enum CandidateSemanticRole
    {
        SubstantiveResolution,
        ConditionalResolution,
        BaselineState,
        ProceduralPathway,
        ProceduralConsequence,
        DependencyProposition,
    }

    // ── Deterministic identity-normalization decision (§4). ─────────────────────────────────────
    internal enum NormalizationDecision
    {
        KeepDistinct,
        Merge,
        RequiresReview,
    }

    // ── Reusable dependency category (§5). ──────────────────────────────────────────────────────
    internal enum DependencyCategory
    {
        Legal,
        Factual,
        Economic,
        Evidentiary,
        Procedural,
    }

    // Material legal identity: the deterministic fingerprint used to decide merges. Title text is only
    // an INPUT; identity is (family + polarity + distinctive qualifier tokens), never string similarity.
    internal sealed record MaterialLegalIdentity(string Family, string Polarity, IReadOnlyList<string> Qualifiers)
    {
        public string Signature => $"{Family}|{Polarity}|{string.Join(",", Qualifiers)}";
    }

    // A normalized candidate cluster after identity resolution. RepresentativeSemanticId is the id every
    // Core-facing artifact should use; MergedSemanticIds are the ids that collapsed into it.
    internal sealed record NormalizedCandidate(
        string RepresentativeSemanticId, string DisplayName, CandidateSemanticRole Role,
        MaterialLegalIdentity Identity, NormalizationDecision Decision,
        IReadOnlyList<string> MergedSemanticIds, IReadOnlyList<string> OriginatingOutcomeNodeIds)
    {
        // Competition eligibility (§9): ONLY materially substantive/conditional resolutions with a
        // settled identity may compete as authoritative Core candidates. Baseline states, procedural
        // pathways/consequences, dependency propositions, and any REQUIRES_REVIEW identity are registered
        // and preserved for provenance but must NOT enter authoritative candidate competition.
        public bool EligibleToCompete =>
            (Role == CandidateSemanticRole.SubstantiveResolution
                || Role == CandidateSemanticRole.ConditionalResolution)
            && Decision != NormalizationDecision.RequiresReview;
    }

    internal sealed record NormalizedDependency(
        string DependencyId, string Label, DependencyCategory Category, string? Question);

    internal sealed record CandidateDependencyEdge(
        string CandidateSemanticId, string DependencyId, string RelationType, string? Rationale);

    internal sealed record RejectedObject(string ObjectKind, string Identifier, string Reason);

    // The complete, validated pre-Core registration plan. Nothing is committed to POLOXI Core interfaces
    // until IsValid is true (Core scoring / materialization consumes the normalized proposal downstream).
    internal sealed record LegalDecisionRegistrationPlan(
        IReadOnlyList<NormalizedCandidate> Candidates,
        IReadOnlyList<NormalizedDependency> Dependencies,
        IReadOnlyList<CandidateDependencyEdge> Edges,
        IReadOnlyList<RejectedObject> Rejected)
    {
        // The subset of registered candidate ids that are authoritative competitors (§9). These are the
        // exact ids that must reach Core ScoreCandidates and remain stable through answer assembly.
        public IReadOnlyList<string> EligibleCandidateSemanticIds =>
            Candidates.Where(c => c.EligibleToCompete).Select(c => c.RepresentativeSemanticId).ToArray();

        public IReadOnlyList<string> DeferredCandidateSemanticIds =>
            Candidates.Where(c => !c.EligibleToCompete).Select(c => c.RepresentativeSemanticId).ToArray();

        // Valid only when at least one authoritative competitor survives registration; a plan whose
        // outcomes are ALL context/baseline/procedural/review is not fit to drive Core competition.
        public bool IsValid =>
            Candidates.Count > 0 && Dependencies.Count > 0 && EligibleCandidateSemanticIds.Count > 0;
    }

    // Machine-readable diagnostics (§10). Purely observational; drives the NORMALIZATION_GATE trace event.
    internal sealed record NormalizationGateDiagnostics(
        int RawProposalCount, int ValidatedOutcomeNodes, int NormalizedCandidates,
        int DuplicateMappings, int SharedDependencies, int ValidatedRelationCount,
        int RejectedObjectCount, IReadOnlyDictionary<string, int> RoleCounts,
        IReadOnlyDictionary<string, int> DecisionCounts,
        int EligibleCandidateCount, int DeferredCandidateCount);

    // Result of running the gate: the normalized proposal + remapped enrichment consumed by Core, plus
    // the validated plan and diagnostics.
    internal sealed record NormalizationGateResult(
        IReadOnlyList<ProposedCandidate> NormalizedProposal,
        SemanticProposalEnrichment RemappedEnrichment,
        LegalDecisionRegistrationPlan Plan,
        NormalizationGateDiagnostics Diagnostics);

    // ── Antonym pairs used for material-identity conflict detection. Presence of BOTH sides of a pair
    //    across two candidates in the SAME family forces KEEP_DISTINCT (they are materially opposed). ──
    private static readonly (string A, string B)[] QualifierAntonyms =
    [
        ("complete", "partial"), ("complete", "below"), ("full", "below"), ("full", "partial"),
        ("full", "reduced"), ("policylimits", "below"), ("related", "independent"),
        ("early", "late"), ("confidential", "public"), ("granted", "denied"),
        ("with", "without"),
    ];

    // Distinctive qualifier tokens that materially change a legal outcome's identity.
    private static readonly string[] IdentityQualifierTokens =
    [
        "complete", "full", "partial", "below", "reduced", "policylimits", "policy-limits",
        "confidential", "public", "mediated", "negotiated", "adjudicated", "contested",
        "settlement-related", "settlementrelated", "related", "independent", "voluntary",
        "involuntary", "early", "late", "with", "without", "prejudice", "granted", "denied",
        "default", "summary",
    ];

    // Entry point: run the full pre-Core normalization gate over a parsed proposal + enrichment.
    internal static NormalizationGateResult RunNormalizationGate(
        IReadOnlyList<ProposedCandidate> proposal, SemanticProposalEnrichment enrichment)
    {
        var rejected = new List<RejectedObject>();

        // 1) Normalize shared dependencies from the semanticRoots forest (attached to every candidate).
        var dependencies = NormalizeSharedDependencies(proposal);
        var dependencyIds = dependencies.Select(d => d.DependencyId).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // 2) Deterministic identity-based candidate normalization (MERGE / KEEP_DISTINCT / REQUIRES_REVIEW).
        var clusters = new List<MutableCluster>();
        var duplicateMappings = 0;
        foreach (var c in proposal)
        {
            var semanticId = string.IsNullOrWhiteSpace(c.SemanticCandidateId)
                ? $"C{clusters.Count + 1}"
                : c.SemanticCandidateId!;
            var role = ClassifyCandidate(c.DisplayName, c.Outcome);
            var identity = ComputeMaterialIdentity(c.DisplayName, c.Outcome);

            MutableCluster? mergeTarget = null;
            var decision = NormalizationDecision.KeepDistinct;
            foreach (var existing in clusters)
            {
                var d = DecideNormalization(existing.Identity, identity);
                if (d == NormalizationDecision.Merge)
                {
                    mergeTarget = existing;
                    decision = NormalizationDecision.Merge;
                    break;
                }
                if (d == NormalizationDecision.RequiresReview)
                {
                    // Overlapping but not identical: keep as its own reviewable cluster, but record the flag.
                    decision = NormalizationDecision.RequiresReview;
                }
            }

            if (mergeTarget is not null)
            {
                duplicateMappings++;
                mergeTarget.MergedSemanticIds.Add(semanticId);
                foreach (var o in c.OriginatingOutcomeNodeIds)
                    mergeTarget.OriginatingOutcomeNodeIds.Add(o);
            }
            else
            {
                clusters.Add(new MutableCluster(semanticId, c, role, identity, decision)
                {
                    OriginatingOutcomeNodeIds = new HashSet<string>(c.OriginatingOutcomeNodeIds, StringComparer.OrdinalIgnoreCase),
                });
            }
        }

        // 3) Build the merged-id → representative-id remap so enrichment relations still resolve.
        var remap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cluster in clusters)
            foreach (var merged in cluster.MergedSemanticIds)
                remap[merged] = cluster.RepresentativeSemanticId;

        var normalizedCandidates = clusters
            .Select(c => new NormalizedCandidate(
                c.RepresentativeSemanticId, c.Source.DisplayName, c.Role, c.Identity, c.Decision,
                c.MergedSemanticIds.ToArray(),
                c.OriginatingOutcomeNodeIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray()))
            .ToList();

        // 4) Validate ONLY the explicit candidate→dependency edges. Never manufacture a missing relation.
        var validCandidateIds = normalizedCandidates
            .Select(c => c.RepresentativeSemanticId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var edges = new List<CandidateDependencyEdge>();
        var seenEdges = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in enrichment.CandidateBranchRelations)
        {
            var resolvedCandidate = remap.TryGetValue(r.CandidateId, out var rep) ? rep : r.CandidateId;
            if (!validCandidateIds.Contains(resolvedCandidate))
            {
                rejected.Add(new RejectedObject("RELATION", $"{r.CandidateId}->{r.BranchId}", "UNKNOWN_CANDIDATE"));
                continue;
            }
            if (!dependencyIds.Contains(r.BranchId))
            {
                rejected.Add(new RejectedObject("RELATION", $"{r.CandidateId}->{r.BranchId}", "UNKNOWN_DEPENDENCY"));
                continue;
            }
            var key = $"{resolvedCandidate}|{r.BranchId}";
            if (!seenEdges.Add(key))
                continue;
            edges.Add(new CandidateDependencyEdge(resolvedCandidate, r.BranchId, r.RelationType, r.Rationale));
        }

        var plan = new LegalDecisionRegistrationPlan(normalizedCandidates, dependencies, edges, rejected);

        // 5) Rebuild the proposal so ONLY eligible representative candidates compete, with unioned
        //    outcome lineage. Ineligible candidates (baseline/procedural/dependency/REQUIRES_REVIEW) stay
        //    registered in the plan for provenance but are withheld from authoritative Core competition.
        var repById = clusters.ToDictionary(c => c.RepresentativeSemanticId, c => c, StringComparer.OrdinalIgnoreCase);
        var eligibleIds = normalizedCandidates
            .Where(c => c.EligibleToCompete)
            .Select(c => c.RepresentativeSemanticId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var normalizedProposal = new List<ProposedCandidate>();
        foreach (var cluster in clusters)
        {
            if (!eligibleIds.Contains(cluster.RepresentativeSemanticId))
            {
                rejected.Add(new RejectedObject(
                    "CANDIDATE", cluster.RepresentativeSemanticId, cluster.Decision == NormalizationDecision.RequiresReview
                        ? "IDENTITY_REQUIRES_REVIEW"
                        : $"NOT_ELIGIBLE_ROLE_{cluster.Role}".ToUpperInvariant()));
                continue;
            }
            var src = cluster.Source;
            normalizedProposal.Add(src with
            {
                OriginatingOutcomeNodeIds = cluster.OriginatingOutcomeNodeIds
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray(),
            });
        }

        // 6) Remap enrichment relations onto representative candidate ids (dedup after collapse).
        var remappedRelations = new List<SemanticCandidateBranchRelation>();
        var seenRelation = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in enrichment.CandidateBranchRelations)
        {
            var resolved = remap.TryGetValue(r.CandidateId, out var rep) ? rep : r.CandidateId;
            var key = $"{resolved}|{r.BranchId}|{r.RelationType}";
            if (!seenRelation.Add(key))
                continue;
            remappedRelations.Add(r with { CandidateId = resolved });
        }
        var remappedEnrichment = new SemanticProposalEnrichment(
            remappedRelations, enrichment.UnresolvedPropositions, enrichment.FactProvenance)
        {
            DecisionIntent = enrichment.DecisionIntent,
            OutcomeNodes = enrichment.OutcomeNodes,
        };

        var roleCounts = normalizedCandidates
            .GroupBy(c => c.Role.ToString())
            .ToDictionary(g => g.Key, g => g.Count());
        var decisionCounts = normalizedCandidates
            .GroupBy(c => c.Decision.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var diagnostics = new NormalizationGateDiagnostics(
            RawProposalCount: proposal.Count,
            ValidatedOutcomeNodes: enrichment.OutcomeNodes.Count,
            NormalizedCandidates: normalizedCandidates.Count,
            DuplicateMappings: duplicateMappings,
            SharedDependencies: dependencies.Count,
            ValidatedRelationCount: edges.Count,
            RejectedObjectCount: rejected.Count,
            RoleCounts: roleCounts,
            DecisionCounts: decisionCounts,
            EligibleCandidateCount: plan.EligibleCandidateSemanticIds.Count,
            DeferredCandidateCount: plan.DeferredCandidateSemanticIds.Count);

        return new NormalizationGateResult(normalizedProposal, remappedEnrichment, plan, diagnostics);
    }

    // ── Classification (§3): deterministic semantic role from the candidate title/outcome text. ──
    internal static CandidateSemanticRole ClassifyCandidate(string? displayName, string? outcome)
    {
        var text = $"{displayName} {outcome}".ToLowerInvariant();
        if (ContainsAny(text, "pending", "status quo", "statusquo", "awaiting", "current posture", "no action", "unresolved demand"))
            return CandidateSemanticRole.BaselineState;
        if (ContainsAny(text, "preparation", "prepare", "prepar", "discovery", "proceed to", "file suit", "trial prep", "litigation prep"))
            return CandidateSemanticRole.ProceduralPathway;
        if (ContainsAny(text, "dismiss", "default judgment", "procedural bar", "summary judgment", "nonsuit", "non-suit"))
            return CandidateSemanticRole.ProceduralConsequence;
        if (ContainsAny(text, "conditional", "contingent", "subject to", "if ", "provided that"))
            return CandidateSemanticRole.ConditionalResolution;
        if (ContainsAny(text, "dependency", "requires proof", "element of", "precondition"))
            return CandidateSemanticRole.DependencyProposition;
        return CandidateSemanticRole.SubstantiveResolution;
    }

    // ── Material legal identity (§4): family + polarity + distinctive qualifier tokens. ──────────
    internal static MaterialLegalIdentity ComputeMaterialIdentity(string? displayName, string? outcome)
    {
        var text = $"{displayName} {outcome}".ToLowerInvariant();
        var family = DetermineLegalFamily(text);
        var polarity = DeterminePolarity(text);
        var qualifiers = IdentityQualifierTokens
            .Where(q => text.Contains(q, StringComparison.OrdinalIgnoreCase))
            .Select(NormalizeQualifier)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return new MaterialLegalIdentity(family, polarity, qualifiers);
    }

    private static string DetermineLegalFamily(string text)
    {
        // Order matters: a "mediated settlement" is a negotiated settlement, an "adjudicated judgment" is
        // its own family, and a "dismissal" is a non-settlement termination — three materially distinct
        // families that must never collapse into one another.
        if (ContainsAny(text, "dismiss", "nonsuit", "non-suit", "withdraw", "abandon"))
            return "non_settlement_termination";
        if (ContainsAny(text, "adjudicat", "judgment", "verdict", "trial award", "court award"))
            return "adjudicated_resolution";
        if (ContainsAny(text, "settle", "settlement", "negotiat", "mediat", "compromise", "policy-limits", "policylimits", "demand"))
            return "negotiated_settlement";
        if (ContainsAny(text, "pending", "status quo", "statusquo", "baseline", "awaiting"))
            return "baseline_state";
        if (ContainsAny(text, "preparation", "prepar", "discovery", "litigation prep"))
            return "procedural_pathway";
        return "generic_resolution";
    }

    private static string DeterminePolarity(string text)
    {
        if (ContainsAny(text, "complete", "full", "policy-limits", "policylimits", "granted", "favorable"))
            return "high";
        if (ContainsAny(text, "below", "partial", "reduced", "denied", "adverse"))
            return "low";
        return "neutral";
    }

    private static string NormalizeQualifier(string q) => q.Replace("-", string.Empty);

    // Deterministic pairwise normalization decision based on material identity, NOT title similarity.
    internal static NormalizationDecision DecideNormalization(MaterialLegalIdentity a, MaterialLegalIdentity b)
    {
        // Different legal family ⇒ never merge (negotiated vs adjudicated vs dismissal stay distinct).
        if (!string.Equals(a.Family, b.Family, StringComparison.OrdinalIgnoreCase))
            return NormalizationDecision.KeepDistinct;

        // Materially opposed qualifiers within the same family ⇒ distinct (complete vs below-limits;
        // settlement-related vs independent dismissal).
        if (HasQualifierConflict(a.Qualifiers, b.Qualifiers))
            return NormalizationDecision.KeepDistinct;

        // Opposed polarity within the same family ⇒ distinct.
        if (!string.Equals(a.Polarity, b.Polarity, StringComparison.OrdinalIgnoreCase)
            && a.Polarity != "neutral" && b.Polarity != "neutral")
            return NormalizationDecision.KeepDistinct;

        // Identical fingerprint ⇒ genuine duplicate ⇒ MERGE.
        if (string.Equals(a.Signature, b.Signature, StringComparison.OrdinalIgnoreCase))
            return NormalizationDecision.Merge;

        // Same family + polarity, no conflict, but different distinctive qualifiers ⇒ overlapping but not
        // provably identical (confidential negotiated vs early mediated settlement) ⇒ REQUIRES_REVIEW.
        return NormalizationDecision.RequiresReview;
    }

    private static bool HasQualifierConflict(IReadOnlyList<string> a, IReadOnlyList<string> b)
    {
        foreach (var (x, y) in QualifierAntonyms)
        {
            var xa = NormalizeQualifier(x);
            var yb = NormalizeQualifier(y);
            if ((a.Contains(xa, StringComparer.OrdinalIgnoreCase) && b.Contains(yb, StringComparer.OrdinalIgnoreCase))
                || (a.Contains(yb, StringComparer.OrdinalIgnoreCase) && b.Contains(xa, StringComparer.OrdinalIgnoreCase)))
                return true;
        }
        return false;
    }

    // ── Shared dependency normalization (§5): reusable typed dependency identities from semanticRoots. ──
    private static IReadOnlyList<NormalizedDependency> NormalizeSharedDependencies(IReadOnlyList<ProposedCandidate> proposal)
    {
        var result = new List<NormalizedDependency>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        static void Walk(IReadOnlyList<ProposedBranch> branches, List<NormalizedDependency> acc, HashSet<string> seen)
        {
            foreach (var b in branches)
            {
                if (!string.IsNullOrWhiteSpace(b.SemanticBranchId) && seen.Add(b.SemanticBranchId!))
                    acc.Add(new NormalizedDependency(
                        b.SemanticBranchId!, b.DisplayName,
                        CategorizeDependency($"{b.DisplayName} {b.Interpretation}"), b.Interpretation));
                if (b.Children is { Count: > 0 })
                    Walk(b.Children, acc, seen);
            }
        }

        foreach (var c in proposal)
            Walk(c.Branches ?? Array.Empty<ProposedBranch>(), result, seen);
        return result;
    }

    private static DependencyCategory CategorizeDependency(string text)
    {
        var t = text.ToLowerInvariant();
        if (ContainsAny(t, "coverage", "policy", "liability", "notice", "duty", "element", "statute", "enforceab", "agreement", "contract"))
            return DependencyCategory.Legal;
        if (ContainsAny(t, "damages", "amount", "value", "economic", "cost", "wage", "bill", "lien"))
            return DependencyCategory.Economic;
        if (ContainsAny(t, "evidence", "proof", "record", "document", "witness", "testimony"))
            return DependencyCategory.Evidentiary;
        if (ContainsAny(t, "procedure", "procedural", "filing", "deadline", "service", "jurisdiction"))
            return DependencyCategory.Procedural;
        return DependencyCategory.Factual;
    }

    private static bool ContainsAny(string text, params string[] needles)
    {
        foreach (var n in needles)
            if (text.Contains(n, StringComparison.OrdinalIgnoreCase))
                return true;
        return false;
    }

    // Mutable scratch cluster used while resolving identity; projected to the immutable record afterwards.
    private sealed class MutableCluster(
        string representativeSemanticId, ProposedCandidate source,
        CandidateSemanticRole role, MaterialLegalIdentity identity, NormalizationDecision decision)
    {
        public string RepresentativeSemanticId { get; } = representativeSemanticId;
        public ProposedCandidate Source { get; } = source;
        public CandidateSemanticRole Role { get; } = role;
        public MaterialLegalIdentity Identity { get; } = identity;
        public NormalizationDecision Decision { get; } = decision;
        public List<string> MergedSemanticIds { get; } = [];
        public HashSet<string> OriginatingOutcomeNodeIds { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    // ── Test seam: run the gate directly over raw DECISION_DISCOVERY_V2 JSON. ────────────────────
    internal static NormalizationGateResult RunNormalizationGateForTest(string json, int maxCandidates)
    {
        var proposal = AdaptSemanticProposal(json, maxCandidates);
        var enrichment = AdaptSemanticEnrichment(json);
        return RunNormalizationGate(proposal, enrichment);
    }
}
