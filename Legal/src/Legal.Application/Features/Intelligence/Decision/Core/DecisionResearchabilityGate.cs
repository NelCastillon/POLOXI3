namespace Legal.Application.Features.Intelligence.Decision.Core;

public sealed class DecisionResearchabilityGate
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        DecisionResearchNeedTypes.LegalRule,
        DecisionResearchNeedTypes.LegalAuthority,
        DecisionResearchNeedTypes.ProceduralStandard,
        DecisionResearchNeedTypes.MatterFact,
        DecisionResearchNeedTypes.MatterEvidence,
        DecisionResearchNeedTypes.Application,
        DecisionResearchNeedTypes.Derived,
    };

    public DecisionResearchabilityResult Evaluate(DecisionResearchSemanticProposal proposal)
    {
        var defects = new List<string>();
        var valid = new List<DecisionResearchSemanticLeaf>();
        var keys = proposal.Leaves
            .Where(leaf => !string.IsNullOrWhiteSpace(leaf.ResearchKey))
            .Select(leaf => leaf.ResearchKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (proposal.Leaves.Count == 0)
            defects.Add("NO_RESEARCH_LEAVES");
        if (proposal.Leaves.Count < 3)
            defects.Add("SEMANTIC_HIERARCHY_TOO_SHALLOW");
        if (keys.Count != proposal.Leaves.Count)
            defects.Add("RESEARCH_KEYS_NOT_UNIQUE");

        foreach (var leaf in proposal.Leaves)
        {
            var leafDefects = new List<string>();
            if (string.IsNullOrWhiteSpace(leaf.ResearchKey)) leafDefects.Add("RESEARCH_KEY_MISSING");
            if (string.IsNullOrWhiteSpace(leaf.Proposition)) leafDefects.Add("PROPOSITION_MISSING");
            if (!AllowedTypes.Contains(leaf.ResearchNeedType)) leafDefects.Add("RESEARCH_NEED_TYPE_INVALID");
            if (leaf.CandidateDiscrimination.Count == 0) leafDefects.Add("CANDIDATE_DISCRIMINATION_MISSING");
            if (leaf.Requires.Any(required => !keys.Contains(required))) leafDefects.Add("REQUIRED_LEAF_UNKNOWN");

            var derived = leaf.ResearchNeedType.Equals(DecisionResearchNeedTypes.Application, StringComparison.OrdinalIgnoreCase)
                || leaf.ResearchNeedType.Equals(DecisionResearchNeedTypes.Derived, StringComparison.OrdinalIgnoreCase);
            if (derived && (leaf.Researchable || !leaf.SourceClass.Equals(DecisionResearchSourceClasses.None, StringComparison.OrdinalIgnoreCase)))
                leafDefects.Add("DERIVED_NODE_MUST_NOT_BE_RETRIEVED");
            if (derived && leaf.Requires.Count < 2)
                leafDefects.Add("DERIVED_NODE_DEPENDENCIES_INSUFFICIENT");

            if (leaf.Researchable)
            {
                var expectedSource = leaf.ResearchNeedType is DecisionResearchNeedTypes.MatterFact or DecisionResearchNeedTypes.MatterEvidence
                    ? DecisionResearchSourceClasses.MatterDocument
                    : DecisionResearchSourceClasses.LegalAuthority;
                if (!leaf.SourceClass.Equals(expectedSource, StringComparison.OrdinalIgnoreCase))
                    leafDefects.Add("SOURCE_CLASS_MISMATCH");
                if (LooksLikeApplicationConclusion(leaf.Proposition))
                    leafDefects.Add("ASSUMED_APPLICATION_CONCLUSION");
                if (leaf.SourceClass.Equals(DecisionResearchSourceClasses.LegalAuthority, StringComparison.OrdinalIgnoreCase)
                    && LooksLikeMatterSpecificFinding(leaf.Proposition))
                    leafDefects.Add("MATTER_FINDING_NOT_PUBLICLY_RESEARCHABLE");
                if (LooksLikeVagueOrBiasedLegalQuestion(leaf.Proposition))
                    leafDefects.Add("LEGAL_PROPOSITION_NOT_PRECISE");
            }

            if (leafDefects.Count == 0 && leaf.Researchable)
                valid.Add(leaf);
            defects.AddRange(leafDefects.Select(defect => $"{leaf.ResearchKey}:{defect}"));
        }

        if (valid.Count == 0)
            defects.Add("NO_SOURCE_RESOLVABLE_LEAVES");
        if (valid.Count < 2)
            defects.Add("INSUFFICIENT_SOURCE_RESOLVABLE_LEAVES");
        if (!proposal.Leaves.Any(leaf =>
                leaf.ResearchNeedType.Equals(DecisionResearchNeedTypes.Application, StringComparison.OrdinalIgnoreCase)
                && !leaf.Researchable))
            defects.Add("APPLICATION_NODE_MISSING");

        return new DecisionResearchabilityResult(defects.Count == 0, defects, valid);
    }

    private static bool LooksLikeApplicationConclusion(string proposition)
    {
        var value = proposition.ToLowerInvariant();
        return value.Contains("there is sufficient evidence")
            || value.Contains("genuine dispute of material fact") && (value.Contains("exists") || value.Contains("regarding"))
            || value.Contains("therefore") && (value.Contains("liable") || value.Contains("misclassified") || value.Contains("entitled"));
    }

    private static bool LooksLikeMatterSpecificFinding(string proposition)
    {
        var value = proposition.ToLowerInvariant();
        var asksWhetherFactExists = value.Contains("whether there is")
            || value.Contains("whether there are")
            || value.Contains("existence of")
            || value.Contains("there is conflicting evidence")
            || value.Contains("there are conflicting accounts");
        var matterFact = value.Contains("conflicting evidence")
            || value.Contains("employee's actual duties")
            || value.Contains("employees' job duties")
            || value.Contains("employer claims")
            || value.Contains("employee claims")
            || value.Contains("in this matter");
        return asksWhetherFactExists && matterFact;
    }

    private static bool LooksLikeVagueOrBiasedLegalQuestion(string proposition)
    {
        var value = proposition.ToLowerInvariant();
        return value.Contains("favor employee protections")
            || value.Contains("favor employer protections")
            || value.Contains("legal standards are ambiguous")
            || value.Contains("whether the legal standards") && value.Contains("impacting");
    }
}