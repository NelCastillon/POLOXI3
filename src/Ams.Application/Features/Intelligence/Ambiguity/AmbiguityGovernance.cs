using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Features.Intelligence;

namespace Ams.Application.Features.Intelligence.Ambiguity;

// Deterministic structural + semantic-lexical validation of a HierarchyProposal. LLM output is a
// proposal, never authoritative: POLOXI validates before anything downstream trusts it.
public sealed class HierarchyValidator:IHierarchyValidator
{
    public HierarchyValidationResult Validate(AmbiguityAnalysisResult proposal,QueryComplexityProfile complexity,ModelCapabilityProfile model)
    {
        var issues=new List<HierarchyValidationIssue>();
        var nodes=proposal.Nodes??[];
        if(nodes.Count==0){issues.Add(new("EMPTY_HIERARCHY",HierarchyValidationIssue.SeverityError,null,"The proposal contains no nodes."));return new(issues,false);}
        var byId=new Dictionary<string,HierarchyNodeDto>(StringComparer.Ordinal);
        foreach(var node in nodes)
        {
            if(string.IsNullOrWhiteSpace(node.Id)){issues.Add(new("MISSING_ID",HierarchyValidationIssue.SeverityError,null,$"Node '{node.Name}' has no id."));continue;}
            if(!byId.TryAdd(node.Id,node))issues.Add(new("DUPLICATE_ID",HierarchyValidationIssue.SeverityError,node.Id,$"Node id '{node.Id}' is not unique."));
        }
        var roots=nodes.Where(x=>string.IsNullOrWhiteSpace(x.ParentId)).ToArray();
        if(roots.Length==0)issues.Add(new("NO_ROOT",HierarchyValidationIssue.SeverityError,null,"The hierarchy has no root node."));
        // Root depth anchors every downstream Depth(parent)+1 check; a non-zero root corrupts them all.
        foreach(var root in roots.Where(x=>x.Depth!=0))
            issues.Add(new("ROOT_DEPTH_INVALID",HierarchyValidationIssue.SeverityError,root.Id,$"Root node '{root.Id}' must have depth 0, found {root.Depth}."));
        // Declared ambiguityCount disagreeing with actual Ambiguity nodes is a coverage/consistency signal.
        var actualAmbiguityCount=nodes.Count(x=>x.NodeType==HierarchyNodeType.Ambiguity);
        if(proposal.AmbiguityCount!=actualAmbiguityCount)
            issues.Add(new("AMBIGUITY_COUNT_MISMATCH",HierarchyValidationIssue.SeverityWarning,null,$"Declared ambiguityCount {proposal.AmbiguityCount} does not match the {actualAmbiguityCount} Ambiguity nodes in the tree."));
        var childrenByParent=nodes.Where(x=>!string.IsNullOrWhiteSpace(x.ParentId)).GroupBy(x=>x.ParentId!,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.ToArray(),StringComparer.Ordinal);
        foreach(var node in nodes)
        {
            if(!string.IsNullOrWhiteSpace(node.ParentId))
            {
                if(!byId.TryGetValue(node.ParentId,out var parent))issues.Add(new("ORPHAN_NODE",HierarchyValidationIssue.SeverityError,node.Id,$"Parent '{node.ParentId}' of node '{node.Id}' does not exist."));
                else if(node.Depth!=parent.Depth+1)issues.Add(new("INVALID_DEPTH",HierarchyValidationIssue.SeverityError,node.Id,$"Invalid depth on '{node.Id}': expected {parent.Depth+1}, found {node.Depth}."));
            }
            if(node.Depth>model.RecommendedMaxDepth)issues.Add(new("MAX_DEPTH_EXCEEDED",HierarchyValidationIssue.SeverityError,node.Id,$"Node '{node.Id}' exceeds the maximum depth {model.RecommendedMaxDepth}."));
            var hasChildren=childrenByParent.ContainsKey(node.Id??string.Empty);
            if(node.IsLeaf&&hasChildren)issues.Add(new("LEAF_HAS_CHILDREN",HierarchyValidationIssue.SeverityError,node.Id,$"Leaf node '{node.Id}' has children."));
            if(!node.IsLeaf&&!hasChildren&&node.NodeType is not(HierarchyNodeType.Root or HierarchyNodeType.EvidenceLeaf))issues.Add(new("NON_LEAF_WITHOUT_CHILDREN",HierarchyValidationIssue.SeverityWarning,node.Id,$"Non-leaf node '{node.Id}' has no children."));
            if(node.IsLeaf&&string.IsNullOrWhiteSpace(node.EvidenceNeeded))issues.Add(new("LEAF_NOT_EVIDENCE_READY",HierarchyValidationIssue.SeverityWarning,node.Id,$"Leaf node '{node.Id}' does not describe the evidence needed."));
        }
        DetectCycles(nodes,byId,issues);
        // Sibling near-duplicate detection (lexical): normalized-name collisions among siblings.
        foreach(var group in childrenByParent.Values)
        foreach(var duplicates in group.GroupBy(x=>Normalize(x.Name)).Where(x=>x.Count()>1))
            issues.Add(new("DUPLICATE_SIBLING",HierarchyValidationIssue.SeverityError,duplicates.First().Id,$"Sibling nodes share the meaning '{duplicates.Key}'."));
        // Dependencies must reference existing nodes.
        foreach(var dependency in proposal.Dependencies??[])
            if(!byId.ContainsKey(dependency.SourceNodeId)||!byId.ContainsKey(dependency.TargetNodeId))
                issues.Add(new("DANGLING_DEPENDENCY",HierarchyValidationIssue.SeverityError,dependency.SourceNodeId,$"Dependency {dependency.SourceNodeId}->{dependency.TargetNodeId} references a missing node."));
        // Coverage suspicion is a signal, never a hard rule: a complex query with many subjective
        // terms returning 0-1 actual ambiguity nodes warrants a second discovery pass or escalation.
        var coverageSuspicion=complexity.OverallLevel>=ComplexityLevel.Complex&&complexity.SubjectiveTermCount>=4&&actualAmbiguityCount<=1;
        return new(issues,coverageSuspicion);
    }

