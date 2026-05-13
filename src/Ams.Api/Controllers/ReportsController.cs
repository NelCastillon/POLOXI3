using Ams.Application.Abstractions.Services;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

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

    private async Task EnsureReportDataAsync(Guid? tenantId, CancellationToken cancellationToken)
    {
        var effectiveTenantId = tenantId ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Core') EXEC(N'CREATE SCHEMA Core');

IF OBJECT_ID(N'Core.ReportDefinition', N'U') IS NULL
BEGIN
    CREATE TABLE Core.ReportDefinition
    (
        ReportDefinitionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NULL,
        ReportCode NVARCHAR(100) NOT NULL,
        ReportName NVARCHAR(250) NOT NULL,
        Description NVARCHAR(1000) NULL,
        ModuleCode NVARCHAR(80) NOT NULL,
        ReportTypeCode NVARCHAR(80) NOT NULL,
        OutputFormats NVARCHAR(200) NOT NULL,
        DefinitionJson NVARCHAR(MAX) NULL,
        IsSystemReport BIT NOT NULL DEFAULT 1,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
    CREATE UNIQUE INDEX UX_ReportDefinition_Code ON Core.ReportDefinition(ReportCode) WHERE IsDeleted = 0;
END;

IF COL_LENGTH(N'Core.ReportDefinition', N'DefinitionJson') IS NULL
BEGIN
    ALTER TABLE Core.ReportDefinition ADD DefinitionJson NVARCHAR(MAX) NULL;
END;

IF COL_LENGTH(N'Core.ReportDefinition', N'CreatedByUserId') IS NULL
BEGIN
    ALTER TABLE Core.ReportDefinition ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
END;

IF COL_LENGTH(N'Core.ReportDefinition', N'ModifiedByUserId') IS NULL
BEGIN
    ALTER TABLE Core.ReportDefinition ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
END;

IF OBJECT_ID(N'Core.ReportExecution', N'U') IS NULL
BEGIN
    CREATE TABLE Core.ReportExecution
    (
        ReportExecutionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ReportDefinitionId UNIQUEIDENTIFIER NOT NULL,
        ReportScheduleId UNIQUEIDENTIFIER NULL,
        StatusCode NVARCHAR(80) NOT NULL,
        OutputFormat NVARCHAR(40) NOT NULL,
        StoragePath NVARCHAR(1000) NULL,
        FileSizeBytes BIGINT NULL,
        [RowCount] INT NULL,
        StartedDateUtc DATETIME2 NULL,
        CompletedDateUtc DATETIME2 NULL,
        ErrorMessage NVARCHAR(2000) NULL,
        RequestedByUserId UNIQUEIDENTIFIER NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END;

IF OBJECT_ID(N'Core.ReportSchedule', N'U') IS NULL
BEGIN
    CREATE TABLE Core.ReportSchedule
    (
        ReportScheduleId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ReportDefinitionId UNIQUEIDENTIFIER NOT NULL,
        ScheduleName NVARCHAR(250) NOT NULL,
        FrequencyCode NVARCHAR(80) NOT NULL,
        CronExpression NVARCHAR(120) NULL,
        OutputFormat NVARCHAR(40) NOT NULL,
        DeliveryEmail NVARCHAR(254) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        NextRunDateUtc DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END;

DECLARE @Reports TABLE (ReportCode NVARCHAR(100), ReportName NVARCHAR(250), Description NVARCHAR(1000), ModuleCode NVARCHAR(80), ReportTypeCode NVARCHAR(80), OutputFormats NVARCHAR(200));
INSERT INTO @Reports VALUES
(N'AGENCY-EXEC-SUMMARY',N'Agency Executive Summary',N'Tenant-wide production, revenue, retention, service, and operational KPI summary.',N'Agency',N'Dashboard',N'PDF,Excel,CSV'),
(N'AGENCY-BRANCH-PERF',N'Branch Performance Report',N'Premium, revenue, policy count, and conversion performance by branch.',N'Agency',N'Operational',N'PDF,Excel'),
(N'SALES-PIPELINE',N'Sales Pipeline Detail',N'Open opportunities by stage, producer, expected close, and estimated revenue.',N'Sales',N'Detail',N'Excel,CSV'),
(N'SALES-CONVERSION',N'Sales Conversion Funnel',N'Lead-to-account and opportunity conversion metrics by source and producer.',N'Sales',N'Analytics',N'PDF,Excel'),
(N'POLICY-BOOK',N'Policy Book of Business',N'Active policies by line of business, carrier, account type, branch, and producer.',N'Policy',N'Detail',N'Excel,CSV'),
(N'POLICY-CARRIER-MIX',N'Carrier Mix and Premium',N'Premium distribution and policy counts by carrier and line of business.',N'Policy',N'Analytics',N'PDF,Excel'),
(N'RETENTION-EXPIRING',N'Expiring Policies and Retention',N'Renewal pipeline, expiration windows, retention risk, and action status.',N'Retention',N'Operational',N'PDF,Excel,CSV'),
(N'RETENTION-RISK',N'Renewal Risk Scorecard',N'Renewal risk scoring, premium at risk, and producer/CSR follow-up status.',N'Retention',N'Analytics',N'PDF,Excel'),
(N'CLAIMS-OPEN',N'Open Claims Register',N'Open claims by account, policy, carrier, status, reserve, and severity.',N'Claims',N'Detail',N'Excel,CSV'),
(N'CLAIMS-LOSS-RATIO',N'Claims Loss Ratio Summary',N'Loss ratio, claim frequency, severity, and trend analysis by LOB and carrier.',N'Claims',N'Analytics',N'PDF,Excel'),
(N'FINANCE-AR-AGING',N'AR Aging Summary',N'Outstanding receivables by aging bucket, account, producer, and billing status.',N'Finance',N'Financial',N'PDF,Excel,CSV'),
(N'FINANCE-REVENUE',N'Revenue and Billing Performance',N'Revenue, invoice, payment, write-off, and collection performance.',N'Finance',N'Financial',N'PDF,Excel'),
(N'PRODUCER-SCORECARD',N'Producer Scorecard',N'Producer premium, revenue, activity, retention, and commission metrics.',N'Producer',N'Analytics',N'PDF,Excel'),
(N'PRODUCER-COMMISSIONS',N'Commission Statement Summary',N'Commission statement totals, exceptions, adjustments, and payout status.',N'Producer',N'Financial',N'PDF,Excel,CSV'),
(N'MARKETING-CAMPAIGN-ROI',N'Marketing Campaign ROI',N'Campaign reach, engagement, conversion, revenue attribution, and ROAS.',N'Marketing',N'Analytics',N'PDF,Excel'),
(N'MARKETING-LEAD-SOURCES',N'Lead Source Attribution',N'Lead volume, conversion, revenue, and ROI by marketing source.',N'Marketing',N'Analytics',N'Excel,CSV');

INSERT INTO Core.ReportDefinition (ReportDefinitionId,TenantId,ReportCode,ReportName,Description,ModuleCode,ReportTypeCode,OutputFormats,IsSystemReport,IsActive,CreatedDateUtc,IsDeleted)
SELECT NEWID(), NULL, r.ReportCode, r.ReportName, r.Description, r.ModuleCode, r.ReportTypeCode, r.OutputFormats,
       1, 1, SYSUTCDATETIME(), 0
FROM @Reports r
WHERE NOT EXISTS (SELECT 1 FROM Core.ReportDefinition d WHERE d.ReportCode = r.ReportCode AND d.IsDeleted = 0);

IF NOT EXISTS (SELECT 1 FROM Core.ReportExecution WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Core.ReportExecution (ReportExecutionId,TenantId,ReportDefinitionId,ReportScheduleId,StatusCode,OutputFormat,StoragePath,FileSizeBytes,[RowCount],StartedDateUtc,CompletedDateUtc,ErrorMessage,CreatedDateUtc,IsDeleted)
    SELECT TOP 8 NEWID(), @TenantId, d.ReportDefinitionId, NULL, N'Completed',
           CASE WHEN d.OutputFormats LIKE '%Excel%' THEN N'Excel' ELSE N'PDF' END,
           CONCAT(N'/reports/', d.ReportCode, N'-seed.', CASE WHEN d.OutputFormats LIKE '%Excel%' THEN N'xlsx' ELSE N'pdf' END),
           128000 + ABS(CHECKSUM(d.ReportCode)) % 900000,
           100 + ABS(CHECKSUM(d.ReportName)) % 9000,
           DATEADD(day, -ABS(CHECKSUM(d.ReportCode)) % 28, SYSUTCDATETIME()),
           DATEADD(day, -ABS(CHECKSUM(d.ReportCode)) % 28, DATEADD(minute, 2, SYSUTCDATETIME())),
           NULL,
           DATEADD(day, -ABS(CHECKSUM(d.ReportCode)) % 28, SYSUTCDATETIME()),
           0
    FROM Core.ReportDefinition d
    WHERE d.IsDeleted = 0 AND d.IsActive = 1
    ORDER BY d.ModuleCode, d.ReportName;
END;

IF NOT EXISTS (SELECT 1 FROM Core.ReportSchedule WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Core.ReportSchedule (ReportScheduleId,TenantId,ReportDefinitionId,ScheduleName,FrequencyCode,CronExpression,OutputFormat,DeliveryEmail,IsActive,NextRunDateUtc,CreatedDateUtc,IsDeleted)
    SELECT TOP 4 NEWID(), @TenantId, d.ReportDefinitionId, CONCAT(d.ReportName, N' - Weekly delivery'), N'Weekly', N'0 8 * * 1',
           CASE WHEN d.OutputFormats LIKE '%Excel%' THEN N'Excel' ELSE N'PDF' END, N'ops@agencybinder.local', 1, DATEADD(day, 7, SYSUTCDATETIME()), SYSUTCDATETIME(), 0
    FROM Core.ReportDefinition d
    WHERE d.IsDeleted = 0 AND d.IsActive = 1 AND d.ModuleCode IN (N'Agency', N'Sales', N'Finance', N'Retention')
    ORDER BY d.ModuleCode, d.ReportName;
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = effectiveTenantId }, cancellationToken: cancellationToken));
    }

    [HttpGet("definitions/{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        await EnsureReportDataAsync(null, cancellationToken);
        var item = await _service.GetByIdAsync(id, cancellationToken);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpGet("definitions")]
    public async Task<IActionResult> SearchDefinitions([FromQuery] Guid? tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureReportDataAsync(tenantId, cancellationToken);
        return Ok(await _service.SearchDefinitionsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));
    }

    [HttpGet("executions")]
    public async Task<IActionResult> SearchExecutions([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureReportDataAsync(tenantId, cancellationToken);
        return Ok(await _service.SearchExecutionsAsync(tenantId, searchTerm, pageNumber, pageSize, cancellationToken));
    }

    [HttpGet("schedules")]
    public async Task<IActionResult> SearchSchedules([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        await EnsureReportDataAsync(tenantId, cancellationToken);
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
        await EnsureReportDataAsync(request.TenantId, cancellationToken);
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

    [HttpGet("definitions/{id:guid}/download")]
    public async Task<IActionResult> DownloadDefinition(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureReportDataAsync(tenantId, cancellationToken);
        const string sql = @"
SELECT ReportDefinitionId, ReportCode, ReportName, Description, ModuleCode, ReportTypeCode, OutputFormats, IsActive, CreatedDateUtc
FROM Core.ReportDefinition
WHERE ReportDefinitionId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var report = await cn.QuerySingleOrDefaultAsync<ReportDefinitionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        if (report is null) return NotFound();

        var executionId = Guid.NewGuid();
        const string insertSql = @"
INSERT INTO Core.ReportExecution (ReportExecutionId, TenantId, ReportDefinitionId, ReportScheduleId, StatusCode, OutputFormat, StoragePath, FileSizeBytes, [RowCount], StartedDateUtc, CompletedDateUtc, ErrorMessage, CreatedDateUtc, IsDeleted)
VALUES (@ExecutionId, @TenantId, @ReportDefinitionId, NULL, N'Completed', N'Excel', @StoragePath, @FileSizeBytes, @RowCount, DATEADD(second, -3, SYSUTCDATETIME()), SYSUTCDATETIME(), NULL, SYSUTCDATETIME(), 0);";
        var fileName = $"{SafeFileName(report.ReportName)}-{DateTime.UtcNow:yyyyMMddHHmmss}.xls";
        var bytes = BuildExcelHtmlWorkbook(report.ReportName, [BuildPreview(report)]);
        await cn.ExecuteAsync(new CommandDefinition(insertSql, new { ExecutionId = executionId, TenantId = tenantId, report.ReportDefinitionId, StoragePath = $"/reports/{fileName}", FileSizeBytes = bytes.Length, RowCount = 1 }, cancellationToken: cancellationToken));
        return File(bytes, "application/vnd.ms-excel", fileName);
    }

    [HttpGet("definitions/{id:guid}/preview")]
    public async Task<IActionResult> PreviewDefinition(Guid id, [FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureReportDataAsync(tenantId, cancellationToken);
        const string sql = @"
SELECT ReportDefinitionId, ReportCode, ReportName, Description, ModuleCode, ReportTypeCode, OutputFormats, IsActive, CreatedDateUtc
FROM Core.ReportDefinition
WHERE ReportDefinitionId = @Id AND IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var report = await cn.QuerySingleOrDefaultAsync<ReportDefinitionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return report is null ? NotFound() : Ok(BuildPreview(report));
    }

    [HttpPost("definitions/download")]
    public async Task<IActionResult> DownloadDefinitions([FromBody] DownloadReportsRequest request, CancellationToken cancellationToken)
    {
        await EnsureReportDataAsync(request.TenantId, cancellationToken);
        const string sql = @"
SELECT ReportDefinitionId, ReportCode, ReportName, Description, ModuleCode, ReportTypeCode, OutputFormats, IsActive, CreatedDateUtc
FROM Core.ReportDefinition
WHERE IsDeleted = 0 AND IsActive = 1
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR ReportName LIKE '%' + @SearchTerm + '%' OR Description LIKE '%' + @SearchTerm + '%' OR ModuleCode LIKE '%' + @SearchTerm + '%')
ORDER BY ModuleCode, ReportName;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var reports = (await cn.QueryAsync<ReportDefinitionDto>(new CommandDefinition(sql, new { request.SearchTerm }, cancellationToken: cancellationToken))).AsList();
        if (!string.IsNullOrWhiteSpace(request.ModuleCode))
            reports = reports.Where(r => r.ModuleCode.Equals(request.ModuleCode, StringComparison.OrdinalIgnoreCase)).ToList();
        if (!string.IsNullOrWhiteSpace(request.Format))
            reports = reports.Where(r => r.OutputFormats.Contains(request.Format, StringComparison.OrdinalIgnoreCase)).ToList();
        if (request.ReportDefinitionIds is { Count: > 0 })
            reports = reports.Where(r => request.ReportDefinitionIds.Contains(r.ReportDefinitionId)).ToList();

        var fileName = $"reports-export-{DateTime.UtcNow:yyyyMMddHHmmss}.xls";
        var previews = reports.Select(BuildPreview).ToList();
        var bytes = BuildExcelHtmlWorkbook("Reports Export", previews);
        return File(bytes, "application/vnd.ms-excel", fileName);
    }

    [HttpPost("schedules")]
    public async Task<IActionResult> Schedule([FromBody] ScheduleReportRequest request, CancellationToken cancellationToken)
    {
        await EnsureReportDataAsync(request.TenantId, cancellationToken);
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

    [HttpPost("schedules/{id:guid}/status")]
    public async Task<IActionResult> SetScheduleStatus(Guid id, [FromQuery] bool isActive, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Core.ReportSchedule SET IsActive = @IsActive, ModifiedDateUtc = SYSUTCDATETIME() WHERE ReportScheduleId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, IsActive = isActive }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpDelete("schedules/{id:guid}")]
    public async Task<IActionResult> DeleteSchedule(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Core.ReportSchedule SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE ReportScheduleId = @Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    private static ReportPreviewDto BuildPreview(ReportDefinitionDto report)
    {
        var columns = report.ModuleCode switch
        {
            "Sales" => new[] { ("Period", "Period"), ("LeadSource", "Lead Source"), ("Leads", "Leads"), ("Opportunities", "Opportunities"), ("Won", "Won"), ("Premium", "Premium") },
            "Policy" => new[] { ("PolicyNumber", "Policy #"), ("Account", "Account"), ("Line", "Line"), ("Carrier", "Carrier"), ("Effective", "Effective"), ("Premium", "Premium") },
            "Retention" => new[] { ("Account", "Account"), ("PolicyNumber", "Policy #"), ("RenewalDate", "Renewal"), ("Status", "Status"), ("ExpiringPremium", "Expiring"), ("RenewalPremium", "Renewal Premium") },
            "Claims" => new[] { ("ClaimNumber", "Claim #"), ("Account", "Account"), ("PolicyNumber", "Policy #"), ("LossDate", "Loss Date"), ("Status", "Status"), ("Incurred", "Incurred") },
            "Finance" => new[] { ("InvoiceNumber", "Invoice #"), ("Account", "Account"), ("DueDate", "Due Date"), ("Status", "Status"), ("Balance", "Balance"), ("AgingBucket", "Aging") },
            "Producer" => new[] { ("Producer", "Producer"), ("Book", "Book"), ("NewBusiness", "New Business"), ("Renewal", "Renewal"), ("Commission", "Commission"), ("Retention", "Retention") },
            "Marketing" => new[] { ("Campaign", "Campaign"), ("Channel", "Channel"), ("Sent", "Sent"), ("Opened", "Opened"), ("Leads", "Leads"), ("Conversion", "Conversion") },
            _ => new[] { ("Metric", "Metric"), ("Category", "Category"), ("Current", "Current"), ("Prior", "Prior"), ("Variance", "Variance"), ("Owner", "Owner") }
        };

        var rows = Enumerable.Range(1, 12).Select(i => BuildPreviewRow(report, columns, i)).ToList();
        return new ReportPreviewDto
        {
            ReportDefinitionId = report.ReportDefinitionId,
            ReportCode = report.ReportCode,
            ReportName = report.ReportName,
            Description = report.Description,
            ModuleCode = report.ModuleCode,
            ReportTypeCode = report.ReportTypeCode,
            OutputFormats = report.OutputFormats,
            GeneratedDateUtc = DateTime.UtcNow,
            RowCount = rows.Count,
            Columns = columns.Select(c => new ReportPreviewColumnDto(c.Item1, c.Item2, InferDataType(c.Item1))).ToList(),
            Rows = rows
        };
    }

    private static Dictionary<string, string> BuildPreviewRow(ReportDefinitionDto report, (string Field, string Header)[] columns, int index)
    {
        var accounts = new[] { "Riverside Construction LLC", "Chen Family Trust", "Torres Household", "Northwind Logistics", "Summit Dental Group", "Blue Harbor Cafe" };
        var carriers = new[] { "Apex Mutual", "North Coast Insurance", "Pioneer Specialty", "Harbor Underwriters" };
        var statuses = new[] { "Active", "Pending", "Completed", "In Review", "Renewed", "Open" };
        var row = new Dictionary<string, string>();
        foreach (var column in columns)
        {
            row[column.Field] = column.Field switch
            {
                "Period" => DateTime.UtcNow.AddMonths(-index + 1).ToString("MMM yyyy", CultureInfo.InvariantCulture),
                "LeadSource" => new[] { "Referral", "Website", "Producer", "Renewal", "Campaign" }[index % 5],
                "PolicyNumber" => $"POL-{DateTime.UtcNow:yy}-{10000 + Math.Abs(HashCode.Combine(report.ReportCode, index)) % 89999}",
                "ClaimNumber" => $"CLM-{DateTime.UtcNow:yy}-{20000 + Math.Abs(HashCode.Combine(report.ReportName, index)) % 79999}",
                "InvoiceNumber" => $"INV-{DateTime.UtcNow:yy}-{30000 + index * 17}",
                "Account" => accounts[index % accounts.Length],
                "Line" => new[] { "Commercial Auto", "BOP", "Workers Comp", "General Liability", "Personal Auto" }[index % 5],
                "Carrier" => carriers[index % carriers.Length],
                "Effective" or "RenewalDate" or "DueDate" => DateTime.UtcNow.AddDays(index * 9).ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
                "LossDate" => DateTime.UtcNow.AddDays(-index * 11).ToString("MMM d, yyyy", CultureInfo.InvariantCulture),
                "Status" => statuses[index % statuses.Length],
                "Premium" or "ExpiringPremium" or "RenewalPremium" or "Balance" or "Incurred" or "Commission" or "NewBusiness" or "Renewal" => FormatUsd(2500 + Math.Abs(HashCode.Combine(report.ReportCode, column.Field, index)) % 95000),
                "Leads" or "Opportunities" or "Won" or "Sent" or "Opened" => (10 + Math.Abs(HashCode.Combine(report.ReportName, column.Field, index)) % 900).ToString(CultureInfo.InvariantCulture),
                "Producer" or "Owner" => new[] { "Avery Stone", "Maya Collins", "Noah Ramirez", "Sophia Lee" }[index % 4],
                "Book" => FormatUsd(125000 + index * 18450),
                "Retention" or "Conversion" or "Variance" => $"{72 + index % 21}%",
                "AgingBucket" => new[] { "Current", "1-30", "31-60", "61-90", "90+" }[index % 5],
                "Campaign" => new[] { "Spring Renewal Outreach", "Commercial Lines Cross-sell", "Referral Thank You", "Win-back Campaign" }[index % 4],
                "Channel" => new[] { "Email", "SMS", "Landing Page", "Producer" }[index % 4],
                "Metric" => $"{report.ModuleCode} KPI {index}",
                "Category" => report.ReportTypeCode,
                "Current" => (100 + index * 13).ToString(CultureInfo.InvariantCulture),
                "Prior" => (90 + index * 11).ToString(CultureInfo.InvariantCulture),
                _ => $"{column.Header} {index}"
            };
        }
        return row;
    }

    private static string InferDataType(string field)
        => field.Contains("Date", StringComparison.OrdinalIgnoreCase) || field is "Effective" or "RenewalDate" or "DueDate" ? "Date" :
           field.Contains("Premium", StringComparison.OrdinalIgnoreCase) || field is "Balance" or "Incurred" or "Commission" or "Book" ? "Currency" :
           field is "Leads" or "Opportunities" or "Won" or "Sent" or "Opened" or "Current" or "Prior" ? "Number" : "Text";

    private static string FormatUsd(decimal amount) => $"${amount.ToString("N0", CultureInfo.InvariantCulture)}";

    private static byte[] BuildExcelHtmlWorkbook(string title, IReadOnlyList<ReportPreviewDto> reports)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<html xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\" xmlns=\"http://www.w3.org/TR/REC-html40\">");
        sb.AppendLine("<head><meta charset=\"utf-8\"><style>th{background:#eef6ff;color:#172f70;font-weight:700}td,th{border:1px solid #dde5f0;padding:6px;mso-number-format:'\\@';} .title{font-size:18px;font-weight:700;background:#f8fafc}</style></head><body>");
        foreach (var report in reports)
        {
            sb.AppendLine("<table>");
            sb.Append("<tr><td class=\"title\" colspan=\"").Append(report.Columns.Count).Append("\">").Append(Html(report.ReportName)).Append("</td></tr>");
            sb.AppendLine("<tr>");
            foreach (var column in report.Columns) sb.Append("<th>").Append(Html(column.Header)).Append("</th>");
            sb.AppendLine("</tr>");
            foreach (var row in report.Rows)
            {
                sb.Append("<tr>");
                foreach (var column in report.Columns) sb.AppendCell(row.GetValueOrDefault(column.Field, string.Empty));
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</table><br/>");
        }
        sb.AppendLine("</table></body></html>");
        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var cleaned = new string(value.Select(ch => invalid.Contains(ch) ? '-' : ch).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "report" : cleaned.Replace(' ', '-').ToLowerInvariant();
    }

    private static string Html(string? value) => System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
}

file static class ReportExcelHtmlExtensions
{
    public static StringBuilder AppendCell(this StringBuilder builder, string value)
        => builder.Append("<td>").Append(System.Net.WebUtility.HtmlEncode(value)).Append("</td>");
}
