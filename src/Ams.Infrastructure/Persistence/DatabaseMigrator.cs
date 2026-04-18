using Ams.Application.Abstractions.Persistence;
using Dapper;
using Microsoft.Extensions.Logging;

namespace Ams.Infrastructure.Persistence;

/// <summary>
/// Lightweight, script-based migration runner.
/// Each migration is identified by a unique name and is applied exactly once.
/// Applied migrations are tracked in dbo._Migrations.
/// </summary>
public sealed class DatabaseMigrator
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(ISqlConnectionFactory connectionFactory, ILogger<DatabaseMigrator> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await EnsureMigrationsTableAsync(cancellationToken);

        foreach (var migration in AllMigrations)
        {
            if (await HasBeenAppliedAsync(migration.Name, cancellationToken))
                continue;

            _logger.LogInformation("Applying migration: {Name}", migration.Name);
            await ApplyAsync(migration, cancellationToken);
            _logger.LogInformation("Migration applied: {Name}", migration.Name);
        }
    }

    // ── Migration registry ────────────────────────────────────────────
    private static readonly Migration[] AllMigrations =
    [
        new("0001_IAM_User_extended_columns", Migration0001_IamUserExtendedColumns),
        new("0002_Core_Branch_location_columns", Migration0002_CoreBranchLocationColumns),
        new("0003_dev_seed_data", Migration0003_DevSeedData),
        new("0004_dev_seed_userprofile", Migration0004_DevSeedUserProfile),
        new("0005_IAM_RoleBundle_schema_fix", Migration0005_IamRoleBundleSchemaFix),
        new("0006_IAM_UserRole_schema_fix", Migration0006_IamUserRoleSchemaFix),
        new("0007_IAM_UserPermission_create", Migration0007_IamUserPermissionCreate),
        new("0008_IAM_UserScope_create", Migration0008_IamUserScopeCreate),
        new("0009_IAM_TrustedDevice_schema_fix", Migration0009_IamTrustedDeviceSchemaFix),
        new("0010_IAM_AccessRequest_schema_fix", Migration0010_IamAccessRequestSchemaFix),
        new("0011_IAM_AccessReview_create", Migration0011_IamAccessReviewCreate),
        new("0012_IAM_AccessReview_ids_fix", Migration0012_IamAccessReviewIdsFix),
        new("0013_IAM_SodRule_schema_fix", Migration0013_IamSodRuleSchemaFix),
        new("0014_IAM_SodConflict_create", Migration0014_IamSodConflictCreate),
        new("0015_Compliance_PolicyDocument_create", Migration0015_CompliancePolicyDocumentCreate),
        new("0016_Compliance_PolicyAudience_create", Migration0016_CompliancePolicyAudienceCreate),
    ];

    // ── 0001 — Add extended profile/security columns to IAM.[User] ────
    private const string Migration0001_IamUserExtendedColumns = @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.[User]') AND name = N'DisplayName')
    ALTER TABLE IAM.[User] ADD DisplayName NVARCHAR(200) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.[User]') AND name = N'PhoneNumber')
    ALTER TABLE IAM.[User] ADD PhoneNumber NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.[User]') AND name = N'Department')
    ALTER TABLE IAM.[User] ADD Department NVARCHAR(200) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.[User]') AND name = N'TimeZoneCode')
    ALTER TABLE IAM.[User] ADD TimeZoneCode NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.[User]') AND name = N'LocaleCode')
    ALTER TABLE IAM.[User] ADD LocaleCode NVARCHAR(20) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.[User]') AND name = N'IsLockedOut')
    ALTER TABLE IAM.[User] ADD IsLockedOut BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.[User]') AND name = N'LockoutEndDateUtc')
    ALTER TABLE IAM.[User] ADD LockoutEndDateUtc DATETIME2(7) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.[User]') AND name = N'FailedLoginAttempts')
    ALTER TABLE IAM.[User] ADD FailedLoginAttempts INT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.[User]') AND name = N'PasswordChangedDateUtc')
    ALTER TABLE IAM.[User] ADD PasswordChangedDateUtc DATETIME2(7) NULL;
";

    // ── 0002 — Add location columns to Core.Branch ───────────────────
    private const string Migration0002_CoreBranchLocationColumns = @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Core.Branch') AND name = N'City')
    ALTER TABLE Core.Branch ADD City NVARCHAR(200) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Core.Branch') AND name = N'StateProvince')
    ALTER TABLE Core.Branch ADD StateProvince NVARCHAR(200) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Core.Branch') AND name = N'CountryCode')
    ALTER TABLE Core.Branch ADD CountryCode NVARCHAR(10) NULL;
";

    // ── 0003 — Dev seed data (Tenant, Company, Branch, User) ─────────
    private const string Migration0003_DevSeedData = @"
