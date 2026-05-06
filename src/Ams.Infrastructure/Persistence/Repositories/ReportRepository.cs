using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class ReportRepository : IReportRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public ReportRepository(ISqlConnectionFactory connectionFactory)
        => _connectionFactory = connectionFactory;

    public async Task<ReportDefinitionDto?> GetByIdAsync(Guid reportDefinitionId, CancellationToken cancellationToken = default)
    {
        const string sql = """
            SELECT ReportDefinitionId, TenantId, ReportCode, ReportName, Description,
                   ModuleCode, ReportTypeCode, OutputFormats, IsSystemReport, IsActive,
                   CreatedDateUtc, ModifiedDateUtc
            FROM Core.ReportDefinition
            WHERE ReportDefinitionId = @ReportDefinitionId AND IsDeleted = 0
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ReportDefinitionDto>(
            new CommandDefinition(sql, new { ReportDefinitionId = reportDefinitionId }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<ReportDefinitionDto>> SearchDefinitionsAsync(Guid? tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT ReportDefinitionId, TenantId, ReportCode, ReportName, Description,
                       ModuleCode, ReportTypeCode, OutputFormats, IsSystemReport, IsActive,
                       CreatedDateUtc, ModifiedDateUtc
                FROM Core.ReportDefinition
                WHERE IsDeleted = 0
                  AND (@TenantId IS NULL OR TenantId = @TenantId OR TenantId IS NULL)
                  AND (@SearchTerm IS NULL OR ReportName LIKE '%' + @SearchTerm + '%'
                                          OR ReportCode LIKE '%' + @SearchTerm + '%'
                                          OR ModuleCode = @SearchTerm)
            )
            SELECT * FROM Cte ORDER BY IsSystemReport DESC, ReportName ASC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1) FROM Core.ReportDefinition
            WHERE IsDeleted = 0
              AND (@TenantId IS NULL OR TenantId = @TenantId OR TenantId IS NULL)
              AND (@SearchTerm IS NULL OR ReportName LIKE '%' + @SearchTerm + '%'
                                      OR ReportCode LIKE '%' + @SearchTerm + '%'
                                      OR ModuleCode = @SearchTerm);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ReportDefinitionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ReportDefinitionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<PagedResult<ReportExecutionDto>> SearchExecutionsAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = """
            ;WITH Cte AS (
                SELECT re.ReportExecutionId, re.TenantId, re.ReportDefinitionId,
                       rd.ReportName, re.ReportScheduleId, re.StatusCode, re.OutputFormat,
                       re.StoragePath, re.FileSizeBytes, re.[RowCount] AS [RowCount],
                       re.StartedDateUtc, re.CompletedDateUtc, re.ErrorMessage,
                       re.RequestedByUserId, re.CreatedDateUtc
                FROM Core.ReportExecution re
                INNER JOIN Core.ReportDefinition rd ON rd.ReportDefinitionId = re.ReportDefinitionId
                WHERE re.TenantId = @TenantId AND re.IsDeleted = 0
                  AND (@SearchTerm IS NULL OR rd.ReportName LIKE '%' + @SearchTerm + '%'
                                          OR re.StatusCode  = @SearchTerm)
            )
            SELECT * FROM Cte ORDER BY CreatedDateUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(1)
            FROM Core.ReportExecution re
            INNER JOIN Core.ReportDefinition rd ON rd.ReportDefinitionId = re.ReportDefinitionId
            WHERE re.TenantId = @TenantId AND re.IsDeleted = 0
              AND (@SearchTerm IS NULL OR rd.ReportName LIKE '%' + @SearchTerm + '%'
                                      OR re.StatusCode  = @SearchTerm);
            """;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm, Offset = (pageNumber - 1) * pageSize, PageSize = pageSize },
                cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<ReportExecutionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<ReportExecutionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
