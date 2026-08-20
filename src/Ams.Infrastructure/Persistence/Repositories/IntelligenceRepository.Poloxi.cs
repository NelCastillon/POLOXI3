using System.Text.Json;
using Ams.Application.Features.Intelligence;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed partial class IntelligenceRepository
{
    public async Task<PoloxiConfiguration> GetPoloxiConfigurationAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""
SELECT
CONVERT(bit,COALESCE(TRY_CONVERT(bit,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Poloxi.EnableHierarchyReuse' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),1)) EnableHierarchyReuse,
COALESCE(TRY_CONVERT(int,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Poloxi.HierarchyCacheHours' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),168) HierarchyCacheHours,
COALESCE(TRY_CONVERT(decimal(5,4),(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Poloxi.MinimumBranchConfidence' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),.65) MinimumBranchConfidence,
COALESCE(TRY_CONVERT(int,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Poloxi.MaximumBranches' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),12) MaximumBranches,
COALESCE(TRY_CONVERT(int,(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Poloxi.MaximumResults' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),50) MaximumResults;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row=await connection.QuerySingleAsync<PoloxiConfigurationRow>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken));
        return new(row.EnableHierarchyReuse,Math.Clamp(row.HierarchyCacheHours,1,8760),Math.Clamp(row.MinimumBranchConfidence,0,1),Math.Clamp(row.MaximumBranches,1,50),Math.Clamp(row.MaximumResults,1,100));
    }

    public async Task<IReadOnlyCollection<PoloxiCapabilityDto>> GetPoloxiCapabilitiesAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""
WITH capabilities AS
(
    SELECT capability.*,ROW_NUMBER() OVER(PARTITION BY capability.CapabilityCode ORDER BY CASE WHEN capability.TenantId=@TenantId THEN 0 ELSE 1 END,capability.SortOrder) Choice
    FROM POLOXI.Capability capability WHERE capability.IsActive=1 AND capability.IsDeleted=0 AND (capability.TenantId=@TenantId OR capability.TenantId IS NULL)
)
SELECT CapabilityId,CapabilityCode,DisplayName,Description,EntityTypeCode,ModuleCode,ExecutionHandlerCode,ApprovedTermsJson,SupportsRecency,MinimumConfidence,SortOrder FROM capabilities WHERE Choice=1 ORDER BY SortOrder,DisplayName;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows=await connection.QueryAsync<PoloxiCapabilityRow>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken));
        return rows.Select(row=>new PoloxiCapabilityDto(row.CapabilityId,row.CapabilityCode,row.DisplayName,row.Description,row.EntityTypeCode,row.ModuleCode,row.ExecutionHandlerCode,JsonSerializer.Deserialize<string[]>(row.ApprovedTermsJson)??[],row.SupportsRecency,row.MinimumConfidence,row.SortOrder)).ToArray();
    }

    public async Task<PoloxiHierarchyRecord?> GetReusablePoloxiHierarchyAsync(Guid tenantId,string querySignature,CancellationToken cancellationToken=default)
    {
        const string sql="""
SELECT TOP(1) HierarchyId,QuerySignature,ConceptCode,DisplayName,NormalizedQuery,VersionNumber,StatusCode,GeneratedByProviderCode,GeneratedByModelCode,Confidence,UsageCount,SuccessfulUsageCount,ExpiresDateUtc
FROM POLOXI.Hierarchy WHERE TenantId=@TenantId AND QuerySignature=@QuerySignature AND StatusCode=N'VALIDATED' AND IsDeleted=0 AND (ExpiresDateUtc IS NULL OR ExpiresDateUtc>SYSUTCDATETIME()) ORDER BY VersionNumber DESC;
SELECT branch.HierarchyBranchId,branch.ParentHierarchyBranchId,branch.BranchCode,branch.DisplayName,branch.ProposedCondition,branch.CapabilityCode,branch.ValidationStatusCode,branch.ValidationMessage,branch.SearchText,branch.OrderByRecency,branch.Confidence,branch.SortOrder
FROM POLOXI.HierarchyBranch branch JOIN POLOXI.Hierarchy hierarchy ON hierarchy.HierarchyId=branch.HierarchyId
WHERE hierarchy.TenantId=@TenantId AND hierarchy.QuerySignature=@QuerySignature AND hierarchy.StatusCode=N'VALIDATED' AND hierarchy.IsDeleted=0 AND branch.IsDeleted=0 AND (hierarchy.ExpiresDateUtc IS NULL OR hierarchy.ExpiresDateUtc>SYSUTCDATETIME())
AND hierarchy.VersionNumber=(SELECT MAX(versioned.VersionNumber) FROM POLOXI.Hierarchy versioned WHERE versioned.TenantId=@TenantId AND versioned.QuerySignature=@QuerySignature AND versioned.StatusCode=N'VALIDATED' AND versioned.IsDeleted=0 AND (versioned.ExpiresDateUtc IS NULL OR versioned.ExpiresDateUtc>SYSUTCDATETIME())) ORDER BY branch.SortOrder;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,new{TenantId=tenantId,QuerySignature=querySignature},cancellationToken:cancellationToken));
        var hierarchy=await multi.ReadSingleOrDefaultAsync<PoloxiHierarchyRow>();
        if(hierarchy is null)return null;
        var branches=(await multi.ReadAsync<PoloxiBranchRecord>()).AsList();
        return ToHierarchy(hierarchy,branches);
    }

    public async Task<PoloxiHierarchyRecord> SavePoloxiHierarchyAsync(Guid tenantId,Guid userId,string querySignature,string normalizedQuery,PoloxiHierarchyProposal proposal,string? providerCode,string? modelCode,DateTime expiresDateUtc,IReadOnlyCollection<PoloxiBranchRecord> branches,CancellationToken cancellationToken=default)
    {
        const string sql="""
SET XACT_ABORT ON;BEGIN TRANSACTION;
DECLARE @HierarchyId UNIQUEIDENTIFIER=NEWID();
DECLARE @VersionNumber INT=COALESCE((SELECT MAX(VersionNumber)+1 FROM POLOXI.Hierarchy WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND QuerySignature=@QuerySignature),1);
INSERT POLOXI.Hierarchy(HierarchyId,TenantId,QuerySignature,ConceptCode,DisplayName,NormalizedQuery,VersionNumber,StatusCode,GeneratedByProviderCode,GeneratedByModelCode,Confidence,UsageCount,SuccessfulUsageCount,ExpiresDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
VALUES(@HierarchyId,@TenantId,@QuerySignature,@ConceptCode,@DisplayName,@NormalizedQuery,@VersionNumber,N'VALIDATED',@ProviderCode,@ModelCode,@Confidence,0,0,@ExpiresDateUtc,SYSUTCDATETIME(),@UserId,0);
INSERT POLOXI.HierarchyBranch(HierarchyBranchId,TenantId,HierarchyId,ParentHierarchyBranchId,BranchCode,DisplayName,ProposedCondition,CapabilityCode,ValidationStatusCode,ValidationMessage,SearchText,OrderByRecency,Confidence,SortOrder,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT HierarchyBranchId,@TenantId,@HierarchyId,ParentHierarchyBranchId,BranchCode,DisplayName,ProposedCondition,CapabilityCode,ValidationStatusCode,ValidationMessage,SearchText,OrderByRecency,Confidence,SortOrder,SYSUTCDATETIME(),@UserId,0 FROM OPENJSON(@BranchesJson) WITH(HierarchyBranchId uniqueidentifier,ParentHierarchyBranchId uniqueidentifier,BranchCode nvarchar(120),DisplayName nvarchar(300),ProposedCondition nvarchar(1000),CapabilityCode nvarchar(120),ValidationStatusCode nvarchar(30),ValidationMessage nvarchar(1000),SearchText nvarchar(500),OrderByRecency bit,Confidence decimal(5,4),SortOrder int);
COMMIT;SELECT @HierarchyId HierarchyId,@QuerySignature QuerySignature,@ConceptCode ConceptCode,@DisplayName DisplayName,@NormalizedQuery NormalizedQuery,@VersionNumber VersionNumber,N'VALIDATED' StatusCode,@ProviderCode GeneratedByProviderCode,@ModelCode GeneratedByModelCode,@Confidence Confidence,0 UsageCount,0 SuccessfulUsageCount,@ExpiresDateUtc ExpiresDateUtc;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row=await connection.QuerySingleAsync<PoloxiHierarchyRow>(new CommandDefinition(sql,new{TenantId=tenantId,UserId=userId,QuerySignature=querySignature,NormalizedQuery=normalizedQuery,proposal.ConceptCode,proposal.DisplayName,proposal.Confidence,ProviderCode=providerCode,ModelCode=modelCode,ExpiresDateUtc=expiresDateUtc,BranchesJson=JsonSerializer.Serialize(branches)},cancellationToken:cancellationToken));
        return ToHierarchy(row,branches);
    }

    public async Task<IReadOnlyCollection<PoloxiEvidenceDto>> ExecutePoloxiBranchAsync(PoloxiSearchRequest request,PoloxiBranchRecord branch,PoloxiCapabilityDto capability,int maximumResults,CancellationToken cancellationToken=default)
    {
        const string sql="""
SELECT TOP(@MaximumResults) document.SearchDocumentId,document.EntityTypeCode,document.EntityId,document.ModuleCode,document.Title,LEFT(document.ContentText,2000) Excerpt,projection.NavigationRoute,
CONVERT(decimal(9,6),CASE WHEN @OrderByRecency=1 THEN .70 WHEN document.Title LIKE N'%'+@SearchText+N'%' THEN .90 WHEN document.ContentText LIKE N'%'+@SearchText+N'%' OR document.Keywords LIKE N'%'+@SearchText+N'%' THEN .75 ELSE .50 END) RelevanceScore
FROM AI.SearchDocument document
LEFT JOIN Search.EntityProjection projection ON projection.TenantId=document.TenantId AND projection.EntityTypeCode=document.EntityTypeCode AND projection.EntityId=document.EntityId AND projection.IsActive=1 AND projection.IsDeleted=0
WHERE document.TenantId=@TenantId AND document.EntityTypeCode=@EntityTypeCode AND document.ModuleCode=@ModuleCode AND document.IsDeleted=0
AND (@OrderByRecency=1 OR @SearchText=N'' OR document.Title LIKE N'%'+@SearchText+N'%' OR document.ContentText LIKE N'%'+@SearchText+N'%' OR document.Keywords LIKE N'%'+@SearchText+N'%')
AND EXISTS(SELECT 1 FROM AI.SearchPermission permission WHERE permission.TenantId=document.TenantId AND permission.SearchDocumentId=document.SearchDocumentId AND permission.PermissionCode=N'READ' AND permission.IsDeleted=0 AND ((permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId) OR (permission.PrincipalTypeCode=N'ROLE' AND EXISTS(SELECT 1 FROM IAM.UserRole userRole WHERE userRole.TenantId=@TenantId AND userRole.UserId=@UserId AND userRole.RoleId=permission.PrincipalId AND userRole.IsActive=1 AND userRole.IsDeleted=0))))
ORDER BY CASE WHEN @OrderByRecency=1 THEN document.SourceCreatedDateUtc END DESC,RelevanceScore DESC,document.Title;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows=(await connection.QueryAsync<PoloxiEvidenceRow>(new CommandDefinition(sql,new{request.TenantId,request.UserId,capability.EntityTypeCode,capability.ModuleCode,SearchText=branch.SearchText??string.Empty,branch.OrderByRecency,MaximumResults=Math.Clamp(maximumResults,1,100)},cancellationToken:cancellationToken))).AsList();
        return rows.Select((row,index)=>new PoloxiEvidenceDto(branch.HierarchyBranchId,row.SearchDocumentId,row.EntityTypeCode,row.EntityId,row.ModuleCode,row.Title,row.Excerpt,row.NavigationRoute,row.RelevanceScore,index+1,[branch.DisplayName])).ToArray();
    }

    public async Task<Guid> StartPoloxiExecutionAsync(PoloxiExecutionStart execution,CancellationToken cancellationToken=default)
    {
        const string sql="""DECLARE @Id UNIQUEIDENTIFIER=NEWID();INSERT POLOXI.Execution(PoloxiExecutionId,TenantId,HierarchyId,UserId,QueryText,CorrelationId,StatusCode,WasHierarchyReused,ValidBranchCount,UnsupportedBranchCount,ResultCount,Confidence,ExplanationStatusCode,StartedDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@Id,@TenantId,@HierarchyId,@UserId,@QueryText,@CorrelationId,N'RUNNING',@WasHierarchyReused,@ValidBranchCount,@UnsupportedBranchCount,0,@Confidence,N'NOT_REQUESTED',SYSUTCDATETIME(),SYSUTCDATETIME(),@UserId,0);UPDATE POLOXI.Hierarchy SET UsageCount=UsageCount+1,LastUsedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE TenantId=@TenantId AND HierarchyId=@HierarchyId AND IsDeleted=0;SELECT @Id;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql,execution,cancellationToken:cancellationToken));
    }

    public async Task CompletePoloxiExecutionAsync(Guid tenantId,Guid userId,Guid poloxiExecutionId,Guid hierarchyId,IReadOnlyCollection<PoloxiEvidenceDto> evidence,string explanationStatusCode,string? explanation,long durationMilliseconds,CancellationToken cancellationToken=default)
    {
        const string sql="""
SET XACT_ABORT ON;BEGIN TRANSACTION;
UPDATE POLOXI.Execution SET StatusCode=N'COMPLETED',ResultCount=@ResultCount,ExplanationStatusCode=@ExplanationStatusCode,Explanation=@Explanation,DurationMilliseconds=@DurationMilliseconds,CompletedDateUtc=SYSUTCDATETIME(),ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE TenantId=@TenantId AND PoloxiExecutionId=@PoloxiExecutionId AND IsDeleted=0;
INSERT POLOXI.ExecutionEvidence(ExecutionEvidenceId,TenantId,PoloxiExecutionId,HierarchyBranchId,SearchDocumentId,EntityTypeCode,EntityId,SourceReference,Title,Excerpt,RelevanceScore,RankNumber,CreatedDateUtc,CreatedByUserId,IsDeleted)
SELECT NEWID(),@TenantId,@PoloxiExecutionId,HierarchyBranchId,SearchDocumentId,EntityTypeCode,EntityId,COALESCE(NavigationRoute,CONCAT(EntityTypeCode,N':',CONVERT(nvarchar(36),EntityId))),Title,Excerpt,RelevanceScore,RankNumber,SYSUTCDATETIME(),@UserId,0 FROM OPENJSON(@EvidenceJson) WITH(HierarchyBranchId uniqueidentifier,SearchDocumentId uniqueidentifier,EntityTypeCode nvarchar(100),EntityId uniqueidentifier,Title nvarchar(500),Excerpt nvarchar(2000),NavigationRoute nvarchar(2000),RelevanceScore decimal(9,6),RankNumber int);
UPDATE POLOXI.Hierarchy SET SuccessfulUsageCount=SuccessfulUsageCount+CASE WHEN @ResultCount>0 THEN 1 ELSE 0 END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE TenantId=@TenantId AND HierarchyId=@HierarchyId AND IsDeleted=0;
COMMIT;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,new{TenantId=tenantId,UserId=userId,PoloxiExecutionId=poloxiExecutionId,HierarchyId=hierarchyId,ResultCount=evidence.Count,ExplanationStatusCode=explanationStatusCode,Explanation=explanation,DurationMilliseconds=durationMilliseconds,EvidenceJson=JsonSerializer.Serialize(evidence)},cancellationToken:cancellationToken));
    }

    private static PoloxiHierarchyRecord ToHierarchy(PoloxiHierarchyRow row,IReadOnlyCollection<PoloxiBranchRecord> branches)=>new(row.HierarchyId,row.QuerySignature,row.ConceptCode,row.DisplayName,row.NormalizedQuery,row.VersionNumber,row.StatusCode,row.GeneratedByProviderCode,row.GeneratedByModelCode,row.Confidence,row.UsageCount,row.SuccessfulUsageCount,row.ExpiresDateUtc,branches);
    private sealed record PoloxiConfigurationRow(bool EnableHierarchyReuse,int HierarchyCacheHours,decimal MinimumBranchConfidence,int MaximumBranches,int MaximumResults);
    private sealed record PoloxiCapabilityRow(Guid CapabilityId,string CapabilityCode,string DisplayName,string Description,string EntityTypeCode,string ModuleCode,string ExecutionHandlerCode,string ApprovedTermsJson,bool SupportsRecency,decimal MinimumConfidence,int SortOrder);
    private sealed record PoloxiHierarchyRow(Guid HierarchyId,string QuerySignature,string ConceptCode,string DisplayName,string NormalizedQuery,int VersionNumber,string StatusCode,string? GeneratedByProviderCode,string? GeneratedByModelCode,decimal Confidence,int UsageCount,int SuccessfulUsageCount,DateTime? ExpiresDateUtc);
    private sealed record PoloxiEvidenceRow(Guid SearchDocumentId,string EntityTypeCode,Guid EntityId,string ModuleCode,string Title,string? Excerpt,string? NavigationRoute,decimal RelevanceScore);
}
