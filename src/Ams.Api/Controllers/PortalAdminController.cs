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

    private async Task EnsurePortalAdminDataAsync(Guid tenantId, CancellationToken ct)
    {
        const string schemaSql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Portal') EXEC(N'CREATE SCHEMA Portal');

IF OBJECT_ID(N'Portal.AdminRecord', N'U') IS NULL
BEGIN
    CREATE TABLE Portal.AdminRecord
    (
        PortalAdminRecordId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
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
    CREATE INDEX IX_Portal_AdminRecord_Tenant_Kind ON Portal.AdminRecord(TenantId, Kind, IsDeleted);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(schemaSql, cancellationToken: ct));

        var now = DateTime.UtcNow;
        var tenant = await GetTenantPortalDefaultsAsync(cn, tenantId, ct);
        var seeds = new List<UpsertPortalAdminRecordRequest>
        {
            Record(tenantId, "PortalUser", "rachel.chen@example.com", "Rachel Chen", "Active", new PortalAdminUserDto { Name = "Rachel Chen", Email = "rachel.chen@example.com", AccountName = "Chen Family", Role = "Policyholder", Status = "Active", LastLogin = now.AddDays(-1), MfaEnabled = true, Logins30d = 14 }),
            Record(tenantId, "PortalUser", "marcus.webb@example.com", "Marcus Webb", "Active", new PortalAdminUserDto { Name = "Marcus Webb", Email = "marcus.webb@example.com", AccountName = "Webb Holdings LLC", Role = "Admin", Status = "Active", LastLogin = now.AddDays(-3), MfaEnabled = true, Logins30d = 9 }),
            Record(tenantId, "PortalUser", "pamela.torres@example.com", "Pamela Torres", "Pending", new PortalAdminUserDto { Name = "Pamela Torres", Email = "pamela.torres@example.com", AccountName = "Torres Household", Role = "Contact", Status = "Pending", LastLogin = DateTime.MinValue, MfaEnabled = false, Logins30d = 0 }),
            Record(tenantId, "PortalUser", "david.kim@example.com", "David Kim", "Suspended", new PortalAdminUserDto { Name = "David Kim", Email = "david.kim@example.com", AccountName = "Kim Dental Group", Role = "Policyholder", Status = "Suspended", LastLogin = now.AddDays(-42), MfaEnabled = false, Logins30d = 0 }),

            Record(tenantId, "SelfServiceRequest", "coi-riverside", "COI request - Riverside Construction", "Open", new PortalAdminRequestDto { SubmittedAt = now.AddHours(-3), ClientName = "Beth Owens", AccountName = "Riverside Construction LLC", RequestType = "COI Request", Summary = "Needs certificate for project owner by Friday.", Priority = "Urgent", AssignedTo = "—", Status = "Open" }),
            Record(tenantId, "SelfServiceRequest", "policy-change-chen", "Policy change - Chen Family", "In Progress", new PortalAdminRequestDto { SubmittedAt = now.AddHours(-8), ClientName = "Rachel Chen", AccountName = "Chen Family", RequestType = "Policy Change", Summary = "Add teen driver to auto policy.", Priority = "Normal", AssignedTo = "Mia Santos", Status = "In Progress" }),
            Record(tenantId, "SelfServiceRequest", "docs-sato", "Document upload - Sato Tech", "Fulfilled", new PortalAdminRequestDto { SubmittedAt = now.AddDays(-1), ClientName = "Ken Sato", AccountName = "Sato Tech LLC", RequestType = "Document Upload", Summary = "Uploaded signed cyber questionnaire.", Priority = "Low", AssignedTo = "Jordan Lee", Status = "Fulfilled" }),

            Record(tenantId, "PortalDocument", "bop-riverside-2026", "Riverside BOP Policy", "Shared", new PortalAdminDocumentDto { Name = "Riverside BOP Policy", AccountName = "Riverside Construction LLC", Category = "Policy", FileType = "PDF", FileSizeKb = 842, Visibility = "Shared", SharedAt = now.AddDays(-7), ViewCount = 18, DownloadCount = 6 }),
            Record(tenantId, "PortalDocument", "coi-chen-hoa", "Chen HOA Certificate", "Shared", new PortalAdminDocumentDto { Name = "Chen HOA Certificate", AccountName = "Chen Family", Category = "Certificate", FileType = "PDF", FileSizeKb = 224, Visibility = "Shared", SharedAt = now.AddDays(-2), ViewCount = 7, DownloadCount = 4 }),
            Record(tenantId, "PortalDocument", "auto-id-torres", "Torres Auto ID Cards", "Shared", new PortalAdminDocumentDto { Name = "Torres Auto ID Cards", AccountName = "Torres Household", Category = "ID Card", FileType = "PDF", FileSizeKb = 128, Visibility = "Shared", SharedAt = now.AddDays(-12), ViewCount = 11, DownloadCount = 9 }),

            Record(tenantId, "PortalActivity", "login-rachel", "Rachel Chen login", "Info", new PortalAdminActivityDto { OccurredAt = now.AddMinutes(-22), UserName = "Rachel Chen", UserEmail = "rachel.chen@example.com", AccountName = "Chen Family", EventType = "Login", Detail = "Successful portal login", Severity = "Info", IpAddress = "72.14.20.18" }),
            Record(tenantId, "PortalActivity", "coi-submit-riverside", "COI submitted", "Info", new PortalAdminActivityDto { OccurredAt = now.AddHours(-3), UserName = "Beth Owens", UserEmail = "beth@riverside.example", AccountName = "Riverside Construction LLC", EventType = "Request Submitted", Detail = "Submitted COI request", Severity = "Info", IpAddress = "24.18.42.8" }),
            Record(tenantId, "PortalActivity", "failed-login-kim", "Failed login", "Warning", new PortalAdminActivityDto { OccurredAt = now.AddHours(-5), UserName = "David Kim", UserEmail = "david.kim@example.com", AccountName = "Kim Dental Group", EventType = "Login", Detail = "Failed login after account suspension", Severity = "Warning", IpAddress = "104.44.12.9" }),

            Record(tenantId, "PortalCapability", "coi", "Request COI", "Active", new PortalCapabilityDto { Name = "Request COI", Description = "Allow clients to request certificates of insurance.", Icon = "bi-file-earmark-text", IconCss = "pwl-cap-blue", Category = "Documents", Enabled = true, RequiresApproval = true, MfaRequired = false, AuditLog = true }),
            Record(tenantId, "PortalCapability", "policy-change", "Request Policy Change", "Active", new PortalCapabilityDto { Name = "Request Policy Change", Description = "Allow policy change requests from the portal.", Icon = "bi-pencil-square", IconCss = "pwl-cap-green", Category = "Service", Enabled = true, RequiresApproval = true, MfaRequired = true, AuditLog = true }),
            Record(tenantId, "PortalCapability", "pay-invoice", "Pay Invoice", "Active", new PortalCapabilityDto { Name = "Pay Invoice", Description = "Enable portal invoice payments.", Icon = "bi-credit-card", IconCss = "pwl-cap-amber", Category = "Billing", Enabled = true, RequiresApproval = false, MfaRequired = true, AuditLog = true }),
            Record(tenantId, "PortalCapability", "claim-intake", "Claim Intake (FNOL)", "Draft", new PortalCapabilityDto { Name = "Claim Intake (FNOL)", Description = "Allow clients to start claim intake from the portal.", Icon = "bi-exclamation-circle", IconCss = "pwl-cap-red", Category = "Claims", Enabled = false, RequiresApproval = true, MfaRequired = true, AuditLog = true }),

            Record(tenantId, "PortalBranding", "branding", tenant.BrandingDisplayName, "Active", new PortalBrandingSettingsDto { DisplayName = tenant.BrandingDisplayName, Domain = tenant.PortalDomain, SupportEmail = tenant.SupportEmail, SupportPhone = tenant.SupportPhone, WelcomeMessage = $"Manage policies, request certificates, upload documents, and message {tenant.AgencyName} in one secure place.", PrimaryColor = "#1d4ed8", AccentColor = "#059669", NavBg = "#1e293b", NavText = "#f8fafc", EmailFromName = tenant.AgencyName, EmailReplyTo = tenant.SupportEmail, EmailFooter = $"{tenant.AgencyName} · Client Portal Support · {tenant.SupportPhone}", ShowAgencyLogo = true, ShowPoweredBy = false, ShowSupportChat = true, ShowNewsWidget = true }),
            Record(tenantId, "PortalMobile", "mobile", $"{tenant.AgencyName} Mobile", "Published", new PortalMobileSettingsDto { AppName = $"{tenant.AgencyName} Mobile", IosUrl = "", AndroidUrl = "", BundleId = tenant.MobileBundleId, AppVersion = "2.4.1", BiometricLogin = true, ForceAppLock = true, LockTimeoutMinutes = 15, RequireMfaOnMobile = true, Notifications = [new() { Name = "Policy Alerts", Description = "Renewals, changes, and document updates", Enabled = true }, new() { Name = "Billing Reminders", Description = "Invoice and payment reminders", Enabled = true }, new() { Name = "Claim Updates", Description = "Claims status notifications", Enabled = false }], Features = [new() { Name = "Policies", Icon = "bi-shield-check", IconCss = "pm-fi-blue", Enabled = true }, new() { Name = "Documents", Icon = "bi-folder2", IconCss = "pm-fi-green", Enabled = true }, new() { Name = "Payments", Icon = "bi-credit-card", IconCss = "pm-fi-amber", Enabled = true }, new() { Name = "Messages", Icon = "bi-chat-dots", IconCss = "pm-fi-purple", Enabled = true }] }),
            Record(tenantId, "PortalMyAccount", "my-account", tenant.AgencyName, "Active", new PortalMyAccountDto { TenantId = tenantId, AgencyName = tenant.AgencyName, AdminName = "Tenant Admin", AdminEmail = tenant.AdminEmail, AdminRole = "Tenant Admin", AdminPhone = tenant.SupportPhone, TimeZone = tenant.TimeZoneId, Locale = tenant.Locale, PlanName = "Enterprise", PlanStatus = "Active", RenewalDateUtc = now.AddMonths(8), PortalUsers = 0, ActivePortalUsers = 0, PendingInvites = 0, OpenRequests = 0, UrgentRequests = 0, SharedDocuments = 0, StorageUsedGb = 0, StorageLimitGb = 250, MonthlyLoginCount = 0, MobileInstalls = 0, ChatSessions30d = 0, ApiCalls30d = 0, LastPortalPublishUtc = now.AddDays(-4), LastAdminLoginUtc = now.AddHours(-2), MfaEnabled = true, SsoEnabled = false, BrandingPublished = true, MobileAppPublished = true, ChatEnabled = true, SupportEmail = tenant.SupportEmail, SupportPhone = tenant.SupportPhone, PortalDomain = tenant.PortalDomain, HealthChecks = [new() { Name = "Portal availability", Status = "Healthy", Detail = "All systems operational", Icon = "bi-check-circle" }, new() { Name = "Branding", Status = "Healthy", Detail = $"{tenant.AgencyName} branding is published", Icon = "bi-palette" }, new() { Name = "Mobile app", Status = "Healthy", Detail = "Mobile settings are configured", Icon = "bi-phone" }], RecentActivity = [] }),

            Record(tenantId, "PortalMobileInstall", "install-rachel-ios", "Rachel Chen", "Active", new PortalMetricRecordDto { Name = "Chen Family", Category = "iOS", Status = "Active", Owner = "Rachel Chen", Detail = "iPhone 15 · v2.4.1", EventDateUtc = now.AddHours(-6), Count = 1 }),
            Record(tenantId, "PortalMobileInstall", "install-webb-android", "Marcus Webb", "Active", new PortalMetricRecordDto { Name = "Webb Holdings LLC", Category = "Android", Status = "Active", Owner = "Marcus Webb", Detail = "Pixel 8 · v2.4.0", EventDateUtc = now.AddDays(-1), Count = 1 }),
            Record(tenantId, "PortalChatSession", "chat-rachel-coi", "Rachel Chen", "AI Resolved", new PortalMetricRecordDto { Name = "Rachel Chen", Category = "COI Request", Status = "AI Resolved", Owner = "Aria", Detail = "Guided client to certificate request flow", EventDateUtc = now.AddHours(-2), Count = 12 }),
            Record(tenantId, "PortalChatSession", "chat-riverside-billing", "Riverside Construction", "Live Handoff", new PortalMetricRecordDto { Name = "Riverside Construction", Category = "Billing", Status = "Live Handoff", Owner = "Mia Santos", Detail = "Transferred to billing team", EventDateUtc = now.AddHours(-9), Count = 18 }),
            Record(tenantId, "PortalApiUsage", "api-documents", "GET /portal/documents", "Successful", new PortalMetricRecordDto { Name = "GET /portal/documents", Category = "Client Portal", Status = "Successful", Owner = "portal-web", Detail = "Document center list endpoint", EventDateUtc = now.AddMinutes(-12), Count = 48200 }),
            Record(tenantId, "PortalApiUsage", "api-requests", "POST /portal/requests", "Successful", new PortalMetricRecordDto { Name = "POST /portal/requests", Category = "Self-Service", Status = "Successful", Owner = "portal-web", Detail = "Request submission endpoint", EventDateUtc = now.AddHours(-3), Count = 2210 }),
            Record(tenantId, "PortalApiUsage", "api-auth-warning", "POST /portal/auth", "Warning", new PortalMetricRecordDto { Name = "POST /portal/auth", Category = "Authentication", Status = "Warning", Owner = "portal-auth", Detail = "Elevated failed login attempts detected", EventDateUtc = now.AddHours(-5), Count = 37 })
        };

        const string seedSql = @"
IF NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId=@TenantId AND Kind=@Kind AND IsDeleted=0)
BEGIN
    INSERT INTO Portal.AdminRecord (PortalAdminRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted)
    VALUES (NEWID(),@TenantId,@Kind,@Code,@Name,@Status,@JsonData,SYSUTCDATETIME(),0);
