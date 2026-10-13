using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Web.Components.Pages;

// ──────────────────────────────────────────────────────────────────────────────────────────────
// Overview candidate-assessment synthesis + Matter Analysis projections.
//
// These are PURE projections over the already-restored decision session (_response) and the existing
// Overview projections (OvOutcomes, OvWhatCouldFlipIt, OvResolutionTargets, OvReadinessBlockers,
// CdcRecentActivity, OvExecutiveSummary, …). Because OvOutcomes is itself a projection of the restored
// session, opening an old run restores this assessment EXACTLY, and generating a new synthesis produces
// a new version — no new persistence is required (session persistence already exists in the run ledger).
//
// Candidate outcomes are kept STRICTLY separate from L1 factors: every assessment row is keyed off a
// decision CANDIDATE (OvOutcome), and the L1→Ln propositions are only GATHERED beneath each candidate
// as supporting / opposing conditions. A shared factor may support one candidate and undermine another;
// its effect is recorded separately for each candidate. Repeated propositions are de-duplicated per
// candidate while contradictions are retained. Nothing here fabricates data — empty sources render as
// honest empty-states.
// ──────────────────────────────────────────────────────────────────────────────────────────────
public partial class LegalPersonalInjuryDecision2
{
    // Expand/collapse state for the synthesis-first candidate assessment cards (by candidate index). The
    // synthesis is always visible; the supporting detail (13 facets + original branch interpretations) is
    // EXPANDED BY DEFAULT so attorneys immediately see the full reasoning and lineage. We track the
    // explicitly COLLAPSED indices so the default (absent from the set) renders expanded.
    private readonly HashSet<int> _ovAssessmentCollapsed = [];
    internal bool IsAssessmentExpanded(int index) => !_ovAssessmentCollapsed.Contains(index);
    internal void ToggleAssessment(int index)
    {
        if (!_ovAssessmentCollapsed.Remove(index))
            _ovAssessmentCollapsed.Add(index);
    }

    // Whether the reusable Matter Analysis block is expanded on the Overview tab (collapsed by default).
    private bool _ovMatterAnalysisExpanded;
    internal bool OvMatterAnalysisExpanded => _ovMatterAnalysisExpanded;
    internal void ToggleMatterAnalysis() => _ovMatterAnalysisExpanded = !_ovMatterAnalysisExpanded;

    // One facet (detail row) of a candidate assessment. Summary is the readable one-liner; Items are the
    // expandable supporting detail (propositions, evidence, questions, …). Empty Items render as a muted note.
    internal sealed record OvAssessmentFacet(
        string Key,
        string Icon,
        string Label,
        string Summary,
        IReadOnlyList<string> Items);

    // A complete per-candidate assessment: a readable synthesis first, the original branch interpretations
    // preserved beneath (Interpretations = OvOutcome.FullNarrative), and the expandable 13-facet detail.
    internal sealed record OvCandidateAssessment(
        int Index,
        string Title,
        string ScopeLabel,
        string ScopeTone,
        string Synthesis,
        string? Narrative,
        IReadOnlyList<(string Label, string Text)> Interpretations,
        IReadOnlyList<OvAssessmentFacet> Facets,
        int SupportPct,
        bool IsLeader,
        bool CompetitionValid,
        string ViabilityLabel,
        string ViabilityTone);

