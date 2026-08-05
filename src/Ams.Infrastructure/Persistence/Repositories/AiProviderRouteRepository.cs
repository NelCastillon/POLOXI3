using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Abstractions.Persistence;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AiProviderRouteRepository(ISqlConnectionFactory connectionFactory):IAiProviderRouteRepository
{
    public async Task<IReadOnlyCollection<AiProviderRoute>> GetRoutesAsync(Guid tenantId,string featureCode,string capabilityCode,CancellationToken cancellationToken=default)
    {
        const string sql="""
WITH routes AS
(
    SELECT policy.TenantId,policy.FeatureCode,provider.ProviderCode,provider.ProviderTypeCode,model.ModelCode,model.DeploymentName,
           endpointSetting.SettingValue EndpointReference,credentialSetting.SettingValue CredentialReference,apiVersion.SettingValue ApiVersion,
           policy.TimeoutSeconds,policy.Temperature,policy.MaximumOutputTokens,0 Priority,CONVERT(bit,0) IsFallback
    FROM AI.FeaturePolicy policy
    JOIN AI.ModelDeployment model ON model.ModelDeploymentId=policy.PrimaryModelDeploymentId AND model.IsActive=1 AND model.IsDeleted=0
    JOIN AI.Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0
    OUTER APPLY(SELECT TOP(1) COALESCE(tenant.SettingValue,platform.SettingValue,platform.DefaultValue) SettingValue FROM Core.ConfigurationSetting platform LEFT JOIN Core.ConfigurationSetting tenant ON tenant.TenantId=policy.TenantId AND tenant.SettingKey=platform.SettingKey AND tenant.IsDeleted=0 WHERE platform.TenantId IS NULL AND platform.SettingKey=provider.EndpointConfigurationKey AND platform.IsDeleted=0) endpointSetting
    OUTER APPLY(SELECT TOP(1) COALESCE(tenant.SettingValue,platform.SettingValue,platform.DefaultValue) SettingValue FROM Core.ConfigurationSetting platform LEFT JOIN Core.ConfigurationSetting tenant ON tenant.TenantId=policy.TenantId AND tenant.SettingKey=platform.SettingKey AND tenant.IsDeleted=0 WHERE platform.TenantId IS NULL AND platform.SettingKey=provider.CredentialConfigurationKey AND platform.IsDeleted=0) credentialSetting
    OUTER APPLY(SELECT TOP(1) COALESCE(tenant.SettingValue,platform.SettingValue,platform.DefaultValue) SettingValue FROM Core.ConfigurationSetting platform LEFT JOIN Core.ConfigurationSetting tenant ON tenant.TenantId=policy.TenantId AND tenant.SettingKey=platform.SettingKey AND tenant.IsDeleted=0 WHERE platform.TenantId IS NULL AND platform.SettingKey=N'Intelligence.AzureOpenAi.ApiVersion' AND platform.IsDeleted=0) apiVersion
    WHERE policy.TenantId=@TenantId AND policy.FeatureCode=@FeatureCode AND policy.IsEnabled=1 AND policy.IsDeleted=0 AND model.CapabilityCode=@CapabilityCode
    UNION ALL
    SELECT policy.TenantId,policy.FeatureCode,provider.ProviderCode,provider.ProviderTypeCode,model.ModelCode,model.DeploymentName,
           endpointSetting.SettingValue,credentialSetting.SettingValue,apiVersion.SettingValue,
           policy.TimeoutSeconds,policy.Temperature,policy.MaximumOutputTokens,1,CONVERT(bit,1)
    FROM AI.FeaturePolicy policy
    JOIN AI.ModelDeployment model ON model.ModelDeploymentId=policy.FallbackModelDeploymentId AND model.IsActive=1 AND model.IsDeleted=0
    JOIN AI.Provider provider ON provider.ProviderId=model.ProviderId AND provider.IsActive=1 AND provider.IsDeleted=0
    OUTER APPLY(SELECT TOP(1) COALESCE(tenant.SettingValue,platform.SettingValue,platform.DefaultValue) SettingValue FROM Core.ConfigurationSetting platform LEFT JOIN Core.ConfigurationSetting tenant ON tenant.TenantId=policy.TenantId AND tenant.SettingKey=platform.SettingKey AND tenant.IsDeleted=0 WHERE platform.TenantId IS NULL AND platform.SettingKey=provider.EndpointConfigurationKey AND platform.IsDeleted=0) endpointSetting
    OUTER APPLY(SELECT TOP(1) COALESCE(tenant.SettingValue,platform.SettingValue,platform.DefaultValue) SettingValue FROM Core.ConfigurationSetting platform LEFT JOIN Core.ConfigurationSetting tenant ON tenant.TenantId=policy.TenantId AND tenant.SettingKey=platform.SettingKey AND tenant.IsDeleted=0 WHERE platform.TenantId IS NULL AND platform.SettingKey=provider.CredentialConfigurationKey AND platform.IsDeleted=0) credentialSetting
    OUTER APPLY(SELECT TOP(1) COALESCE(tenant.SettingValue,platform.SettingValue,platform.DefaultValue) SettingValue FROM Core.ConfigurationSetting platform LEFT JOIN Core.ConfigurationSetting tenant ON tenant.TenantId=policy.TenantId AND tenant.SettingKey=platform.SettingKey AND tenant.IsDeleted=0 WHERE platform.TenantId IS NULL AND platform.SettingKey=N'Intelligence.AzureOpenAi.ApiVersion' AND platform.IsDeleted=0) apiVersion
    WHERE policy.TenantId=@TenantId AND policy.FeatureCode=@FeatureCode AND policy.IsEnabled=1 AND policy.IsDeleted=0 AND model.CapabilityCode=@CapabilityCode
)
SELECT TenantId,FeatureCode,ProviderCode,ProviderTypeCode,ModelCode,DeploymentName,EndpointReference,CredentialReference,ApiVersion,TimeoutSeconds,Temperature,MaximumOutputTokens,Priority,IsFallback FROM routes ORDER BY Priority;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<AiProviderRoute>(new CommandDefinition(sql,new{TenantId=tenantId,FeatureCode=featureCode,CapabilityCode=capabilityCode},cancellationToken:cancellationToken))).AsList();
    }

    public async Task<AiSafetyPolicy> GetSafetyPolicyAsync(Guid tenantId,CancellationToken cancellationToken=default)
    {
        const string sql="""
SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Safety.MaximumInputCharacters' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END;
SELECT TOP(1) COALESCE(SettingValue,DefaultValue) FROM Core.ConfigurationSetting WHERE SettingKey=N'Intelligence.Safety.MaximumOutputCharacters' AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL) ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END;
WITH controls AS
(
    SELECT control.SafetyControlId,control.ControlCode,control.EnforcementStageCode,control.ViolationActionCode ActionCode,control.RequiresHumanReview,ROW_NUMBER() OVER(PARTITION BY control.ControlCode ORDER BY CASE WHEN control.TenantId=@TenantId THEN 0 ELSE 1 END) Choice
    FROM AI.SafetyControl control WHERE control.IsActive=1 AND control.IsDeleted=0 AND (control.TenantId=@TenantId OR control.TenantId IS NULL)
)
SELECT SafetyControlId,ControlCode,EnforcementStageCode,ActionCode,RequiresHumanReview FROM controls WHERE Choice=1;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi=await connection.QueryMultipleAsync(new CommandDefinition(sql,new{TenantId=tenantId},cancellationToken:cancellationToken));
        var maximumInput=int.TryParse(await multi.ReadSingleOrDefaultAsync<string>(),out var input)?input:0;
        var maximumOutput=int.TryParse(await multi.ReadSingleOrDefaultAsync<string>(),out var output)?output:0;
        var controls=(await multi.ReadAsync<AiSafetyControl>()).AsList();
        return new(maximumInput,maximumOutput,controls);
    }

    public async Task RecordSafetyEventAsync(AiSafetyEventRecord safetyEvent,CancellationToken cancellationToken=default)
    {
        const string sql="""INSERT AI.SafetyEvent(TenantId,SafetyControlId,EventTypeCode,EnforcementStageCode,ActionCode,SeverityCode,InputHash,DetailsJson,RequiresHumanReview,ReviewStatusCode,DetectedDateUtc,CreatedDateUtc,IsDeleted,IdempotencyKey) SELECT @TenantId,@SafetyControlId,@EventTypeCode,@EnforcementStageCode,@ActionCode,@SeverityCode,@InputHash,@DetailsJson,@RequiresHumanReview,@ReviewStatusCode,SYSUTCDATETIME(),SYSUTCDATETIME(),0,@IdempotencyKey WHERE NOT EXISTS(SELECT 1 FROM AI.SafetyEvent WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND IdempotencyKey=@IdempotencyKey AND IsDeleted=0);""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,safetyEvent,cancellationToken:cancellationToken));
    }

    public async Task RecordExecutionAsync(AiExecutionRecord execution,CancellationToken cancellationToken=default)
    {
        const string sql="""
INSERT AI.Execution(ExecutionId,TenantId,FeatureCode,ModuleCode,EntityTypeCode,EntityId,ProviderId,ModelDeploymentId,StatusCode,CorrelationId,StartedDateUtc,CompletedDateUtc,DurationMilliseconds,InputTokenCount,OutputTokenCount,Confidence,GroundingSourceCount,InputReference,ErrorCode,ErrorMessage,CreatedDateUtc,IsDeleted)
SELECT @ExecutionId,@TenantId,@FeatureCode,@ModuleCode,@EntityTypeCode,@EntityId,provider.ProviderId,model.ModelDeploymentId,@StatusCode,@CorrelationId,DATEADD(MILLISECOND,-@DurationMilliseconds,SYSUTCDATETIME()),SYSUTCDATETIME(),@DurationMilliseconds,@InputTokenCount,@OutputTokenCount,@Confidence,CASE WHEN @GroundingSourceReference IS NULL THEN 0 ELSE 1 END,@InputReference,@ErrorCode,@ErrorMessage,SYSUTCDATETIME(),0
FROM (SELECT 1 Value) seed
LEFT JOIN AI.Provider provider ON provider.ProviderCode=@ProviderCode AND provider.IsDeleted=0 AND (provider.TenantId=@TenantId OR provider.TenantId IS NULL)
LEFT JOIN AI.ModelDeployment model ON model.ProviderId=provider.ProviderId AND model.ModelCode=@ModelCode AND model.IsDeleted=0 AND (model.TenantId=@TenantId OR model.TenantId IS NULL)
WHERE NOT EXISTS(SELECT 1 FROM AI.Execution WITH(UPDLOCK,HOLDLOCK) WHERE TenantId=@TenantId AND CorrelationId=@CorrelationId AND FeatureCode=@FeatureCode AND IsDeleted=0);
IF @@ROWCOUNT>0 AND @GroundingSourceReference IS NOT NULL
INSERT AI.ExecutionGroundingSource(ExecutionGroundingSourceId,TenantId,ExecutionId,SourceTypeCode,SourceEntityId,SourceReference,Title,CreatedDateUtc,IsDeleted)
VALUES(NEWID(),@TenantId,@ExecutionId,@GroundingSourceTypeCode,@GroundingSourceEntityId,@GroundingSourceReference,@GroundingTitle,SYSUTCDATETIME(),0);
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(sql,execution,cancellationToken:cancellationToken));
    }
}
