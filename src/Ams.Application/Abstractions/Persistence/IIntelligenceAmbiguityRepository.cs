using Ams.Application.Features.Intelligence;

namespace Ams.Application.Abstractions.Persistence;

// Persistence contract for the POLOXI Model-Adaptive Ambiguity subsystem (POLOXI.ModelCapabilityProfile,
// POLOXI.PromptStrategy, POLOXI.AmbiguityRun/Node/NodeDependency/ValidationIssue/ModelInvocation).
// Capability profiles and prompt templates are database-backed configuration — never hardcoded.
public interface IIntelligenceAmbiguityRepository
{
    // First active LIKE-pattern match by SortOrder (tenant rows override global); the seeded '%'
    // row guarantees a STANDARD fallback for unknown model codes.
    Task<ModelCapabilityProfile> GetModelCapabilityProfileAsync(Guid tenantId,string? modelCode,CancellationToken cancellationToken=default);
    // Cheapest active model of a strictly higher tier than the given tier, or null when none exists.
    Task<ModelCapabilityProfile?> GetEscalationProfileAsync(Guid tenantId,ModelTier aboveTier,CancellationToken cancellationToken=default);
    Task<AmbiguityPromptTemplate> GetPromptTemplateAsync(Guid tenantId,string purposeCode,PromptScaffoldingLevel level,CancellationToken cancellationToken=default);
    Task<Guid> StartRunAsync(Guid tenantId,Guid userId,string queryText,QueryComplexityProfile complexity,PromptScaffoldingLevel scaffolding,string? modelCode,CancellationToken cancellationToken=default);
    Task RecordInvocationAsync(Guid tenantId,Guid ambiguityRunId,AmbiguityModelInvocationRecord invocation,CancellationToken cancellationToken=default);
    Task RecordValidationIssuesAsync(Guid tenantId,Guid ambiguityRunId,int attemptNumber,IReadOnlyCollection<HierarchyValidationIssue> issues,CancellationToken cancellationToken=default);
    Task CompleteRunAsync(Guid tenantId,Guid userId,Guid ambiguityRunId,string statusCode,int attemptCount,string? selectedModelCode,string? escalatedFromModelCode,bool coverageSuspicion,ValidatedHierarchy hierarchy,IReadOnlyCollection<BranchRuntimeState> states,string? compositeJson,long durationMilliseconds,CancellationToken cancellationToken=default);
}
