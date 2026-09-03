using Ams.Application.Features.Intelligence;
using Ams.Application.Features.Intelligence.Ambiguity;
using Xunit;

namespace Ams.Application.Tests;

// Deterministic tests for the POLOXI Model-Adaptive Ambiguity subsystem: complexity heuristics,
// prompt scaffolding routing matrix, structural hierarchy validation, entropy-based narrowing
// priority, and semantic-role stitching.
public sealed class AmbiguityResolutionTests
{
    private static ModelCapabilityProfile Model(ModelTier tier,int maxDepth=8)=>new(){ModelId="test-model",Tier=tier,SemanticReasoning=0.8m,MultiAmbiguityRecall=0.8m,RecursiveDecomposition=0.8m,StructuralReliability=0.9m,InstructionFollowing=0.9m,CostScore=0.5m,LatencyScore=0.5m,RecommendedMaxDepth=maxDepth,RecommendedScaffolding=PromptScaffoldingLevel.Medium};

    private static HierarchyNodeDto Node(string id,string? parentId,int depth,string name,HierarchyNodeType type,bool isLeaf=false,decimal? confidence=null,DecisionRole role=DecisionRole.Unknown,Materiality materiality=Materiality.Medium,string? evidenceNeeded=null)
        =>new(){Id=id,ParentId=parentId,Depth=depth,Name=name,NodeType=type,IsLeaf=isLeaf,ProposedConfidence=confidence,DecisionRole=role,Materiality=materiality,EvidenceNeeded=evidenceNeeded,PreferenceDirection=PreferenceDirection.Unknown};

    private static AmbiguityAnalysisResult Proposal(params HierarchyNodeDto[] nodes)=>new(){RootId="root",OriginalRequest="test",AmbiguityCount=nodes.Count(x=>x.NodeType==HierarchyNodeType.Ambiguity),Nodes=nodes};

    [Fact]
    public void ComplexityAnalyzer_SimpleFactualQuery_IsSimple()
    {
        var profile=new QueryComplexityAnalyzer().Analyze("Median house price in Torrance?");
        Assert.Equal(ComplexityLevel.Simple,profile.OverallLevel);
    }

    [Fact]
    public void ComplexityAnalyzer_MultiConceptSubjectiveQuery_IsComplexOrExtreme()
    {
        var profile=new QueryComplexityAnalyzer().Analyze("Find the best affordable city near LA for my family with good schools, reasonable commute, safe neighborhoods and good investment potential.");
        Assert.True(profile.OverallLevel>=ComplexityLevel.Complex);
        Assert.True(profile.SubjectiveTermCount>=4);
    }

    [Theory]
    [InlineData(ModelTier.Small,ComplexityLevel.Simple,PromptScaffoldingLevel.Heavy)]
    [InlineData(ModelTier.Small,ComplexityLevel.Extreme,PromptScaffoldingLevel.Heavy)]
    [InlineData(ModelTier.Standard,ComplexityLevel.Simple,PromptScaffoldingLevel.Light)]
    [InlineData(ModelTier.Standard,ComplexityLevel.Moderate,PromptScaffoldingLevel.Medium)]
    [InlineData(ModelTier.Standard,ComplexityLevel.Complex,PromptScaffoldingLevel.Heavy)]
    [InlineData(ModelTier.Premium,ComplexityLevel.Simple,PromptScaffoldingLevel.Light)]
    [InlineData(ModelTier.Premium,ComplexityLevel.Moderate,PromptScaffoldingLevel.Light)]
    [InlineData(ModelTier.Premium,ComplexityLevel.Complex,PromptScaffoldingLevel.Medium)]
    [InlineData(ModelTier.Premium,ComplexityLevel.Extreme,PromptScaffoldingLevel.Heavy)]
    public void PromptSelector_FollowsRoutingMatrix(ModelTier tier,ComplexityLevel level,PromptScaffoldingLevel expected)
    {
        var selected=new AmbiguityPromptSelector().Select(Model(tier),new(){OverallLevel=level});
        Assert.Equal(expected,selected);
    }

