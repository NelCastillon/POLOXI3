using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Models;
using Ams.Application.Features.Intelligence;
using Ams.Application.Features.Platform;
using Ams.Application.Features.SearchMatching;

namespace Ams.Application;

public sealed class IntelligenceService(IIntelligenceRepository repository,IRecommendationGenerationRepository recommendationRepository,ISemanticQueryExpander queryExpander,IEntityMatchingService entityMatchingService,IRulesPlatformService rulesPlatformService,IAiProviderRouter aiProviderRouter,IPromptCatalog promptCatalog):IIntelligenceService
{
    public Task<IReadOnlyCollection<AiProviderDto>> GetProvidersAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetProvidersAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task<IReadOnlyCollection<AiModelDeploymentDto>> GetModelsAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetModelsAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task<IReadOnlyCollection<AiFeaturePolicyDto>> GetFeaturePoliciesAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetFeaturePoliciesAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task SaveFeaturePolicyAsync(SaveAiFeaturePolicyRequest request,CancellationToken cancellationToken=default){Validate(request);return repository.SaveFeaturePolicyAsync(request,cancellationToken);}
    public Task SaveProviderAsync(SaveAiProviderRequest request,CancellationToken cancellationToken=default){Validate(request);return repository.SaveProviderAsync(request,cancellationToken);}
    public Task SaveModelDeploymentAsync(SaveAiModelDeploymentRequest request,CancellationToken cancellationToken=default){Validate(request);return repository.SaveModelDeploymentAsync(request,cancellationToken);}
    public Task DeleteProviderAsync(Guid tenantId,string providerCode,Guid actorUserId,CancellationToken cancellationToken=default){if(string.IsNullOrWhiteSpace(providerCode))throw new ValidationException("Provider code is required.");return repository.DeleteProviderAsync(tenantId,providerCode,actorUserId,cancellationToken);}
    public Task DeleteModelDeploymentAsync(Guid tenantId,string modelCode,Guid actorUserId,CancellationToken cancellationToken=default){if(string.IsNullOrWhiteSpace(modelCode))throw new ValidationException("Model code is required.");return repository.DeleteModelDeploymentAsync(tenantId,modelCode,actorUserId,cancellationToken);}
    public Task DeleteFeaturePolicyAsync(Guid tenantId,string featureCode,Guid actorUserId,CancellationToken cancellationToken=default){if(string.IsNullOrWhiteSpace(featureCode))throw new ValidationException("Feature code is required.");return repository.DeleteFeaturePolicyAsync(tenantId,featureCode,actorUserId,cancellationToken);}
    public Task<PagedResult<AiExecutionSummaryDto>> SearchExecutionsAsync(SearchAiExecutionsQuery query,CancellationToken cancellationToken=default){ValidatePage(query.TenantId,query.PageNumber,query.PageSize);return repository.SearchExecutionsAsync(query with{PageSize=Math.Clamp(query.PageSize,1,200)},cancellationToken);}
    public Task<AiExecutionDetailDto?> GetExecutionAsync(Guid tenantId,Guid executionId,CancellationToken cancellationToken=default)=>repository.GetExecutionAsync(Required(tenantId,nameof(tenantId)),Required(executionId,nameof(executionId)),cancellationToken);
    public Task SubmitExecutionFeedbackAsync(SubmitAiExecutionFeedbackRequest request,CancellationToken cancellationToken=default){Validate(request);return repository.SubmitExecutionFeedbackAsync(request,cancellationToken);}
    public Task<IReadOnlyCollection<RecommendationTypeDto>> GetRecommendationTypesAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetRecommendationTypesAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task<PagedResult<RecommendationDto>> SearchRecommendationsAsync(SearchRecommendationsQuery query,CancellationToken cancellationToken=default){ValidatePage(query.TenantId,query.PageNumber,query.PageSize);return repository.SearchRecommendationsAsync(query with{PageSize=Math.Clamp(query.PageSize,1,200)},cancellationToken);}
    public Task QueueRecommendationsAsync(GenerateRecommendationsRequest request,CancellationToken cancellationToken=default){Validate(request);return recommendationRepository.GenerateAsync(request,cancellationToken);}
    public Task DecideRecommendationAsync(DecideRecommendationRequest request,CancellationToken cancellationToken=default){Validate(request);return repository.DecideRecommendationAsync(request,cancellationToken);}
    public async Task<IntelligenceSearchResponse> SearchAsync(IntelligenceSearchRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);
        if(request.UserId==Guid.Empty)throw new UnauthorizedAccessException("An authenticated user is required for Intelligence Search.");
        var timer=Stopwatch.StartNew();
        request=request with{Query=NormalizeQuery(request.Query),ModuleCode=OptionalFilter(request.ModuleCode),EntityTypeCode=OptionalFilter(request.EntityTypeCode),MaximumResults=Math.Clamp(request.MaximumResults,1,100),CorrelationId=string.IsNullOrWhiteSpace(request.CorrelationId)?$"intelligence-search:{Guid.NewGuid():N}":request.CorrelationId.Trim()};
        var quickMode=request.IsQuickSearch;
        var requestedMaximum=quickMode?Math.Clamp(request.MaximumResults,1,12):request.MaximumResults;
        request=request with{MaximumResults=requestedMaximum,IncludeRelatedResults=!quickMode&&request.IncludeRelatedResults,IncludeAiSummary=!quickMode&&request.IncludeAiSummary};
        var configuration=await repository.GetSearchConfigurationAsync(request.TenantId,cancellationToken);
        ValidateWeights(configuration.Weights);
        var intentPatterns=await repository.GetSearchIntentPatternsAsync(request.TenantId,cancellationToken);
        var intent=IntelligenceSearchIntentInterpreter.Interpret(request.Query,intentPatterns);
        if(intent.SourceEngineCode.Equals("NONE",StringComparison.OrdinalIgnoreCase)&&configuration.EnableLlmIntentFallback)
            intent=await TryInterpretIntentWithLlmAsync(request,configuration,intent,cancellationToken);
        var fuzzyQuery=string.IsNullOrWhiteSpace(intent.SearchText)?request.Query:intent.SearchText;
        if(string.IsNullOrWhiteSpace(request.EntityTypeCode)&&!string.IsNullOrWhiteSpace(intent.EntityTypeCode))request=request with{EntityTypeCode=intent.EntityTypeCode,ModuleCode=string.IsNullOrWhiteSpace(request.ModuleCode)?intent.ModuleCode:request.ModuleCode};
        request=request with{EffectiveSearchText=fuzzyQuery};
        var useSemanticExpansion=ShouldRunSemanticExpansion(request,intent,quickMode);
        var expansion=useSemanticExpansion?await queryExpander.ExpandAsync(request.TenantId,request.Query,quickMode?4:20,cancellationToken):new SemanticQueryExpansion([],[]);
        var retrievalMaximum=quickMode?Math.Min(24,Math.Max(request.MaximumResults*2,request.MaximumResults)):Math.Min(100,Math.Max(request.MaximumResults*3,request.MaximumResults));
        var baseResponse=await repository.SearchAsync(request with{MaximumResults=retrievalMaximum},expansion.Concepts,expansion.Terms,cancellationToken);