IF NOT EXISTS (SELECT 1 FROM Core.Tenant WHERE TenantId = '00000000-0000-0000-0000-000000000001')
    INSERT INTO Core.Tenant (TenantId, TenantCode, TenantName, DefaultCurrencyCode, DefaultCountryCode, DefaultTimeZoneId, PlanCode, Locale, CurrencyCode, TimeZoneId)
    VALUES ('00000000-0000-0000-0000-000000000001', 'DEMO', 'Demo Agency', 'USD', 'US', 2, 'Enterprise', 'en-US', 'USD', 'America/New_York');

IF NOT EXISTS (SELECT 1 FROM Core.Company WHERE CompanyId = '00000000-0000-0000-0000-000000000004')
    INSERT INTO Core.Company (CompanyId, TenantId, CompanyCode, CompanyName, CountryCode, CurrencyCode)
    VALUES ('00000000-0000-0000-0000-000000000004', '00000000-0000-0000-0000-000000000001', 'DEMO', 'Demo Agency Inc.', 'US', 'USD');

IF NOT EXISTS (SELECT 1 FROM Core.Branch WHERE BranchId = '00000000-0000-0000-0000-000000000003')
    INSERT INTO Core.Branch (BranchId, TenantId, CompanyId, BranchCode, BranchName, TimeZoneId, CountryCode, City, StateProvince)
    VALUES ('00000000-0000-0000-0000-000000000003', '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000004', 'HQ', 'Headquarters', 2, 'US', 'New York', 'NY');

IF NOT EXISTS (SELECT 1 FROM IAM.[User] WHERE UserId = '00000000-0000-0000-0000-000000000002')
    INSERT INTO IAM.[User] (UserId, TenantId, BranchId, UserName, Email, FirstName, LastName, FullName, DisplayName, PhoneNumber, UserTypeCode, StatusCode, TimeZoneCode, LocaleCode, Department, JobTitle, MfaEnabled, IsLockedOut, FailedLoginAttempts)
    VALUES ('00000000-0000-0000-0000-000000000002', '00000000-0000-0000-0000-000000000001', '00000000-0000-0000-0000-000000000003', 'admin', 'admin@demo.agency', 'Alex', 'Johnson', 'Alex Johnson', 'Alex J.', '+1 212 555 0100', 'Internal', 'Active', 'America/New_York', 'en-US', 'Technology', 'Platform Administrator', 1, 0, 0);
";

    // ── 0004 — Dev seed: IAM.UserProfile row for dev user ───────────
    private const string Migration0004_DevSeedUserProfile = @"
IF NOT EXISTS (SELECT 1 FROM IAM.UserProfile WHERE UserId = '00000000-0000-0000-0000-000000000002')
    INSERT INTO IAM.UserProfile (UserId, PhoneNumber, MobileNumber, CountryCode, AddressLine1, City, StateProvince, PostalCode)
    VALUES ('00000000-0000-0000-0000-000000000002', '+1 212 555 0100', '+1 917 555 0200', 'US', '1 Central Park West', 'New York', 'NY', '10023');
";

    // ── 0005 — Add missing columns to IAM.RoleBundle / BundleRole / BundleUser ──
    private const string Migration0005_IamRoleBundleSchemaFix = @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.RoleBundle') AND name = N'BundleId')
    ALTER TABLE IAM.RoleBundle ADD BundleId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.RoleBundle') AND name = N'Description')
    ALTER TABLE IAM.RoleBundle ADD Description NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.RoleBundle') AND name = N'SortOrder')
    ALTER TABLE IAM.RoleBundle ADD SortOrder INT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.RoleBundle') AND name = N'ModifiedDateUtc')
    ALTER TABLE IAM.RoleBundle ADD ModifiedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.RoleBundle') AND name = N'ModifiedByUserId')
    ALTER TABLE IAM.RoleBundle ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.RoleBundle') AND name = N'IsDeleted')
    ALTER TABLE IAM.RoleBundle ADD IsDeleted BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.BundleRole') AND name = N'BundleId')
    ALTER TABLE IAM.BundleRole ADD BundleId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.BundleRole') AND name = N'IsDeleted')
    ALTER TABLE IAM.BundleRole ADD IsDeleted BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.BundleUser') AND name = N'BundleId')
    ALTER TABLE IAM.BundleUser ADD BundleId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.BundleUser') AND name = N'IsDeleted')
    ALTER TABLE IAM.BundleUser ADD IsDeleted BIT NOT NULL DEFAULT 0;
