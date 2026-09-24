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
        // Defects owned exclusively by derived/application (synthesis) leaves. Tracked separately so that a
        // defective application leaf can be targeted for repair — and, if it stays defective, never blocks
        // the independently researchable authority/matter leaves from progressing.
        var applicationLeafDefects = new List<string>();
        // Structural or researchable-leaf defects that a targeted application repair cannot resolve. When any
        // of these exist, partial progression with researchable leaves is NOT permitted (gate not weakened).
        var blockingDefects = new List<string>();
        var valid = new List<DecisionResearchSemanticLeaf>();
        var keys = proposal.Leaves
            .Where(leaf => !string.IsNullOrWhiteSpace(leaf.ResearchKey))
            .Select(leaf => leaf.ResearchKey)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (proposal.Leaves.Count == 0)
        {
            defects.Add("NO_RESEARCH_LEAVES");
            blockingDefects.Add("NO_RESEARCH_LEAVES");
        }
        if (proposal.Leaves.Count < 3)
        {
            defects.Add("SEMANTIC_HIERARCHY_TOO_SHALLOW");
            blockingDefects.Add("SEMANTIC_HIERARCHY_TOO_SHALLOW");
        }
        if (keys.Count != proposal.Leaves.Count)
        {
            defects.Add("RESEARCH_KEYS_NOT_UNIQUE");
            blockingDefects.Add("RESEARCH_KEYS_NOT_UNIQUE");
        }

        foreach (var leaf in proposal.Leaves)
        {
            var leafDefects = new List<string>();
            var derived = IsDerived(leaf);
            if (string.IsNullOrWhiteSpace(leaf.ResearchKey)) leafDefects.Add("RESEARCH_KEY_MISSING");
            if (string.IsNullOrWhiteSpace(leaf.ResearchQuestion)) leafDefects.Add("RESEARCH_QUESTION_MISSING");
            if (string.IsNullOrWhiteSpace(leaf.Proposition)) leafDefects.Add("PROPOSITION_MISSING");
            if (!AllowedTypes.Contains(leaf.ResearchNeedType)) leafDefects.Add("RESEARCH_NEED_TYPE_INVALID");
            if (leaf.CandidateDiscrimination.Count == 0) leafDefects.Add("CANDIDATE_DISCRIMINATION_MISSING");
            if (leaf.Requires.Any(required => !keys.Contains(required))) leafDefects.Add("REQUIRED_LEAF_UNKNOWN");

            if (derived && (leaf.Researchable || !leaf.SourceClass.Equals(DecisionResearchSourceClasses.None, StringComparison.OrdinalIgnoreCase)))
                leafDefects.Add("DERIVED_NODE_MUST_NOT_BE_RETRIEVED");
            if (derived && leaf.Requires.Count < 2)
                leafDefects.Add("DERIVED_NODE_DEPENDENCIES_INSUFFICIENT");
            if (derived && !leaf.ApplicationDeferred)
                leafDefects.Add("APPLICATION_MUST_BE_DEFERRED");
            if (derived && (!string.IsNullOrWhiteSpace(leaf.SearchQuery) || leaf.SearchConcepts.Count > 0 || leaf.AuthorityKinds.Count > 0))
                leafDefects.Add("DERIVED_NODE_MUST_NOT_HAVE_RETRIEVAL_INSTRUCTIONS");

            if (leaf.Researchable)
            {
                if (LooksLikeQuestion(leaf.Proposition)) leafDefects.Add("PROPOSITION_MUST_BE_DECLARATIVE");
                if (string.IsNullOrWhiteSpace(leaf.SearchQuery)) leafDefects.Add("SEARCH_QUERY_MISSING");
                if (leaf.SearchConcepts.Count == 0) leafDefects.Add("SEARCH_CONCEPTS_MISSING");
                if (leaf.ResearchNeedType.Equals(DecisionResearchNeedTypes.Mixed, StringComparison.OrdinalIgnoreCase))
                    leafDefects.Add("MIXED_LEAF_MUST_BE_DECOMPOSED");
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
                if (leaf.SourceClass.Equals(DecisionResearchSourceClasses.LegalAuthority, StringComparison.OrdinalIgnoreCase)
                    && leaf.AuthorityKinds.Count == 0)
                    leafDefects.Add("AUTHORITY_KINDS_MISSING");
                if (leaf.SourceClass.Equals(DecisionResearchSourceClasses.MatterDocument, StringComparison.OrdinalIgnoreCase)
                    && leaf.AuthorityKinds.Count > 0)
                    leafDefects.Add("MATTER_LEAF_MUST_NOT_HAVE_AUTHORITY_KINDS");
            }

            if (leafDefects.Count == 0 && leaf.Researchable)
                valid.Add(leaf);
            var prefixed = leafDefects.Select(defect => $"{leaf.ResearchKey}:{defect}").ToList();
            defects.AddRange(prefixed);
            if (derived && !leaf.Researchable)
                applicationLeafDefects.AddRange(prefixed);
            else
                blockingDefects.AddRange(prefixed);
        }

        if (valid.Count == 0)
        {
            defects.Add("NO_SOURCE_RESOLVABLE_LEAVES");
            blockingDefects.Add("NO_SOURCE_RESOLVABLE_LEAVES");
        }
        if (!proposal.Leaves.Any(leaf =>
                leaf.ResearchNeedType.Equals(DecisionResearchNeedTypes.Application, StringComparison.OrdinalIgnoreCase)
                && !leaf.Researchable))
        {
            defects.Add("APPLICATION_NODE_MISSING");
            blockingDefects.Add("APPLICATION_NODE_MISSING");
        }

        // Partial progression is permitted only when the proposal is not fully acceptable, at least one
        // researchable leaf passed cleanly, and EVERY residual defect is confined to a derived/application
        // synthesis leaf. This never weakens the gate: full acceptance still requires zero defects, and any
        // structural or researchable-leaf defect (tracked in blockingDefects) disables progression.
        var canProgress = defects.Count > 0
            && valid.Count > 0
            && blockingDefects.Count == 0
            && applicationLeafDefects.Count > 0;

        return new DecisionResearchabilityResult(defects.Count == 0, defects, valid)
        {
            CanProgressWithResearchableLeaves = canProgress,
            ApplicationLeafDefects = applicationLeafDefects,
        };
    }

    private static bool IsDerived(DecisionResearchSemanticLeaf leaf) =>
        leaf.ResearchNeedType.Equals(DecisionResearchNeedTypes.Application, StringComparison.OrdinalIgnoreCase)
        || leaf.ResearchNeedType.Equals(DecisionResearchNeedTypes.Derived, StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeQuestion(string proposition)
    {
        var value = proposition.Trim();
        if (value.EndsWith('?'))
            return true;
        return value.StartsWith("what ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("which ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("who ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("when ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("where ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("how ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("whether ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("does ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("do ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("is ", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("are ", StringComparison.OrdinalIgnoreCase);
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