END;";

        foreach (var seed in seeds)
        {
            await cn.ExecuteAsync(new CommandDefinition(seedSql, seed, cancellationToken: ct));
        }

        await SyncTenantPortalIdentityAsync(cn, tenantId, tenant, ct);
    }

    private static UpsertPortalAdminRecordRequest Record<T>(Guid tenantId, string kind, string code, string name, string status, T data) =>
        new(tenantId, kind, code, name, status, JsonSerializer.Serialize(data, JsonOptions));

    private sealed record TenantPortalDefaults(string AgencyName, string Locale, string TimeZoneId, string SupportEmail, string SupportPhone, string PortalDomain, string BrandingDisplayName, string MobileBundleId, string AdminEmail);

    private static async Task<TenantPortalDefaults> GetTenantPortalDefaultsAsync(System.Data.IDbConnection cn, Guid tenantId, CancellationToken ct)
    {
        const string sql = @"
SELECT TOP 1
       COALESCE(NULLIF(t.TenantName, N''), N'Tenant Agency') AS AgencyName,
       COALESCE(NULLIF(t.Locale, N''), N'en-US') AS Locale,
       COALESCE(NULLIF(t.TimeZoneId, N''), N'Central') AS TimeZoneId,
       COALESCE(NULLIF(p.ContactEmail, N''), N'admin@agency.local') AS SupportEmail,
       COALESCE(NULLIF(p.ContactPhone, N''), N'(555) 000-0000') AS SupportPhone
FROM Core.Tenant t
LEFT JOIN Agency.Profile p ON p.TenantId = t.TenantId AND p.IsDeleted = 0
WHERE t.TenantId = @TenantId AND t.IsDeleted = 0;";
        var row = await cn.QuerySingleOrDefaultAsync<(string AgencyName, string Locale, string TimeZoneId, string SupportEmail, string SupportPhone)>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        var agencyName = string.IsNullOrWhiteSpace(row.AgencyName) ? "Tenant Agency" : row.AgencyName;
        var slug = new string(agencyName.ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray()).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal)) slug = slug.Replace("--", "-", StringComparison.Ordinal);
        if (string.IsNullOrWhiteSpace(slug)) slug = "tenant-agency";
        return new TenantPortalDefaults(agencyName, row.Locale, row.TimeZoneId, row.SupportEmail, row.SupportPhone, $"portal.{slug}.com", $"{agencyName} Client Portal", $"com.{slug.Replace("-", "", StringComparison.Ordinal)}.client", row.SupportEmail);
    }

    private static async Task SyncTenantPortalIdentityAsync(System.Data.IDbConnection cn, Guid tenantId, TenantPortalDefaults tenant, CancellationToken ct)
    {
        const string sql = @"
UPDATE Portal.AdminRecord
SET Name = @BrandingDisplayName,
    JsonData = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JsonData,
        '$.displayName', @BrandingDisplayName),
        '$.domain', @PortalDomain),
        '$.supportEmail', @SupportEmail),
        '$.supportPhone', @SupportPhone),
        '$.emailFromName', @AgencyName),
        '$.emailReplyTo', @SupportEmail),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND Kind = N'PortalBranding' AND Code = N'branding' AND IsDeleted = 0 AND ISJSON(JsonData) = 1;

