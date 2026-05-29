using System.ComponentModel.DataAnnotations;

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

public sealed class PortalActivityEventDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string EventNumber { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Severity { get; set; } = "Info";
    public string Status { get; set; } = "Open";
    public string Detail { get; set; } = string.Empty;
    public string WorkflowImpact { get; set; } = string.Empty;
    public string RecommendedAction { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string Device { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public int RiskScore { get; set; }
    public int DurationSeconds { get; set; }
    public bool RequiresReview { get; set; }
    public DateTime? ReviewedDateUtc { get; set; }
    public string ReviewedBy { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class UpdatePortalActivityEventRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(80)]
    public string Status { get; set; } = string.Empty;

    [StringLength(160)]
    public string? AssignedTo { get; set; }

    [StringLength(500)]
    public string? RecommendedAction { get; set; }

    public bool RequiresReview { get; set; }
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

public sealed class UpdatePortalMyAccountRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(200)]
    public string AgencyName { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string AdminName { get; set; } = string.Empty;

    [Required]
    [StringLength(320)]
    public string AdminEmail { get; set; } = string.Empty;

    [StringLength(80)]
    public string AdminRole { get; set; } = string.Empty;

    [StringLength(50)]
    public string AdminPhone { get; set; } = string.Empty;

    [StringLength(120)]
    public string TimeZone { get; set; } = string.Empty;

    [StringLength(40)]
    public string Locale { get; set; } = string.Empty;

    [Required]
    [StringLength(120)]
    public string PlanName { get; set; } = string.Empty;

    [Required]
    [StringLength(80)]
    public string PlanStatus { get; set; } = string.Empty;

    public DateTime RenewalDateUtc { get; set; }

    [Range(0, 100000)]
    public int PortalUsers { get; set; }

    [Range(0, 100000)]
    public int ActivePortalUsers { get; set; }

    [Range(0, 100000)]
    public int PendingInvites { get; set; }

    [Range(0, 100000)]
    public int OpenRequests { get; set; }

    [Range(0, 100000)]
    public int UrgentRequests { get; set; }

    [Range(0, 100000)]
    public int SharedDocuments { get; set; }

    [Range(0, 100000)]
    public int StorageUsedGb { get; set; }

    [Range(1, 100000)]
    public int StorageLimitGb { get; set; } = 250;

    [Range(0, 100000000)]
    public int MonthlyLoginCount { get; set; }

    [Range(0, 1000000)]
    public int MobileInstalls { get; set; }

    [Range(0, 1000000)]
    public int ChatSessions30d { get; set; }

    [Range(0, 100000000)]
    public int ApiCalls30d { get; set; }

    public bool MfaEnabled { get; set; }
    public bool SsoEnabled { get; set; }
    public bool BrandingPublished { get; set; }
    public bool MobileAppPublished { get; set; }
    public bool ChatEnabled { get; set; }

    [Required]
    [StringLength(320)]
    public string SupportEmail { get; set; } = string.Empty;

    [StringLength(50)]
    public string SupportPhone { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string PortalDomain { get; set; } = string.Empty;
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

public sealed class PortalChatSessionDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string SessionNumber { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Channel { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Sentiment { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
    public string NextBestAction { get; set; } = string.Empty;
    public DateTime StartedDateUtc { get; set; }
    public DateTime LastMessageDateUtc { get; set; }
    public DateTime? ResolvedDateUtc { get; set; }
    public int MessageCount { get; set; }
    public int WaitSeconds { get; set; }
    public DateTime? SlaDueDateUtc { get; set; }
    public bool AiHandled { get; set; }
    public bool HandoffRequired { get; set; }
    public DateTime? ReviewedDateUtc { get; set; }
}

public sealed class PortalWhiteLabelConfigurationDto
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string PortalDomain { get; set; } = string.Empty;
    public string DomainStatus { get; set; } = string.Empty;
    public string PublishStatus { get; set; } = string.Empty;
    public DateTime? LastPublishedDateUtc { get; set; }
    public string PrimaryColor { get; set; } = "#1d4ed8";
    public string AccentColor { get; set; } = "#059669";
    public string NavBackgroundColor { get; set; } = "#1e293b";
    public string NavTextColor { get; set; } = "#f8fafc";
    public string LogoUrl { get; set; } = string.Empty;
    public string FaviconUrl { get; set; } = string.Empty;
    public string WelcomeMessage { get; set; } = string.Empty;
    public string SupportEmail { get; set; } = string.Empty;
    public string SupportPhone { get; set; } = string.Empty;
    public bool ShowAgencyLogo { get; set; }
    public bool HidePoweredBy { get; set; }
    public bool ShowNewsWidget { get; set; }
    public bool ShowSupportChat { get; set; }
    public bool EnableAnnouncements { get; set; }
    public bool EnableCrossSellWidget { get; set; }
    public string MobileAppName { get; set; } = string.Empty;
    public string MobileBundleId { get; set; } = string.Empty;
    public string IosStoreUrl { get; set; } = string.Empty;
    public string AndroidStoreUrl { get; set; } = string.Empty;
    public string MobileVersion { get; set; } = string.Empty;
    public string MinimumMobileVersion { get; set; } = string.Empty;
    public bool MobilePublished { get; set; }
    public bool BiometricLogin { get; set; }
    public bool PushNotifications { get; set; }
    public bool OfflinePolicyView { get; set; }
    public bool ForceMobileUpdate { get; set; }
    public bool RequireMfaOnMobile { get; set; }
    public string AssistantName { get; set; } = string.Empty;
    public string AssistantWelcomeMessage { get; set; } = string.Empty;
    public string ChatWidgetColor { get; set; } = "#1d4ed8";
    public string ChatPosition { get; set; } = "bottom-right";
    public string ChatEscalationEmail { get; set; } = string.Empty;
    public string OfficeHours { get; set; } = string.Empty;
    public bool ChatEnabled { get; set; }
    public bool AiResponsesEnabled { get; set; }
    public bool LiveHandoffEnabled { get; set; }
    public bool ShowChatOnMobile { get; set; }
    public bool AllowFileAttachments { get; set; }
    public bool TranscriptEmailEnabled { get; set; }
    public string IdentityProvider { get; set; } = "none";
    public string SsoClientId { get; set; } = string.Empty;
    public string SsoMetadataUrl { get; set; } = string.Empty;
    public string RedirectUris { get; set; } = string.Empty;
    public bool SsoEnabled { get; set; }
    public bool MfaRequired { get; set; }
    public bool AllowSocialLogin { get; set; }
    public bool AutoProvisionUsers { get; set; }
    public int PasswordMinLength { get; set; }
    public int SessionTimeoutMinutes { get; set; }
    public int MaxFailedLoginAttempts { get; set; }
    public int LockoutMinutes { get; set; }
    public bool RequireUppercase { get; set; }
    public bool RequireSpecialCharacter { get; set; }
    public bool IpWhitelistEnabled { get; set; }
    public int ActivePortalUsers { get; set; }
    public int PendingInvites { get; set; }
    public int MobileInstalls { get; set; }
    public int ChatSessions30d { get; set; }
    public int OpenRequests { get; set; }
    public int UrgentRequests { get; set; }
    public int SharedDocuments { get; set; }
    public int ApiCalls30d { get; set; }
    public decimal CsATScore { get; set; }
    public int AiResolutionRate { get; set; }
    public int LiveHandoffs30d { get; set; }
    public int AverageResponseSeconds { get; set; }
    public string ConfigurationJson { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class UpdatePortalWhiteLabelConfigurationRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(200)]
    public string DisplayName { get; set; } = string.Empty;

    [Required]
    [StringLength(255)]
    public string PortalDomain { get; set; } = string.Empty;

    [Required]
    [StringLength(40)]
    public string DomainStatus { get; set; } = "Pending DNS";

    [Required]
    [StringLength(40)]
    public string PublishStatus { get; set; } = "Draft";

    [Required]
    [StringLength(20)]
    public string PrimaryColor { get; set; } = "#1d4ed8";

    [Required]
    [StringLength(20)]
    public string AccentColor { get; set; } = "#059669";

    [Required]
    [StringLength(20)]
    public string NavBackgroundColor { get; set; } = "#1e293b";

    [Required]
    [StringLength(20)]
    public string NavTextColor { get; set; } = "#f8fafc";

    [StringLength(500)]
    public string LogoUrl { get; set; } = string.Empty;

    [StringLength(500)]
    public string FaviconUrl { get; set; } = string.Empty;

    [StringLength(1000)]
    public string WelcomeMessage { get; set; } = string.Empty;

    [Required]
    [StringLength(320)]
    public string SupportEmail { get; set; } = string.Empty;

    [StringLength(50)]
    public string SupportPhone { get; set; } = string.Empty;

    public bool ShowAgencyLogo { get; set; }
    public bool HidePoweredBy { get; set; }
    public bool ShowNewsWidget { get; set; }
    public bool ShowSupportChat { get; set; }
    public bool EnableAnnouncements { get; set; }
    public bool EnableCrossSellWidget { get; set; }

    [Required]
    [StringLength(200)]
    public string MobileAppName { get; set; } = string.Empty;

    [StringLength(160)]
    public string MobileBundleId { get; set; } = string.Empty;

    [StringLength(500)]
    public string IosStoreUrl { get; set; } = string.Empty;

    [StringLength(500)]
    public string AndroidStoreUrl { get; set; } = string.Empty;

    [StringLength(40)]
    public string MobileVersion { get; set; } = string.Empty;

    [StringLength(40)]
    public string MinimumMobileVersion { get; set; } = string.Empty;

    public bool MobilePublished { get; set; }
    public bool BiometricLogin { get; set; }
    public bool PushNotifications { get; set; }
    public bool OfflinePolicyView { get; set; }
    public bool ForceMobileUpdate { get; set; }
    public bool RequireMfaOnMobile { get; set; }

    [Required]
    [StringLength(120)]
    public string AssistantName { get; set; } = string.Empty;

    [StringLength(1000)]
    public string AssistantWelcomeMessage { get; set; } = string.Empty;

    [StringLength(20)]
    public string ChatWidgetColor { get; set; } = "#1d4ed8";

    [StringLength(40)]
    public string ChatPosition { get; set; } = "bottom-right";

    [StringLength(320)]
    public string ChatEscalationEmail { get; set; } = string.Empty;

    [StringLength(120)]
    public string OfficeHours { get; set; } = string.Empty;

    public bool ChatEnabled { get; set; }
    public bool AiResponsesEnabled { get; set; }
    public bool LiveHandoffEnabled { get; set; }
    public bool ShowChatOnMobile { get; set; }
    public bool AllowFileAttachments { get; set; }
    public bool TranscriptEmailEnabled { get; set; }

    [StringLength(80)]
    public string IdentityProvider { get; set; } = "none";

    [StringLength(255)]
    public string SsoClientId { get; set; } = string.Empty;

    [StringLength(500)]
    public string SsoMetadataUrl { get; set; } = string.Empty;

    [StringLength(1000)]
    public string RedirectUris { get; set; } = string.Empty;

    public bool SsoEnabled { get; set; }
    public bool MfaRequired { get; set; }
    public bool AllowSocialLogin { get; set; }
    public bool AutoProvisionUsers { get; set; }

    [Range(8, 32)]
    public int PasswordMinLength { get; set; } = 10;

    [Range(10, 480)]
    public int SessionTimeoutMinutes { get; set; } = 30;

    [Range(3, 10)]
    public int MaxFailedLoginAttempts { get; set; } = 5;

    [Range(5, 1440)]
    public int LockoutMinutes { get; set; } = 15;

    public bool RequireUppercase { get; set; }
    public bool RequireSpecialCharacter { get; set; }
    public bool IpWhitelistEnabled { get; set; }
    public string ConfigurationJson { get; set; } = string.Empty;
}

public sealed class UpdatePortalChatSessionStatusRequest
{
    [Required]
    public Guid TenantId { get; set; }

    [Required]
    [StringLength(80)]
    public string Status { get; set; } = string.Empty;

    [StringLength(160)]
    public string? AssignedTo { get; set; }

    [StringLength(500)]
    public string? NextBestAction { get; set; }
}

public sealed record UpsertPortalAdminRecordRequest(Guid TenantId, string Kind, string Code, string Name, string Status, string JsonData);