        var strongBaseResults=baseResponse.Results.Count(result=>result.KeywordScore>=.85m||result.SemanticScore>=.90m);
        var shouldRunFuzzy=ShouldRunFuzzy(request,intent,quickMode,baseResponse.Results.Count,strongBaseResults);
        IReadOnlyList<SearchMatchResult> fuzzy=Array.Empty<SearchMatchResult>();
        IReadOnlyCollection<IntelligenceSearchResultDto> fuzzyDocuments=[];
        if(shouldRunFuzzy)
        {
            var fuzzyMaximum=quickMode?Math.Min(12,Math.Max(request.MaximumResults,request.MaximumResults*2)):Math.Min(100,request.MaximumResults*3);
            fuzzy=await entityMatchingService.SearchAsync(new(){TenantId=request.TenantId,Query=fuzzyQuery,EntityTypeCodes=string.IsNullOrWhiteSpace(request.EntityTypeCode)?[]:[request.EntityTypeCode],GrantedPermissions=request.GrantedPermissions,MaximumResults=fuzzyMaximum,RequestedByUserId=request.UserId,CorrelationId=request.CorrelationId},cancellationToken);
            fuzzyDocuments=await repository.GetAuthorizedSearchDocumentsAsync(request,fuzzy.Select(match=>new IntelligenceSearchEntityKey(match.EntityTypeCode,match.EntityId)).ToArray(),cancellationToken);
        }
        var candidates=Merge(baseResponse.Results,fuzzyDocuments,fuzzy);
        if(configuration.EnableRelationships&&request.IncludeRelatedResults&&ShouldRunRelationships(intent,candidates))
        {
            var sources=candidates.OrderByDescending(candidate=>candidate.KeywordScore+candidate.SemanticScore+candidate.FuzzyScore).Take(10).Select(candidate=>new IntelligenceSearchEntityKey(candidate.EntityTypeCode,candidate.EntityId)).ToArray();
            candidates=Merge(candidates,await repository.GetRelatedSearchDocumentsAsync(request,sources,configuration.MaximumRelationshipResults,cancellationToken),[]);
        }
        if(configuration.EnableRules&&!quickMode&&!intent.IsEntityList&&!IsSingleAccountRoleIntent(intent))candidates=await ApplyRulesAsync(request,candidates,cancellationToken);
        if(quickMode)candidates=candidates.Where(candidate=>!string.IsNullOrWhiteSpace(candidate.NavigationRoute)).ToList();
        var maximumResults=IsSingleAccountRoleIntent(intent)?1:request.MaximumResults;
        candidates=candidates.Select(candidate=>Score(candidate,configuration)).Where(candidate=>candidate.CombinedScore>=configuration.MinimumUnifiedScore).OrderByDescending(candidate=>candidate.CombinedScore).ThenBy(candidate=>candidate.Title).Take(maximumResults).ToList();

