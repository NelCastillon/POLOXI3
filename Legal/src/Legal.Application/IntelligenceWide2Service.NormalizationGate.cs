using Legal.Application.Features.Intelligence;
using Microsoft.Extensions.Logging;

namespace Legal.Application;

// ───────────────────────────────────────────────────────────────────────────────────────────────
// R4 — Reachable integration seam: route the REAL Personal Injury matter execution (the dynamic
// SearchDynamicAsync pipeline behind /legal/personalinjury_decision2) through the SHARED
// Outcome–Dependency Normalization Gate that lives in LegalDecisionService.
//
// The dedicated LegalDecisionService.DecidePersonalInjuryAsync path already runs the gate, but the PI
// UI does not reach it — it executes the dynamic Wide2 pipeline, which competes a flat string
// candidate universe and never registers a normalized candidate/dependency pool. This adapter projects
// the material the dynamic pipeline already has (proposed dispositions + surviving decision branches +
// the supplied CurrentOutcome/RequestedDisposition) into the gate's input types, runs the deterministic
// gate, and returns the validated registration plan + diagnostics + the eligible representative
// candidate names to drive the existing candidate competition.
//
// It reuses LegalDecisionService.RunNormalizationGate verbatim: NO scoring, NO evidence, NO Core math.
// Strictly EVALUATE + flag gated by the caller; fully fail-soft (returns null on any error).
// ───────────────────────────────────────────────────────────────────────────────────────────────
public sealed partial class IntelligenceWide2Service
{
    // Result of routing the dynamic pipeline through the shared gate. EligibleCandidateNames are the
    // representative display names that must drive the candidate competition when the plan is valid.
    internal sealed record LegalNormalizationOutcome(
        LegalDecisionService.LegalDecisionRegistrationPlan Plan,
        LegalDecisionService.NormalizationGateResult GateResult,
        IReadOnlyList<string> EligibleCandidateNames,
        int OutcomeNodeCount,
        int ProposedCandidateCount);

    // Project the dynamic-pipeline material into the gate's ProposedCandidate/enrichment shape, run the
    // shared normalization gate, and return the normalized outcome. Fail-soft: any exception yields null
    // so the caller degrades to the unchanged competition. Caller guarantees EVALUATE + flag gating.
    internal LegalNormalizationOutcome? RunLegalDecisionNormalizationGate(
        IReadOnlyCollection<string> candidateUniverse,
        IReadOnlyCollection<WideBranchRecord> survivors,
        IReadOnlyCollection<WideInterpretiveResultDto> interpretiveResults)
    {
        try
        {
            var proposal = BuildGateProposal(candidateUniverse, survivors, interpretiveResults, out var outcomeNodeCount);
            if (proposal.Count == 0)
                return null;
            var enrichment = BuildGateEnrichment(proposal, survivors);
            var gate = LegalDecisionService.RunNormalizationGate(proposal, enrichment);
            var eligibleIds = gate.Plan.EligibleCandidateSemanticIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Map eligible representative semantic ids back to the display names the competition consumes,
            // preserving normalized identity (merged origins collapse to one representative name).
            var eligibleNames = gate.NormalizedProposal
                .Select(c => c.DisplayName)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return new LegalNormalizationOutcome(gate.Plan, gate, eligibleNames, outcomeNodeCount, proposal.Count);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Wide2 legal-decision normalization gate failed for matter {MatterId}; degrading to unchanged candidate competition.", _matterContext?.MatterId);
            return null;
        }
    }

