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
}
