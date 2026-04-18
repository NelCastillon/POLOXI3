using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ExternalUserProfileRepository : IExternalUserProfileRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public ExternalUserProfileRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<ExternalUserProfileDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT p.ExternalProfileId, p.TenantId, p.UserId, u.FullName AS UserFullName, p.ExternalUserTypeCode, p.OrganizationName,
       p.LicenseNumber, p.LicenseState, p.LicenseExpiryDate, p.NpnNumber, p.TaxId,
       p.PortalAccessEnabled, p.PortalLastLoginDateUtc, p.SsoProvider, p.CreatedDateUtc, p.ModifiedDateUtc
FROM IAM.ExternalUserProfile p
JOIN IAM.[User] u ON u.UserId = p.UserId
WHERE p.ExternalProfileId = @Id AND p.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ExternalUserProfileDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<ExternalUserProfileDto?> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT p.ExternalProfileId, p.TenantId, p.UserId, u.FullName AS UserFullName, p.ExternalUserTypeCode, p.OrganizationName,
       p.LicenseNumber, p.LicenseState, p.LicenseExpiryDate, p.NpnNumber, p.TaxId,
       p.PortalAccessEnabled, p.PortalLastLoginDateUtc, p.SsoProvider, p.CreatedDateUtc, p.ModifiedDateUtc
FROM IAM.ExternalUserProfile p
JOIN IAM.[User] u ON u.UserId = p.UserId
WHERE p.UserId = @UserId AND p.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ExternalUserProfileDto>(new CommandDefinition(sql, new { UserId = userId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ExternalUserProfileDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (
    SELECT p.ExternalProfileId, p.TenantId, p.UserId, u.FullName AS UserFullName, p.ExternalUserTypeCode, p.OrganizationName,
           p.LicenseNumber, p.LicenseState, p.LicenseExpiryDate, p.NpnNumber, p.TaxId,
           p.PortalAccessEnabled, p.PortalLastLoginDateUtc, p.SsoProvider, p.CreatedDateUtc, p.ModifiedDateUtc
    FROM IAM.ExternalUserProfile p
    JOIN IAM.[User] u ON u.UserId = p.UserId
    WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = '' OR u.FullName LIKE '%' + @SearchTerm + '%' OR p.OrganizationName LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte ORDER BY CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM IAM.ExternalUserProfile p JOIN IAM.[User] u ON u.UserId = p.UserId
WHERE p.TenantId = @TenantId AND p.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR u.FullName LIKE '%' + @SearchTerm + '%' OR p.OrganizationName LIKE '%' + @SearchTerm + '%');";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ExternalUserProfileDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ExternalUserProfileDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
