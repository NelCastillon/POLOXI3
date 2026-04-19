using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.FeatureCatalog;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class FeatureCatalogRepository : IFeatureCatalogRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public FeatureCatalogRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private const string SelectColumns =
        "FeatureId, FeatureCode, FeatureName, Module, TypeCode, DefaultEnabled, IsEnabled, CreatedDateUtc, ModifiedDateUtc";

    public async Task<PagedResult<FeatureCatalogDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT FeatureId, FeatureCode, FeatureName, Module, TypeCode, DefaultEnabled, IsEnabled, CreatedDateUtc, ModifiedDateUtc
    FROM Core.Feature
    WHERE (@SearchTerm IS NULL OR @SearchTerm = ''
           OR FeatureCode LIKE '%' + @SearchTerm + '%'
           OR FeatureName LIKE '%' + @SearchTerm + '%'
           OR Module      LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY Module, FeatureCode
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

SELECT COUNT(1)
FROM Core.Feature
WHERE (@SearchTerm IS NULL OR @SearchTerm = ''
       OR FeatureCode LIKE '%' + @SearchTerm + '%'
       OR FeatureName LIKE '%' + @SearchTerm + '%'
       OR Module      LIKE '%' + @SearchTerm + '%');";

        var safePage = Math.Max(pageNumber, 1);
        var safeSize = Math.Max(pageSize, 1);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql,
            new { SearchTerm = searchTerm, Offset = (safePage - 1) * safeSize, PageSize = safeSize },
            cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<FeatureCatalogDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<FeatureCatalogDto> { Items = items, TotalCount = total, PageNumber = safePage, PageSize = safeSize };
    }

    public async Task<FeatureCatalogDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sql = $"SELECT {SelectColumns} FROM Core.Feature WHERE FeatureId = @Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<FeatureCatalogDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(CreateFeatureRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Core.Feature (FeatureId, FeatureCode, FeatureName, Module, TypeCode, DefaultEnabled, IsEnabled, CreatedDateUtc)
VALUES (@FeatureId, @FeatureCode, @FeatureName, @Module, @TypeCode, @DefaultEnabled, @IsEnabled, SYSUTCDATETIME());
SELECT @FeatureId;";

        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
        {
            FeatureId      = id,
            FeatureCode    = request.FeatureCode.Trim().ToUpperInvariant(),
            FeatureName    = request.FeatureName,
            Module         = request.Module,
            TypeCode       = request.TypeCode,
            DefaultEnabled = request.DefaultEnabled,
            IsEnabled      = request.IsEnabled
        }, cancellationToken: cancellationToken));
    }

    public async Task UpdateAsync(Guid id, UpdateFeatureRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.Feature
SET FeatureName    = @FeatureName,
    Module         = @Module,
    TypeCode       = @TypeCode,
    DefaultEnabled = @DefaultEnabled,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE FeatureId = @Id;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id             = id,
            FeatureName    = request.FeatureName,
            Module         = request.Module,
            TypeCode       = request.TypeCode,
            DefaultEnabled = request.DefaultEnabled
        }, cancellationToken: cancellationToken));
    }

    public async Task SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.Feature SET IsEnabled = @Enabled, ModifiedDateUtc = SYSUTCDATETIME() WHERE FeatureId = @Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, Enabled = enabled }, cancellationToken: cancellationToken));
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "DELETE FROM Core.Feature WHERE FeatureId = @Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }
}
