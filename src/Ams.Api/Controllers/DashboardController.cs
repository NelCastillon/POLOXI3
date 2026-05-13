using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class DashboardController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDashboardService _service;
    private readonly ISqlConnectionFactory _connectionFactory;

    public DashboardController(IDashboardService service, ISqlConnectionFactory connectionFactory)
    {
        _service = service;
        _connectionFactory = connectionFactory;
    }

    private async Task EnsureDashboardDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Analytics') EXEC(N'CREATE SCHEMA Analytics');

IF OBJECT_ID(N'Analytics.DashboardRecord', N'U') IS NULL
BEGIN
    CREATE TABLE Analytics.DashboardRecord
    (
        DashboardRecordId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Kind NVARCHAR(100) NOT NULL,
        Code NVARCHAR(200) NOT NULL,
        Name NVARCHAR(250) NOT NULL,
        Status NVARCHAR(80) NOT NULL,
        JsonData NVARCHAR(MAX) NOT NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_DashboardRecord_Tenant_Kind ON Analytics.DashboardRecord(TenantId, Kind, IsDeleted);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, cancellationToken: cancellationToken));

        var now = DateTime.UtcNow;
        var executive = new ExecutiveDashboardPageDto
        {
            Kpi = new ExecutiveDashboardKpiDto { WrittenPremium = 1842500, WrittenPremiumDelta = 12.4, RetentionRate = 88.7, RetentionDelta = 2.1, NewBusinessPremium = 426000, NewBusinessDelta = 9.3, RenewalAtRiskCount = 12, RenewalAtRiskPremium = 318000, OpenClaimsCount = 18, TotalIncurredLoss = 740000, OverdueReceivables = 96000, OverduePct = 8.4 },
            PremiumTrend = [new() { Label = "Jan", Value = 210000, PriorValue = 188000 }, new() { Label = "Feb", Value = 235000, PriorValue = 198000 }, new() { Label = "Mar", Value = 298000, PriorValue = 255000 }, new() { Label = "Apr", Value = 318000, PriorValue = 276000 }, new() { Label = "May", Value = 372000, PriorValue = 326000 }, new() { Label = "Jun", Value = 409000, PriorValue = 355000 }],
            NewBusinessTrend = [new() { Label = "Jan", NewBiz = 54000, Renewal = 156000 }, new() { Label = "Feb", NewBiz = 61000, Renewal = 174000 }, new() { Label = "Mar", NewBiz = 72000, Renewal = 226000 }, new() { Label = "Apr", NewBiz = 79000, Renewal = 239000 }, new() { Label = "May", NewBiz = 82000, Renewal = 290000 }, new() { Label = "Jun", NewBiz = 78000, Renewal = 331000 }],
            RetentionSegments = [new() { Label = "Renewed", Value = 88.7 }, new() { Label = "At Risk", Value = 7.4 }, new() { Label = "Lost", Value = 3.9 }],
            RenewalsAtRisk = [new() { AccountName = "Riverside Construction LLC", LobCode = "BOP", ExpiryDate = now.AddDays(18), Premium = 84200, RiskLevel = "High" }, new() { AccountName = "Chen Family", LobCode = "Home", ExpiryDate = now.AddDays(24), Premium = 3200, RiskLevel = "Medium" }, new() { AccountName = "Sato Tech LLC", LobCode = "Cyber", ExpiryDate = now.AddDays(36), Premium = 21400, RiskLevel = "High" }],
            ClaimsBySeverity = [new() { Label = "High", Value = 4 }, new() { Label = "Medium", Value = 8 }, new() { Label = "Low", Value = 6 }],
            ReceivablesAging = [new() { Label = "Current", Value = 380000 }, new() { Label = "1-30", Value = 72000 }, new() { Label = "31-60", Value = 18000 }, new() { Label = "61-90", Value = 6000 }, new() { Label = "90+", Value = 1200 }],
            Campaigns = [new() { Name = "Home+Auto Bundle", Leads = 420, Quoted = 184, Bound = 72, ConversionPct = 17.1, Premium = 206000 }, new() { Name = "Umbrella Cross-Sell", Leads = 188, Quoted = 96, Bound = 44, ConversionPct = 23.4, Premium = 94000 }, new() { Name = "Win-Back", Leads = 231, Quoted = 88, Bound = 29, ConversionPct = 12.6, Premium = 115500 }],
            Producers = [new() { ProducerName = "Beth Nguyen", Branch = "Downtown", WrittenPremium = 412000, PoliciesWritten = 38, NewBusiness = 126000, RetentionRate = 91.2, GoalPct = 118, Lob = "Commercial" }, new() { ProducerName = "Jake Park", Branch = "North", WrittenPremium = 356000, PoliciesWritten = 44, NewBusiness = 98000, RetentionRate = 87.5, GoalPct = 101, Lob = "Personal" }, new() { ProducerName = "Sara Kim", Branch = "West", WrittenPremium = 322000, PoliciesWritten = 31, NewBusiness = 112000, RetentionRate = 84.3, GoalPct = 94, Lob = "Benefits" }]
        };

        var dashboards = new[]
        {
            new DashboardDefinitionDto { Name = "Executive Snapshot", Description = "Top-level KPIs for leadership: premium, retention, loss ratio, pipeline.", Icon = "bi-speedometer2", IconCss = "db-di-blue", Audience = "Executive", WidgetCount = 8, IsDefault = true, LastEdited = now.AddDays(-2), Widgets = ["Revenue KPI", "Retention Rate", "Loss Ratio", "Pipeline", "New Business", "Claims Open", "Producer Top 5", "Renewal 30d"] },
            new DashboardDefinitionDto { Name = "Producer Workbench", Description = "Per-producer pipeline, tasks, expiring policies, and activity feed.", Icon = "bi-person-badge", IconCss = "db-di-purple", Audience = "Producer", WidgetCount = 6, IsDefault = false, LastEdited = now.AddDays(-5), Widgets = ["My Pipeline", "Expiring 30d", "Open Tasks", "Recent Activity", "Commission MTD", "Goal Progress"] },
            new DashboardDefinitionDto { Name = "Financial Overview", Description = "Revenue, AR aging, commission payable, and collections summary.", Icon = "bi-bank", IconCss = "db-di-green", Audience = "Accounting", WidgetCount = 7, IsDefault = false, LastEdited = now.AddDays(-3), Widgets = ["Revenue MTD", "AR Aging", "Commission Payable", "Collections", "Invoices Due"] }
        };

        var kpis = new[]
        {
            new DashboardKpiDefinitionDto { Name = "Retention Rate", Domain = "Retention", Description = "Percentage of expiring policies renewed in the period.", Formula = "Renewed ÷ Expiring", Target = "≥ 88%", Warning = "≥ 80%", Critical = "< 80%", Direction = "Higher is better", Frequency = "Monthly", Owner = "Operations VP", IsActive = true },
            new DashboardKpiDefinitionDto { Name = "New Business Premium", Domain = "Sales", Description = "Total written premium from new accounts in the period.", Formula = "SUM(NewPolicies.Premium)", Target = "$500K", Warning = "$400K", Critical = "$300K", Direction = "Higher is better", Frequency = "Monthly", Owner = "Sales Director", IsActive = true },
            new DashboardKpiDefinitionDto { Name = "Loss Ratio", Domain = "Claims", Description = "Incurred losses as a percentage of earned premium.", Formula = "Losses ÷ Earned Premium", Target = "≤ 65%", Warning = "≤ 75%", Critical = "> 75%", Direction = "Lower is better", Frequency = "Monthly", Owner = "Principals", IsActive = true },
            new DashboardKpiDefinitionDto { Name = "AR Days Outstanding", Domain = "Finance", Description = "Average days invoices remain unpaid.", Formula = "AR Balance ÷ Daily Revenue", Target = "≤ 25d", Warning = "≤ 40d", Critical = "> 40d", Direction = "Lower is better", Frequency = "Monthly", Owner = "CFO", IsActive = false }
        };

        const string seedSql = @"