";

    // ── 0006 – Add missing columns to IAM.UserRole ──────────────────
    private const string Migration0006_IamUserRoleSchemaFix = @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserRole') AND name = N'TenantId')
    ALTER TABLE IAM.UserRole ADD TenantId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserRole') AND name = N'IsDeleted')
    ALTER TABLE IAM.UserRole ADD IsDeleted BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserRole') AND name = N'CreatedDateUtc')
    ALTER TABLE IAM.UserRole ADD CreatedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserRole') AND name = N'ModifiedDateUtc')
    ALTER TABLE IAM.UserRole ADD ModifiedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserRole') AND name = N'ModifiedByUserId')
    ALTER TABLE IAM.UserRole ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserRole') AND name = N'AssignedByUserId')
    ALTER TABLE IAM.UserRole ADD AssignedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserRole') AND name = N'Source')
    ALTER TABLE IAM.UserRole ADD Source NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserRole') AND name = N'Reason')
    ALTER TABLE IAM.UserRole ADD Reason NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserRole') AND name = N'ApproverId')
    ALTER TABLE IAM.UserRole ADD ApproverId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserRole') AND name = N'ApprovedDateUtc')
    ALTER TABLE IAM.UserRole ADD ApprovedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserRole') AND name = N'ScopeTypeCode')
    ALTER TABLE IAM.UserRole ADD ScopeTypeCode NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserRole') AND name = N'ScopeValue')
    ALTER TABLE IAM.UserRole ADD ScopeValue NVARCHAR(200) NULL;
";

    // ── 0007 – Create IAM.UserPermission and IAM.UserPermissionScope ────
    private const string Migration0007_IamUserPermissionCreate = @"
IF OBJECT_ID(N'IAM.UserPermission', N'U') IS NULL
CREATE TABLE IAM.UserPermission (
    UserPermissionId      UNIQUEIDENTIFIER  NOT NULL CONSTRAINT PK_UserPermission PRIMARY KEY DEFAULT NEWID(),
    TenantId              UNIQUEIDENTIFIER  NOT NULL,
    UserId                UNIQUEIDENTIFIER  NOT NULL,
    PermissionId          UNIQUEIDENTIFIER  NOT NULL,
    IsGranted             BIT               NOT NULL DEFAULT 1,
    GrantedByUserId       UNIQUEIDENTIFIER  NULL,
    GrantedDateUtc        DATETIME2         NULL,
    EffectiveStartDateUtc DATETIME2         NULL,
    ExpiresDateUtc        DATETIME2         NULL,
    Reason                NVARCHAR(500)     NULL,
    ApprovedByUserId      UNIQUEIDENTIFIER  NULL,
    ModifiedByUserId      UNIQUEIDENTIFIER  NULL,
    ModifiedDateUtc       DATETIME2         NULL,
    CreatedDateUtc        DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted             BIT               NOT NULL DEFAULT 0
);

IF OBJECT_ID(N'IAM.UserPermissionScope', N'U') IS NULL
CREATE TABLE IAM.UserPermissionScope (
    UserPermissionScopeId UNIQUEIDENTIFIER  NOT NULL CONSTRAINT PK_UserPermissionScope PRIMARY KEY DEFAULT NEWID(),
    UserPermissionId      UNIQUEIDENTIFIER  NOT NULL,
    ScopeTypeCode         NVARCHAR(50)      NULL,
    ScopeValue            NVARCHAR(200)     NULL,
    CreatedByUserId       UNIQUEIDENTIFIER  NULL,
    CreatedDateUtc        DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted             BIT               NOT NULL DEFAULT 0
);
";

    // ── 0008 – Create IAM.UserScope ──────────────────────────────────
    private const string Migration0008_IamUserScopeCreate = @"
IF OBJECT_ID(N'IAM.UserScope', N'U') IS NULL
CREATE TABLE IAM.UserScope (
    UserScopeId       UNIQUEIDENTIFIER  NOT NULL CONSTRAINT PK_UserScope PRIMARY KEY DEFAULT NEWID(),
    TenantId          UNIQUEIDENTIFIER  NOT NULL,
    UserId            UNIQUEIDENTIFIER  NOT NULL,
    ScopeTypeCode     NVARCHAR(100)     NOT NULL,
    ScopeValue        NVARCHAR(500)     NOT NULL,
    IsActive          BIT               NOT NULL DEFAULT 1,
    GrantedByUserId   UNIQUEIDENTIFIER  NULL,
    GrantedDateUtc    DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
    ExpiresDateUtc    DATETIME2         NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER  NULL,
    ModifiedDateUtc   DATETIME2         NULL,
    CreatedDateUtc    DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted         BIT               NOT NULL DEFAULT 0
);
";

    // ── 0009 – Add missing columns to IAM.TrustedDevice ───────────
    private const string Migration0009_IamTrustedDeviceSchemaFix = @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'TenantId')
    ALTER TABLE IAM.TrustedDevice ADD TenantId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'IsDeleted')
    ALTER TABLE IAM.TrustedDevice ADD IsDeleted BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'IsActive')
    ALTER TABLE IAM.TrustedDevice ADD IsActive BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'IpAddress')
    ALTER TABLE IAM.TrustedDevice ADD IpAddress NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'UserAgent')
    ALTER TABLE IAM.TrustedDevice ADD UserAgent NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'DeviceTypeCode')
    ALTER TABLE IAM.TrustedDevice ADD DeviceTypeCode NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'BrowserName')
    ALTER TABLE IAM.TrustedDevice ADD BrowserName NVARCHAR(200) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'OperatingSystem')
    ALTER TABLE IAM.TrustedDevice ADD OperatingSystem NVARCHAR(200) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'TrustedDateUtc')
    ALTER TABLE IAM.TrustedDevice ADD TrustedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'ExpiresDateUtc')
    ALTER TABLE IAM.TrustedDevice ADD ExpiresDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'RiskScore')
    ALTER TABLE IAM.TrustedDevice ADD RiskScore INT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'RiskFlags')
    ALTER TABLE IAM.TrustedDevice ADD RiskFlags INT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'RiskNotes')
    ALTER TABLE IAM.TrustedDevice ADD RiskNotes NVARCHAR(1000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'RevokedByUserId')
    ALTER TABLE IAM.TrustedDevice ADD RevokedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'RevokedDateUtc')
    ALTER TABLE IAM.TrustedDevice ADD RevokedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'RevokedReason')
    ALTER TABLE IAM.TrustedDevice ADD RevokedReason NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'CreatedDateUtc')
    ALTER TABLE IAM.TrustedDevice ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE();