    private static void DetectCycles(IReadOnlyList<HierarchyNodeDto> nodes,Dictionary<string,HierarchyNodeDto> byId,List<HierarchyValidationIssue> issues)
    {
        foreach(var node in nodes)
        {
            var visited=new HashSet<string>(StringComparer.Ordinal);var current=node;
            while(current is not null&&!string.IsNullOrWhiteSpace(current.ParentId))
            {
                if(!visited.Add(current.Id)){issues.Add(new("CYCLE_DETECTED",HierarchyValidationIssue.SeverityError,node.Id,$"Node '{node.Id}' participates in a parent cycle."));break;}
                byId.TryGetValue(current.ParentId,out current);
            }
        }
    }

    private static string Normalize(string name)=>string.Join(' ',(name??string.Empty).ToLowerInvariant().Split(' ',StringSplitOptions.RemoveEmptyEntries).Where(x=>x is not("the" or "a" or "an" or "of" or "low" or "cheap" or "inexpensive")).OrderBy(x=>x,StringComparer.Ordinal));
}

// Progressive narrowing: assigns branch runtime state and ambiguity priority. Priority combines
// normalized interpretation entropy × decision impact × unresolvedness × dependency importance —
// not every ambiguity deserves equal effort. Interpretations are demoted to DORMANT, never deleted
// (reversible uncertainty, irreversible invalidation).
public sealed class AmbiguityNarrowingEngine:IAmbiguityNarrowingEngine
{
    private const decimal DormantConfidenceThreshold=0.15m;

