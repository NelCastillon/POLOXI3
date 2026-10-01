using System.Text;
using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Epistemic;

namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// Next Best Action (NBA) — thin application-layer composition over existing POLOXI / HRR outputs.
//
// Ownership split (the user's architecture):
//   POLOXI       : WHAT matters most next?        → MatterPropositionInformationValue (VIV / ADV frontier)
//   CHR / APR    : WHAT must be true / resolved?  → ResolutionRequirement (unresolved + missing info)
//   HRR          : WHERE in the structure?        → HierarchyPath (advisory display lineage)
//   DomainPack   : WHICH domain actions resolve?  → ResolvedDomainPack action vocabulary
//   NBA Resolver : WHICH feasible action next?    → LLM proposes, DETERMINISTIC gate selects
//
// NBA NEVER recalculates IV / ADV / flip potential and NEVER mutates proposition state. It reuses the
// authoritative scores and composes them into decision-directed actions. It is advisory/display-only
// and fails soft: any error yields an empty result so the cockpit degrades to its existing empty state.
// The LLM is UNTRUSTED — the deterministic eligibility gate (not the model) decides which actions qualify.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

// One unresolved, decision-material proposition expressed as something that must be resolved. Assembled
// from existing state only (no new scoring). Application-layer DTO — not persisted.
public sealed record ResolutionRequirement(
    Guid PropositionId,
    string PropositionText,
    IReadOnlyList<string> HierarchyPath,
    string UnresolvedQuestion,
    string MissingInformation,
    string FactStateCode,
    string VerificationStateCode,
    IReadOnlyList<string> SupportingEvidence,
    IReadOnlyList<string> ContradictingEvidence,
    decimal DecisionRelevance,
    decimal Uncertainty,
    decimal LegalAdv);

// A single ranked next best action. Title/Context/InformationSought are prose the UI renders directly.
public sealed record NextBestAction(
    Guid PropositionId,
    string Title,
    string InformationSought,
    string? Context,
    string TargetProposition,
    IReadOnlyList<string> HierarchyPath,
    decimal Adv,
    string ImpactLabel,
    string ImpactTone);

public sealed record NextBestActionResult(
    Guid MatterId,
    IReadOnlyList<NextBestAction> Actions,
    string? NotRunReason = null);

