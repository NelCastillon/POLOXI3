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
public sealed class IntelligenceWideService(IIntelligenceRepository repository,IIntelligenceWideRepository wideRepository,IAiProviderRouter aiProviderRouter,IExternalKnowledgeProvider externalKnowledgeProvider):IIntelligenceWideService
{
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
        foreach(var branch in validBranches)
        {
            var capability=capabilities.First(item=>item.CapabilityCode.Equals(branch.CapabilityCode,StringComparison.OrdinalIgnoreCase));
            var branchEvidence=await repository.ExecutePoloxiBranchAsync(request,branch,capability,configuration.MaximumResults,cancellationToken);
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
                var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_POLOXI_EXPLANATION","Explain only the supplied authorized POLOXI evidence. Cite evidence numbers in brackets. Clearly state unsupported hierarchy branches and never invent facts.",$"Question: {request.Query}\nValidated concept: {hierarchy.DisplayName}\nEvidence:\n{grounding}",null,request.CorrelationId,new("Intelligence",null,null,request.Query,"POLOXI_EVIDENCE",executionId,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
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
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_POLOXI_HIERARCHY","Propose a concise enterprise progressive hierarchy at most two levels deep. Top-level branches are broad entry points; child branches must progressively narrow their parent toward a more specific subset (for example a status, lifecycle stage, or qualifier of the parent), and children must always have empty children arrays. A child narrows the same entity type as its parent and its results are intersected with the parent results, so only nest when top-down narrowing genuinely applies. You may invent reasoning branches, but map a branch to a capabilityCode only when the supplied catalog can ground it. Use null capabilityCode for unsupported branches. Never produce SQL or claim records exist.",$"Question: {request.Query}\nMaximum branches: {configuration.MaximumBranches}\nApproved capability catalog:\n{catalog}",schema,request.CorrelationId,new("Intelligence",null,null,request.Query,"POLOXI_HIERARCHY",null,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
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
        var executionId=await wideRepository.StartWideExecutionAsync(new(request.TenantId,request.UserId,request.Query,request.CorrelationId),cancellationToken);
        var llmCalls=0;
        var allBranches=new List<WideBranchRecord>();
        var evidence=new List<PoloxiEvidenceDto>();
        var branchEvidenceKeys=new Dictionary<Guid,HashSet<string>>();
        var depth=0;
        var terminationReason="LLM_COMPLETE";
        var aggregateConfidence=0m;
        var poloxiRequest=new PoloxiSearchRequest(request.TenantId,request.UserId,request.Query,request.MaximumResults,request.CorrelationId){GrantedPermissions=request.GrantedPermissions};
        try
        {
            // Stage 0 (V2.1): Query Contract — separate hard constraints, output requirements, and the
            // ambiguous concepts that actually need disambiguation. Fail-soft: a null contract degrades
            // to the V2 behavior of branching the whole query.
            WideQueryContract? queryContract=null;
            if(configuration.EnableQueryContract)
            {
                queryContract=await ExtractQueryContractAsync(request,cancellationToken);
                if(queryContract is not null)llmCalls++;
            }

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
                if(depth>=configuration.AbsoluteDepthCeiling){terminationReason="DEPTH_CEILING_REACHED";break;}
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
            var ranked=RankEvidence(survivingEvidence,poloxiRequest,new(false,1,configuration.MinimumBranchConfidence,configuration.MaximumBranchesPerLevel*Math.Max(depth,1),request.MaximumResults));

            // Stage 3: verified answer composed from surviving paths + enterprise evidence.
            var survivorsFinal=allBranches.Where(branch=>!branch.IsEliminated).ToArray();
            if(aggregateConfidence==0m&&survivorsFinal.Length>0)aggregateConfidence=ComputeAggregateConfidence(survivorsFinal);
            // Live external grounding (fail-soft): retrieve fresh web snippets for interpretive paths so
            // time-sensitive figures come from current sources instead of stale model memory.
            var externalKnowledge=await GatherExternalKnowledgeAsync(request,survivorsFinal.Where(branch=>branch.GroundingStatusCode=="INTERPRETIVE").ToArray(),configuration.ExternalRetrievalConcurrency,cancellationToken);
            // V2.1 three-score model: Interpretation Prior (LLM), Evidence Support (deterministic, from
            // enterprise evidence and matched external snippets), POLOXI Confidence (weighted combination).
            survivorsFinal=survivorsFinal.Select(branch=>
            {
                var support=ComputeEvidenceSupport(branch,evidence,externalKnowledge);
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
            var initialEntropy=ComputeUncertainty(survivorsFinal,candidateUniverse,evidence,externalKnowledgeAll,queryContract);
            var finalEntropy=initialEntropy;
            if(configuration.EnableInformationValue&&survivorsFinal.Length>0)
            {
                var weakRounds=0;
                // V2.5 Marginal Information Value state: which branches were already investigated and
                // how effective the previous round actually was. A good player never asks the same
                // question twice unless the first answer demonstrably helped.
                var investigationCounts=new Dictionary<Guid,int>();
                    var priorRoundEffectiveness=1m;
                for(var round=1;round<=configuration.MaximumInformationRounds;round++)
                {
                    // V2.5: freeze the candidate population for this round. EntropyBefore and EntropyAfter
                    // MUST be measured over the IDENTICAL candidate set, otherwise Hmax=log2(N) shifts and
                    // ActualInformationGain compares incomparable distributions. Names discovered during
                    // this round join the NEXT round's basis instead.
                    var roundCandidateBasis=candidateUniverse.ToArray();
                    var entropyBefore=ComputeUncertainty(survivorsFinal,roundCandidateBasis,evidence,externalKnowledgeAll,queryContract);
                    finalEntropy=entropyBefore;
                    if(entropyBefore.NormalizedEntropy<configuration.InformationValueTriggerEntropy)break;
                    if(llmCalls+2>configuration.MaximumTotalLlmCalls)break;
                    try
                    {
                        // One batched estimate for all eligible (ACTIVE/SECONDARY) branches.
                        var eligible=survivorsFinal.Where(branch=>branch.BranchStateCode is WideBranchStates.Active or WideBranchStates.Secondary).ToArray();
                        if(eligible.Length<2)break;
                        var proposal=await EstimateInformationValueAsync(request,eligible,entropyBefore,queryContract,cancellationToken);
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
                            var raw=Math.Clamp(
                                .20m*CategoryValue(configuration,target.Uncertainty)
                                +.25m*CategoryValue(configuration,target.RankingImpact)
                                +.25m*CategoryValue(configuration,target.CandidateDiscrimination)
                                +.15m*CategoryValue(configuration,target.EvidenceAvailability)
                                +.10m*CategoryValue(configuration,target.Novelty)
                                -.05m*CategoryValue(configuration,target.Redundancy),0,1);
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
                        var newKnowledge=await GatherExternalKnowledgeAsync(request,retrievalBranches,configuration.ExternalRetrievalConcurrency,cancellationToken);
                        informationRetrievalCount+=newKnowledge.Count;
                        externalKnowledgeAll.AddRange(newKnowledge);
                        // V2.4: newly retrieved snippets may name candidates not yet in the universe.
                        candidateUniverse.UnionWith(HarvestCandidateNames(externalKnowledgeAll));
                        // Normal POLOXI scoring/reweighting on the enriched evidence pool (never LLM-calculated).
                        survivorsFinal=survivorsFinal.Select(branch=>
                        {
                            var support=ComputeEvidenceSupport(branch,evidence,externalKnowledgeAll);
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
                        var entropyAfter=ComputeUncertainty(survivorsFinal,roundCandidateBasis,evidence,externalKnowledgeAll,queryContract);
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
            try
            {
                answer=await ComposeAnswerAsync(request,survivorsFinal,ranked,aggregateConfidence,externalKnowledge,queryContract,cancellationToken);
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
            if(interpretiveResults.Length>0&&llmCalls<configuration.MaximumTotalLlmCalls)
            {
                candidates=await CompeteCandidatesAsync(request,executionId,queryContract,survivorsFinal,interpretiveResults,candidateUniverse,externalKnowledgeAll,configuration,cancellationToken);
                if(candidates.Count>0)llmCalls++;
            }
            // V2.8.6 post-competition identity dedup: the LLM competition can still echo the same entity
            // under two name forms ("Overland Park, Kansas" and "Overland Park"). Same canonical tokens =
            // same entity = one ranking position. Keep the stronger-scored instance, drop the echo, and
            // re-rank so the next candidate moves up — scores are NEVER altered, only duplicates removed.
            if(candidates.Count>1)candidates=DeduplicateCandidatesByCanonicalTokens(candidates);
            // V2.8.5 answer→candidate reweighting: a clarification answer is DIRECT intent evidence about
            // the candidates themselves, not just a query constraint. Candidates whose name/detail overlap
            // the user's answer get a deterministic composite boost and are re-ranked — zero-LLM, so the
            // user's choice reliably moves the ranking even when the re-executed evidence run was noisy.
            if(!string.IsNullOrWhiteSpace(request.ClarificationAnswer)&&candidates.Count>1)
                candidates=ReweightCandidatesByClarificationAnswer(candidates,request.ClarificationAnswer);
            // V2.9.2 Output Contract Validation: the delivered ranking must mechanically satisfy the
            // query contract. Requested 10 cities → 10 valid candidates; a shortfall is a validation
            // failure, not a composition style choice. One recovery pass re-runs the competition with
            // relaxed candidate discovery (single-source evidence names admitted) to widen the pool;
            // any remaining shortfall is DISCLOSED via the answer contract, never silently accepted.
            WideOutputContractResultDto? outputContract=null;
            if(queryContract?.RequestedCount is int contractCount&&contractCount>0&&candidates.Count>0)
            {
                var deliveredCount=candidates.Count(candidate=>!candidate.IsConstraintViolation);
                var recoveryAttempted=false;
                if(deliveredCount<contractCount&&llmCalls<configuration.MaximumTotalLlmCalls)
                {
                    recoveryAttempted=true;
                    var recovered=await CompeteCandidatesAsync(request,executionId,queryContract,survivorsFinal,interpretiveResults,candidateUniverse,externalKnowledgeAll,configuration,cancellationToken,isRecoveryPass:true);
                    if(recovered.Count>0)
                    {
                        llmCalls++;
                        if(recovered.Count>1)recovered=DeduplicateCandidatesByCanonicalTokens(recovered);
                        if(!string.IsNullOrWhiteSpace(request.ClarificationAnswer)&&recovered.Count>1)
                            recovered=ReweightCandidatesByClarificationAnswer(recovered,request.ClarificationAnswer);
                        // Keep the recovery only when it actually improved contract compliance.
                        if(recovered.Count(candidate=>!candidate.IsConstraintViolation)>deliveredCount)
                        {
                            candidates=recovered;
                            deliveredCount=candidates.Count(candidate=>!candidate.IsConstraintViolation);
                        }
                    }
                }
                outputContract=new(contractCount,deliveredCount,deliveredCount>=contractCount){RecoveryAttempted=recoveryAttempted};
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
                // Termination-label honesty: CONFIDENCE_REACHED was decided from the HIERARCHY-level
                // aggregate confidence before the candidate competition ran. When the calibrated decision
                // confidence falls below the target, the semantic claim "the decision converged" is no
                // longer supported by the telemetry — relabel with why investigation actually stopped.
                if(terminationReason=="CONFIDENCE_REACHED"&&decisionConfidence.Value<configuration.TargetConfidence)
                    terminationReason=informationRounds.Count>=configuration.MaximumInformationRounds?"EVIDENCE_BUDGET_REACHED"
                        :totalActualInformationGain<=configuration.MinimumActualInformationGain?"MARGINAL_INFORMATION_VALUE_EXHAUSTED"
                        :"HIERARCHY_CONFIDENCE_REACHED";
            }
            else if(candidates.Count>0)
            {
                // V2.9.5 Confidence Eligibility Gate: every scored candidate was ruled out by the
                // constraint/granularity engine — there are ZERO eligible finalists. A run with no
                // eligible winner cannot claim the decision converged: cap the reported confidence
                // deterministically and report the real limiting factor. CONFIDENCE_REACHED requires
                // eligible finalists > 0 by invariant.
                decisionConfidence=Math.Min(aggregateConfidence,.25m);
                aggregateConfidence=decisionConfidence.Value;
                terminationReason="INSUFFICIENT_ELIGIBLE_CANDIDATES";
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
                var retrievalStalled=informationRounds.Count>=configuration.MaximumInformationRounds
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
                    var optionItems=comparisonCandidates.Where(candidate=>!candidate.IsConstraintViolation)
                        .Select((candidate,index)=>new WideClarificationOptionDto($"OPTION_{index+1}",
                            string.IsNullOrWhiteSpace(candidate.Detail)?candidate.DisplayName:$"{TrimDescription(candidate.Detail)} ({candidate.DisplayName})"))
                        .ToList();
                    optionItems.Add(new("OTHER","Something else — none of these match"));
                    clarificationOptionItems=optionItems;
                    clarificationOptions=optionItems.Select(option=>option.Label).ToArray();
                    clarificationQuestion=$"I found multiple plausible answers and the available evidence cannot determine which one you mean. Which sounds like the one you're looking for ({clarificationTarget} differs most between them)?";
                    answerStatus="USER_CLARIFICATION_REQUIRED";
                    terminationReason="USER_CLARIFICATION_REQUIRED";
                }
            }
            await wideRepository.UpdateWideExecutionContractAsync(request.TenantId,request.UserId,executionId,queryContract is null?null:JsonSerializer.Serialize(queryContract,JsonOptions),evidenceCoverage,externalKnowledge.Count,relevantEvidence.Length,candidates.Count,cancellationToken);
            // V2.9 Answer Composer: derive the presentation contract deterministically from the final
            // Candidate × Branch outcome — zero-LLM, computed AFTER the gate so response mode reflects it.
            var answerContext=ComposeAnswerContext(answerStatus,topCandidates,decisionConfidence,winnerStability,decisionEvidenceCoverage,isIntentGap,answer.CandidateInsights,outputContract);
            // V2.2: persist execution-level entropy summary and information-round counters (fail-soft).
            try{await wideRepository.UpdateWideExecutionEntropyAsync(request.TenantId,request.UserId,new(executionId,initialEntropy.Entropy,finalEntropy.Entropy,initialEntropy.NormalizedEntropy,finalEntropy.NormalizedEntropy,totalActualInformationGain,informationRounds.Count,informationTargetCount,informationRetrievalCount){EntropyBasisCode=finalEntropy.EntropyBasisCode,DecisionConfidence=decisionConfidence,ClarificationTarget=clarificationTarget,ClarificationQuestion=clarificationQuestion,IntentEntropy=intentEntropy,PriorIntentEntropy=request.PriorIntentEntropy,ClarificationGain=clarificationGain,ClarificationRound=request.ClarificationRound},cancellationToken);}catch{/* diagnostics only; never blocks the answer */}
            // V2.9.2 Ranking Lock: the LLM interprets, POLOXI decides. When a deterministic Candidate ×
            // Branch competition produced a ranking, that ranking is authoritative — the composed prose
            // must never contradict it. The locked ordered ranking is prepended to the final answer text
            // so the Full Answer and the ranking cards can never disagree, regardless of LLM output.
            var finalAnswerText=string.IsNullOrWhiteSpace(answer.Answer)?null:answer.Answer;
            if(finalAnswerText is not null&&topCandidates.Length>0&&answerStatus!="USER_CLARIFICATION_REQUIRED")
            {
                var lockedRanking=string.Join(" ",topCandidates.Take(Math.Max(queryContract?.RequestedCount??0,10)).Select((candidate,index)=>$"{index+1}. {candidate.DisplayName} ({candidate.CompositeScore:P0})."));
                // V2.9.5 candidate-leakage guard: the prose is composed BEFORE the competition, so it
                // can mention interpretive/discovered candidates that did not make the final ranking
                // as though they were winners (FinalAnswerCandidates ⊄ FinalRankedCandidates). The
                // guard is deterministic and zero-LLM: any competed-but-unranked candidate name found
                // in the prose is explicitly disclosed as considered-but-not-selected, so the narrative
                // can never silently promote an unranked candidate.
                var rankedNames=candidates.Where(candidate=>!candidate.IsConstraintViolation).Select(candidate=>candidate.DisplayName).ToArray();
                var leakedNames=candidates.Where(candidate=>candidate.IsConstraintViolation).Select(candidate=>candidate.DisplayName)
                    .Concat(interpretiveResults.SelectMany(result=>result.Items.Select(item=>item.Name)))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Where(name=>IsValidCandidateName(name)
                        &&!rankedNames.Any(ranked=>ranked.Contains(name,StringComparison.OrdinalIgnoreCase)||name.Contains(ranked,StringComparison.OrdinalIgnoreCase))
                        &&finalAnswerText.Contains(name,StringComparison.OrdinalIgnoreCase))
                    .Take(6)
                    .ToArray();
                var leakageNote=leakedNames.Length==0?string.Empty:$"\n\nAlso considered but not selected for the final ranking: {string.Join(", ",leakedNames)}.";
                finalAnswerText=$"Final ranking (deterministic): {lockedRanking}\n\n{finalAnswerText}{leakageNote}";
            }
            timer.Stop();
            await wideRepository.CompleteWideExecutionAsync(request.TenantId,request.UserId,executionId,answerStatus,terminationReason,depth,llmCalls,aggregateConfidence,answer.VerificationCode,finalAnswerText,timer.ElapsedMilliseconds,cancellationToken);
            return new(executionId,request.Query,answerStatus,terminationReason,depth,llmCalls,aggregateConfidence,answer.VerificationCode,finalAnswerText,allBranches.Select(ToDto).ToArray(),relevantEvidence,answer.SuggestedActions.Select(action=>new WideActionSuggestionDto(action.DisplayName,action.NavigationRoute,action.Rationale)).ToArray(),timer.ElapsedMilliseconds){ExternalReferences=MapExternalReferences(answer),InterpretiveResults=interpretiveResults,ExternalKnowledge=externalKnowledge,QueryContract=queryContract,
            Candidates=candidates,EvidenceCoverage=evidenceCoverage,DecisionEvidenceCoverage=decisionEvidenceCoverage,ExternalEvidenceCount=externalKnowledge.Count,EnterpriseEvidenceCount=relevantEvidence.Length,
            InitialEntropy=initialEntropy.Entropy,FinalEntropy=finalEntropy.Entropy,InitialNormalizedEntropy=initialEntropy.NormalizedEntropy,FinalNormalizedEntropy=finalEntropy.NormalizedEntropy,TotalActualInformationGain=totalActualInformationGain,EntropyBasisCode=finalEntropy.EntropyBasisCode,InformationRounds=informationRounds,
            WinnerStability=winnerStability,TopKStability=topKStability,DecisionConfidence=decisionConfidence,
            ClarificationQuestion=clarificationQuestion,ClarificationTarget=clarificationTarget,ClarificationOptions=clarificationOptions,
            ClarificationOptionItems=clarificationOptionItems,IntentEntropy=intentEntropy,BestClarificationValue=bestClarificationValueOut,
            ClarificationGain=clarificationGain,ClarificationRound=request.ClarificationRound,AnswerContext=answerContext};
        }
        catch
        {
            timer.Stop();
            await wideRepository.CompleteWideExecutionAsync(request.TenantId,request.UserId,executionId,"FAILED",terminationReason,depth,llmCalls,aggregateConfidence,"NONE",null,timer.ElapsedMilliseconds,cancellationToken);
            throw;
        }
    }

    // 'POLOXI Engine' filter disabled: complete LLM-based result without POLOXI. One LLM call answers the
    // question directly; the answer is always INTERPRETIVE because nothing is validated against
    // enterprise data. Execution is still audited in POLOXI.WideExecution for governance.
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
        var contractContext=queryContract is null?string.Empty:
            $"\nQuery contract (FIXED, do not reinterpret): entity type: {queryContract.EntityType??"(unspecified)"}; expected answer type (what is allowed to become a final answer candidate): {queryContract.ExpectedAnswerType??"(unspecified)"}; expected answer granularity (the semantic level a valid final answer must sit at): {queryContract.ExpectedAnswerGranularity??"(unspecified)"}; hard constraints: {(queryContract.HardConstraints.Count==0?"(none)":string.Join("; ",queryContract.HardConstraints))}; output requirements: {(queryContract.OutputRequirements.Count==0?"(none)":string.Join("; ",queryContract.OutputRequirements))}\nAmbiguous concepts to disambiguate (branch ONLY these): {(queryContract.AmbiguousConcepts.Count==0?"(whole question)":string.Join("; ",queryContract.AmbiguousConcepts))}";
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_INTENT",
            "You disambiguate an ambiguous enterprise question by dynamically constructing the TOP LEVEL of a problem-specific reasoning hierarchy. The Query Contract is authoritative. Generate only distinct interpretations or decision dimensions whose resolution could materially affect the answer. Branches are NOT limited to the supplied capability catalog - general, industry, and conceptual interpretations are allowed. Map capabilityCode only when the catalog can genuinely ground the branch against enterprise data; otherwise use null. For each branch set continueNarrowing=true when a meaningfully narrower sub-level exists, otherwise false with a stopReason of FULLY_DISAMBIGUATED, NO_FURTHER_RELEVANT_SUBDIVISION, EVIDENCE_SUFFICIENT, or INTERPRETATION_EXHAUSTED. Confidence per branch must be CALIBRATED, not defaulted: it expresses how likely this interpretation matches what the user actually meant, so branches must be differentiated - the most plausible mainstream interpretation scores highest and niche or speculative interpretations score lower. Never assign the same confidence to every branch and never use 1.0; interpretive branches without enterprise grounding are capped at 0.9. For each branch set semanticType: DIMENSION = an aspect along which the problem should be evaluated (jointly valid evaluation criteria - two sibling DIMENSION branches can BOTH be true/relevant at the same time, and there does not need to be a winner among them); ALTERNATIVE = an alternative interpretation of an ambiguous concept, where selecting one makes its siblings incorrect interpretations of the same unknown. When in doubt for ranking, comparison, or best-of questions, prefer DIMENSION. IMPORTANT: ALTERNATIVE does NOT mean 'final answer candidate'. Do not convert evaluation criteria into answer candidates. Do not create entities merely to populate the hierarchy unless the ambiguity itself is directly about competing entities. Example: for 'Which publicly traded U.S. company should I hold for 10 years?' GOOD top-level branches are 'Meaning of unreasonable permanent-loss risk', 'Scope of competitive moat', 'Management quality criteria'; BAD top-level branches are 'Microsoft', 'Alphabet', 'Apple'. Likewise 'Broad Moat' or 'Strategic Vision' may be interpretations but they are not companies. Preserve semantic identity across stages: a DIMENSION is not an ANSWER CANDIDATE, an INTERPRETATION is not EVIDENCE, EVIDENCE is not a candidate, and an ANSWER CANDIDATE must satisfy the expectedAnswerType from the Query Contract. Generate the smallest set of branches that covers the material ambiguity: the maximum branch count is a ceiling, not a target - prefer 3-5 strong branches to many weak or overlapping ones. Never claim records exist and never produce SQL.",
            $"Ambiguous question: {request.Query}{contractContext}\nMaximum branches: {configuration.MaximumBranchesPerLevel}\nApproved capability catalog (for optional grounding):\n{catalog}",
            IntentSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_INTENT",null,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
        return JsonSerializer.Deserialize<WideIntentProposal>(result.Content,JsonOptions)??throw new ValidationException("The Wide intent response was empty.");
    }

    private async Task<WideLevelProposal> ProposeNextLevelAsync(WideSearchRequest request,IReadOnlyCollection<WideBranchRecord> parents,IReadOnlyCollection<PoloxiCapabilityDto> capabilities,WideConfiguration configuration,int levelNumber,List<PoloxiEvidenceDto> evidence,CancellationToken cancellationToken)
    {
        var catalog=BuildCatalog(capabilities);
        var parentSummary=string.Join('\n',parents.Select(parent=>
        {
            var samples=evidence.Where(item=>item.HierarchyBranchId==parent.WideBranchId).Take(3).Select(item=>item.Title);
            return $"- {parent.BranchCode} \"{parent.DisplayName}\" ({parent.GroundingStatusCode}, evidence: {parent.EvidenceCount}, confidence: {parent.Confidence:P0}): {parent.Interpretation}{(parent.EvidenceCount>0?$" | sample evidence: {string.Join("; ",samples)}":string.Empty)}";
        }));
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_HIERARCHY_STEP",
            "Continue the existing problem-specific reasoning hierarchy. You are expanding ONLY the supplied surviving parent branches; the next level MUST depend on the previous level. For each parent: first determine whether further subdivision is genuinely useful; if useful, propose distinct narrower children informed by the parent's enterprise grounding outcome (evidence counts and samples supplied); if not useful, return no children for that parent - returning zero children is a successful response, not a failure. A child must narrow, clarify, test, or decompose its parent. Do NOT restart analysis from the original user question. Do NOT introduce unrelated dimensions merely because another depth level is available. Do NOT restate the parent using different wording and do NOT create semantically duplicate children. Set parentBranchCode to the exact parent branchCode. Children of grounded parents should stay in the same entity type so evidence can be intersected. Branches are not limited to the capability catalog; map capabilityCode only when the catalog genuinely grounds the child, otherwise null. Set continueNarrowing=false with a stopReason when no meaningfully narrower relevant subdivision remains - depth is a maximum, not a target; stop expanding a parent when its ambiguity has been adequately resolved, when narrower children would be semantic restatements, when the next subdivision would not affect a decision, when evidence already resolves the relevant distinction, or when the parent is operationally specific enough for evidence retrieval. Confidence per child must be CALIBRATED, not defaulted: siblings must be differentiated - the most plausible subdivision scores highest and speculative ones score lower. A child may not exceed its parent's confidence unless new supplied grounding evidence explicitly justifies it, never assign the same confidence to every sibling, and never use 1.0; interpretive branches without enterprise grounding are capped at 0.9. Maintain semantic roles: a DIMENSION remains a dimension or decomposes into narrower dimensions (for example Competitive Moat -> Network Effects, Switching Costs, Cost Advantage); an ALTERNATIVE remains an interpretation or decomposes into narrower interpretations. Do not convert a dimension or interpretation into a final answer entity merely because an example is mentioned (Competitive Moat -> Microsoft, Apple is BAD unless the parent itself explicitly represents competition among those entities); concrete entities may appear inside findings and later enter candidate aggregation, but they are not automatically hierarchy branches. Preserve semantic identity across stages: a DIMENSION is not an ANSWER CANDIDATE, an INTERPRETATION is not EVIDENCE, EVIDENCE is not a candidate, and an ANSWER CANDIDATE must satisfy the expectedAnswerType from the Query Contract. For each child set semanticType using this strict test: could TWO sibling children BOTH be true/relevant to the final answer at the same time? If yes, they are DIMENSION; only when selecting one child makes its siblings incorrect interpretations of the same unknown are they ALTERNATIVE; when in doubt for ranking, comparison, or best-of questions, prefer DIMENSION. Prefer 2-4 meaningful children per parent. Never claim records exist and never produce SQL.",
            $"Original question: {request.Query}\nLevel to propose: {levelNumber}\nMaximum branches per parent: {configuration.MaximumBranchesPerLevel}\nSurviving parent branches with grounding outcomes:\n{parentSummary}\nApproved capability catalog (for optional grounding):\n{catalog}",
            LevelSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_HIERARCHY_STEP",null,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
        return JsonSerializer.Deserialize<WideLevelProposal>(result.Content,JsonOptions)??throw new ValidationException("The Wide hierarchy step response was empty.");
    }

    // Cache-first live external grounding for interpretive narrowing paths. Any failure returns an
    // empty collection so the Wide pipeline never breaks when the provider is unavailable.
    // Retrievals run concurrently under a bounded gate; results merge in branch-priority order.
    private async Task<IReadOnlyCollection<WideExternalKnowledgeSnippet>> GatherExternalKnowledgeAsync(WideSearchRequest request,IReadOnlyCollection<WideBranchRecord> interpretiveBranches,int retrievalConcurrency,CancellationToken cancellationToken)
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
                    var query=NormalizeQuery($"{request.Query} {branch.DisplayName}").ToLowerInvariant();
                    var cached=await wideRepository.GetCachedExternalKnowledgeAsync(request.TenantId,query,notBeforeUtc,cancellationToken);
                    if(cached.Count>0){results[index]=cached.Take(configuration.MaximumSnippetsPerQuery).ToArray();return;}
                    var retrieved=await externalKnowledgeProvider.SearchAsync(query,configuration,cancellationToken);
                    if(retrieved.Count==0){results[index]=[];return;}
                    await wideRepository.SaveExternalKnowledgeAsync(request.TenantId,request.UserId,query,retrieved,cancellationToken);
                    results[index]=retrieved;
                }
                catch(Exception)when(!cancellationToken.IsCancellationRequested)
                {
                    // Fail-soft per branch: one provider failure never discards the other branches' snippets.
                    results[index]=[];
                }
                finally{retrievalGate.Release();}
            }));
            return results.SelectMany(item=>item??[]).ToArray();
        }
        catch(Exception)when(!cancellationToken.IsCancellationRequested)
        {
            return [];
        }
    }

