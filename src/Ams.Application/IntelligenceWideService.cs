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

// Isolated clone of the EPH search orchestration used by /intelligence/search/eph_wide.
// Intentionally duplicates IntelligenceService.SearchWithEphAsync so this "Wide" path can be
// tweaked freely without changing /intelligence/search/eph behavior.
public sealed class IntelligenceWideService(IIntelligenceRepository repository,IIntelligenceWideRepository wideRepository,IAiProviderRouter aiProviderRouter,IExternalKnowledgeProvider externalKnowledgeProvider):IIntelligenceWideService
{
    public async Task<EphSearchResponse> SearchWithEphWideAsync(EphSearchRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);
        if(request.UserId==Guid.Empty)throw new UnauthorizedAccessException("An authenticated user is required for EPH search.");
        var timer=Stopwatch.StartNew();
        var normalizedQuery=NormalizeQuery(request.Query).ToLowerInvariant();
        request=request with{Query=NormalizeQuery(request.Query),MaximumResults=Math.Clamp(request.MaximumResults,1,100),CorrelationId=string.IsNullOrWhiteSpace(request.CorrelationId)?$"eph-search-wide:{Guid.NewGuid():N}":request.CorrelationId.Trim()};
        var configuration=await repository.GetEphConfigurationAsync(request.TenantId,cancellationToken);
        var capabilities=await repository.GetEphCapabilitiesAsync(request.TenantId,cancellationToken);
        if(capabilities.Count==0)throw new InvalidOperationException("No active EPH capabilities are configured.");
        if(!request.UseEphEngine)return await SearchWithoutEphEngineAsync(request,capabilities,configuration,timer,cancellationToken);
        var signature=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizedQuery)));
        var hierarchy=configuration.EnableHierarchyReuse?await repository.GetReusableEphHierarchyAsync(request.TenantId,signature,cancellationToken):null;
        var reused=hierarchy is not null;
        if(hierarchy is null)
        {
            var generated=await GenerateEphProposalAsync(request,capabilities,configuration,cancellationToken);
            var branches=ValidateEphBranches(generated.Proposal,capabilities,configuration);
            hierarchy=await repository.SaveEphHierarchyAsync(request.TenantId,request.UserId,signature,normalizedQuery,generated.Proposal,generated.ProviderCode,generated.ModelCode,DateTime.UtcNow.AddHours(configuration.HierarchyCacheHours),branches,cancellationToken);
        }
        var validBranches=hierarchy.Branches.Where(branch=>branch.ValidationStatusCode.Equals("VALID",StringComparison.OrdinalIgnoreCase)).Take(configuration.MaximumBranches).ToArray();
        var executionId=await repository.StartEphExecutionAsync(new(request.TenantId,hierarchy.HierarchyId,request.UserId,request.Query,request.CorrelationId,reused,validBranches.Length,hierarchy.Branches.Count-validBranches.Length,hierarchy.Confidence),cancellationToken);
        var evidence=new List<EphEvidenceDto>();
        // Progressive narrowing: parents execute first; a child branch keeps only evidence entities its parent branch also matched.
        var branchEvidenceKeys=new Dictionary<Guid,HashSet<string>>();
        foreach(var branch in validBranches)
        {
            var capability=capabilities.First(item=>item.CapabilityCode.Equals(branch.CapabilityCode,StringComparison.OrdinalIgnoreCase));
            var branchEvidence=await repository.ExecuteEphBranchAsync(request,branch,capability,configuration.MaximumResults,cancellationToken);
            // Narrow only against parents that searched the same entity type; cross-entity parents cannot share keys.
            if(branch.ParentHierarchyBranchId is{}parentId&&branchEvidenceKeys.TryGetValue(parentId,out var parentKeys)&&parentKeys.Any(key=>key.StartsWith($"{capability.EntityTypeCode}:",StringComparison.OrdinalIgnoreCase)))
                branchEvidence=branchEvidence.Where(item=>parentKeys.Contains($"{item.EntityTypeCode}:{item.EntityId:D}")).ToArray();
            branchEvidenceKeys[branch.HierarchyBranchId]=branchEvidence.Select(item=>$"{item.EntityTypeCode}:{item.EntityId:D}").ToHashSet(StringComparer.OrdinalIgnoreCase);
            evidence.AddRange(branchEvidence);
        }
        var ranked=RankEvidence(evidence,request,configuration);
        string? explanation=null;
        var explanationStatus="NOT_REQUESTED";
        if(request.IncludeExplanation&&ranked.Length>0)
        {
            try
            {
                var grounding=string.Join('\n',ranked.Take(12).Select((item,index)=>$"[{index+1}] {item.Title} ({string.Join(", ",item.MatchedBranches)}): {item.Excerpt}"));
                var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_EPH_EXPLANATION","Explain only the supplied authorized EPH evidence. Cite evidence numbers in brackets. Clearly state unsupported hierarchy branches and never invent facts.",$"Question: {request.Query}\nValidated concept: {hierarchy.DisplayName}\nEvidence:\n{grounding}",null,request.CorrelationId,new("Intelligence",null,null,request.Query,"EPH_EVIDENCE",executionId,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
                explanation=result.Content;
                explanationStatus="COMPLETED";
            }
            catch(Exception exception) when(exception is AiProviderUnavailableException or TimeoutException)
            {
                explanationStatus="UNAVAILABLE";
            }
        }
        timer.Stop();
        await repository.CompleteEphExecutionAsync(request.TenantId,request.UserId,executionId,hierarchy.HierarchyId,ranked,explanationStatus,explanation,timer.ElapsedMilliseconds,cancellationToken);
        return new(executionId,hierarchy.HierarchyId,request.Query,hierarchy.ConceptCode,hierarchy.DisplayName,hierarchy.VersionNumber,reused,hierarchy.Confidence,hierarchy.Branches,ranked,explanation,explanationStatus,timer.ElapsedMilliseconds);
    }

    // 'EPH Engine' filter disabled: bypass LLM hierarchy generation, cache reuse, and execution persistence.
    // Runs the same deterministic authorized capability searches directly against every active capability.
    private async Task<EphSearchResponse> SearchWithoutEphEngineAsync(EphSearchRequest request,IReadOnlyCollection<EphCapabilityDto> capabilities,EphConfiguration configuration,Stopwatch timer,CancellationToken cancellationToken)
    {
        var branches=capabilities.OrderBy(capability=>capability.SortOrder).Take(configuration.MaximumBranches).Select((capability,index)=>new EphBranchRecord(Guid.NewGuid(),null,capability.CapabilityCode,capability.DisplayName,"Direct authorized search without EPH hierarchy.",capability.CapabilityCode,"VALID","EPH engine bypassed by request filter.",request.Query,capability.SupportsRecency,1m,index+1)).ToArray();
        var evidence=new List<EphEvidenceDto>();
        foreach(var branch in branches)
        {
            var capability=capabilities.First(item=>item.CapabilityCode.Equals(branch.CapabilityCode,StringComparison.OrdinalIgnoreCase));
            evidence.AddRange(await repository.ExecuteEphBranchAsync(request,branch,capability,configuration.MaximumResults,cancellationToken));
        }
        var ranked=RankEvidence(evidence,request,configuration);
        timer.Stop();
        return new(Guid.Empty,Guid.Empty,request.Query,"DIRECT_SEARCH","Direct authorized search (EPH engine off)",0,false,1m,branches,ranked,null,"NOT_REQUESTED",timer.ElapsedMilliseconds);
    }

    private static EphEvidenceDto[] RankEvidence(List<EphEvidenceDto> evidence,EphSearchRequest request,EphConfiguration configuration)=>evidence.GroupBy(item=>$"{item.EntityTypeCode}:{item.EntityId:D}",StringComparer.OrdinalIgnoreCase).Select(group=>
    {
        var first=group.OrderByDescending(item=>item.RelevanceScore).First();
        var branchNames=group.SelectMany(item=>item.MatchedBranches).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var score=Math.Clamp(group.Max(item=>item.RelevanceScore)+Math.Min(.20m,(branchNames.Length-1)*.05m),0,1);
        return first with{RelevanceScore=score,MatchedBranches=branchNames};
    }).OrderByDescending(item=>item.RelevanceScore).ThenBy(item=>item.Title).Take(Math.Min(request.MaximumResults,configuration.MaximumResults)).Select((item,index)=>item with{RankNumber=index+1}).ToArray();

    private async Task<(EphHierarchyProposal Proposal,string ProviderCode,string ModelCode)> GenerateEphProposalAsync(EphSearchRequest request,IReadOnlyCollection<EphCapabilityDto> capabilities,EphConfiguration configuration,CancellationToken cancellationToken)
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
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_EPH_HIERARCHY","Propose a concise enterprise progressive hierarchy at most two levels deep. Top-level branches are broad entry points; child branches must progressively narrow their parent toward a more specific subset (for example a status, lifecycle stage, or qualifier of the parent), and children must always have empty children arrays. A child narrows the same entity type as its parent and its results are intersected with the parent results, so only nest when top-down narrowing genuinely applies. You may invent reasoning branches, but map a branch to a capabilityCode only when the supplied catalog can ground it. Use null capabilityCode for unsupported branches. Never produce SQL or claim records exist.",$"Question: {request.Query}\nMaximum branches: {configuration.MaximumBranches}\nApproved capability catalog:\n{catalog}",schema,request.CorrelationId,new("Intelligence",null,null,request.Query,"EPH_HIERARCHY",null,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
        var proposal=JsonSerializer.Deserialize<EphHierarchyProposal>(result.Content,new JsonSerializerOptions{PropertyNameCaseInsensitive=true})??throw new ValidationException("The EPH hierarchy response was empty.");
        return (proposal,result.ProviderCode,result.ModelCode);
    }

    private static IReadOnlyCollection<EphBranchRecord> ValidateEphBranches(EphHierarchyProposal proposal,IReadOnlyCollection<EphCapabilityDto> capabilities,EphConfiguration configuration)
    {
        var validated=new List<EphBranchRecord>();
        void Visit(EphProposedBranch branch,Guid? parentId)
        {
            if(validated.Count>=configuration.MaximumBranches)return;
            var id=Guid.NewGuid();
            var capability=capabilities.FirstOrDefault(item=>item.CapabilityCode.Equals(branch.CapabilityCode,StringComparison.OrdinalIgnoreCase));
            var confidence=Math.Clamp(branch.Confidence,0,1);
            var valid=capability is not null&&capability.ExecutionHandlerCode.Equals("AUTHORIZED_SEARCH_DOCUMENT",StringComparison.OrdinalIgnoreCase)&&confidence>=Math.Max(configuration.MinimumBranchConfidence,capability.MinimumConfidence)&&(!branch.OrderByRecency||capability.SupportsRecency);
            var searchText=valid?NormalizeEphSearchText(branch.SearchText,capability!):null;
            validated.Add(new(id,parentId,NormalizeCode(branch.BranchCode),branch.DisplayName.Trim(),branch.Condition.Trim(),capability?.CapabilityCode,valid?"VALID":"UNSUPPORTED",valid?"Grounded by an approved deterministic capability.":"No approved capability can deterministically ground this branch.",searchText,valid&&branch.OrderByRecency,confidence,validated.Count+1));
            foreach(var child in branch.Children??[])Visit(child,id);
        }
        foreach(var branch in proposal.Branches??[])Visit(branch,null);
        return validated;
    }

    private static string NormalizeEphSearchText(string? searchText,EphCapabilityDto capability)
    {
        if(string.IsNullOrWhiteSpace(searchText))return string.Empty;
        var normalized=searchText.Trim();
        return capability.ApprovedTerms.FirstOrDefault(term=>normalized.Contains(term,StringComparison.OrdinalIgnoreCase))??string.Empty;
    }
    private static string NormalizeCode(string value)=>new(value.Trim().ToUpperInvariant().Select(character=>char.IsLetterOrDigit(character)?character:'_').ToArray());
    private static string NormalizeQuery(string query)=>string.Join(' ',query.Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
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
        // 'EPH Engine' filter disabled: pure LLM answer, no hierarchy, grounding, or elimination.
        if(!request.UseEphEngine)return await SearchLlmOnlyAsync(request,timer,cancellationToken);
        var configuration=await wideRepository.GetWideConfigurationAsync(request.TenantId,cancellationToken);
        // Wide search is knowledge-only: it never grounds branches against AMS enterprise records.
        // An empty capability catalog forces every branch onto the INTERPRETIVE reasoning path.
        var capabilities=Array.Empty<EphCapabilityDto>();
        var executionId=await wideRepository.StartWideExecutionAsync(new(request.TenantId,request.UserId,request.Query,request.CorrelationId),cancellationToken);
        var llmCalls=0;
        var allBranches=new List<WideBranchRecord>();
        var evidence=new List<EphEvidenceDto>();
        var branchEvidenceKeys=new Dictionary<Guid,HashSet<string>>();
        var depth=0;
        var terminationReason="LLM_COMPLETE";
        var aggregateConfidence=0m;
        var ephRequest=new EphSearchRequest(request.TenantId,request.UserId,request.Query,request.MaximumResults,request.CorrelationId){GrantedPermissions=request.GrantedPermissions};
        try
        {
            // Stage 1: Ambiguous intent framing -> problem-specific Level-1 hierarchy (open, not catalog-limited).
            var intent=await ProposeIntentAsync(request,capabilities,configuration,cancellationToken);
            llmCalls++;
            var currentLevel=MaterializeBranches(intent.Branches,executionId,request.TenantId,1,new Dictionary<string,WideBranchRecord>(),configuration);
            await wideRepository.SaveWideBranchesAsync(currentLevel,request.UserId,cancellationToken);
            allBranches.AddRange(currentLevel);

            // Stage 2: iterative loop — ground, eliminate, check confidence, then propose the next narrower level.
            while(currentLevel.Length>0)
            {
                depth++;
                var survivors=new List<WideBranchRecord>();
                foreach(var branch in currentLevel)
                {
                    var grounded=await GroundBranchAsync(branch,ephRequest,capabilities,request.MaximumResults,branchEvidenceKeys,evidence,cancellationToken);
                    // Candidate elimination: grounded-but-empty with weak confidence, or below the confidence floor.
                    var eliminated=false;string? eliminationReason=null;
                    if(branch.Confidence<configuration.MinimumBranchConfidence){eliminated=true;eliminationReason=$"Confidence {branch.Confidence:P0} below minimum {configuration.MinimumBranchConfidence:P0}.";}
                    else if(grounded.StatusCode=="GROUNDED"&&grounded.EvidenceCount==0&&branch.Confidence<configuration.TargetConfidence){eliminated=true;eliminationReason="Grounded capability search returned no enterprise evidence.";}
                    var updated=branch with{GroundingStatusCode=grounded.StatusCode,EvidenceCount=grounded.EvidenceCount,IsEliminated=eliminated,EliminationReason=eliminationReason};
                    await wideRepository.UpdateWideBranchOutcomeAsync(request.TenantId,branch.WideBranchId,updated.GroundingStatusCode,updated.EvidenceCount,updated.IsEliminated,updated.EliminationReason,cancellationToken);
                    allBranches[allBranches.FindIndex(item=>item.WideBranchId==branch.WideBranchId)]=updated;
                    if(!eliminated)survivors.Add(updated);
                }
                if(survivors.Count==0){terminationReason="NO_SURVIVORS";break;}

                // Confidence: evidence-weighted aggregate over surviving paths.
                // Minimum depth of 2: confidence/LLM-complete exits are honored only after Level 2
                // has been processed, so single-level hierarchies cannot terminate early.
                aggregateConfidence=ComputeAggregateConfidence(survivors);
                if(depth>=2&&aggregateConfidence>=configuration.TargetConfidence){terminationReason="CONFIDENCE_REACHED";break;}

                // Natural LLM termination: no surviving branch wants further narrowing.
                if(depth>=2&&survivors.All(branch=>!branch.ContinueNarrowing)){terminationReason="LLM_COMPLETE";break;}

                // Circuit breakers (never functional limits; audited when reached).
                if(depth>=configuration.AbsoluteDepthCeiling){terminationReason="DEPTH_CEILING_REACHED";break;}
                if(llmCalls+2>configuration.MaximumTotalLlmCalls){terminationReason="LLM_CALL_CEILING_REACHED";break;}

                // Progressive narrowing: propose the next level from surviving branches AND their grounding outcomes.
                // When forcing the minimum depth of 2, narrow all survivors even if none opted to continue.
                var narrowingParents=survivors.Where(branch=>branch.ContinueNarrowing).ToArray();
                if(narrowingParents.Length==0)narrowingParents=survivors.ToArray();
                var proposal=await ProposeNextLevelAsync(request,narrowingParents,capabilities,configuration,depth+1,evidence,cancellationToken);
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
            var ranked=RankEvidence(survivingEvidence,ephRequest,new(false,1,configuration.MinimumBranchConfidence,configuration.MaximumBranchesPerLevel*Math.Max(depth,1),request.MaximumResults));

            // Stage 3: verified answer composed from surviving paths + enterprise evidence.
            var survivorsFinal=allBranches.Where(branch=>!branch.IsEliminated).ToArray();
            if(aggregateConfidence==0m&&survivorsFinal.Length>0)aggregateConfidence=ComputeAggregateConfidence(survivorsFinal);
            WideAnswerProposal answer;
            var answerStatus="COMPLETED";
            // Live external grounding (fail-soft): retrieve fresh web snippets for interpretive paths so
            // time-sensitive figures come from current sources instead of stale model memory.
            var externalKnowledge=await GatherExternalKnowledgeAsync(request,survivorsFinal.Where(branch=>branch.GroundingStatusCode=="INTERPRETIVE").ToArray(),cancellationToken);
            try
            {
                answer=await ComposeAnswerAsync(request,survivorsFinal,ranked,aggregateConfidence,externalKnowledge,cancellationToken);
                llmCalls++;
            }
            catch(Exception exception) when(exception is AiProviderUnavailableException or TimeoutException)
            {
                answerStatus="UNAVAILABLE";
                answer=new(string.Empty,ranked.Length>0?"PARTIALLY_VERIFIED":"INTERPRETIVE",aggregateConfidence,[],ranked.Select(item=>item.RankNumber).ToArray());
            }
            // Relevance validation: keep only evidence the answer LLM judged relevant to the question.
            // Keyword grounding can match superficially (for example a name token matching unrelated
            // records); such evidence must not surface or inflate confidence.
            var relevantNumbers=(answer.RelevantEvidenceNumbers??[]).ToHashSet();
            var relevantEvidence=ranked.Where(item=>relevantNumbers.Contains(item.RankNumber)).ToArray();
            if(answer.VerificationCode=="INTERPRETIVE"||relevantEvidence.Length==0)aggregateConfidence=Math.Min(aggregateConfidence,Math.Clamp(answer.Confidence,0,1));
            timer.Stop();
            await wideRepository.CompleteWideExecutionAsync(request.TenantId,request.UserId,executionId,answerStatus,terminationReason,depth,llmCalls,aggregateConfidence,answer.VerificationCode,string.IsNullOrWhiteSpace(answer.Answer)?null:answer.Answer,timer.ElapsedMilliseconds,cancellationToken);
            return new(executionId,request.Query,answerStatus,terminationReason,depth,llmCalls,aggregateConfidence,answer.VerificationCode,string.IsNullOrWhiteSpace(answer.Answer)?null:answer.Answer,allBranches.Select(ToDto).ToArray(),relevantEvidence,answer.SuggestedActions.Select(action=>new WideActionSuggestionDto(action.DisplayName,action.NavigationRoute,action.Rationale)).ToArray(),timer.ElapsedMilliseconds){ExternalReferences=MapExternalReferences(answer),InterpretiveResults=MapInterpretiveResults(answer,survivorsFinal,externalKnowledge),ExternalKnowledge=externalKnowledge};
        }
        catch
        {
            timer.Stop();
            await wideRepository.CompleteWideExecutionAsync(request.TenantId,request.UserId,executionId,"FAILED",terminationReason,depth,llmCalls,aggregateConfidence,"NONE",null,timer.ElapsedMilliseconds,cancellationToken);
            throw;
        }
    }

    // 'EPH Engine' filter disabled: complete LLM-based result without EPH. One LLM call answers the
    // question directly; the answer is always INTERPRETIVE because nothing is validated against
    // enterprise data. Execution is still audited in EPH.WideExecution for governance.
    private async Task<WideSearchResponse> SearchLlmOnlyAsync(WideSearchRequest request,Stopwatch timer,CancellationToken cancellationToken)
    {
        var executionId=await wideRepository.StartWideExecutionAsync(new(request.TenantId,request.UserId,request.Query,request.CorrelationId),cancellationToken);
        try
        {
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_ANSWER",
                "Answer the user's question directly using your own knowledge. Enterprise grounding is disabled for this request, so no enterprise data was retrieved or validated. Set verificationCode to INTERPRETIVE. Clearly note the answer is not verified against enterprise data. Suggested actions must be generic navigation suggestions only; never invent record identifiers. Also provide externalReferences: up to 6 real-world reference links from your knowledge that best answer the question. Each reference needs title, a well-known REAL absolute https URL (official sites, Wikipedia, or authoritative organizations only - never invent or guess deep links; prefer stable root/wiki pages you are certain exist), source, a one-sentence summary, and branchDisplayName set to the question topic. Return an empty array when no trustworthy reference exists. Also provide interpretiveResults: one entry answering the question topic with branchDisplayName set to the question topic, interpretation restating the question, and items: the actual, complete ranked result set the question asks for (rankNumber, name, one-sentence detail). Each item name must be the MOST SPECIFIC individual entity the question asks about - a concrete product model, title, or named instance (for example 'Predator P3 REVO', not 'Predator') - never just a brand, manufacturer, or category unless the question explicitly asks for brands. Return an empty interpretiveResults array only when the question does not ask for a ranked or enumerable result. For each interpretiveResults entry also set dataVolatility: TIME_SENSITIVE when the result depends on current prices, interest rates, market rankings, availability, versions, or other facts that change over months; STABLE when the knowledge is durable. For TIME_SENSITIVE entries do NOT state specific prices, rates, percentages, model years, or numeric rankings from memory - instead describe the evaluation criteria, comparison factors, and where current figures can be verified.",
                $"Question: {request.Query}",
                AnswerSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_LLM_ONLY",null,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
            var answer=JsonSerializer.Deserialize<WideAnswerProposal>(result.Content,JsonOptions)??throw new ValidationException("The Wide LLM-only answer response was empty.");
            var confidence=Math.Clamp(answer.Confidence,0,1);
            timer.Stop();
            await wideRepository.CompleteWideExecutionAsync(request.TenantId,request.UserId,executionId,"COMPLETED","LLM_ONLY",0,1,confidence,"INTERPRETIVE",string.IsNullOrWhiteSpace(answer.Answer)?null:answer.Answer,timer.ElapsedMilliseconds,cancellationToken);
            return new(executionId,request.Query,"COMPLETED","LLM_ONLY",0,1,confidence,"INTERPRETIVE",string.IsNullOrWhiteSpace(answer.Answer)?null:answer.Answer,[],[],answer.SuggestedActions.Select(action=>new WideActionSuggestionDto(action.DisplayName,action.NavigationRoute,action.Rationale)).ToArray(),timer.ElapsedMilliseconds){ExternalReferences=MapExternalReferences(answer),InterpretiveResults=MapInterpretiveResults(answer,[],[])};
        }
        catch
        {
            timer.Stop();
            await wideRepository.CompleteWideExecutionAsync(request.TenantId,request.UserId,executionId,"FAILED","LLM_ONLY",0,1,0m,"NONE",null,timer.ElapsedMilliseconds,cancellationToken);
            throw;
        }
    }

    // Enterprise grounding: a branch mapped to an approved capability executes a deterministic authorized
    // search; child evidence is intersected with the parent's evidence (progressive narrowing of the same
    // entity type). Unmapped branches survive as INTERPRETIVE reasoning paths.
    private async Task<(string StatusCode,int EvidenceCount)> GroundBranchAsync(WideBranchRecord branch,EphSearchRequest ephRequest,IReadOnlyCollection<EphCapabilityDto> capabilities,int maximumResults,Dictionary<Guid,HashSet<string>> branchEvidenceKeys,List<EphEvidenceDto> evidence,CancellationToken cancellationToken)
    {
        var capability=branch.CapabilityCode is null?null:capabilities.FirstOrDefault(item=>item.CapabilityCode.Equals(branch.CapabilityCode,StringComparison.OrdinalIgnoreCase)&&item.ExecutionHandlerCode.Equals("AUTHORIZED_SEARCH_DOCUMENT",StringComparison.OrdinalIgnoreCase));
        if(capability is null)return("INTERPRETIVE",0);
        var searchText=NormalizeEphSearchText(branch.SearchText??branch.DisplayName,capability);
        var ephBranch=new EphBranchRecord(branch.WideBranchId,branch.ParentWideBranchId,branch.BranchCode,branch.DisplayName,branch.Interpretation,capability.CapabilityCode,"VALID","Wide dynamic grounding.",searchText,capability.SupportsRecency,branch.Confidence,branch.SortOrder);
        var branchEvidence=await repository.ExecuteEphBranchAsync(ephRequest,ephBranch,capability,maximumResults,cancellationToken);
        if(branch.ParentWideBranchId is{}parentId&&branchEvidenceKeys.TryGetValue(parentId,out var parentKeys)&&parentKeys.Any(key=>key.StartsWith($"{capability.EntityTypeCode}:",StringComparison.OrdinalIgnoreCase)))
            branchEvidence=branchEvidence.Where(item=>parentKeys.Contains($"{item.EntityTypeCode}:{item.EntityId:D}")).ToArray();
        branchEvidenceKeys[branch.WideBranchId]=branchEvidence.Select(item=>$"{item.EntityTypeCode}:{item.EntityId:D}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        evidence.AddRange(branchEvidence);
        return("GROUNDED",branchEvidence.Count);
    }

    private async Task<WideIntentProposal> ProposeIntentAsync(WideSearchRequest request,IReadOnlyCollection<EphCapabilityDto> capabilities,WideConfiguration configuration,CancellationToken cancellationToken)
    {
        var catalog=BuildCatalog(capabilities);
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_INTENT",
            "You disambiguate an ambiguous enterprise question by dynamically constructing a problem-specific hierarchy. Propose the top level: distinct, mutually exclusive interpretation branches of the question. Branches are NOT limited to the supplied capability catalog — general, industry, and conceptual interpretations are allowed. Map capabilityCode only when the catalog can genuinely ground the branch against enterprise data; otherwise use null. For each branch set continueNarrowing=true when a meaningfully narrower sub-level exists, otherwise false with a stopReason of FULLY_DISAMBIGUATED, NO_FURTHER_RELEVANT_SUBDIVISION, EVIDENCE_SUFFICIENT, or INTERPRETATION_EXHAUSTED. Never claim records exist and never produce SQL.",
            $"Ambiguous question: {request.Query}\nMaximum branches: {configuration.MaximumBranchesPerLevel}\nApproved capability catalog (for optional grounding):\n{catalog}",
            IntentSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_INTENT",null,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
        return JsonSerializer.Deserialize<WideIntentProposal>(result.Content,JsonOptions)??throw new ValidationException("The Wide intent response was empty.");
    }

    private async Task<WideLevelProposal> ProposeNextLevelAsync(WideSearchRequest request,IReadOnlyCollection<WideBranchRecord> parents,IReadOnlyCollection<EphCapabilityDto> capabilities,WideConfiguration configuration,int levelNumber,List<EphEvidenceDto> evidence,CancellationToken cancellationToken)
    {
        var catalog=BuildCatalog(capabilities);
        var parentSummary=string.Join('\n',parents.Select(parent=>
        {
            var samples=evidence.Where(item=>item.HierarchyBranchId==parent.WideBranchId).Take(3).Select(item=>item.Title);
            return $"- {parent.BranchCode} \"{parent.DisplayName}\" ({parent.GroundingStatusCode}, evidence: {parent.EvidenceCount}, confidence: {parent.Confidence:P0}): {parent.Interpretation}{(parent.EvidenceCount>0?$" | sample evidence: {string.Join("; ",samples)}":string.Empty)}";
        }));
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_HIERARCHY_STEP",
            "Continue a dynamic problem-specific disambiguation hierarchy. For each surviving parent branch, propose narrower child branches that progressively move toward a more specific subset of the parent interpretation, informed by the parent's enterprise grounding outcome (evidence counts and samples supplied). Set parentBranchCode to the exact parent branchCode. Children of grounded parents should stay in the same entity type so evidence can be intersected. Branches are not limited to the capability catalog; map capabilityCode only when the catalog genuinely grounds the child, otherwise null. Set continueNarrowing=false with a stopReason when no meaningfully narrower relevant subdivision remains — do not invent depth for its own sake. Never claim records exist and never produce SQL.",
            $"Original question: {request.Query}\nLevel to propose: {levelNumber}\nMaximum branches per parent: {configuration.MaximumBranchesPerLevel}\nSurviving parent branches with grounding outcomes:\n{parentSummary}\nApproved capability catalog (for optional grounding):\n{catalog}",
            LevelSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_HIERARCHY_STEP",null,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
        return JsonSerializer.Deserialize<WideLevelProposal>(result.Content,JsonOptions)??throw new ValidationException("The Wide hierarchy step response was empty.");
    }

    // Cache-first live external grounding for interpretive narrowing paths. Any failure returns an
    // empty collection so the Wide pipeline never breaks when the provider is unavailable.
    private async Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> GatherExternalKnowledgeAsync(WideSearchRequest request,IReadOnlyCollection<WideBranchRecord> interpretiveBranches,CancellationToken cancellationToken)
    {
        if(interpretiveBranches.Count==0)return [];
        try
        {
            var configuration=await wideRepository.GetExternalGroundingConfigurationAsync(request.TenantId,cancellationToken);
            if(!configuration.Enabled||string.IsNullOrWhiteSpace(configuration.ApiKey))return [];
            var snippets=new List<WideExternalKnowledgeSnippet>();
            var notBeforeUtc=DateTime.UtcNow.AddHours(-configuration.CacheHours);
            foreach(var branch in interpretiveBranches.OrderBy(item=>item.LevelNumber).ThenByDescending(item=>item.Confidence).Take(configuration.MaximumQueriesPerExecution))
            {
                var query=NormalizeQuery($"{request.Query} {branch.DisplayName}").ToLowerInvariant();
                var cached=await wideRepository.GetCachedExternalKnowledgeAsync(request.TenantId,query,notBeforeUtc,cancellationToken);
                if(cached.Count>0){snippets.AddRange(cached.Take(configuration.MaximumSnippetsPerQuery));continue;}
                var retrieved=await externalKnowledgeProvider.SearchAsync(query,configuration,cancellationToken);
                if(retrieved.Count==0)continue;
                await wideRepository.SaveExternalKnowledgeAsync(request.TenantId,request.UserId,query,retrieved,cancellationToken);
                snippets.AddRange(retrieved);
            }
            return snippets;
        }
        catch(Exception)when(!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
    }

    private async Task<WideAnswerProposal> ComposeAnswerAsync(WideSearchRequest request,IReadOnlyCollection<WideBranchRecord> survivors,IReadOnlyCollection<EphEvidenceDto> ranked,decimal confidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,CancellationToken cancellationToken)
    {
        var paths=string.Join('\n',survivors.OrderBy(branch=>branch.LevelNumber).ThenBy(branch=>branch.SortOrder).Select(branch=>$"- L{branch.LevelNumber} {branch.DisplayName} ({branch.GroundingStatusCode}, evidence: {branch.EvidenceCount}, confidence: {branch.Confidence:P0}): {branch.Interpretation}"));
        // All interpretive narrowing paths (Level 1 first, then highest confidence) drive real-world reference
        // and interpretive result-set generation; the branch sub-header (Interpretation) is fed to the LLM.
        var topInterpretiveBranches=survivors.Where(branch=>branch.GroundingStatusCode=="INTERPRETIVE").OrderBy(branch=>branch.LevelNumber).ThenByDescending(branch=>branch.Confidence).ToArray();
        var topInterpretive=string.Join('\n',topInterpretiveBranches.Select((branch,index)=>$"{index+1}. [L{branch.LevelNumber}] {branch.DisplayName} ({branch.Confidence:P0}): {branch.Interpretation}"));
        var grounding=ranked.Count==0?"(no enterprise evidence)":string.Join('\n',ranked.Take(12).Select(item=>$"[{item.RankNumber}] {item.Title} ({item.EntityTypeCode}): {item.Excerpt}"));
        // Clamp each live snippet so external grounding cannot blow the answer prompt past the
        // feature-policy input budget (Tavily content blocks can be several thousand characters).
        var externalGrounding=externalKnowledge.Count==0?"(none)":string.Join('\n',externalKnowledge.Take(10).Select((snippet,index)=>$"E{index+1}. {Truncate(snippet.Title,150)} ({snippet.Url}, retrieved {snippet.RetrievedDateUtc:yyyy-MM-dd}): {Truncate(snippet.Snippet,900)}"));
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_ANSWER",
            "Compose the final answer of a progressive disambiguation pipeline. First judge each supplied enterprise evidence item: include its number in relevantEvidenceNumbers ONLY when the record genuinely answers or supports the question. Keyword search can match superficially (for example a name token matching an unrelated email address); such items are irrelevant and must be excluded. Statements supported by relevant evidence must cite evidence numbers in brackets. Reasoning not supported by evidence must be explicitly labeled as interpretation not verified against enterprise data. Set verificationCode to VERIFIED when the answer is fully evidence-backed, PARTIALLY_VERIFIED when mixed, INTERPRETIVE when no relevant evidence supports it. Suggested actions must be navigation suggestions only, using routes present in the evidence when available; never invent record identifiers. Additionally, for the supplied numbered interpretive narrowing paths, provide externalReferences: up to 6 real-world reference links from your knowledge that best answer the question along those paths. Each reference needs title, a well-known REAL absolute https URL (official sites, Wikipedia, or authoritative organizations only - never invent or guess deep links; prefer stable root/wiki pages you are certain exist), source (site or organization name), a one-sentence summary, and branchDisplayName set to the interpretive path it supports. If no trustworthy real-world reference exists, return an empty externalReferences array. Additionally provide interpretiveResults: the supplied interpretive narrowing paths are NUMBERED; you MUST return exactly one interpretiveResults entry for EVERY numbered path in the same order - if N numbered paths are supplied, return exactly N entries; never skip, merge, or summarize paths, and verify the entry count equals the path count before responding. For each path, directly answer that path's interpretation text using your own knowledge and return the actual, complete result set it asks for (for example, when the interpretation asks for a top 5 ranking, return all 5 ranked entries). Each interpretiveResults entry needs branchDisplayName set to the exact path display name, interpretation echoing the path interpretation text, and items: the complete ranked result set with rankNumber (1-based), name, and a one-sentence detail explaining why it holds that rank. Each item name must be the MOST SPECIFIC individual entity the interpretation asks about - a concrete product model, title, or named instance (for example 'Predator P3 REVO', not 'Predator') - never just a brand, manufacturer, or category unless the interpretation explicitly asks for brands; when a brand is relevant, include it as part of the specific item name. This is interpretive knowledge, not enterprise data; never leave items empty when the interpretation asks for a ranked or enumerable result. Return an empty interpretiveResults array only when no interpretive paths are supplied. For each interpretiveResults entry also set dataVolatility: TIME_SENSITIVE when the result depends on current prices, interest rates, market rankings, availability, versions, or other facts that change over months; STABLE when the knowledge is durable. For TIME_SENSITIVE entries, unless external evidence snippets are supplied for that path, do NOT state specific prices, rates, percentages, model years, or numeric rankings from memory - instead describe the evaluation criteria, comparison factors, and where current figures can be verified. When external evidence snippets ARE supplied (the numbered E1..En list), you MUST extract and state the concrete figures from them: each item detail on an externally grounded TIME_SENSITIVE path must include the actual number the interpretation asks about (for example the MPG/MPGe rating, price in dollars, interest rate percentage, or ranking score) followed by the snippet citation in the form [E3]. Never replace available figures with vague qualifiers like 'great mileage' or 'excellent economy' - if a snippet states 57 MPG, write '57 MPG combined [E2]'. Only when the snippets genuinely contain no figure for a specific item may the detail fall back to criteria language, and it must then say the figure was not found in the retrieved sources.",
            $"Question: {request.Query}\nOverall confidence: {confidence:P0}\nSurviving disambiguation paths:\n{paths}\nNumbered interpretive narrowing paths ({topInterpretiveBranches.Length} paths - return {topInterpretiveBranches.Length} interpretiveResults entries):\n{(string.IsNullOrEmpty(topInterpretive)?"(none)":topInterpretive)}\nEnterprise evidence:\n{grounding}\nExternal evidence snippets (live web, current figures - use these for TIME_SENSITIVE paths):\n{externalGrounding}",
            AnswerSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_ANSWER",null,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
        return JsonSerializer.Deserialize<WideAnswerProposal>(result.Content,JsonOptions)??throw new ValidationException("The Wide answer response was empty.");
    }

    private static WideBranchRecord[] MaterializeBranches(IReadOnlyCollection<WideProposedBranch> proposed,Guid executionId,Guid tenantId,int levelNumber,IReadOnlyDictionary<string,WideBranchRecord> parentsByCode,WideConfiguration configuration)=>
        (proposed??[]).Take(configuration.MaximumBranchesPerLevel*Math.Max(parentsByCode.Count,1)).Select((branch,index)=>
        {
            Guid? parentId=branch.ParentBranchCode is not null&&parentsByCode.TryGetValue(NormalizeCode(branch.ParentBranchCode),out var parent)?parent.WideBranchId:null;
            // Clamp LLM free text to EPH.WideBranch column lengths (DB is the source of truth).
            return new WideBranchRecord(Guid.NewGuid(),executionId,parentId,tenantId,levelNumber,Truncate(NormalizeCode(branch.BranchCode),120),Truncate(branch.DisplayName.Trim(),300),Truncate(branch.Interpretation.Trim(),1000),Truncate(branch.CapabilityCode?.Trim(),100),Truncate(branch.SearchText?.Trim(),400),"PENDING",0,Math.Clamp(branch.Confidence,0,1),branch.ContinueNarrowing,Truncate(branch.StopReason?.Trim(),50),false,null,index+1);
        }).ToArray();

    private static string? Truncate(string? value,int maximumLength)=>value is null||value.Length<=maximumLength?value:value[..maximumLength];

    private static decimal ComputeAggregateConfidence(IReadOnlyCollection<WideBranchRecord> survivors)
    {
        if(survivors.Count==0)return 0m;
        // Evidence-weighted: grounded branches with evidence pull confidence up; interpretive branches contribute their raw confidence.
        var weighted=survivors.Select(branch=>branch.GroundingStatusCode=="GROUNDED"&&branch.EvidenceCount>0?Math.Clamp(branch.Confidence+.15m,0,1):branch.Confidence);
        return Math.Clamp(weighted.Max(),0,1);
    }

    private static WideBranchDto ToDto(WideBranchRecord branch)=>new(branch.WideBranchId,branch.ParentWideBranchId,branch.LevelNumber,branch.BranchCode,branch.DisplayName,branch.Interpretation,branch.CapabilityCode,branch.SearchText,branch.GroundingStatusCode,branch.EvidenceCount,branch.Confidence,branch.ContinueNarrowing,branch.StopReason,branch.IsEliminated,branch.EliminationReason,branch.SortOrder);

    // Accept only well-formed absolute https URLs so hallucinated or unsafe links never reach the UI.
    private static WideExternalReferenceDto[] MapExternalReferences(WideAnswerProposal answer)=>
        (answer.ExternalReferences??[]).Where(reference=>Uri.TryCreate(reference.Url,UriKind.Absolute,out var uri)&&uri.Scheme==Uri.UriSchemeHttps)
            .Take(6).Select(reference=>new WideExternalReferenceDto(reference.Title.Trim(),reference.Url.Trim(),reference.Source.Trim(),reference.Summary.Trim(),reference.BranchDisplayName.Trim())).ToArray();

    // Interpretive result sets answered by the LLM for the interpretive narrowing paths, arranged with
    // Level 1 branches first, then by interpretive scoring (branch confidence, highest first).
    private static WideInterpretiveResultDto[] MapInterpretiveResults(WideAnswerProposal answer,IReadOnlyCollection<WideBranchRecord> survivors,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge)
    {
        var interpretive=survivors.Where(branch=>branch.GroundingStatusCode=="INTERPRETIVE").GroupBy(branch=>branch.DisplayName.Trim(),StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key,group=>(Level:group.Min(branch=>branch.LevelNumber),Confidence:group.Max(branch=>branch.Confidence)),StringComparer.OrdinalIgnoreCase);
        var externallyGrounded=externalKnowledge.Count>0;
        return (answer.InterpretiveResults??[]).Where(result=>result.Items is{Count:>0})
            .Select(result=>new WideInterpretiveResultDto(result.BranchDisplayName.Trim(),result.Interpretation.Trim(),interpretive.TryGetValue(result.BranchDisplayName.Trim(),out var branch)?branch.Confidence:Math.Clamp(answer.Confidence,0,1),result.Items.OrderBy(item=>item.RankNumber).Select((item,index)=>new WideInterpretiveResultItemDto(item.RankNumber>0?item.RankNumber:index+1,item.Name.Trim(),item.Detail.Trim())).ToArray()){DataVolatility=result.DataVolatility?.Trim().ToUpperInvariant()=="TIME_SENSITIVE"?"TIME_SENSITIVE":"STABLE",IsExternallyGrounded=externallyGrounded})
            .OrderBy(result=>interpretive.TryGetValue(result.BranchDisplayName,out var branch)?branch.Level:int.MaxValue)
            .ThenByDescending(result=>result.Confidence).ToArray();
    }

    private static string BuildCatalog(IReadOnlyCollection<EphCapabilityDto> capabilities)=>capabilities.Count==0?"(none — knowledge-only mode; capabilityCode must always be null)":string.Join('\n',capabilities.Select(capability=>$"{capability.CapabilityCode}: {capability.Description}; approved terms: {string.Join(", ",capability.ApprovedTerms)}; entity: {capability.EntityTypeCode}"));

    private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNameCaseInsensitive=true};

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
        "continueNarrowing": { "type": "boolean" },
        "stopReason": { "type": ["string", "null"] },
        "parentBranchCode": { "type": ["string", "null"] }
      },
      "required": ["branchCode", "displayName", "interpretation", "capabilityCode", "searchText", "confidence", "continueNarrowing", "stopReason", "parentBranchCode"],
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
                "detail": { "type": "string" }
              },
              "required": ["rankNumber", "name", "detail"],
              "additionalProperties": false
            }
          }
        },
        "required": ["branchDisplayName", "interpretation", "dataVolatility", "items"],
        "additionalProperties": false
      }
    }
  },
  "required": ["answer", "verificationCode", "confidence", "relevantEvidenceNumbers", "externalReferences", "suggestedActions", "interpretiveResults"],
  "additionalProperties": false
}
""";
}
