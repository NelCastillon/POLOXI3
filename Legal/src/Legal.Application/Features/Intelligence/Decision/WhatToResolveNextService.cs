using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Epistemic;

namespace Legal.Application.Features.Intelligence.Decision;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// What To Resolve Next — a THIN application-layer PROJECTION over the EXISTING POLOXI proposition
// Information-Value / LegalADV frontier. It answers only "which unresolved, decision-material
// proposition should be resolved next?" and nothing else.
//
// Hard constraints (the user's contract):
//   • NO new scoring. Every number comes from MatterPropositionInformationValueService.ScoreAsync.
//     InformationValue is the authoritative VIV; LegalADV is the existing NBA-style normalization
//     (InformationValue / maxIV). FlipPotential is NOT exposed by the existing result, so it stays
//     null — unknown metrics must NEVER become zero.
//   • NO operational action generation and NO NBA. A ResolutionTarget stops at the proposition; the
//     separate NBA engine is the operational response to this priority and is untouched here.
//   • Four EXPLICIT states that must stay distinct — NotCalculated must never collapse into
//     NoneRequired:
//        Calculated    → ranked resolution targets exist
//        NotCalculated → the frontier has not been scored yet (no scored propositions)
//        Blocked       → scoring could not run (reason carried through)
//        NoneRequired  → scored, but no unresolved decision-material proposition remains
//   • Every target stays traceable to its exact proposition id and hierarchy node.
//
// Advisory/display-only: it fails soft to a Blocked state so the cockpit degrades gracefully.
// ─────────────────────────────────────────────────────────────────────────────────────────────────

public enum ResolutionTargetState
{
    Calculated,
    NotCalculated,
    Blocked,
    NoneRequired,
}

// One node on the advisory hierarchy lineage (domain → jurisdiction → dimension → proposition).
public sealed record HierarchyPathNodeDto(string Label);

// A single unresolved, decision-material proposition selected from the existing frontier. Thin by
// design: it carries the EXISTING metrics and stops at the proposition — no action, no NBA.
public sealed record ResolutionTargetDto(
    string PropositionId,
    string PropositionText,
    string? CandidateId,
    IReadOnlyList<HierarchyPathNodeDto> HierarchyPath,
    // Existing POLOXI metrics only. Unknown values stay null — never coerced to zero.
    double? InformationValue,
    double? FlipPotential,
    double? LegalAdv,
    string ResolutionQuestion,
    string? MissingInformation,
    IReadOnlyList<string> DependencyIds,
    ResolutionTargetState State,
    string Reason);

public sealed record WhatToResolveNextResult(
    Guid MatterId,
    ResolutionTargetState State,
    string Reason,
    IReadOnlyList<ResolutionTargetDto> Targets);

public interface IWhatToResolveNextService
{
    Task<WhatToResolveNextResult> GetAsync(
        Guid tenantId,
        Guid matterId,
        Guid decisionSessionId,
        Guid? actorUserId,
        CancellationToken cancellationToken = default);
}

