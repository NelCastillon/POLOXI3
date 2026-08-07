using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ams.Application.Common.Models;
using Ams.Application.Features.Intelligence;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed partial class IntelligenceRepository
{
    public async Task<IntelligencePlatformSummaryDto> GetPlatformSummaryAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""
            SELECT IntelligencePillarId,PillarCode,DisplayName,Description,SortOrder,IsActive FROM AI.IntelligencePillar WHERE (TenantId IS NULL OR TenantId=@TenantId) AND IsDeleted=0 ORDER BY SortOrder;
            SELECT IntelligenceCapabilityId,IntelligencePillarId,CapabilityCode,DisplayName,Description,EngineKindCode,OwningModuleCode,IsAdvisory,RequiresHumanReview,SortOrder,IsActive FROM AI.IntelligenceCapability WHERE (TenantId IS NULL OR TenantId=@TenantId) AND IsDeleted=0 ORDER BY IntelligencePillarId,SortOrder;
            SELECT
              (SELECT COUNT(1) FROM AI.IntelligenceFinding WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode IN(N'OPEN',N'REVIEW_REQUIRED',N'IN_REVIEW')) OpenFindingCount,
              (SELECT COUNT(1) FROM AI.BusinessSignal WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode IN(N'OPEN',N'REVIEW_REQUIRED',N'IN_PROGRESS')) OpenBusinessSignalCount,
              (SELECT COUNT(1) FROM AI.ReasoningSession WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode IN(N'QUEUED',N'PROCESSING')) ActiveReasoningSessionCount,
              (SELECT COUNT(1) FROM AI.IntelligenceWorkItem WHERE TenantId=@TenantId AND IsDeleted=0 AND StatusCode IN(N'PENDING',N'PROCESSING',N'RETRY')) PendingWorkItemCount;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken));
        var pillarRows=(await multi.ReadAsync<PillarRow>()).AsList();
        var capabilities=(await multi.ReadAsync<IntelligenceCapabilityDto>()).AsList();
        var totals=await multi.ReadSingleAsync<PlatformTotals>();
        var pillars=pillarRows.Select(p=>new IntelligencePillarDto(p.IntelligencePillarId,p.PillarCode,p.DisplayName,p.Description,p.SortOrder,p.IsActive,capabilities.Where(c=>c.IntelligencePillarId==p.IntelligencePillarId).OrderBy(c=>c.SortOrder).ToArray())).ToArray();
        return new(DateTime.UtcNow,pillars.Count(x=>x.IsActive),capabilities.Count(x=>x.IsActive),totals.OpenFindingCount,totals.OpenBusinessSignalCount,totals.ActiveReasoningSessionCount,totals.PendingWorkItemCount,pillars);
    }

    public async Task<PlatformArchitectureDto> GetPlatformArchitectureAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""
