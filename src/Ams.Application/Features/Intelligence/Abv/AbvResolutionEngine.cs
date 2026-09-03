using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Abstractions.Persistence;

namespace Ams.Application.Features.Intelligence.Abv;

// POLOXI ABV (Actionable Business Value) orchestrator. Runs AFTER convergence:
// Convergence gate → load Domain Pack → LLM intent proposal (taxonomy-bounded) → intent validation
// → deterministic impact/urgency/owner/action resolution → actionability gate → persist.
// The LLM proposes intent ONLY; every business value is deterministic and provenance-tagged.
// Fail-soft: a non-converged composite, a rejected intent, or a model failure yields a BLOCKED
// outcome rather than a fabricated action.
public sealed class AbvResolutionEngine(
    IAiProviderRouter aiRouter,
    IAbvGovernanceEngine governance,
    IIntelligenceAmbiguityRepository ambiguityRepository,
    IIntelligenceAbvRepository abvRepository):IAbvResolutionEngine
{
    public const string FeatureCode="INTELLIGENCE_POLOXI_ABV";
    public const string PurposeIntent="ABV_INTENT";
    private static readonly JsonSerializerOptions SerializerOptions=new(JsonSerializerDefaults.Web){Converters={new JsonStringEnumConverter()}};

    public async Task<AbvResolutionOutcome> ResolveAsync(AbvResolutionRequest request,CancellationToken cancellationToken=default)
    {
        var stopwatch=Stopwatch.StartNew();
        var pack=await abvRepository.GetDomainPackAsync(request.TenantId,request.DomainPackCode,cancellationToken);

        // Truth != Action: ABV never runs on an unconverged composite. Reopen the ambiguity branch
        // instead of acting on an unstable interpretation.
        if(!request.Composite.IsConverged)
            return await FailSoftAsync(request,pack,null,AbvResolutionStatus.NotConverged,null,stopwatch,cancellationToken);

        AbvIntentProposal? proposal=null;string? failure=null;
        try
        {
            var template=await ambiguityRepository.GetPromptTemplateAsync(request.TenantId,PurposeIntent,PromptScaffoldingLevel.Medium,cancellationToken);
            var userPrompt=template.UserPromptTemplate
                .Replace("{IntentTaxonomy}",FormatTaxonomy(pack),StringComparison.Ordinal)
                .Replace("{Composite}",JsonSerializer.Serialize(request.Composite,SerializerOptions),StringComparison.Ordinal);
            var generation=await aiRouter.GenerateAsync(request.TenantId,FeatureCode,template.SystemPrompt,userPrompt,OutputSchemaJson,request.CorrelationId,null,request.ModelCode,cancellationToken);
            proposal=Parse(generation.StructuredOutputJson??generation.Content);
        }
        catch(Exception ex)when(ex is not OperationCanceledException){failure=ex.Message;}

        // The LLM proposes intent ONLY. If the proposal is missing (template/provider failure) or falls
        // outside the taxonomy, POLOXI does NOT go dark: it falls back to a deterministic default intent
        // drawn straight from the Domain-Pack taxonomy. Every downstream business value (impact, urgency,
        // owner, action) still resolves deterministically from configuration with provenance — nothing is
        // fabricated. This guarantees a reviewable Action Business Plan for any converged decision.
        var intent=proposal is null?null:governance.AcceptIntent(proposal,request.Composite,pack);
        if(intent is null)
        {
            var fallbackIntent=DefaultIntent(pack);
            if(fallbackIntent is null)
                return await FailSoftAsync(request,pack,proposal?.IntentCode,proposal is null?AbvResolutionStatus.Failed:AbvResolutionStatus.IntentRejected,failure??$"Proposed intent '{proposal?.IntentCode}' is not in the '{pack.PackCode}' taxonomy.",stopwatch,cancellationToken);
            intent=new(fallbackIntent.IntentCode,fallbackIntent.Name,proposal?.Rationale??"Deterministic default intent applied because no valid model intent proposal was available; business values resolved from Domain-Pack configuration.",AbvSource.DomainConfiguration,[]);
        }

        var impact=governance.ResolveImpact(intent,request.Composite,pack);
        var urgency=governance.ResolveUrgency(intent,impact,pack);
        var executionPath=governance.ResolveExecutionPath(intent,impact,pack);
        var actionability=governance.ResolveActionability(AbvResolutionStatus.Resolved,executionPath,pack);

        var outcome=new AbvResolutionOutcome
        {
            AbvResolutionId=Guid.Empty,
            Status=AbvResolutionStatus.Resolved,
            Intent=intent,
            Impact=impact,
            Urgency=urgency,
            ExecutionPath=executionPath,
            Actionability=actionability
        };
        stopwatch.Stop();
        var id=await abvRepository.RecordResolutionAsync(request.TenantId,request.UserId,request.AmbiguityRunId,pack.AbvDomainPackId,intent.IntentCode,outcome,stopwatch.ElapsedMilliseconds,cancellationToken);
        return outcome with{AbvResolutionId=id};
    }

    // Deterministic default intent for the fallback path: prefer INVESTIGATE (the safe "look closer"
    // response), then MONITOR, otherwise the first taxonomy intent. Pack taxonomy order is authoritative.
    private static AbvIntentDefinition? DefaultIntent(AbvDomainPack pack)=>
        pack.Intents.FirstOrDefault(i=>string.Equals(i.IntentCode,"INVESTIGATE",StringComparison.OrdinalIgnoreCase))
        ??pack.Intents.FirstOrDefault(i=>string.Equals(i.IntentCode,"MONITOR",StringComparison.OrdinalIgnoreCase))
        ??pack.Intents.FirstOrDefault();

    private async Task<AbvResolutionOutcome> FailSoftAsync(AbvResolutionRequest request,AbvDomainPack pack,string? proposedIntentCode,AbvResolutionStatus status,string? failure,Stopwatch stopwatch,CancellationToken cancellationToken)
    {
        var actionability=governance.ResolveActionability(status,null,pack);
        var outcome=new AbvResolutionOutcome
        {
            AbvResolutionId=Guid.Empty,
            Status=status,
            Actionability=actionability,
            FailureMessage=failure
        };
        stopwatch.Stop();
        var id=await abvRepository.RecordResolutionAsync(request.TenantId,request.UserId,request.AmbiguityRunId,pack.AbvDomainPackId,proposedIntentCode,outcome,stopwatch.ElapsedMilliseconds,cancellationToken);
        return outcome with{AbvResolutionId=id};
    }

    private static string FormatTaxonomy(AbvDomainPack pack)=>
        string.Join('\n',pack.Intents.Select(i=>$"- {i.IntentCode}: {i.Name}{(string.IsNullOrWhiteSpace(i.Description)?"":$" — {i.Description}")}"));

    public static AbvIntentProposal? Parse(string? json)
    {
        if(string.IsNullOrWhiteSpace(json))return null;
        try
        {
            var parsed=JsonSerializer.Deserialize<AbvIntentProposal>(json,SerializerOptions);
            return parsed is null||string.IsNullOrWhiteSpace(parsed.IntentCode)?null:parsed;
        }
        catch(JsonException){return null;}
    }

    // Strict structured-output schema: the LLM returns an intent code plus supporting evidence only.
    public const string OutputSchemaJson="""
    {
      "type":"object",
      "additionalProperties":false,
      "required":["intentCode","rationale","supportingDimensionIds","proposedMetricAtRisk"],
      "properties":{
        "intentCode":{"type":"string"},
        "rationale":{"type":["string","null"]},
        "supportingDimensionIds":{"type":"array","items":{"type":"string"}},
        "proposedMetricAtRisk":{"type":["string","null"]}
      }
    }
    """;
}
