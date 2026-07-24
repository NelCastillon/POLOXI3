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

    private async Task EnsurePortalChatSessionDataAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);

        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Portal') EXEC(N'CREATE SCHEMA Portal');

IF OBJECT_ID(N'Portal.ChatSession', N'U') IS NULL
BEGIN
    CREATE TABLE Portal.ChatSession
    (
        ChatSessionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Portal_ChatSession PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SessionNumber NVARCHAR(40) NOT NULL,
        ClientName NVARCHAR(200) NOT NULL,
        AccountName NVARCHAR(200) NOT NULL,
        ContactEmail NVARCHAR(320) NOT NULL CONSTRAINT DF_PortalChatSession_ContactEmail DEFAULT N'',
        Channel NVARCHAR(80) NOT NULL CONSTRAINT DF_PortalChatSession_Channel DEFAULT N'Web Portal',
        Topic NVARCHAR(120) NOT NULL,
        Status NVARCHAR(80) NOT NULL,
        Priority NVARCHAR(40) NOT NULL CONSTRAINT DF_PortalChatSession_Priority DEFAULT N'Normal',
        Sentiment NVARCHAR(40) NOT NULL CONSTRAINT DF_PortalChatSession_Sentiment DEFAULT N'Neutral',
        AssignedTo NVARCHAR(160) NOT NULL CONSTRAINT DF_PortalChatSession_AssignedTo DEFAULT N'Unassigned',
        Summary NVARCHAR(1000) NOT NULL CONSTRAINT DF_PortalChatSession_Summary DEFAULT N'',
        NextBestAction NVARCHAR(500) NOT NULL CONSTRAINT DF_PortalChatSession_NextBestAction DEFAULT N'',
        StartedDateUtc DATETIME2 NOT NULL,
        LastMessageDateUtc DATETIME2 NOT NULL,
        ResolvedDateUtc DATETIME2 NULL,
        MessageCount INT NOT NULL CONSTRAINT DF_PortalChatSession_MessageCount DEFAULT 0,
        WaitSeconds INT NOT NULL CONSTRAINT DF_PortalChatSession_WaitSeconds DEFAULT 0,
        SlaDueDateUtc DATETIME2 NULL,
        AiHandled BIT NOT NULL CONSTRAINT DF_PortalChatSession_AiHandled DEFAULT 0,
        HandoffRequired BIT NOT NULL CONSTRAINT DF_PortalChatSession_HandoffRequired DEFAULT 0,
        ReviewedDateUtc DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PortalChatSession_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_PortalChatSession_IsDeleted DEFAULT 0
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Portal.ChatSession') AND name = N'IX_Portal_ChatSession_TenantStatus')
    CREATE INDEX IX_Portal_ChatSession_TenantStatus ON Portal.ChatSession(TenantId, IsDeleted, Status, Priority, LastMessageDateUtc DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Portal.ChatSession') AND name = N'UX_Portal_ChatSession_Number')
    CREATE UNIQUE INDEX UX_Portal_ChatSession_Number ON Portal.ChatSession(TenantId, SessionNumber) WHERE IsDeleted = 0;

INSERT INTO Portal.ChatSession
(ChatSessionId, TenantId, SessionNumber, ClientName, AccountName, ContactEmail, Channel, Topic, Status, Priority, Sentiment, AssignedTo, Summary, NextBestAction, StartedDateUtc, LastMessageDateUtc, ResolvedDateUtc, MessageCount, WaitSeconds, SlaDueDateUtc, AiHandled, HandoffRequired, ReviewedDateUtc, CreatedDateUtc, IsDeleted)
SELECT ar.PortalAdminRecordId, ar.TenantId, LEFT(ar.Code, 40), ar.Name, ar.Name, N'', N'Web Portal',
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.category'), N''), N'General'),
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.status'), N''), ar.Status),
       CASE WHEN ar.Status = N'Live Handoff' THEN N'High' ELSE N'Normal' END,
       CASE WHEN ar.Status = N'Live Handoff' THEN N'Neutral' ELSE N'Positive' END,
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.owner'), N''), N'Unassigned'),
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.detail'), N''), ar.Name),
       CASE WHEN ar.Status = N'Live Handoff' THEN N'Assign service owner and review transcript.' ELSE N'Quality review transcript and confirm no follow-up is required.' END,
       COALESCE(TRY_CONVERT(DATETIME2, JSON_VALUE(ar.JsonData, '$.eventDateUtc')), ar.CreatedDateUtc),
       COALESCE(TRY_CONVERT(DATETIME2, JSON_VALUE(ar.JsonData, '$.eventDateUtc')), ar.CreatedDateUtc),
       CASE WHEN ar.Status = N'AI Resolved' THEN COALESCE(TRY_CONVERT(DATETIME2, JSON_VALUE(ar.JsonData, '$.eventDateUtc')), ar.CreatedDateUtc) ELSE NULL END,
       COALESCE(TRY_CONVERT(INT, JSON_VALUE(ar.JsonData, '$.count')), 0),
       CASE WHEN ar.Status = N'Live Handoff' THEN 420 ELSE 60 END,
       CASE WHEN ar.Status = N'Live Handoff' THEN DATEADD(MINUTE, 45, SYSUTCDATETIME()) ELSE NULL END,
       CASE WHEN ar.Status = N'AI Resolved' THEN 1 ELSE 0 END,
       CASE WHEN ar.Status = N'Live Handoff' THEN 1 ELSE 0 END,
       NULL,
       SYSUTCDATETIME(),
       0
FROM Portal.AdminRecord ar
WHERE ar.TenantId = @TenantId AND ar.Kind = N'PortalChatSession' AND ar.IsDeleted = 0 AND ISJSON(ar.JsonData) = 1
  AND NOT EXISTS (SELECT 1 FROM Portal.ChatSession cs WHERE cs.TenantId = ar.TenantId AND cs.SessionNumber = LEFT(ar.Code, 40) AND cs.IsDeleted = 0);

IF NOT EXISTS (SELECT 1 FROM Portal.ChatSession WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Portal.ChatSession
    (ChatSessionId, TenantId, SessionNumber, ClientName, AccountName, ContactEmail, Channel, Topic, Status, Priority, Sentiment, AssignedTo, Summary, NextBestAction, StartedDateUtc, LastMessageDateUtc, ResolvedDateUtc, MessageCount, WaitSeconds, SlaDueDateUtc, AiHandled, HandoffRequired, ReviewedDateUtc, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), @TenantId, N'PCS-1001', N'Beth Owens', N'Riverside Construction LLC', N'beth@riverside.example', N'Web Portal', N'Billing', N'Live Handoff', N'Urgent', N'Negative', N'Mia Santos', N'Client disputed invoice finance charge and asked for same-day billing review.', N'Escalate to billing queue and attach invoice history before callback.', DATEADD(HOUR, -9, SYSUTCDATETIME()), DATEADD(MINUTE, -18, SYSUTCDATETIME()), NULL, 18, 512, DATEADD(MINUTE, -30, SYSUTCDATETIME()), 0, 1, NULL, SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, N'PCS-1002', N'Rachel Chen', N'Chen Family', N'rachel.chen@example.com', N'Mobile App', N'COI Request', N'AI Resolved', N'Normal', N'Positive', N'Aria', N'Assistant guided the client through certificate request submission.', N'Quality review only; no human follow-up required.', DATEADD(HOUR, -2, SYSUTCDATETIME()), DATEADD(MINUTE, -7, SYSUTCDATETIME()), DATEADD(HOUR, -1, SYSUTCDATETIME()), 12, 42, NULL, 1, 0, DATEADD(MINUTE, -30, SYSUTCDATETIME()), SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, N'PCS-1003', N'David Kim', N'Kim Dental Group', N'david.kim@example.com', N'Web Portal', N'Login Support', N'Open', N'High', N'Negative', N'Unassigned', N'Suspended user attempted access and requested reinstatement assistance.', N'Assign security owner and verify account status before restoring access.', DATEADD(HOUR, -5, SYSUTCDATETIME()), DATEADD(MINUTE, -11, SYSUTCDATETIME()), NULL, 9, 371, DATEADD(MINUTE, 45, SYSUTCDATETIME()), 0, 1, NULL, SYSUTCDATETIME(), 0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
    }

    private async Task EnsurePortalMobileInstallDataAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);

        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Portal') EXEC(N'CREATE SCHEMA Portal');

IF OBJECT_ID(N'Portal.MobileInstall', N'U') IS NULL
BEGIN
    CREATE TABLE Portal.MobileInstall
    (
        MobileInstallId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Portal_MobileInstall PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        InstallNumber NVARCHAR(40) NOT NULL,
        AccountName NVARCHAR(200) NOT NULL,
        UserName NVARCHAR(200) NOT NULL,
        UserEmail NVARCHAR(320) NOT NULL CONSTRAINT DF_MobileInstall_UserEmail DEFAULT N'',
        Platform NVARCHAR(40) NOT NULL,
        DeviceModel NVARCHAR(160) NOT NULL,
        AppVersion NVARCHAR(40) NOT NULL,
        OsVersion NVARCHAR(80) NOT NULL CONSTRAINT DF_MobileInstall_OsVersion DEFAULT N'',
        Status NVARCHAR(80) NOT NULL,
        ComplianceStatus NVARCHAR(80) NOT NULL CONSTRAINT DF_MobileInstall_Compliance DEFAULT N'Compliant',
        RiskLevel NVARCHAR(40) NOT NULL CONSTRAINT DF_MobileInstall_Risk DEFAULT N'Low',
        EnrollmentType NVARCHAR(80) NOT NULL CONSTRAINT DF_MobileInstall_Enroll DEFAULT N'Client Self-Service',
        LastIpAddress NVARCHAR(80) NOT NULL CONSTRAINT DF_MobileInstall_Ip DEFAULT N'',
        LastLocation NVARCHAR(160) NOT NULL CONSTRAINT DF_MobileInstall_Location DEFAULT N'',
        PushTokenStatus NVARCHAR(80) NOT NULL CONSTRAINT DF_MobileInstall_Push DEFAULT N'Healthy',
        RecommendedAction NVARCHAR(500) NOT NULL CONSTRAINT DF_MobileInstall_Action DEFAULT N'',
        InstalledDateUtc DATETIME2 NOT NULL,
        LastSeenDateUtc DATETIME2 NOT NULL,
        LastPushDateUtc DATETIME2 NULL,
        Sessions30d INT NOT NULL CONSTRAINT DF_MobileInstall_Sessions DEFAULT 0,
        DocumentsViewed30d INT NOT NULL CONSTRAINT DF_MobileInstall_Docs DEFAULT 0,
        RequestsSubmitted30d INT NOT NULL CONSTRAINT DF_MobileInstall_Requests DEFAULT 0,
        PushesSent30d INT NOT NULL CONSTRAINT DF_MobileInstall_Pushes DEFAULT 0,
        BiometricEnabled BIT NOT NULL CONSTRAINT DF_MobileInstall_Biometric DEFAULT 0,
        MfaVerified BIT NOT NULL CONSTRAINT DF_MobileInstall_Mfa DEFAULT 0,
        OfflineAccessEnabled BIT NOT NULL CONSTRAINT DF_MobileInstall_Offline DEFAULT 0,
        UpdateRequired BIT NOT NULL CONSTRAINT DF_MobileInstall_Update DEFAULT 0,
        TrustedDevice BIT NOT NULL CONSTRAINT DF_MobileInstall_Trusted DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_MobileInstall_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_MobileInstall_IsDeleted DEFAULT 0
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Portal.MobileInstall') AND name = N'IX_MobileInstall_Tenant_Status')
    CREATE INDEX IX_MobileInstall_Tenant_Status ON Portal.MobileInstall(TenantId, IsDeleted, Status, ComplianceStatus, LastSeenDateUtc DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Portal.MobileInstall') AND name = N'UX_MobileInstall_Tenant_Number')
    CREATE UNIQUE INDEX UX_MobileInstall_Tenant_Number ON Portal.MobileInstall(TenantId, InstallNumber) WHERE IsDeleted = 0;

IF NOT EXISTS (SELECT 1 FROM Portal.MobileInstall WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Portal.MobileInstall (MobileInstallId, TenantId, InstallNumber, AccountName, UserName, UserEmail, Platform, DeviceModel, AppVersion, OsVersion, Status, ComplianceStatus, RiskLevel, EnrollmentType, LastIpAddress, LastLocation, PushTokenStatus, RecommendedAction, InstalledDateUtc, LastSeenDateUtc, LastPushDateUtc, Sessions30d, DocumentsViewed30d, RequestsSubmitted30d, PushesSent30d, BiometricEnabled, MfaVerified, OfflineAccessEnabled, UpdateRequired, TrustedDevice, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), @TenantId, N'MOB-1001', N'Chen Family', N'Rachel Chen', N'rachel.chen@example.com', N'iOS', N'iPhone 15 Pro', N'2.4.1', N'iOS 18.2', N'Active', N'Compliant', N'Low', N'Client Self-Service', N'72.14.20.18', N'Austin, TX', N'Healthy', N'No action required.', DATEADD(DAY, -32, SYSUTCDATETIME()), DATEADD(HOUR, -2, SYSUTCDATETIME()), DATEADD(HOUR, -5, SYSUTCDATETIME()), 42, 18, 4, 16, 1, 1, 1, 0, 1, SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, N'MOB-1002', N'Webb Holdings LLC', N'Marcus Webb', N'marcus.webb@example.com', N'Android', N'Pixel 8', N'2.4.0', N'Android 15', N'Active', N'Update Recommended', N'Medium', N'Client Self-Service', N'98.21.44.77', N'Fort Worth, TX', N'Healthy', N'Ask client to update to 2.4.1 for latest document fixes.', DATEADD(DAY, -21, SYSUTCDATETIME()), DATEADD(HOUR, -18, SYSUTCDATETIME()), DATEADD(HOUR, -20, SYSUTCDATETIME()), 31, 12, 2, 11, 1, 1, 1, 1, 1, SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, N'MOB-1003', N'Riverside Construction LLC', N'Beth Owens', N'beth@riverside.example', N'iOS', N'iPad Air', N'2.3.8', N'iPadOS 17.6', N'Active', N'Update Required', N'High', N'Broker Assisted', N'24.18.42.8', N'Dallas, TX', N'Registration Stale', N'Force mobile update and refresh push token before renewal campaign.', DATEADD(DAY, -74, SYSUTCDATETIME()), DATEADD(HOUR, -7, SYSUTCDATETIME()), NULL, 58, 33, 8, 0, 0, 1, 1, 1, 0, SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, N'MOB-1004', N'Kim Dental Group', N'David Kim', N'david.kim@example.com', N'iOS', N'iPhone 13', N'2.2.9', N'iOS 16.7', N'Suspended', N'Non-Compliant', N'Critical', N'Client Self-Service', N'104.44.12.9', N'Plano, TX', N'Disabled', N'Review suspended account before reactivating device access.', DATEADD(DAY, -120, SYSUTCDATETIME()), DATEADD(HOUR, -96, SYSUTCDATETIME()), NULL, 9, 1, 0, 0, 0, 0, 0, 1, 0, SYSUTCDATETIME(), 0);
END;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        await SyncMobileInstallAdminRecordsAsync(cn, tenantId, ct);
    }

    private async Task EnsurePortalApiUsageDataAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);

        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Portal') EXEC(N'CREATE SCHEMA Portal');

IF OBJECT_ID(N'Portal.ApiUsage', N'U') IS NULL
BEGIN
    CREATE TABLE Portal.ApiUsage
    (
        ApiUsageId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Portal_ApiUsage PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        EndpointCode NVARCHAR(80) NOT NULL,
        EndpointName NVARCHAR(200) NOT NULL,
        Method NVARCHAR(12) NOT NULL,
        Route NVARCHAR(300) NOT NULL,
        IntegrationName NVARCHAR(160) NOT NULL,
        ApiKeyName NVARCHAR(160) NOT NULL,
        Status NVARCHAR(80) NOT NULL,
        HealthStatus NVARCHAR(80) NOT NULL CONSTRAINT DF_ApiUsage_Health DEFAULT N'Healthy',
        Priority NVARCHAR(40) NOT NULL CONSTRAINT DF_ApiUsage_Priority DEFAULT N'Normal',
        Owner NVARCHAR(160) NOT NULL CONSTRAINT DF_ApiUsage_Owner DEFAULT N'Portal Ops',
        Detail NVARCHAR(1000) NOT NULL CONSTRAINT DF_ApiUsage_Detail DEFAULT N'',
        RecommendedAction NVARCHAR(500) NOT NULL CONSTRAINT DF_ApiUsage_Action DEFAULT N'',
        LastCallUtc DATETIME2 NOT NULL,
        Calls30d INT NOT NULL CONSTRAINT DF_ApiUsage_Calls DEFAULT 0,
        SuccessCount30d INT NOT NULL CONSTRAINT DF_ApiUsage_Success DEFAULT 0,
        WarningCount30d INT NOT NULL CONSTRAINT DF_ApiUsage_Warning DEFAULT 0,
        ErrorCount30d INT NOT NULL CONSTRAINT DF_ApiUsage_Error DEFAULT 0,
        AvgLatencyMs INT NOT NULL CONSTRAINT DF_ApiUsage_AvgLatency DEFAULT 0,
        P95LatencyMs INT NOT NULL CONSTRAINT DF_ApiUsage_P95 DEFAULT 0,
        RateLimitPerMinute INT NOT NULL CONSTRAINT DF_ApiUsage_RateLimit DEFAULT 0,
        QuotaUsedPercent INT NOT NULL CONSTRAINT DF_ApiUsage_Quota DEFAULT 0,
        WebhookDeliveries30d INT NOT NULL CONSTRAINT DF_ApiUsage_Webhooks DEFAULT 0,
        RetryCount30d INT NOT NULL CONSTRAINT DF_ApiUsage_Retries DEFAULT 0,
        RequiresReview BIT NOT NULL CONSTRAINT DF_ApiUsage_Review DEFAULT 0,
        ReviewedDateUtc DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ApiUsage_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_ApiUsage_IsDeleted DEFAULT 0
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Portal.ApiUsage') AND name = N'IX_ApiUsage_Tenant_Status')
    CREATE INDEX IX_ApiUsage_Tenant_Status ON Portal.ApiUsage(TenantId, IsDeleted, Status, HealthStatus, LastCallUtc DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Portal.ApiUsage') AND name = N'UX_ApiUsage_Tenant_Endpoint')
    CREATE UNIQUE INDEX UX_ApiUsage_Tenant_Endpoint ON Portal.ApiUsage(TenantId, EndpointCode) WHERE IsDeleted = 0;

INSERT INTO Portal.ApiUsage
(ApiUsageId, TenantId, EndpointCode, EndpointName, Method, Route, IntegrationName, ApiKeyName, Status, HealthStatus, Priority, Owner, Detail, RecommendedAction, LastCallUtc, Calls30d, SuccessCount30d, WarningCount30d, ErrorCount30d, AvgLatencyMs, P95LatencyMs, RateLimitPerMinute, QuotaUsedPercent, WebhookDeliveries30d, RetryCount30d, RequiresReview, ReviewedDateUtc, CreatedDateUtc, IsDeleted)
SELECT ar.PortalAdminRecordId, ar.TenantId, LEFT(ar.Code, 80), ar.Name,
       LEFT(COALESCE(NULLIF(LEFT(ar.Name, CHARINDEX(N' ', ar.Name + N' ') - 1), N''), N'GET'), 12),
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.name'), N''), ar.Name),
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.category'), N''), N'Client Portal'),
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.owner'), N''), N'portal-web'),
       ar.Status,
       CASE WHEN ar.Status = N'Error' THEN N'At Risk' WHEN ar.Status = N'Warning' THEN N'Watch' ELSE N'Healthy' END,
       CASE WHEN ar.Status = N'Error' THEN N'Critical' WHEN ar.Status = N'Warning' THEN N'High' ELSE N'Normal' END,
       CASE WHEN ar.Status IN (N'Warning', N'Error') THEN N'Portal Ops' ELSE N'Automation' END,
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.detail'), N''), ar.Name),
       CASE WHEN ar.Status IN (N'Warning', N'Error') THEN N'Review trend and remediate integration warnings.' ELSE N'Monitor normal usage trend.' END,
       COALESCE(TRY_CONVERT(DATETIME2, JSON_VALUE(ar.JsonData, '$.eventDateUtc')), ar.ModifiedDateUtc, ar.CreatedDateUtc),
       COALESCE(TRY_CONVERT(INT, JSON_VALUE(ar.JsonData, '$.count')), 0),
       CASE WHEN ar.Status = N'Successful' THEN COALESCE(TRY_CONVERT(INT, JSON_VALUE(ar.JsonData, '$.count')), 0) ELSE 0 END,
       CASE WHEN ar.Status = N'Warning' THEN COALESCE(TRY_CONVERT(INT, JSON_VALUE(ar.JsonData, '$.count')), 0) ELSE 0 END,
       CASE WHEN ar.Status = N'Error' THEN COALESCE(TRY_CONVERT(INT, JSON_VALUE(ar.JsonData, '$.count')), 0) ELSE 0 END,
       220, 850, 300, CASE WHEN ar.Status = N'Warning' THEN 72 WHEN ar.Status = N'Error' THEN 88 ELSE 44 END,
       CASE WHEN ar.Status = N'Successful' THEN COALESCE(TRY_CONVERT(INT, JSON_VALUE(ar.JsonData, '$.count')), 0) / 10 ELSE 0 END,
       CASE WHEN ar.Status IN (N'Warning', N'Error') THEN COALESCE(TRY_CONVERT(INT, JSON_VALUE(ar.JsonData, '$.count')), 0) ELSE 0 END,
       CASE WHEN ar.Status IN (N'Warning', N'Error') THEN 1 ELSE 0 END,
       CASE WHEN ar.Status IN (N'Warning', N'Error') THEN NULL ELSE SYSUTCDATETIME() END,
       SYSUTCDATETIME(), 0
FROM Portal.AdminRecord ar
WHERE ar.TenantId = @TenantId AND ar.Kind = N'PortalApiUsage' AND ar.IsDeleted = 0 AND ISJSON(ar.JsonData) = 1
  AND NOT EXISTS (SELECT 1 FROM Portal.ApiUsage au WHERE au.TenantId = ar.TenantId AND au.EndpointCode = LEFT(ar.Code, 80) AND au.IsDeleted = 0);

IF NOT EXISTS (SELECT 1 FROM Portal.ApiUsage WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Portal.ApiUsage
    (ApiUsageId, TenantId, EndpointCode, EndpointName, Method, Route, IntegrationName, ApiKeyName, Status, HealthStatus, Priority, Owner, Detail, RecommendedAction, LastCallUtc, Calls30d, SuccessCount30d, WarningCount30d, ErrorCount30d, AvgLatencyMs, P95LatencyMs, RateLimitPerMinute, QuotaUsedPercent, WebhookDeliveries30d, RetryCount30d, RequiresReview, ReviewedDateUtc, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), @TenantId, N'documents-list', N'Document center list', N'GET', N'/portal/documents', N'Client Portal', N'portal-web', N'Successful', N'Healthy', N'Normal', N'Portal Ops', N'High-volume document center read endpoint for client portal and mobile app.', N'Monitor cache hit rate and preserve current rate limit.', DATEADD(MINUTE, -12, SYSUTCDATETIME()), 48200, 48011, 151, 38, 118, 390, 1200, 62, 0, 151, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, N'auth-login', N'Portal authentication', N'POST', N'/portal/auth', N'Authentication', N'portal-auth', N'Warning', N'Watch', N'Critical', N'Security Team', N'Elevated failed login attempts and lockout warnings in the last 24 hours.', N'Review suspicious IP patterns and tune lockout messaging.', DATEADD(MINUTE, -18, SYSUTCDATETIME()), 18640, 18172, 431, 37, 164, 610, 900, 71, 0, 431, 1, NULL, SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, N'invites-send', N'Portal invite send', N'POST', N'/portal/invites/send', N'Admin Console', N'portal-admin', N'Error', N'At Risk', N'Critical', N'Portal Ops', N'Invite delivery errors are concentrated on unverified domains.', N'Verify sender domain and retry failed invites after DNS validation.', DATEADD(MINUTE, -135, SYSUTCDATETIME()), 780, 712, 31, 37, 284, 970, 180, 64, 0, 31, 1, NULL, SYSUTCDATETIME(), 0);
END;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        await SyncApiUsageAdminRecordsAsync(cn, tenantId, ct);
    }

    private async Task EnsurePortalActivityEventDataAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);

        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Portal') EXEC(N'CREATE SCHEMA Portal');

IF OBJECT_ID(N'Portal.ActivityEvent', N'U') IS NULL
BEGIN
    CREATE TABLE Portal.ActivityEvent
    (
        ActivityEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Portal_ActivityEvent PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        EventNumber NVARCHAR(40) NOT NULL,
        OccurredAtUtc DATETIME2 NOT NULL,
        UserName NVARCHAR(200) NOT NULL,
        UserEmail NVARCHAR(320) NOT NULL CONSTRAINT DF_ActivityEvent_UserEmail DEFAULT N'',
        AccountName NVARCHAR(200) NOT NULL CONSTRAINT DF_ActivityEvent_Account DEFAULT N'',
        EventType NVARCHAR(100) NOT NULL,
        Category NVARCHAR(80) NOT NULL CONSTRAINT DF_ActivityEvent_Category DEFAULT N'General',
        Severity NVARCHAR(40) NOT NULL CONSTRAINT DF_ActivityEvent_Severity DEFAULT N'Info',
        Status NVARCHAR(60) NOT NULL CONSTRAINT DF_ActivityEvent_Status DEFAULT N'Open',
        Detail NVARCHAR(1000) NOT NULL CONSTRAINT DF_ActivityEvent_Detail DEFAULT N'',
        WorkflowImpact NVARCHAR(500) NOT NULL CONSTRAINT DF_ActivityEvent_Impact DEFAULT N'',
        RecommendedAction NVARCHAR(500) NOT NULL CONSTRAINT DF_ActivityEvent_Action DEFAULT N'',
        AssignedTo NVARCHAR(160) NOT NULL CONSTRAINT DF_ActivityEvent_AssignedTo DEFAULT N'Unassigned',
        IpAddress NVARCHAR(80) NOT NULL CONSTRAINT DF_ActivityEvent_Ip DEFAULT N'',
        Device NVARCHAR(160) NOT NULL CONSTRAINT DF_ActivityEvent_Device DEFAULT N'',
        Location NVARCHAR(160) NOT NULL CONSTRAINT DF_ActivityEvent_Location DEFAULT N'',
        RiskScore INT NOT NULL CONSTRAINT DF_ActivityEvent_Risk DEFAULT 0,
        DurationSeconds INT NOT NULL CONSTRAINT DF_ActivityEvent_Duration DEFAULT 0,
        RequiresReview BIT NOT NULL CONSTRAINT DF_ActivityEvent_Review DEFAULT 0,
        ReviewedDateUtc DATETIME2 NULL,
        ReviewedBy NVARCHAR(160) NOT NULL CONSTRAINT DF_ActivityEvent_ReviewedBy DEFAULT N'',
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ActivityEvent_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_ActivityEvent_IsDeleted DEFAULT 0
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Portal.ActivityEvent') AND name = N'IX_ActivityEvent_Tenant_Occurred')
    CREATE INDEX IX_ActivityEvent_Tenant_Occurred ON Portal.ActivityEvent(TenantId, OccurredAtUtc DESC, IsDeleted);

INSERT INTO Portal.ActivityEvent
(ActivityEventId, TenantId, EventNumber, OccurredAtUtc, UserName, UserEmail, AccountName, EventType, Category, Severity, Status, Detail, WorkflowImpact, RecommendedAction, AssignedTo, IpAddress, Device, Location, RiskScore, DurationSeconds, RequiresReview, ReviewedDateUtc, ReviewedBy, CreatedDateUtc, IsDeleted)
SELECT ar.PortalAdminRecordId, ar.TenantId, LEFT(ar.Code, 40),
       COALESCE(TRY_CONVERT(DATETIME2, JSON_VALUE(ar.JsonData, '$.occurredAt')), ar.CreatedDateUtc),
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.userName'), N''), ar.Name),
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.userEmail'), N''), N''),
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.accountName'), N''), N''),
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.eventType'), N''), ar.Name),
       CASE WHEN ar.Status IN (N'Warning', N'Error') THEN N'Security' ELSE N'General' END,
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.severity'), N''), ar.Status),
       CASE WHEN ar.Status IN (N'Warning', N'Error') THEN N'Open' ELSE N'Reviewed' END,
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.detail'), N''), ar.Name),
       CASE WHEN ar.Status IN (N'Warning', N'Error') THEN N'Requires operations review.' ELSE N'Captured for audit trail.' END,
       CASE WHEN ar.Status IN (N'Warning', N'Error') THEN N'Review and acknowledge the event.' ELSE N'No action required.' END,
       CASE WHEN ar.Status IN (N'Warning', N'Error') THEN N'Security Team' ELSE N'Portal Ops' END,
       COALESCE(NULLIF(JSON_VALUE(ar.JsonData, '$.ipAddress'), N''), N''),
       N'Client Portal', N'Unknown',
       CASE WHEN ar.Status = N'Error' THEN 90 WHEN ar.Status = N'Warning' THEN 70 ELSE 20 END,
       0,
       CASE WHEN ar.Status IN (N'Warning', N'Error') THEN 1 ELSE 0 END,
       CASE WHEN ar.Status IN (N'Warning', N'Error') THEN NULL ELSE SYSUTCDATETIME() END,
       CASE WHEN ar.Status IN (N'Warning', N'Error') THEN N'' ELSE N'Portal Ops' END,
       SYSUTCDATETIME(), 0
FROM Portal.AdminRecord ar
WHERE ar.TenantId = @TenantId AND ar.Kind = N'PortalActivity' AND ar.IsDeleted = 0 AND ISJSON(ar.JsonData) = 1
  AND NOT EXISTS (SELECT 1 FROM Portal.ActivityEvent ae WHERE ae.TenantId = ar.TenantId AND ae.EventNumber = LEFT(ar.Code, 40) AND ae.IsDeleted = 0);

IF NOT EXISTS (SELECT 1 FROM Portal.ActivityEvent WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Portal.ActivityEvent
    (ActivityEventId, TenantId, EventNumber, OccurredAtUtc, UserName, UserEmail, AccountName, EventType, Category, Severity, Status, Detail, WorkflowImpact, RecommendedAction, AssignedTo, IpAddress, Device, Location, RiskScore, DurationSeconds, RequiresReview, ReviewedDateUtc, ReviewedBy, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), @TenantId, N'ACT-1001', DATEADD(MINUTE, -12, SYSUTCDATETIME()), N'Rachel Chen', N'rachel.chen@example.com', N'Chen Family', N'Login', N'Authentication', N'Info', N'Reviewed', N'Successful client portal login with MFA.', N'Confirms active client adoption and secure access.', N'No action required.', N'Portal Ops', N'72.14.20.18', N'Chrome on Windows', N'Austin, TX', 12, 4, 0, SYSUTCDATETIME(), N'Portal Ops', SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, N'ACT-1002', DATEADD(MINUTE, -28, SYSUTCDATETIME()), N'Beth Owens', N'beth@riverside.example', N'Riverside Construction LLC', N'Request Submitted', N'Self-Service', N'Info', N'Open', N'Submitted urgent COI request for project owner.', N'Creates service workload with same-day SLA.', N'Assign to CSR and validate certificate holder details.', N'Unassigned', N'24.18.42.8', N'Safari on iPhone', N'Dallas, TX', 58, 96, 1, NULL, N'', SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, N'ACT-1003', DATEADD(MINUTE, -44, SYSUTCDATETIME()), N'David Kim', N'david.kim@example.com', N'Kim Dental Group', N'Failed Login', N'Security', N'Warning', N'Open', N'Failed login attempt after account suspension.', N'Security review required before reactivation.', N'Review suspension reason and contact account owner.', N'Security Team', N'104.44.12.9', N'Edge on Windows', N'Plano, TX', 84, 7, 1, NULL, N'', SYSUTCDATETIME(), 0),
    (NEWID(), @TenantId, N'ACT-1009', DATEADD(MINUTE, -310, SYSUTCDATETIME()), N'Unknown User', N'unknown@example.com', N'Unknown', N'Blocked Login', N'Security', N'Error', N'Escalated', N'Blocked login from unexpected geography.', N'Potential account takeover signal.', N'Escalate to security and verify user identity.', N'Security Team', N'185.199.108.21', N'Chrome on Linux', N'Unknown', 96, 3, 1, NULL, N'', SYSUTCDATETIME(), 0);
END;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
    }

    private async Task EnsurePortalWhiteLabelDataAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);

        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Portal') EXEC(N'CREATE SCHEMA Portal');