";

    // ── 0010 – Add missing columns to IAM.AccessRequest ───────────────
    private const string Migration0010_IamAccessRequestSchemaFix = @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'RequestTypeCode')
    ALTER TABLE IAM.AccessRequest ADD RequestTypeCode NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'RoleId')
    ALTER TABLE IAM.AccessRequest ADD RoleId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'PermissionId')
    ALTER TABLE IAM.AccessRequest ADD PermissionId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'ScopeCode')
    ALTER TABLE IAM.AccessRequest ADD ScopeCode NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'StartDateUtc')
    ALTER TABLE IAM.AccessRequest ADD StartDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'EndDateUtc')
    ALTER TABLE IAM.AccessRequest ADD EndDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'BusinessJustification')
    ALTER TABLE IAM.AccessRequest ADD BusinessJustification NVARCHAR(2000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'TicketReference')
    ALTER TABLE IAM.AccessRequest ADD TicketReference NVARCHAR(200) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'UrgencyCode')
    ALTER TABLE IAM.AccessRequest ADD UrgencyCode NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'AttachmentFileName')
    ALTER TABLE IAM.AccessRequest ADD AttachmentFileName NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'StatusCode')
    ALTER TABLE IAM.AccessRequest ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Pending';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'ApproverComment')
    ALTER TABLE IAM.AccessRequest ADD ApproverComment NVARCHAR(1000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'IsDeleted')
    ALTER TABLE IAM.AccessRequest ADD IsDeleted BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'CreatedDateUtc')
    ALTER TABLE IAM.AccessRequest ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessRequest') AND name = N'ModifiedDateUtc')
    ALTER TABLE IAM.AccessRequest ADD ModifiedDateUtc DATETIME2 NULL;
";

    // 0011 - Create IAM.AccessReviewCampaign, IAM.AccessReviewItem, IAM.UserAccessReview
    private const string Migration0011_IamAccessReviewCreate = @"
IF OBJECT_ID(N'IAM.AccessReviewCampaign', N'U') IS NULL
CREATE TABLE IAM.AccessReviewCampaign (
    CampaignId       UNIQUEIDENTIFIER  NOT NULL CONSTRAINT PK_AccessReviewCampaign PRIMARY KEY DEFAULT NEWID(),
    TenantId         UNIQUEIDENTIFIER  NOT NULL,
    CampaignName     NVARCHAR(300)     NOT NULL DEFAULT '',
    Description      NVARCHAR(1000)    NULL,
    ScopeTypeCode    NVARCHAR(100)     NULL,
    ScopeReferenceId UNIQUEIDENTIFIER  NULL,
    ReviewerUserId   UNIQUEIDENTIFIER  NULL,
    StartDateUtc     DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
    EndDateUtc       DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
    StatusCode       NVARCHAR(50)      NOT NULL DEFAULT 'Draft',
    CreatedByUserId  UNIQUEIDENTIFIER  NULL,
    CreatedDateUtc   DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
    ModifiedDateUtc  DATETIME2         NULL,
    IsDeleted        BIT               NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'CampaignName')
    ALTER TABLE IAM.AccessReviewCampaign ADD CampaignName NVARCHAR(300) NOT NULL DEFAULT '';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'Description')
    ALTER TABLE IAM.AccessReviewCampaign ADD Description NVARCHAR(1000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'ScopeTypeCode')
    ALTER TABLE IAM.AccessReviewCampaign ADD ScopeTypeCode NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'ScopeReferenceId')
    ALTER TABLE IAM.AccessReviewCampaign ADD ScopeReferenceId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'ReviewerUserId')
    ALTER TABLE IAM.AccessReviewCampaign ADD ReviewerUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'StartDateUtc')
    ALTER TABLE IAM.AccessReviewCampaign ADD StartDateUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'EndDateUtc')
    ALTER TABLE IAM.AccessReviewCampaign ADD EndDateUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'StatusCode')
    ALTER TABLE IAM.AccessReviewCampaign ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Draft';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'CreatedByUserId')
    ALTER TABLE IAM.AccessReviewCampaign ADD CreatedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'CreatedDateUtc')
    ALTER TABLE IAM.AccessReviewCampaign ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'ModifiedDateUtc')
    ALTER TABLE IAM.AccessReviewCampaign ADD ModifiedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'IsDeleted')
    ALTER TABLE IAM.AccessReviewCampaign ADD IsDeleted BIT NOT NULL DEFAULT 0;

