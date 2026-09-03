using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Intelligence;

namespace Ams.Application;

// Isolated clone of the POLOXI search orchestration used by /intelligence/search/poloxi_wide.
// Intentionally duplicates IntelligenceService.SearchWithPoloxiAsync so this "Wide" path can be
// tweaked freely without changing /intelligence/search/poloxi behavior.
public sealed class IntelligenceWideService(IIntelligenceRepository repository,IIntelligenceWideRepository wideRepository,IAiProviderRouter aiProviderRouter,IExternalKnowledgeProvider externalKnowledgeProvider,IPromptCatalog promptCatalog,IAdaptiveRetriever adaptiveRetriever,IAbvResolutionEngine abvEngine):IIntelligenceWideService
{
    private const int WideUserPromptBudget=12000;

    // Model selection: null/whitespace = Auto (feature-policy routing); otherwise route every wide LLM call through the requested model.
    private static string? ModelOverride(WideSearchRequest request)=>string.IsNullOrWhiteSpace(request.ModelCode)?null:request.ModelCode.Trim();

    public Task<IReadOnlyCollection<WideModelOptionDto>> GetWideModelsAsync(Guid tenantId,CancellationToken cancellationToken=default)=>wideRepository.GetWideModelsAsync(tenantId,cancellationToken);

    public async Task<PoloxiSearchResponse> SearchWithPoloxiWideAsync(PoloxiSearchRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);
        if(request.UserId==Guid.Empty)throw new UnauthorizedAccessException("An authenticated user is required for POLOXI search.");
        var timer=Stopwatch.StartNew();
        var normalizedQuery=NormalizeQuery(request.Query).ToLowerInvariant();
        request=request with{Query=NormalizeQuery(request.Query),MaximumResults=Math.Clamp(request.MaximumResults,1,100),CorrelationId=string.IsNullOrWhiteSpace(request.CorrelationId)?$"poloxi-search-wide:{Guid.NewGuid():N}":request.CorrelationId.Trim()};
        var configuration=await repository.GetPoloxiConfigurationAsync(request.TenantId,cancellationToken);
        var capabilities=await repository.GetPoloxiCapabilitiesAsync(request.TenantId,cancellationToken);
        if(capabilities.Count==0)throw new InvalidOperationException("No active POLOXI capabilities are configured.");
        if(!request.UsePoloxiEngine)return await SearchWithoutPoloxiEngineAsync(request,capabilities,configuration,timer,cancellationToken);
        var signature=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedQuery)));
        var hierarchy=configuration.EnableHierarchyReuse?await repository.GetReusablePoloxiHierarchyAsync(request.TenantId,signature,cancellationToken):null;
        var reused=hierarchy is not null;
        if(hierarchy is null)
        {
            var generated=await GeneratePoloxiProposalAsync(request,capabilities,configuration,cancellationToken);
            var branches=ValidatePoloxiBranches(generated.Proposal,capabilities,configuration);
            hierarchy=await repository.SavePoloxiHierarchyAsync(request.TenantId,request.UserId,signature,normalizedQuery,generated.Proposal,generated.ProviderCode,generated.ModelCode,DateTime.UtcNow.AddHours(configuration.HierarchyCacheHours),branches,cancellationToken);
        }
        var validBranches=hierarchy.Branches.Where(branch=>branch.ValidationStatusCode.Equals("VALID",StringComparison.OrdinalIgnoreCase)).Take(configuration.MaximumBranches).ToArray();
        var executionId=await repository.StartPoloxiExecutionAsync(new(request.TenantId,hierarchy.HierarchyId,request.UserId,request.Query,request.CorrelationId,reused,validBranches.Length,hierarchy.Branches.Count-validBranches.Length,hierarchy.Confidence),cancellationToken);
        var evidence=new List<PoloxiEvidenceDto>();
        // Progressive narrowing: parents execute first; a child branch keeps only evidence entities its parent branch also matched.
        var branchEvidenceKeys=new Dictionary<Guid,HashSet<string>>();
        // Global-convergence bookkeeping: raw (pre-narrowing) evidence and kept count per branch, so the
        // convergence round below can distinguish "capability found nothing" from "narrowing emptied it".
        var branchOutcomes=new Dictionary<Guid,(IReadOnlyCollection<PoloxiEvidenceDto> Raw,int KeptCount)>();
        foreach(var branch in validBranches)
        {
            var capability=capabilities.First(item=>item.CapabilityCode.Equals(branch.CapabilityCode,StringComparison.OrdinalIgnoreCase));
            // Level-2 tactical retrieval is delegated to the adaptive retriever; the primary pass uses a
            // single-attempt budget so first-round behavior is identical to direct capability execution.
            var branchEvidence=(await adaptiveRetriever.RetrieveAsync(new(request,branch,capability,configuration.MaximumResults,1),cancellationToken)).Evidence;
            var rawEvidence=branchEvidence;
            // Narrow only against parents that searched the same entity type; cross-entity parents cannot share keys.
            if(branch.ParentHierarchyBranchId is{}parentId&&branchEvidenceKeys.TryGetValue(parentId,out var parentKeys)&&parentKeys.Any(key=>key.StartsWith($"{capability.EntityTypeCode}:",StringComparison.OrdinalIgnoreCase)))
                branchEvidence=branchEvidence.Where(item=>parentKeys.Contains($"{item.EntityTypeCode}:{item.EntityId:D}")).ToArray();
            branchEvidenceKeys[branch.HierarchyBranchId]=branchEvidence.Select(item=>$"{item.EntityTypeCode}:{item.EntityId:D}").ToHashSet(StringComparer.OrdinalIgnoreCase);
            branchOutcomes[branch.HierarchyBranchId]=(rawEvidence,branchEvidence.Count);
            evidence.AddRange(branchEvidence);
        }
        // GRIP-style global-convergence round (single, budgeted): per-branch retrieval finishing is not the
        // same as the decision being resolved. If a decision-critical branch (highest hierarchy confidence
        // first) ended with zero supporting evidence, invest one targeted recovery attempt per branch:
        //   1. If progressive narrowing emptied a branch that DID retrieve evidence, readmit the raw evidence
        //      (same approved deterministic capability; narrowing is a precision heuristic, not an admission gate).
        //   2. Otherwise retry the capability once with a deterministic alternate approved term.
        // Admission gates are never lowered: every readmission/retry stays inside AUTHORIZED_SEARCH_DOCUMENT
        // capabilities and their approved terms, adds no LLM calls, and is bounded by MaxConvergenceRetrievals.
        var recoveries=new Dictionary<Guid,(string OutcomeCode,int RecoveredCount,string? AlternateSearchText)>();
        foreach(var branch in validBranches.Where(item=>branchOutcomes[item.HierarchyBranchId].KeptCount==0).OrderByDescending(item=>item.Confidence).ThenBy(item=>item.SortOrder).Take(MaxConvergenceRetrievals))
        {
            var capability=capabilities.First(item=>item.CapabilityCode.Equals(branch.CapabilityCode,StringComparison.OrdinalIgnoreCase));
            var raw=branchOutcomes[branch.HierarchyBranchId].Raw;
            var packet=raw.Count>0?new PoloxiEvidencePacket(raw,0,null):await adaptiveRetriever.RetrieveAsync(new(request,branch,capability,configuration.MaximumResults,2),cancellationToken);
            if(packet.Evidence.Count==0)continue;
            recoveries[branch.HierarchyBranchId]=(raw.Count>0?"RECOVERED_READMITTED":"RECOVERED_ALTERNATE_TERM",packet.Evidence.Count,packet.AlternateSearchText);
            branchEvidenceKeys[branch.HierarchyBranchId]=packet.Evidence.Select(item=>$"{item.EntityTypeCode}:{item.EntityId:D}").ToHashSet(StringComparer.OrdinalIgnoreCase);
            evidence.AddRange(packet.Evidence);
        }
        var ranked=RankEvidence(evidence,request,configuration);
        string? explanation=null;
        var explanationStatus="NOT_REQUESTED";
        if(request.IncludeExplanation&&ranked.Length>0)
        {
            try
            {
                var grounding=string.Join('\n',ranked.Take(12).Select((item,index)=>$"[{index+1}] {item.Title} ({string.Join(", ",item.MatchedBranches)}): {item.Excerpt}"));
                var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_POLOXI_EXPLANATION",await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.WidePoloxiExplanation,cancellationToken),$"Question: {request.Query}\nValidated concept: {hierarchy.DisplayName}\nEvidence:\n{grounding}",null,request.CorrelationId,new("Intelligence",null,null,request.Query,"POLOXI_EVIDENCE",executionId,request.CorrelationId,"Intelligent Search Wide"),cancellationToken:cancellationToken);
                explanation=result.Content;
                explanationStatus="COMPLETED";
            }
            catch(Exception exception) when(exception is AiProviderUnavailableException or TimeoutException)
            {
                explanationStatus="UNAVAILABLE";
            }
        }
        timer.Stop();
        await repository.CompletePoloxiExecutionAsync(request.TenantId,request.UserId,executionId,hierarchy.HierarchyId,ranked,explanationStatus,explanation,timer.ElapsedMilliseconds,cancellationToken);
        // Convergence observability: one POLOXI.ExecutionBranchOutcome row per valid branch recording
        // whether it was supported first-pass, recovered (readmitted/alternate term), or left unresolved.
        var outcomeRecords=validBranches.Select(branch=>
        {
            var (raw,kept)=branchOutcomes[branch.HierarchyBranchId];
            var recovery=recoveries.TryGetValue(branch.HierarchyBranchId,out var found)?found:default((string OutcomeCode,int RecoveredCount,string? AlternateSearchText)?);
            var outcomeCode=kept>0?"SUPPORTED":recovery?.OutcomeCode??"UNRESOLVED";
            return new PoloxiBranchOutcomeRecord(branch.HierarchyBranchId,outcomeCode,raw.Count,kept,recovery?.RecoveredCount??0,recovery?.AlternateSearchText);
        }).ToArray();
        await wideRepository.SavePoloxiBranchOutcomesAsync(request.TenantId,request.UserId,executionId,outcomeRecords,cancellationToken);
        return new(executionId,hierarchy.HierarchyId,request.Query,hierarchy.ConceptCode,hierarchy.DisplayName,hierarchy.VersionNumber,reused,hierarchy.Confidence,hierarchy.Branches,ranked,explanation,explanationStatus,timer.ElapsedMilliseconds);
    }

    // 'POLOXI Engine' filter disabled: bypass LLM hierarchy generation, cache reuse, and execution persistence.
    // Runs the same deterministic authorized capability searches directly against every active capability.
    private async Task<PoloxiSearchResponse> SearchWithoutPoloxiEngineAsync(PoloxiSearchRequest request,IReadOnlyCollection<PoloxiCapabilityDto> capabilities,PoloxiConfiguration configuration,Stopwatch timer,CancellationToken cancellationToken)
    {
        var branches=capabilities.OrderBy(capability=>capability.SortOrder).Take(configuration.MaximumBranches).Select((capability,index)=>new PoloxiBranchRecord(Guid.NewGuid(),null,capability.CapabilityCode,capability.DisplayName,"Direct authorized search without POLOXI hierarchy.",capability.CapabilityCode,"VALID","POLOXI engine bypassed by request filter.",request.Query,capability.SupportsRecency,1m,index+1)).ToArray();
        var evidence=new List<PoloxiEvidenceDto>();
        foreach(var branch in branches)
        {
            var capability=capabilities.First(item=>item.CapabilityCode.Equals(branch.CapabilityCode,StringComparison.OrdinalIgnoreCase));
            evidence.AddRange(await repository.ExecutePoloxiBranchAsync(request,branch,capability,configuration.MaximumResults,cancellationToken));
        }
        var ranked=RankEvidence(evidence,request,configuration);
        timer.Stop();
        return new(Guid.Empty,Guid.Empty,request.Query,"DIRECT_SEARCH","Direct authorized search (POLOXI engine off)",0,false,1m,branches,ranked,null,"NOT_REQUESTED",timer.ElapsedMilliseconds);
    }

    private static PoloxiEvidenceDto[] RankEvidence(List<PoloxiEvidenceDto> evidence,PoloxiSearchRequest request,PoloxiConfiguration configuration)=>evidence.GroupBy(item=>$"{item.EntityTypeCode}:{item.EntityId:D}",StringComparer.OrdinalIgnoreCase).Select(group=>
    {
        var first=group.OrderByDescending(item=>item.RelevanceScore).First();
        var branchNames=group.SelectMany(item=>item.MatchedBranches).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var score=Math.Clamp(group.Max(item=>item.RelevanceScore)+Math.Min(.20m,(branchNames.Length-1)*.05m),0,1);
        return first with{RelevanceScore=score,MatchedBranches=branchNames};
    }).OrderByDescending(item=>item.RelevanceScore).ThenBy(item=>item.Title).Take(Math.Min(request.MaximumResults,configuration.MaximumResults)).Select((item,index)=>item with{RankNumber=index+1}).ToArray();

    private async Task<(PoloxiHierarchyProposal Proposal,string ProviderCode,string ModelCode)> GeneratePoloxiProposalAsync(PoloxiSearchRequest request,IReadOnlyCollection<PoloxiCapabilityDto> capabilities,PoloxiConfiguration configuration,CancellationToken cancellationToken)
    {
        var schema="""
{
  "type": "object",
  "$defs": {
    "branch": {
      "type": "object",
      "properties": {
        "branchCode": { "type": "string" },
        "displayName": { "type": "string" },
        "condition": { "type": "string" },
        "capabilityCode": { "type": ["string", "null"] },
        "searchText": { "type": ["string", "null"] },
        "orderByRecency": { "type": "boolean" },
        "confidence": { "type": "number" },
        "children": {
          "type": "array",
          "items": { "$ref": "#/$defs/branch" }
        }
      },
      "required": ["branchCode", "displayName", "condition", "capabilityCode", "searchText", "orderByRecency", "confidence", "children"],
      "additionalProperties": false
    }
  },
  "properties": {
    "conceptCode": { "type": "string" },
    "displayName": { "type": "string" },
    "confidence": { "type": "number" },
    "branches": {
      "type": "array",
      "maxItems": 12,
      "items": { "$ref": "#/$defs/branch" }
    }
  },
  "required": ["conceptCode", "displayName", "confidence", "branches"],
  "additionalProperties": false
}
""";
        var catalog=string.Join('\n',capabilities.Select(capability=>$"{capability.CapabilityCode}: {capability.Description}; approved terms: {string.Join(", ",capability.ApprovedTerms)}; recency: {capability.SupportsRecency}"));
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_POLOXI_HIERARCHY",await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.WidePoloxiHierarchy,cancellationToken),$"Question: {request.Query}\nMaximum branches: {configuration.MaximumBranches}\nApproved capability catalog:\n{catalog}",schema,request.CorrelationId,new("Intelligence",null,null,request.Query,"POLOXI_HIERARCHY",null,request.CorrelationId,"Intelligent Search Wide"),cancellationToken:cancellationToken);
        var proposal=JsonSerializer.Deserialize<PoloxiHierarchyProposal>(result.Content,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new ValidationException("The POLOXI hierarchy response was empty.");
        return (proposal,result.ProviderCode,result.ModelCode);
    }

    private static IReadOnlyCollection<PoloxiBranchRecord> ValidatePoloxiBranches(PoloxiHierarchyProposal proposal,IReadOnlyCollection<PoloxiCapabilityDto> capabilities,PoloxiConfiguration configuration)
    {
        var validated=new List<PoloxiBranchRecord>();
        void Visit(PoloxiProposedBranch branch,Guid? parentId)
        {
            if(validated.Count>=configuration.MaximumBranches)return;
            var id=Guid.NewGuid();
            var capability=capabilities.FirstOrDefault(item=>item.CapabilityCode.Equals(branch.CapabilityCode,StringComparison.OrdinalIgnoreCase));
            var confidence=Math.Clamp(branch.Confidence,0,1);
            var valid=capability is not null&&capability.ExecutionHandlerCode.Equals("AUTHORIZED_SEARCH_DOCUMENT",StringComparison.OrdinalIgnoreCase)&&confidence>=Math.Max(configuration.MinimumBranchConfidence,capability.MinimumConfidence)&&(!branch.OrderByRecency||capability.SupportsRecency);
            var searchText=valid?NormalizePoloxiSearchText(branch.SearchText,capability!):null;
            validated.Add(new(id,parentId,NormalizeCode(branch.BranchCode),branch.DisplayName.Trim(),branch.Condition.Trim(),capability?.CapabilityCode,valid?"VALID":"UNSUPPORTED",valid?"Grounded by an approved deterministic capability.":"No approved capability can deterministically ground this branch.",searchText,valid&&branch.OrderByRecency,confidence,validated.Count+1));
            foreach(var child in branch.Children??[])Visit(child,id);
        }
        foreach(var branch in proposal.Branches??[])Visit(branch,null);
        return validated;
    }

    private static string NormalizePoloxiSearchText(string? searchText,PoloxiCapabilityDto capability)
    {
        if(string.IsNullOrWhiteSpace(searchText))return string.Empty;
        var normalized=searchText.Trim();
        return capability.ApprovedTerms.FirstOrDefault(term=>normalized.Contains(term,StringComparison.OrdinalIgnoreCase))??string.Empty;
    }

    // Retrieval budget for the global-convergence round: at most this many zero-evidence branches get one
    // extra recovery attempt each, keeping the worst case bounded and the latency comparable to one level.
    // The recovery tactic itself (deterministic alternate-approved-term retry) lives in the registered
    // IAdaptiveRetriever implementation (StandardPoloxiRetriever).
    private const int MaxConvergenceRetrievals=4;

    private static string NormalizeCode(string value)=>new(value.Trim().ToUpperInvariant().Select(character=>char.IsLetterOrDigit(character)?character:'_').ToArray());
    private static string NormalizeQuery(string query)=>string.Join(' ',query.Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));

    // V3.5 Candidate-Seeking Retrieval Queries: the previous retrieval query was the FULL user query
    // (often a multi-sentence paragraph) concatenated with the branch name. Long conversational
    // queries return generic advice articles that name few concrete entities, so candidate discovery
    // starved (3 candidates for a nationwide search space) and few deciding factors were evidence-
    // backed (LIMITED EVIDENCE). Search engines reward short keyword queries that match listicle
    // titles ("best cities software developer buy home good schools"). Deterministic distillation:
    // keep content keywords of the query (stopwords/filler dropped, first-seen order, capped),
    // append the branch display name VERBATIM - ComputeEvidenceSupport matches snippets to branches
    // via snippet.Query.Contains(branch.DisplayName), so the branch name must survive intact.
    private static string BuildCandidateSeekingQuery(string query,string branchDisplayName)
    {
        var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var keywords=new List<string>();
        foreach(var raw in query.Split([' ','\t','\n','\r',',',';','/','(',')',':','.','?','!','"','\''],StringSplitOptions.RemoveEmptyEntries))
        {
            var token=raw.Trim('-','—','$');
            if(token.Length<3||RetrievalQueryStopwords.Contains(token))continue;
            if(!seen.Add(token))continue;
            keywords.Add(token);
            if(keywords.Count==8)break;
        }
        var distilled=keywords.Count==0?NormalizeQuery(query):string.Join(' ',keywords);
        return NormalizeQuery($"{distilled} {branchDisplayName}").ToLowerInvariant();
    }

    // V3.5: conversational filler that never helps a keyword search. Content words (city, housing,
    // crime, developer, numbers, place names) always survive.
    private static readonly HashSet<string> RetrievalQueryStopwords=new(StringComparer.OrdinalIgnoreCase)
    {
        "the","and","for","with","that","this","these","those","them","they","their","there","then",
        "what","which","when","where","who","whom","whose","why","how","would","could","should","shall",
        "will","can","may","might","must","have","has","had","having","are","was","were","been","being",
        "not","but","however","although","though","while","because","since","about","into","onto","upon",
        "from","over","under","between","among","during","before","after","above","below","some","any",
        "all","every","each","both","few","more","most","other","another","such","very","really","quite",
        "rather","also","too","than","like","want","wants","wanted","need","needs","needed","prefer",
        "prefers","preferred","consider","considering","assume","tell","give","please","currently",
        "planning","plan","looking","somewhere","overall","good","best","strong","reasonable","quality",
        "manageable","important","equally","factor","factors","trade-offs","tradeoffs","candidates",
        "rather","instead","us","our","we","you","your","she","he","its"
    };
    private static void Validate(object request){var context=new ValidationContext(request);Validator.ValidateObject(request,context,true);foreach(var property in request.GetType().GetProperties().Where(x=>x.PropertyType==typeof(Guid))){if((Guid)(property.GetValue(request)??Guid.Empty)==Guid.Empty)throw new ValidationException($"{property.Name} is required.");}}

    // ---------------------------------------------------------------------------------------------------
    // Dynamic progressive disambiguation pipeline:
    // Ambiguous Intent -> Dynamic LLM Hierarchy (problem-specific, level-by-level) -> Progressive
    // Disambiguation -> Enterprise Grounding -> Candidate Elimination -> Confidence -> Verified Answer.
    // Depth is unbounded; the LLM decides when narrowing is no longer relevant. DB-backed circuit breakers
    // (AbsoluteDepthCeiling, MaximumTotalLlmCalls) only guard against runaway cost.
    // ---------------------------------------------------------------------------------------------------
    public async Task<WideSearchResponse> SearchDynamicAsync(WideSearchRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);
        if(request.UserId==Guid.Empty)throw new UnauthorizedAccessException("An authenticated user is required for Wide search.");
        var timer=Stopwatch.StartNew();
        request=request with{Query=NormalizeQuery(request.Query),MaximumResults=Math.Clamp(request.MaximumResults,1,100),CorrelationId=string.IsNullOrWhiteSpace(request.CorrelationId)?$"wide-search:{Guid.NewGuid():N}":request.CorrelationId.Trim()};
        // V3.4 server-side continuation state: when the client presents a continuation token
        // (ParentWideExecutionId) the epistemic chain is derived from the persisted parent execution
        // row - original query text, clarification round, prior intent entropy, answer kind, and
        // clarification target. Client-carried fields are overridden, never trusted, so a tampered
        // or buggy client cannot corrupt round math, gain math, or kind routing. Tenant scoping is
        // enforced in the lookup; an unknown/foreign id degrades to the legacy client-carried path.
        WideContinuationState? continuationState=null;
        if(request.ParentWideExecutionId is{}parentExecutionId&&!string.IsNullOrWhiteSpace(request.ClarificationAnswer))
        {
            try{continuationState=await wideRepository.GetWideContinuationStateAsync(request.TenantId,parentExecutionId,cancellationToken);}
            catch(Exception)when(!cancellationToken.IsCancellationRequested){/* fail-soft to client-carried fields */}
            if(continuationState is not null)
                request=request with
                {
                    Query=NormalizeQuery(continuationState.QueryText),
                    ClarificationRound=continuationState.ClarificationRound+1,
                    PriorIntentEntropy=continuationState.IntentEntropy??request.PriorIntentEntropy,
                    OriginalAnswerKind=continuationState.AnswerKindCode??request.OriginalAnswerKind,
                    ClarificationTarget=continuationState.ClarificationTarget??request.ClarificationTarget
                };
        }
        // V2.8 clarification continuation: a follow-up answer is appended as an added constraint so the
        // pipeline reweights interpretations instead of restarting blind. The clarified query flows through
        // the query contract, hierarchy, grounding, and candidate competition like any hard constraint.
        // V2.8.5: the round counter is normalized here so a continuation execution is ALWAYS at least
        // round 1 even when the caller forgot to increment — round math must never trust the client alone.
        if(!string.IsNullOrWhiteSpace(request.ClarificationAnswer))
        {
            var clarificationConstraint=string.IsNullOrWhiteSpace(request.ClarificationTarget)?NormalizeQuery(request.ClarificationAnswer):$"{request.ClarificationTarget.Trim()}: {NormalizeQuery(request.ClarificationAnswer)}";
            request=request with{Query=$"{request.Query} ({clarificationConstraint})",ClarificationRound=Math.Max(request.ClarificationRound,1)};
        }
        // 'POLOXI Engine' filter disabled: pure LLM answer, no hierarchy, grounding, or elimination.
        if(!request.UsePoloxiEngine)return await SearchLlmOnlyAsync(request,timer,cancellationToken);
        var configuration=await wideRepository.GetWideConfigurationAsync(request.TenantId,cancellationToken);
        // Wide search is knowledge-only: it never grounds branches against AMS enterprise records.
        // An empty capability catalog forces every branch onto the INTERPRETIVE reasoning path.
        var capabilities=Array.Empty<PoloxiCapabilityDto>();
        var executionId=await wideRepository.StartWideExecutionAsync(new(request.TenantId,request.UserId,request.Query,request.CorrelationId){ParentWideExecutionId=continuationState?.WideExecutionId},cancellationToken);
        var llmCalls=0;
        var allBranches=new List<WideBranchRecord>();
        var evidence=new List<PoloxiEvidenceDto>();
        var branchEvidenceKeys=new Dictionary<Guid,HashSet<string>>();
        var depth=0;
        var terminationReason="LLM_COMPLETE";
        var aggregateConfidence=0m;
        var poloxiRequest=new PoloxiSearchRequest(request.TenantId,request.UserId,request.Query,request.MaximumResults,request.CorrelationId){GrantedPermissions=request.GrantedPermissions};
        // Raw first LLM result: fire the PLAIN query at the selected model in parallel — exactly what the
        // user would get from the model's own chat interface, before POLOXI touches anything. Comparison
        // only; fail-soft so a raw-call failure never blocks the POLOXI answer.
        var llmRawTask=GetRawLlmRankingAsync(request,cancellationToken);
        try
        {
            // Stage 0 (V2.1): Query Contract — separate hard constraints, output requirements, and the
            // ambiguous concepts that actually need disambiguation. Fail-soft: a null contract degrades
            // to the V2 behavior of branching the whole query.
            WideQueryContract? queryContract=null;
            if(configuration.EnableQueryContract)
            {
                queryContract=await ExtractQueryContractAsync(request,configuration,cancellationToken);
                if(queryContract is not null)llmCalls++;
            }
            // V3.2.3 AnswerKind carry-forward: a clarification continuation inherits the ORIGINAL run's
            // classification. The appended answer text pollutes Stage 0 re-classification (it reads like
            // a single-answer/enumeration fragment), but the task itself is unchanged — the user merely
            // filled a missing parameter. Inheriting keeps budgets and the candidate competition keyed
            // to the task that actually asked the question.
            if(!string.IsNullOrWhiteSpace(request.ClarificationAnswer)&&NormalizeAnswerKind(configuration,request.OriginalAnswerKind)is{Length:>0}inheritedKind)
                queryContract=queryContract is null?new(null,null,null,null,[],[],[]){AnswerKind=inheritedKind}:queryContract with{AnswerKind=inheritedKind};
            queryContract=RefineQueryContractForAmbiguity(configuration,queryContract,request.Query);
            // Clarification disabled globally: the Stage-0 answer-kind classifier can still route a broad
            // query to CLARIFICATION_REQUIRED (which skips candidate competition and returns no ranking).
            // When the gate is off we downgrade that classification so the full ranking pipeline runs and
            // POLOXI returns the best available answer instead of asking. Same config switch, both paths.
            if(!configuration.EnableClarificationGate)
                queryContract=DowngradeClarificationContract(configuration,queryContract);
            var contractClarificationQuestion=queryContract?.RequiresClarification==true?queryContract.ClarificationQuestion:null;
            var contractClarificationTarget=queryContract?.RequiresClarification==true?queryContract.ClarificationTarget:null;
            IReadOnlyCollection<string> contractClarificationOptions=queryContract?.RequiresClarification==true?queryContract.ClarificationOptions:[];
            IReadOnlyCollection<WideClarificationOptionDto> contractClarificationOptionItems=queryContract?.RequiresClarification==true
                ?queryContract.ClarificationOptions.Select((option,index)=>new WideClarificationOptionDto($"OPTION_{index+1}",option,option)).Append(new("OTHER","Something else",null)).ToArray()
                :[];
            // V3.2 Answer-Kind-Aware Workflow Routing: the Stage 0 AnswerKind classification tunes
            // (never forks) the pipeline budgets. CONTENT_ENUMERATION and SINGLE_ANSWER queries do not
            // benefit from deep entity discrimination, so their depth ceiling and information-round
            // caps shrink. Unknown/null kinds always keep the full budgets (fail-safe toward
            // thoroughness, never toward speed). The governing kind is persisted for audit.
            // V3.2.2/V3.2.3 clarification-continuation guard: when the original kind was carried forward
            // the budgets flow naturally from the inherited classification; when an old client omits
            // OriginalAnswerKind, fail safe toward thoroughness — full budgets, never shrunk by a
            // re-classification of the answer-polluted text.
            var isClarificationContinuation=!string.IsNullOrWhiteSpace(request.ClarificationAnswer);
            var(effectiveDepthCeiling,effectiveInformationRounds,answerKindRoutingApplied)=!isClarificationContinuation||NormalizeAnswerKind(configuration,request.OriginalAnswerKind)is{Length:>0}
                ?ResolveAnswerKindBudgets(configuration,queryContract)
                :(configuration.AbsoluteDepthCeiling,configuration.MaximumInformationRounds,false);
            if(queryContract?.AnswerKind is{Length:>0}answerKindCode)
                try{await wideRepository.UpdateWideExecutionAnswerKindAsync(request.TenantId,request.UserId,executionId,answerKindCode,cancellationToken);}catch{/* diagnostics only; never blocks the answer */}
            // Batch-inference overlap: the candidate-seed enumeration depends only on the query and the
            // FINALIZED query contract (both fixed at this point), so its LLM call is started here and
            // awaited where seeds are consumed (before the information rounds). Same guard, same prompt
            // inputs, same fail-soft semantics as the previous inline call — only the timing overlaps
            // Stage 1 intent, hierarchy narrowing, and grounding. Same accepted pattern as llmRawTask.
            var candidateSeedTask=configuration.EnableInformationValue?EnumerateCandidateSeedsAsync(request,queryContract,cancellationToken):null;

            // Stage 1: Ambiguous intent framing -> problem-specific Level-1 hierarchy (open, not catalog-limited).
            var intent=await ProposeIntentAsync(request,capabilities,configuration,queryContract,cancellationToken);
            llmCalls++;
            var currentLevel=MaterializeBranches(intent.Branches,executionId,request.TenantId,1,new Dictionary<string,WideBranchRecord>(),configuration);
            await wideRepository.SaveWideBranchesAsync(currentLevel,request.UserId,cancellationToken);
            allBranches.AddRange(currentLevel);

            // Stage 2: iterative loop — ground, eliminate, check confidence, then propose the next narrower level.
            while(currentLevel.Length>0)
            {
                depth++;
                var survivors=new List<WideBranchRecord>();
                // Bounded parallel grounding: branches within a level are independent (they only read the
                // previous level's evidence keys), so retrieval waits overlap. Results are merged
                // sequentially in branch order so evidence, keys, and audit output stay deterministic.
                var groundingResults=new (string StatusCode,IReadOnlyCollection<PoloxiEvidenceDto> Evidence,HashSet<string> Keys)[currentLevel.Length];
                using(var groundingGate=new SemaphoreSlim(Math.Max(1,configuration.GroundingConcurrency)))
                    await Task.WhenAll(currentLevel.Select(async(branch,index)=>
                    {
                        await groundingGate.WaitAsync(cancellationToken);
                        try{groundingResults[index]=await GroundBranchAsync(branch,poloxiRequest,capabilities,request.MaximumResults,branchEvidenceKeys,cancellationToken);}
                        finally{groundingGate.Release();}
                    }));
                var levelOutcomes=new List<WideBranchOutcomeUpdate>(currentLevel.Length);
                for(var index=0;index<currentLevel.Length;index++)
                {
                    var branch=currentLevel[index];
                    var grounded=groundingResults[index];
                    branchEvidenceKeys[branch.WideBranchId]=grounded.Keys;
                    evidence.AddRange(grounded.Evidence);
                    // V2.1 branch lifecycle: the LLM percentage is an Interpretation Prior controlling retrieval
                    // allocation, NOT truth. Low priors demote to SECONDARY/DORMANT instead of eliminating.
                    // PRUNED is reserved for hard-constraint violations, explicit contradictions, or
                    // structurally invalid branches — NEVER for merely lacking enterprise evidence, because
                    // Wide search is knowledge-oriented and many valid branches are interpretive-only.
                    var state=branch.Confidence>=configuration.SecondaryBranchThreshold?WideBranchStates.Active
                        :branch.Confidence>=configuration.DormantBranchThreshold?WideBranchStates.Secondary
                        :WideBranchStates.Dormant;
                    string? eliminationReason=null;
                    if(grounded.StatusCode=="GROUNDED"&&grounded.Evidence.Count==0&&branch.Confidence<configuration.TargetConfidence)
                    {
                        // Evidence-void grounding demotes to DORMANT: the branch stays in the answer path with
                        // a reduced footprint and can be reactivated by the V2.1 reweight if external evidence
                        // supports it. It is not pruned — absence of enterprise evidence is not a contradiction.
                        state=WideBranchStates.Dormant;
                        eliminationReason="Grounded capability search returned no enterprise evidence: dormant, reactivatable if evidence supports it.";
                    }
                    else if(state==WideBranchStates.Secondary)eliminationReason=$"Interpretation prior {branch.Confidence:P0} below {configuration.SecondaryBranchThreshold:P0}: reduced retrieval budget, not eliminated.";
                    else if(state==WideBranchStates.Dormant)eliminationReason=$"Interpretation prior {branch.Confidence:P0} below {configuration.DormantBranchThreshold:P0}: dormant, reactivatable if evidence supports it.";
                    var eliminated=state==WideBranchStates.Pruned;
                    var updated=branch with{GroundingStatusCode=grounded.StatusCode,EvidenceCount=grounded.Evidence.Count,IsEliminated=eliminated,EliminationReason=eliminationReason,BranchStateCode=state,InterpretationPrior=branch.Confidence};
                    levelOutcomes.Add(new(updated.WideBranchId,updated.GroundingStatusCode,updated.EvidenceCount,updated.IsEliminated,updated.EliminationReason));
                    allBranches[allBranches.FindIndex(item=>item.WideBranchId==branch.WideBranchId)]=updated;
                    // ACTIVE and SECONDARY branches keep narrowing; DORMANT branches stay in the answer path
                    // with a smaller footprint but do not spawn deeper levels; PRUNED branches stop entirely.
                    if(state is WideBranchStates.Active or WideBranchStates.Secondary)survivors.Add(updated);
                }
                // One round trip persists the whole level's grounding outcomes (same audit data as before).
                await wideRepository.UpdateWideBranchOutcomesAsync(request.TenantId,levelOutcomes,cancellationToken);
                if(survivors.Count==0)
                {
                    // V2.3 termination-code fix: NO_SURVIVORS is reserved for the case where NOTHING remains
                    // in the answer path (everything pruned). When only this level's branches went DORMANT,
                    // earlier levels and dormant branches still participate in the answer — that is a settled
                    // hierarchy, not an empty one.
                    terminationReason=allBranches.Any(branch=>!branch.IsEliminated)?"HIERARCHY_SETTLED":"NO_SURVIVORS";
                    break;
                }

                // Confidence: evidence-weighted aggregate over surviving paths.
                // Minimum depth of 2: confidence/LLM-complete exits are honored only after Level 2
                // has been processed, so single-level hierarchies cannot terminate early.
                aggregateConfidence=ComputeAggregateConfidence(survivors);
                if(depth>=2&&aggregateConfidence>=configuration.TargetConfidence){terminationReason="CONFIDENCE_REACHED";break;}

                // Natural LLM termination: no surviving branch wants further narrowing.
                if(depth>=2&&survivors.All(branch=>!branch.ContinueNarrowing)){terminationReason="LLM_COMPLETE";break;}

                // Circuit breakers (never functional limits; audited when reached).
                if(depth>=effectiveDepthCeiling){terminationReason=answerKindRoutingApplied&&effectiveDepthCeiling<configuration.AbsoluteDepthCeiling?"ANSWER_KIND_DEPTH_BUDGET":"DEPTH_CEILING_REACHED";break;}
                if(llmCalls+2>configuration.MaximumTotalLlmCalls){terminationReason="LLM_CALL_CEILING_REACHED";break;}

                // V2.3 evidence-priority expansion guard: when the hierarchy is already deep but most
                // surviving branches still lack ANY evidence, the weakness is evidence coverage, not
                // semantic understanding — stop generating deeper branches and let the V2.2 information
                // rounds investigate what was already discovered instead.
                if(depth>=configuration.EvidencePriorityMinimumDepth)
                {
                    var supportedShare=survivors.Count==0?0m:(decimal)survivors.Count(branch=>branch.EvidenceCount>0)/survivors.Count;
                    if(supportedShare<configuration.EvidencePriorityCoverageFloor){terminationReason="EVIDENCE_COVERAGE_PRIORITY";break;}
                }

                // Progressive narrowing: propose the next level from surviving branches AND their grounding outcomes.
                // When forcing the minimum depth of 2, narrow all survivors even if none opted to continue.
                var narrowingParents=survivors.Where(branch=>branch.ContinueNarrowing).ToArray();
                if(narrowingParents.Length==0)narrowingParents=survivors.ToArray();
                var proposal=await ProposeNextLevelAsync(request,narrowingParents,capabilities,configuration,depth+1,evidence,queryContract,cancellationToken);
                llmCalls++;
                var parentsByCode=survivors.ToDictionary(branch=>branch.BranchCode,StringComparer.OrdinalIgnoreCase);
                var nextLevel=MaterializeBranches(proposal.Branches,executionId,request.TenantId,depth+1,parentsByCode,configuration);
                // Degenerate-progress guard: stop when the LLM merely rephrases the current level.
                var currentCodes=currentLevel.Select(branch=>branch.BranchCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
                nextLevel=nextLevel.Where(branch=>!currentCodes.Contains(branch.BranchCode)).ToArray();
                if(nextLevel.Length==0){terminationReason="NO_PROGRESS";break;}
                await wideRepository.SaveWideBranchesAsync(nextLevel,request.UserId,cancellationToken);
                allBranches.AddRange(nextLevel);
                currentLevel=nextLevel;
            }

            // Rank deduplicated evidence across surviving grounded paths only — evidence collected for
            // branches that were later eliminated must not surface as authorized evidence.
            var survivingBranchIds=allBranches.Where(branch=>!branch.IsEliminated).Select(branch=>branch.WideBranchId).ToHashSet();
            var survivingEvidence=evidence.Where(item=>survivingBranchIds.Contains(item.HierarchyBranchId)).ToList();
            var ranked=RankEvidence(survivingEvidence,poloxiRequest,new(false,1,configuration.MinimumBranchConfidence,configuration.MaximumBranchesPerLevel*Math.Max(depth,1),request.MaximumResults));

            // Stage 3: verified answer composed from surviving paths + enterprise evidence.
            var survivorsFinal=allBranches.Where(branch=>!branch.IsEliminated).ToArray();
            if(aggregateConfidence==0m&&survivorsFinal.Length>0)aggregateConfidence=ComputeAggregateConfidence(survivorsFinal);
            // Live external grounding (fail-soft): retrieve fresh web snippets for interpretive paths so
            // time-sensitive figures come from current sources instead of stale model memory.
            // V3.4 evidence reuse: a verified continuation first inherits the parent execution's
            // persisted evidence pool - the original run already grounded the base query, so the
            // continuation only needs the clarification-driven delta. Reused and fresh snippets are
            // URL-deduplicated (fresh wins) so the pool never double-counts a source.
            var externalKnowledge=await GatherExternalKnowledgeAsync(request,executionId,survivorsFinal.Where(branch=>branch.GroundingStatusCode=="INTERPRETIVE").ToArray(),configuration.ExternalRetrievalConcurrency,cancellationToken);
            if(continuationState is not null)
            {
                try
                {
                    var inherited=await wideRepository.GetExecutionExternalKnowledgeAsync(request.TenantId,continuationState.WideExecutionId,cancellationToken);
                    if(inherited.Count>0)
                    {
                        var freshUrls=externalKnowledge.Select(snippet=>snippet.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        externalKnowledge=externalKnowledge.Concat(inherited.Where(snippet=>!freshUrls.Contains(snippet.Url))).ToArray();
                    }
                }
                catch(Exception)when(!cancellationToken.IsCancellationRequested){/* reuse is an optimization; never blocks the run */}
            }
            // V2.1 three-score model: Interpretation Prior (LLM), Evidence Support (deterministic, from
            // enterprise evidence and matched external snippets), POLOXI Confidence (weighted combination).
            survivorsFinal=survivorsFinal.Select(branch=>
            {
                var support=ComputeEvidenceSupport(branch,evidence,externalKnowledge,configuration);
                var poloxiConfidence=Math.Clamp(configuration.PriorWeight*branch.Confidence+configuration.EvidenceWeight*support,0,1);
                // V2.1 REWEIGHT: evidence revises the branch state — a DORMANT branch with strong evidence
                // support is reactivated, and a high-prior branch without support is demoted. PRUNED
                // (constraint violation / evidence-void) is terminal and never reactivated here.
                var state=branch.BranchStateCode==WideBranchStates.Pruned?WideBranchStates.Pruned
                    :poloxiConfidence>=configuration.SecondaryBranchThreshold?WideBranchStates.Active
                    :poloxiConfidence>=configuration.DormantBranchThreshold?WideBranchStates.Secondary
                    :WideBranchStates.Dormant;
                return branch with{InterpretationPrior=branch.Confidence,EvidenceSupport=support,PoloxiConfidence=poloxiConfidence,BranchStateCode=state};
            }).ToArray();
            foreach(var branch in survivorsFinal)
                allBranches[allBranches.FindIndex(item=>item.WideBranchId==branch.WideBranchId)]=branch;

            // ── V2.2 Information-Directed Exploration ─────────────────────────────────
            // "Don't explore everything. Explore what will teach you the most."
            // Deterministic Shannon entropy decides WHETHER more information is needed; a single
            // batched LLM call ESTIMATES which branches are most valuable to investigate
            // (EstimatedInformationValue); POLOXI deterministically adjusts, selects, retrieves in
            // parallel, reweights, and MEASURES ActualInformationGain = EntropyBefore - EntropyAfter.
            // Fail-soft: any estimator/entropy failure skips the round and continues V2.1 behavior.
            var totalActualInformationGain=0m;
            var informationRounds=new List<WideInformationRoundDto>();
            // V3.0 Evidence-Guided Adaptive Narrowing: per-round deterministic narrowing audit plus
            // cross-round memory (support at branch resolution for reopen detection; candidate states
            // for transition provenance). Fail-soft: any narrowing failure degrades to V2.x behavior.
            var narrowingIterations=new List<WideNarrowingIterationDto>();
            var branchSupportAtResolution=new Dictionary<Guid,decimal>();
            var previousCandidateStates=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            // V2.6 Candidate Stability: deterministic per-round ranking snapshots (no LLM). If added
            // evidence stops changing the ranking, that is a convergence signal independent of entropy.
            var roundRankings=new List<string[]>();
            var informationTargetCount=0;
            var informationRetrievalCount=0;
            var externalKnowledgeAll=externalKnowledge.ToList();
            // V2.3: candidate universe accumulated across rounds — the falsifiable candidate names the
            // estimator predicts. When the hierarchy is dimension-dominated, entropy is measured over
            // this candidate competition instead of complementary dimensions.
            var candidateUniverse=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // V2.4 Early Candidate Harvest: deterministically extract candidate names from the external
            // result sets already retrieved (proper-noun phrases recurring across independent snippets).
            // No LLM call — this gives the information rounds a real candidate universe up front so
            // Information Gain targets "which candidate wins" from round 1, not after competition.
            candidateUniverse.UnionWith(HarvestCandidateNames(externalKnowledgeAll));
            // V3.5 Candidate Enumeration Seeding: one cheap LLM call lists concrete candidates for the
            // query so the universe is never limited to the handful of names the initial snippets
            // happened to mention (nationwide search spaces were reaching competition with 3 names).
            // Seeds are UNTRUSTED (mini-tier model): each passes the deterministic validity filters
            // here and still has to earn evidence support at the existing admission gates - the gates
            // are never lowered. A short verification retrieval per seed batch gives real seeds the
            // chance to accumulate the host support the gates require. Fail-soft: enumeration or
            // verification failure degrades to the harvested universe.
            if(configuration.EnableInformationValue&&candidateSeedTask is not null)
            {
                var seeds=await candidateSeedTask;
                var queryTopicSeedTokens=BuildQueryTopicTokens(request.Query);
                // Candidate eligibility must be stable for the same proposed name and query. Retrieved
                // corpus capitalization may vary between runs, so it cannot authoritatively reject a
                // seed; every accepted seed still has to earn support at the evidence admission gates.
                var validSeeds=seeds.Where(seed=>IsValidCandidateForContract(seed,queryContract)&&!IsQueryTopicEcho(seed,queryTopicSeedTokens)&&!candidateUniverse.Contains(seed)).Take(20).ToArray();
                if(validSeeds.Length>0)
                {
                    candidateUniverse.UnionWith(validSeeds);
                    var verification=await GatherSeedVerificationKnowledgeAsync(request,executionId,validSeeds,cancellationToken);
                    if(verification.Count>0)
                    {
                        var knownUrls=externalKnowledgeAll.Select(snippet=>snippet.Url).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        externalKnowledgeAll.AddRange(verification.Where(snippet=>!knownUrls.Contains(snippet.Url)));
                        candidateUniverse.UnionWith(HarvestCandidateNames(externalKnowledgeAll));
                    }
                }
            }
            var initialEntropy=ComputeUncertainty(configuration,survivorsFinal,candidateUniverse,evidence,externalKnowledgeAll,queryContract);
            var finalEntropy=initialEntropy;
            if(configuration.EnableInformationValue&&survivorsFinal.Length>0)
            {
                var weakRounds=0;
                // V2.5 Marginal Information Value state: which branches were already investigated and
                // how effective the previous round actually was. A good player never asks the same
                // question twice unless the first answer demonstrably helped.
                var investigationCounts=new Dictionary<Guid,int>();
                    var priorRoundEffectiveness=1m;
                for(var round=1;round<=effectiveInformationRounds;round++)
                {
                    // V2.5: freeze the candidate population for this round. EntropyBefore and EntropyAfter
                    // MUST be measured over the IDENTICAL candidate set, otherwise Hmax=log2(N) shifts and
                    // ActualInformationGain compares incomparable distributions. Names discovered during
                    // this round join the NEXT round's basis instead.
                    var roundCandidateBasis=candidateUniverse.ToArray();
                    var entropyBefore=ComputeUncertainty(configuration,survivorsFinal,roundCandidateBasis,evidence,externalKnowledgeAll,queryContract);
                    finalEntropy=entropyBefore;
                    if(entropyBefore.NormalizedEntropy<configuration.InformationValueTriggerEntropy)break;
                    if(llmCalls+2>configuration.MaximumTotalLlmCalls)break;
                    try
                    {
                        // One batched estimate for all eligible (ACTIVE/SECONDARY) branches.
                        var eligible=survivorsFinal.Where(branch=>branch.BranchStateCode is WideBranchStates.Active or WideBranchStates.Secondary).ToArray();
                        if(eligible.Length<2)break;
                        // Phase 1 (VNext) contested-pair context: tell the estimator WHERE the unresolved
                        // bottleneck is (deterministic leader vs runner-up over the frozen candidate basis).
                        // Prompt-context only — scoring, selection, and narrowing are untouched.
                        var contestedPair=DescribeContestedPair(roundCandidateBasis,evidence,externalKnowledgeAll);
                        var proposal=await EstimateInformationValueAsync(request,eligible,entropyBefore,queryContract,contestedPair,cancellationToken);
                        llmCalls++;
                        if(proposal is null||proposal.Targets.Count==0)break;
                        var branchesByCode=eligible.ToDictionary(branch=>branch.BranchCode,StringComparer.OrdinalIgnoreCase);
                        var maxConfidence=Math.Max(eligible.Max(branch=>branch.PoloxiConfidence),.0001m);
                        // Candidate discrimination need is high when eligible branch scores are tightly packed.
                        var orderedConfidences=eligible.Select(branch=>branch.PoloxiConfidence).OrderByDescending(value=>value).ToArray();
                        var topMargin=orderedConfidences.Length>1?orderedConfidences[0]-orderedConfidences[1]:1m;
                        var candidateNeed=Math.Clamp(1m-topMargin*5m,0,1);
                        var roundId=Guid.NewGuid();
                        var targetRecords=new List<WideInformationTargetRecord>();
                        var targetDtos=new List<WideInformationTargetDto>();
                        var predictionRecords=new List<WideInformationPredictionRecord>();
                        var scored=new List<(WideBranchRecord Branch,WideInformationTargetProposal Target,decimal Raw,decimal Adjusted)>();
                        foreach(var target in proposal.Targets)
                        {
                            if(!branchesByCode.TryGetValue(target.BranchCode,out var branch))continue;
                            if(!ValidateCategories(target))continue;
                            // Deterministic conversion of categorical judgments; redundancy penalizes.
                            // Criterion weights are DB-calibrated (see migration 0164); defaults match the original constants.
                            var raw=Math.Clamp(
                                configuration.CriterionUncertaintyWeight*CategoryValue(configuration,target.Uncertainty)
                                +configuration.CriterionRankingImpactWeight*CategoryValue(configuration,target.RankingImpact)
                                +configuration.CriterionDiscriminationWeight*CategoryValue(configuration,target.CandidateDiscrimination)
                                +configuration.CriterionEvidenceAvailabilityWeight*CategoryValue(configuration,target.EvidenceAvailability)
                                +configuration.CriterionNoveltyWeight*CategoryValue(configuration,target.Novelty)
                                -configuration.CriterionRedundancyPenalty*CategoryValue(configuration,target.Redundancy),0,1);
                            // Adjust with facts POLOXI already knows: evidence gap, branch importance, candidate closeness.
                            var evidenceGap=Math.Clamp(1m-branch.EvidenceSupport,0,1);
                            var branchImportance=Math.Clamp(branch.PoloxiConfidence/maxConfidence,0,1);
                            var adjusted=Math.Clamp(
                                configuration.InformationValueLlmWeight*raw
                                +configuration.InformationValueEvidenceGapWeight*evidenceGap
                                +configuration.InformationValueBranchWeight*branchImportance
                                +configuration.InformationValueCandidateNeedWeight*candidateNeed,0,1);
                            // V2.5 Marginal Information Value: POLOXI already KNOWS whether this branch was
                            // investigated before and whether the prior round actually reduced uncertainty.
                            // Repeats are deterministically discounted — novelty halves per prior
                            // investigation, further scaled by measured prior-round effectiveness — so
                            // AdjustedIV = EstimatedIV × Novelty × PriorRoundEffectiveness on repeats.
                            var priorInvestigations=investigationCounts.GetValueOrDefault(branch.WideBranchId);
                            if(priorInvestigations>0)
                            {
                                var noveltyFactor=Math.Clamp(1m/(1m+priorInvestigations),0,1);
                                adjusted=Math.Clamp(adjusted*noveltyFactor*Math.Clamp(priorRoundEffectiveness,.10m,1m),0,1);
                            }
                            // V3.6.1 calibration guard: after a measured weak round (actual gain below the
                            // minimum) the LLM's info-value estimates are demonstrably uncalibrated for THIS
                            // execution, so fresh-branch targets are also discounted by the measured prior
                            // effectiveness (floored at .25 so a single unlucky round cannot zero the pipeline).
                            // Only measured math changes the discount — never LLM self-reports.
                            else if(weakRounds>0)
                                adjusted=Math.Clamp(adjusted*Math.Clamp(priorRoundEffectiveness,.25m,1m),0,1);
                            scored.Add((branch,target,raw,adjusted));
                        }
                        if(scored.Count==0)break;
                        var selected=scored.Where(item=>item.Adjusted>=configuration.MinimumInformationValue)
                            .OrderByDescending(item=>item.Adjusted)
                            .Take(configuration.MaximumInformationTargetsPerRound)
                            .ToArray();
                        // Persist the round and ALL evaluated targets (selected and unselected) for auditability.
                        await wideRepository.SaveInformationRoundAsync(new(roundId,executionId,request.TenantId,round,entropyBefore.Entropy,entropyBefore.NormalizedEntropy,DateTime.UtcNow){EntropyBasisCode=entropyBefore.EntropyBasisCode,MaxEntropyBefore=entropyBefore.MaximumEntropy,PopulationCountBefore=entropyBefore.EligibleBranchCount},request.UserId,cancellationToken);
                        // Deterministic pre-retrieval baseline for every predicted candidate: the mention-weighted
                        // evidence signal BEFORE this round's targeted retrieval. Makes ranking predictions verifiable.
                        var predictedCandidates=scored.SelectMany(item=>item.Target.PredictedRankingChanges.Select(prediction=>prediction.Candidate.Trim()))
                            .Where(name=>!string.IsNullOrWhiteSpace(name)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                        candidateUniverse.UnionWith(predictedCandidates);
                        var baselineSignals=ComputeCandidateSignals(predictedCandidates,evidence,externalKnowledgeAll);
                        var baselineRanks=RankSignals(baselineSignals);
                        var selectionRank=0;
                        foreach(var item in scored.OrderByDescending(entry=>entry.Adjusted))
                        {
                            var isSelected=selected.Contains(item);
                            var rank=isSelected?++selectionRank:(int?)null;
                            var targetId=Guid.NewGuid();
                            targetRecords.Add(new(targetId,roundId,item.Branch.WideBranchId,request.TenantId,item.Target.Uncertainty,item.Target.RankingImpact,item.Target.CandidateDiscrimination,item.Target.EvidenceAvailability,item.Target.Novelty,item.Target.Redundancy,item.Raw,item.Adjusted,isSelected,rank,Truncate(item.Target.EvidenceTarget,1000),Truncate(item.Target.Rationale,1000))
                            {
                                PredictedRankingImpactCount=item.Target.PredictedRankingChanges.Count,
                                PredictedUpCount=item.Target.PredictedRankingChanges.Count(prediction=>prediction.Direction.Equals("UP",StringComparison.OrdinalIgnoreCase)),
                                PredictedDownCount=item.Target.PredictedRankingChanges.Count(prediction=>prediction.Direction.Equals("DOWN",StringComparison.OrdinalIgnoreCase))
                            });
                            targetDtos.Add(new(item.Branch.DisplayName,item.Target.Uncertainty,item.Target.RankingImpact,item.Target.CandidateDiscrimination,item.Target.EvidenceAvailability,item.Target.Novelty,item.Target.Redundancy,item.Raw,item.Adjusted,isSelected,rank,item.Target.EvidenceTarget,item.Target.Rationale));
                            // Falsifiable per-candidate ranking predictions from the same batched call, stamped with
                            // the deterministic pre-retrieval baseline so POLOXI can verify them after the round.
                            foreach(var prediction in item.Target.PredictedRankingChanges)
                            {
                                var candidateKey=prediction.Candidate.Trim();
                                predictionRecords.Add(new(Guid.NewGuid(),targetId,request.TenantId,Truncate(candidateKey,300)!,prediction.Direction.ToUpperInvariant(),prediction.Magnitude.ToUpperInvariant())
                                {
                                    ScoreBefore=baselineSignals.GetValueOrDefault(candidateKey),
                                    RankBefore=baselineRanks.TryGetValue(candidateKey,out var rankBefore)?rankBefore:null
                                });
                            }
                        }
                        await wideRepository.SaveInformationTargetsAsync(targetRecords,request.UserId,cancellationToken);
                        await wideRepository.SaveInformationPredictionsAsync(predictionRecords,request.UserId,cancellationToken);
                        informationTargetCount+=targetRecords.Count;
                        if(selected.Length==0)
                        {
                            informationRounds.Add(new(round,entropyBefore.Entropy,entropyBefore.NormalizedEntropy,null,null,null,null,targetDtos){EntropyBasisCode=entropyBefore.EntropyBasisCode,MaxEntropyBefore=entropyBefore.MaximumEntropy,PopulationCountBefore=entropyBefore.EligibleBranchCount});
                            break; // NO_HIGH_VALUE_INVESTIGATION: nothing worth retrieving.
                        }
                        // Targeted parallel retrieval: evidence target text focuses the query per branch.
                        var retrievalBranches=selected.Select(item=>string.IsNullOrWhiteSpace(item.Target.EvidenceTarget)?item.Branch:item.Branch with{SearchText=Truncate(item.Target.EvidenceTarget,400)}).ToArray();
                        var newKnowledge=await GatherExternalKnowledgeAsync(request,executionId,retrievalBranches,configuration.ExternalRetrievalConcurrency,cancellationToken);
                        // V3.6.1: rounds frequently re-surface URLs already in the pool (cache-first
                        // retrieval); duplicates would double-count evidence signals and inflate the
                        // disclosed evidence total, so only genuinely new URLs join the pool.
                        var poolUrls=externalKnowledgeAll.Select(snippet=>snippet.Url).Where(url=>!string.IsNullOrWhiteSpace(url)).ToHashSet(StringComparer.OrdinalIgnoreCase);
                        var freshKnowledge=newKnowledge.Where(snippet=>string.IsNullOrWhiteSpace(snippet.Url)||poolUrls.Add(snippet.Url)).ToArray();
                        informationRetrievalCount+=freshKnowledge.Length;
                        externalKnowledgeAll.AddRange(freshKnowledge);
                        // V2.4: newly retrieved snippets may name candidates not yet in the universe.
                        // V3.0 discovery admission gate (Invariant 3): when adaptive narrowing is enabled,
                        // a newly discovered name joins the universe ONLY with sufficient distinct-host
                        // attestation, within the per-round admission budget. Rejections are disclosed.
                        var harvestedNames=HarvestCandidateNames(externalKnowledgeAll);
                        WideNarrowingPolicy.ExpansionResult? expansion=null;
                        if(configuration.EnableAdaptiveNarrowing)
                        {
                            var discoveredNames=harvestedNames.Where(name=>!candidateUniverse.Contains(name)).ToArray();
                            expansion=WideNarrowingPolicy.EvaluateExpansion(discoveredNames,externalKnowledgeAll,configuration);
                            candidateUniverse.UnionWith(expansion.AdmittedNames);
                        }
                        else candidateUniverse.UnionWith(harvestedNames);
                        // Normal POLOXI scoring/reweighting on the enriched evidence pool (never LLM-calculated).
                        survivorsFinal=survivorsFinal.Select(branch=>
                        {
                            var support=ComputeEvidenceSupport(branch,evidence,externalKnowledgeAll,configuration);
                            var poloxiConfidence=Math.Clamp(configuration.PriorWeight*branch.InterpretationPrior+configuration.EvidenceWeight*support,0,1);
                            var state=branch.BranchStateCode==WideBranchStates.Pruned?WideBranchStates.Pruned
                                :poloxiConfidence>=configuration.SecondaryBranchThreshold?WideBranchStates.Active
                                :poloxiConfidence>=configuration.DormantBranchThreshold?WideBranchStates.Secondary
                                :WideBranchStates.Dormant;
                            return branch with{EvidenceSupport=support,PoloxiConfidence=poloxiConfidence,BranchStateCode=state};
                        }).ToArray();
                        foreach(var branch in survivorsFinal)
                            allBranches[allBranches.FindIndex(item=>item.WideBranchId==branch.WideBranchId)]=branch;
                        // Measure ActualInformationGain mathematically on the SAME basis AND the SAME frozen
                        // candidate population the round started with; preserve negative deltas for
                        // diagnostics — evidence can legitimately INCREASE uncertainty (it revealed
                        // overconfidence). Fractional bits are preserved at 4-decimal precision.
                        var entropyAfter=ComputeUncertainty(configuration,survivorsFinal,roundCandidateBasis,evidence,externalKnowledgeAll,queryContract);
                        var rawDelta=entropyBefore.Entropy-entropyAfter.Entropy;
                        var actualGain=Math.Max(0,rawDelta);
                        totalActualInformationGain+=actualGain;
                        // V2.5: record which branches were actually investigated this round and how effective
                        // the round was, so the next round's marginal IV can discount unproductive repeats.
                        foreach(var item in selected)investigationCounts[item.Branch.WideBranchId]=investigationCounts.GetValueOrDefault(item.Branch.WideBranchId)+1;
                        priorRoundEffectiveness=entropyBefore.Entropy<=0?0m:Math.Clamp(actualGain/entropyBefore.Entropy*4m,0,1);
                        finalEntropy=entropyAfter;
                        await wideRepository.CompleteInformationRoundAsync(request.TenantId,request.UserId,roundId,entropyAfter.Entropy,entropyAfter.NormalizedEntropy,actualGain,rawDelta,selected.Length,entropyAfter.MaximumEntropy,entropyAfter.EligibleBranchCount,cancellationToken);
                        // V2.2 prediction verification: re-measure the SAME deterministic candidate signal on the
                        // enriched evidence pool and grade each LLM ranking prediction (DirectionCorrect /
                        // MagnitudeCorrect). Calibration data — the LLM predicted; POLOXI measured.
                        if(predictionRecords.Count>0)
                        {
                            var afterSignals=ComputeCandidateSignals(predictedCandidates,evidence,externalKnowledgeAll);
                            var afterRanks=RankSignals(afterSignals);
                            var outcomes=predictionRecords.Select(record=>
                            {
                                var before=record.ScoreBefore??0m;
                                var after=afterSignals.GetValueOrDefault(record.CandidateName);
                                var delta=after-before;
                                var relative=before<=0m?(delta>0m?1m:0m):Math.Abs(delta)/before;
                                var actualMagnitude=delta==0m?"NONE":relative<.15m?"LOW":relative<.5m?"MEDIUM":"HIGH";
                                var actualDirection=delta>0m?"UP":delta<0m?"DOWN":null;
                                var directionCorrect=actualDirection is null
                                    ?record.PredictedMagnitude.Equals("NONE",StringComparison.OrdinalIgnoreCase)
                                    :record.PredictedDirection.Equals(actualDirection,StringComparison.OrdinalIgnoreCase);
                                var magnitudeCorrect=record.PredictedMagnitude.Equals(actualMagnitude,StringComparison.OrdinalIgnoreCase);
                                return record with{ScoreAfter=after,RankAfter=afterRanks.TryGetValue(record.CandidateName,out var rankAfter)?rankAfter:null,ActualDirection=actualDirection,ActualMagnitude=actualMagnitude,DirectionCorrect=directionCorrect,MagnitudeCorrect=magnitudeCorrect};
                            }).ToArray();
                            await wideRepository.UpdateInformationPredictionOutcomesAsync(request.TenantId,outcomes,cancellationToken);
                        }
                        informationRounds.Add(new(round,entropyBefore.Entropy,entropyBefore.NormalizedEntropy,entropyAfter.Entropy,entropyAfter.NormalizedEntropy,actualGain,rawDelta,targetDtos){EntropyBasisCode=entropyBefore.EntropyBasisCode,MaxEntropyBefore=entropyBefore.MaximumEntropy,PopulationCountBefore=entropyBefore.EligibleBranchCount,MaxEntropyAfter=entropyAfter.MaximumEntropy,PopulationCountAfter=entropyAfter.EligibleBranchCount});
                        // V2.6: snapshot the deterministic candidate ordering on the enriched evidence pool.
                        if(roundCandidateBasis.Length>0)
                        {
                            var roundSignals=ComputeCandidateSignals(roundCandidateBasis,evidence,externalKnowledgeAll);
                            roundRankings.Add(roundSignals.OrderByDescending(entry=>entry.Value).Select(entry=>entry.Key).ToArray());
                        }
                        // ── V3.0 Evidence-Guided Adaptive Narrowing ──────────────────────────────────
                        // Deterministic, zero-LLM: resolve settled branches, reopen branches whose
                        // evidence materially changed, transition candidate states, and record the
                        // round's directional trend with full transition provenance.
                        if(configuration.EnableAdaptiveNarrowing)
                        {
                            try
                            {
                                var activeBranchesBefore=eligible.Length;
                                var adjustedByBranch=scored.GroupBy(item=>item.Branch.WideBranchId).ToDictionary(group=>group.Key,group=>group.Max(item=>item.Adjusted));
                                var branchResult=WideNarrowingPolicy.EvaluateBranches(survivorsFinal,adjustedByBranch,branchSupportAtResolution,configuration);
                                foreach(var branchId in branchResult.ResolvedBranchIds)
                                    branchSupportAtResolution[branchId]=survivorsFinal.First(branch=>branch.WideBranchId==branchId).EvidenceSupport;
                                foreach(var branchId in branchResult.ReopenedBranchIds)
                                    branchSupportAtResolution.Remove(branchId);
                                if(branchResult.ResolvedBranchIds.Count>0||branchResult.ReopenedBranchIds.Count>0)
                                {
                                    survivorsFinal=survivorsFinal.Select(branch=>
                                        branchResult.ResolvedBranchIds.Contains(branch.WideBranchId)?branch with{BranchStateCode=WideBranchStates.Resolved}
                                        :branchResult.ReopenedBranchIds.Contains(branch.WideBranchId)?branch with{BranchStateCode=WideBranchStates.Active}
                                        :branch).ToArray();
                                    foreach(var branch in survivorsFinal)
                                        allBranches[allBranches.FindIndex(item=>item.WideBranchId==branch.WideBranchId)]=branch;
                                }
                                var candidateSignals=ComputeCandidateSignals(roundCandidateBasis,evidence,externalKnowledgeAll);
                                var candidateResult=WideNarrowingPolicy.EvaluateCandidates(candidateSignals,externalKnowledgeAll,previousCandidateStates,configuration);
                                foreach(var(candidateName,candidateState)in candidateResult.CandidateStates)
                                    previousCandidateStates[candidateName]=candidateState;
                                var transitions=branchResult.Transitions.Concat(expansion?.Transitions??[]).Concat(candidateResult.Transitions).ToArray();
                                var activeBranchesAfter=survivorsFinal.Count(branch=>branch.BranchStateCode is WideBranchStates.Active or WideBranchStates.Secondary);
                                var admittedCount=expansion?.AdmittedNames.Count??0;
                                var notAdmittedCount=expansion?.RejectedNames.Count??0;
                                var trend=WideNarrowingPolicy.ComputeTrend(activeBranchesBefore,activeBranchesAfter,roundCandidateBasis.Length,candidateUniverse.Count,branchResult.ReopenedBranchIds.Count,admittedCount,entropyAfter.NormalizedEntropy,configuration.InformationValueTriggerEntropy,actualGain);
                                var iterationDto=new WideNarrowingIterationDto(round,trend,activeBranchesBefore,activeBranchesAfter,roundCandidateBasis.Length,candidateUniverse.Count,entropyBefore.NormalizedEntropy,entropyAfter.NormalizedEntropy,actualGain,transitions){AdmittedCandidateCount=admittedCount,DiscoveredNotAdmittedCount=notAdmittedCount,ResolvedBranchCount=branchResult.ResolvedBranchIds.Count,ReopenedBranchCount=branchResult.ReopenedBranchIds.Count};
                                await wideRepository.SaveNarrowingIterationAsync(new(Guid.NewGuid(),executionId,request.TenantId,round,trend,activeBranchesBefore,activeBranchesAfter,roundCandidateBasis.Length,candidateUniverse.Count,entropyBefore.NormalizedEntropy,entropyAfter.NormalizedEntropy,actualGain,branchResult.ResolvedBranchIds.Count,branchResult.ReopenedBranchIds.Count,admittedCount,notAdmittedCount,JsonSerializer.Serialize(transitions,JsonOptions)),request.UserId,cancellationToken);
                                narrowingIterations.Add(iterationDto);
                            }
                            catch(Exception exception) when(exception is not OperationCanceledException)
                            {
                                // Fail-soft (V3.0 invariant): a narrowing failure must never break the
                                // information round — degrade to V2.x behavior and continue.
                            }
                        }
                        // Stall detection: stop after consecutive weak rounds (INFORMATION_GAIN_STALLED).
                        weakRounds=actualGain<configuration.MinimumActualInformationGain?weakRounds+1:0;
                        if(weakRounds>=configuration.InformationNoProgressRounds)break;
                    }
                    catch(Exception exception) when(exception is AiProviderUnavailableException or TimeoutException or JsonException)
                    {
                        break; // Fail-soft: continue the stable V2.1 pipeline without information targeting.
                    }
                }
            }
            externalKnowledge=externalKnowledgeAll;
            // One round trip persists all final branch scores/states (same audit data as before).
            // Started here and awaited after answer composition so the SQL write overlaps the LLM call.
            var scorePersistTask=wideRepository.UpdateWideBranchScoresAsync(request.TenantId,survivorsFinal.Select(branch=>new WideBranchScoreUpdate(branch.WideBranchId,branch.BranchStateCode,branch.InterpretationPrior,branch.EvidenceSupport,branch.PoloxiConfidence)).ToArray(),cancellationToken);
            WideAnswerProposal answer;
            var answerStatus="COMPLETED";
            string? providerCodeUsed=null,modelCodeUsed=null;
            try
            {
                (answer,providerCodeUsed,modelCodeUsed)=await ComposeAnswerAsync(request,survivorsFinal,ranked,aggregateConfidence,externalKnowledge,queryContract,cancellationToken);
                llmCalls++;
            }
            catch(Exception exception) when(exception is AiProviderUnavailableException or TimeoutException)
            {
                answerStatus="UNAVAILABLE";
                answer=new(string.Empty,ranked.Length>0?"PARTIALLY_VERIFIED":"INTERPRETIVE",aggregateConfidence,[],ranked.Select(item=>item.RankNumber).ToArray());
            }
            await scorePersistTask;
            // Relevance validation: keep only evidence the answer LLM judged relevant to the question.
            // Keyword grounding can match superficially (for example a name token matching unrelated
            // records); such evidence must not surface or inflate confidence.
            var relevantNumbers=(answer.RelevantEvidenceNumbers??[]).ToHashSet();
            var relevantEvidence=ranked.Where(item=>relevantNumbers.Contains(item.RankNumber)).ToArray();
            if(answer.VerificationCode=="INTERPRETIVE"||relevantEvidence.Length==0)aggregateConfidence=Math.Min(aggregateConfidence,Math.Clamp(answer.Confidence,0,1));
            // V2.1 Candidate x Branch competition: extract candidates from the interpretive result sets,
            // enforce hard constraints (PRUNED with a reason, never silently dropped), and compute a
            // composite ranking weighted by branch POLOXI confidence. Fail-soft: empty on LLM failure.
            var interpretiveResults=MapInterpretiveResults(answer,survivorsFinal,externalKnowledge);
            IReadOnlyCollection<WideCandidateDto> candidates=[];
            // V3.1 answer-kind routing: the FIRST LLM reply (query contract) decides the task type.
            // CONTENT_ENUMERATION queries ask for pieces of content (exam questions, tips, examples),
            // not named entities — running the Candidate Competition would force topic vocabulary
            // ("Exam", "Questions") into candidate slots, producing rankings that contradict the
            // interpretive answer. Route such queries to the interpretive composition instead.
            // V3.2.3: the contract's AnswerKind is authoritative here — on a continuation it was
            // inherited from the original run (carry-forward), so a genuinely enumerative original
            // task stays interpretive while a ranking task keeps its Candidate Competition. Only a
            // continuation WITHOUT a carried-forward kind (old client) forces the competition, because
            // the re-classified kind is untrustworthy on answer-polluted text.
            var isContentEnumeration=SkipsCandidateCompetition(configuration,queryContract)
                &&(string.IsNullOrWhiteSpace(request.ClarificationAnswer)||NormalizeAnswerKind(configuration,request.OriginalAnswerKind)is{Length:>0});
            // V3.7 mid-run reclassification checkpoint: Stage 0 can misclassify a choose-one selection
            // as a non-competition kind, but the interpretive results are ground truth — when multiple
            // DISTINCT NAMED candidates emerged across branches, the task demonstrably IS an entity
            // competition and skipping it would deliver an unaudited pick. Deterministic, zero-LLM:
            // count distinct interpretive item names; two or more re-enable the competition. Genuine
            // content-enumeration output (question texts, tips) stays skipped because its "items" are
            // produced content under a kind whose lookup row disables competition AND that carried
            // no ranking concept; a rankingConcept on the contract is direct competition evidence.
            if(isContentEnumeration&&!string.IsNullOrWhiteSpace(queryContract?.RankingConcept))
            {
                var distinctNamedCandidates=interpretiveResults.SelectMany(result=>result.Items.Select(item=>item.Name.Trim()))
                    .Where(name=>name.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).Count();
                if(distinctNamedCandidates>=2)isContentEnumeration=false;
            }
            var rankingCompletionRequired=RequiresRankingCompletion(configuration,queryContract,isContentEnumeration,interpretiveResults);
            var effectiveContractCount=EffectiveRankingContractCount(configuration,queryContract,rankingCompletionRequired);
            var completion=await CompleteRankingAsync(request,executionId,queryContract,survivorsFinal,interpretiveResults,candidateUniverse,externalKnowledgeAll,configuration,llmCalls,rankingCompletionRequired,effectiveContractCount,cancellationToken);
            candidates=completion.Candidates;
            llmCalls=completion.LlmCalls;
            // V2.9.2 Output Contract Validation: the delivered ranking must mechanically satisfy the
            // query contract. Requested 10 cities → 10 valid candidates; a shortfall is a validation
            // failure, not a composition style choice. One recovery pass re-runs the competition with
            // relaxed candidate discovery (single-source evidence names admitted) to widen the pool;
            // any remaining shortfall is DISCLOSED via the answer contract, never silently accepted.
            WideOutputContractResultDto? outputContract=null;
            // V3.1: the output contract counts VERIFIABLE CANDIDATES; a content-enumeration "top 100"
            // refers to content items delivered in the interpretive answer, so candidate-count
            // enforcement (and its recovery pass) would be a category error.
            // V3.5.2 Implicit-plural floor: ranking queries without an explicit count previously skipped
            // contract enforcement entirely, so a starved pool was silently accepted. Such queries now get
            // a default expected count so the SAME recovery pass fires — gates are never lowered; recovery
            // only credits additional independent support. Explicit counts keep exact contract semantics.
            if(!isContentEnumeration&&queryContract?.RequestedCount>0)
            {
                var contractCount=queryContract.RequestedCount.Value;
                var deliveredCount=DeliveredCandidateCount(candidates);
                outputContract=new(contractCount,deliveredCount,deliveredCount>=contractCount){RecoveryAttempted=completion.RecoveryAttempted};
            }
            // V3.6 Fix A: once the Candidate Competition has produced evidence-weighted quality scores,
            // the reported final uncertainty must reflect the RESOLVED competition — the winner-
            // probability entropy of the quality distribution — not the saturated mention-signal
            // entropy (which stays ~100% whenever every well-known candidate has abundant evidence).
            // Only adopt the outcome entropy when it is an improvement; it must never claim MORE
            // uncertainty than the evidence-signal measurement already established.
            if(candidates.Count>1)
            {
                var outcomeEntropy=ComputeCompetitionOutcomeEntropy(candidates);
                if(outcomeEntropy.EligibleBranchCount>1&&outcomeEntropy.NormalizedEntropy<finalEntropy.NormalizedEntropy)
                    finalEntropy=outcomeEntropy;
            }
            // V2.1 evidence metrics: coverage = share of surviving branches supported by any evidence.
            var coveredBranches=survivorsFinal.Count(branch=>branch.EvidenceSupport>0);
            var evidenceCoverage=survivorsFinal.Length==0?0m:Math.Clamp((decimal)coveredBranches/survivorsFinal.Length,0,1);
            // V2.5 Decision Evidence Coverage: measured only over the branches that participated in the
            // final Candidate × Branch competition — the dimensions the ANSWER actually rests on.
            // NOTE (verified V2.8.6): a candidate BranchScore is NOT evidence coverage. Branch scores come
            // from the LLM competition and exist for every dimension; EvidenceSupport counts branches
            // backed by RETRIEVED external evidence. All five dimensions scored with 60% coverage means
            // two dimensions were scored from model knowledge without retrieval backing — intentionally
            // reported lower, never inflated to match the score matrix.
            var decisionBranchIds=candidates.SelectMany(candidate=>candidate.BranchScores.Select(score=>score.BranchDisplayName)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var decisionBranches=survivorsFinal.Where(branch=>decisionBranchIds.Contains(branch.DisplayName)).ToArray();
            var decisionEvidenceCoverage=decisionBranches.Length==0?evidenceCoverage:Math.Clamp((decimal)decisionBranches.Count(branch=>branch.EvidenceSupport>0)/decisionBranches.Length,0,1);
            // V2.6 Candidate Stability: append the FINAL competition ordering as the last snapshot, then
            // measure deterministically how much the ranking moved across snapshots. Stable rankings under
            // added evidence mean additional evidence is not changing the answer — a convergence signal.
            var finalOrdering=candidates.Where(candidate=>!candidate.IsConstraintViolation).OrderBy(candidate=>candidate.RankNumber).Select(candidate=>candidate.DisplayName).ToArray();
            if(finalOrdering.Length>0)roundRankings.Add(finalOrdering);
            var(winnerStability,topKStability)=ComputeRankingStability(roundRankings);
            // V2.6 Decision Confidence: confidence in the RANKING DECISION itself — winner quality,
            // winner-vs-runner-up separation, evidence confidence behind the winner, decision-dimension
            // coverage, and ranking stability. NOT dominated by hierarchy-wide coverage.
            var topCandidates=candidates.Where(candidate=>!candidate.IsConstraintViolation).OrderBy(candidate=>candidate.RankNumber).ToArray();
            decimal? decisionConfidence=null;
            if(topCandidates.Length>0)
            {
                var winner=topCandidates[0];
                var separation=topCandidates.Length<2?1m:Math.Clamp((winner.CompositeScore-topCandidates[1].CompositeScore)/Math.Max(winner.CompositeScore,.0001m),0,1);
                decisionConfidence=Math.Clamp(.35m*winner.QualityScore+.25m*winner.EvidenceConfidence+.15m*separation+.15m*(winnerStability??1m)+.10m*decisionEvidenceCoverage,0,1);
                // V2.6 calibration: when a Candidate Competition produced the answer, the reported final
                // confidence must reflect the quality of THAT decision, not hierarchy-wide coverage. This
                // fixes the "80% decision coverage / 20% confidence" incoherence.
                aggregateConfidence=decisionConfidence.Value;
            }
            // Phase 2a Challenge-the-Winner (WATCH MODE): when enabled and the leader-vs-runner-up
            // composite margin is thin, evidence coverage is weak, or the ranking failed to stabilize,
            // one adversarial LLM assessment argues AGAINST the leader and records its verdict for audit.
            // Default OFF. NEVER changes the winner, ranking, confidence, clarification behavior, or
            // answer — the outcome is diagnostic data only.
            // Batch-inference overlap: the verdict feeds nothing downstream except the response DTO and
            // its audit row, so the call is started here and awaited just before the response is built,
            // overlapping the deterministic clarification/answer-locking work instead of blocking it.
            WideChallengeOutcomeDto? challengeOutcome=null;
            Task<WideChallengeOutcomeDto?>? challengeTask=null;
            if(configuration.EnableChallengeRound&&topCandidates.Length>1)
            {
                var challengeMargin=Math.Clamp((topCandidates[0].CompositeScore-topCandidates[1].CompositeScore)/Math.Max(topCandidates[0].CompositeScore,.0001m),0,1);
                var lowEvidenceCoverage=decisionEvidenceCoverage<.50m;
                var unstableWinner=winnerStability is <=.05m;
                if(challengeMargin<configuration.ChallengeMarginThreshold||lowEvidenceCoverage||unstableWinner)
                    challengeTask=ChallengeWinnerAndPersistAsync(request,executionId,topCandidates[0],topCandidates[1],challengeMargin,externalKnowledge,cancellationToken);
            }
            // V2.8 Clarification Gate, upgraded to V2.8.4 Clarification Intelligence.
            // Intent Gap classifier: unresolved uncertainty is an EVIDENCE gap (POLOXI doesn't know the
            // world — retrieve) or an INTENT gap (POLOXI knows the possibilities but not which one the
            // user means — ask). The gate fires only on an intent gap: multiple distinct candidates,
            // low decision confidence, unstable winner, thin margin, AND retrieval stalled (no more
            // information rounds available or actual information gain at/below the minimum). More
            // retrieval cannot close an intent gap — only the user can.
            string? clarificationQuestion=null;string? clarificationTarget=null;IReadOnlyCollection<string> clarificationOptions=[];
            IReadOnlyCollection<WideClarificationOptionDto> clarificationOptionItems=[];
            decimal? intentEntropy=null;decimal? bestClarificationValueOut=null;
            // V2.8.4 Intent Entropy: normalized Shannon entropy over the top candidates' composite
            // scores — the measurable "which one does the USER mean?" uncertainty. Computed on every
            // run (not only when the gate fires) so Clarification Gain = before − after is diffable
            // across the two executions of a clarification round.
            var intentCandidates=topCandidates.Take(4).Where(candidate=>candidate.CompositeScore>0).ToArray();
            if(intentCandidates.Length>1)
            {
                var total=intentCandidates.Sum(candidate=>candidate.CompositeScore);
                var entropySum=0d;
                foreach(var candidate in intentCandidates)
                {
                    var probability=(double)(candidate.CompositeScore/total);
                    entropySum-=probability*Math.Log2(probability);
                }
                intentEntropy=Math.Clamp((decimal)(entropySum/Math.Log2(intentCandidates.Length)),0,1);
            }
            else if(intentCandidates.Length==1)intentEntropy=0m;
            // V2.8.5 Clarification Gain: prior intent entropy (carried from the execution that ASKED)
            // minus this execution's intent entropy — the MEASURED uncertainty reduction the user's
            // answer produced. Persisted for calibration: which clarification targets actually work.
            decimal? clarificationGain=null;
            if(!string.IsNullOrWhiteSpace(request.ClarificationAnswer)&&request.PriorIntentEntropy is not null&&intentEntropy is not null)
                clarificationGain=Math.Clamp(request.PriorIntentEntropy.Value-intentEntropy.Value,-1,1);
            // V2.8.5 multi-round stop rules: POLOXI may ask again after an answer, but ONLY when (a) the
            // round cap is not exhausted and (b) the PREVIOUS answer measurably reduced intent entropy
            // by at least the configured floor — clarification must converge, never loop. When either
            // rule fails, POLOXI answers with the best available candidate instead of asking again.
            var clarificationRoundBudgetAvailable=request.ClarificationRound<configuration.MaximumClarificationRounds;
            var previousAnswerHelped=string.IsNullOrWhiteSpace(request.ClarificationAnswer)
                ||(clarificationGain is not null&&clarificationGain.Value>=configuration.MinimumClarificationGain);
            // V2.8.6 Uncertainty Router: low confidence + instability is NOT automatically an intent gap.
            //   EVIDENCE GAP    → retrieve (handled upstream by Information Value rounds).
            //   INTENT GAP      → ask (candidates are semantically DISTINCT interpretations — "which
            //                     Mercury?" — the identity discriminator is missing from the query).
            //   DECISION UNCERTAINTY → rank + disclose (candidates are legitimately close on SHARED,
            //                     user-defined criteria — "best city for a family" — asking is wrong).
            // Deterministic, zero-LLM signal: the semantic type of the branches the decision rests on.
            // ALTERNATIVE branches are mutually exclusive interpretations (identity competition → intent
            // gap); DIMENSION branches are jointly valid evaluation criteria (close scores → decision
            // uncertainty). The gate may only fire when the decision predominantly rests on ALTERNATIVEs.
            var decisionBranchTypes=topCandidates.Take(4)
                .SelectMany(candidate=>candidate.BranchScores.Select(score=>score.BranchDisplayName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name=>survivorsFinal.FirstOrDefault(branch=>string.Equals(branch.DisplayName,name,StringComparison.OrdinalIgnoreCase))?.SemanticTypeCode)
                .Where(type=>type is not null)
                .ToArray();
            var alternativeShare=decisionBranchTypes.Length==0?0m:Math.Clamp((decimal)decisionBranchTypes.Count(type=>string.Equals(type,WideBranchSemanticTypes.Alternative,StringComparison.OrdinalIgnoreCase))/decisionBranchTypes.Length,0,1);
            var isIntentGap=alternativeShare>=.5m;
            if(configuration.EnableClarificationGate&&isIntentGap&&clarificationRoundBudgetAvailable&&previousAnswerHelped&&topCandidates.Length>1&&decisionConfidence is not null)
            {
                var winner=topCandidates[0];
                var margin=Math.Clamp((winner.CompositeScore-topCandidates[1].CompositeScore)/Math.Max(winner.CompositeScore,.0001m),0,1);
                // V2.8.4 retrieval-stalled test: asking is justified only when retrieving more cannot
                // help — information rounds exhausted, or the last measured gain fell to the floor.
                var retrievalStalled=informationRounds.Count>=effectiveInformationRounds
                    ||totalActualInformationGain<=configuration.MinimumActualInformationGain;
                // V2.8.1 Clarification Value (CV): what to ASK the user is NOT what to RETRIEVE (IV).
                // Retrieval IV drops to ~0 once POLOXI has learned a dimension — which is precisely when
                // that dimension becomes the BEST question: POLOXI knows Candidate A is aerospace and
                // Candidate B is fintech; only the user knows which one they mean. Deterministic CV per
                // dimension = CandidateSeparation (max−min branch evidence score across the top
                // candidates — discrimination power) × Answerability (mean score — how well the
                // candidates are characterized on that dimension, hence how recognizable the options
                // are to the user). Falls back to the highest unresolved retrieval-IV target only when
                // no dimension separates the candidates. Zero-LLM.
                var comparisonCandidates=topCandidates.Take(4).ToArray();
                string? clarificationValueTarget=null;var bestClarificationValue=0m;
                var dimensionNames=comparisonCandidates.SelectMany(candidate=>candidate.BranchScores.Select(score=>score.BranchDisplayName)).Distinct(StringComparer.OrdinalIgnoreCase);
                // V2.8.5 full 5-factor CV — all deterministic, zero-LLM:
                //   Separation:       max−min evidence score across candidates (discrimination power).
                //   Answerability:    mean evidence score (how well candidates are characterized).
                //   UserAnswerability: share of candidates scored on the dimension — the user can only
                //                      recognize an option that POLOXI could describe for every candidate.
                //   IntentRelevance:  dimension overlaps the query contract's ambiguous concepts (the
                //                      user's OWN words) → full weight; otherwise a discounted baseline.
                //   AnswerSimplicity: fewer competing options on the dimension → simpler question.
                var ambiguousConcepts=queryContract?.AmbiguousConcepts??[];
                foreach(var dimensionName in dimensionNames)
                {
                    var scores=comparisonCandidates
                        .SelectMany(candidate=>candidate.BranchScores.Where(score=>string.Equals(score.BranchDisplayName,dimensionName,StringComparison.OrdinalIgnoreCase)).Select(score=>score.EvidenceScore))
                        .ToArray();
                    if(scores.Length<2)continue;
                    var separation=scores.Max()-scores.Min();
                    var answerability=scores.Average();
                    var userAnswerability=Math.Clamp((decimal)scores.Length/comparisonCandidates.Length,0,1);
                    var intentRelevance=ambiguousConcepts.Any(concept=>dimensionName.Contains(concept,StringComparison.OrdinalIgnoreCase)||concept.Contains(dimensionName,StringComparison.OrdinalIgnoreCase))?1m:.70m;
                    var answerSimplicity=Math.Clamp(1m-.10m*(scores.Length-2),.50m,1m);
                    var clarificationValue=separation*answerability*userAnswerability*intentRelevance*answerSimplicity;
                    if(clarificationValue>bestClarificationValue){bestClarificationValue=clarificationValue;clarificationValueTarget=dimensionName;}
                }
                var unresolvedTarget=informationRounds.SelectMany(round=>round.Targets)
                    .Where(target=>!string.IsNullOrWhiteSpace(target.BranchDisplayName))
                    .OrderByDescending(target=>!target.WasSelected).ThenByDescending(target=>target.AdjustedInformationValue)
                    .FirstOrDefault();
                var selectedTarget=clarificationValueTarget??unresolvedTarget?.BranchDisplayName;
                if(decisionConfidence.Value<configuration.ClarificationConfidenceThreshold
                    &&(winnerStability??1m)<configuration.ClarificationWinnerStabilityThreshold
                    &&margin<configuration.ClarificationMarginThreshold
                    &&retrievalStalled
                    &&selectedTarget is not null)
                {
                    clarificationTarget=selectedTarget;
                    bestClarificationValueOut=bestClarificationValue;
                    // V2.8.4 recognition over recall: the user searching a bare name often does not
                    // recognize legal names — options lead with the evidence-backed DESCRIPTION and
                    // keep the name as attribution, plus an OTHER escape hatch. POLOXI already did the
                    // research; the user only needs to recognize, not recall. Deterministic, zero-LLM.
                    // V3.2.1: Label (shown) and Value (submitted) are separated so the continuation
                    // constraint stays a concise candidate name instead of a full descriptive sentence,
                    // keeping Stage 0 contract extraction clean on the re-run.
                    var optionItems=comparisonCandidates.Where(candidate=>!candidate.IsConstraintViolation)
                        .Select((candidate,index)=>new WideClarificationOptionDto($"OPTION_{index+1}",
                            string.IsNullOrWhiteSpace(candidate.Detail)?candidate.DisplayName:$"{TrimDescription(candidate.Detail)} ({candidate.DisplayName})",
                            candidate.DisplayName))
                        .ToList();
                    // OTHER submits a real exclusion constraint (not its display label), so the re-run
                    // widens away from the rejected candidates instead of appending a nonsense filter.
                    optionItems.Add(new("OTHER","Something else — none of these match","none of the previously suggested candidates; consider other possibilities"));
                    clarificationOptionItems=optionItems;
                    clarificationOptions=optionItems.Select(option=>option.Label).ToArray();
                    // V3.2.1 connected question: restate the user's own query and translate the internal
                    // dimension name into plain terms so the follow-up visibly continues the same search.
                    clarificationQuestion=$"Your search \u201C{request.Query}\u201D matches multiple plausible answers, and the available evidence cannot determine which one you mean \u2014 they differ most on {clarificationTarget}. Which sounds like the one you're looking for?";
                    answerStatus="USER_CLARIFICATION_REQUIRED";
                    terminationReason="USER_CLARIFICATION_REQUIRED";
                }
            }
            if(queryContract?.RequiresClarification==true&&answerStatus!="USER_CLARIFICATION_REQUIRED")
            {
                clarificationQuestion=contractClarificationQuestion;
                clarificationTarget=contractClarificationTarget;
                clarificationOptions=contractClarificationOptions;
                clarificationOptionItems=contractClarificationOptionItems;
            }
            await wideRepository.UpdateWideExecutionContractAsync(request.TenantId,request.UserId,executionId,queryContract is null?null:JsonSerializer.Serialize(queryContract,JsonOptions),evidenceCoverage,externalKnowledge.Count,relevantEvidence.Length,candidates.Count,cancellationToken);
            // V2.9 Answer Composer: derive the presentation contract deterministically from the final
            // Candidate × Branch outcome — zero-LLM, computed AFTER the gate so response mode reflects it.
            var answerContext=ComposeAnswerContext(answerStatus,topCandidates,queryContract,decisionConfidence,winnerStability,decisionEvidenceCoverage,isIntentGap,answer.CandidateInsights,outputContract,allBranches);
            // V2.2: persist execution-level entropy summary and information-round counters (fail-soft).
            try{await wideRepository.UpdateWideExecutionEntropyAsync(request.TenantId,request.UserId,new(executionId,initialEntropy.Entropy,finalEntropy.Entropy,initialEntropy.NormalizedEntropy,finalEntropy.NormalizedEntropy,totalActualInformationGain,informationRounds.Count,informationTargetCount,informationRetrievalCount){EntropyBasisCode=finalEntropy.EntropyBasisCode,DecisionConfidence=decisionConfidence,ClarificationTarget=clarificationTarget,ClarificationQuestion=clarificationQuestion,IntentEntropy=intentEntropy,PriorIntentEntropy=request.PriorIntentEntropy,ClarificationGain=clarificationGain,ClarificationRound=request.ClarificationRound},cancellationToken);}catch{/* diagnostics only; never blocks the answer */}
            var ambiguityGroups=BuildAmbiguityGroups(survivorsFinal,interpretiveResults,candidates,relevantEvidence,externalKnowledge,queryContract);
            // V3.16 POLOXI Full Answer Composer: POLOXI owns the final structure. Grouped ambiguity
            // answers outrank winner-bound prose; ranking locks apply only when ranking is the right task.
            var finalAnswerText=BuildPoloxiFullAnswer(answer.Answer,queryContract,ambiguityGroups,topCandidates,finalEntropy,decisionConfidence,winnerStability,topKStability,decisionEvidenceCoverage,answerStatus,allBranches);
            // V3.17 Deliverable Synthesis: for compute/adjudicate (RESOLUTION) answers and no-candidate
            // fallbacks, assemble a deterministic structured deliverable (determinacy verdict, best-supported
            // reason, blocking inputs, citations) from state already computed by the pipeline - zero extra
            // LLM calls. Fail-soft: any failure leaves the deliverable null and the prose answer unchanged.
            WideResolutionDeliverableDto? resolutionDeliverable=null;
            if(configuration.EnableDeliverableSynthesis&&answerStatus!="USER_CLARIFICATION_REQUIRED"&&ShouldSynthesizeDeliverable(configuration,queryContract,ambiguityGroups,topCandidates))
            {
                try
                {
                    resolutionDeliverable=BuildResolutionDeliverable(request,configuration,queryContract,ambiguityGroups,relevantEvidence,externalKnowledge,decisionConfidence,decisionEvidenceCoverage,finalEntropy);
                    if(resolutionDeliverable is not null)finalAnswerText=BuildResolutionFullAnswer(resolutionDeliverable,finalAnswerText);
                }
                catch{/* synthesis is advisory; never blocks the answer */}
            }
            // Join the overlapped challenge verdict (fail-soft: null on provider failure) so the
            // response DTO and audit row are identical to the previous sequential behavior.
            if(challengeTask is not null)challengeOutcome=await challengeTask;
            // POLOXI ABV (advisory): only on responsibly-answered runs, never on the clarification gate.
            // The InterpretationComposite is projected from THIS execution's already-computed state
            // (query contract + candidate branch scores) — no second ambiguity pass, no duplicate LLM
            // cost. ABV issues exactly one taxonomy-bounded intent call, so it is folded into llmCalls
            // and the execution duration below. Fail-soft: any failure leaves AbvAction null.
            WideAbvActionDto? abvAction=null;
            {
                // ABV runs on every responsibly-produced run, including clarification-required runs:
                // the top-ranked meaning is treated as the provisional decision so an actual Action
                // Business Plan is always surfaced for review. Fail-soft: any failure leaves it null.
                var abvComposite=BuildAbvComposite(request,queryContract,candidates,ambiguityGroups,decisionConfidence);
                if(abvComposite is not null)
                {
                    llmCalls++;
                    abvAction=await ResolveAbvActionAsync(request,abvComposite,cancellationToken);
                }
            }
            timer.Stop();
            await wideRepository.CompleteWideExecutionAsync(request.TenantId,request.UserId,executionId,answerStatus,terminationReason,depth,llmCalls,aggregateConfidence,answer.VerificationCode,finalAnswerText,timer.ElapsedMilliseconds,cancellationToken);
            var response=new WideSearchResponse(executionId,request.Query,answerStatus,terminationReason,depth,llmCalls,aggregateConfidence,answer.VerificationCode,finalAnswerText,allBranches.Select(ToDto).ToArray(),relevantEvidence,answer.SuggestedActions.Select(action=>new WideActionSuggestionDto(action.DisplayName,action.NavigationRoute,action.Rationale)).ToArray(),timer.ElapsedMilliseconds){ExternalReferences=MapExternalReferences(answer),InterpretiveResults=interpretiveResults,ExternalKnowledge=externalKnowledge,QueryContract=queryContract,
            Candidates=candidates,AmbiguityGroups=ambiguityGroups,EvidenceCoverage=evidenceCoverage,DecisionEvidenceCoverage=decisionEvidenceCoverage,ExternalEvidenceCount=externalKnowledge.Count,EnterpriseEvidenceCount=relevantEvidence.Length,
            InitialEntropy=initialEntropy.Entropy,FinalEntropy=finalEntropy.Entropy,InitialNormalizedEntropy=initialEntropy.NormalizedEntropy,FinalNormalizedEntropy=finalEntropy.NormalizedEntropy,TotalActualInformationGain=totalActualInformationGain,EntropyBasisCode=finalEntropy.EntropyBasisCode,InformationRounds=informationRounds,
            WinnerStability=winnerStability,TopKStability=topKStability,DecisionConfidence=decisionConfidence,ChallengeOutcome=challengeOutcome,
            ClarificationQuestion=clarificationQuestion,ClarificationTarget=clarificationTarget,ClarificationOptions=clarificationOptions,
            ClarificationOptionItems=clarificationOptionItems,IntentEntropy=intentEntropy,BestClarificationValue=bestClarificationValueOut,
            ClarificationGain=clarificationGain,ClarificationRound=request.ClarificationRound,AnswerContext=answerContext,
            NarrowingIterations=narrowingIterations,FinalNarrowingTrend=narrowingIterations.Count>0?narrowingIterations[^1].TrendCode:null,
            AnswerKindCode=queryContract?.AnswerKind,AnswerKindRoutingApplied=answerKindRoutingApplied,ProviderCodeUsed=providerCodeUsed,ModelCodeUsed=modelCodeUsed,LlmRawItems=await llmRawTask,AbvAction=abvAction,ResolutionDeliverable=resolutionDeliverable};
            return response;
        }
        catch
        {
            timer.Stop();
            await wideRepository.CompleteWideExecutionAsync(request.TenantId,request.UserId,executionId,"FAILED",terminationReason,depth,llmCalls,aggregateConfidence,"NONE",null,timer.ElapsedMilliseconds,cancellationToken);
            throw;
        }
    }

    // POLOXI ABV (Actionable Business Value) advisory stage. Feeds a CONVERGED composite — projected
    // from this execution's existing state, never a second ambiguity pass — into the ABV engine.
    // Fail-soft: any failure (rejected intent, provider error) returns null so the delivered answer is
    // never affected. The LLM proposes intent only; impact/urgency/owner/action resolve
    // deterministically from the database-backed Domain Pack with provenance.
    private async Task<WideAbvActionDto?> ResolveAbvActionAsync(WideSearchRequest request,InterpretationComposite composite,CancellationToken cancellationToken)
    {
        try
        {
            var correlationId=string.IsNullOrWhiteSpace(request.CorrelationId)?$"abv:{Guid.NewGuid():N}":request.CorrelationId;
            var outcome=await abvEngine.ResolveAsync(new(request.TenantId,request.UserId,null,composite,correlationId){ModelCode=ModelOverride(request)},cancellationToken);
            return MapAbv(outcome);
        }
        catch(Exception)when(!cancellationToken.IsCancellationRequested)
        {
            // Advisory only: never surface ABV failures to the user-facing answer. Cancellation still propagates.
            return null;
        }
    }

    // Projects an InterpretationComposite from THIS wide execution's already-computed reasoning state
    // instead of re-running the ambiguity engine (which would duplicate the entire discovery+validation
    // LLM pass). Decision dimensions come from the deterministic Candidate × Branch competition scores;
    // hard constraints and the objective come from the extracted query contract. Convergence mirrors the
    // pipeline's own confidence: a decisive, evidence-grounded ranking is a converged interpretation.
    private static InterpretationComposite? BuildAbvComposite(WideSearchRequest request,WideQueryContract? queryContract,IReadOnlyCollection<WideCandidateDto> candidates,IReadOnlyCollection<WideAmbiguityGroupDto> ambiguityGroups,decimal? decisionConfidence)
    {
        var ranked=candidates.Where(c=>!c.IsConstraintViolation).OrderBy(c=>c.RankNumber).ToArray();
        // Decision dimensions: aggregate each branch's evidence score across the surviving candidates,
        // normalized to 0..1 so ABV impact-tier derivation reads a comparable decision weight.
        var branchWeights=ranked
            .SelectMany(c=>c.BranchScores)
            .GroupBy(b=>b.BranchDisplayName,StringComparer.OrdinalIgnoreCase)
            .Select(g=>(Name:g.Key,Score:g.Average(b=>b.EvidenceScore)))
            .Where(x=>!string.IsNullOrWhiteSpace(x.Name))
            .ToArray();
        var maxScore=branchWeights.Length==0?0m:branchWeights.Max(x=>x.Score);
        var dimensions=branchWeights
            .Select((x,index)=>new ResolvedDimension($"dim-{index}",x.Name,SemanticRole.DecisionCriterion,PreferenceDirection.HigherIsBetter,x.Name,null,maxScore<=0m?0m:Math.Clamp(x.Score/maxScore,0m,1m)))
            .ToArray();
        // Ambiguity-first fallback: on a grouped/clarification run there is no single ranked winner, so
        // the winner-ranking candidate set is empty. Project the decision dimensions from the ambiguity
        // GROUPS instead (each possible meaning is a decision criterion weighted by its POLOXI confidence)
        // so ABV still surfaces an actual Action Business Plan for the leading interpretation.
        if(dimensions.Length==0)
        {
            var groups=ambiguityGroups
                .Where(g=>!string.IsNullOrWhiteSpace(g.DisplayName))
                .OrderByDescending(g=>g.Confidence)
                .ToArray();
            if(groups.Length==0)return null;
            var maxConfidence=groups.Max(g=>g.Confidence);
            dimensions=groups
                .Select((g,index)=>new ResolvedDimension($"dim-{index}",g.DisplayName,SemanticRole.DecisionCriterion,PreferenceDirection.HigherIsBetter,g.DisplayName,null,maxConfidence<=0m?0m:Math.Clamp(g.Confidence/maxConfidence,0m,1m)))
                .ToArray();
        }
        if(dimensions.Length==0)return null;
        var constraints=(queryContract?.HardConstraints??[])
            .Select((text,index)=>new ResolvedConstraint($"con-{index}",text,null))
            .ToArray();
        // Always treat the top-ranked meaning as the provisional decision so ABV surfaces an actual
        // Action Business Plan for review, even when the run is clarification-required or the ranking is
        // close. Governance still keeps every business value deterministic and provenance-tagged.
        var converged=true;
        return new()
        {
            Objective=string.IsNullOrWhiteSpace(request.Query)?"Wide search decision":request.Query.Trim(),
            Dimensions=dimensions,
            HardConstraints=constraints,
            Preferences=[],
            Interactions=[],
            Uncertainties=[],
            IsConverged=converged
        };
    }

    private static WideAbvActionDto MapAbv(AbvResolutionOutcome outcome)=>new(
        StatusCode:outcome.Status.ToString(),
        ActionabilityStatusCode:outcome.Actionability.Status.ToString(),
        ExecutionAllowed:outcome.Actionability.ExecutionAllowed,
        HumanApprovalRequired:outcome.Actionability.HumanApprovalRequired,
        IntentCode:outcome.Intent?.IntentCode,
        IntentName:outcome.Intent?.Name,
        IntentRationale:outcome.Intent?.Rationale,
        IntentSourceCode:outcome.Intent is null?null:outcome.Intent.Source.ToString(),
        ImpactTierCode:outcome.Impact is null?null:outcome.Impact.Tier.ToString(),
        MetricAtRisk:outcome.Impact?.MetricAtRisk,
        EstimatedExposure:outcome.Impact?.EstimatedExposure,
        ImpactSourceCode:outcome.Impact is null?null:outcome.Impact.Source.ToString(),
        PriorityCode:outcome.Urgency is null?null:outcome.Urgency.Priority.ToString(),
        SlaHours:outcome.Urgency?.SlaHours,
        UrgencyPolicyCode:outcome.Urgency?.PolicyCode,
        UrgencySourceCode:outcome.Urgency is null?null:outcome.Urgency.Source.ToString(),
        OwnerRole:outcome.ExecutionPath?.OwnerRole,
        OwnerSourceCode:outcome.ExecutionPath is null?null:outcome.ExecutionPath.Source.ToString(),
        ActionCode:outcome.ExecutionPath?.ActionCode,
        NextStep:outcome.ExecutionPath?.NextStep,
        ExecutionSourceCode:outcome.ExecutionPath is null?null:outcome.ExecutionPath.Source.ToString(),
        PlaybookCode:outcome.ExecutionPath?.PlaybookCode,
        FailureMessage:outcome.FailureMessage);

    // 'POLOXI Engine' filter disabled: complete LLM-based result without POLOXI. One LLM call answers the
    // question directly; the answer is always INTERPRETIVE because nothing is validated against
    // enterprise data. Execution is still audited in POLOXI.WideExecution for governance.
    private async Task<WideSearchResponse> SearchLlmOnlyAsync(WideSearchRequest request,Stopwatch timer,CancellationToken cancellationToken)
    {
        var executionId=await wideRepository.StartWideExecutionAsync(new(request.TenantId,request.UserId,request.Query,request.CorrelationId),cancellationToken);
        try
        {
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_ANSWER",
                await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.WideLlmOnlyAnswer,cancellationToken),
                $"Question: {request.Query}",
                AnswerSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_LLM_ONLY",null,request.CorrelationId,"Intelligent Search Wide"),ModelOverride(request),cancellationToken);
            var answer=JsonSerializer.Deserialize<WideAnswerProposal>(result.Content,JsonOptions)??throw new ValidationException("The Wide LLM-only answer response was empty.");
            var confidence=Math.Clamp(answer.Confidence,0,1);
            timer.Stop();
            await wideRepository.CompleteWideExecutionAsync(request.TenantId,request.UserId,executionId,"COMPLETED","LLM_ONLY",0,1,confidence,"INTERPRETIVE",string.IsNullOrWhiteSpace(answer.Answer)?null:answer.Answer,timer.ElapsedMilliseconds,cancellationToken);
            return new(executionId,request.Query,"COMPLETED","LLM_ONLY",0,1,confidence,"INTERPRETIVE",string.IsNullOrWhiteSpace(answer.Answer)?null:answer.Answer,[],[],answer.SuggestedActions.Select(action=>new WideActionSuggestionDto(action.DisplayName,action.NavigationRoute,action.Rationale)).ToArray(),timer.ElapsedMilliseconds){ExternalReferences=MapExternalReferences(answer),InterpretiveResults=MapInterpretiveResults(answer,[],[]),ProviderCodeUsed=result.ProviderCode,ModelCodeUsed=result.ModelCode,LlmRawItems=(answer.InterpretiveResults??[]).FirstOrDefault(entry=>entry.Items is{Count:>0})?.Items.OrderBy(item=>item.RankNumber).Select((item,index)=>new WideInterpretiveResultItemDto(item.RankNumber>0?item.RankNumber:index+1,item.Name.Trim(),item.Detail.Trim())).ToArray()??[]};
        }
        catch
        {
            timer.Stop();
            await wideRepository.CompleteWideExecutionAsync(request.TenantId,request.UserId,executionId,"FAILED","LLM_ONLY",0,1,0m,"NONE",null,timer.ElapsedMilliseconds,cancellationToken);
            throw;
        }
    }

    // Raw first LLM result for the POLOXI comparison table: one single-shot call carrying ONLY the plain
    // user query — no query contract, no branches, no evidence, no constraints — so the returned ranking
    // is exactly what the model itself would answer in its own chat interface. Fail-soft: any failure
    // returns an empty list and never disturbs the POLOXI pipeline.
    private async Task<IReadOnlyCollection<WideInterpretiveResultItemDto>> GetRawLlmRankingAsync(WideSearchRequest request,CancellationToken cancellationToken)
    {
        try
        {
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_ANSWER",
                await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.WideLlmRawAnswer,cancellationToken),
                $"Question: {request.Query}",
                AnswerSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_LLM_RAW",null,request.CorrelationId,"Intelligent Search Wide"),ModelOverride(request),cancellationToken);
            var answer=JsonSerializer.Deserialize<WideAnswerProposal>(result.Content,JsonOptions);
            var items=(answer?.InterpretiveResults??[]).FirstOrDefault(entry=>entry.Items is{Count:>0})?.Items;
            return items is null?[]:items.OrderBy(item=>item.RankNumber).Select((item,index)=>new WideInterpretiveResultItemDto(item.RankNumber>0?item.RankNumber:index+1,item.Name.Trim(),item.Detail.Trim(),NormalizeScore(item.Score))).ToArray();
        }
        catch
        {
            return [];
        }
    }

    // Enterprise grounding: a branch mapped to an approved capability executes a deterministic authorized
    // search; child evidence is intersected with the parent's evidence (progressive narrowing of the same
    // entity type). Unmapped branches survive as INTERPRETIVE reasoning paths.
    // Pure grounding: reads parent evidence keys but never mutates shared state, so branches within a
    // level can be grounded concurrently. The caller merges results sequentially in branch order.
    private async Task<(string StatusCode,IReadOnlyCollection<PoloxiEvidenceDto> Evidence,HashSet<string> Keys)> GroundBranchAsync(WideBranchRecord branch,PoloxiSearchRequest poloxiRequest,IReadOnlyCollection<PoloxiCapabilityDto> capabilities,int maximumResults,Dictionary<Guid,HashSet<string>> branchEvidenceKeys,CancellationToken cancellationToken)
    {
        var capability=branch.CapabilityCode is null?null:capabilities.FirstOrDefault(item=>item.CapabilityCode.Equals(branch.CapabilityCode,StringComparison.OrdinalIgnoreCase)&&item.ExecutionHandlerCode.Equals("AUTHORIZED_SEARCH_DOCUMENT",StringComparison.OrdinalIgnoreCase));
        if(capability is null)return("INTERPRETIVE",[],new(StringComparer.OrdinalIgnoreCase));
        var searchText=NormalizePoloxiSearchText(branch.SearchText??branch.DisplayName,capability);
        var poloxiBranch=new PoloxiBranchRecord(branch.WideBranchId,branch.ParentWideBranchId,branch.BranchCode,branch.DisplayName,branch.Interpretation,capability.CapabilityCode,"VALID","Wide dynamic grounding.",searchText,capability.SupportsRecency,branch.Confidence,branch.SortOrder);
        var branchEvidence=await repository.ExecutePoloxiBranchAsync(poloxiRequest,poloxiBranch,capability,maximumResults,cancellationToken);
        if(branch.ParentWideBranchId is{}parentId&&branchEvidenceKeys.TryGetValue(parentId,out var parentKeys)&&parentKeys.Any(key=>key.StartsWith($"{capability.EntityTypeCode}:",StringComparison.OrdinalIgnoreCase)))
            branchEvidence=branchEvidence.Where(item=>parentKeys.Contains($"{item.EntityTypeCode}:{item.EntityId:D}")).ToArray();
        var keys=branchEvidence.Select(item=>$"{item.EntityTypeCode}:{item.EntityId:D}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        return("GROUNDED",branchEvidence,keys);
    }

    private async Task<WideIntentProposal> ProposeIntentAsync(WideSearchRequest request,IReadOnlyCollection<PoloxiCapabilityDto> capabilities,WideConfiguration configuration,WideQueryContract? queryContract,CancellationToken cancellationToken)
    {
        var catalog=BuildCatalog(capabilities);
        // V2.1: when a query contract exists, the LLM branches ONLY the ambiguous concepts; hard
        // constraints and output requirements are fixed by the user and must never be reinterpreted.
        var contractContext=queryContract is null?string.Empty:$"\n{BuildQueryContractContext(queryContract)}";
        var userPrompt=BuildIntentUserPrompt(request.Query,contractContext,catalog,configuration.MaximumBranchesPerLevel);
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_INTENT",
                await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.WideIntent,cancellationToken),
            userPrompt,
            IntentSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_INTENT",null,request.CorrelationId,"Intelligent Search Wide"),ModelOverride(request),cancellationToken);
        return JsonSerializer.Deserialize<WideIntentProposal>(result.Content,JsonOptions)??throw new ValidationException("The Wide intent response was empty.");
    }

    private async Task<WideLevelProposal> ProposeNextLevelAsync(WideSearchRequest request,IReadOnlyCollection<WideBranchRecord> parents,IReadOnlyCollection<PoloxiCapabilityDto> capabilities,WideConfiguration configuration,int levelNumber,List<PoloxiEvidenceDto> evidence,WideQueryContract? queryContract,CancellationToken cancellationToken)
    {
        var catalog=BuildCatalog(capabilities);
        var parentSummary=string.Join('\n',parents.Select(parent=>
        {
            var samples=evidence.Where(item=>item.HierarchyBranchId==parent.WideBranchId).Take(3).Select(item=>item.Title);
            return $"- {parent.BranchCode} \"{parent.DisplayName}\" ({parent.GroundingStatusCode}, evidence: {parent.EvidenceCount}, confidence: {parent.Confidence:P0}): {parent.Interpretation}{(parent.EvidenceCount>0?$" | sample evidence: {string.Join("; ",samples)}":string.Empty)}";
        }));
        var contractContext=queryContract is null?string.Empty:$"\n{BuildQueryContractContext(queryContract)}";
        var userPrompt=BuildHierarchyUserPrompt(request.Query,contractContext,parentSummary,catalog,levelNumber,configuration.MaximumBranchesPerLevel);
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_HIERARCHY_STEP",
                await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.WideHierarchyStep,cancellationToken),
            userPrompt,
            LevelSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_HIERARCHY_STEP",null,request.CorrelationId,"Intelligent Search Wide"),ModelOverride(request),cancellationToken);
        return JsonSerializer.Deserialize<WideLevelProposal>(result.Content,JsonOptions)??throw new ValidationException("The Wide hierarchy step response was empty.");
    }

    private static string BuildIntentUserPrompt(string query,string contractContext,string catalog,int maximumBranches)
    {
        var boundedContract=Truncate(contractContext,3500)??string.Empty;
        var boundedCatalog=Truncate(catalog,1500)??string.Empty;
        var envelope=$"{boundedContract}\nMaximum branches: {maximumBranches}\nApproved capability catalog (for optional grounding):\n{boundedCatalog}";
        var boundedQuery=Truncate(query,Math.Max(0,WideUserPromptBudget-"Ambiguous question: ".Length-envelope.Length))??string.Empty;
        return $"Ambiguous question: {boundedQuery}{envelope}";
    }

    private static string BuildHierarchyUserPrompt(string query,string contractContext,string parentSummary,string catalog,int levelNumber,int maximumBranches)
    {
        var boundedQuery=Truncate(query,4500)??string.Empty;
        var boundedContract=Truncate(contractContext,2500)??string.Empty;
        var boundedParents=Truncate(parentSummary,3000)??string.Empty;
        var boundedCatalog=Truncate(catalog,1000)??string.Empty;
        var prompt=$"Original question: {boundedQuery}{boundedContract}\nLevel to propose: {levelNumber}\nMaximum branches per parent: {maximumBranches}\nSurviving parent branches with grounding outcomes:\n{boundedParents}\nApproved capability catalog (for optional grounding):\n{boundedCatalog}";
        return Truncate(prompt,WideUserPromptBudget)!;
    }

    private static string BuildQueryContractContext(WideQueryContract queryContract)
    {
        static string Join(IReadOnlyCollection<string> values)=>values.Count==0?"(none)":string.Join("; ",values);
        return $"Query contract (FIXED, do not reinterpret): answer kind: {queryContract.AnswerKind??"(unspecified)"}; candidate kind: {queryContract.CandidateKind??"(unspecified)"}; intent: {queryContract.Intent??"(unspecified)"}; target object: {queryContract.TargetObject??queryContract.EntityType??"(unspecified)"}; entity type: {queryContract.EntityType??"(unspecified)"}; geography: {queryContract.GeographicConstraint??"(unspecified)"}; requested count: {(queryContract.RequestedCount?.ToString()??"(unspecified)")}; ranking concept: {queryContract.RankingConcept??"(unspecified)"}; output shape: {queryContract.OutputShape??"(unspecified)"}; safety risk: {queryContract.SafetyRiskCode??(queryContract.IsSafetySensitive?"MEDIUM":"NONE")}\nHard constraints: {Join(queryContract.HardConstraints)}\nRequired terms: {Join(queryContract.RequiredTerms)}\nExcluded terms: {Join(queryContract.ExcludedTerms)}\nOutput requirements: {Join(queryContract.OutputRequirements)}\nAmbiguous concepts to disambiguate (branch ONLY these unless evidence proves incompleteness): {Join(queryContract.AmbiguousConcepts)}\nAmbiguous terms: {Join(queryContract.AmbiguousTerms)}";
    }

    // Cache-first live external grounding for interpretive narrowing paths. Any failure returns an
    // empty collection so the Wide pipeline never breaks when the provider is unavailable.
    // Retrievals run concurrently under a bounded gate; results merge in branch-priority order.
    private async Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> GatherExternalKnowledgeAsync(WideSearchRequest request,Guid executionId,IReadOnlyCollection<WideBranchRecord> interpretiveBranches,int retrievalConcurrency,CancellationToken cancellationToken)
    {
        if(interpretiveBranches.Count==0)return [];
        try
        {
            var configuration=await wideRepository.GetExternalGroundingConfigurationAsync(request.TenantId,cancellationToken);
            if(!configuration.Enabled||string.IsNullOrWhiteSpace(configuration.ApiKey))return [];
            var notBeforeUtc=DateTime.UtcNow.AddHours(-configuration.CacheHours);
            var targets=interpretiveBranches.OrderBy(item=>item.LevelNumber).ThenByDescending(item=>item.Confidence).Take(configuration.MaximumQueriesPerExecution).ToArray();
            var results=new IReadOnlyCollection<WideExternalKnowledgeSnippet>[targets.Length];
            using var retrievalGate=new SemaphoreSlim(Math.Max(1,retrievalConcurrency));
            await Task.WhenAll(targets.Select(async(branch,index)=>
            {
                await retrievalGate.WaitAsync(cancellationToken);
                try
                {
                    var query=BuildCandidateSeekingQuery(request.Query,branch.DisplayName);
                    var cached=await wideRepository.GetCachedExternalKnowledgeAsync(request.TenantId,query,notBeforeUtc,cancellationToken);
                    if(cached.Count>0){results[index]=cached.Take(configuration.MaximumSnippetsPerQuery).ToArray();return;}
                    var retrieved=await externalKnowledgeProvider.SearchAsync(query,configuration,cancellationToken);
                    if(retrieved.Count==0){results[index]=[];return;}
                    await wideRepository.SaveExternalKnowledgeAsync(request.TenantId,request.UserId,query,retrieved,executionId,cancellationToken);
                    results[index]=retrieved;
                }
                catch(Exception)when(!cancellationToken.IsCancellationRequested)
                {
                    // Fail-soft per branch: one provider failure never discards the other branches' snippets.
                    results[index]=[];
                }
                finally{retrievalGate.Release();}
            }));
            // V3.6.1: two branches can resolve to the SAME retrieval query (display-name overlap),
            // returning identical snippets that would double-count in every score sum. Exact
            // duplicates (same URL for the same query) are collapsed; the same URL retrieved under
            // DIFFERENT branch queries is preserved because snippet.Query drives branch attribution.
            return results.SelectMany(item=>item??[])
                .DistinctBy(snippet=>$"{snippet.Query}\u0001{snippet.Url}",StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch(Exception)when(!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
    }

    private async Task<(WideAnswerProposal Proposal,string ProviderCode,string ModelCode)> ComposeAnswerAsync(WideSearchRequest request,IReadOnlyCollection<WideBranchRecord> survivors,IReadOnlyCollection<PoloxiEvidenceDto> ranked,decimal confidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,WideQueryContract? queryContract,CancellationToken cancellationToken)
    {
        // V3.14: the fixed query contract is non-negotiable in the final answer.
        var contractContext=queryContract is null?string.Empty:$"\n{BuildQueryContractContext(queryContract)}";
        // Input budget: the tenant AI safety guard (Intelligence.Safety.MaximumInputCharacters) blocks
        // prompts over the configured limit. The answer system prompt is large and the survivor/evidence
        // sections grow with depth, so every variable section is clamped and, if the assembled user prompt
        // still exceeds the budget, the sections are progressively shrunk instead of failing the search.
        var orderedSurvivors=survivors.OrderBy(branch=>branch.LevelNumber).ThenBy(branch=>branch.SortOrder).ToArray();
        // All interpretive narrowing paths (Level 1 first, then highest confidence) drive real-world reference
        // and interpretive result-set generation; the branch sub-header (Interpretation) is fed to the LLM.
        // Not-prioritized (DORMANT) branches are included so their interpretations still receive full
        // result sets — they render as secondary-importance results, never disappear from the surface.
        var allInterpretiveBranches=survivors.Where(branch=>branch.GroundingStatusCode=="INTERPRETIVE"||branch.BranchStateCode==WideBranchStates.Dormant).OrderBy(branch=>branch.LevelNumber).ThenByDescending(branch=>branch.Confidence).ToArray();
        var pathCount=orderedSurvivors.Length;var interpretiveCount=Math.Min(allInterpretiveBranches.Length,10);var evidenceCount=Math.Min(ranked.Count,12);var snippetCount=Math.Min(externalKnowledge.Count,10);var snippetLength=900;
        WideBranchRecord[] topInterpretiveBranches;string userPrompt;
        while(true)
        {
            var paths=string.Join('\n',orderedSurvivors.Take(pathCount).Select(branch=>$"- L{branch.LevelNumber} {branch.DisplayName} ({branch.GroundingStatusCode}, evidence: {branch.EvidenceCount}, confidence: {branch.Confidence:P0}): {Truncate(branch.Interpretation,300)}"));
            topInterpretiveBranches=allInterpretiveBranches.Take(interpretiveCount).ToArray();
            var topInterpretive=string.Join('\n',topInterpretiveBranches.Select((branch,index)=>$"{index+1}. [L{branch.LevelNumber}] {branch.DisplayName} ({branch.Confidence:P0}): {Truncate(branch.Interpretation,300)}"));
            var grounding=ranked.Count==0?"(no enterprise evidence)":string.Join('\n',ranked.Take(evidenceCount).Select(item=>$"[{item.RankNumber}] {Truncate(item.Title,150)} ({item.EntityTypeCode}): {Truncate(item.Excerpt,300)}"));
            // Clamp each live snippet so external grounding cannot blow the answer prompt past the
            // feature-policy input budget (Tavily content blocks can be several thousand characters).
            var externalGrounding=externalKnowledge.Count==0?"(none)":string.Join('\n',externalKnowledge.Take(snippetCount).Select((snippet,index)=>$"E{index+1}. {Truncate(snippet.Title,150)} ({snippet.Url}, retrieved {snippet.RetrievedDateUtc:yyyy-MM-dd}): {Truncate(snippet.Snippet,snippetLength)}"));
            userPrompt=$"Question: {Truncate(request.Query,4000)}{Truncate(contractContext,3000)}\nOverall confidence: {confidence:P0}\nSurviving disambiguation paths:\n{paths}\nNumbered interpretive narrowing paths ({topInterpretiveBranches.Length} paths - return {topInterpretiveBranches.Length} interpretiveResults entries):\n{(string.IsNullOrEmpty(topInterpretive)?"(none)":topInterpretive)}\nEnterprise evidence:\n{grounding}\nExternal evidence snippets (live web, current figures - use these for TIME_SENSITIVE paths):\n{externalGrounding}";
            if(userPrompt.Length<=WideUserPromptBudget)break;
            // Shrink in evidence-preserving order: snippet length, snippet count, path list, then interpretive paths.
            if(snippetLength>400){snippetLength=400;continue;}
            if(snippetCount>4){snippetCount=4;continue;}
            if(pathCount>12){pathCount=12;continue;}
            if(evidenceCount>6){evidenceCount=6;continue;}
            if(interpretiveCount>4){interpretiveCount=4;continue;}
            break;
        }
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_ANSWER",
                await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.WideAnswer,cancellationToken),
            userPrompt,
            AnswerSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_ANSWER",null,request.CorrelationId,"Intelligent Search Wide"),ModelOverride(request),cancellationToken);
        return (JsonSerializer.Deserialize<WideAnswerProposal>(result.Content,JsonOptions)??throw new ValidationException("The Wide answer response was empty."),result.ProviderCode,result.ModelCode);
    }

    private static WideBranchRecord[] MaterializeBranches(IReadOnlyCollection<WideProposedBranch> proposed,Guid executionId,Guid tenantId,int levelNumber,IReadOnlyDictionary<string,WideBranchRecord> parentsByCode,WideConfiguration configuration)=>
        (proposed??[]).Take(configuration.MaximumBranchesPerLevel*Math.Max(parentsByCode.Count,1)).Select((branch,index)=>
        {
            Guid? parentId=branch.ParentBranchCode is not null&&parentsByCode.TryGetValue(NormalizeCode(branch.ParentBranchCode),out var parent)?parent.WideBranchId:null;
            // Calibration guard: LLM self-reported confidence is capped at 0.9 for interpretive (ungrounded)
            // branches and may never exceed the parent's confidence, so deeper levels cannot all show 100%.
            var ceiling=string.IsNullOrWhiteSpace(branch.CapabilityCode)?0.9m:1m;
            var parentRecord=branch.ParentBranchCode is not null&&parentsByCode.TryGetValue(NormalizeCode(branch.ParentBranchCode),out var parentForConfidence)?parentForConfidence:null;
            var confidence=Math.Clamp(branch.Confidence,0,Math.Min(ceiling,parentRecord?.Confidence??ceiling));
            // Clamp LLM free text to POLOXI.WideBranch column lengths (DB is the source of truth).
            // V2.3: semantic type defaults to ALTERNATIVE (backward compatible) unless the LLM
            // explicitly classified the branch as a DIMENSION (jointly valid evaluation criterion).
            var semanticType=string.Equals(branch.SemanticType?.Trim(),WideBranchSemanticTypes.Dimension,StringComparison.OrdinalIgnoreCase)?WideBranchSemanticTypes.Dimension:WideBranchSemanticTypes.Alternative;
            return new WideBranchRecord(Guid.NewGuid(),executionId,parentId,tenantId,levelNumber,Truncate(NormalizeCode(branch.BranchCode),120),Truncate(branch.DisplayName.Trim(),300),Truncate(branch.Interpretation.Trim(),1000),Truncate(branch.CapabilityCode?.Trim(),100),Truncate(branch.SearchText?.Trim(),400),"PENDING",0,confidence,branch.ContinueNarrowing,Truncate(branch.StopReason?.Trim(),50),false,null,index+1){SemanticTypeCode=semanticType};
        }).ToArray();

    private static string? Truncate(string? value,int maximumLength)=>value is null||value.Length<=maximumLength?value:value[..maximumLength];

    // V2.8.4: clarification option labels are recognition prompts, not paragraphs — first sentence,
    // capped, so choices scan like "Business banking / fintech for startups" rather than an essay.
    private static string TrimDescription(string detail)
    {
        var firstSentence=detail.Split(['.','!','?'],2)[0].Trim();
        return firstSentence.Length<=120?firstSentence:firstSentence[..117]+"...";
    }

    // V2.8.5 answer→candidate reweighting: the user's clarification answer is direct intent evidence.
    // Token overlap between the answer and each candidate's name/detail produces a deterministic
    // composite boost (up to +35% relative), then candidates are re-ranked. Zero-LLM by design so the
    // user's stated choice reliably moves the ranking even when the re-executed evidence run is noisy.
    // V2.9 Answer Composer: translate the Candidate × Branch outcome into a presentation contract.
    // Everything is computed deterministically from data the engine already produced — the composer
    // NEVER reranks, invents evidence, or resolves uncertainty POLOXI did not resolve. The presentation
    // layer's job narrows to: communicate POLOXI's decision clearly and faithfully.
    // Branch display names are phrased for the reasoning hierarchy ("Best by Quality of Life",
    // "Best in terms of affordability and cost of living"). On ranking cards those prefixes read
    // as noise ("Best for: Best by Quality of Life"), so strip the leading qualifier phrasing and
    // title-shape the remainder into a clean dimension label ("Quality of Life").
    private static string HumanizeDimensionName(string displayName)
    {
        var name=displayName.Trim();
        string[] prefixes=["best in terms of ","best by ","best for ","best on ","ranked by ","evaluated by ","evaluated on ","based on "];
        foreach(var prefix in prefixes)
        {
            if(name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)){name=name[prefix.Length..].Trim();break;}
        }
        return name.Length==0?displayName.Trim():char.ToUpperInvariant(name[0])+name[1..];
    }

    private static string HamrcFamilyKey(string displayName)
    {
        var humanized=HumanizeDimensionName(displayName);
        var separator=humanized.IndexOf(':');
        var family=separator>0?humanized[..separator]:humanized;
        return family.Trim();
    }

    private static string NormalizeBranchDisplayKey(string displayName)
    {
        var value=displayName.Trim();
        value=System.Text.RegularExpressions.Regex.Replace(value,@"^L\d+\s*(?:·|-|:)?\s*",string.Empty,System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant).Trim();
        return value;
    }

    private static Dictionary<string,WideBranchRecord> BuildBranchDisplayLookup(IReadOnlyCollection<WideBranchRecord> branches)=>
        branches.GroupBy(branch=>NormalizeBranchDisplayKey(branch.DisplayName),StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group=>group.Key,group=>group.OrderBy(branch=>branch.LevelNumber).First(),StringComparer.OrdinalIgnoreCase);

    private static WideBranchRecord? ResolveBranchForReason(string displayName,IReadOnlyDictionary<string,WideBranchRecord> branchLookup)
    {
        var key=NormalizeBranchDisplayKey(displayName);
        if(branchLookup.TryGetValue(key,out var exact))return exact;
        var partial=branchLookup.Where(entry=>entry.Key.Contains(key,StringComparison.OrdinalIgnoreCase)||key.Contains(entry.Key,StringComparison.OrdinalIgnoreCase)).Take(2).ToArray();
        return partial.Length==1?partial[0].Value:null;
    }

    private static WideBranchRecord ResolveReasonFamilyBranch(WideBranchRecord branch,IReadOnlyDictionary<Guid,WideBranchRecord> branchesById)
    {
        var current=branch;
        while(current.ParentWideBranchId is Guid parentId&&branchesById.TryGetValue(parentId,out var parent))current=parent;
        return current;
    }

    private static IReadOnlyList<WideDimensionScoreDto> CompressReasonScores(IReadOnlyCollection<WideCandidateBranchScoreDto> scores,IReadOnlyCollection<WideBranchRecord>? hierarchy=null)
    {
        if(scores.Count==0)return [];
        if(hierarchy is{Count:>0})
        {
            var lookup=BuildBranchDisplayLookup(hierarchy);
            var byId=hierarchy.GroupBy(branch=>branch.WideBranchId).ToDictionary(group=>group.Key,group=>group.First());
            var hierarchyCompressed=scores
                .Select(score=>(Score:score,Branch:ResolveBranchForReason(score.BranchDisplayName,lookup)))
                .Where(item=>item.Branch is not null)
                .GroupBy(item=>ResolveReasonFamilyBranch(item.Branch!,byId).WideBranchId)
                .Select(group=>
                {
                    var family=ResolveReasonFamilyBranch(group.First().Branch!,byId);
                    var direct=group.FirstOrDefault(item=>item.Branch!.WideBranchId==family.WideBranchId).Score;
                    var familyScore=direct is not null?direct.EvidenceScore:group.Average(item=>item.Score.EvidenceScore);
                    return new WideDimensionScoreDto(HumanizeDimensionName(family.DisplayName),Math.Clamp(familyScore,0,1));
                })
                .OrderByDescending(score=>score.Score)
                .ToArray();
            if(hierarchyCompressed.Length>0)return hierarchyCompressed;
        }
        var childNames=scores.SelectMany(score=>score.ChildScores.Select(child=>HumanizeDimensionName(child.BranchDisplayName))).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var independent=scores
            .Where(score=>score.ChildScores.Count>0||!childNames.Contains(HumanizeDimensionName(score.BranchDisplayName)))
            .GroupBy(score=>HamrcFamilyKey(score.BranchDisplayName),StringComparer.OrdinalIgnoreCase)
            .Select(group=>
            {
                var preferred=group.OrderByDescending(score=>score.ChildScores.Count>0).ThenByDescending(score=>score.EvidenceScore).First();
                return new WideDimensionScoreDto(HumanizeDimensionName(preferred.BranchDisplayName),preferred.EvidenceScore);
            })
            .OrderByDescending(score=>score.Score)
            .ToArray();
        if(independent.Length>0)return independent;
        return scores.GroupBy(score=>HumanizeDimensionName(score.BranchDisplayName),StringComparer.OrdinalIgnoreCase)
            .Select(group=>new WideDimensionScoreDto(group.Key,group.Max(score=>score.EvidenceScore)))
            .OrderByDescending(score=>score.Score)
            .ToArray();
    }

    private static Dictionary<string,decimal> CompressReasonScoreMap(IReadOnlyCollection<WideCandidateBranchScoreDto> scores,IReadOnlyCollection<WideBranchRecord>? hierarchy=null)=>
        CompressReasonScores(scores,hierarchy).ToDictionary(score=>score.DimensionName,score=>score.Score,StringComparer.OrdinalIgnoreCase);

    private static string BuildWinnerBoundFinalAnswer(WideCandidateDto[] topCandidates,WideQueryContract? queryContract,WideEntropyResult finalEntropy,decimal? decisionConfidence,decimal? winnerStability,decimal? topKStability,decimal decisionEvidenceCoverage,IReadOnlyCollection<WideBranchRecord>? hierarchy=null)
    {
        var winner=topCandidates[0];
        var requestedCount=Math.Clamp(queryContract?.RequestedCount??topCandidates.Length,1,topCandidates.Length);
        var lockedRanking=string.Join(" ",topCandidates.Take(requestedCount).Select((candidate,index)=>$"{index+1}. {candidate.DisplayName} ({candidate.CompositeScore:P0})."));
        var runnerUp=topCandidates.Length>1?topCandidates[1]:null;
        var margin=runnerUp is null?1m:Math.Clamp(winner.CompositeScore-runnerUp.CompositeScore,0,1);
        var compressedWinnerScores=CompressReasonScores(winner.BranchScores,hierarchy).ToArray();
        var leadingScores=compressedWinnerScores.Where(score=>score.Score>.05m).Take(3).Select(score=>$"{score.DimensionName} ({score.Score:P0})").ToArray();
        // Trade-offs are the winner's genuinely LOWEST dimensions (excluding the ones already cited
        // as strengths) — never a tail slice of the high-score list, which misreported 100% scores
        // as weaknesses while hiding real 0% gaps.
        var leadingNames=compressedWinnerScores.Where(score=>score.Score>.05m).Take(3).Select(score=>score.DimensionName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var weakerScores=compressedWinnerScores.Where(score=>!leadingNames.Contains(score.DimensionName)).OrderBy(score=>score.Score).Take(2).Where(score=>score.Score<.5m).Select(score=>$"{score.DimensionName} ({score.Score:P0})").ToArray();
        var stabilitySentence=winnerStability switch
        {
            null=>"Ranking stability could not be measured for this run.",
            <=.05m=>"The hierarchy settled, but the candidate ranking did not stabilize; the current leader remains highly sensitive to additional evidence or weighting changes.",
            <.60m=>"The candidate ranking has limited stability, so the winner should be treated as a current evidence-weighted leader rather than a settled recommendation.",
            _=>"The candidate ranking was stable across measurement rounds, which supports the final ordering."
        };
        var confidenceText=decisionConfidence is null?"not separately computed":decisionConfidence.Value.ToString("P0");
        var rankingCertainty=Math.Clamp(1m-finalEntropy.NormalizedEntropy,0,1);
        var builder=new StringBuilder();
        builder.Append("Final ranking (deterministic): ").Append(lockedRanking).AppendLine().AppendLine();
        builder.Append(winner.DisplayName).Append(" is the immutable POLOXI winner for this execution because the deterministic Candidate × Branch competition scored it highest at ").Append(winner.CompositeScore.ToString("P0")).Append('.');
        if(runnerUp is not null)builder.Append(' ').Append(runnerUp.DisplayName).Append(" was next at ").Append(runnerUp.CompositeScore.ToString("P0")).Append(", a margin of ").Append(margin.ToString("P1")).Append('.');
        builder.AppendLine().AppendLine();
        builder.Append("Why ").Append(winner.DisplayName).Append(" won: ");
        builder.Append(leadingScores.Length>0?$"its strongest deciding dimensions were {string.Join(", ",leadingScores)}.":"it had the strongest composite score across the surviving interpretation paths.");
        if(weakerScores.Length>0)builder.Append(" Its weaker trade-off areas were ").Append(string.Join(", ",weakerScores)).Append('.');
        if(!string.IsNullOrWhiteSpace(winner.Detail))builder.Append(' ').Append(TrimDescription(winner.Detail));
        builder.AppendLine().AppendLine();
        builder.Append(stabilitySentence).Append(' ')
            .Append("Decision evidence confidence is ").Append(confidenceText)
            .Append(", ranking certainty is ").Append(rankingCertainty.ToString("P0"))
            .Append(", remaining ranking uncertainty is ").Append(finalEntropy.NormalizedEntropy.ToString("P0"))
            .Append(", and decision-dimension coverage is ").Append(decisionEvidenceCoverage.ToString("P0")).Append('.');
        if(topCandidates.Length>1)
        {
            var alternatives=string.Join("; ",topCandidates.Skip(1).Take(4).Select(candidate=>$"{candidate.DisplayName} ({candidate.CompositeScore:P0})"));
            builder.AppendLine().AppendLine().Append("Other ranked candidates remained competitive but did not win under the locked scoring: ").Append(alternatives).Append('.');
        }
        if(topKStability is not null)builder.Append(" Top-3 stability measured ").Append(topKStability.Value.ToString("P0")).Append('.');
        return builder.ToString();
    }

    private static string BuildPoloxiFullAnswer(string? llmAnswer,WideQueryContract? queryContract,IReadOnlyCollection<WideAmbiguityGroupDto> ambiguityGroups,WideCandidateDto[] topCandidates,WideEntropyResult finalEntropy,decimal? decisionConfidence,decimal? winnerStability,decimal? topKStability,decimal decisionEvidenceCoverage,string answerStatus,IReadOnlyCollection<WideBranchRecord>? hierarchy=null)
    {
        if(answerStatus=="USER_CLARIFICATION_REQUIRED")return string.IsNullOrWhiteSpace(llmAnswer)?"POLOXI needs one clarification before it can responsibly complete this answer.":llmAnswer;
        var groups=ambiguityGroups.OrderByDescending(group=>group.Confidence).ToArray();
        var hasMaterialGroups=groups.Length>1;
        var hasSafetyRisk=groups.Any(group=>group.SafetyRiskCode is "POSSIBLE" or "MEDIUM" or "HIGH")||queryContract?.SafetyRiskCode is "POSSIBLE" or "MEDIUM" or "HIGH"||queryContract?.IsSafetySensitive==true;
        if(topCandidates.Length>0&&string.Equals(queryContract?.AnswerKind,AnswerKindEntityRanking,StringComparison.OrdinalIgnoreCase))return ValidateWinnerBoundFinalAnswer(BuildWinnerBoundFinalAnswer(topCandidates,queryContract,finalEntropy,decisionConfidence,winnerStability,topKStability,decisionEvidenceCoverage,hierarchy),topCandidates,queryContract,finalEntropy,decisionConfidence,winnerStability,topKStability,decisionEvidenceCoverage,hierarchy);
        if(hasMaterialGroups)return BuildGroupedAmbiguityFullAnswer(groups,queryContract,hasSafetyRisk,topCandidates,finalEntropy,decisionConfidence,decisionEvidenceCoverage);
        if(topCandidates.Length>0)return ValidateWinnerBoundFinalAnswer(BuildWinnerBoundFinalAnswer(topCandidates,queryContract,finalEntropy,decisionConfidence,winnerStability,topKStability,decisionEvidenceCoverage,hierarchy),topCandidates,queryContract,finalEntropy,decisionConfidence,winnerStability,topKStability,decisionEvidenceCoverage,hierarchy);
        if(queryContract?.AnswerKind is AnswerKindDiagnosticProcedure or AnswerKindTechnicalRecommendation)
            return BuildProcedureFullAnswer(llmAnswer,queryContract,hasSafetyRisk);
        return string.IsNullOrWhiteSpace(llmAnswer)?"POLOXI completed the analysis, but no final prose was returned by the answer composer.":llmAnswer;
    }

    private static string BuildGroupedAmbiguityFullAnswer(WideAmbiguityGroupDto[] groups,WideQueryContract? queryContract,bool hasSafetyRisk,WideCandidateDto[] topCandidates,WideEntropyResult finalEntropy,decimal? decisionConfidence,decimal decisionEvidenceCoverage)
    {
        var topGroup=groups[0];
        var builder=new StringBuilder();
        builder.Append("POLOXI found ").Append(groups.Length).Append(" plausible meaning").Append(groups.Length==1?string.Empty:"s").Append(" for this query and analyzed each meaning separately.").AppendLine().AppendLine();
        if(hasSafetyRisk)
            builder.Append("Safety note: one or more interpretations may involve real-world safety risk. Do not perform physical, medical, legal, structural, electrical, or other hazardous actions from this answer alone; use qualified professionals or authoritative guidance where applicable.").AppendLine().AppendLine();
        builder.Append("Most supported interpretation: ").Append(topGroup.DisplayName).Append(" (").Append(topGroup.Confidence.ToString("P0")).Append("). ");
        builder.Append(TrimDescription(topGroup.Summary??topGroup.Interpretation)).AppendLine().AppendLine();
        builder.Append("Other plausible meanings considered:").AppendLine();
        foreach(var group in groups.Skip(1).Take(6))
            builder.Append("- ").Append(group.DisplayName).Append(" (").Append(group.Confidence.ToString("P0")).Append("): ").Append(TrimDescription(group.Summary??group.Interpretation)).AppendLine();
        builder.AppendLine();
        if(topCandidates.Length>0&&string.Equals(queryContract?.AnswerKind,AnswerKindEntityRanking,StringComparison.OrdinalIgnoreCase))
        {
            var requestedCount=Math.Clamp(queryContract?.RequestedCount??Math.Min(topCandidates.Length,5),1,topCandidates.Length);
            builder.Append("Within the ranked-candidate portion of the analysis, the locked POLOXI ordering is: ")
                .Append(string.Join(" ",topCandidates.Take(requestedCount).Select((candidate,index)=>$"{index+1}. {candidate.DisplayName} ({candidate.CompositeScore:P0})."))).AppendLine().AppendLine();
        }
        builder.Append("Confidence context: decision evidence confidence is ").Append((decisionConfidence??0m).ToString("P0"))
            .Append(", remaining uncertainty is ").Append(finalEntropy.NormalizedEntropy.ToString("P0"))
            .Append(", and decision-dimension coverage is ").Append(decisionEvidenceCoverage.ToString("P0")).Append('.');
        if(queryContract?.RequiresClarification==true&&!string.IsNullOrWhiteSpace(queryContract.ClarificationQuestion))
            builder.AppendLine().AppendLine().Append("Optional narrowing: ").Append(queryContract.ClarificationQuestion);
        return builder.ToString();
    }

    private static string BuildProcedureFullAnswer(string? llmAnswer,WideQueryContract queryContract,bool hasSafetyRisk)
    {
        var builder=new StringBuilder();
        if(hasSafetyRisk)
            builder.Append("Safety note: this request may involve real-world risk. Use qualified professionals or authoritative documentation before taking action.").AppendLine().AppendLine();
        builder.Append("POLOXI treated this as ").Append(queryContract.AnswerKind?.Replace('_',' ').ToLowerInvariant()??"a procedural answer");
        if(!string.IsNullOrWhiteSpace(queryContract.TargetObject))builder.Append(" for ").Append(queryContract.TargetObject);
        builder.Append('.').AppendLine().AppendLine();
        builder.Append(string.IsNullOrWhiteSpace(llmAnswer)?"No additional procedural prose was returned by the answer composer.":llmAnswer);
        return builder.ToString();
    }

    // V3.17 Deliverable Synthesis gate. Synthesis applies when the query is a compute/adjudicate
    // RESOLUTION task, when the ambiguity groups themselves are resolution deliverables, OR when a
    // non-ranking answer would otherwise fall back to bare interpretive prose (no ranked candidates
    // and no material ambiguity groups). True named-entity rankings keep their existing composer.
    private static bool ShouldSynthesizeDeliverable(WideConfiguration configuration,WideQueryContract? queryContract,IReadOnlyCollection<WideAmbiguityGroupDto> ambiguityGroups,WideCandidateDto[] topCandidates)
    {
        if(string.Equals(queryContract?.AnswerKind,AnswerKindResolution,StringComparison.OrdinalIgnoreCase))return true;
        var hasMaterialGroups=ambiguityGroups.Count>1;
        var isRanking=string.Equals(queryContract?.AnswerKind,AnswerKindEntityRanking,StringComparison.OrdinalIgnoreCase);
        if(hasMaterialGroups&&ambiguityGroups.Any(group=>IsResolutionLikeAmbiguityGroup(configuration,group)))return true;
        return !hasMaterialGroups&&topCandidates.Length==0&&!isRanking;
    }

    private static bool IsResolutionLikeAmbiguityGroup(WideConfiguration configuration,WideAmbiguityGroupDto group)
    {
        if(string.Equals(group.AnswerKindCode,AnswerKindResolution,StringComparison.OrdinalIgnoreCase))return true;
        if(string.Equals(group.CandidateKindCode,CandidateKindActionableSolution,StringComparison.OrdinalIgnoreCase))return true;
        if(FindAnswerKind(configuration,group.AnswerKindCode) is{RunsCandidateCompetition:false})return true;
        if(configuration.DeliverableSynthesisIndicators.Count==0)return false;
        var text=$"{group.GroupCode} {group.DisplayName} {group.Interpretation} {group.Summary}";
        return configuration.DeliverableSynthesisIndicators.Any(indicator=>ContainsConfiguredIndicator(text,indicator));
    }

    private static bool ContainsConfiguredIndicator(string text,string indicator)
    {
        if(string.IsNullOrWhiteSpace(text)||string.IsNullOrWhiteSpace(indicator))return false;
        var normalizedText=NormalizeIndicatorText(text);
        var normalizedIndicator=NormalizeIndicatorText(indicator);
        return normalizedIndicator.Length>0&&normalizedText.Contains(normalizedIndicator,StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeIndicatorText(string value)
    {
        var builder=new StringBuilder(value.Length);
        foreach(var character in value)
            builder.Append(char.IsLetterOrDigit(character)||character=='/'?char.ToLowerInvariant(character):' ');
        return System.Text.RegularExpressions.Regex.Replace(builder.ToString(),@"\s+"," ").Trim();
    }

    // Deterministic assembly of the resolution deliverable from pipeline state (no LLM call). The
    // determinacy verdict is driven by the query's declared output requirements vs. the grounded
    // evidence coverage and remaining uncertainty; blocking inputs are the output requirements the
    // run could not ground; citations reference the strongest enterprise and external evidence.
    private static WideResolutionDeliverableDto? BuildResolutionDeliverable(WideSearchRequest request,WideConfiguration configuration,WideQueryContract? queryContract,IReadOnlyCollection<WideAmbiguityGroupDto> ambiguityGroups,PoloxiEvidenceDto[] relevantEvidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,decimal? decisionConfidence,decimal decisionEvidenceCoverage,WideEntropyResult finalEntropy)
    {
        var confidence=Math.Clamp(decisionConfidence??0m,0m,1m);
        var coverage=Math.Clamp(decisionEvidenceCoverage,0m,1m);
        var uncertainty=Math.Clamp(finalEntropy.NormalizedEntropy,0m,1m);
        var hasEvidence=relevantEvidence.Length>0||externalKnowledge.Count>0;
        var resolutionGroups=ambiguityGroups.Where(group=>IsResolutionLikeAmbiguityGroup(configuration,group)).OrderByDescending(group=>group.Confidence).ToArray();
        var hasResolutionAmbiguity=resolutionGroups.Length>1;
        // Output requirements the run declared but could not ground become the blocking-input checklist.
        var outputRequirements=(queryContract?.OutputRequirements??[]).Where(item=>!string.IsNullOrWhiteSpace(item)).Select(item=>item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var ambiguousInputs=(queryContract?.AmbiguousConcepts??[]).Concat(queryContract?.AmbiguousTerms??[]).Where(item=>!string.IsNullOrWhiteSpace(item)).Select(item=>item.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var blockingInputs=new List<string>();
        if(!hasEvidence)blockingInputs.Add("No grounded facts or rules were retrieved for the supplied inputs; the outcome cannot be computed from the material provided.");
        if(hasResolutionAmbiguity)blockingInputs.Add($"Resolve the requested deliverable meaning: {string.Join(" or ",resolutionGroups.Take(4).Select(group=>group.DisplayName))}.");
        blockingInputs.AddRange(ambiguousInputs.Select(item=>$"Unresolved input: {item}"));
        if(outputRequirements.Length>0&&coverage<0.5m)
            blockingInputs.AddRange(outputRequirements.Select(item=>$"Insufficient grounding for required output: {item}"));
        var target=BuildResolutionTarget(queryContract,resolutionGroups);
        // Determinacy: RESOLVED needs grounded evidence, adequate coverage, and low residual uncertainty.
        string determinacy;
        if(hasEvidence&&coverage>=0.5m&&uncertainty<=0.5m&&blockingInputs.Count==0)determinacy="RESOLVED";
        else if(hasEvidence&&(coverage>0m||confidence>0m))determinacy="PARTIAL";
        else determinacy="BLOCKED";
        var citations=BuildResolutionCitations(relevantEvidence,externalKnowledge);
        var reason=BuildResolutionReason(determinacy,target,relevantEvidence,externalKnowledge,coverage,confidence,uncertainty);
        var headline=determinacy switch
        {
            "RESOLVED"=>$"POLOXI resolved {target} from grounded evidence.",
            "PARTIAL"=>hasResolutionAmbiguity?$"POLOXI partially resolved {target}; multiple deliverable meanings remain possible.":$"POLOXI partially resolved {target}; some required inputs are still unresolved.",
            _=>$"POLOXI cannot resolve {target} yet — required inputs are missing."
        };
        var outcome=determinacy=="BLOCKED"?null:BuildResolutionOutcome(target,relevantEvidence,externalKnowledge);
        return new(determinacy,headline,outcome,reason,blockingInputs,citations,confidence,coverage,uncertainty);
    }

    private static string BuildResolutionTarget(WideQueryContract? queryContract,IReadOnlyCollection<WideAmbiguityGroupDto> resolutionGroups)
    {
        if(resolutionGroups.Count>1)
        {
            var leading=resolutionGroups.OrderByDescending(group=>group.Confidence).First();
            return $"the requested resolution, led by {leading.DisplayName}";
        }
        if(resolutionGroups.Count==1)return resolutionGroups.First().DisplayName;
        return string.IsNullOrWhiteSpace(queryContract?.TargetObject)?"the requested outcome":queryContract!.TargetObject!.Trim();
    }

    private static IReadOnlyCollection<WideResolutionCitationDto> BuildResolutionCitations(PoloxiEvidenceDto[] relevantEvidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge)
    {
        var citations=new List<WideResolutionCitationDto>();
        foreach(var item in relevantEvidence.OrderByDescending(e=>e.RelevanceScore).Take(5))
            citations.Add(new("ENTERPRISE",item.Title,TrimDescription(item.Excerpt),item.NavigationRoute));
        foreach(var item in externalKnowledge.OrderByDescending(e=>e.Score).Take(5))
            citations.Add(new("EXTERNAL",item.Title,TrimDescription(item.Snippet),item.Url));
        return citations;
    }

    private static string BuildResolutionReason(string determinacy,string target,PoloxiEvidenceDto[] relevantEvidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,decimal coverage,decimal confidence,decimal uncertainty)
    {
        var builder=new StringBuilder();
        var enterpriseCount=relevantEvidence.Length;
        var externalCount=externalKnowledge.Count;
        if(determinacy=="BLOCKED")
        {
            builder.Append("The supplied facts and rules were not sufficient to compute ").Append(target)
                .Append(". POLOXI grounded ").Append(enterpriseCount).Append(" enterprise and ").Append(externalCount)
                .Append(" external evidence items, but the deciding inputs remain missing or unresolved. Provide the blocking inputs below to complete the determination.");
            return builder.ToString();
        }
        var strongest=relevantEvidence.OrderByDescending(e=>e.RelevanceScore).FirstOrDefault();
        builder.Append("Best-supported determination for ").Append(target).Append(": ");
        if(strongest is not null)builder.Append("primarily supported by \"").Append(strongest.Title).Append("\"");
        else if(externalKnowledge.Count>0)builder.Append("primarily supported by \"").Append(externalKnowledge.OrderByDescending(e=>e.Score).First().Title).Append("\"");
        else builder.Append("supported by the grounded analysis");
        builder.Append(" (decision-evidence coverage ").Append(coverage.ToString("P0")).Append(", decision confidence ").Append(confidence.ToString("P0")).Append(", residual uncertainty ").Append(uncertainty.ToString("P0")).Append(").");
        if(determinacy=="PARTIAL")builder.Append(" This is a partial resolution: resolve the blocking inputs below to reach a fully determinate outcome.");
        return builder.ToString();
    }

    private static string? BuildResolutionOutcome(string target,PoloxiEvidenceDto[] relevantEvidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge)
    {
        var strongest=relevantEvidence.OrderByDescending(e=>e.RelevanceScore).FirstOrDefault();
        if(strongest is not null&&!string.IsNullOrWhiteSpace(strongest.Excerpt))return TrimDescription(strongest.Excerpt);
        var external=externalKnowledge.OrderByDescending(e=>e.Score).FirstOrDefault();
        if(external is not null&&!string.IsNullOrWhiteSpace(external.Snippet))return TrimDescription(external.Snippet);
        return null;
    }

    // Fold the structured deliverable into the human-ready prose answer so the primary text response
    // leads with the determination, reason, and any blocking inputs before the pipeline's own prose.
    private static string BuildResolutionFullAnswer(WideResolutionDeliverableDto deliverable,string? existingAnswer)
    {
        var builder=new StringBuilder();
        builder.Append("Deliverable Synthesis").AppendLine().AppendLine();
        builder.Append(deliverable.Headline).AppendLine().AppendLine();
        if(!string.IsNullOrWhiteSpace(deliverable.Outcome))builder.Append("Outcome: ").Append(deliverable.Outcome).AppendLine().AppendLine();
        builder.Append(deliverable.Reason).AppendLine();
        if(deliverable.BlockingInputs.Count>0)
        {
            builder.AppendLine().Append("Required to fully resolve:").AppendLine();
            foreach(var item in deliverable.BlockingInputs)builder.Append("- ").Append(item).AppendLine();
        }
        if(deliverable.Citations.Count>0)
        {
            builder.AppendLine().Append("Supporting evidence:").AppendLine();
            foreach(var citation in deliverable.Citations)
                builder.Append("- [").Append(citation.SourceCode=="EXTERNAL"?"Web":"Enterprise").Append("] ").Append(citation.Title).AppendLine();
        }
        if(!string.IsNullOrWhiteSpace(existingAnswer)&&!string.Equals(existingAnswer,"POLOXI completed the analysis, but no final prose was returned by the answer composer.",StringComparison.Ordinal))
            builder.AppendLine().Append("Analysis detail:").AppendLine().Append(existingAnswer);
        return builder.ToString();
    }

    private static string ValidateWinnerBoundFinalAnswer(string answerText,WideCandidateDto[] topCandidates,WideQueryContract? queryContract,WideEntropyResult finalEntropy,decimal? decisionConfidence,decimal? winnerStability,decimal? topKStability,decimal decisionEvidenceCoverage,IReadOnlyCollection<WideBranchRecord>? hierarchy=null)
    {
        if(topCandidates.Length==0||string.IsNullOrWhiteSpace(answerText))return answerText;
        var winner=topCandidates[0].DisplayName;
        if(!answerText.Contains(winner,StringComparison.OrdinalIgnoreCase))
            return BuildWinnerBoundFinalAnswer(topCandidates,queryContract,finalEntropy,decisionConfidence,winnerStability,topKStability,decisionEvidenceCoverage,hierarchy);
        foreach(var nonWinner in topCandidates.Skip(1))
        {
            if(ContainsNonWinnerRecommendation(answerText,nonWinner.DisplayName))
                return BuildWinnerBoundFinalAnswer(topCandidates,queryContract,finalEntropy,decisionConfidence,winnerStability,topKStability,decisionEvidenceCoverage,hierarchy);
        }
        return answerText;
    }

    private static bool ContainsNonWinnerRecommendation(string text,string candidateName)
    {
        if(string.IsNullOrWhiteSpace(candidateName)||!text.Contains(candidateName,StringComparison.OrdinalIgnoreCase))return false;
        var escaped=System.Text.RegularExpressions.Regex.Escape(candidateName);
        return System.Text.RegularExpressions.Regex.IsMatch(text,$@"\b(select|choose|pick|recommend|recommended|conclude|concludes|winner|best|ranks?\s+#?1|ranked\s+first)\b[^.\n]{{0,160}}{escaped}|{escaped}[^.\n]{{0,160}}\b(is\s+the\s+winner|is\s+best|ranks?\s+#?1|ranked\s+first|should\s+be\s+selected|should\s+be\s+chosen|best\s+fits|best\s+aligns)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static WideAnswerContext? ComposeAnswerContext(string answerStatus,WideCandidateDto[] topCandidates,WideQueryContract? queryContract,decimal? decisionConfidence,decimal? winnerStability,decimal decisionEvidenceCoverage,bool isIntentGap,IReadOnlyCollection<WideCandidateInsight>? candidateInsights=null,WideOutputContractResultDto? outputContract=null,IReadOnlyCollection<WideBranchRecord>? hierarchy=null)
    {
        if(topCandidates.Length==0)return queryContract is null?null:new(WideResponseModes.Answer,"Moderate",BuildNonRankingConfidenceNarrative(queryContract))
        {
            AnswerKindCode=queryContract.AnswerKind,
            CandidateKindCode=queryContract.CandidateKind,
            OutputShape=queryContract.OutputShape,
            TargetObject=queryContract.TargetObject??queryContract.EntityType,
            PresentationGuidance=BuildPresentationGuidance(queryContract)
        };
        // Response mode routes the UX: intent gap → candidate choice; weak grounding → evidence
        // warning; close ranking → ranking + optional preference; decisive winner → direct answer.
        var margin=topCandidates.Length<2?1m:Math.Clamp((topCandidates[0].CompositeScore-topCandidates[1].CompositeScore)/Math.Max(topCandidates[0].CompositeScore,.0001m),0,1);
        var responseMode=answerStatus=="USER_CLARIFICATION_REQUIRED"?WideResponseModes.ClarificationRequired
            :decisionEvidenceCoverage<.35m?WideResponseModes.LimitedEvidence
            :topCandidates.Length>1&&(margin<.15m||(winnerStability??1m)<.75m)?WideResponseModes.AnswerWithRefinement
            :WideResponseModes.Answer;
        // Human confidence: translate the metric bundle into language an ordinary user understands.
        var confidence=decisionConfidence??0m;
        var confidenceLabel=confidence>=.75m?"High":confidence>=.5m?"Moderate":"Low";
        var winner=topCandidates[0];
        var confidenceNarrative=responseMode switch
        {
            WideResponseModes.ClarificationRequired=>"Several distinct matches are possible — one detail from you resolves which one is meant.",
            WideResponseModes.LimitedEvidence=>$"{winner.DisplayName} leads this analysis, but fewer of the deciding factors are backed by retrieved evidence than POLOXI prefers — treat the ranking as directional.",
            WideResponseModes.AnswerWithRefinement=>$"{winner.DisplayName} leads this analysis, but several candidates scored closely and the ranking could change depending on how the deciding factors are weighted.",
            _=>$"{winner.DisplayName} leads this analysis with clear separation from the alternatives.",
        };
        // Winner strengths and weaknesses: the candidate's best and worst decision dimensions.
        // V2.9.5: dimensions are DEDUPLICATED by humanized name and a dimension can never appear in
        // both lists (with few dimensions, top-3 and bottom-2 windows can overlap — "Quality of Life"
        // must not render as a strength and a weakness simultaneously). Names are humanized here so
        // the presentation layer never shows raw "Best by X" branch labels in winner explanations.
        var winnerScores=CompressReasonScores(winner.BranchScores,hierarchy).ToArray();
        var strengths=winnerScores.Where(score=>score.Score>.05m).Take(3).ToArray();
        var strengthNames=strengths.Select(score=>score.DimensionName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var weaknesses=winnerScores.Reverse().Take(2)
            .Where(score=>!strengthNames.Contains(score.DimensionName)&&score.Score<winnerScores[0].Score)
            .ToArray();
        // Ranking-card summaries: each candidate's best dimension and its weakest (main trade-off),
        // with their evidence scores and human-friendly dimension names ("Best by Quality of Life"
        // → "Quality of Life"). The trade-off is only surfaced when it is meaningfully weaker than
        // the best dimension — a flat profile has no honest trade-off to report.
        var summaries=topCandidates.Take(10).Select(candidate=>
        {
            var ordered=CompressReasonScores(candidate.BranchScores,hierarchy).ToArray();
            var best=ordered.Length>0?ordered[0]:null;
            var worst=ordered.Length>1?ordered[^1]:null;
            var hasTradeOff=best is not null&&worst is not null&&best.Score-worst.Score>=.05m;
            // V2.9.1: grounded human-facing themes (from the answer LLM, constrained to supplied
            // evidence) take presentation priority over raw dimension chips; the dimension data is
            // kept as fallback and tooltip context. Matching is by candidate name, tolerant of the
            // LLM using a shorter or longer form of the same entity name.
            var insight=candidateInsights?.FirstOrDefault(item=>!string.IsNullOrWhiteSpace(item.CandidateName)
                &&(string.Equals(item.CandidateName.Trim(),candidate.DisplayName.Trim(),StringComparison.OrdinalIgnoreCase)
                ||candidate.DisplayName.Contains(item.CandidateName.Trim(),StringComparison.OrdinalIgnoreCase)
                ||item.CandidateName.Contains(candidate.DisplayName.Trim(),StringComparison.OrdinalIgnoreCase)));
            return new WideCandidateSummaryDto(candidate.DisplayName,candidate.CompositeScore,
                best?.DimensionName,
                hasTradeOff?worst!.DimensionName:null)
            {BestForScore=best?.Score,TradeOffScore=hasTradeOff?worst!.Score:null,
             BestFor=string.IsNullOrWhiteSpace(insight?.BestFor)?null:insight!.BestFor!.Trim(),
             PraisedFor=insight?.PraisedFor?.Where(theme=>!string.IsNullOrWhiteSpace(theme)).Select(theme=>theme.Trim()).Take(4).ToArray()??[],
             WatchOutFor=insight?.WatchOutFor?.Where(theme=>!string.IsNullOrWhiteSpace(theme)).Select(theme=>theme.Trim()).Take(4).ToArray()??[],
             SupportTierCode=candidate.SupportTierCode};
        }).ToArray();
        // Winner-vs-alternative contrasts: dimensions each side leads on, from the same score matrix.
        var contrasts=topCandidates.Skip(1).Take(3).Select(alternative=>
        {
            var winnerLeads=new List<string>();var alternativeLeads=new List<string>();
            var winnerMap=CompressReasonScoreMap(winner.BranchScores,hierarchy);
            var alternativeMap=CompressReasonScoreMap(alternative.BranchScores,hierarchy);
            foreach(var winnerScore in winnerMap)
            {
                if(!alternativeMap.TryGetValue(winnerScore.Key,out var alternativeScore))continue;
                if(winnerScore.Value>alternativeScore+.02m)winnerLeads.Add(winnerScore.Key);
                else if(alternativeScore>winnerScore.Value+.02m)alternativeLeads.Add(winnerScore.Key);
            }
            return new WideCandidateContrastDto(alternative.DisplayName,alternative.CompositeScore,winnerLeads,alternativeLeads);
        }).ToArray();
        // Changeable dimensions: highest cross-candidate separation — reweighting these could flip
        // the ranking, so they become the personalization/"could change if" chips. Intent-gap runs
        // skip this: personalization applies to decision uncertainty, not identity ambiguity.
        var changeable=isIntentGap?[]:topCandidates.Take(4)
            .SelectMany(candidate=>CompressReasonScores(candidate.BranchScores,hierarchy))
            .GroupBy(score=>score.DimensionName,StringComparer.OrdinalIgnoreCase)
            .Where(group=>group.Count()>1)
            .Select(group=>(Dimension:group.Key,Separation:group.Max(score=>score.Score)-group.Min(score=>score.Score)))
            .Where(item=>item.Separation>=.05m)
            .OrderByDescending(item=>item.Separation)
            .Take(5)
            .Select(item=>item.Dimension)
            .ToArray();
        // V2.9.2 Single Ranking-Changing Uncertainty: identify the ONE unresolved dimension most
        // likely to change #1 and the candidate most likely to replace it. Deterministic, zero-LLM:
        // for each non-violating challenger, the driving dimension is where the challenger most
        // out-scores the winner; the challenger with the smallest composite gap AND at least one
        // dimension advantage is the likely replacement. Only surfaced when the decision is not
        // decisive (thin margin, low confidence, or unstable winner) and not an intent gap.
        WideRankingChangeDriverDto? rankingChangeDriver=null;
        var rankingUnsettled=margin<.15m||(decisionConfidence??1m)<.75m||(winnerStability??1m)<.75m;
        if(!isIntentGap&&rankingUnsettled&&topCandidates.Length>1)
        {
            foreach(var challenger in topCandidates.Skip(1).Take(3))
            {
                var winnerMap=CompressReasonScoreMap(winner.BranchScores,hierarchy);
                var bestAdvantage=CompressReasonScores(challenger.BranchScores,hierarchy)
                    .Where(challengerScore=>winnerMap.ContainsKey(challengerScore.DimensionName))
                    .Select(challengerScore=>(ChallengerScore:challengerScore,WinnerScore:winnerMap[challengerScore.DimensionName],Advantage:challengerScore.Score-winnerMap[challengerScore.DimensionName]))
                    .OrderByDescending(pair=>pair.Advantage)
                    .FirstOrDefault();
                if(bestAdvantage.ChallengerScore is null||bestAdvantage.Advantage<=0)continue;
                rankingChangeDriver=new(bestAdvantage.ChallengerScore.DimensionName,challenger.DisplayName,Math.Clamp(winner.CompositeScore-challenger.CompositeScore,0,1))
                {WinnerScore=bestAdvantage.WinnerScore,ChallengerScore=bestAdvantage.ChallengerScore.Score};
                break;
            }
        }
        return new(responseMode,confidenceLabel,confidenceNarrative)
        {
            AnswerKindCode=queryContract?.AnswerKind,
            CandidateKindCode=queryContract?.CandidateKind,
            OutputShape=queryContract?.OutputShape,
            TargetObject=queryContract?.TargetObject??queryContract?.EntityType,
            PresentationGuidance=queryContract is null?[]:BuildPresentationGuidance(queryContract),
            WinnerDisplayName=responseMode==WideResponseModes.ClarificationRequired?null:winner.DisplayName,
            WinnerStrengths=strengths,WinnerWeaknesses=weaknesses,
            CandidateSummaries=summaries,CandidateContrasts=contrasts,ChangeableDimensions=changeable,
            OutputContract=outputContract,RankingChangeDriver=rankingChangeDriver,
        };
    }

    private static string BuildNonRankingConfidenceNarrative(WideQueryContract queryContract)=>queryContract.AnswerKind switch
    {
        AnswerKindClarificationRequired=>"The query has materially different meanings, so POLOXI needs clarification before evidence execution.",
        AnswerKindDiagnosticProcedure=>"POLOXI produced a diagnostic or procedural answer rather than a candidate ranking.",
        AnswerKindTechnicalRecommendation=>"POLOXI produced a technical recommendation using the fixed query contract instead of a named-entity competition.",
        AnswerKindContentEnumeration=>"POLOXI enumerated content matching the fixed query contract rather than selecting a winner.",
        _=>"POLOXI answered using the fixed query contract without a candidate competition."
    };

    private static IReadOnlyCollection<string> BuildPresentationGuidance(WideQueryContract queryContract)
    {
        var guidance=new List<string>();
        if(!string.IsNullOrWhiteSpace(queryContract.OutputShape))guidance.Add($"Render as {queryContract.OutputShape}.");
        switch(queryContract.AnswerKind)
        {
            case AnswerKindEntityRanking:
                guidance.Add(queryContract.RequestedCount is >0?$"Show exactly up to {queryContract.RequestedCount} ranked, non-violating candidates when enough evidence-backed candidates exist.":"Show an evidence-weighted ranked list.");
                break;
            case AnswerKindDiagnosticProcedure:
                guidance.Add("Show ordered diagnostic or troubleshooting steps, not competing entity cards.");
                break;
            case AnswerKindTechnicalRecommendation:
                guidance.Add("Show recommended actions with rationale, prerequisites, risks, and trade-offs.");
                break;
            case AnswerKindContentEnumeration:
                guidance.Add("Show a grouped enumeration and avoid declaring a single winner unless the user asked for one.");
                break;
            case AnswerKindClarificationRequired:
                guidance.Add("Show the clarification question and options before deeper execution.");
                break;
            default:
                guidance.Add("Render a direct answer while preserving stated constraints.");
                break;
        }
        if(queryContract.RequiredTerms.Count>0)guidance.Add($"Preserve required terms: {string.Join("; ",queryContract.RequiredTerms)}.");
        if(queryContract.ExcludedTerms.Count>0)guidance.Add($"Exclude terms: {string.Join("; ",queryContract.ExcludedTerms)}.");
        if(queryContract.SafetyRiskCode is "MEDIUM" or "HIGH")guidance.Add("Include safety-aware caveats and avoid unsafe operational instructions.");
        return guidance.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    // V2.8.6 post-competition identity dedup: two ranked candidates whose CORE tokens are identical
    // ("Overland Park, Kansas" / "Overland Park") are one entity and must hold one ranking position.
    // The stronger instance (better rank; richer name on ties) survives; scores are never altered.
    // V3.3.1 qualifier-echo merge: the comma strip in CanonicalTokens loses the qualifier tokens, so
    // "White Beach, Boracay Island" ([White, Beach]) and "Boracay" ([Boracay]) never key-match even
    // though the LLM echoed one entity twice. Bare containment would be WRONG ("Palawan" is not
    // "Nacpan Beach, Palawan"), so the merge additionally requires an IDENTICAL detail payload —
    // the LLM describing two rows with the exact same text is deterministic evidence of an echo,
    // while genuinely distinct places always describe differently.
    private static IReadOnlyCollection<WideCandidateDto> DeduplicateCandidatesByCanonicalTokens(IReadOnlyCollection<WideCandidateDto> candidates,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge)
    {
        var survivors=new List<WideCandidateDto>();
        var seen=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        foreach(var candidate in candidates.OrderBy(candidate=>candidate.IsConstraintViolation).ThenBy(candidate=>candidate.RankNumber))
        {
            var key=string.Join(' ',CanonicalTokens(candidate.DisplayName));
            if(key.Length==0)key=candidate.DisplayName.Trim();
            if(seen.TryGetValue(key,out var existingIndex))
            {
                // Same entity: keep the earlier (better-ranked) instance but prefer the richer name form.
                if(candidate.DisplayName.Length>survivors[existingIndex].DisplayName.Length)
                    survivors[existingIndex]=survivors[existingIndex] with{DisplayName=candidate.DisplayName,Detail=survivors[existingIndex].Detail??candidate.Detail};
                continue;
            }
            var echoIndex=survivors.FindIndex(existing=>IsQualifierEcho(existing,candidate));
            if(echoIndex>=0)
            {
                if(candidate.DisplayName.Length>survivors[echoIndex].DisplayName.Length)
                    survivors[echoIndex]=survivors[echoIndex] with{DisplayName=candidate.DisplayName,Detail=survivors[echoIndex].Detail??candidate.Detail};
                continue;
            }
            // V3.4.3 subset-alias merge: within ONE ranked competition, a candidate whose significant
            // tokens are a subset of another ranked candidate's is the same entity echoed under a
            // shorter/partial name ("University of the Philippines" vs "University of the Philippines
            // Diliman", "Santo Tomas" vs "University of Santo Tomas", "Manila University" vs "Ateneo de
            // Manila University"). Connective tokens (of/the/de/la...) never count as distinguishing.
            // Guardrail: the shorter side must have >=2 significant tokens - single-token names
            // ("Manila", "Palawan") are legitimate standalone entities and are never subset-merged.
            // The better-ranked instance keeps the position; scores are never altered.
            // V3.6.1 guardrail: a subset name independently attested by the evidence (mentioned in a
            // snippet with all occurrences of the longer form removed) is a DISTINCT sibling entity
            // ("Rolling Hills" vs "Rolling Hills Estates") and is never subset-merged.
            var subsetIndex=survivors.FindIndex(existing=>!existing.IsConstraintViolation&&!candidate.IsConstraintViolation
                &&IsSubsetAlias(existing.DisplayName,candidate.DisplayName)
                &&!SubsetNameIndependentlyAttested(existing.DisplayName,candidate.DisplayName,externalKnowledge));
            if(subsetIndex>=0)
            {
                if(candidate.DisplayName.Length>survivors[subsetIndex].DisplayName.Length)
                    survivors[subsetIndex]=survivors[subsetIndex] with{DisplayName=candidate.DisplayName,Detail=survivors[subsetIndex].Detail??candidate.Detail};
                continue;
            }
            seen[key]=survivors.Count;
            survivors.Add(candidate);
        }
        return survivors.Select((candidate,index)=>candidate with{RankNumber=index+1}).ToArray();
    }

    // V3.3.1: full-name tokens (qualifiers INCLUDED, noise suffixes removed) — the containment side
    // of the qualifier-echo test. CanonicalTokens intentionally strips qualifiers for keying; this
    // variant keeps them so "Boracay" can be found inside "White Beach, Boracay Island".
    private static string[] FullNameTokens(string name)=>
        name.Split([' ',',','(',')','-','—','/'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Where(token=>!CanonicalNoiseSuffixes.Contains(token,StringComparer.OrdinalIgnoreCase))
            .ToArray();

    private static bool IsQualifierEcho(WideCandidateDto first,WideCandidateDto second)
    {
        // Identical detail payload is mandatory: containment alone falsely merges a region into a
        // place within it. Blank details carry no echo evidence — never merge on them.
        if(string.IsNullOrWhiteSpace(first.Detail)||string.IsNullOrWhiteSpace(second.Detail))return false;
        if(!string.Equals(first.Detail.Trim(),second.Detail.Trim(),StringComparison.OrdinalIgnoreCase))return false;
        var firstTokens=FullNameTokens(first.DisplayName);
        var secondTokens=FullNameTokens(second.DisplayName);
        if(firstTokens.Length==0||secondTokens.Length==0)return false;
        var(shorter,longer)=firstTokens.Length<=secondTokens.Length?(firstTokens,secondTokens):(secondTokens,firstTokens);
        return shorter.All(token=>longer.Contains(token,StringComparer.OrdinalIgnoreCase));
    }

    // V3.4.3: connective/filler tokens that never distinguish one institution/place from another.
    private static readonly HashSet<string> SubsetConnectiveTokens=new(StringComparer.OrdinalIgnoreCase)
    {
        "of","the","de","del","della","la","le","los","las","da","di","du","van","von","and","&","at","in","for","on","with"
    };

    // V3.4.3 subset-alias test: after removing connective and legal-suffix noise, if one name's
    // significant tokens are a strict subset of the other's, the shorter form is a partial echo of
    // the longer entity within the SAME ranked list. Requires >=2 significant tokens on the shorter
    // side so standalone single-token entities are never merged into larger names that contain them.
    private static bool IsSubsetAlias(string first,string second)
    {
        var firstTokens=SignificantNameTokens(first);
        var secondTokens=SignificantNameTokens(second);
        if(firstTokens.Count<2||secondTokens.Count<2)return false;
        if(firstTokens.Count==secondTokens.Count)return false;
        var(shorter,longer)=firstTokens.Count<secondTokens.Count?(firstTokens,secondTokens):(secondTokens,firstTokens);
        return shorter.All(longer.Contains);
    }

    private static HashSet<string> SignificantNameTokens(string name)=>
        FullNameTokens(name)
            .Where(token=>!SubsetConnectiveTokens.Contains(token))
            .Select(token=>token.ToLowerInvariant())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    // V3.6.1: orders the pair by significant-token count and checks whether the shorter name is
    // mentioned on its own in the external evidence — if so the two names are sibling entities and
    // the subset-alias merge must not fire.
    private static bool SubsetNameIndependentlyAttested(string first,string second,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge)
    {
        var(shorterName,longerName)=SignificantNameTokens(first).Count<=SignificantNameTokens(second).Count?(first,second):(second,first);
        return HasExclusiveMention(shorterName,longerName,externalKnowledge);
    }

    private static IReadOnlyCollection<WideCandidateDto> ReweightCandidatesByClarificationAnswer(IReadOnlyCollection<WideCandidateDto> candidates,string answer,decimal boost)
    {
        var answerTokens=answer.Split([' ',',',';','/','(',')','-','—',':','.'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Where(token=>token.Length>2).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(answerTokens.Count==0)return candidates;
        var reweighted=candidates.Select(candidate=>
        {
            if(candidate.IsConstraintViolation)return(Candidate:candidate,Score:candidate.CompositeScore);
            var candidateText=$"{candidate.DisplayName} {candidate.Detail}";
            var candidateTokens=candidateText.Split([' ',',',';','/','(',')','-','—',':','.'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
                .Where(token=>token.Length>2).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if(candidateTokens.Count==0)return(Candidate:candidate,Score:candidate.CompositeScore);
            var overlap=Math.Clamp((decimal)answerTokens.Count(candidateTokens.Contains)/answerTokens.Count,0,1);
            return(Candidate:candidate,Score:Math.Clamp(candidate.CompositeScore*(1m+boost*overlap),0,1));
        }).ToArray();
        return reweighted
            .OrderBy(entry=>entry.Candidate.IsConstraintViolation)
            .ThenByDescending(entry=>entry.Score)
            .Select((entry,index)=>entry.Candidate with{CompositeScore=entry.Score,RankNumber=index+1})
            .ToArray();
    }

    private static decimal ComputeAggregateConfidence(IReadOnlyCollection<WideBranchRecord> survivors)
    {
        if(survivors.Count==0)return 0m;
        // Evidence-weighted: grounded branches with evidence pull confidence up; interpretive branches contribute their raw confidence.
        var weighted=survivors.Select(branch=>branch.GroundingStatusCode=="GROUNDED"&&branch.EvidenceCount>0?Math.Clamp(branch.Confidence+.15m,0,1):branch.Confidence);
        return Math.Clamp(weighted.Max(),0,1);
    }

    // V2.6 deterministic ranking stability across information-round snapshots (no LLM involvement).
    // WinnerStability: fraction of consecutive snapshot pairs where the #1 candidate did not change.
    // TopKStability: average Jaccard overlap of the top-3 sets between consecutive snapshots.
    private static (decimal? Winner,decimal? TopK) ComputeRankingStability(IReadOnlyList<string[]> rankings)
    {
        var usable=rankings.Where(ranking=>ranking.Length>0).ToArray();
        if(usable.Length<2)return(null,null);
        var winnerStable=0;var topKOverlap=0m;var pairs=usable.Length-1;
        for(var index=1;index<usable.Length;index++)
        {
            if(string.Equals(usable[index-1][0],usable[index][0],StringComparison.OrdinalIgnoreCase))winnerStable++;
            var previousTop=usable[index-1].Take(3).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var currentTop=usable[index].Take(3).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var union=previousTop.Union(currentTop,StringComparer.OrdinalIgnoreCase).Count();
            topKOverlap+=union==0?1m:(decimal)previousTop.Intersect(currentTop,StringComparer.OrdinalIgnoreCase).Count()/union;
        }
        return(Math.Clamp((decimal)winnerStable/pairs,0,1),Math.Clamp(topKOverlap/pairs,0,1));
    }

    private static WideBranchDto ToDto(WideBranchRecord branch)=>new(branch.WideBranchId,branch.ParentWideBranchId,branch.LevelNumber,branch.BranchCode,branch.DisplayName,branch.Interpretation,branch.CapabilityCode,branch.SearchText,branch.GroundingStatusCode,branch.EvidenceCount,branch.Confidence,branch.ContinueNarrowing,branch.StopReason,branch.IsEliminated,branch.EliminationReason,branch.SortOrder){BranchStateCode=branch.BranchStateCode,InterpretationPrior=branch.InterpretationPrior,EvidenceSupport=branch.EvidenceSupport,PoloxiConfidence=branch.PoloxiConfidence,SemanticTypeCode=branch.SemanticTypeCode};

    private static IReadOnlyCollection<WideAmbiguityGroupDto> BuildAmbiguityGroups(IReadOnlyCollection<WideBranchRecord> branches,IReadOnlyCollection<WideInterpretiveResultDto> interpretiveResults,IReadOnlyCollection<WideCandidateDto> candidates,IReadOnlyCollection<PoloxiEvidenceDto> evidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,WideQueryContract? queryContract)
    {
        var branchArray=branches.Where(branch=>!branch.IsEliminated).OrderBy(branch=>branch.LevelNumber).ThenBy(branch=>branch.SortOrder).ToArray();
        var roots=branchArray.Where(branch=>branch.LevelNumber==1).OrderByDescending(branch=>branch.PoloxiConfidence).ThenBy(branch=>branch.SortOrder).ToArray();
        if(roots.Length==0)return [];
        var byParent=branchArray.Where(branch=>branch.ParentWideBranchId is not null).ToLookup(branch=>branch.ParentWideBranchId!.Value);
        var resultNames=interpretiveResults.ToLookup(result=>result.BranchDisplayName,StringComparer.OrdinalIgnoreCase);
        var groups=new List<WideAmbiguityGroupDto>();
        foreach(var root in roots)
        {
            var groupBranches=GetDescendants(root,byParent).Prepend(root).ToArray();
            var groupBranchIds=groupBranches.Select(branch=>branch.WideBranchId).ToHashSet();
            var groupBranchNames=groupBranches.Select(branch=>branch.DisplayName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var groupResults=interpretiveResults.Where(result=>groupBranchNames.Contains(result.BranchDisplayName)).ToArray();
            if(groupResults.Length==0&&resultNames[root.DisplayName].Any())groupResults=resultNames[root.DisplayName].ToArray();
            var groupCandidateNames=groupResults.SelectMany(result=>result.Items.Select(item=>item.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var groupCandidates=groupCandidateNames.Count==0?[]:candidates.Where(candidate=>groupCandidateNames.Contains(candidate.DisplayName)).ToArray();
            var groupEvidence=evidence.Where(item=>groupBranchIds.Contains(item.HierarchyBranchId)).ToArray();
            var groupKnowledge=externalKnowledge.Where(snippet=>groupBranchNames.Any(name=>$"{snippet.Query} {snippet.Title} {snippet.Snippet}".Contains(name,StringComparison.OrdinalIgnoreCase))).ToArray();
            var summary=groupResults.FirstOrDefault()?.Interpretation??root.Interpretation;
            groups.Add(new(root.WideBranchId,root.BranchCode,root.DisplayName,root.Interpretation,root.PoloxiConfidence,ResolveGroupSafetyRisk(queryContract,root),queryContract?.AnswerKind,queryContract?.CandidateKind)
            {
                InterpretationPrior=root.InterpretationPrior,
                EvidenceSupport=root.EvidenceSupport,
                PoloxiConfidence=root.PoloxiConfidence,
                Branches=groupBranches.Select(ToDto).ToArray(),
                InterpretiveResults=groupResults,
                Candidates=groupCandidates,
                Evidence=groupEvidence,
                ExternalKnowledge=groupKnowledge,
                Summary=summary
            });
        }
        return groups;
    }

    private static IEnumerable<WideBranchRecord> GetDescendants(WideBranchRecord parent,ILookup<Guid,WideBranchRecord> byParent)
    {
        foreach(var child in byParent[parent.WideBranchId].OrderBy(branch=>branch.LevelNumber).ThenBy(branch=>branch.SortOrder))
        {
            yield return child;
            foreach(var descendant in GetDescendants(child,byParent))yield return descendant;
        }
    }

    private static string? ResolveGroupSafetyRisk(WideQueryContract? queryContract,WideBranchRecord root)
    {
        if(queryContract?.SafetyRiskCode is{Length:>0}risk&&risk!="NONE")return risk;
        var text=$"{root.DisplayName} {root.Interpretation}";
        return ContainsAny(text,["structural","civil","bridge repair","collapse","hazard","dental","medical","electrical","fire","gas"])?"POSSIBLE":queryContract?.SafetyRiskCode;
    }

    // Accept only well-formed absolute https URLs so hallucinated or unsafe links never reach the UI.
    private static WideExternalReferenceDto[] MapExternalReferences(WideAnswerProposal answer)=>
        (answer.ExternalReferences??[]).Where(reference=>Uri.TryCreate(reference.Url,UriKind.Absolute,out var uri)&&uri.Scheme==Uri.UriSchemeHttps)
            .Take(6).Select(reference=>new WideExternalReferenceDto(reference.Title.Trim(),reference.Url.Trim(),reference.Source.Trim(),reference.Summary.Trim(),reference.BranchDisplayName.Trim())).ToArray();

    private static decimal? NormalizeScore(decimal? score)=>score is null?null:Math.Clamp(score.Value,0,1);

    // Interpretive result sets answered by the LLM for the interpretive narrowing paths, arranged with
    // Level 1 branches first, then by POLOXI branch-support confidence (prior + evidence, highest first).
    private static WideInterpretiveResultDto[] MapInterpretiveResults(WideAnswerProposal answer,IReadOnlyCollection<WideBranchRecord> survivors,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge)
    {
        var interpretive=survivors.Where(branch=>branch.GroundingStatusCode=="INTERPRETIVE"||branch.BranchStateCode==WideBranchStates.Dormant).GroupBy(branch=>NormalizeBranchDisplayKey(branch.DisplayName),StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key,group=>(Level:group.Min(branch=>branch.LevelNumber),Confidence:group.Max(branch=>branch.PoloxiConfidence>0?branch.PoloxiConfidence:branch.Confidence),StateCode:group.OrderByDescending(branch=>branch.PoloxiConfidence>0?branch.PoloxiConfidence:branch.Confidence).First().BranchStateCode),StringComparer.OrdinalIgnoreCase);
        // The answer LLM may echo a slightly different display name than the stored branch name; without a
        // tolerant lookup every card silently falls back to the single shared answer confidence, which makes
        // all interpretive scores identical. Exact normalized match first, then the most specific containment
        // match so a child like "Best Affordability: Housing Costs" does not resolve to parent "Best Affordability".
        (int Level,decimal Confidence,string StateCode)? Resolve(string displayName)
        {
            var key=NormalizeBranchDisplayKey(displayName);
            if(interpretive.TryGetValue(key,out var exact))return exact;
            var partial=interpretive
                .Where(entry=>entry.Key.Contains(key,StringComparison.OrdinalIgnoreCase)||key.Contains(entry.Key,StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(entry=>entry.Key.Length)
                .ThenByDescending(entry=>entry.Value.Level)
                .FirstOrDefault();
            return partial.Key is null?null:partial.Value;
        }
        var externallyGrounded=externalKnowledge.Count>0;
        return (answer.InterpretiveResults??[]).Where(result=>result.Items is{Count:>0})
            .Select(result=>new WideInterpretiveResultDto(result.BranchDisplayName.Trim(),result.Interpretation.Trim(),Resolve(result.BranchDisplayName.Trim())?.Confidence??Math.Clamp(answer.Confidence,0,1),result.Items.OrderBy(item=>item.RankNumber).Select((item,index)=>new WideInterpretiveResultItemDto(item.RankNumber>0?item.RankNumber:index+1,item.Name.Trim(),item.Detail.Trim(),NormalizeScore(item.Score))).ToArray()){DataVolatility=result.DataVolatility?.Trim().ToUpperInvariant()=="TIME_SENSITIVE"?"TIME_SENSITIVE":"STABLE",IsExternallyGrounded=externallyGrounded,BranchStateCode=Resolve(result.BranchDisplayName.Trim())?.StateCode??WideBranchStates.Active,LevelNumber=Resolve(result.BranchDisplayName.Trim())?.Level??0})
            .OrderBy(result=>Resolve(result.BranchDisplayName)?.Level??int.MaxValue)
            .ThenByDescending(result=>result.Confidence).ToArray();
    }

    private static string BuildCatalog(IReadOnlyCollection<PoloxiCapabilityDto> capabilities)=>capabilities.Count==0?"(none — knowledge-only mode; capabilityCode must always be null)":string.Join('\n',capabilities.Select(capability=>$"{capability.CapabilityCode}: {capability.Description}; approved terms: {string.Join(", ",capability.ApprovedTerms)}; entity: {capability.EntityTypeCode}"));

    // -----------------------------------------------------------------------------------------------
    // V2.1 Query Contract Engine: separate what the query FIXES (hard constraints, entity type, output
    // requirements) from what actually needs disambiguation (ambiguous concepts). Fail-soft: any LLM
    // failure returns null and the pipeline degrades to V2 whole-query branching.
    // -----------------------------------------------------------------------------------------------
    private async Task<WideQueryContract?> ExtractQueryContractAsync(WideSearchRequest request,WideConfiguration configuration,CancellationToken cancellationToken)
    {
        try
        {
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_INTENT",
                await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.WideQueryContract,cancellationToken),
                $"Question: {request.Query}",
                QueryContractSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_QUERY_CONTRACT",null,request.CorrelationId,"Intelligent Search Wide"),ModelOverride(request),cancellationToken);
            var proposal=JsonSerializer.Deserialize<WideQueryContractProposal>(result.Content,JsonOptions);
            if(proposal is null)return null;
            var contract=new WideQueryContract(proposal.EntityType,proposal.GeographicConstraint,proposal.RequestedCount,proposal.RankingConcept,proposal.HardConstraints??[],proposal.AmbiguousConcepts??[],proposal.OutputRequirements??[])
            {
                AnswerKind=NormalizeAnswerKind(configuration,proposal.AnswerKind),
                CandidateKind=NormalizeCandidateKind(proposal.CandidateKind),
                Intent=NormalizeNullable(proposal.Intent),
                TargetObject=NormalizeNullable(proposal.TargetObject),
                RequiredTerms=NormalizeTermList(proposal.RequiredTerms),
                ExcludedTerms=NormalizeTermList(proposal.ExcludedTerms),
                AmbiguousTerms=NormalizeTermList(proposal.AmbiguousTerms),
                SafetyRiskCode=NormalizeSafetyRiskCode(proposal.SafetyRiskCode,proposal.IsSafetySensitive),
                OutputShape=NormalizeNullable(proposal.OutputShape),
                RequiresClarification=proposal.RequiresClarification,
                ClarificationQuestion=NormalizeNullable(proposal.ClarificationQuestion),
                ClarificationTarget=NormalizeNullable(proposal.ClarificationTarget),
                ClarificationOptions=proposal.ClarificationOptions?.Where(option=>!string.IsNullOrWhiteSpace(option)).Select(option=>option.Trim()).Take(6).ToArray()??[],
                IsSafetySensitive=proposal.IsSafetySensitive
            };
            contract=ApplyStructuralQueryContract(contract,request.Query,request.MaximumResults);
            return ApplySelectionUpgradeGuard(configuration,contract,request.Query);
        }
        catch(Exception)when(!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    // -----------------------------------------------------------------------------------------------
    // V2.2 Information-Directed Exploration helpers.
    // The LLM proposes. Evidence informs. POLOXI decides — entropy and information gain are always
    // calculated deterministically in POLOXI code, never by the LLM.
    // -----------------------------------------------------------------------------------------------

    // Deterministic Shannon entropy over the eligible (ACTIVE/SECONDARY) ALTERNATIVE branch belief
    // distribution. V2.3: DIMENSION branches are jointly valid criteria, NOT competing hypotheses —
    // they never participate in winner-take-all entropy. PRUNED and DORMANT never inflate entropy.
    private static WideEntropyResult ComputeEntropy(IReadOnlyCollection<WideBranchRecord> branches)
    {
        var eligible=branches.Where(branch=>branch.BranchStateCode is WideBranchStates.Active or WideBranchStates.Secondary
                &&branch.SemanticTypeCode==WideBranchSemanticTypes.Alternative)
            .Select(branch=>Math.Max(branch.PoloxiConfidence,.0001m)).ToArray();
        return EntropyFromValues(eligible,WideEntropyBases.Branch);
    }

    // V2.3 candidate-signal entropy: when the hierarchy is dimension-dominated, uncertainty means
    // "which candidate wins", so entropy is measured over the deterministic candidate-signal
    // distribution (mention-weighted evidence support), never over complementary dimensions.
    private static WideEntropyResult ComputeCandidateEntropy(IReadOnlyCollection<string> candidateNames,IReadOnlyCollection<PoloxiEvidenceDto> evidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        var signals=ComputeCandidateSignals(candidateNames,evidence,knowledge).Values.Select(value=>Math.Max(value,.0001m)).ToArray();
        return EntropyFromValues(signals,WideEntropyBases.Candidate);
    }

    // V3.6 Fix A — competition-outcome entropy: mention-signal entropy saturates near 100% for
    // well-known candidates (every city is mentioned everywhere, so signals are nearly uniform) and
    // says "all candidates have evidence" rather than "we do not know who wins". After the Candidate
    // Competition produces evidence-weighted quality scores, the true remaining uncertainty is the
    // entropy of the WINNER-probability distribution. Quality scores are converted to winner
    // probabilities via softmax with a sharpening temperature so real quality gaps (75% vs 52%)
    // translate into genuinely lower measured uncertainty. Deterministic and zero-LLM.
    private static WideEntropyResult ComputeCompetitionOutcomeEntropy(IReadOnlyCollection<WideCandidateDto> candidates)
    {
        const double Temperature=.1;
        var qualities=candidates.Where(candidate=>!candidate.IsConstraintViolation).Select(candidate=>(double)candidate.QualityScore).ToArray();
        if(qualities.Length<2)return new(0m,0m,0m,qualities.Length){EntropyBasisCode=WideEntropyBases.Candidate};
        var max=qualities.Max();
        var weights=qualities.Select(quality=>Math.Max((decimal)Math.Exp((quality-max)/Temperature),.0000001m)).ToArray();
        return EntropyFromValues(weights,WideEntropyBases.Candidate);
    }

    // V2.3 basis selection: competing ALTERNATIVE branches use branch entropy; a dimension-dominated
    // hierarchy (fewer than 2 competing alternatives) switches to candidate-competition entropy.
    // V2.5 regression fix: ranking/recommendation queries (the contract fixes a rankingConcept or a
    // requestedCount) are ALWAYS a "which candidate wins" problem — their root branches are
    // complementary dimensions even when the proposer mislabels them ALTERNATIVE — so the CANDIDATE
    // basis takes precedence whenever a competitive candidate universe exists.
    // Before any candidates are known, uncertainty is reported as maximal on the CANDIDATE basis so
    // information rounds keep investigating instead of falsely reporting resolution.
    // V3.1 answer-kind routing: the FIRST LLM reply (the query contract) decides the task type the
    // whole pipeline commits to. CONTENT_ENUMERATION means the requested items are pieces of content
    // (questions, tips, steps, examples), not named entities — the Candidate Competition and the
    // Output Contract are category errors for such queries. The value is LLM-proposed but validated
    // to a strict enumeration; anything unrecognized degrades to null (pre-V3.1 heuristics).
    private const string AnswerKindEntityRanking="ENTITY_RANKING";
    private const string AnswerKindContentEnumeration="CONTENT_ENUMERATION";
    private const string AnswerKindSingleAnswer="SINGLE_ANSWER";
    private const string AnswerKindTechnicalRecommendation="TECHNICAL_RECOMMENDATION";
    private const string AnswerKindDiagnosticProcedure="DIAGNOSTIC_PROCEDURE";
    private const string AnswerKindClarificationRequired="CLARIFICATION_REQUIRED";
    // V3.17 RESOLUTION: compute/adjudicate/decide a specific outcome (a value, an amount, a reason, a
    // determination) rather than rank named entities. When candidate competition legitimately finds
    // nothing to rank, POLOXI must still deliver a structured determinacy verdict + blocking inputs +
    // best-supported reason + citations, not bare interpretive prose.
    private const string AnswerKindResolution="RESOLUTION";
    private const string CandidateKindNamedEntity="NAMED_ENTITY";
    private const string CandidateKindActionableSolution="ACTIONABLE_SOLUTION";
    private const string CandidateKindDiagnosticStep="DIAGNOSTIC_STEP";
    private const string CandidateKindProcedureStep="PROCEDURE_STEP";

    // V3.3: answer kinds are recognized against the POLOXI.AnswerKind lookup table (DB is the source
    // of truth) so new kinds (COMPARISON, YES_NO, ...) can be added without recompiling. When the
    // table is empty the pre-V3.3 compiled constants remain as fail-safe fallbacks. Anything not
    // recognized degrades to null (full pipeline, thoroughness over speed).
    private static string? NormalizeAnswerKind(WideConfiguration configuration,string? value)
    {
        var normalized=value?.Trim().ToUpperInvariant();
        if(string.IsNullOrEmpty(normalized))return null;
        var builtIn=normalized switch
        {
            AnswerKindEntityRanking=>AnswerKindEntityRanking,
            AnswerKindContentEnumeration=>AnswerKindContentEnumeration,
            AnswerKindSingleAnswer=>AnswerKindSingleAnswer,
            AnswerKindTechnicalRecommendation=>AnswerKindTechnicalRecommendation,
            AnswerKindDiagnosticProcedure=>AnswerKindDiagnosticProcedure,
            AnswerKindClarificationRequired=>AnswerKindClarificationRequired,
            AnswerKindResolution=>AnswerKindResolution,
            _=>null
        };
        if(builtIn is not null)return builtIn;
        if(configuration.AnswerKinds.Count>0)
            return configuration.AnswerKinds.FirstOrDefault(kind=>kind.AnswerKindCode==normalized)?.AnswerKindCode;
        return null;
    }

    private static string? NormalizeCandidateKind(string? value)
    {
        var normalized=value?.Trim().ToUpperInvariant();
        if(string.IsNullOrEmpty(normalized))return null;
        return normalized switch
        {
            CandidateKindNamedEntity=>CandidateKindNamedEntity,
            CandidateKindActionableSolution=>CandidateKindActionableSolution,
            CandidateKindDiagnosticStep=>CandidateKindDiagnosticStep,
            CandidateKindProcedureStep=>CandidateKindProcedureStep,
            _=>null
        };
    }

    private static string? NormalizeNullable(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static IReadOnlyCollection<string> NormalizeTermList(IReadOnlyCollection<string>? terms,int maxItems=10)=>terms?.Select(NormalizeTerm)
        .Where(term=>term.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).Take(maxItems).ToArray()??[];

    private static string NormalizeTerm(string? value)
    {
        if(string.IsNullOrWhiteSpace(value))return string.Empty;
        var term=value.Trim(' ','"','\'','`',':','-','–','—');
        term=System.Text.RegularExpressions.Regex.Replace(term,@"\s+"," ").Trim();
        term=System.Text.RegularExpressions.Regex.Replace(term,@"^(?:a|an|the)\s+",string.Empty,System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant).Trim();
        return term.Length>120?term[..120].Trim():term;
    }

    private static string? NormalizeSafetyRiskCode(string? safetyRiskCode,bool isSafetySensitive)
    {
        var normalized=NormalizeNullable(safetyRiskCode)?.ToUpperInvariant().Replace(' ','_');
        if(normalized is "NONE" or "LOW" or "MEDIUM" or "HIGH")return normalized;
        return isSafetySensitive?"MEDIUM":"NONE";
    }

    private static WideQueryContract ApplyStructuralQueryContract(WideQueryContract contract,string query,int maximumResults)
    {
        var requestedCount=contract.RequestedCount??ExtractRequestedCount(query);
        requestedCount=requestedCount is null?null:Math.Clamp(requestedCount.Value,1,Math.Clamp(maximumResults,1,100));
        var requiredTerms=MergeTerms(contract.RequiredTerms,ExtractTerms(RequiredTermPattern,query));
        var excludedTerms=MergeTerms(contract.ExcludedTerms,ExtractTerms(ExcludedTermPattern,query));
        var ambiguousTerms=MergeTerms(contract.AmbiguousTerms,contract.AmbiguousConcepts);
        return contract with
        {
            RequestedCount=requestedCount,
            RequiredTerms=requiredTerms,
            ExcludedTerms=excludedTerms,
            AmbiguousTerms=ambiguousTerms,
            Intent=NormalizeNullable(contract.Intent)??InferIntent(query,contract),
            TargetObject=NormalizeNullable(contract.TargetObject)??NormalizeNullable(contract.EntityType),
            SafetyRiskCode=NormalizeSafetyRiskCode(contract.SafetyRiskCode,contract.IsSafetySensitive),
            OutputShape=NormalizeNullable(contract.OutputShape)??InferOutputShape(query,requestedCount)
        };
    }

    private static IReadOnlyCollection<string> MergeTerms(params IReadOnlyCollection<string>[] termSets)=>termSets.SelectMany(term=>term)
        .Select(NormalizeTerm).Where(term=>term.Length>0).Distinct(StringComparer.OrdinalIgnoreCase).Take(12).ToArray();

    private static IReadOnlyCollection<string> ExtractTerms(System.Text.RegularExpressions.Regex pattern,string query)
    {
        var terms=new List<string>();
        foreach(System.Text.RegularExpressions.Match match in pattern.Matches(query))
        {
            var clause=match.Groups["term"].Value;
            foreach(var part in System.Text.RegularExpressions.Regex.Split(clause,@"\s+(?:and|or)\s+|/|\\"))
            {
                var term=NormalizeTerm(System.Text.RegularExpressions.Regex.Replace(part,@"\b(?:but|while|unless|except)\b.*$",string.Empty,System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant));
                if(term.Length>0)terms.Add(term);
            }
        }
        return terms.Distinct(StringComparer.OrdinalIgnoreCase).Take(8).ToArray();
    }

    private static int? ExtractRequestedCount(string query)
    {
        var match=RequestedCountPattern.Match(query);
        if(!match.Success)return null;
        var value=match.Groups["count"].Value;
        if(int.TryParse(value,out var count))return count;
        return value.ToLowerInvariant() switch
        {
            "one"=>1,"two"=>2,"three"=>3,"four"=>4,"five"=>5,"six"=>6,"seven"=>7,"eight"=>8,"nine"=>9,"ten"=>10,
            "eleven"=>11,"twelve"=>12,"fifteen"=>15,"twenty"=>20,
            _=>null
        };
    }

    private static string? InferIntent(string query,WideQueryContract contract)
    {
        if(RepairIntentPattern.IsMatch(query))return "diagnose_or_fix";
        if(!string.IsNullOrWhiteSpace(contract.RankingConcept)||SelectionVerbPattern.IsMatch(query)||RequestedCountPattern.IsMatch(query))return "rank_or_select";
        if(EnumerationIntentPattern.IsMatch(query))return "enumerate";
        return null;
    }

    private static string InferOutputShape(string query,int? requestedCount)
    {
        if(RepairIntentPattern.IsMatch(query))return "steps";
        if(requestedCount is >1||EnumerationIntentPattern.IsMatch(query))return "ranked_list";
        if(query.Contains("compare",StringComparison.OrdinalIgnoreCase)||query.Contains("versus",StringComparison.OrdinalIgnoreCase)||query.Contains(" vs ",StringComparison.OrdinalIgnoreCase))return "comparison";
        return "answer";
    }

    private static WideAnswerKindDefinition? FindAnswerKind(WideConfiguration configuration,string? answerKindCode)=>
        string.IsNullOrWhiteSpace(answerKindCode)?null:configuration.AnswerKinds.FirstOrDefault(kind=>kind.AnswerKindCode.Equals(answerKindCode.Trim(),StringComparison.OrdinalIgnoreCase));

    // V3.7 selection-upgrade guard: "choose/select/pick exactly one named entity" is a ranking with
    // requestedCount=1, NOT a single factual answer. Any model (mini or premium) reliably misreads
    // "select exactly one" as SINGLE_ANSWER, which turns off the Candidate Competition and funnels the
    // run to an ungrounded interpretive pick. When the contract itself proves entity competition (an
    // entityType plus a rankingConcept or an explicit selection verb in the query), deterministically
    // upgrade to ENTITY_RANKING. Never fires for factual/definitional questions (no entityType) and
    // never downgrades a kind — it only ever ADDS rigor.
    private static readonly System.Text.RegularExpressions.Regex SelectionVerbPattern=new(@"\b(choose|select|pick)\b|\bwhich\s+one\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex RepairIntentPattern=new(@"\b(how\s+do\s+i\s+)?(fix|repair|troubleshoot|resolve|diagnose|optimi[sz]e|reduce|prevent)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex EnumerationIntentPattern=new(@"\b(list|enumerate|types?|examples?|categories|top\s+\d+|top\s+ten|options)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex RequestedCountPattern=new(@"\b(?:top|best|first)\s+(?<count>\d{1,3}|one|two|three|four|five|six|seven|eight|nine|ten|eleven|twelve|fifteen|twenty)\b|\b(?<count>\d{1,3})\s+(?:best|options|choices|recommendations|results|items|candidates)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.Compiled|System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static readonly System.Text.RegularExpressions.Regex RequiredTermPattern=new(@"\b(?:must|should|needs?\s+to|has\s+to|required\s+to)\s+(?:include|contain|have|support|use|be|with)\s+(?<term>[^,.;?]+)",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.Compiled|System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static readonly System.Text.RegularExpressions.Regex ExcludedTermPattern=new(@"\b(?:exclude|excluding|without|avoid|except|no|not)\s+(?<term>[^,.;?]+)",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.Compiled|System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    private static readonly System.Text.RegularExpressions.Regex AmbiguousBridgePattern=new(@"\bbridge\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.Compiled);
    private static readonly System.Text.RegularExpressions.Regex BridgeDomainQualifierPattern=new(@"\b(road|highway|structural|civil|network|ethernet|software|integration|api|dental|tooth|card|game)\s+bridge\b|\bbridge\s+(network|interface|adapter|api|integration|dental|tooth|repair|structure|deck|span)\b",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.Compiled);
    private static WideQueryContract ApplySelectionUpgradeGuard(WideConfiguration configuration,WideQueryContract contract,string query)
    {
        if(!string.Equals(contract.AnswerKind,AnswerKindSingleAnswer,StringComparison.OrdinalIgnoreCase))return contract;
        if(string.IsNullOrWhiteSpace(contract.EntityType))return contract;
        var hasSelectionIntent=!string.IsNullOrWhiteSpace(contract.RankingConcept)||SelectionVerbPattern.IsMatch(query);
        if(!hasSelectionIntent)return contract;
        // Only upgrade to a kind the tenant actually recognizes (DB lookup first, compiled fallback).
        if(NormalizeAnswerKind(configuration,AnswerKindEntityRanking)is not{Length:>0}rankingKind)return contract;
        return contract with{AnswerKind=rankingKind,RequestedCount=contract.RequestedCount??1};
    }

    private static WideQueryContract? RefineQueryContractForAmbiguity(WideConfiguration configuration,WideQueryContract? contract,string query)
    {
        var refined=contract??new(null,null,null,null,[],[],[]);
        var isRepairOrOptimization=RepairIntentPattern.IsMatch(query);
        if(isRepairOrOptimization&&string.Equals(refined.AnswerKind,AnswerKindContentEnumeration,StringComparison.OrdinalIgnoreCase)&&!EnumerationIntentPattern.IsMatch(query))
            refined=refined with{AnswerKind=NormalizeAnswerKind(configuration,AnswerKindDiagnosticProcedure)??AnswerKindDiagnosticProcedure};
        if(isRepairOrOptimization&&string.IsNullOrWhiteSpace(refined.CandidateKind))
            refined=refined with{CandidateKind=query.Contains("how",StringComparison.OrdinalIgnoreCase)?CandidateKindProcedureStep:CandidateKindActionableSolution};
        if(AmbiguousBridgePattern.IsMatch(query)&&!BridgeDomainQualifierPattern.IsMatch(query)&&!refined.RequiresClarification)
        {
            refined=refined with
            {
                AnswerKind=NormalizeAnswerKind(configuration,AnswerKindClarificationRequired)??AnswerKindClarificationRequired,
                CandidateKind=CandidateKindDiagnosticStep,
                RequiresClarification=true,
                ClarificationTarget="bridge meaning",
                ClarificationQuestion="What kind of bridge do you mean?",
                ClarificationOptions=["Physical road/structural bridge","Network bridge","Software/integration bridge","Dental bridge","Card/game bridge","Something else"],
                IsSafetySensitive=true,
                AmbiguousConcepts=refined.AmbiguousConcepts.Concat(["bridge"]).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            };
        }
        return refined;
    }

    // Clarification-off downgrade: when EnableClarificationGate is false the pipeline must never stop to
    // ask the user, so a CLARIFICATION_REQUIRED contract is converted into a normal answerable contract.
    // AnswerKind is cleared to null (full pipeline, thoroughness over speed) unless the tenant recognizes
    // ENTITY_RANKING, in which case a ranking task is preferred so the Candidate x Branch competition runs
    // and a Top-N ranking is produced. Clarification prompt fields are stripped so no question surfaces.
    private static WideQueryContract? DowngradeClarificationContract(WideConfiguration configuration,WideQueryContract? contract)
    {
        if(contract is null)return null;
        var isClarificationKind=string.Equals(contract.AnswerKind,AnswerKindClarificationRequired,StringComparison.OrdinalIgnoreCase);
        if(!contract.RequiresClarification&&!isClarificationKind)return contract;
        var downgradedKind=isClarificationKind
            ?NormalizeAnswerKind(configuration,AnswerKindEntityRanking)
            :contract.AnswerKind;
        // The clarification classifier also stamps CandidateKind=DIAGNOSTIC_STEP, which makes
        // IsValidCandidateForContract admit only action-style names and reject named entities (cities,
        // places, products). That would leave candidate competition with zero admissible candidates,
        // so when the answer kind becomes a ranking we reset the candidate kind to NAMED_ENTITY.
        var downgradedCandidateKind=isClarificationKind
            &&contract.CandidateKind is CandidateKindDiagnosticStep or CandidateKindActionableSolution or CandidateKindProcedureStep
            ?CandidateKindNamedEntity
            :contract.CandidateKind;
        return contract with
        {
            AnswerKind=downgradedKind,
            CandidateKind=downgradedCandidateKind,
            RequiresClarification=false,
            ClarificationQuestion=null,
            ClarificationTarget=null,
            ClarificationOptions=[]
        };
    }


    private static bool SkipsCandidateCompetition(WideConfiguration configuration,WideQueryContract? queryContract)
    {
        if(FindAnswerKind(configuration,queryContract?.AnswerKind)is{}definition)return!definition.RunsCandidateCompetition;
        return queryContract?.AnswerKind is AnswerKindContentEnumeration or AnswerKindTechnicalRecommendation or AnswerKindDiagnosticProcedure or AnswerKindClarificationRequired;
    }

    // V3.2 answer-kind budgets, V3.3 data-driven: kind-specific budgets come from the lookup table's
    // per-kind columns; the five per-kind config keys remain only as compiled fallbacks. A kind depth
    // ceiling of 0 (or null rounds) means "use the full default"; budgets only ever SHRINK the
    // defaults, never expand them. Unknown/null kinds always run the full pipeline.
    private static(int DepthCeiling,int InformationRounds,bool RoutingApplied)ResolveAnswerKindBudgets(WideConfiguration configuration,WideQueryContract? queryContract)
    {
        if(!configuration.EnableAnswerKindRouting)
            return(configuration.AbsoluteDepthCeiling,configuration.MaximumInformationRounds,false);
        var(kindDepth,kindRounds)=FindAnswerKind(configuration,queryContract?.AnswerKind)is{}definition
            ?(definition.DepthCeiling,definition.MaxInformationRounds??configuration.MaximumInformationRounds)
            :queryContract?.AnswerKind switch
            {
                AnswerKindContentEnumeration=>(configuration.ContentEnumerationDepthCeiling,configuration.ContentEnumerationMaxInformationRounds),
                AnswerKindSingleAnswer=>(configuration.SingleAnswerDepthCeiling,configuration.SingleAnswerMaxInformationRounds),
                _=>(0,configuration.MaximumInformationRounds)
            };
        var depthCeiling=kindDepth>0?Math.Min(kindDepth,configuration.AbsoluteDepthCeiling):configuration.AbsoluteDepthCeiling;
        var informationRounds=Math.Min(kindRounds,configuration.MaximumInformationRounds);
        return(depthCeiling,informationRounds,depthCeiling<configuration.AbsoluteDepthCeiling||informationRounds<configuration.MaximumInformationRounds);
    }

    private static WideEntropyResult ComputeUncertainty(WideConfiguration configuration,IReadOnlyCollection<WideBranchRecord> branches,IReadOnlyCollection<string> candidateNames,IReadOnlyCollection<PoloxiEvidenceDto> evidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge,WideQueryContract? queryContract)
    {
        // V3.1: a content-enumeration query is NEVER a "which candidate wins" problem — its
        // "candidates" would be topic vocabulary fragments, not competing entities.
        // V3.7: a choose-exactly-one selection (RequestedCount == 1 with a ranking concept or
        // ENTITY_RANKING kind) is still a candidate competition — one winner must beat the field.
        var isRankingQuery=!SkipsCandidateCompetition(configuration,queryContract)
            &&queryContract is not null&&(!string.IsNullOrWhiteSpace(queryContract.RankingConcept)||queryContract.RequestedCount is >=1);
        if(isRankingQuery&&candidateNames.Count>=2)return ComputeCandidateEntropy(candidateNames,evidence,knowledge);
        var alternativeCount=branches.Count(branch=>branch.BranchStateCode is WideBranchStates.Active or WideBranchStates.Secondary
            &&branch.SemanticTypeCode==WideBranchSemanticTypes.Alternative);
        if(alternativeCount>=2&&!isRankingQuery)return ComputeEntropy(branches);
        if(candidateNames.Count>=2)return ComputeCandidateEntropy(candidateNames,evidence,knowledge);
        return new(0m,0m,1m,0){EntropyBasisCode=WideEntropyBases.Candidate};
    }

    private static WideEntropyResult EntropyFromValues(IReadOnlyCollection<decimal> values,string basisCode)
    {
        if(values.Count<2)return new(0m,0m,0m,values.Count){EntropyBasisCode=basisCode};
        var total=values.Sum();
        var entropy=0d;
        foreach(var value in values)
        {
            var p=(double)(value/total);
            entropy-=p*Math.Log2(p);
        }
        var maxEntropy=Math.Log2(values.Count);
        var normalized=maxEntropy<=0?0m:Math.Clamp((decimal)(entropy/maxEntropy),0,1);
        return new(Math.Round((decimal)entropy,4),Math.Round((decimal)maxEntropy,4),Math.Round(normalized,4),values.Count){EntropyBasisCode=basisCode};
    }

    // Deterministic conversion of an LLM categorical judgment (VERY_LOW..VERY_HIGH) to a configured value.
    private static decimal CategoryValue(WideConfiguration configuration,string category)=>category.Trim().ToUpperInvariant() switch
    {
        WideInformationCategories.VeryLow=>configuration.VeryLowInformationValue,
        WideInformationCategories.Low=>configuration.LowInformationValue,
        WideInformationCategories.Medium=>configuration.MediumInformationValue,
        WideInformationCategories.High=>configuration.HighInformationValue,
        WideInformationCategories.VeryHigh=>configuration.VeryHighInformationValue,
        _=>configuration.MediumInformationValue
    };

    // Reject malformed estimator output: every category must be an allowed value.
    private static bool ValidateCategories(WideInformationTargetProposal target)
    {
        static bool Valid(string value)=>WideInformationCategories.All.Contains(value.Trim().ToUpperInvariant());
        return Valid(target.Uncertainty)&&Valid(target.RankingImpact)&&Valid(target.CandidateDiscrimination)
            &&Valid(target.EvidenceAvailability)&&Valid(target.Novelty)&&Valid(target.Redundancy)
            &&target.PredictedRankingChanges.All(prediction=>
                prediction.Direction.Trim().ToUpperInvariant() is "UP" or "DOWN"
                &&prediction.Magnitude.Trim().ToUpperInvariant() is "NONE" or "LOW" or "MEDIUM" or "HIGH");
    }

    // Deterministic candidate signal: how strongly a candidate name is currently supported by the
    // evidence pool — the sum of relevance scores of enterprise evidence items and external snippets
    // that mention the candidate, saturated into 0..1 (raw/(1+raw)) so it always fits the
    // DECIMAL(5,4) ScoreBefore/ScoreAfter columns while preserving ordering and relative change.
    // Purely mechanical; used to verify LLM ranking-change predictions (baseline before targeted
    // retrieval, re-measured after). Never produced by the LLM.
    private static Dictionary<string,decimal> ComputeCandidateSignals(IReadOnlyCollection<string> candidates,IReadOnlyCollection<PoloxiEvidenceDto> evidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        var signals=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
        foreach(var candidate in candidates)
        {
            // V3.6.1: a candidate contained in a LONGER sibling candidate's name ("Rolling Hills" in
            // "Rolling Hills Estates") substring-matches every snippet naming only the sibling; such
            // matches credit the wrong entity. Sibling names are stripped before the mention test so
            // each candidate is scored only on text that names IT.
            var longerSiblings=candidates.Where(other=>other.Length>candidate.Length&&other.Contains(candidate,StringComparison.OrdinalIgnoreCase)).ToArray();
            var raw=evidence.Where(item=>Mentions(item.Title,candidate,longerSiblings)||Mentions(item.Excerpt,candidate,longerSiblings)).Sum(item=>item.RelevanceScore)
                +knowledge.Where(item=>Mentions(item.Title,candidate,longerSiblings)||Mentions(item.Snippet,candidate,longerSiblings)).Sum(item=>item.Score);
            signals[candidate]=Math.Round(raw/(1m+raw),4);
        }
        return signals;
        static bool Mentions(string? text,string candidate,string[] longerSiblings)
        {
            if(text?.Contains(candidate,StringComparison.OrdinalIgnoreCase)!=true)return false;
            foreach(var sibling in longerSiblings)text=text!.Replace(sibling,string.Empty,StringComparison.OrdinalIgnoreCase);
            return text!.Contains(candidate,StringComparison.OrdinalIgnoreCase);
        }
    }

    private static decimal ComputeCandidateBranchSignal(string candidate,WideBranchRecord branch,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        var candidateKeys=CandidateMatchKeys(candidate);
        var display=NormalizeBranchDisplayKey(branch.DisplayName);
        var branchKeys=new[]{branch.DisplayName,display,System.Text.RegularExpressions.Regex.Replace(display,@"^Best\s+(by|for)\s+",string.Empty,System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant),branch.SearchText}
            .Where(key=>!string.IsNullOrWhiteSpace(key)&&key!.Trim().Length>=4)
            .Select(key=>key!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var raw=0m;
        foreach(var snippet in knowledge)
        {
            var candidateText=$"{snippet.Title} {snippet.Snippet}";
            if(!candidateKeys.Any(key=>candidateText.Contains(key,StringComparison.OrdinalIgnoreCase)))continue;
            var branchText=$"{snippet.Query} {snippet.Title} {snippet.Snippet}";
            if(branchKeys.Length>0&&!branchKeys.Any(key=>branchText.Contains(key,StringComparison.OrdinalIgnoreCase)))continue;
            raw+=Math.Clamp(snippet.Score,0,1);
        }
        return Math.Round(raw/(1m+raw),4);
    }

    private static IReadOnlyDictionary<string,IReadOnlyDictionary<Guid,decimal>> CompileMetricNormalizedScores(IReadOnlyCollection<(string Name,string? Detail)> candidates,IReadOnlyCollection<WideBranchRecord> branches,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        var valuesByBranch=new Dictionary<Guid,List<(string Candidate,decimal Value)>>();
        foreach(var branch in branches)
        {
            if(!IsStructuredMetricBranch(branch))continue;
            foreach(var candidate in candidates)
            {
                if(TryExtractCandidateBranchMetric(candidate.Name,branch,knowledge,out var value))
                {
                    if(!valuesByBranch.TryGetValue(branch.WideBranchId,out var values))valuesByBranch[branch.WideBranchId]=values=[];
                    values.Add((candidate.Name,value));
                }
            }
        }
        var scores=new Dictionary<string,Dictionary<Guid,decimal>>(StringComparer.OrdinalIgnoreCase);
        foreach(var(branchId,values)in valuesByBranch)
        {
            var distinct=values.GroupBy(item=>item.Candidate,StringComparer.OrdinalIgnoreCase).Select(group=>(Candidate:group.Key,Value:group.Average(item=>item.Value))).ToArray();
            if(distinct.Length<2)continue;
            var min=distinct.Min(item=>item.Value);
            var max=distinct.Max(item=>item.Value);
            if(max<=min)continue;
            var branch=branches.First(item=>item.WideBranchId==branchId);
            var lowerIsBetter=IsLowerMetricBetter(branch);
            foreach(var item in distinct)
            {
                var normalized=(item.Value-min)/(max-min);
                if(lowerIsBetter)normalized=1m-normalized;
                if(!scores.TryGetValue(item.Candidate,out var candidateScores))scores[item.Candidate]=candidateScores=[];
                candidateScores[branchId]=Math.Clamp(normalized,0,1);
            }
        }
        return scores.ToDictionary(entry=>entry.Key,entry=>(IReadOnlyDictionary<Guid,decimal>)entry.Value,StringComparer.OrdinalIgnoreCase);
    }

    // V3.10.5: only DisplayName + SearchText are inspected — branch Interpretation text is
    // boilerplate ("Ranking ... based on ...") that made EVERY branch match generic tokens like
    // "ranking"/"rate", so the deterministic metric override hijacked qualitative dimensions.
    // Tokens are restricted to explicitly quantitative concepts; subjective ones (score, rating,
    // rank, ranking, growth, average alone) no longer qualify.
    private static bool IsStructuredMetricBranch(WideBranchRecord branch)
    {
        var text=$"{branch.DisplayName} {branch.SearchText}";
        return ContainsAny(text,["cost","costs","price","prices","rent","rental","median","crime rate","commute time","minutes","population","income","salary"]);
    }

    private static bool TryExtractCandidateBranchMetric(string candidate,WideBranchRecord branch,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge,out decimal value)
    {
        var candidateKeys=CandidateMatchKeys(candidate);
        var branchText=NormalizeBranchDisplayKey($"{branch.DisplayName} {branch.Interpretation} {branch.SearchText}");
        // V3.10.5 unit-aware extraction: the branch's metric category dictates which number tokens
        // are admissible. Monetary branches accept only $-prefixed or magnitude-suffixed values;
        // rate branches accept only percentages. Bare numbers (years, list positions, star ratings)
        // previously polluted the average and fabricated metrics that displaced real LLM scores.
        var monetary=ContainsAny(branchText,["cost","costs","price","prices","rent","rental","income","salary"]);
        var percentBased=ContainsAny(branchText,["crime rate","percent","vacancy","unemployment"]);
        var numbers=new List<decimal>();
        var hosts=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var snippet in knowledge)
        {
            var text=$"{snippet.Title} {snippet.Snippet}";
            if(!candidateKeys.Any(key=>text.Contains(key,StringComparison.OrdinalIgnoreCase)))continue;
            if(!BranchMetricTextMatches(branchText,$"{snippet.Query} {text}"))continue;
            var matchedAny=false;
            foreach(System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(text,@"(?<![\w.])\$?\d{1,3}(?:,\d{3})*(?:\.\d+)?\s*(?:%|percent|k|m|million|thousand)?",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant))
            {
                var token=match.Value.Trim();
                var lower=token.ToLowerInvariant();
                var hasDollar=token.StartsWith('$');
                var hasPercent=lower.Contains('%')||lower.EndsWith("percent");
                var hasMagnitude=System.Text.RegularExpressions.Regex.IsMatch(lower,@"(k|m|million|thousand)$");
                if(monetary&&!(hasDollar||hasMagnitude))continue;
                if(!monetary&&percentBased&&!hasPercent)continue;
                if(TryParseMetricNumber(token,out var parsed)){numbers.Add(parsed);matchedAny=true;}
            }
            if(!matchedAny)continue;
            var host=Uri.TryCreate(snippet.Url,UriKind.Absolute,out var uri)?uri.Host:snippet.Url??string.Empty;
            if(!string.IsNullOrWhiteSpace(host))hosts.Add(host);
        }
        // Minimum independent-host requirement: a metric derived from a single source is not trusted
        // enough to influence the Candidate × Branch matrix.
        value=numbers.Count==0?0:numbers.Average();
        return numbers.Count>0&&hosts.Count>=2;
    }

    private static bool BranchMetricTextMatches(string branchText,string snippetText)
    {
        var tokens=System.Text.RegularExpressions.Regex.Matches(branchText.ToLowerInvariant(),@"[a-z]{4,}")
            .Select(match=>match.Value)
            .Where(token=>token is not "best" and not "ranking" and not "places" and not "based" and not "south" and not "angeles")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToArray();
        return tokens.Length==0||tokens.Any(token=>snippetText.Contains(token,StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryParseMetricNumber(string text,out decimal value)
    {
        var normalized=System.Text.RegularExpressions.Regex.Replace(text.Trim().Replace("$",string.Empty).Replace(",",string.Empty).Replace("%",string.Empty),@"\bpercent\b",string.Empty,System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant).Trim();
        var lower=normalized.ToLowerInvariant();
        var multiplier=lower.EndsWith("k")?1000m:lower.EndsWith("m")?1000000m:lower.EndsWith("million")?1000000m:lower.EndsWith("thousand")?1000m:1m;
        normalized=System.Text.RegularExpressions.Regex.Replace(normalized,@"\s*(k|m|million|thousand)$",string.Empty,System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant).Trim();
        if(decimal.TryParse(normalized,System.Globalization.NumberStyles.Number,System.Globalization.CultureInfo.InvariantCulture,out value))
        {
            value*=multiplier;
            return true;
        }
        value=0;
        return false;
    }

    private static bool IsLowerMetricBetter(WideBranchRecord branch)
    {
        var text=$"{branch.DisplayName} {branch.Interpretation} {branch.SearchText}";
        return ContainsAny(text,["cost","costs","price","prices","rent","rental","tax","taxes","crime","traffic","commute time","delay","rate"])
            &&!ContainsAny(text,["school rating","score","quality score","income","salary","growth","access"]);
    }

    // Dense rank of candidate signals (1 = strongest). Ranks are relative to the round's predicted
    // candidates only — enough to grade predicted UP/DOWN movement deterministically.
    private static Dictionary<string,int> RankSignals(Dictionary<string,decimal> signals)
    {
        var ranks=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);
        var rank=0;
        foreach(var entry in signals.OrderByDescending(item=>item.Value))ranks[entry.Key]=++rank;
        return ranks;
    }

    // V3.10.4 shared match-key derivation for evidence attribution. Keys per candidate:
    //   1. the full name verbatim,
    //   2. the qualifier-stripped primary (text before a comma/parenthesis/dash - V2.6.1),
    //   3. the connective-tail core (text before " and " / " & ") - LLM names carry descriptive
    //      tails ("Danny K's Billiards and Sports Bar") that snippets shorten ("Danny K's
    //      Billiards"); a verbatim-only match starved such candidates of evidence hosts and the
    //      corpus-evidence floor then excluded genuinely attested venues.
    // Guards: every derived key must be >=4 chars and >=2 tokens (single-token cores are never
    // used as keys - too many trivial substring hits), and must differ from keys already taken.
    // Both CountDistinctSourceHosts and CountExclusiveSourceHosts use this SAME derivation so
    // mention counting and exclusive attribution stay consistent. Deterministic, zero LLM.
    private static string[] CandidateMatchKeys(string candidate)
    {
        var keys=new List<string>{candidate};
        void AddKey(string? key)
        {
            key=key?.Trim();
            if(string.IsNullOrEmpty(key)||key.Length<4)return;
            if(key.Split(' ',StringSplitOptions.RemoveEmptyEntries).Length<2&&!string.Equals(key,candidate,StringComparison.OrdinalIgnoreCase))
            {
                // Single-token cores only allowed via the V2.6.1 primary rule when long enough.
                if(key.Length<4)return;
            }
            if(!keys.Contains(key,StringComparer.OrdinalIgnoreCase))keys.Add(key);
        }
        AddKey(candidate.Split(',','(','\u2013','\u2014')[0]);
        foreach(var separator in new[]{" and "," & "})
        {
            var tailIndex=candidate.IndexOf(separator,StringComparison.OrdinalIgnoreCase);
            if(tailIndex>0)
            {
                var core=candidate[..tailIndex].Trim();
                if(core.Split(' ',StringSplitOptions.RemoveEmptyEntries).Length>=2)AddKey(core);
            }
        }
        return [..keys];
    }

    // V2.5 Independent Evidence Diversity: number of DISTINCT source hosts whose title or snippet
    // mentions the candidate. Deterministic and zero-LLM. One article claiming a candidate excels
    // across many dimensions is weaker support than independent sources agreeing.
    // V2.6.1 audit fix: LLM candidate names are often qualified ("Raleigh, North Carolina") while
    // snippets say "Raleigh" — a verbatim full-name match made distinctHosts=0 for nearly every
    // candidate, so evidence confidence collapsed to the single-source floor (a de-facto 70%
    // default). Match on the primary name (text before a comma/parenthesis/dash qualifier) too,
    // provided it is long enough to avoid trivial substring hits.
    // V3.10.4: key derivation extracted to CandidateMatchKeys (adds the connective-tail core).
    private static int CountDistinctSourceHosts(string candidate,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        var keys=CandidateMatchKeys(candidate);
        var hosts=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var snippet in knowledge)
        {
            var matched=false;
            foreach(var key in keys)
                if(snippet.Title?.Contains(key,StringComparison.OrdinalIgnoreCase)==true
                    ||snippet.Snippet?.Contains(key,StringComparison.OrdinalIgnoreCase)==true){matched=true;break;}
            if(!matched)continue;
            var host=Uri.TryCreate(snippet.Url,UriKind.Absolute,out var uri)?uri.Host:snippet.Url??string.Empty;
            if(!string.IsNullOrWhiteSpace(host))hosts.Add(host);
        }
        return hosts.Count;
    }

    // V3.10 EEA - Exclusive Evidence Attribution: for each candidate, count the distinct evidence
    // hosts whose snippets mention THAT candidate and NO other pool candidate. Shared "listicle"
    // hosts naming everyone attest nothing about any single entity; hosts that discuss exactly one
    // candidate are genuine entity-specific evidence. Deterministic string matching over the
    // already-retrieved snippets - O(candidates x snippets), zero LLM. Matching mirrors
    // CountDistinctSourceHosts via the shared CandidateMatchKeys derivation so both signals
    // attribute the same mentions consistently.
    private static Dictionary<string,int> CountExclusiveSourceHosts(IReadOnlyCollection<string> candidates,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        var keysByCandidate=candidates.ToDictionary(candidate=>candidate,CandidateMatchKeys,StringComparer.OrdinalIgnoreCase);
        // V3.6.1: a candidate whose name is contained in a LONGER sibling's name ("Rolling Hills" in
        // "Rolling Hills Estates") substring-matches every snippet naming only the sibling, wrongly
        // turning the sibling's genuinely exclusive snippets into "multiple-candidate" snippets.
        // Strip longer sibling names from the text before testing the shorter candidate so a match
        // requires a residual mention of the short form itself.
        var longerSiblingsByCandidate=candidates.ToDictionary(
            candidate=>candidate,
            candidate=>candidates.Where(other=>other.Length>candidate.Length&&other.Contains(candidate,StringComparison.OrdinalIgnoreCase)).ToArray(),
            StringComparer.OrdinalIgnoreCase);
        var exclusiveHosts=candidates.ToDictionary(candidate=>candidate,_=>new HashSet<string>(StringComparer.OrdinalIgnoreCase),StringComparer.OrdinalIgnoreCase);
        foreach(var snippet in knowledge)
        {
            var text=$"{snippet.Title} {snippet.Snippet}";
            if(string.IsNullOrWhiteSpace(text))continue;
            string? sole=null;
            var multiple=false;
            foreach(var(candidate,keys)in keysByCandidate)
            {
                var candidateText=text;
                foreach(var sibling in longerSiblingsByCandidate[candidate])
                    if(candidateText.Contains(sibling,StringComparison.OrdinalIgnoreCase))
                        candidateText=candidateText.Replace(sibling,string.Empty,StringComparison.OrdinalIgnoreCase);
                if(!keys.Any(key=>candidateText.Contains(key,StringComparison.OrdinalIgnoreCase)))continue;
                if(sole is not null&&!string.Equals(sole,candidate,StringComparison.OrdinalIgnoreCase)){multiple=true;break;}
                sole=candidate;
            }
            if(multiple||sole is null)continue;
            var host=Uri.TryCreate(snippet.Url,UriKind.Absolute,out var uri)?uri.Host:snippet.Url??string.Empty;
            if(!string.IsNullOrWhiteSpace(host))exclusiveHosts[sole].Add(host);
        }
        return exclusiveHosts.ToDictionary(entry=>entry.Key,entry=>entry.Value.Count,StringComparer.OrdinalIgnoreCase);
    }

    // V3.10 FD - Fragment Domination: a candidate whose canonical tokens form a STRICT SUBSET of
    // another pool candidate's tokens ("Research" vs "Research Triangle Park", "Income" vs "Realty
    // Income") is a name fragment, not an independent entity - UNLESS it has exclusive evidence of
    // its own (hosts discussing it and nothing else), which proves independent existence. Returns
    // the dominating candidate's name per dominated fragment. O(n^2) over the small candidate pool,
    // deterministic, zero LLM, no vocabulary.
    // V3.10.3: domination compares against the other candidate's QUALIFIER-INCLUSIVE full-name
    // tokens (FullNameTokens), not only its canonical core. A bare geographic/qualifier echo
    // ("Los Angeles" harvested from "Q's Billiard Club (Los Angeles)") was invisible to the
    // canonical check because CanonicalTokens strips parenthetical qualifiers. Full-name tokens
    // are a superset of canonical tokens, so every previously detected fragment is still detected;
    // the exclusive-evidence escape hatch is unchanged, so a genuinely independent entity with
    // its own exclusive hosts is never dominated.
    private static Dictionary<string,string> FindDominatedFragments(IReadOnlyCollection<string> candidates,IReadOnlyDictionary<string,int> exclusiveHostCounts)
    {
        var tokenSetsByCandidate=candidates.ToDictionary(candidate=>candidate,
            candidate=>CanonicalTokens(candidate).Select(token=>token.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        var tokensByCandidate=candidates.ToDictionary(candidate=>candidate,
            candidate=>CanonicalTokens(candidate).Select(token=>token.ToLowerInvariant()).ToArray(),
            StringComparer.OrdinalIgnoreCase);
        var fullTokenSetsByCandidate=candidates.ToDictionary(candidate=>candidate,
            candidate=>FullNameTokens(candidate).Select(token=>token.ToLowerInvariant()).ToHashSet(StringComparer.OrdinalIgnoreCase),
            StringComparer.OrdinalIgnoreCase);
        var fullTokensByCandidate=candidates.ToDictionary(candidate=>candidate,
            candidate=>FullNameTokens(candidate).Select(token=>token.ToLowerInvariant()).ToArray(),
            StringComparer.OrdinalIgnoreCase);
        var dominated=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var(candidate,tokens)in tokenSetsByCandidate)
        {
            if(tokens.Count==0||exclusiveHostCounts.GetValueOrDefault(candidate)>0)continue;
            foreach(var(other,otherTokens)in fullTokenSetsByCandidate)
            {
                if(string.Equals(candidate,other,StringComparison.OrdinalIgnoreCase))continue;
                if(otherTokens.Count>tokens.Count&&tokens.All(otherTokens.Contains)
                    &&(tokens.Count==1||IsContiguousTokenSubsequence(tokensByCandidate[candidate],fullTokensByCandidate[other])))
                {
                    dominated[candidate]=other;
                    break;
                }
            }
        }
        return dominated;
    }

    private static bool IsContiguousTokenSubsequence(string[] shorter,string[] longer)
    {
        if(shorter.Length==0||shorter.Length>longer.Length)return false;
        for(var start=0;start<=longer.Length-shorter.Length;start++)
        {
            var matches=true;
            for(var index=0;index<shorter.Length;index++)
            {
                if(string.Equals(shorter[index],longer[start+index],StringComparison.OrdinalIgnoreCase))continue;
                matches=false;
                break;
            }
            if(matches)return true;
        }
        return false;
    }

    // V2.4 Early Candidate Harvest: deterministic, zero-LLM extraction of candidate names from external
    // result sets. A candidate is a capitalized proper-noun phrase that appears in at least two DISTINCT
    // snippets. Venue/list pages often include names with apostrophes, ampersands, and business suffixes
    // ("Q's Billiard Club", "Danny K's Billiards & Sports Bar"); keep those concrete names so the
    // mini interpretive pass cannot starve the competition with category placeholders.
    private static IReadOnlyCollection<string> HarvestCandidateNames(IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        if(knowledge.Count<2)return [];
        var occurrences=new Dictionary<string,HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        // V3.11 Common-Word Corpus Evidence: Title-Case headlines capitalize EVERY word, so the
        // sentence-start heuristic below cannot catch single common-English tokens harvested from
        // titles ("Safe", "Median", "Great", "Stars"). A real proper noun essentially never appears
        // lowercase in the same corpus; any single-token name whose lowercase form is also observed
        // mid-text is a common word, not a competing entity — deterministically rejected here so it
        // never inflates the candidate pool, the entropy basis, or the matrix prompt.
        var lowercaseCorpusTokens=new HashSet<string>(StringComparer.Ordinal);
        var index=0;
        foreach(var snippet in knowledge)
        {
            index++;
            var text=$"{snippet.Title}. {snippet.Snippet}";
            foreach(var raw in text.Split([' ','\t','\n','\r'],StringSplitOptions.RemoveEmptyEntries))
            {
                var token=raw.Trim('.',',',';',':','!','?','(',')','[',']','"','\'','\u2019','\u201C','\u201D','\u2014','-','\u00B7');
                if(token.Length>1&&char.IsLower(token[0]))lowercaseCorpusTokens.Add(token.ToLowerInvariant());
            }
            foreach(var phrase in ExtractProperPhrases(text))
            {
                if(!occurrences.TryGetValue(phrase,out var set))occurrences[phrase]=set=[];
                set.Add(index);
            }
        }
        // Cross-source repetition: a real candidate is named by at least two distinct snippets.
        return occurrences.Where(entry=>entry.Value.Count>=2&&!IsCommonWordPseudoCandidate(entry.Key,lowercaseCorpusTokens))
            .OrderByDescending(entry=>entry.Value.Count)
            .Take(100)
            .Select(entry=>entry.Key)
            .ToArray();
    }

    // V3.11: single-token pseudo-candidate rejection backed by corpus evidence. Possessive
    // fragments ("LA's") are references to another entity, never candidates themselves; a token
    // that the corpus also uses lowercase is common English vocabulary, not a proper noun.
    private static bool IsCommonWordPseudoCandidate(string name,HashSet<string> lowercaseCorpusTokens)
    {
        var words=name.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(words.Length!=1)return false;
        var word=words[0];
        if(word.EndsWith("'s",StringComparison.OrdinalIgnoreCase)||word.EndsWith("\u2019s",StringComparison.OrdinalIgnoreCase))return true;
        return lowercaseCorpusTokens.Contains(word.ToLowerInvariant());
    }

    private static IEnumerable<string> ExtractProperPhrases(string text)
    {
        var tokens=text.Split([' ','\t','\n','\r'],StringSplitOptions.RemoveEmptyEntries);
        var current=new List<string>();
        // A SINGLE capitalized word that only starts a sentence carries no proper-noun
        // signal ("Every table is...", "Relax with friends...", "All ages welcome...") - the same
        // signal. Single-word phrases that began at a sentence start are discarded; a real single-word entity is also mentioned mid-text
        // in the corpus and is harvested from there. Multi-word runs ("On Cue Billiards is...")
        // keep their sentence-start occurrences - common words never chain into Title Case runs.
        var currentStartsSentence=false;
        var atSentenceStart=true;
        foreach(var raw in tokens)
        {
            var word=raw.Trim('.',',',';',':','!','?','(',')','[',']','"','\'','’','“','”','—','-','·');
            var isConnector=word is "&" or "and";
            var isProper=word.Length>1&&char.IsUpper(word[0])&&word.Skip(1).All(c=>char.IsLetter(c)||c=='\''||c=='’'||char.IsDigit(c)||c=='&');
            if(isProper||(isConnector&&current.Count>0))
            {
                if(current.Count==0)currentStartsSentence=atSentenceStart;
                current.Add(word);
                if(current.Count==6)
                {
                    yield return string.Join(' ',current);
                    current.Clear();
                }
            }
            else
            {
                if(current.Count>1||(current.Count==1&&!currentStartsSentence))yield return string.Join(' ',current);
                current.Clear();
            }
            atSentenceStart=raw.EndsWith('.')||raw.EndsWith('!')||raw.EndsWith('?');
        }
        if(current.Count>1||(current.Count==1&&!currentStartsSentence))yield return string.Join(' ',current);
    }

    // V3.5 enumeration seeding: one cheap LLM call naming concrete candidates. Enumeration is the one
    // task mini-tier models do reliably; output is untrusted and every seed faces the deterministic
    // filters and evidence gates downstream. Fail-soft: any failure returns an empty list.
    private async Task<IReadOnlyCollection<string>> EnumerateCandidateSeedsAsync(WideSearchRequest request,WideQueryContract? queryContract,CancellationToken cancellationToken)
    {
        try
        {
            var contractContext=queryContract is null?"(none)":$"answerKind: {queryContract.AnswerKind}; candidateKind: {queryContract.CandidateKind}; entityType: {queryContract.EntityType}; ranking: {queryContract.RankingConcept}; hard constraints: {string.Join("; ",queryContract.HardConstraints)}";
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_INFORMATION_VALUE",
                await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.WideCandidateEnumeration,cancellationToken),
                $"Question: {request.Query}\nQuery contract: {contractContext}",
                CandidateEnumerationSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_CANDIDATE_ENUMERATION",null,request.CorrelationId,"Intelligent Search Wide"),ModelOverride(request),cancellationToken);
            var proposal=JsonSerializer.Deserialize<WideCandidateEnumerationProposal>(result.Content,JsonOptions);
            return proposal?.Candidates?.Where(name=>!string.IsNullOrWhiteSpace(name)).Select(name=>name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()??[];
        }
        catch(Exception)when(!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
    }

    private sealed record WideCandidateEnumerationProposal(IReadOnlyList<string>? Candidates);

    // Phase 2a Challenge-the-Winner (WATCH MODE): one adversarial LLM assessment that argues AGAINST
    // the current leader using only the already-retrieved evidence — no new retrieval calls. Fail-soft:
    // any provider/parse failure returns null and the run proceeds exactly as before.
    private async Task<WideChallengeOutcomeDto?> ChallengeWinnerAndPersistAsync(WideSearchRequest request,Guid executionId,WideCandidateDto leader,WideCandidateDto runnerUp,decimal margin,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,CancellationToken cancellationToken)
    {
        var challengeOutcome=await ChallengeWinnerAsync(request,executionId,leader,runnerUp,margin,externalKnowledge,cancellationToken);
        if(challengeOutcome is not null)
        {
            try{await wideRepository.UpdateWideExecutionChallengeOutcomeAsync(request.TenantId,request.UserId,executionId,JsonSerializer.Serialize(challengeOutcome,JsonOptions),cancellationToken);}
            catch{/* audit only; never blocks the answer */}
        }
        return challengeOutcome;
    }

    private async Task<WideChallengeOutcomeDto?> ChallengeWinnerAsync(WideSearchRequest request,Guid executionId,WideCandidateDto leader,WideCandidateDto runnerUp,decimal margin,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,CancellationToken cancellationToken)
    {
        try
        {
            var leaderScores=string.Join("; ",leader.BranchScores.Select(score=>$"{score.BranchDisplayName}: {score.EvidenceScore:0.00}"));
            var runnerUpScores=string.Join("; ",runnerUp.BranchScores.Select(score=>$"{score.BranchDisplayName}: {score.EvidenceScore:0.00}"));
            var evidenceContext=string.Join('\n',externalKnowledge.Take(12).Select((snippet,index)=>$"[{index+1}] {snippet.Title}: {snippet.Snippet}"));
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_INFORMATION_VALUE",
                await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.WideChallengeRound,cancellationToken),
                $"Question: {request.Query}\nLeader: {leader.DisplayName} (composite {leader.CompositeScore:0.00}; dimensions: {leaderScores})\nRunner-up: {runnerUp.DisplayName} (composite {runnerUp.CompositeScore:0.00}; dimensions: {runnerUpScores})\nMargin: {margin:0.00}\nEvidence:\n{evidenceContext}",
                ChallengeVerdictSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_CHALLENGE_ROUND",executionId,request.CorrelationId,"Intelligent Search Wide"),ModelOverride(request),cancellationToken);
            var proposal=JsonSerializer.Deserialize<WideChallengeVerdictProposal>(result.Content,JsonOptions);
            if(proposal is null||string.IsNullOrWhiteSpace(proposal.VerdictCode))return null;
            var verdict=proposal.VerdictCode.Trim().ToUpperInvariant() switch
            {
                WideChallengeVerdicts.Weakened=>WideChallengeVerdicts.Weakened,
                WideChallengeVerdicts.OverturnSuggested=>WideChallengeVerdicts.OverturnSuggested,
                _=>WideChallengeVerdicts.Upheld
            };
            return new(leader.DisplayName,runnerUp.DisplayName,margin,verdict,proposal.Rationale?.Trim()??string.Empty,
                verdict==WideChallengeVerdicts.OverturnSuggested?runnerUp.DisplayName:null);
        }
        catch(Exception)when(!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    private sealed record WideChallengeVerdictProposal(string? VerdictCode,string? Rationale);

    private const string ChallengeVerdictSchema="""
{
  "type": "object",
  "properties": {
    "verdictCode": { "type": "string", "enum": ["UPHELD", "WEAKENED", "OVERTURN_SUGGESTED"] },
    "rationale": { "type": "string" }
  },
  "required": ["verdictCode", "rationale"],
  "additionalProperties": false
}
""";

    private const string CandidateEnumerationSchema="""
{
  "type": "object",
  "properties": {
    "candidates": {
      "type": "array",
      "maxItems": 20,
      "items": { "type": "string" }
    }
  },
  "required": ["candidates"],
  "additionalProperties": false
}
""";

    // V3.5 seed verification retrieval: a few short comparative queries covering the seed batch so
    // legitimate seeds can accumulate the multi-host evidence support the admission gates require.
    // Batched (4 seeds per query, max 5 queries) to bound provider cost. Fail-soft per query.
    private async Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> GatherSeedVerificationKnowledgeAsync(WideSearchRequest request,Guid executionId,IReadOnlyCollection<string> seeds,CancellationToken cancellationToken)
    {
        try
        {
            var configuration=await wideRepository.GetExternalGroundingConfigurationAsync(request.TenantId,cancellationToken);
            if(!configuration.Enabled||string.IsNullOrWhiteSpace(configuration.ApiKey))return [];
            var notBeforeUtc=DateTime.UtcNow.AddHours(-configuration.CacheHours);
            var topic=BuildCandidateSeekingQuery(request.Query,string.Empty);
            var batches=seeds.Chunk(4).Take(5).ToArray();
            var collected=new List<WideExternalKnowledgeSnippet>();
            foreach(var batch in batches)
            {
                try
                {
                    var query=NormalizeQuery($"{string.Join(" vs ",batch)} {topic} comparison").ToLowerInvariant();
                    var cached=await wideRepository.GetCachedExternalKnowledgeAsync(request.TenantId,query,notBeforeUtc,cancellationToken);
                    if(cached.Count>0){collected.AddRange(cached.Take(configuration.MaximumSnippetsPerQuery));continue;}
                    var retrieved=await externalKnowledgeProvider.SearchAsync(query,configuration,cancellationToken);
                    if(retrieved.Count==0)continue;
                    await wideRepository.SaveExternalKnowledgeAsync(request.TenantId,request.UserId,query,retrieved,executionId,cancellationToken);
                    collected.AddRange(retrieved);
                }
                catch(Exception)when(!cancellationToken.IsCancellationRequested){/* one failed batch never blocks the rest */}
            }
            return collected;
        }
        catch(Exception)when(!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
    }

    // One BATCHED call estimates information value for all eligible branches, including falsifiable
    // candidate ranking-change predictions POLOXI can later verify. Fail-soft: returns null on any failure.
    private async Task<WideInformationValueProposal?> EstimateInformationValueAsync(WideSearchRequest request,IReadOnlyCollection<WideBranchRecord> eligible,WideEntropyResult entropy,WideQueryContract? queryContract,string? contestedPair,CancellationToken cancellationToken)
    {
        try
        {
            var branchContext=string.Join('\n',eligible.Select(branch=>$"- branchCode: {branch.BranchCode} | name: {branch.DisplayName} | interpretation: {Truncate(branch.Interpretation,200)} | state: {branch.BranchStateCode} | poloxiConfidence: {branch.PoloxiConfidence:F2} | evidenceSupport: {branch.EvidenceSupport:F2} | evidenceCount: {branch.EvidenceCount}"));
            var contractContext=queryContract is null?"(none)":$"entityType: {queryContract.EntityType}; ranking: {queryContract.RankingConcept}; hard constraints: {string.Join("; ",queryContract.HardConstraints)}";
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_INFORMATION_VALUE",
                await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.WideInformationValue,cancellationToken),
                $"Question: {request.Query}\nQuery contract: {contractContext}\nCurrent normalized uncertainty (0=resolved, 1=maximal): {entropy.NormalizedEntropy:F2}\nUnresolved bottleneck: {contestedPair??"(no contested pair yet — candidate signals are not established)"}\nBranches:\n{branchContext}",
                InformationValueSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_INFORMATION_VALUE",null,request.CorrelationId,"Intelligent Search Wide"),ModelOverride(request),cancellationToken);
            return JsonSerializer.Deserialize<WideInformationValueProposal>(result.Content,JsonOptions);
        }
        catch(Exception)when(!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    // Phase 1 (VNext): deterministic contested-pair description — the current leader vs runner-up over
    // the frozen candidate basis and their margin. Reuses the existing mention-weighted candidate
    // signals; prompt-context only, no effect on any scoring or narrowing path.
    private static string? DescribeContestedPair(IReadOnlyCollection<string> candidateNames,IReadOnlyCollection<PoloxiEvidenceDto> evidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        if(candidateNames.Count<2)return null;
        var top=ComputeCandidateSignals(candidateNames,evidence,knowledge).OrderByDescending(item=>item.Value).Take(2).ToArray();
        if(top.Length<2||top[0].Value<=0m)return null;
        var margin=top[0].Value-top[1].Value;
        return $"the current leader is \"{top[0].Key}\" (signal {top[0].Value:F2}) vs runner-up \"{top[1].Key}\" (signal {top[1].Value:F2}), separated by {margin:F2} — evidence that best separates these two is the most valuable.";
    }
    // never invented by the LLM. Bounded Consensus Fallback combines enterprise and external signals:
    // enterprise evidence wins clear support conflicts, corroborated sources use the stronger support,
    // external-only support is discounted, and unsupported branches score 0.
    private static decimal ComputeEvidenceSupport(WideBranchRecord branch,IReadOnlyCollection<PoloxiEvidenceDto> evidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,WideConfiguration configuration)
    {
        var enterpriseCount=evidence.Count(item=>item.HierarchyBranchId==branch.WideBranchId);
        // Saturating enterprise contribution (DB-calibrated; see migration 0163): 1 item = base, ceiling caps growth.
        var enterpriseSupport=enterpriseCount==0?0m:Math.Min(configuration.EnterpriseSupportCeiling,configuration.EnterpriseSupportBase+configuration.EnterpriseSupportIncrement*(enterpriseCount-1));
        // External snippets match a branch when the retrieval query included the branch display name.
        var matchedSnippets=externalKnowledge.Where(snippet=>snippet.Query.Contains(branch.DisplayName,StringComparison.OrdinalIgnoreCase)).ToArray();
        var externalSupport=matchedSnippets.Length==0?0m:Math.Clamp(matchedSnippets.Max(snippet=>snippet.Score),0,1)*Math.Min(1m,configuration.ExternalSupportBase+configuration.ExternalSupportIncrement*matchedSnippets.Length);
        return ResolveBoundedConsensusEvidenceSupport(enterpriseSupport,externalSupport,configuration);
    }

    private static decimal ResolveBoundedConsensusEvidenceSupport(decimal enterpriseSupport,decimal externalSupport,WideConfiguration configuration)
    {
        var consensusThreshold=Math.Clamp(configuration.EvidenceConsensusThreshold,0,1);
        var externalOnlyDiscount=Math.Clamp(configuration.ExternalOnlySupportDiscount,0,1);
        var enterprise=Math.Clamp(enterpriseSupport,0,1);
        var external=Math.Clamp(externalSupport,0,1);
        if(enterprise>0&&external>0)
            return Math.Abs(enterprise-external)<=consensusThreshold?Math.Max(enterprise,external):enterprise;
        if(enterprise>0)return enterprise;
        if(external>0)return Math.Clamp(external*externalOnlyDiscount,0,1);
        return 0m;
    }

    // -----------------------------------------------------------------------------------------------
    // V2.1 Candidate Engine: build the candidate universe from the interpretive result sets, apply the
    // hard-constraint filter (violators are kept but flagged PRUNED, never silently dropped), score
    // each candidate against each surviving branch, and rank by the branch-importance-weighted
    // composite. Fail-soft: any LLM failure returns an empty collection.
    // -----------------------------------------------------------------------------------------------
    // V2.7 Candidate Validity: deterministic rejection of category/placeholder phrases that are not
    // concrete entities ("Other cities with high quality of life", "Best places to raise a family").
    // A valid candidate is a short proper-noun phrase; phrases containing category/plural/generic
    // words are rejected regardless of how they entered the pipeline.
    private static readonly HashSet<string> CandidateInvalidWords=new(StringComparer.OrdinalIgnoreCase)
    {
        // Category/plural/placeholder words only — never plain adjectives ("High Point, NC" is a real
        // city). A phrase containing any of these is a category description, not a concrete entity.
        "other","others","cities","places","towns","suburbs","areas","regions","locations","options",
        "various","several","many","some","etc","numerous","additional","remaining","alternative","alternatives",
        "venues","halls","reviews","ratings"
    };

    private static readonly HashSet<string> CandidateArtifactWords=new(StringComparer.OrdinalIgnoreCase)
    {
        "a","an","the","and","or","not","pro","con","what","why","how","when","where","who","instructions","instruction",
        "processor","processors","cpu","gpu","arm","fpga","cuda","august","june","view","see","get","share","about","with"
    };

    private static readonly HashSet<string> ActionCandidateVerbs=new(StringComparer.OrdinalIgnoreCase)
    {
        "add","adjust","analyze","batch","cache","change","decouple","detect","diagnose","disable","enable",
        "implement","increase","inspect","isolate","measure","monitor","move","optimize","parallelize","profile",
        "reduce","replace","schedule","split","tune","use","validate"
    };

    // V2.8.1 Attribute-Hypothesis Rejection, narrowed in V2.8.3: only NON-IDENTIFYING qualifiers
    // (geography/scale/generic — "Mercury (US)", "Mercury (large company)") mark a pseudo-candidate.
    // INDUSTRY qualifiers are how interpretive lists disambiguate real same-named entities —
    // "Mercury (Fintech)" was the fintech's ONLY interpretive name, and rejecting it silently
    // removed a legitimate entity from competition (candidate recall loss). Industry-qualified
    // references now survive; duplicates collapse via the qualifier-normalized dedup key below.
    private static readonly HashSet<string> NonIdentifyingQualifierWords=new(StringComparer.OrdinalIgnoreCase)
    {
        "us","usa","u.s.","europe","european","american","global","international","domestic","based",
        "company","companies","business","enterprise","sme","large","small","mid-size","midsize","startup"
    };

    private static bool IsAttributeHypothesis(string name)
    {
        var open=name.IndexOf('(');
        if(open<0)return false;
        var close=name.IndexOf(')',open+1);
        var qualifier=(close>open?name[(open+1)..close]:name[(open+1)..]).Trim();
        if(qualifier.Length==0)return false;
        var tokens=qualifier.Split([' ','-','/',','],StringSplitOptions.RemoveEmptyEntries);
        return tokens.Length>0&&tokens.All(token=>NonIdentifyingQualifierWords.Contains(token));
    }

    // V2.8.3 qualifier-normalized identity key: "Mercury (technology)" and "Mercury (technology
    // company)" are the same reference — core tokens plus identifying qualifier tokens (generic
    // filler removed), order-insensitive. Collapses qualifier phrasing variants without merging
    // distinct entities (different industries produce different keys).
    private static string CandidateIdentityKey(string name)
    {
        var open=name.IndexOf('(');
        var core=CanonicalTokens(name);
        var qualifierTokens=Array.Empty<string>();
        if(open>=0)
        {
            var close=name.IndexOf(')',open+1);
            var qualifier=(close>open?name[(open+1)..close]:name[(open+1)..]).Trim();
            qualifierTokens=qualifier.Split([' ','-','/',','],StringSplitOptions.RemoveEmptyEntries)
                .Where(token=>!NonIdentifyingQualifierWords.Contains(token))
                .Select(token=>token.ToLowerInvariant())
                .OrderBy(token=>token,StringComparer.Ordinal)
                .ToArray();
        }
        return string.Join(' ',core.Select(token=>token.ToLowerInvariant()))+"|"+string.Join(' ',qualifierTokens);
    }

    private static bool IsValidCandidateName(string name)
    {
        var trimmed=name.Trim();
        if(trimmed.Length<2||trimmed.Length>80)return false;
        var words=trimmed.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(words.Length>5)return false;
        if(words.Any(word=>CandidateInvalidWords.Contains(word.TrimEnd(','))))return false;
        if(words.All(word=>SubsetConnectiveTokens.Contains(word.Trim(',',':',';','.','(',')'))))return false;
        // V2.8.1: reject attribute-qualified hypotheses — dimensions masquerading as entities.
        if(IsAttributeHypothesis(trimmed))return false;
        // Must look like a proper noun: first alphabetic character uppercase.
        var firstLetter=trimmed.FirstOrDefault(char.IsLetter);
        if(firstLetter==default||!char.IsUpper(firstLetter))return false;
        // V3.4.4 Proper-Noun Density: entities are Title Case ("Austin, TX", "De La Salle University
        // Manila"); criterion/approach descriptions are sentence case ("Balanced multi-factor
        // approach", "Commute times under 30 minutes", "Significantly below-average violent crime
        // rates") - only their first word is capitalized. Excluding connective tokens (of/the/de...),
        // ALL significant alphabetic words of a real entity name start uppercase. Any lowercase
        // significant word marks the name as a description, not an entity - rejected.
        var significantWords=words
            .Select(word=>word.Trim(',','(',')'))
            .Where(word=>word.Length>0&&char.IsLetter(word[0])&&!SubsetConnectiveTokens.Contains(word))
            .ToArray();
        if(significantWords.Length>0&&!significantWords.All(word=>char.IsUpper(word[0])))return false;
        return true;
    }

    private static bool IsCandidateArtifact(string name)
    {
        var trimmed=name.Trim();
        if(CandidateArtifactWords.Contains(trimmed))return true;
        var tokens=trimmed.Split([' ','-','/'],StringSplitOptions.RemoveEmptyEntries).Select(token=>token.Trim(',',':',';','.','(',')')).Where(token=>token.Length>0).ToArray();
        return tokens.Length==1&&CandidateArtifactWords.Contains(tokens[0]);
    }

    private static bool IsValidCandidateForContract(string name,WideQueryContract? queryContract)
    {
        if(IsCandidateArtifact(name))return false;
        var candidateKind=queryContract?.CandidateKind;
        if(candidateKind is CandidateKindActionableSolution or CandidateKindDiagnosticStep or CandidateKindProcedureStep)
            return IsActionCandidate(name);
        return IsValidCandidateName(name);
    }

    private static bool HasNamedEntityAdmissionSupport(string name,WideQueryContract? queryContract,int interpretiveSupport,int distinctHosts,int exclusiveHosts,int requiredSupport)
    {
        if(queryContract?.CandidateKind is CandidateKindActionableSolution or CandidateKindDiagnosticStep or CandidateKindProcedureStep)return true;
        if(IsContractScopeOrCategoryEcho(name,queryContract))return false;
        var significantTokens=CanonicalTokens(name)
            .Select(token=>token.Trim(',',':',';','.','(',')'))
            .Where(token=>token.Length>0&&!SubsetConnectiveTokens.Contains(token))
            .ToArray();
        if(significantTokens.Length!=1)return true;
        if(IsCandidateArtifact(significantTokens[0]))return false;
        var required=Math.Max(requiredSupport,1);
        return interpretiveSupport>=required||exclusiveHosts>=2||(exclusiveHosts>=1&&distinctHosts>=required+1);
    }

    private static bool IsNamedEntityRankingContract(WideQueryContract? queryContract)=>
        string.Equals(queryContract?.CandidateKind,CandidateKindNamedEntity,StringComparison.OrdinalIgnoreCase)
        ||(!string.IsNullOrWhiteSpace(queryContract?.EntityType)&&queryContract?.CandidateKind is null);

    private static bool IsContractScopeOrCategoryEcho(string name,WideQueryContract? queryContract)
    {
        if(!IsNamedEntityRankingContract(queryContract))return false;
        var candidateTokens=CanonicalTokens(name)
            .Select(token=>StemToken(token.Trim(',',':',';','.','(',')')))
            .Where(token=>token.Length>2&&!SubsetConnectiveTokens.Contains(token))
            .ToArray();
        if(candidateTokens.Length==0)return false;
        var contractText=string.Join(' ',new[]{queryContract?.EntityType,queryContract?.TargetObject,queryContract?.GeographicConstraint,queryContract?.RankingConcept,queryContract?.OutputShape}.Where(value=>!string.IsNullOrWhiteSpace(value))!);
        var contractTokens=BuildQueryTopicTokens(contractText);
        if(contractTokens.Count==0)return false;
        var echoed=candidateTokens.Count(contractTokens.Contains);
        if(echoed==candidateTokens.Length)return true;
        if(candidateTokens.Length<=2&&echoed>0&&ContainsAny(contractText,[name]))return true;
        return IsVenueRankingContract(queryContract)&&(IsGeographicScopeCandidate(name)||IsGenericContentTitleCandidate(name)||IsAcronymConnectorPhrase(name));
    }

    private static bool IsVenueRankingContract(WideQueryContract? queryContract)
    {
        var text=string.Join(' ',new[]{queryContract?.EntityType,queryContract?.TargetObject,queryContract?.RankingConcept,queryContract?.OutputShape}.Where(value=>!string.IsNullOrWhiteSpace(value))!);
        return text.Contains("venue",StringComparison.OrdinalIgnoreCase)
            ||text.Contains("pool hall",StringComparison.OrdinalIgnoreCase)
            ||text.Contains("billiard",StringComparison.OrdinalIgnoreCase)
            ||text.Contains("play pool",StringComparison.OrdinalIgnoreCase)
            ||text.Contains("places to play",StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGeographicScopeCandidate(string name)
    {
        var core=NormalizeCandidateDisplayName(name).Split('(')[0].Trim();
        var tokens=core.Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
        if(tokens.Length==0)return false;
        if(tokens.Any(token=>token.Equals("County",StringComparison.OrdinalIgnoreCase)||token.Equals("California",StringComparison.OrdinalIgnoreCase)||token.Equals("SoCal",StringComparison.OrdinalIgnoreCase)||token.Equals("NorCal",StringComparison.OrdinalIgnoreCase)))return true;
        return tokens.Length==2&&tokens[0] is "San" or "Santa" or "Los" or "Las";
    }

    private static bool IsGenericContentTitleCandidate(string name)
    {
        var tokens=CanonicalTokens(name).Select(token=>token.Trim(',',':',';','.','(',')')).Where(token=>token.Length>0).ToArray();
        return tokens.Length>0&&tokens[0].Equals("Best",StringComparison.OrdinalIgnoreCase)&&tokens.Any(token=>CandidateInvalidWords.Contains(token));
    }

    private static bool IsAcronymConnectorPhrase(string name)
    {
        var tokens=CanonicalTokens(name).Select(token=>token.Trim(',',':',';','.','(',')')).Where(token=>token.Length>0&&!SubsetConnectiveTokens.Contains(token)).ToArray();
        return tokens.Length>=2&&tokens.All(token=>token.Length is >=2 and <=5&&token.All(char.IsUpper));
    }

    private static bool IsActionCandidate(string name)
    {
        var trimmed=name.Trim();
        if(trimmed.Length<4||trimmed.Length>120)return false;
        var words=trimmed.Split(' ',StringSplitOptions.RemoveEmptyEntries);
        if(words.Length<2||words.Length>9)return false;
        var first=words[0].Trim(',',':',';','.','(',')').TrimEnd('s').ToLowerInvariant();
        if(ActionCandidateVerbs.Contains(first))return true;
        return words.Any(word=>ActionCandidateVerbs.Contains(word.Trim(',',':',';','.','(',')').TrimEnd('s')))
            &&words.Any(word=>word.Contains("flush",StringComparison.OrdinalIgnoreCase)||word.Contains("buffer",StringComparison.OrdinalIgnoreCase)||word.Contains("stall",StringComparison.OrdinalIgnoreCase)||word.Contains("threshold",StringComparison.OrdinalIgnoreCase)||word.Contains("profil",StringComparison.OrdinalIgnoreCase)||word.Contains("queue",StringComparison.OrdinalIgnoreCase));
    }

    // V3.4.1: tokens of the query itself (base query plus any clarification-constraint text), used to
    // detect topic-echo pseudo-candidates. Singular/plural tolerant via a trailing-'s' stem.
    private static HashSet<string> BuildQueryTopicTokens(string query)
        =>query.Split([' ',',',';','/','(',')','-','\u2014',':','.','?','!'],StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries)
            .Where(token=>token.Length>2)
            .Select(StemToken)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string StemToken(string token)=>token.Length>3&&token.EndsWith('s')?token[..^1]:token;

    // A candidate name whose canonical tokens ALL echo the query's own vocabulary is a topic label
    // ("Dividend Stocks", "High Yield Dividend"), not a competing entity. Names with at least one
    // token the query never mentioned ("Pfizer", "Realty Income") always survive.
    private static bool IsQueryTopicEcho(string name,HashSet<string> queryTopicTokens)
    {
        if(queryTopicTokens.Count==0)return false;
        var tokens=CanonicalTokens(name);
        return tokens.Length>0&&tokens.All(token=>token.Length<=2||queryTopicTokens.Contains(StemToken(token)));
    }

    // Stable methodology/criterion role detection. Unlike corpus-frequency genericity, this result
    // depends only on the candidate and the POLOXI query/branch contract for this execution. A label
    // whose identifying tokens are fully explained by one criterion branch is that criterion restated
    // as a candidate, not an independently named entity. Partial overlap is never enough, so proper
    // names that contain a criterion word remain eligible and must pass the normal evidence gates.
    private static bool IsMethodologyOrCriterionEcho(string name,IReadOnlyCollection<WideBranchRecord> branches)
    {
        var candidateTokens=CanonicalTokens(name)
            .Where(token=>token.Length>2)
            .Select(StemToken)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(candidateTokens.Count<2)return false;
        foreach(var branch in branches)
        {
            var branchTokens=BuildQueryTopicTokens($"{branch.DisplayName} {branch.Interpretation}");
            var explained=candidateTokens.Count(branchTokens.Contains);
            if(explained>=2&&candidateTokens.Count-explained<=1)return true;
        }
        return false;
    }

    private static bool IsUnsupportedMethodologyOrCriterionLabel(string name,IReadOnlyCollection<WideBranchRecord> branches,int exclusiveHosts)
        =>exclusiveHosts==0&&IsMethodologyOrCriterionEcho(name,branches);

    private enum CompetitionRole
    {
        ScoreableCriterion,
        HardConstraint,
        NonScoring
    }

    private static CompetitionRole ClassifyCompetitionRole(WideBranchRecord branch,WideQueryContract? queryContract)
    {
        if(IsHardConstraintBranch(branch,queryContract))return CompetitionRole.HardConstraint;
        if(IsNonScoringReasoningBranch(branch))return CompetitionRole.NonScoring;
        return CompetitionRole.ScoreableCriterion;
    }

    private static bool IsHardConstraintBranch(WideBranchRecord branch,WideQueryContract? queryContract)
    {
        var text=$"{branch.DisplayName} {branch.Interpretation}";
        if(ContainsAny(text,["hard constraint","constraint","eligibility","eligible only","must be","required to","only include"]))return true;
        if(queryContract?.HardConstraints is not{Count:>0})return false;
        var branchTokens=BuildQueryTopicTokens(text);
        foreach(var constraint in queryContract.HardConstraints)
        {
            var constraintTokens=BuildQueryTopicTokens(constraint).Where(token=>token.Length>3).ToArray();
            if(constraintTokens.Length<2)continue;
            var overlap=constraintTokens.Count(branchTokens.Contains);
            if(overlap>=Math.Min(3,constraintTokens.Length)&&overlap>=Math.Ceiling(constraintTokens.Length*.75m))return true;
        }
        return false;
    }

    private static bool IsNonScoringReasoningBranch(WideBranchRecord branch)
    {
        var text=$"{branch.DisplayName} {branch.Interpretation}";
        if(ContainsAny(text,["challenge the leader","challenge current leader","challenge leading candidate","challenge winner","stress-test the leader","stress test the leader","methodology to","method to","process instruction","evidence policy","required evidence quality","evidence quality","output requirement","answer format","citation requirement","cite sources","what does the user mean","clarify the meaning","interpret the request","determine appropriate time horizon","decide whether","investigate whether","retrieve evidence","gather evidence","information round"]))return true;
        return false;
    }

    private static bool IsGuardrailBranch(WideBranchRecord branch,WideQueryContract? queryContract)
    {
        var text=$"{branch.DisplayName} {branch.Interpretation}";
        if(ContainsAny(text,["minimum","acceptable","avoid","must","required","requirement","only include","eligibility","hard constraint","constraint","veto","disqualify","exclude"]))return true;
        if(queryContract?.HardConstraints is not{Count:>0})return false;
        return queryContract.HardConstraints.Any(constraint=>ContainsAny($"{constraint} {text}",["within budget","not too expensive","budget","maximum price","under $","below $","must be affordable","minimum safety","safe only","low crime required"]));
    }

    private static decimal ComputeGuardrailPenalty(IReadOnlyCollection<WideBranchRecord> branches,IReadOnlyDictionary<Guid,decimal> scores,WideConfiguration configuration,WideQueryContract? queryContract)
    {
        var veto=Math.Clamp(configuration.GuardrailVetoThreshold,0,1);
        var threshold=Math.Clamp(configuration.GuardrailAcceptableThreshold,0,1);
        if(threshold<=veto)threshold=Math.Min(1m,veto+.01m);
        var exponent=Math.Clamp(configuration.GuardrailPenaltyExponent,.01m,5m);
        var penalty=1m;
        foreach(var branch in branches.Where(branch=>IsGuardrailBranch(branch,queryContract)))
        {
            if(!scores.TryGetValue(branch.WideBranchId,out var score))continue;
            score=Math.Clamp(score,0,1);
            if(score>=threshold)continue;
            if(score<=veto)return 0m;
            var normalized=(score-veto)/(threshold-veto);
            penalty*=Math.Clamp((decimal)Math.Pow((double)normalized,(double)exponent),0,1);
        }
        return Math.Clamp(penalty,0,1);
    }

    private static bool ContainsAny(string text,IReadOnlyCollection<string> phrases)
        =>phrases.Any(phrase=>text.Contains(phrase,StringComparison.OrdinalIgnoreCase));

    // V2.7.1 Candidate Identity Resolution: collapse alias variants of the SAME entity ("Mercury",
    // "Mercury Technologies Inc.", "Mercury (finance)") into one canonical candidate BEFORE the
    // Candidate × Branch competition. String similarity alone NEVER merges — a token-prefix relation
    // is only a merge HYPOTHESIS; the merge happens only when evidence establishes identity: at least
    // one source host attests the more specific form AND the alias and canonical share ≥1 source host.
    // An alias appearing only on hosts where the specific form never appears stays separate (it may be
    // a different entity with the same short name). Deterministic, zero-LLM.
    // V2.7.2: only corporate LEGAL suffixes are noise. Descriptive name parts like "Technologies"
    // are distinguishing tokens ("Mercury Technologies" vs "Mercury Systems") and must be preserved,
    // otherwise a fintech "Mercury Technologies" collapses to bare "Mercury" and falsely merges into
    // a different Mercury entity — a candidate recall loss.
    private static readonly string[] CanonicalNoiseSuffixes=["inc","inc.","llc","corp","corp.","co","co.","ltd","ltd.","company","corporation"];

    private static string[] CanonicalTokens(string name)
    {
        var core=NormalizeCandidateDisplayName(name).Split('(')[0].Split(',')[0].Trim();
        return core.Split(' ',StringSplitOptions.RemoveEmptyEntries)
            .Where(token=>!CanonicalNoiseSuffixes.Contains(token,StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    private static string NormalizeCandidateDisplayName(string name)
    {
        var trimmed=System.Text.RegularExpressions.Regex.Replace(name.Trim(),@"\s+"," ").Trim(' ',',',';',':','.','-','–','—');
        trimmed=System.Text.RegularExpressions.Regex.Replace(trimmed,@"^(?:in|near|around|within|across|throughout|inside|outside|from|for|to|at|on|with|about|regarding|including|include|includes|featuring|feature|features|ranked|ranking|best|top|the|a|an)\s+",string.Empty,System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant).Trim(' ',',',';',':','.','-','–','—');
        trimmed=System.Text.RegularExpressions.Regex.Replace(trimmed,@"\s+(?:is|are|was|were|has|have|offers|provides|features|includes|ranks|ranked|boasts)$",string.Empty,System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant).Trim(' ',',',';',':','.','-','–','—');
        while(trimmed.Length>0)
        {
            var cleaned=System.Text.RegularExpressions.Regex.Replace(trimmed,@"[\s,;:./\-–—]+(?:and|or|a|an)$",string.Empty,System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant).Trim(' ',',',';',':','.','-','–','—');
            if(cleaned.Length==trimmed.Length)return trimmed;
            trimmed=cleaned;
        }
        return trimmed;
    }

    private static IReadOnlyCollection<string> ExpandNormalizedCandidateNames(string name,WideQueryContract? queryContract)
    {
        var normalized=NormalizeCandidateDisplayName(name);
        if(normalized.Length==0)return [];
        var candidates=new List<string>{normalized};
        if(queryContract?.CandidateKind is CandidateKindActionableSolution or CandidateKindDiagnosticStep or CandidateKindProcedureStep)return candidates;
        var parts=System.Text.RegularExpressions.Regex.Split(normalized,@"\s+(?:and|&)\s+|\s*/\s*",System.Text.RegularExpressions.RegexOptions.IgnoreCase|System.Text.RegularExpressions.RegexOptions.CultureInvariant)
            .Select(NormalizeCandidateDisplayName)
            .Where(part=>part.Length>0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if(parts.Length is >=2 and <=4&&parts.All(part=>IsValidCandidateForContract(part,queryContract)))
            candidates.AddRange(parts);
        return candidates.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static IEnumerable<(string Name,string? Detail)> ExpandNormalizedCandidates(string name,string? detail,WideQueryContract? queryContract)
        =>ExpandNormalizedCandidateNames(name,queryContract).Select(expanded=>(expanded,detail));

    private static bool IsTokenPrefix(string[] shorter,string[] longer)
    {
        if(shorter.Length==0||shorter.Length>longer.Length)return false;
        for(var index=0;index<shorter.Length;index++)
            if(!string.Equals(shorter[index],longer[index],StringComparison.OrdinalIgnoreCase))return false;
        return true;
    }

    // V3.6.1 exclusive-mention test: a shorter name substring-matches every snippet that names a
    // longer entity starting with it ("Rolling Hills" matches every "Rolling Hills Estates"
    // snippet), so host overlap alone cannot prove identity for genuine prefix relations. The
    // shorter form is only a distinct entity when at least one snippet still mentions it AFTER all
    // occurrences of the longer form are removed — deterministic evidence that sources talk about
    // the short name on its own ("Rolling Hills is the smallest city on the Peninsula").
    private static bool HasExclusiveMention(string shorterName,string longerName,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        foreach(var snippet in knowledge)
        {
            var text=$"{snippet.Title} {snippet.Snippet}";
            if(!text.Contains(shorterName,StringComparison.OrdinalIgnoreCase))continue;
            var stripped=text.Replace(longerName,string.Empty,StringComparison.OrdinalIgnoreCase);
            if(stripped.Contains(shorterName,StringComparison.OrdinalIgnoreCase))return true;
        }
        return false;
    }

    private static HashSet<string> MentioningHosts(string name,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        var hosts=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var snippet in knowledge)
        {
            if(snippet.Title?.Contains(name,StringComparison.OrdinalIgnoreCase)!=true
                &&snippet.Snippet?.Contains(name,StringComparison.OrdinalIgnoreCase)!=true)continue;
            var host=Uri.TryCreate(snippet.Url,UriKind.Absolute,out var uri)?uri.Host:snippet.Url??string.Empty;
            if(!string.IsNullOrWhiteSpace(host))hosts.Add(host);
        }
        return hosts;
    }

    private static IReadOnlyList<(string Name,string? Detail)> CanonicalizeCandidates(IReadOnlyList<(string Name,string? Detail)> candidates,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,Dictionary<string,int> dimensionSupport)
    {
        candidates=candidates.Select(candidate=>(NormalizeCandidateDisplayName(candidate.Name),candidate.Detail)).Where(candidate=>candidate.Item1.Length>0).ToArray();
        if(candidates.Count<2)return candidates;
        // Most specific (most tokens) first so aliases merge INTO the specific canonical form.
        var ordered=candidates.Select(candidate=>(candidate.Name,candidate.Detail,Tokens:CanonicalTokens(candidate.Name))).OrderByDescending(item=>item.Tokens.Length).ToList();
        var canonical=new List<(string Name,string? Detail,string[] Tokens,HashSet<string> Hosts)>();
        var aliasTarget=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(var item in ordered)
        {
            var itemHosts=MentioningHosts(item.Name,externalKnowledge);
            var merged=false;
            // V2.7.2 Candidate Recall Preservation: an alias whose tokens have a prefix relation with
            // MORE THAN ONE canonical entry is ambiguous between distinct entities ("Mercury" vs
            // "Mercury Systems" AND "Mercury Business Services"). Its host overlap is evidence of
            // ambiguity, not identity — a bare short name substring-matches every snippet naming any
            // entity that starts with it. Ambiguous aliases stay separate and compete on their own.
            var prefixMatches=canonical.Where(existing=>IsTokenPrefix(item.Tokens,existing.Tokens)||IsTokenPrefix(existing.Tokens,item.Tokens)).ToArray();
            if(prefixMatches.Length==1)
            {
                var existing=prefixMatches[0];
                // V2.8.6 identity merge: EXACTLY equal core tokens ("Overland Park, Kansas" vs
                // "Overland Park" — both tokenize to [Overland, Park] after the comma/paren strip) are
                // the SAME name, not a prefix hypothesis — merge unconditionally. Host evidence is only
                // required for genuine prefix relations where the shorter form might be a different entity.
                var identicalTokens=item.Tokens.Length==existing.Tokens.Length;
                // Merge hypothesis — confirm with evidence identity: the specific form is attested by at
                // least one host, and the alias shares at least one host with it.
                var specificHosts=existing.Tokens.Length>=item.Tokens.Length?existing.Hosts:itemHosts;
                // V3.6.1: genuine prefix relations (non-identical tokens) additionally require that the
                // shorter name has NO exclusive mentions — substring host overlap is otherwise guaranteed
                // and would conflate distinct sibling entities ("Rolling Hills" vs "Rolling Hills Estates").
                var(shorterName,longerName)=item.Tokens.Length<=existing.Tokens.Length?(item.Name,existing.Name):(existing.Name,item.Name);
                var distinctEntityAttested=!identicalTokens&&HasExclusiveMention(shorterName,longerName,externalKnowledge);
                if(identicalTokens||(!distinctEntityAttested&&specificHosts.Count>0&&(itemHosts.Overlaps(existing.Hosts)||itemHosts.Count==0)))
                {
                    aliasTarget[item.Name]=existing.Name;
                    existing.Hosts.UnionWith(itemHosts);
                    merged=true;
                }
            }
            if(!merged)canonical.Add((item.Name,item.Detail,item.Tokens,itemHosts));
        }
        // Fold alias dimension support into the canonical entry (max wins — support is evidence-backed).
        foreach(var(alias,target)in aliasTarget)
        {
            var aliasSupport=dimensionSupport.GetValueOrDefault(alias);
            if(aliasSupport>dimensionSupport.GetValueOrDefault(target))dimensionSupport[target]=aliasSupport;
        }
        return canonical.Select(item=>(item.Name,item.Detail)).ToList();
    }

    private sealed record RankingCompletionResult(IReadOnlyCollection<WideCandidateDto> Candidates,int LlmCalls,bool RecoveryAttempted);

    private static bool RequiresRankingCompletion(WideConfiguration configuration,WideQueryContract? queryContract,bool isContentEnumeration,IReadOnlyCollection<WideInterpretiveResultDto> interpretiveResults)=>
        !isContentEnumeration&&interpretiveResults.Any(result=>result.Items.Count>0)
        &&(string.Equals(NormalizeAnswerKind(configuration,queryContract?.AnswerKind),AnswerKindEntityRanking,StringComparison.OrdinalIgnoreCase)
            ||string.Equals(queryContract?.OutputShape,"ranked_list",StringComparison.OrdinalIgnoreCase)
            ||(queryContract?.RequestedCount??0)>0
            ||!string.IsNullOrWhiteSpace(queryContract?.RankingConcept));

    private static int EffectiveRankingContractCount(WideConfiguration configuration,WideQueryContract? queryContract,bool rankingCompletionRequired)=>
        queryContract?.RequestedCount>0?queryContract.RequestedCount.Value:rankingCompletionRequired?Math.Min(5,Math.Max(1,configuration.MaximumCandidates)):0;

    private static int DeliveredCandidateCount(IReadOnlyCollection<WideCandidateDto> candidates)=>candidates.Count(candidate=>!candidate.IsConstraintViolation);

    private static bool RankingContractSatisfied(IReadOnlyCollection<WideCandidateDto> candidates,int requiredCount)=>
        requiredCount<=0?DeliveredCandidateCount(candidates)>0:DeliveredCandidateCount(candidates)>=requiredCount;

    private static decimal BranchAllocationWeight(WideBranchRecord branch)
    {
        var structuralPrior=branch.InterpretationPrior>0?branch.InterpretationPrior:branch.Confidence;
        var confidencePenalty=branch.PoloxiConfidence>0?branch.PoloxiConfidence:1m;
        var allocation=structuralPrior*confidencePenalty;
        return Math.Clamp(allocation,.0001m,1m);
    }

    // RFN-H Option B: Recursive Fractional Normalized Hierarchical weighting with a soft confidence
    // penalty. Sibling branches allocate by structural intent prior × POLOXI branch-support confidence,
    // so weakly supported dimensions lose influence without being hard-pruned. If a scored parent has
    // scored descendants, the parent becomes a container and only the deepest scored units receive global
    // weight.
    private static IReadOnlyDictionary<Guid,decimal> CompileRfnGlobalBranchWeights(IReadOnlyCollection<WideBranchRecord> hierarchy,IReadOnlyCollection<WideBranchRecord> scoringBranches)
    {
        var scoringById=scoringBranches.GroupBy(branch=>branch.WideBranchId).ToDictionary(group=>group.Key,group=>group.First());
        if(scoringById.Count==0)return new Dictionary<Guid,decimal>();
        var branchById=hierarchy.Concat(scoringBranches).GroupBy(branch=>branch.WideBranchId).ToDictionary(group=>group.Key,group=>group.First());
        var relevantIds=new HashSet<Guid>(scoringById.Keys);
        foreach(var branch in scoringById.Values)
        {
            var current=branch;
            while(current.ParentWideBranchId is Guid parentId&&branchById.TryGetValue(parentId,out var parent))
            {
                if(!relevantIds.Add(parent.WideBranchId))break;
                current=parent;
            }
        }
        var relevantBranches=branchById.Values.Where(branch=>relevantIds.Contains(branch.WideBranchId)).ToArray();
        var childrenByParent=relevantBranches.Where(branch=>branch.ParentWideBranchId is not null).GroupBy(branch=>branch.ParentWideBranchId!.Value).ToDictionary(group=>group.Key,group=>group.OrderBy(child=>child.SortOrder).ToArray());
        var roots=relevantBranches.Where(branch=>branch.ParentWideBranchId is null||!relevantIds.Contains(branch.ParentWideBranchId.Value)).OrderBy(branch=>branch.SortOrder).ToArray();
        var weights=new Dictionary<Guid,decimal>();
        void Visit(WideBranchRecord branch,decimal globalWeight)
        {
            if(!childrenByParent.TryGetValue(branch.WideBranchId,out var children)||children.Length==0)
            {
                if(scoringById.ContainsKey(branch.WideBranchId))weights[branch.WideBranchId]=weights.GetValueOrDefault(branch.WideBranchId)+globalWeight;
                return;
            }
            var total=children.Sum(BranchAllocationWeight);
            if(total<=0)total=children.Length;
            foreach(var child in children)
            {
                var local=total<=0?1m/children.Length:BranchAllocationWeight(child)/total;
                Visit(child,globalWeight*local);
            }
        }
        var rootTotal=roots.Sum(BranchAllocationWeight);
        if(rootTotal<=0)rootTotal=roots.Length;
        foreach(var root in roots)
        {
            var local=rootTotal<=0?1m/roots.Length:BranchAllocationWeight(root)/rootTotal;
            Visit(root,local);
        }
        if(weights.Count==0)
        {
            var total=scoringById.Values.Sum(BranchAllocationWeight);
            if(total<=0)total=scoringById.Count;
            foreach(var branch in scoringById.Values)weights[branch.WideBranchId]=BranchAllocationWeight(branch)/total;
        }
        var sum=weights.Values.Sum();
        return sum<=0?weights:weights.ToDictionary(entry=>entry.Key,entry=>entry.Value/sum);
    }

    private static IReadOnlyCollection<WideCandidateDto> PostProcessRankingCandidates(IReadOnlyCollection<WideCandidateDto> candidates,WideSearchRequest request,WideConfiguration configuration,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge)
    {
        if(candidates.Count>1)candidates=DeduplicateCandidatesByCanonicalTokens(candidates,externalKnowledge);
        if(!string.IsNullOrWhiteSpace(request.ClarificationAnswer)&&candidates.Count>1)
            candidates=ReweightCandidatesByClarificationAnswer(candidates,request.ClarificationAnswer,configuration.ClarificationReweightBoost);
        return candidates;
    }

    private async Task<RankingCompletionResult> CompleteRankingAsync(WideSearchRequest request,Guid executionId,WideQueryContract? queryContract,IReadOnlyCollection<WideBranchRecord> survivors,IReadOnlyCollection<WideInterpretiveResultDto> interpretiveResults,IReadOnlyCollection<string> discoveredCandidates,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,WideConfiguration configuration,int llmCalls,bool rankingCompletionRequired,int requiredCount,CancellationToken cancellationToken)
    {
        IReadOnlyCollection<WideCandidateDto> candidates=[];
        var recoveryAttempted=false;

        if(interpretiveResults.Count>0&&llmCalls<configuration.MaximumTotalLlmCalls)
        {
            candidates=await CompeteCandidatesAsync(request,executionId,queryContract,survivors,interpretiveResults,discoveredCandidates,externalKnowledge,configuration,cancellationToken);
            if(candidates.Count>0)llmCalls++;
            candidates=PostProcessRankingCandidates(candidates,request,configuration,externalKnowledge);
        }

        if(!rankingCompletionRequired||RankingContractSatisfied(candidates,requiredCount))return new(candidates,llmCalls,recoveryAttempted);

        if(llmCalls<configuration.MaximumTotalLlmCalls)
        {
            recoveryAttempted=true;
            var recovered=await CompeteCandidatesAsync(request,executionId,queryContract,survivors,interpretiveResults,discoveredCandidates,externalKnowledge,configuration,cancellationToken,isRecoveryPass:true);
            if(recovered.Count>0)
            {
                llmCalls++;
                recovered=PostProcessRankingCandidates(recovered,request,configuration,externalKnowledge);
                if(DeliveredCandidateCount(recovered)>DeliveredCandidateCount(candidates))candidates=recovered;
            }
        }

        if(!RankingContractSatisfied(candidates,requiredCount))
        {
            var fallback=BuildInterpretiveFallbackCandidates(request,queryContract,survivors,interpretiveResults,externalKnowledge,configuration);
            if(DeliveredCandidateCount(fallback)>DeliveredCandidateCount(candidates))candidates=fallback;
        }

        return new(candidates,llmCalls,recoveryAttempted);
    }

    private static IReadOnlyCollection<WideCandidateDto> BuildInterpretiveFallbackCandidates(WideSearchRequest request,WideQueryContract? queryContract,IReadOnlyCollection<WideBranchRecord> survivors,IReadOnlyCollection<WideInterpretiveResultDto> interpretiveResults,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,WideConfiguration configuration)
    {
        if(interpretiveResults.Count==0)return [];
        var queryTopicTokens=BuildQueryTopicTokens(request.Query);
        var branchIdentityKeys=survivors.Select(branch=>CandidateIdentityKey(branch.DisplayName)).Where(key=>key.Length>0).ToHashSet(StringComparer.Ordinal);
        var branchLookup=survivors.GroupBy(branch=>branch.DisplayName,StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key,group=>group.OrderByDescending(branch=>branch.PoloxiConfidence).First(),StringComparer.OrdinalIgnoreCase);
        var sourceBranches=interpretiveResults
            .Where(result=>result.Items.Count>0&&result.BranchStateCode!=WideBranchStates.Pruned)
            .Select(result=>(Result:result,Branch:branchLookup.GetValueOrDefault(result.BranchDisplayName)))
            .Where(item=>item.Branch is null||ClassifyCompetitionRole(item.Branch,queryContract)==CompetitionRole.ScoreableCriterion)
            .ToArray();
        if(sourceBranches.Length==0)return [];
        var scoringFallbackBranches=sourceBranches.Select(item=>item.Branch).Where(branch=>branch is not null).Select(branch=>branch!).ToArray();
        var branchWeights=scoringFallbackBranches.Length>0
            ?CompileRfnGlobalBranchWeights(survivors,scoringFallbackBranches).ToDictionary(entry=>scoringFallbackBranches.First(branch=>branch.WideBranchId==entry.Key).DisplayName,entry=>entry.Value,StringComparer.OrdinalIgnoreCase)
            :sourceBranches.ToDictionary(item=>item.Result.BranchDisplayName,item=>Math.Clamp(item.Result.Confidence,.05m,1m),StringComparer.OrdinalIgnoreCase);
        var totalBranchWeight=branchWeights.Values.Sum();
        if(totalBranchWeight<=0)return [];
        var requiredSupport=interpretiveResults.Count<=1?1:Math.Min(configuration.MinimumCandidateDimensionSupport,interpretiveResults.Count);
        var requestedCount=queryContract?.RequestedCount??0;
        var finalCount=Math.Max(configuration.MaximumCandidates,requestedCount);
        var candidates=new Dictionary<string,(string Name,string? Detail,Dictionary<string,decimal> Scores,HashSet<string> Dimensions)>(StringComparer.Ordinal);
        foreach(var(result,_)in sourceBranches)
        {
            var items=result.Items.Where(item=>!string.IsNullOrWhiteSpace(item.Name)).OrderBy(item=>item.RankNumber).ToArray();
            if(items.Length==0)continue;
            var maxRank=Math.Max(items.Max(item=>item.RankNumber),items.Length);
            foreach(var item in items)
            {
                var normalized=NormalizeCandidateDisplayName(item.Name);
                if(!IsValidCandidateForContract(normalized,queryContract)||IsQueryTopicEcho(normalized,queryTopicTokens)||branchIdentityKeys.Contains(CandidateIdentityKey(normalized)))continue;
                var key=CandidateIdentityKey(normalized);
                if(key.Length==0)continue;
                var rankScore=Math.Clamp((decimal)(maxRank-Math.Max(item.RankNumber,1)+1)/Math.Max(maxRank,1),0,1);
                var branchScore=Math.Clamp(.65m*rankScore+.35m*result.Confidence,0,1);
                if(!candidates.TryGetValue(key,out var existing))
                {
                    existing=(normalized,string.IsNullOrWhiteSpace(item.Detail)?null:item.Detail.Trim(),new(StringComparer.OrdinalIgnoreCase),new(StringComparer.OrdinalIgnoreCase));
                    candidates[key]=existing;
                }
                if(normalized.Length>existing.Name.Length)existing.Name=normalized;
                if(string.IsNullOrWhiteSpace(existing.Detail)&&!string.IsNullOrWhiteSpace(item.Detail))existing.Detail=item.Detail.Trim();
                if(!existing.Scores.TryGetValue(result.BranchDisplayName,out var current)||branchScore>current)existing.Scores[result.BranchDisplayName]=branchScore;
                existing.Dimensions.Add(result.BranchDisplayName);
                candidates[key]=existing;
            }
        }
        if(candidates.Count==0)return [];
        var ranked=candidates.Values.Select(candidate=>
        {
            var weightedQuality=branchWeights.Sum(branch=>branch.Value*candidate.Scores.GetValueOrDefault(branch.Key))/totalBranchWeight;
            var coverage=Math.Clamp(candidate.Scores.Keys.Where(branchWeights.ContainsKey).Sum(key=>branchWeights[key])/totalBranchWeight,0,1);
            var hosts=CountDistinctSourceHosts(candidate.Name,externalKnowledge);
            var evidenceConfidence=Math.Clamp((hosts<=1?.70m:Math.Min(1m,.70m+.15m*(hosts-1)))*(.5m+.5m*coverage),0,1);
            var support=Math.Max(candidate.Dimensions.Count,Math.Min(hosts,interpretiveResults.Count));
            var tier=support>=requiredSupport?"STRONG":candidate.Dimensions.Count+hosts>=requiredSupport?"MODERATE":"LIMITED";
            var composite=Math.Clamp(.85m*weightedQuality+.15m*Math.Min(1m,(decimal)hosts/Math.Max(requiredSupport,1)),0,1);
            return new WideCandidateDto(Guid.NewGuid(),0,candidate.Name,candidate.Detail,composite,candidate.Scores.OrderByDescending(score=>score.Value).Select(score=>new WideCandidateBranchScoreDto(score.Key,score.Value)).ToArray())
            {
                EvidenceCoverage=coverage,
                QualityScore=composite,
                EvidenceConfidence=evidenceConfidence,
                AdmissionModeCode="INTERPRETIVE_FALLBACK",
                SupportTierCode=tier,
                InterpretiveSupportCount=candidate.Dimensions.Count,
                EvidenceHostSupportCount=hosts,
                TotalSupportCount=candidate.Dimensions.Count+hosts
            };
        })
        .OrderByDescending(candidate=>candidate.CompositeScore)
        .ThenByDescending(candidate=>candidate.InterpretiveSupportCount)
        .ThenByDescending(candidate=>candidate.EvidenceHostSupportCount)
        .ThenBy(candidate=>candidate.DisplayName,StringComparer.OrdinalIgnoreCase)
        .Take(finalCount)
        .Select((candidate,index)=>candidate with{RankNumber=index+1})
        .ToArray();
        return ranked;
    }

    private async Task<IReadOnlyCollection<WideCandidateDto>> CompeteCandidatesAsync(WideSearchRequest request,Guid executionId,WideQueryContract? queryContract,IReadOnlyCollection<WideBranchRecord> survivors,IReadOnlyCollection<WideInterpretiveResultDto> interpretiveResults,IReadOnlyCollection<string> discoveredCandidates,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,WideConfiguration configuration,CancellationToken cancellationToken,bool isRecoveryPass=false)
    {
        // V2.9.3 Recovery Pass invariant: POLOXI never relaxes evidence requirements to fill Top N;
        // recovery only recognizes additional INDEPENDENT support that the normal candidate-discovery
        // path did not fully credit. requiredSupport is NEVER lowered. In recovery mode:
        //   - discovery admits single-host evidence names into the SCORED pool (they still face the gate),
        //   - the admission gate credits RecoverySupport = DistinctInterpretiveDimensions +
        //     DistinctEvidenceHosts (repeat mentions within one branch and repeat articles from one
        //     host each count once — support is signal diversity, never raw mention count).
        var minimumDiscoverySourceHosts=isRecoveryPass?1:2;
        // V3.4.1 Query-Topic Echo filter: the evidence-harvest and LLM lists can echo the query's own
        // topic vocabulary as pseudo-candidates ("High Yield Dividend", "Dividend Stocks" for a
        // dividend-stock ranking) because article titles repeat the query terms across many sources.
        // Continuation evidence reuse widened the pool and made these echoes clear the 2-host gate.
        // A name whose canonical tokens ALL appear in the query text (singular/plural tolerant)
        // describes the topic, not a competing entity - deterministically rejected from the pool.
        // Real entities (Pfizer, Realty Income) are never named inside a ranking query.
        var queryTopicTokens=BuildQueryTopicTokens(request.Query);
        try
        {
            // V3 semantic firewall: keep POLOXI's existing broad narrowing behavior, but protect only
            // the numerical Candidate × Branch matrix. Root/surviving branches still guide narrowing,
            // evidence, uncertainty, and explanation; only obvious process/constraint artifacts are
            // prevented from becoming bogus candidate scores (for example "Challenge the leader = 100%").
            var topSurvivors=survivors.Where(branch=>branch.BranchStateCode is WideBranchStates.Active or WideBranchStates.Secondary).OrderByDescending(branch=>branch.PoloxiConfidence).Take(8);
            var rootDimensions=survivors.Where(branch=>branch.LevelNumber==1
                &&branch.BranchStateCode!=WideBranchStates.Pruned)
                .OrderByDescending(branch=>branch.PoloxiConfidence)
                .Take(6);
            var competitionCandidates=topSurvivors.Concat(rootDimensions).DistinctBy(branch=>branch.WideBranchId).OrderByDescending(branch=>branch.PoloxiConfidence).Take(10).ToArray();
            var branches=competitionCandidates.Where(branch=>ClassifyCompetitionRole(branch,queryContract)==CompetitionRole.ScoreableCriterion).ToArray();
            if(branches.Length==0)return [];
            // V3.5 Hierarchical Roll-Up: the progressive-narrowing children of each scoring dimension
            // carry concrete meaning ("Safe Environment" -> "Low Violent Crime Rate", "Police Response
            // Time"). Scoring those children and rolling them up makes each parent dimension's score
            // derived from specifics instead of one coarse judgment \u2014 WITHOUT double counting, because
            // the composite still sums over the parent branches only. Children are capped per parent
            // and by a confidence floor so the prompt stays bounded and noise branches stay out.
            var scoringBranchIds=branches.Select(branch=>branch.WideBranchId).ToHashSet();
            var childBranches=survivors
                .Where(branch=>branch.ParentWideBranchId is not null&&scoringBranchIds.Contains(branch.ParentWideBranchId.Value)
                    &&!branch.IsEliminated&&branch.PoloxiConfidence>=.15m&&!scoringBranchIds.Contains(branch.WideBranchId))
                .GroupBy(branch=>branch.ParentWideBranchId!.Value)
                .SelectMany(group=>group.OrderByDescending(branch=>branch.PoloxiConfidence).Take(5))
                .Take(20)
                .ToArray();
            var childrenByParent=childBranches.ToLookup(branch=>branch.ParentWideBranchId!.Value);
            // V2.3 candidate admission: a candidate must appear in enough distinct interpretive
            // dimensions to compete for the OVERALL answer. Appearing in a single interpretive list
            // (for example an affordability-only ranking) is not cross-dimensional support; such
            // candidates are flagged as exclusions with a reason \u2014 kept visible, never silently dropped.
            var dimensionSupport=interpretiveResults
                .SelectMany(result=>result.Items.SelectMany(item=>ExpandNormalizedCandidateNames(item.Name,queryContract).Select(name=>(result.BranchDisplayName,Name:name))))
                .Distinct(new CandidateDimensionComparer())
                .GroupBy(entry=>entry.Name,StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group=>group.Key,group=>group.Count(),StringComparer.OrdinalIgnoreCase);
            var requiredSupport=interpretiveResults.Count<=1?1:Math.Min(configuration.MinimumCandidateDimensionSupport,interpretiveResults.Count);
            // V2.5 cardinality: an explicit requested count ("top 10") controls the delivered ranking.
            // Discovery is deliberately wider so the average interpretive candidate list is only one
            // signal, not the ceiling for the final competition.
            var requestedCount=queryContract?.RequestedCount??0;
            var finalCount=Math.Max(configuration.MaximumCandidates,requestedCount);
            var discoveryCount=Math.Min(100,Math.Max(finalCount*4,25));
            // V2.7 Candidate Discovery: the competition scores the FULL candidate universe, not just the
            // candidates the interpretive lists happened to name. Evidence-harvested candidates (strong
            // entities named by retrieved sources — e.g. a #1 city in a live ranking) are merged into the
            // scored pool after deterministic validity filtering. Category/placeholder phrases are
            // rejected everywhere — they are descriptions, not entities.
            // V3.4.6 Branch-Echo Rejection: mini-tier models sometimes emit a branch's own display name
            // ("Low Violent Crime Rates") as an interpretive result item. A candidate whose canonical
            // identity equals any hierarchy branch name is a dimension label leaking into the pool, never
            // a competing entity.
            var branchIdentityKeys=survivors.Select(branch=>CandidateIdentityKey(branch.DisplayName)).Where(key=>key.Length>0).ToHashSet(StringComparer.Ordinal);
            // V3.11: IsContractScopeOrCategoryEcho previously ran only at the late admission gate, so
            // scope echoes (the query's own state/region) entered the pool, inflated N (corrupting the
            // entropy basis) and wasted matrix tokens before being ruled out. Same check, applied at
            // pool entry — semantics unchanged, pollution prevented.
            var interpretiveCandidates=interpretiveResults.SelectMany(result=>result.Items.SelectMany(item=>ExpandNormalizedCandidates(item.Name,item.Detail,queryContract))).Where(item=>IsValidCandidateForContract(item.Name,queryContract)&&!IsQueryTopicEcho(item.Name,queryTopicTokens)&&!IsContractScopeOrCategoryEcho(item.Name,queryContract)&&!branchIdentityKeys.Contains(CandidateIdentityKey(item.Name))).GroupBy(item=>CandidateIdentityKey(item.Name),StringComparer.Ordinal).Select(group=>group.OrderByDescending(item=>item.Name.Length).First()).ToArray();
            var knownNames=interpretiveCandidates.Select(item=>item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Discovered candidates ranked by independent-source diversity so the strongest evidence-named
            // entities are merged first; annotated so their admission path is visible and explainable.
            var evidenceCandidates=discoveredCandidates
                .SelectMany(name=>ExpandNormalizedCandidateNames(name,queryContract))
                .Where(name=>IsValidCandidateForContract(name,queryContract)&&!knownNames.Contains(name)&&!IsQueryTopicEcho(name,queryTopicTokens)&&!IsContractScopeOrCategoryEcho(name,queryContract)&&!branchIdentityKeys.Contains(CandidateIdentityKey(name)))
                .Select(name=>(Name:name,Hosts:CountDistinctSourceHosts(name,externalKnowledge)))
                .Where(item=>item.Hosts>=minimumDiscoverySourceHosts)
                .OrderByDescending(item=>item.Hosts)
                .Take(discoveryCount)
                .Select(item=>(item.Name,Detail:(string?)$"Discovered from retrieved evidence ({item.Hosts} independent sources)."))
                .ToArray();
            var provisionalCandidateNames=CanonicalizeCandidates(interpretiveCandidates.Concat(evidenceCandidates).ToArray(),externalKnowledge,dimensionSupport).ToArray();
            if(provisionalCandidateNames.Length==0)return [];
            var provisionalPoolNames=provisionalCandidateNames.Select(candidate=>candidate.Name).ToArray();
            var provisionalExclusiveHosts=CountExclusiveSourceHosts(provisionalPoolNames,externalKnowledge);
            var candidateNames=provisionalCandidateNames
                .Select(candidate=>(Candidate:candidate,InterpretiveSupport:dimensionSupport.GetValueOrDefault(candidate.Name),EvidenceHosts:CountDistinctSourceHosts(candidate.Name,externalKnowledge),ExclusiveHosts:provisionalExclusiveHosts.GetValueOrDefault(candidate.Name)))
                .Where(item=>!IsUnsupportedMethodologyOrCriterionLabel(item.Candidate.Name,branches,item.ExclusiveHosts)
                    &&(item.EvidenceHosts>0||item.InterpretiveSupport>0))
                .OrderByDescending(item=>item.InterpretiveSupport)
                .ThenByDescending(item=>item.EvidenceHosts)
                .ThenByDescending(item=>item.ExclusiveHosts)
                .ThenBy(item=>item.Candidate.Name.Length)
                .Take(discoveryCount)
                .Select(item=>item.Candidate)
                .ToArray();
            // V2.8.2 Support Lineage Repair: dimensionSupport is keyed by RAW interpretive item names
            // ("Mercury (fintech platform)"). Canonicalization (V2.7.2) no longer merges ambiguous
            // aliases and attribute-hypotheses (V2.8.1) never enter the candidate list, so their
            // per-dimension support was stranded on names that no longer compete — canonical entities
            // then failed the cross-dimensional admission gate with support=1 and were zeroed. The
            // V2.7 evidence-based support rule now applies to EVERY canonical candidate, not only
            // evidence-discovered ones: independent source hosts attesting the entity are the
            // admission evidence, capped by the number of interpretation dimensions.
            foreach(var(name,_)in candidateNames)
            {
                var mentionHosts=CountDistinctSourceHosts(name,externalKnowledge);
                var evidenceSupport=Math.Min(mentionHosts,interpretiveResults.Count);
                if(evidenceSupport>dimensionSupport.GetValueOrDefault(name))dimensionSupport[name]=evidenceSupport;
            }
            // V2.9.3: pure interpretive-dimension support per candidate (distinct dimensions only —
            // the (dimension, candidate) pairs above are already deduplicated, so repeated mentions
            // inside one interpretive branch count as ONE branch-support signal). Kept separate from
            // the host-augmented dimensionSupport so recovery support and audit provenance are exact.
            var interpretiveSupport=interpretiveResults
                .SelectMany(result=>result.Items.SelectMany(item=>ExpandNormalizedCandidateNames(item.Name,queryContract).Select(name=>(result.BranchDisplayName,Name:name))))
                .Distinct(new CandidateDimensionComparer())
                .GroupBy(entry=>entry.Name,StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group=>group.Key,group=>group.Count(),StringComparer.OrdinalIgnoreCase);
            if(candidateNames.Length==0)return [];
            // V3.10 EEA + FD: exclusive-host attribution and fragment domination are computed once
            // over the FINAL candidate pool (post-canonicalization) so every merit decision below
            // uses the same run-relative, deterministic signals. Zero LLM calls.
            var poolNames=candidateNames.Select(candidate=>candidate.Name).ToArray();
            var exclusiveHostCounts=CountExclusiveSourceHosts(poolNames,externalKnowledge);
            var dominatedFragments=FindDominatedFragments(poolNames,exclusiveHostCounts);
            var branchList=string.Join('\n',branches.Select((branch,index)=>$"B{index+1}. {branch.DisplayName}: {branch.Interpretation}"));
            // V3.5: child sub-criteria are appended as S-labelled lines referencing their parent so the
            // model scores every candidate on the narrowed specifics too. They do NOT enter the
            // composite directly — they feed the parent's roll-up.
            if(childBranches.Length>0)
            {
                var parentIndexById=branches.Select((branch,index)=>(branch.WideBranchId,Index:index+1)).ToDictionary(pair=>pair.WideBranchId,pair=>pair.Index);
                var childList=string.Join('\n',childBranches.Select((branch,index)=>$"S{index+1} (sub-criterion of B{parentIndexById[branch.ParentWideBranchId!.Value]}). {branch.DisplayName}: {branch.Interpretation}"));
                branchList=$"{branchList}\n{childList}";
            }
            var contractContext=queryContract is null?"(none)":BuildQueryContractContext(queryContract);
            var candidateKind=queryContract?.CandidateKind??CandidateKindNamedEntity;
            // V3.11 Matrix Chunking + Re-Ask: a single Candidate × Branch call over a large pool
            // (40 candidates × 20+ branch lines) exceeds what mini-tier models reliably echo in one
            // structured response — most of the pool silently came back unscored and every omitted
            // candidate fell to the ruled-out floor. The matrix is now scored in bounded chunks against
            // the SAME branches, prompt, and schema, and pool candidates still unresolved after the
            // chunked pass get exactly one targeted re-ask call. Each chunk is fail-soft so one bad
            // response degrades coverage instead of zeroing the whole competition.
            async Task<List<WideCandidateScore>> ScoreCandidateChunkAsync((string Name,string? Detail)[] chunk)
            {
                try
                {
                    var candidateList=string.Join('\n',chunk.Select((candidate,index)=>$"C{index+1}. {candidate.Name}: {candidate.Detail}"));
                    var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_ANSWER",
                        await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.WideCandidateMatrix,cancellationToken),
                        $"Question: {request.Query}\n{contractContext}\nCandidate kind: {candidateKind}\nInterpretation branches:\n{branchList}\nCandidates:\n{candidateList}",
                        CandidateScoringSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_CANDIDATE_MATRIX",executionId,request.CorrelationId,"Intelligent Search Wide"),ModelOverride(request),cancellationToken);
                    var chunkProposal=JsonSerializer.Deserialize<WideCandidateScoringProposal>(result.Content,JsonOptions);
                    return chunkProposal?.Candidates?.ToList()??[];
                }
                catch(Exception)when(!cancellationToken.IsCancellationRequested)
                {
                    return [];
                }
            }
            // V2.8.2 Candidate Identity Resolution on echo: candidates are prompted as "C<n>. Name:
            // Detail" — the echoed name may carry the label or detail. Resolve back to the SUPPLIED
            // canonical name so dimension-support admission and evidence lookups key on the same
            // identity the pipeline built, not on the model's echo formatting.
            string? ResolveCandidateName(string echoed)
            {
                var cleaned=echoed.Trim();
                var labelMatch=System.Text.RegularExpressions.Regex.Match(cleaned,@"^C\d+\.\s*");
                if(labelMatch.Success)cleaned=cleaned[labelMatch.Length..].Trim();
                if(candidateNames.Any(item=>string.Equals(item.Name,cleaned,StringComparison.OrdinalIgnoreCase)))return cleaned;
                var colon=cleaned.IndexOf(':');
                var withoutDetail=colon>0?cleaned[..colon].Trim():cleaned;
                var supplied=candidateNames.FirstOrDefault(item=>string.Equals(item.Name,withoutDetail,StringComparison.OrdinalIgnoreCase));
                if(supplied.Name is not null)return supplied.Name;
                // Canonical-identity fallback: models often echo a qualified form of the supplied name
                // ("Iloilo City, Iloilo" for pool name "Iloilo City"). A strict string miss silently
                // dropped the candidate's scores. Resolve via CandidateIdentityKey so qualifier
                // phrasing variants map back to the supplied canonical name; ambiguity still fails.
                var echoKey=CandidateIdentityKey(withoutDetail);
                if(echoKey.Length==0)return null;
                var byIdentity=candidateNames.Where(item=>string.Equals(CandidateIdentityKey(item.Name),echoKey,StringComparison.Ordinal)).ToArray();
                return byIdentity.Length==1?byIdentity[0].Name:null;
            }
            var scoredCandidates=new List<WideCandidateScore>();
            var resolvedPoolNames=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            // Merge dedupes on the RESOLVED pool identity so the re-ask pass can never double-score a
            // candidate the chunked pass already covered; unresolvable echoes flow through unchanged
            // (they are dropped later by the existing resolution logic, exactly as before).
            void MergeScores(IEnumerable<WideCandidateScore> scores)
            {
                foreach(var score in scores)
                {
                    var resolved=ResolveCandidateName(score.Name);
                    if(resolved is not null&&!resolvedPoolNames.Add(resolved))continue;
                    scoredCandidates.Add(score);
                }
            }
            // V3.11.1 Anchor Calibration: chunk-local scoring has NO shared basis — the model grades
            // each chunk relatively, so every chunk produced its own 100% "winner" per branch and the
            // merged matrix carried several fabricated per-branch maxima (multiple 100%s on the same
            // Safety branch). The top interpretive-support candidates now ride along in EVERY chunk as
            // shared anchors; each later chunk's branch scores are rescaled so its anchor scores align
            // with the reference chunk's anchor scores, giving all chunks one common scoring basis.
            // Anchors are scored once (merge dedupes on resolved identity), never re-admitted twice.
            var anchorCandidates=candidateNames.Take(Math.Min(3,candidateNames.Length)).ToArray();
            var anchorNames=new HashSet<string>(anchorCandidates.Select(anchor=>anchor.Name),StringComparer.OrdinalIgnoreCase);
            // Reference anchor branch scores from the FIRST chunk that returns them.
            var anchorReference=new Dictionary<string,Dictionary<string,decimal>>(StringComparer.OrdinalIgnoreCase);
            List<WideCandidateScore> CalibrateChunk(List<WideCandidateScore> chunkScores)
            {
                if(chunkScores.Count==0)return chunkScores;
                var chunkAnchors=chunkScores
                    .Select(score=>(Score:score,Resolved:ResolveCandidateName(score.Name)))
                    .Where(pair=>pair.Resolved is not null&&anchorNames.Contains(pair.Resolved))
                    .ToArray();
                if(anchorReference.Count==0)
                {
                    foreach(var(score,resolved)in chunkAnchors)
                        anchorReference[resolved!]=score.BranchScores.GroupBy(branchScore=>branchScore.BranchDisplayName,StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key,group=>group.First().EvidenceScore,StringComparer.OrdinalIgnoreCase);
                    return chunkScores;
                }
                // Per-branch scale = mean(reference anchor score)/mean(this chunk's anchor score).
                var scaleByBranch=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
                foreach(var branchName in chunkAnchors.SelectMany(pair=>pair.Score.BranchScores.Select(branchScore=>branchScore.BranchDisplayName)).Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    var pairs=chunkAnchors
                        .Select(pair=>(Chunk:pair.Score.BranchScores.FirstOrDefault(branchScore=>string.Equals(branchScore.BranchDisplayName,branchName,StringComparison.OrdinalIgnoreCase))?.EvidenceScore,
                            Reference:pair.Resolved is not null&&anchorReference.TryGetValue(pair.Resolved,out var reference)&&reference.TryGetValue(branchName,out var referenceScore)?referenceScore:(decimal?)null))
                        .Where(pair=>pair.Chunk is>0m&&pair.Reference is not null)
                        .ToArray();
                    if(pairs.Length==0)continue;
                    var scale=pairs.Average(pair=>pair.Reference!.Value)/pairs.Average(pair=>pair.Chunk!.Value);
                    // Only deflate — inflating non-anchor scores would fabricate strength.
                    if(scale<1m)scaleByBranch[branchName]=scale;
                }
                if(scaleByBranch.Count==0)return chunkScores;
                return chunkScores.Select(score=>
                {
                    var resolved=ResolveCandidateName(score.Name);
                    if(resolved is not null&&anchorNames.Contains(resolved))return score;
                    var rescaled=score.BranchScores.Select(branchScore=>scaleByBranch.TryGetValue(branchScore.BranchDisplayName,out var scale)
                        ?branchScore with{EvidenceScore=Math.Clamp(branchScore.EvidenceScore*scale,0,1)}
                        :branchScore).ToArray();
                    return score with{BranchScores=rescaled};
                }).ToList();
            }
            var nonAnchorPool=candidateNames.Where(candidate=>!anchorNames.Contains(candidate.Name)).ToArray();
            var matrixChunks=new List<(string Name,string? Detail)[]>();
            foreach(var chunkBody in nonAnchorPool.Chunk(12-anchorCandidates.Length))
                matrixChunks.Add(anchorCandidates.Concat(chunkBody).ToArray());
            if(matrixChunks.Count==0&&anchorCandidates.Length>0)matrixChunks.Add(anchorCandidates);
            foreach(var chunk in matrixChunks)MergeScores(CalibrateChunk(await ScoreCandidateChunkAsync(chunk)));
            var unresolvedCandidates=candidateNames.Where(candidate=>!resolvedPoolNames.Contains(candidate.Name)).ToArray();
            if(unresolvedCandidates.Length>0&&scoredCandidates.Count>0)MergeScores(CalibrateChunk(await ScoreCandidateChunkAsync(anchorCandidates.Concat(unresolvedCandidates.Where(candidate=>!anchorNames.Contains(candidate.Name))).ToArray())));
            var proposal=new WideCandidateScoringProposal(scoredCandidates);
            if(proposal.Candidates is not{Count:>0})return [];
            static decimal InterpretationWeight(WideBranchRecord branch)=>BranchAllocationWeight(branch);
            var rfnBranchWeights=CompileRfnGlobalBranchWeights(survivors.Concat(childBranches).ToArray(),branches.Concat(childBranches).ToArray());
            if(rfnBranchWeights.Count==0)return [];
            var metricScores=CompileMetricNormalizedScores(candidateNames,branches.Concat(childBranches).ToArray(),externalKnowledge);
            var branchesByName=branches.Concat(childBranches).GroupBy(branch=>branch.DisplayName.Trim(),StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key,group=>group.First(),StringComparer.OrdinalIgnoreCase);
            // V2.8.2 Branch Identity Resolution: the prompt labels branches "B1. Name: Interpretation",
            // and models sometimes echo the label or append the interpretation. A strict dictionary miss
            // silently dropped the score — zeroing coverage and Quality for EVERY candidate while the
            // evidence was intact. Resolution now strips "B<n>." label prefixes and trailing ": ..."
            // decorations, then falls back to unambiguous containment before giving up.
            WideBranchRecord? ResolveBranch(string echoed)
            {
                var cleaned=echoed.Trim();
                var labelMatch=System.Text.RegularExpressions.Regex.Match(cleaned,@"^[BS]\d+(\s*\([^)]*\))?\.\s*");
                if(labelMatch.Success)cleaned=cleaned[labelMatch.Length..].Trim();
                var colon=cleaned.IndexOf(':');
                var withoutDetail=colon>0?cleaned[..colon].Trim():cleaned;
                if(branchesByName.TryGetValue(cleaned,out var branch))return branch;
                if(branchesByName.TryGetValue(withoutDetail,out branch))return branch;
                var contains=branches.Concat(childBranches).Where(candidate=>cleaned.Contains(candidate.DisplayName,StringComparison.OrdinalIgnoreCase)||candidate.DisplayName.Contains(withoutDetail,StringComparison.OrdinalIgnoreCase)).ToArray();
                return contains.Length==1?contains[0]:null;
            }
            var entries=new List<(WideCandidateRecord Record,bool SupportExcluded,decimal RawComposite)>();
            var evidenceConfidences=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
            // V3.5: per-candidate roll-up disclosure (parent dimension -> direct score + child scores).
            var rollUpDisclosures=new Dictionary<string,Dictionary<string,(decimal Direct,IReadOnlyCollection<WideCandidateChildScoreDto> Children)>>(StringComparer.OrdinalIgnoreCase);
            // V2.9.3 admission provenance per candidate: mode, interpretive-dimension support,
            // independent-host support, and the total support credited at admission time.
            var admissionInfo=new Dictionary<string,(string Mode,int Interpretive,int Hosts,int Total)>(StringComparer.OrdinalIgnoreCase);
            // V2.9.4 support tier per candidate (STRONG/MODERATE/LIMITED) for transparent disclosure.
            var supportTiers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            // V3.6 pass 1: resolve each candidate's direct + child scores and rolled-up effective
            // dimension scores. Composites are computed in pass 2, AFTER cross-candidate contrast
            // normalization, because the confidence-weighted roll-up (a mean of sub-scores) compresses
            // per-dimension differences between candidates and flattens the final ranking.
            var prepared=proposal.Candidates.Select(candidate=>
            {
                var candidateId=Guid.NewGuid();
                var resolvedName=ResolveCandidateName(candidate.Name);
                if(resolvedName is null)return null;
                // V3.5: split resolved scores into parent-dimension scores and child sub-criterion scores.
                var directScores=new Dictionary<Guid,decimal>();
                var childScoresByParent=new Dictionary<Guid,List<(WideBranchRecord Child,decimal Score)>>();
                foreach(var score in candidate.BranchScores??[])
                {
                    var branch=ResolveBranch(score.BranchDisplayName);
                    if(branch is null)continue;
                    // V3.10.5: a trusted deterministic metric (tightened branch gate + unit-aware
                    // extraction + ≥2 independent hosts) is BLENDED 50/50 with the model's evidence
                    // judgment instead of replacing it — the metric grounds the score while the LLM
                    // signal prevents min-max normalization artifacts (0%/100%) from dominating.
                    var llmScore=Math.Clamp(score.EvidenceScore,0,1);
                    var clamped=metricScores.TryGetValue(resolvedName,out var candidateMetrics)&&candidateMetrics.TryGetValue(branch.WideBranchId,out var metricScore)?Math.Clamp(0.5m*metricScore+0.5m*llmScore,0,1):llmScore;
                    if(branch.ParentWideBranchId is not null&&childrenByParent.Contains(branch.ParentWideBranchId.Value)&&!scoringBranchIds.Contains(branch.WideBranchId))
                    {
                        var list=childScoresByParent.TryGetValue(branch.ParentWideBranchId.Value,out var existing)?existing:childScoresByParent[branch.ParentWideBranchId.Value]=[];
                        if(!list.Any(entry=>entry.Child.WideBranchId==branch.WideBranchId))list.Add((branch,clamped));
                        continue;
                    }
                    if(!directScores.ContainsKey(branch.WideBranchId))directScores[branch.WideBranchId]=clamped;
                }
                // V3.5 Hierarchical Roll-Up: each parent dimension's effective score blends the model's
                // direct parent-level judgment with the confidence-weighted mean of its scored children
                // (50/50). Children carry the narrowed specifics; the direct score keeps the holistic
                // judgment and protects against missing child echoes. Each dimension still counts ONCE
                // in the composite, so the hierarchy informs the score without double counting.
                var effectiveByBranch=new Dictionary<Guid,decimal>();
                var childRows=new List<WideCandidateBranchScoreRecord>();
                var childDisclosure=new Dictionary<string,(decimal Direct,IReadOnlyCollection<WideCandidateChildScoreDto> Children)>(StringComparer.OrdinalIgnoreCase);
                foreach(var branch in branches)
                {
                    if(!directScores.TryGetValue(branch.WideBranchId,out var direct))continue;
                    var effective=direct;
                    if(childScoresByParent.TryGetValue(branch.WideBranchId,out var children)&&children.Count>0)
                    {
                        var weightTotal=children.Sum(entry=>InterpretationWeight(entry.Child));
                        var rollUp=weightTotal<=0?children.Average(entry=>entry.Score):children.Sum(entry=>InterpretationWeight(entry.Child)*entry.Score)/weightTotal;
                        effective=Math.Clamp(.5m*direct+.5m*rollUp,0,1);
                        childDisclosure[branch.DisplayName]=(direct,children.Select(entry=>new WideCandidateChildScoreDto(entry.Child.DisplayName,entry.Score,entry.Child.PoloxiConfidence)).ToArray());
                        // Persist child scores so the roll-up is auditable per candidate.
                        foreach(var(child,childScore)in children)childRows.Add(new(Guid.NewGuid(),candidateId,child.WideBranchId,request.TenantId,child.DisplayName,childScore));
                    }
                    effectiveByBranch[branch.WideBranchId]=effective;
                }
                return new{Candidate=candidate,CandidateId=candidateId,ResolvedName=resolvedName,Effective=effectiveByBranch,ChildRows=childRows,Disclosure=childDisclosure};
            }).Where(item=>item is not null).Select(item=>item!).ToList();
            // V3.6 Fix B — per-dimension contrast normalization: rolled-up dimension scores regress
            // toward the mean (averaging many sub-scores compresses spread), which made POLOXI's final
            // ranking collapse toward the unweighted mention baseline. For each dimension scored by 2+
            // candidates, deviations from the cross-candidate mean are amplified by a fixed gain
            // (order-preserving, mean-preserving, clamped to 0..1) so real differences between
            // candidates stay visible in the composite. Child scores are never altered — only the
            // parent-dimension score that feeds the composite.
            const decimal ContrastGain=1.6m;
            foreach(var branch in branches)
            {
                var scored=prepared.Where(entry=>entry.Effective.ContainsKey(branch.WideBranchId)).ToArray();
                if(scored.Length<2)continue;
                var mean=scored.Average(entry=>entry.Effective[branch.WideBranchId]);
                foreach(var entry in scored)
                    entry.Effective[branch.WideBranchId]=Math.Clamp(mean+(entry.Effective[branch.WideBranchId]-mean)*ContrastGain,0,1);
            }
            foreach(var item in prepared)
            {
                var candidate=item.Candidate;
                var candidateId=item.CandidateId;
                var resolvedName=item.ResolvedName;
                var childDisclosure=item.Disclosure;
                var scores=new List<WideCandidateBranchScoreRecord>(item.ChildRows);
                var scoreByBranch=item.Effective.ToDictionary(entry=>entry.Key,entry=>entry.Value);
                foreach(var childScore in item.ChildRows)
                    if(!scoreByBranch.ContainsKey(childScore.WideBranchId))scoreByBranch[childScore.WideBranchId]=childScore.EvidenceScore;
                var composite=0m;
                foreach(var branch in branches)
                {
                    if(!item.Effective.TryGetValue(branch.WideBranchId,out var effective))continue;
                    scores.Add(new(Guid.NewGuid(),candidateId,branch.WideBranchId,request.TenantId,branch.DisplayName,effective));
                }
                foreach(var weight in rfnBranchWeights)
                    if(scoreByBranch.TryGetValue(weight.Key,out var weightedScore))composite+=weight.Value*weightedScore;
                // V2.1 Candidate Evidence Coverage: a candidate scored on only a fraction of the surviving
                // dimensions must not compete equally with fully-covered candidates — missing data is not
                // strength. The weighted sum already scales by coverage implicitly (unscored dimensions
                // contribute zero weight×score), so coverage is disclosed but NOT multiplied in again —
                // the previous extra multiply applied a quadratic penalty that collapsed every composite
                // toward 0 (e.g. 35% covered → 12% ceiling even with perfect dimension scores).
                var coveredWeight=scoreByBranch.Where(entry=>rfnBranchWeights.ContainsKey(entry.Key)).Sum(entry=>rfnBranchWeights[entry.Key]);
                var coverage=Math.Clamp(coveredWeight,0,1);
                if(configuration.EnableGuardrailPenalty)
                    composite*=ComputeGuardrailPenalty(branches,item.Effective,configuration,queryContract);
                // V2.6 separation of concerns: the coverage-scaled dimension composite IS the candidate's
                // QUALITY ("how good is it?"). Evidence weakness must not rewrite quality — the V2.5
                // independent-source diversity now feeds EVIDENCE CONFIDENCE ("how well can we support
                // that claim?") instead of discounting the composite. Ranking follows quality; confidence
                // in the ranking is reported separately and rolls up into Decision Confidence.
                var distinctHosts=CountDistinctSourceHosts(resolvedName,externalKnowledge);
                var diversityFactor=distinctHosts<=1?.70m:Math.Min(1m,.70m+.15m*(distinctHosts-1));
                var evidenceConfidence=Math.Clamp(diversityFactor*(.5m+.5m*coverage),0,1);
                // Constraint Engine: violators score 0 and carry the reason; they remain visible as PRUNED.
                var violates=candidate.ViolatesConstraint;
                var violationReason=candidate.ConstraintViolationReason?.Trim();
                // V2.3 candidate admission, upgraded to V2.9.4 TIERED admission and V3.10 MERIT-BASED
                // admission: the requested result count is honored whenever enough plausible,
                // evidence-backed candidates exist; weaker-but-valid candidates are admitted with a
                // disclosed lower support tier instead of being silently dropped. V3.10 replaces the
                // raw mention-count arithmetic with merit signals:
                //   EEA - exclusive hosts (sources discussing ONLY this candidate) are worth more
                //         than shared listicle mentions and can restore a tier on their own;
                //   FD  - a token-subset fragment of another candidate with zero exclusive evidence
                //         is a name fragment, excluded with the dominating name disclosed;
                //   SB  - the scoring model's isEntityOfRequestedKind=false demotes the tier one
                //         level (untrusted signal - it never hard-excludes alone; strong evidence
                //         still admits). Zero-support names remain excluded; nothing is invented.
                var support=dimensionSupport.GetValueOrDefault(resolvedName);
                var interpretiveCount=interpretiveSupport.GetValueOrDefault(resolvedName);
                var exclusiveHosts=exclusiveHostCounts.GetValueOrDefault(resolvedName);
                var combinedSupport=interpretiveCount+distinctHosts+exclusiveHosts;
                var admissionMode=support>=requiredSupport?"NORMAL":"RECOVERY";
                // V3.10.5 Candidate Recall Floor: corpus support remains preferred, but a concrete entity
                // appearing across multiple independent interpretation dimensions is not silently removed
                // just because snippet substring matching missed its name. It may compete as MODERATE at
                // most without corpus hosts; zero-support names are still excluded.
                var hasCorpusSupport=distinctHosts>0||exclusiveHosts>0;
                var hasCrossInterpretiveSupport=interpretiveCount>=requiredSupport;
                var supportTier=!hasCorpusSupport&&!hasCrossInterpretiveSupport?null
                    :hasCorpusSupport&&(support>=requiredSupport||exclusiveHosts>=2)?"STRONG"
                    :combinedSupport>=requiredSupport||exclusiveHosts>=1?"MODERATE"
                    :hasCrossInterpretiveSupport?"MODERATE"
                    :combinedSupport>=1?"LIMITED"
                    :null;
                // Entity-role gate: a criterion/category/methodology proposal cannot be admitted by
                // shared listicle mentions. Exclusive evidence discussing this exact candidate can
                // override the untrusted model classification, preventing the model from hard-rejecting
                // a genuinely attested entity while keeping methodology labels out of the ranking.
                if(supportTier is not null&&!candidate.IsEntityOfRequestedKind&&exclusiveHosts==0)
                    supportTier=null;
                // V3.11.2 Interpretive-Absence Damping: a candidate appearing in ZERO interpretation
                // results is evidence-discovered only — it was never independently proposed by any
                // interpretation dimension. It stays admitted (evidence attests it exists), but it
                // cannot claim STRONG support and its composite is damped so it cannot outrank
                // interpretation-backed candidates on near-tied quality scores. Disclosed, not removed.
                if(supportTier=="STRONG"&&interpretiveCount==0)supportTier="MODERATE";
                if(interpretiveCount==0)composite*=.85m;
                if(supportTier is not null&&!HasNamedEntityAdmissionSupport(resolvedName,queryContract,interpretiveCount,distinctHosts,exclusiveHosts,requiredSupport))
                    supportTier=null;
                var supportExcluded=false;
                if(!violates&&dominatedFragments.TryGetValue(resolvedName,out var dominator))
                {
                    supportExcluded=true;
                    supportTier=null;
                    violates=true;
                    violationReason=$"Name fragment of '{dominator}': no evidence host discusses it independently.";
                }
                else if(!violates&&supportTier is null)
                {
                    supportExcluded=true;
                    violates=true;
                    violationReason=candidate.IsEntityOfRequestedKind
                        ?$"No credible support: appears in {interpretiveCount} of {interpretiveResults.Count} interpretation dimensions, {distinctHosts} evidence hosts, {exclusiveHosts} exclusively."
                        :$"Not an entity of the requested kind and no credible support ({distinctHosts} evidence hosts, {exclusiveHosts} exclusive).";
                }
                entries.Add((new(candidateId,executionId,request.TenantId,Truncate(resolvedName,300)!,Truncate(candidate.Detail?.Trim(),1000),violates?0m:Math.Clamp(composite,0,1),0,violates,Truncate(violationReason,400),scores),supportExcluded,Math.Clamp(composite,0,1)));
                evidenceConfidences[resolvedName]=evidenceConfidence;
                rollUpDisclosures[resolvedName]=childDisclosure;
                admissionInfo[resolvedName]=(supportTier is null?"EXCLUDED":admissionMode,interpretiveCount,distinctHosts,Math.Max(support,combinedSupport));
                supportTiers[resolvedName]=supportTier??"EXCLUDED";
            }
            // Pool-coverage transparency + explicit Top-N contract: a pool candidate the scoring model
            // never echoed (or whose echo could not be resolved back to a supplied name) previously became
            // a rank-0 ruled-out row automatically. That made "top 10" a best-effort hint even when the
            // deterministic pool still contained credible, supported entities. When the user requested a
            // count, use a conservative deterministic completion score for supported unscored candidates so
            // the request is satisfied whenever enough valid candidates exist. Unsupported names still stay
            // ruled out with an explicit reason; nothing is invented.
            var scoredNames=entries.Select(entry=>entry.Record.DisplayName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var validScoredCount=entries.Count(entry=>!entry.Record.IsConstraintViolation);
            var neededForContract=Math.Max(0,finalCount-validScoredCount);
            var candidateSignals=neededForContract>0?ComputeCandidateSignals(poolNames,[],externalKnowledge):new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
            foreach(var(name,_)in candidateNames)
            {
                if(scoredNames.Contains(Truncate(name,300)!))continue;
                var candidateId=Guid.NewGuid();
                var distinctHosts=CountDistinctSourceHosts(name,externalKnowledge);
                var exclusiveHosts=exclusiveHostCounts.GetValueOrDefault(name);
                var interpretiveCount=interpretiveSupport.GetValueOrDefault(name);
                var support=dimensionSupport.GetValueOrDefault(name);
                var combinedSupport=interpretiveCount+distinctHosts+exclusiveHosts;
                var hasCorpusSupport=distinctHosts>0||exclusiveHosts>0;
                var hasCrossInterpretiveSupport=interpretiveCount>=requiredSupport;
                var supportTier=!hasCorpusSupport&&!hasCrossInterpretiveSupport?null
                    :hasCorpusSupport&&(support>=requiredSupport||exclusiveHosts>=2)?"STRONG"
                    :combinedSupport>=requiredSupport||exclusiveHosts>=1?"MODERATE"
                    :hasCrossInterpretiveSupport?"MODERATE"
                    :combinedSupport>=1?"LIMITED"
                    :null;
                if(neededForContract>0&&supportTier is not null&&!dominatedFragments.ContainsKey(name)&&IsValidCandidateForContract(name,queryContract)&&HasNamedEntityAdmissionSupport(name,queryContract,interpretiveCount,distinctHosts,exclusiveHosts,requiredSupport))
                {
                    // Deterministic completion is intentionally conservative: it is only used for pool
                    // candidates that missed the LLM matrix response. Scores are branch-specific and
                    // evidence-derived; an omitted candidate cannot receive one uniform quality score across
                    // every dimension merely because it was mentioned in retrieved text.
                    var fallbackScorePairs=branches
                        .Select(branch=>(Branch:branch,Score:ComputeCandidateBranchSignal(name,branch,externalKnowledge)))
                        .Where(item=>item.Score>0m)
                        .ToArray();
                    if(fallbackScorePairs.Length==0)continue;
                    var scoreByBranch=fallbackScorePairs.ToDictionary(item=>item.Branch.WideBranchId,item=>item.Score);
                    var weightedQuality=rfnBranchWeights.Where(weight=>scoreByBranch.ContainsKey(weight.Key)).Sum(weight=>weight.Value*scoreByBranch[weight.Key]);
                    var coveredWeight=rfnBranchWeights.Where(weight=>scoreByBranch.ContainsKey(weight.Key)).Sum(weight=>weight.Value);
                    var coverage=Math.Clamp(coveredWeight,0,1);
                    var supportRatio=Math.Clamp((decimal)Math.Min(combinedSupport,Math.Max(requiredSupport,1))/Math.Max(requiredSupport,1),0m,1m);
                    var composite=Math.Clamp(weightedQuality*coverage,0m,.75m);
                    var fallbackScores=fallbackScorePairs.Select(item=>new WideCandidateBranchScoreRecord(Guid.NewGuid(),candidateId,item.Branch.WideBranchId,request.TenantId,item.Branch.DisplayName,item.Score)).ToArray();
                    entries.Add((new(candidateId,executionId,request.TenantId,Truncate(name,300)!,"Admitted by deterministic Top-N completion from retrieved evidence and interpretive support; the scoring model omitted this candidate from the matrix response.",composite,0,false,null,fallbackScores),false,composite));
                    evidenceConfidences[name]=Math.Clamp((distinctHosts<=1?.55m:Math.Min(1m,.55m+.15m*(distinctHosts-1)))*supportRatio,0m,1m);
                    admissionInfo[name]=("TOP_N_COMPLETION",interpretiveCount,distinctHosts,Math.Max(support,combinedSupport));
                    supportTiers[name]=supportTier;
                    neededForContract--;
                    continue;
                }
                var reason=dominatedFragments.TryGetValue(name,out var dominator)
                    ?$"Name fragment of '{dominator}': no evidence host discusses it independently."
                    :"Not scored: the scoring model did not return a resolvable score for this candidate in the Candidate \u00D7 Branch matrix.";
                entries.Add((new(candidateId,executionId,request.TenantId,Truncate(name,300)!,null,0m,0,true,reason,[]),true,0m));
                supportTiers[name]="EXCLUDED";
                admissionInfo[name]=("EXCLUDED",interpretiveCount,distinctHosts,Math.Max(support,combinedSupport));
            }
            // V2.9.4 rule: POLOXI honors the requested Top N whenever enough plausible, evidence-backed
            // candidates exist — weaker-but-valid candidates compete with a disclosed lower support
            // tier. Only zero-support names and constraint violators are excluded; POLOXI never invents
            // candidates and never hides evidence weakness to fill a count.
            // V3.10.6: excluded names and constraint violators NEVER occupy Top N slots — they are
            // dropped from the final ranking entirely instead of filling the requested count with 0%
            // rows. Admitted candidates rank by composite quality FIRST; support tier remains an
            // evidence-confidence tie-breaker. This keeps interpretation/result quality from being
            // overridden by source-count tiering while still disclosing weaker support.
            static int TierRank(string? tier)=>tier switch{"STRONG"=>3,"MODERATE"=>2,"LIMITED"=>1,_=>0};
            var ranked=entries
                .Where(entry=>!entry.Record.IsConstraintViolation)
                .OrderByDescending(entry=>entry.Record.CompositeScore)
                .ThenByDescending(entry=>TierRank(supportTiers.GetValueOrDefault(entry.Record.DisplayName)))
                .Take(finalCount)
                .Select((entry,index)=>entry.Record with{RankNumber=index+1}).ToArray();
            // Ruled-out transparency: excluded candidates (constraint violators, zero-support names,
            // dominated fragments) never occupy Top N slots, but they are returned as rank-0 PRUNED
            // rows carrying their exclusion reason so the caller/UI can disclose WHY each interpretive
            // candidate did not survive. Every downstream consumer already filters on
            // !IsConstraintViolation, so ranking, stability, and delivered-count semantics are unchanged.
            var ruledOut=entries
                .Where(entry=>entry.Record.IsConstraintViolation)
                .OrderByDescending(entry=>entry.RawComposite)
                .Select(entry=>entry.Record with{RankNumber=0}).ToArray();
            var persisted=ranked.Concat(ruledOut).ToArray();
            await wideRepository.SaveWideCandidatesAsync(persisted,request.UserId,cancellationToken);
            return persisted.Select(record=>{var admission=admissionInfo.GetValueOrDefault(record.DisplayName,("EXCLUDED",0,0,0));var disclosure=rollUpDisclosures.GetValueOrDefault(record.DisplayName);var parentScores=record.BranchScores.Where(score=>scoringBranchIds.Contains(score.WideBranchId)).ToArray();return new WideCandidateDto(record.WideCandidateId,record.RankNumber,record.DisplayName,record.IsConstraintViolation?$"Ruled out: {record.ConstraintViolationReason}":record.Detail,record.CompositeScore,parentScores.Select(score=>{var detail=disclosure is not null&&disclosure.TryGetValue(score.BranchDisplayName,out var info)?info:default;return new WideCandidateBranchScoreDto(score.BranchDisplayName,score.EvidenceScore){DirectScore=detail.Children is{Count:>0}?detail.Direct:null,ChildScores=detail.Children??[]};}).ToArray()){EvidenceCoverage=branches.Length==0?0m:Math.Clamp((decimal)parentScores.Length/branches.Length,0,1),IsConstraintViolation=record.IsConstraintViolation,QualityScore=record.CompositeScore,EvidenceConfidence=evidenceConfidences.GetValueOrDefault(record.DisplayName),AdmissionModeCode=admission.Item1,InterpretiveSupportCount=admission.Item2,EvidenceHostSupportCount=admission.Item3,TotalSupportCount=admission.Item4,SupportTierCode=supportTiers.GetValueOrDefault(record.DisplayName,"EXCLUDED")};}).ToArray();
        }
        catch(Exception)when(!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
    }

    private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=true};

    // V2.3: case-insensitive (dimension, candidate) pair comparer for candidate-admission counting.
    private sealed class CandidateDimensionComparer:IEqualityComparer<(string BranchDisplayName,string Name)>
    {
        public bool Equals((string BranchDisplayName,string Name)x,(string BranchDisplayName,string Name)y)=>
            string.Equals(x.BranchDisplayName,y.BranchDisplayName,StringComparison.OrdinalIgnoreCase)&&string.Equals(x.Name,y.Name,StringComparison.OrdinalIgnoreCase);
        public int GetHashCode((string BranchDisplayName,string Name)obj)=>
            HashCode.Combine(obj.BranchDisplayName.ToUpperInvariant(),obj.Name.ToUpperInvariant());
    }

    private const string BranchSchemaFragment="""
    "branch": {
      "type": "object",
      "properties": {
        "branchCode": { "type": "string" },
        "displayName": { "type": "string" },
        "interpretation": { "type": "string" },
        "capabilityCode": { "type": ["string", "null"] },
        "searchText": { "type": ["string", "null"] },
        "confidence": { "type": "number" },
        "semanticType": { "type": "string", "enum": ["ALTERNATIVE", "DIMENSION"] },
        "continueNarrowing": { "type": "boolean" },
        "stopReason": { "type": ["string", "null"] },
        "parentBranchCode": { "type": ["string", "null"] }
      },
      "required": ["branchCode", "displayName", "interpretation", "capabilityCode", "searchText", "confidence", "semanticType", "continueNarrowing", "stopReason", "parentBranchCode"],
      "additionalProperties": false
    }
""";

    private const string IntentSchema=$$"""
{
  "type": "object",
  "$defs": {
{{BranchSchemaFragment}}
  },
  "properties": {
    "conceptCode": { "type": "string" },
    "displayName": { "type": "string" },
    "ambiguityScore": { "type": "number" },
    "branches": { "type": "array", "maxItems": 12, "items": { "$ref": "#/$defs/branch" } }
  },
  "required": ["conceptCode", "displayName", "ambiguityScore", "branches"],
  "additionalProperties": false
}
""";

    private const string LevelSchema=$$"""
{
  "type": "object",
  "$defs": {
{{BranchSchemaFragment}}
  },
  "properties": {
    "branches": { "type": "array", "maxItems": 24, "items": { "$ref": "#/$defs/branch" } }
  },
  "required": ["branches"],
  "additionalProperties": false
}
""";

    private const string QueryContractSchema="""
{
  "type": "object",
  "properties": {
    "answerKind": { "type": ["string", "null"], "enum": ["ENTITY_RANKING", "CONTENT_ENUMERATION", "SINGLE_ANSWER", "TECHNICAL_RECOMMENDATION", "DIAGNOSTIC_PROCEDURE", "CLARIFICATION_REQUIRED", "RESOLUTION", null] },
    "candidateKind": { "type": ["string", "null"], "enum": ["NAMED_ENTITY", "ACTIONABLE_SOLUTION", "DIAGNOSTIC_STEP", "PROCEDURE_STEP", null] },
    "intent": { "type": ["string", "null"] },
    "targetObject": { "type": ["string", "null"] },
    "requiredTerms": { "type": "array", "maxItems": 10, "items": { "type": "string" } },
    "excludedTerms": { "type": "array", "maxItems": 10, "items": { "type": "string" } },
    "ambiguousTerms": { "type": "array", "maxItems": 10, "items": { "type": "string" } },
    "safetyRiskCode": { "type": ["string", "null"], "enum": ["NONE", "LOW", "MEDIUM", "HIGH", null] },
    "outputShape": { "type": ["string", "null"] },
    "requiresClarification": { "type": "boolean" },
    "clarificationQuestion": { "type": ["string", "null"] },
    "clarificationTarget": { "type": ["string", "null"] },
    "clarificationOptions": { "type": "array", "maxItems": 6, "items": { "type": "string" } },
    "isSafetySensitive": { "type": "boolean" },
    "entityType": { "type": ["string", "null"] },
    "geographicConstraint": { "type": ["string", "null"] },
    "requestedCount": { "type": ["integer", "null"] },
    "rankingConcept": { "type": ["string", "null"] },
    "hardConstraints": { "type": "array", "maxItems": 10, "items": { "type": "string" } },
    "ambiguousConcepts": { "type": "array", "maxItems": 6, "items": { "type": "string" } },
    "outputRequirements": { "type": "array", "maxItems": 6, "items": { "type": "string" } }
  },
  "required": ["answerKind", "candidateKind", "intent", "targetObject", "requiredTerms", "excludedTerms", "ambiguousTerms", "safetyRiskCode", "outputShape", "requiresClarification", "clarificationQuestion", "clarificationTarget", "clarificationOptions", "isSafetySensitive", "entityType", "geographicConstraint", "requestedCount", "rankingConcept", "hardConstraints", "ambiguousConcepts", "outputRequirements"],
  "additionalProperties": false
}
""";

    // V2.2 batched Information Value estimation: categorical judgments plus falsifiable candidate
    // ranking predictions, in ONE call for all eligible branches. Categories are strictly enumerated.
    private const string InformationValueSchema="""
{
  "type": "object",
  "properties": {
    "targets": {
      "type": "array",
      "maxItems": 12,
      "items": {
        "type": "object",
        "properties": {
          "branchCode": { "type": "string" },
          "uncertainty": { "type": "string", "enum": ["VERY_LOW", "LOW", "MEDIUM", "HIGH", "VERY_HIGH"] },
          "rankingImpact": { "type": "string", "enum": ["VERY_LOW", "LOW", "MEDIUM", "HIGH", "VERY_HIGH"] },
          "candidateDiscrimination": { "type": "string", "enum": ["VERY_LOW", "LOW", "MEDIUM", "HIGH", "VERY_HIGH"] },
          "evidenceAvailability": { "type": "string", "enum": ["VERY_LOW", "LOW", "MEDIUM", "HIGH", "VERY_HIGH"] },
          "novelty": { "type": "string", "enum": ["VERY_LOW", "LOW", "MEDIUM", "HIGH", "VERY_HIGH"] },
          "redundancy": { "type": "string", "enum": ["VERY_LOW", "LOW", "MEDIUM", "HIGH", "VERY_HIGH"] },
          "evidenceTarget": { "type": ["string", "null"] },
          "rationale": { "type": "string" },
          "predictedRankingChanges": {
            "type": "array",
            "maxItems": 8,
            "items": {
              "type": "object",
              "properties": {
                "candidate": { "type": "string" },
                "direction": { "type": "string", "enum": ["UP", "DOWN"] },
                "magnitude": { "type": "string", "enum": ["NONE", "LOW", "MEDIUM", "HIGH"] }
              },
              "required": ["candidate", "direction", "magnitude"],
              "additionalProperties": false
            }
          }
        },
        "required": ["branchCode", "uncertainty", "rankingImpact", "candidateDiscrimination", "evidenceAvailability", "novelty", "redundancy", "evidenceTarget", "rationale", "predictedRankingChanges"],
        "additionalProperties": false
      }
    }
  },
  "required": ["targets"],
  "additionalProperties": false
}
""";

    private const string CandidateScoringSchema="""
{
  "type": "object",
  "properties": {
    "candidates": {
      "type": "array",
      "maxItems": 25,
      "items": {
        "type": "object",
        "properties": {
          "name": { "type": "string" },
          "detail": { "type": ["string", "null"] },
          "violatesConstraint": { "type": "boolean" },
          "constraintViolationReason": { "type": ["string", "null"] },
          "isEntityOfRequestedKind": { "type": "boolean" },
          "branchScores": {
            "type": "array",
            "maxItems": 10,
            "items": {
              "type": "object",
              "properties": {
                "branchDisplayName": { "type": "string" },
                "evidenceScore": { "type": "number" }
              },
              "required": ["branchDisplayName", "evidenceScore"],
              "additionalProperties": false
            }
          }
        },
        "required": ["name", "detail", "violatesConstraint", "constraintViolationReason", "isEntityOfRequestedKind", "branchScores"],
        "additionalProperties": false
      }
    }
  },
  "required": ["candidates"],
  "additionalProperties": false
}
""";

    private const string AnswerSchema="""
{
  "type": "object",
  "properties": {
    "answer": { "type": "string" },
    "verificationCode": { "type": "string", "enum": ["VERIFIED", "PARTIALLY_VERIFIED", "INTERPRETIVE"] },
    "confidence": { "type": "number" },
    "relevantEvidenceNumbers": { "type": "array", "maxItems": 50, "items": { "type": "integer" } },
    "externalReferences": {
      "type": "array",
      "maxItems": 6,
      "items": {
        "type": "object",
        "properties": {
          "title": { "type": "string" },
          "url": { "type": "string" },
          "source": { "type": "string" },
          "summary": { "type": "string" },
          "branchDisplayName": { "type": "string" }
        },
        "required": ["title", "url", "source", "summary", "branchDisplayName"],
        "additionalProperties": false
      }
    },
    "suggestedActions": {
      "type": "array",
      "maxItems": 5,
      "items": {
        "type": "object",
        "properties": {
          "displayName": { "type": "string" },
          "navigationRoute": { "type": "string" },
          "rationale": { "type": "string" }
        },
        "required": ["displayName", "navigationRoute", "rationale"],
        "additionalProperties": false
      }
    },
    "interpretiveResults": {
      "type": "array",
      "maxItems": 25,
      "items": {
        "type": "object",
        "properties": {
          "branchDisplayName": { "type": "string" },
          "interpretation": { "type": "string" },
          "dataVolatility": { "type": "string", "enum": ["STABLE", "TIME_SENSITIVE"] },
          "items": {
            "type": "array",
            "maxItems": 10,
            "items": {
              "type": "object",
              "properties": {
                "rankNumber": { "type": "integer" },
                "name": { "type": "string" },
                "detail": { "type": "string" },
                "score": { "type": ["number", "null"], "minimum": 0, "maximum": 1 }
              },
              "required": ["rankNumber", "name", "detail", "score"],
              "additionalProperties": false
            }
          }
        },
        "required": ["branchDisplayName", "interpretation", "dataVolatility", "items"],
        "additionalProperties": false
      }
    },
    "candidateInsights": {
      "type": "array",
      "maxItems": 10,
      "items": {
        "type": "object",
        "properties": {
          "candidateName": { "type": "string" },
          "bestFor": { "type": ["string", "null"] },
          "praisedFor": { "type": "array", "maxItems": 4, "items": { "type": "string" } },
          "watchOutFor": { "type": "array", "maxItems": 4, "items": { "type": "string" } }
        },
        "required": ["candidateName", "bestFor", "praisedFor", "watchOutFor"],
        "additionalProperties": false
      }
    }
  },
  "required": ["answer", "verificationCode", "confidence", "relevantEvidenceNumbers", "externalReferences", "suggestedActions", "interpretiveResults", "candidateInsights"],
  "additionalProperties": false
}
""";
}