WITH services AS
(
  SELECT service.*,ROW_NUMBER() OVER(PARTITION BY service.ServiceCode ORDER BY CASE WHEN service.TenantId=@TenantId THEN 0 ELSE 1 END) Choice FROM Platform.ServiceCatalog service WHERE service.IsDeleted=0 AND (service.TenantId=@TenantId OR service.TenantId IS NULL)
)
SELECT PlatformServiceId,ServiceCode,DisplayName,Description,ServiceKindCode,OwningSchemaCode,ContractReference,AdministrationRoute,MaturityCode,ImplementationStatusCode,ImplementationNotes,IsInfrastructureOnly,IsActive,SortOrder FROM services WHERE Choice=1 ORDER BY SortOrder,DisplayName;
WITH modules AS
(
  SELECT module.*,ROW_NUMBER() OVER(PARTITION BY module.ModuleCode ORDER BY CASE WHEN module.TenantId=@TenantId THEN 0 ELSE 1 END) Choice FROM Platform.BusinessModuleCatalog module WHERE module.IsDeleted=0 AND (module.TenantId=@TenantId OR module.TenantId IS NULL)
)
SELECT BusinessModuleId,ModuleCode,DisplayName,Description,OwningSchemaCode,NavigationRoute,IsActive,SortOrder FROM modules WHERE Choice=1 ORDER BY SortOrder,DisplayName;
SELECT dependency.ModuleServiceDependencyId,dependency.BusinessModuleId,dependency.PlatformServiceId,service.ServiceCode,service.DisplayName ServiceName,dependency.UsageCode,dependency.Description,dependency.AdoptionStatusCode,dependency.ConsumerReference,dependency.LastVerifiedDateUtc,dependency.IsRequired,dependency.IsActive FROM Platform.ModuleServiceDependency dependency JOIN Platform.ServiceCatalog service ON service.PlatformServiceId=dependency.PlatformServiceId AND service.IsDeleted=0 WHERE dependency.IsDeleted=0 AND (dependency.TenantId=@TenantId OR dependency.TenantId IS NULL) ORDER BY dependency.BusinessModuleId,service.SortOrder;
SELECT gap.MigrationGapId,gap.GapCode,gap.PlatformServiceId,service.ServiceCode,service.DisplayName ServiceName,gap.BusinessModuleId,module.ModuleCode,gap.SourceReference,gap.TargetContractReference,gap.Description,gap.PriorityCode,gap.StatusCode,gap.RemediationJson,gap.DetectedDateUtc,gap.CompletedDateUtc
FROM Platform.MigrationGap gap
JOIN Platform.ServiceCatalog service ON service.PlatformServiceId=gap.PlatformServiceId AND service.IsDeleted=0
LEFT JOIN Platform.BusinessModuleCatalog module ON module.BusinessModuleId=gap.BusinessModuleId AND module.IsDeleted=0
WHERE gap.IsDeleted=0 AND (gap.TenantId=@TenantId OR gap.TenantId IS NULL)
ORDER BY CASE gap.PriorityCode WHEN N'CRITICAL' THEN 1 WHEN N'HIGH' THEN 2 WHEN N'MEDIUM' THEN 3 ELSE 4 END,gap.DetectedDateUtc DESC;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken));
        var services=(await multi.ReadAsync<PlatformServiceCatalogDto>()).AsList();
        var moduleRows=(await multi.ReadAsync<BusinessModuleRow>()).AsList();
        var dependencies=(await multi.ReadAsync<ModuleServiceDependencyRow>()).AsList();
        var gaps=(await multi.ReadAsync<PlatformMigrationGapDto>()).AsList();
        var modules=moduleRows.Select(module=>new BusinessModuleCatalogDto(module.BusinessModuleId,module.ModuleCode,module.DisplayName,module.Description,module.OwningSchemaCode,module.NavigationRoute,module.IsActive,module.SortOrder,dependencies.Where(x=>x.BusinessModuleId==module.BusinessModuleId).Select(x=>new ModuleServiceDependencyDto(x.ModuleServiceDependencyId,x.PlatformServiceId,x.ServiceCode,x.ServiceName,x.UsageCode,x.Description,x.AdoptionStatusCode,x.ConsumerReference,x.LastVerifiedDateUtc,x.IsRequired,x.IsActive)).ToArray())).ToArray();
        return new(DateTime.UtcNow,services,modules,gaps);
    }

    public async Task<IReadOnlyCollection<IntelligenceComplianceRequirementDto>> GetComplianceRequirementsAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""SELECT ComplianceRequirementId,RequirementCode,DisplayName,Description,RequirementScopeCode,JurisdictionCode,CarrierId,LineOfBusinessCode,EntityTypeCode,RequirementTypeCode,SeverityCode,BlocksTransaction,CanBeWaived,WaiverPermissionCode,ApprovalPermissionCode,VersionNumber,IsActive,RowVersion FROM AI.ComplianceRequirement WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY RequirementScopeCode,DisplayName,VersionNumber DESC;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);return (await connection.QueryAsync<IntelligenceComplianceRequirementDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken))).AsList();
    }

    public async Task<IReadOnlyCollection<IntelligenceSafetyEventDto>> GetSafetyEventsAsync(Guid tenantId,int pageSize,CancellationToken cancellationToken=default)
    {
        const string sql="""SELECT TOP(@PageSize) event.SafetyEventId,control.ControlCode,control.DisplayName ControlName,event.EventTypeCode,event.EnforcementStageCode,event.ActionCode,event.SeverityCode,event.RequiresHumanReview,event.ReviewStatusCode,event.DetectedDateUtc FROM AI.SafetyEvent event JOIN AI.SafetyControl control ON control.SafetyControlId=event.SafetyControlId WHERE event.TenantId=@TenantId AND event.IsDeleted=0 ORDER BY event.DetectedDateUtc DESC;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);return (await connection.QueryAsync<IntelligenceSafetyEventDto>(new CommandDefinition(sql,new{TenantId=tenantId,PageSize=pageSize},cancellationToken:cancellationToken))).AsList();
    }

    public async Task<IReadOnlyCollection<IntelligencePromptDefinitionDto>> GetPromptDefinitionsAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""SELECT prompt.PromptDefinitionId,prompt.TenantId,prompt.IntelligenceCapabilityId,capability.CapabilityCode,prompt.PromptCode,prompt.VersionLabel,prompt.DisplayName,prompt.SystemInstructions,prompt.InputSchemaJson,prompt.OutputSchemaJson,prompt.StatusCode,prompt.ApprovedByUserId,prompt.ApprovedDateUtc,prompt.EffectiveFromUtc,prompt.EffectiveToUtc,prompt.RowVersion FROM AI.PromptDefinition prompt JOIN AI.IntelligenceCapability capability ON capability.IntelligenceCapabilityId=prompt.IntelligenceCapabilityId WHERE (prompt.TenantId IS NULL OR prompt.TenantId=@TenantId) AND prompt.IsDeleted=0 ORDER BY prompt.PromptCode,CASE prompt.StatusCode WHEN N'APPROVED' THEN 1 WHEN N'DRAFT' THEN 2 ELSE 3 END,prompt.CreatedDateUtc DESC;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);return (await connection.QueryAsync<IntelligencePromptDefinitionDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken))).AsList();
    }

    public async Task SavePromptDefinitionAsync(SaveIntelligencePromptDefinitionRequest request,CancellationToken cancellationToken=default)
    {
        const string sql="""
            IF NOT EXISTS(SELECT 1 FROM AI.IntelligenceCapability WHERE IntelligenceCapabilityId=@IntelligenceCapabilityId AND (TenantId IS NULL OR TenantId=@TenantId) AND IsDeleted=0) THROW 51000,'Intelligence capability was not found.',1;
            IF @StatusCode NOT IN(N'DRAFT',N'APPROVED',N'RETIRED') THROW 51000,'Prompt status is invalid.',1;
            IF @RowVersion IS NOT NULL
            BEGIN
              IF EXISTS(SELECT 1 FROM AI.PromptDefinition WHERE TenantId=@TenantId AND PromptCode=@PromptCode AND VersionLabel=@VersionLabel AND StatusCode=N'APPROVED' AND IsDeleted=0) THROW 51000,'Approved prompts are immutable; create a new version.',1;
              UPDATE AI.PromptDefinition SET IntelligenceCapabilityId=@IntelligenceCapabilityId,DisplayName=@DisplayName,SystemInstructions=@SystemInstructions,InputSchemaJson=@InputSchemaJson,OutputSchemaJson=@OutputSchemaJson,StatusCode=@StatusCode,ApprovedByUserId=CASE WHEN @StatusCode=N'APPROVED' THEN @ActorUserId ELSE NULL END,ApprovedDateUtc=CASE WHEN @StatusCode=N'APPROVED' THEN SYSUTCDATETIME() ELSE NULL END,EffectiveFromUtc=@EffectiveFromUtc,EffectiveToUtc=@EffectiveToUtc,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND PromptCode=@PromptCode AND VersionLabel=@VersionLabel AND RowVersion=@RowVersion AND IsDeleted=0;
              IF @@ROWCOUNT=0 THROW 51000,'Prompt definition changed before this update.',1;
            END
            ELSE
            BEGIN
              INSERT AI.PromptDefinition(TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,EffectiveToUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
              SELECT @TenantId,@IntelligenceCapabilityId,@PromptCode,@VersionLabel,@DisplayName,@SystemInstructions,@InputSchemaJson,@OutputSchemaJson,@StatusCode,CASE WHEN @StatusCode=N'APPROVED' THEN @ActorUserId END,CASE WHEN @StatusCode=N'APPROVED' THEN SYSUTCDATETIME() END,@EffectiveFromUtc,@EffectiveToUtc,SYSUTCDATETIME(),@ActorUserId,0
              WHERE NOT EXISTS(SELECT 1 FROM AI.PromptDefinition WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND PromptCode=@PromptCode AND VersionLabel=@VersionLabel AND IsDeleted=0);
              IF @@ROWCOUNT=0 THROW 51000,'Prompt version already exists.',1;
            END;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);await connection.ExecuteAsync(new CommandDefinition(sql,request,cancellationToken:cancellationToken));
    }

    public async Task SubmitEvaluationSampleLabelAsync(SubmitEvaluationSampleLabelRequest request,CancellationToken cancellationToken=default)
    {
        const string sql="""IF NOT EXISTS(SELECT 1 FROM AI.Execution WHERE TenantId=@TenantId AND ExecutionId=@ExecutionId AND IsDeleted=0) THROW 51000,'Execution was not found.',1; INSERT AI.EvaluationSampleLabel(TenantId,ExecutionId,EvaluationDefinitionId,PredictedPositive,ActualPositive,IsHallucination,IsAccurate,LabelSourceCode,Notes,LabeledByUserId,LabeledDateUtc,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@TenantId,@ExecutionId,@EvaluationDefinitionId,@PredictedPositive,@ActualPositive,@IsHallucination,@IsAccurate,@LabelSourceCode,@Notes,@ActorUserId,SYSUTCDATETIME(),SYSUTCDATETIME(),@ActorUserId,0);""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);await connection.ExecuteAsync(new CommandDefinition(sql,request,cancellationToken:cancellationToken));
    }

    public async Task<IReadOnlyCollection<IntelligenceEnginePolicyDto>> GetEnginePoliciesAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""SELECT policy.EnginePolicyId,policy.TenantId,policy.IntelligenceCapabilityId,capability.CapabilityCode,policy.PolicyCode,policy.DisplayName,policy.Description,policy.ExecutionModeCode,policy.ConfigurationJson,policy.MinimumConfidence,policy.RequiresHumanReview,policy.FailClosed,policy.EffectiveFromUtc,policy.EffectiveToUtc,policy.VersionNumber,policy.IsActive,policy.RowVersion FROM AI.EnginePolicy policy JOIN AI.IntelligenceCapability capability ON capability.IntelligenceCapabilityId=policy.IntelligenceCapabilityId AND capability.IsDeleted=0 WHERE (policy.TenantId IS NULL OR policy.TenantId=@TenantId) AND policy.IsDeleted=0 ORDER BY capability.SortOrder,policy.PolicyCode,policy.VersionNumber DESC;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<IntelligenceEnginePolicyDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken))).AsList();
    }

    public async Task SaveEnginePolicyAsync(SaveIntelligenceEnginePolicyRequest request,CancellationToken cancellationToken=default)
    {
        const string sql="""
            IF NOT EXISTS(SELECT 1 FROM AI.IntelligenceCapability WHERE IntelligenceCapabilityId=@IntelligenceCapabilityId AND (TenantId IS NULL OR TenantId=@TenantId) AND IsDeleted=0) THROW 51000,'Intelligence capability was not found.',1;
            IF @RowVersion IS NOT NULL
            BEGIN
              UPDATE AI.EnginePolicy SET IntelligenceCapabilityId=@IntelligenceCapabilityId,DisplayName=@DisplayName,Description=@Description,ExecutionModeCode=@ExecutionModeCode,ConfigurationJson=@ConfigurationJson,MinimumConfidence=@MinimumConfidence,RequiresHumanReview=@RequiresHumanReview,FailClosed=@FailClosed,EffectiveFromUtc=@EffectiveFromUtc,EffectiveToUtc=@EffectiveToUtc,IsActive=@IsActive,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND PolicyCode=@PolicyCode AND VersionNumber=@VersionNumber AND RowVersion=@RowVersion AND IsDeleted=0;
              IF @@ROWCOUNT=0 THROW 51000,'Engine policy changed before this update.',1;
            END
            ELSE
            BEGIN
              INSERT AI.EnginePolicy(TenantId,IntelligenceCapabilityId,PolicyCode,DisplayName,Description,ExecutionModeCode,ConfigurationJson,MinimumConfidence,RequiresHumanReview,FailClosed,EffectiveFromUtc,EffectiveToUtc,VersionNumber,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted)
              SELECT @TenantId,@IntelligenceCapabilityId,@PolicyCode,@DisplayName,@Description,@ExecutionModeCode,@ConfigurationJson,@MinimumConfidence,@RequiresHumanReview,@FailClosed,@EffectiveFromUtc,@EffectiveToUtc,@VersionNumber,@IsActive,SYSUTCDATETIME(),@ActorUserId,0
              WHERE NOT EXISTS(SELECT 1 FROM AI.EnginePolicy WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND PolicyCode=@PolicyCode AND VersionNumber=@VersionNumber AND IsDeleted=0);
              IF @@ROWCOUNT=0 THROW 51000,'Engine policy version already exists; reload it before updating.',1;
            END;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,request,cancellationToken:cancellationToken));
    }

    public async Task<IReadOnlyCollection<IntelligenceSafetyControlDto>> GetSafetyControlsAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""SELECT SafetyControlId,TenantId,ControlCode,DisplayName,Description,ControlTypeCode,EnforcementStageCode,ConfigurationJson,ViolationActionCode,RequiresHumanReview,SortOrder,IsActive,RowVersion FROM AI.SafetyControl WHERE (TenantId IS NULL OR TenantId=@TenantId) AND IsDeleted=0 ORDER BY SortOrder,DisplayName;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<IntelligenceSafetyControlDto>(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken))).AsList();
    }

    public async Task SaveSafetyControlAsync(SaveIntelligenceSafetyControlRequest request,CancellationToken cancellationToken=default)
    {
        const string sql="""
            IF @RowVersion IS NOT NULL
            BEGIN
              UPDATE AI.SafetyControl SET DisplayName=@DisplayName,Description=@Description,ControlTypeCode=@ControlTypeCode,EnforcementStageCode=@EnforcementStageCode,ConfigurationJson=@ConfigurationJson,ViolationActionCode=@ViolationActionCode,RequiresHumanReview=@RequiresHumanReview,SortOrder=@SortOrder,IsActive=@IsActive,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND ControlCode=@ControlCode AND RowVersion=@RowVersion AND IsDeleted=0;
              IF @@ROWCOUNT=0 THROW 51000,'Safety control changed before this update.',1;
            END
            ELSE
            BEGIN
              INSERT AI.SafetyControl(TenantId,ControlCode,DisplayName,Description,ControlTypeCode,EnforcementStageCode,ConfigurationJson,ViolationActionCode,RequiresHumanReview,SortOrder,IsActive,CreatedDateUtc,CreatedByUserId,IsDeleted)
              SELECT @TenantId,@ControlCode,@DisplayName,@Description,@ControlTypeCode,@EnforcementStageCode,@ConfigurationJson,@ViolationActionCode,@RequiresHumanReview,@SortOrder,@IsActive,SYSUTCDATETIME(),@ActorUserId,0
              WHERE NOT EXISTS(SELECT 1 FROM AI.SafetyControl WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND ControlCode=@ControlCode AND IsDeleted=0);
              IF @@ROWCOUNT=0 THROW 51000,'Safety control already exists; reload it before updating.',1;
            END;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,request,cancellationToken:cancellationToken));
    }

    public async Task<PagedResult<IntelligenceFindingDto>> SearchFindingsAsync(SearchIntelligenceFindingsQuery query,CancellationToken cancellationToken=default)
    {
        const string from=""" FROM AI.IntelligenceFinding finding JOIN AI.IntelligenceCapability capability ON capability.IntelligenceCapabilityId=finding.IntelligenceCapabilityId WHERE finding.TenantId=@TenantId AND finding.IsDeleted=0 AND (@SearchTerm IS NULL OR finding.Title LIKE '%'+@SearchTerm+'%' OR finding.Summary LIKE '%'+@SearchTerm+'%' OR finding.Explanation LIKE '%'+@SearchTerm+'%') AND (@CapabilityCode IS NULL OR capability.CapabilityCode=@CapabilityCode) AND (@EntityTypeCode IS NULL OR finding.EntityTypeCode=@EntityTypeCode) AND (@EntityId IS NULL OR finding.EntityId=@EntityId) AND (@SeverityCode IS NULL OR finding.SeverityCode=@SeverityCode) AND (@StatusCode IS NULL OR finding.StatusCode=@StatusCode)""";
        var sql=$"""SELECT finding.IntelligenceFindingId,finding.TenantId,capability.CapabilityCode,capability.DisplayName CapabilityName,finding.EntityTypeCode,finding.EntityId,finding.FindingTypeCode,finding.SeverityCode,finding.StatusCode,finding.Title,finding.Summary,finding.Explanation,finding.Score,finding.Confidence,finding.RuleVersion,finding.DetectedDateUtc,finding.DueDateUtc,finding.ResolvedDateUtc,finding.ResolutionCode,finding.RowVersion {from} ORDER BY finding.DetectedDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY; SELECT COUNT(1) {from};""";
        var parameters=new{query.TenantId,SearchTerm=Null(query.SearchTerm),CapabilityCode=Null(query.CapabilityCode),EntityTypeCode=Null(query.EntityTypeCode),query.EntityId,SeverityCode=Null(query.SeverityCode),StatusCode=Null(query.StatusCode),Offset=(query.PageNumber-1)*query.PageSize,query.PageSize};
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,parameters,cancellationToken:cancellationToken));
        return new(){Items=(await multi.ReadAsync<IntelligenceFindingDto>()).AsList(),TotalCount=await multi.ReadSingleAsync<int>(),PageNumber=query.PageNumber,PageSize=query.PageSize};
    }

    public async Task<IntelligenceFindingDetailDto?> GetFindingAsync(Guid tenantId,Guid findingId,CancellationToken cancellationToken=default)
    {
        const string sql="""
            SELECT finding.IntelligenceFindingId,finding.TenantId,capability.CapabilityCode,capability.DisplayName CapabilityName,finding.EntityTypeCode,finding.EntityId,finding.FindingTypeCode,finding.SeverityCode,finding.StatusCode,finding.Title,finding.Summary,finding.Explanation,finding.Score,finding.Confidence,finding.RuleVersion,finding.DetectedDateUtc,finding.DueDateUtc,finding.ResolvedDateUtc,finding.ResolutionCode,finding.RowVersion FROM AI.IntelligenceFinding finding JOIN AI.IntelligenceCapability capability ON capability.IntelligenceCapabilityId=finding.IntelligenceCapabilityId WHERE finding.TenantId=@TenantId AND finding.IntelligenceFindingId=@FindingId AND finding.IsDeleted=0;
            SELECT evidence.FindingEvidenceId,evidence.EvidenceTypeCode,evidence.SourceModuleCode,evidence.SourceEntityTypeCode,evidence.SourceEntityId,evidence.SourceReference,evidence.Description,evidence.EvidenceValueJson,evidence.RelevanceScore FROM AI.FindingEvidence evidence JOIN AI.IntelligenceFinding finding ON finding.IntelligenceFindingId=evidence.IntelligenceFindingId AND finding.TenantId=@TenantId AND finding.IsDeleted=0 WHERE evidence.TenantId=@TenantId AND evidence.IntelligenceFindingId=@FindingId AND evidence.IsDeleted=0 ORDER BY evidence.RelevanceScore DESC,evidence.CreatedDateUtc;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,new{TenantId=tenantId,FindingId=findingId},cancellationToken:cancellationToken));
        var finding=await multi.ReadSingleOrDefaultAsync<IntelligenceFindingDto>();
        if(finding is null)return null;
        return new(finding,(await multi.ReadAsync<IntelligenceFindingEvidenceDto>()).AsList());
    }

    public async Task DecideFindingAsync(DecideIntelligenceFindingRequest request,CancellationToken cancellationToken=default)
    {
        const string sql="""UPDATE AI.IntelligenceFinding SET StatusCode=CASE WHEN @ResolutionCode=N'REOPEN' THEN N'OPEN' ELSE N'RESOLVED' END,ResolvedDateUtc=CASE WHEN @ResolutionCode=N'REOPEN' THEN NULL ELSE SYSUTCDATETIME() END,ResolvedByUserId=CASE WHEN @ResolutionCode=N'REOPEN' THEN NULL ELSE @ActorUserId END,ResolutionCode=@ResolutionCode,ResolutionNotes=@ResolutionNotes,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND IntelligenceFindingId=@IntelligenceFindingId AND RowVersion=@RowVersion AND IsDeleted=0; IF @@ROWCOUNT=0 THROW 51000,'Intelligence finding changed before this decision.',1;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,request,cancellationToken:cancellationToken));
    }

    public async Task<EntityRelationshipGraphDto> GetRelationshipGraphAsync(RelationshipQuery query,CancellationToken cancellationToken=default)
    {
        const string sql="""
            IF NOT EXISTS
            (
              SELECT 1 FROM AI.SearchDocument document
              JOIN AI.SearchPermission permission ON permission.SearchDocumentId=document.SearchDocumentId AND permission.TenantId=@TenantId AND permission.PermissionCode=N'READ' AND permission.IsDeleted=0
              WHERE document.TenantId=@TenantId AND document.EntityTypeCode=@EntityTypeCode AND document.EntityId=@EntityId AND document.IsDeleted=0
              AND ((permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId) OR (permission.PrincipalTypeCode=N'ROLE' AND EXISTS(SELECT 1 FROM IAM.UserRole userRole WHERE userRole.TenantId=@TenantId AND userRole.UserId=@UserId AND userRole.RoleId=permission.PrincipalId AND userRole.IsActive=1 AND userRole.IsDeleted=0)))
            ) THROW 51000,'The requested discovery root is not authorized.',1;
            ;WITH graph AS
            (
              SELECT relationship.EntityRelationshipId,relationship.SourceEntityTypeCode,relationship.SourceEntityId,relationship.RelationshipTypeCode,relationship.TargetEntityTypeCode,relationship.TargetEntityId,relationship.SourceModuleCode,relationship.SourceReference,relationship.Strength,relationship.EffectiveFromUtc,relationship.EffectiveToUtc,relationship.LastSynchronizedDateUtc,1 Depth
              FROM AI.EntityRelationship relationship
              WHERE relationship.TenantId=@TenantId AND relationship.IsDeleted=0 AND relationship.SourceEntityTypeCode=@EntityTypeCode AND relationship.SourceEntityId=@EntityId
              AND EXISTS(SELECT 1 FROM AI.SearchDocument document JOIN AI.SearchPermission permission ON permission.SearchDocumentId=document.SearchDocumentId AND permission.TenantId=@TenantId AND permission.PermissionCode=N'READ' AND permission.IsDeleted=0 WHERE document.TenantId=@TenantId AND document.EntityTypeCode=relationship.TargetEntityTypeCode AND document.EntityId=relationship.TargetEntityId AND document.IsDeleted=0 AND ((permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId) OR (permission.PrincipalTypeCode=N'ROLE' AND EXISTS(SELECT 1 FROM IAM.UserRole userRole WHERE userRole.TenantId=@TenantId AND userRole.UserId=@UserId AND userRole.RoleId=permission.PrincipalId AND userRole.IsActive=1 AND userRole.IsDeleted=0))))
              UNION ALL
              SELECT relationship.EntityRelationshipId,relationship.SourceEntityTypeCode,relationship.SourceEntityId,relationship.RelationshipTypeCode,relationship.TargetEntityTypeCode,relationship.TargetEntityId,relationship.SourceModuleCode,relationship.SourceReference,relationship.Strength,relationship.EffectiveFromUtc,relationship.EffectiveToUtc,relationship.LastSynchronizedDateUtc,graph.Depth+1
              FROM graph JOIN AI.EntityRelationship relationship ON relationship.TenantId=@TenantId AND relationship.SourceEntityTypeCode=graph.TargetEntityTypeCode AND relationship.SourceEntityId=graph.TargetEntityId AND relationship.IsDeleted=0
              WHERE graph.Depth<@MaximumDepth
              AND EXISTS(SELECT 1 FROM AI.SearchDocument document JOIN AI.SearchPermission permission ON permission.SearchDocumentId=document.SearchDocumentId AND permission.TenantId=@TenantId AND permission.PermissionCode=N'READ' AND permission.IsDeleted=0 WHERE document.TenantId=@TenantId AND document.EntityTypeCode=relationship.TargetEntityTypeCode AND document.EntityId=relationship.TargetEntityId AND document.IsDeleted=0 AND ((permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId) OR (permission.PrincipalTypeCode=N'ROLE' AND EXISTS(SELECT 1 FROM IAM.UserRole userRole WHERE userRole.TenantId=@TenantId AND userRole.UserId=@UserId AND userRole.RoleId=permission.PrincipalId AND userRole.IsActive=1 AND userRole.IsDeleted=0))))
            )
            SELECT DISTINCT EntityRelationshipId,SourceEntityTypeCode,SourceEntityId,RelationshipTypeCode,TargetEntityTypeCode,TargetEntityId,SourceModuleCode,SourceReference,Strength,EffectiveFromUtc,EffectiveToUtc,LastSynchronizedDateUtc FROM graph OPTION(MAXRECURSION 10);
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows=(await connection.QueryAsync<EntityRelationshipDto>(new CommandDefinition(sql,query,cancellationToken:cancellationToken))).AsList();
        return new(query.EntityTypeCode,query.EntityId,query.MaximumDepth,rows);
    }

    public async Task<IReadOnlyCollection<EntitySimilarityDto>> GetSimilarEntitiesAsync(SimilarityQuery query,CancellationToken cancellationToken=default)
    {
        const string sql="""IF NOT EXISTS(SELECT 1 FROM AI.SearchDocument document JOIN AI.SearchPermission permission ON permission.SearchDocumentId=document.SearchDocumentId AND permission.TenantId=@TenantId AND permission.PermissionCode=N'READ' AND permission.IsDeleted=0 WHERE document.TenantId=@TenantId AND document.EntityTypeCode=@EntityTypeCode AND document.EntityId=@EntityId AND document.IsDeleted=0 AND ((permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId) OR (permission.PrincipalTypeCode=N'ROLE' AND EXISTS(SELECT 1 FROM IAM.UserRole userRole WHERE userRole.TenantId=@TenantId AND userRole.UserId=@UserId AND userRole.RoleId=permission.PrincipalId AND userRole.IsActive=1 AND userRole.IsDeleted=0)))) THROW 51000,'The requested discovery root is not authorized.',1;DECLARE @ConfiguredMinimumScore decimal(5,4)=COALESCE(TRY_CONVERT(decimal(5,4),(SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Similarity.MinimumScore' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END)),.70);DECLARE @EffectiveMinimumScore decimal(5,4)=CASE WHEN @MinimumScore>@ConfiguredMinimumScore THEN @MinimumScore ELSE @ConfiguredMinimumScore END;SELECT TOP(@MaximumResults) similarity.EntitySimilarityId,similarity.EntityTypeCode,similarity.SourceEntityId,similarity.SimilarEntityId,similarity.SimilarityModelCode,similarity.SimilarityModelVersion,similarity.SimilarityScore,similarity.FeatureEvidenceJson,similarity.CalculatedDateUtc,similarity.ExpiresDateUtc FROM AI.EntitySimilarity similarity WHERE similarity.TenantId=@TenantId AND similarity.EntityTypeCode=@EntityTypeCode AND similarity.SourceEntityId=@EntityId AND similarity.SimilarityScore>=@EffectiveMinimumScore AND (similarity.ExpiresDateUtc IS NULL OR similarity.ExpiresDateUtc>SYSUTCDATETIME()) AND similarity.IsDeleted=0 AND EXISTS(SELECT 1 FROM AI.SearchDocument document JOIN AI.SearchPermission permission ON permission.SearchDocumentId=document.SearchDocumentId AND permission.TenantId=@TenantId AND permission.PermissionCode=N'READ' AND permission.IsDeleted=0 WHERE document.TenantId=@TenantId AND document.EntityTypeCode=similarity.EntityTypeCode AND document.EntityId=similarity.SimilarEntityId AND document.IsDeleted=0 AND ((permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId) OR (permission.PrincipalTypeCode=N'ROLE' AND EXISTS(SELECT 1 FROM IAM.UserRole userRole WHERE userRole.TenantId=@TenantId AND userRole.UserId=@UserId AND userRole.RoleId=permission.PrincipalId AND userRole.IsActive=1 AND userRole.IsDeleted=0)))) ORDER BY similarity.SimilarityScore DESC,similarity.CalculatedDateUtc DESC;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<EntitySimilarityDto>(new CommandDefinition(sql,query,cancellationToken:cancellationToken))).AsList();
    }

    public async Task<PagedResult<BusinessIntelligenceSignalDto>> SearchBusinessSignalsAsync(SearchBusinessIntelligenceSignalsQuery query,CancellationToken cancellationToken=default)
    {
        const string from=""" FROM AI.BusinessSignal signal JOIN AI.IntelligenceCapability capability ON capability.IntelligenceCapabilityId=signal.IntelligenceCapabilityId WHERE signal.TenantId=@TenantId AND signal.IsDeleted=0 AND (@SearchTerm IS NULL OR signal.Title LIKE '%'+@SearchTerm+'%' OR signal.Summary LIKE '%'+@SearchTerm+'%') AND (@CapabilityCode IS NULL OR capability.CapabilityCode=@CapabilityCode) AND (@EntityTypeCode IS NULL OR signal.EntityTypeCode=@EntityTypeCode) AND (@EntityId IS NULL OR signal.EntityId=@EntityId) AND (@SeverityCode IS NULL OR signal.SeverityCode=@SeverityCode) AND (@StatusCode IS NULL OR signal.StatusCode=@StatusCode) AND (@AssignedToUserId IS NULL OR signal.AssignedToUserId=@AssignedToUserId)""";
        var sql=$"""SELECT signal.BusinessSignalId,signal.TenantId,capability.CapabilityCode,capability.DisplayName CapabilityName,signal.EntityTypeCode,signal.EntityId,signal.SignalTypeCode,signal.SignalDateUtc,signal.SeverityCode,signal.Score,signal.Confidence,signal.Title,signal.Summary,signal.EvidenceJson,signal.RecommendedActionCode,signal.StatusCode,signal.AssignedToUserId,signal.DueDateUtc,signal.RowVersion {from} ORDER BY signal.SignalDateUtc DESC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY; SELECT COUNT(1) {from};""";
        var parameters=new{query.TenantId,SearchTerm=Null(query.SearchTerm),CapabilityCode=Null(query.CapabilityCode),EntityTypeCode=Null(query.EntityTypeCode),query.EntityId,SeverityCode=Null(query.SeverityCode),StatusCode=Null(query.StatusCode),query.AssignedToUserId,Offset=(query.PageNumber-1)*query.PageSize,query.PageSize};
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,parameters,cancellationToken:cancellationToken));
        return new(){Items=(await multi.ReadAsync<BusinessIntelligenceSignalDto>()).AsList(),TotalCount=await multi.ReadSingleAsync<int>(),PageNumber=query.PageNumber,PageSize=query.PageSize};
    }

    public async Task DecideBusinessSignalAsync(DecideBusinessIntelligenceSignalRequest request,CancellationToken cancellationToken=default)
    {
        const string sql="""UPDATE AI.BusinessSignal SET StatusCode=CASE WHEN @DecisionCode=N'REOPEN' THEN N'OPEN' ELSE @DecisionCode END,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@ActorUserId WHERE TenantId=@TenantId AND BusinessSignalId=@BusinessSignalId AND RowVersion=@RowVersion AND IsDeleted=0; IF @@ROWCOUNT=0 THROW 51000,'Business signal changed before this decision.',1;""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,request,cancellationToken:cancellationToken));
    }

    public async Task<InsuranceReasoningResponse> ExecuteReasoningAsync(InsuranceReasoningRequest request,IReadOnlyCollection<SemanticConceptMatchDto> concepts,CancellationToken cancellationToken=default)
    {
        var permissionHash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('|',request.GrantedPermissions.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)))));
        var intent=ClassifyReasoningIntent(request.Question);
        const string sql="""
            IF EXISTS(SELECT 1 FROM AI.ReasoningSession WHERE TenantId=@TenantId AND CorrelationId=@CorrelationId AND IsDeleted=0)
            BEGIN
              SELECT ReasoningSessionId FROM AI.ReasoningSession WHERE TenantId=@TenantId AND CorrelationId=@CorrelationId AND RequestedByUserId=@UserId AND IsDeleted=0;
              RETURN;
            END;
            IF NOT EXISTS(SELECT 1 FROM AI.SearchDocument document JOIN AI.SearchPermission permission ON permission.SearchDocumentId=document.SearchDocumentId AND permission.TenantId=@TenantId AND permission.PermissionCode=N'READ' AND permission.IsDeleted=0 WHERE document.TenantId=@TenantId AND document.EntityTypeCode=@EntityTypeCode AND document.EntityId=@EntityId AND document.IsDeleted=0 AND ((permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId) OR (permission.PrincipalTypeCode=N'ROLE' AND EXISTS(SELECT 1 FROM IAM.UserRole userRole WHERE userRole.TenantId=@TenantId AND userRole.UserId=@UserId AND userRole.RoleId=permission.PrincipalId AND userRole.IsActive=1 AND userRole.IsDeleted=0)))) THROW 51000,'The requested entity was not found or is not authorized.',1;
            DECLARE @SessionId UNIQUEIDENTIFIER=NEWID();
            DECLARE @PolicyId UNIQUEIDENTIFIER=(SELECT TOP(1) policy.EnginePolicyId FROM AI.EnginePolicy policy JOIN AI.IntelligenceCapability capability ON capability.IntelligenceCapabilityId=policy.IntelligenceCapabilityId WHERE capability.CapabilityCode=N'INSURANCE_REASONING' AND capability.IsDeleted=0 AND policy.IsActive=1 AND policy.IsDeleted=0 AND (policy.TenantId=@TenantId OR policy.TenantId IS NULL) AND policy.EffectiveFromUtc<=SYSUTCDATETIME() AND (policy.EffectiveToUtc IS NULL OR policy.EffectiveToUtc>SYSUTCDATETIME()) ORDER BY CASE WHEN policy.TenantId=@TenantId THEN 0 ELSE 1 END,policy.VersionNumber DESC);
            IF @PolicyId IS NULL THROW 51000,'No active insurance reasoning policy is configured.',1;
            INSERT AI.ReasoningSession(ReasoningSessionId,TenantId,RequestedByUserId,EntityTypeCode,EntityId,Question,IntentCode,StatusCode,CorrelationId,EnginePolicyId,PermissionSnapshotHash,StartedDateUtc,RequiresHumanReview,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(@SessionId,@TenantId,@UserId,@EntityTypeCode,@EntityId,@Question,@IntentCode,N'PROCESSING',@CorrelationId,@PolicyId,@PermissionHash,SYSUTCDATETIME(),0,SYSUTCDATETIME(),@UserId,0);
            INSERT AI.ReasoningEvidence(ReasoningEvidenceId,TenantId,ReasoningSessionId,EvidenceTypeCode,SourceModuleCode,SourceEntityTypeCode,SourceEntityId,SourceReference,Title,Summary,EvidenceValueJson,RelevanceScore,IsAuthoritative,CreatedDateUtc,CreatedByUserId,IsDeleted)
            SELECT NEWID(),@TenantId,@SessionId,N'ENTITY_RECORD',document.ModuleCode,document.EntityTypeCode,document.EntityId,N'intelligence-search:'+CONVERT(nvarchar(36),document.SearchDocumentId),document.Title,LEFT(document.ContentText,2000),NULL,1.0000,1,SYSUTCDATETIME(),@UserId,0
            FROM AI.SearchDocument document WHERE document.TenantId=@TenantId AND document.EntityTypeCode=@EntityTypeCode AND document.EntityId=@EntityId AND document.IsDeleted=0;
            INSERT AI.ReasoningEvidence(ReasoningEvidenceId,TenantId,ReasoningSessionId,EvidenceTypeCode,SourceModuleCode,SourceEntityTypeCode,SourceEntityId,SourceReference,Title,Summary,EvidenceValueJson,RelevanceScore,IsAuthoritative,CreatedDateUtc,CreatedByUserId,IsDeleted)
            SELECT NEWID(),@TenantId,@SessionId,N'FINDING',capability.OwningModuleCode,finding.EntityTypeCode,finding.EntityId,N'intelligence-finding:'+CONVERT(nvarchar(36),finding.IntelligenceFindingId),finding.Title,finding.Summary,NULL,COALESCE(finding.Confidence,0.7500),1,SYSUTCDATETIME(),@UserId,0 FROM AI.IntelligenceFinding finding JOIN AI.IntelligenceCapability capability ON capability.IntelligenceCapabilityId=finding.IntelligenceCapabilityId WHERE finding.TenantId=@TenantId AND finding.EntityTypeCode=@EntityTypeCode AND finding.EntityId=@EntityId AND finding.StatusCode IN(N'OPEN',N'REVIEW_REQUIRED',N'IN_REVIEW') AND finding.IsDeleted=0;
            INSERT AI.ReasoningEvidence(ReasoningEvidenceId,TenantId,ReasoningSessionId,EvidenceTypeCode,SourceModuleCode,SourceEntityTypeCode,SourceEntityId,SourceReference,Title,Summary,EvidenceValueJson,RelevanceScore,IsAuthoritative,CreatedDateUtc,CreatedByUserId,IsDeleted)
            SELECT NEWID(),@TenantId,@SessionId,N'BIND_VALIDATION',N'Submissions',N'BIND_TRANSACTION',bind.PolicyBindTransactionId,N'Submissions.BindValidationResult:'+CONVERT(nvarchar(36),validation.BindValidationResultId),validation.RequirementName,COALESCE(validation.Message,N'Bind requirement has not passed.'),CONCAT(N'{"requirementCode":"',STRING_ESCAPE(validation.RequirementCode,'json'),N'","status":"',STRING_ESCAPE(validation.StatusCode,'json'),N'","blocking":',CASE WHEN validation.IsBlocking=1 THEN N'true' ELSE N'false' END,N',"evidenceDocumentId":',CASE WHEN validation.EvidenceDocumentId IS NULL THEN N'null' ELSE N'"'+CONVERT(nvarchar(36),validation.EvidenceDocumentId)+N'"' END,N'}'),1.0000,1,SYSUTCDATETIME(),@UserId,0
            FROM Submissions.PolicyBindTransaction bind JOIN Submissions.BindValidationResult validation ON validation.TenantId=bind.TenantId AND validation.PolicyBindTransactionId=bind.PolicyBindTransactionId AND validation.IsDeleted=0
            WHERE bind.TenantId=@TenantId AND bind.IsDeleted=0 AND ((@EntityTypeCode=N'SUBMISSION' AND bind.SubmissionId=@EntityId) OR (@EntityTypeCode=N'BIND_TRANSACTION' AND bind.PolicyBindTransactionId=@EntityId)) AND validation.StatusCode IN(N'Failed',N'Pending');
            INSERT AI.ReasoningEvidence(ReasoningEvidenceId,TenantId,ReasoningSessionId,EvidenceTypeCode,SourceModuleCode,SourceEntityTypeCode,SourceEntityId,SourceReference,Title,Summary,EvidenceValueJson,RelevanceScore,IsAuthoritative,CreatedDateUtc,CreatedByUserId,IsDeleted)
            SELECT NEWID(),@TenantId,@SessionId,N'CARRIER_RULE',N'Agency',N'CARRIER_PRODUCT_RULE',carrierRule.CarrierProductRuleId,N'Agency.CarrierProductRule:'+CONVERT(nvarchar(36),carrierRule.CarrierProductRuleId),carrierRule.RuleName,COALESCE(carrierRule.RuleDescription,N'Active carrier product rule applies to this bind transaction.'),CONCAT(N'{"ruleCode":"',STRING_ESCAPE(carrierRule.RuleCode,'json'),N'","carrier":"',STRING_ESCAPE(COALESCE(carrierRule.CarrierName,N''),'json'),N'","state":"',STRING_ESCAPE(COALESCE(carrierRule.StateCode,N''),'json'),N'","requireSignedApplication":',CASE WHEN carrierRule.RequireSignedApplication=1 THEN N'true' ELSE N'false' END,N',"requireLossRuns":',CASE WHEN carrierRule.RequireLossRuns=1 THEN N'true' ELSE N'false' END,N',"requireUnderwriterApproval":',CASE WHEN carrierRule.RequireUnderwriterApproval=1 THEN N'true' ELSE N'false' END,N'}'),.9500,1,SYSUTCDATETIME(),@UserId,0
            FROM Submissions.PolicyBindTransaction bind JOIN Submissions.Submission submission ON submission.TenantId=bind.TenantId AND submission.SubmissionId=bind.SubmissionId AND submission.IsDeleted=0 JOIN Agency.CarrierProductRule carrierRule ON carrierRule.TenantId=bind.TenantId AND carrierRule.IsDeleted=0 AND carrierRule.IsActive=1 AND (carrierRule.CarrierId IS NULL OR carrierRule.CarrierId=bind.CarrierId) AND (carrierRule.LineOfBusinessCode IS NULL OR carrierRule.LineOfBusinessCode=submission.LineOfBusiness) AND carrierRule.EffectiveDate<=CONVERT(date,SYSUTCDATETIME()) AND (carrierRule.ExpirationDate IS NULL OR carrierRule.ExpirationDate>=CONVERT(date,SYSUTCDATETIME()))
            WHERE bind.TenantId=@TenantId AND bind.IsDeleted=0 AND ((@EntityTypeCode=N'SUBMISSION' AND bind.SubmissionId=@EntityId) OR (@EntityTypeCode=N'BIND_TRANSACTION' AND bind.PolicyBindTransactionId=@EntityId));
            INSERT AI.ReasoningEvidence(ReasoningEvidenceId,TenantId,ReasoningSessionId,EvidenceTypeCode,SourceModuleCode,SourceEntityTypeCode,SourceEntityId,SourceReference,Title,Summary,EvidenceValueJson,RelevanceScore,IsAuthoritative,CreatedDateUtc,CreatedByUserId,IsDeleted)
            SELECT NEWID(),@TenantId,@SessionId,N'APPROVAL',N'Submissions',N'BIND_APPROVAL',approval.BindApprovalId,N'Submissions.BindApproval:'+CONVERT(nvarchar(36),approval.BindApprovalId),N'Bind approval '+approval.StatusCode,CONCAT(N'Approval reason ',approval.ApprovalReasonCode,N'; assigned approver ',COALESCE(CONVERT(nvarchar(36),approval.AssignedApproverUserId),N'not assigned'),N'.'),CONCAT(N'{"status":"',STRING_ESCAPE(approval.StatusCode,'json'),N'","approvalReasonCode":"',STRING_ESCAPE(approval.ApprovalReasonCode,'json'),N'","assignedApproverUserId":',CASE WHEN approval.AssignedApproverUserId IS NULL THEN N'null' ELSE N'"'+CONVERT(nvarchar(36),approval.AssignedApproverUserId)+N'"' END,N'}'),1.0000,1,SYSUTCDATETIME(),@UserId,0 FROM Submissions.PolicyBindTransaction bind JOIN Submissions.BindApproval approval ON approval.TenantId=bind.TenantId AND approval.PolicyBindTransactionId=bind.PolicyBindTransactionId AND approval.IsDeleted=0 WHERE bind.TenantId=@TenantId AND bind.IsDeleted=0 AND ((@EntityTypeCode=N'SUBMISSION' AND bind.SubmissionId=@EntityId) OR (@EntityTypeCode=N'BIND_TRANSACTION' AND bind.PolicyBindTransactionId=@EntityId));
            INSERT AI.ReasoningEvidence(ReasoningEvidenceId,TenantId,ReasoningSessionId,EvidenceTypeCode,SourceModuleCode,SourceEntityTypeCode,SourceEntityId,SourceReference,Title,Summary,EvidenceValueJson,RelevanceScore,IsAuthoritative,CreatedDateUtc,CreatedByUserId,IsDeleted)
            SELECT TOP(10) NEWID(),@TenantId,@SessionId,N'SIMILAR_HISTORY',N'AI',N'SUBMISSION',similarity.SimilarEntityId,N'AI.EntitySimilarity:'+CONVERT(nvarchar(36),similarity.EntitySimilarityId),N'Similar historical submission',CONCAT(N'Similarity ',FORMAT(similarity.SimilarityScore,N'P0'),N' using ',similarity.SimilarityModelCode,N' version ',similarity.SimilarityModelVersion,N'.'),similarity.FeatureEvidenceJson,similarity.SimilarityScore,0,SYSUTCDATETIME(),@UserId,0 FROM Submissions.PolicyBindTransaction bind JOIN AI.EntitySimilarity similarity ON similarity.TenantId=bind.TenantId AND similarity.EntityTypeCode=N'SUBMISSION' AND similarity.SourceEntityId=bind.SubmissionId AND similarity.IsDeleted=0 AND (similarity.ExpiresDateUtc IS NULL OR similarity.ExpiresDateUtc>SYSUTCDATETIME()) WHERE bind.TenantId=@TenantId AND bind.IsDeleted=0 AND ((@EntityTypeCode=N'SUBMISSION' AND bind.SubmissionId=@EntityId) OR (@EntityTypeCode=N'BIND_TRANSACTION' AND bind.PolicyBindTransactionId=@EntityId)) AND EXISTS(SELECT 1 FROM AI.SearchDocument document JOIN AI.SearchPermission permission ON permission.SearchDocumentId=document.SearchDocumentId AND permission.TenantId=@TenantId AND permission.PermissionCode=N'READ' AND permission.IsDeleted=0 WHERE document.TenantId=@TenantId AND document.EntityTypeCode=N'SUBMISSION' AND document.EntityId=similarity.SimilarEntityId AND document.IsDeleted=0 AND ((permission.PrincipalTypeCode=N'USER' AND permission.PrincipalId=@UserId) OR (permission.PrincipalTypeCode=N'ROLE' AND EXISTS(SELECT 1 FROM IAM.UserRole userRole WHERE userRole.TenantId=@TenantId AND userRole.UserId=@UserId AND userRole.RoleId=permission.PrincipalId AND userRole.IsActive=1 AND userRole.IsDeleted=0)))) ORDER BY similarity.SimilarityScore DESC;
            INSERT AI.ReasoningConclusion(ReasoningConclusionId,TenantId,ReasoningSessionId,ConclusionCode,SequenceNumber,Title,Explanation,RuleCode,RuleVersion,Confidence,IsBlocking,CanBeWaived,WaiverPermissionCode,CreatedDateUtc,CreatedByUserId,IsDeleted)
            SELECT NEWID(),@TenantId,@SessionId,finding.FindingTypeCode,ROW_NUMBER() OVER(ORDER BY CASE finding.SeverityCode WHEN N'CRITICAL' THEN 1 WHEN N'HIGH' THEN 2 WHEN N'MEDIUM' THEN 3 ELSE 4 END,finding.DetectedDateUtc),finding.Title,finding.Explanation,policy.PolicyCode,finding.RuleVersion,COALESCE(finding.Confidence,0.7500),CASE WHEN finding.SeverityCode IN(N'CRITICAL',N'HIGH') THEN 1 ELSE 0 END,0,NULL,SYSUTCDATETIME(),@UserId,0 FROM AI.IntelligenceFinding finding LEFT JOIN AI.EnginePolicy policy ON policy.EnginePolicyId=finding.EnginePolicyId WHERE finding.TenantId=@TenantId AND finding.EntityTypeCode=@EntityTypeCode AND finding.EntityId=@EntityId AND finding.StatusCode IN(N'OPEN',N'REVIEW_REQUIRED',N'IN_REVIEW') AND finding.IsDeleted=0;
            DECLARE @ConclusionOffset INT=(SELECT COUNT(1) FROM AI.ReasoningConclusion WHERE TenantId=@TenantId AND ReasoningSessionId=@SessionId AND IsDeleted=0);
            INSERT AI.ReasoningConclusion(ReasoningConclusionId,TenantId,ReasoningSessionId,ConclusionCode,SequenceNumber,Title,Explanation,RuleCode,RuleVersion,Confidence,IsBlocking,CanBeWaived,WaiverPermissionCode,CreatedDateUtc,CreatedByUserId,IsDeleted)
            SELECT NEWID(),@TenantId,@SessionId,N'BIND_REQUIREMENT',@ConclusionOffset+ROW_NUMBER() OVER(ORDER BY validation.IsBlocking DESC,validation.RequirementName),validation.RequirementName,CONCAT(COALESCE(validation.Message,N'The requirement has not passed.'),N' Required by ',COALESCE(requirement.Description,validation.RequirementCode),N'.',CASE WHEN requirement.ApprovalPermissionCode IS NULL THEN N'' ELSE N' Approval requires permission '+requirement.ApprovalPermissionCode+N'.' END),validation.RequirementCode,NULL,1.0000,validation.IsBlocking,COALESCE(requirement.CanBeWaived,0),requirement.WaiverPermissionCode,SYSUTCDATETIME(),@UserId,0 FROM Submissions.PolicyBindTransaction bind JOIN Submissions.BindValidationResult validation ON validation.TenantId=bind.TenantId AND validation.PolicyBindTransactionId=bind.PolicyBindTransactionId AND validation.IsDeleted=0 LEFT JOIN Submissions.BindRequirement requirement ON requirement.TenantId=validation.TenantId AND requirement.BindRequirementId=validation.BindRequirementId AND requirement.IsDeleted=0 WHERE bind.TenantId=@TenantId AND bind.IsDeleted=0 AND ((@EntityTypeCode=N'SUBMISSION' AND bind.SubmissionId=@EntityId) OR (@EntityTypeCode=N'BIND_TRANSACTION' AND bind.PolicyBindTransactionId=@EntityId)) AND validation.StatusCode IN(N'Failed',N'Pending');
            IF NOT EXISTS(SELECT 1 FROM AI.ReasoningConclusion WHERE TenantId=@TenantId AND ReasoningSessionId=@SessionId AND IsDeleted=0)
              INSERT AI.ReasoningConclusion(ReasoningConclusionId,TenantId,ReasoningSessionId,ConclusionCode,SequenceNumber,Title,Explanation,Confidence,IsBlocking,CanBeWaived,CreatedDateUtc,CreatedByUserId,IsDeleted) VALUES(NEWID(),@TenantId,@SessionId,N'NO_ACTIVE_FINDINGS',1,N'No active governed findings',N'No active risk, compliance, or workflow findings are currently recorded for this authorized entity. This does not replace required operational validation.',1.0000,0,0,SYSUTCDATETIME(),@UserId,0);
            INSERT AI.ReasoningAction(ReasoningActionId,TenantId,ReasoningSessionId,SequenceNumber,ActionCode,DisplayName,Description,TargetRoute,RequiredPermissionCode,RequiresConfirmation,IsAvailable,UnavailableReason,CreatedDateUtc,CreatedByUserId,IsDeleted)
            SELECT NEWID(),@TenantId,@SessionId,1,N'REVIEW_ENTITY',N'Review source record',N'Open the authoritative entity record and review its current workflow and supporting evidence.',NULL,N'Intelligence.Read',0,1,NULL,SYSUTCDATETIME(),@UserId,0;
            INSERT AI.ReasoningAction(ReasoningActionId,TenantId,ReasoningSessionId,SequenceNumber,ActionCode,DisplayName,Description,TargetRoute,RequiredPermissionCode,RequiresConfirmation,IsAvailable,UnavailableReason,CreatedDateUtc,CreatedByUserId,IsDeleted)
            SELECT NEWID(),@TenantId,@SessionId,2,N'RESOLVE_BIND_REQUIREMENT',N'Resolve missing bind requirement',N'Upload or link the required evidence and rerun authoritative bind validation.',N'/submissions/'+CONVERT(nvarchar(36),bind.SubmissionId),N'SUBMISSION_EDIT',0,CASE WHEN EXISTS(SELECT 1 FROM OPENJSON(@GrantedPermissionsJson) WITH(PermissionCode nvarchar(150) '$') WHERE PermissionCode IN(N'SUBMISSION_EDIT',N'NAV_ALL')) THEN 1 ELSE 0 END,CASE WHEN EXISTS(SELECT 1 FROM OPENJSON(@GrantedPermissionsJson) WITH(PermissionCode nvarchar(150) '$') WHERE PermissionCode IN(N'SUBMISSION_EDIT',N'NAV_ALL')) THEN NULL ELSE N'The current user lacks submission edit permission.' END,SYSUTCDATETIME(),@UserId,0 FROM Submissions.PolicyBindTransaction bind WHERE bind.TenantId=@TenantId AND bind.IsDeleted=0 AND ((@EntityTypeCode=N'SUBMISSION' AND bind.SubmissionId=@EntityId) OR (@EntityTypeCode=N'BIND_TRANSACTION' AND bind.PolicyBindTransactionId=@EntityId)) AND EXISTS(SELECT 1 FROM Submissions.BindValidationResult validation WHERE validation.TenantId=bind.TenantId AND validation.PolicyBindTransactionId=bind.PolicyBindTransactionId AND validation.StatusCode IN(N'Failed',N'Pending') AND validation.IsDeleted=0);
            DECLARE @Confidence DECIMAL(5,4)=(SELECT MIN(Confidence) FROM AI.ReasoningConclusion WHERE TenantId=@TenantId AND ReasoningSessionId=@SessionId AND IsDeleted=0);
            DECLARE @RequiresReview BIT=CASE WHEN @Confidence<(SELECT TOP(1) MinimumConfidence FROM AI.EnginePolicy WHERE EnginePolicyId=@PolicyId) OR EXISTS(SELECT 1 FROM AI.ReasoningConclusion WHERE TenantId=@TenantId AND ReasoningSessionId=@SessionId AND IsBlocking=1 AND IsDeleted=0) THEN 1 ELSE 0 END;
            UPDATE AI.ReasoningSession SET StatusCode=N'COMPLETED',CompletedDateUtc=SYSUTCDATETIME(),Confidence=@Confidence,RequiresHumanReview=@RequiresReview,ModifiedDateUtc=SYSUTCDATETIME(),ModifiedByUserId=@UserId WHERE ReasoningSessionId=@SessionId AND TenantId=@TenantId;
            SELECT @SessionId;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var sessionId=await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(sql,new{request.TenantId,request.UserId,request.EntityTypeCode,request.EntityId,request.Question,request.CorrelationId,IntentCode=intent,PermissionHash=permissionHash,GrantedPermissionsJson=JsonSerializer.Serialize(request.GrantedPermissions)},cancellationToken:cancellationToken));
        if(concepts.Count>0)
        {
            const string conceptSql="""INSERT AI.ReasoningEvidence(ReasoningEvidenceId,TenantId,ReasoningSessionId,EvidenceTypeCode,SourceModuleCode,SourceEntityTypeCode,SourceEntityId,SourceReference,Title,Summary,EvidenceValueJson,RelevanceScore,IsAuthoritative,CreatedDateUtc,CreatedByUserId,IsDeleted) SELECT NEWID(),@TenantId,@ReasoningSessionId,N'KNOWLEDGE_CONCEPT',N'Knowledge',N'KNOWLEDGE_CONCEPT',@ConceptId,N'knowledge-concept:'+CONVERT(nvarchar(36),@ConceptId),@PreferredLabel,N'Approved canonical concept matched through the Knowledge semantic contract.',@EvidenceValueJson,@Score,1,SYSUTCDATETIME(),@UserId,0 WHERE NOT EXISTS(SELECT 1 FROM AI.ReasoningEvidence WHERE TenantId=@TenantId AND ReasoningSessionId=@ReasoningSessionId AND SourceEntityId=@ConceptId AND EvidenceTypeCode=N'KNOWLEDGE_CONCEPT' AND IsDeleted=0);""";
            var rows=concepts.Select(concept=>new{request.TenantId,ReasoningSessionId=sessionId,request.UserId,concept.ConceptId,concept.PreferredLabel,concept.Score,EvidenceValueJson=JsonSerializer.Serialize(new{concept.ConceptCode,concept.VersionNumber,concept.MatchReasonCode})});
            await connection.ExecuteAsync(new CommandDefinition(conceptSql,rows,cancellationToken:cancellationToken));
        }
        return (await GetReasoningSessionAsync(request.TenantId,request.UserId,sessionId,cancellationToken))!;
    }

    public async Task<InsuranceReasoningResponse?> GetReasoningSessionAsync(Guid tenantId,Guid userId,Guid reasoningSessionId,CancellationToken cancellationToken=default)
    {
        const string sql="""
            SELECT session.ReasoningSessionId,session.EntityTypeCode,session.EntityId,session.Question,session.IntentCode,session.StatusCode,session.CorrelationId,session.Confidence,session.RequiresHumanReview,session.StartedDateUtc,session.CompletedDateUtc FROM AI.ReasoningSession session WHERE session.TenantId=@TenantId AND session.RequestedByUserId=@UserId AND session.ReasoningSessionId=@ReasoningSessionId AND session.IsDeleted=0;
            SELECT ReasoningEvidenceId,EvidenceTypeCode,SourceModuleCode,SourceEntityTypeCode,SourceEntityId,SourceReference,Title,Summary,EvidenceValueJson,RelevanceScore,IsAuthoritative FROM AI.ReasoningEvidence WHERE TenantId=@TenantId AND ReasoningSessionId=@ReasoningSessionId AND IsDeleted=0 ORDER BY RelevanceScore DESC,CreatedDateUtc;
            SELECT ReasoningConclusionId,ConclusionCode,SequenceNumber,Title,Explanation,RuleCode,RuleVersion,Confidence,IsBlocking,CanBeWaived,WaiverPermissionCode FROM AI.ReasoningConclusion WHERE TenantId=@TenantId AND ReasoningSessionId=@ReasoningSessionId AND IsDeleted=0 ORDER BY SequenceNumber;
            SELECT ReasoningActionId,SequenceNumber,ActionCode,DisplayName,Description,TargetRoute,RequiredPermissionCode,RequiresConfirmation,IsAvailable,UnavailableReason FROM AI.ReasoningAction WHERE TenantId=@TenantId AND ReasoningSessionId=@ReasoningSessionId AND IsDeleted=0 ORDER BY SequenceNumber;
            """;
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,new{TenantId=tenantId,UserId=userId,ReasoningSessionId=reasoningSessionId},cancellationToken:cancellationToken));
        var session=await multi.ReadSingleOrDefaultAsync<ReasoningSessionRow>();
        if(session is null)return null;
        var evidence=(await multi.ReadAsync<InsuranceReasoningEvidenceDto>()).AsList();
        var conclusions=(await multi.ReadAsync<InsuranceReasoningConclusionDto>()).AsList();
        var actions=(await multi.ReadAsync<InsuranceReasoningActionDto>()).AsList();
        return new(session.ReasoningSessionId,session.EntityTypeCode,session.EntityId,session.Question,session.IntentCode,session.StatusCode,session.CorrelationId,session.Confidence,session.RequiresHumanReview,session.StartedDateUtc,session.CompletedDateUtc,evidence,conclusions,actions);
    }

    private static string ClassifyReasoningIntent(string question)
    {
        var value=question.ToUpperInvariant();
        if(value.Contains("BIND",StringComparison.Ordinal))return "BIND_ELIGIBILITY";
        if(value.Contains("COMPLI",StringComparison.Ordinal)||value.Contains("LICENSE",StringComparison.Ordinal))return "COMPLIANCE";
        if(value.Contains("RISK",StringComparison.Ordinal)||value.Contains("LOSS",StringComparison.Ordinal))return "RISK_REVIEW";
        if(value.Contains("DOCUMENT",StringComparison.Ordinal)||value.Contains("MISSING",StringComparison.Ordinal))return "REQUIREMENT_REVIEW";
        if(value.Contains("RENEW",StringComparison.Ordinal))return "RENEWAL_REVIEW";
        if(value.Contains("CLAIM",StringComparison.Ordinal))return "CLAIM_REVIEW";
        return "GENERAL_GUIDANCE";
    }

    private sealed record PillarRow(Guid IntelligencePillarId,string PillarCode,string DisplayName,string Description,int SortOrder,bool IsActive);
    private sealed record PlatformTotals(int OpenFindingCount,int OpenBusinessSignalCount,int ActiveReasoningSessionCount,int PendingWorkItemCount);
    private sealed record BusinessModuleRow(Guid BusinessModuleId,string ModuleCode,string DisplayName,string Description,string? OwningSchemaCode,string? NavigationRoute,bool IsActive,int SortOrder);
    private sealed record ModuleServiceDependencyRow(Guid ModuleServiceDependencyId,Guid BusinessModuleId,Guid PlatformServiceId,string ServiceCode,string ServiceName,string UsageCode,string Description,string AdoptionStatusCode,string? ConsumerReference,DateTime? LastVerifiedDateUtc,bool IsRequired,bool IsActive);
    private sealed record ReasoningSessionRow(Guid ReasoningSessionId,string EntityTypeCode,Guid EntityId,string Question,string IntentCode,string StatusCode,string CorrelationId,decimal? Confidence,bool RequiresHumanReview,DateTime StartedDateUtc,DateTime? CompletedDateUtc);
}