IF OBJECT_ID(N'Portal.WhiteLabelConfiguration', N'U') IS NULL
BEGIN
    CREATE TABLE Portal.WhiteLabelConfiguration
    (
        WhiteLabelConfigurationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Portal_WhiteLabelConfiguration PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        PortalDomain NVARCHAR(255) NOT NULL,
        DomainStatus NVARCHAR(40) NOT NULL CONSTRAINT DF_WhiteLabel_DomainStatus DEFAULT N'Pending DNS',
        PublishStatus NVARCHAR(40) NOT NULL CONSTRAINT DF_WhiteLabel_PublishStatus DEFAULT N'Draft',
        LastPublishedDateUtc DATETIME2 NULL,
        PrimaryColor NVARCHAR(20) NOT NULL CONSTRAINT DF_WhiteLabel_PrimaryColor DEFAULT N'#1d4ed8',
        AccentColor NVARCHAR(20) NOT NULL CONSTRAINT DF_WhiteLabel_AccentColor DEFAULT N'#059669',
        NavBackgroundColor NVARCHAR(20) NOT NULL CONSTRAINT DF_WhiteLabel_NavBg DEFAULT N'#1e293b',
        NavTextColor NVARCHAR(20) NOT NULL CONSTRAINT DF_WhiteLabel_NavText DEFAULT N'#f8fafc',
        LogoUrl NVARCHAR(500) NOT NULL CONSTRAINT DF_WhiteLabel_LogoUrl DEFAULT N'',
        FaviconUrl NVARCHAR(500) NOT NULL CONSTRAINT DF_WhiteLabel_FaviconUrl DEFAULT N'',
        WelcomeMessage NVARCHAR(1000) NOT NULL CONSTRAINT DF_WhiteLabel_Welcome DEFAULT N'',
        SupportEmail NVARCHAR(320) NOT NULL,
        SupportPhone NVARCHAR(50) NOT NULL CONSTRAINT DF_WhiteLabel_SupportPhone DEFAULT N'',
        ShowAgencyLogo BIT NOT NULL CONSTRAINT DF_WhiteLabel_ShowAgencyLogo DEFAULT 1,
        HidePoweredBy BIT NOT NULL CONSTRAINT DF_WhiteLabel_HidePoweredBy DEFAULT 0,
        ShowNewsWidget BIT NOT NULL CONSTRAINT DF_WhiteLabel_ShowNews DEFAULT 1,
        ShowSupportChat BIT NOT NULL CONSTRAINT DF_WhiteLabel_ShowChat DEFAULT 1,
        EnableAnnouncements BIT NOT NULL CONSTRAINT DF_WhiteLabel_Announcements DEFAULT 1,
        EnableCrossSellWidget BIT NOT NULL CONSTRAINT DF_WhiteLabel_CrossSell DEFAULT 1,
        MobileAppName NVARCHAR(200) NOT NULL,
        MobileBundleId NVARCHAR(160) NOT NULL CONSTRAINT DF_WhiteLabel_Bundle DEFAULT N'',
        IosStoreUrl NVARCHAR(500) NOT NULL CONSTRAINT DF_WhiteLabel_IosUrl DEFAULT N'',
        AndroidStoreUrl NVARCHAR(500) NOT NULL CONSTRAINT DF_WhiteLabel_AndroidUrl DEFAULT N'',
        MobileVersion NVARCHAR(40) NOT NULL CONSTRAINT DF_WhiteLabel_MobileVersion DEFAULT N'2.4.1',
        MinimumMobileVersion NVARCHAR(40) NOT NULL CONSTRAINT DF_WhiteLabel_MinMobileVersion DEFAULT N'2.0.0',
        MobilePublished BIT NOT NULL CONSTRAINT DF_WhiteLabel_MobilePublished DEFAULT 1,
        BiometricLogin BIT NOT NULL CONSTRAINT DF_WhiteLabel_Biometric DEFAULT 1,
        PushNotifications BIT NOT NULL CONSTRAINT DF_WhiteLabel_Push DEFAULT 1,
        OfflinePolicyView BIT NOT NULL CONSTRAINT DF_WhiteLabel_Offline DEFAULT 1,
        ForceMobileUpdate BIT NOT NULL CONSTRAINT DF_WhiteLabel_ForceUpdate DEFAULT 0,
        RequireMfaOnMobile BIT NOT NULL CONSTRAINT DF_WhiteLabel_MobileMfa DEFAULT 1,
        AssistantName NVARCHAR(120) NOT NULL CONSTRAINT DF_WhiteLabel_Assistant DEFAULT N'Aria',
        AssistantWelcomeMessage NVARCHAR(1000) NOT NULL CONSTRAINT DF_WhiteLabel_AssistantWelcome DEFAULT N'',
        ChatWidgetColor NVARCHAR(20) NOT NULL CONSTRAINT DF_WhiteLabel_ChatColor DEFAULT N'#1d4ed8',
        ChatPosition NVARCHAR(40) NOT NULL CONSTRAINT DF_WhiteLabel_ChatPosition DEFAULT N'bottom-right',
        ChatEscalationEmail NVARCHAR(320) NOT NULL CONSTRAINT DF_WhiteLabel_ChatEmail DEFAULT N'',
        OfficeHours NVARCHAR(120) NOT NULL CONSTRAINT DF_WhiteLabel_OfficeHours DEFAULT N'Mon-Fri, 8am-5pm CT',
        ChatEnabled BIT NOT NULL CONSTRAINT DF_WhiteLabel_ChatEnabled DEFAULT 1,
        AiResponsesEnabled BIT NOT NULL CONSTRAINT DF_WhiteLabel_AiResponses DEFAULT 1,
        LiveHandoffEnabled BIT NOT NULL CONSTRAINT DF_WhiteLabel_Handoff DEFAULT 1,
        ShowChatOnMobile BIT NOT NULL CONSTRAINT DF_WhiteLabel_MobileChat DEFAULT 1,
        AllowFileAttachments BIT NOT NULL CONSTRAINT DF_WhiteLabel_Attachments DEFAULT 1,
        TranscriptEmailEnabled BIT NOT NULL CONSTRAINT DF_WhiteLabel_Transcript DEFAULT 1,
        IdentityProvider NVARCHAR(80) NOT NULL CONSTRAINT DF_WhiteLabel_Idp DEFAULT N'none',
        SsoClientId NVARCHAR(255) NOT NULL CONSTRAINT DF_WhiteLabel_SsoClient DEFAULT N'',
        SsoMetadataUrl NVARCHAR(500) NOT NULL CONSTRAINT DF_WhiteLabel_Metadata DEFAULT N'',
        RedirectUris NVARCHAR(1000) NOT NULL CONSTRAINT DF_WhiteLabel_Redirects DEFAULT N'',
        SsoEnabled BIT NOT NULL CONSTRAINT DF_WhiteLabel_SsoEnabled DEFAULT 0,
        MfaRequired BIT NOT NULL CONSTRAINT DF_WhiteLabel_MfaRequired DEFAULT 0,
        AllowSocialLogin BIT NOT NULL CONSTRAINT DF_WhiteLabel_Social DEFAULT 1,
        AutoProvisionUsers BIT NOT NULL CONSTRAINT DF_WhiteLabel_AutoProvision DEFAULT 0,
        PasswordMinLength INT NOT NULL CONSTRAINT DF_WhiteLabel_PwdMin DEFAULT 10,
        SessionTimeoutMinutes INT NOT NULL CONSTRAINT DF_WhiteLabel_Timeout DEFAULT 30,
        MaxFailedLoginAttempts INT NOT NULL CONSTRAINT DF_WhiteLabel_Failed DEFAULT 5,
        LockoutMinutes INT NOT NULL CONSTRAINT DF_WhiteLabel_Lockout DEFAULT 15,
        RequireUppercase BIT NOT NULL CONSTRAINT DF_WhiteLabel_Upper DEFAULT 1,
        RequireSpecialCharacter BIT NOT NULL CONSTRAINT DF_WhiteLabel_Special DEFAULT 1,
        IpWhitelistEnabled BIT NOT NULL CONSTRAINT DF_WhiteLabel_Ip DEFAULT 0,
        ActivePortalUsers INT NOT NULL CONSTRAINT DF_WhiteLabel_ActiveUsers DEFAULT 0,
        PendingInvites INT NOT NULL CONSTRAINT DF_WhiteLabel_PendingInvites DEFAULT 0,
        MobileInstalls INT NOT NULL CONSTRAINT DF_WhiteLabel_MobileInstalls DEFAULT 0,
        ChatSessions30d INT NOT NULL CONSTRAINT DF_WhiteLabel_ChatSessions DEFAULT 0,
        OpenRequests INT NOT NULL CONSTRAINT DF_WhiteLabel_OpenRequests DEFAULT 0,
        UrgentRequests INT NOT NULL CONSTRAINT DF_WhiteLabel_UrgentRequests DEFAULT 0,
        SharedDocuments INT NOT NULL CONSTRAINT DF_WhiteLabel_SharedDocuments DEFAULT 0,
        ApiCalls30d INT NOT NULL CONSTRAINT DF_WhiteLabel_ApiCalls DEFAULT 0,
        CsATScore DECIMAL(4,2) NOT NULL CONSTRAINT DF_WhiteLabel_Csat DEFAULT 4.60,
        AiResolutionRate INT NOT NULL CONSTRAINT DF_WhiteLabel_AiRate DEFAULT 74,
        LiveHandoffs30d INT NOT NULL CONSTRAINT DF_WhiteLabel_Handoffs DEFAULT 0,
        AverageResponseSeconds INT NOT NULL CONSTRAINT DF_WhiteLabel_Response DEFAULT 108,
        ConfigurationJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_WhiteLabel_ConfigJson DEFAULT N'{}',
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_WhiteLabel_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_WhiteLabel_IsDeleted DEFAULT 0
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Portal.WhiteLabelConfiguration') AND name = N'UX_WhiteLabel_Tenant')
    CREATE UNIQUE INDEX UX_WhiteLabel_Tenant ON Portal.WhiteLabelConfiguration(TenantId) WHERE IsDeleted = 0;

DECLARE @AgencyName NVARCHAR(200) = COALESCE((SELECT TOP 1 TenantName FROM Core.Tenant WHERE TenantId = @TenantId), N'Tenant Agency');
DECLARE @SupportEmail NVARCHAR(320) = COALESCE((SELECT TOP 1 ContactEmail FROM Agency.Profile WHERE TenantId = @TenantId AND IsDeleted = 0), N'admin@agency.local');
DECLARE @SupportPhone NVARCHAR(50) = COALESCE((SELECT TOP 1 ContactPhone FROM Agency.Profile WHERE TenantId = @TenantId AND IsDeleted = 0), N'(555) 000-0000');
DECLARE @PortalDomain NVARCHAR(255) = CONCAT(N'portal.', LOWER(REPLACE(REPLACE(@AgencyName, N' ', N''), N'.', N'')), N'.com');

INSERT INTO Portal.WhiteLabelConfiguration
(WhiteLabelConfigurationId, TenantId, DisplayName, PortalDomain, DomainStatus, PublishStatus, LastPublishedDateUtc, PrimaryColor, AccentColor, NavBackgroundColor, NavTextColor, WelcomeMessage, SupportEmail, SupportPhone, ShowAgencyLogo, HidePoweredBy, ShowNewsWidget, ShowSupportChat, EnableAnnouncements, EnableCrossSellWidget, MobileAppName, MobileBundleId, IosStoreUrl, AndroidStoreUrl, MobileVersion, MinimumMobileVersion, MobilePublished, BiometricLogin, PushNotifications, OfflinePolicyView, ForceMobileUpdate, RequireMfaOnMobile, AssistantName, AssistantWelcomeMessage, ChatWidgetColor, ChatPosition, ChatEscalationEmail, OfficeHours, ChatEnabled, AiResponsesEnabled, LiveHandoffEnabled, ShowChatOnMobile, AllowFileAttachments, TranscriptEmailEnabled, IdentityProvider, SsoEnabled, MfaRequired, AllowSocialLogin, AutoProvisionUsers, PasswordMinLength, SessionTimeoutMinutes, MaxFailedLoginAttempts, LockoutMinutes, RequireUppercase, RequireSpecialCharacter, IpWhitelistEnabled, ActivePortalUsers, PendingInvites, MobileInstalls, ChatSessions30d, OpenRequests, UrgentRequests, SharedDocuments, ApiCalls30d, CsATScore, AiResolutionRate, LiveHandoffs30d, AverageResponseSeconds, ConfigurationJson, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @TenantId,
       COALESCE(NULLIF(JSON_VALUE(b.JsonData, '$.displayName'), N''), CONCAT(@AgencyName, N' Client Portal')),
       COALESCE(NULLIF(JSON_VALUE(b.JsonData, '$.domain'), N''), @PortalDomain),
       N'Verified', N'Live', DATEADD(DAY, -4, SYSUTCDATETIME()),
       COALESCE(NULLIF(JSON_VALUE(b.JsonData, '$.primaryColor'), N''), N'#1d4ed8'),
       COALESCE(NULLIF(JSON_VALUE(b.JsonData, '$.accentColor'), N''), N'#059669'),
       COALESCE(NULLIF(JSON_VALUE(b.JsonData, '$.navBg'), N''), N'#1e293b'),
       COALESCE(NULLIF(JSON_VALUE(b.JsonData, '$.navText'), N''), N'#f8fafc'),
       COALESCE(NULLIF(JSON_VALUE(b.JsonData, '$.welcomeMessage'), N''), CONCAT(N'Manage policies, request certificates, upload documents, and message ', @AgencyName, N' in one secure place.')),
       COALESCE(NULLIF(JSON_VALUE(b.JsonData, '$.supportEmail'), N''), @SupportEmail),
       COALESCE(NULLIF(JSON_VALUE(b.JsonData, '$.supportPhone'), N''), @SupportPhone),
       COALESCE(TRY_CONVERT(BIT, JSON_VALUE(b.JsonData, '$.showAgencyLogo')), 1),
       CASE WHEN COALESCE(TRY_CONVERT(BIT, JSON_VALUE(b.JsonData, '$.showPoweredBy')), 0) = 1 THEN 0 ELSE 1 END,
       COALESCE(TRY_CONVERT(BIT, JSON_VALUE(b.JsonData, '$.showNewsWidget')), 1),
       COALESCE(TRY_CONVERT(BIT, JSON_VALUE(b.JsonData, '$.showSupportChat')), 1),
       1, 1,
       COALESCE(NULLIF(JSON_VALUE(m.JsonData, '$.appName'), N''), CONCAT(@AgencyName, N' Mobile')),
       COALESCE(NULLIF(JSON_VALUE(m.JsonData, '$.bundleId'), N''), N''),
       COALESCE(NULLIF(JSON_VALUE(m.JsonData, '$.iosUrl'), N''), N''),
       COALESCE(NULLIF(JSON_VALUE(m.JsonData, '$.androidUrl'), N''), N''),
       COALESCE(NULLIF(JSON_VALUE(m.JsonData, '$.appVersion'), N''), N'2.4.1'),
       N'2.0.0', 1,
       COALESCE(TRY_CONVERT(BIT, JSON_VALUE(m.JsonData, '$.biometricLogin')), 1),
       1, 1, 0,
       COALESCE(TRY_CONVERT(BIT, JSON_VALUE(m.JsonData, '$.requireMfaOnMobile')), 1),
       N'Aria', CONCAT(N'Hi there! I''m Aria, your ', @AgencyName, N' assistant. I can help with COI requests, policy questions, payments, and more.'), COALESCE(NULLIF(JSON_VALUE(b.JsonData, '$.primaryColor'), N''), N'#1d4ed8'), N'bottom-right', @SupportEmail, N'Mon-Fri, 8am-5pm CT',
       COALESCE(TRY_CONVERT(BIT, JSON_VALUE(a.JsonData, '$.chatEnabled')), 1), 1, 1, 1, 1, 1, N'none',
       COALESCE(TRY_CONVERT(BIT, JSON_VALUE(a.JsonData, '$.ssoEnabled')), 0),
       COALESCE(TRY_CONVERT(BIT, JSON_VALUE(a.JsonData, '$.mfaEnabled')), 0),
       1, 0, 10, 30, 5, 15, 1, 1, 0,
       COALESCE(TRY_CONVERT(INT, JSON_VALUE(a.JsonData, '$.activePortalUsers')), 47),
       COALESCE(TRY_CONVERT(INT, JSON_VALUE(a.JsonData, '$.pendingInvites')), 6),
       COALESCE(TRY_CONVERT(INT, JSON_VALUE(a.JsonData, '$.mobileInstalls')), 23),
       COALESCE(TRY_CONVERT(INT, JSON_VALUE(a.JsonData, '$.chatSessions30d')), 184),
       COALESCE(TRY_CONVERT(INT, JSON_VALUE(a.JsonData, '$.openRequests')), 9),
       COALESCE(TRY_CONVERT(INT, JSON_VALUE(a.JsonData, '$.urgentRequests')), 3),
       COALESCE(TRY_CONVERT(INT, JSON_VALUE(a.JsonData, '$.sharedDocuments')), 42),
       COALESCE(TRY_CONVERT(INT, JSON_VALUE(a.JsonData, '$.apiCalls30d')), 50410),
       4.60, 74, 18, 108, N'{}', SYSUTCDATETIME(), 0
FROM (SELECT 1 AS x) seed
LEFT JOIN Portal.AdminRecord b ON b.TenantId = @TenantId AND b.Kind = N'PortalBranding' AND b.Code = N'branding' AND b.IsDeleted = 0 AND ISJSON(b.JsonData) = 1
LEFT JOIN Portal.AdminRecord m ON m.TenantId = @TenantId AND m.Kind = N'PortalMobile' AND m.Code = N'mobile' AND m.IsDeleted = 0 AND ISJSON(m.JsonData) = 1
LEFT JOIN Portal.AdminRecord a ON a.TenantId = @TenantId AND a.Kind = N'PortalMyAccount' AND a.Code = N'my-account' AND a.IsDeleted = 0 AND ISJSON(a.JsonData) = 1
WHERE NOT EXISTS (SELECT 1 FROM Portal.WhiteLabelConfiguration WHERE TenantId = @TenantId AND IsDeleted = 0);

UPDATE w
SET ActivePortalUsers = COALESCE(NULLIF((SELECT COUNT(1) FROM Portal.AdminRecord u WHERE u.TenantId = @TenantId AND u.Kind = N'PortalUser' AND u.Status = N'Active' AND u.IsDeleted = 0), 0), w.ActivePortalUsers),
    PendingInvites = COALESCE(NULLIF((SELECT COUNT(1) FROM Portal.AdminRecord u WHERE u.TenantId = @TenantId AND u.Kind = N'PortalUser' AND u.Status = N'Pending' AND u.IsDeleted = 0), 0), w.PendingInvites),
    OpenRequests = COALESCE(NULLIF((SELECT COUNT(1) FROM Portal.AdminRecord r WHERE r.TenantId = @TenantId AND r.Kind = N'SelfServiceRequest' AND r.Status IN (N'Open', N'In Progress') AND r.IsDeleted = 0), 0), w.OpenRequests),
    UrgentRequests = COALESCE(NULLIF((SELECT COUNT(1) FROM Portal.AdminRecord r WHERE r.TenantId = @TenantId AND r.Kind = N'SelfServiceRequest' AND r.JsonData LIKE N'%Urgent%' AND r.IsDeleted = 0), 0), w.UrgentRequests),
    SharedDocuments = COALESCE(NULLIF((SELECT COUNT(1) FROM Portal.AdminRecord d WHERE d.TenantId = @TenantId AND d.Kind = N'PortalDocument' AND d.IsDeleted = 0), 0), w.SharedDocuments),
    MobileInstalls = COALESCE(NULLIF((SELECT COUNT(1) FROM Portal.AdminRecord mi WHERE mi.TenantId = @TenantId AND mi.Kind = N'PortalMobileInstall' AND mi.IsDeleted = 0), 0), w.MobileInstalls),
    ChatSessions30d = COALESCE(NULLIF((SELECT COUNT(1) FROM Portal.ChatSession cs WHERE cs.TenantId = @TenantId AND cs.IsDeleted = 0 AND cs.StartedDateUtc >= DATEADD(DAY, -30, SYSUTCDATETIME())), 0), w.ChatSessions30d),
    LiveHandoffs30d = COALESCE(NULLIF((SELECT COUNT(1) FROM Portal.ChatSession cs WHERE cs.TenantId = @TenantId AND cs.IsDeleted = 0 AND cs.HandoffRequired = 1 AND cs.StartedDateUtc >= DATEADD(DAY, -30, SYSUTCDATETIME())), 0), w.LiveHandoffs30d),
    ModifiedDateUtc = SYSUTCDATETIME()
FROM Portal.WhiteLabelConfiguration w
WHERE w.TenantId = @TenantId AND w.IsDeleted = 0;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
    }

    private async Task EnsurePortalMyAccountProfileDataAsync(Guid tenantId, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);

        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Portal') EXEC(N'CREATE SCHEMA Portal');

IF OBJECT_ID(N'Portal.MyAccountProfile', N'U') IS NULL
BEGIN
    CREATE TABLE Portal.MyAccountProfile
    (
        MyAccountProfileId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Portal_MyAccountProfile PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AgencyName NVARCHAR(200) NOT NULL,
        AdminName NVARCHAR(200) NOT NULL CONSTRAINT DF_MyAccount_AdminName DEFAULT N'Tenant Admin',
        AdminEmail NVARCHAR(320) NOT NULL,
        AdminRole NVARCHAR(80) NOT NULL CONSTRAINT DF_MyAccount_AdminRole DEFAULT N'Tenant Admin',
        AdminPhone NVARCHAR(50) NOT NULL CONSTRAINT DF_MyAccount_AdminPhone DEFAULT N'',
        TimeZone NVARCHAR(120) NOT NULL CONSTRAINT DF_MyAccount_TimeZone DEFAULT N'Central Standard Time',
        Locale NVARCHAR(40) NOT NULL CONSTRAINT DF_MyAccount_Locale DEFAULT N'en-US',
        PlanName NVARCHAR(120) NOT NULL CONSTRAINT DF_MyAccount_PlanName DEFAULT N'Enterprise',
        PlanStatus NVARCHAR(80) NOT NULL CONSTRAINT DF_MyAccount_PlanStatus DEFAULT N'Active',
        RenewalDateUtc DATETIME2 NOT NULL,
        PortalUsers INT NOT NULL CONSTRAINT DF_MyAccount_PortalUsers DEFAULT 0,
        ActivePortalUsers INT NOT NULL CONSTRAINT DF_MyAccount_ActiveUsers DEFAULT 0,
        PendingInvites INT NOT NULL CONSTRAINT DF_MyAccount_PendingInvites DEFAULT 0,
        OpenRequests INT NOT NULL CONSTRAINT DF_MyAccount_OpenRequests DEFAULT 0,
        UrgentRequests INT NOT NULL CONSTRAINT DF_MyAccount_UrgentRequests DEFAULT 0,
        SharedDocuments INT NOT NULL CONSTRAINT DF_MyAccount_SharedDocuments DEFAULT 0,
        StorageUsedGb INT NOT NULL CONSTRAINT DF_MyAccount_StorageUsed DEFAULT 0,
        StorageLimitGb INT NOT NULL CONSTRAINT DF_MyAccount_StorageLimit DEFAULT 250,
        MonthlyLoginCount INT NOT NULL CONSTRAINT DF_MyAccount_LoginCount DEFAULT 0,
        MobileInstalls INT NOT NULL CONSTRAINT DF_MyAccount_MobileInstalls DEFAULT 0,
        ChatSessions30d INT NOT NULL CONSTRAINT DF_MyAccount_ChatSessions DEFAULT 0,
        ApiCalls30d INT NOT NULL CONSTRAINT DF_MyAccount_ApiCalls DEFAULT 0,
        LastPortalPublishUtc DATETIME2 NOT NULL,
        LastAdminLoginUtc DATETIME2 NOT NULL,
        MfaEnabled BIT NOT NULL CONSTRAINT DF_MyAccount_Mfa DEFAULT 1,
        SsoEnabled BIT NOT NULL CONSTRAINT DF_MyAccount_Sso DEFAULT 0,
        BrandingPublished BIT NOT NULL CONSTRAINT DF_MyAccount_Branding DEFAULT 1,
        MobileAppPublished BIT NOT NULL CONSTRAINT DF_MyAccount_Mobile DEFAULT 1,
        ChatEnabled BIT NOT NULL CONSTRAINT DF_MyAccount_Chat DEFAULT 1,
        SupportEmail NVARCHAR(320) NOT NULL,
        SupportPhone NVARCHAR(50) NOT NULL CONSTRAINT DF_MyAccount_SupportPhone DEFAULT N'',
        PortalDomain NVARCHAR(255) NOT NULL,
        HealthJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_MyAccount_Health DEFAULT N'[]',
        ActivityJson NVARCHAR(MAX) NOT NULL CONSTRAINT DF_MyAccount_Activity DEFAULT N'[]',
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_MyAccount_Created DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_MyAccount_IsDeleted DEFAULT 0
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Portal.MyAccountProfile') AND name = N'UX_MyAccount_Tenant')
    CREATE UNIQUE INDEX UX_MyAccount_Tenant ON Portal.MyAccountProfile(TenantId) WHERE IsDeleted = 0;

DECLARE @AgencyName NVARCHAR(200) = COALESCE((SELECT TOP 1 TenantName FROM Core.Tenant WHERE TenantId = @TenantId), N'Demo Agency');
DECLARE @AdminEmail NVARCHAR(320) = COALESCE((SELECT TOP 1 Email FROM IAM.[User] WHERE TenantId = @TenantId ORDER BY CreatedDateUtc), N'admin@demoagency.com');
DECLARE @SupportEmail NVARCHAR(320) = COALESCE((SELECT TOP 1 ContactEmail FROM Agency.Profile WHERE TenantId = @TenantId AND IsDeleted = 0), @AdminEmail);
DECLARE @SupportPhone NVARCHAR(50) = COALESCE((SELECT TOP 1 ContactPhone FROM Agency.Profile WHERE TenantId = @TenantId AND IsDeleted = 0), N'(555) 000-0000');
DECLARE @PortalDomain NVARCHAR(255) = CONCAT(N'portal.', LOWER(REPLACE(REPLACE(@AgencyName, N' ', N''), N'.', N'')), N'.com');

INSERT INTO Portal.MyAccountProfile
(MyAccountProfileId, TenantId, AgencyName, AdminName, AdminEmail, AdminRole, AdminPhone, TimeZone, Locale, PlanName, PlanStatus, RenewalDateUtc, PortalUsers, ActivePortalUsers, PendingInvites, OpenRequests, UrgentRequests, SharedDocuments, StorageUsedGb, StorageLimitGb, MonthlyLoginCount, MobileInstalls, ChatSessions30d, ApiCalls30d, LastPortalPublishUtc, LastAdminLoginUtc, MfaEnabled, SsoEnabled, BrandingPublished, MobileAppPublished, ChatEnabled, SupportEmail, SupportPhone, PortalDomain, HealthJson, ActivityJson, CreatedDateUtc, IsDeleted)
SELECT @ExistingId, @TenantId, @AgencyName, N'Tenant Admin', @AdminEmail, N'Tenant Admin', @SupportPhone, N'Central Standard Time', N'en-US', N'Enterprise', N'Active', DATEADD(MONTH, 8, SYSUTCDATETIME()), 52, 47, 6, 23, 3, 184, 42, 250, 1260, 23, 184, 50410, DATEADD(DAY, -4, SYSUTCDATETIME()), DATEADD(HOUR, -2, SYSUTCDATETIME()), 1, 0, 1, 1, 1, @SupportEmail, @SupportPhone, @PortalDomain,
       N'[{""name"":""Portal availability"",""status"":""Healthy"",""detail"":""All portal systems operational"",""icon"":""bi-check-circle""},{""name"":""Security posture"",""status"":""Watch"",""detail"":""SSO not enabled; MFA is active"",""icon"":""bi-shield-lock""},{""name"":""Storage capacity"",""status"":""Healthy"",""detail"":""42 GB of 250 GB used"",""icon"":""bi-hdd""}]',
       N'[{""title"":""Branding published"",""detail"":""White-label portal configuration is live"",""severity"":""Healthy"",""icon"":""bi-palette""},{""title"":""Urgent request queue"",""detail"":""3 urgent self-service requests need review"",""severity"":""Watch"",""icon"":""bi-exclamation-triangle""},{""title"":""Admin login"",""detail"":""Tenant admin accessed portal console"",""severity"":""Info"",""icon"":""bi-person-check""}]',
       SYSUTCDATETIME(), 0
WHERE NOT EXISTS (SELECT 1 FROM Portal.MyAccountProfile WHERE TenantId = @TenantId AND IsDeleted = 0);";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var existingId = await cn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition("SELECT TOP 1 PortalAdminRecordId FROM Portal.AdminRecord WHERE TenantId = @TenantId AND Kind = N'PortalMyAccount' AND Code = N'my-account' AND IsDeleted = 0", new { TenantId = tenantId }, cancellationToken: ct));
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, ExistingId = existingId ?? Guid.NewGuid() }, cancellationToken: ct));
    }

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
        await EnsurePortalActivityEventDataAsync(tenantId, ct);
        const string sql = @"
