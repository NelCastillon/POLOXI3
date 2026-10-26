using System.Text;
using System.Text.Json;
using Legal.Application.Features.Intelligence;
using Microsoft.Extensions.Logging;

namespace Legal.Application;

// On-demand, metered attorney-style Decision Memorandum composer. This is a DEDICATED LLM call that is
// distinct from the per-run Candidate Landscape pass: it is triggered explicitly by the user from the
// Decision Intelligence Overview, it runs against the FULL persisted run context (never a fresh analysis),
// and it is advisory presentation only. It never re-ranks outcomes, never introduces facts/authorities not
// present in the saved result, and plainly discloses provisional status when readiness gates are open.
public sealed partial class IntelligenceWide2Service
{
    private const string DecisionMemoSchema = """
{
  "type": "object",
  "properties": {
    "issue": { "type": "string" },
    "shortAnswer": { "type": "string" },
    "analysis": { "type": "string" },
    "evidenceAndAuthority": { "type": "string" },
    "risks": { "type": "array", "maxItems": 8, "items": { "type": "string" } },
    "openItems": { "type": "array", "maxItems": 10, "items": { "type": "string" } },
    "recommendedNextSteps": { "type": "array", "maxItems": 8, "items": { "type": "string" } },
    "disclaimer": { "type": "string" }
  },
  "required": ["issue", "shortAnswer", "analysis", "evidenceAndAuthority", "risks", "openItems", "recommendedNextSteps", "disclaimer"],
  "additionalProperties": false
}
""";

    private sealed record DecisionMemoProposal(
        string? Issue,
        string? ShortAnswer,
        string? Analysis,
        string? EvidenceAndAuthority,
        IReadOnlyList<string>? Risks,
        IReadOnlyList<string>? OpenItems,
        IReadOnlyList<string>? RecommendedNextSteps,
        string? Disclaimer);

    public async Task<DecisionMemoDto?> ComposeDecisionMemoAsync(Guid tenantId, Guid analysisRunId, WideSearchResponse savedResponse, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(savedResponse);

        var matterTitle = savedResponse.Query;
        var leader = savedResponse.Candidates.OrderBy(c => c.RankNumber).FirstOrDefault();
        var leadingOutcome = leader?.DisplayName ?? "No leading outcome resolved";
        var hasEvidence = savedResponse.Evidence.Count > 0 || savedResponse.EnterpriseEvidenceCount > 0 || savedResponse.ExternalEvidenceCount > 0;
        var attorneyReady = hasEvidence
            && savedResponse.Candidates.Count > 0
            && string.Equals(savedResponse.AnswerVerificationCode, "VERIFIED", StringComparison.OrdinalIgnoreCase);
        var readinessLabel = attorneyReady ? "Decision ready" : "Review required";

        var correlationId = string.IsNullOrWhiteSpace(savedResponse.Query)
            ? analysisRunId.ToString("N")
            : $"memo-{analysisRunId:N}";
        var modelCode = savedResponse.ModelCodeUsed;

        try
        {
            var systemPrompt = await promptCatalog.GetSystemPromptAsync(tenantId, IntelligencePromptCodes.WideDecisionMemo, cancellationToken);
            var userPrompt = BuildDecisionMemoContext(savedResponse, leadingOutcome, readinessLabel, attorneyReady);

            var result = await aiProviderRouter.GenerateAsync(
                tenantId,
                "INTELLIGENCE_WIDE_LANDSCAPE_ANSWER",
                systemPrompt,
                userPrompt,
                DecisionMemoSchema,
                correlationId,
                new("Intelligence", null, null, savedResponse.Query, "WIDE_DECISION_MEMO", null, correlationId, "Decision Memorandum"),
                string.IsNullOrWhiteSpace(modelCode) ? "gpt-6-astra" : modelCode,
                cancellationToken);

            var proposal = JsonSerializer.Deserialize<DecisionMemoProposal>(result.Content, JsonOptions);
            if (proposal is null) return BuildFallbackMemo(matterTitle, leadingOutcome, readinessLabel, attorneyReady, savedResponse, modelCode);

            return new DecisionMemoDto(
                MatterTitle: matterTitle,
                LeadingOutcome: leadingOutcome,
                ReadinessLabel: readinessLabel,
                AttorneyReady: attorneyReady,
                Issue: Coalesce(proposal.Issue, savedResponse.Query),
                ShortAnswer: Coalesce(proposal.ShortAnswer, savedResponse.FinalAnswer ?? leadingOutcome),
                Analysis: Coalesce(proposal.Analysis, "No analysis was produced for this run."),
                EvidenceAndAuthority: Coalesce(proposal.EvidenceAndAuthority, hasEvidence ? "Evidence was admitted; see the Evidence tab for details." : "No verified evidence has been admitted for this run."),
                Risks: CleanList(proposal.Risks),
                OpenItems: CleanList(proposal.OpenItems).Count > 0 ? CleanList(proposal.OpenItems) : DefaultOpenItems(attorneyReady),
                RecommendedNextSteps: CleanList(proposal.RecommendedNextSteps).Count > 0 ? CleanList(proposal.RecommendedNextSteps) : DefaultNextSteps(savedResponse),
                Disclaimer: Coalesce(proposal.Disclaimer, DefaultDisclaimer(attorneyReady)),
                ModelCodeUsed: modelCode);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Decision memorandum composition failed for analysis run {AnalysisRunId}; returning deterministic fallback. {Message}", analysisRunId, ex.Message);
            return BuildFallbackMemo(matterTitle, leadingOutcome, readinessLabel, attorneyReady, savedResponse, modelCode);
        }
    }

