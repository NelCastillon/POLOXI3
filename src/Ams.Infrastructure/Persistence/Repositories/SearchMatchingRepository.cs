using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.SearchMatching;
using Ams.Application.Features.Intelligence;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SearchMatchingRepository(ISqlConnectionFactory connectionFactory) : ISearchMatchingRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SearchMatchingAdministration> GetAdministrationAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
;WITH profiles AS (SELECT profile.*,ROW_NUMBER() OVER(PARTITION BY ProfileCode ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END) Choice FROM Search.MatchProfile profile WHERE IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL)) SELECT MatchProfileId,CAST(CASE WHEN TenantId IS NULL THEN 1 ELSE 0 END AS bit) IsInherited,ProfileCode,EntityTypeCode,DisplayName,Description,ExactThreshold,StrongThreshold,PossibleThreshold,MaximumCandidates,SemanticMaximumConcepts,RequiresReview,IsActive,RowVersion FROM profiles WHERE Choice=1 ORDER BY ProfileCode;
SELECT fieldRule.MatchFieldRuleId,CAST(CASE WHEN fieldRule.TenantId IS NULL THEN 1 ELSE 0 END AS bit) IsInherited,fieldRule.MatchProfileId,fieldRule.FieldCode,fieldRule.DisplayName,fieldRule.MatchAlgorithmId,algorithm.AlgorithmCode,fieldRule.Weight,fieldRule.MinimumSimilarity,fieldRule.IsRequired,fieldRule.IsCriticalIdentifier,fieldRule.ExactMatchOnly,fieldRule.IsSensitive,fieldRule.SortOrder,fieldRule.IsActive,fieldRule.RowVersion FROM Search.MatchFieldRule fieldRule JOIN Search.MatchProfile profile ON profile.MatchProfileId=fieldRule.MatchProfileId JOIN Search.MatchAlgorithm algorithm ON algorithm.MatchAlgorithmId=fieldRule.MatchAlgorithmId WHERE fieldRule.IsDeleted=0 AND profile.IsDeleted=0 AND (fieldRule.TenantId=@TenantId OR fieldRule.TenantId IS NULL) AND (profile.TenantId=@TenantId OR profile.TenantId IS NULL) ORDER BY profile.ProfileCode,fieldRule.SortOrder;
;WITH algorithms AS (SELECT algorithm.*,ROW_NUMBER() OVER(PARTITION BY AlgorithmCode ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END) Choice FROM Search.MatchAlgorithm algorithm WHERE IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL)) SELECT MatchAlgorithmId,CAST(CASE WHEN TenantId IS NULL THEN 1 ELSE 0 END AS bit) IsInherited,AlgorithmCode,DisplayName,AlgorithmKindCode,Description,ConfigurationJson,IsActive,RowVersion FROM algorithms WHERE Choice=1 ORDER BY AlgorithmCode;
SELECT NormalizationTermId,CAST(CASE WHEN TenantId IS NULL THEN 1 ELSE 0 END AS bit) IsInherited,EntityTypeCode,FieldCode,SourceValue,NormalizedValue,TermKindCode,CultureCode,SortOrder,IsActive,RowVersion FROM Search.NormalizationTerm WHERE IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY EntityTypeCode,FieldCode,SortOrder,IsInherited;
;WITH capabilities AS (SELECT capability.*,ROW_NUMBER() OVER(PARTITION BY CapabilityCode ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END) Choice FROM Search.SearchCapability capability WHERE IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL)) SELECT CapabilityCode,DisplayName,IsAvailable,IsEnabled,ConfigurationJson,LastVerifiedDateUtc,LastError FROM capabilities WHERE Choice=1 ORDER BY CapabilityCode;
SELECT
 (SELECT COUNT_BIG(*) FROM Search.MatchExecution WHERE TenantId=@TenantId AND IsDeleted=0) ExecutionCount,
 (SELECT COUNT_BIG(*) FROM Search.MatchExecution WHERE TenantId=@TenantId AND StatusCode=N'COMPLETED' AND IsDeleted=0) CompletedExecutionCount,
 (SELECT COUNT_BIG(*) FROM Search.MatchExecution WHERE TenantId=@TenantId AND StatusCode=N'FAILED' AND IsDeleted=0) FailedExecutionCount,
 (SELECT COUNT_BIG(*) FROM Search.SemanticQueryEvidence WHERE TenantId=@TenantId AND IsDeleted=0) SemanticEvidenceCount,
 (SELECT COUNT_BIG(*) FROM Search.MatchReviewDecision WHERE TenantId=@TenantId AND IsDeleted=0) ReviewDecisionCount,
 (SELECT COUNT_BIG(*) FROM CRM.DuplicateGroup WHERE TenantId=@TenantId AND StatusCode IN(N'Open',N'Under Review') AND IsDeleted=0) OpenDuplicateGroupCount,
 (SELECT MAX(StartedDateUtc) FROM Search.MatchExecution WHERE TenantId=@TenantId AND IsDeleted=0) LastExecutionDateUtc,
 (SELECT MAX(CreatedDateUtc) FROM Search.SemanticQueryEvidence WHERE TenantId=@TenantId AND IsDeleted=0) LastSemanticEvidenceDateUtc,
 (SELECT MAX(CreatedDateUtc) FROM Search.MatchReviewDecision WHERE TenantId=@TenantId AND IsDeleted=0) LastReviewDecisionDateUtc;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        var profiles = (await multi.ReadAsync<ProfileSettingRow>()).AsList();
        var rules = (await multi.ReadAsync<MatchFieldRuleSetting>()).AsList();
        var algorithms = (await multi.ReadAsync<MatchAlgorithmSetting>()).AsList();
        var terms = (await multi.ReadAsync<NormalizationTermSetting>()).AsList();
        var capabilities = (await multi.ReadAsync<SearchCapabilitySetting>()).AsList();
        var telemetry = await multi.ReadSingleAsync<SearchMatchingOperationalTelemetry>();
        return new(profiles.Select(profile => new MatchProfileSetting(profile.MatchProfileId, profile.IsInherited, profile.ProfileCode, profile.EntityTypeCode, profile.DisplayName, profile.Description, profile.ExactThreshold, profile.StrongThreshold, profile.PossibleThreshold, profile.MaximumCandidates, profile.SemanticMaximumConcepts, profile.RequiresReview, profile.IsActive, profile.RowVersion, rules.Where(rule => rule.MatchProfileId == profile.MatchProfileId).ToList())).ToList(), algorithms, terms, capabilities, telemetry);
    }

    public async Task<SemanticPreprocessingSettings> GetSemanticPreprocessingSettingsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
;WITH capabilities AS
(
    SELECT capability.*,ROW_NUMBER() OVER(PARTITION BY CapabilityCode ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END) Choice
    FROM Search.SearchCapability capability
    WHERE CapabilityCode=N'SEMANTIC' AND IsEnabled=1 AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL)
)
SELECT COALESCE(TRY_CAST(JSON_VALUE(ConfigurationJson,N'$.maximumTokens') AS int),12) MaximumTokens,
       COALESCE(TRY_CAST(JSON_VALUE(ConfigurationJson,N'$.maximumPhraseLength') AS int),3) MaximumPhraseLength,
       COALESCE(TRY_CAST(JSON_VALUE(ConfigurationJson,N'$.maximumPhrases') AS int),30) MaximumPhrases
