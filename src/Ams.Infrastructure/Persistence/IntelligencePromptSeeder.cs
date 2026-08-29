using Ams.Application;
using Ams.Application.Abstractions.Persistence;
using Dapper;

namespace Ams.Infrastructure.Persistence;

// Seeds the global (tenant-agnostic) default system prompts into the user-managed
// AI.PromptDefinition registry at startup. Idempotent: a prompt code that already has any global
// row (regardless of version or status) is never touched, so user edits are always preserved.
public sealed class IntelligencePromptSeeder(ISqlConnectionFactory connectionFactory)
{
    public async Task SeedAsync(CancellationToken cancellationToken=default)
    {
        const string sql="""
DECLARE @CapabilityId UNIQUEIDENTIFIER=(SELECT TOP(1) IntelligenceCapabilityId FROM AI.IntelligenceCapability WHERE TenantId IS NULL AND IsDeleted=0 ORDER BY CASE WHEN CapabilityCode LIKE N'%SEARCH%' THEN 0 ELSE 1 END,SortOrder);
IF @CapabilityId IS NOT NULL AND NOT EXISTS(SELECT 1 FROM AI.PromptDefinition WHERE TenantId IS NULL AND PromptCode=@PromptCode AND IsDeleted=0)
    INSERT AI.PromptDefinition(TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,CreatedDateUtc,IsDeleted)
    VALUES(NULL,@CapabilityId,@PromptCode,N'v1',@DisplayName,@SystemInstructions,N'{}',N'{}',N'APPROVED',@SystemUserId,SYSUTCDATETIME(),SYSUTCDATETIME(),SYSUTCDATETIME(),0);
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        foreach(var (promptCode,(displayName,systemPrompt)) in IntelligencePromptDefaults.All)
            await connection.ExecuteAsync(new CommandDefinition(sql,new{PromptCode=promptCode,DisplayName=displayName,SystemInstructions=systemPrompt,SystemUserId=Guid.Empty},cancellationToken:cancellationToken));
    }
}