    // Builds the grounded, read-only context block from the persisted run. Zero new reasoning happens here;
    // the composer only explains the state it is handed.
    private static string BuildDecisionMemoContext(WideSearchResponse r, string leadingOutcome, string readinessLabel, bool attorneyReady)
    {
        var sb = new StringBuilder();
        sb.AppendLine("DECISION QUESTION");
        sb.AppendLine(r.Query);
        sb.AppendLine();
        sb.AppendLine("RUN STATE (authoritative; do not reinterpret)");
        sb.AppendLine($"- Leading outcome: {leadingOutcome}");
        sb.AppendLine($"- Readiness: {readinessLabel} (attorneyReady={attorneyReady.ToString().ToLowerInvariant()})");
        sb.AppendLine($"- Answer verification: {r.AnswerVerificationCode}");
        sb.AppendLine($"- Final confidence: {r.FinalConfidence:0.##}");
        if (r.DecisionConfidence is { } dc) sb.AppendLine($"- Decision confidence: {dc:0.##}");
        sb.AppendLine($"- Enterprise evidence: {r.EnterpriseEvidenceCount}; External evidence: {r.ExternalEvidenceCount}; Total admitted: {r.Evidence.Count}");
        sb.AppendLine($"- Decision evidence coverage: {r.DecisionEvidenceCoverage:0.##}");
        if (!string.IsNullOrWhiteSpace(r.FinalAnswer))
        {
            sb.AppendLine();
            sb.AppendLine("POLOXI FINAL ANSWER (presentation input; do not change its substance)");
            sb.AppendLine(r.FinalAnswer);
        }

        if (r.Candidates.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("COMPETING OUTCOMES (ranked)");
            foreach (var c in r.Candidates.OrderBy(c => c.RankNumber).Take(12))
            {
                sb.AppendLine($"- #{c.RankNumber} {c.DisplayName} (support {c.CompositeScore:0.##}){(string.IsNullOrWhiteSpace(c.Detail) ? null : $": {c.Detail}")}");
            }
        }

        if (r.Evidence.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("ADMITTED EVIDENCE (retrieved; verification state varies)");
            foreach (var e in r.Evidence.OrderBy(e => e.RankNumber).Take(16))
            {
                sb.AppendLine($"- {e.Title}{(string.IsNullOrWhiteSpace(e.Excerpt) ? null : $" — {Trim(e.Excerpt, 240)}")}");
            }
        }

        if (r.ProposedLegalAuthorities.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("PROPOSED LEGAL AUTHORITIES (leads only; verification state shown)");
            foreach (var a in r.ProposedLegalAuthorities.Take(16))
            {
                sb.AppendLine($"- [{a.Kind}] {a.Name} ({a.VerificationStatus}) — {a.Relevance}");
            }
        }

        if (r.SuggestedActions.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("SUGGESTED NEXT ACTIONS (from the run)");
            foreach (var s in r.SuggestedActions.Take(10))
            {
                sb.AppendLine($"- {s.DisplayName}: {s.Rationale}");
            }
        }

        if (r.MatterContext is { } mc && !string.IsNullOrWhiteSpace(mc.SerializedContext))
        {
            sb.AppendLine();
            sb.AppendLine("MATTER CONTEXT (immutable snapshot)");
            sb.AppendLine(Trim(mc.SerializedContext, 6000));
        }

        return sb.ToString();
    }