IF OBJECT_ID(N'IAM.AccessReviewItem', N'U') IS NULL
CREATE TABLE IAM.AccessReviewItem (
    ReviewItemId      UNIQUEIDENTIFIER  NOT NULL CONSTRAINT PK_AccessReviewItem PRIMARY KEY DEFAULT NEWID(),
    CampaignId        UNIQUEIDENTIFIER  NOT NULL,
    UserId            UNIQUEIDENTIFIER  NOT NULL,
    AccessTypeCode    NVARCHAR(100)     NULL,
    AccessReferenceId UNIQUEIDENTIFIER  NULL,
    AccessName        NVARCHAR(300)     NULL,
    RiskLevel         NVARCHAR(50)      NULL,
    DecisionCode      NVARCHAR(50)      NULL,
    ReviewerNotes     NVARCHAR(1000)    NULL,
    ReviewedByUserId  UNIQUEIDENTIFIER  NULL,
    ReviewedDateUtc   DATETIME2         NULL,
    CreatedDateUtc    DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted         BIT               NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewItem') AND name = N'AccessTypeCode')
    ALTER TABLE IAM.AccessReviewItem ADD AccessTypeCode NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewItem') AND name = N'AccessReferenceId')
    ALTER TABLE IAM.AccessReviewItem ADD AccessReferenceId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewItem') AND name = N'AccessName')
    ALTER TABLE IAM.AccessReviewItem ADD AccessName NVARCHAR(300) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewItem') AND name = N'RiskLevel')
    ALTER TABLE IAM.AccessReviewItem ADD RiskLevel NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewItem') AND name = N'DecisionCode')
    ALTER TABLE IAM.AccessReviewItem ADD DecisionCode NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewItem') AND name = N'ReviewerNotes')
    ALTER TABLE IAM.AccessReviewItem ADD ReviewerNotes NVARCHAR(1000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewItem') AND name = N'ReviewedByUserId')
    ALTER TABLE IAM.AccessReviewItem ADD ReviewedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewItem') AND name = N'ReviewedDateUtc')
    ALTER TABLE IAM.AccessReviewItem ADD ReviewedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewItem') AND name = N'CreatedDateUtc')
    ALTER TABLE IAM.AccessReviewItem ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewItem') AND name = N'IsDeleted')
    ALTER TABLE IAM.AccessReviewItem ADD IsDeleted BIT NOT NULL DEFAULT 0;

IF OBJECT_ID(N'IAM.UserAccessReview', N'U') IS NULL
CREATE TABLE IAM.UserAccessReview (
    ReviewId        UNIQUEIDENTIFIER  NOT NULL CONSTRAINT PK_UserAccessReview PRIMARY KEY DEFAULT NEWID(),
    TenantId        UNIQUEIDENTIFIER  NOT NULL,
    ReviewCycleCode NVARCHAR(100)     NULL,
    ReviewerUserId  UNIQUEIDENTIFIER  NOT NULL,
    SubjectUserId   UNIQUEIDENTIFIER  NOT NULL,
    RoleId          UNIQUEIDENTIFIER  NOT NULL,
    DecisionCode    NVARCHAR(50)      NULL,
    DecisionNotes   NVARCHAR(1000)    NULL,
    ReviewedDateUtc DATETIME2         NULL,
    DueByDateUtc    DATETIME2         NULL,
    StatusCode      NVARCHAR(50)      NOT NULL DEFAULT 'Pending',
    CreatedDateUtc  DATETIME2         NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT               NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserAccessReview') AND name = N'ReviewCycleCode')
    ALTER TABLE IAM.UserAccessReview ADD ReviewCycleCode NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserAccessReview') AND name = N'DecisionCode')
    ALTER TABLE IAM.UserAccessReview ADD DecisionCode NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserAccessReview') AND name = N'DecisionNotes')
    ALTER TABLE IAM.UserAccessReview ADD DecisionNotes NVARCHAR(1000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserAccessReview') AND name = N'ReviewedDateUtc')
    ALTER TABLE IAM.UserAccessReview ADD ReviewedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserAccessReview') AND name = N'DueByDateUtc')
    ALTER TABLE IAM.UserAccessReview ADD DueByDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserAccessReview') AND name = N'StatusCode')
    ALTER TABLE IAM.UserAccessReview ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Pending';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserAccessReview') AND name = N'CreatedDateUtc')
    ALTER TABLE IAM.UserAccessReview ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserAccessReview') AND name = N'IsDeleted')
    ALTER TABLE IAM.UserAccessReview ADD IsDeleted BIT NOT NULL DEFAULT 0;
";

    // 0012 - Add missing PK/FK columns omitted from 0011 ALTER TABLE guards
    private const string Migration0012_IamAccessReviewIdsFix = @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'CampaignId')
    ALTER TABLE IAM.AccessReviewCampaign ADD CampaignId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewCampaign') AND name = N'TenantId')
    ALTER TABLE IAM.AccessReviewCampaign ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewItem') AND name = N'CampaignId')
    ALTER TABLE IAM.AccessReviewItem ADD CampaignId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.AccessReviewItem') AND name = N'UserId')
    ALTER TABLE IAM.AccessReviewItem ADD UserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserAccessReview') AND name = N'TenantId')
    ALTER TABLE IAM.UserAccessReview ADD TenantId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserAccessReview') AND name = N'ReviewerUserId')
    ALTER TABLE IAM.UserAccessReview ADD ReviewerUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserAccessReview') AND name = N'SubjectUserId')
    ALTER TABLE IAM.UserAccessReview ADD SubjectUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserAccessReview') AND name = N'RoleId')
    ALTER TABLE IAM.UserAccessReview ADD RoleId UNIQUEIDENTIFIER NULL;
";

    // 0013 - Add missing columns to IAM.SegregationOfDutyRule
    private const string Migration0013_IamSodRuleSchemaFix = @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'RoleAId')
    ALTER TABLE IAM.SegregationOfDutyRule ADD RoleAId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'RoleBId')
    ALTER TABLE IAM.SegregationOfDutyRule ADD RoleBId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'PermissionAId')
    ALTER TABLE IAM.SegregationOfDutyRule ADD PermissionAId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'PermissionBId')
    ALTER TABLE IAM.SegregationOfDutyRule ADD PermissionBId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'Reason')
    ALTER TABLE IAM.SegregationOfDutyRule ADD Reason NVARCHAR(1000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'ExceptionPolicyCode')
    ALTER TABLE IAM.SegregationOfDutyRule ADD ExceptionPolicyCode NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'Description')
    ALTER TABLE IAM.SegregationOfDutyRule ADD Description NVARCHAR(1000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'SeverityCode')
    ALTER TABLE IAM.SegregationOfDutyRule ADD SeverityCode NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'IsActive')
    ALTER TABLE IAM.SegregationOfDutyRule ADD IsActive BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'IsSystemDefined')
    ALTER TABLE IAM.SegregationOfDutyRule ADD IsSystemDefined BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'IsDeleted')
    ALTER TABLE IAM.SegregationOfDutyRule ADD IsDeleted BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'CreatedByUserId')
    ALTER TABLE IAM.SegregationOfDutyRule ADD CreatedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'CreatedDateUtc')
    ALTER TABLE IAM.SegregationOfDutyRule ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SegregationOfDutyRule') AND name = N'ModifiedDateUtc')
    ALTER TABLE IAM.SegregationOfDutyRule ADD ModifiedDateUtc DATETIME2 NULL;