    private async Task<WideAnswerProposal> ComposeAnswerAsync(WideSearchRequest request,IReadOnlyCollection<WideBranchRecord> survivors,IReadOnlyCollection<PoloxiEvidenceDto> ranked,decimal confidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,WideQueryContract? queryContract,CancellationToken cancellationToken)
    {
        // V2.1: hard constraints from the query contract are non-negotiable in the final answer.
        var contractContext=queryContract is null||(queryContract.HardConstraints.Count==0&&string.IsNullOrWhiteSpace(queryContract.ExpectedAnswerType)&&string.IsNullOrWhiteSpace(queryContract.ExpectedAnswerGranularity))?string.Empty:
            $"\nEXPECTED ANSWER TYPE (a valid final answer must be of this type; evaluation criteria and dimensions are never final answers): {queryContract.ExpectedAnswerType??"(unspecified)"}\nEXPECTED ANSWER GRANULARITY (a valid final answer must sit at this semantic level; do not answer with a brand when a model is required or vice versa): {queryContract.ExpectedAnswerGranularity??"(unspecified)"}\nHARD CONSTRAINTS (every named item in the answer MUST satisfy these; exclude any item that does not): {(queryContract.HardConstraints.Count==0?"(none)":string.Join("; ",queryContract.HardConstraints))}";
        // Input budget: the tenant AI safety guard (Intelligence.Safety.MaximumInputCharacters) blocks
        // prompts over the configured limit. The answer system prompt is large and the survivor/evidence
        // sections grow with depth, so every variable section is clamped and, if the assembled user prompt
        // still exceeds the budget, the sections are progressively shrunk instead of failing the search.
        const int userPromptBudget=12000;
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
            userPrompt=$"Question: {request.Query}{contractContext}\nOverall confidence: {confidence:P0}\nSurviving disambiguation paths:\n{paths}\nNumbered interpretive narrowing paths ({topInterpretiveBranches.Length} paths - return {topInterpretiveBranches.Length} interpretiveResults entries):\n{(string.IsNullOrEmpty(topInterpretive)?"(none)":topInterpretive)}\nEnterprise evidence:\n{grounding}\nExternal evidence snippets (live web, current figures - use these for TIME_SENSITIVE paths):\n{externalGrounding}";
            if(userPrompt.Length<=userPromptBudget)break;
            // Shrink in evidence-preserving order: snippet length, snippet count, path list, then interpretive paths.
            if(snippetLength>400){snippetLength=400;continue;}
            if(snippetCount>4){snippetCount=4;continue;}
            if(pathCount>12){pathCount=12;continue;}
            if(evidenceCount>6){evidenceCount=6;continue;}
            if(interpretiveCount>4){interpretiveCount=4;continue;}
            break;
        }
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_ANSWER",
            "Compose the final answer of a progressive disambiguation pipeline. The surviving disambiguation paths, rankings, and confidence values supplied to you were computed deterministically by POLOXI and are AUTHORITATIVE: never reorder, replace, or silently substitute a different winner. If the leading result does not satisfy the expected answer type or a hard constraint from the query contract, say so explicitly and present the best VALID result instead, clearly labeled; when no valid candidate exists, state that the pipeline produced insufficient valid candidates rather than inventing one. Evaluation criteria and interpretation dimensions are never final answers. First judge each supplied enterprise evidence item: include its number in relevantEvidenceNumbers ONLY when the record genuinely answers or supports the question. Keyword search can match superficially (for example a name token matching an unrelated email address); such items are irrelevant and must be excluded. Statements supported by relevant evidence must cite evidence numbers in brackets. Reasoning not supported by evidence must be explicitly labeled as interpretation not verified against enterprise data. Set verificationCode to VERIFIED when the answer is fully evidence-backed, PARTIALLY_VERIFIED when mixed, INTERPRETIVE when no relevant evidence supports it. Suggested actions must be navigation suggestions only, using routes present in the evidence when available; never invent record identifiers. Additionally, for the supplied numbered interpretive narrowing paths, provide externalReferences: up to 6 real-world reference links from your knowledge that best answer the question along those paths. Each reference needs title, a well-known REAL absolute https URL (official sites, Wikipedia, or authoritative organizations only - never invent or guess deep links; prefer stable root/wiki pages you are certain exist), source (site or organization name), a one-sentence summary, and branchDisplayName set to the interpretive path it supports. If no trustworthy real-world reference exists, return an empty externalReferences array. Additionally provide interpretiveResults: the supplied interpretive narrowing paths are NUMBERED; you MUST return exactly one interpretiveResults entry for EVERY numbered path in the same order - if N numbered paths are supplied, return exactly N entries; never skip, merge, or summarize paths, and verify the entry count equals the path count before responding. For each path, directly answer that path's interpretation text using your own knowledge and return the actual, complete result set it asks for (for example, when the interpretation asks for a top 5 ranking, return all 5 ranked entries). Each interpretiveResults entry needs branchDisplayName set to the exact path display name, interpretation echoing the path interpretation text, and items: the complete ranked result set with rankNumber (1-based), name, and detail: a rich 2-3 sentence explanation covering WHY the item holds that rank, its most distinguishing attributes or specifications, and its main strength plus one notable trade-off or limitation compared to adjacent ranks. Each item name must be the MOST SPECIFIC individual entity the interpretation asks about - a concrete product model, title, or named instance (for example 'Predator P3 REVO', not 'Predator') - never just a brand, manufacturer, or category unless the interpretation explicitly asks for brands; when a brand is relevant, include it as part of the specific item name. This is interpretive knowledge, not enterprise data; never leave items empty when the interpretation asks for a ranked or enumerable result. Return an empty interpretiveResults array only when no interpretive paths are supplied. For each interpretiveResults entry also set dataVolatility: TIME_SENSITIVE when the result depends on current prices, interest rates, market rankings, availability, versions, or other facts that change over months; STABLE when the knowledge is durable. For TIME_SENSITIVE entries, unless external evidence snippets are supplied for that path, do NOT state specific prices, rates, percentages, model years, or numeric rankings from memory - instead describe the evaluation criteria, comparison factors, and where current figures can be verified. When external evidence snippets ARE supplied (the numbered E1..En list), you MUST extract and state the concrete figures from them: each item detail on an externally grounded TIME_SENSITIVE path must include the actual number the interpretation asks about (for example the MPG/MPGe rating, price in dollars, interest rate percentage, or ranking score) followed by the snippet citation in the form [E3]. Never replace available figures with vague qualifiers like 'great mileage' or 'excellent economy' - if a snippet states 57 MPG, write '57 MPG combined [E2]'. Only when the snippets genuinely contain no figure for a specific item may the detail fall back to criteria language, and it must then say the figure was not found in the retrieved sources. Finally provide candidateInsights: one entry per ranked candidate entity discussed in the answer, with candidateName echoing the candidate's name, bestFor (one short buyer-facing phrase describing what the candidate is genuinely best for based on the supplied material, or null), praisedFor (up to 4 short recurring strength themes such as 'Performance' or 'Build quality' that the supplied evidence, snippets, or result-set details actually support), and watchOutFor (up to 4 short recurring complaint or limitation themes the supplied material actually supports, such as 'Battery life' or 'Fan noise'). These themes are GROUNDED summaries, never inventions: only include a theme when the supplied enterprise evidence, external snippets, or interpretive result details genuinely mention or support it, and never present a low ranking score as a product flaw. Return empty arrays and a null bestFor when nothing in the supplied material supports themes for a candidate; return an empty candidateInsights array when there are no ranked candidate entities. RANKING LOCK: you do NOT decide the final ranking. The final ordered ranking of candidate entities is computed deterministically by the engine after your response and is authoritative. In the answer text NEVER output your own numbered or ordered ranking of candidate entities, never declare a #1/winner/best overall candidate, and never state that one candidate ranks above another; instead explain the evidence, criteria, and characteristics in prose. Interpretive result sets for the numbered narrowing paths are exempt: return their items as instructed.",
            userPrompt,
            AnswerSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_ANSWER",null,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
        return JsonSerializer.Deserialize<WideAnswerProposal>(result.Content,JsonOptions)??throw new ValidationException("The Wide answer response was empty.");
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