    public IReadOnlyList<BranchRuntimeState> Resolve(ValidatedHierarchy hierarchy)
    {
        var nodes=hierarchy.Nodes;
        var dependencyCounts=CountDependencies(hierarchy.Dependencies);
        var interpretationsByAmbiguity=nodes.Where(x=>x.NodeType==HierarchyNodeType.Interpretation&&x.ParentId is not null).GroupBy(x=>x.ParentId!,StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.ToArray(),StringComparer.Ordinal);
        var states=new List<BranchRuntimeState>(nodes.Count);
        foreach(var node in nodes)
        {
            var impact=node.Materiality switch{Materiality.Critical=>1.0m,Materiality.High=>0.8m,Materiality.Medium=>0.5m,_=>0.25m};
            var dependencyWeight=1m+Math.Min(dependencyCounts.GetValueOrDefault(node.Id)*0.15m,0.6m);
            var entropy=node.NodeType==HierarchyNodeType.Ambiguity&&interpretationsByAmbiguity.TryGetValue(node.Id,out var interpretations)?NormalizedEntropy(interpretations):0.5m;
            var uncertainty=1m-(node.ProposedConfidence??0.5m);
            var status=BranchStatus.Active;string? reason=null;
            if(node.NodeType==HierarchyNodeType.Interpretation&&(node.ProposedConfidence??0.5m)<DormantConfidenceThreshold){status=BranchStatus.Dormant;reason=$"Interpretation prior {node.ProposedConfidence:0.00} is below the active threshold; kept dormant for possible reopening.";}
            states.Add(new(){NodeId=node.Id,Status=status,Priority=Math.Round(entropy*impact*uncertainty*dependencyWeight,6),EvidenceSupport=0m,InformationGain=entropy*impact,DecisionImpact=impact,ResidualUncertainty=uncertainty,ResolutionReason=reason,SemanticRole=node.NodeType==HierarchyNodeType.Interpretation?SemanticRole.CompetingInterpretation:SemanticRole.EvidenceDimension});
        }
        return states;
    }

    private static Dictionary<string,int> CountDependencies(IReadOnlyList<NodeDependencyDto> dependencies)
    {
        var counts=new Dictionary<string,int>(StringComparer.Ordinal);
        foreach(var dependency in dependencies){counts[dependency.SourceNodeId]=counts.GetValueOrDefault(dependency.SourceNodeId)+1;counts[dependency.TargetNodeId]=counts.GetValueOrDefault(dependency.TargetNodeId)+1;}
        return counts;
    }

    // Shannon entropy over interpretation priors, normalized to [0,1] by log2(n).
    public static decimal NormalizedEntropy(IReadOnlyList<HierarchyNodeDto> interpretations)
    {
        if(interpretations.Count<=1)return 0m;
        var priors=interpretations.Select(x=>(double)(x.ProposedConfidence??(1m/interpretations.Count))).ToArray();
        var total=priors.Sum();if(total<=0)return 1m;
        var entropy=priors.Where(x=>x>0).Sum(x=>{var p=x/total;return -p*Math.Log2(p);});
        return (decimal)Math.Clamp(entropy/Math.Log2(interpretations.Count),0d,1d);
    }
}