";

    // ── 0014 — Create IAM.SodConflict ─────────────────────────────────
    private const string Migration0014_IamSodConflictCreate = @"
IF OBJECT_ID(N'IAM.SodConflict', N'U') IS NULL
    CREATE TABLE IAM.SodConflict (
        SodConflictId    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId         UNIQUEIDENTIFIER NULL,
        SodRuleId        UNIQUEIDENTIFIER NULL,
        UserId           UNIQUEIDENTIFIER NULL,
        DetectedDateUtc  DATETIME2 NULL,
        StatusCode       NVARCHAR(50)     NOT NULL DEFAULT 'Open',
        ReviewerUserId   UNIQUEIDENTIFIER NULL,
        RemediationNote  NVARCHAR(2000)   NULL,
        ResolvedByUserId UNIQUEIDENTIFIER NULL,
        ResolutionNote   NVARCHAR(2000)   NULL,
        ResolvedDateUtc  DATETIME2 NULL,
        CreatedDateUtc   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc  DATETIME2 NULL,
        IsDeleted        BIT              NOT NULL DEFAULT 0
    );

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'SodConflictId')
    ALTER TABLE IAM.SodConflict ADD SodConflictId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'TenantId')
    ALTER TABLE IAM.SodConflict ADD TenantId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'SodRuleId')
    ALTER TABLE IAM.SodConflict ADD SodRuleId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'UserId')
    ALTER TABLE IAM.SodConflict ADD UserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'DetectedDateUtc')
    ALTER TABLE IAM.SodConflict ADD DetectedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'StatusCode')
    ALTER TABLE IAM.SodConflict ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Open';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'ReviewerUserId')
    ALTER TABLE IAM.SodConflict ADD ReviewerUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'RemediationNote')
    ALTER TABLE IAM.SodConflict ADD RemediationNote NVARCHAR(2000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'ResolvedByUserId')
    ALTER TABLE IAM.SodConflict ADD ResolvedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'ResolutionNote')
    ALTER TABLE IAM.SodConflict ADD ResolutionNote NVARCHAR(2000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'ResolvedDateUtc')
    ALTER TABLE IAM.SodConflict ADD ResolvedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'CreatedDateUtc')
    ALTER TABLE IAM.SodConflict ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'ModifiedDateUtc')
    ALTER TABLE IAM.SodConflict ADD ModifiedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.SodConflict') AND name = N'IsDeleted')
    ALTER TABLE IAM.SodConflict ADD IsDeleted BIT NOT NULL DEFAULT 0;