public interface INextBestActionService
{
    Task<NextBestActionResult> GenerateAsync(
        Guid tenantId,
        Guid matterId,
        Guid decisionSessionId,
        Guid? actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed class NextBestActionService(
    IMatterPropositionInformationValueService informationValueService,
    ILegalDocumentCorpusRepository corpusRepository,
    ILegalDecisionRepository decisionRepository,
    IDomainPackResolver domainPackResolver,
    IAiProviderRouter aiProviderRouter) : INextBestActionService
{
    // A proposition is "unresolved" when its fact state is not ESTABLISHED (proven) or INVALIDATED (refuted).
    private static readonly HashSet<string> ResolvedFactStates =
        new(StringComparer.OrdinalIgnoreCase) { LegalFactStates.Established, LegalFactStates.Invalidated };

    // Decision-materiality floor: below this, a proposition is not worth an action even if unresolved.
    private const decimal DecisionMaterialityFloor = 0.20m;

    // Generic, non-targeted action phrasing that is never admissible — an action must seek SPECIFIC
    // information tied to an unresolved decision-structure condition, not a catch-all like "do discovery".
    private static readonly string[] GenericActionMarkers =
    [
        "conduct discovery", "additional discovery", "further discovery", "more discovery",
        "gather more information", "gather more evidence", "investigate further",
        "do more research", "research the case", "review the file", "review the case",
        "continue investigating", "look into it", "general investigation", "as needed",
    ];

    public async Task<NextBestActionResult> GenerateAsync(
        Guid tenantId,
        Guid matterId,
        Guid decisionSessionId,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // 1) POLOXI — reuse the authoritative VIV / ADV frontier. We never recompute these scores.
            var scored = await informationValueService.ScoreAsync(
                tenantId, matterId, decisionSessionId, actorUserId, persist: false, cancellationToken);
            if (scored.Propositions.Count == 0)
                return new NextBestActionResult(matterId, [], "No scored propositions yet.");

            var maxIv = Math.Max(0.001m, scored.Propositions.Max(p => p.InformationValue));

            // 2) HRR / evidence — the unresolved decision path and current evidence state for each proposition.
            var graph = await corpusRepository.GetMatterEvidenceGraphAsync(tenantId, matterId, cancellationToken);
            var evidenceById = (graph.Evidence ?? [])
                .GroupBy(e => e.LegalEvidenceItemId)
                .ToDictionary(g => g.Key, g => g.First().Summary);
            var propositionById = (graph.Propositions ?? [])
                .GroupBy(p => p.LegalFactPropositionId)
                .ToDictionary(g => g.Key, g => g.First());

            // 3) Domain pack — jurisdiction-scoped, domain-appropriate action vocabulary (semantics only).
            var matter = await decisionRepository.GetMatterAsync(tenantId, matterId, cancellationToken);
            var packCode = string.IsNullOrWhiteSpace(matter?.DomainPackCode)
                ? DecisionDomainPackCodes.PersonalInjury
                : matter!.DomainPackCode;
            var pack = await domainPackResolver.ResolveAsync(tenantId, packCode, cancellationToken);

            // 4) CHR / APR — assemble ResolutionRequirements from unresolved, decision-material propositions.
            var requirements = BuildResolutionRequirements(scored, propositionById, evidenceById, maxIv, matter, pack);
            if (requirements.Count == 0)
                return new NextBestActionResult(matterId, [], "No outstanding unresolved decision-material propositions.");

            // 5) NBA — the LLM proposes domain-appropriate candidate actions for those requirements…
            var proposals = await ProposeActionsAsync(tenantId, matter, pack, requirements, cancellationToken);

            // 6) …and the DETERMINISTIC eligibility gate (not the model) selects the winning actions.
            var actions = SelectEligibleActions(proposals, requirements);
            if (actions.Count == 0)
            {
                // There IS an unresolved frontier (requirements > 0) but no action survived. Explain why instead
                // of implying the matter is fully resolved, so the cockpit shows an honest, actionable state.
                var reason = proposals.Count == 0
                    ? $"{requirements.Count} unresolved decision-material proposition(s) remain, but no next best action could be generated for them right now."
                    : $"{requirements.Count} unresolved decision-material proposition(s) remain; proposed actions were too generic or untargeted to admit.";
                return new NextBestActionResult(matterId, actions, reason);
            }
            return new NextBestActionResult(matterId, actions);
        }
        catch (Exception ex)
        {
            // Advisory overlay must never break the cockpit; degrade to the existing empty state.
            return new NextBestActionResult(matterId, [], $"Next best actions are temporarily unavailable: {ex.Message}");
        }
    }

    // ── CHR / APR: build the unresolved-requirement set from existing scored + graph state ───────────
    private static IReadOnlyList<ResolutionRequirement> BuildResolutionRequirements(
        MatterPropositionInformationValueResult scored,
        IReadOnlyDictionary<Guid, LegalEvidenceGraphPropositionDto> propositionById,
        IReadOnlyDictionary<Guid, string> evidenceById,
        decimal maxIv,
        DecisionMatterDto? matter,
        ResolvedDomainPack pack)
    {
        var requirements = new List<ResolutionRequirement>();
        foreach (var p in scored.Propositions
                     .Where(p => IsUnresolved(p.FactStateCode) && p.DecisionImpact >= DecisionMaterialityFloor)
                     .OrderByDescending(p => p.InformationValue))
        {
            propositionById.TryGetValue(p.PropositionId, out var graphProp);
            var support = graphProp?.Support ?? [];
            var supporting = support
                .Where(s => string.Equals(s.RelationshipTypeCode, LegalDocumentRelationshipTypes.Supports, StringComparison.OrdinalIgnoreCase))
                .Select(s => evidenceById.TryGetValue(s.LegalEvidenceItemId, out var text) ? text : null)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .ToArray();
            var contradicting = support
                .Where(s => string.Equals(s.RelationshipTypeCode, LegalDocumentRelationshipTypes.Contradicts, StringComparison.OrdinalIgnoreCase))
                .Select(s => evidenceById.TryGetValue(s.LegalEvidenceItemId, out var text) ? text : null)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .ToArray();

            var adv = Math.Round(p.InformationValue / maxIv, 2);
            requirements.Add(new ResolutionRequirement(
                p.PropositionId,
                p.PropositionText,
                BuildHierarchyPath(matter, pack, p),
                BuildUnresolvedQuestion(p.PropositionText),
                DescribeMissingInformation(p.FactStateCode, supporting.Length, contradicting.Length),
                p.FactStateCode,
                p.VerificationStateCode,
                supporting,
                contradicting,
                p.DecisionImpact,
                p.Uncertainty,
                adv));
        }

        // Present the full unresolved decision-material frontier (highest-value first). The model is
        // asked about every requirement so the cockpit can list all next best actions, not just a preview.
        return requirements;
    }

    private static bool IsUnresolved(string? factStateCode)
        => string.IsNullOrWhiteSpace(factStateCode) || !ResolvedFactStates.Contains(factStateCode);

    // Advisory HRR lineage: domain → jurisdiction → dimension → proposition. Display-only.
    private static IReadOnlyList<string> BuildHierarchyPath(DecisionMatterDto? matter, ResolvedDomainPack pack, MatterPropositionInformationValue p)
    {
        var path = new List<string>();
        if (!string.IsNullOrWhiteSpace(pack.PracticeAreaCode))
            path.Add(pack.PracticeAreaCode.Replace('_', ' '));
        var jurisdiction = matter?.State ?? matter?.Jurisdiction;
        if (!string.IsNullOrWhiteSpace(jurisdiction))
            path.Add(jurisdiction);
        var posture = matter?.Posture ?? matter?.RequestedDisposition;
        if (!string.IsNullOrWhiteSpace(posture))
            path.Add(posture);
        return path;
    }

    private static string BuildUnresolvedQuestion(string propositionText)
        => $"What establishes whether: {propositionText.TrimEnd('.')}?";

    private static string DescribeMissingInformation(string? factStateCode, int supportingCount, int contradictingCount)
    {
        if (contradictingCount > 0 && supportingCount > 0)
            return "Conflicting evidence on record; an independent source is needed to resolve the conflict.";
        if (contradictingCount > 0)
            return "Only contradicting evidence on record; corroborating independent evidence is missing.";
        if (supportingCount == 0)
            return "No admitted evidence yet; independent evidence is needed to establish this proposition.";
        return "Independent corroboration is needed to move this proposition to established.";
    }

    // ── NBA: ask the LLM for domain-appropriate candidate actions (fail-soft) ───────────────────────
    private sealed record ActionProposalEnvelope(IReadOnlyList<ActionProposal>? Actions);
    private sealed record ActionProposal(string? PropositionId, string? Title, string? InformationSought, string? Context);

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Build the actions output schema so maxItems tracks the supplied unresolved frontier (one action per
    // proposition), instead of a fixed small cap.
    private static string BuildActionsSchema(int maxItems) =>
        $$"""
        {
          "type": "object",
          "properties": {
            "actions": {
              "type": "array",
              "maxItems": {{Math.Max(maxItems, 1)}},
              "items": {
                "type": "object",
                "properties": {
                  "propositionId": { "type": "string" },
                  "title": { "type": "string" },
                  "informationSought": { "type": "string" },
                  "context": { "type": ["string", "null"] }
                },
                "required": ["propositionId", "title", "informationSought", "context"],
                "additionalProperties": false
              }
            }
          },
          "required": ["actions"],
          "additionalProperties": false
        }
        """;

    private async Task<IReadOnlyList<ActionProposal>> ProposeActionsAsync(
        Guid tenantId,
        DecisionMatterDto? matter,
        ResolvedDomainPack pack,
        IReadOnlyList<ResolutionRequirement> requirements,
        CancellationToken cancellationToken)
    {
        try
        {
            var system =
                "You are a legal decision-support assistant proposing the NEXT BEST ACTION to resolve " +
                "specific unresolved conditions in a decision structure. Each action MUST target exactly one " +
                "of the supplied propositions (by propositionId) and MUST state the SPECIFIC information it " +
                "would obtain to resolve that proposition's missing information. Prefer actions grounded in the " +
                "domain-appropriate information sources provided. Do NOT propose generic actions such as " +
                "'conduct discovery' or 'investigate further'. Propose one materially distinct action for EACH " +
                "supplied proposition that warrants one, the most decision-impactful first. Do not predetermine " +
                "the outcome — an action seeks information, it does not assume what that information will show.";

            var result = await aiProviderRouter.GenerateAsync(
                tenantId,
                "INTELLIGENCE_WIDE_ANSWER",
                system,
                BuildActionUserPrompt(matter, pack, requirements),
                BuildActionsSchema(requirements.Count),
                Guid.NewGuid().ToString("N"),
                new AiExecutionContext("Intelligence", "DecisionMatter", matter?.DecisionMatterId, null, "NEXT_BEST_ACTION", null, null, "Next Best Action"),
                null,
                cancellationToken);

            var envelope = JsonSerializer.Deserialize<ActionProposalEnvelope>(result.Content, JsonOptions);
            return envelope?.Actions ?? [];
        }
        catch (Exception ex)
        {
            // Surface the real cause (route not configured, provider unavailable, invalid JSON, …) to the
            // caller so the cockpit explains WHY no actions were produced instead of a generic empty state.
            throw new InvalidOperationException($"action proposal failed: {ex.Message}", ex);
        }
    }

    private static string BuildActionUserPrompt(DecisionMatterDto? matter, ResolvedDomainPack pack, IReadOnlyList<ResolutionRequirement> requirements)
    {
        var sb = new StringBuilder();
        sb.Append("Matter: ").AppendLine(matter?.Title ?? "(untitled)");
        if (!string.IsNullOrWhiteSpace(matter?.Jurisdiction) || !string.IsNullOrWhiteSpace(matter?.State))
            sb.Append("Jurisdiction: ").AppendLine(matter?.State ?? matter?.Jurisdiction);
        sb.Append("Domain pack: ").AppendLine(pack.PracticeAreaCode);

        var infoSources = pack.Concepts
            .Where(c => !string.IsNullOrWhiteSpace(c.Name))
            .Select(c => c.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(24)
            .ToArray();
        if (infoSources.Length > 0)
        {
            sb.AppendLine("Domain-appropriate information sources / concepts:");
            foreach (var s in infoSources)
                sb.Append("- ").AppendLine(s);
        }

        sb.AppendLine();
        sb.AppendLine("Unresolved decision-material propositions to resolve (target one per action by propositionId):");
        foreach (var r in requirements)
        {
            sb.Append("propositionId: ").AppendLine(r.PropositionId.ToString());
            sb.Append("  proposition: ").AppendLine(r.PropositionText);
            if (r.HierarchyPath.Count > 0)
                sb.Append("  decision path: ").AppendLine(string.Join(" → ", r.HierarchyPath));
            sb.Append("  unresolved question: ").AppendLine(r.UnresolvedQuestion);
            sb.Append("  missing information: ").AppendLine(r.MissingInformation);
            sb.Append("  fact state: ").Append(r.FactStateCode)
              .Append(" | decision relevance: ").Append(r.DecisionRelevance.ToString("0.00"))
              .Append(" | uncertainty: ").AppendLine(r.Uncertainty.ToString("0.00"));
            if (r.SupportingEvidence.Count > 0)
                sb.Append("  supporting evidence: ").AppendLine(string.Join("; ", r.SupportingEvidence.Take(3)));
            if (r.ContradictingEvidence.Count > 0)
                sb.Append("  contradicting evidence: ").AppendLine(string.Join("; ", r.ContradictingEvidence.Take(3)));
        }
        return sb.ToString();
    }

    // ── DETERMINISTIC eligibility gate + ranking (the model only proposes; this decides) ────────────
    private static IReadOnlyList<NextBestAction> SelectEligibleActions(
        IReadOnlyList<ActionProposal> proposals,
        IReadOnlyList<ResolutionRequirement> requirements)
    {
        var requirementById = requirements.ToDictionary(r => r.PropositionId);
        var actions = new List<NextBestAction>();
        var seenPropositions = new HashSet<Guid>();

        foreach (var proposal in proposals)
        {
            // Gate 1: must carry a title and specific information sought.
            if (string.IsNullOrWhiteSpace(proposal.Title) || string.IsNullOrWhiteSpace(proposal.InformationSought))
                continue;

            // Gate 2: Action → ResolutionRequirement → Proposition link must be valid and unresolved+material.
            if (!Guid.TryParse(proposal.PropositionId, out var propositionId) ||
                !requirementById.TryGetValue(propositionId, out var requirement))
                continue;

            // Gate 3: reject generic, non-targeted discovery phrasing.
            if (IsGeneric(proposal.Title) || IsGeneric(proposal.InformationSought))
                continue;

            // One action per proposition target — the highest-value frontier first.
            if (!seenPropositions.Add(propositionId))
                continue;

            var (impactLabel, impactTone) = ImpactBand(requirement.DecisionRelevance);
            actions.Add(new NextBestAction(
                propositionId,
                proposal.Title!.Trim(),
                proposal.InformationSought!.Trim(),
                string.IsNullOrWhiteSpace(proposal.Context) ? null : proposal.Context!.Trim(),
                requirement.PropositionText,
                requirement.HierarchyPath,
                requirement.LegalAdv,
                impactLabel,
                impactTone));
        }

        // Rank by ADV (LegalAdv frontier) descending; present the full eligible frontier.
        return actions
            .OrderByDescending(a => a.Adv)
            .ToList();
    }

    private static bool IsGeneric(string text)
    {
        var normalized = text.Trim().ToLowerInvariant();
        return GenericActionMarkers.Any(marker => normalized.Contains(marker, StringComparison.Ordinal));
    }

    private static (string Label, string Tone) ImpactBand(decimal decisionRelevance) => decisionRelevance switch
    {
        >= 0.66m => ("High Impact", "danger"),
        >= 0.33m => ("Medium Impact", "warn"),
        _ => ("Low Impact", "info"),
    };
}
