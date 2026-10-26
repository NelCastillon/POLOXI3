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
    // collapsed by default for every candidate EXCEPT the leading one (C1), which starts expanded so the
    // attorney immediately sees the full reasoning for the top outcome. We track explicit user overrides so
    // toggling still works in both directions; absent an override the default rule (only C1 expanded) applies.
    private const int LeadingAssessmentIndex = 1;
    private readonly Dictionary<int, bool> _ovAssessmentOverride = [];
    internal bool IsAssessmentExpanded(int index) =>
        _ovAssessmentOverride.TryGetValue(index, out var expanded) ? expanded : index == LeadingAssessmentIndex;
    internal void ToggleAssessment(int index)
        => _ovAssessmentOverride[index] = !IsAssessmentExpanded(index);

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

    // One legal-authority citation surfaced under a candidate assessment. Projected (never fabricated)
    // from the run's already-proposed legal authorities (DaAuthorities) whose name/tokens genuinely appear
    // in THIS candidate's narrative, branch, or synthesis text. SearchUrl is an external lookup link used
    // when no stored reference/URL exists for the authority.
    internal sealed record OvCitation(
        string Name,
        string Kind,
        string VerificationStatus,
        string SearchUrl);

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
        IReadOnlyList<OvCitation> Citations,
        int SupportPct,
        bool IsLeader,
        bool CompetitionValid,
        string ViabilityLabel,
        string ViabilityTone);

    // Builds an external search URL for an authority when no stored reference/URL exists, so the attorney
    // can still look the citation up. Uses a neutral web search over the exact authority text.
    internal static string OvCitationSearchUrl(string authorityName)
        => $"https://www.google.com/search?q={Uri.EscapeDataString(authorityName)}";

    // Matches the run's proposed legal authorities to a single candidate by checking whether the authority
    // name (or its distinctive tokens) genuinely appears in that candidate's narrative / full analysis /
    // branch / detail text. Pure projection over persisted values — empty when nothing matches, never
    // fabricated and never duplicated.
    private IReadOnlyList<OvCitation> OvCitationsForCandidate(OvOutcome o)
    {
        var authorities = DaAuthorities;
        if (authorities.Count == 0) return [];

        // Assemble the candidate-specific text corpus from persisted values only.
        var corpus = new System.Text.StringBuilder();
        void AddText(string? text) { if (!string.IsNullOrWhiteSpace(text)) corpus.Append(' ').Append(text); }
        AddText(o.Title);
        AddText(o.Detail);
        AddText(o.CardText);
        AddText(o.Narrative);
        foreach (var seg in o.FullNarrative)
        {
            AddText(seg.Label);
            AddText(seg.Text);
        }
        foreach (var b in o.BranchScores)
            AddText(b.BranchDisplayName);

        var haystack = corpus.ToString();
        if (string.IsNullOrWhiteSpace(haystack)) return [];

        var matched = new List<OvCitation>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var a in authorities)
        {
            if (string.IsNullOrWhiteSpace(a.Name) || !seen.Add(a.Name)) continue;
            if (!OvAuthorityAppearsInText(a.Name, haystack)) continue;
            matched.Add(new OvCitation(a.Name, a.Kind, a.VerificationStatus, OvCitationSearchUrl(a.Name)));
        }
        return matched;
    }

    // True when an authority name genuinely appears in the candidate text: either the full name is present,
    // or every distinctive token (length >= 4, numbers kept) of the authority appears. This avoids matching
    // on common filler words while remaining resilient to citation punctuation differences.
    private static bool OvAuthorityAppearsInText(string authorityName, string haystack)
    {
        if (haystack.Contains(authorityName, StringComparison.OrdinalIgnoreCase)) return true;

        var tokens = authorityName
            .Split([' ', ',', '.', ';', ':', '(', ')', '§', '-', '/', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Where(t => t.Length >= 4 || t.Any(char.IsDigit))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (tokens.Length == 0) return false;
        return tokens.All(t => haystack.Contains(t, StringComparison.OrdinalIgnoreCase));
    }

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

                // Opposing conditions: ALWAYS present. Every candidate scored in the Candidate × Branch
                // competition carries per-dimension LLM judgments oriented to ITS OWN outcome, so a dimension
                // the model scored below the decision threshold for this candidate IS a real condition that
                // argues against THIS outcome (for a Plaintiff candidate: the dimensions favouring the defense;
                // on the mirror candidate the same dimensions reverse meaning). We merge any title-joined
                // required-dependency gaps on top so nothing authoritative is lost. There is always an
                // opposing view — a settled matter would not have produced competing candidates.
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
                foreach (var b in o.BranchScores
                             .Where(b => b.EvidenceScore < 0.6m)
                             .OrderBy(b => b.EvidenceScore))
                {
                    if (seenOppose.Add(b.BranchDisplayName))
                        opposing.Add($"{b.BranchDisplayName} — weak support for this outcome ({b.EvidenceScore:P0})");
                }
                // Guarantee a contrary view even when every dimension scored strongly for the leader: the
                // strongest-scored competing outcome is itself the standing opposition to THIS candidate.
                if (opposing.Count == 0)
                {
                    var rival = outcomes
                        .Where(x => x.Index != o.Index)
                        .OrderByDescending(x => x.SupportPct)
                        .FirstOrDefault();
                    if (rival is not null)
                        opposing.Add($"{rival.Title} — competing outcome holding {rival.SupportPct}% relative support");
                }
                opposing = opposing.Take(6).ToList();

                // THIS candidate's own factor vocabulary — the required dependencies and scored branch
                // dimensions that actually belong to this outcome. Used to scope the otherwise matter-global
                // flip / question / resolution facets down to this candidate, so each assessment reflects its
                // own outcome (e.g. a factor that supports one outcome contradicts the opposite one) instead
                // of repeating one identical matter-wide list on every card.
                var candidateFactorNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (dep is not null)
                    foreach (var d in dep.Required)
                        candidateFactorNames.Add(d.FactorName);
                foreach (var b in o.BranchScores)
                    candidateFactorNames.Add(b.BranchDisplayName);

                bool MentionsCandidateFactor(string? text) =>
                    !string.IsNullOrWhiteSpace(text)
                    && candidateFactorNames.Any(n => !string.IsNullOrWhiteSpace(n)
                        && text.Contains(n, StringComparison.OrdinalIgnoreCase));

                // Unresolved questions: resolution targets that concern THIS candidate's factors. Falls back
                // to the matter-wide frontier only when none of them name this candidate's factors, so the
                // card is never empty but prefers outcome-specific questions.
                var scopedQuestions = resolution
                    .Where(t => !string.IsNullOrWhiteSpace(t.ResolutionQuestion)
                        && (MentionsCandidateFactor(t.ResolutionQuestion) || MentionsCandidateFactor(t.PropositionText)))
                    .Select(t => t.ResolutionQuestion)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(4)
                    .ToList();
                var questions = scopedQuestions.Count > 0
                    ? scopedQuestions
                    : resolution
                        .Where(t => !string.IsNullOrWhiteSpace(t.ResolutionQuestion))
                        .Select(t => t.ResolutionQuestion)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Take(4)
                        .ToList();

                // Flip conditions: contradicted / unverified factors that could reopen THIS candidate. Prefer
                // the candidate's own unresolved/contradicted required dependencies (these already carry the
                // candidate-specific polarity), then add any matter-wide flip risks that name this candidate's
                // factors. A factor that is merely unsupported for the opposite outcome is not surfaced here.
                var flipItems = new List<string>();
                var seenFlip = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (dep is not null)
                {
                    foreach (var d in dep.Required.Where(r =>
                                 !string.Equals(r.State, "SUPPORTED", StringComparison.OrdinalIgnoreCase)
                                 && !string.Equals(r.Tone, "ok", StringComparison.OrdinalIgnoreCase)))
                    {
                        if (seenFlip.Add(d.FactorName))
                            flipItems.Add($"{d.FactorName} — {d.State}");
                    }
                }
                foreach (var f in flips.Where(f => candidateFactorNames.Contains(f.Name)))
                {
                    if (seenFlip.Add(f.Name))
                        flipItems.Add($"{f.Name} — {f.StateLabel}");
                }
                // Always include the contested (mid-range) Candidate × Branch dimensions — these are the LLM-
                // scored factors most able to flip THIS outcome (strongly-decided dimensions are settled, while
                // dimensions near the decision threshold could reopen the result if new evidence shifts them).
                // Oriented to this candidate: a dimension that could flip the Plaintiff outcome is, on the
                // mirror candidate, the dimension that could flip the defense.
                foreach (var b in o.BranchScores
                             .Where(b => b.EvidenceScore >= 0.35m && b.EvidenceScore <= 0.75m)
                             .OrderBy(b => Math.Abs(b.EvidenceScore - 0.5m)))
                {
                    if (seenFlip.Add(b.BranchDisplayName))
                        flipItems.Add($"{b.BranchDisplayName} — contested at {b.EvidenceScore:P0}; new evidence here could reopen this outcome");
                }
                // Guarantee a flip path even when every dimension is decisively scored: closing the margin to
                // the nearest competing outcome is itself the standing way this result could flip.
                if (flipItems.Count == 0)
                {
                    var rival = outcomes
                        .Where(x => x.Index != o.Index)
                        .OrderByDescending(x => x.SupportPct)
                        .FirstOrDefault();
                    if (rival is not null)
                        flipItems.Add($"New evidence closing the margin to {rival.Title} ({rival.SupportPct}% relative support) could flip this outcome");
                }
                flipItems = flipItems.Take(4).ToList();

                // Verification & Dependencies: the Candidate × Branch dimensions ARE this outcome's essential
                // conditions. A dimension the LLM scored strongly for this outcome is an established dependency;
                // a weakly scored one is still unverified. We prefer authoritative title-joined dependency rows
                // when present and otherwise read the competition the model actually ran, so the cards always
                // carry real values — never a bare "0 of 0".
                var branchDeps = o.BranchScores;
                var branchEstablished = branchDeps.Count(b => b.EvidenceScore >= 0.6m);
                var branchUnverified = branchDeps.Count(b => b.EvidenceScore < 0.6m);
                var branchCritical = branchDeps.Count(b => b.EvidenceScore < 0.4m);

                var established = dep?.Established ?? branchEstablished;
                var totalDeps = dep?.Total ?? (branchDeps.Count > 0 ? branchDeps.Count : o.CriticalDependencies);

                // Dependency facet items: the actual per-outcome dependencies with their established/unverified
                // state, so the card is itemized (not a bare count) and reflects THIS candidate's conditions.
                var dependencyItems = new List<string>();
                if (dep is not null)
                {
                    foreach (var d in dep.Required.Take(6))
                        dependencyItems.Add($"{d.FactorName} — {d.State}");
                }
                else
                {
                    foreach (var b in branchDeps.OrderByDescending(b => b.EvidenceScore).Take(6))
                        dependencyItems.Add($"{b.BranchDisplayName} — {(b.EvidenceScore >= 0.6m ? "established" : "unverified")} ({b.EvidenceScore:P0})");
                }
                var dependencyCritical = dep is not null ? o.CriticalDependencies : branchCritical;
                var verificationMissing = dep is not null ? o.MissingEvidence : branchUnverified;

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

                // Next resolution targets: prefer this candidate's own targets, fall back to the frontier.
                var scopedResolution = resolution
                    .Where(t => MentionsCandidateFactor(t.ResolutionQuestion) || MentionsCandidateFactor(t.PropositionText))
                    .ToList();
                var nextTargets = (scopedResolution.Count > 0 ? scopedResolution : resolution.ToList())
                    .Take(4)
                    .Select(t => t.InformationValue.HasValue
                        ? $"{t.ResolutionQuestion} (IV {t.InformationValue.Value:0.00})"
                        : t.ResolutionQuestion)
                    .ToList();

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
                        $"{established} of {Math.Max(totalDeps, established)} required dependency(ies) established; {verificationMissing} missing or unverified.", []),
                    new("dependencies", "bi-diagram-2", "Dependencies",
                        totalDeps > 0 ? $"{totalDeps} essential dependency(ies), {dependencyCritical} critical." : "No critical dependencies recorded.",
                        dependencyItems),
                    new("questions", "bi-question-circle", "Unresolved questions",
                        questions.Count > 0 ? $"{questions.Count} open question(s) to resolve." : "No outstanding decision-material questions.",
                        questions),
                    new("poloxi", "bi-cpu", "POLOXI assessment", poloxiSummary, []),
                    new("flip", "bi-shuffle", "Flip conditions",
                        flipItems.Count > 0 ? "Information that could materially strengthen, weaken, or reopen this outcome." : "No contradicted or unresolved factors could currently flip the decision.",
                        flipItems),
                    new("next", "bi-flag", "Next resolution targets",
                        questions.Count > 0 ? "Highest-value items to investigate or resolve next." : "No prioritized resolution targets yet.",
                        nextTargets),
                };

                var scopeLabel = competitionValid
                    ? (o.IsLeader ? "Current leader" : "Competing outcome")
                    : "Proposed outcome";
                var scopeTone = competitionValid ? (o.IsLeader ? "ok" : "info") : "muted";

                var synthesis = BuildCandidateSynthesis(o, competitionValid, supporting.Count, opposing.Count, questions.Count);

                var citations = OvCitationsForCandidate(o);

                list.Add(new OvCandidateAssessment(
                    o.Index,
                    o.Title,
                    scopeLabel,
                    scopeTone,
                    synthesis,
                    o.Narrative,
                    o.FullNarrative,
                    facets,
                    citations,
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
