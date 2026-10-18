using System.Text;
using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Microsoft.Extensions.Logging;

namespace Legal.Application;

// 0436 matter-funneled Domain Pack L2 -> L3 (accuracy-first, hybrid):
//   1. Deterministic matter relevance per configured L2 (matter type, DB relevance keywords, name tokens,
//      PRIORITIZE rules, direct document classification, mapped passages) -> PriorityScore + RelevanceReasons.
//   2. Optional LLM relevance review over the FIXED catalog, run in dimension-grouped batches (0440) so no
//      catalog row is truncated out of the prompt. Each line carries the DB linkage metadata (Description,
//      MatterTypeCodes, RelevanceKeywords). Every returned code is validated against the Domain Pack Evidence
//      Type / Concept catalog (the materialized L2 set); unknown codes are dropped.
//      Output only blends into PriorityScore/TierCode - never adds, removes, or renames a catalog row.
//   3. Bounded-worker passage -> L2 mapping computed ONCE per run (top-N per parent, persisted) so L3 batches
//      never rescan the corpus. Lexical/keyword/evidence-type methods only; embeddings remain optional.
//   4. Factor Inventory slice per parent (matter values, availability, contradictions, missing items).
//   Tier A/B changes CONTEXT DENSITY and batch size only. Every in-scope L2 still receives an L3 model pass.
public sealed partial class IntelligenceWide2Service
{
    private sealed record PassageMatch(LegalEvidenceGraphItemDto Passage,string Method,decimal Score);

    private readonly Dictionary<Guid,PassageMatch[]> _passageMapByBranch=new();
    private readonly Dictionary<Guid,string> _factorSliceByBranch=new();
    private readonly Dictionary<Guid,decimal> _relevanceByBranch=new();
    // 0441: raw halves behind the blended PriorityScore so the per-matter scope table can show both.
    private readonly Dictionary<Guid,(decimal Deterministic,decimal? Llm,string? LlmReason)> _relevanceDetailByBranch=new();

    private void ResetMatterFunnelState(){_passageMapByBranch.Clear();_factorSliceByBranch.Clear();_relevanceByBranch.Clear();_relevanceDetailByBranch.Clear();}

    // ── Stage 1 + 2 + 3: run once before L3 batching ──
    private async Task<int> PrepareMatterFunnelAsync(WideSearchRequest request,Guid executionId,WideBranchRecord[] parents,WideConfiguration configuration,int llmCalls,CancellationToken cancellationToken)
    {
        ResetMatterFunnelState();
        if(parents.Length==0)return llmCalls;

        // Stage 3 first: passage counts feed the deterministic relevance score.
        await MapPassagesToParentsAsync(request,executionId,parents,configuration,cancellationToken);

        var deterministic=new Dictionary<Guid,(decimal Score,List<string> Reasons)>();
        foreach(var parent in parents)deterministic[parent.WideBranchId]=ComputeDeterministicRelevance(parent);

        IReadOnlyDictionary<string,(decimal Score,string? Reason)> reviewed=new Dictionary<string,(decimal,string?)>(StringComparer.OrdinalIgnoreCase);
        if(configuration.EnableL2RelevanceReview&&llmCalls+2<=configuration.MaximumTotalLlmCalls)
        {
            try{(reviewed,var used)=await ReviewLevel2RelevanceAsync(request,parents,deterministic,configuration,configuration.MaximumTotalLlmCalls-llmCalls-1,cancellationToken);llmCalls+=used;}
            catch(Exception ex)when(!cancellationToken.IsCancellationRequested){logger.LogWarning(ex,"Wide2 matter funnel: L2 relevance review failed; deterministic relevance only.");}
        }

        var tierA=0;
        foreach(var parent in parents)
        {
            var (score,reasons)=deterministic[parent.WideBranchId];
            var deterministicScore=Math.Clamp(score,0,1);
            decimal? llmScore=null;string? llmReason=null;
            if(reviewed.TryGetValue(parent.BranchCode,out var review))
            {
                llmScore=Math.Clamp(review.Score,0,1);llmReason=review.Reason?.Trim();
                score=Math.Round(score*0.5m+review.Score*0.5m,4);
                if(!string.IsNullOrWhiteSpace(review.Reason))reasons.Add($"LLM: {review.Reason.Trim()}");
            }
            _relevanceDetailByBranch[parent.WideBranchId]=(deterministicScore,llmScore,llmReason);
            score=Math.Clamp(score,0,1);
            var tier=score>=configuration.L3TierAThreshold?"TIER_A":"TIER_B";
            if(tier=="TIER_A")tierA++;
            _relevanceByBranch[parent.WideBranchId]=score;
            if(_scopeLedgerByBranchCode.TryGetValue(NormalizeCode(parent.BranchCode),out var index))
            {
                var row=_scopeLedger[index];
                _passageMapByBranch.TryGetValue(parent.WideBranchId,out var mapped);
                _scopeLedger[index]=row with{PriorityScore=score,TierCode=tier,MappedPassageCount=mapped?.Length??row.MappedPassageCount,RelevanceReasons=Truncate(string.Join(" | ",reasons),2000)};
            }
            _factorSliceByBranch[parent.WideBranchId]=BuildFactorSlice(parent,configuration.L3FactorSliceMaxItems);
        }
        logger.LogInformation("Wide2 matter funnel: parents={Parents} tierA={TierA} reviewed={Reviewed} mappedParents={Mapped} passages={Passages}.",parents.Length,tierA,reviewed.Count,_passageMapByBranch.Count,_matterPassageIndex.Count);
        return llmCalls;
    }

