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
                var groundingResults=new (string StatusCode,IReadOnlyCollection<EphEvidenceDto> Evidence,HashSet<string> Keys)[currentLevel.Length];
                using(var groundingGate=new SemaphoreSlim(Math.Max(1,configuration.GroundingConcurrency)))
                    await Task.WhenAll(currentLevel.Select(async(branch,index)=>
                    {
                        await groundingGate.WaitAsync(cancellationToken);
                        try{groundingResults[index]=await GroundBranchAsync(branch,ephRequest,capabilities,request.MaximumResults,branchEvidenceKeys,cancellationToken);}
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
            var ranked=RankEvidence(survivingEvidence,ephRequest,new(false,1,configuration.MinimumBranchConfidence,configuration.MaximumBranchesPerLevel*Math.Max(depth,1),request.MaximumResults));

            // Stage 3: verified answer composed from surviving paths + enterprise evidence.
            var survivorsFinal=allBranches.Where(branch=>!branch.IsEliminated).ToArray();
            if(aggregateConfidence==0m&&survivorsFinal.Length>0)aggregateConfidence=ComputeAggregateConfidence(survivorsFinal);
            // Live external grounding (fail-soft): retrieve fresh web snippets for interpretive paths so
            // time-sensitive figures come from current sources instead of stale model memory.
            var externalKnowledge=await GatherExternalKnowledgeAsync(request,survivorsFinal.Where(branch=>branch.GroundingStatusCode=="INTERPRETIVE").ToArray(),configuration.ExternalRetrievalConcurrency,cancellationToken);
            // V2.1 three-score model: Interpretation Prior (LLM), Evidence Support (deterministic, from
            // enterprise evidence and matched external snippets), EPH Confidence (weighted combination).
            survivorsFinal=survivorsFinal.Select(branch=>
            {
                var support=ComputeEvidenceSupport(branch,evidence,externalKnowledge);
                var ephConfidence=Math.Clamp(configuration.PriorWeight*branch.Confidence+configuration.EvidenceWeight*support,0,1);
                // V2.1 REWEIGHT: evidence revises the branch state — a DORMANT branch with strong evidence
                // support is reactivated, and a high-prior branch without support is demoted. PRUNED
                // (constraint violation / evidence-void) is terminal and never reactivated here.
                var state=branch.BranchStateCode==WideBranchStates.Pruned?WideBranchStates.Pruned
                    :ephConfidence>=configuration.SecondaryBranchThreshold?WideBranchStates.Active
                    :ephConfidence>=configuration.DormantBranchThreshold?WideBranchStates.Secondary
                    :WideBranchStates.Dormant;
                return branch with{InterpretationPrior=branch.Confidence,EvidenceSupport=support,EphConfidence=ephConfidence,BranchStateCode=state};
            }).ToArray();
            foreach(var branch in survivorsFinal)
                allBranches[allBranches.FindIndex(item=>item.WideBranchId==branch.WideBranchId)]=branch;

            // ── V2.2 Information-Directed Exploration ─────────────────────────────────
            // "Don't explore everything. Explore what will teach you the most."
            // Deterministic Shannon entropy decides WHETHER more information is needed; a single
            // batched LLM call ESTIMATES which branches are most valuable to investigate
            // (EstimatedInformationValue); EPH deterministically adjusts, selects, retrieves in
            // parallel, reweights, and MEASURES ActualInformationGain = EntropyBefore - EntropyAfter.
            // Fail-soft: any estimator/entropy failure skips the round and continues V2.1 behavior.
            var totalActualInformationGain=0m;
            var informationRounds=new List<WideInformationRoundDto>();
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
                        var maxConfidence=Math.Max(eligible.Max(branch=>branch.EphConfidence),.0001m);
                        // Candidate discrimination need is high when eligible branch scores are tightly packed.
                        var orderedConfidences=eligible.Select(branch=>branch.EphConfidence).OrderByDescending(value=>value).ToArray();
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
                            // Adjust with facts EPH already knows: evidence gap, branch importance, candidate closeness.
                            var evidenceGap=Math.Clamp(1m-branch.EvidenceSupport,0,1);
                            var branchImportance=Math.Clamp(branch.EphConfidence/maxConfidence,0,1);
                            var adjusted=Math.Clamp(
                                configuration.InformationValueLlmWeight*raw
                                +configuration.InformationValueEvidenceGapWeight*evidenceGap
                                +configuration.InformationValueBranchWeight*branchImportance
                                +configuration.InformationValueCandidateNeedWeight*candidateNeed,0,1);
                            // V2.5 Marginal Information Value: EPH already KNOWS whether this branch was
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
                            // the deterministic pre-retrieval baseline so EPH can verify them after the round.
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
                        // Normal EPH scoring/reweighting on the enriched evidence pool (never LLM-calculated).
                        survivorsFinal=survivorsFinal.Select(branch=>
                        {
                            var support=ComputeEvidenceSupport(branch,evidence,externalKnowledgeAll);
                            var ephConfidence=Math.Clamp(configuration.PriorWeight*branch.InterpretationPrior+configuration.EvidenceWeight*support,0,1);
                            var state=branch.BranchStateCode==WideBranchStates.Pruned?WideBranchStates.Pruned
                                :ephConfidence>=configuration.SecondaryBranchThreshold?WideBranchStates.Active
                                :ephConfidence>=configuration.DormantBranchThreshold?WideBranchStates.Secondary
                                :WideBranchStates.Dormant;
                            return branch with{EvidenceSupport=support,EphConfidence=ephConfidence,BranchStateCode=state};
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
                        // MagnitudeCorrect). Calibration data — the LLM predicted; EPH measured.
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
            var scorePersistTask=wideRepository.UpdateWideBranchScoresAsync(request.TenantId,survivorsFinal.Select(branch=>new WideBranchScoreUpdate(branch.WideBranchId,branch.BranchStateCode,branch.InterpretationPrior,branch.EvidenceSupport,branch.EphConfidence)).ToArray(),cancellationToken);
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
            // composite ranking weighted by branch EPH confidence. Fail-soft: empty on LLM failure.
            var interpretiveResults=MapInterpretiveResults(answer,survivorsFinal,externalKnowledge);
            IReadOnlyCollection<WideCandidateDto> candidates=[];
            if(interpretiveResults.Length>0&&llmCalls<configuration.MaximumTotalLlmCalls)
            {
                candidates=await CompeteCandidatesAsync(request,executionId,queryContract,survivorsFinal,interpretiveResults,externalKnowledgeAll,configuration,cancellationToken);
                if(candidates.Count>0)llmCalls++;
            }
            // V2.1 evidence metrics: coverage = share of surviving branches supported by any evidence.
            var coveredBranches=survivorsFinal.Count(branch=>branch.EvidenceSupport>0);
            var evidenceCoverage=survivorsFinal.Length==0?0m:Math.Clamp((decimal)coveredBranches/survivorsFinal.Length,0,1);
            // V2.5 Decision Evidence Coverage: measured only over the branches that participated in the
            // final Candidate × Branch competition — the dimensions the ANSWER actually rests on.
            var decisionBranchIds=candidates.SelectMany(candidate=>candidate.BranchScores.Select(score=>score.BranchDisplayName)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var decisionBranches=survivorsFinal.Where(branch=>decisionBranchIds.Contains(branch.DisplayName)).ToArray();
            var decisionEvidenceCoverage=decisionBranches.Length==0?evidenceCoverage:Math.Clamp((decimal)decisionBranches.Count(branch=>branch.EvidenceSupport>0)/decisionBranches.Length,0,1);
            await wideRepository.UpdateWideExecutionContractAsync(request.TenantId,request.UserId,executionId,queryContract is null?null:JsonSerializer.Serialize(queryContract,JsonOptions),evidenceCoverage,externalKnowledge.Count,relevantEvidence.Length,candidates.Count,cancellationToken);
            // V2.2: persist execution-level entropy summary and information-round counters (fail-soft).
            try{await wideRepository.UpdateWideExecutionEntropyAsync(request.TenantId,request.UserId,new(executionId,initialEntropy.Entropy,finalEntropy.Entropy,initialEntropy.NormalizedEntropy,finalEntropy.NormalizedEntropy,totalActualInformationGain,informationRounds.Count,informationTargetCount,informationRetrievalCount){EntropyBasisCode=finalEntropy.EntropyBasisCode},cancellationToken);}catch{/* diagnostics only; never blocks the answer */}
            timer.Stop();
            await wideRepository.CompleteWideExecutionAsync(request.TenantId,request.UserId,executionId,answerStatus,terminationReason,depth,llmCalls,aggregateConfidence,answer.VerificationCode,string.IsNullOrWhiteSpace(answer.Answer)?null:answer.Answer,timer.ElapsedMilliseconds,cancellationToken);
            return new(executionId,request.Query,answerStatus,terminationReason,depth,llmCalls,aggregateConfidence,answer.VerificationCode,string.IsNullOrWhiteSpace(answer.Answer)?null:answer.Answer,allBranches.Select(ToDto).ToArray(),relevantEvidence,answer.SuggestedActions.Select(action=>new WideActionSuggestionDto(action.DisplayName,action.NavigationRoute,action.Rationale)).ToArray(),timer.ElapsedMilliseconds){ExternalReferences=MapExternalReferences(answer),InterpretiveResults=interpretiveResults,ExternalKnowledge=externalKnowledge,QueryContract=queryContract,
            Candidates=candidates,EvidenceCoverage=evidenceCoverage,DecisionEvidenceCoverage=decisionEvidenceCoverage,ExternalEvidenceCount=externalKnowledge.Count,EnterpriseEvidenceCount=relevantEvidence.Length,
            InitialEntropy=initialEntropy.Entropy,FinalEntropy=finalEntropy.Entropy,InitialNormalizedEntropy=initialEntropy.NormalizedEntropy,FinalNormalizedEntropy=finalEntropy.NormalizedEntropy,TotalActualInformationGain=totalActualInformationGain,EntropyBasisCode=finalEntropy.EntropyBasisCode,InformationRounds=informationRounds};
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
    // Pure grounding: reads parent evidence keys but never mutates shared state, so branches within a
    // level can be grounded concurrently. The caller merges results sequentially in branch order.
    private async Task<(string StatusCode,IReadOnlyCollection<EphEvidenceDto> Evidence,HashSet<string> Keys)> GroundBranchAsync(WideBranchRecord branch,EphSearchRequest ephRequest,IReadOnlyCollection<EphCapabilityDto> capabilities,int maximumResults,Dictionary<Guid,HashSet<string>> branchEvidenceKeys,CancellationToken cancellationToken)
    {
        var capability=branch.CapabilityCode is null?null:capabilities.FirstOrDefault(item=>item.CapabilityCode.Equals(branch.CapabilityCode,StringComparison.OrdinalIgnoreCase)&&item.ExecutionHandlerCode.Equals("AUTHORIZED_SEARCH_DOCUMENT",StringComparison.OrdinalIgnoreCase));
        if(capability is null)return("INTERPRETIVE",[],new(StringComparer.OrdinalIgnoreCase));
        var searchText=NormalizeEphSearchText(branch.SearchText??branch.DisplayName,capability);
        var ephBranch=new EphBranchRecord(branch.WideBranchId,branch.ParentWideBranchId,branch.BranchCode,branch.DisplayName,branch.Interpretation,capability.CapabilityCode,"VALID","Wide dynamic grounding.",searchText,capability.SupportsRecency,branch.Confidence,branch.SortOrder);
        var branchEvidence=await repository.ExecuteEphBranchAsync(ephRequest,ephBranch,capability,maximumResults,cancellationToken);
        if(branch.ParentWideBranchId is{}parentId&&branchEvidenceKeys.TryGetValue(parentId,out var parentKeys)&&parentKeys.Any(key=>key.StartsWith($"{capability.EntityTypeCode}:",StringComparison.OrdinalIgnoreCase)))
            branchEvidence=branchEvidence.Where(item=>parentKeys.Contains($"{item.EntityTypeCode}:{item.EntityId:D}")).ToArray();
        var keys=branchEvidence.Select(item=>$"{item.EntityTypeCode}:{item.EntityId:D}").ToHashSet(StringComparer.OrdinalIgnoreCase);
        return("GROUNDED",branchEvidence,keys);
    }

    private async Task<WideIntentProposal> ProposeIntentAsync(WideSearchRequest request,IReadOnlyCollection<EphCapabilityDto> capabilities,WideConfiguration configuration,WideQueryContract? queryContract,CancellationToken cancellationToken)
    {
        var catalog=BuildCatalog(capabilities);
        // V2.1: when a query contract exists, the LLM branches ONLY the ambiguous concepts; hard
        // constraints and output requirements are fixed by the user and must never be reinterpreted.
        var contractContext=queryContract is null?string.Empty:
            $"\nQuery contract (FIXED, do not reinterpret): entity type: {queryContract.EntityType??"(unspecified)"}; hard constraints: {(queryContract.HardConstraints.Count==0?"(none)":string.Join("; ",queryContract.HardConstraints))}; output requirements: {(queryContract.OutputRequirements.Count==0?"(none)":string.Join("; ",queryContract.OutputRequirements))}\nAmbiguous concepts to disambiguate (branch ONLY these): {(queryContract.AmbiguousConcepts.Count==0?"(whole question)":string.Join("; ",queryContract.AmbiguousConcepts))}";
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_INTENT",
            "You disambiguate an ambiguous enterprise question by dynamically constructing a problem-specific hierarchy. Propose the top level: distinct interpretation branches of the question. Branches are NOT limited to the supplied capability catalog - general, industry, and conceptual interpretations are allowed. Map capabilityCode only when the catalog can genuinely ground the branch against enterprise data; otherwise use null. For each branch set continueNarrowing=true when a meaningfully narrower sub-level exists, otherwise false with a stopReason of FULLY_DISAMBIGUATED, NO_FURTHER_RELEVANT_SUBDIVISION, EVIDENCE_SUFFICIENT, or INTERPRETATION_EXHAUSTED. Confidence per branch must be CALIBRATED, not defaulted: it expresses how likely this interpretation matches what the user actually meant, so branches must be differentiated - the most plausible mainstream interpretation scores highest and niche or speculative interpretations score lower. Never assign the same confidence to every branch and never use 1.0; interpretive branches without enterprise grounding are capped at 0.9. For each branch also set semanticType using this strict test: could TWO sibling branches BOTH be true/relevant to the final answer at the same time? If yes, they are DIMENSION (jointly valid evaluation criteria - for example quality of life AND affordability AND jobs AND education for a best-city question; there does not need to be a winner among them). Only when selecting one branch makes its siblings incorrect interpretations of the same unknown (for example an incoming document is a claim OR renewal OR endorsement OR cancellation) are they ALTERNATIVE. When in doubt for ranking, comparison, or best-of questions, prefer DIMENSION. Never claim records exist and never produce SQL.",
            $"Ambiguous question: {request.Query}{contractContext}\nMaximum branches: {configuration.MaximumBranchesPerLevel}\nApproved capability catalog (for optional grounding):\n{catalog}",
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
            "Continue a dynamic problem-specific disambiguation hierarchy. For each surviving parent branch, propose narrower child branches that progressively move toward a more specific subset of the parent interpretation, informed by the parent's enterprise grounding outcome (evidence counts and samples supplied). Set parentBranchCode to the exact parent branchCode. Children of grounded parents should stay in the same entity type so evidence can be intersected. Branches are not limited to the capability catalog; map capabilityCode only when the catalog genuinely grounds the child, otherwise null. Set continueNarrowing=false with a stopReason when no meaningfully narrower relevant subdivision remains - do not invent depth for its own sake. Confidence per child must be CALIBRATED, not defaulted: it expresses how likely this narrower interpretation matches the user's actual intent given the parent, so siblings must be differentiated - the most plausible subdivision scores highest and speculative ones score lower. A child may not exceed its parent's confidence, never assign the same confidence to every sibling, and never use 1.0; interpretive branches without enterprise grounding are capped at 0.9. For each child also set semanticType using this strict test: could TWO sibling children BOTH be true/relevant to the final answer at the same time? If yes, they are DIMENSION (jointly valid evaluation criteria such as affordability, safety, healthcare, or quality of life - there does not need to be a winner among them). Only when selecting one child makes its siblings incorrect interpretations of the same unknown are they ALTERNATIVE. When in doubt for ranking, comparison, or best-of questions, prefer DIMENSION. Never claim records exist and never produce SQL.",
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

    private async Task<WideAnswerProposal> ComposeAnswerAsync(WideSearchRequest request,IReadOnlyCollection<WideBranchRecord> survivors,IReadOnlyCollection<EphEvidenceDto> ranked,decimal confidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,WideQueryContract? queryContract,CancellationToken cancellationToken)
    {
        // V2.1: hard constraints from the query contract are non-negotiable in the final answer.
        var contractContext=queryContract is null||queryContract.HardConstraints.Count==0?string.Empty:
            $"\nHARD CONSTRAINTS (every named item in the answer MUST satisfy these; exclude any item that does not): {string.Join("; ",queryContract.HardConstraints)}";
        // Input budget: the tenant AI safety guard (Intelligence.Safety.MaximumInputCharacters) blocks
        // prompts over the configured limit. The answer system prompt is large and the survivor/evidence
        // sections grow with depth, so every variable section is clamped and, if the assembled user prompt
        // still exceeds the budget, the sections are progressively shrunk instead of failing the search.
        const int userPromptBudget=12000;
        var orderedSurvivors=survivors.OrderBy(branch=>branch.LevelNumber).ThenBy(branch=>branch.SortOrder).ToArray();
        // All interpretive narrowing paths (Level 1 first, then highest confidence) drive real-world reference
        // and interpretive result-set generation; the branch sub-header (Interpretation) is fed to the LLM.
        var allInterpretiveBranches=survivors.Where(branch=>branch.GroundingStatusCode=="INTERPRETIVE").OrderBy(branch=>branch.LevelNumber).ThenByDescending(branch=>branch.Confidence).ToArray();
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
            "Compose the final answer of a progressive disambiguation pipeline. First judge each supplied enterprise evidence item: include its number in relevantEvidenceNumbers ONLY when the record genuinely answers or supports the question. Keyword search can match superficially (for example a name token matching an unrelated email address); such items are irrelevant and must be excluded. Statements supported by relevant evidence must cite evidence numbers in brackets. Reasoning not supported by evidence must be explicitly labeled as interpretation not verified against enterprise data. Set verificationCode to VERIFIED when the answer is fully evidence-backed, PARTIALLY_VERIFIED when mixed, INTERPRETIVE when no relevant evidence supports it. Suggested actions must be navigation suggestions only, using routes present in the evidence when available; never invent record identifiers. Additionally, for the supplied numbered interpretive narrowing paths, provide externalReferences: up to 6 real-world reference links from your knowledge that best answer the question along those paths. Each reference needs title, a well-known REAL absolute https URL (official sites, Wikipedia, or authoritative organizations only - never invent or guess deep links; prefer stable root/wiki pages you are certain exist), source (site or organization name), a one-sentence summary, and branchDisplayName set to the interpretive path it supports. If no trustworthy real-world reference exists, return an empty externalReferences array. Additionally provide interpretiveResults: the supplied interpretive narrowing paths are NUMBERED; you MUST return exactly one interpretiveResults entry for EVERY numbered path in the same order - if N numbered paths are supplied, return exactly N entries; never skip, merge, or summarize paths, and verify the entry count equals the path count before responding. For each path, directly answer that path's interpretation text using your own knowledge and return the actual, complete result set it asks for (for example, when the interpretation asks for a top 5 ranking, return all 5 ranked entries). Each interpretiveResults entry needs branchDisplayName set to the exact path display name, interpretation echoing the path interpretation text, and items: the complete ranked result set with rankNumber (1-based), name, and a one-sentence detail explaining why it holds that rank. Each item name must be the MOST SPECIFIC individual entity the interpretation asks about - a concrete product model, title, or named instance (for example 'Predator P3 REVO', not 'Predator') - never just a brand, manufacturer, or category unless the interpretation explicitly asks for brands; when a brand is relevant, include it as part of the specific item name. This is interpretive knowledge, not enterprise data; never leave items empty when the interpretation asks for a ranked or enumerable result. Return an empty interpretiveResults array only when no interpretive paths are supplied. For each interpretiveResults entry also set dataVolatility: TIME_SENSITIVE when the result depends on current prices, interest rates, market rankings, availability, versions, or other facts that change over months; STABLE when the knowledge is durable. For TIME_SENSITIVE entries, unless external evidence snippets are supplied for that path, do NOT state specific prices, rates, percentages, model years, or numeric rankings from memory - instead describe the evaluation criteria, comparison factors, and where current figures can be verified. When external evidence snippets ARE supplied (the numbered E1..En list), you MUST extract and state the concrete figures from them: each item detail on an externally grounded TIME_SENSITIVE path must include the actual number the interpretation asks about (for example the MPG/MPGe rating, price in dollars, interest rate percentage, or ranking score) followed by the snippet citation in the form [E3]. Never replace available figures with vague qualifiers like 'great mileage' or 'excellent economy' - if a snippet states 57 MPG, write '57 MPG combined [E2]'. Only when the snippets genuinely contain no figure for a specific item may the detail fall back to criteria language, and it must then say the figure was not found in the retrieved sources.",
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
            // Clamp LLM free text to EPH.WideBranch column lengths (DB is the source of truth).
            // V2.3: semantic type defaults to ALTERNATIVE (backward compatible) unless the LLM
            // explicitly classified the branch as a DIMENSION (jointly valid evaluation criterion).
            var semanticType=string.Equals(branch.SemanticType?.Trim(),WideBranchSemanticTypes.Dimension,StringComparison.OrdinalIgnoreCase)?WideBranchSemanticTypes.Dimension:WideBranchSemanticTypes.Alternative;
            return new WideBranchRecord(Guid.NewGuid(),executionId,parentId,tenantId,levelNumber,Truncate(NormalizeCode(branch.BranchCode),120),Truncate(branch.DisplayName.Trim(),300),Truncate(branch.Interpretation.Trim(),1000),Truncate(branch.CapabilityCode?.Trim(),100),Truncate(branch.SearchText?.Trim(),400),"PENDING",0,confidence,branch.ContinueNarrowing,Truncate(branch.StopReason?.Trim(),50),false,null,index+1){SemanticTypeCode=semanticType};
        }).ToArray();

    private static string? Truncate(string? value,int maximumLength)=>value is null||value.Length<=maximumLength?value:value[..maximumLength];

    private static decimal ComputeAggregateConfidence(IReadOnlyCollection<WideBranchRecord> survivors)
    {
        if(survivors.Count==0)return 0m;
        // Evidence-weighted: grounded branches with evidence pull confidence up; interpretive branches contribute their raw confidence.
        var weighted=survivors.Select(branch=>branch.GroundingStatusCode=="GROUNDED"&&branch.EvidenceCount>0?Math.Clamp(branch.Confidence+.15m,0,1):branch.Confidence);
        return Math.Clamp(weighted.Max(),0,1);
    }

    private static WideBranchDto ToDto(WideBranchRecord branch)=>new(branch.WideBranchId,branch.ParentWideBranchId,branch.LevelNumber,branch.BranchCode,branch.DisplayName,branch.Interpretation,branch.CapabilityCode,branch.SearchText,branch.GroundingStatusCode,branch.EvidenceCount,branch.Confidence,branch.ContinueNarrowing,branch.StopReason,branch.IsEliminated,branch.EliminationReason,branch.SortOrder){BranchStateCode=branch.BranchStateCode,InterpretationPrior=branch.InterpretationPrior,EvidenceSupport=branch.EvidenceSupport,EphConfidence=branch.EphConfidence,SemanticTypeCode=branch.SemanticTypeCode};

    // Accept only well-formed absolute https URLs so hallucinated or unsafe links never reach the UI.
    private static WideExternalReferenceDto[] MapExternalReferences(WideAnswerProposal answer)=>
        (answer.ExternalReferences??[]).Where(reference=>Uri.TryCreate(reference.Url,UriKind.Absolute,out var uri)&&uri.Scheme==Uri.UriSchemeHttps)
            .Take(6).Select(reference=>new WideExternalReferenceDto(reference.Title.Trim(),reference.Url.Trim(),reference.Source.Trim(),reference.Summary.Trim(),reference.BranchDisplayName.Trim())).ToArray();

    // Interpretive result sets answered by the LLM for the interpretive narrowing paths, arranged with
    // Level 1 branches first, then by interpretive scoring (branch confidence, highest first).
    private static WideInterpretiveResultDto[] MapInterpretiveResults(WideAnswerProposal answer,IReadOnlyCollection<WideBranchRecord> survivors,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge)
    {
        var interpretive=survivors.Where(branch=>branch.GroundingStatusCode=="INTERPRETIVE").GroupBy(branch=>branch.DisplayName.Trim(),StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key,group=>(Level:group.Min(branch=>branch.LevelNumber),Confidence:group.Max(branch=>branch.Confidence)),StringComparer.OrdinalIgnoreCase);
        // The answer LLM may echo a slightly different display name than the stored branch name; without a
        // tolerant lookup every card silently falls back to the single shared answer confidence, which makes
        // all interpretive scores identical. Exact match first, then containment either way.
        (int Level,decimal Confidence)? Resolve(string displayName)
        {
            if(interpretive.TryGetValue(displayName,out var exact))return exact;
            var partial=interpretive.FirstOrDefault(entry=>entry.Key.Contains(displayName,StringComparison.OrdinalIgnoreCase)||displayName.Contains(entry.Key,StringComparison.OrdinalIgnoreCase));
            return partial.Key is null?null:partial.Value;
        }
        var externallyGrounded=externalKnowledge.Count>0;
        return (answer.InterpretiveResults??[]).Where(result=>result.Items is{Count:>0})
            .Select(result=>new WideInterpretiveResultDto(result.BranchDisplayName.Trim(),result.Interpretation.Trim(),Resolve(result.BranchDisplayName.Trim())?.Confidence??Math.Clamp(answer.Confidence,0,1),result.Items.OrderBy(item=>item.RankNumber).Select((item,index)=>new WideInterpretiveResultItemDto(item.RankNumber>0?item.RankNumber:index+1,item.Name.Trim(),item.Detail.Trim())).ToArray()){DataVolatility=result.DataVolatility?.Trim().ToUpperInvariant()=="TIME_SENSITIVE"?"TIME_SENSITIVE":"STABLE",IsExternallyGrounded=externallyGrounded})
            .OrderBy(result=>Resolve(result.BranchDisplayName)?.Level??int.MaxValue)
            .ThenByDescending(result=>result.Confidence).ToArray();
    }

    private static string BuildCatalog(IReadOnlyCollection<EphCapabilityDto> capabilities)=>capabilities.Count==0?"(none — knowledge-only mode; capabilityCode must always be null)":string.Join('\n',capabilities.Select(capability=>$"{capability.CapabilityCode}: {capability.Description}; approved terms: {string.Join(", ",capability.ApprovedTerms)}; entity: {capability.EntityTypeCode}"));

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
                "Extract a query contract from the user's question. Separate what the query FIXES from what is genuinely ambiguous. entityType: the kind of thing being asked about (for example City, Policy, Product) or null. geographicConstraint: an explicit geographic scope stated in the query (for example 'Southern California') or null. requestedCount: an explicit result count (for example 10 from 'top 10') or null. rankingConcept: the evaluative word being ranked on (for example 'best') or null. hardConstraints: every explicit non-negotiable filter stated in the query (geography, time period, category, price bounds); these are FIXED user intent, never interpretations. outputRequirements: explicit output shape requirements (top N, ranked list, comparison). ambiguousConcepts: ONLY the genuinely ambiguous evaluative or vague concepts that need interpretation (for example 'best', 'in trouble'); never include hard constraints here. Return empty arrays when nothing applies.",
                $"Question: {request.Query}",
                QueryContractSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_QUERY_CONTRACT",null,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
            var proposal=JsonSerializer.Deserialize<WideQueryContractProposal>(result.Content,JsonOptions);
            if(proposal is null)return null;
            return new(proposal.EntityType,proposal.GeographicConstraint,proposal.RequestedCount,proposal.RankingConcept,proposal.HardConstraints??[],proposal.AmbiguousConcepts??[],proposal.OutputRequirements??[]);
        }
        catch(Exception)when(!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }

    // -----------------------------------------------------------------------------------------------
    // V2.2 Information-Directed Exploration helpers.
    // The LLM proposes. Evidence informs. EPH decides — entropy and information gain are always
    // calculated deterministically in EPH code, never by the LLM.
    // -----------------------------------------------------------------------------------------------

    // Deterministic Shannon entropy over the eligible (ACTIVE/SECONDARY) ALTERNATIVE branch belief
    // distribution. V2.3: DIMENSION branches are jointly valid criteria, NOT competing hypotheses —
    // they never participate in winner-take-all entropy. PRUNED and DORMANT never inflate entropy.
    private static WideEntropyResult ComputeEntropy(IReadOnlyCollection<WideBranchRecord> branches)
    {
        var eligible=branches.Where(branch=>branch.BranchStateCode is WideBranchStates.Active or WideBranchStates.Secondary
                &&branch.SemanticTypeCode==WideBranchSemanticTypes.Alternative)
            .Select(branch=>Math.Max(branch.EphConfidence,.0001m)).ToArray();
        return EntropyFromValues(eligible,WideEntropyBases.Branch);
    }

    // V2.3 candidate-signal entropy: when the hierarchy is dimension-dominated, uncertainty means
    // "which candidate wins", so entropy is measured over the deterministic candidate-signal
    // distribution (mention-weighted evidence support), never over complementary dimensions.
    private static WideEntropyResult ComputeCandidateEntropy(IReadOnlyCollection<string> candidateNames,IReadOnlyCollection<EphEvidenceDto> evidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
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
    private static WideEntropyResult ComputeUncertainty(IReadOnlyCollection<WideBranchRecord> branches,IReadOnlyCollection<string> candidateNames,IReadOnlyCollection<EphEvidenceDto> evidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge,WideQueryContract? queryContract)
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
    private static Dictionary<string,decimal> ComputeCandidateSignals(IReadOnlyCollection<string> candidates,IReadOnlyCollection<EphEvidenceDto> evidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
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
    private static int CountDistinctSourceHosts(string candidate,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        var hosts=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var snippet in knowledge)
        {
            if(snippet.Title?.Contains(candidate,StringComparison.OrdinalIgnoreCase)!=true
                &&snippet.Snippet?.Contains(candidate,StringComparison.OrdinalIgnoreCase)!=true)continue;
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
    // candidate ranking-change predictions EPH can later verify. Fail-soft: returns null on any failure.
    private async Task<WideInformationValueProposal?> EstimateInformationValueAsync(WideSearchRequest request,IReadOnlyCollection<WideBranchRecord> eligible,WideEntropyResult entropy,WideQueryContract? queryContract,CancellationToken cancellationToken)
    {
        try
        {
            var branchContext=string.Join('\n',eligible.Select(branch=>$"- branchCode: {branch.BranchCode} | name: {branch.DisplayName} | interpretation: {Truncate(branch.Interpretation,200)} | state: {branch.BranchStateCode} | ephConfidence: {branch.EphConfidence:F2} | evidenceSupport: {branch.EvidenceSupport:F2} | evidenceCount: {branch.EvidenceCount}"));
            var contractContext=queryContract is null?"(none)":$"entityType: {queryContract.EntityType}; ranking: {queryContract.RankingConcept}; hard constraints: {string.Join("; ",queryContract.HardConstraints)}";
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_INFORMATION_VALUE",
                "You are the EPH Information Value estimator. For EVERY listed branch, assess how valuable investigating it next is likely to be. This is a PREDICTION of usefulness, never a measurement. For each branch return: uncertainty (how unresolved this dimension is), rankingImpact (how likely new evidence changes the final answer ranking), candidateDiscrimination (how well evidence here separates currently close candidates), evidenceAvailability (how likely useful public evidence exists), novelty (how different from evidence already retrieved), redundancy (overlap with evidence already retrieved). Allowed values for all six: VERY_LOW, LOW, MEDIUM, HIGH, VERY_HIGH — no other values. evidenceTarget: one concrete sentence describing exactly what evidence to retrieve for this branch. rationale: one sentence why. predictedRankingChanges: which current candidates are most likely to move up or down if this branch is investigated — candidate (exact name), direction (UP or DOWN), magnitude (NONE, LOW, MEDIUM, or HIGH). Make these predictions falsifiable and specific; return an empty array when no candidate movement is expected. Return every branch exactly once.",
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
    private static decimal ComputeEvidenceSupport(WideBranchRecord branch,IReadOnlyCollection<EphEvidenceDto> evidence,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge)
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
    private async Task<IReadOnlyCollection<WideCandidateDto>> CompeteCandidatesAsync(WideSearchRequest request,Guid executionId,WideQueryContract? queryContract,IReadOnlyCollection<WideBranchRecord> survivors,IReadOnlyCollection<WideInterpretiveResultDto> interpretiveResults,IReadOnlyCollection<WideExternalKnowledgeSnippet> externalKnowledge,WideConfiguration configuration,CancellationToken cancellationToken)
    {
        try
        {
            // V2.5: the final Candidate × Branch matrix must include EVERY primary (root-level,
            // non-pruned) DIMENSION branch EPH itself discovered — a top-level criterion like
            // "overall quality of life" must not silently drop out because deeper branches
            // out-scored it. Root dimensions are unioned with the top-confidence survivors.
            var topSurvivors=survivors.Where(branch=>branch.BranchStateCode is WideBranchStates.Active or WideBranchStates.Secondary).OrderByDescending(branch=>branch.EphConfidence).Take(8);
            var rootDimensions=survivors.Where(branch=>branch.LevelNumber==1
                &&branch.SemanticTypeCode==WideBranchSemanticTypes.Dimension
                &&branch.BranchStateCode!=WideBranchStates.Pruned);
            var branches=topSurvivors.Concat(rootDimensions).DistinctBy(branch=>branch.WideBranchId).OrderByDescending(branch=>branch.EphConfidence).Take(10).ToArray();
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
            var candidateNames=interpretiveResults.SelectMany(result=>result.Items.Select(item=>(item.Name,item.Detail))).GroupBy(item=>item.Name,StringComparer.OrdinalIgnoreCase).Select(group=>group.First()).Take(targetCount*2).ToArray();
            if(candidateNames.Length==0)return [];
            var branchList=string.Join('\n',branches.Select((branch,index)=>$"B{index+1}. {branch.DisplayName}: {branch.Interpretation}"));
            var candidateList=string.Join('\n',candidateNames.Select((candidate,index)=>$"C{index+1}. {candidate.Name}: {candidate.Detail}"));
            var constraints=queryContract is null||queryContract.HardConstraints.Count==0?"(none)":string.Join("; ",queryContract.HardConstraints);
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_ANSWER",
                "Score each supplied candidate against each supplied interpretation branch. For every candidate return: name (echo exactly), detail (echo or improve, one sentence), violatesConstraint=true with constraintViolationReason when the candidate does NOT satisfy ALL hard constraints (for example a city outside the required geography); otherwise false with null reason. branchScores: one entry per supplied branch with branchDisplayName echoed exactly and evidenceScore between 0 and 1 expressing how strongly that candidate performs on that interpretation dimension based on your knowledge. Scores must be differentiated per candidate and branch; never assign identical scores across the board.",
                $"Question: {request.Query}\nHard constraints: {constraints}\nInterpretation branches:\n{branchList}\nCandidates:\n{candidateList}",
                CandidateScoringSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_CANDIDATE_MATRIX",executionId,request.CorrelationId,"Intelligent Search Wide"),cancellationToken);
            var proposal=JsonSerializer.Deserialize<WideCandidateScoringProposal>(result.Content,JsonOptions);
            if(proposal?.Candidates is not{Count:>0})return [];
            var branchWeightTotal=branches.Sum(branch=>branch.EphConfidence);
            if(branchWeightTotal<=0)branchWeightTotal=1;
            var branchesByName=branches.GroupBy(branch=>branch.DisplayName.Trim(),StringComparer.OrdinalIgnoreCase).ToDictionary(group=>group.Key,group=>group.First(),StringComparer.OrdinalIgnoreCase);
            var entries=new List<(WideCandidateRecord Record,bool SupportExcluded,decimal RawComposite)>();
            foreach(var candidate in proposal.Candidates)
            {
                var candidateId=Guid.NewGuid();
                var scores=new List<WideCandidateBranchScoreRecord>();
                var composite=0m;
                foreach(var score in candidate.BranchScores??[])
                {
                    if(!branchesByName.TryGetValue(score.BranchDisplayName.Trim(),out var branch))continue;
                    var clamped=Math.Clamp(score.EvidenceScore,0,1);
                    scores.Add(new(Guid.NewGuid(),candidateId,branch.WideBranchId,request.TenantId,branch.DisplayName,clamped));
                    composite+=branch.EphConfidence/branchWeightTotal*clamped;
                }
                // V2.1 Candidate Evidence Coverage: a candidate scored on only a fraction of the surviving
                // dimensions must not compete equally with fully-covered candidates — missing data is not
                // strength. Coverage scales the composite so gaps pull the ranking down, never up.
                var coverage=branches.Length==0?0m:Math.Clamp((decimal)scores.Count/branches.Length,0,1);
                composite*=coverage;
                // V2.5 Independent Evidence Diversity: appearing in four dimensions supported by four
                // independent sources must beat four claims recycled from one article. The composite is
                // mildly scaled by how many DISTINCT source hosts mention the candidate — a single-source
                // candidate keeps 70% of its score; each additional independent host recovers the rest.
                var distinctHosts=CountDistinctSourceHosts(candidate.Name,externalKnowledge);
                var diversityFactor=distinctHosts<=1?.70m:Math.Min(1m,.70m+.15m*(distinctHosts-1));
                composite*=diversityFactor;
                // Constraint Engine: violators score 0 and carry the reason; they remain visible as PRUNED.
                var violates=candidate.ViolatesConstraint;
                var violationReason=candidate.ConstraintViolationReason?.Trim();
                // V2.3 candidate admission: insufficient cross-dimensional support is treated as a
                // constraint-style exclusion so single-dimension list appearances (for example a
                // cheapest-places ranking) cannot win the overall competition.
                var support=dimensionSupport.GetValueOrDefault(candidate.Name.Trim());
                var supportExcluded=false;
                if(!violates&&support<requiredSupport)
                {
                    supportExcluded=true;
                    violates=true;
                    violationReason=$"Insufficient cross-dimensional support: appears in {support} of {interpretiveResults.Count} interpretation dimensions (minimum {requiredSupport}).";
                }
                entries.Add((new(candidateId,executionId,request.TenantId,Truncate(candidate.Name.Trim(),300)!,Truncate(candidate.Detail?.Trim(),1000),violates?0m:Math.Clamp(composite,0,1),0,violates,Truncate(violationReason,400),scores),supportExcluded,Math.Clamp(composite,0,1)));
            }
            // V2.5 cardinality rule: when the query explicitly requests N results and fewer than N
            // candidates were admitted, re-admit the strongest support-excluded candidates (NEVER hard
            // constraint violators) to fill the shortfall — visibly annotated, never silent.
            if(requestedCount>0)
            {
                var shortfall=requestedCount-entries.Count(entry=>!entry.Record.IsConstraintViolation);
                if(shortfall>0)
                    for(var index=0;index<entries.Count;index++)
                    {
                        if(shortfall<=0)break;
                        var entry=entries[index];
                        if(!entry.SupportExcluded)continue;
                        entries[index]=(entry.Record with{CompositeScore=entry.RawComposite,IsConstraintViolation=false,ConstraintViolationReason=null,Detail=Truncate($"{entry.Record.Detail} (Re-admitted to satisfy the requested count of {requestedCount}; limited cross-dimensional support.)",1000)},false,entry.RawComposite);
                        shortfall--;
                    }
            }
            var records=entries.Select(entry=>entry.Record).ToList();
            var ranked=records.OrderBy(record=>record.IsConstraintViolation).ThenByDescending(record=>record.CompositeScore).Take(targetCount).Select((record,index)=>record with{RankNumber=index+1}).ToArray();
            await wideRepository.SaveWideCandidatesAsync(ranked,request.UserId,cancellationToken);
            return ranked.Select(record=>new WideCandidateDto(record.WideCandidateId,record.RankNumber,record.DisplayName,record.IsConstraintViolation?$"Ruled out: {record.ConstraintViolationReason}":record.Detail,record.CompositeScore,record.BranchScores.Select(score=>new WideCandidateBranchScoreDto(score.BranchDisplayName,score.EvidenceScore)).ToArray()){EvidenceCoverage=branches.Length==0?0m:Math.Clamp((decimal)record.BranchScores.Count/branches.Length,0,1),IsConstraintViolation=record.IsConstraintViolation}).ToArray();
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
    "geographicConstraint": { "type": ["string", "null"] },
    "requestedCount": { "type": ["integer", "null"] },
    "rankingConcept": { "type": ["string", "null"] },
    "hardConstraints": { "type": "array", "maxItems": 10, "items": { "type": "string" } },
    "ambiguousConcepts": { "type": "array", "maxItems": 6, "items": { "type": "string" } },
    "outputRequirements": { "type": "array", "maxItems": 6, "items": { "type": "string" } }
  },
  "required": ["entityType", "geographicConstraint", "requestedCount", "rankingConcept", "hardConstraints", "ambiguousConcepts", "outputRequirements"],
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
    }
  },
  "required": ["answer", "verificationCode", "confidence", "relevantEvidenceNumbers", "externalReferences", "suggestedActions", "interpretiveResults"],
  "additionalProperties": false
}
""";
}
