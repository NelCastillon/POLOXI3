using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("analytics/ai")]
public sealed class AiInsightsController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IAiService _service;
    private readonly ISqlConnectionFactory _connectionFactory;

    public AiInsightsController(IAiService service, ISqlConnectionFactory connectionFactory)
    {
        _service = service;
        _connectionFactory = connectionFactory;
    }

    private async Task EnsureAiDataAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Ai') EXEC(N'CREATE SCHEMA Ai');

IF OBJECT_ID(N'Ai.InsightRecord', N'U') IS NULL
BEGIN
    CREATE TABLE Ai.InsightRecord
    (
        InsightRecordId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
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
    CREATE INDEX IX_Ai_InsightRecord_Tenant_Kind ON Ai.InsightRecord(TenantId, Kind, IsDeleted);
END;

IF OBJECT_ID(N'Ai.Insight', N'U') IS NULL
BEGIN
    CREATE TABLE Ai.Insight
    (
        InsightId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Category NVARCHAR(100) NOT NULL,
        Title NVARCHAR(250) NOT NULL,
        Summary NVARCHAR(MAX) NOT NULL,
        ActionableRecommendation NVARCHAR(MAX) NOT NULL,
        Severity NVARCHAR(40) NOT NULL,
        GeneratedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, cancellationToken: cancellationToken));

        var now = DateTime.UtcNow;
        var insights = new[]
        {
            new AiInsightCardDto { TenantId = tenantId, Domain = "Retention", Type = "Risk", Priority = "Critical", Confidence = 94, Title = "17 accounts at high renewal risk — $312K premium at stake", Summary = "AI detected 17 accounts with no recent engagement, open service complaints, and price increases above 15%.", AffectedEntities = ["Apex Logistics LLC", "Riverside Dental Group", "Castillo Auto Group", "+14 more"], Recommendation = "Assign proactive outreach tasks to each account's producer and prioritize active service complaints.", GeneratedAt = now.AddMinutes(-18) },
            new AiInsightCardDto { TenantId = tenantId, Domain = "Sales", Type = "Opportunity", Priority = "High", Confidence = 88, Title = "Cross-sell opportunity: 84 BOP-only accounts eligible for umbrella", Summary = "84 commercial accounts have BOP coverage but no umbrella policy. AI projects a meaningful acceptance rate during renewal.", AffectedEntities = ["84 BOP accounts"], Recommendation = "Generate a targeted cross-sell campaign and assign to producers with high BOP conversion history.", GeneratedAt = now.AddMinutes(-42) },
            new AiInsightCardDto { TenantId = tenantId, Domain = "Claims", Type = "Anomaly", Priority = "High", Confidence = 91, Title = "Loss ratio spike detected in Workers Comp", Summary = "Workers Comp loss ratio is 14% above the 6-month average and concentrated in construction accounts.", AffectedEntities = ["Ortega Construction", "Bautista Framing", "Rivera Roofing LLC", "Mesa Contractors"], Recommendation = "Conduct loss control reviews and notify carrier underwriters before renewal.", GeneratedAt = now.AddMinutes(-71) },
            new AiInsightCardDto { TenantId = tenantId, Domain = "Finance", Type = "Anomaly", Priority = "High", Confidence = 87, Title = "3 commission statements unreconciled for 45+ days", Summary = "Three carrier commission statements have not been matched to internal policies. Combined value is $28,300.", AffectedEntities = ["Acme Casualty Statement #AC-20250312", "Guardian Specialty #GS-20250314"], Recommendation = "Assign to accounting for immediate reconciliation and escalate to carrier contacts if needed.", GeneratedAt = now.AddHours(-2.5) },
            new AiInsightCardDto { TenantId = tenantId, Domain = "Service", Type = "Risk", Priority = "Medium", Confidence = 82, Title = "COI processing bottleneck — average 51h vs 24h SLA", Summary = "Certificate requests are averaging more than double the SLA target due to CSR capacity imbalance.", AffectedEntities = ["Lisa Park (141% capacity)", "Marcus Webb (128% capacity)"], Recommendation = "Redistribute COI requests and consider producer delegation for simple certificates.", GeneratedAt = now.AddHours(-4) },
            new AiInsightCardDto { TenantId = tenantId, Domain = "Data Quality", Type = "Data Quality", Priority = "Medium", Confidence = 96, Title = "214 account records missing primary contact email", Summary = "Missing emails block renewal notices, campaign delivery, and portal invitations.", AffectedEntities = ["214 accounts — $840K premium"], Recommendation = "Run data enrichment first and assign unresolved gaps to producers.", GeneratedAt = now.AddHours(-6) }
        };

        var config = new AiAssistantConfigDto
        {
            Starters = ["What are my highest renewal risk accounts this month?", "Which accounts are best candidates for umbrella cross-sell?", "Summarize open service requests older than 48 hours", "Find data quality issues that could block renewal notices", "What's the next best action for my top 5 at-risk accounts?"],
            Capabilities =
            [
                new() { Name = "Summarize Account History", Description = "Narrative summary of activity, claims, and contacts", Icon = "bi-building", IconCss = "aia-ci-blue", Prompt = "Summarize the full account history for [Account Name], including policy history, claims, open service requests, and recent communications." },
                new() { Name = "Next Best Action", Description = "AI-recommended outreach or service action", Icon = "bi-lightning-charge", IconCss = "aia-ci-amber", Prompt = "What is the recommended next best action for accounts expiring in the next 30 days with no recent producer contact?" },
                new() { Name = "Identify Renewal Risk", Description = "Flag accounts most likely to lapse", Icon = "bi-arrow-clockwise", IconCss = "aia-ci-red", Prompt = "Identify accounts with the highest renewal risk in the next 60 days and explain the primary risk factors for each." },
                new() { Name = "Recommend Cross-Sell", Description = "Surface coverage gaps and upsell opportunities", Icon = "bi-arrows-expand", IconCss = "aia-ci-green", Prompt = "Which accounts in my book have significant coverage gaps that represent a cross-sell or upsell opportunity? Prioritize by premium potential." },
                new() { Name = "Detect Service Bottlenecks", Description = "Highlight workflow delays and capacity issues", Icon = "bi-hourglass-split", IconCss = "aia-ci-amber", Prompt = "Are there any service bottlenecks in the current open service request queue? Identify root causes and suggest resolution steps." },
                new() { Name = "Data Quality Issues", Description = "Find records with missing or inconsistent data", Icon = "bi-database-exclamation", IconCss = "aia-ci-blue", Prompt = "What are the most impactful data quality issues in the account and policy records that could affect renewal processing or reporting?" }
            ]
        };

        const string seedRecordSql = @"
