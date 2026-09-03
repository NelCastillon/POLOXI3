using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Intelligence;

// ── POLOXI Model-Adaptive Ambiguity & Hierarchy Subsystem — canonical contracts ────────────────
// Central rule: POLOXI decides how much semantic scaffolding a model needs. The model proposes
// the hierarchy; POLOXI validates, governs, narrows, stitches, and converges it. Every model
// (Small/Standard/Premium) returns the SAME canonical schema, so downstream stages are
// model-independent. Two kinds of hierarchy exist: the Interpretation Hierarchy ("what does the
// request mean?") which resolves into the Decision Hierarchy ("what should be evaluated?").

// Canonical model-independent ambiguity analysis result (the LLM's HierarchyProposal — never
// authoritative until validated by POLOXI).
public sealed record AmbiguityAnalysisResult
{
    public required string RootId{get;init;}
    public required string OriginalRequest{get;init;}
    public int AmbiguityCount{get;init;}
    public required IReadOnlyList<HierarchyNodeDto> Nodes{get;init;}
    public IReadOnlyList<NodeDependencyDto> Dependencies{get;init;}=[];
    public IReadOnlyList<ExcludedAmbiguityDto> ExcludedAmbiguities{get;init;}=[];
    public AmbiguityAuditDto? Audit{get;init;}
}

// Generic hierarchy node supporting arbitrary depth without schema changes.
public sealed record HierarchyNodeDto
{
    public required string Id{get;init;}
    public string? ParentId{get;init;}
    public int Depth{get;init;}
    public required string Name{get;init;}
    public string? SourceText{get;init;}
    public required HierarchyNodeType NodeType{get;init;}
    public AmbiguityType? AmbiguityType{get;init;}
    public Materiality Materiality{get;init;}
    public DecisionRole DecisionRole{get;init;}
    public string? OperationalDefinition{get;init;}
    public string? MetricOrObservation{get;init;}
    public string? EvidenceNeeded{get;init;}
    public string? EvidenceType{get;init;}
    public PreferenceDirection PreferenceDirection{get;init;}
    public bool IsLeaf{get;init;}
    public decimal? ProposedConfidence{get;init;}
}

// Cross-tree relationship: trees handle parent→child only; ambiguity relationships can span
// branches (e.g. Affordable ↔ Good Schools). Foundation for stitching and interaction scoring.
public sealed record NodeDependencyDto
{
    public required string SourceNodeId{get;init;}
    public required string TargetNodeId{get;init;}
    public required DependencyType Type{get;init;}
    public string? Reason{get;init;}
    public decimal? Strength{get;init;}
}

public sealed record ExcludedAmbiguityDto(string Name,string Reason);
public sealed record AmbiguityAuditDto(bool SecondScanPerformed,bool SiblingDistinctnessVerified,bool ParentChildVerified,bool LeafEvidenceVerified,string? Notes);

public enum HierarchyNodeType{Root,Ambiguity,Interpretation,Dimension,SubDimension,EvidenceLeaf}
public enum AmbiguityType{Semantic,Metric,Scope,Threshold,Temporal,Referential,Relational,Constraint,Objective,MissingVariable,Interaction,Operational}
public enum Materiality{Low,Medium,High,Critical}
public enum DecisionRole{Unknown,HardConstraint,SoftPreference,OptimizationObjective,Context}
public enum PreferenceDirection{Unknown,HigherIsBetter,LowerIsBetter,TargetRange,Boolean,Categorical}
public enum DependencyType{DependsOn,Modifies,Constrains,Overlaps,ConflictsWith,ProvidesContextFor}
public enum ModelTier{Small,Standard,Premium}
public enum PromptScaffoldingLevel{Heavy,Medium,Light}
public enum ComplexityLevel{Simple,Moderate,Complex,Extreme}
public enum BranchStatus{Proposed,Active,Dormant,Resolved,Reopened,Invalidated}
public enum SemanticRole{CompetingInterpretation,DecisionCriterion,Constraint,Context,EvidenceDimension,Excluded}
public enum ReasoningTaskType{InitialContract,AmbiguityDiscovery,HierarchyGeneration,HierarchyRepair,ParentChildValidation,EvidenceExtraction,EvidenceClassification,InterpretationResolution,CandidateComparison,FinalSynthesis}

// ── Model capability profile (database-backed via POLOXI.ModelCapabilityProfile) ──────────────
public sealed record ModelCapabilityProfile
{
    public required string ModelId{get;init;}
    public ModelTier Tier{get;init;}
    public decimal SemanticReasoning{get;init;}
    public decimal MultiAmbiguityRecall{get;init;}
    public decimal RecursiveDecomposition{get;init;}
    public decimal StructuralReliability{get;init;}
    public decimal InstructionFollowing{get;init;}
    public decimal CostScore{get;init;}
    public decimal LatencyScore{get;init;}
    public int RecommendedMaxDepth{get;init;}
    public PromptScaffoldingLevel RecommendedScaffolding{get;init;}
}

// ── Query complexity profile (deterministic heuristics; model capability alone never decides) ─
public sealed record QueryComplexityProfile
{
    public decimal AmbiguityLikelihood{get;init;}
    public decimal SemanticComplexity{get;init;}
    public decimal ConstraintComplexity{get;init;}
    public decimal InteractionComplexity{get;init;}
    public decimal EvidenceComplexity{get;init;}
    public int ConceptCount{get;init;}
    public int SubjectiveTermCount{get;init;}
    public ComplexityLevel OverallLevel{get;init;}
}