    // Build the gate's global candidate pool. Each competing disposition becomes ONE ProposedCandidate
    // with a deterministic originating outcome-node id (DISCOVERY provenance only, never evidence). The
    // supplied CurrentOutcome (a recorded ASSERTION) and RequestedDisposition (the user's OBJECTIVE) are
    // added as DISTINCT candidates so the gate can keep them separate and let them compete — neither is
    // ever promoted to an established outcome. Surviving decision branches are attached to every
    // candidate as the shared dependency forest so NormalizeSharedDependencies produces reusable deps.
    private IReadOnlyList<LegalDecisionService.ProposedCandidate> BuildGateProposal(
        IReadOnlyCollection<string> candidateUniverse,
        IReadOnlyCollection<WideBranchRecord> survivors,
        IReadOnlyCollection<WideInterpretiveResultDto> interpretiveResults,
        out int outcomeNodeCount)
    {
        // Phase A — collect the ordered, deduped competing-outcome candidates FIRST (name + outcome).
        // These are the mutually-exclusive dispositions/outcomes; none is a shared dependency factor.
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var planned = new List<(string Name, string Outcome)>();

        void PlanCandidate(string? name, string outcome)
        {
            if (string.IsNullOrWhiteSpace(name))
                return;
            var trimmed = name.Trim();
            if (seenNames.Add(trimmed))
                planned.Add((trimmed, outcome));
        }

        // 1) Supplied matter assertions/objectives compete as distinct candidates (never merged/promoted).
        var suppliedOutcome = FindMatterFieldValue("Current Outcome");
        var requestedDisposition = FindMatterFieldValue("Requested Disposition");
        PlanCandidate(suppliedOutcome, "[SUPPLIED CURRENT OUTCOME — recorded assertion, not a verified ruling]");
        PlanCandidate(requestedDisposition, "[REQUESTED DISPOSITION — user objective, not an established outcome]");

        // 2) Proposed competing dispositions harvested by the dynamic pipeline.
        foreach (var name in candidateUniverse)
            PlanCandidate(name, "Proposed competing disposition.");

        // 3) Distinct interpretive item names (competing outcome alternatives surfaced under branches).
        foreach (var result in interpretiveResults)
            foreach (var item in result.Items)
                PlanCandidate(item.Name, string.IsNullOrWhiteSpace(item.Detail) ? result.Interpretation : item.Detail);

        // Phase B — build the shared dependency forest, EXCLUDING any branch that is itself one of the
        // competing outcomes above (verb-led OR noun-form). Only genuine sub-issues become factors.
        var outcomeNames = planned.Select(p => p.Name).ToArray();
        var branches = BuildDependencyBranches(survivors, outcomeNames);

        // Phase C — construct the candidates, all sharing the (now outcome-free) dependency forest.
        var proposal = new List<LegalDecisionService.ProposedCandidate>();
        var nodeSeq = 0;
        foreach (var (name, outcome) in planned)
        {
            var id = $"WC{proposal.Count + 1}";
            var nodeId = $"O{++nodeSeq}";
            proposal.Add(new LegalDecisionService.ProposedCandidate(
                name, outcome, 0, 0, 0, 0, 0, 0, 0, branches)
            {
                SemanticCandidateId = id,
                OriginatingOutcomeNodeIds = [nodeId],
            });
        }

        outcomeNodeCount = nodeSeq;
        return proposal;
    }