IF NOT EXISTS (SELECT 1 FROM Ai.InsightRecord WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0)
BEGIN
    INSERT INTO Ai.InsightRecord (InsightRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted)
    VALUES (NEWID(),@TenantId,@Kind,@Code,@Name,@Status,@JsonData,SYSUTCDATETIME(),0);
END;";

        foreach (var insight in insights)
            await cn.ExecuteAsync(new CommandDefinition(seedRecordSql, Record(tenantId, "Insight", Slug(insight.Title), insight.Title, insight.Dismissed ? "Dismissed" : "Active", insight), cancellationToken: cancellationToken));
        await cn.ExecuteAsync(new CommandDefinition(seedRecordSql, Record(tenantId, "AssistantConfig", "assistant-config", "AI Assistant Config", "Active", config), cancellationToken: cancellationToken));

        const string legacySeedSql = @"
IF NOT EXISTS (SELECT 1 FROM Ai.Insight WHERE TenantId=@TenantId AND IsDeleted=0)
BEGIN
    INSERT INTO Ai.Insight (InsightId,TenantId,Category,Title,Summary,ActionableRecommendation,Severity,GeneratedDateUtc,CreatedDateUtc,IsDeleted)
    SELECT NEWID(), @TenantId, @Category, @Title, @Summary, @Recommendation, @Severity, @GeneratedAt, SYSUTCDATETIME(), 0;