// Interpretation stitching: surviving interpretations, dependencies, constraints, and preferences
// become one coherent InterpretationComposite. Performs explicit semantic role conversion so MCDA
// never accidentally scores competing meanings simultaneously: a surviving competing interpretation
// becomes a decision criterion; dormant/invalidated branches are excluded.
public sealed class InterpretationStitcher:IInterpretationStitcher
{
    public InterpretationComposite Stitch(ValidatedHierarchy hierarchy,IReadOnlyCollection<BranchRuntimeState> states,IReadOnlyCollection<NodeDependencyDto> dependencies)
    {
        var stateById=states.ToDictionary(x=>x.NodeId,StringComparer.Ordinal);
        var byId=hierarchy.Nodes.ToDictionary(x=>x.Id,StringComparer.Ordinal);
        var surviving=hierarchy.Nodes.Where(node=>stateById.TryGetValue(node.Id,out var state)&&state.Status is BranchStatus.Active or BranchStatus.Resolved or BranchStatus.Reopened).ToArray();
        var dimensions=new List<ResolvedDimension>();var constraints=new List<ResolvedConstraint>();var preferences=new List<ResolvedPreference>();var uncertainties=new List<RemainingUncertainty>();
        foreach(var node in surviving)
        {
            var state=stateById[node.Id];
            switch(node.DecisionRole)
            {
                case DecisionRole.HardConstraint:
                    // Hard constraints stay OUTSIDE soft scoring — no preference weight can compensate.
                    constraints.Add(new(node.Id,node.Name,node.OperationalDefinition));
                    if(stateById.TryGetValue(node.Id,out var constraintState))constraintState.SemanticRole=SemanticRole.Constraint;
                    break;
                case DecisionRole.SoftPreference:
                    preferences.Add(new(node.Id,node.Name,node.PreferenceDirection,Weight(node,state)));
                    state.SemanticRole=SemanticRole.DecisionCriterion;
                    break;
                case DecisionRole.OptimizationObjective when node.NodeType is HierarchyNodeType.Interpretation or HierarchyNodeType.Dimension or HierarchyNodeType.SubDimension or HierarchyNodeType.EvidenceLeaf:
                    dimensions.Add(new(node.Id,node.Name,SemanticRole.DecisionCriterion,node.PreferenceDirection,node.MetricOrObservation,node.EvidenceNeeded,Weight(node,state)));
                    state.SemanticRole=SemanticRole.DecisionCriterion;
                    break;
                case DecisionRole.Context:
                    state.SemanticRole=SemanticRole.Context;
                    break;
                default:
                    // Surviving competing interpretations convert to decision dimensions after resolution.
                    if(node.NodeType is HierarchyNodeType.Interpretation or HierarchyNodeType.Dimension or HierarchyNodeType.SubDimension or HierarchyNodeType.EvidenceLeaf)
                    {
                        dimensions.Add(new(node.Id,node.Name,SemanticRole.DecisionCriterion,node.PreferenceDirection,node.MetricOrObservation,node.EvidenceNeeded,Weight(node,state)));
                        state.SemanticRole=SemanticRole.DecisionCriterion;
                    }
                    break;
            }
            if(state.ResidualUncertainty>=0.6m&&node.Materiality>=Materiality.High)uncertainties.Add(new(node.Id,node.Name,state.ResidualUncertainty,state.ResolutionReason));
        }
        foreach(var state in states.Where(x=>x.Status is BranchStatus.Dormant or BranchStatus.Invalidated))state.SemanticRole=SemanticRole.Excluded;
        // Only registered dependencies participate in cross-dimension interaction scoring.
        var interactions=dependencies.Where(x=>byId.ContainsKey(x.SourceNodeId)&&byId.ContainsKey(x.TargetNodeId)&&SurvivesBoth(stateById,x)).Select(x=>new InteractionRule(x.SourceNodeId,x.TargetNodeId,x.Type,x.Strength??0.5m,x.Reason)).ToArray();
        var objective=hierarchy.Nodes.FirstOrDefault(x=>x.NodeType==HierarchyNodeType.Root)?.Name??hierarchy.Proposal.OriginalRequest;
        // Global convergence: could a different resolution of a remaining uncertainty change the
        // ranking? Proxy: the highest decision-impact critical/high uncertainty. If one exists, the
        // composite is not converged and that node is the reopen candidate (highest priority first).
        var reopen=uncertainties.Count==0?null:uncertainties.OrderByDescending(x=>stateById.TryGetValue(x.NodeId,out var s)?s.DecisionImpact*x.ResidualUncertainty:0m).First();
        return new(){Objective=objective,Dimensions=dimensions,HardConstraints=constraints,Preferences=preferences,Interactions=interactions,Uncertainties=uncertainties,IsConverged=reopen is null,ReopenCandidateNodeId=reopen?.NodeId,ReopenReason=reopen is null?null:$"Residual uncertainty {reopen.ResidualUncertainty:0.00} on high-materiality node '{reopen.Name}' could change the ranking if resolved differently."};
    }

    private static bool SurvivesBoth(Dictionary<string,BranchRuntimeState> stateById,NodeDependencyDto dependency)
        =>stateById.TryGetValue(dependency.SourceNodeId,out var source)&&source.Status is not(BranchStatus.Dormant or BranchStatus.Invalidated)
        &&stateById.TryGetValue(dependency.TargetNodeId,out var target)&&target.Status is not(BranchStatus.Dormant or BranchStatus.Invalidated);

    private static decimal Weight(HierarchyNodeDto node,BranchRuntimeState state)
        =>Math.Round(Math.Clamp((node.Materiality switch{Materiality.Critical=>1.0m,Materiality.High=>0.8m,Materiality.Medium=>0.5m,_=>0.25m})*(0.5m+(node.ProposedConfidence??0.5m)/2m),0.05m,1m),4);
}