        var summaryStatus="NOT_REQUESTED";
        string? summary=null;
        Guid? summaryExecutionId=null;
        if(request.IncludeAiSummary&&configuration.EnableAiSummary&&candidates.Count>0)
        {
            try
            {
                var grounding=string.Join('\n',candidates.Take(8).Select((result,index)=>$"[{index+1}] {result.Title}: {result.Excerpt}"));
                var generated=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_SEARCH_SUMMARY",await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.SearchSummary,cancellationToken),$"Query: {request.Query}\nAuthorized evidence:\n{grounding}",null,request.CorrelationId,new("Intelligence",request.EntityTypeCode,null,request.Query,"SEARCH_QUERY",baseResponse.SearchQueryId,request.CorrelationId,"Unified Intelligence Search"),cancellationToken:cancellationToken);
                summary=generated.Content;
                summaryStatus="COMPLETED";
            }
            catch(Exception exception) when(exception is AiProviderUnavailableException or TimeoutException)
            {
                summaryStatus="UNAVAILABLE";
            }
        }
        timer.Stop();
        await repository.CompleteUnifiedSearchAsync(request.TenantId,request.UserId,baseResponse.SearchQueryId,request.Query.ToLowerInvariant(),configuration.Weights,candidates,summaryStatus,summaryExecutionId,timer.ElapsedMilliseconds,cancellationToken);
        return baseResponse with{Results=candidates,DurationMilliseconds=timer.ElapsedMilliseconds,NormalizedQuery=request.Query.ToLowerInvariant(),EffectiveWeights=configuration.Weights,GroundedSummary=summary,SummaryStatusCode=summaryStatus,SummaryExecutionId=summaryExecutionId};
    }

    public async Task<PoloxiSearchResponse> SearchWithPoloxiAsync(PoloxiSearchRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);
        if(request.UserId==Guid.Empty)throw new UnauthorizedAccessException("An authenticated user is required for POLOXI search.");
        var timer=Stopwatch.StartNew();
        var normalizedQuery=NormalizeQuery(request.Query).ToLowerInvariant();
        request=request with{Query=NormalizeQuery(request.Query),MaximumResults=Math.Clamp(request.MaximumResults,1,100),CorrelationId=string.IsNullOrWhiteSpace(request.CorrelationId)?$"poloxi-search:{Guid.NewGuid():N}":request.CorrelationId.Trim()};
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
        var ranked=evidence.GroupBy(item=>$"{item.EntityTypeCode}:{item.EntityId:D}",StringComparer.OrdinalIgnoreCase).Select(group=>
        {
            var first=group.OrderByDescending(item=>item.RelevanceScore).First();
            var branchNames=group.SelectMany(item=>item.MatchedBranches).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var score=Math.Clamp(group.Max(item=>item.RelevanceScore)+Math.Min(.20m,(branchNames.Length-1)*.05m),0,1);
            return first with{RelevanceScore=score,MatchedBranches=branchNames};
        }).OrderByDescending(item=>item.RelevanceScore).ThenBy(item=>item.Title).Take(Math.Min(request.MaximumResults,configuration.MaximumResults)).Select((item,index)=>item with{RankNumber=index+1}).ToArray();
        string? explanation=null;
        var explanationStatus="NOT_REQUESTED";
        if(request.IncludeExplanation&&ranked.Length>0)
        {
            try
            {
                var grounding=string.Join('\n',ranked.Take(12).Select((item,index)=>$"[{index+1}] {item.Title} ({string.Join(", ",item.MatchedBranches)}): {item.Excerpt}"));
                var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_POLOXI_EXPLANATION",await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.PoloxiExplanation,cancellationToken),$"Question: {request.Query}\nValidated concept: {hierarchy.DisplayName}\nEvidence:\n{grounding}",null,request.CorrelationId,new("Intelligence",null,null,request.Query,"POLOXI_EVIDENCE",executionId,request.CorrelationId,"Intelligent Search Using POLOXI"),cancellationToken:cancellationToken);
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
        var ranked=evidence.GroupBy(item=>$"{item.EntityTypeCode}:{item.EntityId:D}",StringComparer.OrdinalIgnoreCase).Select(group=>
        {
            var first=group.OrderByDescending(item=>item.RelevanceScore).First();
            var branchNames=group.SelectMany(item=>item.MatchedBranches).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            var score=Math.Clamp(group.Max(item=>item.RelevanceScore)+Math.Min(.20m,(branchNames.Length-1)*.05m),0,1);
            return first with{RelevanceScore=score,MatchedBranches=branchNames};
        }).OrderByDescending(item=>item.RelevanceScore).ThenBy(item=>item.Title).Take(Math.Min(request.MaximumResults,configuration.MaximumResults)).Select((item,index)=>item with{RankNumber=index+1}).ToArray();
        timer.Stop();
        return new(Guid.Empty,Guid.Empty,request.Query,"DIRECT_SEARCH","Direct authorized search (POLOXI engine off)",0,false,1m,branches,ranked,null,"NOT_REQUESTED",timer.ElapsedMilliseconds);
    }

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
        var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_POLOXI_HIERARCHY",await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.PoloxiHierarchy,cancellationToken),$"Question: {request.Query}\nMaximum branches: {configuration.MaximumBranches}\nApproved capability catalog:\n{catalog}",schema,request.CorrelationId,new("Intelligence",null,null,request.Query,"POLOXI_HIERARCHY",null,request.CorrelationId,"Intelligent Search Using POLOXI"),cancellationToken:cancellationToken);
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
    public async Task<QuickSearchFastPathResponse> QuickSearchFastPathAsync(QuickSearchRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);
        if(request.UserId==Guid.Empty)throw new UnauthorizedAccessException("An authenticated user is required for Quick Search.");
        var timer=Stopwatch.StartNew();
        var query=NormalizeQuery(request.Query);
        var maximumResults=Math.Clamp(request.MaximumResults,1,12);
        var correlationId=string.IsNullOrWhiteSpace(request.CorrelationId)?$"quick-search:{Guid.NewGuid():N}":request.CorrelationId.Trim();
        var searchRequest=new IntelligenceSearchRequest(request.TenantId,request.UserId,query,null,null,maximumResults,correlationId)
        {
            IncludeAiSummary=false,
            IncludeRelatedResults=true,
            IsQuickSearch=true,
            GrantedPermissions=request.GrantedPermissions
        };
        var configuration=await repository.GetSearchConfigurationAsync(request.TenantId,cancellationToken);
        ValidateWeights(configuration.Weights);

        var retrievalMaximum=Math.Min(24,maximumResults*3);
        var baseSearchTask=repository.SearchAsync(searchRequest with{MaximumResults=retrievalMaximum},[],[],cancellationToken);
        var fastMatchesTask=entityMatchingService.SearchFastAsync(new(){TenantId=request.TenantId,Query=query,GrantedPermissions=request.GrantedPermissions,MaximumResults=retrievalMaximum,RequestedByUserId=request.UserId,CorrelationId=correlationId},cancellationToken);
        await Task.WhenAll(baseSearchTask,fastMatchesTask);
        var baseResponse=await baseSearchTask;
        var fastMatches=await fastMatchesTask;
        var fastDocuments=await repository.GetAuthorizedSearchDocumentsAsync(searchRequest,fastMatches.Select(match=>new IntelligenceSearchEntityKey(match.EntityTypeCode,match.EntityId)).ToArray(),cancellationToken);
        var candidates=Merge(baseResponse.Results,fastDocuments,fastMatches);
        var fastQuality=candidates.Count==0?0:candidates.Max(candidate=>Math.Max(candidate.KeywordScore,candidate.FuzzyScore));
        var poorFastPath=candidates.Count<configuration.QuickSearchFastPathMinimumResults||fastQuality<configuration.QuickSearchFastPathMinimumScore;
        candidates=candidates.Where(candidate=>!string.IsNullOrWhiteSpace(candidate.NavigationRoute)).Select(candidate=>Score(candidate,configuration)).Where(candidate=>candidate.CombinedScore>=configuration.MinimumUnifiedScore).OrderByDescending(candidate=>candidate.CombinedScore).ThenBy(candidate=>candidate.Title).Take(maximumResults).ToList();
        timer.Stop();
        await repository.CompleteUnifiedSearchAsync(request.TenantId,request.UserId,baseResponse.SearchQueryId,query.ToLowerInvariant(),configuration.Weights,candidates,"NOT_REQUESTED",null,timer.ElapsedMilliseconds,cancellationToken);
        return new(ToQuickSearchResponse(baseResponse.SearchQueryId,query,candidates,timer.ElapsedMilliseconds),configuration.EnableQuickSearchIntelligentFallback&&poorFastPath);
    }
    public async Task<QuickSearchResponse> QuickSearchIntelligentFallbackAsync(QuickSearchRequest request,CancellationToken cancellationToken=default)
    {
        Validate(request);
        if(request.UserId==Guid.Empty)throw new UnauthorizedAccessException("An authenticated user is required for Quick Search.");
        var timer=Stopwatch.StartNew();
        var query=NormalizeQuery(request.Query);
        var maximumResults=Math.Clamp(request.MaximumResults,1,12);
        var correlationId=string.IsNullOrWhiteSpace(request.CorrelationId)?$"quick-search-fallback:{Guid.NewGuid():N}":request.CorrelationId.Trim();
        var searchRequest=new IntelligenceSearchRequest(request.TenantId,request.UserId,query,null,null,maximumResults,correlationId)
        {
            IncludeAiSummary=false,
            IncludeRelatedResults=true,
            IsQuickSearch=true,
            GrantedPermissions=request.GrantedPermissions
        };
        var configuration=await repository.GetSearchConfigurationAsync(request.TenantId,cancellationToken);
        ValidateWeights(configuration.Weights);
        var retrievalMaximum=Math.Min(24,maximumResults*3);

        var semanticMatches=await entityMatchingService.SearchAsync(new(){TenantId=request.TenantId,Query=query,GrantedPermissions=request.GrantedPermissions,MaximumResults=retrievalMaximum,RequestedByUserId=request.UserId,CorrelationId=$"{correlationId}:semantic"},cancellationToken);
        var semanticDocuments=await repository.GetAuthorizedSearchDocumentsAsync(searchRequest,semanticMatches.Select(match=>new IntelligenceSearchEntityKey(match.EntityTypeCode,match.EntityId)).ToArray(),cancellationToken);
        var candidates=Merge([],semanticDocuments,semanticMatches);

        var ontology=await queryExpander.ExpandAsync(request.TenantId,query,12,cancellationToken);
        var ontologyResponse=await repository.SearchAsync(searchRequest with{MaximumResults=retrievalMaximum},ontology.Concepts,ontology.Terms,cancellationToken);
        candidates=Merge(candidates,ontologyResponse.Results,[]);

        if(configuration.EnableRelationships&&candidates.Count>0)
        {
            var sources=candidates.OrderByDescending(candidate=>candidate.KeywordScore+candidate.SemanticScore+candidate.FuzzyScore).Take(8).Select(candidate=>new IntelligenceSearchEntityKey(candidate.EntityTypeCode,candidate.EntityId)).ToArray();
            candidates=Merge(candidates,await repository.GetRelatedSearchDocumentsAsync(searchRequest,sources,configuration.MaximumRelationshipResults,cancellationToken),[]);
        }

        var intentPatterns=await repository.GetSearchIntentPatternsAsync(request.TenantId,cancellationToken);
        var intent=IntelligenceSearchIntentInterpreter.Interpret(query,intentPatterns);
        if(configuration.EnableLlmIntentFallback&&intent.SourceEngineCode.Equals("NONE",StringComparison.OrdinalIgnoreCase))
        {
            intent=await TryInterpretIntentWithLlmAsync(searchRequest,configuration,intent,cancellationToken);
            if(!string.Equals(intent.SearchText,query,StringComparison.OrdinalIgnoreCase))
            {
                var interpretedRequest=searchRequest with{EffectiveSearchText=intent.SearchText,EntityTypeCode=intent.EntityTypeCode,ModuleCode=intent.ModuleCode,MaximumResults=retrievalMaximum};
                var interpretedResponse=await repository.SearchAsync(interpretedRequest,[],[],cancellationToken);
                candidates=Merge(candidates,interpretedResponse.Results,[]);
            }
        }

        candidates=candidates.Where(candidate=>!string.IsNullOrWhiteSpace(candidate.NavigationRoute)).Select(candidate=>Score(candidate,configuration)).Where(candidate=>candidate.CombinedScore>=configuration.MinimumUnifiedScore).OrderByDescending(candidate=>candidate.CombinedScore).ThenBy(candidate=>candidate.Title).Take(maximumResults).ToList();
        timer.Stop();
        await repository.CompleteUnifiedSearchAsync(request.TenantId,request.UserId,ontologyResponse.SearchQueryId,query.ToLowerInvariant(),configuration.Weights,candidates,"NOT_REQUESTED",null,timer.ElapsedMilliseconds,cancellationToken);
        return ToQuickSearchResponse(ontologyResponse.SearchQueryId,query,candidates,timer.ElapsedMilliseconds);
    }
    public Task<PagedResult<AiReviewQueueItemDto>> SearchReviewQueueAsync(SearchAiReviewQueueQuery query,CancellationToken cancellationToken=default){ValidatePage(query.TenantId,query.PageNumber,query.PageSize);return repository.SearchReviewQueueAsync(query with{PageSize=Math.Clamp(query.PageSize,1,200)},cancellationToken);}
    public Task DecideReviewAsync(DecideAiReviewRequest request,CancellationToken cancellationToken=default){Validate(request);return repository.DecideReviewAsync(request,cancellationToken);}
    public Task<IReadOnlyCollection<AiEvaluationDefinitionDto>> GetEvaluationDefinitionsAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetEvaluationDefinitionsAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task<IReadOnlyCollection<AiEvaluationRunDto>> GetEvaluationRunsAsync(Guid tenantId,int pageSize,CancellationToken cancellationToken=default)=>repository.GetEvaluationRunsAsync(Required(tenantId,nameof(tenantId)),Math.Clamp(pageSize,1,500),cancellationToken);
    public Task<Guid> QueueEvaluationAsync(QueueAiEvaluationRequest request,CancellationToken cancellationToken=default){Validate(request);if(request.WindowEndUtc<=request.WindowStartUtc)throw new ValidationException("Evaluation window end must be after its start.");return repository.QueueEvaluationAsync(request,cancellationToken);}
    public Task<IntelligenceDashboardDto> GetDashboardAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetDashboardAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task<IntelligencePlatformSummaryDto> GetPlatformSummaryAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetPlatformSummaryAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task<PlatformArchitectureDto> GetPlatformArchitectureAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetPlatformArchitectureAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task<IReadOnlyCollection<IntelligenceEnginePolicyDto>> GetEnginePoliciesAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetEnginePoliciesAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task SaveEnginePolicyAsync(SaveIntelligenceEnginePolicyRequest request,CancellationToken cancellationToken=default){Validate(request);ValidateJson(request.ConfigurationJson,nameof(request.ConfigurationJson));if(request.EffectiveToUtc<=request.EffectiveFromUtc)throw new ValidationException("Effective end must be after effective start.");return repository.SaveEnginePolicyAsync(request,cancellationToken);}
    public Task<IReadOnlyCollection<IntelligenceSafetyControlDto>> GetSafetyControlsAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetSafetyControlsAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task SaveSafetyControlAsync(SaveIntelligenceSafetyControlRequest request,CancellationToken cancellationToken=default){Validate(request);ValidateJson(request.ConfigurationJson,nameof(request.ConfigurationJson));return repository.SaveSafetyControlAsync(request,cancellationToken);}
    public Task<IReadOnlyCollection<IntelligenceComplianceRequirementDto>> GetComplianceRequirementsAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetComplianceRequirementsAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task<IReadOnlyCollection<IntelligenceSafetyEventDto>> GetSafetyEventsAsync(Guid tenantId,int pageSize,CancellationToken cancellationToken=default)=>repository.GetSafetyEventsAsync(Required(tenantId,nameof(tenantId)),Math.Clamp(pageSize,1,500),cancellationToken);
    public Task<IReadOnlyCollection<IntelligencePromptDefinitionDto>> GetPromptDefinitionsAsync(Guid tenantId,CancellationToken cancellationToken=default)=>repository.GetPromptDefinitionsAsync(Required(tenantId,nameof(tenantId)),cancellationToken);
    public Task SavePromptDefinitionAsync(SaveIntelligencePromptDefinitionRequest request,CancellationToken cancellationToken=default){Validate(request);ValidateJson(request.InputSchemaJson,nameof(request.InputSchemaJson));ValidateJson(request.OutputSchemaJson,nameof(request.OutputSchemaJson));if(request.EffectiveToUtc<=request.EffectiveFromUtc)throw new ValidationException("Effective end must be after effective start.");return repository.SavePromptDefinitionAsync(request,cancellationToken);}
    public Task DeletePromptDefinitionAsync(Guid tenantId,string promptCode,string versionLabel,Guid actorUserId,CancellationToken cancellationToken=default){if(string.IsNullOrWhiteSpace(promptCode)||string.IsNullOrWhiteSpace(versionLabel))throw new ValidationException("Prompt code and version label are required.");return repository.DeletePromptDefinitionAsync(tenantId,promptCode,versionLabel,actorUserId,cancellationToken);}
    public Task SubmitEvaluationSampleLabelAsync(SubmitEvaluationSampleLabelRequest request,CancellationToken cancellationToken=default){Validate(request);return repository.SubmitEvaluationSampleLabelAsync(request,cancellationToken);}
    public Task<PagedResult<IntelligenceFindingDto>> SearchFindingsAsync(SearchIntelligenceFindingsQuery query,CancellationToken cancellationToken=default){ValidatePage(query.TenantId,query.PageNumber,query.PageSize);return repository.SearchFindingsAsync(query with{PageSize=Math.Clamp(query.PageSize,1,200)},cancellationToken);}
    public Task<IntelligenceFindingDetailDto?> GetFindingAsync(Guid tenantId,Guid findingId,CancellationToken cancellationToken=default)=>repository.GetFindingAsync(Required(tenantId,nameof(tenantId)),Required(findingId,nameof(findingId)),cancellationToken);
    public Task DecideFindingAsync(DecideIntelligenceFindingRequest request,CancellationToken cancellationToken=default){Validate(request);return repository.DecideFindingAsync(request,cancellationToken);}
    public Task<EntityRelationshipGraphDto> GetRelationshipGraphAsync(RelationshipQuery query,CancellationToken cancellationToken=default){Validate(query);return repository.GetRelationshipGraphAsync(query with{MaximumDepth=Math.Clamp(query.MaximumDepth,1,10)},cancellationToken);}
    public Task<IReadOnlyCollection<EntitySimilarityDto>> GetSimilarEntitiesAsync(SimilarityQuery query,CancellationToken cancellationToken=default){Validate(query);return repository.GetSimilarEntitiesAsync(query with{MaximumResults=Math.Clamp(query.MaximumResults,1,100)},cancellationToken);}
    public Task<PagedResult<BusinessIntelligenceSignalDto>> SearchBusinessSignalsAsync(SearchBusinessIntelligenceSignalsQuery query,CancellationToken cancellationToken=default){ValidatePage(query.TenantId,query.PageNumber,query.PageSize);return repository.SearchBusinessSignalsAsync(query with{PageSize=Math.Clamp(query.PageSize,1,200)},cancellationToken);}
    public Task DecideBusinessSignalAsync(DecideBusinessIntelligenceSignalRequest request,CancellationToken cancellationToken=default){Validate(request);return repository.DecideBusinessSignalAsync(request,cancellationToken);}
    public async Task<InsuranceReasoningResponse> ExecuteReasoningAsync(InsuranceReasoningRequest request,CancellationToken cancellationToken=default){Validate(request);if(!request.GrantedPermissions.Contains("Intelligence.Reason",StringComparer.OrdinalIgnoreCase)&&!request.GrantedPermissions.Contains("NAV_ALL",StringComparer.OrdinalIgnoreCase))throw new ValidationException("Insurance reasoning permission is required.");var expansion=await queryExpander.ExpandAsync(request.TenantId,request.Question,20,cancellationToken);return await repository.ExecuteReasoningAsync(request,expansion.Concepts,cancellationToken);}
    public Task<InsuranceReasoningResponse?> GetReasoningSessionAsync(Guid tenantId,Guid userId,Guid reasoningSessionId,CancellationToken cancellationToken=default)=>repository.GetReasoningSessionAsync(Required(tenantId,nameof(tenantId)),Required(userId,nameof(userId)),Required(reasoningSessionId,nameof(reasoningSessionId)),cancellationToken);

    private async Task<List<IntelligenceSearchResultDto>> ApplyRulesAsync(IntelligenceSearchRequest request,IReadOnlyCollection<IntelligenceSearchResultDto> candidates,CancellationToken cancellationToken)
    {
        var results=new List<IntelligenceSearchResultDto>(candidates.Count);
        foreach(var candidate in candidates)
        {
            try
            {
                using var facts=JsonDocument.Parse(JsonSerializer.Serialize(new{candidate.Title,candidate.ModuleCode,candidate.EntityTypeCode,candidate.KeywordScore,candidate.SemanticScore,candidate.FuzzyScore,candidate.RelationshipScore,candidate.IsRelatedResult,statusCode=DetectStatus(candidate)}));
                var evaluation=await rulesPlatformService.EvaluateAsync(new(request.TenantId,"INTELLIGENCE_SEARCH_RESULT",candidate.EntityId,candidate.ModuleCode,request.CorrelationId,facts.RootElement.Clone(),request.UserId),cancellationToken);
                var matched=evaluation.Results.Where(result=>result.IsMatch==true&&result.StatusCode.Equals("COMPLETED",StringComparison.OrdinalIgnoreCase)).ToArray();
                if(matched.Length==0)
                {
                    results.Add(candidate);
                    continue;
                }
                var priority=matched.Select(result=>ReadDecimal(result.Outcome,"businessPriorityScore")).DefaultIfEmpty(0).Max();
                var explanations=candidate.Explanations.Concat(matched.Select(result=>new IntelligenceSearchMatchExplanationDto("BUSINESS_RULE_BOOST","Business-rule boost",ReadString(result.Outcome,"explanation")??$"Matched tenant-effective rule {result.RuleCode}.",priority,"RULES_PLATFORM"))).ToArray();
                results.Add(candidate with{BusinessPriorityScore=Math.Clamp(priority,0,1),Explanations=explanations});
            }
            catch(InvalidOperationException)
            {
                results.Add(candidate);
            }
        }
        return results;
    }

    private static List<IntelligenceSearchResultDto> Merge(IReadOnlyCollection<IntelligenceSearchResultDto> current,IReadOnlyCollection<IntelligenceSearchResultDto> incoming,IReadOnlyCollection<SearchMatchResult> fuzzy)
    {
        var fuzzyByEntity=fuzzy.GroupBy(result=>EntityKey(result.EntityTypeCode,result.EntityId)).ToDictionary(group=>group.Key,group=>group.OrderByDescending(result=>result.Score).First());
        var merged=new Dictionary<string,IntelligenceSearchResultDto>(StringComparer.OrdinalIgnoreCase);
        foreach(var candidate in current.Concat(incoming))
        {
            var key=EntityKey(candidate.EntityTypeCode,candidate.EntityId);
            fuzzyByEntity.TryGetValue(key,out var fuzzyResult);
            var fuzzyScore=fuzzyResult is null?candidate.FuzzyScore:Math.Clamp(fuzzyResult.Score/100m,0,1);
            var fuzzyExplanations=fuzzyResult?.Reasons.Select(ToExplanation)??[];
            var enriched=candidate with
            {
                FuzzyScore=Math.Max(candidate.FuzzyScore,fuzzyScore),
                NavigationRoute=candidate.NavigationRoute??fuzzyResult?.NavigationRoute,
                Explanations=DistinctExplanations(candidate.Explanations.Concat(fuzzyExplanations))
            };
            if(!merged.TryGetValue(key,out var existing))merged[key]=enriched;
            else merged[key]=existing with
            {
                KeywordScore=Math.Max(existing.KeywordScore,enriched.KeywordScore),
                SemanticScore=Math.Max(existing.SemanticScore,enriched.SemanticScore),
                FuzzyScore=Math.Max(existing.FuzzyScore,enriched.FuzzyScore),
                RelationshipScore=Math.Max(existing.RelationshipScore,enriched.RelationshipScore),
                RecencyScore=Math.Max(existing.RecencyScore,enriched.RecencyScore),
                BusinessPriorityScore=Math.Max(existing.BusinessPriorityScore,enriched.BusinessPriorityScore),
                IsRelatedResult=existing.IsRelatedResult||enriched.IsRelatedResult,
                NavigationRoute=existing.NavigationRoute??enriched.NavigationRoute,
                Concepts=existing.Concepts.Concat(enriched.Concepts).DistinctBy(concept=>concept.ConceptId).ToArray(),
                Explanations=DistinctExplanations(existing.Explanations.Concat(enriched.Explanations))
            };
        }
        return merged.Values.ToList();
    }

    private static string EntityKey(string entityTypeCode,Guid entityId)=>$"{entityTypeCode.Trim().ToUpperInvariant()}:{entityId:D}";

    private static IntelligenceSearchResultDto Score(IntelligenceSearchResultDto candidate,IntelligenceSearchConfiguration configuration)
    {
        var weights=configuration.Weights;
        var total=weights.TotalWeight;
        var unified=(candidate.KeywordScore*weights.KeywordWeight+candidate.SemanticScore*weights.SemanticWeight+candidate.FuzzyScore*weights.FuzzyWeight+candidate.RelationshipScore*weights.RelationshipWeight+candidate.RecencyScore*weights.RecencyWeight+candidate.BusinessPriorityScore*weights.BusinessPriorityWeight)/total;
        return candidate with{CombinedScore=Math.Round(Math.Clamp(unified,0,1),6)};
    }

    private static IntelligenceSearchMatchExplanationDto ToExplanation(MatchReason reason)
    {
        var displayName=reason.AlgorithmCode.ToUpperInvariant() switch
        {
            "SOUNDEX" or "METAPHONE"=>"Phonetic match",
            "DAMERAU_LEVENSHTEIN" or "LEVENSHTEIN"=>"Spelling-distance match",
            "TOKEN_OVERLAP" or "JACCARD"=>"Token-overlap match",
            _=>reason.IsExactMatch?"Exact field match":"Fuzzy match"
        };
        var reasonCode=reason.AlgorithmCode.Equals("SOUNDEX",StringComparison.OrdinalIgnoreCase)?"PHONETIC_MATCH":reason.ReasonCode;
        return new(reasonCode,displayName,reason.Explanation,Math.Clamp(reason.SimilarityScore/100m,0,1),"SEARCH_MATCHING");
    }

    private static IReadOnlyCollection<IntelligenceSearchMatchExplanationDto> DistinctExplanations(IEnumerable<IntelligenceSearchMatchExplanationDto> explanations)=>explanations.GroupBy(explanation=>(explanation.ReasonCode,explanation.SourceEngineCode,explanation.Explanation)).Select(group=>group.OrderByDescending(explanation=>explanation.Score).First()).ToArray();
    private static QuickSearchResponse ToQuickSearchResponse(Guid searchQueryId,string query,IReadOnlyCollection<IntelligenceSearchResultDto> results,long durationMilliseconds)
        =>new(searchQueryId,query,results.Select(result=>new QuickSearchResultDto(result.SearchDocumentId,result.EntityTypeCode,result.EntityId,result.ModuleCode,result.Title,result.NavigationRoute!,result.CombinedScore)).ToArray(),durationMilliseconds);
    private static string NormalizeQuery(string query)=>string.Join(' ',query.Trim().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
    private static string? OptionalFilter(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static bool ShouldRunSemanticExpansion(IntelligenceSearchRequest request,IntelligenceSearchIntent intent,bool quickMode)
    {
        if(intent.IsEntityList)return false;
        if(!string.Equals(intent.SearchText,request.Query,StringComparison.OrdinalIgnoreCase))return false;
        var tokenCount=request.Query.Split(' ',StringSplitOptions.RemoveEmptyEntries|StringSplitOptions.TrimEntries).Length;
        return !quickMode||tokenCount>=4;
    }
    private static bool ShouldRunFuzzy(IntelligenceSearchRequest request,IntelligenceSearchIntent intent,bool quickMode,int baseResultCount,int strongBaseResultCount)
    {
        if(IsSingleAccountRoleIntent(intent))return false;
        if(intent.IsEntityList&&baseResultCount>0)return false;
        if(quickMode&&strongBaseResultCount>=request.MaximumResults)return false;
        return baseResultCount<request.MaximumResults||strongBaseResultCount==0||!string.IsNullOrWhiteSpace(request.EntityTypeCode);
    }
    private static bool ShouldRunRelationships(IntelligenceSearchIntent intent,IReadOnlyCollection<IntelligenceSearchResultDto> candidates)=>!IsSingleAccountRoleIntent(intent)&&!intent.IsEntityList&&candidates.Count>0;
    private static bool IsSingleAccountRoleIntent(IntelligenceSearchIntent intent)=>IsPrimaryContactIntent(intent)||intent.PatternCode?.Equals("PRODUCER_FOR_ACCOUNT",StringComparison.OrdinalIgnoreCase)==true;
    private static bool IsPrimaryContactIntent(IntelligenceSearchIntent intent)=>intent.PatternCode?.Equals("PRIMARY_CONTACT_FOR_ACCOUNT",StringComparison.OrdinalIgnoreCase)==true;
    private async Task<IntelligenceSearchIntent> TryInterpretIntentWithLlmAsync(IntelligenceSearchRequest request,IntelligenceSearchConfiguration configuration,IntelligenceSearchIntent fallbackIntent,CancellationToken cancellationToken)
    {
        using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(configuration.LlmIntentTimeoutSeconds));
        using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,timeout.Token);
        try
        {
            var schema="""{"type":"object","properties":{"entityTypeCode":{"type":["string","null"]},"moduleCode":{"type":["string","null"]},"searchText":{"type":"string"},"isEntityList":{"type":"boolean"},"confidence":{"type":"number"}},"required":["searchText","isEntityList","confidence"]}""";
            var allowedEntities="Account, Contact, Lead, Submission, Policy, Claim, Document, Certificate, Carrier, Producer, Location, Vehicle, ClaimParty, CommissionLine";
            var prompt=$"Convert this AMS search query to structured JSON only. Allowed entityTypeCode values: {allowedEntities}. Allowed moduleCode values: Client, CRM, Submissions, Claims, DMS, Agency, Commission. Do not infer authorization. Query: {request.Query}";
            var result=await aiProviderRouter.GenerateAsync(request.TenantId,"INTELLIGENCE_SEARCH_INTENT",await promptCatalog.GetSystemPromptAsync(request.TenantId,IntelligencePromptCodes.SearchIntent,cancellationToken),prompt,schema,request.CorrelationId,new("Intelligence",null,null,request.Query,"SEARCH_INTENT",null,request.CorrelationId,"LLM search intent interpretation"),cancellationToken:linked.Token);
            var interpreted=ParseLlmIntent(result.Content,request.Query,configuration.LlmIntentMinimumConfidence);
            await repository.RecordSearchIntentInterpretationAsync(new(request.TenantId,request.UserId,request.Query,interpreted.EntityTypeCode,interpreted.ModuleCode,interpreted.SearchText,"LLM",interpreted.Confidence,"ACCEPTED",null,request.CorrelationId),cancellationToken);
            return interpreted;
        }
        catch(Exception exception) when(!cancellationToken.IsCancellationRequested)
        {
            await repository.RecordSearchIntentInterpretationAsync(new(request.TenantId,request.UserId,request.Query,null,null,request.Query,"LLM",0,"REJECTED",exception.Message,request.CorrelationId),cancellationToken);
            return fallbackIntent;
        }
    }
    private static IntelligenceSearchIntent ParseLlmIntent(string content,string originalQuery,decimal minimumConfidence)
    {
        using var document=JsonDocument.Parse(content);
        var root=document.RootElement;
        var confidence=ReadDecimal(root,"confidence");
        if(confidence<minimumConfidence)throw new ValidationException("LLM intent confidence was below the configured threshold.");
        var entity=ReadString(root,"entityTypeCode");
        var module=ReadString(root,"moduleCode");
        if(!IsAllowedEntity(entity)||!IsAllowedModule(module))throw new ValidationException("LLM intent contained an unsupported entity or module.");
        var searchText=ReadString(root,"searchText")?.Trim();
        if(string.IsNullOrWhiteSpace(searchText))searchText=originalQuery;
        var isList=root.TryGetProperty("isEntityList",out var listElement)&&listElement.ValueKind==JsonValueKind.True;
        return new(entity,module,false,isList,searchText,"LLM",confidence);
    }
    private static bool IsAllowedEntity(string? value)=>string.IsNullOrWhiteSpace(value)||new[]{"Account","Contact","Lead","Submission","Policy","Claim","Document","Certificate","Carrier","Producer","Location","Vehicle","ClaimParty","CommissionLine"}.Contains(value,StringComparer.OrdinalIgnoreCase);
    private static bool IsAllowedModule(string? value)=>string.IsNullOrWhiteSpace(value)||new[]{"Client","CRM","Submissions","Claims","DMS","Agency","Commission"}.Contains(value,StringComparer.OrdinalIgnoreCase);
    private static void ValidateWeights(IntelligenceSearchWeightsDto weights){if(weights.KeywordWeight<0||weights.SemanticWeight<0||weights.FuzzyWeight<0||weights.RelationshipWeight<0||weights.RecencyWeight<0||weights.BusinessPriorityWeight<0||weights.TotalWeight<=0)throw new ValidationException("Unified search weights must be non-negative and have a positive total.");}
    private static string? DetectStatus(IntelligenceSearchResultDto candidate)=>new[]{"OPEN","ACTIVE","PENDING","IN_REVIEW"}.FirstOrDefault(status=>$" {candidate.Excerpt} ".Contains($" {status} ",StringComparison.OrdinalIgnoreCase));
    private static decimal ReadDecimal(JsonElement element,string propertyName)=>element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(propertyName,out var value)&&value.TryGetDecimal(out var result)?result:0;
    private static string? ReadString(JsonElement element,string propertyName)=>element.ValueKind==JsonValueKind.Object&&element.TryGetProperty(propertyName,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString():null;
    private static Guid Required(Guid value,string name)=>value==Guid.Empty?throw new ValidationException($"{name} is required."):value;
    private static void ValidatePage(Guid tenantId,int pageNumber,int pageSize){Required(tenantId,nameof(tenantId));if(pageNumber<1||pageSize<1)throw new ValidationException("Page number and page size must be positive.");}
    private static void Validate(object request){var context=new ValidationContext(request);Validator.ValidateObject(request,context,true);foreach(var property in request.GetType().GetProperties().Where(x=>x.PropertyType==typeof(Guid))){if((Guid)(property.GetValue(request)??Guid.Empty)==Guid.Empty)throw new ValidationException($"{property.Name} is required.");}}
    private static void ValidateJson(string value,string name){try{using var _=System.Text.Json.JsonDocument.Parse(value);}catch(System.Text.Json.JsonException ex){throw new ValidationException($"{name} must contain valid JSON.",ex);}}
}
