namespace Ams.Application.Common.Dtos;

public sealed class PortalAdminRecordDto
{
    public Guid PortalAdminRecordId { get; set; }
    public Guid TenantId { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? JsonData { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class PortalAdminUserDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Role { get; set; } = "Policyholder";
    public string Status { get; set; } = "Pending";
    public DateTime LastLogin { get; set; }
    public bool MfaEnabled { get; set; }
    public int Logins30d { get; set; }
}

public sealed class PortalAdminRequestDto
{
    public Guid Id { get; set; }
    public DateTime SubmittedAt { get; set; }
    public string ClientName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string RequestType { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string AssignedTo { get; set; } = "—";
    public string Status { get; set; } = "Open";
}

public sealed class PortalAdminDocumentDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string Category { get; set; } = "Policy";
    public string FileType { get; set; } = "PDF";
    public int FileSizeKb { get; set; }
    public string Visibility { get; set; } = "Shared";
    public DateTime SharedAt { get; set; }
    public int ViewCount { get; set; }
    public int DownloadCount { get; set; }
}

public sealed class PortalAdminActivityDto
{
    public Guid Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string IpAddress { get; set; } = string.Empty;
}

public sealed class PortalCapabilityDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-circle";
    public string IconCss { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public bool RequiresApproval { get; set; }
    public bool MfaRequired { get; set; }
    public bool AuditLog { get; set; }
}

public sealed class PortalBrandingSettingsDto
{
    public string DisplayName { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string SupportEmail { get; set; } = string.Empty;
    public string SupportPhone { get; set; } = string.Empty;
    public string WelcomeMessage { get; set; } = string.Empty;
    public string PrimaryColor { get; set; } = "#1d4ed8";
    public string AccentColor { get; set; } = "#059669";
    public string NavBg { get; set; } = "#1e293b";
    public string NavText { get; set; } = "#f8fafc";
    public string EmailFromName { get; set; } = string.Empty;
    public string EmailReplyTo { get; set; } = string.Empty;
    public string EmailFooter { get; set; } = string.Empty;
    public bool ShowAgencyLogo { get; set; } = true;
    public bool ShowPoweredBy { get; set; }
    public bool ShowSupportChat { get; set; } = true;
    public bool ShowNewsWidget { get; set; } = true;
}

public sealed class PortalMobileSettingsDto
{
    public string AppName { get; set; } = string.Empty;
    public string IosUrl { get; set; } = string.Empty;
    public string AndroidUrl { get; set; } = string.Empty;
    public string BundleId { get; set; } = string.Empty;
    public string AppVersion { get; set; } = string.Empty;
    public bool BiometricLogin { get; set; }
    public bool ForceAppLock { get; set; }
    public int LockTimeoutMinutes { get; set; } = 15;
    public bool RequireMfaOnMobile { get; set; }
    public List<PortalMobileToggleDto> Notifications { get; set; } = [];
    public List<PortalMobileFeatureDto> Features { get; set; } = [];
}

public sealed class PortalMobileToggleDto
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public sealed class PortalMobileFeatureDto
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-circle";
    public string IconCss { get; set; } = string.Empty;
    public bool Enabled { get; set; }
}

public sealed class PortalMyAccountDto
{
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
    public List<PortalAccountHealthDto> HealthChecks { get; set; } = [];
    public List<PortalAccountActivityDto> RecentActivity { get; set; } = [];
}

public sealed class PortalAccountHealthDto
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-check-circle";
}

public sealed class PortalAccountActivityDto
{
    public DateTime OccurredAtUtc { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Icon { get; set; } = "bi-info-circle";
}

public sealed class PortalMetricRecordDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Owner { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public DateTime EventDateUtc { get; set; }
    public int Count { get; set; }
    public decimal Amount { get; set; }
}

public sealed record UpsertPortalAdminRecordRequest(Guid TenantId, string Kind, string Code, string Name, string Status, string JsonData);
