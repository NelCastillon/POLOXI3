using System.Data;
using System.Diagnostics;
using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Models;
using Ams.Application.Features.Intelligence;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed partial class IntelligenceRepository(ISqlConnectionFactory connectionFactory):IIntelligenceRepository,IRecommendationGenerationRepository
{
    public async Task<IReadOnlyCollection<AiProviderDto>> GetProvidersAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""SELECT ProviderId,ProviderCode,DisplayName,ProviderTypeCode,SupportsChat,SupportsEmbeddings,SupportsVision,SupportsStructuredOutput,Priority,IsActive,RowVersion FROM AI.Provider WHERE (TenantId IS NULL OR TenantId=@TenantId) AND IsDeleted=0 ORDER BY Priority,DisplayName;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);return (await connection.QueryAsync<AiProviderDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken))).AsList();
    }

    public async Task<IntelligenceSearchConfiguration> GetSearchConfigurationAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""
            SELECT
              COALESCE(TRY_CONVERT(decimal(9,6),(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.KeywordWeight' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),.25) KeywordWeight,
              COALESCE(TRY_CONVERT(decimal(9,6),(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.SemanticWeight' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),.30) SemanticWeight,
              COALESCE(TRY_CONVERT(decimal(9,6),(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.FuzzyWeight' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),.25) FuzzyWeight,
              COALESCE(TRY_CONVERT(decimal(9,6),(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.RelationshipWeight' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),.10) RelationshipWeight,
              COALESCE(TRY_CONVERT(decimal(9,6),(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.RecencyWeight' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),.05) RecencyWeight,
              COALESCE(TRY_CONVERT(decimal(9,6),(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.BusinessPriorityWeight' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),.05) BusinessPriorityWeight,
              COALESCE(TRY_CONVERT(int,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.RecencyWindowDays' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),365) RecencyWindowDays,
              COALESCE(TRY_CONVERT(int,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.MaximumRelationshipResults' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),20) MaximumRelationshipResults,
              COALESCE(TRY_CONVERT(decimal(9,6),(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.MinimumUnifiedScore' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),.05) MinimumUnifiedScore,
              CONVERT(bit,COALESCE(TRY_CONVERT(bit,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.EnableRules' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),1)) EnableRules,
              CONVERT(bit,COALESCE(TRY_CONVERT(bit,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.EnableRelationships' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),1)) EnableRelationships,
              CONVERT(bit,COALESCE(TRY_CONVERT(bit,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.EnableAiSummary' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),1)) EnableAiSummary,
              CONVERT(bit,COALESCE(TRY_CONVERT(bit,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.EnableLlmIntentFallback' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),1)) EnableLlmIntentFallback,
              COALESCE(TRY_CONVERT(decimal(9,6),(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.LlmIntentMinimumConfidence' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),.70) LlmIntentMinimumConfidence,
              COALESCE(TRY_CONVERT(int,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.LlmIntentTimeoutSeconds' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),8) LlmIntentTimeoutSeconds;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row=await connection.QuerySingleAsync<SearchConfigurationRow>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken));
        return new(new(row.KeywordWeight,row.SemanticWeight,row.FuzzyWeight,row.RelationshipWeight,row.RecencyWeight,row.BusinessPriorityWeight),Math.Clamp(row.RecencyWindowDays,1,3650),Math.Clamp(row.MaximumRelationshipResults,0,100),Math.Clamp(row.MinimumUnifiedScore,0,1),row.EnableRules,row.EnableRelationships,row.EnableAiSummary,row.EnableLlmIntentFallback,Math.Clamp(row.LlmIntentMinimumConfidence,0,1),Math.Clamp(row.LlmIntentTimeoutSeconds,1,60));
    }

    public async Task<IReadOnlyCollection<IntelligenceSearchIntentPatternDto>> GetSearchIntentPatternsAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""
            ;WITH patterns AS
            (
                SELECT pattern.*,ROW_NUMBER() OVER(PARTITION BY pattern.PatternCode ORDER BY CASE WHEN pattern.TenantId=@TenantId THEN 0 ELSE 1 END,pattern.Priority) Choice
                FROM AI.SearchIntentPattern pattern
                WHERE pattern.IsActive=1 AND pattern.IsDeleted=0 AND (pattern.TenantId=@TenantId OR pattern.TenantId IS NULL)
            )
            SELECT PatternCode,EntityTypeCode,ModuleCode,ExtractionStrategyCode,Priority,IsEntityList
            FROM patterns WHERE Choice=1 ORDER BY Priority,PatternCode;

            ;WITH patterns AS
            (
                SELECT pattern.*,ROW_NUMBER() OVER(PARTITION BY pattern.PatternCode ORDER BY CASE WHEN pattern.TenantId=@TenantId THEN 0 ELSE 1 END,pattern.Priority) Choice
                FROM AI.SearchIntentPattern pattern
                WHERE pattern.IsActive=1 AND pattern.IsDeleted=0 AND (pattern.TenantId=@TenantId OR pattern.TenantId IS NULL)
            )
            SELECT pattern.PatternCode,phrase.PhraseKindCode,phrase.PhraseText,phrase.SortOrder
            FROM patterns pattern
            JOIN AI.SearchIntentPatternPhrase phrase ON phrase.SearchIntentPatternId=pattern.SearchIntentPatternId AND phrase.IsActive=1 AND phrase.IsDeleted=0
            WHERE pattern.Choice=1
            ORDER BY pattern.Priority,pattern.PatternCode,phrase.PhraseKindCode,phrase.SortOrder,phrase.PhraseText;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken));
        var patterns=(await multi.ReadAsync<IntentPatternRow>()).AsList();
        var phrases=(await multi.ReadAsync<IntentPatternPhraseRow>()).AsList();
        return patterns.Select(pattern=>new IntelligenceSearchIntentPatternDto(pattern.PatternCode,pattern.EntityTypeCode,pattern.ModuleCode,pattern.ExtractionStrategyCode,phrases.Where(phrase=>phrase.PatternCode==pattern.PatternCode&&phrase.PhraseKindCode.Equals("MATCH",StringComparison.OrdinalIgnoreCase)).OrderBy(phrase=>phrase.SortOrder).Select(phrase=>phrase.PhraseText).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),phrases.Where(phrase=>phrase.PatternCode==pattern.PatternCode&&phrase.PhraseKindCode.Equals("EXTRACT",StringComparison.OrdinalIgnoreCase)).OrderBy(phrase=>phrase.SortOrder).Select(phrase=>phrase.PhraseText).Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),pattern.Priority,pattern.IsEntityList)).ToArray();
    }

    public async Task RecordSearchIntentInterpretationAsync(IntelligenceSearchIntentLogRecord record,CancellationToken cancellationToken=default)
    {
        const string sql="""IF OBJECT_ID(N'AI.SearchIntentInterpretationLog',N'U') IS NOT NULL INSERT AI.SearchIntentInterpretationLog(SearchIntentInterpretationLogId,TenantId,UserId,QueryText,EntityTypeCode,ModuleCode,SearchText,SourceEngineCode,Confidence,StatusCode,ErrorMessage,CorrelationId,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@UserId,@QueryText,@EntityTypeCode,@ModuleCode,@SearchText,@SourceEngineCode,@Confidence,@StatusCode,@ErrorMessage,@CorrelationId,SYSUTCDATETIME(),@UserId,0);""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,record,cancellationToken:cancellationToken));
    }

    public async Task<IReadOnlyCollection<IntelligenceSearchResultDto>> GetAuthorizedSearchDocumentsAsync(IntelligenceSearchRequest request,IReadOnlyCollection<IntelligenceSearchEntityKey> entities,CancellationToken cancellationToken=default)
    {
        if(entities.Count==0)return [];
        const string sql="""
            SELECT document.SearchDocumentId,document.EntityTypeCode,document.EntityId,document.ModuleCode,document.Title,LEFT(document.ContentText,500) Excerpt,document.SourceCreatedDateUtc,projection.NavigationRoute,
              CONVERT(bit,CASE WHEN document.EntityTypeCode=N'DOCUMENT' AND EXISTS(SELECT 1 FROM DMS.IntakeSessionDocument link JOIN DMS.IntakeDraftField field ON field.TenantId=link.TenantId AND field.IntakeSessionId=link.IntakeSessionId WHERE link.TenantId=document.TenantId AND link.DocumentId=document.EntityId AND (field.ExtractedValue LIKE N'%'+@Query+N'%' OR field.NormalizedValue LIKE N'%'+@Query+N'%' OR field.ReviewedValue LIKE N'%'+@Query+N'%')) THEN 1 ELSE 0 END) ExtractedFieldMatch
            FROM AI.SearchDocument document
            JOIN OPENJSON(@EntitiesJson) WITH(EntityTypeCode nvarchar(100) '$.EntityTypeCode',EntityId uniqueidentifier '$.EntityId') entity ON entity.EntityTypeCode=document.EntityTypeCode AND entity.EntityId=document.EntityId
            LEFT JOIN Search.EntityProjection projection ON projection.TenantId=document.TenantId AND projection.EntityTypeCode=document.EntityTypeCode AND projection.EntityId=document.EntityId AND projection.IsActive=1 AND projection.IsDeleted=0
            WHERE document.TenantId=@TenantId AND document.IsDeleted=0 AND (@ModuleCode IS NULL OR document.ModuleCode=@ModuleCode) AND (@EntityTypeCode IS NULL OR document.EntityTypeCode=@EntityTypeCode)
              AND EXISTS(SELECT 1 FROM AI.SearchPermission permission WHERE permission.TenantId=document.TenantId AND permission.SearchDocumentId=document.SearchDocumentId AND permission.PermissionCode=N'READ' AND permission.IsDeleted=0 AND ((permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId) OR (permission.PrincipalTypeCode=N'ROLE' AND EXISTS(SELECT 1 FROM IAM.UserRole userRole WHERE userRole.TenantId=@TenantId AND userRole.UserId=@UserId AND userRole.RoleId=permission.PrincipalId AND userRole.IsActive=1 AND userRole.IsDeleted=0))));
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows=await connection.QueryAsync<AuthorizedSearchRow>(new CommandDefinition(sql,new{request.TenantId,request.UserId,request.ModuleCode,request.EntityTypeCode,request.Query,EntitiesJson=JsonSerializer.Serialize(entities)},cancellationToken:cancellationToken));
        return rows.Select(row=>WithExtractedFieldExplanation(ToSearchResult(row),row.ExtractedFieldMatch)).ToArray();
    }

    public async Task<IReadOnlyCollection<IntelligenceSearchResultDto>> GetRelatedSearchDocumentsAsync(IntelligenceSearchRequest request,IReadOnlyCollection<IntelligenceSearchEntityKey> sources,int maximumResults,CancellationToken cancellationToken=default)
    {
        if(sources.Count==0||maximumResults<=0)return [];
        const string sql="""
            SELECT TOP(@MaximumResults) document.SearchDocumentId,document.EntityTypeCode,document.EntityId,document.ModuleCode,document.Title,LEFT(document.ContentText,500) Excerpt,document.SourceCreatedDateUtc,projection.NavigationRoute,MAX(relationship.Strength) RelationshipScore,MAX(relationship.RelationshipTypeCode) RelationshipTypeCode
            FROM OPENJSON(@SourcesJson) WITH(EntityTypeCode nvarchar(100) '$.EntityTypeCode',EntityId uniqueidentifier '$.EntityId') source
            JOIN AI.EntityRelationship relationship ON relationship.TenantId=@TenantId AND relationship.SourceEntityTypeCode=source.EntityTypeCode AND relationship.SourceEntityId=source.EntityId AND relationship.IsDeleted=0 AND (relationship.EffectiveFromUtc IS NULL OR relationship.EffectiveFromUtc<=SYSUTCDATETIME()) AND (relationship.EffectiveToUtc IS NULL OR relationship.EffectiveToUtc>SYSUTCDATETIME())
            JOIN AI.SearchDocument document ON document.TenantId=@TenantId AND document.EntityTypeCode=relationship.TargetEntityTypeCode AND document.EntityId=relationship.TargetEntityId AND document.IsDeleted=0
            LEFT JOIN Search.EntityProjection projection ON projection.TenantId=document.TenantId AND projection.EntityTypeCode=document.EntityTypeCode AND projection.EntityId=document.EntityId AND projection.IsActive=1 AND projection.IsDeleted=0
            WHERE (@ModuleCode IS NULL OR document.ModuleCode=@ModuleCode) AND (@EntityTypeCode IS NULL OR document.EntityTypeCode=@EntityTypeCode)
              AND EXISTS(SELECT 1 FROM AI.SearchPermission permission WHERE permission.TenantId=document.TenantId AND permission.SearchDocumentId=document.SearchDocumentId AND permission.PermissionCode=N'READ' AND permission.IsDeleted=0 AND ((permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId) OR (permission.PrincipalTypeCode=N'ROLE' AND EXISTS(SELECT 1 FROM IAM.UserRole userRole WHERE userRole.TenantId=@TenantId AND userRole.UserId=@UserId AND userRole.RoleId=permission.PrincipalId AND userRole.IsActive=1 AND userRole.IsDeleted=0))))
            GROUP BY document.SearchDocumentId,document.EntityTypeCode,document.EntityId,document.ModuleCode,document.Title,document.ContentText,document.SourceCreatedDateUtc,projection.NavigationRoute ORDER BY MAX(relationship.Strength) DESC,document.Title;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows=await connection.QueryAsync<AuthorizedSearchRow>(new CommandDefinition(sql,new{request.TenantId,request.UserId,request.ModuleCode,request.EntityTypeCode,MaximumResults=maximumResults,SourcesJson=JsonSerializer.Serialize(sources)},cancellationToken:cancellationToken));
        return rows.Select(row=>ToSearchResult(row) with{RelationshipScore=row.RelationshipScore,IsRelatedResult=true,Explanations=[new("RELATED_ENTITY","Related entity",$"Connected through {row.RelationshipTypeCode??"an approved relationship"}.",row.RelationshipScore,"RELATIONSHIP_DISCOVERY")]}).ToArray();
    }

    public async Task<IReadOnlyCollection<AiModelDeploymentDto>> GetModelsAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""SELECT model.ModelDeploymentId,model.ProviderId,provider.ProviderCode,model.ModelCode,model.DeploymentName,model.ModelFamily,model.CapabilityCode,model.ContextWindowTokens,model.MaximumOutputTokens,model.InputCostPerMillionTokens,model.OutputCostPerMillionTokens,model.CurrencyCode,model.Priority,model.IsFallback,model.IsActive,model.RowVersion FROM AI.ModelDeployment model JOIN AI.Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsDeleted=0 WHERE (model.TenantId IS NULL OR model.TenantId=@TenantId) AND model.IsDeleted=0 ORDER BY model.Priority,model.DeploymentName;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);return (await connection.QueryAsync<AiModelDeploymentDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken))).AsList();
    }

    public async Task<IReadOnlyCollection<AiFeaturePolicyDto>> GetFeaturePoliciesAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""SELECT FeaturePolicyId,TenantId,FeatureCode,ModuleCode,PrimaryModelDeploymentId,FallbackModelDeploymentId,Temperature,MaximumInputTokens,MaximumOutputTokens,TimeoutSeconds,DailyCostLimit,MonthlyCostLimit,MinimumConfidence,RequiresHumanReview,IsEnabled,RowVersion FROM AI.FeaturePolicy WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY ModuleCode,FeatureCode;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);return (await connection.QueryAsync<AiFeaturePolicyDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken))).AsList();
    }

    public async Task SaveFeaturePolicyAsync(SaveAiFeaturePolicyRequest request,CancellationToken cancellationToken=default)
    {
        const string sql="""IF @RowVersion IS NOT NULL BEGIN UPDATE AI.FeaturePolicy SET ModuleCode=@ModuleCode,PrimaryModelDeploymentId=@PrimaryModelDeploymentId,FallbackModelDeploymentId=@FallbackModelDeploymentId,Temperature=@Temperature,MaximumInputTokens=@MaximumInputTokens,MaximumOutputTokens=@MaximumOutputTokens,TimeoutSeconds=@TimeoutSeconds,DailyCostLimit=@DailyCostLimit,MonthlyCostLimit=@MonthlyCostLimit,MinimumConfidence=@MinimumConfidence,RequiresHumanReview=@RequiresHumanReview,IsEnabled=@IsEnabled,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND FeatureCode=@FeatureCode AND IsDeleted=0 AND RowVersion=@RowVersion; IF @@ROWCOUNT=0 THROW 51000,'AI feature policy changed before this update.',1; END ELSE BEGIN INSERT AI.FeaturePolicy(FeaturePolicyId,TenantId,FeatureCode,ModuleCode,PrimaryModelDeploymentId,FallbackModelDeploymentId,Temperature,MaximumInputTokens,MaximumOutputTokens,TimeoutSeconds,DailyCostLimit,MonthlyCostLimit,MinimumConfidence,RequiresHumanReview,IsEnabled,CreatedDateUtc,CreatedByUserId,IsDeleted) SELECT NEWID(),@TenantId,@FeatureCode,@ModuleCode,@PrimaryModelDeploymentId,@FallbackModelDeploymentId,@Temperature,@MaximumInputTokens,@MaximumOutputTokens,@TimeoutSeconds,@DailyCostLimit,@MonthlyCostLimit,@MinimumConfidence,@RequiresHumanReview,@IsEnabled,SYSUTCDATETIME(),@ActorUserId,0 WHERE NOT EXISTS(SELECT 1 FROM AI.FeaturePolicy WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND FeatureCode=@FeatureCode AND IsDeleted=0); IF @@ROWCOUNT=0 THROW 51000,'AI feature policy already exists; reload it before updating.',1; END;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);await connection.ExecuteAsync(new CommandDefinition(sql,request,cancellationToken:cancellationToken));
    }

    public async Task<PagedResult<AiExecutionSummaryDto>> SearchExecutionsAsync(SearchAiExecutionsQuery query,CancellationToken cancellationToken=default)
    {
        const string from=""" FROM AI.Execution execution LEFT JOIN AI.Provider provider ON provider.ProviderId=execution.ProviderId LEFT JOIN AI.ModelDeployment model ON model.ModelDeploymentId=execution.ModelDeploymentId WHERE execution.TenantId=@TenantId AND execution.IsDeleted=0 AND (@SearchTerm IS NULL OR execution.CorrelationId LIKE '%'+@SearchTerm+'%' OR execution.FeatureCode LIKE '%'+@SearchTerm+'%' OR execution.EntityTypeCode LIKE '%'+@SearchTerm+'%' OR execution.ErrorMessage LIKE '%'+@SearchTerm+'%') AND (@FeatureCode IS NULL OR execution.FeatureCode=@FeatureCode) AND (@StatusCode IS NULL OR execution.StatusCode=@StatusCode) AND (@FromUtc IS NULL OR execution.CreatedDateUtc>=@FromUtc) AND (@ToUtc IS NULL OR execution.CreatedDateUtc<@ToUtc)""";
        var sql=$"""SELECT execution.ExecutionId,execution.TenantId,execution.FeatureCode,execution.ModuleCode,execution.EntityTypeCode,execution.EntityId,execution.StatusCode,provider.ProviderCode,model.ModelCode,execution.PromptVersion,execution.DurationMilliseconds,execution.InputTokenCount,execution.OutputTokenCount,execution.EstimatedCost,execution.CurrencyCode,execution.Confidence,execution.GroundingSourceCount,execution.RequestedByUserId,execution.StartedDateUtc,execution.CompletedDateUtc,execution.CorrelationId,execution.ErrorCode,execution.ErrorMessage,execution.RowVersion {from} ORDER BY execution.CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY; SELECT COUNT(1) {from};""";
        var parameters=new{query.TenantId,SearchTerm=Null(query.SearchTerm),FeatureCode=Null(query.FeatureCode),StatusCode=Null(query.StatusCode),query.FromUtc,query.ToUtc,Offset=(query.PageNumber-1)*query.PageSize,query.PageSize};
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,parameters,cancellationToken:cancellationToken));return new(){Items=(await multi.ReadAsync<AiExecutionSummaryDto>()).AsList(),TotalCount=await multi.ReadSingleAsync<int>(),PageNumber=query.PageNumber,PageSize=query.PageSize};
    }

    public async Task<AiExecutionDetailDto?> GetExecutionAsync(Guid tenantId,Guid executionId,CancellationToken cancellationToken=default)
    {
        const string sql="""SELECT execution.ExecutionId,execution.TenantId,execution.FeatureCode,execution.ModuleCode,execution.EntityTypeCode,execution.EntityId,execution.StatusCode,provider.ProviderCode,model.ModelCode,execution.PromptVersion,execution.DurationMilliseconds,execution.InputTokenCount,execution.OutputTokenCount,execution.EstimatedCost,execution.CurrencyCode,execution.Confidence,execution.GroundingSourceCount,execution.RequestedByUserId,execution.StartedDateUtc,execution.CompletedDateUtc,execution.CorrelationId,execution.ErrorCode,execution.ErrorMessage,execution.RowVersion FROM AI.Execution execution LEFT JOIN AI.Provider provider ON provider.ProviderId=execution.ProviderId LEFT JOIN AI.ModelDeployment model ON model.ModelDeploymentId=execution.ModelDeploymentId WHERE execution.TenantId=@TenantId AND execution.ExecutionId=@ExecutionId AND execution.IsDeleted=0; SELECT ExecutionGroundingSourceId GroundingSourceId,SourceTypeCode,CAST(NULL AS NVARCHAR(100)) SourceEntityTypeCode,SourceEntityId,SourceReference,Title,RelevanceScore FROM AI.ExecutionGroundingSource WHERE TenantId=@TenantId AND ExecutionId=@ExecutionId AND IsDeleted=0 ORDER BY RelevanceScore DESC; SELECT ExecutionFeedbackId FeedbackId,FeedbackTypeCode,Rating,CorrectionReference,Comment,DecisionCode,ReviewedByUserId,ReviewedDateUtc,CreatedDateUtc FROM AI.ExecutionFeedback WHERE TenantId=@TenantId AND ExecutionId=@ExecutionId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,new{TenantId=tenantId,ExecutionId=executionId},cancellationToken:cancellationToken));var execution=await multi.ReadSingleOrDefaultAsync<AiExecutionSummaryDto>();if(execution is null)return null;return new(execution,(await multi.ReadAsync<AiGroundingSourceDto>()).AsList(),(await multi.ReadAsync<AiExecutionFeedbackDto>()).AsList());
    }

    public async Task SubmitExecutionFeedbackAsync(SubmitAiExecutionFeedbackRequest request,CancellationToken cancellationToken=default)
    {
        const string sql="""IF NOT EXISTS(SELECT 1 FROM AI.Execution WHERE TenantId=@TenantId AND ExecutionId=@ExecutionId AND IsDeleted=0) THROW 51000,'AI execution was not found for tenant.',1; INSERT AI.ExecutionFeedback(TenantId,ExecutionId,FeedbackTypeCode,Rating,CorrectionReference,Comment,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@TenantId,@ExecutionId,@FeedbackTypeCode,@Rating,@CorrectionReference,@Comment,SYSUTCDATETIME(),@ActorUserId,0);""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);await connection.ExecuteAsync(new CommandDefinition(sql,request,cancellationToken:cancellationToken));
    }

    public async Task<IReadOnlyCollection<RecommendationTypeDto>> GetRecommendationTypesAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""SELECT RecommendationTypeId,TypeCode,DisplayName,Description,TargetModuleCode,DefaultPriorityCode,DefaultExpirationHours,RequiresHumanReview,SortOrder,IsActive FROM AI.RecommendationType WHERE (TenantId IS NULL OR TenantId=@TenantId) AND IsDeleted=0 AND IsActive=1 ORDER BY SortOrder,DisplayName;""";using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);return (await connection.QueryAsync<RecommendationTypeDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken))).AsList();
    }

    public async Task<PagedResult<RecommendationDto>> SearchRecommendationsAsync(SearchRecommendationsQuery query,CancellationToken cancellationToken=default)
    {
        const string from=""" FROM AI.Recommendation recommendation JOIN AI.RecommendationType type ON type.RecommendationTypeId=recommendation.RecommendationTypeId WHERE recommendation.TenantId=@TenantId AND recommendation.IsDeleted=0 AND (@SearchTerm IS NULL OR recommendation.Title LIKE '%'+@SearchTerm+'%' OR recommendation.Summary LIKE '%'+@SearchTerm+'%') AND (@TypeCode IS NULL OR type.TypeCode=@TypeCode) AND (@StatusCode IS NULL OR recommendation.StatusCode=@StatusCode) AND (@EntityTypeCode IS NULL OR recommendation.EntityTypeCode=@EntityTypeCode) AND (@EntityId IS NULL OR recommendation.EntityId=@EntityId) AND (@AssignedToUserId IS NULL OR recommendation.AssignedToUserId=@AssignedToUserId)""";var sql=$"""SELECT recommendation.RecommendationId,recommendation.TenantId,type.TypeCode,type.DisplayName TypeName,recommendation.EntityTypeCode,recommendation.EntityId,recommendation.Title,recommendation.Summary,recommendation.Rationale,recommendation.ActionCode,recommendation.ActionPayloadJson,recommendation.PriorityCode,recommendation.StatusCode,recommendation.Confidence,recommendation.Score,recommendation.AssignedToUserId,recommendation.ExpiresDateUtc,recommendation.CreatedDateUtc,recommendation.RowVersion {from} ORDER BY recommendation.Score DESC,recommendation.CreatedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;SELECT COUNT(1) {from};""";var parameters=new{query.TenantId,SearchTerm=Null(query.SearchTerm),TypeCode=Null(query.TypeCode),StatusCode=Null(query.StatusCode),EntityTypeCode=Null(query.EntityTypeCode),query.EntityId,query.AssignedToUserId,Offset=(query.PageNumber-1)*query.PageSize,query.PageSize};using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,parameters,cancellationToken:cancellationToken));return new(){Items=(await multi.ReadAsync<RecommendationDto>()).AsList(),TotalCount=await multi.ReadSingleAsync<int>(),PageNumber=query.PageNumber,PageSize=query.PageSize};
    }

    public async Task GenerateAsync(GenerateRecommendationsRequest request,CancellationToken cancellationToken=default)
    {
        const string sql="""DECLARE @Key NVARCHAR(240)=CONCAT(@EntityTypeCode,N':',CONVERT(nvarchar(36),@EntityId),N':',CONVERT(nvarchar(10),CONVERT(date,SYSUTCDATETIME()),112));IF NOT EXISTS(SELECT 1 FROM AI.RecommendationWorkItem WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND IdempotencyKey=@Key AND IsDeleted=0) INSERT AI.RecommendationWorkItem(TenantId,EntityTypeCode,EntityId,StatusCode,AttemptCount,MaximumAttempts,AvailableDateUtc,CorrelationId,IdempotencyKey,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@TenantId,@EntityTypeCode,@EntityId,N'PENDING',0,5,SYSUTCDATETIME(),@CorrelationId,@Key,SYSUTCDATETIME(),@ActorUserId,0);""";using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);await connection.ExecuteAsync(new CommandDefinition(sql,request,cancellationToken:cancellationToken));
    }

    public async Task DecideRecommendationAsync(DecideRecommendationRequest request,CancellationToken cancellationToken=default)
    {
        const string sql="""UPDATE AI.Recommendation SET StatusCode=CASE @DecisionCode WHEN N'ACCEPT' THEN N'ACCEPTED' WHEN N'COMPLETE' THEN N'COMPLETED' WHEN N'DISMISS' THEN N'DISMISSED' ELSE N'REVIEWED' END,DismissedReason=CASE WHEN @DecisionCode=N'DISMISS' THEN @Reason ELSE DismissedReason END,CompletedDateUtc=CASE WHEN @DecisionCode=N'COMPLETE' THEN SYSUTCDATETIME() ELSE CompletedDateUtc END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND RecommendationId=@RecommendationId AND RowVersion=@RowVersion AND IsDeleted=0 AND StatusCode IN(N'OPEN',N'REVIEW_REQUIRED',N'ACCEPTED');IF @@ROWCOUNT=0 THROW 51000,'Recommendation changed or cannot transition from its current state.',1;""";using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);await connection.ExecuteAsync(new CommandDefinition(sql,request,cancellationToken:cancellationToken));
    }

    public async Task<IntelligenceSearchResponse> SearchAsync(IntelligenceSearchRequest request,IReadOnlyCollection<SemanticConceptMatchDto> concepts,IReadOnlyCollection<string> expandedTerms,CancellationToken cancellationToken=default)
    {
        var timer=Stopwatch.StartNew();
        var intentPatterns=await GetSearchIntentPatternsAsync(request.TenantId,cancellationToken);
        var intent=IntelligenceSearchIntentInterpreter.Interpret(request.Query,intentPatterns);
        var entityTypeCode=Null(request.EntityTypeCode)??intent.EntityTypeCode;
        var moduleCode=Null(request.ModuleCode)??intent.ModuleCode;
        var effectiveQuery=string.IsNullOrWhiteSpace(request.EffectiveSearchText)?(string.IsNullOrWhiteSpace(intent.SearchText)?request.Query:intent.SearchText):request.EffectiveSearchText;
        if(intent.PatternCode?.Equals("PRIMARY_CONTACT_FOR_ACCOUNT",StringComparison.OrdinalIgnoreCase)==true)
            return await SearchPrimaryContactForAccountAsync(request,effectiveQuery,moduleCode,entityTypeCode,concepts,timer,cancellationToken);
        if(intent.PatternCode?.Equals("PRODUCER_FOR_ACCOUNT",StringComparison.OrdinalIgnoreCase)==true)
            return await SearchProducerForAccountAsync(request,effectiveQuery,moduleCode,entityTypeCode,concepts,timer,cancellationToken);
        var terms=expandedTerms.Prepend(effectiveQuery).Where(x=>!string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
        var conceptIds=concepts.Select(x=>x.ConceptId.ToString()).ToArray();
        const string sql="""DECLARE @QueryId UNIQUEIDENTIFIER=NEWID();DECLARE @ConfiguredMaximumResults int=COALESCE(TRY_CONVERT(int,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Search.MaximumResults' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),100);DECLARE @EffectiveMaximumResults int=CASE WHEN @MaximumResults<@ConfiguredMaximumResults THEN @MaximumResults ELSE @ConfiguredMaximumResults END;WITH permitted AS(SELECT DISTINCT document.SearchDocumentId,document.EntityTypeCode,document.EntityId,document.ModuleCode,document.Title,document.ContentText,document.SourceCreatedDateUtc,projection.NavigationRoute,CASE WHEN @IsEntityList=1 AND @EntityTypeCode IS NOT NULL THEN .70 WHEN document.Title=@Query THEN 1.0 WHEN document.Title LIKE '%'+@Query+'%' THEN .85 WHEN document.ContentText LIKE '%'+@Query+'%' OR document.Keywords LIKE '%'+@Query+'%' THEN .70 ELSE 0 END KeywordScore,CASE WHEN @ConceptIdsJson<>N'[]' AND EXISTS(SELECT 1 FROM OPENJSON(@ConceptIdsJson) concept WHERE document.ConceptIdsJson LIKE '%'+CONVERT(nvarchar(36),concept.[value])+'%') THEN 1.0 WHEN @ExpandedTermsJson<>N'[]' AND EXISTS(SELECT 1 FROM OPENJSON(@ExpandedTermsJson) term WHERE document.ContentText LIKE '%'+CONVERT(nvarchar(500),term.[value])+'%' OR document.Keywords LIKE '%'+CONVERT(nvarchar(500),term.[value])+'%') THEN .65 ELSE 0 END SemanticScore FROM AI.SearchDocument document LEFT JOIN Search.EntityProjection projection ON projection.TenantId=document.TenantId AND projection.EntityTypeCode=document.EntityTypeCode AND projection.EntityId=document.EntityId AND projection.IsActive=1 AND projection.IsDeleted=0 WHERE document.TenantId=@TenantId AND document.IsDeleted=0 AND (@ModuleCode IS NULL OR document.ModuleCode=@ModuleCode) AND (@EntityTypeCode IS NULL OR document.EntityTypeCode=@EntityTypeCode) AND EXISTS(SELECT 1 FROM AI.SearchPermission permission WHERE permission.TenantId=document.TenantId AND permission.SearchDocumentId=document.SearchDocumentId AND permission.IsDeleted=0 AND permission.PermissionCode=N'READ' AND ((permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId) OR (permission.PrincipalTypeCode=N'ROLE' AND EXISTS(SELECT 1 FROM IAM.UserRole userRole WHERE userRole.TenantId=@TenantId AND userRole.UserId=@UserId AND userRole.RoleId=permission.PrincipalId AND userRole.IsActive=1 AND userRole.IsDeleted=0))))) SELECT TOP(@EffectiveMaximumResults) SearchDocumentId,EntityTypeCode,EntityId,ModuleCode,Title,LEFT(ContentText,500) Excerpt,KeywordScore,SemanticScore,SourceCreatedDateUtc,NavigationRoute FROM permitted WHERE @OrderByRecency=1 OR @IsEntityList=1 OR KeywordScore>0 OR SemanticScore>0 ORDER BY CASE WHEN @OrderByRecency=1 THEN SourceCreatedDateUtc END DESC,KeywordScore+SemanticScore DESC,Title;INSERT AI.SearchQuery(SearchQueryId,TenantId,UserId,QueryText,NormalizedQuery,SearchModeCode,ExpandedConceptIdsJson,FilterJson,ResultCount,DurationMilliseconds,CorrelationId,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@QueryId,@TenantId,@UserId,@Query,LOWER(LTRIM(RTRIM(@Query))),CASE WHEN @OrderByRecency=1 THEN N'RECENCY' ELSE N'UNIFIED' END,@ConceptIdsJson,@FilterJson,@@ROWCOUNT,@DurationMilliseconds,@CorrelationId,SYSUTCDATETIME(),@UserId,0);SELECT @QueryId;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,new{request.TenantId,request.UserId,Query=effectiveQuery,ModuleCode=moduleCode,EntityTypeCode=entityTypeCode,request.MaximumResults,request.CorrelationId,OrderByRecency=intent.OrderByRecency,IsEntityList=intent.IsEntityList,ConceptIdsJson=JsonSerializer.Serialize(conceptIds),ExpandedTermsJson=JsonSerializer.Serialize(terms),FilterJson=JsonSerializer.Serialize(new{ModuleCode=moduleCode,EntityTypeCode=entityTypeCode,intent.OrderByRecency,intent.IsEntityList,SearchText=effectiveQuery}),DurationMilliseconds=timer.ElapsedMilliseconds},cancellationToken:cancellationToken));
        var rows=(await multi.ReadAsync<SearchRow>()).AsList();
        var queryId=await multi.ReadSingleAsync<Guid>();
        var results=rows.Select(row=>new IntelligenceSearchResultDto(row.SearchDocumentId,row.EntityTypeCode,row.EntityId,row.ModuleCode,row.Title,row.Excerpt,row.KeywordScore,row.SemanticScore,0,concepts.Where(c=>(row.Excerpt??string.Empty).Contains(c.PreferredLabel,StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            NavigationRoute=row.NavigationRoute,
            RecencyScore=Recency(row.SourceCreatedDateUtc,365),
            Explanations=BaseExplanations(row.KeywordScore,row.SemanticScore,row.Title.Equals(request.Query,StringComparison.OrdinalIgnoreCase),concepts)
        }).ToArray();
        return new(queryId,request.Query,terms,results,timer.ElapsedMilliseconds);
    }

    private async Task<IntelligenceSearchResponse> SearchPrimaryContactForAccountAsync(IntelligenceSearchRequest request,string effectiveQuery,string? moduleCode,string? entityTypeCode,IReadOnlyCollection<SemanticConceptMatchDto> concepts,Stopwatch timer,CancellationToken cancellationToken)
    {
        const string sql="""
            DECLARE @QueryId UNIQUEIDENTIFIER=NEWID();
            WITH AccountMatch AS
            (
                SELECT TOP(1) account.AccountId
                FROM Client.Account account
                LEFT JOIN AI.SearchDocument accountDocument ON accountDocument.TenantId=account.TenantId AND accountDocument.EntityTypeCode=N'Account' AND accountDocument.EntityId=account.AccountId AND accountDocument.IsDeleted=0
                WHERE account.TenantId=@TenantId AND account.IsDeleted=0
                  AND (account.AccountName LIKE N'%'+@Query+N'%' OR account.AccountNumber LIKE N'%'+@Query+N'%' OR account.MainEmail LIKE N'%'+@Query+N'%' OR account.MainPhone LIKE N'%'+@Query+N'%' OR accountDocument.Title LIKE N'%'+@Query+N'%' OR accountDocument.ContentText LIKE N'%'+@Query+N'%')
                ORDER BY CASE WHEN account.AccountName=@Query THEN 0 WHEN account.AccountName LIKE @Query+N'%' THEN 1 WHEN account.AccountName LIKE N'%'+@Query+N'%' THEN 2 ELSE 3 END,account.AccountName
            ), PrimaryContact AS
            (
                SELECT TOP(1) document.SearchDocumentId,document.EntityTypeCode,document.EntityId,document.ModuleCode,document.Title,LEFT(document.ContentText,500) Excerpt,CONVERT(decimal(9,6),1.0) KeywordScore,CONVERT(decimal(9,6),0.0) SemanticScore,document.SourceCreatedDateUtc,projection.NavigationRoute
                FROM AccountMatch accountMatch
                JOIN Client.Contact contact ON contact.TenantId=@TenantId AND contact.AccountId=accountMatch.AccountId AND contact.IsDeleted=0 AND (contact.StatusCode IS NULL OR contact.StatusCode=N'Active')
                LEFT JOIN Client.AccountContact accountContact ON accountContact.TenantId=contact.TenantId AND accountContact.AccountId=contact.AccountId AND accountContact.ContactId=contact.ContactId AND accountContact.IsDeleted=0 AND accountContact.IsActive=1 AND accountContact.IsPrimary=1
                JOIN AI.SearchDocument document ON document.TenantId=contact.TenantId AND document.EntityTypeCode=N'Contact' AND document.EntityId=contact.ContactId AND document.IsDeleted=0
                LEFT JOIN Search.EntityProjection projection ON projection.TenantId=document.TenantId AND projection.EntityTypeCode=document.EntityTypeCode AND projection.EntityId=document.EntityId AND projection.IsActive=1 AND projection.IsDeleted=0
                WHERE (@ModuleCode IS NULL OR document.ModuleCode=@ModuleCode) AND (@EntityTypeCode IS NULL OR document.EntityTypeCode=@EntityTypeCode)
                  AND (accountContact.AccountContactId IS NOT NULL OR contact.ContactTypeCode=N'Primary')
                  AND EXISTS(SELECT 1 FROM AI.SearchPermission permission WHERE permission.TenantId=document.TenantId AND permission.SearchDocumentId=document.SearchDocumentId AND permission.IsDeleted=0 AND permission.PermissionCode=N'READ' AND ((permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId) OR (permission.PrincipalTypeCode=N'ROLE' AND EXISTS(SELECT 1 FROM IAM.UserRole userRole WHERE userRole.TenantId=@TenantId AND userRole.UserId=@UserId AND userRole.RoleId=permission.PrincipalId AND userRole.IsActive=1 AND userRole.IsDeleted=0))))
                ORDER BY CASE WHEN accountContact.AccountContactId IS NOT NULL THEN 0 ELSE 1 END,contact.CreatedDateUtc,contact.LastName,contact.FirstName
            )
            SELECT SearchDocumentId,EntityTypeCode,EntityId,ModuleCode,Title,Excerpt,KeywordScore,SemanticScore,SourceCreatedDateUtc,NavigationRoute FROM PrimaryContact;
            DECLARE @ResultCount int=@@ROWCOUNT;
            INSERT AI.SearchQuery(SearchQueryId,TenantId,UserId,QueryText,NormalizedQuery,SearchModeCode,ExpandedConceptIdsJson,FilterJson,ResultCount,DurationMilliseconds,CorrelationId,CreatedDateUtc,CreatedByUserId,IsDeleted)
            VALUES(@QueryId,@TenantId,@UserId,@OriginalQuery,LOWER(LTRIM(RTRIM(@OriginalQuery))),N'UNIFIED',N'[]',@FilterJson,@ResultCount,@DurationMilliseconds,@CorrelationId,SYSUTCDATETIME(),@UserId,0);
            SELECT @QueryId;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,new{request.TenantId,request.UserId,Query=effectiveQuery,OriginalQuery=request.Query,ModuleCode=moduleCode,EntityTypeCode=entityTypeCode,request.CorrelationId,FilterJson=JsonSerializer.Serialize(new{ModuleCode=moduleCode,EntityTypeCode=entityTypeCode,PrimaryContactOnly=true,SearchText=effectiveQuery}),DurationMilliseconds=timer.ElapsedMilliseconds},cancellationToken:cancellationToken));
        var rows=(await multi.ReadAsync<SearchRow>()).AsList();
        var queryId=await multi.ReadSingleAsync<Guid>();
        var results=rows.Select(row=>new IntelligenceSearchResultDto(row.SearchDocumentId,row.EntityTypeCode,row.EntityId,row.ModuleCode,row.Title,row.Excerpt,row.KeywordScore,row.SemanticScore,0,concepts.Where(c=>(row.Excerpt??string.Empty).Contains(c.PreferredLabel,StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            NavigationRoute=row.NavigationRoute,
            RecencyScore=Recency(row.SourceCreatedDateUtc,365),
            Explanations=[new("PRIMARY_CONTACT","Primary contact","Matched the account's DB-marked primary contact.",1,"DB_PATTERN")]
        }).ToArray();
        return new(queryId,request.Query,[effectiveQuery],results,timer.ElapsedMilliseconds);
    }

    private async Task<IntelligenceSearchResponse> SearchProducerForAccountAsync(IntelligenceSearchRequest request,string effectiveQuery,string? moduleCode,string? entityTypeCode,IReadOnlyCollection<SemanticConceptMatchDto> concepts,Stopwatch timer,CancellationToken cancellationToken)
    {
        const string sql="""
            DECLARE @QueryId UNIQUEIDENTIFIER=NEWID();
            WITH AccountMatch AS
            (
                SELECT TOP(1) account.AccountId,account.AccountOwnerUserId
                FROM Client.Account account
                LEFT JOIN AI.SearchDocument accountDocument ON accountDocument.TenantId=account.TenantId AND accountDocument.EntityTypeCode=N'Account' AND accountDocument.EntityId=account.AccountId AND accountDocument.IsDeleted=0
                WHERE account.TenantId=@TenantId AND account.IsDeleted=0
                  AND (account.AccountName LIKE N'%'+@Query+N'%' OR account.AccountNumber LIKE N'%'+@Query+N'%' OR account.MainEmail LIKE N'%'+@Query+N'%' OR account.MainPhone LIKE N'%'+@Query+N'%' OR accountDocument.Title LIKE N'%'+@Query+N'%' OR accountDocument.ContentText LIKE N'%'+@Query+N'%')
                ORDER BY CASE WHEN account.AccountName=@Query THEN 0 WHEN account.AccountName LIKE @Query+N'%' THEN 1 WHEN account.AccountName LIKE N'%'+@Query+N'%' THEN 2 ELSE 3 END,account.AccountName
            ), ProducerMatch AS
            (
                SELECT TOP(1) document.SearchDocumentId,document.EntityTypeCode,document.EntityId,document.ModuleCode,document.Title,LEFT(document.ContentText,500) Excerpt,CONVERT(decimal(9,6),1.0) KeywordScore,CONVERT(decimal(9,6),0.0) SemanticScore,document.SourceCreatedDateUtc,projection.NavigationRoute
                FROM AccountMatch accountMatch
                JOIN Agency.Staff staff ON staff.TenantId=@TenantId AND staff.IsDeleted=0 AND staff.IsActive=1 AND staff.Role=N'Producer'
                LEFT JOIN Client.AccountServiceAssignment assignment ON assignment.TenantId=@TenantId AND assignment.AccountId=accountMatch.AccountId AND assignment.UserId=staff.UserId AND assignment.IsDeleted=0 AND UPPER(assignment.AssignmentRoleCode) IN(N'PRODUCER',N'ACCOUNT_PRODUCER') AND assignment.EffectiveDate<=CONVERT(date,SYSUTCDATETIME()) AND (assignment.ExpirationDate IS NULL OR assignment.ExpirationDate>=CONVERT(date,SYSUTCDATETIME()))
                JOIN AI.SearchDocument document ON document.TenantId=staff.TenantId AND document.EntityTypeCode=N'Producer' AND document.EntityId=staff.StaffId AND document.IsDeleted=0
                LEFT JOIN Search.EntityProjection projection ON projection.TenantId=document.TenantId AND projection.EntityTypeCode=document.EntityTypeCode AND projection.EntityId=document.EntityId AND projection.IsActive=1 AND projection.IsDeleted=0
                WHERE (@ModuleCode IS NULL OR document.ModuleCode=@ModuleCode) AND (@EntityTypeCode IS NULL OR document.EntityTypeCode=@EntityTypeCode)
                  AND (assignment.AccountServiceAssignmentId IS NOT NULL OR accountMatch.AccountOwnerUserId=staff.UserId)
                  AND EXISTS(SELECT 1 FROM AI.SearchPermission permission WHERE permission.TenantId=document.TenantId AND permission.SearchDocumentId=document.SearchDocumentId AND permission.IsDeleted=0 AND permission.PermissionCode=N'READ' AND ((permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId) OR (permission.PrincipalTypeCode=N'ROLE' AND EXISTS(SELECT 1 FROM IAM.UserRole userRole WHERE userRole.TenantId=@TenantId AND userRole.UserId=@UserId AND userRole.RoleId=permission.PrincipalId AND userRole.IsActive=1 AND userRole.IsDeleted=0))))
                ORDER BY CASE WHEN assignment.AccountServiceAssignmentId IS NOT NULL THEN 0 ELSE 1 END,CASE WHEN assignment.IsPrimary=1 THEN 0 ELSE 1 END,assignment.EffectiveDate DESC,staff.LastName,staff.FirstName
            )
            SELECT SearchDocumentId,EntityTypeCode,EntityId,ModuleCode,Title,Excerpt,KeywordScore,SemanticScore,SourceCreatedDateUtc,NavigationRoute FROM ProducerMatch;
            DECLARE @ResultCount int=@@ROWCOUNT;
            INSERT AI.SearchQuery(SearchQueryId,TenantId,UserId,QueryText,NormalizedQuery,SearchModeCode,ExpandedConceptIdsJson,FilterJson,ResultCount,DurationMilliseconds,CorrelationId,CreatedDateUtc,CreatedByUserId,IsDeleted)
            VALUES(@QueryId,@TenantId,@UserId,@OriginalQuery,LOWER(LTRIM(RTRIM(@OriginalQuery))),N'UNIFIED',N'[]',@FilterJson,@ResultCount,@DurationMilliseconds,@CorrelationId,SYSUTCDATETIME(),@UserId,0);
            SELECT @QueryId;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,new{request.TenantId,request.UserId,Query=effectiveQuery,OriginalQuery=request.Query,ModuleCode=moduleCode,EntityTypeCode=entityTypeCode,request.CorrelationId,FilterJson=JsonSerializer.Serialize(new{ModuleCode=moduleCode,EntityTypeCode=entityTypeCode,ProducerForAccount=true,SearchText=effectiveQuery}),DurationMilliseconds=timer.ElapsedMilliseconds},cancellationToken:cancellationToken));
        var rows=(await multi.ReadAsync<SearchRow>()).AsList();
        var queryId=await multi.ReadSingleAsync<Guid>();
        var results=rows.Select(row=>new IntelligenceSearchResultDto(row.SearchDocumentId,row.EntityTypeCode,row.EntityId,row.ModuleCode,row.Title,row.Excerpt,row.KeywordScore,row.SemanticScore,0,concepts.Where(c=>(row.Excerpt??string.Empty).Contains(c.PreferredLabel,StringComparison.OrdinalIgnoreCase)).ToArray())
        {
            NavigationRoute=row.NavigationRoute,
            RecencyScore=Recency(row.SourceCreatedDateUtc,365),
            Explanations=[new("ACCOUNT_PRODUCER","Account producer","Matched the account's DB-backed producer assignment.",1,"DB_PATTERN")]
        }).ToArray();
        return new(queryId,request.Query,[effectiveQuery],results,timer.ElapsedMilliseconds);
    }

    public async Task CompleteUnifiedSearchAsync(Guid tenantId,Guid userId,Guid searchQueryId,string normalizedQuery,IntelligenceSearchWeightsDto weights,IReadOnlyCollection<IntelligenceSearchResultDto> results,string summaryStatusCode,Guid? summaryExecutionId,long durationMilliseconds,CancellationToken cancellationToken=default)
    {
        const string updateSql="""UPDATE AI.SearchQuery SET NormalizedQuery=@NormalizedQuery,SearchModeCode=N'UNIFIED',ResultCount=@ResultCount,DurationMilliseconds=@DurationMilliseconds,ScoringWeightsJson=@WeightsJson,SummaryStatusCode=@SummaryStatusCode,SummaryExecutionId=@SummaryExecutionId,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE SearchQueryId=@SearchQueryId AND TenantId=@TenantId AND UserId=@UserId AND IsDeleted=0;DELETE AI.SearchResultEvidence WHERE TenantId=@TenantId AND SearchQueryId=@SearchQueryId;""";
        const string insertSql="""INSERT AI.SearchResultEvidence(SearchResultEvidenceId,TenantId,SearchQueryId,SearchDocumentId,EntityTypeCode,EntityId,RankNumber,KeywordScore,SemanticScore,FuzzyScore,RelationshipScore,RecencyScore,BusinessPriorityScore,UnifiedScore,ExplanationsJson,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@SearchQueryId,@SearchDocumentId,@EntityTypeCode,@EntityId,@RankNumber,@KeywordScore,@SemanticScore,@FuzzyScore,@RelationshipScore,@RecencyScore,@BusinessPriorityScore,@UnifiedScore,@ExplanationsJson,SYSUTCDATETIME(),@UserId,0);""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction=connection.BeginTransaction();
        await connection.ExecuteAsync(new CommandDefinition(updateSql,new{TenantId=tenantId,UserId=userId,SearchQueryId=searchQueryId,NormalizedQuery=normalizedQuery,ResultCount=results.Count,DurationMilliseconds=durationMilliseconds,WeightsJson=JsonSerializer.Serialize(weights),SummaryStatusCode=summaryStatusCode,SummaryExecutionId=summaryExecutionId},transaction,cancellationToken:cancellationToken));
        var rank=0;
        foreach(var result in results)await connection.ExecuteAsync(new CommandDefinition(insertSql,new{TenantId=tenantId,UserId=userId,SearchQueryId=searchQueryId,result.SearchDocumentId,result.EntityTypeCode,result.EntityId,RankNumber=++rank,result.KeywordScore,result.SemanticScore,result.FuzzyScore,result.RelationshipScore,result.RecencyScore,result.BusinessPriorityScore,UnifiedScore=result.CombinedScore,ExplanationsJson=JsonSerializer.Serialize(result.Explanations)},transaction,cancellationToken:cancellationToken));
        transaction.Commit();
    }

    public async Task<PagedResult<AiReviewQueueItemDto>> SearchReviewQueueAsync(SearchAiReviewQueueQuery query,CancellationToken cancellationToken=default)
    {
        const string from=""" FROM AI.ReviewQueueItem WHERE TenantId=@TenantId AND IsDeleted=0 AND (@SearchTerm IS NULL OR Title LIKE '%'+@SearchTerm+'%' OR Summary LIKE '%'+@SearchTerm+'%') AND (@ReviewTypeCode IS NULL OR ReviewTypeCode=@ReviewTypeCode) AND (@StatusCode IS NULL OR StatusCode=@StatusCode) AND (@PriorityCode IS NULL OR PriorityCode=@PriorityCode) AND (@AssignedToUserId IS NULL OR AssignedToUserId=@AssignedToUserId)""";var sql=$"""SELECT ReviewQueueItemId,ReviewTypeCode,SourceEntityTypeCode,SourceEntityId,ExecutionId,Title,Summary,PriorityCode,StatusCode,Confidence,AssignedToUserId,DueDateUtc,CreatedDateUtc,RowVersion {from} ORDER BY CASE PriorityCode WHEN N'CRITICAL' THEN 1 WHEN N'HIGH' THEN 2 WHEN N'NORMAL' THEN 3 ELSE 4 END,DueDateUtc,CreatedDateUtc OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;SELECT COUNT(1) {from};""";var parameters=new{query.TenantId,SearchTerm=Null(query.SearchTerm),ReviewTypeCode=Null(query.ReviewTypeCode),StatusCode=Null(query.StatusCode),PriorityCode=Null(query.PriorityCode),query.AssignedToUserId,Offset=(query.PageNumber-1)*query.PageSize,query.PageSize};using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,parameters,cancellationToken:cancellationToken));return new(){Items=(await multi.ReadAsync<AiReviewQueueItemDto>()).AsList(),TotalCount=await multi.ReadSingleAsync<int>(),PageNumber=query.PageNumber,PageSize=query.PageSize};
    }

    public async Task DecideReviewAsync(DecideAiReviewRequest request,CancellationToken cancellationToken=default)
    {
        const string sql="""UPDATE AI.ReviewQueueItem SET StatusCode=N'COMPLETED',DecisionCode=@DecisionCode,DecisionReason=@Reason,ReviewedByUserId=@ActorUserId,ReviewedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND ReviewQueueItemId=@ReviewQueueItemId AND RowVersion=@RowVersion AND IsDeleted=0 AND StatusCode IN(N'OPEN',N'ASSIGNED',N'IN_REVIEW');IF @@ROWCOUNT=0 THROW 51000,'Review item changed or cannot transition from its current state.',1;""";using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);await connection.ExecuteAsync(new CommandDefinition(sql,request,cancellationToken:cancellationToken));
    }

    public async Task<IReadOnlyCollection<AiEvaluationDefinitionDto>> GetEvaluationDefinitionsAsync(Guid tenantId,CancellationToken cancellationToken=default){const string sql="""SELECT EvaluationDefinitionId,TenantId,EvaluationCode,DisplayName,FeatureCode,MetricCode,CalculationCode,TargetValue,WarningValue,WindowHours,MinimumSampleSize,IsActive,RowVersion FROM AI.EvaluationDefinition WHERE (TenantId IS NULL OR TenantId=@TenantId) AND IsDeleted=0 ORDER BY FeatureCode,DisplayName;""";using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);return (await connection.QueryAsync<AiEvaluationDefinitionDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken))).AsList();}
    public async Task<IReadOnlyCollection<AiEvaluationRunDto>> GetEvaluationRunsAsync(Guid tenantId,int pageSize,CancellationToken cancellationToken=default){const string sql="""SELECT TOP(@PageSize) run.EvaluationRunId,run.TenantId,definition.EvaluationCode,definition.DisplayName,definition.FeatureCode,definition.MetricCode,run.WindowStartUtc,run.WindowEndUtc,run.StatusCode,run.SampleCount,run.MetricValue,run.Passed,run.DetailsJson,run.ErrorMessage,run.StartedDateUtc,run.CompletedDateUtc FROM AI.EvaluationRun run JOIN AI.EvaluationDefinition definition ON definition.EvaluationDefinitionId=run.EvaluationDefinitionId WHERE (run.TenantId=@TenantId OR (run.TenantId IS NULL AND definition.TenantId IS NULL)) AND run.IsDeleted=0 ORDER BY run.CreatedDateUtc DESC;""";using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);return (await connection.QueryAsync<AiEvaluationRunDto>(new CommandDefinition(sql,new{TenantId=tenantId,PageSize=pageSize},cancellationToken:cancellationToken))).AsList();}
    public async Task<Guid> QueueEvaluationAsync(QueueAiEvaluationRequest request,CancellationToken cancellationToken=default){const string sql="""IF NOT EXISTS(SELECT 1 FROM AI.EvaluationDefinition WHERE EvaluationDefinitionId=@EvaluationDefinitionId AND (TenantId IS NULL OR TenantId=@TenantId) AND IsActive=1 AND IsDeleted=0) THROW 51000,'Active evaluation definition was not found.',1;DECLARE @Id UNIQUEIDENTIFIER=NEWID();INSERT AI.EvaluationRun(EvaluationRunId,TenantId,EvaluationDefinitionId,WindowStartUtc,WindowEndUtc,StatusCode,SampleCount,StartedDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@Id,@TenantId,@EvaluationDefinitionId,@WindowStartUtc,@WindowEndUtc,N'QUEUED',0,SYSUTCDATETIME(),SYSUTCDATETIME(),@ActorUserId,0);SELECT @Id;""";using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql,request,cancellationToken:cancellationToken));}

    public async Task<IntelligenceDashboardDto> GetDashboardAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""DECLARE @Today DATETIME2=CONVERT(date,SYSUTCDATETIME());SELECT COUNT(1) ExecutionsToday,COUNT(CASE WHEN StatusCode=N'FAILED' THEN 1 END) FailedExecutionsToday,COALESCE(SUM(EstimatedCost),0) EstimatedCostToday,AVG(Confidence) AverageConfidenceToday,AVG(DurationMilliseconds) AverageDurationMillisecondsToday FROM AI.Execution WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Today;SELECT COUNT(1) FROM AI.ReviewQueueItem WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode IN(N'OPEN',N'ASSIGNED',N'IN_REVIEW');SELECT COUNT(1) FROM AI.Recommendation WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode IN(N'OPEN',N'REVIEW_REQUIRED',N'ACCEPTED');SELECT COUNT(1) FROM AI.SearchQuery WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Today;SELECT COALESCE((SELECT TOP(1) CONVERT(int,MetricValue) FROM AI.ModuleMetricSnapshot WHERE TenantId=@TenantId AND ModuleCode=N'KNOWLEDGE' AND MetricCode=N'CHANGES_TODAY' AND IsDeleted=0 ORDER BY WindowEndUtc DESC),0) KnowledgeChangesToday,COALESCE((SELECT TOP(1) CONVERT(int,MetricValue) FROM AI.ModuleMetricSnapshot WHERE TenantId=@TenantId AND ModuleCode=N'KNOWLEDGE' AND MetricCode=N'IMPORTS_IN_PROGRESS' AND IsDeleted=0 ORDER BY WindowEndUtc DESC),0) ImportJobsInProgress,(SELECT COUNT(1) FROM AI.RecommendationWorkItem WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode IN(N'PENDING',N'PROCESSING',N'RETRY')) WorkerQueueDepth;SELECT FeatureCode,COUNT(1) ExecutionCount,COUNT(CASE WHEN StatusCode=N'FAILED' THEN 1 END) FailedCount,COALESCE(SUM(EstimatedCost),0) EstimatedCost,AVG(Confidence) AverageConfidence,AVG(DurationMilliseconds) AverageDurationMilliseconds FROM AI.Execution WHERE TenantId=@TenantId AND IsDeleted=0 AND CreatedDateUtc>=@Today GROUP BY FeatureCode ORDER BY ExecutionCount DESC;SELECT type.TypeCode,type.DisplayName,COUNT(CASE WHEN recommendation.StatusCode IN(N'OPEN',N'REVIEW_REQUIRED') THEN 1 END) OpenCount,COUNT(CASE WHEN recommendation.StatusCode IN(N'ACCEPTED',N'COMPLETED') THEN 1 END) AcceptedCount,COUNT(CASE WHEN recommendation.StatusCode=N'DISMISSED' THEN 1 END) DismissedCount FROM AI.RecommendationType type LEFT JOIN AI.Recommendation recommendation ON recommendation.RecommendationTypeId=type.RecommendationTypeId AND recommendation.TenantId=@TenantId AND recommendation.IsDeleted=0 WHERE (type.TenantId IS NULL OR type.TenantId=@TenantId) AND type.IsDeleted=0 GROUP BY type.TypeCode,type.DisplayName ORDER BY OpenCount DESC,type.DisplayName;""";using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken));var totals=await multi.ReadSingleAsync<DashboardTotals>();var reviews=await multi.ReadSingleAsync<int>();var recommendations=await multi.ReadSingleAsync<int>();var searches=await multi.ReadSingleAsync<int>();var modules=await multi.ReadSingleAsync<ModuleMetrics>();var usage=(await multi.ReadAsync<IntelligenceUsageMetricDto>()).AsList();var byType=(await multi.ReadAsync<RecommendationTypeMetricDto>()).AsList();return new(DateTime.UtcNow,totals.ExecutionsToday,totals.FailedExecutionsToday,totals.EstimatedCostToday,totals.AverageConfidenceToday,totals.AverageDurationMillisecondsToday,reviews,recommendations,searches,modules.KnowledgeChangesToday,modules.ImportJobsInProgress,modules.WorkerQueueDepth,usage,byType);
    }

    private static string? Null(string? value)=>string.IsNullOrWhiteSpace(value)?null:value.Trim();
    private static IntelligenceSearchResultDto ToSearchResult(AuthorizedSearchRow row)=>new(row.SearchDocumentId,row.EntityTypeCode,row.EntityId,row.ModuleCode,row.Title,row.Excerpt,0,0,0,[]){NavigationRoute=row.NavigationRoute,RecencyScore=Recency(row.SourceCreatedDateUtc,365)};
    private static IntelligenceSearchResultDto WithExtractedFieldExplanation(IntelligenceSearchResultDto result,bool extractedFieldMatch)=>!extractedFieldMatch?result:result with{Explanations=result.Explanations.Append(new("EXTRACTED_DOCUMENT_FIELD","Extracted document field","The query matched a governed field extracted by Document Intelligence.",1,"DOCUMENT_INTELLIGENCE")).ToArray()};
    private static decimal Recency(DateTime? sourceDateUtc,int windowDays)=>sourceDateUtc is null?0:Math.Clamp(1m-(decimal)(DateTime.UtcNow-sourceDateUtc.Value).TotalDays/Math.Max(1,windowDays),0,1);
    private static IReadOnlyCollection<IntelligenceSearchMatchExplanationDto> BaseExplanations(decimal keywordScore,decimal semanticScore,bool exactTitle,IReadOnlyCollection<SemanticConceptMatchDto> concepts)
    {
        var explanations=new List<IntelligenceSearchMatchExplanationDto>();
        if(exactTitle)explanations.Add(new("EXACT_TITLE","Exact title match","The normalized query exactly matched the result title.",1,"KEYWORD_RETRIEVAL"));
        else if(keywordScore>0)explanations.Add(new("KEYWORD_MATCH","Keyword match","The query matched indexed title, keywords, or document text.",keywordScore,"KEYWORD_RETRIEVAL"));
        if(semanticScore>0)explanations.Add(new("CONCEPT_MATCH","Synonym or concept match","Knowledge concepts or approved expanded terms matched this result.",semanticScore,"KNOWLEDGE_CONCEPT"));
        foreach(var concept in concepts.Where(concept=>concept.Score>0).Take(3))explanations.Add(new(concept.MatchReasonCode,"Concept evidence",$"Matched knowledge concept {concept.PreferredLabel}.",concept.Score,"KNOWLEDGE_CONCEPT"));
        return explanations;
    }

    private sealed record SearchRow(Guid SearchDocumentId,string EntityTypeCode,Guid EntityId,string ModuleCode,string Title,string? Excerpt,decimal KeywordScore,decimal SemanticScore,DateTime? SourceCreatedDateUtc,string? NavigationRoute);
    private sealed class AuthorizedSearchRow
    {
        public Guid SearchDocumentId { get; init; }
        public string EntityTypeCode { get; init; } = string.Empty;
        public Guid EntityId { get; init; }
        public string ModuleCode { get; init; } = string.Empty;
        public string Title { get; init; } = string.Empty;
        public string? Excerpt { get; init; }
        public DateTime? SourceCreatedDateUtc { get; init; }
        public string? NavigationRoute { get; init; }
        public decimal RelationshipScore { get; init; }
        public string? RelationshipTypeCode { get; init; }
        public bool ExtractedFieldMatch { get; init; }
    }
    private sealed record SearchConfigurationRow(decimal KeywordWeight,decimal SemanticWeight,decimal FuzzyWeight,decimal RelationshipWeight,decimal RecencyWeight,decimal BusinessPriorityWeight,int RecencyWindowDays,int MaximumRelationshipResults,decimal MinimumUnifiedScore,bool EnableRules,bool EnableRelationships,bool EnableAiSummary,bool EnableLlmIntentFallback,decimal LlmIntentMinimumConfidence,int LlmIntentTimeoutSeconds);
    private sealed record IntentPatternRow(string PatternCode,string? EntityTypeCode,string? ModuleCode,string ExtractionStrategyCode,int Priority,bool IsEntityList);
    private sealed record IntentPatternPhraseRow(string PatternCode,string PhraseKindCode,string PhraseText,int SortOrder);
    private sealed record DashboardTotals(int ExecutionsToday,int FailedExecutionsToday,decimal EstimatedCostToday,decimal? AverageConfidenceToday,long? AverageDurationMillisecondsToday);
    private sealed record ModuleMetrics(int KnowledgeChangesToday,int ImportJobsInProgress,int WorkerQueueDepth);
}
