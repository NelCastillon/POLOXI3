using Dapper;
using Legal.Application.Abstractions.Persistence;

namespace Legal.Infrastructure.Persistence.Repositories;

// Dapper reader for the curated statute-text store (POLOXI.Legal_AuthorityStatuteText). Platform rows
// (TenantId NULL) are shared; a tenant-scoped row of the same (ProviderCode,JurisdictionCode,Section)
// overrides the platform row. Read-only: seeding/maintenance is done via migrations.
public sealed class LegalCuratedStatuteStore(ISqlConnectionFactory connectionFactory):ILegalCuratedStatuteStore
{
    public async Task<CuratedStatuteText?> GetAsync(Guid tenantId,string providerCode,string jurisdictionCode,string sectionNumber,CancellationToken cancellationToken=default)
    {
        const string sql="""
SELECT TOP(1) SectionNumber,StatuteText,SourceUrl,SourceLabel,VerifiedDateUtc
FROM POLOXI.Legal_AuthorityStatuteText
WHERE ProviderCode=@ProviderCode AND JurisdictionCode=@JurisdictionCode AND SectionNumber=@SectionNumber
  AND IsDeleted=0 AND (TenantId=@TenantId OR TenantId IS NULL)
ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END;
""";
        using var connection=await connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<CuratedStatuteText>(new CommandDefinition(
            sql,new{TenantId=tenantId,ProviderCode=providerCode,JurisdictionCode=jurisdictionCode,SectionNumber=sectionNumber},
            cancellationToken:cancellationToken));
    }
}