    [Fact]
    public void Validator_DetectsInvalidDepthOrphanAndDuplicateSiblings()
    {
        var proposal=Proposal(
            Node("root",null,0,"Request",HierarchyNodeType.Root),
            Node("a1","root",1,"Affordable",HierarchyNodeType.Ambiguity),
            Node("c1","a1",3,"Purchase affordability",HierarchyNodeType.Interpretation,isLeaf:true,evidenceNeeded:"prices"),
            Node("c2","missing",2,"Rental affordability",HierarchyNodeType.Interpretation,isLeaf:true,evidenceNeeded:"rents"),
            Node("c3","a1",2,"Cheap housing",HierarchyNodeType.Interpretation,isLeaf:true,evidenceNeeded:"prices"),
            Node("c4","a1",2,"Inexpensive housing",HierarchyNodeType.Interpretation,isLeaf:true,evidenceNeeded:"prices"));
        var result=new HierarchyValidator().Validate(proposal,new(){OverallLevel=ComplexityLevel.Moderate},Model(ModelTier.Standard));
        Assert.Contains(result.Issues,x=>x.IssueCode=="INVALID_DEPTH"&&x.NodeId=="c1");
        Assert.Contains(result.Issues,x=>x.IssueCode=="ORPHAN_NODE"&&x.NodeId=="c2");
        Assert.Contains(result.Issues,x=>x.IssueCode=="DUPLICATE_SIBLING");
        Assert.False(result.IsValid);
    }

    [Fact]
    public void Validator_ValidHierarchy_Passes()
    {
        var proposal=Proposal(
            Node("root",null,0,"Request",HierarchyNodeType.Root),
            Node("a1","root",1,"Affordable",HierarchyNodeType.Ambiguity),
            Node("c1","a1",2,"Purchase affordability",HierarchyNodeType.Interpretation,isLeaf:true,confidence:0.6m,evidenceNeeded:"median prices"),
            Node("c2","a1",2,"Income-relative affordability",HierarchyNodeType.Interpretation,isLeaf:true,confidence:0.4m,evidenceNeeded:"income ratios"));
        var result=new HierarchyValidator().Validate(proposal,new(){OverallLevel=ComplexityLevel.Moderate},Model(ModelTier.Standard));
        Assert.True(result.IsValid);
    }

    [Fact]
    public void Validator_FlagsCoverageSuspicion_ForComplexQueryWithOneAmbiguity()
    {
        var proposal=Proposal(Node("root",null,0,"Request",HierarchyNodeType.Root),Node("a1","root",1,"Only ambiguity",HierarchyNodeType.Ambiguity));
        var result=new HierarchyValidator().Validate(proposal,new(){OverallLevel=ComplexityLevel.Complex,SubjectiveTermCount=6,ConceptCount=9},Model(ModelTier.Small));
        Assert.True(result.PossibleMissedMaterialAmbiguity);
    }

    [Fact]
    public void Validator_RejectsNonZeroRootDepth()
    {
        var proposal=Proposal(Node("root",null,1,"Request",HierarchyNodeType.Root),Node("a1","root",2,"Ambiguity",HierarchyNodeType.Ambiguity,isLeaf:true,evidenceNeeded:"x"));
        var result=new HierarchyValidator().Validate(proposal,new(){OverallLevel=ComplexityLevel.Moderate},Model(ModelTier.Standard));
        Assert.Contains(result.Issues,x=>x.IssueCode=="ROOT_DEPTH_INVALID"&&x.Severity==HierarchyValidationIssue.SeverityError);
    }

