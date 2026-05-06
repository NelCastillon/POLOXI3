using System.Text.Json;
using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;
using Microsoft.AspNetCore.Mvc;

namespace Ams.Api.Controllers;

[ApiController]
[Route("api/portal-admin")]
public sealed class PortalAdminController : ControllerBase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISqlConnectionFactory _connectionFactory;

    public PortalAdminController(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    [HttpGet("records")]
    public async Task<IActionResult> SearchRecords([FromQuery] Guid tenantId, [FromQuery] string kind, [FromQuery] string? searchTerm, CancellationToken ct)
    {
        const string sql = @"
SELECT PortalAdminRecordId, TenantId, Kind, Code, Name, Status, JsonData, CreatedDateUtc, ModifiedDateUtc
FROM Portal.AdminRecord
WHERE TenantId = @TenantId AND Kind = @Kind AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Name LIKE '%' + @SearchTerm + '%' OR Code LIKE '%' + @SearchTerm + '%' OR JsonData LIKE '%' + @SearchTerm + '%')
ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var items = (await cn.QueryAsync<PortalAdminRecordDto>(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind, SearchTerm = searchTerm }, cancellationToken: ct))).AsList();
        return Ok(new PagedResult<PortalAdminRecordDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken ct)
        => Ok(await ReadJsonRecordsAsync<PortalAdminUserDto>(tenantId, "PortalUser", searchTerm, ct));

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken ct)
        => Ok(await ReadJsonRecordsAsync<PortalAdminRequestDto>(tenantId, "SelfServiceRequest", searchTerm, ct));

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken ct)
        => Ok(await ReadJsonRecordsAsync<PortalAdminDocumentDto>(tenantId, "PortalDocument", searchTerm, ct));

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken ct)
        => Ok(await ReadJsonRecordsAsync<PortalAdminActivityDto>(tenantId, "PortalActivity", searchTerm, ct));

    [HttpGet("capabilities")]
    public async Task<IActionResult> GetCapabilities([FromQuery] Guid tenantId, CancellationToken ct)
        => Ok(await ReadJsonRecordsAsync<PortalCapabilityDto>(tenantId, "PortalCapability", null, ct));

    [HttpGet("branding")]
    public async Task<IActionResult> GetBranding([FromQuery] Guid tenantId, CancellationToken ct)
        => Ok(await ReadSingleJsonRecordAsync<PortalBrandingSettingsDto>(tenantId, "PortalBranding", "branding", ct));

    [HttpGet("mobile")]
    public async Task<IActionResult> GetMobile([FromQuery] Guid tenantId, CancellationToken ct)
        => Ok(await ReadSingleJsonRecordAsync<PortalMobileSettingsDto>(tenantId, "PortalMobile", "mobile", ct));

    [HttpGet("my-account")]
    public async Task<IActionResult> GetMyAccount([FromQuery] Guid tenantId, CancellationToken ct)
        => Ok(await ReadSingleJsonRecordAsync<PortalMyAccountDto>(tenantId, "PortalMyAccount", "my-account", ct));

    [HttpGet("metrics/{kind}")]
    public async Task<IActionResult> GetMetrics(string kind, [FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken ct)
        => Ok(await ReadJsonRecordsAsync<PortalMetricRecordDto>(tenantId, kind, searchTerm, ct));

    [HttpPut("my-account")]
    public async Task<IActionResult> UpdateMyAccount([FromQuery] Guid tenantId, [FromBody] PortalMyAccountDto account, CancellationToken ct)
    {
        account.TenantId = tenantId;
        var json = JsonSerializer.Serialize(account, JsonOptions);
        const string sql = @"
UPDATE Portal.AdminRecord
SET Name = @Name,
    Status = @Status,
    JsonData = @JsonData,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND Kind = N'PortalMyAccount' AND Code = N'my-account' AND IsDeleted = 0;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO Portal.AdminRecord (PortalAdminRecordId, TenantId, Kind, Code, Name, Status, JsonData, CreatedDateUtc, IsDeleted)
    VALUES (NEWID(), @TenantId, N'PortalMyAccount', N'my-account', @Name, @Status, @JsonData, SYSUTCDATETIME(), 0);
END";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            Name = account.AgencyName,
            Status = account.PlanStatus,
            JsonData = json
        }, cancellationToken: ct));
        return NoContent();
    }

    [HttpPost("records")]
    public async Task<IActionResult> CreateRecord([FromBody] UpsertPortalAdminRecordRequest request, CancellationToken ct)
    {
        var id = Guid.NewGuid();
        const string sql = @"
INSERT INTO Portal.AdminRecord (PortalAdminRecordId, TenantId, Kind, Code, Name, Status, JsonData, CreatedDateUtc, IsDeleted)
VALUES (@Id, @TenantId, @Kind, @Code, @Name, @Status, @JsonData, SYSUTCDATETIME(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.Kind, request.Code, request.Name, request.Status, request.JsonData }, cancellationToken: ct));
        return Ok(new IdResult { Id = id });
    }

    [HttpPut("records/{id:guid}")]
    public async Task<IActionResult> UpdateRecord(Guid id, [FromBody] UpsertPortalAdminRecordRequest request, CancellationToken ct)
    {
        const string sql = @"
UPDATE Portal.AdminRecord
SET Code = @Code, Name = @Name, Status = @Status, JsonData = @JsonData, ModifiedDateUtc = SYSUTCDATETIME()
WHERE PortalAdminRecordId = @Id AND TenantId = @TenantId AND Kind = @Kind AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.Kind, request.Code, request.Name, request.Status, request.JsonData }, cancellationToken: ct));
        return NoContent();
    }

    [HttpPost("records/{id:guid}/status")]
    public async Task<IActionResult> SetStatus(Guid id, [FromQuery] string status, CancellationToken ct)
    {
        const string sql = @"
UPDATE Portal.AdminRecord
SET Status = @Status,
    JsonData = CASE WHEN ISJSON(JsonData) = 1 THEN JSON_MODIFY(JsonData, '$.status', @Status) ELSE JsonData END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE PortalAdminRecordId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, Status = status }, cancellationToken: ct));
        return NoContent();
    }

    [HttpDelete("records/{id:guid}")]
    public async Task<IActionResult> DeleteRecord(Guid id, CancellationToken ct)
    {
        const string sql = "UPDATE Portal.AdminRecord SET IsDeleted = 1, ModifiedDateUtc = SYSUTCDATETIME() WHERE PortalAdminRecordId = @Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id }, cancellationToken: ct));
        return NoContent();
    }

    private async Task<PagedResult<T>> ReadJsonRecordsAsync<T>(Guid tenantId, string kind, string? searchTerm, CancellationToken ct)
    {
        const string sql = @"
SELECT PortalAdminRecordId, JsonData
FROM Portal.AdminRecord
WHERE TenantId = @TenantId AND Kind = @Kind AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR Name LIKE '%' + @SearchTerm + '%' OR JsonData LIKE '%' + @SearchTerm + '%')
ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var rows = await cn.QueryAsync<(Guid PortalAdminRecordId, string JsonData)>(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind, SearchTerm = searchTerm }, cancellationToken: ct));
        var items = rows.Select(row =>
        {
            var item = JsonSerializer.Deserialize<T>(row.JsonData, JsonOptions)!;
            var idProperty = typeof(T).GetProperty("Id");
            if (idProperty?.PropertyType == typeof(Guid)) idProperty.SetValue(item, row.PortalAdminRecordId);
            return item;
        }).ToList();
        return new PagedResult<T> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count };
    }

    private async Task<T?> ReadSingleJsonRecordAsync<T>(Guid tenantId, string kind, string code, CancellationToken ct)
    {
        const string sql = @"
SELECT TOP 1 JsonData
FROM Portal.AdminRecord
WHERE TenantId = @TenantId AND Kind = @Kind AND Code = @Code AND IsDeleted = 0
ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var json = await cn.QuerySingleOrDefaultAsync<string>(new CommandDefinition(sql, new { TenantId = tenantId, Kind = kind, Code = code }, cancellationToken: ct));
        return string.IsNullOrWhiteSpace(json) ? default : JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}
