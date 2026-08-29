using System.Collections.Concurrent;
using Ams.Application;
using Ams.Application.Abstractions.Intelligence;
using Ams.Application.Abstractions.Persistence;
using Dapper;

namespace Ams.Infrastructure.Services;

// Resolves system prompts from the user-managed AI.PromptDefinition registry. A tenant-specific
// approved row overrides a global (TenantId NULL) row; the newest effective approved version wins.
// Falls back to the embedded defaults in IntelligencePromptDefaults so behavior is unchanged when
// no row exists. Results are cached briefly to keep the hot search path off the database.
public sealed class PromptCatalog(ISqlConnectionFactory connectionFactory):IPromptCatalog
{
    private static readonly TimeSpan CacheDuration=TimeSpan.FromSeconds(60);
    private readonly ConcurrentDictionary<(Guid TenantId,string PromptCode),(string Prompt,DateTime ExpiresUtc)> cache=new();

    public async Task<string> GetSystemPromptAsync(Guid tenantId,string promptCode,CancellationToken cancellationToken=default)
    {
        var key=(tenantId,promptCode);
        if(cache.TryGetValue(key,out var cached)&&cached.ExpiresUtc>DateTime.UtcNow)return cached.Prompt;
        var prompt=await LoadAsync(tenantId,promptCode,cancellationToken)
            ??(IntelligencePromptDefaults.All.TryGetValue(promptCode,out var fallback)?fallback.SystemPrompt:throw new InvalidOperationException($"Unknown prompt code '{promptCode}'."));
        cache[key]=(prompt,DateTime.UtcNow.Add(CacheDuration));
        return prompt;
    }

    private async Task<string?> LoadAsync(Guid tenantId,string promptCode,CancellationToken cancellationToken)
    {
        const string sql="""
SELECT TOP(1) prompt.SystemInstructions
FROM AI.PromptDefinition prompt
WHERE prompt.PromptCode=@PromptCode AND prompt.StatusCode=N'APPROVED' AND prompt.IsDeleted=0
  AND (prompt.TenantId=@TenantId OR prompt.TenantId IS NULL)
  AND prompt.EffectiveFromUtc<=SYSUTCDATETIME() AND (prompt.EffectiveToUtc IS NULL OR prompt.EffectiveToUtc>SYSUTCDATETIME())
ORDER BY CASE WHEN prompt.TenantId=@TenantId THEN 0 ELSE 1 END,prompt.EffectiveFromUtc DESC;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var value=await connection.ExecuteScalarAsync<string?>(new CommandDefinition(sql,new{TenantId=tenantId,PromptCode=promptCode},cancellationToken:cancellationToken));
        return string.IsNullOrWhiteSpace(value)?null:value;
    }
}