    // ── 0441 per-matter Evidence Type scope (HIGH IMPACT: this is the only L2→L3 membership gate) ──
    // Syncs the funnel output (deterministic + LLM scores, signals) into POLOXI.Legal_MatterEvidenceScope for
    // the request's matter, then reads the persisted decisions back and returns the parents that must NOT
    // receive an L3 pass. Guarantees:
    //   * USER rows are authoritative - the repository never overwrites their status/decider/note;
    //   * required-coverage, document-present and PRIORITIZE-fired targets are never auto-excluded;
    //   * bypassed (nothing held back) when the gate is disabled, there is no matter, or the matter has
    //     neither a matter type nor any uploaded documents (no signal to decide against);
    //   * fail-soft: any persistence/read error logs a warning and holds nothing back (today's behaviour).
    private async Task<IReadOnlySet<Guid>> ApplyMatterEvidenceScopeAsync(WideSearchRequest request,Guid executionId,WideBranchRecord[] parents,WideConfiguration configuration,CancellationToken cancellationToken)
    {
        var held=new HashSet<Guid>();
        if(!configuration.EnableMatterEvidenceScopeGate||request.MatterId is not{}matterId||parents.Length==0)return held;
        if(_matterTypeSignals.Length==0&&_matterDocumentEvidenceTypeCodes.Count==0&&_matterPassageIndex.Count==0)
        {
            logger.LogInformation("Wide2 matter scope gate bypassed for matter {MatterId}: no matter type and no documents to decide against.",matterId);
            return held;
        }
        var packCode=_resolvedDomainPackCode;
        if(string.IsNullOrWhiteSpace(packCode))return held;

        try
        {
            var evaluations=new List<MatterEvidenceScopeEvaluation>(parents.Length);
            var autoOut=0;
            foreach(var parent in parents)
            {
                if(!_scopeLedgerByBranchCode.TryGetValue(NormalizeCode(parent.BranchCode),out var index))continue;
                var ledger=_scopeLedger[index];
                var blended=ledger.PriorityScore;
                _relevanceDetailByBranch.TryGetValue(parent.WideBranchId,out var detail);
                decimal? deterministic=detail.Deterministic;var llmScore=detail.Llm;
                var protectedTarget=ledger.IsRequiredCoverage||ledger.DocumentPresent||ledger.IsPrioritized||ledger.MappedPassageCount>0;
                var status=MatterEvidenceScopeStatusCodes.InScope;
                if(!protectedTarget&&blended is{}score&&score<configuration.MatterScopeOutThreshold){status=MatterEvidenceScopeStatusCodes.OutOfScope;autoOut++;}
                var decidedBy=string.Equals(ledger.DecidedByCode,"RULE",StringComparison.OrdinalIgnoreCase)?MatterEvidenceScopeDeciderCodes.Rule
                    :llmScore is not null?MatterEvidenceScopeDeciderCodes.Llm:MatterEvidenceScopeDeciderCodes.System;
                evaluations.Add(new MatterEvidenceScopeEvaluation(packCode,ledger.TargetKindCode,ledger.TargetCode,ledger.DimensionCode,ledger.DisplayName,status,decidedBy,
                    deterministic,llmScore,blended,ledger.TierCode,ledger.RelevanceReasons,Truncate(detail.LlmReason,400),
                    ledger.IsRequiredCoverage,ledger.IsPrioritized,ledger.DocumentPresent,ledger.MappedPassageCount,executionId,ledger.SortOrder));
            }
            await legalDecisionRepository.UpsertSystemMatterEvidenceScopesAsync(request.TenantId,request.UserId,matterId,evaluations,cancellationToken);

            var persisted=await legalDecisionRepository.GetMatterEvidenceScopesAsync(request.TenantId,matterId,cancellationToken);
            var statuses=await legalDecisionRepository.GetMatterEvidenceScopeStatusesAsync(request.TenantId,cancellationToken);
            var participating=statuses.Where(s=>s.ParticipatesInL3).Select(s=>s.StatusCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var byTarget=persisted.Where(p=>string.Equals(p.PackCode,packCode,StringComparison.OrdinalIgnoreCase))
                .ToDictionary(p=>$"{p.TargetKindCode}|{p.TargetCode}",StringComparer.OrdinalIgnoreCase);
            var userHeld=0;
            foreach(var parent in parents)
            {
                if(!_scopeLedgerByBranchCode.TryGetValue(NormalizeCode(parent.BranchCode),out var index))continue;
                var ledger=_scopeLedger[index];
                if(!byTarget.TryGetValue($"{ledger.TargetKindCode}|{ledger.TargetCode}",out var row))continue;
                if(participating.Contains(row.ScopeStatusCode))continue;
                held.Add(parent.WideBranchId);
                if(string.Equals(row.DecidedByCode,MatterEvidenceScopeDeciderCodes.User,StringComparison.OrdinalIgnoreCase))userHeld++;
                _scopeLedger[index]=ledger with{ScopeStatusCode=row.ScopeStatusCode,DecidedByCode=row.DecidedByCode,Reason=Truncate(row.UserNote??row.LlmReason??$"Matter scope {row.ScopeStatusCode} ({row.DecidedByCode})",1000)};
            }
            // Safety: a gate that would remove every parent is treated as misconfiguration, not a decision.
            if(held.Count>0&&held.Count>=parents.Length)
            {
                logger.LogWarning("Wide2 matter scope gate for matter {MatterId} would hold back all {Parents} parents; ignoring the gate for this run.",matterId,parents.Length);
                held.Clear();
            }
            logger.LogInformation("Wide2 matter scope gate for matter {MatterId}: parents={Parents} autoOut={AutoOut} heldBack={Held} (userDecided={UserHeld}) threshold={Threshold}.",matterId,parents.Length,autoOut,held.Count,userHeld,configuration.MatterScopeOutThreshold);
        }
        catch(Exception ex)when(!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex,"Wide2 matter scope gate failed for matter {MatterId}; all parents proceed to L3.",matterId);
            held.Clear();
        }
        return held;
    }