FROM capabilities WHERE Choice=1;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<SemanticPreprocessingSettings>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken)) ?? new(12, 3, 30);
    }

    public async Task<Guid> SaveProfileAsync(Guid tenantId, Guid actorUserId, SaveMatchProfileSettingRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = """
IF EXISTS(SELECT 1 FROM Search.MatchProfile WHERE TenantId=@TenantId AND ProfileCode=@ProfileCode AND MatchProfileId<>@Id AND IsDeleted=0) THROW 51000,N'A tenant override with this code already exists.',1;
IF @RowVersion IS NULL
BEGIN
    INSERT Search.MatchProfile(MatchProfileId,TenantId,ProfileCode,EntityTypeCode,DisplayName,Description,ExactThreshold,StrongThreshold,PossibleThreshold,MaximumCandidates,SemanticMaximumConcepts,AllowAutomaticLink,RequiresReview,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted)
    VALUES(@Id,@TenantId,@ProfileCode,@EntityTypeCode,@DisplayName,@Description,@ExactThreshold,@StrongThreshold,@PossibleThreshold,@MaximumCandidates,@SemanticMaximumConcepts,0,@RequiresReview,@IsActive,SYSUTCDATETIME(),@ActorUserId,0);
    DECLARE @PlatformProfileId UNIQUEIDENTIFIER=(SELECT TOP(1) MatchProfileId FROM Search.MatchProfile WHERE TenantId IS NULL AND ProfileCode=@ProfileCode AND IsDeleted=0);
    INSERT Search.MatchFieldRule(MatchFieldRuleId,TenantId,MatchProfileId,FieldCode,DisplayName,MatchAlgorithmId,Weight,MinimumSimilarity,IsRequired,IsCriticalIdentifier,ExactMatchOnly,IsSensitive,SortOrder,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted)
    SELECT NEWID(),@TenantId,@Id,fieldRule.FieldCode,fieldRule.DisplayName,COALESCE(tenantAlgorithm.MatchAlgorithmId,fieldRule.MatchAlgorithmId),fieldRule.Weight,fieldRule.MinimumSimilarity,fieldRule.IsRequired,fieldRule.IsCriticalIdentifier,fieldRule.ExactMatchOnly,fieldRule.IsSensitive,fieldRule.SortOrder,fieldRule.IsActive,SYSUTCDATETIME(),@ActorUserId,0
    FROM Search.MatchFieldRule fieldRule JOIN Search.MatchAlgorithm platformAlgorithm ON platformAlgorithm.MatchAlgorithmId=fieldRule.MatchAlgorithmId LEFT JOIN Search.MatchAlgorithm tenantAlgorithm ON tenantAlgorithm.TenantId=@TenantId AND tenantAlgorithm.AlgorithmCode=platformAlgorithm.AlgorithmCode AND tenantAlgorithm.IsActive=1 AND tenantAlgorithm.IsDeleted=0 WHERE fieldRule.MatchProfileId=@PlatformProfileId AND fieldRule.TenantId IS NULL AND fieldRule.IsDeleted=0;
END
ELSE
BEGIN
    UPDATE Search.MatchProfile SET ProfileCode=@ProfileCode,EntityTypeCode=@EntityTypeCode,DisplayName=@DisplayName,Description=@Description,ExactThreshold=@ExactThreshold,StrongThreshold=@StrongThreshold,PossibleThreshold=@PossibleThreshold,MaximumCandidates=@MaximumCandidates,SemanticMaximumConcepts=@SemanticMaximumConcepts,AllowAutomaticLink=0,RequiresReview=@RequiresReview,IsActive=@IsActive,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId
    WHERE MatchProfileId=@Id AND TenantId=@TenantId AND RowVersion=@RowVersion AND IsDeleted=0;
    IF @@ROWCOUNT=0 THROW 51000,N'The tenant setting changed or was not found.',1;
END;
SELECT @Id;
""";
        var parameters = new DynamicParameters(request);
        parameters.Add("TenantId", tenantId);
        parameters.Add("ActorUserId", actorUserId);
        parameters.Add("Id", request.MatchProfileId ?? Guid.NewGuid());
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        try
        {
            var id = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, parameters, transaction, cancellationToken: cancellationToken));
            transaction.Commit();
            return id;
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public Task<Guid> SaveFieldRuleAsync(Guid tenantId, Guid actorUserId, SaveMatchFieldRuleSettingRequest request, CancellationToken cancellationToken = default)
        => SaveAsync(tenantId, actorUserId, request.MatchFieldRuleId, request.RowVersion, "Search.MatchFieldRule", "MatchFieldRuleId", null, null, "MatchProfileId=@MatchProfileId,FieldCode=@FieldCode,DisplayName=@DisplayName,MatchAlgorithmId=@MatchAlgorithmId,Weight=@Weight,MinimumSimilarity=@MinimumSimilarity,IsRequired=@IsRequired,IsCriticalIdentifier=@IsCriticalIdentifier,ExactMatchOnly=@ExactMatchOnly,IsSensitive=@IsSensitive,SortOrder=@SortOrder,IsActive=@IsActive", request, cancellationToken, "IF @IsCriticalIdentifier=1 AND @ExactMatchOnly=0 THROW 51000,N'Critical identifiers must use exact-match-only enforcement.',1; IF NOT EXISTS(SELECT 1 FROM Search.MatchProfile WHERE MatchProfileId=@MatchProfileId AND TenantId=@TenantId AND IsDeleted=0) THROW 51000,N'Tenant match profile was not found.',1; IF NOT EXISTS(SELECT 1 FROM Search.MatchAlgorithm WHERE MatchAlgorithmId=@MatchAlgorithmId AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL)) THROW 51000,N'Match algorithm was not found.',1;");

    public Task<Guid> SaveAlgorithmAsync(Guid tenantId, Guid actorUserId, SaveMatchAlgorithmSettingRequest request, CancellationToken cancellationToken = default)
        => SaveAsync(tenantId, actorUserId, request.MatchAlgorithmId, request.RowVersion, "Search.MatchAlgorithm", "MatchAlgorithmId", "AlgorithmCode", request.AlgorithmCode, "AlgorithmCode=@AlgorithmCode,DisplayName=@DisplayName,AlgorithmKindCode=@AlgorithmKindCode,Description=@Description,ConfigurationJson=@ConfigurationJson,IsActive=@IsActive", request, cancellationToken, "IF ISJSON(@ConfigurationJson)<>1 THROW 51000,N'Algorithm configuration must be valid JSON.',1;");

    public Task<Guid> SaveNormalizationTermAsync(Guid tenantId, Guid actorUserId, SaveNormalizationTermSettingRequest request, CancellationToken cancellationToken = default)
        => SaveAsync(tenantId, actorUserId, request.NormalizationTermId, request.RowVersion, "Search.NormalizationTerm", "NormalizationTermId", null, null, "EntityTypeCode=@EntityTypeCode,FieldCode=@FieldCode,SourceValue=@SourceValue,NormalizedValue=@NormalizedValue,TermKindCode=@TermKindCode,CultureCode=@CultureCode,SortOrder=@SortOrder,IsActive=@IsActive", request, cancellationToken);

    public Task DeleteProfileAsync(Guid tenantId, Guid actorUserId, Guid matchProfileId, byte[] rowVersion, CancellationToken cancellationToken = default) => DeleteAsync(tenantId, actorUserId, "Search.MatchProfile", "MatchProfileId", matchProfileId, rowVersion, "IF EXISTS(SELECT 1 FROM Search.MatchFieldRule WHERE MatchProfileId=@Id AND IsDeleted=0) THROW 51000,N'Remove profile field rules before deleting the profile.',1;", cancellationToken);
    public Task DeleteFieldRuleAsync(Guid tenantId, Guid actorUserId, Guid matchFieldRuleId, byte[] rowVersion, CancellationToken cancellationToken = default) => DeleteAsync(tenantId, actorUserId, "Search.MatchFieldRule", "MatchFieldRuleId", matchFieldRuleId, rowVersion, null, cancellationToken);
    public Task DeleteAlgorithmAsync(Guid tenantId, Guid actorUserId, Guid matchAlgorithmId, byte[] rowVersion, CancellationToken cancellationToken = default) => DeleteAsync(tenantId, actorUserId, "Search.MatchAlgorithm", "MatchAlgorithmId", matchAlgorithmId, rowVersion, "IF EXISTS(SELECT 1 FROM Search.MatchFieldRule WHERE MatchAlgorithmId=@Id AND IsDeleted=0) THROW 51000,N'The algorithm is referenced by an active field rule.',1;", cancellationToken);
    public Task DeleteNormalizationTermAsync(Guid tenantId, Guid actorUserId, Guid normalizationTermId, byte[] rowVersion, CancellationToken cancellationToken = default) => DeleteAsync(tenantId, actorUserId, "Search.NormalizationTerm", "NormalizationTermId", normalizationTermId, rowVersion, null, cancellationToken);

    private async Task<Guid> SaveAsync(Guid tenantId, Guid actorUserId, Guid? id, byte[]? rowVersion, string table, string idColumn, string? codeColumn, string? code, string assignments, object request, CancellationToken cancellationToken, string? prerequisite = null)
    {
        var itemId = id ?? Guid.NewGuid();
        var parameters = new DynamicParameters(request);
        parameters.Add("TenantId", tenantId); parameters.Add("ActorUserId", actorUserId); parameters.Add("Id", itemId); parameters.Add("RowVersion", rowVersion); parameters.Add("Code", code);
        var columns = assignments.Split(',').Select(value => value.Split('=')[0]).ToArray();
        var insertColumns = string.Join(',', columns);
        var insertValues = string.Join(',', columns.Select(column => $"@{column}"));
        var uniqueness = codeColumn is null ? string.Empty : $"IF EXISTS(SELECT 1 FROM {table} WHERE TenantId=@TenantId AND {codeColumn}=@Code AND {idColumn}<>@Id AND IsDeleted=0) THROW 51000,N'A tenant override with this code already exists.',1;";
        var sql = $"{prerequisite}{uniqueness} IF @RowVersion IS NULL BEGIN INSERT {table}({idColumn},TenantId,{insertColumns},CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@Id,@TenantId,{insertValues},SYSUTCDATETIME(),@ActorUserId,0); END ELSE BEGIN UPDATE {table} SET {assignments},ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE {idColumn}=@Id AND TenantId=@TenantId AND RowVersion=@RowVersion AND IsDeleted=0; IF @@ROWCOUNT=0 THROW 51000,N'The tenant setting changed or was not found.',1; END SELECT @Id;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    private async Task DeleteAsync(Guid tenantId, Guid actorUserId, string table, string idColumn, Guid id, byte[] rowVersion, string? prerequisite, CancellationToken cancellationToken)
    {
        var sql = $"{prerequisite} UPDATE {table} SET IsDeleted=1,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE {idColumn}=@Id AND TenantId=@TenantId AND RowVersion=@RowVersion AND IsDeleted=0; IF @@ROWCOUNT=0 THROW 51000,N'The tenant setting changed or was not found.',1;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, ActorUserId = actorUserId, Id = id, RowVersion = rowVersion }, cancellationToken: cancellationToken));
    }

    public async Task<MatchPolicy?> GetPolicyAsync(Guid tenantId, string profileCode, CancellationToken cancellationToken = default)
    {
        const string sql = """
;WITH profiles AS
(
    SELECT profile.*,ROW_NUMBER() OVER(PARTITION BY profile.ProfileCode ORDER BY CASE WHEN profile.TenantId=@TenantId THEN 0 ELSE 1 END) Choice
    FROM Search.MatchProfile profile
    WHERE profile.ProfileCode=@ProfileCode AND profile.IsActive=1 AND profile.IsDeleted=0 AND (profile.TenantId=@TenantId OR profile.TenantId IS NULL)
)
SELECT MatchProfileId,ProfileCode,EntityTypeCode,ExactThreshold,StrongThreshold,PossibleThreshold,MaximumCandidates,SemanticMaximumConcepts,RequiresReview FROM profiles WHERE Choice=1;

DECLARE @ProfileId UNIQUEIDENTIFIER=(SELECT TOP(1) MatchProfileId FROM Search.MatchProfile WHERE ProfileCode=@ProfileCode AND IsActive=1 AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END);
SELECT field.MatchFieldRuleId,field.FieldCode,field.DisplayName,COALESCE(tenantAlgorithm.AlgorithmCode,algorithm.AlgorithmCode) AlgorithmCode,field.Weight,field.MinimumSimilarity,field.IsRequired,field.IsCriticalIdentifier,field.ExactMatchOnly,field.IsSensitive
FROM Search.MatchFieldRule field JOIN Search.MatchAlgorithm algorithm ON algorithm.MatchAlgorithmId=field.MatchAlgorithmId AND algorithm.IsDeleted=0 LEFT JOIN Search.MatchAlgorithm tenantAlgorithm ON tenantAlgorithm.TenantId=@TenantId AND tenantAlgorithm.AlgorithmCode=algorithm.AlgorithmCode AND tenantAlgorithm.IsDeleted=0
WHERE field.MatchProfileId=@ProfileId AND field.IsActive=1 AND field.IsDeleted=0 AND (field.TenantId=@TenantId OR field.TenantId IS NULL)
  AND COALESCE(tenantAlgorithm.IsActive,algorithm.IsActive)=1
ORDER BY field.SortOrder;

DECLARE @EntityTypeCode NVARCHAR(80)=(SELECT EntityTypeCode FROM Search.MatchProfile WHERE MatchProfileId=@ProfileId);
;WITH terms AS
(
    SELECT term.*,ROW_NUMBER() OVER(PARTITION BY term.EntityTypeCode,term.FieldCode,term.SourceValue,ISNULL(term.CultureCode,N'') ORDER BY CASE WHEN term.TenantId=@TenantId THEN 0 ELSE 1 END) Choice
    FROM Search.NormalizationTerm term
    WHERE term.IsActive=1 AND term.IsDeleted=0 AND (term.TenantId=@TenantId OR term.TenantId IS NULL) AND (term.EntityTypeCode=@EntityTypeCode OR term.EntityTypeCode=N'Global')
)
SELECT EntityTypeCode,FieldCode,SourceValue,NormalizedValue,TermKindCode FROM terms WHERE Choice=1 ORDER BY SortOrder;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await connection.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, ProfileCode = profileCode.Trim() }, cancellationToken: cancellationToken));
        var profile = await multi.ReadSingleOrDefaultAsync<ProfileRow>();
        var fields = (await multi.ReadAsync<MatchFieldPolicy>()).AsList();
        var terms = (await multi.ReadAsync<NormalizationTermPolicy>()).AsList();
        return profile is null ? null : new(profile.MatchProfileId, profile.ProfileCode, profile.EntityTypeCode, profile.ExactThreshold, profile.StrongThreshold, profile.PossibleThreshold, profile.MaximumCandidates, profile.SemanticMaximumConcepts, profile.RequiresReview, fields, terms);
    }

    public async Task<MatchReviewDecision> SaveReviewDecisionAsync(MatchReviewDecisionRequest request, CancellationToken cancellationToken = default)
    {
        if (!MatchReviewDecisionCodes.All.Contains(request.DecisionCode)) throw new ArgumentException("Unsupported match review decision.", nameof(request));
        const string sql = """
IF NOT EXISTS(SELECT 1 FROM Search.MatchExecution WHERE TenantId=@TenantId AND MatchExecutionId=@MatchExecutionId AND StatusCode=N'COMPLETED' AND IsDeleted=0) THROW 51000,N'Completed match execution not found for the tenant.',1;
IF @CandidateEntityId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM Search.MatchCandidate candidate JOIN Search.MatchExecution execution ON execution.MatchExecutionId=candidate.MatchExecutionId WHERE execution.TenantId=@TenantId AND candidate.MatchExecutionId=@MatchExecutionId AND candidate.CandidateEntityId=@CandidateEntityId AND candidate.IsDeleted=0) THROW 51000,N'Match candidate not found for the execution.',1;
DECLARE @Id UNIQUEIDENTIFIER=NEWID();
INSERT Search.MatchReviewDecision(MatchReviewDecisionId,TenantId,MatchExecutionId,CandidateEntityId,DecisionCode,Notes,RequestedByUserId,CorrelationId,CreatedDateUtc,IsDeleted) VALUES(@Id,@TenantId,@MatchExecutionId,@CandidateEntityId,@DecisionCode,@Notes,@RequestedByUserId,@CorrelationId,SYSUTCDATETIME(),0);
SELECT MatchReviewDecisionId,MatchExecutionId,CandidateEntityId,DecisionCode,Notes,RequestedByUserId,CorrelationId,CreatedDateUtc FROM Search.MatchReviewDecision WHERE MatchReviewDecisionId=@Id;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<MatchReviewDecision>(new CommandDefinition(sql, request, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MatchReviewDecision>> GetReviewDecisionsAsync(Guid tenantId, Guid matchExecutionId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT MatchReviewDecisionId,MatchExecutionId,CandidateEntityId,DecisionCode,Notes,RequestedByUserId,CorrelationId,CreatedDateUtc FROM Search.MatchReviewDecision WHERE TenantId=@TenantId AND MatchExecutionId=@MatchExecutionId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<MatchReviewDecision>(new CommandDefinition(sql, new { TenantId = tenantId, MatchExecutionId = matchExecutionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task SaveSemanticEvidenceAsync(Guid tenantId, Guid? requestedByUserId, string correlationId, string query, IReadOnlyCollection<string> terms, IReadOnlyCollection<SemanticConceptMatchDto> concepts, CancellationToken cancellationToken = default)
    {
        const string sql = """
INSERT Search.SemanticQueryEvidence(SemanticQueryEvidenceId,TenantId,RequestedByUserId,CorrelationId,QueryText,ExpandedTermsJson,ConceptsJson,ProviderCode,CreatedDateUtc,IsDeleted)
VALUES(NEWID(),@TenantId,@RequestedByUserId,@CorrelationId,@QueryText,@ExpandedTermsJson,@ConceptsJson,N'KnowledgePlatform',SYSUTCDATETIME(),0);
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, RequestedByUserId = requestedByUserId, CorrelationId = correlationId, QueryText = query.Trim(), ExpandedTermsJson = JsonSerializer.Serialize(terms, JsonOptions), ConceptsJson = JsonSerializer.Serialize(concepts, JsonOptions) }, cancellationToken: cancellationToken));
    }

    public async Task<int> RefreshProjectionsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureFullTextSearchAsync(cancellationToken);
        const string sql = """
DECLARE @Changed INT=0;
DECLARE @Source TABLE(TenantId UNIQUEIDENTIFIER,EntityTypeCode NVARCHAR(80),EntityId UNIQUEIDENTIFIER,DisplayName NVARCHAR(500),SecondaryText NVARCHAR(1000),NavigationRoute NVARCHAR(500),SourceSchemaName NVARCHAR(128),SourceTableName NVARCHAR(128),SourceModifiedDateUtc DATETIME2,SearchText NVARCHAR(MAX),NormalizedFieldsJson NVARCHAR(MAX),ExactIdentifiersJson NVARCHAR(MAX),PermissionCode NVARCHAR(150));
INSERT @Source
SELECT account.TenantId,N'Account',account.AccountId,account.AccountName,CONCAT(account.AccountNumber,N' · ',COALESCE(account.MainEmail,N'')),CONCAT(N'/accounts/',account.AccountId),N'Client',N'Account',COALESCE(account.ModifiedDateUtc,account.CreatedDateUtc,contactEvidence.LastContactModifiedDateUtc),CONCAT_WS(N' ',account.AccountNumber,account.AccountName,account.DbaName,account.MainEmail,account.MainPhone,account.Industry,account.Website,contactEvidence.ContactSearchText),
       (SELECT account.AccountName DisplayName,CONCAT_WS(N' ',account.AccountNumber,account.AccountName,account.DbaName,account.MainEmail,account.MainPhone,account.Industry,account.Website,contactEvidence.ContactSearchText) SearchText,account.AccountName BusinessName,account.MainEmail Email,account.MainPhone Phone,account.DbaName,account.Industry,contactEvidence.ContactSearchText Contacts FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT account.AccountNumber FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
FROM Client.Account account OUTER APPLY(SELECT STRING_AGG(CONCAT_WS(N' ',contact.FirstName,contact.LastName,contact.Email,contact.Phone,contact.JobTitle),N' ') WITHIN GROUP(ORDER BY contact.LastName,contact.FirstName) ContactSearchText,MAX(COALESCE(contact.ModifiedDateUtc,contact.CreatedDateUtc)) LastContactModifiedDateUtc FROM Client.Contact contact WHERE contact.TenantId=account.TenantId AND contact.AccountId=account.AccountId AND contact.IsDeleted=0 AND (contact.StatusCode IS NULL OR contact.StatusCode=N'Active')) contactEvidence WHERE account.IsDeleted=0
UNION ALL
SELECT contact.TenantId,N'Contact',contact.ContactId,CONCAT(contact.FirstName,N' ',contact.LastName),CONCAT(account.AccountName,N' · ',COALESCE(contact.Email,N'')),CONCAT(N'/client/contacts/',contact.ContactId),N'Client',N'Contact',COALESCE(contact.ModifiedDateUtc,contact.CreatedDateUtc),CONCAT_WS(N' ',contact.FirstName,contact.LastName,contact.Email,contact.Phone,contact.JobTitle,account.AccountName,contact.StatusCode),
       (SELECT CONCAT(contact.FirstName,N' ',contact.LastName) DisplayName,CONCAT_WS(N' ',contact.FirstName,contact.LastName,contact.Email,contact.Phone,contact.JobTitle,account.AccountName,contact.StatusCode) SearchText,CONCAT(contact.FirstName,N' ',contact.LastName) FullName,contact.FirstName,contact.LastName,contact.Email,contact.Phone,account.AccountName,contact.StatusCode FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT contact.Email,contact.Phone FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
FROM Client.Contact contact JOIN Client.Account account ON account.TenantId=contact.TenantId AND account.AccountId=contact.AccountId AND account.IsDeleted=0 WHERE contact.IsDeleted=0 AND (contact.StatusCode IS NULL OR contact.StatusCode=N'Active')
UNION ALL
SELECT TenantId,N'Lead',LeadId,COALESCE(NULLIF(AccountName,N''),NULLIF(CONCAT(FirstName,N' ',LastName),N' '),LeadNumber),CONCAT(LeadNumber,N' · ',COALESCE(Email,N'')),CONCAT(N'/leads/',LeadId),N'CRM',N'Lead',COALESCE(ModifiedDateUtc,CreatedDateUtc),CONCAT_WS(N' ',LeadNumber,AccountName,FirstName,LastName,Email,Phone,InterestedService),
       (SELECT COALESCE(NULLIF(AccountName,N''),NULLIF(CONCAT(FirstName,N' ',LastName),N' '),LeadNumber) DisplayName,CONCAT_WS(N' ',LeadNumber,AccountName,FirstName,LastName,Email,Phone,InterestedService) SearchText,AccountName CompanyName,CONCAT(FirstName,N' ',LastName) FullName,FirstName,LastName,Email,Phone FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT LeadNumber,Email,Phone FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
FROM CRM.Lead WHERE IsDeleted=0
UNION ALL
SELECT submission.TenantId,N'Submission',submission.SubmissionId,submission.SubmissionNumber,CONCAT(account.AccountName,N' · ',submission.LineOfBusiness,N' · ',submission.Status),CONCAT(N'/submissions/',submission.SubmissionId),N'Submissions',N'Submission',COALESCE(submission.ModifiedDateUtc,submission.CreatedDateUtc),CONCAT_WS(N' ',submission.SubmissionNumber,account.AccountName,submission.LineOfBusiness,submission.Status,submission.Priority),
       (SELECT submission.SubmissionNumber DisplayName,CONCAT_WS(N' ',submission.SubmissionNumber,account.AccountName,submission.LineOfBusiness,submission.Status,submission.Priority) SearchText,submission.SubmissionNumber,CONVERT(NVARCHAR(36),submission.AccountId) AccountId,submission.LineOfBusiness,CONVERT(NVARCHAR(30),submission.EffectiveDate,126) EffectiveDate FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT submission.SubmissionNumber FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
FROM Submissions.Submission submission JOIN Client.Account account ON account.TenantId=submission.TenantId AND account.AccountId=submission.AccountId AND account.IsDeleted=0 WHERE submission.IsDeleted=0
UNION ALL
SELECT policy.TenantId,N'Policy',policy.PolicyId,policy.PolicyNumber,CONCAT(account.AccountName,N' · ',carrier.CarrierName,N' · ',policy.Status),CONCAT(N'/policies/',policy.PolicyId),N'Submissions',N'BoundPolicy',policy.BoundDateUtc,CONCAT_WS(N' ',policy.PolicyNumber,account.AccountName,carrier.CarrierName,policy.LineOfBusiness,policy.Status),
       (SELECT policy.PolicyNumber DisplayName,CONCAT_WS(N' ',policy.PolicyNumber,account.AccountName,carrier.CarrierName,policy.LineOfBusiness,policy.Status) SearchText,policy.PolicyNumber,CONVERT(NVARCHAR(36),policy.CarrierId) CarrierId,account.AccountName NamedInsured,CONVERT(NVARCHAR(30),policy.EffectiveDate,126) EffectiveDate FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT policy.PolicyNumber FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
FROM Submissions.BoundPolicy policy JOIN Client.Account account ON account.TenantId=policy.TenantId AND account.AccountId=policy.AccountId AND account.IsDeleted=0 LEFT JOIN Agency.Carrier carrier ON carrier.TenantId=policy.TenantId AND carrier.CarrierId=policy.CarrierId AND carrier.IsDeleted=0 WHERE policy.IsDeleted=0
UNION ALL
SELECT claim.TenantId,N'Claim',claim.ClaimId,claim.ClaimNumber,CONCAT(claim.AccountName,N' · ',claim.PolicyNumber,N' · ',claim.Status),CONCAT(N'/claims/',claim.ClaimId),N'Claims',N'Claim',COALESCE(claim.ModifiedDateUtc,claim.CreatedDateUtc),CONCAT_WS(N' ',claim.ClaimNumber,claim.CarrierClaimNumber,claim.PolicyNumber,claim.AccountName,claim.PrimaryClaimant,claim.Carrier,claim.Lob,claim.LossType,claim.LossLocation,claim.Status),
       (SELECT claim.ClaimNumber DisplayName,CONCAT_WS(N' ',claim.ClaimNumber,claim.CarrierClaimNumber,claim.PolicyNumber,claim.AccountName,claim.PrimaryClaimant,claim.Carrier,claim.Lob,claim.LossType,claim.LossLocation,claim.Status) SearchText,claim.ClaimNumber,claim.PolicyNumber,claim.PrimaryClaimant ClaimantName,CONVERT(NVARCHAR(30),claim.DateOfLoss,126) LossDate FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT claim.ClaimNumber,claim.PolicyNumber,claim.CarrierClaimNumber FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
FROM Claims.Claim claim WHERE claim.IsDeleted=0
UNION ALL
SELECT document.TenantId,N'Document',document.DocumentId,document.FileName,CONCAT(document.DocumentTypeCode,N' · ',document.EntityName,N' · ',document.StatusCode),CONCAT(N'/documents/',document.DocumentId),N'DMS',N'Document',COALESCE(document.ModifiedDateUtc,document.CreatedDateUtc),CONCAT_WS(N' ',document.FileName,document.DocumentTypeCode,document.CategoryCode,document.EntityName,document.Description,document.Tags),
       (SELECT document.FileName DisplayName,CONCAT_WS(N' ',document.FileName,document.DocumentTypeCode,document.CategoryCode,document.EntityName,document.Description,document.Tags) SearchText,document.Description DocumentText FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT document.FileName FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
FROM DMS.Document document WHERE document.IsDeleted=0
UNION ALL
SELECT holder.TenantId,N'Certificate',holder.CertificateHolderId,holder.LegalName,CONCAT_WS(N' · ',holder.HolderCode,holder.City,holder.StateProvince),CONCAT(N'/policies/certificates?holderId=',holder.CertificateHolderId),N'Policy',N'CertificateHolder',COALESCE(holder.ModifiedDateUtc,holder.CreatedDateUtc),CONCAT_WS(N' ',holder.HolderCode,holder.LegalName,holder.AddressLine1,holder.AddressLine2,holder.City,holder.StateProvince,holder.PostalCode,holder.ContactName,holder.EmailAddress,holder.PhoneNumber),
       (SELECT holder.LegalName DisplayName,CONCAT_WS(N' ',holder.HolderCode,holder.LegalName,holder.AddressLine1,holder.AddressLine2,holder.City,holder.StateProvince,holder.PostalCode,holder.ContactName,holder.EmailAddress,holder.PhoneNumber) SearchText,holder.LegalName PartyName,CONCAT_WS(N' ',holder.AddressLine1,holder.AddressLine2,holder.City,holder.StateProvince,holder.PostalCode) Address,holder.EmailAddress Email,holder.PhoneNumber Phone FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT holder.HolderCode FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
FROM Policy.CertificateHolder holder WHERE holder.IsDeleted=0 AND holder.IsActive=1
UNION ALL
SELECT carrier.TenantId,N'Carrier',carrier.CarrierId,carrier.CarrierName,CONCAT_WS(N' · ',carrier.NaicCode,carrier.AmBestRating),N'/admin/reference/carriers',N'Agency',N'Carrier',COALESCE(carrier.ModifiedDateUtc,carrier.CreatedDateUtc),CONCAT_WS(N' ',carrier.CarrierName,carrier.NaicCode,carrier.AmBestRating),
       (SELECT carrier.CarrierName DisplayName,CONCAT_WS(N' ',carrier.CarrierName,carrier.NaicCode,carrier.AmBestRating) SearchText,carrier.CarrierName,carrier.NaicCode CarrierCode FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT carrier.NaicCode CarrierCode FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
FROM Agency.Carrier carrier WHERE carrier.IsDeleted=0 AND carrier.IsActive=1
UNION ALL
SELECT location.TenantId,N'Location',location.AccountLocationId,COALESCE(NULLIF(location.LocationName,N''),location.LocationNumber),CONCAT_WS(N' · ',account.AccountName,location.City,location.StateCode,location.PostalCode),CONCAT(N'/accounts/',location.AccountId),N'Client',N'AccountLocation',COALESCE(location.ModifiedDateUtc,location.CreatedDateUtc),CONCAT_WS(N' ',account.AccountName,location.LocationNumber,location.LocationName,location.AddressLine1,location.AddressLine2,location.City,location.StateCode,location.PostalCode),
       (SELECT COALESCE(NULLIF(location.LocationName,N''),location.LocationNumber) DisplayName,CONCAT_WS(N' ',account.AccountName,location.LocationNumber,location.LocationName,location.AddressLine1,location.AddressLine2,location.City,location.StateCode,location.PostalCode) SearchText,location.LocationName,CONCAT_WS(N' ',location.AddressLine1,location.AddressLine2,location.City,location.StateCode,location.PostalCode) Address,location.PostalCode FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT location.LocationNumber FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
FROM Client.AccountLocation location JOIN Client.Account account ON account.TenantId=location.TenantId AND account.AccountId=location.AccountId AND account.IsDeleted=0 WHERE location.IsDeleted=0
UNION ALL
SELECT vehicle.TenantId,N'Vehicle',vehicle.AccountVehicleId,CONCAT_WS(N' ',vehicle.ModelYear,vehicle.Make,vehicle.Model),CONCAT(account.AccountName,N' · ',vehicle.VehicleNumber),CONCAT(N'/accounts/',vehicle.AccountId),N'Client',N'AccountVehicle',COALESCE(vehicle.ModifiedDateUtc,vehicle.CreatedDateUtc),CONCAT_WS(N' ',account.AccountName,vehicle.VehicleNumber,vehicle.Vin,vehicle.ModelYear,vehicle.Make,vehicle.Model,vehicle.VehicleTypeCode),
       (SELECT CONCAT_WS(N' ',vehicle.ModelYear,vehicle.Make,vehicle.Model) DisplayName,CONCAT_WS(N' ',account.AccountName,vehicle.VehicleNumber,vehicle.Vin,vehicle.ModelYear,vehicle.Make,vehicle.Model,vehicle.VehicleTypeCode) SearchText,vehicle.Vin,CONCAT_WS(N' ',vehicle.Make,vehicle.Model) MakeModel,CONVERT(NVARCHAR(10),vehicle.ModelYear) ModelYear FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT vehicle.Vin FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
FROM Client.AccountVehicle vehicle JOIN Client.Account account ON account.TenantId=vehicle.TenantId AND account.AccountId=vehicle.AccountId AND account.IsDeleted=0 WHERE vehicle.IsDeleted=0 AND vehicle.IsActive=1
UNION ALL
SELECT party.TenantId,N'ClaimParty',party.ClaimPartyId,COALESCE(NULLIF(party.DisplayName,N''),party.OrganizationName),CONCAT(claim.ClaimNumber,N' · ',party.PartyTypeCode),CONCAT(N'/claims/',party.ClaimId),N'Claims',N'ClaimParty',COALESCE(party.ModifiedDateUtc,party.CreatedDateUtc),CONCAT_WS(N' ',claim.ClaimNumber,party.DisplayName,party.OrganizationName,party.EmailAddress,party.PhoneNumber,party.PartyTypeCode),
       (SELECT COALESCE(NULLIF(party.DisplayName,N''),party.OrganizationName) DisplayName,CONCAT_WS(N' ',claim.ClaimNumber,party.DisplayName,party.OrganizationName,party.EmailAddress,party.PhoneNumber,party.PartyTypeCode) SearchText,COALESCE(NULLIF(party.DisplayName,N''),party.OrganizationName) FullName,party.EmailAddress Email,party.PhoneNumber Phone FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT party.EmailAddress,party.PhoneNumber FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
FROM Claims.ClaimParty party JOIN Claims.Claim claim ON claim.TenantId=party.TenantId AND claim.ClaimId=party.ClaimId AND claim.IsDeleted=0 WHERE party.IsDeleted=0 AND party.IsActive=1
UNION ALL
SELECT receivable.TenantId,N'CommissionLine',receivable.CommissionExpectedReceivableId,receivable.PolicyNumber,CONCAT(receivable.AccountName,N' · ',receivable.CarrierName,N' · ',receivable.ExpectedCommissionAmount),N'/commissions/reconciliation',N'Commission',N'CommissionExpectedReceivable',COALESCE(receivable.ModifiedDateUtc,receivable.CreatedDateUtc),CONCAT_WS(N' ',receivable.PolicyNumber,receivable.AccountName,receivable.CarrierName,receivable.LineOfBusinessCode,receivable.BusinessTypeCode),
       (SELECT receivable.PolicyNumber DisplayName,CONCAT_WS(N' ',receivable.PolicyNumber,receivable.AccountName,receivable.CarrierName,receivable.LineOfBusinessCode,receivable.BusinessTypeCode) SearchText,receivable.PolicyNumber,CONVERT(NVARCHAR(36),receivable.CarrierId) CarrierId,receivable.AccountName InsuredName,CONVERT(NVARCHAR(50),receivable.PremiumAmount) PremiumAmount FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT receivable.PolicyNumber FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N'Intelligence.Search'
FROM Commission.CommissionExpectedReceivable receivable WHERE receivable.IsDeleted=0;

IF OBJECT_ID(N'Agency.Staff',N'U') IS NOT NULL
BEGIN
    INSERT @Source
    EXEC sys.sp_executesql N'SELECT staff.TenantId,N''Producer'',staff.StaffId,CONCAT(staff.FirstName,N'' '',staff.LastName),CONCAT_WS(N'' · '',staff.Role,staff.Department,staff.Team,staff.Email),CONCAT(N''/tenant/agency/producers?staffId='',staff.StaffId),N''Agency'',N''Staff'',COALESCE(staff.ModifiedDateUtc,staff.CreatedDateUtc),CONCAT_WS(N'' '',staff.FirstName,staff.LastName,staff.Email,staff.Phone,staff.Title,staff.Role,staff.Department,staff.Team,staff.LicenseNumber,staff.LicenseStates,staff.EmploymentStatus),(SELECT CONCAT(staff.FirstName,N'' '',staff.LastName) DisplayName,CONCAT_WS(N'' '',staff.FirstName,staff.LastName,staff.Email,staff.Phone,staff.Title,staff.Role,staff.Department,staff.Team,staff.LicenseNumber,staff.LicenseStates,staff.EmploymentStatus) SearchText,CONCAT(staff.FirstName,N'' '',staff.LastName) FullName,staff.Email,staff.Phone,staff.Role,staff.Department,staff.Team,staff.LicenseNumber NpnLicense,staff.LicenseStates FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),(SELECT staff.Email,staff.Phone,staff.LicenseNumber FOR JSON PATH,WITHOUT_ARRAY_WRAPPER),N''Intelligence.Search'' FROM Agency.Staff staff WHERE staff.IsDeleted=0 AND staff.IsActive=1 AND staff.Role=N''Producer''';
END;

MERGE Search.EntityProjection target USING @Source source ON target.TenantId=source.TenantId AND target.EntityTypeCode=source.EntityTypeCode AND target.EntityId=source.EntityId AND target.IsDeleted=0
WHEN MATCHED AND (target.SourceModifiedDateUtc<source.SourceModifiedDateUtc OR target.SourceModifiedDateUtc IS NULL OR target.IsActive=0 OR ISNULL(target.DisplayName,N'')<>ISNULL(source.DisplayName,N'') OR ISNULL(target.SecondaryText,N'')<>ISNULL(source.SecondaryText,N'') OR ISNULL(target.NavigationRoute,N'')<>ISNULL(source.NavigationRoute,N'') OR ISNULL(target.SearchText,N'')<>ISNULL(source.SearchText,N'') OR ISNULL(target.NormalizedFieldsJson,N'')<>ISNULL(source.NormalizedFieldsJson,N'') OR ISNULL(target.ExactIdentifiersJson,N'')<>ISNULL(source.ExactIdentifiersJson,N'') OR ISNULL(target.PermissionCode,N'')<>ISNULL(source.PermissionCode,N'')) THEN UPDATE SET DisplayName=source.DisplayName,SecondaryText=source.SecondaryText,NavigationRoute=source.NavigationRoute,SourceModifiedDateUtc=source.SourceModifiedDateUtc,SearchText=source.SearchText,NormalizedFieldsJson=source.NormalizedFieldsJson,ExactIdentifiersJson=source.ExactIdentifiersJson,PermissionCode=source.PermissionCode,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(EntityProjectionId,TenantId,EntityTypeCode,EntityId,DisplayName,SecondaryText,NavigationRoute,SourceSchemaName,SourceTableName,SourceModifiedDateUtc,SearchText,NormalizedFieldsJson,ExactIdentifiersJson,PermissionCode,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),source.TenantId,source.EntityTypeCode,source.EntityId,source.DisplayName,source.SecondaryText,source.NavigationRoute,source.SourceSchemaName,source.SourceTableName,source.SourceModifiedDateUtc,source.SearchText,source.NormalizedFieldsJson,source.ExactIdentifiersJson,source.PermissionCode,1,SYSUTCDATETIME(),0);
SET @Changed+=@@ROWCOUNT;
UPDATE projection SET IsActive=0,ModifiedDateUtc=SYSUTCDATETIME() FROM Search.EntityProjection projection WHERE projection.EntityTypeCode IN(N'Account',N'Contact',N'Lead',N'Submission',N'Policy',N'Claim',N'Document',N'Certificate',N'Carrier',N'Producer',N'Location',N'Vehicle',N'ClaimParty',N'CommissionLine') AND projection.IsActive=1 AND projection.IsDeleted=0 AND NOT EXISTS(SELECT 1 FROM @Source source WHERE source.TenantId=projection.TenantId AND source.EntityTypeCode=projection.EntityTypeCode AND source.EntityId=projection.EntityId);
SET @Changed+=@@ROWCOUNT;
;WITH SearchSource AS
(
    SELECT projection.TenantId,projection.EntityTypeCode,projection.EntityId,projection.SourceSchemaName ModuleCode,projection.DisplayName Title,COALESCE(projection.SearchText,N'') ContentText,CONCAT_WS(N' ',projection.DisplayName,projection.SecondaryText,projection.ExactIdentifiersJson) Keywords,projection.SourceModifiedDateUtc,CONVERT(char(64),HASHBYTES('SHA2_256',CONVERT(varbinary(max),CONCAT_WS(N'|',projection.DisplayName,projection.SecondaryText,projection.SearchText,projection.NormalizedFieldsJson,projection.ExactIdentifiersJson))),2) ContentHash
    FROM Search.EntityProjection projection
    WHERE projection.EntityTypeCode IN(N'Account',N'Contact',N'Lead',N'Submission',N'Policy',N'Claim',N'Document',N'Certificate',N'Carrier',N'Producer',N'Location',N'Vehicle',N'ClaimParty',N'CommissionLine') AND projection.IsActive=1 AND projection.IsDeleted=0
)
MERGE AI.SearchDocument target USING SearchSource source ON target.TenantId=source.TenantId AND target.EntityTypeCode=source.EntityTypeCode AND target.EntityId=source.EntityId AND target.IsDeleted=0
WHEN MATCHED AND (target.ContentHash<>source.ContentHash OR target.SourceModifiedDateUtc<>source.SourceModifiedDateUtc OR target.SourceModifiedDateUtc IS NULL) THEN UPDATE SET ModuleCode=source.ModuleCode,Title=source.Title,ContentText=source.ContentText,Keywords=source.Keywords,SecurityScopeJson=N'{"permissionCode":"Intelligence.Search"}',ContentHash=source.ContentHash,SourceModifiedDateUtc=source.SourceModifiedDateUtc,SourceCreatedDateUtc=COALESCE(target.SourceCreatedDateUtc,source.SourceModifiedDateUtc),IndexedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME(),IsDeleted=0
WHEN NOT MATCHED THEN INSERT(SearchDocumentId,TenantId,EntityTypeCode,EntityId,ModuleCode,Title,ContentText,Keywords,ConceptIdsJson,SecurityScopeJson,ContentHash,IndexedDateUtc,SourceModifiedDateUtc,SourceCreatedDateUtc,CreatedDateUtc,IsDeleted) VALUES(NEWID(),source.TenantId,source.EntityTypeCode,source.EntityId,source.ModuleCode,source.Title,source.ContentText,source.Keywords,N'[]',N'{"permissionCode":"Intelligence.Search"}',source.ContentHash,SYSUTCDATETIME(),source.SourceModifiedDateUtc,source.SourceModifiedDateUtc,SYSUTCDATETIME(),0);
SET @Changed+=@@ROWCOUNT;
MERGE AI.SearchPermission target USING
(
    SELECT DISTINCT document.TenantId,document.SearchDocumentId,N'ROLE' PrincipalTypeCode,rolePermission.RoleId PrincipalId,N'READ' PermissionCode
    FROM AI.SearchDocument document
    JOIN Search.EntityProjection projection ON projection.TenantId=document.TenantId AND projection.EntityTypeCode=document.EntityTypeCode AND projection.EntityId=document.EntityId AND projection.IsActive=1 AND projection.IsDeleted=0
    JOIN IAM.RolePermission rolePermission ON rolePermission.TenantId=document.TenantId AND rolePermission.PermissionCode=projection.PermissionCode AND rolePermission.IsDeleted=0
    JOIN IAM.Role role ON role.TenantId=rolePermission.TenantId AND role.RoleId=rolePermission.RoleId AND role.IsDeleted=0
    WHERE document.IsDeleted=0 AND projection.EntityTypeCode IN(N'Account',N'Contact',N'Lead',N'Submission',N'Policy',N'Claim',N'Document',N'Certificate',N'Carrier',N'Producer',N'Location',N'Vehicle',N'ClaimParty',N'CommissionLine')
) source ON target.TenantId=source.TenantId AND target.SearchDocumentId=source.SearchDocumentId AND target.PrincipalTypeCode=source.PrincipalTypeCode AND target.PrincipalId=source.PrincipalId AND target.PermissionCode=source.PermissionCode
WHEN MATCHED AND target.IsDeleted=1 THEN UPDATE SET IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(SearchPermissionId,TenantId,SearchDocumentId,PrincipalTypeCode,PrincipalId,PermissionCode,CreatedDateUtc,IsDeleted) VALUES(NEWID(),source.TenantId,source.SearchDocumentId,source.PrincipalTypeCode,source.PrincipalId,source.PermissionCode,SYSUTCDATETIME(),0);
SET @Changed+=@@ROWCOUNT;
MERGE Search.ProjectionCheckpoint target USING(SELECT TenantId,EntityTypeCode,MAX(SourceModifiedDateUtc) LastSourceModifiedDateUtc FROM @Source GROUP BY TenantId,EntityTypeCode) source ON target.TenantId=source.TenantId AND target.EntityTypeCode=source.EntityTypeCode AND target.IsDeleted=0
WHEN MATCHED THEN UPDATE SET LastSourceModifiedDateUtc=source.LastSourceModifiedDateUtc,LastSuccessfulDateUtc=SYSUTCDATETIME(),ErrorMessage=NULL,RetryCount=0,ModifiedDateUtc=SYSUTCDATETIME()
WHEN NOT MATCHED THEN INSERT(ProjectionCheckpointId,TenantId,EntityTypeCode,LastSourceModifiedDateUtc,LastSuccessfulDateUtc,CreatedDateUtc,IsDeleted) VALUES(NEWID(),source.TenantId,source.EntityTypeCode,source.LastSourceModifiedDateUtc,SYSUTCDATETIME(),SYSUTCDATETIME(),0);
SELECT @Changed;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        for(var attempt=1;;attempt++)
        {
            try
            {
                var lockResult=await connection.ExecuteScalarAsync<int>(new CommandDefinition("DECLARE @Result int;EXEC @Result=sys.sp_getapplock @Resource=N'Ams.SearchProjection.Refresh',@LockMode=N'Exclusive',@LockOwner=N'Session',@LockTimeout=10000;SELECT @Result;",commandTimeout:15,cancellationToken:cancellationToken));
                if(lockResult<0)return 0;
                try
                {
                    return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql,commandTimeout:180,cancellationToken:cancellationToken));
                }
                finally
                {
                    await connection.ExecuteAsync(new CommandDefinition("EXEC sys.sp_releaseapplock @Resource=N'Ams.SearchProjection.Refresh',@LockOwner=N'Session';",commandTimeout:15,cancellationToken:cancellationToken));
                }
            }
            catch(SqlException exception) when(IsDeadlockVictim(exception)&&attempt<3)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250*attempt),cancellationToken);
            }
        }
    }

    private static bool IsDeadlockVictim(SqlException exception)=>exception.Errors.Cast<SqlError>().Any(error=>error.Number==1205);

    private async Task EnsureFullTextSearchAsync(CancellationToken cancellationToken)
    {
        const string sql = """
IF FULLTEXTSERVICEPROPERTY(N'IsFullTextInstalled')=1
BEGIN
    IF NOT EXISTS(SELECT 1 FROM sys.fulltext_catalogs WHERE name=N'AmsSearchCatalog') CREATE FULLTEXT CATALOG AmsSearchCatalog AS DEFAULT;
    IF NOT EXISTS(SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'Search.EntityProjection'))
        CREATE FULLTEXT INDEX ON Search.EntityProjection(DisplayName LANGUAGE 1033,SecondaryText LANGUAGE 1033,SearchText LANGUAGE 1033) KEY INDEX PK_Search_EntityProjection ON AmsSearchCatalog WITH CHANGE_TRACKING AUTO;
    UPDATE Search.SearchCapability SET IsAvailable=1,LastVerifiedDateUtc=SYSUTCDATETIME(),LastError=NULL,ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND CapabilityCode=N'FULL_TEXT' AND IsDeleted=0;
END
ELSE
    UPDATE Search.SearchCapability SET IsAvailable=0,LastVerifiedDateUtc=SYSUTCDATETIME(),LastError=N'SQL Server Full-Text Search is not installed; bounded database fallback is active.',ModifiedDateUtc=SYSUTCDATETIME() WHERE TenantId IS NULL AND CapabilityCode=N'FULL_TEXT' AND IsDeleted=0;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<MatchProjection>> GetCandidatesAsync(Guid tenantId, string entityTypeCode, IReadOnlyDictionary<string, string?> fields, int maximumCandidates, CancellationToken cancellationToken = default)
    {
        var values = fields.Values.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();
        if (values.Length == 0) return [];
        const string sql = """
CREATE TABLE #Ranked(EntityProjectionId UNIQUEIDENTIFIER PRIMARY KEY,SearchRank INT NOT NULL);
IF EXISTS(SELECT 1 FROM Search.SearchCapability WHERE CapabilityCode=N'FULL_TEXT' AND IsAvailable=1 AND IsEnabled=1 AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL))
   AND EXISTS(SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'Search.EntityProjection'))
BEGIN
    DECLARE @FullTextSql NVARCHAR(MAX)=N'INSERT #Ranked(EntityProjectionId,SearchRank) SELECT projection.EntityProjectionId,fullText.[RANK] FROM Search.EntityProjection projection JOIN CONTAINSTABLE(Search.EntityProjection,(DisplayName,SecondaryText,SearchText),@FullTextQuery,@MaximumCandidates) fullText ON fullText.[KEY]=projection.EntityProjectionId WHERE projection.TenantId=@TenantId AND projection.EntityTypeCode=@EntityTypeCode AND projection.IsActive=1 AND projection.IsDeleted=0;';
    EXEC sp_executesql @FullTextSql,N'@TenantId UNIQUEIDENTIFIER,@EntityTypeCode NVARCHAR(80),@FullTextQuery NVARCHAR(4000),@MaximumCandidates INT',@TenantId,@EntityTypeCode,@FullTextQuery,@MaximumCandidates;
END;
INSERT @Ranked(EntityProjectionId,SearchRank)
SELECT TOP(@MaximumCandidates) projection.EntityProjectionId,CASE WHEN EXISTS(SELECT 1 FROM OPENJSON(@ValuesJson) WITH(Value NVARCHAR(500) '$') exactValue WHERE projection.DisplayName=exactValue.Value) THEN 1000 ELSE 100 END
FROM Search.EntityProjection projection
WHERE projection.TenantId=@TenantId AND projection.EntityTypeCode=@EntityTypeCode AND projection.IsActive=1 AND projection.IsDeleted=0
  AND EXISTS(SELECT 1 FROM OPENJSON(@ValuesJson) WITH(Value NVARCHAR(500) '$') valuesToMatch WHERE projection.DisplayName LIKE N'%' + valuesToMatch.Value + N'%' OR projection.SearchText LIKE N'%' + valuesToMatch.Value + N'%' OR projection.NormalizedFieldsJson LIKE N'%' + STRING_ESCAPE(valuesToMatch.Value,'json') + N'%')
  AND NOT EXISTS(SELECT 1 FROM #Ranked ranked WHERE ranked.EntityProjectionId=projection.EntityProjectionId);
SELECT TOP(@MaximumCandidates) projection.EntityProjectionId,projection.EntityId,projection.EntityTypeCode,projection.DisplayName,projection.SecondaryText,projection.NavigationRoute,projection.PermissionCode,projection.SearchText,projection.NormalizedFieldsJson
FROM #Ranked ranked JOIN Search.EntityProjection projection ON projection.EntityProjectionId=ranked.EntityProjectionId ORDER BY ranked.SearchRank DESC,projection.DisplayName;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ProjectionRow>(new CommandDefinition(sql, new { TenantId = tenantId, EntityTypeCode = entityTypeCode, MaximumCandidates = Math.Clamp(maximumCandidates, 1, 500), ValuesJson = JsonSerializer.Serialize(values, JsonOptions), FullTextQuery = BuildFullTextQuery(values) }, cancellationToken: cancellationToken));
        return rows.Select(ToProjection).ToList();
    }

    public async Task<IReadOnlyList<MatchProjection>> SearchProjectionsAsync(Guid tenantId, string query, string originalQuery, IReadOnlyCollection<string> entityTypeCodes, IReadOnlyCollection<string> grantedPermissions, int maximumResults, CancellationToken cancellationToken = default)
    {
        const string sql = """
CREATE TABLE #Ranked(EntityProjectionId UNIQUEIDENTIFIER PRIMARY KEY,SearchRank INT NOT NULL);
INSERT #Ranked(EntityProjectionId,SearchRank)
SELECT TOP(@MaximumResults) projection.EntityProjectionId,
    CASE WHEN projection.DisplayName=@OriginalQuery THEN 1400 WHEN projection.DisplayName=@Query THEN 1200 WHEN projection.DisplayName LIKE @OriginalQuery+N'%' THEN 1000 WHEN projection.DisplayName LIKE @Query+N'%' THEN 900 ELSE 100 END
FROM Search.EntityProjection projection
WHERE projection.TenantId=@TenantId AND projection.IsActive=1 AND projection.IsDeleted=0
  AND (@AllTypes=1 OR projection.EntityTypeCode IN(SELECT [value] FROM OPENJSON(@EntityTypeCodesJson)))
  AND (@CanNavigateAll=1 OR projection.PermissionCode IN(SELECT [value] FROM OPENJSON(@GrantedPermissionsJson)))
  AND (projection.DisplayName LIKE N'%' + @OriginalQuery + N'%' OR projection.SearchText LIKE N'%' + @OriginalQuery + N'%' OR projection.NormalizedFieldsJson LIKE N'%' + STRING_ESCAPE(@OriginalQuery,'json') + N'%'
       OR projection.DisplayName LIKE N'%' + @Query + N'%' OR projection.SearchText LIKE N'%' + @Query + N'%' OR projection.NormalizedFieldsJson LIKE N'%' + STRING_ESCAPE(@Query,'json') + N'%');
IF EXISTS(SELECT 1 FROM Search.SearchCapability WHERE CapabilityCode=N'FULL_TEXT' AND IsAvailable=1 AND IsEnabled=1 AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL))
   AND EXISTS(SELECT 1 FROM sys.fulltext_indexes WHERE object_id=OBJECT_ID(N'Search.EntityProjection'))
BEGIN
    DECLARE @FullTextSql NVARCHAR(MAX)=N'INSERT #Ranked(EntityProjectionId,SearchRank) SELECT projection.EntityProjectionId,fullText.[RANK] FROM Search.EntityProjection projection JOIN CONTAINSTABLE(Search.EntityProjection,(DisplayName,SecondaryText,SearchText),@FullTextQuery,@MaximumResults) fullText ON fullText.[KEY]=projection.EntityProjectionId WHERE projection.TenantId=@TenantId AND projection.IsActive=1 AND projection.IsDeleted=0 AND (@AllTypes=1 OR projection.EntityTypeCode IN(SELECT [value] FROM OPENJSON(@EntityTypeCodesJson))) AND (@CanNavigateAll=1 OR projection.PermissionCode IN(SELECT [value] FROM OPENJSON(@GrantedPermissionsJson))) AND NOT EXISTS(SELECT 1 FROM #Ranked ranked WHERE ranked.EntityProjectionId=projection.EntityProjectionId);';
    EXEC sp_executesql @FullTextSql,N'@TenantId UNIQUEIDENTIFIER,@FullTextQuery NVARCHAR(4000),@MaximumResults INT,@AllTypes BIT,@EntityTypeCodesJson NVARCHAR(MAX),@CanNavigateAll BIT,@GrantedPermissionsJson NVARCHAR(MAX)',@TenantId,@FullTextQuery,@MaximumResults,@AllTypes,@EntityTypeCodesJson,@CanNavigateAll,@GrantedPermissionsJson;
END;
INSERT #Ranked(EntityProjectionId,SearchRank)
SELECT TOP(@MaximumResults) projection.EntityProjectionId,500+DIFFERENCE(projection.DisplayName,@OriginalQuery)*50
FROM Search.EntityProjection projection
WHERE projection.TenantId=@TenantId AND projection.IsActive=1 AND projection.IsDeleted=0
  AND (@AllTypes=1 OR projection.EntityTypeCode IN(SELECT [value] FROM OPENJSON(@EntityTypeCodesJson)))
  AND (@CanNavigateAll=1 OR projection.PermissionCode IN(SELECT [value] FROM OPENJSON(@GrantedPermissionsJson)))
  AND DIFFERENCE(projection.DisplayName,@OriginalQuery)>=3
  AND NOT EXISTS(SELECT 1 FROM #Ranked ranked WHERE ranked.EntityProjectionId=projection.EntityProjectionId)
ORDER BY DIFFERENCE(projection.DisplayName,@OriginalQuery) DESC,ABS(LEN(projection.DisplayName)-LEN(@OriginalQuery)),projection.DisplayName;
SELECT TOP(@MaximumResults) projection.EntityProjectionId,projection.EntityId,projection.EntityTypeCode,projection.DisplayName,projection.SecondaryText,projection.NavigationRoute,projection.PermissionCode,projection.SearchText,projection.NormalizedFieldsJson
FROM #Ranked ranked JOIN Search.EntityProjection projection ON projection.EntityProjectionId=ranked.EntityProjectionId
WHERE (@AllTypes=1 OR projection.EntityTypeCode IN(SELECT [value] FROM OPENJSON(@EntityTypeCodesJson))) AND (@CanNavigateAll=1 OR projection.PermissionCode IN(SELECT [value] FROM OPENJSON(@GrantedPermissionsJson)))
ORDER BY ranked.SearchRank DESC,projection.DisplayName;
""";
        var types = entityTypeCodes.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var permissions = grantedPermissions.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var normalizedQuery=query.Trim();
        var rows = await connection.QueryAsync<ProjectionRow>(new CommandDefinition(sql, new { TenantId = tenantId, Query = normalizedQuery, OriginalQuery = originalQuery.Trim(), FullTextQuery = BuildFullTextQuery([query]), MaximumResults = Math.Clamp(maximumResults * 4, 1, 400), AllTypes = types.Length == 0, EntityTypeCodesJson = JsonSerializer.Serialize(types, JsonOptions), CanNavigateAll = permissions.Contains("NAV_ALL", StringComparer.OrdinalIgnoreCase), GrantedPermissionsJson = JsonSerializer.Serialize(permissions, JsonOptions) }, cancellationToken: cancellationToken));
        return rows.Select(ToProjection).ToList();
    }

    private static string BuildFullTextQuery(IEnumerable<string?> values)
    {
        var tokens = values.Where(value => !string.IsNullOrWhiteSpace(value))
            .SelectMany(value => value!.Split([' ', ',', ';', '/', '-', '.'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Where(token => token.Length > 1)
            .Select(token => token.Replace("\"", "\"\""))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .Select(token => $"\"{token}*\"")
            .ToArray();
        return tokens.Length == 0 ? "\"__no_match__\"" : string.Join(" OR ", tokens);
    }

    public async Task<Guid> BeginExecutionAsync(EntityMatchRequest request, MatchPolicy policy, CancellationToken cancellationToken = default)
    {
        const string sql = """
DECLARE @Id UNIQUEIDENTIFIER,@ExistingHash VARBINARY(32); SELECT @Id=MatchExecutionId,@ExistingHash=RequestHash FROM Search.MatchExecution WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND CorrelationId=@CorrelationId AND IsDeleted=0;
IF @Id IS NOT NULL AND @ExistingHash<>@RequestHash THROW 51000,N'Correlation identifier was already used for a different matching request.',1;
IF @Id IS NULL BEGIN SET @Id=NEWID(); INSERT Search.MatchExecution(MatchExecutionId,TenantId,MatchProfileId,EntityTypeCode,SourceEntityId,CorrelationId,RequestHash,StatusCode,RequestedByUserId,StartedDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@Id,@TenantId,@MatchProfileId,@EntityTypeCode,@SourceEntityId,@CorrelationId,@RequestHash,N'RUNNING',@RequestedByUserId,SYSUTCDATETIME(),SYSUTCDATETIME(),@RequestedByUserId,0); END;
SELECT @Id;
""";
        var requestJson = JsonSerializer.Serialize(request.Fields.OrderBy(field => field.Key), JsonOptions);
        var requestHash = SHA256.HashData(Encoding.UTF8.GetBytes(requestJson));
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { request.TenantId, policy.MatchProfileId, request.EntityTypeCode, request.SourceEntityId, request.CorrelationId, RequestHash = requestHash, request.RequestedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task CompleteExecutionAsync(Guid matchExecutionId, IReadOnlyList<MatchCandidate> candidates, CancellationToken cancellationToken = default)
    {
        const string headerSql = """
DECLARE @TenantId UNIQUEIDENTIFIER=(SELECT TenantId FROM Search.MatchExecution WHERE MatchExecutionId=@MatchExecutionId AND IsDeleted=0);
DELETE evidence FROM Search.MatchReasonEvidence evidence JOIN Search.MatchCandidate candidate ON candidate.MatchCandidateId=evidence.MatchCandidateId WHERE candidate.MatchExecutionId=@MatchExecutionId;
DELETE FROM Search.MatchCandidate WHERE MatchExecutionId=@MatchExecutionId;
UPDATE Search.MatchExecution SET StatusCode=N'COMPLETED',CandidateCount=@CandidateCount,CompletedDateUtc=SYSUTCDATETIME(),ErrorMessage=NULL,ModifiedDateUtc=SYSUTCDATETIME() WHERE MatchExecutionId=@MatchExecutionId;
SELECT @TenantId;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var transaction = connection.BeginTransaction();
        var tenantId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(headerSql, new { MatchExecutionId = matchExecutionId, CandidateCount = candidates.Count }, transaction, cancellationToken: cancellationToken));
        for (var rank = 0; rank < candidates.Count; rank++)
        {
            var candidate = candidates[rank];
            var candidateId = Guid.NewGuid();
            const string candidateSql = "INSERT Search.MatchCandidate(MatchCandidateId,TenantId,MatchExecutionId,CandidateEntityId,DisplayName,OverallScore,ConfidenceBandCode,IsExactMatch,RequiresReview,RankOrder,CreatedDateUtc,IsDeleted) VALUES(@MatchCandidateId,@TenantId,@MatchExecutionId,@CandidateEntityId,@DisplayName,@OverallScore,@ConfidenceBandCode,@IsExactMatch,@RequiresReview,@RankOrder,SYSUTCDATETIME(),0);";
            await connection.ExecuteAsync(new CommandDefinition(candidateSql, new { MatchCandidateId = candidateId, TenantId = tenantId, MatchExecutionId = matchExecutionId, CandidateEntityId = candidate.EntityId, candidate.DisplayName, candidate.OverallScore, candidate.ConfidenceBandCode, candidate.IsExactMatch, candidate.RequiresReview, RankOrder = rank + 1 }, transaction, cancellationToken: cancellationToken));
            foreach (var reason in candidate.Reasons)
            {
                const string reasonSql = """
INSERT Search.MatchReasonEvidence(MatchReasonEvidenceId,TenantId,MatchCandidateId,MatchFieldRuleId,FieldCode,AlgorithmCode,SimilarityScore,WeightedScore,ReasonCode,Explanation,IsExactMatch,IsDiscrepancy,CreatedDateUtc,IsDeleted)
SELECT NEWID(),@TenantId,@MatchCandidateId,field.MatchFieldRuleId,@FieldCode,@AlgorithmCode,@SimilarityScore,@WeightedScore,@ReasonCode,@Explanation,@IsExactMatch,@IsDiscrepancy,SYSUTCDATETIME(),0
FROM Search.MatchExecution execution JOIN Search.MatchFieldRule field ON field.MatchProfileId=execution.MatchProfileId AND field.FieldCode=@FieldCode AND field.IsDeleted=0 JOIN Search.MatchAlgorithm algorithm ON algorithm.MatchAlgorithmId=field.MatchAlgorithmId AND algorithm.AlgorithmCode=@AlgorithmCode AND algorithm.IsDeleted=0 WHERE execution.MatchExecutionId=@MatchExecutionId;
""";
                await connection.ExecuteAsync(new CommandDefinition(reasonSql, new { TenantId = tenantId, MatchCandidateId = candidateId, MatchExecutionId = matchExecutionId, reason.FieldCode, reason.AlgorithmCode, reason.SimilarityScore, reason.WeightedScore, reason.ReasonCode, reason.Explanation, reason.IsExactMatch, reason.IsDiscrepancy }, transaction, cancellationToken: cancellationToken));
            }
        }
        transaction.Commit();
    }

    public async Task FailExecutionAsync(Guid matchExecutionId, string errorMessage, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Search.MatchExecution SET StatusCode=N'FAILED',CompletedDateUtc=SYSUTCDATETIME(),ErrorMessage=@ErrorMessage,ModifiedDateUtc=SYSUTCDATETIME() WHERE MatchExecutionId=@MatchExecutionId AND IsDeleted=0;";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql, new { MatchExecutionId = matchExecutionId, ErrorMessage = errorMessage[..Math.Min(errorMessage.Length, 2000)] }, cancellationToken: cancellationToken));
    }

    private static MatchProjection ToProjection(ProjectionRow row)
    {
        var fields=JsonSerializer.Deserialize<Dictionary<string,string?>>(row.NormalizedFieldsJson,JsonOptions)??[];
        fields["DisplayName"]=row.DisplayName;
        fields["SearchText"]=row.SearchText;
        return new(row.EntityProjectionId,row.EntityId,row.EntityTypeCode,row.DisplayName,row.SecondaryText,row.NavigationRoute,row.PermissionCode,fields);
    }
    private sealed record ProfileRow(Guid MatchProfileId, string ProfileCode, string EntityTypeCode, decimal ExactThreshold, decimal StrongThreshold, decimal PossibleThreshold, int MaximumCandidates, int SemanticMaximumConcepts, bool RequiresReview);
    private sealed record ProfileSettingRow(Guid MatchProfileId, bool IsInherited, string ProfileCode, string EntityTypeCode, string DisplayName, string? Description, decimal ExactThreshold, decimal StrongThreshold, decimal PossibleThreshold, int MaximumCandidates, int SemanticMaximumConcepts, bool RequiresReview, bool IsActive, byte[] RowVersion);
    private sealed record ProjectionRow(Guid EntityProjectionId,Guid EntityId,string EntityTypeCode,string DisplayName,string? SecondaryText,string? NavigationRoute,string PermissionCode,string? SearchText,string NormalizedFieldsJson);
}