";

    // ── 0015 — Create Compliance schema + PolicyDocument + PolicyAcknowledgement ──
    private const string Migration0015_CompliancePolicyDocumentCreate = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Compliance')
    EXEC('CREATE SCHEMA [Compliance]');

IF OBJECT_ID(N'Compliance.PolicyDocument', N'U') IS NULL
    CREATE TABLE Compliance.PolicyDocument (
        PolicyDocumentId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId               UNIQUEIDENTIFIER NULL,
        PolicyCode             NVARCHAR(50)     NOT NULL,
        PolicyTitle            NVARCHAR(500)    NOT NULL,
        PolicyTypeCode         NVARCHAR(100)    NOT NULL,
        Version                NVARCHAR(20)     NOT NULL DEFAULT '1.0',
        EffectiveDateUtc       DATETIME2 NULL,
        IsActive               BIT              NOT NULL DEFAULT 1,
        StatusCode             NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
        Description            NVARCHAR(MAX)    NULL,
        Content                NVARCHAR(MAX)    NULL,
        OwnedByUserId          UNIQUEIDENTIFIER NULL,
        ParentPolicyDocumentId UNIQUEIDENTIFIER NULL,
        PublishedByUserId      UNIQUEIDENTIFIER NULL,
        PublishedDateUtc       DATETIME2 NULL,
        RetiredByUserId        UNIQUEIDENTIFIER NULL,
        RetiredDateUtc         DATETIME2 NULL,
        CreatedByUserId        UNIQUEIDENTIFIER NULL,
        CreatedDateUtc         DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc        DATETIME2 NULL,
        IsDeleted              BIT              NOT NULL DEFAULT 0
    );

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'PolicyDocumentId')
    ALTER TABLE Compliance.PolicyDocument ADD PolicyDocumentId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'TenantId')
    ALTER TABLE Compliance.PolicyDocument ADD TenantId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'PolicyCode')
    ALTER TABLE Compliance.PolicyDocument ADD PolicyCode NVARCHAR(50) NOT NULL DEFAULT '';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'PolicyTitle')
    ALTER TABLE Compliance.PolicyDocument ADD PolicyTitle NVARCHAR(500) NOT NULL DEFAULT '';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'PolicyTypeCode')
    ALTER TABLE Compliance.PolicyDocument ADD PolicyTypeCode NVARCHAR(100) NOT NULL DEFAULT '';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'Version')
    ALTER TABLE Compliance.PolicyDocument ADD Version NVARCHAR(20) NOT NULL DEFAULT '1.0';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'EffectiveDateUtc')
    ALTER TABLE Compliance.PolicyDocument ADD EffectiveDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'IsActive')
    ALTER TABLE Compliance.PolicyDocument ADD IsActive BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'StatusCode')
    ALTER TABLE Compliance.PolicyDocument ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Draft';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'Description')
    ALTER TABLE Compliance.PolicyDocument ADD Description NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'Content')
    ALTER TABLE Compliance.PolicyDocument ADD Content NVARCHAR(MAX) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'OwnedByUserId')
    ALTER TABLE Compliance.PolicyDocument ADD OwnedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'ParentPolicyDocumentId')
    ALTER TABLE Compliance.PolicyDocument ADD ParentPolicyDocumentId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'PublishedByUserId')
    ALTER TABLE Compliance.PolicyDocument ADD PublishedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'PublishedDateUtc')
    ALTER TABLE Compliance.PolicyDocument ADD PublishedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'RetiredByUserId')
    ALTER TABLE Compliance.PolicyDocument ADD RetiredByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'RetiredDateUtc')
    ALTER TABLE Compliance.PolicyDocument ADD RetiredDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'CreatedByUserId')
    ALTER TABLE Compliance.PolicyDocument ADD CreatedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'CreatedDateUtc')
    ALTER TABLE Compliance.PolicyDocument ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'ModifiedDateUtc')
    ALTER TABLE Compliance.PolicyDocument ADD ModifiedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'IsDeleted')
    ALTER TABLE Compliance.PolicyDocument ADD IsDeleted BIT NOT NULL DEFAULT 0;

