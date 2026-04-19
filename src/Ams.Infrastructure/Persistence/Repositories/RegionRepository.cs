using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Regions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class RegionRepository : IRegionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public RegionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns =
        "RegionId, RegionCode, RegionName, CloudRegion, ComplianceProfile, PrimaryStamp, SecondaryStamp, IsActive, CreatedDateUtc, ModifiedDateUtc";

    public async Task<PagedResult<RegionDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT RegionId, RegionCode, RegionName, CloudRegion, ComplianceProfile, PrimaryStamp, SecondaryStamp, IsActive, CreatedDateUtc, ModifiedDateUtc
    FROM Core.Region
    WHERE IsDeleted = 0
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR RegionCode        LIKE '%' + @SearchTerm + '%'
           OR RegionName        LIKE '%' + @SearchTerm + '%'
           OR CloudRegion       LIKE '%' + @SearchTerm + '%'
           OR ComplianceProfile LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY RegionCode
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Core.Region
WHERE IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = ''
       OR RegionCode        LIKE '%' + @SearchTerm + '%'
       OR RegionName        LIKE '%' + @SearchTerm + '%'
       OR CloudRegion       LIKE '%' + @SearchTerm + '%'
       OR ComplianceProfile LIKE '%' + @SearchTerm + '%');";

        var safePage = Math.Max(pageNumber, 1);
        var safeSize = Math.Max(pageSize, 1);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { SearchTerm = searchTerm, Offset = (safePage - 1) * safeSize, PageSize = safeSize },
            cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<RegionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<RegionDto> { Items = items, TotalCount = total, PageNumber = safePage, PageSize = safeSize };
    }

    public async Task<RegionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM Core.Region WHERE RegionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<RegionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(CreateRegionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Core.Region (RegionId, RegionCode, RegionName, CloudRegion, ComplianceProfile, PrimaryStamp, SecondaryStamp, IsActive, CreatedDateUtc)
VALUES (@RegionId, @RegionCode, @RegionName, @CloudRegion, @ComplianceProfile, @PrimaryStamp, @SecondaryStamp, @IsActive, SYSUTCDATETIME());
SELECT @RegionId;";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
        {
            RegionId          = id,
            RegionCode        = request.RegionCode.Trim().ToUpperInvariant(),
            RegionName        = request.RegionName,
            CloudRegion       = request.CloudRegion,
            ComplianceProfile = request.ComplianceProfile,
            PrimaryStamp      = request.PrimaryStamp,
            SecondaryStamp    = request.SecondaryStamp,
            IsActive          = request.IsActive
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Guid id, UpdateRegionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.Region
SET RegionName        = @RegionName,
    CloudRegion       = @CloudRegion,
    ComplianceProfile = @ComplianceProfile,
    PrimaryStamp      = @PrimaryStamp,
    SecondaryStamp    = @SecondaryStamp,
    ModifiedDateUtc   = SYSUTCDATETIME()
WHERE RegionId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id                = id,
            RegionName        = request.RegionName,
            CloudRegion       = request.CloudRegion,
            ComplianceProfile = request.ComplianceProfile,
            PrimaryStamp      = request.PrimaryStamp,
            SecondaryStamp    = request.SecondaryStamp
        }, cancellationToken: cancellationToken));
    }

    public async Task SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.Region SET IsActive = @IsActive, ModifiedDateUtc = SYSUTCDATETIME() WHERE RegionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, IsActive = isActive }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.Region SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE RegionId = @Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
