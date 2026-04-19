using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.SlaDefinitions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SlaDefinitionRepository : ISlaDefinitionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SlaDefinitionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<SlaDefinitionDto>> SearchAsync(string? searchTerm, string? complianceStatus, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT SlaDefinitionId, SlaName, ServiceName, MetricTypeCode, TargetValue, TargetUnit,
           PeriodCode, CurrentValue, ComplianceStatus, LastEvaluatedUtc, IsActive, Notes,
           CreatedDateUtc, ModifiedDateUtc
    FROM Core.SlaDefinition
    WHERE IsDeleted = 0
      AND (@ComplianceStatus IS NULL OR @ComplianceStatus = '' OR ComplianceStatus = @ComplianceStatus)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR SlaName        LIKE '%' + @SearchTerm + '%'
           OR ServiceName    LIKE '%' + @SearchTerm + '%'
           OR MetricTypeCode LIKE '%' + @SearchTerm + '%')
)
SELECT COUNT(*) FROM Cte;

;WITH Cte AS
(
    SELECT SlaDefinitionId, SlaName, ServiceName, MetricTypeCode, TargetValue, TargetUnit,
           PeriodCode, CurrentValue, ComplianceStatus, LastEvaluatedUtc, IsActive, Notes,
           CreatedDateUtc, ModifiedDateUtc
    FROM Core.SlaDefinition
    WHERE IsDeleted = 0
      AND (@ComplianceStatus IS NULL OR @ComplianceStatus = '' OR ComplianceStatus = @ComplianceStatus)
      AND (@SearchTerm IS NULL OR @SearchTerm = ''
           OR SlaName        LIKE '%' + @SearchTerm + '%'
           OR ServiceName    LIKE '%' + @SearchTerm + '%'
           OR MetricTypeCode LIKE '%' + @SearchTerm + '%')
)
SELECT * FROM Cte
ORDER BY ServiceName, SlaName
OFFSET (@PageNumber - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await conn.QueryMultipleAsync(sql, new
        {
            SearchTerm       = searchTerm,
            ComplianceStatus = complianceStatus,
            PageNumber       = pageNumber,
            PageSize         = pageSize
        });

        var total = await multi.ReadSingleAsync<int>();
        var items = (await multi.ReadAsync<SlaDefinitionDto>()).ToList();
        return new PagedResult<SlaDefinitionDto>
        {
            Items      = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize   = pageSize
        };
    }

    public async Task<SlaDefinitionDto?> GetByIdAsync(Guid slaDefinitionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT SlaDefinitionId, SlaName, ServiceName, MetricTypeCode, TargetValue, TargetUnit,
       PeriodCode, CurrentValue, ComplianceStatus, LastEvaluatedUtc, IsActive, Notes,
       CreatedDateUtc, ModifiedDateUtc
FROM Core.SlaDefinition
WHERE SlaDefinitionId = @SlaDefinitionId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.QuerySingleOrDefaultAsync<SlaDefinitionDto>(sql, new { SlaDefinitionId = slaDefinitionId });
    }

    public async Task<Guid> CreateAsync(CreateSlaDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NewId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Core.SlaDefinition
    (SlaDefinitionId, SlaName, ServiceName, MetricTypeCode, TargetValue, TargetUnit,
     PeriodCode, Notes, CreatedDateUtc, CreatedByUserId)
VALUES
    (@NewId, @SlaName, @ServiceName, @MetricTypeCode, @TargetValue, @TargetUnit,
     @PeriodCode, @Notes, SYSUTCDATETIME(), @CreatedByUserId);
SELECT @NewId;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await conn.ExecuteScalarAsync<Guid>(sql, new
        {
            request.SlaName, request.ServiceName, request.MetricTypeCode,
            request.TargetValue, request.TargetUnit, request.PeriodCode,
            request.Notes, request.CreatedByUserId
        });
    }

    public async Task UpdateAsync(Guid slaDefinitionId, UpdateSlaDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Core.SlaDefinition
SET SlaName          = @SlaName,
    ServiceName      = @ServiceName,
    MetricTypeCode   = @MetricTypeCode,
    TargetValue      = @TargetValue,
    TargetUnit       = @TargetUnit,
    PeriodCode       = @PeriodCode,
    CurrentValue     = @CurrentValue,
    ComplianceStatus = @ComplianceStatus,
    IsActive         = @IsActive,
    Notes            = @Notes,
    LastEvaluatedUtc = SYSUTCDATETIME(),
    ModifiedDateUtc  = SYSUTCDATETIME()
WHERE SlaDefinitionId = @SlaDefinitionId AND IsDeleted = 0;";

        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new
        {
            SlaDefinitionId = slaDefinitionId,
            request.SlaName, request.ServiceName, request.MetricTypeCode,
            request.TargetValue, request.TargetUnit, request.PeriodCode,
            request.CurrentValue, request.ComplianceStatus, request.IsActive, request.Notes
        });
    }

    public async Task DeleteAsync(Guid slaDefinitionId, CancellationToken cancellationToken = default)
    {
        const string sql = "UPDATE Core.SlaDefinition SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE SlaDefinitionId = @SlaDefinitionId;";
        using var conn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await conn.ExecuteAsync(sql, new { SlaDefinitionId = slaDefinitionId });
    }
}