IF OBJECT_ID(N'Compliance.PolicyAcknowledgement', N'U') IS NULL
    CREATE TABLE Compliance.PolicyAcknowledgement (
        AcknowledgementId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        PolicyDocumentId    UNIQUEIDENTIFIER NOT NULL,
        UserId              UNIQUEIDENTIFIER NOT NULL,
        TenantId            UNIQUEIDENTIFIER NULL,
        AcknowledgedDateUtc DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        Channel             NVARCHAR(50)     NULL,
        IpAddress           NVARCHAR(100)    NULL,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE()
    );

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAcknowledgement') AND name = N'AcknowledgementId')
    ALTER TABLE Compliance.PolicyAcknowledgement ADD AcknowledgementId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAcknowledgement') AND name = N'PolicyDocumentId')
    ALTER TABLE Compliance.PolicyAcknowledgement ADD PolicyDocumentId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAcknowledgement') AND name = N'UserId')
    ALTER TABLE Compliance.PolicyAcknowledgement ADD UserId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAcknowledgement') AND name = N'TenantId')
    ALTER TABLE Compliance.PolicyAcknowledgement ADD TenantId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAcknowledgement') AND name = N'AcknowledgedDateUtc')
    ALTER TABLE Compliance.PolicyAcknowledgement ADD AcknowledgedDateUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAcknowledgement') AND name = N'Channel')
    ALTER TABLE Compliance.PolicyAcknowledgement ADD Channel NVARCHAR(50) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAcknowledgement') AND name = N'IpAddress')
    ALTER TABLE Compliance.PolicyAcknowledgement ADD IpAddress NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAcknowledgement') AND name = N'CreatedDateUtc')
    ALTER TABLE Compliance.PolicyAcknowledgement ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE();
";

    // ── 0016 — Create Compliance.PolicyAudience ──────────────────────
    private const string Migration0016_CompliancePolicyAudienceCreate = @"
IF OBJECT_ID(N'Compliance.PolicyAudience', N'U') IS NULL
    CREATE TABLE Compliance.PolicyAudience (
        AudienceId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        PolicyDocumentId UNIQUEIDENTIFIER NOT NULL,
        TargetTypeCode   NVARCHAR(50)     NOT NULL DEFAULT 'AllUsers',
        TargetId         UNIQUEIDENTIFIER NULL,
        TargetName       NVARCHAR(200)    NOT NULL DEFAULT '',
        IsRequired       BIT              NOT NULL DEFAULT 1,
        AddedByUserId    UNIQUEIDENTIFIER NULL,
        AddedDateUtc     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        IsDeleted        BIT              NOT NULL DEFAULT 0
    );

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAudience') AND name = N'AudienceId')
    ALTER TABLE Compliance.PolicyAudience ADD AudienceId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAudience') AND name = N'PolicyDocumentId')
    ALTER TABLE Compliance.PolicyAudience ADD PolicyDocumentId UNIQUEIDENTIFIER NOT NULL DEFAULT '00000000-0000-0000-0000-000000000000';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAudience') AND name = N'TargetTypeCode')
    ALTER TABLE Compliance.PolicyAudience ADD TargetTypeCode NVARCHAR(50) NOT NULL DEFAULT 'AllUsers';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAudience') AND name = N'TargetId')
    ALTER TABLE Compliance.PolicyAudience ADD TargetId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAudience') AND name = N'TargetName')
    ALTER TABLE Compliance.PolicyAudience ADD TargetName NVARCHAR(200) NOT NULL DEFAULT '';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAudience') AND name = N'IsRequired')
    ALTER TABLE Compliance.PolicyAudience ADD IsRequired BIT NOT NULL DEFAULT 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAudience') AND name = N'AddedByUserId')
    ALTER TABLE Compliance.PolicyAudience ADD AddedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAudience') AND name = N'AddedDateUtc')
    ALTER TABLE Compliance.PolicyAudience ADD AddedDateUtc DATETIME2 NOT NULL DEFAULT GETUTCDATE();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Compliance.PolicyAudience') AND name = N'IsDeleted')
    ALTER TABLE Compliance.PolicyAudience ADD IsDeleted BIT NOT NULL DEFAULT 0;
";

    // ── Internals ─────────────────────────────────────────────────────
    private async Task EnsureMigrationsTableAsync(CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = '_Migrations' AND schema_id = SCHEMA_ID('dbo'))
    CREATE TABLE dbo._Migrations (
        MigrationId   INT           IDENTITY(1,1) PRIMARY KEY,
        Name          NVARCHAR(200) NOT NULL UNIQUE,
        AppliedDateUtc DATETIME2(7) NOT NULL DEFAULT GETUTCDATE()
    );";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(sql);
    }

    private async Task<bool> HasBeenAppliedAsync(string name, CancellationToken cancellationToken)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo._Migrations WHERE Name = @Name;",
            new { Name = name }) > 0;
    }

    private async Task ApplyAsync(Migration migration, CancellationToken cancellationToken)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var tx = cn.BeginTransaction();
        try
        {
            await cn.ExecuteAsync(migration.Sql, transaction: tx);
            await cn.ExecuteAsync(
                "INSERT INTO dbo._Migrations (Name) VALUES (@Name);",
                new { migration.Name }, transaction: tx);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }

    private sealed record Migration(string Name, string Sql);
}