    [Fact]
    public void Validator_WarnsWhenDeclaredAmbiguityCountDisagreesWithTree()
    {
        var proposal=Proposal(Node("root",null,0,"Request",HierarchyNodeType.Root),Node("a1","root",1,"Ambiguity",HierarchyNodeType.Ambiguity,isLeaf:true,evidenceNeeded:"x")) with{AmbiguityCount=3};
        var result=new HierarchyValidator().Validate(proposal,new(){OverallLevel=ComplexityLevel.Moderate},Model(ModelTier.Standard));
        Assert.Contains(result.Issues,x=>x.IssueCode=="AMBIGUITY_COUNT_MISMATCH"&&x.Severity==HierarchyValidationIssue.SeverityWarning);
        Assert.True(result.IsValid); // warning only — never blocks a structurally sound tree
    }

    [Fact]
    public void NarrowingEngine_DemotesLowPriorInterpretationsToDormant_AndPrioritizesHighEntropyCriticalAmbiguity()
    {
        var proposal=Proposal(
            Node("root",null,0,"Request",HierarchyNodeType.Root),
            Node("a1","root",1,"Affordable",HierarchyNodeType.Ambiguity,materiality:Materiality.Critical),
            Node("c1","a1",2,"Purchase",HierarchyNodeType.Interpretation,isLeaf:true,confidence:0.52m,evidenceNeeded:"prices"),
            Node("c2","a1",2,"Rental",HierarchyNodeType.Interpretation,isLeaf:true,confidence:0.08m,evidenceNeeded:"rents"),
            Node("c3","a1",2,"Income-relative",HierarchyNodeType.Interpretation,isLeaf:true,confidence:0.40m,evidenceNeeded:"ratios"),
            Node("a2","root",1,"Minor scope",HierarchyNodeType.Ambiguity,materiality:Materiality.Low));
        var states=new AmbiguityNarrowingEngine().Resolve(ValidatedHierarchy.From(proposal));
        Assert.Equal(BranchStatus.Dormant,states.Single(x=>x.NodeId=="c2").Status);
        Assert.Equal(BranchStatus.Active,states.Single(x=>x.NodeId=="c1").Status);
        Assert.True(states.Single(x=>x.NodeId=="a1").Priority>states.Single(x=>x.NodeId=="a2").Priority);
    }

    [Fact]
    public void NormalizedEntropy_IsZeroForSingleInterpretation_AndOneForUniformPriors()
    {
        var uniform=new[]{Node("x",null,1,"A",HierarchyNodeType.Interpretation,confidence:0.5m),Node("y",null,1,"B",HierarchyNodeType.Interpretation,confidence:0.5m)};
        Assert.Equal(0m,AmbiguityNarrowingEngine.NormalizedEntropy([uniform[0]]));
        Assert.Equal(1m,AmbiguityNarrowingEngine.NormalizedEntropy(uniform));
    }

    [Fact]
    public void Stitcher_ConvertsRoles_KeepsConstraintsOutsideScoring_AndExcludesDormantBranches()
    {
        var proposal=new AmbiguityAnalysisResult
        {
            RootId="root",OriginalRequest="best affordable city",AmbiguityCount=1,
            Nodes=[
                Node("root",null,0,"Best affordable city",HierarchyNodeType.Root),
                Node("a1","root",1,"Affordable",HierarchyNodeType.Ambiguity,materiality:Materiality.High),
                Node("c1","a1",2,"Purchase affordability",HierarchyNodeType.Interpretation,isLeaf:true,confidence:0.6m,materiality:Materiality.High,evidenceNeeded:"prices"),
                Node("c2","a1",2,"Rental affordability",HierarchyNodeType.Interpretation,isLeaf:true,confidence:0.05m,evidenceNeeded:"rents"),
                Node("h1","root",1,"Budget under 850k",HierarchyNodeType.Dimension,isLeaf:true,role:DecisionRole.HardConstraint,materiality:Materiality.Critical,evidenceNeeded:"listing prices")],
            Dependencies=[new(){SourceNodeId="c1",TargetNodeId="h1",Type=DependencyType.Constrains,Strength=0.8m},new(){SourceNodeId="c2",TargetNodeId="h1",Type=DependencyType.Overlaps}]
        };
        var hierarchy=ValidatedHierarchy.From(proposal);
        var states=new AmbiguityNarrowingEngine().Resolve(hierarchy);
        var composite=new InterpretationStitcher().Stitch(hierarchy,states,hierarchy.Dependencies);
        Assert.Contains(composite.HardConstraints,x=>x.NodeId=="h1");
        Assert.DoesNotContain(composite.Dimensions,x=>x.NodeId=="h1");
        Assert.Contains(composite.Dimensions,x=>x.NodeId=="c1"&&x.Role==SemanticRole.DecisionCriterion);
        Assert.DoesNotContain(composite.Dimensions,x=>x.NodeId=="c2");
        Assert.Equal(SemanticRole.Excluded,states.Single(x=>x.NodeId=="c2").SemanticRole);
        // Interactions touching a dormant branch are dropped; only surviving registered dependencies remain.
        Assert.Single(composite.Interactions);
        Assert.Equal("c1",composite.Interactions[0].SourceNodeId);
    }

