using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Decision.Core;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal V2.1 — Outcome-directed ResearchNeed generation (§18).
//
// After a recompetition, POLOXI selects the highest-Information-Value unresolved frontier branch and
// turns it into a structured ResearchNeed (what to retrieve next, and why it is decision-relevant).
// This is what makes the loop closed: a verification change can change WHICH investigation POLOXI
// runs next. Deterministic; returns null when no open frontier remains (nothing worth researching).
// ─────────────────────────────────────────────────────────────────────────────────────────────
public static class DecisionResearchNeedFactory
{
    public static DecisionResearchNeedPersistence? Create(
        IReadOnlyList<DecisionBranchPersistence> branches,
        DependencyImpact impact,
        Guid sessionId,
        Guid tenantId,
        Guid? actorUserId,
        Guid? matterId,
        Guid? dependencyEventId)
    {
        // Prefer a branch that was reopened by the impact, else the highest-IV open frontier branch.
        var reopened = branches
            .Where(b => b.BranchStateCode == DecisionBranchStates.Reopened)
            .OrderByDescending(b => b.InformationValue)
            .FirstOrDefault();
        var target = reopened ?? branches
            .Where(b => b.IsOnFrontier)
            .OrderByDescending(b => b.InformationValue)
            .ThenByDescending(b => b.AdvScore)
            .FirstOrDefault();

        if (target is null)
            return null;

        var uncertainty = 1m - target.EvidenceAvailability;
        var essentialFailed = impact.EssentialDependenciesFailed.Count > 0;
        var why = essentialFailed
            ? "An essential dependency failed verification, reopening this issue; resolving it can change the ranking."
            : "This is the highest information-value unresolved issue on the decision frontier.";
        var proposition = string.IsNullOrWhiteSpace(target.Interpretation) ? target.DisplayName : target.Interpretation;
        var needType = ClassifyResearchNeed(proposition);

        return new DecisionResearchNeedPersistence(
            DecisionResearchNeedId: Guid.NewGuid(),
            DecisionSessionId: sessionId,
            TenantId: tenantId,
            ActorUserId: actorUserId,
            MatterId: matterId,
            DecisionBranchId: target.DecisionBranchId,
            DecisionDependencyEventId: dependencyEventId,
            IssueLabel: target.DisplayName,
            PropositionToResolve: proposition,
            AuthorityKind: essentialFailed ? "CONTROLLING_AUTHORITY" : "PERSUASIVE_OR_CONTROLLING",
            RequiredEvidenceKind: "VERIFIED_AUTHORITY",
            WhyDecisionRelevant: why,
            ExpectedDiscrimination: target.FlipPotential,
            CurrentUncertainty: uncertainty < 0 ? 0 : uncertainty,
            InformationValue: target.InformationValue,
            FalsificationCondition: $"Retrieval fails to establish or refute '{target.DisplayName}' with verified authority.",
            StatusCode: "OPEN")
        {
            ResearchNeedTypeCode = needType,
        };
    }

    public static DecisionResearchNeedPersistence CreateFromLeaf(
        DecisionResearchNeedPersistence frontierNeed,
        DecisionResearchSemanticLeaf leaf,
        string proposalStatus,
        string? proposalReason) => frontierNeed with
    {
        PropositionToResolve = leaf.Proposition.Trim(),
        ResearchNeedTypeCode = leaf.ResearchNeedType,
        SourceClassCode = leaf.SourceClass,
        IsResearchable = leaf.Researchable,
        ParentResearchKey = leaf.ParentResearchKey,
        RequiredResearchKeysJson = System.Text.Json.JsonSerializer.Serialize(leaf.Requires),
        CandidateDiscriminationJson = System.Text.Json.JsonSerializer.Serialize(leaf.CandidateDiscrimination),
        SemanticProposalStatusCode = proposalStatus,
        SemanticProposalReasonCode = proposalReason,
        AuthorityKind = leaf.SourceClass == DecisionResearchSourceClasses.LegalAuthority
            ? frontierNeed.AuthorityKind
            : null,
        RequiredEvidenceKind = leaf.SourceClass == DecisionResearchSourceClasses.MatterDocument
            ? "VERIFIED_MATTER_DOCUMENT"
            : "VERIFIED_AUTHORITY",
        FalsificationCondition = $"Verified evidence fails to establish, refute, or materially narrow '{leaf.Proposition.Trim()}'.",
    };

    internal static string ClassifyResearchNeed(string? proposition)
    {
        var value = proposition?.ToLowerInvariant() ?? string.Empty;
        var matter = ContainsAny(value, "employee", "actual duties", "worked", "document", "record", "email",
            "contract", "declaration", "deposition", "correspondence", "invoice", "payroll", "timecard");
        var legal = ContainsAny(value, "law", "legal", "statute", "regulation", "rule", "court", "holding",
            "authority", "precedent", "exemption", "element", "standard");
        if (matter && legal)
            return DecisionResearchNeedTypes.Mixed;
        if (matter)
            return ContainsAny(value, "document", "record", "email", "contract", "declaration", "deposition",
                "correspondence", "invoice", "payroll", "timecard")
                ? DecisionResearchNeedTypes.MatterEvidence
                : DecisionResearchNeedTypes.MatterFact;
        return legal ? DecisionResearchNeedTypes.LegalRule : DecisionResearchNeedTypes.LegalAuthority;
    }

    private static bool ContainsAny(string value, params string[] candidates) => candidates.Any(value.Contains);
}