// ── Branch runtime state: lifecycle separate from semantic definition ─────────────────────────
public sealed class BranchRuntimeState
{
    public required string NodeId{get;init;}
    public BranchStatus Status{get;set;}
    public decimal Priority{get;set;}
    public decimal EvidenceSupport{get;set;}
    public decimal InformationGain{get;set;}
    public decimal DecisionImpact{get;set;}
    public decimal ResidualUncertainty{get;set;}
    public SemanticRole SemanticRole{get;set;}=SemanticRole.CompetingInterpretation;
    public string? ResolutionReason{get;set;}
}

// ── Validation ─────────────────────────────────────────────────────────────────────────────────
public sealed record HierarchyValidationIssue(string IssueCode,string Severity,string? NodeId,string Message)
{
    public const string SeverityError="ERROR";
    public const string SeverityWarning="WARNING";
}

public sealed record HierarchyValidationResult(IReadOnlyList<HierarchyValidationIssue> Issues,bool PossibleMissedMaterialAmbiguity)
{
    public bool IsValid=>Issues.All(x=>x.Severity!=HierarchyValidationIssue.SeverityError);
    public int SemanticFailureCount=>Issues.Count(x=>x.Severity==HierarchyValidationIssue.SeverityError);
}

// Authoritative hierarchy after POLOXI validation/repair (proposal is never authoritative).
public sealed record ValidatedHierarchy(AmbiguityAnalysisResult Proposal,HierarchyValidationResult Validation)
{
    public IReadOnlyList<HierarchyNodeDto> Nodes=>Proposal.Nodes;
    public IReadOnlyList<NodeDependencyDto> Dependencies=>Proposal.Dependencies;
    public static ValidatedHierarchy From(AmbiguityAnalysisResult proposal)=>new(proposal,new([],false));
}

// ── Stitching output: multiple ambiguities become one coherent meaning ─────────────────────────
public sealed record ResolvedDimension(string NodeId,string Name,SemanticRole Role,PreferenceDirection Direction,string? MetricOrObservation,string? EvidenceNeeded,decimal Weight);
public sealed record ResolvedConstraint(string NodeId,string Name,string? OperationalDefinition);
public sealed record ResolvedPreference(string NodeId,string Name,PreferenceDirection Direction,decimal Weight);
public sealed record InteractionRule(string SourceNodeId,string TargetNodeId,DependencyType Type,decimal Strength,string? Reason);
public sealed record RemainingUncertainty(string NodeId,string Name,decimal ResidualUncertainty,string? Reason);

public sealed record InterpretationComposite
{
    public required string Objective{get;init;}
    public required IReadOnlyList<ResolvedDimension> Dimensions{get;init;}
    public required IReadOnlyList<ResolvedConstraint> HardConstraints{get;init;}
    public required IReadOnlyList<ResolvedPreference> Preferences{get;init;}
    public required IReadOnlyList<InteractionRule> Interactions{get;init;}
    public required IReadOnlyList<RemainingUncertainty> Uncertainties{get;init;}
    // Global convergence: stitching is reversible. When not converged, ReopenCandidateNodeId names
    // the highest-decision-impact remaining uncertainty whose different resolution could change the
    // ranking; downstream stages reopen that branch (and its dependents) rather than rerunning all.
    public required bool IsConverged{get;init;}
    public string? ReopenCandidateNodeId{get;init;}
    public string? ReopenReason{get;init;}
}

// ── Routing / execution ───────────────────────────────────────────────────────────────────────
public sealed record ExecutionBudget(int MaxAttempts,int MaxEscalations,long MaxLatencyMilliseconds)
{
    public static ExecutionBudget Default=>new(4,1,120000);
}

public sealed record ModelRoute(ModelCapabilityProfile Model,PromptScaffoldingLevel Scaffolding,string? EscalatedFromModelId=null);

public sealed record AmbiguityResolutionRequest(Guid TenantId,Guid UserId,[Required,StringLength(4000,MinimumLength=2)]string Query,string CorrelationId)
{
    // Null/empty = Auto routing through the feature's configured routes; otherwise a model override.
    [StringLength(100)]public string? ModelCode{get;init;}
}

public sealed record AmbiguityResolutionOutcome(Guid AmbiguityRunId,QueryComplexityProfile Complexity,ValidatedHierarchy Hierarchy,IReadOnlyList<BranchRuntimeState> BranchStates,InterpretationComposite Composite,int AttemptCount,string? SelectedModelCode,PromptScaffoldingLevel SelectedScaffolding,bool CoverageSuspicion);

// Prompt template resolved from POLOXI.PromptStrategy (database-backed, versioned).
public sealed record AmbiguityPromptTemplate(string PurposeCode,PromptScaffoldingLevel Level,int VersionNumber,string SystemPrompt,string UserPromptTemplate)
{
    public const string PurposeDiscovery="AMBIGUITY_DISCOVERY";
    public const string PurposeRepair="HIERARCHY_REPAIR";
}

// Observability record for POLOXI.AmbiguityModelInvocation (raw data for capability learning).
public sealed record AmbiguityModelInvocationRecord(ReasoningTaskType TaskType,string? ModelCode,string PromptPurposeCode,PromptScaffoldingLevel Scaffolding,int PromptVersionNumber,int InputTokenCount,int OutputTokenCount,long DurationMilliseconds,bool IsSuccess,bool IsSchemaValid,int RetryNumber,string? EscalatedFromModelCode,string? FailureMessage);