UPDATE Portal.AdminRecord
SET Name = @AgencyName,
    JsonData = JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JSON_MODIFY(JsonData,
        '$.agencyName', @AgencyName),
        '$.adminName', N'Tenant Admin'),
        '$.adminEmail', @AdminEmail),
        '$.adminRole', N'Tenant Admin'),
        '$.supportEmail', @SupportEmail),
        '$.supportPhone', @SupportPhone),
        '$.portalDomain', @PortalDomain),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND Kind = N'PortalMyAccount' AND Code = N'my-account' AND IsDeleted = 0 AND ISJSON(JsonData) = 1;

UPDATE Portal.AdminRecord
SET Name = CONCAT(@AgencyName, N' Mobile'),
    JsonData = JSON_MODIFY(JSON_MODIFY(JsonData,
        '$.appName', CONCAT(@AgencyName, N' Mobile')),
        '$.bundleId', @MobileBundleId),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND Kind = N'PortalMobile' AND Code = N'mobile' AND IsDeleted = 0 AND ISJSON(JsonData) = 1;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, tenant.AgencyName, tenant.SupportEmail, tenant.SupportPhone, tenant.PortalDomain, tenant.BrandingDisplayName, tenant.AdminEmail, tenant.MobileBundleId }, cancellationToken: ct));
    }

    [HttpGet("records")]
    public async Task<IActionResult> SearchRecords([FromQuery] Guid tenantId, [FromQuery] string kind, [FromQuery] string? searchTerm, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);
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
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);
        return Ok(await ReadJsonRecordsAsync<PortalAdminUserDto>(tenantId, "PortalUser", searchTerm, ct));
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);
        return Ok(await ReadJsonRecordsAsync<PortalAdminRequestDto>(tenantId, "SelfServiceRequest", searchTerm, ct));
    }

    [HttpGet("documents")]
    public async Task<IActionResult> GetDocuments([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);
        return Ok(await ReadJsonRecordsAsync<PortalAdminDocumentDto>(tenantId, "PortalDocument", searchTerm, ct));
    }

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivity([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);
        return Ok(await ReadJsonRecordsAsync<PortalAdminActivityDto>(tenantId, "PortalActivity", searchTerm, ct));
    }

    [HttpGet("capabilities")]
    public async Task<IActionResult> GetCapabilities([FromQuery] Guid tenantId, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);
        return Ok(await ReadJsonRecordsAsync<PortalCapabilityDto>(tenantId, "PortalCapability", null, ct));
    }

    [HttpGet("branding")]
    public async Task<IActionResult> GetBranding([FromQuery] Guid tenantId, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);
        return Ok(await ReadSingleJsonRecordAsync<PortalBrandingSettingsDto>(tenantId, "PortalBranding", "branding", ct));
    }

    [HttpGet("mobile")]
    public async Task<IActionResult> GetMobile([FromQuery] Guid tenantId, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);
        return Ok(await ReadSingleJsonRecordAsync<PortalMobileSettingsDto>(tenantId, "PortalMobile", "mobile", ct));
    }

    [HttpGet("my-account")]
    public async Task<IActionResult> GetMyAccount([FromQuery] Guid tenantId, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);
        return Ok(await ReadSingleJsonRecordAsync<PortalMyAccountDto>(tenantId, "PortalMyAccount", "my-account", ct));
    }

    [HttpGet("metrics/{kind}")]
    public async Task<IActionResult> GetMetrics(string kind, [FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);
        return Ok(await ReadJsonRecordsAsync<PortalMetricRecordDto>(tenantId, kind, searchTerm, ct));
    }

    [HttpPut("my-account")]
    public async Task<IActionResult> UpdateMyAccount([FromQuery] Guid tenantId, [FromBody] PortalMyAccountDto account, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);
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
        await EnsurePortalAdminDataAsync(request.TenantId, ct);
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