    private DecisionMemoDto BuildFallbackMemo(string matterTitle, string leadingOutcome, string readinessLabel, bool attorneyReady, WideSearchResponse r, string? modelCode)
    {
        var hasEvidence = r.Evidence.Count > 0 || r.EnterpriseEvidenceCount > 0 || r.ExternalEvidenceCount > 0;
        return new DecisionMemoDto(
            MatterTitle: matterTitle,
            LeadingOutcome: leadingOutcome,
            ReadinessLabel: readinessLabel,
            AttorneyReady: attorneyReady,
            Issue: r.Query,
            ShortAnswer: r.FinalAnswer ?? leadingOutcome,
            Analysis: r.Candidates.Count > 0
                ? $"The analysis currently leads toward {leadingOutcome}, competing against {r.Candidates.Count - 1} other outcome(s). This is a provisional working position derived from the persisted run; the full model-authored memorandum could not be generated for this request."
                : "No competing outcomes were resolved for this run.",
            EvidenceAndAuthority: hasEvidence
                ? $"{r.Evidence.Count} evidence item(s) were admitted (enterprise {r.EnterpriseEvidenceCount}, external {r.ExternalEvidenceCount}); verification state varies by item. See the Evidence tab for details."
                : "No verified evidence has been admitted for this run.",
            Risks: ["The model-authored memorandum could not be generated; this fallback reflects persisted scoring only."],
            OpenItems: DefaultOpenItems(attorneyReady),
            RecommendedNextSteps: DefaultNextSteps(r),
            Disclaimer: DefaultDisclaimer(attorneyReady),
            ModelCodeUsed: modelCode);
    }

    private static IReadOnlyList<string> DefaultOpenItems(bool attorneyReady) =>
        attorneyReady
            ? ["Confirm operative claims, motion evidence, and authority citations before filing."]
            : ["Operative claims remain unresolved.", "Motion evidence is incomplete.", "Authoritative legal support is unverified.", "Candidate dependencies remain unresolved."];

    private static IReadOnlyList<string> DefaultNextSteps(WideSearchResponse r) =>
        r.SuggestedActions.Count > 0
            ? r.SuggestedActions.Take(6).Select(a => a.DisplayName).ToArray()
            : ["Review the admitted evidence and resolve outstanding dependencies before advancing the decision."];

    private static string DefaultDisclaimer(bool attorneyReady) =>
        attorneyReady
            ? "This is an AI-generated working memorandum summarizing the completed analysis. Confirm all citations and evidence before relying on it for a filed legal conclusion."
            : "This is an AI-generated working memorandum, not an attorney-ready filed legal conclusion. Readiness gates remain open: operative claims, motion evidence, authoritative legal support, and candidate dependencies are not yet fully resolved.";

    private static string Coalesce(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();

    private static IReadOnlyList<string> CleanList(IReadOnlyList<string>? items) =>
        items is null ? [] : items.Where(i => !string.IsNullOrWhiteSpace(i)).Select(i => i.Trim()).ToArray();

    private static string Trim(string value, int max) =>
        value.Length <= max ? value : value[..max] + "…";
}