    private static WideAnswerContext? ComposeAnswerContext(string answerStatus,WideCandidateDto[] topCandidates,decimal? decisionConfidence,decimal? winnerStability,decimal decisionEvidenceCoverage,bool isIntentGap,IReadOnlyCollection<WideCandidateInsight>? candidateInsights=null,WideOutputContractResultDto? outputContract=null)
    {
        if(topCandidates.Length==0)return null;
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
        var winnerScores=winner.BranchScores
            .GroupBy(score=>HumanizeDimensionName(score.BranchDisplayName),StringComparer.OrdinalIgnoreCase)
            .Select(group=>new WideDimensionScoreDto(group.Key,group.Max(score=>score.EvidenceScore)))
            .OrderByDescending(score=>score.Score)
            .ToArray();
        var strengths=winnerScores.Take(3).ToArray();
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
            var ordered=candidate.BranchScores.OrderByDescending(score=>score.EvidenceScore).ToArray();
            var best=ordered.Length>0?ordered[0]:null;
            var worst=ordered.Length>1?ordered[^1]:null;
            var hasTradeOff=best is not null&&worst is not null&&best.EvidenceScore-worst.EvidenceScore>=.05m;
            // V2.9.1: grounded human-facing themes (from the answer LLM, constrained to supplied
            // evidence) take presentation priority over raw dimension chips; the dimension data is
            // kept as fallback and tooltip context. Matching is by candidate name, tolerant of the
            // LLM using a shorter or longer form of the same entity name.
            var insight=candidateInsights?.FirstOrDefault(item=>!string.IsNullOrWhiteSpace(item.CandidateName)
                &&(string.Equals(item.CandidateName.Trim(),candidate.DisplayName.Trim(),StringComparison.OrdinalIgnoreCase)
                ||candidate.DisplayName.Contains(item.CandidateName.Trim(),StringComparison.OrdinalIgnoreCase)
                ||item.CandidateName.Contains(candidate.DisplayName.Trim(),StringComparison.OrdinalIgnoreCase)));
            return new WideCandidateSummaryDto(candidate.DisplayName,candidate.CompositeScore,
                best is null?null:HumanizeDimensionName(best.BranchDisplayName),
                hasTradeOff?HumanizeDimensionName(worst!.BranchDisplayName):null)
            {BestForScore=best?.EvidenceScore,TradeOffScore=hasTradeOff?worst!.EvidenceScore:null,
             BestFor=string.IsNullOrWhiteSpace(insight?.BestFor)?null:insight!.BestFor!.Trim(),
             PraisedFor=insight?.PraisedFor?.Where(theme=>!string.IsNullOrWhiteSpace(theme)).Select(theme=>theme.Trim()).Take(4).ToArray()??[],
             WatchOutFor=insight?.WatchOutFor?.Where(theme=>!string.IsNullOrWhiteSpace(theme)).Select(theme=>theme.Trim()).Take(4).ToArray()??[],
             SupportTierCode=candidate.SupportTierCode};
        }).ToArray();
        // Winner-vs-alternative contrasts: dimensions each side leads on, from the same score matrix.
        var contrasts=topCandidates.Skip(1).Take(3).Select(alternative=>
        {
            var winnerLeads=new List<string>();var alternativeLeads=new List<string>();
            foreach(var winnerScore in winner.BranchScores)
            {
                var alternativeScore=alternative.BranchScores.FirstOrDefault(score=>string.Equals(score.BranchDisplayName,winnerScore.BranchDisplayName,StringComparison.OrdinalIgnoreCase));
                if(alternativeScore is null)continue;
                if(winnerScore.EvidenceScore>alternativeScore.EvidenceScore+.02m)winnerLeads.Add(winnerScore.BranchDisplayName);
                else if(alternativeScore.EvidenceScore>winnerScore.EvidenceScore+.02m)alternativeLeads.Add(winnerScore.BranchDisplayName);
            }
            return new WideCandidateContrastDto(alternative.DisplayName,alternative.CompositeScore,winnerLeads,alternativeLeads);
        }).ToArray();
        // Changeable dimensions: highest cross-candidate separation — reweighting these could flip
        // the ranking, so they become the personalization/"could change if" chips. Intent-gap runs
        // skip this: personalization applies to decision uncertainty, not identity ambiguity.
        var changeable=isIntentGap?[]:topCandidates.Take(4)
            .SelectMany(candidate=>candidate.BranchScores)
            .GroupBy(score=>score.BranchDisplayName,StringComparer.OrdinalIgnoreCase)
            .Where(group=>group.Count()>1)
            .Select(group=>(Dimension:group.Key,Separation:group.Max(score=>score.EvidenceScore)-group.Min(score=>score.EvidenceScore)))
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
                var bestAdvantage=challenger.BranchScores
                    .Select(challengerScore=>(ChallengerScore:challengerScore,WinnerScore:winner.BranchScores.FirstOrDefault(score=>string.Equals(score.BranchDisplayName,challengerScore.BranchDisplayName,StringComparison.OrdinalIgnoreCase))))
                    .Where(pair=>pair.WinnerScore is not null)
                    .Select(pair=>(pair.ChallengerScore,pair.WinnerScore,Advantage:pair.ChallengerScore.EvidenceScore-pair.WinnerScore!.EvidenceScore))
                    .OrderByDescending(pair=>pair.Advantage)
                    .FirstOrDefault();
                if(bestAdvantage.ChallengerScore is null||bestAdvantage.Advantage<=0)continue;
                rankingChangeDriver=new(HumanizeDimensionName(bestAdvantage.ChallengerScore.BranchDisplayName),challenger.DisplayName,Math.Clamp(winner.CompositeScore-challenger.CompositeScore,0,1))
                {WinnerScore=bestAdvantage.WinnerScore!.EvidenceScore,ChallengerScore=bestAdvantage.ChallengerScore.EvidenceScore};
                break;
            }
        }
        return new(responseMode,confidenceLabel,confidenceNarrative)
        {
            WinnerDisplayName=responseMode==WideResponseModes.ClarificationRequired?null:winner.DisplayName,
            WinnerStrengths=strengths,WinnerWeaknesses=weaknesses,
            CandidateSummaries=summaries,CandidateContrasts=contrasts,ChangeableDimensions=changeable,
            OutputContract=outputContract,RankingChangeDriver=rankingChangeDriver,
        };
    }

    // V2.8.6 post-competition identity dedup: two ranked candidates whose CORE tokens are identical
    // ("Overland Park, Kansas" / "Overland Park") are one entity and must hold one ranking position.
    // The stronger instance (better rank; richer name on ties) survives; scores are never altered.
    private static IReadOnlyCollection<WideCandidateDto> DeduplicateCandidatesByCanonicalTokens(IReadOnlyCollection<WideCandidateDto> candidates)
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
            seen[key]=survivors.Count;
            survivors.Add(candidate);
        }
        return survivors.Select((candidate,index)=>candidate with{RankNumber=index+1}).ToArray();
    }

    private static IReadOnlyCollection<WideCandidateDto> ReweightCandidatesByClarificationAnswer(IReadOnlyCollection<WideCandidateDto> candidates,string answer)
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
            return(Candidate:candidate,Score:Math.Clamp(candidate.CompositeScore*(1m+.35m*overlap),0,1));
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

    // Accept only well-formed absolute https URLs so hallucinated or unsafe links never reach the UI.
    private static WideExternalReferenceDto[] MapExternalReferences(WideAnswerProposal answer)=>
        (answer.ExternalReferences??[]).Where(reference=>Uri.TryCreate(reference.Url,UriKind.Absolute,out var uri)&&uri.Scheme==Uri.UriSchemeHttps)
            .Take(6).Select(reference=>new WideExternalReferenceDto(reference.Title.Trim(),reference.Url.Trim(),reference.Source.Trim(),reference.Summary.Trim(),reference.BranchDisplayName.Trim())).ToArray();

    // Interpretive result sets answered by the LLM for the interpretive narrowing paths, arranged with
    // Level 1 branches first, then by interpretive scoring (branch confidence, highest first).
    private static WideInterpretiveResultDto[] MapInterpretiveResults(WideAnswerProposal answer,IReadOnlyCollection<WideBranchRecord> survivors,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge)
    {
        var interpretive=survivors.Where(branch=>branch.GroundingStatusCode=="INTERPRETIVE"||branch.BranchStateCode==WideBranchStates.Dormant).GroupBy(branch=>branch.DisplayName.Trim(),StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key,group=>(Level:group.Min(branch=>branch.LevelNumber),Confidence:group.Max(branch=>branch.Confidence),StateCode:group.OrderByDescending(branch=>branch.Confidence).First().BranchStateCode),StringComparer.OrdinalIgnoreCase);
        // The answer LLM may echo a slightly different display name than the stored branch name; without a
        // tolerant lookup every card silently falls back to the single shared answer confidence, which makes
        // all interpretive scores identical. Exact match first, then containment either way.
        (int Level,decimal Confidence,string StateCode)? Resolve(string displayName)
        {
            if(interpretive.TryGetValue(displayName,out var exact))return exact;
            var partial=interpretive.FirstOrDefault(entry=>entry.Key.Contains(displayName,StringComparison.OrdinalIgnoreCase)||displayName.Contains(entry.Key,StringComparison.OrdinalIgnoreCase));
            return partial.Key is null?null:partial.Value;
        }
        var externallyGrounded=externalKnowledge.Count>0;
        return (answer.InterpretiveResults??[]).Where(result=>result.Items is{Count:>0})
            .Select(result=>new WideInterpretiveResultDto(result.BranchDisplayName.Trim(),result.Interpretation.Trim(),Resolve(result.BranchDisplayName.Trim())?.Confidence??Math.Clamp(answer.Confidence,0,1),result.Items.OrderBy(item=>item.RankNumber).Select((item,index)=>new WideInterpretiveResultItemDto(item.RankNumber>0?item.RankNumber:index+1,item.Name.Trim(),item.Detail.Trim())).ToArray()){DataVolatility=result.DataVolatility?.Trim().ToUpperInvariant()=="TIME_SENSITIVE"?"TIME_SENSITIVE":"STABLE",IsExternallyGrounded=externallyGrounded,BranchStateCode=Resolve(result.BranchDisplayName.Trim())?.StateCode??WideBranchStates.Active,LevelNumber=Resolve(result.BranchDisplayName.Trim())?.Level??0})
            .OrderBy(result=>Resolve(result.BranchDisplayName)?.Level??int.MaxValue)
            .ThenByDescending(result=>result.Confidence).ToArray();
    }

    private static string BuildCatalog(IReadOnlyCollection<PoloxiCapabilityDto> capabilities)=>capabilities.Count==0?"(none — knowledge-only mode; capabilityCode must always be null)":string.Join('\n',capabilities.Select(capability=>$"{capability.CapabilityCode}: {capability.Description}; approved terms: {string.Join(", ",capability.ApprovedTerms)}; entity: {capability.EntityTypeCode}"));

    // -----------------------------------------------------------------------------------------------
    // V2.1 Query Contract Engine: separate what the query FIXES (hard constraints, entity type, output
    // requirements) from what actually needs disambiguation (ambiguous concepts). Fail-soft: any LLM
    // failure returns null and the pipeline degrades to V2 whole-query branching.
    // -----------------------------------------------------------------------------------------------
    private async Task<WideQueryContract?> ExtractQueryContractAsync(WideSearchRequest request,CancellationToken cancellationToken)
    {
        try
        {
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_INTENT",
                "You extract a Query Contract from the user's question. Your purpose is to preserve explicit user intent before any interpretive branching begins. Separate what the query FIXES from what is genuinely ambiguous. entityType: the kind of thing the question concerns (for example City, Policy, Product) or null. expectedAnswerType: the semantic type a valid FINAL ANSWER must satisfy — what is actually allowed to win (for example 'Which U.S. company...' -> COMPANY or PUBLICLY_TRADED_COMPANY; 'What city...' -> CITY; 'Who discovered...' -> PERSON_OR_GROUP; 'Which policy...' -> POLICY; 'What caused...' -> CAUSE; 'Which strategy...' -> STRATEGY; 'Which coverage...' -> INSURANCE_COVERAGE) or null. IMPORTANT: expectedAnswerType describes what is allowed to become a final answer candidate; evaluation criteria, interpretations, dimensions, methodologies, attributes, evidence categories, and reasoning concepts are NOT final answer candidates unless the user explicitly asks for one. expectedAnswerGranularity: the SEMANTIC LEVEL a valid final answer must sit at — candidate granularity must match decision granularity. Examples: 'best-selling pool cue' or 'what pool cue should I buy' -> PRODUCT type with MODEL granularity (a concrete named model, never a brand); 'best pool cue brand' or 'who makes the best pool cues' -> BRAND type with BRAND granularity (a brand or manufacturer, never an individual model); 'best drug for X' may be ACTIVE_COMPOUND or BRAND granularity depending on phrasing; 'which insurance carrier' -> CARRIER granularity versus 'which coverage' -> COVERAGE granularity. Use a short uppercase token such as MODEL, BRAND, MANUFACTURER, COMPANY, CITY, PERSON, CASE, DOCTRINE, CARRIER, PRODUCT_LINE, COVERAGE; return null only when the question genuinely does not fix a level. geographicConstraint: an explicit geographic scope stated in the query (for example 'Southern California') or null. requestedCount: an explicit result count (for example 10 from 'top 10') or null. rankingConcept: the ranking or decision objective — what the user wants optimized, compared, selected, explained, predicted, or resolved (for example 'best') — or null. hardConstraints: every explicit non-negotiable filter stated in the query (geography, time period, category, price bounds); these are FIXED user intent, never interpretations. Name references: 'called X' or 'named X' means the entity is COMMONLY KNOWN AS X — brand names, common names, and legal names with corporate suffixes (X Technologies Inc., X Systems) all satisfy it; phrase such constraints as 'commonly known as X', NEVER as 'name is exactly X'. outputRequirements: explicit output shape requirements (top N, ranked list, comparison). ambiguousConcepts: ONLY the genuinely ambiguous evaluative or vague concepts whose meaning materially affects the answer (for example 'best', 'in trouble'); never include hard constraints here. Do not invent constraints not present in the question. Do not resolve ambiguity here. Return empty arrays when nothing applies.",
                $"Question: {request.Query}",
                QueryContractSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_QUERY_CONTRACT",null,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
            var proposal=JsonSerializer.Deserialize<WideQueryContractProposal>(result.Content,JsonOptions);
            if(proposal is null)return null;
            return new(proposal.EntityType,proposal.ExpectedAnswerType,proposal.ExpectedAnswerGranularity,proposal.GeographicConstraint,proposal.RequestedCount,proposal.RankingConcept,proposal.HardConstraints??[],proposal.AmbiguousConcepts??[],proposal.OutputRequirements??[]);
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

    // V2.3 basis selection: competing ALTERNATIVE branches use branch entropy; a dimension-dominated
    // hierarchy (fewer than 2 competing alternatives) switches to candidate-competition entropy.
    // V2.5 regression fix: ranking/recommendation queries (the contract fixes a rankingConcept or a
    // requestedCount) are ALWAYS a "which candidate wins" problem — their root branches are
    // complementary dimensions even when the proposer mislabels them ALTERNATIVE — so the CANDIDATE
    // basis takes precedence whenever a competitive candidate universe exists.
    // Before any candidates are known, uncertainty is reported as maximal on the CANDIDATE basis so
    // information rounds keep investigating instead of falsely reporting resolution.
    private static WideEntropyResult ComputeUncertainty(IReadOnlyCollection<WideBranchRecord> branches,IReadOnlyCollection<string> candidateNames,IReadOnlyCollection<PoloxiEvidenceDto> evidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge,WideQueryContract? queryContract)
    {
        var isRankingQuery=queryContract is not null&&(!string.IsNullOrWhiteSpace(queryContract.RankingConcept)||queryContract.RequestedCount is >1);
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
            var raw=evidence.Where(item=>Mentions(item.Title,candidate)||Mentions(item.Excerpt,candidate)).Sum(item=>item.RelevanceScore)
                +knowledge.Where(item=>Mentions(item.Title,candidate)||Mentions(item.Snippet,candidate)).Sum(item=>item.Score);
            signals[candidate]=Math.Round(raw/(1m+raw),4);
        }
        return signals;
        static bool Mentions(string? text,string candidate)=>text?.Contains(candidate,StringComparison.OrdinalIgnoreCase)==true;
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

    // V2.5 Independent Evidence Diversity: number of DISTINCT source hosts whose title or snippet
    // mentions the candidate. Deterministic and zero-LLM. One article claiming a candidate excels
    // across many dimensions is weaker support than independent sources agreeing.
    // V2.6.1 audit fix: LLM candidate names are often qualified ("Raleigh, North Carolina") while
    // snippets say "Raleigh" — a verbatim full-name match made distinctHosts=0 for nearly every
    // candidate, so evidence confidence collapsed to the single-source floor (a de-facto 70%
    // default). Match on the primary name (text before a comma/parenthesis/dash qualifier) too,
    // provided it is long enough to avoid trivial substring hits.
    private static int CountDistinctSourceHosts(string candidate,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        var primary=candidate.Split(',','(','\u2013','\u2014')[0].Trim();
        var keys=primary.Length>=4&&!string.Equals(primary,candidate,StringComparison.OrdinalIgnoreCase)
            ?new[]{candidate,primary}:[candidate];
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

    // V2.4 Early Candidate Harvest: deterministic, zero-LLM extraction of candidate names from external
    // result sets. A candidate is a capitalized proper-noun phrase (1-3 words) that appears in at least
    // two DISTINCT snippets (cross-source repetition filters out one-off article words). Common leading
    // sentence words and generic terms are excluded via a small stopword set.
    private static readonly HashSet<string> HarvestStopwords=new(StringComparer.OrdinalIgnoreCase)
    {
        "The","A","An","This","That","These","Those","It","Its","In","On","At","Of","For","From","With","And","Or","But","As","By","To","Is","Are","Was","Were","Be","Best","Top","New","Most","More","How","What","Why","When","Where","Which","Who","US","USA","United","States","America","American","City","Cities","State","County","Guide","List","Ranking","Rankings","Report","Study","Index","Overview","According","Based","Living","Life","Cost","Quality","Family","Families","Home","Homes","Housing","Job","Jobs","School","Schools","Education","Safety","Healthcare","January","February","March","April","May","June","July","August","September","October","November","December"
    };

    private static IReadOnlyCollection<string> HarvestCandidateNames(IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        if(knowledge.Count<2)return [];
        var occurrences=new Dictionary<string,HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        var index=0;
        foreach(var snippet in knowledge)
        {
            index++;
            foreach(var phrase in ExtractProperPhrases($"{snippet.Title}. {snippet.Snippet}"))
            {
                if(!occurrences.TryGetValue(phrase,out var set))occurrences[phrase]=set=[];
                set.Add(index);
            }
        }
        // Cross-source repetition: a real candidate is named by at least two distinct snippets.
        return occurrences.Where(entry=>entry.Value.Count>=2)
            .OrderByDescending(entry=>entry.Value.Count)
            .Take(24)
            .Select(entry=>entry.Key)
            .ToArray();
    }

    private static IEnumerable<string> ExtractProperPhrases(string text)
    {
        var tokens=text.Split([' ','\t','\n','\r'],StringSplitOptions.RemoveEmptyEntries);
        var current=new List<string>();
        foreach(var raw in tokens)
        {
            var word=raw.Trim('.',',',';',':','!','?','(',')','[',']','"','\'','’','“','”','—','-','·');
            var isProper=word.Length>1&&char.IsUpper(word[0])&&word.Skip(1).All(c=>char.IsLetter(c)&&char.IsLower(c));
            if(isProper&&!HarvestStopwords.Contains(word))
            {
                current.Add(word);
                if(current.Count==3){yield return string.Join(' ',current);current.Clear();}
            }
            else
            {
                if(current.Count>0)yield return string.Join(' ',current);
                current.Clear();
            }
        }
        if(current.Count>0)yield return string.Join(' ',current);
    }

    // One BATCHED call estimates information value for all eligible branches, including falsifiable
    // candidate ranking-change predictions POLOXI can later verify. Fail-soft: returns null on any failure.
    private async Task<WideInformationValueProposal?> EstimateInformationValueAsync(WideSearchRequest request,IReadOnlyCollection<WideBranchRecord> eligible,WideEntropyResult entropy,WideQueryContract? queryContract,CancellationToken cancellationToken)
    {
        try
        {
            var branchContext=string.Join('\n',eligible.Select(branch=>$"- branchCode: {branch.BranchCode} | name: {branch.DisplayName} | interpretation: {Truncate(branch.Interpretation,200)} | state: {branch.BranchStateCode} | poloxiConfidence: {branch.PoloxiConfidence:F2} | evidenceSupport: {branch.EvidenceSupport:F2} | evidenceCount: {branch.EvidenceCount}"));
            var contractContext=queryContract is null?"(none)":$"entityType: {queryContract.EntityType}; expectedAnswerType: {queryContract.ExpectedAnswerType}; expectedAnswerGranularity: {queryContract.ExpectedAnswerGranularity}; ranking: {queryContract.RankingConcept}; hard constraints: {string.Join("; ",queryContract.HardConstraints)}";
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_INFORMATION_VALUE",
                "You are the POLOXI Information Value estimator. Your task is NOT to determine the answer. Your task is to predict which unresolved branch is most valuable to investigate NEXT. This is a PREDICTION of usefulness, never a measurement. For EVERY listed branch return: uncertainty (how unresolved this dimension is), rankingImpact (how likely new evidence changes the final answer ranking), candidateDiscrimination (how well evidence here separates currently close candidates), evidenceAvailability (how likely useful evidence can realistically be retrieved from the approved routes), novelty (how different from evidence already retrieved), redundancy (overlap with evidence already retrieved). Allowed values for all six: VERY_LOW, LOW, MEDIUM, HIGH, VERY_HIGH — no other values. Important principle: HIGH UNCERTAINTY alone does NOT imply high information value. A branch has high information value only when resolving it could materially change the current winner, the ordering of leading candidates, elimination of a serious candidate, confidence in a deciding dimension, or the stop/convergence decision. Candidate discrimination is especially important: if evidence would affect all candidates similarly, discrimination is LOW even when the topic itself is important. Redundant investigations must receive sharply reduced value. evidenceTarget: one concrete sentence describing exactly what evidence to retrieve for this branch. rationale: one sentence why. predictedRankingChanges: which current candidates are most likely to move up or down if this branch is investigated — candidate (exact name), direction (UP or DOWN), magnitude (NONE, LOW, MEDIUM, or HIGH). Make these predictions falsifiable and specific (GOOD: 'If Alphabet's normalized valuation is materially lower than Microsoft's while forward growth remains comparable, Alphabet could overtake Microsoft.' BAD: 'More information could change the result.'); return an empty array when no candidate movement is expected. Preserve semantic identity across stages: a DIMENSION is not an ANSWER CANDIDATE, an INTERPRETATION is not EVIDENCE, EVIDENCE is not a candidate, and an ANSWER CANDIDATE must satisfy the expectedAnswerType from the Query Contract. Never calculate Shannon entropy or information gain — POLOXI computes those deterministically. Never fabricate evidence. Return every branch exactly once.",
                $"Question: {request.Query}\nQuery contract: {contractContext}\nCurrent normalized uncertainty (0=resolved, 1=maximal): {entropy.NormalizedEntropy:F2}\nBranches:\n{branchContext}",
                InformationValueSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_INFORMATION_VALUE",null,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
            return JsonSerializer.Deserialize<WideInformationValueProposal>(result.Content,JsonOptions);
        }
        catch(Exception)when(!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
    // never invented by the LLM. Enterprise evidence dominates; matched external snippets contribute
    // with their provider relevance score; unsupported branches score 0.
    private static decimal ComputeEvidenceSupport(WideBranchRecord branch,IReadOnlyCollection<PoloxiEvidenceDto> evidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge)
    {
        var enterpriseCount=evidence.Count(item=>item.HierarchyBranchId==branch.WideBranchId);
        // Saturating enterprise contribution: 1 item = .5, 3+ items ~ .9.
        var enterpriseSupport=enterpriseCount==0?0m:Math.Min(.9m,.5m+.2m*(enterpriseCount-1));
        // External snippets match a branch when the retrieval query included the branch display name.
        var matchedSnippets=externalKnowledge.Where(snippet=>snippet.Query.Contains(branch.DisplayName,StringComparison.OrdinalIgnoreCase)).ToArray();
        var externalSupport=matchedSnippets.Length==0?0m:Math.Clamp(matchedSnippets.Max(snippet=>snippet.Score),0,1)*Math.Min(1m,.6m+.1m*matchedSnippets.Length);
        return Math.Clamp(Math.Max(enterpriseSupport,externalSupport),0,1);
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
        "various","several","many","some","etc","numerous","additional","remaining","alternative","alternatives"
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
        // V2.8.1: reject attribute-qualified hypotheses — dimensions masquerading as entities.
        if(IsAttributeHypothesis(trimmed))return false;
        // Must look like a proper noun: first alphabetic character uppercase.
        var firstLetter=trimmed.FirstOrDefault(char.IsLetter);
        return firstLetter!=default&&char.IsUpper(firstLetter);
    }

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
        var core=name.Split('(')[0].Split(',')[0].Trim();
        return core.Split(' ',StringSplitOptions.RemoveEmptyEntries)
            .Where(token=>!CanonicalNoiseSuffixes.Contains(token,StringComparer.OrdinalIgnoreCase))
            .ToArray();
    }

    private static bool IsTokenPrefix(string[] shorter,string[] longer)
    {
        if(shorter.Length==0||shorter.Length>longer.Length)return false;
        for(var index=0;index<shorter.Length;index++)
            if(!string.Equals(shorter[index],longer[index],StringComparison.OrdinalIgnoreCase))return false;
        return true;
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
                if(identicalTokens||(specificHosts.Count>0&&(itemHosts.Overlaps(existing.Hosts)||itemHosts.Count==0)))
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
        try
        {
            // V2.5: the final Candidate × Branch matrix must include EVERY primary (root-level,
            // non-pruned) DIMENSION branch POLOXI itself discovered — a top-level criterion like
            // "overall quality of life" must not silently drop out because deeper branches
            // out-scored it. Root dimensions are unioned with the top-confidence survivors.
            var topSurvivors=survivors.Where(branch=>branch.BranchStateCode is WideBranchStates.Active or WideBranchStates.Secondary).OrderByDescending(branch=>branch.PoloxiConfidence).Take(8);
            var rootDimensions=survivors.Where(branch=>branch.LevelNumber==1
                &&branch.SemanticTypeCode==WideBranchSemanticTypes.Dimension
                &&branch.BranchStateCode!=WideBranchStates.Pruned);
            var branches=topSurvivors.Concat(rootDimensions).DistinctBy(branch=>branch.WideBranchId).OrderByDescending(branch=>branch.PoloxiConfidence).Take(10).ToArray();
            if(branches.Length==0)return [];
            // V2.3 candidate admission: a candidate must appear in enough distinct interpretive
            // dimensions to compete for the OVERALL answer. Appearing in a single interpretive list
            // (for example an affordability-only ranking) is not cross-dimensional support; such
            // candidates are flagged as exclusions with a reason \u2014 kept visible, never silently dropped.
            var dimensionSupport=interpretiveResults
                .SelectMany(result=>result.Items.Select(item=>(result.BranchDisplayName,item.Name)))
                .Distinct(new CandidateDimensionComparer())
                .GroupBy(entry=>entry.Name,StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group=>group.Key,group=>group.Count(),StringComparer.OrdinalIgnoreCase);
            var requiredSupport=interpretiveResults.Count<=1?1:Math.Min(configuration.MinimumCandidateDimensionSupport,interpretiveResults.Count);
            // V2.5 cardinality: an explicit requested count ("top 10") widens both the scored candidate
            // harvest and the final ranking size so the answer can actually satisfy the query contract.
            var requestedCount=queryContract?.RequestedCount??0;
            var targetCount=Math.Max(configuration.MaximumCandidates,requestedCount);
            // V2.7 Candidate Discovery: the competition scores the FULL candidate universe, not just the
            // candidates the interpretive lists happened to name. Evidence-harvested candidates (strong
            // entities named by retrieved sources — e.g. a #1 city in a live ranking) are merged into the
            // scored pool after deterministic validity filtering. Category/placeholder phrases are
            // rejected everywhere — they are descriptions, not entities.
            var interpretiveCandidates=interpretiveResults.SelectMany(result=>result.Items.Select(item=>(item.Name,item.Detail))).Where(item=>IsValidCandidateName(item.Name)).GroupBy(item=>CandidateIdentityKey(item.Name),StringComparer.Ordinal).Select(group=>group.OrderByDescending(item=>item.Name.Length).First()).ToArray();
            // V2.9.5 Candidate Provenance & Admission Priority: the scored pool must be built by
            // branch importance × rank-within-branch, not by cross-branch occurrence frequency.
            // Previously the dominant interpretation's #1/#2 candidates (e.g. Sales Volume: GSSE,
            // GARSEN at 90% branch confidence) could be truncated out of the pool while secondary
            // branches' recurring names (expert-review brands) filled it. A high-priority branch's
            // strongly supported candidate must never disappear because secondary-branch candidates
            // occur more often. Provenance = (best originating branch confidence, best rank within
            // that branch); admission ordering follows provenance before the Take() truncation.
            var branchPriority=survivors.GroupBy(branch=>branch.DisplayName.Trim(),StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key,group=>group.Max(branch=>branch.PoloxiConfidence),StringComparer.OrdinalIgnoreCase);
            var candidateProvenance=new Dictionary<string,(decimal BranchConfidence,int RankInBranch)>(StringComparer.OrdinalIgnoreCase);
            foreach(var interpretiveResult in interpretiveResults)
            {
                var confidence=branchPriority.GetValueOrDefault(interpretiveResult.BranchDisplayName.Trim());
                var rank=0;
                foreach(var item in interpretiveResult.Items)
                {
                    rank++;
                    if(!candidateProvenance.TryGetValue(item.Name,out var existing)||confidence>existing.BranchConfidence||(confidence==existing.BranchConfidence&&rank<existing.RankInBranch))
                        candidateProvenance[item.Name]=(confidence,rank);
                }
            }
            (decimal BranchConfidence,int RankInBranch)ProvenanceOf(string name)=>candidateProvenance.TryGetValue(name,out var provenance)?provenance:(0m,int.MaxValue);
            var knownNames=interpretiveCandidates.Select(item=>item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            // Discovered candidates ranked by independent-source diversity so the strongest evidence-named
            // entities are merged first; annotated so their admission path is visible and explainable.
            var evidenceCandidates=discoveredCandidates
                .Where(name=>IsValidCandidateName(name)&&!knownNames.Contains(name))
                .Select(name=>(Name:name,Hosts:CountDistinctSourceHosts(name,externalKnowledge)))
                .Where(item=>item.Hosts>=minimumDiscoverySourceHosts)
                .OrderByDescending(item=>item.Hosts)
                .Take(targetCount)
                .Select(item=>(item.Name,Detail:(string?)$"Discovered from retrieved evidence ({item.Hosts} independent sources)."))
                .ToArray();
            var candidateNames=CanonicalizeCandidates(interpretiveCandidates.Concat(evidenceCandidates).ToArray(),externalKnowledge,dimensionSupport)
                .OrderByDescending(item=>ProvenanceOf(item.Name).BranchConfidence)
                .ThenBy(item=>ProvenanceOf(item.Name).RankInBranch)
                .Take(targetCount*2).ToArray();
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
                .SelectMany(result=>result.Items.Select(item=>(result.BranchDisplayName,item.Name)))
                .Distinct(new CandidateDimensionComparer())
                .GroupBy(entry=>entry.Name,StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group=>group.Key,group=>group.Count(),StringComparer.OrdinalIgnoreCase);
            if(candidateNames.Length==0)return [];
            var branchList=string.Join('\n',branches.Select((branch,index)=>$"B{index+1}. {branch.DisplayName}: {branch.Interpretation}"));
            var candidateList=string.Join('\n',candidateNames.Select((candidate,index)=>$"C{index+1}. {candidate.Name}: {candidate.Detail}"));
            // V2.9.4 Branch-Score Grounding: the scorer previously saw only branch names, so branch
            // scores drifted to LLM general knowledge and contradicted the supplied branch orderings.
            // Supply each branch's own ranked results so scoring is anchored to POLOXI's evidence.
            var branchResultList=interpretiveResults.Count==0?"(none)":string.Join('\n',interpretiveResults.Select(result=>$"{result.BranchDisplayName}: "+string.Join("; ",result.Items.Select((item,index)=>$"#{index+1} {item.Name}"))));
            var constraints=queryContract is null||queryContract.HardConstraints.Count==0?"(none)":string.Join("; ",queryContract.HardConstraints);
            var expectedAnswerType=queryContract?.ExpectedAnswerType is{Length:>0}answerType?answerType:"(unspecified)";
            var expectedAnswerGranularity=queryContract?.ExpectedAnswerGranularity is{Length:>0}granularity?granularity:"(unspecified)";
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_ANSWER",
				"You score ONLY the supplied candidates against the supplied interpretation branches. You do not generate new candidates. The Query Contract is authoritative. A candidate is eligible only when it satisfies the expected answer type AND the expected answer granularity AND ALL hard constraints. Candidate granularity must match decision granularity: when the granularity is MODEL, only concrete named product models are eligible and brands, manufacturers, or categories are violations (for 'best-selling pool cue': 'OKHEALING Carbon Fiber Pool Cue' and 'BenX 57 Inch Cue' are VALID while 'Predator', 'Viking', and 'Cuetec' as bare brands are INVALID); when the granularity is BRAND or MANUFACTURER, only brands/makers are eligible and individual models are violations ('Predator' VALID, 'OKHEALING Carbon Fiber Pool Cue' INVALID). For every candidate return: name (echo exactly), detail (echo or improve, one sentence), violatesConstraint=true with constraintViolationReason when the candidate does NOT satisfy the expected answer type, does NOT sit at the expected answer granularity, or does NOT satisfy ALL hard constraints (for example a city outside the required geography, or an evaluation criterion where a company is required); otherwise false with null reason. Eligibility is three-way, not binary: VALID means the candidate already sits at the required granularity; RESOLVABLE means the candidate is broader than required (a brand, series, or family) BUT the supplied evidence explicitly names a specific qualifying child entity - in that case set violatesConstraint=false and REWRITE the candidate name to the specific child named in the supplied evidence (for example 'Predator Revo Carbon Fiber Cues' resolves to 'Predator 9K-1' when the supplied evidence names that cue), noting the resolution in the detail; INVALID means the wrong entity type or a component ('Predator 314-3 Shaft' is a component, not a full cue) or a broader entity that NO supplied evidence resolves to a qualifying child - mark those violatesConstraint=true. Resolution is allowed ONLY from the supplied evidence, never from your general knowledge. Do not reinterpret an unresolvable invalid candidate into a valid one. Examples: when the expected answer type is PUBLICLY_TRADED_COMPANY, 'Microsoft Corporation', 'Alphabet Inc.', and 'Apple Inc.' are VALID while 'Broad Moat', 'Strategic Vision', 'Moderate Volatility', 'Scenario Analysis', and 'Management Quality' are INVALID; when the expected answer type is CITY, 'Iowa City', 'Knoxville', and 'Madison' are VALID while 'Affordability', 'Healthcare Access', and 'Tax Friendliness' are INVALID. A name constraint like 'called X' or 'commonly known as X' is satisfied when the entity is commonly known as X - brand names, common names, and legal names with corporate suffixes (X Technologies Inc., X Systems) all satisfy it; never mark such candidates as violations for not being exactly named X. Preserve semantic identity across stages: a DIMENSION is not an ANSWER CANDIDATE, an INTERPRETATION is not EVIDENCE, EVIDENCE is not a candidate, and an ANSWER CANDIDATE must satisfy the expected answer type and granularity from the Query Contract. branchScores: one entry per supplied branch with branchDisplayName echoed exactly and evidenceScore between 0 and 1 derived ONLY from the supplied branch results and evidence context for that branch - never from your general model knowledge. When a branch supplies an explicit ranked list, your scores for that branch MUST preserve the supplied ordering: a candidate ranked #7 in a branch cannot receive a higher score than that branch's #1 unless other SUPPLIED evidence explicitly contradicts the ordering, in which case cite the contradiction in the candidate detail. Evidence absence must lower the score, not be replaced by inferred performance: when a candidate does not appear in a branch's supplied results and no supplied evidence covers it for that dimension, assign a LOW score (at or below 0.3) for that branch. Do not reward brand reputation, prestige, or assumed market position that is not present in the supplied branch results or evidence. Scores must be differentiated per candidate and branch; never assign identical scores across the board, and violatesConstraint must reflect hard eligibility, not merely a weak score. Composite ranking, branch weighting, entropy, and information gain are calculated deterministically outside the LLM.",
                $"Question: {request.Query}\nExpected answer type (only candidates of this type are eligible): {expectedAnswerType}\nExpected answer granularity (only candidates at this semantic level are eligible): {expectedAnswerGranularity}\nHard constraints: {constraints}\nInterpretation branches:\n{branchList}\nSupplied branch results (authoritative per-branch orderings; branch scores must preserve these orderings and treat absence as low evidence):\n{branchResultList}\nCandidates:\n{candidateList}",
                CandidateScoringSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_CANDIDATE_MATRIX",executionId,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
            var proposal=JsonSerializer.Deserialize<WideCandidateScoringProposal>(result.Content,JsonOptions);
            if(proposal?.Candidates is not{Count:>0})return [];
            var branchWeightTotal=branches.Sum(branch=>branch.PoloxiConfidence);
            if(branchWeightTotal<=0)branchWeightTotal=1;
            var branchesByName=branches.GroupBy(branch=>branch.DisplayName.Trim(),StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key,group=>group.First(),StringComparer.OrdinalIgnoreCase);
            // V2.8.2 Branch Identity Resolution: the prompt labels branches "B1. Name: Interpretation",
            // and models sometimes echo the label or append the interpretation. A strict dictionary miss
            // silently dropped the score — zeroing coverage and Quality for EVERY candidate while the
            // evidence was intact. Resolution now strips "B<n>." label prefixes and trailing ": ..."
            // decorations, then falls back to unambiguous containment before giving up.
            WideBranchRecord? ResolveBranch(string echoed)
            {
                var cleaned=echoed.Trim();
                var labelMatch=System.Text.RegularExpressions.Regex.Match(cleaned,@"^B\d+\.\s*");
                if(labelMatch.Success)cleaned=cleaned[labelMatch.Length..].Trim();
                var colon=cleaned.IndexOf(':');
                var withoutDetail=colon>0?cleaned[..colon].Trim():cleaned;
                if(branchesByName.TryGetValue(cleaned,out var branch))return branch;
                if(branchesByName.TryGetValue(withoutDetail,out branch))return branch;
                var contains=branches.Where(candidate=>cleaned.Contains(candidate.DisplayName,StringComparison.OrdinalIgnoreCase)||candidate.DisplayName.Contains(withoutDetail,StringComparison.OrdinalIgnoreCase)).ToArray();
                return contains.Length==1?contains[0]:null;
            }
            var entries=new List<(WideCandidateRecord Record,bool SupportExcluded,decimal RawComposite)>();
            var evidenceConfidences=new Dictionary<string,decimal>(StringComparer.OrdinalIgnoreCase);
            // V2.9.3 admission provenance per candidate: mode, interpretive-dimension support,
            // independent-host support, and the total support credited at admission time.
            var admissionInfo=new Dictionary<string,(string Mode,int Interpretive,int Hosts,int Total)>(StringComparer.OrdinalIgnoreCase);
            // V2.9.4 support tier per candidate (STRONG/MODERATE/LIMITED) for transparent disclosure.
            var supportTiers=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
            // V2.8.2 Candidate Identity Resolution on echo: candidates are prompted as "C<n>. Name:
            // Detail" — the echoed name may carry the label or detail. Resolve back to the SUPPLIED
            // canonical name so dimension-support admission and evidence lookups key on the same
            // identity the pipeline built, not on the model's echo formatting.
            string ResolveCandidateName(string echoed)
            {
                var cleaned=echoed.Trim();
                var labelMatch=System.Text.RegularExpressions.Regex.Match(cleaned,@"^C\d+\.\s*");
                if(labelMatch.Success)cleaned=cleaned[labelMatch.Length..].Trim();
                if(candidateNames.Any(item=>string.Equals(item.Name,cleaned,StringComparison.OrdinalIgnoreCase)))return cleaned;
                var colon=cleaned.IndexOf(':');
                var withoutDetail=colon>0?cleaned[..colon].Trim():cleaned;
                var supplied=candidateNames.FirstOrDefault(item=>string.Equals(item.Name,withoutDetail,StringComparison.OrdinalIgnoreCase));
                return supplied.Name??cleaned;
            }
            foreach(var candidate in proposal.Candidates)
            {
                var candidateId=Guid.NewGuid();
                var resolvedName=ResolveCandidateName(candidate.Name);
                var scores=new List<WideCandidateBranchScoreRecord>();
                var composite=0m;
                foreach(var score in candidate.BranchScores??[])
                {
                    var branch=ResolveBranch(score.BranchDisplayName);
                    if(branch is null||scores.Any(existing=>existing.WideBranchId==branch.WideBranchId))continue;
                    var clamped=Math.Clamp(score.EvidenceScore,0,1);
                    scores.Add(new(Guid.NewGuid(),candidateId,branch.WideBranchId,request.TenantId,branch.DisplayName,clamped));
                    composite+=branch.PoloxiConfidence/branchWeightTotal*clamped;
                }
                // V2.1 Candidate Evidence Coverage: a candidate scored on only a fraction of the surviving
                // dimensions must not compete equally with fully-covered candidates — missing data is not
                // strength. Coverage scales the composite so gaps pull the ranking down, never up.
                var coverage=branches.Length==0?0m:Math.Clamp((decimal)scores.Count/branches.Length,0,1);
                composite*=coverage;
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
                // V2.3 candidate admission, upgraded to V2.9.4 TIERED admission: the requested result
                // count is honored whenever enough plausible, evidence-backed candidates exist. Weaker-
                // but-valid candidates are admitted with a disclosed lower support tier instead of being
                // silently dropped. The rule is "never hide weaker evidence just to satisfy Top N" — not
                // "never admit it". Zero-support names are still excluded; nothing is ever invented.
                var support=dimensionSupport.GetValueOrDefault(resolvedName);
                var interpretiveCount=interpretiveSupport.GetValueOrDefault(resolvedName);
                var combinedSupport=interpretiveCount+distinctHosts;
                var admissionMode=support>=requiredSupport?"NORMAL":"RECOVERY";
                var supportTier=support>=requiredSupport?"STRONG"
                    :combinedSupport>=requiredSupport?"MODERATE"
                    :combinedSupport>=1?"LIMITED"
                    :null;
                var supportExcluded=false;
                if(!violates&&supportTier is null)
                {
                    supportExcluded=true;
                    violates=true;
                    violationReason=$"No credible support: appears in {interpretiveCount} of {interpretiveResults.Count} interpretation dimensions and {distinctHosts} independent evidence hosts.";
                }
                entries.Add((new(candidateId,executionId,request.TenantId,Truncate(resolvedName,300)!,Truncate(candidate.Detail?.Trim(),1000),violates?0m:Math.Clamp(composite,0,1),0,violates,Truncate(violationReason,400),scores),supportExcluded,Math.Clamp(composite,0,1)));
                evidenceConfidences[resolvedName]=evidenceConfidence;
                admissionInfo[resolvedName]=(supportTier is null?"EXCLUDED":admissionMode,interpretiveCount,distinctHosts,Math.Max(support,combinedSupport));
                supportTiers[resolvedName]=supportTier??"LIMITED";
            }
            // V2.9.4 rule: POLOXI honors the requested Top N whenever enough plausible, evidence-backed
            // candidates exist — weaker-but-valid candidates compete with a disclosed lower support
            // tier. Only zero-support names and constraint violators are excluded; POLOXI never invents
            // candidates and never hides evidence weakness to fill a count.
            var records=entries.Select(entry=>entry.Record).ToList();
            var ranked=records.OrderBy(record=>record.IsConstraintViolation).ThenByDescending(record=>record.CompositeScore).Take(targetCount).Select((record,index)=>record with{RankNumber=index+1}).ToArray();
            await wideRepository.SaveWideCandidatesAsync(ranked,request.UserId,cancellationToken);
            return ranked.Select(record=>{var admission=admissionInfo.GetValueOrDefault(record.DisplayName,("NORMAL",0,0,0));return new WideCandidateDto(record.WideCandidateId,record.RankNumber,record.DisplayName,record.IsConstraintViolation?$"Ruled out: {record.ConstraintViolationReason}":record.Detail,record.CompositeScore,record.BranchScores.Select(score=>new WideCandidateBranchScoreDto(score.BranchDisplayName,score.EvidenceScore)).ToArray()){EvidenceCoverage=branches.Length==0?0m:Math.Clamp((decimal)record.BranchScores.Count/branches.Length,0,1),IsConstraintViolation=record.IsConstraintViolation,QualityScore=record.CompositeScore,EvidenceConfidence=evidenceConfidences.GetValueOrDefault(record.DisplayName),AdmissionModeCode=admission.Item1,InterpretiveSupportCount=admission.Item2,EvidenceHostSupportCount=admission.Item3,TotalSupportCount=admission.Item4,SupportTierCode=supportTiers.GetValueOrDefault(record.DisplayName,"STRONG")};}).ToArray();
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
    "entityType": { "type": ["string", "null"] },
    "expectedAnswerType": { "type": ["string", "null"] },
    "expectedAnswerGranularity": { "type": ["string", "null"] },
    "geographicConstraint": { "type": ["string", "null"] },
    "requestedCount": { "type": ["integer", "null"] },
    "rankingConcept": { "type": ["string", "null"] },
    "hardConstraints": { "type": "array", "maxItems": 10, "items": { "type": "string" } },
    "ambiguousConcepts": { "type": "array", "maxItems": 6, "items": { "type": "string" } },
    "outputRequirements": { "type": "array", "maxItems": 6, "items": { "type": "string" } }
  },
  "required": ["entityType", "expectedAnswerType", "expectedAnswerGranularity", "geographicConstraint", "requestedCount", "rankingConcept", "hardConstraints", "ambiguousConcepts", "outputRequirements"],
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
        "required": ["name", "detail", "violatesConstraint", "constraintViolationReason", "branchScores"],
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
