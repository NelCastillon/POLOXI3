using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.Intelligence;

namespace Ams.Application.Features.Intelligence.Ambiguity;

// Database-backed model routing for the ambiguity subsystem: capability profiles come from
// POLOXI.ModelCapabilityProfile (never hardcoded), and escalation moves to the cheapest active
// higher-tier profile.
public sealed class AmbiguityModelRouter(IIntelligenceAmbiguityRepository repository,IAmbiguityPromptSelector promptSelector):IAmbiguityModelRouter
{
    public async Task<ModelRoute> SelectAsync(Guid tenantId,ReasoningTaskType task,QueryComplexityProfile query,string? modelCodeOverride,CancellationToken cancellationToken=default)
    {
        var model=await repository.GetModelCapabilityProfileAsync(tenantId,modelCodeOverride,cancellationToken);
        return new(model,promptSelector.Select(model,query));
    }

    public async Task<ModelRoute?> EscalateAsync(Guid tenantId,ModelRoute current,CancellationToken cancellationToken=default)
    {
        var stronger=await repository.GetEscalationProfileAsync(tenantId,current.Model.Tier,cancellationToken);
        return stronger is null?null:new(stronger,PromptScaffoldingLevel.Medium,current.Model.ModelId);
    }
}

// Application orchestrator for Stage 1.1–1.8 of the model-adaptive POLOXI pipeline:
// complexity → routing → discovery → validation → repair/escalation ladder → narrowing → stitching.
// Retry ladder: (1) selected prompt, (2) same model + repair prompt, (3) heavier scaffolding,
// (4) model escalation, then fail-soft. The LLM proposes; POLOXI validates and decides.
public sealed class AmbiguityResolutionEngine(
    IQueryComplexityAnalyzer complexityAnalyzer,
    IAmbiguityModelRouter modelRouter,
    IEnumerable<IAmbiguityPromptStrategy> promptStrategies,
    IAiProviderRouter aiRouter,
    IHierarchyValidator validator,
    IModelEscalationPolicy escalationPolicy,
    IAmbiguityNarrowingEngine narrowingEngine,
    IInterpretationStitcher stitcher,
    IIntelligenceAmbiguityRepository repository):IAmbiguityResolutionEngine
{
    public const string FeatureCode="INTELLIGENCE_POLOXI_AMBIGUITY";
    private static readonly JsonSerializerOptions SerializerOptions=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};

    public async Task<AmbiguityResolutionOutcome> ResolveAsync(AmbiguityResolutionRequest request,CancellationToken cancellationToken=default)
    {
        var stopwatch=Stopwatch.StartNew();
        var complexity=complexityAnalyzer.Analyze(request.Query);
        var route=await modelRouter.SelectAsync(request.TenantId,ReasoningTaskType.AmbiguityDiscovery,complexity,request.ModelCode,cancellationToken);
        var runId=await repository.StartRunAsync(request.TenantId,request.UserId,request.Query,complexity,route.Scaffolding,route.Model.ModelId,cancellationToken);
        var budget=ExecutionBudget.Default;
        AmbiguityAnalysisResult? proposal=null;HierarchyValidationResult? validation=null;string? escalatedFrom=null;var attempt=0;
        try
        {
            while(attempt<budget.MaxAttempts)
            {
                attempt++;
                // Repair only when the previous proposal failed validation; a valid-but-coverage-suspicious
                // proposal gets a second DISCOVERY pass (possibly escalated), never a repair with no errors.
                var isRepair=attempt==2&&proposal is not null&&validation is not null&&!validation.IsValid;
                var purpose=isRepair?AmbiguityPromptTemplate.PurposeRepair:AmbiguityPromptTemplate.PurposeDiscovery;
                // Ladder: attempt 3 escalates scaffolding on the SAME model; after model escalation the
                // escalated route's own scaffolding governs (Mini+Medium → Mini+Heavy → Premium+Medium).
                var level=isRepair?PromptScaffoldingLevel.Heavy:attempt>=3&&escalatedFrom is null&&route.Scaffolding!=PromptScaffoldingLevel.Heavy?PromptScaffoldingLevel.Heavy:route.Scaffolding;
                var template=await repository.GetPromptTemplateAsync(request.TenantId,purpose,level,cancellationToken);
                var strategy=promptStrategies.Single(x=>x.Level==template.Level);
                var userPrompt=isRepair
                    ?template.UserPromptTemplate
                        .Replace("{Query}",request.Query,StringComparison.Ordinal)
                        .Replace("{Proposal}",JsonSerializer.Serialize(proposal,SerializerOptions),StringComparison.Ordinal)
                        .Replace("{Issues}",string.Join('\n',validation!.Issues.Select(x=>$"{x.Severity} {x.IssueCode} {x.NodeId}: {x.Message}")),StringComparison.Ordinal)
                    :strategy.BuildUserPrompt(template,request.Query,complexity,route.Model);
                var invocationTimer=Stopwatch.StartNew();
                AiGenerationResult? generation=null;AmbiguityAnalysisResult? parsed=null;string? failure=null;
                try
                {
                    generation=await aiRouter.GenerateAsync(request.TenantId,FeatureCode,template.SystemPrompt,userPrompt,OutputSchemaJson,request.CorrelationId,null,ModelOverride(route),cancellationToken);
                    parsed=Parse(generation.StructuredOutputJson??generation.Content);
                }
                catch(Exception ex)when(ex is not OperationCanceledException){failure=ex.Message;}
                invocationTimer.Stop();
                await repository.RecordInvocationAsync(request.TenantId,runId,new(isRepair?ReasoningTaskType.HierarchyRepair:ReasoningTaskType.AmbiguityDiscovery,route.Model.ModelId,purpose,template.Level,template.VersionNumber,generation?.InputTokenCount??0,generation?.OutputTokenCount??0,invocationTimer.ElapsedMilliseconds,failure is null,parsed is not null,attempt-1,escalatedFrom,failure),cancellationToken);
                if(parsed is not null)
                {
                    proposal=parsed;
                    validation=validator.Validate(proposal,complexity,route.Model);
                    if(validation.Issues.Count>0)await repository.RecordValidationIssuesAsync(request.TenantId,runId,attempt,validation.Issues,cancellationToken);
                    if(validation.IsValid&&!validation.PossibleMissedMaterialAmbiguity)break;
                }
                var failedValidation=validation??new([new("NO_PROPOSAL",HierarchyValidationIssue.SeverityError,null,failure??"The model returned no parseable hierarchy proposal.")],false);
                if(escalationPolicy.ShouldEscalate(failedValidation,complexity,route.Model,attempt,budget))
                {
                    var escalated=await modelRouter.EscalateAsync(request.TenantId,route,cancellationToken);
                    if(escalated is not null){escalatedFrom=route.Model.ModelId;route=escalated;}
                }
                if(validation is not null&&validation.IsValid)break; // coverage suspicion only: accept after the extra pass attempt
            }
            if(proposal is null)throw new InvalidOperationException("The ambiguity discovery ladder produced no usable hierarchy proposal.");
            validation??=validator.Validate(proposal,complexity,route.Model);
            var hierarchy=new ValidatedHierarchy(proposal,validation);
            var states=narrowingEngine.Resolve(hierarchy);
            var composite=stitcher.Stitch(hierarchy,states,hierarchy.Dependencies);
            stopwatch.Stop();
            var status=validation.IsValid?"COMPLETED":"FAIL_SOFT";
            await repository.CompleteRunAsync(request.TenantId,request.UserId,runId,status,attempt,route.Model.ModelId,escalatedFrom,validation.PossibleMissedMaterialAmbiguity,hierarchy,states,JsonSerializer.Serialize(composite,SerializerOptions),stopwatch.ElapsedMilliseconds,cancellationToken);
            return new(runId,complexity,hierarchy,states,composite,attempt,route.Model.ModelId,route.Scaffolding,validation.PossibleMissedMaterialAmbiguity);
        }
        catch(Exception ex)when(ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            await repository.CompleteRunAsync(request.TenantId,request.UserId,runId,"FAILED",attempt,route.Model.ModelId,escalatedFrom,false,ValidatedHierarchy.From(new(){RootId="",OriginalRequest=request.Query,Nodes=[]}),[],null,stopwatch.ElapsedMilliseconds,cancellationToken);
            throw;
        }
    }

    private static string? ModelOverride(ModelRoute route)=>string.IsNullOrWhiteSpace(route.Model.ModelId)?null:route.Model.ModelId;

    private static AmbiguityAnalysisResult? Parse(string content)
    {
        if(string.IsNullOrWhiteSpace(content))return null;
        var start=content.IndexOf('{');var end=content.LastIndexOf('}');
        if(start<0||end<=start)return null;
        try{return JsonSerializer.Deserialize<AmbiguityAnalysisResult>(content[start..(end+1)],SerializerOptions);}catch(JsonException){return null;}
    }

    // Canonical model-independent output schema: identical for Small, Standard, and Premium models.
    private const string OutputSchemaJson="""
{"type":"object","properties":{"rootId":{"type":"string"},"originalRequest":{"type":"string"},"ambiguityCount":{"type":"integer"},"nodes":{"type":"array","items":{"type":"object","properties":{"id":{"type":"string"},"parentId":{"type":["string","null"]},"depth":{"type":"integer"},"name":{"type":"string"},"sourceText":{"type":["string","null"]},"nodeType":{"type":"string","enum":["Root","Ambiguity","Interpretation","Dimension","SubDimension","EvidenceLeaf"]},"ambiguityType":{"type":["string","null"],"enum":["Semantic","Metric","Scope","Threshold","Temporal","Referential","Relational","Constraint","Objective","MissingVariable","Interaction","Operational",null]},"materiality":{"type":"string","enum":["Low","Medium","High","Critical"]},"decisionRole":{"type":"string","enum":["Unknown","HardConstraint","SoftPreference","OptimizationObjective","Context"]},"operationalDefinition":{"type":["string","null"]},"metricOrObservation":{"type":["string","null"]},"evidenceNeeded":{"type":["string","null"]},"evidenceType":{"type":["string","null"]},"preferenceDirection":{"type":"string","enum":["Unknown","HigherIsBetter","LowerIsBetter","TargetRange","Boolean","Categorical"]},"isLeaf":{"type":"boolean"},"proposedConfidence":{"type":["number","null"]}},"required":["id","depth","name","nodeType","materiality","decisionRole","preferenceDirection","isLeaf"]}},"dependencies":{"type":"array","items":{"type":"object","properties":{"sourceNodeId":{"type":"string"},"targetNodeId":{"type":"string"},"type":{"type":"string","enum":["DependsOn","Modifies","Constrains","Overlaps","ConflictsWith","ProvidesContextFor"]},"reason":{"type":["string","null"]},"strength":{"type":["number","null"]}},"required":["sourceNodeId","targetNodeId","type"]}},"excludedAmbiguities":{"type":"array","items":{"type":"object","properties":{"name":{"type":"string"},"reason":{"type":"string"}},"required":["name","reason"]}},"audit":{"type":["object","null"],"properties":{"secondScanPerformed":{"type":"boolean"},"siblingDistinctnessVerified":{"type":"boolean"},"parentChildVerified":{"type":"boolean"},"leafEvidenceVerified":{"type":"boolean"},"notes":{"type":["string","null"]}}}},"required":["rootId","originalRequest","ambiguityCount","nodes"]}
""";
}