SELECT ActivityEventId AS Id,
       TenantId,
       EventNumber,
       OccurredAtUtc,
       UserName,
       UserEmail,
       AccountName,
       EventType,
       Category,
       Severity,
       Status,
       Detail,
       WorkflowImpact,
       RecommendedAction,
       AssignedTo,
       IpAddress,
       Device,
       Location,
       RiskScore,
       DurationSeconds,
       RequiresReview,
       ReviewedDateUtc,
       ReviewedBy,
       CreatedDateUtc,
       ModifiedDateUtc
FROM Portal.ActivityEvent
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = '' OR UserName LIKE '%' + @SearchTerm + '%' OR UserEmail LIKE '%' + @SearchTerm + '%' OR AccountName LIKE '%' + @SearchTerm + '%' OR EventType LIKE '%' + @SearchTerm + '%' OR Detail LIKE '%' + @SearchTerm + '%')
ORDER BY OccurredAtUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var items = (await cn.QueryAsync<PortalActivityEventDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: ct))).AsList();
        return Ok(new PagedResult<PortalActivityEventDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpGet("mobile-installs")]
    public async Task<IActionResult> GetMobileInstalls([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken ct)
    {
        await EnsurePortalMobileInstallDataAsync(tenantId, ct);
        const string sql = @"
SELECT MobileInstallId AS Id,
       TenantId,
       InstallNumber,
       AccountName,
       UserName,
       UserEmail,
       Platform,
       DeviceModel,
       AppVersion,
       OsVersion,
       Status,
       ComplianceStatus,
       RiskLevel,
       EnrollmentType,
       LastIpAddress,
       LastLocation,
       PushTokenStatus,
       RecommendedAction,
       InstalledDateUtc,
       LastSeenDateUtc,
       LastPushDateUtc,
       Sessions30d,
       DocumentsViewed30d,
       RequestsSubmitted30d,
       PushesSent30d,
       BiometricEnabled,
       MfaVerified,
       OfflineAccessEnabled,
       UpdateRequired,
       TrustedDevice,
       CreatedDateUtc,
       ModifiedDateUtc
FROM Portal.MobileInstall
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = ''
       OR InstallNumber LIKE '%' + @SearchTerm + '%'
       OR AccountName LIKE '%' + @SearchTerm + '%'
       OR UserName LIKE '%' + @SearchTerm + '%'
       OR UserEmail LIKE '%' + @SearchTerm + '%'
       OR Platform LIKE '%' + @SearchTerm + '%'
       OR DeviceModel LIKE '%' + @SearchTerm + '%'
       OR Status LIKE '%' + @SearchTerm + '%'
       OR ComplianceStatus LIKE '%' + @SearchTerm + '%')
ORDER BY CASE RiskLevel WHEN N'Critical' THEN 0 WHEN N'High' THEN 1 WHEN N'Medium' THEN 2 ELSE 3 END,
         CASE WHEN UpdateRequired = 1 THEN 0 ELSE 1 END,
         LastSeenDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var items = (await cn.QueryAsync<PortalMobileInstallDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: ct))).AsList();
        return Ok(new PagedResult<PortalMobileInstallDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpPost("mobile-installs/{id:guid}/status")]
    public async Task<IActionResult> UpdateMobileInstallStatus(Guid id, [FromBody] UpdatePortalMobileInstallRequest request, CancellationToken ct)
    {
        await EnsurePortalMobileInstallDataAsync(request.TenantId, ct);
        const string sql = @"
UPDATE Portal.MobileInstall
SET Status = @Status,
    ComplianceStatus = @ComplianceStatus,
    RiskLevel = @RiskLevel,
    RecommendedAction = @RecommendedAction,
    UpdateRequired = @UpdateRequired,
    TrustedDevice = @TrustedDevice,
    LastPushDateUtc = CASE WHEN @UpdateRequired = 1 THEN SYSUTCDATETIME() ELSE LastPushDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE MobileInstallId = @Id AND TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.Status, request.ComplianceStatus, request.RiskLevel, request.RecommendedAction, request.UpdateRequired, request.TrustedDevice }, cancellationToken: ct));
        await SyncMobileInstallAdminRecordsAsync(cn, request.TenantId, ct);
        return affected == 0 ? NotFound() : NoContent();
    }

    [HttpGet("api-usage")]
    public async Task<IActionResult> GetApiUsage([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken ct)
    {
        await EnsurePortalApiUsageDataAsync(tenantId, ct);
        const string sql = @"
SELECT ApiUsageId AS Id,
       TenantId,
       EndpointCode,
       EndpointName,
       Method,
       Route,
       IntegrationName,
       ApiKeyName,
       Status,
       HealthStatus,
       Priority,
       Owner,
       Detail,
       RecommendedAction,
       LastCallUtc,
       Calls30d,
       SuccessCount30d,
       WarningCount30d,
       ErrorCount30d,
       AvgLatencyMs,
       P95LatencyMs,
       RateLimitPerMinute,
       QuotaUsedPercent,
       WebhookDeliveries30d,
       RetryCount30d,
       RequiresReview,
       ReviewedDateUtc,
       CreatedDateUtc,
       ModifiedDateUtc
FROM Portal.ApiUsage
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = ''
       OR EndpointCode LIKE '%' + @SearchTerm + '%'
       OR EndpointName LIKE '%' + @SearchTerm + '%'
       OR Route LIKE '%' + @SearchTerm + '%'
       OR IntegrationName LIKE '%' + @SearchTerm + '%'
       OR ApiKeyName LIKE '%' + @SearchTerm + '%'
       OR Status LIKE '%' + @SearchTerm + '%'
       OR HealthStatus LIKE '%' + @SearchTerm + '%'
       OR Detail LIKE '%' + @SearchTerm + '%')
ORDER BY CASE WHEN RequiresReview = 1 THEN 0 ELSE 1 END,
         CASE Priority WHEN N'Critical' THEN 0 WHEN N'High' THEN 1 WHEN N'Normal' THEN 2 ELSE 3 END,
         QuotaUsedPercent DESC,
         ErrorCount30d DESC,
         LastCallUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var items = (await cn.QueryAsync<PortalApiUsageDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: ct))).AsList();
        return Ok(new PagedResult<PortalApiUsageDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpPost("api-usage/{id:guid}/status")]
    public async Task<IActionResult> UpdateApiUsageStatus(Guid id, [FromBody] UpdatePortalApiUsageRequest request, CancellationToken ct)
    {
        await EnsurePortalApiUsageDataAsync(request.TenantId, ct);
        const string sql = @"
UPDATE Portal.ApiUsage
SET Status = @Status,
    HealthStatus = @HealthStatus,
    Priority = @Priority,
    Owner = @Owner,
    RecommendedAction = @RecommendedAction,
    RequiresReview = @RequiresReview,
    ReviewedDateUtc = CASE WHEN @RequiresReview = 0 THEN SYSUTCDATETIME() ELSE ReviewedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE ApiUsageId = @Id AND TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.Status, request.HealthStatus, request.Priority, request.Owner, request.RecommendedAction, request.RequiresReview }, cancellationToken: ct));
        await SyncApiUsageAdminRecordsAsync(cn, request.TenantId, ct);
        return affected == 0 ? NotFound() : NoContent();
    }

    [HttpPost("activity/{id:guid}/status")]
    public async Task<IActionResult> UpdateActivityStatus(Guid id, [FromBody] UpdatePortalActivityEventRequest request, CancellationToken ct)
    {
        await EnsurePortalActivityEventDataAsync(request.TenantId, ct);
        const string sql = @"
UPDATE Portal.ActivityEvent
SET Status = @Status,
    AssignedTo = COALESCE(NULLIF(@AssignedTo, N''), AssignedTo),
    RecommendedAction = COALESCE(NULLIF(@RecommendedAction, N''), RecommendedAction),
    RequiresReview = @RequiresReview,
    ReviewedDateUtc = CASE WHEN @Status IN (N'Reviewed', N'Acknowledged', N'Resolved') THEN SYSUTCDATETIME() ELSE ReviewedDateUtc END,
    ReviewedBy = CASE WHEN @Status IN (N'Reviewed', N'Acknowledged', N'Resolved') THEN COALESCE(NULLIF(@AssignedTo, N''), N'Portal Ops') ELSE ReviewedBy END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE ActivityEventId = @Id AND TenantId = @TenantId AND IsDeleted = 0;

UPDATE Portal.AdminRecord
SET Status = @Status,
    JsonData = CASE WHEN ISJSON(JsonData) = 1 THEN JSON_MODIFY(JSON_MODIFY(JsonData, '$.severity', @Status), '$.detail', COALESCE(NULLIF(@RecommendedAction, N''), JSON_VALUE(JsonData, '$.detail'))) ELSE JsonData END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE PortalAdminRecordId = @Id AND TenantId = @TenantId AND Kind = N'PortalActivity' AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.Status, AssignedTo = request.AssignedTo ?? string.Empty, RecommendedAction = request.RecommendedAction ?? string.Empty, request.RequiresReview }, cancellationToken: ct));
        return NoContent();
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
        await EnsurePortalMyAccountProfileDataAsync(tenantId, ct);
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var account = await GetMyAccountDtoAsync(cn, tenantId, ct);
        return account is null ? NotFound() : Ok(account);
    }

    [HttpPut("my-account/profile")]
    public async Task<IActionResult> UpdateMyAccountProfile([FromBody] UpdatePortalMyAccountRequest request, CancellationToken ct)
    {
        await EnsurePortalMyAccountProfileDataAsync(request.TenantId, ct);
        const string sql = @"
UPDATE Portal.MyAccountProfile
SET AgencyName = @AgencyName,
    AdminName = @AdminName,
    AdminEmail = @AdminEmail,
    AdminRole = @AdminRole,
    AdminPhone = @AdminPhone,
    TimeZone = @TimeZone,
    Locale = @Locale,
    PlanName = @PlanName,
    PlanStatus = @PlanStatus,
    RenewalDateUtc = @RenewalDateUtc,
    PortalUsers = @PortalUsers,
    ActivePortalUsers = @ActivePortalUsers,
    PendingInvites = @PendingInvites,
    OpenRequests = @OpenRequests,
    UrgentRequests = @UrgentRequests,
    SharedDocuments = @SharedDocuments,
    StorageUsedGb = @StorageUsedGb,
    StorageLimitGb = @StorageLimitGb,
    MonthlyLoginCount = @MonthlyLoginCount,
    MobileInstalls = @MobileInstalls,
    ChatSessions30d = @ChatSessions30d,
    ApiCalls30d = @ApiCalls30d,
    MfaEnabled = @MfaEnabled,
    SsoEnabled = @SsoEnabled,
    BrandingPublished = @BrandingPublished,
    MobileAppPublished = @MobileAppPublished,
    ChatEnabled = @ChatEnabled,
    SupportEmail = @SupportEmail,
    SupportPhone = @SupportPhone,
    PortalDomain = @PortalDomain,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: ct));
        var account = await GetMyAccountDtoAsync(cn, request.TenantId, ct);
        if (account is not null)
            await SyncMyAccountAdminRecordAsync(cn, account, ct);
        return NoContent();
    }

    [HttpGet("white-label")]
    public async Task<IActionResult> GetWhiteLabel([FromQuery] Guid tenantId, CancellationToken ct)
    {
        await EnsurePortalWhiteLabelDataAsync(tenantId, ct);
        const string sql = @"
SELECT WhiteLabelConfigurationId AS Id,
       TenantId,
       DisplayName,
       PortalDomain,
       DomainStatus,
       PublishStatus,
       LastPublishedDateUtc,
       PrimaryColor,
       AccentColor,
       NavBackgroundColor,
       NavTextColor,
       LogoUrl,
       FaviconUrl,
       WelcomeMessage,
       SupportEmail,
       SupportPhone,
       ShowAgencyLogo,
       HidePoweredBy,
       ShowNewsWidget,
       ShowSupportChat,
       EnableAnnouncements,
       EnableCrossSellWidget,
       MobileAppName,
       MobileBundleId,
       IosStoreUrl,
       AndroidStoreUrl,
       MobileVersion,
       MinimumMobileVersion,
       MobilePublished,
       BiometricLogin,
       PushNotifications,
       OfflinePolicyView,
       ForceMobileUpdate,
       RequireMfaOnMobile,
       AssistantName,
       AssistantWelcomeMessage,
       ChatWidgetColor,
       ChatPosition,
       ChatEscalationEmail,
       OfficeHours,
       ChatEnabled,
       AiResponsesEnabled,
       LiveHandoffEnabled,
       ShowChatOnMobile,
       AllowFileAttachments,
       TranscriptEmailEnabled,
       IdentityProvider,
       SsoClientId,
       SsoMetadataUrl,
       RedirectUris,
       SsoEnabled,
       MfaRequired,
       AllowSocialLogin,
       AutoProvisionUsers,
       PasswordMinLength,
       SessionTimeoutMinutes,
       MaxFailedLoginAttempts,
       LockoutMinutes,
       RequireUppercase,
       RequireSpecialCharacter,
       IpWhitelistEnabled,
       ActivePortalUsers,
       PendingInvites,
       MobileInstalls,
       ChatSessions30d,
       OpenRequests,
       UrgentRequests,
       SharedDocuments,
       ApiCalls30d,
       CsATScore,
       AiResolutionRate,
       LiveHandoffs30d,
       AverageResponseSeconds,
       ConfigurationJson,
       CreatedDateUtc,
       ModifiedDateUtc
FROM Portal.WhiteLabelConfiguration
WHERE TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var item = await cn.QuerySingleOrDefaultAsync<PortalWhiteLabelConfigurationDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPut("white-label")]
    public async Task<IActionResult> UpdateWhiteLabel([FromBody] UpdatePortalWhiteLabelConfigurationRequest request, CancellationToken ct)
    {
        await EnsurePortalWhiteLabelDataAsync(request.TenantId, ct);
        const string sql = @"
UPDATE Portal.WhiteLabelConfiguration
SET DisplayName = @DisplayName,
    PortalDomain = @PortalDomain,
    DomainStatus = @DomainStatus,
    PublishStatus = @PublishStatus,
    PrimaryColor = @PrimaryColor,
    AccentColor = @AccentColor,
    NavBackgroundColor = @NavBackgroundColor,
    NavTextColor = @NavTextColor,
    LogoUrl = @LogoUrl,
    FaviconUrl = @FaviconUrl,
    WelcomeMessage = @WelcomeMessage,
    SupportEmail = @SupportEmail,
    SupportPhone = @SupportPhone,
    ShowAgencyLogo = @ShowAgencyLogo,
    HidePoweredBy = @HidePoweredBy,
    ShowNewsWidget = @ShowNewsWidget,
    ShowSupportChat = @ShowSupportChat,
    EnableAnnouncements = @EnableAnnouncements,
    EnableCrossSellWidget = @EnableCrossSellWidget,
    MobileAppName = @MobileAppName,
    MobileBundleId = @MobileBundleId,
    IosStoreUrl = @IosStoreUrl,
    AndroidStoreUrl = @AndroidStoreUrl,
    MobileVersion = @MobileVersion,
    MinimumMobileVersion = @MinimumMobileVersion,
    MobilePublished = @MobilePublished,
    BiometricLogin = @BiometricLogin,
    PushNotifications = @PushNotifications,
    OfflinePolicyView = @OfflinePolicyView,
    ForceMobileUpdate = @ForceMobileUpdate,
    RequireMfaOnMobile = @RequireMfaOnMobile,
    AssistantName = @AssistantName,
    AssistantWelcomeMessage = @AssistantWelcomeMessage,
    ChatWidgetColor = @ChatWidgetColor,
    ChatPosition = @ChatPosition,
    ChatEscalationEmail = @ChatEscalationEmail,
    OfficeHours = @OfficeHours,
    ChatEnabled = @ChatEnabled,
    AiResponsesEnabled = @AiResponsesEnabled,
    LiveHandoffEnabled = @LiveHandoffEnabled,
    ShowChatOnMobile = @ShowChatOnMobile,
    AllowFileAttachments = @AllowFileAttachments,
    TranscriptEmailEnabled = @TranscriptEmailEnabled,
    IdentityProvider = @IdentityProvider,
    SsoClientId = @SsoClientId,
    SsoMetadataUrl = @SsoMetadataUrl,
    RedirectUris = @RedirectUris,
    SsoEnabled = @SsoEnabled,
    MfaRequired = @MfaRequired,
    AllowSocialLogin = @AllowSocialLogin,
    AutoProvisionUsers = @AutoProvisionUsers,
    PasswordMinLength = @PasswordMinLength,
    SessionTimeoutMinutes = @SessionTimeoutMinutes,
    MaxFailedLoginAttempts = @MaxFailedLoginAttempts,
    LockoutMinutes = @LockoutMinutes,
    RequireUppercase = @RequireUppercase,
    RequireSpecialCharacter = @RequireSpecialCharacter,
    IpWhitelistEnabled = @IpWhitelistEnabled,
    ConfigurationJson = @ConfigurationJson,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, request, cancellationToken: ct));
        await SyncWhiteLabelAdminRecordsAsync(cn, request, ct);
        return NoContent();
    }

    [HttpPost("white-label/publish")]
    public async Task<IActionResult> PublishWhiteLabel([FromQuery] Guid tenantId, CancellationToken ct)
    {
        await EnsurePortalWhiteLabelDataAsync(tenantId, ct);
        const string sql = @"
UPDATE Portal.WhiteLabelConfiguration
SET PublishStatus = N'Live',
    DomainStatus = CASE WHEN DomainStatus = N'Pending DNS' THEN N'Verified' ELSE DomainStatus END,
    LastPublishedDateUtc = SYSUTCDATETIME(),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return NoContent();
    }

    [HttpPost("white-label/action")]
    public async Task<IActionResult> RunWhiteLabelAction([FromQuery] Guid tenantId, [FromQuery] string action, CancellationToken ct)
    {
        await EnsurePortalWhiteLabelDataAsync(tenantId, ct);
        var (domainStatus, publishStatus, configPatch) = action switch
        {
            "verify-domain" => ("Verified", (string?)null, "domain verified"),
            "mark-draft" => ((string?)null, "Draft", "draft mode"),
            "enable-sso" => ((string?)null, (string?)null, "sso enabled"),
            "enable-chat" => ((string?)null, (string?)null, "chat enabled"),
            "force-mobile-update" => ((string?)null, (string?)null, "mobile update required"),
            _ => ((string?)null, (string?)null, "action recorded")
        };
        const string sql = @"
UPDATE Portal.WhiteLabelConfiguration
SET DomainStatus = COALESCE(@DomainStatus, DomainStatus),
    PublishStatus = COALESCE(@PublishStatus, PublishStatus),
    SsoEnabled = CASE WHEN @Action = N'enable-sso' THEN 1 ELSE SsoEnabled END,
    ChatEnabled = CASE WHEN @Action = N'enable-chat' THEN 1 ELSE ChatEnabled END,
    ForceMobileUpdate = CASE WHEN @Action = N'force-mobile-update' THEN 1 ELSE ForceMobileUpdate END,
    ConfigurationJson = JSON_MODIFY(CASE WHEN ISJSON(ConfigurationJson) = 1 THEN ConfigurationJson ELSE N'{}' END, '$.lastAction', @ConfigPatch),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId, Action = action, DomainStatus = domainStatus, PublishStatus = publishStatus, ConfigPatch = configPatch }, cancellationToken: ct));
        return NoContent();
    }

    [HttpGet("metrics/{kind}")]
    public async Task<IActionResult> GetMetrics(string kind, [FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken ct)
    {
        await EnsurePortalAdminDataAsync(tenantId, ct);
        return Ok(await ReadJsonRecordsAsync<PortalMetricRecordDto>(tenantId, kind, searchTerm, ct));
    }

    [HttpGet("chat-sessions")]
    public async Task<IActionResult> GetChatSessions([FromQuery] Guid tenantId, [FromQuery] string? searchTerm, CancellationToken ct)
    {
        await EnsurePortalChatSessionDataAsync(tenantId, ct);
        const string sql = @"
SELECT ChatSessionId AS Id,
       TenantId,
       SessionNumber,
       ClientName,
       AccountName,
       ContactEmail,
       Channel,
       Topic,
       Status,
       Priority,
       Sentiment,
       AssignedTo,
       Summary,
       NextBestAction,
       StartedDateUtc,
       LastMessageDateUtc,
       ResolvedDateUtc,
       MessageCount,
       WaitSeconds,
       SlaDueDateUtc,
       AiHandled,
       HandoffRequired,
       ReviewedDateUtc
FROM Portal.ChatSession
WHERE TenantId = @TenantId AND IsDeleted = 0
  AND (@SearchTerm IS NULL OR @SearchTerm = ''
       OR SessionNumber LIKE '%' + @SearchTerm + '%'
       OR ClientName LIKE '%' + @SearchTerm + '%'
       OR AccountName LIKE '%' + @SearchTerm + '%'
       OR ContactEmail LIKE '%' + @SearchTerm + '%'
       OR Topic LIKE '%' + @SearchTerm + '%'
       OR Status LIKE '%' + @SearchTerm + '%'
       OR Priority LIKE '%' + @SearchTerm + '%'
       OR Sentiment LIKE '%' + @SearchTerm + '%'
       OR AssignedTo LIKE '%' + @SearchTerm + '%'
       OR Summary LIKE '%' + @SearchTerm + '%')
ORDER BY CASE WHEN HandoffRequired = 1 THEN 0 WHEN Status = N'Open' THEN 1 WHEN ReviewedDateUtc IS NULL THEN 2 ELSE 3 END,
         CASE Priority WHEN N'Urgent' THEN 0 WHEN N'High' THEN 1 WHEN N'Normal' THEN 2 ELSE 3 END,
         LastMessageDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var items = (await cn.QueryAsync<PortalChatSessionDto>(new CommandDefinition(sql, new { TenantId = tenantId, SearchTerm = searchTerm }, cancellationToken: ct))).AsList();
        return Ok(new PagedResult<PortalChatSessionDto> { Items = items, TotalCount = items.Count, PageNumber = 1, PageSize = items.Count });
    }

    [HttpPost("chat-sessions/{id:guid}/status")]
    public async Task<IActionResult> UpdateChatSessionStatus(Guid id, [FromBody] UpdatePortalChatSessionStatusRequest request, CancellationToken ct)
    {
        await EnsurePortalChatSessionDataAsync(request.TenantId, ct);
        const string sql = @"
UPDATE Portal.ChatSession
SET Status = @Status,
    AssignedTo = COALESCE(NULLIF(@AssignedTo, N''), AssignedTo),
    NextBestAction = COALESCE(NULLIF(@NextBestAction, N''), NextBestAction),
    ReviewedDateUtc = CASE WHEN @Status IN (N'Reviewed', N'Resolved by Agent', N'AI Resolved') THEN COALESCE(ReviewedDateUtc, SYSUTCDATETIME()) ELSE ReviewedDateUtc END,
    ResolvedDateUtc = CASE WHEN @Status IN (N'Resolved by Agent', N'AI Resolved') THEN COALESCE(ResolvedDateUtc, SYSUTCDATETIME()) ELSE ResolvedDateUtc END,
    HandoffRequired = CASE WHEN @Status IN (N'Reviewed', N'Resolved by Agent', N'AI Resolved') THEN 0 ELSE HandoffRequired END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE ChatSessionId = @Id AND TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        var affected = await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.TenantId, request.Status, request.AssignedTo, request.NextBestAction }, cancellationToken: ct));
        return affected == 0 ? NotFound() : NoContent();
    }

    [HttpPut("my-account")]
    public async Task<IActionResult> UpdateMyAccount([FromQuery] Guid tenantId, [FromBody] PortalMyAccountDto account, CancellationToken ct)
    {
        await EnsurePortalMyAccountProfileDataAsync(tenantId, ct);
        account.TenantId = tenantId;
        const string sql = @"
UPDATE Portal.MyAccountProfile
SET AgencyName = @AgencyName,
    AdminName = @AdminName,
    AdminEmail = @AdminEmail,
    AdminRole = @AdminRole,
    AdminPhone = @AdminPhone,
    TimeZone = @TimeZone,
    Locale = @Locale,
    PlanName = @PlanName,
    PlanStatus = @PlanStatus,
    RenewalDateUtc = @RenewalDateUtc,
    PortalUsers = @PortalUsers,
    ActivePortalUsers = @ActivePortalUsers,
    PendingInvites = @PendingInvites,
    OpenRequests = @OpenRequests,
    UrgentRequests = @UrgentRequests,
    SharedDocuments = @SharedDocuments,
    StorageUsedGb = @StorageUsedGb,
    StorageLimitGb = @StorageLimitGb,
    MonthlyLoginCount = @MonthlyLoginCount,
    MobileInstalls = @MobileInstalls,
    ChatSessions30d = @ChatSessions30d,
    ApiCalls30d = @ApiCalls30d,
    MfaEnabled = @MfaEnabled,
    SsoEnabled = @SsoEnabled,
    BrandingPublished = @BrandingPublished,
    MobileAppPublished = @MobileAppPublished,
    ChatEnabled = @ChatEnabled,
    SupportEmail = @SupportEmail,
    SupportPhone = @SupportPhone,
    PortalDomain = @PortalDomain,
    HealthJson = @HealthJson,
    ActivityJson = @ActivityJson,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(ct);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            account.TenantId,
            account.AgencyName,
            account.AdminName,
            account.AdminEmail,
            account.AdminRole,
            account.AdminPhone,
            account.TimeZone,
            account.Locale,
            account.PlanName,
            account.PlanStatus,
            account.RenewalDateUtc,
            account.PortalUsers,
            account.ActivePortalUsers,
            account.PendingInvites,
            account.OpenRequests,
            account.UrgentRequests,
            account.SharedDocuments,
            account.StorageUsedGb,
            account.StorageLimitGb,
            account.MonthlyLoginCount,
            account.MobileInstalls,
            account.ChatSessions30d,
            account.ApiCalls30d,
            account.MfaEnabled,
            account.SsoEnabled,
            account.BrandingPublished,
            account.MobileAppPublished,
            account.ChatEnabled,
            account.SupportEmail,
            account.SupportPhone,
            account.PortalDomain,
            HealthJson = JsonSerializer.Serialize(account.HealthChecks, JsonOptions),
            ActivityJson = JsonSerializer.Serialize(account.RecentActivity, JsonOptions)
        }, cancellationToken: ct));
        var saved = await GetMyAccountDtoAsync(cn, tenantId, ct);
        if (saved is not null)
            await SyncMyAccountAdminRecordAsync(cn, saved, ct);
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

    private static async Task SyncWhiteLabelAdminRecordsAsync(System.Data.IDbConnection cn, UpdatePortalWhiteLabelConfigurationRequest request, CancellationToken ct)
    {
        var branding = new PortalBrandingSettingsDto
        {
            DisplayName = request.DisplayName,
            Domain = request.PortalDomain,
            SupportEmail = request.SupportEmail,
            SupportPhone = request.SupportPhone,
            WelcomeMessage = request.WelcomeMessage,
            PrimaryColor = request.PrimaryColor,
            AccentColor = request.AccentColor,
            NavBg = request.NavBackgroundColor,
            NavText = request.NavTextColor,
            EmailFromName = request.DisplayName,
            EmailReplyTo = request.SupportEmail,
            EmailFooter = $"{request.DisplayName} · Client Portal Support · {request.SupportPhone}",
            ShowAgencyLogo = request.ShowAgencyLogo,
            ShowPoweredBy = !request.HidePoweredBy,
            ShowSupportChat = request.ShowSupportChat,
            ShowNewsWidget = request.ShowNewsWidget
        };
        var mobile = new PortalMobileSettingsDto
        {
            AppName = request.MobileAppName,
            IosUrl = request.IosStoreUrl,
            AndroidUrl = request.AndroidStoreUrl,
            BundleId = request.MobileBundleId,
            AppVersion = request.MobileVersion,
            BiometricLogin = request.BiometricLogin,
            ForceAppLock = request.ForceMobileUpdate,
            LockTimeoutMinutes = request.SessionTimeoutMinutes,
            RequireMfaOnMobile = request.RequireMfaOnMobile
        };

        var brandingJson = JsonSerializer.Serialize(branding, JsonOptions);
        var mobileJson = JsonSerializer.Serialize(mobile, JsonOptions);
        const string sql = @"
UPDATE Portal.AdminRecord
SET Name = @BrandingName, Status = @BrandingStatus, JsonData = @BrandingJson, ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND Kind = N'PortalBranding' AND Code = N'branding' AND IsDeleted = 0;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO Portal.AdminRecord (PortalAdminRecordId, TenantId, Kind, Code, Name, Status, JsonData, CreatedDateUtc, IsDeleted)
    VALUES (NEWID(), @TenantId, N'PortalBranding', N'branding', @BrandingName, @BrandingStatus, @BrandingJson, SYSUTCDATETIME(), 0);
END;

UPDATE Portal.AdminRecord
SET Name = @MobileName, Status = @MobileStatus, JsonData = @MobileJson, ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND Kind = N'PortalMobile' AND Code = N'mobile' AND IsDeleted = 0;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO Portal.AdminRecord (PortalAdminRecordId, TenantId, Kind, Code, Name, Status, JsonData, CreatedDateUtc, IsDeleted)
    VALUES (NEWID(), @TenantId, N'PortalMobile', N'mobile', @MobileName, @MobileStatus, @MobileJson, SYSUTCDATETIME(), 0);
END;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            request.TenantId,
            BrandingName = request.DisplayName,
            BrandingStatus = request.PublishStatus,
            BrandingJson = brandingJson,
            MobileName = request.MobileAppName,
            MobileStatus = request.MobilePublished ? "Published" : "Draft",
            MobileJson = mobileJson
        }, cancellationToken: ct));
    }

    private static async Task<PortalMyAccountDto?> GetMyAccountDtoAsync(System.Data.IDbConnection cn, Guid tenantId, CancellationToken ct)
    {
        const string sql = @"
SELECT TOP 1 MyAccountProfileId AS Id,
       TenantId,
       AgencyName,
       AdminName,
       AdminEmail,
       AdminRole,
       AdminPhone,
       TimeZone,
       Locale,
       PlanName,
       PlanStatus,
       RenewalDateUtc,
       PortalUsers,
       ActivePortalUsers,
       PendingInvites,
       OpenRequests,
       UrgentRequests,
       SharedDocuments,
       StorageUsedGb,
       StorageLimitGb,
       MonthlyLoginCount,
       MobileInstalls,
       ChatSessions30d,
       ApiCalls30d,
       LastPortalPublishUtc,
       LastAdminLoginUtc,
       MfaEnabled,
       SsoEnabled,
       BrandingPublished,
       MobileAppPublished,
       ChatEnabled,
       SupportEmail,
       SupportPhone,
       PortalDomain,
       HealthJson,
       ActivityJson
FROM Portal.MyAccountProfile
WHERE TenantId = @TenantId AND IsDeleted = 0;";
        var row = await cn.QuerySingleOrDefaultAsync<MyAccountProfileRow>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: ct));
        return row?.ToDto();
    }

    private static async Task SyncMyAccountAdminRecordAsync(System.Data.IDbConnection cn, PortalMyAccountDto account, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(account, JsonOptions);
        const string sql = @"
UPDATE Portal.AdminRecord
SET Name = @Name, Status = @Status, JsonData = @JsonData, ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND Kind = N'PortalMyAccount' AND Code = N'my-account' AND IsDeleted = 0;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO Portal.AdminRecord (PortalAdminRecordId, TenantId, Kind, Code, Name, Status, JsonData, CreatedDateUtc, IsDeleted)
    VALUES (NEWID(), @TenantId, N'PortalMyAccount', N'my-account', @Name, @Status, @JsonData, SYSUTCDATETIME(), 0);
END;";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { account.TenantId, Name = account.AgencyName, Status = account.PlanStatus, JsonData = json }, cancellationToken: ct));
    }

    private static async Task SyncMobileInstallAdminRecordsAsync(System.Data.IDbConnection cn, Guid tenantId, CancellationToken ct)
    {
        const string readSql = @"
SELECT MobileInstallId AS Id,
       TenantId,
       InstallNumber,
       AccountName,
       UserName,
       UserEmail,
       Platform,
       DeviceModel,
       AppVersion,
       OsVersion,
       Status,
       ComplianceStatus,
       RiskLevel,
       EnrollmentType,
       LastIpAddress,
       LastLocation,
       PushTokenStatus,
       RecommendedAction,
       InstalledDateUtc,
       LastSeenDateUtc,
       LastPushDateUtc,
       Sessions30d,
       DocumentsViewed30d,
       RequestsSubmitted30d,
       PushesSent30d,
       BiometricEnabled,
       MfaVerified,
       OfflineAccessEnabled,
       UpdateRequired,
       TrustedDevice,
       CreatedDateUtc,
       ModifiedDateUtc
FROM Portal.MobileInstall
WHERE TenantId = @TenantId AND IsDeleted = 0;";
        var installs = (await cn.QueryAsync<PortalMobileInstallDto>(new CommandDefinition(readSql, new { TenantId = tenantId }, cancellationToken: ct))).AsList();
        const string upsertSql = @"
UPDATE Portal.AdminRecord
SET Name = @Name, Status = @Status, JsonData = @JsonData, ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND Kind = N'PortalMobileInstall' AND Code = @Code AND IsDeleted = 0;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO Portal.AdminRecord (PortalAdminRecordId, TenantId, Kind, Code, Name, Status, JsonData, CreatedDateUtc, IsDeleted)
    VALUES (@Id, @TenantId, N'PortalMobileInstall', @Code, @Name, @Status, @JsonData, SYSUTCDATETIME(), 0);
END;";
        foreach (var install in installs)
        {
            var metric = new PortalMetricRecordDto
            {
                Id = install.Id,
                Name = install.AccountName,
                Category = install.Platform,
                Status = install.Status,
                Owner = install.UserName,
                Detail = $"{install.DeviceModel} · v{install.AppVersion} · {install.ComplianceStatus}",
                EventDateUtc = install.LastSeenDateUtc,
                Count = 1,
                Amount = install.Sessions30d
            };
            await cn.ExecuteAsync(new CommandDefinition(upsertSql, new { install.Id, install.TenantId, Code = install.InstallNumber, Name = install.UserName, install.Status, JsonData = JsonSerializer.Serialize(metric, JsonOptions) }, cancellationToken: ct));
        }
    }

    private static async Task SyncApiUsageAdminRecordsAsync(System.Data.IDbConnection cn, Guid tenantId, CancellationToken ct)
    {
        const string readSql = @"
SELECT ApiUsageId AS Id,
       TenantId,
       EndpointCode,
       EndpointName,
       Method,
       Route,
       IntegrationName,
       ApiKeyName,
       Status,
       HealthStatus,
       Priority,
       Owner,
       Detail,
       RecommendedAction,
       LastCallUtc,
       Calls30d,
       SuccessCount30d,
       WarningCount30d,
       ErrorCount30d,
       AvgLatencyMs,
       P95LatencyMs,
       RateLimitPerMinute,
       QuotaUsedPercent,
       WebhookDeliveries30d,
       RetryCount30d,
       RequiresReview,
       ReviewedDateUtc,
       CreatedDateUtc,
       ModifiedDateUtc
FROM Portal.ApiUsage
WHERE TenantId = @TenantId AND IsDeleted = 0;";
        var records = (await cn.QueryAsync<PortalApiUsageDto>(new CommandDefinition(readSql, new { TenantId = tenantId }, cancellationToken: ct))).AsList();
        const string upsertSql = @"
UPDATE Portal.AdminRecord
SET Name = @Name, Status = @Status, JsonData = @JsonData, ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId AND Kind = N'PortalApiUsage' AND Code = @Code AND IsDeleted = 0;

IF @@ROWCOUNT = 0
BEGIN
    INSERT INTO Portal.AdminRecord (PortalAdminRecordId, TenantId, Kind, Code, Name, Status, JsonData, CreatedDateUtc, IsDeleted)
    VALUES (@Id, @TenantId, N'PortalApiUsage', @Code, @Name, @Status, @JsonData, SYSUTCDATETIME(), 0);
END;";
        foreach (var item in records)
        {
            var metric = new PortalMetricRecordDto
            {
                Id = item.Id,
                Name = $"{item.Method} {item.Route}",
                Category = item.IntegrationName,
                Status = item.Status,
                Owner = item.ApiKeyName,
                Detail = $"{item.HealthStatus} · {item.Detail}",
                EventDateUtc = item.LastCallUtc,
                Count = item.Calls30d,
                Amount = item.QuotaUsedPercent
            };
            await cn.ExecuteAsync(new CommandDefinition(upsertSql, new { item.Id, item.TenantId, Code = item.EndpointCode, Name = item.EndpointName, item.Status, JsonData = JsonSerializer.Serialize(metric, JsonOptions) }, cancellationToken: ct));
        }
    }

    private sealed class MyAccountProfileRow
    {
        public Guid Id { get; set; }
        public Guid TenantId { get; set; }
        public string AgencyName { get; set; } = string.Empty;
        public string AdminName { get; set; } = string.Empty;
        public string AdminEmail { get; set; } = string.Empty;
        public string AdminRole { get; set; } = string.Empty;
        public string AdminPhone { get; set; } = string.Empty;
        public string TimeZone { get; set; } = string.Empty;
        public string Locale { get; set; } = string.Empty;
        public string PlanName { get; set; } = string.Empty;
        public string PlanStatus { get; set; } = string.Empty;
        public DateTime RenewalDateUtc { get; set; }
        public int PortalUsers { get; set; }
        public int ActivePortalUsers { get; set; }
        public int PendingInvites { get; set; }
        public int OpenRequests { get; set; }
        public int UrgentRequests { get; set; }
        public int SharedDocuments { get; set; }
        public int StorageUsedGb { get; set; }
        public int StorageLimitGb { get; set; }
        public int MonthlyLoginCount { get; set; }
        public int MobileInstalls { get; set; }
        public int ChatSessions30d { get; set; }
        public int ApiCalls30d { get; set; }
        public DateTime LastPortalPublishUtc { get; set; }
        public DateTime LastAdminLoginUtc { get; set; }
        public bool MfaEnabled { get; set; }
        public bool SsoEnabled { get; set; }
        public bool BrandingPublished { get; set; }
        public bool MobileAppPublished { get; set; }
        public bool ChatEnabled { get; set; }
        public string SupportEmail { get; set; } = string.Empty;
        public string SupportPhone { get; set; } = string.Empty;
        public string PortalDomain { get; set; } = string.Empty;
        public string HealthJson { get; set; } = "[]";
        public string ActivityJson { get; set; } = "[]";

        public PortalMyAccountDto ToDto() => new()
        {
            TenantId = TenantId,
            AgencyName = AgencyName,
            AdminName = AdminName,
            AdminEmail = AdminEmail,
            AdminRole = AdminRole,
            AdminPhone = AdminPhone,
            TimeZone = TimeZone,
            Locale = Locale,
            PlanName = PlanName,
            PlanStatus = PlanStatus,
            RenewalDateUtc = RenewalDateUtc,
            PortalUsers = PortalUsers,
            ActivePortalUsers = ActivePortalUsers,
            PendingInvites = PendingInvites,
            OpenRequests = OpenRequests,
            UrgentRequests = UrgentRequests,
            SharedDocuments = SharedDocuments,
            StorageUsedGb = StorageUsedGb,
            StorageLimitGb = StorageLimitGb,
            MonthlyLoginCount = MonthlyLoginCount,
            MobileInstalls = MobileInstalls,
            ChatSessions30d = ChatSessions30d,
            ApiCalls30d = ApiCalls30d,
            LastPortalPublishUtc = LastPortalPublishUtc,
            LastAdminLoginUtc = LastAdminLoginUtc,
            MfaEnabled = MfaEnabled,
            SsoEnabled = SsoEnabled,
            BrandingPublished = BrandingPublished,
            MobileAppPublished = MobileAppPublished,
            ChatEnabled = ChatEnabled,
            SupportEmail = SupportEmail,
            SupportPhone = SupportPhone,
            PortalDomain = PortalDomain,
            HealthChecks = DeserializeList<PortalAccountHealthDto>(HealthJson),
            RecentActivity = DeserializeList<PortalAccountActivityDto>(ActivityJson)
        };
    }

    private static List<T> DeserializeList<T>(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<T>>(json, JsonOptions) ?? []; }
        catch { return []; }
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