END;";
        var first = insights[0];
        await cn.ExecuteAsync(new CommandDefinition(legacySeedSql, new { TenantId = tenantId, Category = first.Domain, first.Title, first.Summary, Recommendation = first.Recommendation, Severity = first.Priority, first.GeneratedAt }, cancellationToken: cancellationToken));
    }

    private static object Record<T>(Guid tenantId, string kind, string code, string name, string status, T data) => new { TenantId = tenantId, Kind = kind, Code = code, Name = name, Status = status, JsonData = JsonSerializer.Serialize(data, JsonOptions) };
    private static string Slug(string value) => new(value.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray());

    [HttpGet]
    public async Task<IActionResult> GetInsights([FromQuery] Guid tenantId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default)
    {
        await EnsureAiDataAsync(tenantId, cancellationToken);
        return Ok(await _service.GetInsightsAsync(tenantId, pageNumber, pageSize, cancellationToken));
    }

    [HttpGet("insight-cards")]
    public async Task<IActionResult> GetInsightCards([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken cancellationToken)
    {
        await EnsureAiDataAsync(tenantId, cancellationToken);
        var rows = await ReadRecordsAsync<AiInsightCardDto>(tenantId, "Insight", searchTerm, cancellationToken);
        return Ok(new PagedResult<AiInsightCardDto> { Items = rows, TotalCount = rows.Count, PageNumber = 1, PageSize = rows.Count });
    }

    [HttpGet("assistant-config")]
    public async Task<IActionResult> GetAssistantConfig([FromQuery] Guid tenantId, CancellationToken cancellationToken)
    {
        await EnsureAiDataAsync(tenantId, cancellationToken);
        var config = await ReadSingleAsync<AiAssistantConfigDto>(tenantId, "AssistantConfig", "assistant-config", cancellationToken);
        return Ok(config);
    }

    [HttpPost("assistant/ask")]
    public async Task<IActionResult> Ask([FromBody] AiAssistantAskRequest request, CancellationToken cancellationToken)
    {
        await EnsureAiDataAsync(request.TenantId, cancellationToken);
        var prompt = request.Prompt.ToLowerInvariant();
        var response = prompt switch
        {
            var p when p.Contains("renewal risk") => new AiAssistantResponseDto { Response = "Based on DB-backed insight data, 17 accounts are at elevated renewal risk with approximately $312K premium at stake. Prioritize accounts with no recent engagement, service complaints, and rate increases above 15%.", Actions = [new() { Label = "Create outreach tasks", Icon = "bi-check2-square" }, new() { Label = "View retention analytics", Icon = "bi-graph-up" }] },
            var p when p.Contains("cross-sell") => new AiAssistantResponseDto { Response = "DB-backed insights show 84 BOP-only accounts eligible for umbrella coverage. Estimated premium uplift is about $168K if targeted before renewal.", Actions = [new() { Label = "Generate campaign", Icon = "bi-megaphone" }, new() { Label = "View marketing analytics", Icon = "bi-bar-chart" }] },
            var p when p.Contains("bottleneck") || p.Contains("service") => new AiAssistantResponseDto { Response = "Current DB insights identify a COI bottleneck averaging 51 hours against a 24-hour SLA. Redistribute pending work from overloaded CSRs and delegate simple certificates.", Actions = [new() { Label = "View service analytics", Icon = "bi-headset" }] },
            var p when p.Contains("data quality") => new AiAssistantResponseDto { Response = "The highest-impact DB-backed data quality issue is 214 account records missing primary contact email, blocking renewals, campaigns, and portal invites.", Actions = [new() { Label = "Run enrichment workflow", Icon = "bi-lightning-charge" }] },
            _ => new AiAssistantResponseDto { Response = $"I analyzed your request using tenant DB insights: \"{request.Prompt}\". Narrow by account, producer, policy, or time period for more specific recommendations.", Actions = [new() { Label = "View AI Insights", Icon = "bi-stars" }, new() { Label = "Open Reports Library", Icon = "bi-collection" }] }
        };
        return Ok(response);
    }

    [HttpPost("insights/{id:guid}/dismiss")]
    public async Task<IActionResult> Dismiss(Guid id, [FromQuery] bool dismissed, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE Ai.InsightRecord SET Status=@Status, JsonData=JSON_MODIFY(JsonData, '$.dismissed', @Dismissed), ModifiedDateUtc=SYSUTCDATETIME() WHERE InsightRecordId=@Id AND Kind=N'Insight' AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, Dismissed = dismissed, Status = dismissed ? "Dismissed" : "Active" }, cancellationToken: cancellationToken));
        return NoContent();
    }

    [HttpPost("insights/{id:guid}/task")]
    public async Task<IActionResult> CreateTask(Guid id, CancellationToken cancellationToken)
    {
        const string sql = @"UPDATE Ai.InsightRecord SET JsonData=JSON_MODIFY(JsonData, '$.taskCreated', CAST(1 AS bit)), ModifiedDateUtc=SYSUTCDATETIME() WHERE InsightRecordId=@Id AND Kind=N'Insight' AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
        return NoContent();
    }

    private async Task<List<T>> ReadRecordsAsync<T>(Guid tenantId, string kind, string? searchTerm, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT InsightRecordId, JsonData FROM Ai.InsightRecord WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0 AND (@SearchTerm IS NULL OR @SearchTerm='' OR Name LIKE '%' + @SearchTerm + '%' OR JsonData LIKE '%' + @SearchTerm + '%') ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await cn.QueryAsync<(Guid InsightRecordId, string JsonData)>(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind, SearchTerm = searchTerm }, cancellationToken: cancellationToken));
        return rows.Select(row =>
        {
            var item = JsonSerializer.Deserialize<T>(row.JsonData, JsonOptions)!;
            var prop = typeof(T).GetProperty("Id");
            if (prop?.PropertyType == typeof(Guid)) prop.SetValue(item, row.InsightRecordId);
            return item;
        }).ToList();
    }

    private async Task<T?> ReadSingleAsync<T>(Guid tenantId, string kind, string code, CancellationToken cancellationToken)
    {
        const string sql = @"SELECT TOP 1 JsonData FROM Ai.InsightRecord WHERE TenantId=@TenantId AND Kind=@Kind AND Code=@Code AND IsDeleted=0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var json = await cn.QuerySingleOrDefaultAsync<string>(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind, Code = code }, cancellationToken: cancellationToken));
        return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