    [Fact]
    public void Stitcher_ReportsConvergence_AndNamesReopenCandidateWhenHighMaterialityUncertaintyRemains()
    {
        // Confident high-materiality interpretation => converged, no reopen candidate.
        var converged=Proposal(
            Node("root",null,0,"Request",HierarchyNodeType.Root),
            Node("a1","root",1,"Affordable",HierarchyNodeType.Ambiguity,materiality:Materiality.High,confidence:0.9m),
            Node("c1","a1",2,"Purchase affordability",HierarchyNodeType.Interpretation,isLeaf:true,confidence:0.9m,materiality:Materiality.High,evidenceNeeded:"prices"));
        var hierarchy=ValidatedHierarchy.From(converged);
        var composite=new InterpretationStitcher().Stitch(hierarchy,new AmbiguityNarrowingEngine().Resolve(hierarchy),hierarchy.Dependencies);
        Assert.True(composite.IsConverged);
        Assert.Null(composite.ReopenCandidateNodeId);

        // Low-confidence critical interpretation => not converged; it becomes the reopen candidate.
        var uncertain=Proposal(
            Node("root",null,0,"Request",HierarchyNodeType.Root),
            Node("a1","root",1,"Good schools",HierarchyNodeType.Ambiguity,materiality:Materiality.Critical,confidence:0.3m),
            Node("c1","a1",2,"Assigned-school performance",HierarchyNodeType.Interpretation,isLeaf:true,confidence:0.3m,materiality:Materiality.Critical,evidenceNeeded:"ratings"));
        var uncertainHierarchy=ValidatedHierarchy.From(uncertain);
        var uncertainComposite=new InterpretationStitcher().Stitch(uncertainHierarchy,new AmbiguityNarrowingEngine().Resolve(uncertainHierarchy),uncertainHierarchy.Dependencies);
        Assert.False(uncertainComposite.IsConverged);
        Assert.NotNull(uncertainComposite.ReopenCandidateNodeId);
        Assert.NotNull(uncertainComposite.ReopenReason);
    }

    [Fact]
    public void EscalationPolicy_EscalatesOnRepeatedSemanticFailures_ButNeverForPremium()
    {
        var policy=new ModelEscalationPolicy();
        var failing=new HierarchyValidationResult([new("INVALID_DEPTH",HierarchyValidationIssue.SeverityError,"x","bad"),new("ORPHAN_NODE",HierarchyValidationIssue.SeverityError,"y","bad")],false);
        Assert.True(policy.ShouldEscalate(failing,new(){OverallLevel=ComplexityLevel.Complex},Model(ModelTier.Small),1,ExecutionBudget.Default));
        Assert.False(policy.ShouldEscalate(failing,new(){OverallLevel=ComplexityLevel.Complex},Model(ModelTier.Premium),1,ExecutionBudget.Default));
    }
}