    // ── 0442 per-matter Atomic Propositions: capture + gate (HIGH IMPACT) ──────────────────────────
    // Every accepted L3+ child is persisted per matter under its Domain Pack L2 target with the worker that
    // produced it and the model-call provenance. Then the persisted decisions are read back: a proposition
    // whose status does not participate in narrowing (USER authoritative) is marked eliminated for this run
    // (MATTER_SCOPE_OUT) - it stays in the hierarchy/ledgers as an audit row and is never deleted.
    // Guarantees: fail-soft (any error eliminates nothing); a gate that would eliminate EVERY child of the
    // level is treated as misconfiguration and ignored; propositions without a resolvable catalog parent are
    // not persisted (never written outside the catalog) and are never gated.
    internal sealed record AtomicPropositionProvenance(string WorkerName,int BatchNumber,int AttemptNumber,string? BatchTierCode,AiGenerationResult? Call,int AnchorCount,string? AnchorsJson,string? DependsOnCodes,string? EquivalentToCodes);

    private async Task<WideBranchRecord[]> PersistMatterAtomicPropositionsAsync(WideSearchRequest request,Guid executionId,WideBranchRecord[] level,IReadOnlyCollection<WideBranchRecord> allBranches,WideConfiguration configuration,int levelNumber,CancellationToken cancellationToken)
    {
        if(request.MatterId is not{}matterId||level.Length==0)return level;
        var packCode=_resolvedDomainPackCode;
        if(string.IsNullOrWhiteSpace(packCode))return level;
        try
        {
            var byId=allBranches.Concat(level).GroupBy(branch=>branch.WideBranchId).ToDictionary(group=>group.Key,group=>group.First());
            var evaluations=new List<MatterAtomicPropositionEvaluation>(level.Length);
            var parentKeyByBranch=new Dictionary<Guid,string>();
            foreach(var child in level)
            {
                // Walk up to the L2 catalog target (CPT_/EVD_ branch with a scope-ledger row); the direct parent
                // is the parent proposition when the child is deeper than L3.
                WideBranchRecord? l2=null;string? parentPropositionCode=null;
                var cursor=child.ParentWideBranchId is{}parentId&&byId.TryGetValue(parentId,out var direct)?direct:null;
                if(cursor is not null&&!_scopeLedgerByBranchCode.ContainsKey(NormalizeCode(cursor.BranchCode)))parentPropositionCode=cursor.BranchCode;
                while(cursor is not null)
                {
                    if(_scopeLedgerByBranchCode.ContainsKey(NormalizeCode(cursor.BranchCode))){l2=cursor;break;}
                    cursor=cursor.ParentWideBranchId is{}up&&byId.TryGetValue(up,out var next)?next:null;
                }
                if(l2 is null||!_scopeLedgerByBranchCode.TryGetValue(NormalizeCode(l2.BranchCode),out var index))continue;
                var ledger=_scopeLedger[index];
                _atomicProvenanceByBranch.TryGetValue(child.WideBranchId,out var prov);
                var call=prov?.Call??_lastHierarchyStepCall;
                parentKeyByBranch[child.WideBranchId]=$"{ledger.TargetKindCode}|{ledger.TargetCode}|{NormalizeCode(child.BranchCode)}";
                evaluations.Add(new MatterAtomicPropositionEvaluation(packCode,ledger.TargetKindCode,ledger.TargetCode,ledger.DisplayName,ledger.DimensionCode,parentPropositionCode,
                    NormalizeCode(child.BranchCode),child.LevelNumber,Truncate(child.DisplayName,300)??child.BranchCode,Truncate(string.IsNullOrWhiteSpace(child.Interpretation)?child.DisplayName:child.Interpretation,2000)??child.BranchCode,
                    Truncate(child.SearchText,600),Truncate(child.CapabilityCode,120),Math.Clamp(child.Confidence,0m,1m),child.SemanticTypeCode,child.BranchRoleCode,child.ContinueNarrowing,Truncate(child.StopReason,600),
                    Truncate(prov?.DependsOnCodes,2000),Truncate(prov?.EquivalentToCodes,2000),prov?.AnchorCount??0,prov?.AnchorsJson,
                    prov?.WorkerName??"Hierarchy step",prov?.BatchNumber??0,prov?.AttemptNumber??1,prov?.BatchTierCode,
                    Truncate(call?.ModelCode,120),Truncate(call?.ProviderCode,60),call?.InputTokenCount,call?.OutputTokenCount,call is null?null:(int)Math.Min(int.MaxValue,call.Duration.TotalMilliseconds),Truncate(call?.ProviderRequestId,200),
                    child.WideBranchId,executionId,child.SortOrder));
            }
            if(evaluations.Count==0)return level;
            await legalDecisionRepository.UpsertSystemMatterAtomicPropositionsAsync(request.TenantId,request.UserId,matterId,evaluations,cancellationToken);

            var persisted=await legalDecisionRepository.GetMatterAtomicPropositionsAsync(request.TenantId,matterId,cancellationToken);
            var statuses=await legalDecisionRepository.GetMatterAtomicPropositionStatusesAsync(request.TenantId,cancellationToken);
            var participating=statuses.Where(s=>s.ParticipatesInNarrowing).Select(s=>s.StatusCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var rows=persisted.Where(p=>string.Equals(p.PackCode,packCode,StringComparison.OrdinalIgnoreCase))
                .GroupBy(p=>$"{p.ParentTargetKindCode}|{p.ParentTargetCode}|{p.PropositionCode}",StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
            var eliminated=new Dictionary<Guid,MatterAtomicPropositionDto>();
            foreach(var child in level)
            {
                if(!parentKeyByBranch.TryGetValue(child.WideBranchId,out var key)||!rows.TryGetValue(key,out var row))continue;
                if(participating.Contains(row.ScopeStatusCode))continue;
                eliminated[child.WideBranchId]=row;
            }
            if(eliminated.Count>0&&eliminated.Count>=level.Length)
            {
                logger.LogWarning("Wide2 atomic-proposition gate for matter {MatterId} would eliminate all {Count} level-{Level} propositions; ignoring the gate for this run.",matterId,level.Length,levelNumber);
                return level;
            }
            var userHeld=eliminated.Values.Count(r=>string.Equals(r.DecidedByCode,MatterAtomicPropositionDeciderCodes.User,StringComparison.OrdinalIgnoreCase));
            logger.LogInformation("Wide2 atomic propositions for matter {MatterId}: level={Level} persisted={Persisted} eliminated={Eliminated} (userDecided={UserHeld}).",matterId,levelNumber,evaluations.Count,eliminated.Count,userHeld);
            if(eliminated.Count==0)return level;
            return level.Select(child=>eliminated.TryGetValue(child.WideBranchId,out var row)
                ?child with{IsEliminated=true,EliminationReason=Truncate($"MATTER_SCOPE_OUT: {row.UserNote??$"Atomic proposition {row.ScopeStatusCode} ({row.DecidedByCode})"}",1000),ContinueNarrowing=false}
                :child).ToArray();
        }
        catch(Exception ex)when(!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex,"Wide2 atomic-proposition persistence/gate failed for matter {MatterId} at level {Level}; all propositions proceed.",matterId,levelNumber);
            return level;
        }
    }


    // ── Stage 1: deterministic relevance (auditable reasons) ──
    private (decimal Score,List<string> Reasons) ComputeDeterministicRelevance(WideBranchRecord parent)
    {
        var reasons=new List<string>();var score=0m;
        var isEvidence=parent.BranchCode.StartsWith("EVD_",StringComparison.OrdinalIgnoreCase);
        var targetCode=isEvidence?parent.BranchCode["EVD_".Length..]:parent.BranchCode.StartsWith("CPT_",StringComparison.OrdinalIgnoreCase)?parent.BranchCode["CPT_".Length..]:parent.BranchCode;
        var evidence=isEvidence?_packEvidenceTypes.FirstOrDefault(e=>string.Equals(e.EvidenceTypeCode,targetCode,StringComparison.OrdinalIgnoreCase)):null;

        if(evidence is not null)
        {
            if(_matterDocumentEvidenceTypeCodes.Contains(evidence.EvidenceTypeCode)){score+=0.35m;reasons.Add("Uploaded document classified as this evidence type");}
            if(!string.IsNullOrWhiteSpace(evidence.MatterTypeCodes)&&_matterTypeSignals.Length>0)
            {
                var codes=evidence.MatterTypeCodes.Split(',',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries);
                var matched=_matterTypeSignals.FirstOrDefault(signal=>codes.Any(c=>string.Equals(c,signal,StringComparison.OrdinalIgnoreCase)||signal.Contains(c,StringComparison.OrdinalIgnoreCase)||c.Contains(signal,StringComparison.OrdinalIgnoreCase)));
                if(matched is not null){score+=0.15m;reasons.Add($"Matter type match ({matched})");}
            }
            if(!string.IsNullOrWhiteSpace(evidence.RelevanceKeywords))
            {
                var hits=DomainPackEvidencePresenceMatcher.NormalizeCorpusTokens(evidence.RelevanceKeywords).Where(_matterPresenceTokens.Contains).Distinct().Take(5).ToArray();
                if(hits.Length>0){score+=Math.Min(0.2m,0.05m*hits.Length);reasons.Add($"Relevance keywords in matter: {string.Join(", ",hits)}");}
            }
            if(evidence.IsRequiredCoverage){score+=0.05m;reasons.Add("Required coverage");}
            if(evidence.PoloxiImportanceScore is{}importance)score+=Math.Clamp(importance,0,1)*0.1m;
        }
        var nameHits=DomainPackEvidencePresenceMatcher.NormalizeCorpusTokens(parent.DisplayName).Where(t=>t.Length>3&&_matterPresenceTokens.Contains(t)).Distinct().Take(4).ToArray();
        if(nameHits.Length>0){score+=Math.Min(0.15m,0.05m*nameHits.Length);reasons.Add($"Name terms in matter: {string.Join(", ",nameHits)}");}
        if(_scopeLedgerByBranchCode.TryGetValue(NormalizeCode(parent.BranchCode),out var index)&&_scopeLedger[index].IsPrioritized){score+=0.15m;reasons.Add("PRIORITIZE relevance rule fired");}
        if(_passageMapByBranch.TryGetValue(parent.WideBranchId,out var mapped)&&mapped.Length>0){score+=Math.Min(0.25m,0.05m*mapped.Length);reasons.Add($"{mapped.Length} mapped document passage(s)");}
        if(parent.EvidenceCount>0){score+=Math.Min(0.1m,0.02m*parent.EvidenceCount);reasons.Add($"{parent.EvidenceCount} retrieval evidence item(s)");}
        if(reasons.Count==0)reasons.Add("Catalog membership only; no matter signal matched");
        return(Math.Round(Math.Clamp(score,0,1),4),reasons);
    }

    // ── Stage 2: optional LLM relevance review, strictly bound to the catalog, batched by dimension ──
    private async Task<(IReadOnlyDictionary<string,(decimal Score,string? Reason)> Reviewed,int LlmCalls)> ReviewLevel2RelevanceAsync(WideSearchRequest request,WideBranchRecord[] parents,IReadOnlyDictionary<Guid,(decimal Score,List<string> Reasons)> deterministic,WideConfiguration configuration,int callBudget,CancellationToken cancellationToken)
    {
        var evidenceByCode=_packEvidenceTypes.ToDictionary(e=>e.EvidenceTypeCode,StringComparer.OrdinalIgnoreCase);
        var conceptByCode=_packConcepts.GroupBy(c=>c.ConceptCode,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First(),StringComparer.OrdinalIgnoreCase);
        var batchSize=Math.Max(5,configuration.L2ReviewBatchSize);
        var batches=parents
            .GroupBy(parent=>DimensionCodeFor(parent,evidenceByCode,conceptByCode),StringComparer.OrdinalIgnoreCase)
            .OrderBy(group=>group.Key,StringComparer.OrdinalIgnoreCase)
            .SelectMany(group=>group.OrderBy(p=>p.SortOrder).Chunk(batchSize).Select(chunk=>(Dimension:group.Key,Parents:chunk)))
            .ToArray();
        if(batches.Length>callBudget)
        {
            logger.LogWarning("Wide2 matter funnel: relevance review needs {Needed} batch call(s) but only {Budget} remain; reviewing the first {Budget} batch(es), remaining parents keep deterministic relevance.",batches.Length,callBudget,callBudget);
            batches=batches.Take(Math.Max(0,callBudget)).ToArray();
        }
        if(batches.Length==0)return(new Dictionary<string,(decimal,string?)>(StringComparer.OrdinalIgnoreCase),0);

        var matterContext=BoundContractContext(BuildMatterContextSection(),ContractContextPrefixBudgetChars);
        var systemPrompt=await GetWideSystemPromptAsync(request,IntelligencePromptCodes.WideL2RelevanceReview,cancellationToken);
        var gate=new SemaphoreSlim(Math.Max(1,configuration.L3PassageMapConcurrency));
        var results=await Task.WhenAll(batches.Select(async batch=>
        {
            await gate.WaitAsync(cancellationToken);
            try{return await ReviewOneBatchAsync(request,batch.Dimension,batch.Parents,deterministic,evidenceByCode,conceptByCode,matterContext,systemPrompt,configuration,cancellationToken);}
            finally{gate.Release();}
        }));

        var accepted=new Dictionary<string,(decimal,string?)>(StringComparer.OrdinalIgnoreCase);
        var rejected=0;var failed=0;
        foreach(var (items,batchRejected,ok) in results)
        {
            rejected+=batchRejected;if(!ok)failed++;
            foreach(var (code,value) in items)if(!accepted.ContainsKey(code))accepted[code]=value;
        }
        var unreviewed=parents.Count(p=>!accepted.ContainsKey(NormalizeCode(p.BranchCode)));
        if(rejected>0)logger.LogWarning("Wide2 matter funnel: relevance review returned {Rejected} code(s) outside the Domain Pack catalog; dropped.",rejected);
        logger.LogInformation("Wide2 matter funnel: relevance review batches={Batches} failed={Failed} batchSize={BatchSize} reviewed={Reviewed} unreviewed={Unreviewed} of {Parents}.",batches.Length,failed,batchSize,accepted.Count,unreviewed,parents.Length);
        return(accepted,batches.Length);
    }

    private async Task<(List<KeyValuePair<string,(decimal,string?)>> Items,int Rejected,bool Ok)> ReviewOneBatchAsync(WideSearchRequest request,string dimension,WideBranchRecord[] batch,IReadOnlyDictionary<Guid,(decimal Score,List<string> Reasons)> deterministic,IReadOnlyDictionary<string,DecisionDomainPackEvidenceTypeDto> evidenceByCode,IReadOnlyDictionary<string,DecisionDomainConceptDto> conceptByCode,string? matterContext,string systemPrompt,WideConfiguration configuration,CancellationToken cancellationToken)
    {
        var items=new List<KeyValuePair<string,(decimal,string?)>>();
        var catalog=batch.Select(parent=>parent.BranchCode).ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            var lines=string.Join('\n',batch.Select(parent=>BuildReviewLine(parent,deterministic[parent.WideBranchId],evidenceByCode,conceptByCode)));
            var prompt=Truncate($"Original question: {Truncate(request.Query,3000)}{matterContext}\nConfigured Domain Pack Level-2 catalog, dimension {dimension} (FIXED - return exactly these {batch.Length} codes, no additions). Each line gives the DB definition of the evidence type/concept: description, applicable matter types, relevance keywords, and the deterministic matter signals already found:\n{lines}",WideUserPromptBudget)!;
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_WIDE_HIERARCHY_STEP",systemPrompt,
                prompt,L2RelevanceSchema,request.CorrelationId,new("Intelligence",null,null,request.Query,"WIDE_L2_RELEVANCE_REVIEW",null,request.CorrelationId,"Intelligent Search Wide"),MechanicalModel(configuration,request),cancellationToken);
            var proposal=JsonSerializer.Deserialize<WideL2RelevanceProposal>(result.Content,JsonOptions);
            // Catalog gate: only codes that exist in this batch's materialized Domain Pack L2 set are accepted.
            var rejected=0;var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var item in proposal?.Items??[])
            {
                var code=NormalizeCode(item.BranchCode);
                if(!catalog.Contains(code)){rejected++;continue;}
                if(seen.Add(code))items.Add(new(code,(Math.Clamp(item.Relevance,0,1),Truncate(item.Reason,300))));
            }
            return(items,rejected,true);
        }
        catch(Exception ex)when(!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex,"Wide2 matter funnel: relevance review batch for dimension {Dimension} ({Count} parents) failed; deterministic relevance only for this batch.",dimension,batch.Length);
            return(items,0,false);
        }
    }

    private static string DimensionCodeFor(WideBranchRecord parent,IReadOnlyDictionary<string,DecisionDomainPackEvidenceTypeDto> evidenceByCode,IReadOnlyDictionary<string,DecisionDomainConceptDto> conceptByCode)
    {
        if(parent.BranchCode.StartsWith("EVD_",StringComparison.OrdinalIgnoreCase)&&evidenceByCode.TryGetValue(parent.BranchCode["EVD_".Length..],out var evidence))return string.IsNullOrWhiteSpace(evidence.DimensionCode)?"GENERAL":evidence.DimensionCode;
        if(parent.BranchCode.StartsWith("CPT_",StringComparison.OrdinalIgnoreCase)&&conceptByCode.TryGetValue(parent.BranchCode["CPT_".Length..],out var concept))return string.IsNullOrWhiteSpace(concept.DimensionCode)?"GENERAL":concept.DimensionCode;
        return "GENERAL";
    }

    private static string BuildReviewLine(WideBranchRecord parent,(decimal Score,List<string> Reasons) deterministic,IReadOnlyDictionary<string,DecisionDomainPackEvidenceTypeDto> evidenceByCode,IReadOnlyDictionary<string,DecisionDomainConceptDto> conceptByCode)
    {
        var sb=new StringBuilder();
        sb.Append("- ").Append(parent.BranchCode).Append(" \"").Append(parent.DisplayName).Append('"');
        if(parent.BranchCode.StartsWith("EVD_",StringComparison.OrdinalIgnoreCase)&&evidenceByCode.TryGetValue(parent.BranchCode["EVD_".Length..],out var evidence))
        {
            if(!string.IsNullOrWhiteSpace(evidence.Description))sb.Append(" | def: ").Append(Truncate(evidence.Description.Trim(),220));
            if(!string.IsNullOrWhiteSpace(evidence.MatterTypeCodes))sb.Append(" | matter types: ").Append(Truncate(evidence.MatterTypeCodes.Trim(),160));
            if(!string.IsNullOrWhiteSpace(evidence.RelevanceKeywords))sb.Append(" | keywords: ").Append(Truncate(evidence.RelevanceKeywords.Trim(),240));
            if(evidence.IsRequiredCoverage)sb.Append(" | required coverage");
        }
        else if(parent.BranchCode.StartsWith("CPT_",StringComparison.OrdinalIgnoreCase)&&conceptByCode.TryGetValue(parent.BranchCode["CPT_".Length..],out var concept))
        {
            if(!string.IsNullOrWhiteSpace(concept.Description))sb.Append(" | def: ").Append(Truncate(concept.Description.Trim(),220));
            if(!string.IsNullOrWhiteSpace(concept.MatterTypeCode))sb.Append(" | matter type: ").Append(concept.MatterTypeCode.Trim());
            if(concept.IsRequiredCoverage)sb.Append(" | required coverage");
        }
        sb.Append(" | deterministic ").Append(deterministic.Score.ToString("0.00")).Append(": ").Append(Truncate(string.Join("; ",deterministic.Reasons),200));
        return sb.ToString();
    }

    // ── Stage 3: bounded-worker passage -> parent mapping, computed once, top-N persisted ──
    private async Task MapPassagesToParentsAsync(WideSearchRequest request,Guid executionId,WideBranchRecord[] parents,WideConfiguration configuration,CancellationToken cancellationToken)
    {
        if(_matterPassageIndex.Count==0)return;
        var passages=_matterPassageIndex.Where(item=>item.LegalDocumentPassageId is not null&&!string.IsNullOrWhiteSpace(item.PassageText)).GroupBy(item=>item.LegalDocumentPassageId!.Value).Select(group=>group.First()).ToArray();
        // Tokenize the corpus ONCE; each parent then scans token sets rather than re-tokenizing text.
        var passageTokens=passages.Select(item=>DomainPackEvidencePresenceMatcher.NormalizeCorpusTokens(item.PassageText!).ToHashSet(StringComparer.Ordinal)).ToArray();
        var evidenceByCode=_packEvidenceTypes.ToDictionary(e=>e.EvidenceTypeCode,StringComparer.OrdinalIgnoreCase);
        var gate=new SemaphoreSlim(Math.Max(1,configuration.L3PassageMapConcurrency));
        var topN=Math.Max(1,configuration.L3PassageMapTopN);
        var results=await Task.WhenAll(parents.Select(async parent=>
        {
            await gate.WaitAsync(cancellationToken);
            try{return (parent,await Task.Run(()=>MapOneParent(parent,passages,passageTokens,evidenceByCode,topN),cancellationToken));}
            finally{gate.Release();}
        }));
        var rows=new List<WideBranchPassageMapRecord>();
        foreach(var (parent,matches) in results)
        {
            if(matches.Length==0)continue;
            _passageMapByBranch[parent.WideBranchId]=matches;
            for(var rank=0;rank<matches.Length;rank++)
                rows.Add(new(Guid.NewGuid(),executionId,parent.WideBranchId,matches[rank].Passage.LegalDocumentPassageId!.Value,request.TenantId,matches[rank].Method,Math.Round(matches[rank].Score,4),rank+1));
        }
        try{await wideRepository.SaveWideBranchPassageMapsAsync(rows,request.UserId,cancellationToken);}
        catch(Exception ex)when(!cancellationToken.IsCancellationRequested){logger.LogWarning(ex,"Wide2 matter funnel: passage-map persistence failed (advisory); {Rows} rows dropped.",rows.Count);}
        logger.LogInformation("Wide2 matter funnel: passage map parents={Parents} passages={Passages} rows={Rows} workers={Workers}.",parents.Length,passages.Length,rows.Count,configuration.L3PassageMapConcurrency);
    }

    private static PassageMatch[] MapOneParent(WideBranchRecord parent,LegalEvidenceGraphItemDto[] passages,HashSet<string>[] passageTokens,IReadOnlyDictionary<string,DecisionDomainPackEvidenceTypeDto> evidenceByCode,int topN)
    {
        var isEvidence=parent.BranchCode.StartsWith("EVD_",StringComparison.OrdinalIgnoreCase);
        var code=isEvidence?parent.BranchCode["EVD_".Length..]:null;
        var keywordTokens=code is not null&&evidenceByCode.TryGetValue(code,out var evidence)&&!string.IsNullOrWhiteSpace(evidence.RelevanceKeywords)
            ?DomainPackEvidencePresenceMatcher.NormalizeCorpusTokens(evidence.RelevanceKeywords).Where(t=>t.Length>3).ToHashSet(StringComparer.Ordinal):[];
        var nameTokens=DomainPackEvidencePresenceMatcher.NormalizeCorpusTokens(parent.DisplayName).Where(t=>t.Length>3).ToHashSet(StringComparer.Ordinal);
        var matches=new List<PassageMatch>();
        for(var i=0;i<passages.Length;i++)
        {
            var passage=passages[i];var tokens=passageTokens[i];
            if(code is not null&&string.Equals(passage.EvidenceTypeCode,code,StringComparison.OrdinalIgnoreCase)){matches.Add(new(passage,"EVIDENCE_TYPE",1m));continue;}
            var keywordHits=keywordTokens.Count==0?0:keywordTokens.Count(tokens.Contains);
            if(keywordHits>0){matches.Add(new(passage,"KEYWORD",Math.Min(0.95m,0.5m+0.15m*keywordHits)));continue;}
            var nameHits=nameTokens.Count==0?0:nameTokens.Count(tokens.Contains);
            if(nameHits>0&&(nameHits>=2||nameTokens.Count==1))matches.Add(new(passage,"LEXICAL",Math.Min(0.8m,0.3m+0.15m*nameHits)));
        }
        return matches.OrderByDescending(m=>m.Score).ThenByDescending(m=>m.Passage.ExtractionConfidence??0).Take(topN).ToArray();
    }

    // ── Stage 4: matter-bound Factor Inventory slice for one parent ──
    private string BuildFactorSlice(WideBranchRecord parent,int maximum)
    {
        if(maximum<=0||_matterContext is null)return string.Empty;
        var tokens=DomainPackEvidencePresenceMatcher.NormalizeCorpusTokens(parent.DisplayName).Where(t=>t.Length>3).ToHashSet(StringComparer.Ordinal);
        if(tokens.Count==0)return string.Empty;
        var fields=_matterContext.Decision.Concat(_matterContext.LegalScope).Concat(_matterContext.PersonalInjuryProfile).Concat(_matterContext.Facts).Concat(_matterContext.Evidence)
            .Where(f=>f.HasValue)
            .Select(f=>(Field:f,Hits:DomainPackEvidencePresenceMatcher.NormalizeCorpusTokens(f.Label+" "+f.Value).Count(tokens.Contains)))
            .Where(x=>x.Hits>0).OrderByDescending(x=>x.Hits).Take(maximum).ToArray();
        if(fields.Length==0)return string.Empty;
        var builder=new StringBuilder();
        foreach(var (field,_) in fields)builder.Append("\n    [matter ").Append(field.Provenance.ToString().ToUpperInvariant()).Append("] ").Append(field.Label).Append(": ").Append(Truncate(field.Value?.Trim(),240));
        return builder.ToString();
    }

    private sealed record WideL2RelevanceProposal(IReadOnlyCollection<WideL2RelevanceItem>? Items);
    private sealed record WideL2RelevanceItem(string BranchCode,decimal Relevance,string? Reason);

    private const string L2RelevanceSchema="""
{
  "type": "object",
  "properties": {
    "items": { "type": "array", "maxItems": 400, "items": {
      "type": "object",
      "properties": {
        "branchCode": { "type": "string" },
        "relevance": { "type": "number" },
        "reason": { "type": ["string", "null"] }
      },
      "required": ["branchCode", "relevance", "reason"],
      "additionalProperties": false } }
  },
  "required": ["items"],
  "additionalProperties": false
}
""";
}
