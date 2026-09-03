using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Intelligence;

// POLOXI Model-Adaptive Ambiguity subsystem abstractions. Flow:
// Model → Proposal → POLOXI Validation → Authoritative State → Narrowing → Stitching → Decision.
// ModelTask = f(TaskType, QueryComplexity, BranchComplexity, ModelCapability, Confidence, Budget).

public interface IQueryComplexityAnalyzer
{
    QueryComplexityProfile Analyze(string query);
}

public interface IAmbiguityPromptStrategy
{
    PromptScaffoldingLevel Level{get;}
    string BuildUserPrompt(AmbiguityPromptTemplate template,string userQuery,QueryComplexityProfile complexity,ModelCapabilityProfile model);
}

public interface IAmbiguityPromptSelector
{
    PromptScaffoldingLevel Select(ModelCapabilityProfile model,QueryComplexityProfile query);
}

public interface IAmbiguityModelRouter
{
    Task<ModelRoute> SelectAsync(Guid tenantId,ReasoningTaskType task,QueryComplexityProfile query,string? modelCodeOverride,CancellationToken cancellationToken=default);
    Task<ModelRoute?> EscalateAsync(Guid tenantId,ModelRoute current,CancellationToken cancellationToken=default);
}

public interface IHierarchyValidator
{
    HierarchyValidationResult Validate(AmbiguityAnalysisResult proposal,QueryComplexityProfile complexity,ModelCapabilityProfile model);
}

public interface IModelEscalationPolicy
{
    bool ShouldEscalate(HierarchyValidationResult validation,QueryComplexityProfile complexity,ModelCapabilityProfile currentModel,int attemptNumber,ExecutionBudget budget);
}

public interface IAmbiguityNarrowingEngine
{
    IReadOnlyList<BranchRuntimeState> Resolve(ValidatedHierarchy hierarchy);
}

public interface IInterpretationStitcher
{
    InterpretationComposite Stitch(ValidatedHierarchy hierarchy,IReadOnlyCollection<BranchRuntimeState> states,IReadOnlyCollection<NodeDependencyDto> dependencies);
}

public interface IAmbiguityResolutionEngine
{
    Task<AmbiguityResolutionOutcome> ResolveAsync(AmbiguityResolutionRequest request,CancellationToken cancellationToken=default);
}
