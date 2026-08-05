using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class DocumentOcrRouteRepository(ISqlConnectionFactory connectionFactory) : IDocumentOcrRouteRepository
{
    public async Task<DocumentOcrRoute?> GetRouteAsync(Guid? tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = """
WITH resolved AS
(
    SELECT setting.SettingKey,COALESCE(NULLIF(setting.SettingValue,N''),setting.DefaultValue) SettingValue,
           ROW_NUMBER() OVER(PARTITION BY setting.SettingKey ORDER BY CASE WHEN setting.TenantId=@TenantId THEN 0 ELSE 1 END) Choice
    FROM Core.ConfigurationSetting setting
    WHERE setting.ScopeCode=N'Platform' AND setting.IsDeleted=0
      AND setting.SettingKey IN(N'DocumentIntelligence.Endpoint',N'DocumentIntelligence.ModelId',N'DocumentIntelligence.ApiVersion',N'DocumentIntelligence.CredentialReference',N'DocumentIntelligence.TimeoutSeconds')
      AND (setting.TenantId IS NULL OR setting.TenantId=@TenantId)
)
SELECT
    MAX(CASE WHEN SettingKey=N'DocumentIntelligence.Endpoint' AND Choice=1 THEN SettingValue END) Endpoint,
    MAX(CASE WHEN SettingKey=N'DocumentIntelligence.ModelId' AND Choice=1 THEN SettingValue END) ModelId,
    MAX(CASE WHEN SettingKey=N'DocumentIntelligence.ApiVersion' AND Choice=1 THEN SettingValue END) ApiVersion,
    MAX(CASE WHEN SettingKey=N'DocumentIntelligence.CredentialReference' AND Choice=1 THEN SettingValue END) CredentialReference,
    TRY_CONVERT(int,MAX(CASE WHEN SettingKey=N'DocumentIntelligence.TimeoutSeconds' AND Choice=1 THEN SettingValue END)) TimeoutSeconds;
""";
        using var connection = await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync<RouteRow>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        if (string.IsNullOrWhiteSpace(row.Endpoint) || string.IsNullOrWhiteSpace(row.ModelId) || string.IsNullOrWhiteSpace(row.ApiVersion))
            return null;
        return new(row.Endpoint, row.ModelId, row.ApiVersion, NullIfEmpty(row.CredentialReference), Math.Clamp(row.TimeoutSeconds ?? 180, 30, 900));
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private sealed record RouteRow(string? Endpoint, string? ModelId, string? ApiVersion, string? CredentialReference, int? TimeoutSeconds);
}