    // Project the surviving DECISION-DEPENDENCY branches (procedural/legal/factual/evidentiary/economic
    // sub-issues) into the gate's ProposedBranch forest. Two exclusions guarantee a competing OUTCOME
    // never becomes a shared factor: (a) IsDispositionBranch catches verb-led dispositions, and
    // (b) any branch whose DisplayName is (or materially restates) a proposed competing outcome — this
    // catches noun-form outcomes ("Confidential Settlement", "Plaintiff Verdict") the verb heuristic
    // misses. Each surviving branch id is the stable BranchCode so relations resolve.
    private IReadOnlyList<LegalDecisionService.ProposedBranch> BuildDependencyBranches(
        IReadOnlyCollection<WideBranchRecord> survivors,
        IReadOnlyCollection<string>? competingOutcomeNames = null)
    {
        var outcomeTokenSets = (competingOutcomeNames ?? Array.Empty<string>())
            .Select(NormalizationTokens)
            .Where(t => t.Count > 0)
            .ToArray();

        var branches = new List<LegalDecisionService.ProposedBranch>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in survivors)
        {
            if (b.IsEliminated || IsDispositionBranch(b))
                continue;
            if (MatchesCompetingOutcome(b.DisplayName, outcomeTokenSets))
                continue;
            var id = string.IsNullOrWhiteSpace(b.BranchCode) ? $"B{branches.Count + 1}" : b.BranchCode;
            if (!seen.Add(id))
                continue;
            branches.Add(new LegalDecisionService.ProposedBranch(
                b.DisplayName ?? id, b.Interpretation ?? string.Empty, 0, 0, 0, [])
            {
                SemanticBranchId = id,
            });
        }
        return branches;
    }

    // Build ONLY relevance-justified candidate→factor relations. The former unconditional all-to-all
    // DEPENDS_ON mapping is removed: an edge is emitted for a (candidate, factor) pair ONLY when the
    // factor's proposition is materially relevant to that candidate (deterministic token overlap between
    // the candidate outcome text and the factor label/question). Each edge carries a semantic relation
    // type (REQUIRED / SUPPORTS / CONDITIONAL) and a recorded rationale. A shared factor legitimately
    // relates to several candidates, but never to candidates whose outcome it does not touch.
    private LegalDecisionService.SemanticProposalEnrichment BuildGateEnrichment(
        IReadOnlyList<LegalDecisionService.ProposedCandidate> proposal,
        IReadOnlyCollection<WideBranchRecord> survivors)
    {
        var outcomeNames = proposal.Select(c => c.DisplayName).ToArray();
        var branches = BuildDependencyBranches(survivors, outcomeNames);

        var relations = new List<LegalDecisionService.SemanticCandidateBranchRelation>();
        foreach (var c in proposal)
        {
            var candidateTokens = NormalizationTokens($"{c.DisplayName} {c.Outcome}");
            foreach (var b in branches)
            {
                var factorTokens = NormalizationTokens($"{b.DisplayName} {b.Interpretation}");
                var shared = SharedStemCount(candidateTokens, factorTokens);
                if (shared == 0)
                    continue; // Factor proposition does not touch this candidate — no relationship.

                var (relationType, rationale) = ClassifyFactorRelation(shared, b.DisplayName, c.DisplayName);
                relations.Add(new LegalDecisionService.SemanticCandidateBranchRelation(
                    c.SemanticCandidateId!, b.SemanticBranchId!, relationType, rationale));
            }
        }
        return new LegalDecisionService.SemanticProposalEnrichment(relations, [], []);
    }

    // Deterministic relation classification: stronger token overlap => REQUIRED, otherwise SUPPORTS.
    // (CONDITIONAL is reserved for a single incidental token touch.) Zero-LLM, provenance recorded.
    private static (string RelationType, string Rationale) ClassifyFactorRelation(
        int sharedTokenCount, string factorLabel, string candidateName)
    {
        var relation = sharedTokenCount >= 2 ? "REQUIRED" : "SUPPORTS";
        var rationale = $"'{factorLabel}' shares {sharedTokenCount} material term(s) with candidate '{candidateName}'; relevance-derived ({relation}).";
        return (relation, rationale);
    }

    // Whether a branch DisplayName is (or materially restates) one of the competing outcomes. A branch
    // is treated as an outcome when it shares ALL of its significant tokens with an outcome, or the
    // outcome shares all of its significant tokens with the branch (subset either direction).
    private static bool MatchesCompetingOutcome(string? displayName, IReadOnlyCollection<IReadOnlyCollection<string>> outcomeTokenSets)
    {
        if (string.IsNullOrWhiteSpace(displayName) || outcomeTokenSets.Count == 0)
            return false;
        var branchTokens = NormalizationTokens(displayName);
        if (branchTokens.Count == 0)
            return false;
        foreach (var outcome in outcomeTokenSets)
        {
            var shared = branchTokens.Intersect(outcome, StringComparer.OrdinalIgnoreCase).Count();
            if (shared == 0)
                continue;
            // Subset in either direction => the branch names the outcome, not a distinct sub-issue.
            if (shared == branchTokens.Count || shared == outcome.Count)
                return true;
        }
        return false;
    }

    private static readonly HashSet<string> NormalizationStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","of","for","and","or","to","in","is","are","was","were","status","value",
        "any","all","with","on","by","at","this","that","its","their","whether","did","does",
    };

    private static IReadOnlyCollection<string> NormalizationTokens(string text)
        => (text ?? string.Empty)
            .Split([' ', '\t', '-', '—', ',', '/', '(', ')', ':', ';', '.', '?', '\''],
                StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length > 2 && !NormalizationStopWords.Contains(t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    // Count materially-shared terms allowing morphological variants (e.g. "confidential" vs
    // "confidentiality", "settle" vs "settlement") by comparing conservative stems. Deterministic and
    // zero-LLM: two tokens match when their stems are equal or one stem is a prefix of the other and the
    // shared stem is at least 5 chars long (avoids accidental short-prefix collisions).
    private static int SharedStemCount(IReadOnlyCollection<string> left, IReadOnlyCollection<string> right)
    {
        var rightStems = right.Select(Stem).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var count = 0;
        foreach (var l in left.Select(Stem).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var r in rightStems)
            {
                if (string.Equals(l, r, StringComparison.OrdinalIgnoreCase)
                    || (Math.Min(l.Length, r.Length) >= 5
                        && (l.StartsWith(r, StringComparison.OrdinalIgnoreCase)
                            || r.StartsWith(l, StringComparison.OrdinalIgnoreCase))))
                {
                    count++;
                    break;
                }
            }
        }
        return count;
    }

    // Conservative suffix-stripping stem (no external stemmer). Trims a few common legal-morphology
    // suffixes so related word forms collapse to a shared root.
    private static string Stem(string token)
    {
        var t = token;
        foreach (var suffix in StemSuffixes)
        {
            if (t.Length - suffix.Length >= 4 && t.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                return t[..^suffix.Length];
        }
        return t;
    }

    private static readonly string[] StemSuffixes =
        ["ability", "ibility", "ment", "tion", "sion", "ness", "ing", "ed", "es", "s"];
}