IF NOT EXISTS (SELECT 1 FROM Analytics.DashboardRecord WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0)
BEGIN
    INSERT INTO Analytics.DashboardRecord (DashboardRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted)
    VALUES (NEWID(),@TenantId,@Kind,@Code,@Name,@Status,@JsonData,SYSUTCDATETIME(),0);
END;";

        await cn.ExecuteAsync(new CommandDefinition(seedSql, Record(tenantId, "ExecutiveDashboard", "executive", "Executive Dashboard", "Active", executive), cancellationToken: cancellationToken));
        foreach (var dashboard in dashboards)
            await cn.ExecuteAsync(new CommandDefinition(seedSql, Record(tenantId, "CustomDashboard", Slug(dashboard.Name), dashboard.Name, dashboard.IsDefault ? "Default" : "Active", dashboard), cancellationToken: cancellationToken));
        foreach (var kpi in kpis)
            await cn.ExecuteAsync(new CommandDefinition(seedSql, Record(tenantId, "KpiDefinition", Slug(kpi.Name), kpi.Name, kpi.IsActive ? "Active" : "Inactive", kpi), cancellationToken: cancellationToken));
    }

    private static object Record<T>(Guid tenantId, string kind, string code, string name, string status, T data) => new { TenantId = tenantId, Kind = kind, Code = code, Name = name, Status = status, JsonData = JsonSerializer.Serialize(data, JsonOptions) };
    private static string Slug(string value) => value.Trim().ToLowerInvariant().Replace(" ", "-").Replace("/", "-");

    [HttpGet]
    public async Task<IActionResult> GetKpi([FromQuery] Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsureDashboardDataAsync(tenantId, cancellationToken);
        return Ok(await _service.GetKpiAsync(tenantId, cancellationToken));
    }

    [HttpGet("executive")]
    public async Task<IActionResult> GetExecutive([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureDashboardDataAsync(tenantId, cancellationToken);
        var item = await ReadSingleAsync<ExecutiveDashboardPageDto>(tenantId, "ExecutiveDashboard", "executive", cancellationToken);
        return Ok(item);
    }

    [HttpGet("records/{kind}")]
    public async Task<IActionResult> SearchRecords(string kind, [FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        await EnsureDashboardDataAsync(tenantId, cancellationToken);
        const string sql = @"SELECT DashboardRecordId, JsonData FROM Analytics.DashboardRecord WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0 AND (@SearchTerm IS NULL OR @SearchTerm='' OR Name LIKE '%' + @SearchTerm + '%' OR JsonData LIKE '%' + @SearchTerm + '%') ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await cn.QueryAsync<(Guid DashboardRecordId, string JsonData)>(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind, SearchTerm = searchTerm }, cancellationToken: cancellationToken))).ToList();
        return Ok(new PagedResult<DashboardRecordEnvelope> { Items = rows.Select(r => new DashboardRecordEnvelope { Id = r.DashboardRecordId, JsonData = r.JsonData }).ToList(), TotalCount = rows.Count, PageNumber = 1, PageSize = rows.Count });
    }

    [HttpPost("records")]
    public async Task<IActionResult> CreateRecord([FromBody] UpsertDashboardRecordRequest request, CancellationToken cancellationToken)
    {
        await EnsureDashboardDataAsync(request.TenantId, cancellationToken);
        var id = Guid.NewGuid();
        const string sql = @"INSERT INTO Analytics.DashboardRecord (DashboardRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted) VALUES (@Id,@TenantId,@Kind,@Code,@Name,@Status,@JsonData,SYSUTCDATETIME(),0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.Kind, request.Code, request.Name, request.Status, request.JsonData }, cancellationToken: cancellationToken));
        return Ok(new IdResult { Id = id });
    }

    [HttpPut("records/{id:guid}")]
    public async Task<IActionResult> UpdateRecord(Guid id, [FromBody] UpsertDashboardRecordRequest request, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE Analytics.DashboardRecord SET Code=@Code, Name=@Name, Status=@Status, JsonData=@JsonData, ModifiedDateUtc=SYSUTCDATETIME() WHERE DashboardRecordId=@Id AND TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.Kind, request.Code, request.Name, request.Status, request.JsonData }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpDelete("records/{id:guid}")]
    public async Task<IActionResult> DeleteRecord(Guid id, CancellationToken cancellationToken)
    {
        const string sql = "UPDATE Analytics.DashboardRecord SET IsDeleted=1, ModifiedDateUtc=SYSUTCDATETIME() WHERE DashboardRecordId=@Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    private async Task<T?> ReadSingleAsync<T>(Guid tenantId, string kind, string code, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT TOP 1 JsonData FROM Analytics.DashboardRecord WHERE TenantId=@TenantId AND Kind=@Kind AND Code=@Code AND IsDeleted=0 ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var json = await cn.QuerySingleOrDefaultAsync<string>(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind, Code = code }, cancellationToken: cancellationToken));
        return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    public sealed class DashboardRecordEnvelope
    {
        public Guid Id { get; set; }
        public string JsonData { get; set; } = string.Empty;
    }
}
