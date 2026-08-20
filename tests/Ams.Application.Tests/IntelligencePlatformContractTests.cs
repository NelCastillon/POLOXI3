using System.Data;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.Intelligence;
using Ams.Application.Services;
using Ams.Infrastructure.Persistence;
using Ams.Infrastructure.Persistence.Repositories;
using Ams.Web.Services;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Ams.Application.Tests;

public sealed class IntelligencePlatformContractTests
{
    [Fact]
    public void IntelligentSearchUsingPoloxi_DefinesGovernedDatabaseApiAndUiFlow()
    {
        var assembly=typeof(DatabaseMigrator).Assembly;
        var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0139_IntelligentSearchPoloxi.sql",StringComparison.Ordinal));
        var sql=Read(assembly,resource);
        foreach(var table in new[]{"POLOXI.Capability","POLOXI.Hierarchy","POLOXI.HierarchyBranch","POLOXI.Execution","POLOXI.ExecutionEvidence"})Assert.Contains(table,sql,StringComparison.OrdinalIgnoreCase);
        foreach(var feature in new[]{"INTELLIGENCE_POLOXI_HIERARCHY","INTELLIGENCE_POLOXI_EXPLANATION"})Assert.Contains(feature,sql,StringComparison.Ordinal);
        Assert.Contains("AUTHORIZED_SEARCH_DOCUMENT",sql,StringComparison.Ordinal);
        Assert.Contains("TenantId",sql,StringComparison.Ordinal);
        Assert.Contains("CreatedDateUtc",sql,StringComparison.Ordinal);
        Assert.Contains("IsDeleted",sql,StringComparison.Ordinal);

        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));
        var service=File.ReadAllText(Path.Combine(root,"src","Ams.Application","IntelligenceService.cs"));
        var repository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","IntelligenceRepository.Poloxi.cs"));
        var controller=File.ReadAllText(Path.Combine(root,"src","Ams.Api","Controllers","IntelligenceController.cs"));
        var client=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Services","ApiClients.Intelligence.cs"));
        var page=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Components","Pages","Intelligence","IntelligenceSearchPoloxi.razor"));
        var navigation=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Components","Layout","NavSidebar.razor"));
        Assert.Contains("INTELLIGENCE_POLOXI_HIERARCHY",service,StringComparison.Ordinal);
        Assert.Contains("INTELLIGENCE_POLOXI_EXPLANATION",service,StringComparison.Ordinal);
        Assert.Contains("ValidationStatusCode.Equals(\"VALID\"",service,StringComparison.Ordinal);
        Assert.Contains("AI.SearchPermission",repository,StringComparison.Ordinal);
        Assert.Contains("permission.PrincipalTypeCode=N'USER'",repository,StringComparison.Ordinal);
        Assert.Contains("HttpPost(\"search/poloxi\")",controller,StringComparison.Ordinal);
        Assert.Contains("IntelligentSearchUsingPoloxiAsync",client,StringComparison.Ordinal);
        Assert.Contains("@page \"/intelligence/search/poloxi\"",page,StringComparison.Ordinal);
        Assert.Contains("Intelligent Search Using POLOXI",navigation,StringComparison.Ordinal);
        Assert.DoesNotContain("ExecuteSql",service,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void IntelligenceSearchAiRouteMigration_BackfillsGovernedIntentAndSummaryPolicies()
    {
        var assembly=typeof(DatabaseMigrator).Assembly;
        var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0138_IntelligenceSearchAiRouteSeed.sql",StringComparison.Ordinal));
        var sql=Read(assembly,resource);
        Assert.Contains("INTELLIGENCE_SEARCH_INTENT",sql,StringComparison.Ordinal);
        Assert.Contains("INTELLIGENCE_SEARCH_SUMMARY",sql,StringComparison.Ordinal);
        Assert.Contains("AI:AzureOpenAI:Endpoint",sql,StringComparison.Ordinal);
        Assert.Contains("Intelligence.Search.EnableLlmIntentFallback",sql,StringComparison.Ordinal);
        Assert.Contains("Intelligence.Search.EnableAiSummary",sql,StringComparison.Ordinal);
        Assert.Contains("model.CapabilityCode=N'CHAT'",sql,StringComparison.Ordinal);
        Assert.Contains("model.TenantId=tenant.TenantId OR model.TenantId IS NULL",sql,StringComparison.Ordinal);
        Assert.Contains("MERGE AI.FeaturePolicy",sql,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("target.IsDeleted=0",sql,StringComparison.Ordinal);
        Assert.DoesNotContain("INSERT INTO Core.Tenant",sql,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SharedAzureOpenAiRoute_UsesDeployedResourceAndManagedIdentityFallback()
    {
        var assembly=typeof(DatabaseMigrator).Assembly;
        var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0140_ConfigureSharedAzureOpenAiRoute.sql",StringComparison.Ordinal));
        var sql=Read(assembly,resource);
        Assert.Contains("https://agencybinder-1226-resource.cognitiveservices.azure.com/",sql,StringComparison.Ordinal);
        Assert.Contains("AI:AzureOpenAI:Endpoint",sql,StringComparison.Ordinal);
        Assert.Contains("AI:AzureOpenAI:Credential",sql,StringComparison.Ordinal);
        Assert.Contains("SettingValue=N''",sql,StringComparison.Ordinal);
        Assert.Contains("DefaultAzureCredential",sql,StringComparison.Ordinal);
        Assert.DoesNotContain("api-key",sql,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Migration_DefinesCompleteTenantAwarePlatformWithoutFabricatedEvidence()
    {
        var assembly=typeof(DatabaseMigrator).Assembly;var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0081_EnterpriseIntelligencePlatform.sql",StringComparison.Ordinal));var sql=Read(assembly,resource);
        foreach(var table in new[]{"AI.Provider","AI.ModelDeployment","AI.FeaturePolicy","AI.Execution","AI.ExecutionGroundingSource","AI.ExecutionFeedback","AI.Recommendation","AI.RecommendationWorkItem","AI.SearchDocument","AI.SearchPermission","AI.SearchQuery","AI.ReviewQueueItem","AI.EvaluationDefinition","AI.EvaluationRun","AI.ModuleMetricSnapshot"})Assert.Contains(table,sql,StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MERGE AI.Provider",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("MERGE AI.RecommendationType",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("MERGE AI.EvaluationDefinition",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("MERGE Core.ConfigurationSetting",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("INSERT(SettingId,TenantId,ScopeCode",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("ConfigurationSettingId",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("LeaseExpiresDateUtc",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("WHERE existing.PermissionCode=source.PermissionCode",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("role.TenantId,role.RoleId,permission.PermissionId",sql,StringComparison.OrdinalIgnoreCase);Assert.Contains("Intelligence.Audit.Read",sql,StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CREATE FULLTEXT",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("INSERT AI.Execution(",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("INSERT AI.Recommendation(",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("INSERT AI.EvaluationRun(",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("INSERT INTO Core.Tenant",sql,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void UnifiedSearchExplanationMigration_ActivatesSoundexAndDocumentFieldEvidence()
    {
        var assembly=typeof(DatabaseMigrator).Assembly;
        var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0096_UnifiedSearchExplanationCompletion.sql",StringComparison.Ordinal));
        var sql=Read(assembly,resource);
        Assert.Contains("GLOBAL_ENTERPRISE_SEARCH",sql,StringComparison.Ordinal);
        Assert.Contains("SOUNDEX",sql,StringComparison.Ordinal);
        Assert.Contains("Display Name Phonetic",sql,StringComparison.Ordinal);
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));
        var repository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","IntelligenceRepository.cs"));
        Assert.Contains("DMS.IntakeDraftField",repository,StringComparison.Ordinal);
        Assert.Contains("EXTRACTED_DOCUMENT_FIELD",repository,StringComparison.Ordinal);
        Assert.Contains("DOCUMENT_INTELLIGENCE",repository,StringComparison.Ordinal);
        Assert.Contains("private sealed class AuthorizedSearchRow",repository,StringComparison.Ordinal);
    }

    [Fact]
    public void UnifiedSearchMigration_DefinesTenantEffectiveWeightsEvidenceAndGovernedRules()
    {
        var assembly=typeof(DatabaseMigrator).Assembly;
        var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0095_UnifiedIntelligenceSearch.sql",StringComparison.Ordinal));
        var sql=Read(assembly,resource);
        foreach(var setting in new[]{"KeywordWeight","SemanticWeight","FuzzyWeight","RelationshipWeight","RecencyWeight","BusinessPriorityWeight"})Assert.Contains($"Intelligence.Search.{setting}",sql,StringComparison.Ordinal);
        Assert.Contains("AI.SearchResultEvidence",sql,StringComparison.Ordinal);
        Assert.Contains("ScoringWeightsJson",sql,StringComparison.Ordinal);
        Assert.Contains("SummaryStatusCode",sql,StringComparison.Ordinal);
        Assert.Contains("INTELLIGENCE_SEARCH.OPEN_WORK_PRIORITY",sql,StringComparison.Ordinal);
        Assert.Contains("businessPriorityScore",sql,StringComparison.Ordinal);
    }

    [Fact]
    public void UnifiedSearchOrchestration_UsesSharedEnginesAuthorizationScoringAndExplanations()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));
        var service=File.ReadAllText(Path.Combine(root,"src","Ams.Application","IntelligenceService.cs"));
        var repository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","IntelligenceRepository.cs"));
        Assert.Contains("queryExpander.ExpandAsync",service,StringComparison.Ordinal);
        Assert.Contains("entityMatchingService.SearchAsync",service,StringComparison.Ordinal);
        Assert.DoesNotContain("catch(InvalidOperationException)\n        {\n            fuzzy=[]",service,StringComparison.Ordinal);
        Assert.Contains("rulesPlatformService.EvaluateAsync",service,StringComparison.Ordinal);
        Assert.Contains("aiProviderRouter.GenerateAsync",service,StringComparison.Ordinal);
        Assert.Contains("AiProviderUnavailableException or TimeoutException",service,StringComparison.Ordinal);
        Assert.Contains("GetRelatedSearchDocumentsAsync",service,StringComparison.Ordinal);
        Assert.Contains("weights.FuzzyWeight",service,StringComparison.Ordinal);
        Assert.Contains("EntityKey(result.EntityTypeCode,result.EntityId)",service,StringComparison.Ordinal);
        Assert.Contains("ToUpperInvariant()",service,StringComparison.Ordinal);
        Assert.Contains("BUSINESS_RULE_BOOST",service,StringComparison.Ordinal);
        Assert.Contains("SEARCH_MATCHING",service,StringComparison.Ordinal);
        Assert.Contains("permission.PrincipalTypeCode=N'USER'",repository,StringComparison.Ordinal);
        Assert.Contains("AI.SearchResultEvidence",repository,StringComparison.Ordinal);
        Assert.Contains("CONVERT(bit,COALESCE(TRY_CONVERT(bit",repository,StringComparison.Ordinal);
        Assert.Contains("'$.EntityTypeCode'",repository,StringComparison.Ordinal);
        Assert.Contains("'$.EntityId'",repository,StringComparison.Ordinal);
        var controller=File.ReadAllText(Path.Combine(root,"src","Ams.Api","Controllers","IntelligenceController.cs"));
        Assert.Contains("GrantedPermissions=AuthenticatedRequestContext.GetGrantedPermissions(User)",controller,StringComparison.Ordinal);
        var matchingRepository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","SearchMatchingRepository.cs"));
        Assert.Contains("projection.SearchText,projection.NormalizedFieldsJson",matchingRepository,StringComparison.Ordinal);
        Assert.Contains("fields[\"DisplayName\"]=row.DisplayName",matchingRepository,StringComparison.Ordinal);
        Assert.Contains("fields[\"SearchText\"]=row.SearchText",matchingRepository,StringComparison.Ordinal);
        Assert.DoesNotContain("fuzzy=[]",service,StringComparison.Ordinal);
    }

    [Fact]
    public void QuickSearch_UsesDedicatedFastFirstTieredPipelineOnlyFromTopBar()
    {
        var assembly=typeof(DatabaseMigrator).Assembly;
        var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0100_TieredQuickSearch.sql",StringComparison.Ordinal));
        var sql=Read(assembly,resource);
        foreach(var setting in new[]{"Intelligence.QuickSearch.EnableIntelligentFallback","Intelligence.QuickSearch.FastPathMinimumResults","Intelligence.QuickSearch.FastPathMinimumScore"})Assert.Contains(setting,sql,StringComparison.Ordinal);

        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));
        var service=File.ReadAllText(Path.Combine(root,"src","Ams.Application","IntelligenceService.cs"));
        var fastStart=service.IndexOf("QuickSearchFastPathAsync(QuickSearchRequest",StringComparison.Ordinal);
        var fallbackStart=service.IndexOf("QuickSearchIntelligentFallbackAsync(QuickSearchRequest",StringComparison.Ordinal);
        var fallbackEnd=service.IndexOf("SearchReviewQueueAsync",fallbackStart,StringComparison.Ordinal);
        Assert.True(fastStart>=0&&fallbackStart>fastStart&&fallbackEnd>fallbackStart);
        var fastPath=service[fastStart..fallbackStart];
        var fallback=service[fallbackStart..fallbackEnd];
        Assert.Contains("SearchFastAsync",fastPath,StringComparison.Ordinal);
        Assert.Contains("Task.WhenAll(baseSearchTask,fastMatchesTask)",fastPath,StringComparison.Ordinal);
        Assert.Contains("ToQuickSearchResponse",fastPath,StringComparison.Ordinal);
        Assert.Contains("EnableQuickSearchIntelligentFallback&&poorFastPath",fastPath,StringComparison.Ordinal);
        Assert.DoesNotContain("entityMatchingService.SearchAsync",fastPath,StringComparison.Ordinal);
        Assert.DoesNotContain("queryExpander.ExpandAsync",fastPath,StringComparison.Ordinal);
        Assert.DoesNotContain("GetRelatedSearchDocumentsAsync",fastPath,StringComparison.Ordinal);
        Assert.DoesNotContain("TryInterpretIntentWithLlmAsync",fastPath,StringComparison.Ordinal);
        var semanticIndex=fallback.IndexOf("entityMatchingService.SearchAsync",StringComparison.Ordinal);
        var ontologyIndex=fallback.IndexOf("queryExpander.ExpandAsync",StringComparison.Ordinal);
        var relationshipIndex=fallback.IndexOf("GetRelatedSearchDocumentsAsync",StringComparison.Ordinal);
        var llmIndex=fallback.IndexOf("TryInterpretIntentWithLlmAsync",StringComparison.Ordinal);
        Assert.True(semanticIndex>=0&&semanticIndex<ontologyIndex&&ontologyIndex<relationshipIndex&&relationshipIndex<llmIndex);

        var controller=File.ReadAllText(Path.Combine(root,"src","Ams.Api","Controllers","IntelligenceController.cs"));
        Assert.Contains("HttpPost(\"quick-search/fast\")",controller,StringComparison.Ordinal);
        Assert.Contains("HttpPost(\"quick-search/intelligent-fallback\")",controller,StringComparison.Ordinal);
        var topBar=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Components","Layout","AppQuickSearch.razor"));
        Assert.Contains("Api.QuickSearchFastPathAsync",topBar,StringComparison.Ordinal);
        Assert.Contains("if (_usedIntelligentFallback)",topBar,StringComparison.Ordinal);
        Assert.Contains("Api.QuickSearchIntelligentFallbackAsync",topBar,StringComparison.Ordinal);
        Assert.Contains("await Task.Delay(150, debounceCancellation.Token)",topBar,StringComparison.Ordinal);
        Assert.Contains("class=\"aqs-search-row um-search-box\"",topBar,StringComparison.Ordinal);
        Assert.Contains("class=\"um-search-input\"",topBar,StringComparison.Ordinal);
        Assert.DoesNotContain("class=\"aqs-input\"",topBar,StringComparison.Ordinal);
        Assert.DoesNotContain("await SearchRecordsAsync();\n                break;",topBar,StringComparison.Ordinal);
        Assert.DoesNotContain("Press <strong>Enter</strong> to search records",topBar,StringComparison.Ordinal);
        Assert.DoesNotContain("Api.IntelligenceSearchAsync",topBar,StringComparison.Ordinal);
        var contracts=File.ReadAllText(Path.Combine(root,"src","Ams.Application","Features","Intelligence","IntelligenceContracts.cs"));
        Assert.Contains("record QuickSearchResultDto",contracts,StringComparison.Ordinal);
        Assert.Contains("record QuickSearchResponse",contracts,StringComparison.Ordinal);
        Assert.Contains("QuickSearchFastPathResponse(QuickSearchResponse Search",contracts,StringComparison.Ordinal);
        var intelligencePage=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Components","Pages","Intelligence","IntelligenceSearch.razor"));
        Assert.Contains("Api.IntelligenceSearchAsync",intelligencePage,StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformRuntimeCompletion_TracksTruthfulMaturityVerifiedAdoptionAndRemainingGaps()
    {
        var assembly=typeof(DatabaseMigrator).Assembly;var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0089_PlatformRuntimeCompletion.sql",StringComparison.Ordinal));var sql=Read(assembly,resource);
        foreach(var column in new[]{"MaturityCode","ImplementationStatusCode","AdoptionStatusCode","ConsumerReference","LastVerifiedDateUtc"})Assert.Contains(column,sql,StringComparison.Ordinal);
        Assert.Contains("Platform.MigrationGap",sql,StringComparison.Ordinal);Assert.Contains("DOCUMENT_AI_ROUTER_BYPASS",sql,StringComparison.Ordinal);Assert.Contains("N'COMPLETED'",sql,StringComparison.Ordinal);Assert.Contains("DOCUMENT_OCR_CONFIGURATION",sql,StringComparison.Ordinal);Assert.Contains("PROPOSAL_SMTP_BYPASS",sql,StringComparison.Ordinal);Assert.Contains("BUSINESS_OPTIONS_CONFIGURATION",sql,StringComparison.Ordinal);
        Assert.Contains("UPDATE Platform.ModuleServiceDependency SET AdoptionStatusCode=N'PLANNED'",sql,StringComparison.Ordinal);Assert.Contains("AdoptionStatusCode=N'VERIFIED'",sql,StringComparison.Ordinal);Assert.DoesNotContain("Ams.Application.AuthorizationService",sql,StringComparison.Ordinal);
    }

    [Fact]
    public void RulesAndValidationRuntimes_AreConstrainedAuthorizedPersistedAndConsumed()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));
        var rules=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Services","RulesPlatformService.cs"));var validation=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Services","ValidationPlatformService.cs"));var evaluator=File.ReadAllText(Path.Combine(root,"src","Ams.Application","Services","JsonConditionEvaluator.cs"));var rulesController=File.ReadAllText(Path.Combine(root,"src","Ams.Api","Controllers","RulesPlatformController.cs"));var validationController=File.ReadAllText(Path.Combine(root,"src","Ams.Api","Controllers","ValidationPlatformController.cs"));var documentWorker=File.ReadAllText(Path.Combine(root,"Ams.Worker","Documents","DocumentIntakeProcessor.cs"));
        Assert.Contains("INSERT Rules.RuleExecution",rules,StringComparison.Ordinal);Assert.Contains("TenantId=@TenantId OR TenantId IS NULL",rules,StringComparison.Ordinal);Assert.Contains("StopsProcessing",rules,StringComparison.Ordinal);
        Assert.Contains("Validation.ValidationExecution",validation,StringComparison.Ordinal);Assert.Contains("Validation.ValidationResult",validation,StringComparison.Ordinal);Assert.Contains("JurisdictionCode",validation,StringComparison.Ordinal);Assert.Contains("CorrelationId",validation,StringComparison.Ordinal);
        foreach(var operation in new[]{"EQUALS","NOT_EQUALS","GREATER_THAN","IS_EMPTY","CONTAINS","IN"})Assert.Contains($"\"{operation}\"",evaluator,StringComparison.Ordinal);
        Assert.Contains("Authorize(Policy = IntelligencePolicies.Evaluate)",rulesController,StringComparison.Ordinal);Assert.Contains("Authorize(Policy = IntelligencePolicies.Evaluate)",validationController,StringComparison.Ordinal);Assert.Contains("_rules.EvaluateAsync",documentWorker,StringComparison.Ordinal);Assert.Contains("_validation.ValidateAsync",documentWorker,StringComparison.Ordinal);
    }

    [Fact]
    public void JsonConditionEvaluator_HandlesAllowlistedNestedConditions()
    {
        using var facts=JsonDocument.Parse("""{"premium":1200,"state":"CA","tags":["priority","renewal"]}""");
        using var condition=JsonDocument.Parse("""{"all":[{"field":"premium","operator":"GREATER_THAN","value":1000},{"field":"state","operator":"IN","value":["CA","NY"]},{"field":"tags","operator":"CONTAINS","value":"priority"}]}""");
        Assert.True(JsonConditionEvaluator.Evaluate(condition.RootElement,facts.RootElement));
        using var rejected=JsonDocument.Parse("""{"field":"premium","operator":"EXECUTE_SQL","value":1000}""");
        Assert.Throws<InvalidOperationException>(()=>JsonConditionEvaluator.Evaluate(rejected.RootElement,facts.RootElement));
    }

    [Fact]
    public void DocumentAi_UsesGovernedRoutingExecutionEvidenceAndGroundingWithoutDirectProviderCalls()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));var adapter=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Services","AzureOpenAiDocumentInterpretationProvider.cs"));var router=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Services","AiProviderRouter.cs"));var repository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","AiProviderRouteRepository.cs"));
        Assert.Contains("router.GenerateAsync",adapter,StringComparison.Ordinal);Assert.Contains("AiExecutionContext",adapter,StringComparison.Ordinal);Assert.DoesNotContain("HttpClient",adapter,StringComparison.Ordinal);Assert.DoesNotContain("AzureOpenAiEndpoint",adapter,StringComparison.Ordinal);
        Assert.Contains("RecordExecutionAsync",router,StringComparison.Ordinal);Assert.Contains("GetSafetyPolicyAsync",router,StringComparison.Ordinal);Assert.Contains("AI.ExecutionGroundingSource",repository,StringComparison.Ordinal);Assert.Contains("GroundingSourceReference",repository,StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformAdministration_UsesDatabaseMaturityAdoptionAndGapDataWithoutFallbackCatalogs()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));var repository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","IntelligenceRepository.Platform.cs"));var page=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Components","Pages","Intelligence","IntelligencePlatform.razor"));
        Assert.Contains("Platform.MigrationGap",repository,StringComparison.Ordinal);Assert.Contains("ImplementationStatusCode",repository,StringComparison.Ordinal);Assert.Contains("AdoptionStatusCode",repository,StringComparison.Ordinal);Assert.Contains("ConsumerReference",repository,StringComparison.Ordinal);
        Assert.Contains("GetPlatformArchitectureAsync",page,StringComparison.Ordinal);Assert.Contains("MigrationGaps",page,StringComparison.Ordinal);Assert.Contains("ImplementationStatusCode",page,StringComparison.Ordinal);Assert.Contains("AdoptionStatusCode",page,StringComparison.Ordinal);Assert.Contains("_loadError",page,StringComparison.Ordinal);Assert.DoesNotContain("new PlatformServiceCatalogDto",page,StringComparison.Ordinal);Assert.DoesNotContain("static readonly",page,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PlatformGapRemediation_UsesSecureTenantAwareRoutesAndTruthfulEvidence()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));var assembly=typeof(DatabaseMigrator).Assembly;var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0090_PlatformGapRemediation.sql",StringComparison.Ordinal));var sql=Read(assembly,resource);var routeSeedResource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0135_EnterpriseDocumentIntelligenceRouteSeed.sql",StringComparison.Ordinal));var routeSeed=Read(assembly,routeSeedResource);var ocrRoute=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","DocumentOcrRouteRepository.cs"));var ocrProvider=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Services","AzureDocumentIntelligenceOcrProvider.cs"));var readiness=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Services","DocumentIntakeReadinessHealthCheck.cs"));
        Assert.Contains("ROW_NUMBER() OVER(PARTITION BY setting.SettingKey",ocrRoute,StringComparison.Ordinal);Assert.Contains("CASE WHEN setting.TenantId=@TenantId THEN 0 ELSE 1 END",ocrRoute,StringComparison.Ordinal);Assert.Contains("setting.TenantId IS NULL OR setting.TenantId=@TenantId",ocrRoute,StringComparison.Ordinal);Assert.Contains("FROM resolved",ocrRoute,StringComparison.Ordinal);Assert.Contains("IDocumentOcrRouteRepository",ocrProvider,StringComparison.Ordinal);Assert.Contains("TokenCredential credential",ocrProvider,StringComparison.Ordinal);Assert.DoesNotContain("new DefaultAzureCredential",ocrProvider,StringComparison.Ordinal);Assert.Contains("Operation-Location",ocrProvider,StringComparison.Ordinal);Assert.Contains("ResolveEndpoint",ocrProvider,StringComparison.Ordinal);Assert.Contains("Uri.UriSchemeHttps",ocrProvider,StringComparison.Ordinal);Assert.Contains("DOCUMENT_INTELLIGENCE_ENDPOINT_MISSING",ocrProvider,StringComparison.Ordinal);Assert.Contains("DOCUMENT_INTELLIGENCE_CREDENTIAL_INVALID",ocrProvider,StringComparison.Ordinal);Assert.DoesNotContain("DocumentAiOptions",ocrProvider,StringComparison.Ordinal);Assert.Contains("GetRouteAsync",readiness,StringComparison.Ordinal);Assert.DoesNotContain("DocumentAiOptions",readiness,StringComparison.Ordinal);
        var endpointResource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0136_ConfigureDocumentIntelligenceEndpoint.sql",StringComparison.Ordinal));var endpointSeed=Read(assembly,endpointResource);foreach(var setting in new[]{"DocumentIntelligence.Endpoint","DocumentIntelligence.ModelId","DocumentIntelligence.ApiVersion","DocumentIntelligence.CredentialReference","DocumentIntelligence.TimeoutSeconds"})Assert.Contains(setting,routeSeed,StringComparison.Ordinal);Assert.Contains("env://AMS_DOCUMENT_INTELLIGENCE_ENDPOINT",routeSeed,StringComparison.Ordinal);Assert.Contains("prebuilt-layout",routeSeed,StringComparison.Ordinal);Assert.Contains("2024-11-30",routeSeed,StringComparison.Ordinal);Assert.Contains("DefaultAzureCredential and managed identity",routeSeed,StringComparison.Ordinal);Assert.Contains("https://ams-document-intelligence-dev.cognitiveservices.azure.com/",endpointSeed,StringComparison.Ordinal);Assert.Contains("tenant-specific database setting",endpointSeed,StringComparison.Ordinal);
        foreach(var gap in new[]{"DOCUMENT_OCR_CONFIGURATION","PROPOSAL_SMTP_BYPASS","CONTACT_SMTP_BYPASS","BUSINESS_OPTIONS_CONFIGURATION"})Assert.Contains($"GapCode=N'{gap}'",sql,StringComparison.Ordinal);Assert.Contains("GapCode=N'BUSINESS_OPTIONS_CONFIGURATION'",sql,StringComparison.Ordinal);Assert.Contains("StatusCode=N'IN_PROGRESS'",sql,StringComparison.Ordinal);Assert.Contains("remove generated operational BuildStub datasets",sql,StringComparison.Ordinal);Assert.Contains("DocumentOcrRouteRepository tenant-over-platform query",sql,StringComparison.Ordinal);
    }

    [Fact]
    public void NotificationConsumers_UseSharedDurableDeliveryWithoutDirectSmtpBypasses()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));var proposal=File.ReadAllText(Path.Combine(root,"Ams.Worker","Submissions","ProposalDeliveryWorkerService.cs"));var contact=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Services","ContactIntakeNotificationService.cs"));var delivery=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Services","NotificationDeliveryService.cs"));var worker=File.ReadAllText(Path.Combine(root,"Ams.Worker","Communications","NotificationDeliveryWorkerService.cs"));
        Assert.Contains("INotificationDeliveryService",proposal,StringComparison.Ordinal);Assert.Contains("QueueEmailAsync",proposal,StringComparison.Ordinal);Assert.Contains("NotificationAttachmentRequest",proposal,StringComparison.Ordinal);Assert.DoesNotContain("SmtpClient",proposal,StringComparison.Ordinal);Assert.DoesNotContain("SmtpSettings",proposal,StringComparison.Ordinal);
        Assert.Contains("INotificationDeliveryService",contact,StringComparison.Ordinal);Assert.Contains("QueueEmailAsync",contact,StringComparison.Ordinal);Assert.Contains("contact-intake:",contact,StringComparison.Ordinal);Assert.Contains("Platform.ContactIntakeNotificationTenantId",contact,StringComparison.Ordinal);Assert.DoesNotContain("SmtpClient",contact,StringComparison.Ordinal);Assert.DoesNotContain("ContactIntakeNotificationOptions",contact,StringComparison.Ordinal);
        Assert.Contains("UPDLOCK,READPAST,ROWLOCK",delivery,StringComparison.Ordinal);Assert.Contains("DeliveryProvider=N'PLATFORM_SMTP'",delivery,StringComparison.Ordinal);Assert.Contains("NULLIF(LTRIM(RTRIM(RecipientAddress)),N'') IS NOT NULL",delivery,StringComparison.Ordinal);Assert.Contains("Notification recipient address is missing.",delivery,StringComparison.Ordinal);Assert.Contains("retry: false",delivery,StringComparison.Ordinal);Assert.Contains("env://",delivery,StringComparison.Ordinal);Assert.Contains("ExternalCorrelationId",delivery,StringComparison.Ordinal);Assert.Contains(": BackgroundService",worker,StringComparison.Ordinal);
    }

    [Fact]
    public void OperationalOptions_AreTenantOverPlatformAndConsumedWithoutTargetedLocalArrays()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));var repository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","OperationalOptionRepository.cs"));var controller=File.ReadAllText(Path.Combine(root,"src","Ams.Api","Controllers","OperationalOptionsController.cs"));var activities=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Components","Pages","Workbench","MyActivities.razor"));var communications=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Components","Shared","CommunicationsOpsWorkbench.razor"));var tasks=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Components","Shared","TaskWorkflowOpsWorkbench.razor"));var renewals=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Components","Shared","RenewalOpsWorkbench.razor"));var shell=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Components","Shared","WorkbenchShell.razor"));
        Assert.Contains("ROW_NUMBER() OVER(PARTITION BY OptionCode",repository,StringComparison.Ordinal);Assert.Contains("TenantId=@TenantId OR TenantId IS NULL",repository,StringComparison.Ordinal);Assert.Contains("CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END",repository,StringComparison.Ordinal);Assert.Contains("tenantId == Guid.Empty",controller,StringComparison.Ordinal);Assert.Contains("optionGroupCode",controller,StringComparison.Ordinal);
        Assert.Contains("GetOperationalOptionsAsync(TenantId, \"ActivityType\")",activities,StringComparison.Ordinal);Assert.DoesNotContain("private static readonly string[] ActivityTypes",activities,StringComparison.Ordinal);Assert.Contains("GetOperationalOptionsAsync(context.TenantId, \"NotificationChannel\")",communications,StringComparison.Ordinal);Assert.DoesNotContain("AllChannels",communications,StringComparison.Ordinal);Assert.DoesNotContain("AllStatuses",communications,StringComparison.Ordinal);Assert.Contains("GetOperationalOptionsAsync(context.TenantId, \"WorkflowStatus\")",tasks,StringComparison.Ordinal);Assert.Contains("GetOperationalOptionsAsync(context.TenantId, \"WorkPriority\")",tasks,StringComparison.Ordinal);Assert.DoesNotContain("static readonly string[] WorkflowLanes",tasks,StringComparison.Ordinal);Assert.Contains("GetOperationalOptionsAsync(context.TenantId, \"RenewalStage\")",renewals,StringComparison.Ordinal);Assert.DoesNotContain("static readonly string[] Stages",renewals,StringComparison.Ordinal);Assert.Contains("[Parameter] public IReadOnlyList<LabelValue> BranchOptions",shell,StringComparison.Ordinal);Assert.DoesNotContain("Downtown",shell,StringComparison.Ordinal);Assert.DoesNotContain("CSR Team",shell,StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformArchitectureFoundation_DefinesSharedServicesRulesValidationAndDependencies()
    {
        var assembly=typeof(DatabaseMigrator).Assembly;var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0088_PlatformArchitectureFoundation.sql",StringComparison.Ordinal));var sql=Read(assembly,resource);
        foreach(var table in new[]{"Platform.ServiceCatalog","Platform.BusinessModuleCatalog","Platform.ModuleServiceDependency","Rules.RuleDefinition","Rules.RuleExecution","Validation.ValidationDefinition","Validation.ValidationExecution","Validation.ValidationResult"})Assert.Contains(table,sql,StringComparison.OrdinalIgnoreCase);
        foreach(var service in new[]{"IDENTITY","AUTHORIZATION","AUDIT","WORKFLOW","NOTIFICATION","SEARCH","DOCUMENT","INTELLIGENCE","RULES","VALIDATION","CONFIGURATION","INTEGRATION","REPORTING"})Assert.Contains($"N'{service}'",sql,StringComparison.Ordinal);
        foreach(var module in new[]{"CRM","LEAD","OPPORTUNITY","ACCOUNT","SUBMISSION","QUOTE","PROPOSAL","BIND_REQUEST","POLICY","ENDORSEMENT","RENEWAL","CLAIMS","CERTIFICATES","ACCOUNTING","DOCUMENTS"})Assert.Contains($"N'{module}'",sql,StringComparison.Ordinal);
        Assert.Contains("Platform.Rules.AllowedConditionOperators",sql,StringComparison.Ordinal);Assert.Contains("Platform.Validation.AllowedConditionOperators",sql,StringComparison.Ordinal);Assert.Contains("FROM AI.RecommendationRule",sql,StringComparison.Ordinal);Assert.Contains("FROM AI.ComplianceRequirement",sql,StringComparison.Ordinal);Assert.DoesNotContain("INSERT Rules.RuleExecution",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("INSERT Validation.ValidationExecution",sql,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SharedRuntime_EnforcesSafetyRulesConfigurationAndDocumentPlatformBoundaries()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));var worker=File.ReadAllText(Path.Combine(root,"Ams.Worker","Intelligence","IntelligenceWorkerProcessor.cs"));var router=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Services","AiProviderRouter.cs"));var routeRepository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","AiProviderRouteRepository.cs"));var platformRepository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","IntelligenceRepository.Platform.cs"));var policyDocuments=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Components","Pages","Policies","PolicyDocuments.razor"));var platformPage=File.ReadAllText(Path.Combine(root,"src","Ams.Web","Components","Pages","Intelligence","IntelligencePlatform.razor"));
        Assert.Contains("JOIN Rules.RuleDefinition",worker,StringComparison.Ordinal);Assert.Contains("Platform.Rules.AllowedConditionOperators",worker,StringComparison.Ordinal);Assert.Contains("TryCompareNumbers",worker,StringComparison.Ordinal);Assert.DoesNotContain(":int.MinValue",worker,StringComparison.Ordinal);Assert.Contains("TenantId IS NULL AND SettingKey=N'Intelligence.Renewal.HighRiskRetentionProbability'",worker,StringComparison.Ordinal);Assert.Contains("WHEN NOT MATCHED BY SOURCE AND target.SignalTypeCode",worker,StringComparison.Ordinal);
        Assert.Contains("GetSafetyPolicyAsync",router,StringComparison.Ordinal);Assert.Contains("RecordViolationAsync",router,StringComparison.Ordinal);Assert.Contains("STRUCTURED_OUTPUT_VALIDATION",router,StringComparison.Ordinal);Assert.Contains("AI.SafetyEvent",routeRepository,StringComparison.Ordinal);Assert.Contains("IdempotencyKey",routeRepository,StringComparison.Ordinal);
        Assert.Contains("Platform.ServiceCatalog",platformRepository,StringComparison.Ordinal);Assert.Contains("Platform.ModuleServiceDependency",platformRepository,StringComparison.Ordinal);Assert.Contains("GetPlatformArchitectureAsync",platformPage,StringComparison.Ordinal);
        Assert.Contains("SearchDocumentConfigItemsAsync",policyDocuments,StringComparison.Ordinal);Assert.Contains("SearchDocumentGroupsAsync",policyDocuments,StringComparison.Ordinal);Assert.DoesNotContain("private static readonly string[] DocumentTypes",policyDocuments,StringComparison.Ordinal);Assert.DoesNotContain("private static readonly string[] Categories",policyDocuments,StringComparison.Ordinal);
    }

    [Fact]
    public void PillarMigration_DefinesAllEnginesGovernanceEvidenceAndNoSyntheticOutcomes()
    {
        var assembly=typeof(DatabaseMigrator).Assembly;var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0083_AgencyBinderIntelligencePillars.sql",StringComparison.Ordinal));var sql=Read(assembly,resource);
        foreach(var table in new[]{"AI.IntelligencePillar","AI.IntelligenceCapability","AI.EnginePolicy","AI.PromptDefinition","AI.SafetyControl","AI.IntelligenceFinding","AI.FindingEvidence","AI.EntityRelationship","AI.EntitySimilarity","AI.BusinessSignal","AI.ReasoningSession","AI.ReasoningEvidence","AI.ReasoningConclusion","AI.ReasoningAction","AI.IntelligenceWorkItem"})Assert.Contains(table,sql,StringComparison.OrdinalIgnoreCase);
        foreach(var capability in new[]{"KNOWLEDGE_REPOSITORY","SEMANTIC_MAPPING","AI_GROUNDING","DOCUMENT_INTELLIGENCE","ONTOLOGY_MANAGER","DETERMINISTIC_RULES","RECOMMENDATION","RISK_INTELLIGENCE","COMPLIANCE_INTELLIGENCE","EXPLAINABILITY","SEARCH_INTELLIGENCE","RELATIONSHIP_ENGINE","SIMILARITY_ENGINE","PROMPT_REGISTRY","AI_CONFIGURATION","AI_EXECUTION","AI_EVALUATION","AI_SAFETY_GOVERNANCE","WORKFLOW_INTELLIGENCE","RENEWAL_INTELLIGENCE","CLAIMS_INTELLIGENCE","PRODUCER_INTELLIGENCE","CUSTOMER_INTELLIGENCE","INSURANCE_REASONING"})Assert.Contains(capability,sql,StringComparison.Ordinal);
        foreach(var permission in new[]{"Intelligence.Analyze","Intelligence.Reason","Intelligence.Findings.Read","Intelligence.Findings.Review","Intelligence.Relationships.Read","Intelligence.Governance.Manage"})Assert.Contains(permission,sql,StringComparison.Ordinal);
        Assert.Contains("PERMISSION_SCOPED_GROUNDING",sql,StringComparison.Ordinal);Assert.Contains("REGULATED_DECISION_GUARD",sql,StringComparison.Ordinal);Assert.Contains("ISJSON(ConfigurationJson) = 1",sql,StringComparison.Ordinal);Assert.Contains("UX_AI_ReasoningSession_Correlation",sql,StringComparison.Ordinal);Assert.DoesNotContain("INSERT AI.IntelligenceFinding(",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("INSERT AI.BusinessSignal(",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("INSERT AI.EntitySimilarity(",sql,StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExpandedPersistenceWorkerAndApi_AreTenantPermissionEvidenceAndLeaseAware()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));var repository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","IntelligenceRepository.Platform.cs"));var worker=File.ReadAllText(Path.Combine(root,"Ams.Worker","Intelligence","IntelligenceWorkerProcessor.cs"));var controller=File.ReadAllText(Path.Combine(root,"src","Ams.Api","Controllers","IntelligenceController.cs"));var service=File.ReadAllText(Path.Combine(root,"src","Ams.Application","IntelligenceService.cs"));
        Assert.Contains("finding.TenantId=@TenantId",repository,StringComparison.Ordinal);Assert.Contains("signal.TenantId=@TenantId",repository,StringComparison.Ordinal);Assert.Contains("permission.PrincipalId=@UserId",repository,StringComparison.Ordinal);Assert.Contains("RequestedByUserId=@UserId",repository,StringComparison.Ordinal);Assert.Contains("PermissionSnapshotHash",repository,StringComparison.Ordinal);Assert.Contains("Knowledge semantic contract",repository,StringComparison.Ordinal);Assert.Contains("Intelligence.Reason",service,StringComparison.Ordinal);Assert.Contains("queryExpander.ExpandAsync",service,StringComparison.Ordinal);Assert.Contains("UPDLOCK,READPAST,ROWLOCK",worker,StringComparison.Ordinal);Assert.Contains("AI.PlatformProjection.Synchronize",worker,StringComparison.Ordinal);Assert.Contains(":BackgroundService",File.ReadAllText(Path.Combine(root,"Ams.Worker","Intelligence","IntelligenceWorkerService.cs")),StringComparison.Ordinal);Assert.Contains("AuthenticatedRequestContext.GetGrantedPermissions(User)",controller,StringComparison.Ordinal);Assert.Contains("Authorize(Policy=IntelligencePolicies.Reason)",controller,StringComparison.Ordinal);Assert.Contains("Authorize(Policy=IntelligencePolicies.GovernanceManage)",controller,StringComparison.Ordinal);
    }

    [Fact]
    public void WorkerSynchronization_TreatsOnlyKnownApplicationLockContentionAsNoOp()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));
        var worker=File.ReadAllText(Path.Combine(root,"Ams.Worker","Intelligence","IntelligenceWorkerProcessor.cs"));

        Assert.Contains("catch(SqlException ex)when(IsSynchronizationAlreadyRunning(ex))",worker,StringComparison.Ordinal);
        Assert.Contains("exception.Number==51000",worker,StringComparison.Ordinal);
        Assert.Contains("synchronization is already running.",worker,StringComparison.Ordinal);
        Assert.Contains("return 0;",worker,StringComparison.Ordinal);
        Assert.DoesNotContain("catch(SqlException ex)",worker.Replace("catch(SqlException ex)when(IsSynchronizationAlreadyRunning(ex))",string.Empty,StringComparison.Ordinal),StringComparison.Ordinal);
    }

    [Fact]
    public void DiscoveryRuntime_UsesDatabaseConfigurationScopedRetirementAndRootAuthorization()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));var repository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","IntelligenceRepository.cs"));var platformRepository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","IntelligenceRepository.Platform.cs"));var worker=File.ReadAllText(Path.Combine(root,"Ams.Worker","Intelligence","IntelligenceWorkerProcessor.cs"));var assembly=typeof(DatabaseMigrator).Assembly;var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0086_IntelligenceDiscoveryConfiguration.sql",StringComparison.Ordinal));var migration=Read(assembly,resource);
        foreach(var setting in new[]{"Intelligence.Similarity.ExactAttributeScore","Intelligence.Similarity.PrimaryAttributeScore","Intelligence.Similarity.RelatedAttributeScore","Intelligence.Similarity.FixedAmountTolerance","Intelligence.Similarity.SubmissionAmountTolerancePercent","Intelligence.Similarity.ClaimAmountTolerancePercent","Intelligence.Similarity.ExpirationDays"})Assert.Contains(setting,migration,StringComparison.Ordinal);
        Assert.Contains("Intelligence.Search.KeywordWeight",repository,StringComparison.Ordinal);Assert.Contains("Intelligence.Search.SemanticWeight",repository,StringComparison.Ordinal);Assert.Contains("@EffectiveMaximumResults",repository,StringComparison.Ordinal);Assert.DoesNotContain("KeywordScore*0.45+SemanticScore*0.55",repository,StringComparison.Ordinal);
        Assert.Contains("target.SourceReference IN(N'Submissions.Submission.AccountId',N'Claims.Claim.PolicyNumber',N'DMS.Document.EntityId')",worker,StringComparison.Ordinal);Assert.Contains("target.SourceReference IN(N'Submissions.BoundPolicy.AccountId',N'Client.AccountRelationship',N'Submissions.PolicyBindTransaction.SubmissionId')",worker,StringComparison.Ordinal);Assert.Contains("@SimilarityExpirationDays",worker,StringComparison.Ordinal);
        Assert.Contains("The requested discovery root is not authorized.",platformRepository,StringComparison.Ordinal);Assert.Contains("Intelligence.Similarity.MinimumScore",platformRepository,StringComparison.Ordinal);Assert.Contains("IAM.UserRole",platformRepository,StringComparison.Ordinal);
    }

    [Fact]
    public void PersistenceAndWorker_EnforceTenantPermissionsLeasesAndDatabaseRules()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));var repository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","IntelligenceRepository.cs"));var worker=File.ReadAllText(Path.Combine(root,"Ams.Worker","Intelligence","IntelligenceWorkerProcessor.cs"));var service=File.ReadAllText(Path.Combine(root,"Ams.Worker","Intelligence","IntelligenceWorkerService.cs"));
        Assert.Contains("execution.TenantId=@TenantId",repository,StringComparison.Ordinal);Assert.Contains("permission.PrincipalTypeCode=N'USER'",repository,StringComparison.Ordinal);Assert.Contains("IAM.UserRole",repository,StringComparison.Ordinal);Assert.Contains("RowVersion=@RowVersion",repository,StringComparison.Ordinal);Assert.Contains("AI feature policy changed before this update",repository,StringComparison.Ordinal);Assert.Contains(") KnowledgeChangesToday",repository,StringComparison.Ordinal);Assert.Contains(") ImportJobsInProgress",repository,StringComparison.Ordinal);Assert.Contains(") WorkerQueueDepth",repository,StringComparison.Ordinal);Assert.Contains("ParseSearchIntent(request.Query)",repository,StringComparison.Ordinal);Assert.Contains("document.SourceCreatedDateUtc",repository,StringComparison.Ordinal);Assert.Contains("CASE WHEN @OrderByRecency=1 THEN SourceCreatedDateUtc END DESC",repository,StringComparison.Ordinal);Assert.Contains("new(\"SUBMISSION\",\"Submissions\",orderByRecency)",repository,StringComparison.Ordinal);Assert.Contains("UPDLOCK,READPAST,ROWLOCK",worker,StringComparison.Ordinal);Assert.Contains("AI.RecommendationRule",worker,StringComparison.Ordinal);Assert.Contains("SourceCreatedDateUtc=source.SourceCreatedDateUtc",worker,StringComparison.Ordinal);Assert.Contains("sp_getapplock",worker,StringComparison.Ordinal);Assert.Contains("target.IsDeleted=1",worker,StringComparison.Ordinal);Assert.Contains("LeaseExpiresDateUtc<SYSUTCDATETIME()",worker,StringComparison.Ordinal);Assert.Contains("IAM.RolePermission",worker,StringComparison.Ordinal);Assert.Contains(":BackgroundService",service,StringComparison.Ordinal);Assert.DoesNotContain("new RecommendationDto",service,StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApiClient_UsesTenantFreeRoutesAndPreservesDecisionConcurrency()
    {
        var requests=new List<CapturedRequest>();var handler=new StubHandler(requests,Json(HttpStatusCode.OK,"{\"items\":[],\"totalCount\":0,\"pageNumber\":1,\"pageSize\":50}"),new(HttpStatusCode.NoContent),Json(HttpStatusCode.OK,"{\"items\":[],\"totalCount\":0,\"pageNumber\":1,\"pageSize\":50}"),new(HttpStatusCode.NoContent));var client=new ApiClient(new HttpClient(handler){BaseAddress=new("https://ams.test/")});var id=Guid.NewGuid();var rowVersion=new byte[]{1,2,3,4,5,6,7,8};
        await client.SearchIntelligenceExecutionsAsync(pageSize:50);await client.SubmitIntelligenceFeedbackAsync(id,new(Guid.NewGuid(),id,"QUALITY",5,null,"Verified",Guid.NewGuid()));await client.SearchRecommendationsAsync(pageSize:50);await client.DecideRecommendationAsync(id,new(Guid.NewGuid(),id,"ACCEPT","Verified",Guid.NewGuid(),rowVersion));
        Assert.All(requests,request=>Assert.DoesNotContain("tenantId",request.Path,StringComparison.OrdinalIgnoreCase));Assert.Equal("api/intelligence/executions?searchTerm=&featureCode=&statusCode=&pageNumber=1&pageSize=50",requests[0].Path);Assert.Equal($"api/intelligence/executions/{id}/feedback",requests[1].Path);Assert.Contains(Convert.ToBase64String(rowVersion),requests[3].Body);
    }

    [Fact]
    public void BlazorWorkspaces_UseTypedApisAndValidDatabaseEmptyStates()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","src","Ams.Web","Components","Pages","Intelligence"));var files=Directory.GetFiles(root,"*.razor").Select(File.ReadAllText).ToArray();var source=string.Join(Environment.NewLine,files);
        Assert.Contains("Api.GetIntelligenceDashboardAsync",source,StringComparison.Ordinal);Assert.Contains("Api.IntelligenceSearchAsync",source,StringComparison.Ordinal);Assert.Contains("Api.SearchRecommendationsAsync",source,StringComparison.Ordinal);Assert.Contains("Api.SearchIntelligenceReviewQueueAsync",source,StringComparison.Ordinal);Assert.Contains("Api.SearchIntelligenceExecutionsAsync",source,StringComparison.Ordinal);Assert.Contains("Api.GetIntelligenceEvaluationDefinitionsAsync",source,StringComparison.Ordinal);Assert.Contains("Policy=\"@Ams.Web.Security.IntelligencePolicies.Recommend\"",source,StringComparison.Ordinal);Assert.Contains("Policy=\"@Ams.Web.Security.KnowledgePolicies.Import\"",source,StringComparison.Ordinal);Assert.Contains("No authorized indexed records matched",source,StringComparison.Ordinal);Assert.DoesNotContain("new List<RecommendationDto>",source,StringComparison.Ordinal);
    }

    [Fact]
    public void ExpandedBlazorWorkspaces_CoverEveryPillarAndReasoningWithoutMockData()
    {
        var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..","..","src","Ams.Web","Components","Pages","Intelligence"));var expected=new Dictionary<string,string>{{"IntelligencePlatform.razor","Api.GetIntelligencePlatformAsync"},{"IntelligenceFindings.razor","Api.SearchIntelligenceFindingsAsync"},{"IntelligenceDiscovery.razor","Api.GetIntelligenceRelationshipsAsync"},{"IntelligenceBusinessSignals.razor","Api.SearchBusinessIntelligenceSignalsAsync"},{"IntelligenceReasoning.razor","Api.ExecuteInsuranceReasoningAsync"},{"IntelligenceGovernance.razor","Api.GetIntelligenceEnginePoliciesAsync"}};
        foreach(var item in expected){var source=File.ReadAllText(Path.Combine(root,item.Key));Assert.Contains(item.Value,source,StringComparison.Ordinal);Assert.Contains("<IntelligenceWorkspace>",source,StringComparison.Ordinal);Assert.DoesNotContain("mock",source,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("sample data",source,StringComparison.OrdinalIgnoreCase);}
        var navigation=File.ReadAllText(Path.Combine(root,"IntelligenceWorkspace.razor"));foreach(var route in new[]{"/intelligence/platform","/intelligence/reasoning","/intelligence/discovery","/intelligence/findings","/intelligence/business","/intelligence/governance"})Assert.Contains(route,navigation,StringComparison.Ordinal);
    }

    [Fact]
    public void ScenarioCoverage_UsesAuthoritativeComplianceSafetyLabelsAndBusinessSources()
    {
        var assembly=typeof(DatabaseMigrator).Assembly;var resource=assembly.GetManifestResourceNames().Single(x=>x.EndsWith("0084_IntelligenceScenarioCoverage.sql",StringComparison.Ordinal));var sql=Read(assembly,resource);var root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","..","..","..",".."));var repository=File.ReadAllText(Path.Combine(root,"src","Ams.Infrastructure","Persistence","Repositories","IntelligenceRepository.Platform.cs"));var worker=File.ReadAllText(Path.Combine(root,"Ams.Worker","Intelligence","IntelligenceWorkerProcessor.cs"));var controller=File.ReadAllText(Path.Combine(root,"src","Ams.Api","Controllers","IntelligenceController.cs"));
        foreach(var table in new[]{"AI.ComplianceRequirement","AI.EvaluationSampleLabel","AI.SafetyEvent"})Assert.Contains(table,sql,StringComparison.OrdinalIgnoreCase);
        foreach(var metric in new[]{"LABELED_ACCURACY","LABELED_PRECISION","LABELED_RECALL","HALLUCINATION_RATE","SAFETY_EVENT_RATE"}){Assert.Contains(metric,sql,StringComparison.Ordinal);Assert.Contains(metric,worker,StringComparison.Ordinal);}
        foreach(var setting in new[]{"Intelligence.Risk.LargeLossThreshold","Intelligence.Risk.HighRiskAccountLossCount","Intelligence.Renewal.ReadinessDays","Intelligence.Workflow.DelayDays","Intelligence.Producer.FollowUpDays"})Assert.Contains(setting,sql,StringComparison.Ordinal);
        foreach(var evidence in new[]{"Submissions.BindValidationResult","Agency.CarrierProductRule","Submissions.BindApproval","AI.EntitySimilarity"})Assert.Contains(evidence,repository,StringComparison.Ordinal);
        foreach(var source in new[]{"Submissions.Submission","Submissions.BindApproval","Renewal.RetentionCase","Claims.Claim","CRM.Opportunity","Client.Account"})Assert.Contains(source,worker,StringComparison.Ordinal);
        Assert.Contains("GetComplianceRequirementsAsync",controller,StringComparison.Ordinal);Assert.Contains("GetSafetyEventsAsync",controller,StringComparison.Ordinal);Assert.Contains("SubmitEvaluationSampleLabelAsync",controller,StringComparison.Ordinal);Assert.DoesNotContain("INSERT AI.SafetyEvent(",sql,StringComparison.OrdinalIgnoreCase);Assert.DoesNotContain("INSERT AI.EvaluationSampleLabel(",sql,StringComparison.OrdinalIgnoreCase);
    }

    [SqlIntegrationFact]
    [Trait("Category","SqlIntegration")]
    public async Task ExecutionSearch_IsStrictlyTenantScoped()
    {
        var connectionString=SqlIntegrationConnection.GetRequired();var tenantA=Guid.NewGuid();var tenantB=Guid.NewGuid();var executionA=Guid.NewGuid();var executionB=Guid.NewGuid();await using(var connection=new SqlConnection(connectionString)){await connection.OpenAsync();const string insert="INSERT AI.Execution(ExecutionId,TenantId,FeatureCode,ModuleCode,StatusCode,CorrelationId,StartedDateUtc,CreatedDateUtc,IsDeleted) VALUES(@Id,@TenantId,N'TEST',N'TEST',N'COMPLETED',@Correlation,SYSUTCDATETIME(),SYSUTCDATETIME(),0);";await connection.ExecuteAsync(insert,new{Id=executionA,TenantId=tenantA,Correlation=executionA.ToString("N")});await connection.ExecuteAsync(insert,new{Id=executionB,TenantId=tenantB,Correlation=executionB.ToString("N")});}
        try{var repository=new IntelligenceRepository(new TestConnectionFactory(connectionString));var result=await repository.SearchExecutionsAsync(new(tenantA,null,null,null,null,null,1,50));Assert.Contains(result.Items,x=>x.ExecutionId==executionA);Assert.DoesNotContain(result.Items,x=>x.ExecutionId==executionB);}finally{await using var cleanup=new SqlConnection(connectionString);await cleanup.OpenAsync();await cleanup.ExecuteAsync("DELETE AI.Execution WHERE ExecutionId IN @Ids",new{Ids=new[]{executionA,executionB}});}
    }

    private static string Read(Assembly assembly,string resource){using var stream=assembly.GetManifestResourceStream(resource)!;using var reader=new StreamReader(stream);return reader.ReadToEnd();}
    private static HttpResponseMessage Json(HttpStatusCode status,string json)=>new(status){Content=new StringContent(json,Encoding.UTF8,"application/json")};
    private sealed record CapturedRequest(string Path,string Body);
    private sealed class StubHandler(List<CapturedRequest> requests,params HttpResponseMessage[] responses):HttpMessageHandler{private int _index;protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken){requests.Add(new(request.RequestUri!.PathAndQuery.TrimStart('/'),request.Content is null?string.Empty:await request.Content.ReadAsStringAsync(cancellationToken)));return responses[_index++];}}
    private sealed class TestConnectionFactory(string connectionString):ISqlConnectionFactory{public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken=default){var connection=new SqlConnection(connectionString);await connection.OpenAsync(cancellationToken);return connection;}}
}
