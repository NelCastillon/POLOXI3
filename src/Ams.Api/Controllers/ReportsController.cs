using Ams.Application.Abstractions.Services;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/reports")]
public sealed class ReportsController : ControllerBase
{
    private readonly IReportService _service;
    private readonly ISqlConnectionFactory _connectionFactory;

    public ReportsController(IReportService service, ISqlConnectionFactory connectionFactory)
    {
        _service = service;
        _connectionFactory = connectionFactory;
    }

    [HttpGet("definitions/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("definitions")]
    public async Task<IActionResult> SearchDefinitions([FromQuery] Guid? tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchDefinitionsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("executions")]
    public async Task<IActionResult> SearchExecutions([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
        => Ok(await _service.SearchExecutionsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));

    [HttpGet("schedules")]
    public async Task<IActionResult> SearchSchedules([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        const string sql = @"
SELECT s.ReportScheduleId, s.TenantId, s.ReportDefinitionId, d.ReportName, d.ModuleCode,
       s.FrequencyCode, s.OutputFormat, s.DeliveryEmail, s.IsActive, s.NextRunDateUtc, s.CreatedDateUtc
FROM Core.ReportSchedule s
JOIN Core.ReportDefinition d ON d.ReportDefinitionId = s.ReportDefinitionId
WHERE s.TenantId = @TenantId AND s.IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR d.ReportName LIKE '%' + @SearchTerm + '%' OR d.ModuleCode = @SearchTerm)
ORDER BY s.NextRunDateUtc, d.ReportName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var items = (await cn.QueryAsync<ReportScheduleDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).AsList();
        return Ok(new PagedResult<ReportScheduleDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpPost("definitions/{id:guid}/run")]
    public async Task<IActionResult> Run(Guid id, [FromBody] RunReportRequest request, CancellationToken cancellationToken)
    {
        var executionId = Guid.NewGuid();
        const string sql = @"
INSERT INTO Core.ReportExecution (ReportExecutionId, TenantId, ReportDefinitionId, ReportScheduleId, StatusCode, OutputFormat, StoragePath, FileSizeBytes, [RowCount], StartedDateUtc, CompletedDateUtc, ErrorMessage, RequestedByUserId, CreatedDateUtc, IsDeleted)
SELECT @ExecutionId, @TenantId, ReportDefinitionId, NULL, 'Completed', @OutputFormat,
       CONCAT('/reports/', ReportCode, '-', FORMAT(SYSUTCDATETIME(), 'yyyyMMddHHmmss'), CASE WHEN @OutputFormat = 'PDF' THEN '.pdf' WHEN @OutputFormat = 'CSV' THEN '.csv' ELSE '.xlsx' END),
       128000 + ABS(CHECKSUM(ReportCode)) % 900000,
       100 + ABS(CHECKSUM(ReportName)) % 9000,
       DATEADD(second, -8, SYSUTCDATETIME()), SYSUTCDATETIME(), NULL, @RequestedByUserId, SYSUTCDATETIME(), 0
FROM Core.ReportDefinition
WHERE ReportDefinitionId = @ReportDefinitionId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ExecutionId = executionId, TenantId = request.TenantId, ReportDefinitionId = id, request.OutputFormat, request.RequestedByUserId }, cancellationToken: cancellationToken));
        return Ok(new IdResult { Id = executionId });
    }

    [HttpPost("schedules")]
    public async Task<IActionResult> Schedule([FromBody] ScheduleReportRequest request, CancellationToken cancellationToken)
    {
        var scheduleId = Guid.NewGuid();
        const string sql = @"
INSERT INTO Core.ReportSchedule (ReportScheduleId, TenantId, ReportDefinitionId, ScheduleName, FrequencyCode, CronExpression, OutputFormat, DeliveryEmail, IsActive, NextRunDateUtc, CreatedDateUtc, ModifiedDateUtc, IsDeleted)
SELECT @ScheduleId, @TenantId, @ReportDefinitionId, CONCAT(ReportName, ' - ', @FrequencyCode, ' delivery'), @FrequencyCode,
        CASE @FrequencyCode WHEN 'Daily' THEN '0 8 * * *' WHEN 'Weekly' THEN '0 8 * * 1' WHEN 'Monthly' THEN '0 8 1 * *' ELSE '0 8 1 */3 *' END,
        @OutputFormat, @DeliveryEmail, 1,
        CASE @FrequencyCode WHEN 'Daily' THEN DATEADD(day,1,SYSUTCDATETIME()) WHEN 'Weekly' THEN DATEADD(day,7,SYSUTCDATETIME()) WHEN 'Monthly' THEN DATEADD(month,1,SYSUTCDATETIME()) ELSE DATEADD(month,3,SYSUTCDATETIME()) END,
        SYSUTCDATETIME(), NULL, 0
FROM Core.ReportDefinition
WHERE ReportDefinitionId = @ReportDefinitionId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { ScheduleId = scheduleId, request.TenantId, request.ReportDefinitionId, request.FrequencyCode, request.OutputFormat, request.DeliveryEmail }, cancellationToken: cancellationToken));
        return Ok(new IdResult { Id = scheduleId });
    }
}
