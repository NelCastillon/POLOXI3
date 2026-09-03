using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Features.Intelligence;

namespace Ams.Application.Features.Intelligence.Ambiguity;

// Deterministic query complexity heuristics. The query itself matters as much as model capability:
// even a Premium model receives heavy scaffolding for an extreme query. Signals are lexical only
// (no LLM cost): subjective terms, constraint markers, interaction markers, concept density.
public sealed class QueryComplexityAnalyzer:IQueryComplexityAnalyzer
{
    private static readonly string[] SubjectiveTerms=["best","good","great","affordable","cheap","reasonable","safe","nice","quality","reliable","suitable","ideal","optimal","comfortable","convenient","modern","top","worst","fast","slow","strong","weak","easy","friendly","potential"];
    private static readonly string[] ConstraintMarkers=["must","under","over","at least","at most","no more than","within","before","after","between","maximum","minimum","required","only","except","excluding","less than","greater than","up to"];
    private static readonly string[] InteractionMarkers=["and","with","while","near","for my","balance","trade-off","tradeoff","versus","vs","but also","as well as","combined","both"];
    private static readonly string[] EvidenceMarkers=["compare","rank","evaluate","evidence","data","statistics","history","trend","rate","score","price","cost","average","median","per","percent"];
    private static readonly char[] Separators=[' ','\t','\r','\n',',',';','.','!','?','(',')','[',']','"','\''];

    public QueryComplexityProfile Analyze(string query)
    {
        var text=(query??string.Empty).Trim();
        var lower=text.ToLowerInvariant();
        var words=lower.Split(Separators,StringSplitOptions.RemoveEmptyEntries);
        var subjective=SubjectiveTerms.Count(term=>ContainsTerm(lower,words,term));
        var constraints=ConstraintMarkers.Count(term=>ContainsTerm(lower,words,term));
        var interactions=InteractionMarkers.Count(term=>ContainsTerm(lower,words,term));
        var evidence=EvidenceMarkers.Count(term=>ContainsTerm(lower,words,term));
        // Concept count: distinct content words (length > 3) as a cheap proxy for interacting concepts.
        var conceptCount=words.Where(word=>word.Length>3).Distinct(StringComparer.Ordinal).Count();
        var ambiguityLikelihood=Clamp01(subjective*0.15m+(conceptCount>8?0.25m:0m)+(interactions>2?0.2m:0m));
        var semanticComplexity=Clamp01(conceptCount/25m+subjective*0.08m);
        var constraintComplexity=Clamp01(constraints*0.18m);
        var interactionComplexity=Clamp01(interactions*0.12m);
        var evidenceComplexity=Clamp01(evidence*0.15m+subjective*0.05m);
        var overall=(ambiguityLikelihood+semanticComplexity+constraintComplexity+interactionComplexity+evidenceComplexity)/5m;
        var level=overall switch{<0.15m=>ComplexityLevel.Simple,<0.35m=>ComplexityLevel.Moderate,<0.60m=>ComplexityLevel.Complex,_=>ComplexityLevel.Extreme};
        // Short factual questions with no subjective terms stay Simple regardless of density.
        if(subjective==0&&constraints==0&&words.Length<=8)level=ComplexityLevel.Simple;
        return new(){AmbiguityLikelihood=ambiguityLikelihood,SemanticComplexity=semanticComplexity,ConstraintComplexity=constraintComplexity,InteractionComplexity=interactionComplexity,EvidenceComplexity=evidenceComplexity,ConceptCount=conceptCount,SubjectiveTermCount=subjective,OverallLevel=level};
    }

    private static bool ContainsTerm(string lower,string[] words,string term)=>term.Contains(' ')?lower.Contains(term,StringComparison.Ordinal):words.Contains(term,StringComparer.Ordinal);
    private static decimal Clamp01(decimal value)=>Math.Clamp(value,0m,1m);
}

// PromptStrategy = f(ModelCapability, QueryComplexity) — the routing matrix, not "Mini = long prompt".
public sealed class AmbiguityPromptSelector:IAmbiguityPromptSelector
{
    public PromptScaffoldingLevel Select(ModelCapabilityProfile model,QueryComplexityProfile query)
    {
        if(model.Tier==ModelTier.Small)return PromptScaffoldingLevel.Heavy;
        if(query.OverallLevel==ComplexityLevel.Extreme)return PromptScaffoldingLevel.Heavy;
        if(model.Tier==ModelTier.Premium&&query.OverallLevel<=ComplexityLevel.Moderate)return PromptScaffoldingLevel.Light;
        if(model.Tier==ModelTier.Standard&&query.OverallLevel==ComplexityLevel.Simple)return PromptScaffoldingLevel.Light;
        if(model.Tier==ModelTier.Standard&&query.OverallLevel==ComplexityLevel.Complex)return PromptScaffoldingLevel.Heavy;
        return PromptScaffoldingLevel.Medium;
    }
}

// Prompt strategies fill database-backed templates (POLOXI.PromptStrategy). Placeholders:
// {Query} = original request, {MaxDepth} = model's recommended maximum recursion depth.
public abstract class AmbiguityPromptStrategyBase:IAmbiguityPromptStrategy
{
    public abstract PromptScaffoldingLevel Level{get;}
    public string BuildUserPrompt(AmbiguityPromptTemplate template,string userQuery,QueryComplexityProfile complexity,ModelCapabilityProfile model)
        =>template.UserPromptTemplate.Replace("{Query}",userQuery,StringComparison.Ordinal).Replace("{MaxDepth}",model.RecommendedMaxDepth.ToString(),StringComparison.Ordinal);
}

public sealed class HeavyAmbiguityPromptStrategy:AmbiguityPromptStrategyBase{public override PromptScaffoldingLevel Level=>PromptScaffoldingLevel.Heavy;}
public sealed class MediumAmbiguityPromptStrategy:AmbiguityPromptStrategyBase{public override PromptScaffoldingLevel Level=>PromptScaffoldingLevel.Medium;}
public sealed class LightAmbiguityPromptStrategy:AmbiguityPromptStrategyBase{public override PromptScaffoldingLevel Level=>PromptScaffoldingLevel.Light;}

// Mid-flight escalation: don't decide Premium vs Mini only at request start. Escalate when the
// validated proposal shows repeated semantic failures, suspicious coverage, or budget allows and
// scaffolding is already at maximum for a non-premium model.
public sealed class ModelEscalationPolicy:IModelEscalationPolicy
{
    public bool ShouldEscalate(HierarchyValidationResult validation,QueryComplexityProfile complexity,ModelCapabilityProfile currentModel,int attemptNumber,ExecutionBudget budget)
    {
        if(currentModel.Tier==ModelTier.Premium)return false;
        if(attemptNumber>=budget.MaxAttempts)return false;
        if(validation.SemanticFailureCount>=2)return true;
        if(validation.PossibleMissedMaterialAmbiguity&&complexity.OverallLevel>=ComplexityLevel.Complex)return true;
        // Retry ladder exhausted scaffolding for this model tier: attempts 1..2 stay on-model.
        return attemptNumber>=3&&!validation.IsValid;
    }
}