    // The assembled candidate assessment across EVERY relevant L1→Ln branch, one row per decision candidate.
    // Built from the same OvOutcomes pool the Decision Outcomes cards use, so it is always consistent with
    // the restored run and never collapses a candidate into an L1 heading.
    internal IReadOnlyList<OvCandidateAssessment> OvCandidateAssessments
    {
        get
        {
            var outcomes = OvOutcomes;
            if (outcomes.Count == 0) return [];

            var competitionValid = OvCompetitionValid;
            var flips = OvWhatCouldFlipIt;
            var resolution = OvResolutionTargets;
            var list = new List<OvCandidateAssessment>(outcomes.Count);

            foreach (var o in outcomes)
            {
                var dep = OvDependencyFor(o.Title);

                // Supporting conditions: established required dependencies + this candidate's strongest scored
                // branch dimensions. De-duplicated per candidate (a proposition appears at most once here).
                var supporting = new List<string>();
                var seenSupport = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (dep is not null)
                {
                    foreach (var d in dep.Required.Where(r =>
                                 string.Equals(r.State, "SUPPORTED", StringComparison.OrdinalIgnoreCase)
                                 || string.Equals(r.Tone, "ok", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (seenSupport.Add(d.FactorName))
                            supporting.Add(d.FactorName);
                    }
                }
                foreach (var b in o.BranchScores.Where(b => b.EvidenceScore >= 0.6m).Take(4))
                {
                    if (seenSupport.Add(b.BranchDisplayName))
                        supporting.Add($"{b.BranchDisplayName} ({b.EvidenceScore:P0} scored support)");
                }

                // Opposing conditions: unresolved / contradicted required dependencies for THIS candidate.
                var opposing = new List<string>();
                var seenOppose = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (dep is not null)
                {
                    foreach (var d in dep.Required.Where(r =>
                                 !string.Equals(r.State, "SUPPORTED", StringComparison.OrdinalIgnoreCase)
                                 && !string.Equals(r.Tone, "ok", StringComparison.OrdinalIgnoreCase)))
                    {
                        var label = string.IsNullOrWhiteSpace(d.Obligation)
                            ? $"{d.FactorName} — {d.State}"
                            : $"{d.FactorName} — {d.State} ({d.Obligation})";
                        if (seenOppose.Add(d.FactorName))
                            opposing.Add(label);
                    }
                }

                // Unresolved questions: the next resolution targets, shown against this candidate for context.
                var questions = resolution
                    .Where(t => !string.IsNullOrWhiteSpace(t.ResolutionQuestion))
                    .Select(t => t.ResolutionQuestion)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(4)
                    .ToList();

                // Flip conditions: contradicted / unverified factors that could reopen the candidate.
                var flipItems = flips
                    .Select(f => $"{f.Name} — {f.StateLabel}")
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(4)
                    .ToList();

                var established = dep?.Established ?? o.KeySupportingFactors;
                var totalDeps = dep?.Total ?? o.CriticalDependencies;

                // Per-candidate evidence & factor references: the Factor Inventory rows mapped to THIS
                // candidate plus the admitted document/authority sources, so the assessment card surfaces
                // the same proof layer shown in the Admitted evidence and Factor Inventory panels.
                var evidenceItems = OvEvidenceAndFactorsFor(o.Title);
                var evidenceSummary = o.EvidenceCoverage > 0m
                    ? $"{o.EvidenceCoverage:P0} evidence coverage across scored dimensions"
                    : (evidenceItems.Count > 0
                        ? $"{evidenceItems.Count} linked factor/evidence reference(s); none independently verified yet (UNVERIFIED)"
                        : "No independently verified evidence admitted yet (UNVERIFIED)");

                var poloxiSummary = competitionValid
                    ? $"POLOXI competition ran — {o.SupportPct}% relative support, {o.ViabilityLabel.ToLowerInvariant()}"
                    : $"No Candidate × Branch competition ran — advisory support only ({o.SupportPct}%)";

                var mapped = o.MappedOutcomes.Count > 0
                    ? o.MappedOutcomes.Select(m => $"{m.Code} · {m.Name}").ToList()
                    : new List<string>();

                var facets = new List<OvAssessmentFacet>
                {
                    new("scope", "bi-bullseye", "Outcome and scope",
                        string.IsNullOrWhiteSpace(o.Detail) ? o.CardText : o.Detail!,
                        mapped.Count > 0 ? new List<string> { "Mapped canonical outcome(s):" }.Concat(mapped).ToList() : []),
                    new("interpretation", "bi-lightbulb", "Interpretative analysis",
                        string.IsNullOrWhiteSpace(o.Narrative) ? o.CardText : o.Narrative!,
                        o.FullNarrative.Select(s => string.IsNullOrWhiteSpace(s.Label) ? s.Text : $"{s.Label}: {s.Text}").ToList()),
                    new("supporting", "bi-check2-circle", "Supporting conditions",
                        supporting.Count > 0 ? $"{supporting.Count} relevant proposition(s) support this outcome." : "No scored supporting conditions yet.",
                        supporting),
                    new("opposing", "bi-shield-exclamation", "Opposing conditions",
                        opposing.Count > 0 ? $"{opposing.Count} unresolved or contrary condition(s)." : "No opposing conditions currently recorded.",
                        opposing),
                    new("evidence", "bi-paperclip", "Evidence and sources", evidenceSummary, evidenceItems),
                    new("verification", "bi-patch-check", "Verification",
                        $"{established} of {Math.Max(totalDeps, established)} required dependency(ies) established; {o.MissingEvidence} missing or unverified.", []),
                    new("dependencies", "bi-diagram-2", "Dependencies",
                        totalDeps > 0 ? $"{totalDeps} essential dependency(ies), {o.CriticalDependencies} critical." : "No critical dependencies recorded.",
                        []),
                    new("questions", "bi-question-circle", "Unresolved questions",
                        questions.Count > 0 ? $"{questions.Count} open question(s) to resolve." : "No outstanding decision-material questions.",
                        questions),
                    new("poloxi", "bi-cpu", "POLOXI assessment", poloxiSummary, []),
                    new("flip", "bi-shuffle", "Flip conditions",
                        flipItems.Count > 0 ? "Information that could materially strengthen, weaken, or reopen this outcome." : "No contradicted or unresolved factors could currently flip the decision.",
                        flipItems),
                    new("next", "bi-flag", "Next resolution targets",
                        questions.Count > 0 ? "Highest-value items to investigate or resolve next." : "No prioritized resolution targets yet.",
                        resolution.Take(4).Select(t =>
                            t.InformationValue.HasValue
                                ? $"{t.ResolutionQuestion} (IV {t.InformationValue.Value:0.00})"
                                : t.ResolutionQuestion).ToList()),
                };

                var scopeLabel = competitionValid
                    ? (o.IsLeader ? "Current leader" : "Competing outcome")
                    : "Proposed outcome";
                var scopeTone = competitionValid ? (o.IsLeader ? "ok" : "info") : "muted";

                var synthesis = BuildCandidateSynthesis(o, competitionValid, supporting.Count, opposing.Count, questions.Count);

                list.Add(new OvCandidateAssessment(
                    o.Index,
                    o.Title,
                    scopeLabel,
                    scopeTone,
                    synthesis,
                    o.Narrative,
                    o.FullNarrative,
                    facets,
                    o.SupportPct,
                    o.IsLeader,
                    competitionValid,
                    o.ViabilityLabel,
                    o.ViabilityTone));
            }

            return list;
        }
    }

    // Readable, grounded synthesis sentence for one candidate — built only from persisted run values.
    private string BuildCandidateSynthesis(OvOutcome o, bool competitionValid, int supportingCount, int opposingCount, int questionCount)
    {
        if (!string.IsNullOrWhiteSpace(o.Narrative))
            return o.Narrative!;

        var lead = competitionValid
            ? (o.IsLeader ? "leads the field" : "competes")
            : "is proposed";
        var supportText = competitionValid ? $" at {o.SupportPct}% relative support" : string.Empty;
        return $"{o.Title} {lead}{supportText} with {o.ViabilityLabel.ToLowerInvariant()}. "
            + $"{supportingCount} supporting condition(s), {opposingCount} opposing condition(s), "
            + $"and {questionCount} unresolved question(s) across all relevant branches.";
    }

    // Resolve this candidate's required-dependency summary by title (candidate identity — never an L1 node).
    private DfiCandidateDependencySummary? OvDependencyFor(string title) =>
        DfiCandidateDependencySummaries.FirstOrDefault(s =>
            string.Equals(s.CandidateTitle, title, StringComparison.OrdinalIgnoreCase));

    // Gathers THIS candidate's proof layer for the "Evidence and sources" facet: the Factor Inventory rows
    // whose candidate mapping is this candidate (or shared "All outcomes") plus the admitted document /
    // legal-authority sources. Read-only projection over the restored run — never fabricates evidence; an
    // empty list renders as the honest UNVERIFIED empty-state summary.
    private IReadOnlyList<string> OvEvidenceAndFactorsFor(string title)
    {
        var items = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 1) Factor Inventory rows mapped to this candidate (or shared across all outcomes).
        foreach (var f in DfiFactors)
        {
            var cand = DfiFactorCandidate(f);
            var sharedOrMine = string.Equals(cand, title, StringComparison.OrdinalIgnoreCase)
                || string.Equals(cand, "All outcomes", StringComparison.OrdinalIgnoreCase);
            if (!sharedOrMine) continue;

            var relation = DfiFactorRelation(f);
            var label = $"{f.FactorName} · {f.NodeKind} · {relation} · {f.Availability}/{f.VerificationStatus}";
            if (seen.Add(label))
                items.Add(label);
            if (items.Count >= 8) break;
        }

        // 2) Admitted document / legal-authority sources (same admitted set the Evidence panel shows).
        var retrieved = (_response?.ExternalKnowledge ?? (IReadOnlyCollection<WideExternalKnowledgeSnippet>)Array.Empty<WideExternalKnowledgeSnippet>())
            .Where(DfiIsAdmitted)
            .GroupBy(DfiSourceKey)
            .Select(g => g.OrderByDescending(s => s.PropositionSupportScore).ThenByDescending(s => s.Score).First())
            .OrderByDescending(s => s.PropositionSupportScore)
            .Take(6);
        foreach (var snip in retrieved)
        {
            var titleText = string.IsNullOrWhiteSpace(snip.Title) ? (snip.Url ?? "(untitled source)") : snip.Title;
            var kind = string.IsNullOrWhiteSpace(snip.AuthorityKind) ? "Admitted" : snip.AuthorityKind;
            var label = $"{titleText} · {kind} · support {snip.PropositionSupportScore:P0}";
            if (seen.Add(label))
                items.Add(label);
        }

        return items;
    }

    // ── Matter Analysis ───────────────────────────────────────────────────────────────────────────
    // Reuses the SAME candidate assessment to explain the matter: active decisions / plausible outcomes,
    // what supports and undermines each, what is unknown or disputed, what changed, what to resolve next,
    // and which lifecycle requirements are affected. Projection-only; restored exactly with the run.
    internal sealed record OvMatterAnalysisSection(string Icon, string Title, string Lead, IReadOnlyList<string> Points);

    internal bool OvMatterAnalysisAvailable => OvOutcomes.Count > 0;

    internal string OvMatterAnalysisSummary => OvExecutiveSummary;

    internal IReadOnlyList<OvMatterAnalysisSection> OvMatterAnalysisSections
    {
        get
        {
            var assessments = OvCandidateAssessments;
            if (assessments.Count == 0) return [];

            var competitionValid = OvCompetitionValid;
            var sections = new List<OvMatterAnalysisSection>();

            // Active decisions & plausible outcomes.
            sections.Add(new OvMatterAnalysisSection(
                "bi-diagram-3",
                "Active decisions & plausible outcomes",
                competitionValid
                    ? "Outcomes that remain plausible after the Candidate × Branch competition."
                    : "Proposed outcomes — competition has not yet established a leader.",
                assessments.Select(a =>
                    competitionValid
                        ? $"C{a.Index} {a.Title} — {a.SupportPct}% support, {a.ViabilityLabel.ToLowerInvariant()}"
                        : $"C{a.Index} {a.Title} — proposed, {a.ViabilityLabel.ToLowerInvariant()}").ToList()));

            // What supports and undermines each outcome.
            var supportUndermine = new List<string>();
            foreach (var a in assessments)
            {
                var sup = a.Facets.FirstOrDefault(f => f.Key == "supporting");
                var opp = a.Facets.FirstOrDefault(f => f.Key == "opposing");
                supportUndermine.Add($"C{a.Index} {a.Title}: {sup?.Summary ?? "no supporting conditions"} / {opp?.Summary ?? "no opposing conditions"}");
            }
            sections.Add(new OvMatterAnalysisSection(
                "bi-scale",
                "What supports and undermines each outcome",
                "Shared factors may support one outcome and undermine another; each effect is recorded per candidate.",
                supportUndermine));

            // What remains unknown or disputed.
            var unknown = new List<string>();
            unknown.AddRange(OvReadinessBlockers);
            foreach (var f in OvWhatCouldFlipIt.Take(4))
                unknown.Add($"{f.Name} — {f.StateLabel}");
            sections.Add(new OvMatterAnalysisSection(
                "bi-question-octagon",
                "What remains unknown or disputed",
                unknown.Count > 0 ? "Open blockers, contradictions, and unverified conditions." : "No material unknowns are currently recorded.",
                unknown.Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList()));

            // What changed after new information arrived.
            var changes = CdcRecentActivity
                .Take(5)
                .Select(a => string.IsNullOrWhiteSpace(a.Detail) ? a.Title : $"{a.Title} — {a.Detail}")
                .ToList();
            sections.Add(new OvMatterAnalysisSection(
                "bi-arrow-repeat",
                "What changed after new information arrived",
                changes.Count > 0 ? "Recent decision-affecting activity since the prior run." : "No decision-affecting changes recorded yet.",
                changes));

            // What the attorney should resolve next.
            var next = OvResolutionTargets
                .Take(5)
                .Select(t => t.InformationValue.HasValue
                    ? $"{t.ResolutionQuestion} (IV {t.InformationValue.Value:0.00})"
                    : t.ResolutionQuestion)
                .ToList();
            if (next.Count == 0)
                next = OvNextBestActions.Take(5).Select(a => a.Title).ToList();
            sections.Add(new OvMatterAnalysisSection(
                "bi-flag",
                "What the attorney should resolve next",
                next.Count > 0 ? "Highest-value resolution targets, in priority order." : "No prioritized resolution targets yet.",
                next));

            // Which lifecycle requirements are affected.
            var lifecycle = new List<string>();
            if (!string.IsNullOrWhiteSpace(_matter?.StatusCode))
                lifecycle.Add($"Matter status: {_matter!.StatusCode}");
            if (!string.IsNullOrWhiteSpace(_matter?.Posture ?? _matter?.RequestedDisposition))
                lifecycle.Add($"Posture / requested disposition: {_matter!.Posture ?? _matter!.RequestedDisposition}");
            lifecycle.Add($"Decision readiness: {OvReadinessPct}% ({OvDecisionStateLabel})");
            sections.Add(new OvMatterAnalysisSection(
                "bi-signpost-split",
                "Lifecycle requirements affected",
                "How the current assessment affects the matter lifecycle.",
                lifecycle));

            return sections;
        }
    }
}