public sealed class WhatToResolveNextService(
    IMatterPropositionInformationValueService informationValueService,
    ILegalDocumentCorpusRepository corpusRepository,
    ILegalDecisionRepository decisionRepository,
    IDomainPackResolver domainPackResolver) : IWhatToResolveNextService
{
    // A proposition is "resolved" when its fact state is proven (ESTABLISHED) or refuted (INVALIDATED).
    private static readonly HashSet<string> ResolvedFactStates =
        new(StringComparer.OrdinalIgnoreCase) { LegalFactStates.Established, LegalFactStates.Invalidated };

    // Decision-materiality floor: below this a proposition is not worth surfacing as a resolution target.
    private const decimal DecisionMaterialityFloor = 0.20m;

    public async Task<WhatToResolveNextResult> GetAsync(
        Guid tenantId,
        Guid matterId,
        Guid decisionSessionId,
        Guid? actorUserId,
        CancellationToken cancellationToken = default)
    {
        MatterPropositionInformationValueResult scored;
        try
        {
            // Reuse the authoritative VIV / LegalADV frontier. We never recompute these scores.
            scored = await informationValueService.ScoreAsync(
                tenantId, matterId, decisionSessionId, actorUserId, persist: false, cancellationToken);
        }
        catch (Exception ex)
        {
            // Scoring could not run — this is BLOCKED, and must never be reported as NoneRequired.
            return Blocked(matterId, $"Resolution priority is blocked: {ex.Message}");
        }

        // No scored propositions yet → the frontier has NOT been calculated (distinct from "none required").
        if (scored.Propositions.Count == 0)
            return NotCalculated(matterId, "Resolution priority has not been calculated.");

        // HRR / evidence graph — the current evidence state and dependency lineage for each proposition.
        var graph = await corpusRepository.GetMatterEvidenceGraphAsync(tenantId, matterId, cancellationToken);
        var evidenceById = (graph.Evidence ?? [])
            .GroupBy(e => e.LegalEvidenceItemId)
            .ToDictionary(g => g.Key, g => g.First().Summary);
        var propositionById = (graph.Propositions ?? [])
            .GroupBy(p => p.LegalFactPropositionId)
            .ToDictionary(g => g.Key, g => g.First());

        // Advisory hierarchy context (domain → jurisdiction → posture). Display-only lineage.
        var matter = await decisionRepository.GetMatterAsync(tenantId, matterId, cancellationToken);
        var packCode = string.IsNullOrWhiteSpace(matter?.DomainPackCode)
            ? DecisionDomainPackCodes.PersonalInjury
            : matter!.DomainPackCode;
        var pack = await domainPackResolver.ResolveAsync(tenantId, packCode, cancellationToken);

        // Existing LegalADV normalization (same projection NBA uses). Not a new formula.
        var maxIv = Math.Max(0.001m, scored.Propositions.Max(p => p.InformationValue));

        var targets = scored.Propositions
            .Where(p => IsUnresolved(p.FactStateCode) && p.DecisionImpact >= DecisionMaterialityFloor)
            .OrderByDescending(p => p.InformationValue)
            .Select(p => MapTarget(p, propositionById, evidenceById, maxIv, matter, pack))
            .ToArray();

        // Scored, but nothing unresolved and decision-material remains → NONE REQUIRED (distinct state).
        if (targets.Length == 0)
            return NoneRequired(matterId,
                "No unresolved decision-material propositions currently require resolution.");

        return new WhatToResolveNextResult(
            matterId,
            ResolutionTargetState.Calculated,
            "Ranked resolution targets from the current decision frontier.",
            targets);
    }

    private static ResolutionTargetDto MapTarget(
        MatterPropositionInformationValue p,
        IReadOnlyDictionary<Guid, LegalEvidenceGraphPropositionDto> propositionById,
        IReadOnlyDictionary<Guid, string> evidenceById,
        decimal maxIv,
        DecisionMatterDto? matter,
        ResolvedDomainPack pack)
    {
        propositionById.TryGetValue(p.PropositionId, out var graphProp);
        var support = graphProp?.Support ?? [];

        var supportingCount = support.Count(s =>
            string.Equals(s.RelationshipTypeCode, LegalDocumentRelationshipTypes.Supports, StringComparison.OrdinalIgnoreCase));
        var contradictingCount = support.Count(s =>
            string.Equals(s.RelationshipTypeCode, LegalDocumentRelationshipTypes.Contradicts, StringComparison.OrdinalIgnoreCase));

        var dependencyIds = support
            .Select(s => s.LegalEvidenceItemId)
            .Where(evidenceById.ContainsKey)
            .Select(id => id.ToString())
            .ToArray();

        // Existing LegalADV normalization — reuse, do not recompute with a new formula.
        var legalAdv = (double)Math.Round(p.InformationValue / maxIv, 2);

        return new ResolutionTargetDto(
            PropositionId: p.PropositionId.ToString(),
            PropositionText: p.PropositionText,
            CandidateId: null,
            HierarchyPath: BuildHierarchyPath(matter, pack),
            // Existing metric. FlipPotential is not exposed by the frontier, so it stays null (never zero).
            InformationValue: (double)p.InformationValue,
            FlipPotential: null,
            LegalAdv: legalAdv,
            ResolutionQuestion: BuildResolutionQuestion(p.PropositionText),
            MissingInformation: DescribeMissingInformation(supportingCount, contradictingCount),
            DependencyIds: dependencyIds,
            State: ResolutionTargetState.Calculated,
            Reason: "Unresolved decision-material proposition on the current frontier.");
    }

    private static bool IsUnresolved(string? factStateCode)
        => string.IsNullOrWhiteSpace(factStateCode) || !ResolvedFactStates.Contains(factStateCode);

    // Advisory HRR lineage: domain → jurisdiction → posture. Display-only, mirrors the NBA projection.
    private static IReadOnlyList<HierarchyPathNodeDto> BuildHierarchyPath(DecisionMatterDto? matter, ResolvedDomainPack pack)
    {
        var path = new List<HierarchyPathNodeDto>();
        if (!string.IsNullOrWhiteSpace(pack.PracticeAreaCode))
            path.Add(new HierarchyPathNodeDto(pack.PracticeAreaCode.Replace('_', ' ')));
        var jurisdiction = matter?.State ?? matter?.Jurisdiction;
        if (!string.IsNullOrWhiteSpace(jurisdiction))
            path.Add(new HierarchyPathNodeDto(jurisdiction));
        var posture = matter?.Posture ?? matter?.RequestedDisposition;
        if (!string.IsNullOrWhiteSpace(posture))
            path.Add(new HierarchyPathNodeDto(posture));
        return path;
    }

    // Deterministic semantic transformation: proposition → attorney-facing question. Never changes
    // the target proposition — ResolutionQuestion → the exact same proposition id.
    private static string BuildResolutionQuestion(string propositionText)
        => $"Determine whether {LowerFirst(propositionText.TrimEnd('.'))}.";

    private static string LowerFirst(string text)
        => string.IsNullOrEmpty(text) ? text : char.ToLowerInvariant(text[0]) + text[1..];

    private static string DescribeMissingInformation(int supportingCount, int contradictingCount)
    {
        if (contradictingCount > 0 && supportingCount > 0)
            return "Conflicting evidence on record; an independent source is needed to resolve the conflict.";
        if (contradictingCount > 0)
            return "Only contradicting evidence on record; corroborating independent evidence is missing.";
        if (supportingCount == 0)
            return "No admitted evidence yet; independent evidence is needed to establish this proposition.";
        return "Independent corroboration is needed to move this proposition to established.";
    }

    private static WhatToResolveNextResult NotCalculated(Guid matterId, string reason)
        => new(matterId, ResolutionTargetState.NotCalculated, reason, []);

    private static WhatToResolveNextResult Blocked(Guid matterId, string reason)
        => new(matterId, ResolutionTargetState.Blocked, reason, []);

    private static WhatToResolveNextResult NoneRequired(Guid matterId, string reason)
        => new(matterId, ResolutionTargetState.NoneRequired, reason, []);
}
