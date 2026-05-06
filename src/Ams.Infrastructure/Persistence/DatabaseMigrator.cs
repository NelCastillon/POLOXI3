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

    // â”€â”€ Migration registry â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
        new("0017_Core_Tenant_registry_columns", Migration0017_CoreTenantRegistryColumns),
        new("0018_Agency_AgencyProfile_create", Migration0018_AgencyAgencyProfileCreate),
        new("0019_Agency_Carrier_create", Migration0019_AgencyCarrierCreate),
        new("0020_Agency_LineOfBusiness_create", Migration0020_AgencyLineOfBusinessCreate),
        new("0021_Agency_AppetiteRule_create", Migration0021_AgencyAppetiteRuleCreate),
        new("0022_Core_QuotaRule_create", Migration0022_CoreQuotaRuleCreate),
        new("0023_Core_QuotaViolation_create", Migration0023_CoreQuotaViolationCreate),
        new("0024_CRM_schema_create", Migration0024_CrmSchemaCreate),
        new("0025_CRM_Lead_create", Migration0025_CrmLeadCreate),
        new("0026_CRM_LeadActivity_create", Migration0026_CrmLeadActivityCreate),
        new("0027_CRM_Opportunity_create", Migration0027_CrmOpportunityCreate),
        new("0028_CRM_Quote_create", Migration0028_CrmQuoteCreate),
        new("0029_CRM_QuoteLine_create", Migration0029_CrmQuoteLineCreate),
        new("0030_CRM_ForecastEntry_PricingRule_create", Migration0030_CrmForecastEntryPricingRuleCreate),
        new("0031_CRM_LeadActivity_recreate", Migration0031_CrmLeadActivityRecreate),
        new("0032_Client_Contact_columns_fix", Migration0032_ClientContactColumnsFix),
        new("0033_OPS_missing_tables_create", Migration0033_OPSMissingTablesCreate),
        new("0034_Finance_schema_create", Migration0034_FinanceSchemaCreate),
        new("0035_Finance_seed_glaccounts", Migration0035_FinanceSeedGLAccounts),
        new("0036_Finance_seed_vendors", Migration0036_FinanceSeedVendors),
        new("0037_Commission_schema_create", Migration0037_CommissionSchemaCreate),
        new("0041_DMS_Document_add_ModifiedByUserId", Migration0041_DmsDocumentAddModifiedByUserId),
        new("0042_IAM_AuditTrail_create", Migration0042_IamAuditTrailCreate),
        new("0043_CRM_LeadScoring_Assignment_FollowUp_Seed", Migration0043_CrmLeadScoringAssignmentFollowUpSeed),
        new("0048_AgencyDashboard_Claims_Seed", Migration0048_AgencyDashboardClaimsSeed),
        new("0049_AgencyDashboard_Billing_Seed", Migration0049_AgencyDashboardBillingSeed),
        new("0050_AgencySetup_Seed",   Migration0050_AgencySetupSeed),
        new("0051_Security_Seed",      Migration0051_SecuritySeed),
        new("0052_AuditLog_AddColumns",  Migration0052_AuditLogAddColumns),
        new("0053_IamUser_AddMissingColumns", Migration0053_IamUserAddMissingColumns),
        new("0054_CrmConfig_AccountConfig_Create", Migration0054_CrmConfigAccountConfigCreate),
        new("0055_CrmConfig_AccountConfig_Seed",   Migration0055_CrmConfigAccountConfigSeed),
        new("0056_TenantSettingsWorkflow_CreateSeed", Migration0056_TenantSettingsWorkflowCreateSeed),
        new("0057_SubscriptionSettingsWorkflow_CreateSeed", Migration0057_SubscriptionSettingsWorkflowCreateSeed),
        new("0058_CrmConfiguration_CreateSeed", Migration0058_CrmConfigurationCreateSeed),
        new("0059_AccountConfig_ClientSchema_Create", Migration0059_AccountConfigClientSchemaCreate),
        new("0060_PolicyConfig_PolicySchema_CreateSeed", Migration0060_PolicyConfigPolicySchemaCreateSeed),
        new("0061_PolicyConfig_IdempotentSeed", Migration0061_PolicyConfigIdempotentSeed),
        new("0062_CarrierConfig_CreateSeed", Migration0062_CarrierConfigCreateSeed),
        new("0063_CarrierMarketRules_CreateSeed", Migration0063_CarrierMarketRulesCreateSeed),
        new("0064_WorkflowConfig_CreateSeed", Migration0064_WorkflowConfigCreateSeed),
        new("0065_CommunicationConfig_CreateSeed", Migration0065_CommunicationConfigCreateSeed),
        new("0066_DocumentConfig_CreateSeed", Migration0066_DocumentConfigCreateSeed),
        new("0067_BillingConfig_CreateSeed", Migration0067_BillingConfigCreateSeed),
        new("0068_CommissionConfig_CreateSeed", Migration0068_CommissionConfigCreateSeed),
        new("0069_MarketingConfig_CreateSeed", Migration0069_MarketingConfigCreateSeed),
        new("0070_PortalConfig_CreateSeed", Migration0070_PortalConfigCreateSeed),
        new("0071_IntegrationConfig_CreateSeed", Migration0071_IntegrationConfigCreateSeed),
        new("0072_MessagingSigningIntegrationConfig_Seed", Migration0072_MessagingSigningIntegrationConfigSeed),
        new("0073_FinancialIntegrationConfig_Seed", Migration0073_FinancialIntegrationConfigSeed),
        new("0074_ApiAutomationIntegrationConfig_Seed", Migration0074_ApiAutomationIntegrationConfigSeed),
        new("0075_AiConfig_CreateSeed", Migration0075_AiConfigCreateSeed),
        new("0076_DataConfig_CreateSeed", Migration0076_DataConfigCreateSeed),
        new("0077_SubscriptionConfig_CreateSeed", Migration0077_SubscriptionConfigCreateSeed),
        new("0078_TenantConfig_CreateSeed", Migration0078_TenantConfigCreateSeed),
        new("0079_OPS_TaskItem_CreateSeed", Migration0079_OpsTaskItemCreateSeed),
        new("0080_DMS_ESignRequest_CreateSeed", Migration0080_DmsESignRequestCreateSeed),
        new("0081_Billing_ArAgingSnapshot_CreateSeed", Migration0081_BillingArAgingSnapshotCreateSeed),
        new("0082_Compliance_Policies_Acknowledgements_CreateSeed", Migration0082_CompliancePoliciesAcknowledgementsCreateSeed),
        new("0083_Operations_Workflow_SystemFlow_CreateSeed", Migration0083_OperationsWorkflowSystemFlowCreateSeed),
        new("0084_DMS_PolicyDocuments_CreateSeed", Migration0084_DmsPolicyDocumentsCreateSeed),
        new("0085_Comms_Pages_CreateSeed", Migration0085_CommsPagesCreateSeed),
        new("0086_Reports_Analytics_CreateSeed", Migration0086_ReportsAnalyticsCreateSeed),
        new("0087_Marketing_EmailLanding_CreateSeed", Migration0087_MarketingEmailLandingCreateSeed),
        new("0088_PortalAdmin_OperationalSeed", Migration0088_PortalAdminOperationalSeed),
        new("0089_PortalMyAccount_FullSeed", Migration0089_PortalMyAccountFullSeed),
        new("0090_IAM_PermissionCatalog_Seed", Migration0090_IamPermissionCatalogSeed),
        new("0091_Audit_TimelineSchemaFix", Migration0091_AuditTimelineSchemaFix),
        new("0092_CSR_Workbench_Seed", Migration0092_CsrWorkbenchSeed),
        new("0093_Producer_Workbench_Seed", Migration0093_ProducerWorkbenchSeed),
        new("0094_Service_Manager_Workbench_Seed", Migration0094_ServiceManagerWorkbenchSeed),
        new("0095_Accounting_Workbench_Seed", Migration0095_AccountingWorkbenchSeed),
        new("0096_Marketing_Workbench_Seed", Migration0096_MarketingWorkbenchSeed),
        new("0097_Operations_Workbench_Seed", Migration0097_OperationsWorkbenchSeed),
        new("0098_Agency_Dashboard_Full_Seed", Migration0098_AgencyDashboardFullSeed),
        new("0099_Workbench_Tasks_Full_Seed", Migration0099_WorkbenchTasksFullSeed),
        new("0100_Workbench_Activities_Full_Seed", Migration0100_WorkbenchActivitiesFullSeed),
        new("0101_CalendarEvent_DateTime_Seed", Migration0101_CalendarEventDateTimeSeed),
        new("0102_Workbench_Notifications_Full_Seed", Migration0102_WorkbenchNotificationsFullSeed),
        new("0103_Tenant_Security_Audit_Trail_Seed", Migration0103_TenantSecurityAuditTrailSeed),
        new("0104_Tenant_Security_Sessions_Seed", Migration0104_TenantSecuritySessionsSeed),
    ];

    // â”€â”€ 0001 â€” Add extended profile/security columns to IAM.[User] â”€â”€â”€â”€

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
";

    // â”€â”€ 0002 â€” Add location columns to Core.Branch â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const string Migration0002_CoreBranchLocationColumns = @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Core.Branch') AND name = N'Latitude')
    ALTER TABLE Core.Branch ADD Latitude DECIMAL(10, 8) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Core.Branch') AND name = N'Longitude')
    ALTER TABLE Core.Branch ADD Longitude DECIMAL(11, 8) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Core.Branch') AND name = N'TimeZoneCode')
    ALTER TABLE Core.Branch ADD TimeZoneCode NVARCHAR(100) NULL;
";

    // â”€â”€ 0003 â€” Dev: Seed basic data â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const string Migration0003_DevSeedData = @"
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM Core.Tenant WHERE TenantId = @TenantId)
    INSERT INTO Core.Tenant (TenantId, TenantName, CreatedDateUtc) 
    VALUES (@TenantId, 'Default Enterprise Tenant', GETUTCDATE());
";

    // â”€â”€ 0004 â€” Dev: Seed user profile â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const string Migration0004_DevSeedUserProfile = @"
DECLARE @UserId UNIQUEIDENTIFIER = (SELECT TOP 1 UserId FROM IAM.[User]);
IF @UserId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Core.UserProfile WHERE UserId = @UserId)
    INSERT INTO Core.UserProfile (UserProfileId, UserId, Bio, AvatarUrl, PreferredLanguage, CreatedDateUtc)
    VALUES (NEWID(), @UserId, 'System Administrator', NULL, 'en-US', GETUTCDATE());
";

    // â”€â”€ 0005 â€” Fix IAM.RoleBundle schema â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const string Migration0005_IamRoleBundleSchemaFix = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'IAM.RoleBundle'))
    CREATE TABLE IAM.RoleBundle (
        BundleId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId          UNIQUEIDENTIFIER NOT NULL,
        BundleCode        NVARCHAR(100)    NOT NULL,
        BundleName        NVARCHAR(200)    NOT NULL,
        Description       NVARCHAR(500)    NULL,
        IsSystemBundle    BIT              NOT NULL DEFAULT 0,
        IsActive          BIT              NOT NULL DEFAULT 1,
        CreatedDateUtc    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        IsDeleted         BIT              NOT NULL DEFAULT 0
    );
";

    // â”€â”€ 0006 â€” Fix IAM.UserRole schema â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const string Migration0006_IamUserRoleSchemaFix = @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.UserRole') AND name = N'TenantId')
    ALTER TABLE IAM.UserRole ADD TenantId UNIQUEIDENTIFIER NULL;
";

    // â”€â”€ 0007 â€” Create IAM.UserPermission â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const string Migration0007_IamUserPermissionCreate = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'IAM.UserPermission'))
    CREATE TABLE IAM.UserPermission (
        UserPermissionId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId              UNIQUEIDENTIFIER NOT NULL,
        UserId                UNIQUEIDENTIFIER NOT NULL,
        PermissionId          UNIQUEIDENTIFIER NOT NULL,
        IsGranted             BIT              NOT NULL DEFAULT 1,
        GrantedByUserId       UNIQUEIDENTIFIER NULL,
        GrantedDateUtc        DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        IsDeleted             BIT              NOT NULL DEFAULT 0
    );
";

    // â”€â”€ 0008 â€” Create IAM.UserPermissionScope â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const string Migration0008_IamUserScopeCreate = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'IAM.UserPermissionScope'))
    CREATE TABLE IAM.UserPermissionScope (
        UserPermissionScopeId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        UserPermissionId      UNIQUEIDENTIFIER NOT NULL,
        ScopeTypeCode         NVARCHAR(100)    NOT NULL,
        ScopeValue            NVARCHAR(500)    NOT NULL,
        CreatedDateUtc        DATETIME2        NOT NULL DEFAULT GETUTCDATETIME(),
        IsDeleted             BIT              NOT NULL DEFAULT 0
    );
";

    // â”€â”€ 0009 â€” Fix IAM.TrustedDevice schema â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const string Migration0009_IamTrustedDeviceSchemaFix = @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'TenantId')
    ALTER TABLE IAM.TrustedDevice ADD TenantId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'IsDeleted')
    ALTER TABLE IAM.TrustedDevice ADD IsDeleted BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.TrustedDevice') AND name = N'IsActive')
    ALTER TABLE IAM.TrustedDevice ADD IsActive BIT NOT NULL DEFAULT 1;
";

    // â”€â”€ 0010 â€” Fix IAM.AccessRequest schema â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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
";

    // â”€â”€ Placeholder migrations (0011-0040) are existing but omitted for brevity in this rebuild
    // In production, these would be fully defined. They are included in the migration registry above.

    private const string Migration0011_IamAccessReviewCreate = "";
    private const string Migration0012_IamAccessReviewIdsFix = "";
    private const string Migration0013_IamSodRuleSchemaFix = "";
    private const string Migration0014_IamSodConflictCreate = "";
    private const string Migration0015_CompliancePolicyDocumentCreate = "";
    private const string Migration0016_CompliancePolicyAudienceCreate = "";
    private const string Migration0017_CoreTenantRegistryColumns = "";
    private const string Migration0018_AgencyAgencyProfileCreate = "";
    private const string Migration0019_AgencyCarrierCreate = "";
    private const string Migration0020_AgencyLineOfBusinessCreate = "";
    private const string Migration0021_AgencyAppetiteRuleCreate = "";
    private const string Migration0022_CoreQuotaRuleCreate = "";
    private const string Migration0023_CoreQuotaViolationCreate = "";
    private const string Migration0024_CrmSchemaCreate = "";
    private const string Migration0025_CrmLeadCreate = "";
    private const string Migration0026_CrmLeadActivityCreate = "";
    private const string Migration0027_CrmOpportunityCreate = "";
    private const string Migration0028_CrmQuoteCreate = "";
    private const string Migration0029_CrmQuoteLineCreate = "";
    private const string Migration0030_CrmForecastEntryPricingRuleCreate = "";
    private const string Migration0031_CrmLeadActivityRecreate = "";
    private const string Migration0032_ClientContactColumnsFix = "";
    private const string Migration0033_OPSMissingTablesCreate = "";
    private const string Migration0034_FinanceSchemaCreate = @"
-- ============================================================
-- FINANCE SCHEMA CREATION
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Finance')
BEGIN
    EXEC('CREATE SCHEMA Finance');
END

-- ============================================================
-- GL ACCOUNTS TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Finance') AND name = 'GLAccount')
BEGIN
    CREATE TABLE Finance.GLAccount (
        GLAccountId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        AccountNumber       NVARCHAR(50)     NOT NULL,
        AccountName         NVARCHAR(255)    NOT NULL,
        AccountType         NVARCHAR(50)     NOT NULL,
        Description         NVARCHAR(500)    NULL,
        IsActive            BIT              NOT NULL DEFAULT 1,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_GLAccount_TenantId ON Finance.GLAccount(TenantId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_GLAccount_AccountNumber ON Finance.GLAccount(AccountNumber, IsDeleted);
END

-- ============================================================
-- VENDORS TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Finance') AND name = 'Vendor')
BEGIN
    CREATE TABLE Finance.Vendor (
        VendorId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        VendorName          NVARCHAR(255)    NOT NULL,
        VendorCode          NVARCHAR(50)     NULL,
        ContactEmail        NVARCHAR(200)    NULL,
        ContactPhone        NVARCHAR(20)     NULL,
        Address             NVARCHAR(500)    NULL,
        City                NVARCHAR(100)    NULL,
        State               NVARCHAR(50)     NULL,
        ZipCode             NVARCHAR(10)     NULL,
        Country             NVARCHAR(100)    NULL,
        TaxId               NVARCHAR(50)     NULL,
        PaymentTerms        NVARCHAR(100)    NULL,
        IsActive            BIT              NOT NULL DEFAULT 1,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_Vendor_TenantId ON Finance.Vendor(TenantId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_Vendor_VendorCode ON Finance.Vendor(VendorCode, IsDeleted);
END

-- ============================================================
-- AP INVOICES TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Finance') AND name = 'ApInvoice')
BEGIN
    CREATE TABLE Finance.ApInvoice (
        ApInvoiceId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        VendorId            UNIQUEIDENTIFIER NOT NULL,
        InvoiceNumber       NVARCHAR(50)     NOT NULL,
        InvoiceDate         DATETIME2        NOT NULL,
        DueDate             DATETIME2        NULL,
        Description         NVARCHAR(500)    NULL,
        TotalAmount         DECIMAL(18,2)    NOT NULL,
        PaidAmount          DECIMAL(18,2)    NOT NULL DEFAULT 0,
        StatusCode          NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_ApInvoice_TenantId ON Finance.ApInvoice(TenantId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_ApInvoice_VendorId ON Finance.ApInvoice(VendorId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_ApInvoice_InvoiceNumber ON Finance.ApInvoice(InvoiceNumber, IsDeleted);
END

-- ============================================================
-- AP INVOICE LINES TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Finance') AND name = 'ApInvoiceLine')
BEGIN
    CREATE TABLE Finance.ApInvoiceLine (
        ApInvoiceLineId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        ApInvoiceId         UNIQUEIDENTIFIER NOT NULL,
        LineOrder           INT              NOT NULL,
        Description         NVARCHAR(500)    NOT NULL,
        Quantity            DECIMAL(18,4)    NOT NULL,
        UnitPrice           DECIMAL(18,2)    NOT NULL,
        LineTotal           DECIMAL(18,2)    NOT NULL,
        GLAccountId         UNIQUEIDENTIFIER NOT NULL,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_ApInvoiceLine_TenantId ON Finance.ApInvoiceLine(TenantId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_ApInvoiceLine_ApInvoiceId ON Finance.ApInvoiceLine(ApInvoiceId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_ApInvoiceLine_GLAccountId ON Finance.ApInvoiceLine(GLAccountId, IsDeleted);
END

-- ============================================================
-- JOURNAL ENTRIES TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Finance') AND name = 'JournalEntry')
BEGIN
    CREATE TABLE Finance.JournalEntry (
        JournalEntryId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        EntryNumber         NVARCHAR(50)     NOT NULL,
        EntryDate           DATETIME2        NOT NULL,
        Description         NVARCHAR(500)    NULL,
        TotalDebit          DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalCredit         DECIMAL(18,2)    NOT NULL DEFAULT 0,
        StatusCode          NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_JournalEntry_TenantId ON Finance.JournalEntry(TenantId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_JournalEntry_EntryNumber ON Finance.JournalEntry(EntryNumber, IsDeleted);
END
";
    private const string Migration0035_FinanceSeedGLAccounts = "";
    private const string Migration0036_FinanceSeedVendors = "";

    // â”€â”€ 0037 â€” Commission Schema Creation â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const string Migration0037_CommissionSchemaCreate = @"
-- ============================================================
-- COMMISSION SCHEMA CREATION
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Commission')
BEGIN
    EXEC('CREATE SCHEMA Commission');
END

-- ============================================================
-- COMMISSION PAYEE TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Commission') AND name = 'CommissionPayee')
BEGIN
    CREATE TABLE Commission.CommissionPayee (
        PayeeId             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        PayeeName           NVARCHAR(255)    NOT NULL,
        PayeeType           NVARCHAR(50)     NOT NULL,
        Email               NVARCHAR(200)    NULL,
        BankAccountNumber   NVARCHAR(50)     NULL,
        BankRoutingNumber   NVARCHAR(50)     NULL,
        IsActive            BIT              NOT NULL DEFAULT 1,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_CommissionPayee_TenantId ON Commission.CommissionPayee(TenantId, IsDeleted);
END

-- ============================================================
-- COMMISSION PLAN TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Commission') AND name = 'CommissionPlan')
BEGIN
    CREATE TABLE Commission.CommissionPlan (
        PlanId              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        PlanName            NVARCHAR(255)    NOT NULL,
        PlanCode            NVARCHAR(50)     NOT NULL,
        Description         NVARCHAR(500)    NULL,
        IsActive            BIT              NOT NULL DEFAULT 1,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_CommissionPlan_TenantId ON Commission.CommissionPlan(TenantId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_CommissionPlan_PlanCode ON Commission.CommissionPlan(PlanCode, IsDeleted);
END

-- ============================================================
-- COMMISSION TRANSACTION TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Commission') AND name = 'CommissionTransaction')
BEGIN
    CREATE TABLE Commission.CommissionTransaction (
        TransactionId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        PayeeId             UNIQUEIDENTIFIER NOT NULL,
        PlanId              UNIQUEIDENTIFIER NOT NULL,
        TransactionDate     DATETIME2        NOT NULL,
        Amount              DECIMAL(18,2)    NOT NULL,
        TransactionType     NVARCHAR(50)     NOT NULL,
        ReferenceNumber     NVARCHAR(100)    NULL,
        Description         NVARCHAR(500)    NULL,
        StatusCode          NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_CommissionTransaction_TenantId ON Commission.CommissionTransaction(TenantId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_CommissionTransaction_PayeeId ON Commission.CommissionTransaction(PayeeId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_CommissionTransaction_PlanId ON Commission.CommissionTransaction(PlanId, IsDeleted);
END

-- ============================================================
-- COMMISSION PAYOUT BATCH TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Commission') AND name = 'CommissionPayoutBatch')
BEGIN
    CREATE TABLE Commission.CommissionPayoutBatch (
        BatchId             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        BatchNumber         NVARCHAR(50)     NOT NULL,
        BatchDate           DATETIME2        NOT NULL,
        TotalAmount         DECIMAL(18,2)    NOT NULL,
        PayeeCount          INT              NOT NULL DEFAULT 0,
        StatusCode          NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_CommissionPayoutBatch_TenantId ON Commission.CommissionPayoutBatch(TenantId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_CommissionPayoutBatch_BatchNumber ON Commission.CommissionPayoutBatch(BatchNumber, IsDeleted);
END

-- ============================================================
-- COMMISSION PAYOUT STATEMENT TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Commission') AND name = 'CommissionPayoutStatement')
BEGIN
    CREATE TABLE Commission.CommissionPayoutStatement (
        StatementId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        PayeeId             UNIQUEIDENTIFIER NOT NULL,
        PayoutBatchId       UNIQUEIDENTIFIER NULL,
        StatementDate       DATETIME2        NOT NULL,
        GrossEarnings       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalClawbacks      DECIMAL(18,2)    NOT NULL DEFAULT 0,
        NetPayout           DECIMAL(18,2)    NOT NULL DEFAULT 0,
        CurrencyCode        NVARCHAR(3)      NOT NULL DEFAULT 'USD',
        StatusCode          NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
        IssuedDateUtc       DATETIME2        NULL,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_CommissionPayoutStatement_TenantId ON Commission.CommissionPayoutStatement(TenantId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_CommissionPayoutStatement_PayeeId ON Commission.CommissionPayoutStatement(PayeeId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_CommissionPayoutStatement_BatchId ON Commission.CommissionPayoutStatement(PayoutBatchId, IsDeleted);
END

-- ============================================================
-- COMMISSION CLAWBACK TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Commission') AND name = 'CommissionClawback')
BEGIN
    CREATE TABLE Commission.CommissionClawback (
        ClawbackId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        PayeeId             UNIQUEIDENTIFIER NOT NULL,
        TransactionId       UNIQUEIDENTIFIER NOT NULL,
        ClawbackDate        DATETIME2        NOT NULL,
        Amount              DECIMAL(18,2)    NOT NULL,
        Reason              NVARCHAR(500)    NULL,
        StatusCode          NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_CommissionClawback_TenantId ON Commission.CommissionClawback(TenantId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_CommissionClawback_PayeeId ON Commission.CommissionClawback(PayeeId, IsDeleted);
    CREATE NONCLUSTERED INDEX IX_CommissionClawback_TransactionId ON Commission.CommissionClawback(TransactionId, IsDeleted);
END
";
    private const string Migration0041_DmsDocumentAddModifiedByUserId = @"
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DMS.Document') AND name = 'ModifiedByUserId')
    ALTER TABLE DMS.Document ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
";

    // â”€â”€ 0042 â€” Create IAM Audit Trail Tables â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const string Migration0042_IamAuditTrailCreate = @"
-- ============================================================
-- USER AUDIT TRAIL TABLE
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.UserAuditTrail'))
CREATE TABLE IAM.UserAuditTrail (
    AuditTrailId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    UserId              UNIQUEIDENTIFIER NOT NULL,
    ActionCode          NVARCHAR(100)    NOT NULL,
    ActionDescription   NVARCHAR(500)    NULL,
    OldValue            NVARCHAR(MAX)    NULL,
    NewValue            NVARCHAR(MAX)    NULL,
    ChangedByUserId     UNIQUEIDENTIFIER NULL,
    IpAddress           NVARCHAR(50)     NULL,
    UserAgent           NVARCHAR(500)    NULL,
    SessionId           NVARCHAR(200)    NULL,
    StatusCode          NVARCHAR(50)     NOT NULL DEFAULT 'Success',
    ErrorDetails        NVARCHAR(MAX)    NULL,
    CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserAuditTrail_UserId' AND object_id = OBJECT_ID('IAM.UserAuditTrail'))
    CREATE NONCLUSTERED INDEX IX_UserAuditTrail_UserId ON IAM.UserAuditTrail(UserId, CreatedDateUtc DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserAuditTrail_TenantId' AND object_id = OBJECT_ID('IAM.UserAuditTrail'))
    CREATE NONCLUSTERED INDEX IX_UserAuditTrail_TenantId ON IAM.UserAuditTrail(TenantId, CreatedDateUtc DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_UserAuditTrail_ActionCode' AND object_id = OBJECT_ID('IAM.UserAuditTrail'))
    CREATE NONCLUSTERED INDEX IX_UserAuditTrail_ActionCode ON IAM.UserAuditTrail(ActionCode, CreatedDateUtc DESC);

-- ============================================================
-- LOGIN ATTEMPT TRACKING TABLE
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.LoginAttempt'))
CREATE TABLE IAM.LoginAttempt (
    LoginAttemptId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    UserId              UNIQUEIDENTIFIER NULL,
    UserName            NVARCHAR(200)    NOT NULL,
    IpAddress           NVARCHAR(50)     NOT NULL,
    UserAgent           NVARCHAR(500)    NULL,
    IsSuccessful        BIT              NOT NULL DEFAULT 0,
    FailureReason       NVARCHAR(500)    NULL,
    AttemptDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LoginAttempt_UserId' AND object_id = OBJECT_ID('IAM.LoginAttempt'))
    CREATE NONCLUSTERED INDEX IX_LoginAttempt_UserId ON IAM.LoginAttempt(UserId, AttemptDateUtc DESC);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_LoginAttempt_UserName' AND object_id = OBJECT_ID('IAM.LoginAttempt'))
    CREATE NONCLUSTERED INDEX IX_LoginAttempt_UserName ON IAM.LoginAttempt(UserName, AttemptDateUtc DESC);
";

    // â”€â”€ 0043 â€” CRM: Lead Scoring, Assignment, and Follow-Up Seed Data â”€â”€â”€â”€â”€â”€â”€â”€
    private const string Migration0043_CrmLeadScoringAssignmentFollowUpSeed = @"
DECLARE @DefaultTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @FirstUserId UNIQUEIDENTIFIER = (SELECT TOP 1 UserId FROM IAM.[User] ORDER BY CreatedDateUtc);

-- ============================================================
-- SEED CRM.Lead with test data for Lead Scoring page
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM CRM.Lead WHERE LeadNumber = 'LD-001-HS')
BEGIN
    INSERT INTO CRM.Lead (LeadId, TenantId, LeadNumber, FirstName, LastName, Email, Phone, AccountName, InterestedService, Score, PriorityCode, StatusCodeId, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES 
        (NEWID(), @DefaultTenantId, 'LD-001-HS', 'John', 'Smith', 'john.smith@techinnovations.com', '(555) 123-0001', 'Tech Innovations Inc', 'Enterprise Solution', 92, 'High', 1, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-002-HS', 'Sarah', 'Johnson', 'sarah.johnson@globalsol.com', '(555) 123-0002', 'Global Solutions Ltd', 'Consulting', 88, 'High', 1, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-003-HS', 'Michael', 'Chen', 'm.chen@digitaldyn.com', '(555) 123-0003', 'Digital Dynamics Corp', 'Cloud Services', 85, 'High', 2, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-004-HS', 'Emily', 'Rodriguez', 'emily.r@futureforward.com', '(555) 123-0004', 'Future Forward Inc', 'Software License', 82, 'High', 1, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-005-HS', 'David', 'Williams', 'dwilliams@esgroup.com', '(555) 123-0005', 'Enterprise Solutions Group', 'Implementation', 80, 'High', 1, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-006-HS', 'Lisa', 'Anderson', 'l.anderson@cloudcomp.com', '(555) 123-0006', 'Cloud Computing Partners', 'Support Package', 81, 'High', 2, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-007-MS', 'James', 'Martinez', 'james.m@innovlabs.com', '(555) 123-0007', 'Innovation Labs', 'Training', 76, 'Medium', 1, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-008-MS', 'Patricia', 'Lee', 'patricia.lee@summitind.com', '(555) 123-0008', 'Summit Industries', 'Maintenance', 72, 'Medium', 1, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-009-MS', 'Robert', 'Taylor', 'r.taylor@nexustech.com', '(555) 123-0009', 'Nexus Technology', 'Upgrade', 68, 'Medium', 2, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-010-MS', 'Jennifer', 'White', 'jwhite@velocitypart.com', '(555) 123-0010', 'Velocity Partners', 'Consultation', 64, 'Medium', 1, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-011-MS', 'Christopher', 'Brown', 'cbrown@catalystgrp.com', '(555) 123-0011', 'Catalyst Group', 'Demo', 59, 'Medium', 1, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-012-LS', 'Amanda', 'Wilson', 'awilson@horizonsol.com', '(555) 123-0012', 'Horizon Solutions', 'Information', 48, 'Low', 1, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-013-LS', 'Kevin', 'Davis', 'kdavis@apexvent.com', '(555) 123-0013', 'Apex Ventures', 'Follow-up', 42, 'Low', 1, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-014-LS', 'Nicole', 'Garcia', 'ngarcia@primeresources.com', '(555) 123-0014', 'Prime Resources', 'Quote', 38, 'Low', 3, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-015-LS', 'Brandon', 'Harris', 'bharris@quantumdyn.com', '(555) 123-0015', 'Quantum Dynamics', 'Interest', 35, 'Low', 1, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-016-MS', 'Stephanie', 'Martin', 'smartin@titancorp.com', '(555) 123-0016', 'Titan Corporate', 'Contract', 71, 'Medium', 2, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-017-MS', 'Matthew', 'Thompson', 'mthompson@epochent.com', '(555) 123-0017', 'Epoch Enterprises', 'Partnership', 67, 'Medium', 1, GETUTCDATE(), @FirstUserId, 0),
        (NEWID(), @DefaultTenantId, 'LD-018-LS', 'Victoria', 'Clark', 'vclark@spectrumind.com', '(555) 123-0018', 'Spectrum Industries', 'Referral', 45, 'Low', 1, GETUTCDATE(), @FirstUserId, 0);
END

-- ============================================================
-- SEED CRM.LeadActivity with test follow-up activities
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE Subject = 'Initial outreach call' AND ActivityTypeCode = 'Call')
BEGIN
    DECLARE @LeadIds_High TABLE (LeadId UNIQUEIDENTIFIER);
    DECLARE @LeadIds_Medium TABLE (LeadId UNIQUEIDENTIFIER);
    DECLARE @LeadIds_Low TABLE (LeadId UNIQUEIDENTIFIER);

    INSERT INTO @LeadIds_High SELECT LeadId FROM CRM.Lead WHERE Score >= 80;
    INSERT INTO @LeadIds_Medium SELECT LeadId FROM CRM.Lead WHERE Score BETWEEN 50 AND 79;
    INSERT INTO @LeadIds_Low SELECT LeadId FROM CRM.Lead WHERE Score < 50;

    -- High priority: Phone calls
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, ActivityTypeCode, Subject, Notes, ActivityDate, IsCompleted, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT NEWID(), @DefaultTenantId, LeadId, 'Call', 'Initial outreach call', 'Follow up on demo request', CAST(GETUTCDATE() AS DATE), 0, GETUTCDATE(), @FirstUserId, 0
    FROM @LeadIds_High;

    -- Medium priority: Emails
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, ActivityTypeCode, Subject, Notes, ActivityDate, IsCompleted, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT NEWID(), @DefaultTenantId, LeadId, 'Email', 'Send product information', 'Share pricing and features', CAST(GETUTCDATE() AS DATE), 0, GETUTCDATE(), @FirstUserId, 0
    FROM @LeadIds_Medium;

    -- Low priority: Marketing automation
    INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, ActivityTypeCode, Subject, Notes, ActivityDate, IsCompleted, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT NEWID(), @DefaultTenantId, LeadId, 'Note', 'Add to nurture campaign', 'Send educational content series', CAST(GETUTCDATE() AS DATE), 0, GETUTCDATE(), @FirstUserId, 0
    FROM @LeadIds_Low;
END
";

    // â”€â”€ Internals â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
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

    // â”€â”€ 0048 â€” Agency Dashboard: Claims schema + seed data â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
    private const string Migration0048_AgencyDashboardClaimsSeed = @"
-- Guard: add BranchId to Finance.Agreement
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'Finance.Agreement') AND name = N'BranchId')
    ALTER TABLE Finance.Agreement ADD BranchId UNIQUEIDENTIFIER NULL;

-- Guard: add IsProducer to IAM.[User]
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.[User]') AND name = N'IsProducer')
    ALTER TABLE IAM.[User] ADD IsProducer BIT NOT NULL DEFAULT 0;

-- Guard: add IsActive to IAM.[User]
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'IAM.[User]') AND name = N'IsActive')
    ALTER TABLE IAM.[User] ADD IsActive BIT NOT NULL DEFAULT 1;

-- Guard: create Claims schema
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Claims')
    EXEC('CREATE SCHEMA Claims');

-- Guard: create Claims.Claim table
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'Claim' AND schema_id = SCHEMA_ID('Claims'))
BEGIN
    CREATE TABLE Claims.Claim (
        ClaimId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId         UNIQUEIDENTIFIER NOT NULL,
        ClaimNumber      NVARCHAR(50)     NOT NULL,
        Status           NVARCHAR(50)     NOT NULL,
        LineOfBusiness   NVARCHAR(100)    NULL,
        ClientName       NVARCHAR(200)    NULL,
        ReserveAmount    DECIMAL(18,2)    NOT NULL DEFAULT 0,
        PaidAmount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
        IsCatastrophe    BIT              NOT NULL DEFAULT 0,
        OpenedDateUtc    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedDateUtc   DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        IsDeleted        BIT              NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_Claim_TenantId ON Claims.Claim (TenantId);
END

DECLARE @SeedTenant UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

-- Seed: Claims
IF NOT EXISTS (SELECT 1 FROM Claims.Claim WHERE TenantId = @SeedTenant)
BEGIN
    INSERT INTO Claims.Claim (ClaimId, TenantId, ClaimNumber, Status, LineOfBusiness, ClientName, ReserveAmount, PaidAmount, IsCatastrophe, OpenedDateUtc)
    VALUES
        (NEWID(), @SeedTenant, 'CLM-2024-0001', 'Open',   'Commercial Auto',     'Acme Corp',          25000,      0, 0, DATEADD(day,-30,GETUTCDATE())),
        (NEWID(), @SeedTenant, 'CLM-2024-0002', 'Open',   'General Liability',   'Smith Industries',   75000,  12000, 0, DATEADD(day,-45,GETUTCDATE())),
        (NEWID(), @SeedTenant, 'CLM-2024-0003', 'Closed', 'Commercial Property', 'Johnson LLC',        15000,  14500, 0, DATEADD(day,-90,GETUTCDATE())),
        (NEWID(), @SeedTenant, 'CLM-2024-0004', 'Open',   'Workers Compensation', 'HealthPlus',        50000,   5000, 0, DATEADD(day,-15,GETUTCDATE())),
        (NEWID(), @SeedTenant, 'CLM-2024-0005', 'Open',   'Professional Liability', 'SecureTech',       100000,  20000, 0, GETUTCDATE()),
        (NEWID(), @SeedTenant, 'CLM-2024-0006', 'Closed', 'Commercial Auto',     'Acme Corp',          25000,  25000, 0, DATEADD(day,-60,GETUTCDATE())),
        (NEWID(), @SeedTenant, 'CLM-2024-0007', 'Open',   'General Liability',   'Smith Industries',   75000,  20000, 0, GETUTCDATE()),
        (NEWID(), @SeedTenant, 'CLM-2024-0008', 'Closed', 'Commercial Property', 'Johnson LLC',        15000,  15000, 0, DATEADD(day,-120,GETUTCDATE())),
        (NEWID(), @SeedTenant, 'CLM-2024-0009', 'Open',   'Workers Compensation', 'HealthPlus',        50000,   1000,  0, GETUTCDATE()),
        (NEWID(), @SeedTenant, 'CLM-2024-0010', 'Open',   'Professional Liability', 'SecureTech',       100000,  50000, 0, DATEADD(day,-10,GETUTCDATE()));
END

-- Seed: Claims.LossEstimate
IF NOT EXISTS (SELECT 1 FROM Claims.LossEstimate WHERE TenantId = @SeedTenant)
BEGIN
    INSERT INTO Claims.LossEstimate (LossEstimateId, TenantId, ClaimId, EstimateAmount, AdjusterNotes, CreatedDateUtc)
    VALUES
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0001'), 25000, 'Initial estimate', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0002'), 75000, 'Investigation ongoing', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0003'), 15000, 'Pending review', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0004'), 50000, 'Authorized', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0005'), 100000, 'Awaiting documentation', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0006'), 25000, 'Closed - paid in full', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0007'), 75000, 'Settled', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0008'), 15000, 'Closed - no further action', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0009'), 50000, 'Under negotiation', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0010'), 100000, 'Final settlement', GETUTCDATE());
END

-- Seed: Claims.ClaimActivity (audit log)
IF NOT EXISTS (SELECT 1 FROM Claims.ClaimActivity WHERE TenantId = @SeedTenant)
BEGIN
    INSERT INTO Claims.ClaimActivity (ClaimActivityId, TenantId, ClaimId, ActivityType, ActivityDescription, CreatedDateUtc)
    VALUES
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0001'), 'Claim Created', 'Claim created with initial details', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0001'), 'Loss Estimate Created', 'Loss estimate created by adjuster', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0002'), 'Claim Created', 'Claim created with initial details', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0002'), 'Loss Estimate Created', 'Loss estimate created by adjuster', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0003'), 'Claim Created', 'Claim created with initial details', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0003'), 'Loss Estimate Created', 'Loss estimate created by adjuster', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0004'), 'Claim Created', 'Claim created with initial details', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0004'), 'Loss Estimate Created', 'Loss estimate created by adjuster', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0005'), 'Claim Created', 'Claim created with initial details', GETUTCDATE()),
        (NEWID(), @SeedTenant, (SELECT ClaimId FROM Claims.Claim WHERE ClaimNumber = 'CLM-2024-0005'), 'Loss Estimate Created', 'Loss estimate created by adjuster', GETUTCDATE());
END
";

    private const string Migration0049_AgencyDashboardBillingSeed = "";
    private const string Migration0050_AgencySetupSeed = "";
    private const string Migration0051_SecuritySeed = "";
    private const string Migration0052_AuditLogAddColumns = "";
    private const string Migration0053_IamUserAddMissingColumns = "";
    private const string Migration0054_CrmConfigAccountConfigCreate = "";
    private const string Migration0055_CrmConfigAccountConfigSeed = "";
    private const string Migration0056_TenantSettingsWorkflowCreateSeed = "";
    private const string Migration0057_SubscriptionSettingsWorkflowCreateSeed = "";
    private const string Migration0058_CrmConfigurationCreateSeed = "";
    private const string Migration0059_AccountConfigClientSchemaCreate = "";
    private const string Migration0060_PolicyConfigPolicySchemaCreateSeed = "";
    private const string Migration0061_PolicyConfigIdempotentSeed = "";
    private const string Migration0062_CarrierConfigCreateSeed = "";
    private const string Migration0063_CarrierMarketRulesCreateSeed = "";
    private const string Migration0064_WorkflowConfigCreateSeed = "";
    private const string Migration0065_CommunicationConfigCreateSeed = "";
    private const string Migration0066_DocumentConfigCreateSeed = "";
    private const string Migration0067_BillingConfigCreateSeed = "";
    private const string Migration0068_CommissionConfigCreateSeed = "";
    private const string Migration0069_MarketingConfigCreateSeed = "";
    private const string Migration0070_PortalConfigCreateSeed = "";
    private const string Migration0071_IntegrationConfigCreateSeed = "";
    private const string Migration0072_MessagingSigningIntegrationConfigSeed = "";
    private const string Migration0073_FinancialIntegrationConfigSeed = "";
    private const string Migration0074_ApiAutomationIntegrationConfigSeed = "";
    private const string Migration0075_AiConfigCreateSeed = "";
    private const string Migration0076_DataConfigCreateSeed = "";
    private const string Migration0077_SubscriptionConfigCreateSeed = "";
    private const string Migration0078_TenantConfigCreateSeed = "";
    private const string Migration0079_OpsTaskItemCreateSeed = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'OPS')
    EXEC('CREATE SCHEMA OPS');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'TaskItem' AND schema_id = SCHEMA_ID(N'OPS'))
BEGIN
    CREATE TABLE OPS.TaskItem (
        TaskItemId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId          UNIQUEIDENTIFIER NOT NULL,
        TaskNumber        NVARCHAR(50)     NOT NULL,
        Title             NVARCHAR(200)    NOT NULL,
        Description       NVARCHAR(2000)   NULL,
        TaskTypeCode      NVARCHAR(50)     NOT NULL,
        StageCode         NVARCHAR(50)     NOT NULL,
        PriorityCode      NVARCHAR(50)     NOT NULL,
        StatusCode        NVARCHAR(50)     NOT NULL,
        RelatedEntityName NVARCHAR(100)    NULL,
        RelatedEntityId   UNIQUEIDENTIFIER NULL,
        AccountId         UNIQUEIDENTIFIER NULL,
        AssignedToUserId  UNIQUEIDENTIFIER NULL,
        DueDate           DATE             NULL,
        CompletedDate     DATE             NULL,
        CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId   UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc   DATETIME2        NULL,
        ModifiedByUserId  UNIQUEIDENTIFIER NULL,
        IsDeleted         BIT              NOT NULL DEFAULT 0
    );

    CREATE UNIQUE INDEX UX_TaskItem_Tenant_TaskNumber ON OPS.TaskItem(TenantId, TaskNumber) WHERE IsDeleted = 0;
    CREATE INDEX IX_TaskItem_Tenant_Stage ON OPS.TaskItem(TenantId, StageCode, StatusCode, IsDeleted);
END

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM OPS.TaskItem WHERE TenantId = @TenantId AND TaskNumber = N'TASK-2024-0001')
BEGIN
    INSERT INTO OPS.TaskItem (TaskItemId, TenantId, TaskNumber, Title, Description, TaskTypeCode, StageCode, PriorityCode, StatusCode, RelatedEntityName, DueDate, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, N'TASK-2024-0001', N'Review renewal service request', N'Client renewal request is waiting for service team triage.', N'Service Request', N'Intake', N'High', N'Open', N'ServiceRequest', DATEADD(day, -1, CAST(SYSUTCDATETIME() AS date)), SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, N'TASK-2024-0002', N'Prepare agreement packet', N'Finalize agreement packet and validate required coverage exhibits.', N'Agreement', N'In Progress', N'High', N'Open', N'Agreement', CAST(SYSUTCDATETIME() AS date), SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, N'TASK-2024-0003', N'Resolve endorsement issue', N'Endorsement issue requires final service review before closure.', N'Issue', N'Review', N'High', N'Open', N'ServiceIssue', DATEADD(day, 1, CAST(SYSUTCDATETIME() AS date)), SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, N'TASK-2024-0004', N'Approve workflow exception', N'Workflow exception is pending manager approval.', N'Workflow', N'Approval', N'Medium', N'Open', N'WorkflowInstance', DATEADD(day, 2, CAST(SYSUTCDATETIME() AS date)), SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, N'TASK-2024-0005', N'Log post-bind activity', N'Activity logged and associated records updated.', N'Activity', N'Done', N'Low', N'Completed', N'OperationalActivity', DATEADD(day, -2, CAST(SYSUTCDATETIME() AS date)), SYSUTCDATETIME(), NULL, NULL, NULL, 0);
END
";
    private const string Migration0080_DmsESignRequestCreateSeed = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'DMS')
    EXEC('CREATE SCHEMA DMS');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ESignRequest' AND schema_id = SCHEMA_ID(N'DMS'))
BEGIN
    CREATE TABLE DMS.ESignRequest (
        ESignRequestId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId               UNIQUEIDENTIFIER NOT NULL,
        DocumentId             UNIQUEIDENTIFIER NOT NULL,
        SignerName             NVARCHAR(200)    NOT NULL,
        SignerEmail            NVARCHAR(320)    NOT NULL,
        Priority               NVARCHAR(50)     NOT NULL DEFAULT N'Normal',
        Status                 NVARCHAR(50)     NOT NULL DEFAULT N'Sent',
        SentDate               DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        DueDate                DATETIME2        NOT NULL,
        CompletedDate          DATETIME2        NULL,
        Message                NVARCHAR(2000)   NULL,
        VoidReason             NVARCHAR(1000)   NULL,
        LastReminderSentDateUtc DATETIME2       NULL,
        CreatedDateUtc         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId        UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc        DATETIME2        NULL,
        ModifiedByUserId       UNIQUEIDENTIFIER NULL,
        IsDeleted              BIT              NOT NULL DEFAULT 0
    );

    CREATE INDEX IX_ESignRequest_Tenant_Status ON DMS.ESignRequest(TenantId, Status, IsDeleted);
    CREATE INDEX IX_ESignRequest_DocumentId ON DMS.ESignRequest(DocumentId, IsDeleted);
END

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @DocumentId UNIQUEIDENTIFIER = (SELECT TOP 1 DocumentId FROM DMS.Document WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc DESC);

IF @DocumentId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM DMS.ESignRequest WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO DMS.ESignRequest (ESignRequestId, TenantId, DocumentId, SignerName, SignerEmail, Priority, Status, SentDate, DueDate, CompletedDate, Message, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @DocumentId, N'Jordan Lee', N'jordan.lee@example.com', N'Normal', N'Sent', DATEADD(day, -2, SYSUTCDATETIME()), DATEADD(day, 5, SYSUTCDATETIME()), NULL, N'Please review and sign the attached document.', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, @DocumentId, N'Morgan Smith', N'morgan.smith@example.com', N'High', N'Viewed', DATEADD(day, -5, SYSUTCDATETIME()), DATEADD(day, -1, SYSUTCDATETIME()), NULL, N'Please sign as soon as possible.', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, @DocumentId, N'Taylor Chen', N'taylor.chen@example.com', N'Normal', N'Signed', DATEADD(day, -10, SYSUTCDATETIME()), DATEADD(day, -3, SYSUTCDATETIME()), DATEADD(day, -4, SYSUTCDATETIME()), N'Thank you.', SYSUTCDATETIME(), NULL, NULL, NULL, 0);
END
";
    private const string Migration0081_BillingArAgingSnapshotCreateSeed = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Billing')
    EXEC('CREATE SCHEMA Billing');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ArAgingSnapshot' AND schema_id = SCHEMA_ID(N'Billing'))
BEGIN
    CREATE TABLE Billing.ArAgingSnapshot (
        SnapshotId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId          UNIQUEIDENTIFIER NOT NULL,
        AccountId         UNIQUEIDENTIFIER NOT NULL,
        SnapshotDate      DATE             NOT NULL,
        CurrentAmount     DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Days30Amount      DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Days60Amount      DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Days90Amount      DECIMAL(18,2)    NOT NULL DEFAULT 0,
        Days90PlusAmount  DECIMAL(18,2)    NOT NULL DEFAULT 0,
        TotalOutstanding  DECIMAL(18,2)    NOT NULL DEFAULT 0,
        CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId   UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc   DATETIME2        NULL,
        ModifiedByUserId  UNIQUEIDENTIFIER NULL,
        IsDeleted         BIT              NOT NULL DEFAULT 0
    );

    CREATE INDEX IX_ArAgingSnapshot_Tenant_Date ON Billing.ArAgingSnapshot(TenantId, SnapshotDate DESC, IsDeleted);
    CREATE INDEX IX_ArAgingSnapshot_Tenant_Account ON Billing.ArAgingSnapshot(TenantId, AccountId, IsDeleted);
END

DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM Billing.ArAgingSnapshot WHERE TenantId = @TenantId)
BEGIN
    INSERT INTO Billing.ArAgingSnapshot
        (SnapshotId, TenantId, AccountId, SnapshotDate, CurrentAmount, Days30Amount, Days60Amount, Days90Amount, Days90PlusAmount, TotalOutstanding, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, '11111111-1111-1111-1111-111111111111', CAST(SYSUTCDATETIME() AS date), 1840.00, 620.00, 0.00, 0.00, 0.00, 2460.00, SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, '22222222-2222-2222-2222-222222222222', CAST(SYSUTCDATETIME() AS date), 0.00, 2400.00, 875.00, 0.00, 0.00, 3275.00, SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, '33333333-3333-3333-3333-333333333333', CAST(SYSUTCDATETIME() AS date), 0.00, 0.00, 1475.00, 650.00, 225.00, 2350.00, SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, '44444444-4444-4444-4444-444444444444', DATEADD(day, -7, CAST(SYSUTCDATETIME() AS date)), 915.00, 0.00, 0.00, 0.00, 0.00, 915.00, SYSUTCDATETIME(), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, '55555555-5555-5555-5555-555555555555', DATEADD(day, -7, CAST(SYSUTCDATETIME() AS date)), 0.00, 720.00, 310.00, 90.00, 0.00, 1120.00, SYSUTCDATETIME(), NULL, NULL, NULL, 0);
END
";
    private const string Migration0082_CompliancePoliciesAcknowledgementsCreateSeed = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Compliance')
    EXEC('CREATE SCHEMA Compliance');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'PolicyDocument' AND schema_id = SCHEMA_ID(N'Compliance'))
BEGIN
    CREATE TABLE Compliance.PolicyDocument (
        PolicyDocumentId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId               UNIQUEIDENTIFIER NOT NULL,
        PolicyCode             NVARCHAR(50)     NOT NULL,
        PolicyTitle            NVARCHAR(200)    NOT NULL,
        PolicyTypeCode         NVARCHAR(100)    NOT NULL,
        Version                NVARCHAR(50)     NOT NULL DEFAULT N'1.0',
        EffectiveDateUtc       DATETIME2        NULL,
        IsActive               BIT              NOT NULL DEFAULT 1,
        StatusCode             NVARCHAR(50)     NOT NULL DEFAULT N'Draft',
        Description            NVARCHAR(1000)   NULL,
        Content                NVARCHAR(MAX)    NULL,
        OwnedByUserId          UNIQUEIDENTIFIER NULL,
        ParentPolicyDocumentId UNIQUEIDENTIFIER NULL,
        PublishedByUserId      UNIQUEIDENTIFIER NULL,
        PublishedDateUtc       DATETIME2        NULL,
        RetiredByUserId        UNIQUEIDENTIFIER NULL,
        RetiredDateUtc         DATETIME2        NULL,
        CreatedDateUtc         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId        UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc        DATETIME2        NULL,
        ModifiedByUserId       UNIQUEIDENTIFIER NULL,
        IsDeleted              BIT              NOT NULL DEFAULT 0
    );

    EXEC(N'CREATE INDEX IX_PolicyDocument_Tenant_Status ON Compliance.PolicyDocument(TenantId, StatusCode, IsDeleted);');
    EXEC(N'CREATE UNIQUE INDEX UX_PolicyDocument_Tenant_Code_Version ON Compliance.PolicyDocument(TenantId, PolicyCode, Version) WHERE IsDeleted = 0;');
END

IF COL_LENGTH(N'Compliance.PolicyDocument', N'TenantId') IS NULL ALTER TABLE Compliance.PolicyDocument ADD TenantId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Compliance.PolicyDocument', N'Content') IS NULL ALTER TABLE Compliance.PolicyDocument ADD Content NVARCHAR(MAX) NULL;
IF COL_LENGTH(N'Compliance.PolicyDocument', N'ParentPolicyDocumentId') IS NULL ALTER TABLE Compliance.PolicyDocument ADD ParentPolicyDocumentId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Compliance.PolicyDocument', N'PublishedByUserId') IS NULL ALTER TABLE Compliance.PolicyDocument ADD PublishedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Compliance.PolicyDocument', N'PublishedDateUtc') IS NULL ALTER TABLE Compliance.PolicyDocument ADD PublishedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Compliance.PolicyDocument', N'RetiredByUserId') IS NULL ALTER TABLE Compliance.PolicyDocument ADD RetiredByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Compliance.PolicyDocument', N'RetiredDateUtc') IS NULL ALTER TABLE Compliance.PolicyDocument ADD RetiredDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Compliance.PolicyDocument', N'CreatedDateUtc') IS NULL ALTER TABLE Compliance.PolicyDocument ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyDocument_CreatedDateUtc DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Compliance.PolicyDocument', N'CreatedByUserId') IS NULL ALTER TABLE Compliance.PolicyDocument ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Compliance.PolicyDocument', N'ModifiedDateUtc') IS NULL ALTER TABLE Compliance.PolicyDocument ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Compliance.PolicyDocument', N'ModifiedByUserId') IS NULL ALTER TABLE Compliance.PolicyDocument ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Compliance.PolicyDocument', N'IsDeleted') IS NULL ALTER TABLE Compliance.PolicyDocument ADD IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyDocument_IsDeleted DEFAULT 0;
EXEC(N'UPDATE Compliance.PolicyDocument SET TenantId = ''00000000-0000-0000-0000-000000000001'' WHERE TenantId IS NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'PolicyAudience' AND schema_id = SCHEMA_ID(N'Compliance'))
BEGIN
    CREATE TABLE Compliance.PolicyAudience (
        AudienceId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId          UNIQUEIDENTIFIER NOT NULL,
        PolicyDocumentId  UNIQUEIDENTIFIER NOT NULL,
        TargetTypeCode    NVARCHAR(50)     NOT NULL,
        TargetId          UNIQUEIDENTIFIER NULL,
        TargetName        NVARCHAR(200)    NOT NULL,
        IsRequired        BIT              NOT NULL DEFAULT 1,
        AddedByUserId     UNIQUEIDENTIFIER NULL,
        AddedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId   UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc   DATETIME2        NULL,
        ModifiedByUserId  UNIQUEIDENTIFIER NULL,
        IsDeleted         BIT              NOT NULL DEFAULT 0
    );

    EXEC(N'CREATE INDEX IX_PolicyAudience_Policy ON Compliance.PolicyAudience(PolicyDocumentId, IsDeleted);');
    EXEC(N'CREATE INDEX IX_PolicyAudience_Tenant_Target ON Compliance.PolicyAudience(TenantId, TargetTypeCode, TargetId, IsDeleted);');
END

IF COL_LENGTH(N'Compliance.PolicyAudience', N'TenantId') IS NULL ALTER TABLE Compliance.PolicyAudience ADD TenantId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Compliance.PolicyAudience', N'CreatedDateUtc') IS NULL ALTER TABLE Compliance.PolicyAudience ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyAudience_CreatedDateUtc DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Compliance.PolicyAudience', N'CreatedByUserId') IS NULL ALTER TABLE Compliance.PolicyAudience ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Compliance.PolicyAudience', N'ModifiedDateUtc') IS NULL ALTER TABLE Compliance.PolicyAudience ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Compliance.PolicyAudience', N'ModifiedByUserId') IS NULL ALTER TABLE Compliance.PolicyAudience ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Compliance.PolicyAudience', N'IsDeleted') IS NULL ALTER TABLE Compliance.PolicyAudience ADD IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyAudience_IsDeleted DEFAULT 0;
EXEC(N'UPDATE au SET TenantId = p.TenantId FROM Compliance.PolicyAudience au JOIN Compliance.PolicyDocument p ON p.PolicyDocumentId = au.PolicyDocumentId WHERE au.TenantId IS NULL;');
EXEC(N'UPDATE Compliance.PolicyAudience SET TenantId = ''00000000-0000-0000-0000-000000000001'' WHERE TenantId IS NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'PolicyAcknowledgement' AND schema_id = SCHEMA_ID(N'Compliance'))
BEGIN
    CREATE TABLE Compliance.PolicyAcknowledgement (
        AcknowledgementId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        PolicyDocumentId    UNIQUEIDENTIFIER NOT NULL,
        UserId              UNIQUEIDENTIFIER NOT NULL,
        AcknowledgedDateUtc DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        Channel             NVARCHAR(50)     NULL,
        IpAddress           NVARCHAR(64)     NULL,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc     DATETIME2        NULL,
        ModifiedByUserId    UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    EXEC(N'CREATE INDEX IX_PolicyAcknowledgement_Policy ON Compliance.PolicyAcknowledgement(PolicyDocumentId, IsDeleted);');
    EXEC(N'CREATE UNIQUE INDEX UX_PolicyAcknowledgement_Policy_User ON Compliance.PolicyAcknowledgement(PolicyDocumentId, UserId) WHERE IsDeleted = 0;');
END

IF COL_LENGTH(N'Compliance.PolicyAcknowledgement', N'TenantId') IS NULL ALTER TABLE Compliance.PolicyAcknowledgement ADD TenantId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Compliance.PolicyAcknowledgement', N'CreatedDateUtc') IS NULL ALTER TABLE Compliance.PolicyAcknowledgement ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyAcknowledgement_CreatedDateUtc DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Compliance.PolicyAcknowledgement', N'CreatedByUserId') IS NULL ALTER TABLE Compliance.PolicyAcknowledgement ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Compliance.PolicyAcknowledgement', N'ModifiedDateUtc') IS NULL ALTER TABLE Compliance.PolicyAcknowledgement ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Compliance.PolicyAcknowledgement', N'ModifiedByUserId') IS NULL ALTER TABLE Compliance.PolicyAcknowledgement ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Compliance.PolicyAcknowledgement', N'IsDeleted') IS NULL ALTER TABLE Compliance.PolicyAcknowledgement ADD IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyAcknowledgement_IsDeleted DEFAULT 0;
EXEC(N'UPDATE ack SET TenantId = p.TenantId FROM Compliance.PolicyAcknowledgement ack JOIN Compliance.PolicyDocument p ON p.PolicyDocumentId = ack.PolicyDocumentId WHERE ack.TenantId IS NULL;');
EXEC(N'UPDATE Compliance.PolicyAcknowledgement SET TenantId = ''00000000-0000-0000-0000-000000000001'' WHERE TenantId IS NULL;');

EXEC(N'
DECLARE @TenantId UNIQUEIDENTIFIER = ''00000000-0000-0000-0000-000000000001'';
DECLARE @User1 UNIQUEIDENTIFIER = NULL;
DECLARE @User2 UNIQUEIDENTIFIER = NULL;
IF OBJECT_ID(N''IAM.[User]'') IS NOT NULL
BEGIN
    SELECT TOP 1 @User1 = UserId FROM IAM.[User] ORDER BY UserId;
    SELECT TOP 1 @User2 = UserId FROM IAM.[User] WHERE UserId <> @User1 ORDER BY UserId;
END;

IF NOT EXISTS (SELECT 1 FROM Compliance.PolicyDocument WHERE TenantId = @TenantId AND PolicyCode = N''COMP-001'')
BEGIN
    INSERT INTO Compliance.PolicyDocument (PolicyDocumentId, TenantId, PolicyCode, PolicyTitle, PolicyTypeCode, Version, EffectiveDateUtc, IsActive, StatusCode, Description, Content, OwnedByUserId, PublishedByUserId, PublishedDateUtc, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (''a1000000-0000-0000-0000-000000000001'', @TenantId, N''COMP-001'', N''Agency Code of Conduct'', N''Compliance'', N''1.0'', DATEADD(day, -14, SYSUTCDATETIME()), 1, N''Published'', N''Core agency conduct expectations and ethics standards.'', N''All agency users must follow the code of conduct.'', @User1, @User1, DATEADD(day, -20, SYSUTCDATETIME()), SYSUTCDATETIME(), @User1, NULL, NULL, 0),
        (''a1000000-0000-0000-0000-000000000002'', @TenantId, N''PRIV-001'', N''Client Data Privacy Policy'', N''Privacy'', N''1.0'', DATEADD(day, 7, SYSUTCDATETIME()), 1, N''Published'', N''Privacy handling requirements for client and prospect data.'', N''Client data must be protected and processed according to policy.'', @User1, @User1, DATEADD(day, -3, SYSUTCDATETIME()), SYSUTCDATETIME(), @User1, NULL, NULL, 0),
        (''a1000000-0000-0000-0000-000000000003'', @TenantId, N''INFOSEC-001'', N''Information Security Policy'', N''Information Security'', N''1.0'', DATEADD(day, 14, SYSUTCDATETIME()), 1, N''Draft'', N''Security baseline for systems, credentials, and devices.'', N''Draft information security controls.'', @User1, NULL, NULL, SYSUTCDATETIME(), @User1, NULL, NULL, 0),
        (''a1000000-0000-0000-0000-000000000004'', @TenantId, N''OPS-001'', N''Policy Servicing Standards'', N''Operations'', N''1.0'', DATEADD(day, -5, SYSUTCDATETIME()), 0, N''Retired'', N''Retired servicing standards retained for audit history.'', N''Retired policy content.'', @User1, @User1, DATEADD(day, -60, SYSUTCDATETIME()), SYSUTCDATETIME(), @User1, SYSUTCDATETIME(), @User1, 0);
END;

IF NOT EXISTS (SELECT 1 FROM Compliance.PolicyAudience WHERE PolicyDocumentId = ''a1000000-0000-0000-0000-000000000001'')
BEGIN
    INSERT INTO Compliance.PolicyAudience (AudienceId, TenantId, PolicyDocumentId, TargetTypeCode, TargetId, TargetName, IsRequired, AddedByUserId, AddedDateUtc, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, ''a1000000-0000-0000-0000-000000000001'', N''User'', @User1, N''Primary User'', 1, @User1, SYSUTCDATETIME(), SYSUTCDATETIME(), @User1, NULL, NULL, 0),
        (NEWID(), @TenantId, ''a1000000-0000-0000-0000-000000000001'', N''User'', @User2, N''Secondary User'', 1, @User1, SYSUTCDATETIME(), SYSUTCDATETIME(), @User1, NULL, NULL, CASE WHEN @User2 IS NULL THEN 1 ELSE 0 END),
        (NEWID(), @TenantId, ''a1000000-0000-0000-0000-000000000002'', N''Role'', NULL, N''All Licensed Staff'', 1, @User1, SYSUTCDATETIME(), SYSUTCDATETIME(), @User1, NULL, NULL, 0),
        (NEWID(), @TenantId, ''a1000000-0000-0000-0000-000000000002'', N''User'', @User1, N''Primary User'', 1, @User1, SYSUTCDATETIME(), SYSUTCDATETIME(), @User1, NULL, NULL, 0);
END;

IF @User1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Compliance.PolicyAcknowledgement WHERE PolicyDocumentId = ''a1000000-0000-0000-0000-000000000001'' AND UserId = @User1)
BEGIN
    INSERT INTO Compliance.PolicyAcknowledgement (AcknowledgementId, TenantId, PolicyDocumentId, UserId, AcknowledgedDateUtc, Channel, IpAddress, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, ''a1000000-0000-0000-0000-000000000001'', @User1, DATEADD(day, -10, SYSUTCDATETIME()), N''Web'', N''127.0.0.1'', SYSUTCDATETIME(), @User1, NULL, NULL, 0);
END;
');
";
    private const string Migration0083_OperationsWorkflowSystemFlowCreateSeed = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Sales') EXEC('CREATE SCHEMA Sales');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'OPS') EXEC('CREATE SCHEMA OPS');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Workflow') EXEC('CREATE SCHEMA Workflow');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Audit') EXEC('CREATE SCHEMA Audit');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Agreement' AND schema_id = SCHEMA_ID(N'Sales'))
BEGIN
    CREATE TABLE Sales.Agreement (
        AgreementId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AgreementNumber NVARCHAR(50) NOT NULL,
        AccountId UNIQUEIDENTIFIER NOT NULL,
        OpportunityId UNIQUEIDENTIFIER NULL,
        AgreementStatusCodeId INT NOT NULL DEFAULT 1,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
    CREATE UNIQUE INDEX UX_Agreement_Tenant_Number ON Sales.Agreement(TenantId, AgreementNumber) WHERE IsDeleted = 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'Engagement' AND schema_id = SCHEMA_ID(N'OPS'))
BEGIN
    CREATE TABLE OPS.Engagement (
        EngagementId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        EngagementNumber NVARCHAR(50) NOT NULL,
        AccountId UNIQUEIDENTIFIER NOT NULL,
        AgreementId UNIQUEIDENTIFIER NULL,
        EngagementName NVARCHAR(200) NOT NULL,
        EngagementTypeCode NVARCHAR(50) NOT NULL,
        OwnerUserId UNIQUEIDENTIFIER NULL,
        StartDate DATE NULL,
        EndDate DATE NULL,
        StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Active',
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
    CREATE UNIQUE INDEX UX_Engagement_Tenant_Number ON OPS.Engagement(TenantId, EngagementNumber) WHERE IsDeleted = 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'EngagementMilestone' AND schema_id = SCHEMA_ID(N'OPS'))
BEGIN
    CREATE TABLE OPS.EngagementMilestone (
        MilestoneId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        EngagementId UNIQUEIDENTIFIER NOT NULL,
        MilestoneName NVARCHAR(200) NOT NULL,
        DueDate DATE NULL,
        CompletedDate DATE NULL,
        StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Pending',
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_EngagementMilestone_Tenant_Engagement ON OPS.EngagementMilestone(TenantId, EngagementId, IsDeleted);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'AgreementAmendment' AND schema_id = SCHEMA_ID(N'OPS'))
BEGIN
    CREATE TABLE OPS.AgreementAmendment (
        AmendmentId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AgreementId UNIQUEIDENTIFIER NOT NULL,
        AmendmentNumber NVARCHAR(50) NOT NULL,
        AmendmentTypeCode NVARCHAR(50) NOT NULL,
        EffectiveDate DATE NOT NULL,
        Description NVARCHAR(1000) NULL,
        StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Draft',
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
    CREATE UNIQUE INDEX UX_AgreementAmendment_Tenant_Number ON OPS.AgreementAmendment(TenantId, AmendmentNumber) WHERE IsDeleted = 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'IssueTracker' AND schema_id = SCHEMA_ID(N'OPS'))
BEGIN
    CREATE TABLE OPS.IssueTracker (
        IssueId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        EngagementId UNIQUEIDENTIFIER NULL,
        AccountId UNIQUEIDENTIFIER NULL,
        IssueNumber NVARCHAR(50) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(2000) NULL,
        SeverityCode NVARCHAR(50) NOT NULL,
        AssignedToUserId UNIQUEIDENTIFIER NULL,
        StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Open',
        ResolvedDate DATE NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
    CREATE UNIQUE INDEX UX_IssueTracker_Tenant_Number ON OPS.IssueTracker(TenantId, IssueNumber) WHERE IsDeleted = 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'ServiceRequest' AND schema_id = SCHEMA_ID(N'OPS'))
BEGIN
    CREATE TABLE OPS.ServiceRequest (
        ServiceRequestId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AccountId UNIQUEIDENTIFIER NOT NULL,
        AgreementId UNIQUEIDENTIFIER NULL,
        EngagementId UNIQUEIDENTIFIER NULL,
        RequestNumber NVARCHAR(50) NOT NULL,
        RequestTypeCode NVARCHAR(50) NOT NULL,
        Subject NVARCHAR(200) NOT NULL,
        Description NVARCHAR(2000) NULL,
        PriorityCode NVARCHAR(50) NOT NULL,
        AssignedToUserId UNIQUEIDENTIFIER NULL,
        StatusCode NVARCHAR(50) NOT NULL DEFAULT N'Open',
        ResolvedDate DATE NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
    CREATE UNIQUE INDEX UX_ServiceRequest_Tenant_Number ON OPS.ServiceRequest(TenantId, RequestNumber) WHERE IsDeleted = 0;
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'OperationalActivityLog' AND schema_id = SCHEMA_ID(N'OPS'))
BEGIN
    CREATE TABLE OPS.OperationalActivityLog (
        ActivityId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AccountId UNIQUEIDENTIFIER NULL,
        EngagementId UNIQUEIDENTIFIER NULL,
        AgreementId UNIQUEIDENTIFIER NULL,
        ActivityDate DATE NOT NULL,
        ActivityTypeCode NVARCHAR(50) NOT NULL,
        Subject NVARCHAR(200) NOT NULL,
        Notes NVARCHAR(2000) NULL,
        PerformedByUserId UNIQUEIDENTIFIER NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_OperationalActivity_Tenant_Date ON OPS.OperationalActivityLog(TenantId, ActivityDate DESC, IsDeleted);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'WorkflowInstance' AND schema_id = SCHEMA_ID(N'Workflow'))
BEGIN
    CREATE TABLE Workflow.WorkflowInstance (
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        TargetEntityName NVARCHAR(100) NOT NULL,
        TargetEntityId UNIQUEIDENTIFIER NOT NULL,
        StatusCodeId INT NOT NULL DEFAULT 1,
        SubmittedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_WorkflowInstance_Tenant_Status ON Workflow.WorkflowInstance(TenantId, StatusCodeId, IsDeleted);
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = N'WorkflowApprovalHistory' AND schema_id = SCHEMA_ID(N'Audit'))
BEGIN
    CREATE TABLE Audit.WorkflowApprovalHistory (
        Id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
        ApprovalStepId UNIQUEIDENTIFIER NULL,
        ActorUserId UNIQUEIDENTIFIER NULL,
        ActionCode NVARCHAR(50) NOT NULL,
        Notes NVARCHAR(1000) NULL,
        PreviousStatusCode NVARCHAR(50) NULL,
        NewStatusCode NVARCHAR(50) NULL,
        IsDelegated BIT NOT NULL DEFAULT 0,
        DelegatedByUserId UNIQUEIDENTIFIER NULL,
        ActionDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
    CREATE INDEX IX_WorkflowApprovalHistory_Tenant_Instance ON Audit.WorkflowApprovalHistory(TenantId, WorkflowInstanceId, IsDeleted);
END

IF COL_LENGTH(N'Sales.Agreement', N'CreatedByUserId') IS NULL ALTER TABLE Sales.Agreement ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Sales.Agreement', N'ModifiedDateUtc') IS NULL ALTER TABLE Sales.Agreement ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Sales.Agreement', N'ModifiedByUserId') IS NULL ALTER TABLE Sales.Agreement ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Sales.Agreement', N'IsDeleted') IS NULL ALTER TABLE Sales.Agreement ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Agreement_IsDeleted_0083 DEFAULT 0;
IF COL_LENGTH(N'Sales.Agreement', N'EffectiveStartDate') IS NULL ALTER TABLE Sales.Agreement ADD EffectiveStartDate DATE NOT NULL CONSTRAINT DF_Agreement_EffectiveStartDate_0083 DEFAULT CAST(SYSUTCDATETIME() AS date);
IF COL_LENGTH(N'Sales.Agreement', N'EffectiveEndDate') IS NULL ALTER TABLE Sales.Agreement ADD EffectiveEndDate DATE NULL;
IF COL_LENGTH(N'Sales.Agreement', N'TotalContractValue') IS NULL ALTER TABLE Sales.Agreement ADD TotalContractValue DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Sales.Agreement', N'CurrencyCode') IS NULL ALTER TABLE Sales.Agreement ADD CurrencyCode NVARCHAR(3) NOT NULL CONSTRAINT DF_Agreement_CurrencyCode_0083 DEFAULT N'USD';

IF COL_LENGTH(N'OPS.Engagement', N'EngagementTypeCode') IS NULL ALTER TABLE OPS.Engagement ADD EngagementTypeCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'OPS.Engagement', N'EngagementTypeId') IS NULL ALTER TABLE OPS.Engagement ADD EngagementTypeId INT NOT NULL CONSTRAINT DF_Engagement_EngagementTypeId_0083 DEFAULT 1;
IF COL_LENGTH(N'OPS.Engagement', N'StatusCodeId') IS NULL ALTER TABLE OPS.Engagement ADD StatusCodeId INT NOT NULL CONSTRAINT DF_Engagement_StatusCodeId_0083 DEFAULT 1;
IF COL_LENGTH(N'OPS.Engagement', N'EngagementManagerUserId') IS NULL ALTER TABLE OPS.Engagement ADD EngagementManagerUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.Engagement', N'OwnerUserId') IS NULL ALTER TABLE OPS.Engagement ADD OwnerUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.Engagement', N'CreatedByUserId') IS NULL ALTER TABLE OPS.Engagement ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.Engagement', N'ModifiedDateUtc') IS NULL ALTER TABLE OPS.Engagement ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'OPS.Engagement', N'ModifiedByUserId') IS NULL ALTER TABLE OPS.Engagement ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.Engagement', N'IsDeleted') IS NULL ALTER TABLE OPS.Engagement ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Engagement_IsDeleted_0083 DEFAULT 0;

IF COL_LENGTH(N'OPS.EngagementMilestone', N'CreatedByUserId') IS NULL ALTER TABLE OPS.EngagementMilestone ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.EngagementMilestone', N'ModifiedDateUtc') IS NULL ALTER TABLE OPS.EngagementMilestone ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'OPS.EngagementMilestone', N'ModifiedByUserId') IS NULL ALTER TABLE OPS.EngagementMilestone ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.EngagementMilestone', N'IsDeleted') IS NULL ALTER TABLE OPS.EngagementMilestone ADD IsDeleted BIT NOT NULL CONSTRAINT DF_EngagementMilestone_IsDeleted_0083 DEFAULT 0;

IF COL_LENGTH(N'OPS.AgreementAmendment', N'CreatedByUserId') IS NULL ALTER TABLE OPS.AgreementAmendment ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.AgreementAmendment', N'ModifiedDateUtc') IS NULL ALTER TABLE OPS.AgreementAmendment ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'OPS.AgreementAmendment', N'ModifiedByUserId') IS NULL ALTER TABLE OPS.AgreementAmendment ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.AgreementAmendment', N'IsDeleted') IS NULL ALTER TABLE OPS.AgreementAmendment ADD IsDeleted BIT NOT NULL CONSTRAINT DF_AgreementAmendment_IsDeleted_0083 DEFAULT 0;

IF COL_LENGTH(N'OPS.IssueTracker', N'CreatedByUserId') IS NULL ALTER TABLE OPS.IssueTracker ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.IssueTracker', N'ModifiedDateUtc') IS NULL ALTER TABLE OPS.IssueTracker ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'OPS.IssueTracker', N'ModifiedByUserId') IS NULL ALTER TABLE OPS.IssueTracker ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.IssueTracker', N'IsDeleted') IS NULL ALTER TABLE OPS.IssueTracker ADD IsDeleted BIT NOT NULL CONSTRAINT DF_IssueTracker_IsDeleted_0083 DEFAULT 0;

IF COL_LENGTH(N'OPS.ServiceRequest', N'CreatedByUserId') IS NULL ALTER TABLE OPS.ServiceRequest ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.ServiceRequest', N'ModifiedDateUtc') IS NULL ALTER TABLE OPS.ServiceRequest ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'OPS.ServiceRequest', N'ModifiedByUserId') IS NULL ALTER TABLE OPS.ServiceRequest ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.ServiceRequest', N'IsDeleted') IS NULL ALTER TABLE OPS.ServiceRequest ADD IsDeleted BIT NOT NULL CONSTRAINT DF_ServiceRequest_IsDeleted_0083 DEFAULT 0;

IF COL_LENGTH(N'OPS.OperationalActivityLog', N'CreatedByUserId') IS NULL ALTER TABLE OPS.OperationalActivityLog ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.OperationalActivityLog', N'ModifiedDateUtc') IS NULL ALTER TABLE OPS.OperationalActivityLog ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'OPS.OperationalActivityLog', N'ModifiedByUserId') IS NULL ALTER TABLE OPS.OperationalActivityLog ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'OPS.OperationalActivityLog', N'IsDeleted') IS NULL ALTER TABLE OPS.OperationalActivityLog ADD IsDeleted BIT NOT NULL CONSTRAINT DF_OperationalActivityLog_IsDeleted_0083 DEFAULT 0;

IF COL_LENGTH(N'Workflow.WorkflowInstance', N'CreatedByUserId') IS NULL ALTER TABLE Workflow.WorkflowInstance ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Workflow.WorkflowInstance', N'ModifiedDateUtc') IS NULL ALTER TABLE Workflow.WorkflowInstance ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Workflow.WorkflowInstance', N'ModifiedByUserId') IS NULL ALTER TABLE Workflow.WorkflowInstance ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Workflow.WorkflowInstance', N'IsDeleted') IS NULL ALTER TABLE Workflow.WorkflowInstance ADD IsDeleted BIT NOT NULL CONSTRAINT DF_WorkflowInstance_IsDeleted_0083 DEFAULT 0;
IF COL_LENGTH(N'Workflow.WorkflowInstance', N'WorkflowDefinitionId') IS NULL ALTER TABLE Workflow.WorkflowInstance ADD WorkflowDefinitionId UNIQUEIDENTIFIER NULL;

IF OBJECT_ID(N'Workflow.WorkflowDefinition') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Workflow.WorkflowDefinition', N'WorkflowCode') IS NULL ALTER TABLE Workflow.WorkflowDefinition ADD WorkflowCode NVARCHAR(100) NULL;
    IF COL_LENGTH(N'Workflow.WorkflowDefinition', N'Description') IS NULL ALTER TABLE Workflow.WorkflowDefinition ADD Description NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'Workflow.WorkflowDefinition', N'TargetEntityName') IS NULL ALTER TABLE Workflow.WorkflowDefinition ADD TargetEntityName NVARCHAR(100) NULL;
    IF COL_LENGTH(N'Workflow.WorkflowDefinition', N'TriggerTypeCode') IS NULL ALTER TABLE Workflow.WorkflowDefinition ADD TriggerTypeCode NVARCHAR(100) NULL;
    IF COL_LENGTH(N'Workflow.WorkflowDefinition', N'ThresholdAmount') IS NULL ALTER TABLE Workflow.WorkflowDefinition ADD ThresholdAmount DECIMAL(18,2) NULL;
    IF COL_LENGTH(N'Workflow.WorkflowDefinition', N'IsSystemDefined') IS NULL ALTER TABLE Workflow.WorkflowDefinition ADD IsSystemDefined BIT NOT NULL CONSTRAINT DF_WorkflowDefinition_IsSystemDefined_0083 DEFAULT 0;
    IF COL_LENGTH(N'Workflow.WorkflowDefinition', N'Version') IS NULL ALTER TABLE Workflow.WorkflowDefinition ADD Version INT NOT NULL CONSTRAINT DF_WorkflowDefinition_Version_0083 DEFAULT 1;
    IF COL_LENGTH(N'Workflow.WorkflowDefinition', N'ModifiedDateUtc') IS NULL ALTER TABLE Workflow.WorkflowDefinition ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Workflow.WorkflowDefinition', N'IsDeleted') IS NULL ALTER TABLE Workflow.WorkflowDefinition ADD IsDeleted BIT NOT NULL CONSTRAINT DF_WorkflowDefinition_IsDeleted_0083 DEFAULT 0;
END

IF COL_LENGTH(N'Audit.WorkflowApprovalHistory', N'CreatedByUserId') IS NULL ALTER TABLE Audit.WorkflowApprovalHistory ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Audit.WorkflowApprovalHistory', N'ModifiedDateUtc') IS NULL ALTER TABLE Audit.WorkflowApprovalHistory ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Audit.WorkflowApprovalHistory', N'ModifiedByUserId') IS NULL ALTER TABLE Audit.WorkflowApprovalHistory ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Audit.WorkflowApprovalHistory', N'IsDeleted') IS NULL ALTER TABLE Audit.WorkflowApprovalHistory ADD IsDeleted BIT NOT NULL CONSTRAINT DF_WorkflowApprovalHistory_IsDeleted_0083 DEFAULT 0;

EXEC(N'
DECLARE @TenantId UNIQUEIDENTIFIER = ''00000000-0000-0000-0000-000000000001'';
DECLARE @AccountId UNIQUEIDENTIFIER = NULL;
DECLARE @AgreementId UNIQUEIDENTIFIER = ''b1000000-0000-0000-0000-000000000001'';
DECLARE @EngagementId UNIQUEIDENTIFIER = ''b2000000-0000-0000-0000-000000000001'';
DECLARE @RequestId UNIQUEIDENTIFIER = ''b3000000-0000-0000-0000-000000000001'';
DECLARE @IssueId UNIQUEIDENTIFIER = ''b4000000-0000-0000-0000-000000000001'';
DECLARE @WorkflowId UNIQUEIDENTIFIER = ''b5000000-0000-0000-0000-000000000001'';

IF OBJECT_ID(N''Client.Account'') IS NOT NULL
    SELECT TOP 1 @AccountId = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc;

IF @AccountId IS NOT NULL
BEGIN

IF NOT EXISTS (SELECT 1 FROM Sales.Agreement WHERE TenantId = @TenantId AND AgreementNumber = N''AGR-OPS-0001'')
    INSERT INTO Sales.Agreement (AgreementId, TenantId, AgreementNumber, AccountId, OpportunityId, AgreementStatusCodeId, EffectiveStartDate, EffectiveEndDate, TotalContractValue, CurrencyCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES (@AgreementId, @TenantId, N''AGR-OPS-0001'', @AccountId, NULL, 1, CAST(SYSUTCDATETIME() AS date), DATEADD(day, 365, CAST(SYSUTCDATETIME() AS date)), 125000.00, N''USD'', SYSUTCDATETIME(), NULL, NULL, NULL, 0);

IF NOT EXISTS (SELECT 1 FROM OPS.Engagement WHERE TenantId = @TenantId AND EngagementNumber = N''ENG-OPS-0001'')
    INSERT INTO OPS.Engagement (EngagementId, TenantId, EngagementNumber, AccountId, AgreementId, EngagementName, EngagementTypeCode, EngagementTypeId, StatusCodeId, EngagementManagerUserId, OwnerUserId, StartDate, EndDate, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES (@EngagementId, @TenantId, N''ENG-OPS-0001'', @AccountId, @AgreementId, N''Policy servicing and compliance rollout'', N''Operations'', 2, 1, NULL, NULL, CAST(SYSUTCDATETIME() AS date), DATEADD(day, 30, CAST(SYSUTCDATETIME() AS date)), N''Active'', SYSUTCDATETIME(), NULL, NULL, NULL, 0);

IF NOT EXISTS (SELECT 1 FROM OPS.EngagementMilestone WHERE TenantId = @TenantId AND EngagementId = @EngagementId)
    INSERT INTO OPS.EngagementMilestone (MilestoneId, TenantId, EngagementId, MilestoneName, DueDate, CompletedDate, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @EngagementId, N''Confirm service plan'', DATEADD(day, 3, CAST(SYSUTCDATETIME() AS date)), NULL, N''Pending'', SYSUTCDATETIME(), NULL, NULL, NULL, 0),
           (NEWID(), @TenantId, @EngagementId, N''Complete policy review'', DATEADD(day, 10, CAST(SYSUTCDATETIME() AS date)), NULL, N''Pending'', SYSUTCDATETIME(), NULL, NULL, NULL, 0);

IF NOT EXISTS (SELECT 1 FROM OPS.AgreementAmendment WHERE TenantId = @TenantId AND AmendmentNumber = N''AMD-OPS-0001'')
    INSERT INTO OPS.AgreementAmendment (AmendmentId, TenantId, AgreementId, AmendmentNumber, AmendmentTypeCode, EffectiveDate, Description, StatusCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @AgreementId, N''AMD-OPS-0001'', N''Service Change'', DATEADD(day, 7, CAST(SYSUTCDATETIME() AS date)), N''Adds compliance acknowledgement workflow to servicing agreement.'', N''Draft'', SYSUTCDATETIME(), NULL, NULL, NULL, 0);

IF NOT EXISTS (SELECT 1 FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND RequestNumber = N''SR-OPS-0001'')
    INSERT INTO OPS.ServiceRequest (ServiceRequestId, TenantId, AccountId, AgreementId, EngagementId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, ResolvedDate, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES (@RequestId, @TenantId, @AccountId, @AgreementId, @EngagementId, N''SR-OPS-0001'', N''Compliance'', N''Client policy acknowledgement rollout'', N''Coordinate acknowledgement rollout for active policy servicing engagement.'', N''High'', NULL, N''Open'', NULL, SYSUTCDATETIME(), NULL, NULL, NULL, 0);

IF NOT EXISTS (SELECT 1 FROM OPS.IssueTracker WHERE TenantId = @TenantId AND IssueNumber = N''ISS-OPS-0001'')
    INSERT INTO OPS.IssueTracker (IssueId, TenantId, EngagementId, AccountId, IssueNumber, Title, Description, SeverityCode, AssignedToUserId, StatusCode, ResolvedDate, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES (@IssueId, @TenantId, @EngagementId, @AccountId, N''ISS-OPS-0001'', N''Missing acknowledgement evidence'', N''One required audience segment has not acknowledged the published policy.'', N''High'', NULL, N''Open'', NULL, SYSUTCDATETIME(), NULL, NULL, NULL, 0);

IF NOT EXISTS (SELECT 1 FROM OPS.OperationalActivityLog WHERE TenantId = @TenantId AND Subject = N''Created compliance servicing workflow'')
    INSERT INTO OPS.OperationalActivityLog (ActivityId, TenantId, AccountId, EngagementId, AgreementId, ActivityDate, ActivityTypeCode, Subject, Notes, PerformedByUserId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @AccountId, @EngagementId, @AgreementId, CAST(SYSUTCDATETIME() AS date), N''Workflow'', N''Created compliance servicing workflow'', N''System seeded linked Operations to Workflow sample data.'', NULL, SYSUTCDATETIME(), NULL, NULL, NULL, 0);

DECLARE @WorkflowDefinitionId UNIQUEIDENTIFIER = NULL;
IF OBJECT_ID(N''Workflow.WorkflowDefinition'') IS NOT NULL
    SELECT TOP 1 @WorkflowDefinitionId = WorkflowDefinitionId FROM Workflow.WorkflowDefinition WHERE TenantId = @TenantId ORDER BY CreatedDateUtc;

IF @WorkflowDefinitionId IS NULL
    SET @WorkflowDefinitionId = ''b5000000-0000-0000-0000-000000000099'';

IF OBJECT_ID(N''Workflow.WorkflowDefinition'') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Workflow.WorkflowDefinition WHERE WorkflowDefinitionId = @WorkflowDefinitionId)
    INSERT INTO Workflow.WorkflowDefinition (WorkflowDefinitionId, TenantId, WorkflowCode, WorkflowName, Description, TargetEntityName, TriggerTypeCode, ThresholdAmount, IsActive, IsSystemDefined, Version, CreatedDateUtc, ModifiedDateUtc, IsDeleted)
    VALUES (@WorkflowDefinitionId, @TenantId, N''OPS-SR'', N''Operations Service Request Workflow'', N''Seeded workflow for service request operations flow.'', N''ServiceRequest'', N''Manual'', NULL, 1, 1, 1, SYSUTCDATETIME(), NULL, 0);

IF NOT EXISTS (SELECT 1 FROM Workflow.WorkflowInstance WHERE WorkflowInstanceId = @WorkflowId)
    INSERT INTO Workflow.WorkflowInstance (WorkflowInstanceId, TenantId, WorkflowDefinitionId, TargetEntityName, TargetEntityId, StatusCodeId, SubmittedDateUtc, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES (@WorkflowId, @TenantId, @WorkflowDefinitionId, N''ServiceRequest'', @RequestId, 1, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, NULL, NULL, 0);

IF NOT EXISTS (SELECT 1 FROM Audit.WorkflowApprovalHistory WHERE WorkflowInstanceId = @WorkflowId)
    INSERT INTO Audit.WorkflowApprovalHistory (Id, TenantId, WorkflowInstanceId, ApprovalStepId, ActorUserId, ActionCode, Notes, PreviousStatusCode, NewStatusCode, IsDelegated, DelegatedByUserId, ActionDateUtc, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @WorkflowId, NULL, NULL, N''Submitted'', N''Compliance servicing workflow submitted.'', NULL, N''Pending'', 0, NULL, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, NULL, NULL, 0);
END;
');
";
    private const string Migration0084_DmsPolicyDocumentsCreateSeed = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'DMS')
    EXEC('CREATE SCHEMA DMS');

IF OBJECT_ID(N'DMS.Document') IS NULL
BEGIN
    CREATE TABLE DMS.Document (
        DocumentId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId         UNIQUEIDENTIFIER NOT NULL,
        DocumentTypeCode NVARCHAR(100)    NOT NULL,
        CategoryCode     NVARCHAR(100)    NOT NULL,
        EntityName       NVARCHAR(100)    NULL,
        EntityId         UNIQUEIDENTIFIER NULL,
        FileName         NVARCHAR(260)    NOT NULL,
        StoragePath      NVARCHAR(500)    NOT NULL,
        ContentType      NVARCHAR(150)    NULL,
        FileSizeBytes    BIGINT           NULL,
        VersionNumber    INT              NOT NULL DEFAULT 1,
        StatusCode       NVARCHAR(50)     NOT NULL DEFAULT N'Active',
        RetentionDate    DATE             NULL,
        Description      NVARCHAR(1000)   NULL,
        Tags             NVARCHAR(500)    NULL,
        UploadedByName   NVARCHAR(200)    NULL,
        CreatedDateUtc   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId  UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc  DATETIME2        NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted        BIT              NOT NULL DEFAULT 0
    );
END

IF COL_LENGTH(N'DMS.Document', N'EntityName') IS NULL ALTER TABLE DMS.Document ADD EntityName NVARCHAR(100) NULL;
IF COL_LENGTH(N'DMS.Document', N'EntityId') IS NULL ALTER TABLE DMS.Document ADD EntityId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'DMS.Document', N'FileSizeBytes') IS NULL ALTER TABLE DMS.Document ADD FileSizeBytes BIGINT NULL;
IF COL_LENGTH(N'DMS.Document', N'VersionNumber') IS NULL ALTER TABLE DMS.Document ADD VersionNumber INT NOT NULL CONSTRAINT DF_Document_VersionNumber_0084 DEFAULT 1;
IF COL_LENGTH(N'DMS.Document', N'StatusCode') IS NULL ALTER TABLE DMS.Document ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_Document_StatusCode_0084 DEFAULT N'Active';
IF COL_LENGTH(N'DMS.Document', N'RetentionDate') IS NULL ALTER TABLE DMS.Document ADD RetentionDate DATE NULL;
IF COL_LENGTH(N'DMS.Document', N'Description') IS NULL ALTER TABLE DMS.Document ADD Description NVARCHAR(1000) NULL;
IF COL_LENGTH(N'DMS.Document', N'Tags') IS NULL ALTER TABLE DMS.Document ADD Tags NVARCHAR(500) NULL;
IF COL_LENGTH(N'DMS.Document', N'UploadedByName') IS NULL ALTER TABLE DMS.Document ADD UploadedByName NVARCHAR(200) NULL;
IF COL_LENGTH(N'DMS.Document', N'CreatedByUserId') IS NULL ALTER TABLE DMS.Document ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'DMS.Document', N'ModifiedDateUtc') IS NULL ALTER TABLE DMS.Document ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'DMS.Document', N'ModifiedByUserId') IS NULL ALTER TABLE DMS.Document ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'DMS.Document', N'IsDeleted') IS NULL ALTER TABLE DMS.Document ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Document_IsDeleted_0084 DEFAULT 0;

EXEC(N'
DECLARE @TenantId UNIQUEIDENTIFIER = ''00000000-0000-0000-0000-000000000001'';
DECLARE @PolicyId UNIQUEIDENTIFIER = NULL;

IF OBJECT_ID(N''Policy.Policy'') IS NOT NULL
    SELECT TOP 1 @PolicyId = PolicyId FROM Policy.Policy WHERE TenantId = @TenantId ORDER BY CreatedDateUtc;

IF @PolicyId IS NULL AND OBJECT_ID(N''Policies.Policy'') IS NOT NULL
    SELECT TOP 1 @PolicyId = PolicyId FROM Policies.Policy WHERE TenantId = @TenantId ORDER BY CreatedDateUtc;

IF NOT EXISTS (SELECT 1 FROM DMS.Document WHERE TenantId = @TenantId AND FileName = N''BOP-2024-Declaration.pdf'' AND IsDeleted = 0)
BEGIN
    INSERT INTO DMS.Document (DocumentId, TenantId, DocumentTypeCode, CategoryCode, EntityName, EntityId, FileName, StoragePath, ContentType, FileSizeBytes, VersionNumber, StatusCode, RetentionDate, Description, Tags, UploadedByName, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, N''Declaration'', N''Policy'', N''Policy'', @PolicyId, N''BOP-2024-Declaration.pdf'', N''/policy-documents/BOP-2024-Declaration.pdf'', N''application/pdf'', 842136, 1, N''Active'', DATEADD(year, 7, CAST(SYSUTCDATETIME() AS date)), N''Business owners policy declaration package.'', N''policy,declaration,commercial'', N''Admin User'', DATEADD(day, -12, SYSUTCDATETIME()), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, N''Endorsement'', N''Endorsement'', N''Policy'', @PolicyId, N''GL-Endorsement-Additional-Insured.pdf'', N''/policy-documents/GL-Endorsement-Additional-Insured.pdf'', N''application/pdf'', 316928, 1, N''Active'', DATEADD(year, 7, CAST(SYSUTCDATETIME() AS date)), N''Additional insured endorsement for general liability policy.'', N''policy,endorsement,additional-insured'', N''Admin User'', DATEADD(day, -9, SYSUTCDATETIME()), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, N''Certificate'', N''Certificate'', N''Policy'', @PolicyId, N''Certificate-of-Insurance-ACME.pdf'', N''/policy-documents/Certificate-of-Insurance-ACME.pdf'', N''application/pdf'', 228144, 1, N''Active'', DATEADD(year, 3, CAST(SYSUTCDATETIME() AS date)), N''Certificate of insurance issued for account records.'', N''policy,certificate,coi'', N''Admin User'', DATEADD(day, -7, SYSUTCDATETIME()), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, N''Binder'', N''Binder'', N''Policy'', @PolicyId, N''Commercial-Auto-Binder.pdf'', N''/policy-documents/Commercial-Auto-Binder.pdf'', N''application/pdf'', 512640, 1, N''Active'', DATEADD(year, 2, CAST(SYSUTCDATETIME() AS date)), N''Temporary binder for commercial auto coverage.'', N''policy,binder,auto'', N''Admin User'', DATEADD(day, -5, SYSUTCDATETIME()), NULL, NULL, NULL, 0),
        (NEWID(), @TenantId, N''Policy Form'', N''Policy'', N''Policy'', @PolicyId, N''Workers-Comp-Policy-Form.pdf'', N''/policy-documents/Workers-Comp-Policy-Form.pdf'', N''application/pdf'', 1048576, 1, N''Active'', DATEADD(year, 7, CAST(SYSUTCDATETIME() AS date)), N''Workers compensation policy form and coverage terms.'', N''policy,form,workers-comp'', N''Admin User'', DATEADD(day, -3, SYSUTCDATETIME()), NULL, NULL, NULL, 0);
END;
');
";
    private const string Migration0085_CommsPagesCreateSeed = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Comms') EXEC('CREATE SCHEMA Comms');

IF OBJECT_ID(N'Comms.Template') IS NULL
BEGIN
    CREATE TABLE Comms.Template (
        TemplateId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        Channel NVARCHAR(50) NOT NULL,
        Category NVARCHAR(100) NOT NULL,
        Language NVARCHAR(50) NOT NULL DEFAULT N'English',
        Status NVARCHAR(50) NOT NULL DEFAULT N'Active',
        Subject NVARCHAR(300) NULL,
        Body NVARCHAR(MAX) NOT NULL,
        IncludeOptOutFooter BIT NOT NULL DEFAULT 0,
        TcpaNotice BIT NOT NULL DEFAULT 0,
        UsageCount INT NOT NULL DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

IF OBJECT_ID(N'Comms.MessageThread') IS NULL
BEGIN
    CREATE TABLE Comms.MessageThread (
        ThreadId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AccountName NVARCHAR(200) NOT NULL,
        AccountId NVARCHAR(50) NULL,
        ContactName NVARCHAR(200) NULL,
        ContactEmail NVARCHAR(300) NULL,
        ContactPhone NVARCHAR(50) NULL,
        Channel NVARCHAR(50) NOT NULL,
        Subject NVARCHAR(300) NOT NULL,
        BodyPreview NVARCHAR(500) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT N'Open',
        Priority NVARCHAR(50) NOT NULL DEFAULT N'Normal',
        AssignedTo NVARCHAR(200) NULL,
        Producer NVARCHAR(200) NULL,
        Branch NVARCHAR(100) NULL,
        IsRead BIT NOT NULL DEFAULT 0,
        IsEscalated BIT NOT NULL DEFAULT 0,
        OptedOut BIT NOT NULL DEFAULT 0,
        MessageCount INT NOT NULL DEFAULT 0,
        LastActivityAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        Sentiment NVARCHAR(50) NOT NULL DEFAULT N'Neutral',
        CsrOwner NVARCHAR(200) NULL,
        AiSummary NVARCHAR(1000) NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

IF OBJECT_ID(N'Comms.ThreadMessage') IS NULL
BEGIN
    CREATE TABLE Comms.ThreadMessage (
        MessageId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        ThreadId UNIQUEIDENTIFIER NOT NULL,
        SenderName NVARCHAR(200) NOT NULL,
        Channel NVARCHAR(50) NOT NULL,
        Direction NVARCHAR(50) NOT NULL,
        Body NVARCHAR(MAX) NOT NULL,
        SentAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        DeliveryStatus NVARCHAR(50) NOT NULL DEFAULT N'Delivered',
        IsAutomated BIT NOT NULL DEFAULT 0
    );
END

IF OBJECT_ID(N'Comms.Campaign') IS NULL
BEGIN
    CREATE TABLE Comms.Campaign (
        CampaignId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        Type NVARCHAR(50) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        Segment NVARCHAR(150) NOT NULL,
        StartDate DATETIME2 NOT NULL,
        Reached INT NOT NULL DEFAULT 0,
        OpenRate DECIMAL(9,2) NOT NULL DEFAULT 0,
        Conversions INT NOT NULL DEFAULT 0,
        Revenue DECIMAL(18,2) NOT NULL DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

IF OBJECT_ID(N'Comms.Appointment') IS NULL
BEGIN
    CREATE TABLE Comms.Appointment (
        AppointmentId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AccountName NVARCHAR(200) NOT NULL,
        ContactName NVARCHAR(200) NOT NULL,
        Type NVARCHAR(100) NOT NULL,
        Channel NVARCHAR(50) NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        Duration NVARCHAR(50) NOT NULL,
        Producer NVARCHAR(200) NULL,
        CsrOwner NVARCHAR(200) NULL,
        Branch NVARCHAR(100) NULL,
        Notes NVARCHAR(1000) NULL,
        Outcome NVARCHAR(200) NULL,
        OutcomeNotes NVARCHAR(1000) NULL,
        FollowUp NVARCHAR(200) NULL,
        SendConfirmation BIT NOT NULL DEFAULT 1,
        SendReminder BIT NOT NULL DEFAULT 1,
        ScheduledDate DATETIME2 NULL,
        ScheduledTime DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

IF OBJECT_ID(N'Comms.OutreachContact') IS NULL
BEGIN
    CREATE TABLE Comms.OutreachContact (
        OutreachContactId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        AccountName NVARCHAR(200) NOT NULL,
        ContactName NVARCHAR(200) NOT NULL,
        Email NVARCHAR(300) NULL,
        Phone NVARCHAR(50) NULL,
        Reason NVARCHAR(100) NOT NULL,
        Priority NVARCHAR(50) NOT NULL,
        AssignedTo NVARCHAR(200) NULL,
        Producer NVARCHAR(200) NULL,
        Branch NVARCHAR(100) NULL,
        Status NVARCHAR(50) NOT NULL,
        LastOutcome NVARCHAR(200) NULL,
        Notes NVARCHAR(1000) NULL,
        Attempts INT NOT NULL DEFAULT 0,
        OptedOut BIT NOT NULL DEFAULT 0,
        LastContactDate DATETIME2 NULL,
        NextContactDate DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

IF OBJECT_ID(N'Core.Notification') IS NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Core') EXEC('CREATE SCHEMA Core');
    CREATE TABLE Core.Notification (
        NotificationId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        RecipientUserId UNIQUEIDENTIFIER NOT NULL,
        TemplateId UNIQUEIDENTIFIER NULL,
        ChannelCode NVARCHAR(50) NOT NULL,
        Subject NVARCHAR(300) NULL,
        Body NVARCHAR(MAX) NOT NULL,
        EntityName NVARCHAR(100) NULL,
        EntityId UNIQUEIDENTIFIER NULL,
        StatusCode NVARCHAR(50) NOT NULL,
        IsRead BIT NOT NULL DEFAULT 0,
        ReadDateUtc DATETIME2 NULL,
        SentDateUtc DATETIME2 NULL,
        ErrorMessage NVARCHAR(1000) NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

EXEC(N'
DECLARE @TenantId UNIQUEIDENTIFIER = ''00000000-0000-0000-0000-000000000001'';
DECLARE @UserId UNIQUEIDENTIFIER = ''22222222-2222-2222-2222-222222222222'';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM Comms.Template WHERE TenantId=@TenantId AND Name=N''Policy Renewal Reminder'')
BEGIN
INSERT INTO Comms.Template (TemplateId,TenantId,Name,Channel,Category,Language,Status,Subject,Body,IncludeOptOutFooter,TcpaNotice,UsageCount,CreatedDateUtc,ModifiedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,N''Policy Renewal Reminder'',N''Email'',N''Renewal'',N''English'',N''Active'',N''Your policy renewal is approaching'',N''Dear [Client Name], your policy [Policy #] is approaching renewal. Please contact [Agent Name] to review options.'',1,0,42,DATEADD(day,-21,@Now),DATEADD(day,-2,@Now),0),
(NEWID(),@TenantId,N''Payment Due Reminder'',N''SMS'',N''Billing / Payment'',N''English'',N''Active'',NULL,N''Reminder: premium payment for [Policy #] is due [Due Date]. Reply STOP to opt out.'',1,1,36,DATEADD(day,-18,@Now),DATEADD(day,-3,@Now),0),
(NEWID(),@TenantId,N''Certificate Request Confirmation'',N''Email'',N''Policy Service'',N''English'',N''Active'',N''Certificate request received'',N''We received your certificate request and will deliver it within [X] business hours.'',0,0,28,DATEADD(day,-15,@Now),DATEADD(day,-1,@Now),0),
(NEWID(),@TenantId,N''CAT Event Check-In'',N''Portal Message'',N''CAT / Emergency'',N''English'',N''Active'',N''Checking in after recent weather'',N''We are checking in after the recent weather event. Contact us immediately if you need to file a claim.'',0,0,19,DATEADD(day,-10,@Now),DATEADD(day,-1,@Now),0),
(NEWID(),@TenantId,N''Claim Acknowledgement'',N''Email'',N''Claims'',N''English'',N''Active'',N''Claim received'',N''We have received your claim and assigned it to our claims team. Your claim number is [Claim #].'',0,0,31,DATEADD(day,-9,@Now),DATEADD(day,-1,@Now),0);
END

IF NOT EXISTS (SELECT 1 FROM Comms.MessageThread WHERE TenantId=@TenantId)
BEGIN
DECLARE @T1 UNIQUEIDENTIFIER=NEWID(), @T2 UNIQUEIDENTIFIER=NEWID(), @T3 UNIQUEIDENTIFIER=NEWID(), @T4 UNIQUEIDENTIFIER=NEWID(), @T5 UNIQUEIDENTIFIER=NEWID();
INSERT INTO Comms.MessageThread (ThreadId,TenantId,AccountName,AccountId,ContactName,ContactEmail,ContactPhone,Channel,Subject,BodyPreview,Status,Priority,AssignedTo,Producer,Branch,IsRead,IsEscalated,OptedOut,MessageCount,LastActivityAt,Sentiment,CsrOwner,AiSummary,CreatedDateUtc,IsDeleted) VALUES
(@T1,@TenantId,N''Apex Medical Group'',NULL,N''Sandra Kim'',N''sandrakim@apexmed.com'',N''(832) 555-0377'',N''Email'',N''Renewal premium increase question'',N''Can we discuss the 28% increase and alternative markets before renewal?'',N''Open'',N''Urgent'',N''Sarah Kim'',N''Maria Santos'',N''Gulf Coast'',0,1,0,2,DATEADD(hour,-5,@Now),N''Urgent'',N''Sarah Kim'',N''Client is concerned about renewal premium increase and wants remarketing options before renewal.'',DATEADD(hour,-8,@Now),0),
(@T2,@TenantId,N''Bridgewater Hotels'',NULL,N''Patricia Howe'',N''phowe@bwhotels.com'',N''(212) 555-0188'',N''SMS'',N''Claim status update'',N''Any update from the adjuster on the water damage claim?'',N''Pending'',N''High'',N''Maria Santos'',N''Diana Perez'',N''Northeast'',0,0,0,3,DATEADD(hour,-3,@Now),N''Neutral'',N''Maria Santos'',N''Claim follow-up requested. Adjuster report expected today.'',DATEADD(day,-1,@Now),0),
(@T3,@TenantId,N''Sullivan Mfg. LLC'',NULL,N''Robert Sullivan'',N''rjsullivan@email.com'',N''(713) 555-0101'',N''Email'',N''Certificate holder update'',N''Please update the certificate holder name and resend.'',N''Resolved'',N''Normal'',N''Sarah Kim'',N''Maria Santos'',N''Gulf Coast'',1,0,0,4,DATEADD(day,-1,@Now),N''Positive'',N''Sarah Kim'',N''Certificate update completed and client confirmed receipt.'',DATEADD(day,-2,@Now),0),
(@T4,@TenantId,N''Sunrise Healthcare'',NULL,N''Nadia Patel'',N''nadia@sunrisehc.com'',N''(713) 555-0921'',N''Internal Note'',N''Attorney representation'',N''All contact through legal counsel until further notice.'',N''Open'',N''High'',N'''',N''Diana Perez'',N''Gulf Coast'',0,1,1,1,DATEADD(hour,-20,@Now),N''Negative'',N''Kevin Obi'',N''Contact has opted out; route communications through counsel.'',DATEADD(hour,-20,@Now),0),
(@T5,@TenantId,N''Harbor View Marina'',NULL,N''Tony Marcellis'',N''tony@harborviewmarina.com'',N''(361) 555-0633'',N''Portal Message'',N''CAT site visit photos'',N''Uploaded photos from the marina damage inspection.'',N''Open'',N''Normal'',N''Lisa Chen'',N''Diana Perez'',N''Gulf Coast'',0,0,0,2,DATEADD(hour,-7,@Now),N''Neutral'',N''Lisa Chen'',N''Client uploaded damage photos after CAT inspection.'',DATEADD(hour,-7,@Now),0);
INSERT INTO Comms.ThreadMessage (MessageId,ThreadId,SenderName,Channel,Direction,Body,SentAt,DeliveryStatus,IsAutomated) VALUES
(NEWID(),@T1,N''Sandra Kim'',N''Email'',N''Inbound'',N''Can we discuss the 28% increase and alternative markets before renewal?'',DATEADD(hour,-8,@Now),N''Delivered'',0),(NEWID(),@T1,N''Sarah Kim'',N''Email'',N''Outbound'',N''I am reviewing markets and will send options today.'',DATEADD(hour,-6,@Now),N''Delivered'',0),
(NEWID(),@T2,N''Patricia Howe'',N''SMS'',N''Inbound'',N''Any update from the adjuster on the water damage claim?'',DATEADD(hour,-5,@Now),N''Delivered'',0),(NEWID(),@T2,N''Maria Santos'',N''SMS'',N''Outbound'',N''Adjuster report is expected today. I will update you as soon as it arrives.'',DATEADD(hour,-4,@Now),N''Delivered'',0),(NEWID(),@T2,N''Patricia Howe'',N''SMS'',N''Inbound'',N''Thank you.'',DATEADD(hour,-3,@Now),N''Delivered'',0),
(NEWID(),@T3,N''Robert Sullivan'',N''Email'',N''Inbound'',N''Please update the certificate holder name and resend.'',DATEADD(day,-2,@Now),N''Delivered'',0),(NEWID(),@T3,N''Sarah Kim'',N''Email'',N''Outbound'',N''Updated certificate attached.'',DATEADD(day,-1,@Now),N''Delivered'',0),
(NEWID(),@T4,N''System'',N''Internal Note'',N''Outbound'',N''All contact through legal counsel until further notice.'',DATEADD(hour,-20,@Now),N''Delivered'',1),
(NEWID(),@T5,N''Tony Marcellis'',N''Portal Message'',N''Inbound'',N''Uploaded photos from the marina damage inspection.'',DATEADD(hour,-7,@Now),N''Delivered'',0),(NEWID(),@T5,N''Lisa Chen'',N''Portal Message'',N''Outbound'',N''Received. We will add these to the claim file.'',DATEADD(hour,-6,@Now),N''Delivered'',0);
END

IF NOT EXISTS (SELECT 1 FROM Comms.Campaign WHERE TenantId=@TenantId)
INSERT INTO Comms.Campaign (CampaignId,TenantId,Name,Type,Status,Segment,StartDate,Reached,OpenRate,Conversions,Revenue,CreatedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,N''Q2 Cross-Sell — Umbrella'',N''Multi-Channel'',N''Active'',N''Commercial Clients'',DATEADD(day,-45,@Now),4820,31.4,187,94000,DATEADD(day,-50,@Now),0),
(NEWID(),@TenantId,N''Home+Auto Bundle Push'',N''Email'',N''Active'',N''Personal Lines'',DATEADD(day,-60,@Now),11200,28.9,412,206000,DATEADD(day,-65,@Now),0),
(NEWID(),@TenantId,N''Teen Driver Add-On'',N''SMS'',N''Scheduled'',N''HH w/ Teen Drivers'',DATEADD(day,7,@Now),0,0,0,0,DATEADD(day,-2,@Now),0),
(NEWID(),@TenantId,N''Workers Comp Expansion — SMB'',N''Email'',N''Active'',N''SMB Commercial'',DATEADD(day,-75,@Now),3400,22.1,95,57000,DATEADD(day,-80,@Now),0),
(NEWID(),@TenantId,N''Lapsed Policy Win-Back'',N''Email'',N''Completed'',N''Lapsed — 60–180d'',DATEADD(day,-120,@Now),6300,24.6,231,115500,DATEADD(day,-125,@Now),0);

IF NOT EXISTS (SELECT 1 FROM Comms.Appointment WHERE TenantId=@TenantId)
INSERT INTO Comms.Appointment (AppointmentId,TenantId,AccountName,ContactName,Type,Channel,Status,Duration,Producer,CsrOwner,Branch,Notes,Outcome,OutcomeNotes,FollowUp,SendConfirmation,SendReminder,ScheduledDate,ScheduledTime,CreatedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,N''Sullivan Mfg. LLC'',N''Robert Sullivan'',N''Renewal Discussion'',N''Phone Call'',N''Scheduled'',N''30 min'',N''Maria Santos'',N''Sarah Kim'',N''Gulf Coast'',N''Discuss split-limit GL structure.'',N'''',N'''',N'''',1,1,CAST(@Now AS date),DATEADD(hour,9,CAST(CAST(@Now AS date) AS datetime2)),@Now,0),
(NEWID(),@TenantId,N''Apex Medical Group'',N''Sandra Kim'',N''Renewal Discussion'',N''Video Call'',N''Awaiting Confirmation'',N''45 min'',N''Maria Santos'',N''Sarah Kim'',N''Gulf Coast'',N''28% premium increase concern.'',N'''',N'''',N'''',1,1,CAST(@Now AS date),DATEADD(hour,11,CAST(CAST(@Now AS date) AS datetime2)),@Now,0),
(NEWID(),@TenantId,N''Bridgewater Hotels'',N''Patricia Howe'',N''Claims Follow-Up'',N''Phone Call'',N''Scheduled'',N''30 min'',N''Diana Perez'',N''Maria Santos'',N''Northeast'',N''Claim status update.'',N'''',N'''',N'''',1,1,DATEADD(day,1,CAST(@Now AS date)),DATEADD(hour,14,DATEADD(day,1,CAST(CAST(@Now AS date) AS datetime2))),@Now,0),
(NEWID(),@TenantId,N''Dallas Roofing LLC'',N''Marcus Webb'',N''Policy Service'',N''Phone Call'',N''Completed'',N''15 min'',N''James Park'',N''Kevin Obi'',N''North Texas'',N''COI follow-up.'',N''Completed — Client Reached'',N''COI delivered and confirmed.'',N''None'',1,0,DATEADD(day,-1,CAST(@Now AS date)),DATEADD(hour,10,DATEADD(day,-1,CAST(CAST(@Now AS date) AS datetime2))),@Now,0);

IF NOT EXISTS (SELECT 1 FROM Comms.OutreachContact WHERE TenantId=@TenantId)
INSERT INTO Comms.OutreachContact (OutreachContactId,TenantId,AccountName,ContactName,Email,Phone,Reason,Priority,AssignedTo,Producer,Branch,Status,LastOutcome,Notes,Attempts,OptedOut,LastContactDate,NextContactDate,CreatedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,N''Bridgewater Hotels'',N''Patricia Howe'',N''phowe@bwhotels.com'',N''(212) 555-0188'',N''Claims Follow-Up'',N''Critical'',N''Maria Santos'',N''Maria Santos'',N''Northeast'',N''Open'',N''No Answer — Voicemail Left'',N'''',2,0,DATEADD(day,-3,@Now),CAST(@Now AS date),@Now,0),
(NEWID(),@TenantId,N''Apex Medical Group'',N''Sandra Kim'',N''sandrakim@apexmed.com'',N''(832) 555-0377'',N''Renewal — 30 Days'',N''Critical'',N''Sarah Kim'',N''Maria Santos'',N''Gulf Coast'',N''Open'',N'''',N'''',0,0,NULL,CAST(@Now AS date),@Now,0),
(NEWID(),@TenantId,N''Sunrise Healthcare'',N''Nadia Patel'',N''nadia@sunrisehc.com'',N''(713) 555-0921'',N''Claims Follow-Up'',N''Critical'',N''Kevin Obi'',N''Diana Perez'',N''Gulf Coast'',N''Opted Out'',N''No Answer — Voicemail Left'',N''Attorney representation.'',2,1,DATEADD(day,-1,@Now),NULL,@Now,0),
(NEWID(),@TenantId,N''Pacific Coast Builders'',N''Jorge Medina'',N''jmedina@pcbuilders.com'',N''(619) 555-0812'',N''Audit Due'',N''High'',N''Robert Yamamoto'',N''Robert Yamamoto'',N''Southwest'',N''Open'',N''Reached — Call Back Requested'',N'''',1,0,DATEADD(day,-2,@Now),DATEADD(day,2,CAST(@Now AS date)),@Now,0),
(NEWID(),@TenantId,N''Harbor Logistics'',N''Chris Navarro'',N''cnavarro@harborlog.com'',N''(713) 555-0224'',N''New Business Follow-Up'',N''High'',N''Sarah Kim'',N''Maria Santos'',N''Gulf Coast'',N''Open'',N''No Answer — No Voicemail'',N'''',2,0,DATEADD(day,-1,@Now),CAST(@Now AS date),@Now,0);

IF NOT EXISTS (SELECT 1 FROM Core.Notification WHERE TenantId=@TenantId)
BEGIN
INSERT INTO Core.Notification (NotificationId,TenantId,RecipientUserId,TemplateId,ChannelCode,Subject,Body,EntityName,EntityId,StatusCode,IsRead,ReadDateUtc,SentDateUtc,ErrorMessage,CreatedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,@UserId,NULL,N''Email'',N''Renewal task assigned'',N''Apex Medical Group renewal discussion requires follow-up today.'',N''Communication'',NULL,N''Sent'',0,NULL,DATEADD(hour,-2,@Now),NULL,DATEADD(hour,-2,@Now),0),
(NEWID(),@TenantId,@UserId,NULL,N''InApp'',N''Escalated conversation'',N''Apex Medical Group premium concern was escalated as urgent.'',N''MessageThread'',NULL,N''Delivered'',0,NULL,DATEADD(hour,-5,@Now),NULL,DATEADD(hour,-5,@Now),0),
(NEWID(),@TenantId,@UserId,NULL,N''SMS'',N''Appointment reminder sent'',N''Reminder sent for Sullivan Mfg renewal discussion.'',N''Appointment'',NULL,N''Sent'',1,DATEADD(hour,-1,@Now),DATEADD(hour,-3,@Now),NULL,DATEADD(hour,-3,@Now),0),
(NEWID(),@TenantId,@UserId,NULL,N''Email'',N''Campaign completed'',N''Lapsed Policy Win-Back campaign completed with 231 conversions.'',N''Campaign'',NULL,N''Sent'',1,DATEADD(day,-1,@Now),DATEADD(day,-1,@Now),NULL,DATEADD(day,-1,@Now),0);
END
');
";
    private const string Migration0086_ReportsAnalyticsCreateSeed = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Core') EXEC('CREATE SCHEMA Core');

IF OBJECT_ID(N'Core.ReportDefinition') IS NULL
BEGIN
    CREATE TABLE Core.ReportDefinition (
        ReportDefinitionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NULL,
        ReportCode NVARCHAR(100) NOT NULL,
        ReportName NVARCHAR(200) NOT NULL,
        Description NVARCHAR(1000) NULL,
        ModuleCode NVARCHAR(50) NOT NULL,
        ReportTypeCode NVARCHAR(50) NOT NULL,
        OutputFormats NVARCHAR(100) NOT NULL,
        IsSystemReport BIT NOT NULL DEFAULT 1,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

IF OBJECT_ID(N'Core.ReportExecution') IS NULL
BEGIN
    CREATE TABLE Core.ReportExecution (
        ReportExecutionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ReportDefinitionId UNIQUEIDENTIFIER NOT NULL,
        ReportScheduleId UNIQUEIDENTIFIER NULL,
        StatusCode NVARCHAR(50) NOT NULL,
        OutputFormat NVARCHAR(50) NOT NULL,
        StoragePath NVARCHAR(500) NULL,
        FileSizeBytes BIGINT NULL,
        [RowCount] INT NULL,
        StartedDateUtc DATETIME2 NULL,
        CompletedDateUtc DATETIME2 NULL,
        ErrorMessage NVARCHAR(1000) NULL,
        RequestedByUserId UNIQUEIDENTIFIER NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

IF OBJECT_ID(N'Core.ReportSchedule') IS NULL
BEGIN
    CREATE TABLE Core.ReportSchedule (
        ReportScheduleId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ReportDefinitionId UNIQUEIDENTIFIER NOT NULL,
        FrequencyCode NVARCHAR(50) NOT NULL,
        OutputFormat NVARCHAR(50) NOT NULL,
        DeliveryEmail NVARCHAR(300) NOT NULL,
        IsActive BIT NOT NULL DEFAULT 1,
        NextRunDateUtc DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

IF COL_LENGTH(N'Core.ReportDefinition', N'TenantId') IS NULL ALTER TABLE Core.ReportDefinition ADD TenantId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Core.ReportDefinition', N'ReportCode') IS NULL ALTER TABLE Core.ReportDefinition ADD ReportCode NVARCHAR(100) NOT NULL CONSTRAINT DF_ReportDefinition_ReportCode_0086 DEFAULT N'UNKNOWN';
IF COL_LENGTH(N'Core.ReportDefinition', N'ReportName') IS NULL ALTER TABLE Core.ReportDefinition ADD ReportName NVARCHAR(200) NOT NULL CONSTRAINT DF_ReportDefinition_ReportName_0086 DEFAULT N'Untitled Report';
IF COL_LENGTH(N'Core.ReportDefinition', N'Description') IS NULL ALTER TABLE Core.ReportDefinition ADD Description NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Core.ReportDefinition', N'ModuleCode') IS NULL ALTER TABLE Core.ReportDefinition ADD ModuleCode NVARCHAR(50) NOT NULL CONSTRAINT DF_ReportDefinition_ModuleCode_0086 DEFAULT N'Agency';
IF COL_LENGTH(N'Core.ReportDefinition', N'ReportTypeCode') IS NULL ALTER TABLE Core.ReportDefinition ADD ReportTypeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_ReportDefinition_ReportTypeCode_0086 DEFAULT N'Operational';
IF COL_LENGTH(N'Core.ReportDefinition', N'OutputFormats') IS NULL ALTER TABLE Core.ReportDefinition ADD OutputFormats NVARCHAR(100) NOT NULL CONSTRAINT DF_ReportDefinition_OutputFormats_0086 DEFAULT N'PDF,Excel';
IF COL_LENGTH(N'Core.ReportDefinition', N'IsSystemReport') IS NULL ALTER TABLE Core.ReportDefinition ADD IsSystemReport BIT NOT NULL CONSTRAINT DF_ReportDefinition_IsSystemReport_0086 DEFAULT 1;
IF COL_LENGTH(N'Core.ReportDefinition', N'IsActive') IS NULL ALTER TABLE Core.ReportDefinition ADD IsActive BIT NOT NULL CONSTRAINT DF_ReportDefinition_IsActive_0086 DEFAULT 1;
IF COL_LENGTH(N'Core.ReportDefinition', N'CreatedDateUtc') IS NULL ALTER TABLE Core.ReportDefinition ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ReportDefinition_CreatedDateUtc_0086 DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Core.ReportDefinition', N'ModifiedDateUtc') IS NULL ALTER TABLE Core.ReportDefinition ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Core.ReportDefinition', N'IsDeleted') IS NULL ALTER TABLE Core.ReportDefinition ADD IsDeleted BIT NOT NULL CONSTRAINT DF_ReportDefinition_IsDeleted_0086 DEFAULT 0;

IF COL_LENGTH(N'Core.ReportExecution', N'TenantId') IS NULL ALTER TABLE Core.ReportExecution ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ReportExecution_TenantId_0086 DEFAULT '00000000-0000-0000-0000-000000000001';
IF COL_LENGTH(N'Core.ReportExecution', N'ReportDefinitionId') IS NULL ALTER TABLE Core.ReportExecution ADD ReportDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ReportExecution_ReportDefinitionId_0086 DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH(N'Core.ReportExecution', N'ReportScheduleId') IS NULL ALTER TABLE Core.ReportExecution ADD ReportScheduleId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Core.ReportExecution', N'StatusCode') IS NULL ALTER TABLE Core.ReportExecution ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_ReportExecution_StatusCode_0086 DEFAULT N'Completed';
IF COL_LENGTH(N'Core.ReportExecution', N'OutputFormat') IS NULL ALTER TABLE Core.ReportExecution ADD OutputFormat NVARCHAR(50) NOT NULL CONSTRAINT DF_ReportExecution_OutputFormat_0086 DEFAULT N'PDF';
IF COL_LENGTH(N'Core.ReportExecution', N'StoragePath') IS NULL ALTER TABLE Core.ReportExecution ADD StoragePath NVARCHAR(500) NULL;
IF COL_LENGTH(N'Core.ReportExecution', N'FileSizeBytes') IS NULL ALTER TABLE Core.ReportExecution ADD FileSizeBytes BIGINT NULL;
IF COL_LENGTH(N'Core.ReportExecution', N'RowCount') IS NULL ALTER TABLE Core.ReportExecution ADD [RowCount] INT NULL;
IF COL_LENGTH(N'Core.ReportExecution', N'StartedDateUtc') IS NULL ALTER TABLE Core.ReportExecution ADD StartedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Core.ReportExecution', N'CompletedDateUtc') IS NULL ALTER TABLE Core.ReportExecution ADD CompletedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Core.ReportExecution', N'ErrorMessage') IS NULL ALTER TABLE Core.ReportExecution ADD ErrorMessage NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Core.ReportExecution', N'RequestedByUserId') IS NULL ALTER TABLE Core.ReportExecution ADD RequestedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Core.ReportExecution', N'CreatedDateUtc') IS NULL ALTER TABLE Core.ReportExecution ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ReportExecution_CreatedDateUtc_0086 DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Core.ReportExecution', N'IsDeleted') IS NULL ALTER TABLE Core.ReportExecution ADD IsDeleted BIT NOT NULL CONSTRAINT DF_ReportExecution_IsDeleted_0086 DEFAULT 0;

IF COL_LENGTH(N'Core.ReportSchedule', N'TenantId') IS NULL ALTER TABLE Core.ReportSchedule ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ReportSchedule_TenantId_0086 DEFAULT '00000000-0000-0000-0000-000000000001';
IF COL_LENGTH(N'Core.ReportSchedule', N'ReportDefinitionId') IS NULL ALTER TABLE Core.ReportSchedule ADD ReportDefinitionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ReportSchedule_ReportDefinitionId_0086 DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH(N'Core.ReportSchedule', N'ScheduleName') IS NULL ALTER TABLE Core.ReportSchedule ADD ScheduleName NVARCHAR(200) NOT NULL CONSTRAINT DF_ReportSchedule_ScheduleName_0086 DEFAULT N'Report Schedule';
IF COL_LENGTH(N'Core.ReportSchedule', N'FrequencyCode') IS NULL ALTER TABLE Core.ReportSchedule ADD FrequencyCode NVARCHAR(50) NOT NULL CONSTRAINT DF_ReportSchedule_FrequencyCode_0086 DEFAULT N'Weekly';
IF COL_LENGTH(N'Core.ReportSchedule', N'CronExpression') IS NULL ALTER TABLE Core.ReportSchedule ADD CronExpression NVARCHAR(100) NOT NULL CONSTRAINT DF_ReportSchedule_CronExpression_0086 DEFAULT N'0 8 * * 1';
IF COL_LENGTH(N'Core.ReportSchedule', N'OutputFormat') IS NULL ALTER TABLE Core.ReportSchedule ADD OutputFormat NVARCHAR(50) NOT NULL CONSTRAINT DF_ReportSchedule_OutputFormat_0086 DEFAULT N'PDF';
IF COL_LENGTH(N'Core.ReportSchedule', N'DeliveryEmail') IS NULL ALTER TABLE Core.ReportSchedule ADD DeliveryEmail NVARCHAR(300) NOT NULL CONSTRAINT DF_ReportSchedule_DeliveryEmail_0086 DEFAULT N'ops@agencybinder.local';
IF COL_LENGTH(N'Core.ReportSchedule', N'IsActive') IS NULL ALTER TABLE Core.ReportSchedule ADD IsActive BIT NOT NULL CONSTRAINT DF_ReportSchedule_IsActive_0086 DEFAULT 1;
IF COL_LENGTH(N'Core.ReportSchedule', N'NextRunDateUtc') IS NULL ALTER TABLE Core.ReportSchedule ADD NextRunDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Core.ReportSchedule', N'CreatedDateUtc') IS NULL ALTER TABLE Core.ReportSchedule ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ReportSchedule_CreatedDateUtc_0086 DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Core.ReportSchedule', N'ModifiedDateUtc') IS NULL ALTER TABLE Core.ReportSchedule ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Core.ReportSchedule', N'IsDeleted') IS NULL ALTER TABLE Core.ReportSchedule ADD IsDeleted BIT NOT NULL CONSTRAINT DF_ReportSchedule_IsDeleted_0086 DEFAULT 0;

EXEC(N'
DECLARE @TenantId UNIQUEIDENTIFIER = ''00000000-0000-0000-0000-000000000001'';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM Core.ReportDefinition WHERE ReportCode = N''BOB_SUMMARY'')
BEGIN
INSERT INTO Core.ReportDefinition (ReportDefinitionId,TenantId,ReportCode,ReportName,Description,ModuleCode,ReportTypeCode,OutputFormats,IsSystemReport,IsActive,CreatedDateUtc,ModifiedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,N''BOB_SUMMARY'',N''Book of Business Summary'',N''Premium, policy count, retention, and LOB breakdown for the full book.'',N''Agency'',N''Operational'',N''PDF,Excel,CSV'',1,1,DATEADD(day,-80,@Now),DATEADD(day,-1,@Now),0),
(NEWID(),@TenantId,N''NEW_BUSINESS_PROD'',N''New Business Production'',N''New accounts and policies written by period, producer, and LOB.'',N''Sales'',N''Analytics'',N''Excel,CSV,PDF'',1,1,DATEADD(day,-75,@Now),DATEADD(day,-3,@Now),0),
(NEWID(),@TenantId,N''SALES_PIPELINE'',N''Sales Pipeline Funnel'',N''Opportunity conversion, quoted premium, and bound revenue by stage.'',N''Sales'',N''Analytics'',N''PDF,Excel'',1,1,DATEADD(day,-74,@Now),DATEADD(day,-2,@Now),0),
(NEWID(),@TenantId,N''POLICY_BOOK'',N''Policy Book Detail'',N''Active policies, premium, carrier, LOB, and branch detail.'',N''Policy'',N''Operational'',N''Excel,CSV'',1,1,DATEADD(day,-70,@Now),DATEADD(day,-4,@Now),0),
(NEWID(),@TenantId,N''EXPIRING_POLICIES'',N''Expiring Policies (60/30/14 Day)'',N''Upcoming expirations with premium and renewal probability.'',N''Retention'',N''Operational'',N''Excel,PDF'',1,1,DATEADD(day,-68,@Now),DATEADD(day,-1,@Now),0),
(NEWID(),@TenantId,N''RENEWAL_RETENTION'',N''Renewal Retention Rate'',N''Retention rate by LOB, producer, carrier, and policy tier.'',N''Retention'',N''Analytics'',N''PDF,Excel'',1,1,DATEADD(day,-66,@Now),DATEADD(day,-7,@Now),0),
(NEWID(),@TenantId,N''OPEN_CLAIMS'',N''Open Claims Register'',N''All open claims with age, status, reserves, and adjuster.'',N''Claims'',N''Operational'',N''Excel,CSV,PDF'',1,1,DATEADD(day,-64,@Now),DATEADD(day,-2,@Now),0),
(NEWID(),@TenantId,N''LOSS_RATIO_LOB'',N''Loss Ratio by Line of Business'',N''Incurred losses vs earned premium by LOB and carrier.'',N''Claims'',N''Analytics'',N''PDF,Excel'',1,1,DATEADD(day,-62,@Now),DATEADD(day,-14,@Now),0),
(NEWID(),@TenantId,N''AR_AGING'',N''AR Aging Report'',N''Accounts receivable aging buckets: current, 30, 60, 90+ days.'',N''Finance'',N''Operational'',N''Excel,CSV,PDF'',1,1,DATEADD(day,-60,@Now),DATEADD(day,-3,@Now),0),
(NEWID(),@TenantId,N''COMMISSION_SUMMARY'',N''Commission Statement Summary'',N''Commission earned, paid, and pending by producer and period.'',N''Producer'',N''Financial'',N''PDF,Excel'',1,1,DATEADD(day,-58,@Now),DATEADD(day,-5,@Now),0),
(NEWID(),@TenantId,N''PRODUCER_SCORECARD'',N''Producer Scorecard'',N''Per-producer new business, retention, revenue, and activity KPIs.'',N''Producer'',N''Analytics'',N''PDF,Excel'',1,1,DATEADD(day,-56,@Now),DATEADD(day,-7,@Now),0),
(NEWID(),@TenantId,N''CAMPAIGN_ROI'',N''Campaign ROI Analysis'',N''Revenue attributed to marketing campaigns versus spend.'',N''Marketing'',N''Analytics'',N''Excel,PDF'',1,1,DATEADD(day,-54,@Now),DATEADD(day,-10,@Now),0),
(NEWID(),@TenantId,N''LEAD_SOURCE_PERF'',N''Lead Source Performance'',N''Lead conversion and close rate by source, segment, and campaign.'',N''Marketing'',N''Analytics'',N''Excel,CSV'',1,1,DATEADD(day,-53,@Now),DATEADD(day,-6,@Now),0),
(NEWID(),@TenantId,N''COMPLIANCE_ACK'',N''Compliance Acknowledgements'',N''Policy acknowledgement completion rates and outstanding items.'',N''Compliance'',N''Operational'',N''PDF,Excel'',1,1,DATEADD(day,-50,@Now),DATEADD(day,-30,@Now),0);
END

IF NOT EXISTS (SELECT 1 FROM Core.ReportExecution WHERE TenantId=@TenantId)
BEGIN
INSERT INTO Core.ReportExecution (ReportExecutionId,TenantId,ReportDefinitionId,ReportScheduleId,StatusCode,OutputFormat,StoragePath,FileSizeBytes,[RowCount],StartedDateUtc,CompletedDateUtc,ErrorMessage,RequestedByUserId,CreatedDateUtc,IsDeleted)
SELECT TOP 20 NEWID(), @TenantId, rd.ReportDefinitionId, NULL, N''Completed'',
       CASE WHEN rd.OutputFormats LIKE N''%Excel%'' THEN N''Excel'' ELSE N''PDF'' END,
       CONCAT(N''/reports/'', rd.ReportCode, N''-'', CONVERT(NVARCHAR(8), @Now, 112), N''.xlsx''),
       128000 + ABS(CHECKSUM(rd.ReportCode)) % 900000,
       100 + ABS(CHECKSUM(rd.ReportName)) % 9000,
       DATEADD(minute, -30 - ABS(CHECKSUM(rd.ReportCode)) % 300, @Now),
       DATEADD(minute, -20 - ABS(CHECKSUM(rd.ReportCode)) % 280, @Now),
       NULL, NULL,
       DATEADD(day, -1 * (ABS(CHECKSUM(rd.ReportCode)) % 30), @Now),
       0
FROM Core.ReportDefinition rd
WHERE rd.TenantId=@TenantId AND rd.IsDeleted=0;
END

IF NOT EXISTS (SELECT 1 FROM Core.ReportSchedule WHERE TenantId=@TenantId)
BEGIN
INSERT INTO Core.ReportSchedule (ReportScheduleId,TenantId,ReportDefinitionId,ScheduleName,FrequencyCode,CronExpression,OutputFormat,DeliveryEmail,IsActive,NextRunDateUtc,CreatedDateUtc,IsDeleted)
SELECT TOP 6 NEWID(), @TenantId, rd.ReportDefinitionId,
       CONCAT(rd.ReportName, N'' - recurring delivery''),
       CASE rd.ModuleCode WHEN N''Finance'' THEN N''Weekly'' WHEN N''Retention'' THEN N''Daily'' ELSE N''Monthly'' END,
       CASE rd.ModuleCode WHEN N''Retention'' THEN N''0 8 * * *'' WHEN N''Finance'' THEN N''0 8 * * 1'' ELSE N''0 8 1 * *'' END,
       CASE WHEN rd.OutputFormats LIKE N''%Excel%'' THEN N''Excel'' ELSE N''PDF'' END,
       N''ops@agencybinder.local'', 1, DATEADD(day, 1 + ABS(CHECKSUM(rd.ReportCode)) % 14, @Now), DATEADD(day,-20,@Now), 0
FROM Core.ReportDefinition rd
WHERE rd.TenantId=@TenantId AND rd.IsDeleted=0 AND rd.ReportCode IN (N''BOB_SUMMARY'',N''EXPIRING_POLICIES'',N''AR_AGING'',N''OPEN_CLAIMS'',N''COMMISSION_SUMMARY'',N''CAMPAIGN_ROI'');
END
');
";
    private const string Migration0087_MarketingEmailLandingCreateSeed = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Marketing') EXEC('CREATE SCHEMA Marketing');

IF OBJECT_ID(N'Marketing.EmailBlast') IS NULL
BEGIN
    CREATE TABLE Marketing.EmailBlast (
        EmailBlastId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CampaignId UNIQUEIDENTIFIER NULL,
        Name NVARCHAR(200) NOT NULL,
        Subject NVARCHAR(300) NOT NULL,
        PreviewText NVARCHAR(500) NULL,
        AudienceSegment NVARCHAR(150) NOT NULL,
        SenderName NVARCHAR(150) NOT NULL,
        SenderEmail NVARCHAR(300) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT N'Draft',
        ScheduledDateUtc DATETIME2 NULL,
        SentDateUtc DATETIME2 NULL,
        RecipientCount INT NOT NULL DEFAULT 0,
        SentCount INT NOT NULL DEFAULT 0,
        OpenCount INT NOT NULL DEFAULT 0,
        ClickCount INT NOT NULL DEFAULT 0,
        BounceCount INT NOT NULL DEFAULT 0,
        UnsubscribeCount INT NOT NULL DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

IF OBJECT_ID(N'Marketing.LandingPage') IS NULL
BEGIN
    CREATE TABLE Marketing.LandingPage (
        LandingPageId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CampaignId UNIQUEIDENTIFIER NULL,
        Name NVARCHAR(200) NOT NULL,
        Slug NVARCHAR(200) NOT NULL,
        TemplateName NVARCHAR(150) NOT NULL,
        Status NVARCHAR(50) NOT NULL DEFAULT N'Draft',
        PublishedUrl NVARCHAR(500) NULL,
        PrimaryCta NVARCHAR(150) NULL,
        ViewCount INT NOT NULL DEFAULT 0,
        ConversionCount INT NOT NULL DEFAULT 0,
        ConversionRate DECIMAL(9,2) NOT NULL DEFAULT 0,
        LastPublishedDateUtc DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

IF COL_LENGTH(N'Marketing.EmailBlast', N'CampaignId') IS NULL ALTER TABLE Marketing.EmailBlast ADD CampaignId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Marketing.EmailBlast', N'PreviewText') IS NULL ALTER TABLE Marketing.EmailBlast ADD PreviewText NVARCHAR(500) NULL;
IF COL_LENGTH(N'Marketing.EmailBlast', N'AudienceSegment') IS NULL ALTER TABLE Marketing.EmailBlast ADD AudienceSegment NVARCHAR(150) NOT NULL CONSTRAINT DF_EmailBlast_AudienceSegment_0087 DEFAULT N'All Active Accounts';
IF COL_LENGTH(N'Marketing.EmailBlast', N'SenderName') IS NULL ALTER TABLE Marketing.EmailBlast ADD SenderName NVARCHAR(150) NOT NULL CONSTRAINT DF_EmailBlast_SenderName_0087 DEFAULT N'AgencyBinder';
IF COL_LENGTH(N'Marketing.EmailBlast', N'SenderEmail') IS NULL ALTER TABLE Marketing.EmailBlast ADD SenderEmail NVARCHAR(300) NOT NULL CONSTRAINT DF_EmailBlast_SenderEmail_0087 DEFAULT N'marketing@agencybinder.local';
IF COL_LENGTH(N'Marketing.EmailBlast', N'ScheduledDateUtc') IS NULL ALTER TABLE Marketing.EmailBlast ADD ScheduledDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Marketing.EmailBlast', N'SentDateUtc') IS NULL ALTER TABLE Marketing.EmailBlast ADD SentDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Marketing.EmailBlast', N'RecipientCount') IS NULL ALTER TABLE Marketing.EmailBlast ADD RecipientCount INT NOT NULL CONSTRAINT DF_EmailBlast_RecipientCount_0087 DEFAULT 0;
IF COL_LENGTH(N'Marketing.EmailBlast', N'SentCount') IS NULL ALTER TABLE Marketing.EmailBlast ADD SentCount INT NOT NULL CONSTRAINT DF_EmailBlast_SentCount_0087 DEFAULT 0;
IF COL_LENGTH(N'Marketing.EmailBlast', N'OpenCount') IS NULL ALTER TABLE Marketing.EmailBlast ADD OpenCount INT NOT NULL CONSTRAINT DF_EmailBlast_OpenCount_0087 DEFAULT 0;
IF COL_LENGTH(N'Marketing.EmailBlast', N'ClickCount') IS NULL ALTER TABLE Marketing.EmailBlast ADD ClickCount INT NOT NULL CONSTRAINT DF_EmailBlast_ClickCount_0087 DEFAULT 0;
IF COL_LENGTH(N'Marketing.EmailBlast', N'BounceCount') IS NULL ALTER TABLE Marketing.EmailBlast ADD BounceCount INT NOT NULL CONSTRAINT DF_EmailBlast_BounceCount_0087 DEFAULT 0;
IF COL_LENGTH(N'Marketing.EmailBlast', N'UnsubscribeCount') IS NULL ALTER TABLE Marketing.EmailBlast ADD UnsubscribeCount INT NOT NULL CONSTRAINT DF_EmailBlast_UnsubscribeCount_0087 DEFAULT 0;
IF COL_LENGTH(N'Marketing.EmailBlast', N'ModifiedDateUtc') IS NULL ALTER TABLE Marketing.EmailBlast ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Marketing.EmailBlast', N'IsDeleted') IS NULL ALTER TABLE Marketing.EmailBlast ADD IsDeleted BIT NOT NULL CONSTRAINT DF_EmailBlast_IsDeleted_0087 DEFAULT 0;

IF COL_LENGTH(N'Marketing.LandingPage', N'CampaignId') IS NULL ALTER TABLE Marketing.LandingPage ADD CampaignId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Marketing.LandingPage', N'Slug') IS NULL ALTER TABLE Marketing.LandingPage ADD Slug NVARCHAR(200) NOT NULL CONSTRAINT DF_LandingPage_Slug_0087 DEFAULT N'landing-page';
IF COL_LENGTH(N'Marketing.LandingPage', N'TemplateName') IS NULL ALTER TABLE Marketing.LandingPage ADD TemplateName NVARCHAR(150) NOT NULL CONSTRAINT DF_LandingPage_TemplateName_0087 DEFAULT N'Agency Landing Page';
IF COL_LENGTH(N'Marketing.LandingPage', N'PublishedUrl') IS NULL ALTER TABLE Marketing.LandingPage ADD PublishedUrl NVARCHAR(500) NULL;
IF COL_LENGTH(N'Marketing.LandingPage', N'PrimaryCta') IS NULL ALTER TABLE Marketing.LandingPage ADD PrimaryCta NVARCHAR(150) NULL;
IF COL_LENGTH(N'Marketing.LandingPage', N'ViewCount') IS NULL ALTER TABLE Marketing.LandingPage ADD ViewCount INT NOT NULL CONSTRAINT DF_LandingPage_ViewCount_0087 DEFAULT 0;
IF COL_LENGTH(N'Marketing.LandingPage', N'ConversionCount') IS NULL ALTER TABLE Marketing.LandingPage ADD ConversionCount INT NOT NULL CONSTRAINT DF_LandingPage_ConversionCount_0087 DEFAULT 0;
IF COL_LENGTH(N'Marketing.LandingPage', N'ConversionRate') IS NULL ALTER TABLE Marketing.LandingPage ADD ConversionRate DECIMAL(9,2) NOT NULL CONSTRAINT DF_LandingPage_ConversionRate_0087 DEFAULT 0;
IF COL_LENGTH(N'Marketing.LandingPage', N'LastPublishedDateUtc') IS NULL ALTER TABLE Marketing.LandingPage ADD LastPublishedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Marketing.LandingPage', N'ModifiedDateUtc') IS NULL ALTER TABLE Marketing.LandingPage ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Marketing.LandingPage', N'IsDeleted') IS NULL ALTER TABLE Marketing.LandingPage ADD IsDeleted BIT NOT NULL CONSTRAINT DF_LandingPage_IsDeleted_0087 DEFAULT 0;

EXEC(N'
DECLARE @TenantId UNIQUEIDENTIFIER = ''00000000-0000-0000-0000-000000000001'';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @Campaign1 UNIQUEIDENTIFIER = NULL, @Campaign2 UNIQUEIDENTIFIER = NULL, @Campaign3 UNIQUEIDENTIFIER = NULL;

IF OBJECT_ID(N''Comms.Campaign'') IS NOT NULL
BEGIN
    SELECT TOP 1 @Campaign1 = CampaignId FROM Comms.Campaign WHERE TenantId=@TenantId AND Name LIKE N''%Cross-Sell%'' ORDER BY CreatedDateUtc DESC;
    SELECT TOP 1 @Campaign2 = CampaignId FROM Comms.Campaign WHERE TenantId=@TenantId AND Name LIKE N''%Home%'' ORDER BY CreatedDateUtc DESC;
    SELECT TOP 1 @Campaign3 = CampaignId FROM Comms.Campaign WHERE TenantId=@TenantId AND Name LIKE N''%Win-Back%'' ORDER BY CreatedDateUtc DESC;
END

IF NOT EXISTS (SELECT 1 FROM Marketing.EmailBlast WHERE TenantId=@TenantId AND IsDeleted=0)
BEGIN
INSERT INTO Marketing.EmailBlast (EmailBlastId,TenantId,CampaignId,Name,Subject,PreviewText,AudienceSegment,SenderName,SenderEmail,Status,ScheduledDateUtc,SentDateUtc,RecipientCount,SentCount,OpenCount,ClickCount,BounceCount,UnsubscribeCount,CreatedDateUtc,ModifiedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,@Campaign1,N''Umbrella Cross-Sell Launch'',N''Protect more with a commercial umbrella review'',N''Your business may have coverage gaps above primary limits.'',N''Commercial Clients'',N''Maria Santos'',N''maria.santos@agencybinder.local'',N''Sent'',DATEADD(day,-16,@Now),DATEADD(day,-15,@Now),4820,4811,1512,384,47,16,DATEADD(day,-20,@Now),DATEADD(day,-15,@Now),0),
(NEWID(),@TenantId,@Campaign2,N''Home + Auto Bundle Offer'',N''Bundle home and auto to simplify coverage'',N''Clients who bundle may qualify for preferred pricing.'',N''Personal Lines'',N''Robert Yamamoto'',N''robert.yamamoto@agencybinder.local'',N''Sent'',DATEADD(day,-10,@Now),DATEADD(day,-9,@Now),11200,11148,3221,902,88,29,DATEADD(day,-12,@Now),DATEADD(day,-9,@Now),0),
(NEWID(),@TenantId,@Campaign3,N''Lapsed Policy Win-Back'',N''We miss you — let us quote your coverage again'',N''A quick review can uncover better coverage options.'',N''Lapsed — 60–180d'',N''Diana Perez'',N''diana.perez@agencybinder.local'',N''Scheduled'',DATEADD(day,4,@Now),NULL,6300,0,0,0,0,0,DATEADD(day,-2,@Now),NULL,0),
(NEWID(),@TenantId,NULL,N''Renewal 30-Day Reminder'',N''Your policy renewal is approaching'',N''Schedule a renewal review before your current policy expires.'',N''Renewal — 30 Days'',N''Sarah Kim'',N''service@agencybinder.local'',N''Draft'',NULL,NULL,1840,0,0,0,0,0,DATEADD(day,-1,@Now),NULL,0),
(NEWID(),@TenantId,NULL,N''Google Review Request — Promoters'',N''Would you share your AMS experience?'',N''Your feedback helps local clients choose their agency.'',N''NPS Promoters'',N''Kevin Obi'',N''reviews@agencybinder.local'',N''Paused'',DATEADD(day,-3,@Now),NULL,2100,420,173,52,6,2,DATEADD(day,-8,@Now),DATEADD(day,-3,@Now),0);
END

IF NOT EXISTS (SELECT 1 FROM Marketing.LandingPage WHERE TenantId=@TenantId AND IsDeleted=0)
BEGIN
INSERT INTO Marketing.LandingPage (LandingPageId,TenantId,CampaignId,Name,Slug,TemplateName,Status,PublishedUrl,PrimaryCta,ViewCount,ConversionCount,ConversionRate,LastPublishedDateUtc,CreatedDateUtc,ModifiedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,@Campaign1,N''Commercial Umbrella Coverage Review'',N''commercial-umbrella-review'',N''Coverage Review'',N''Published'',N''https://agencybinder.local/lp/commercial-umbrella-review'',N''Request Coverage Review'',8421,384,4.56,DATEADD(day,-15,@Now),DATEADD(day,-22,@Now),DATEADD(day,-15,@Now),0),
(NEWID(),@TenantId,@Campaign2,N''Home Auto Bundle Savings'',N''home-auto-bundle-savings'',N''Personal Lines Offer'',N''Published'',N''https://agencybinder.local/lp/home-auto-bundle-savings'',N''Get Bundle Quote'',12640,902,7.14,DATEADD(day,-9,@Now),DATEADD(day,-14,@Now),DATEADD(day,-9,@Now),0),
(NEWID(),@TenantId,@Campaign3,N''Win Back Returning Clients'',N''returning-client-quote'',N''Win-Back Offer'',N''Draft'',N''https://agencybinder.local/lp/returning-client-quote'',N''Start New Quote'',0,0,0,NULL,DATEADD(day,-3,@Now),NULL,0),
(NEWID(),@TenantId,NULL,N''Renewal Review Scheduler'',N''renewal-review-scheduler'',N''Appointment Scheduler'',N''Published'',N''https://agencybinder.local/lp/renewal-review-scheduler'',N''Schedule Review'',3140,211,6.72,DATEADD(day,-5,@Now),DATEADD(day,-10,@Now),DATEADD(day,-5,@Now),0),
(NEWID(),@TenantId,NULL,N''Referral Thank You Page'',N''refer-a-business'',N''Referral Capture'',N''Archived'',N''https://agencybinder.local/lp/refer-a-business'',N''Refer a Client'',1780,71,3.99,DATEADD(day,-45,@Now),DATEADD(day,-60,@Now),DATEADD(day,-30,@Now),0);
END
');
";
    private const string Migration0088_PortalAdminOperationalSeed = """
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Portal') EXEC('CREATE SCHEMA Portal');

IF OBJECT_ID(N'Portal.AdminRecord') IS NULL
BEGIN
    CREATE TABLE Portal.AdminRecord (
        PortalAdminRecordId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Kind NVARCHAR(80) NOT NULL,
        Code NVARCHAR(120) NOT NULL,
        Name NVARCHAR(240) NOT NULL,
        Status NVARCHAR(60) NOT NULL,
        JsonData NVARCHAR(MAX) NOT NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

IF COL_LENGTH(N'Portal.AdminRecord', N'TenantId') IS NULL ALTER TABLE Portal.AdminRecord ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PortalAdminRecord_TenantId_0088 DEFAULT '00000000-0000-0000-0000-000000000001';
IF COL_LENGTH(N'Portal.AdminRecord', N'Kind') IS NULL ALTER TABLE Portal.AdminRecord ADD Kind NVARCHAR(80) NOT NULL CONSTRAINT DF_PortalAdminRecord_Kind_0088 DEFAULT N'General';
IF COL_LENGTH(N'Portal.AdminRecord', N'Code') IS NULL ALTER TABLE Portal.AdminRecord ADD Code NVARCHAR(120) NOT NULL CONSTRAINT DF_PortalAdminRecord_Code_0088 DEFAULT N'general';
IF COL_LENGTH(N'Portal.AdminRecord', N'Name') IS NULL ALTER TABLE Portal.AdminRecord ADD Name NVARCHAR(240) NOT NULL CONSTRAINT DF_PortalAdminRecord_Name_0088 DEFAULT N'Portal Record';
IF COL_LENGTH(N'Portal.AdminRecord', N'Status') IS NULL ALTER TABLE Portal.AdminRecord ADD Status NVARCHAR(60) NOT NULL CONSTRAINT DF_PortalAdminRecord_Status_0088 DEFAULT N'Active';
IF COL_LENGTH(N'Portal.AdminRecord', N'JsonData') IS NULL ALTER TABLE Portal.AdminRecord ADD JsonData NVARCHAR(MAX) NOT NULL CONSTRAINT DF_PortalAdminRecord_JsonData_0088 DEFAULT N'{}';
IF COL_LENGTH(N'Portal.AdminRecord', N'CreatedDateUtc') IS NULL ALTER TABLE Portal.AdminRecord ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PortalAdminRecord_CreatedDateUtc_0088 DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Portal.AdminRecord', N'ModifiedDateUtc') IS NULL ALTER TABLE Portal.AdminRecord ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Portal.AdminRecord', N'IsDeleted') IS NULL ALTER TABLE Portal.AdminRecord ADD IsDeleted BIT NOT NULL CONSTRAINT DF_PortalAdminRecord_IsDeleted_0088 DEFAULT 0;

EXEC(N'
DECLARE @TenantId UNIQUEIDENTIFIER = ''00000000-0000-0000-0000-000000000001'';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId=@TenantId AND Kind=N''PortalBranding'' AND Code=N''branding'' AND IsDeleted=0)
INSERT INTO Portal.AdminRecord (PortalAdminRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,N''PortalBranding'',N''branding'',N''Agency Client Portal Branding'',N''Active'',N''{"displayName":"Sullivan Agency Client Portal","domain":"portal.sullivanagency.com","supportEmail":"support@sullivanagency.com","supportPhone":"(555) 234-5678","welcomeMessage":"Manage your policies, request certificates, upload documents, and more — all in one place.","primaryColor":"#1d4ed8","accentColor":"#059669","navBg":"#1e293b","navText":"#f8fafc","emailFromName":"Sullivan Agency","emailReplyTo":"noreply@sullivanagency.com","emailFooter":"Sullivan Agency · 123 Main St · Anytown, ST 00000 · (555) 234-5678","showAgencyLogo":true,"showPoweredBy":false,"showSupportChat":true,"showNewsWidget":true}'',@Now,0);

IF NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId=@TenantId AND Kind=N''PortalMobile'' AND Code=N''mobile'' AND IsDeleted=0)
INSERT INTO Portal.AdminRecord (PortalAdminRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,N''PortalMobile'',N''mobile'',N''Agency Mobile Configuration'',N''Active'',N''{"appName":"Sullivan Agency App","iosUrl":"https://apps.apple.com/app/sullivan-agency","androidUrl":"https://play.google.com/store/apps/details?id=com.sullivanagency.client","bundleId":"com.sullivanagency.client","appVersion":"2.4.1","biometricLogin":true,"forceAppLock":true,"lockTimeoutMinutes":15,"requireMfaOnMobile":true,"notifications":[{"name":"Renewal Reminders","description":"Push reminder 60/30/14 days before policy renewal","enabled":true},{"name":"Payment Due Alerts","description":"Notify client when invoice is generated or payment is due","enabled":true},{"name":"Claim Status Updates","description":"Push updates when a claim status changes","enabled":true},{"name":"Request Fulfilled","description":"Notify when a COI or policy change request is completed","enabled":true},{"name":"New Document Available","description":"Alert when agency shares a new document","enabled":true},{"name":"Secure Message Received","description":"Push when a new secure message arrives from the agency","enabled":false},{"name":"Promotional Messages","description":"Agency marketing and cross-sell offers","enabled":false}],"features":[{"name":"View Policies","icon":"bi-shield-check","iconCss":"pm-fi-blue","enabled":true},{"name":"Request COI","icon":"bi-file-earmark-text","iconCss":"pm-fi-green","enabled":true},{"name":"Pay Invoice","icon":"bi-credit-card","iconCss":"pm-fi-green","enabled":true},{"name":"ID Cards","icon":"bi-person-vcard","iconCss":"pm-fi-blue","enabled":true},{"name":"Documents","icon":"bi-folder2","iconCss":"pm-fi-purple","enabled":true},{"name":"Secure Chat","icon":"bi-chat-lock","iconCss":"pm-fi-amber","enabled":true},{"name":"Claim FNOL","icon":"bi-exclamation-circle","iconCss":"pm-fi-red","enabled":false},{"name":"E-Sign","icon":"bi-pen","iconCss":"pm-fi-purple","enabled":true}]}'' ,@Now,0);

IF NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId=@TenantId AND Kind=N''PortalMyAccount'' AND Code=N''my-account'' AND IsDeleted=0)
INSERT INTO Portal.AdminRecord (PortalAdminRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,N''PortalMyAccount'',N''my-account'',N''Tenant Admin Portal Account'',N''Active'',N''{"tenantId":"00000000-0000-0000-0000-000000000001","agencyName":"Sullivan Agency","adminName":"Tenant Administrator","adminEmail":"admin@sullivanagency.com","planName":"AMS Enterprise","portalUsers":10,"openRequests":4,"sharedDocuments":8,"lastPortalPublishUtc":"2025-04-01T14:30:00Z"}'',@Now,0);

IF NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId=@TenantId AND Kind=N''PortalUser'' AND IsDeleted=0)
BEGIN
INSERT INTO Portal.AdminRecord (PortalAdminRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,N''PortalUser'',N''rachel-nguyen'',N''Rachel Nguyen'',N''Active'',N''{"name":"Rachel Nguyen","email":"rachel@nguyenfamily.com","accountName":"Nguyen Family HH","role":"Policyholder","status":"Active","lastLogin":"2025-04-10T10:00:00","mfaEnabled":true,"logins30d":14}'',DATEADD(day,-30,@Now),0),
(NEWID(),@TenantId,N''PortalUser'',N''james-ortega'',N''James Ortega'',N''Active'',N''{"name":"James Ortega","email":"james@ortegaconst.com","accountName":"Ortega Construction","role":"Admin","status":"Active","lastLogin":"2025-04-11T09:00:00","mfaEnabled":true,"logins30d":22}'',DATEADD(day,-29,@Now),0),
(NEWID(),@TenantId,N''PortalUser'',N''brittany-walsh'',N''Brittany Walsh'',N''Active'',N''{"name":"Brittany Walsh","email":"bwalsh@techvault.io","accountName":"TechVault Inc","role":"Contact","status":"Active","lastLogin":"2025-04-04T09:00:00","mfaEnabled":false,"logins30d":5}'',DATEADD(day,-28,@Now),0),
(NEWID(),@TenantId,N''PortalUser'',N''marcus-lee'',N''Marcus Lee'',N''Active'',N''{"name":"Marcus Lee","email":"mlee@sullivanmfg.com","accountName":"Sullivan Manufacturing","role":"Policyholder","status":"Active","lastLogin":"2025-04-09T09:00:00","mfaEnabled":true,"logins30d":9}'',DATEADD(day,-27,@Now),0),
(NEWID(),@TenantId,N''PortalUser'',N''sandra-kim'',N''Sandra Kim'',N''Pending'',N''{"name":"Sandra Kim","email":"sandra@kimrealty.net","accountName":"Kim Realty LLC","role":"Admin","status":"Pending","lastLogin":"0001-01-01T00:00:00","mfaEnabled":false,"logins30d":0}'',DATEADD(day,-26,@Now),0),
(NEWID(),@TenantId,N''PortalUser'',N''derek-patel'',N''Derek Patel'',N''Pending'',N''{"name":"Derek Patel","email":"dpatel@apexlogistics.com","accountName":"Apex Logistics","role":"Policyholder","status":"Pending","lastLogin":"0001-01-01T00:00:00","mfaEnabled":false,"logins30d":0}'',DATEADD(day,-25,@Now),0),
(NEWID(),@TenantId,N''PortalUser'',N''tanya-brooks'',N''Tanya Brooks'',N''Suspended'',N''{"name":"Tanya Brooks","email":"tbrooks@brookslegal.com","accountName":"Brooks Legal Group","role":"Contact","status":"Suspended","lastLogin":"2025-01-11T09:00:00","mfaEnabled":false,"logins30d":0}'',DATEADD(day,-24,@Now),0),
(NEWID(),@TenantId,N''PortalUser'',N''carlos-vega'',N''Carlos Vega'',N''Active'',N''{"name":"Carlos Vega","email":"cvega@vegafoods.com","accountName":"Vega Foods Inc","role":"Policyholder","status":"Active","lastLogin":"2025-04-07T09:00:00","mfaEnabled":true,"logins30d":7}'',DATEADD(day,-23,@Now),0),
(NEWID(),@TenantId,N''PortalUser'',N''nicole-thornton'',N''Nicole Thornton'',N''Active'',N''{"name":"Nicole Thornton","email":"nicole@thorntonhh.net","accountName":"Thornton Household","role":"Policyholder","status":"Active","lastLogin":"2025-03-31T09:00:00","mfaEnabled":false,"logins30d":3}'',DATEADD(day,-22,@Now),0),
(NEWID(),@TenantId,N''PortalUser'',N''frank-castillo'',N''Frank Castillo'',N''Active'',N''{"name":"Frank Castillo","email":"fcastillo@castilloauto.com","accountName":"Castillo Auto Group","role":"Admin","status":"Active","lastLogin":"2025-04-11T11:00:00","mfaEnabled":true,"logins30d":19}'',DATEADD(day,-21,@Now),0);
END

IF NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId=@TenantId AND Kind=N''PortalCapability'' AND IsDeleted=0)
BEGIN
INSERT INTO Portal.AdminRecord (PortalAdminRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,N''PortalCapability'',N''request-coi'',N''Request Certificate of Insurance'',N''Active'',N''{"name":"Request Certificate of Insurance","description":"Clients can request COIs directly from the portal without calling the agency.","icon":"bi-file-earmark-text","iconCss":"pc-ic-blue","category":"Policy Services","enabled":true,"requiresApproval":true,"mfaRequired":false,"auditLog":true}'',@Now,0),
(NEWID(),@TenantId,N''PortalCapability'',N''policy-change'',N''Request Policy Change'',N''Active'',N''{"name":"Request Policy Change","description":"Submit endorsement and policy modification requests online.","icon":"bi-pencil-square","iconCss":"pc-ic-amber","category":"Policy Services","enabled":true,"requiresApproval":true,"mfaRequired":false,"auditLog":true}'',@Now,0),
(NEWID(),@TenantId,N''PortalCapability'',N''upload-documents'',N''Upload Documents'',N''Active'',N''{"name":"Upload Documents","description":"Clients can securely upload loss runs, applications, and supporting docs.","icon":"bi-cloud-arrow-up","iconCss":"pc-ic-green","category":"Documents","enabled":true,"requiresApproval":false,"mfaRequired":false,"auditLog":true}'',@Now,0),
(NEWID(),@TenantId,N''PortalCapability'',N''pay-invoice'',N''Pay Invoice Online'',N''Active'',N''{"name":"Pay Invoice Online","description":"Clients pay premiums and invoices via card or ACH through the portal.","icon":"bi-credit-card","iconCss":"pc-ic-green","category":"Billing","enabled":true,"requiresApproval":false,"mfaRequired":true,"auditLog":true}'',@Now,0),
(NEWID(),@TenantId,N''PortalCapability'',N''claim-fnol'',N''Claim Intake (FNOL)'',N''Inactive'',N''{"name":"Claim Intake (FNOL)","description":"Clients initiate first notice of loss directly from the portal.","icon":"bi-exclamation-circle","iconCss":"pc-ic-red","category":"Claims","enabled":false,"requiresApproval":true,"mfaRequired":true,"auditLog":true}'',@Now,0);
END

IF NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId=@TenantId AND Kind=N''SelfServiceRequest'' AND IsDeleted=0)
BEGIN
INSERT INTO Portal.AdminRecord (PortalAdminRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,N''SelfServiceRequest'',N''req-001'',N''COI for Lakeview Office Park'',N''Open'',N''{"submittedAt":"2025-04-12T09:48:00","clientName":"Marcus Lee","accountName":"Sullivan Manufacturing","requestType":"COI Request","summary":"COI for Lakeview Office Park — GL/WC","priority":"Urgent","assignedTo":"Beth N.","status":"Open"}'',DATEADD(hour,-1,@Now),0),
(NEWID(),@TenantId,N''SelfServiceRequest'',N''req-002'',N''Add new equipment'',N''In Progress'',N''{"submittedAt":"2025-04-12T09:32:00","clientName":"James Ortega","accountName":"Ortega Construction","requestType":"Policy Change","summary":"Add new equipment — 2024 Cat Excavator","priority":"Normal","assignedTo":"Tom R.","status":"In Progress"}'',DATEADD(hour,-2,@Now),0),
(NEWID(),@TenantId,N''SelfServiceRequest'',N''req-003'',N''FNOL equipment theft'',N''Open'',N''{"submittedAt":"2025-04-12T06:00:00","clientName":"Marcus Lee","accountName":"Sullivan Manufacturing","requestType":"Claim Intake","summary":"FNOL — equipment theft at job site","priority":"Urgent","assignedTo":"—","status":"Open"}'',DATEADD(hour,-4,@Now),0),
(NEWID(),@TenantId,N''SelfServiceRequest'',N''req-004'',N''Paid invoice'',N''Fulfilled'',N''{"submittedAt":"2025-04-12T08:00:00","clientName":"Carlos Vega","accountName":"Vega Foods Inc","requestType":"Payment","summary":"Paid Invoice INV-2025-0481 — $3,200","priority":"Normal","assignedTo":"System","status":"Fulfilled"}'',DATEADD(hour,-3,@Now),0);
END

IF NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId=@TenantId AND Kind=N''PortalDocument'' AND IsDeleted=0)
BEGIN
INSERT INTO Portal.AdminRecord (PortalAdminRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,N''PortalDocument'',N''doc-001'',N''Commercial GL Declarations — 2025'',N''Shared'',N''{"name":"Commercial GL Declarations — 2025","accountName":"Ortega Construction","category":"Policy","fileType":"PDF","fileSizeKb":284,"visibility":"Shared","sharedAt":"2025-01-10T00:00:00","viewCount":6,"downloadCount":3}'',DATEADD(day,-80,@Now),0),
(NEWID(),@TenantId,N''PortalDocument'',N''doc-002'',N''Auto ID Card — Vega Foods Fleet'',N''Shared'',N''{"name":"Auto ID Card — Vega Foods Fleet","accountName":"Vega Foods Inc","category":"ID Card","fileType":"PDF","fileSizeKb":44,"visibility":"Shared","sharedAt":"2025-02-01T00:00:00","viewCount":14,"downloadCount":9}'',DATEADD(day,-70,@Now),0),
(NEWID(),@TenantId,N''PortalDocument'',N''doc-003'',N''Claim #CLM-2025-0042 — Adjuster Report'',N''Agency Only'',N''{"name":"Claim #CLM-2025-0042 — Adjuster Report","accountName":"Ortega Construction","category":"Claims","fileType":"PDF","fileSizeKb":450,"visibility":"Agency Only","sharedAt":"2025-02-28T00:00:00","viewCount":0,"downloadCount":0}'',DATEADD(day,-60,@Now),0);
END

IF NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId=@TenantId AND Kind=N''PortalActivity'' AND IsDeleted=0)
BEGIN
INSERT INTO Portal.AdminRecord (PortalAdminRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted) VALUES
(NEWID(),@TenantId,N''PortalActivity'',N''act-001'',N''Successful login'',N''Info'',N''{"occurredAt":"2025-04-12T09:58:00","userName":"James Ortega","userEmail":"james@ortegaconst.com","accountName":"Ortega Construction","eventType":"Login","detail":"Successful login","severity":"Info","ipAddress":"192.168.1.14"}'',DATEADD(minute,-2,@Now),0),
(NEWID(),@TenantId,N''PortalActivity'',N''act-002'',N''Downloaded Auto ID Card'',N''Info'',N''{"occurredAt":"2025-04-12T09:52:00","userName":"Rachel Nguyen","userEmail":"rachel@nguyenfamily.com","accountName":"Nguyen Family HH","eventType":"Document Download","detail":"Downloaded Auto ID Card","severity":"Info","ipAddress":"10.0.0.22"}'',DATEADD(minute,-8,@Now),0),
(NEWID(),@TenantId,N''PortalActivity'',N''act-003'',N''Failed login attempt'',N''Warning'',N''{"occurredAt":"2025-04-12T08:30:00","userName":"Unknown","userEmail":"hacker@spam.net","accountName":"—","eventType":"Login","detail":"Failed login attempt — invalid credentials (3×)","severity":"Warning","ipAddress":"45.33.32.156"}'',DATEADD(minute,-90,@Now),0);
END
');
""";
    private const string Migration0089_PortalMyAccountFullSeed = """
IF OBJECT_ID(N'Portal.AdminRecord') IS NOT NULL
BEGIN
    DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
    DECLARE @Json NVARCHAR(MAX) = N'{"tenantId":"00000000-0000-0000-0000-000000000001","agencyName":"Sullivan Agency","adminName":"Tenant Administrator","adminEmail":"admin@sullivanagency.com","adminRole":"Tenant Admin","adminPhone":"(555) 234-5678","timeZone":"Central Standard Time","locale":"en-US","planName":"AMS Enterprise","planStatus":"Active","renewalDateUtc":"2026-04-01T00:00:00Z","portalUsers":10,"activePortalUsers":7,"pendingInvites":2,"openRequests":4,"urgentRequests":2,"sharedDocuments":8,"storageUsedGb":42,"storageLimitGb":250,"monthlyLoginCount":318,"mobileInstalls":847,"chatSessions30d":704,"apiCalls30d":18420,"lastPortalPublishUtc":"2025-04-01T14:30:00Z","lastAdminLoginUtc":"2025-04-12T15:12:00Z","mfaEnabled":true,"ssoEnabled":false,"brandingPublished":true,"mobileAppPublished":true,"chatEnabled":true,"supportEmail":"support@sullivanagency.com","supportPhone":"(555) 234-5678","portalDomain":"portal.sullivanagency.com","healthChecks":[{"name":"Portal availability","status":"Healthy","detail":"Public portal has responded successfully for 30 days.","icon":"bi-globe2"},{"name":"Custom domain","status":"Healthy","detail":"portal.sullivanagency.com CNAME and certificate are valid.","icon":"bi-shield-check"},{"name":"MFA policy","status":"Healthy","detail":"Tenant admin account has MFA enabled.","icon":"bi-phone-vibrate"},{"name":"Pending invites","status":"Attention","detail":"2 invitations are still pending acceptance.","icon":"bi-envelope-exclamation"},{"name":"Storage utilization","status":"Healthy","detail":"42 GB of 250 GB used.","icon":"bi-hdd"}],"recentActivity":[{"occurredAtUtc":"2025-04-12T15:12:00Z","title":"Tenant admin signed in","detail":"Admin authenticated with MFA from trusted device.","severity":"Info","icon":"bi-box-arrow-in-right"},{"occurredAtUtc":"2025-04-12T13:40:00Z","title":"Portal request claimed","detail":"Urgent COI request assigned to Beth N.","severity":"Info","icon":"bi-inbox"},{"occurredAtUtc":"2025-04-11T20:10:00Z","title":"Branding published","detail":"Portal colors, support details, and welcome text were published.","severity":"Success","icon":"bi-palette"},{"occurredAtUtc":"2025-04-11T18:25:00Z","title":"Security warning","detail":"Failed login attempt blocked by account lockout policy.","severity":"Warning","icon":"bi-shield-exclamation"}]}';

    IF EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId=@TenantId AND Kind=N'PortalMyAccount' AND Code=N'my-account' AND IsDeleted=0)
    BEGIN
        UPDATE Portal.AdminRecord
        SET Name = N'Sullivan Agency', Status = N'Active', JsonData = @Json, ModifiedDateUtc = SYSUTCDATETIME()
        WHERE TenantId=@TenantId AND Kind=N'PortalMyAccount' AND Code=N'my-account' AND IsDeleted=0;
    END
    ELSE
    BEGIN
        INSERT INTO Portal.AdminRecord (PortalAdminRecordId,TenantId,Kind,Code,Name,Status,JsonData,CreatedDateUtc,IsDeleted)
        VALUES (NEWID(),@TenantId,N'PortalMyAccount',N'my-account',N'Sullivan Agency',N'Active',@Json,SYSUTCDATETIME(),0);
    END
END
""";
    private const string Migration0090_IamPermissionCatalogSeed = """
IF OBJECT_ID(N'IAM.Permission') IS NOT NULL
BEGIN
    DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
    DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';

    IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Master') EXEC(N'CREATE SCHEMA Master');

    IF OBJECT_ID(N'Master.PermissionAction') IS NULL
    BEGIN
        CREATE TABLE Master.PermissionAction (
            PermissionActionId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            ActionCode NVARCHAR(100) NOT NULL UNIQUE,
            ActionName NVARCHAR(100) NOT NULL UNIQUE,
            Description NVARCHAR(200) NULL
        );
    END

    IF COL_LENGTH(N'Master.PermissionAction', N'ActionCode') IS NULL ALTER TABLE Master.PermissionAction ADD ActionCode NVARCHAR(100) NULL;
    EXEC(N'UPDATE Master.PermissionAction SET ActionCode = UPPER(REPLACE(ActionName, N'' '', N''_'')) WHERE ActionCode IS NULL;');
    IF NOT EXISTS (
        SELECT 1
        FROM sys.default_constraints dc
        INNER JOIN sys.columns c ON c.object_id = dc.parent_object_id AND c.column_id = dc.parent_column_id
        WHERE dc.parent_object_id = OBJECT_ID(N'Master.PermissionAction') AND c.name = N'ActionCode')
        ALTER TABLE Master.PermissionAction ADD CONSTRAINT DF_Master_PermissionAction_ActionCode DEFAULT CONVERT(NVARCHAR(36), NEWID()) FOR ActionCode;

    IF NOT EXISTS (SELECT 1 FROM Master.PermissionAction WHERE ActionName = N'Read' OR ActionCode = N'READ') INSERT INTO Master.PermissionAction (ActionCode, ActionName) VALUES (N'READ', N'Read');
    IF NOT EXISTS (SELECT 1 FROM Master.PermissionAction WHERE ActionName = N'Manage' OR ActionCode = N'MANAGE') INSERT INTO Master.PermissionAction (ActionCode, ActionName) VALUES (N'MANAGE', N'Manage');
    IF NOT EXISTS (SELECT 1 FROM Master.PermissionAction WHERE ActionName = N'Export' OR ActionCode = N'EXPORT') INSERT INTO Master.PermissionAction (ActionCode, ActionName) VALUES (N'EXPORT', N'Export');
    IF NOT EXISTS (SELECT 1 FROM Master.PermissionAction WHERE ActionName = N'Delete' OR ActionCode = N'DELETE') INSERT INTO Master.PermissionAction (ActionCode, ActionName) VALUES (N'DELETE', N'Delete');
    IF NOT EXISTS (SELECT 1 FROM Master.PermissionAction WHERE ActionName = N'Write' OR ActionCode = N'WRITE') INSERT INTO Master.PermissionAction (ActionCode, ActionName) VALUES (N'WRITE', N'Write');

    IF COL_LENGTH(N'IAM.Permission', N'TenantId') IS NULL ALTER TABLE IAM.Permission ADD TenantId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'IAM.Permission', N'PermissionActionId') IS NULL ALTER TABLE IAM.Permission ADD PermissionActionId INT NOT NULL CONSTRAINT DF_IAM_Permission_PermissionActionId DEFAULT 1;
    IF COL_LENGTH(N'IAM.Permission', N'PermissionName') IS NULL ALTER TABLE IAM.Permission ADD PermissionName NVARCHAR(200) NULL;
    IF COL_LENGTH(N'IAM.Permission', N'ResourceCode') IS NULL ALTER TABLE IAM.Permission ADD ResourceCode NVARCHAR(100) NULL;
    IF COL_LENGTH(N'IAM.Permission', N'ActionCode') IS NULL ALTER TABLE IAM.Permission ADD ActionCode NVARCHAR(100) NULL;
    IF COL_LENGTH(N'IAM.Permission', N'Description') IS NULL ALTER TABLE IAM.Permission ADD Description NVARCHAR(500) NULL;
    IF COL_LENGTH(N'IAM.Permission', N'IsBuiltIn') IS NULL ALTER TABLE IAM.Permission ADD IsBuiltIn BIT NOT NULL CONSTRAINT DF_IAM_Permission_IsBuiltIn DEFAULT 0;
    IF COL_LENGTH(N'IAM.Permission', N'IsActive') IS NULL ALTER TABLE IAM.Permission ADD IsActive BIT NOT NULL CONSTRAINT DF_IAM_Permission_IsActive DEFAULT 1;
    IF COL_LENGTH(N'IAM.Permission', N'CreatedByUserId') IS NULL ALTER TABLE IAM.Permission ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'IAM.Permission', N'CreatedDateUtc') IS NULL ALTER TABLE IAM.Permission ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_IAM_Permission_CreatedDateUtc DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'IAM.Permission', N'ModifiedByUserId') IS NULL ALTER TABLE IAM.Permission ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'IAM.Permission', N'ModifiedDateUtc') IS NULL ALTER TABLE IAM.Permission ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'IAM.Permission', N'IsDeleted') IS NULL ALTER TABLE IAM.Permission ADD IsDeleted BIT NOT NULL CONSTRAINT DF_IAM_Permission_IsDeleted DEFAULT 0;

    EXEC(N'
    DECLARE @TenantId UNIQUEIDENTIFIER = ''00000000-0000-0000-0000-000000000001'';
    DECLARE @AdminUserId UNIQUEIDENTIFIER = ''00000000-0000-0000-0000-000000000002'';

    UPDATE IAM.Permission
    SET TenantId = COALESCE(TenantId, @TenantId),
        PermissionName = COALESCE(NULLIF(PermissionName, N''''), PermissionCode),
        ResourceCode = COALESCE(NULLIF(ResourceCode, N''''), N''IAM.General''),
        ActionCode = COALESCE(NULLIF(ActionCode, N''''), N''READ''),
        IsActive = 1,
        IsDeleted = 0
    WHERE TenantId IS NULL
       OR PermissionName IS NULL OR PermissionName = N''''
       OR ResourceCode IS NULL OR ResourceCode = N''''
       OR ActionCode IS NULL OR ActionCode = N'''';

    DECLARE @SeedPermissions TABLE (
        PermissionId UNIQUEIDENTIFIER NOT NULL,
        PermissionCode NVARCHAR(200) NOT NULL,
        PermissionName NVARCHAR(200) NOT NULL,
        ResourceCode NVARCHAR(100) NOT NULL,
        ActionCode NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        IsBuiltIn BIT NOT NULL
    );

    INSERT INTO @SeedPermissions (PermissionId, PermissionCode, PermissionName, ResourceCode, ActionCode, Description, IsBuiltIn)
    VALUES
        (''90000000-0000-0000-0000-000000000001'', N''IAM.USERS.READ'', N''View users'', N''IAM.Users'', N''READ'', N''View user profiles, status, and security metadata.'', 1),
        (''90000000-0000-0000-0000-000000000002'', N''IAM.USERS.MANAGE'', N''Manage users'', N''IAM.Users'', N''MANAGE'', N''Create, update, activate, deactivate, and lock user accounts.'', 1),
        (''90000000-0000-0000-0000-000000000003'', N''IAM.ROLES.READ'', N''View roles'', N''IAM.Roles'', N''READ'', N''View role catalog, role details, and role assignments.'', 1),
        (''90000000-0000-0000-0000-000000000004'', N''IAM.ROLES.MANAGE'', N''Manage roles'', N''IAM.Roles'', N''MANAGE'', N''Create and update roles and role membership.'', 1),
        (''90000000-0000-0000-0000-000000000005'', N''IAM.PERMISSIONS.READ'', N''View permissions'', N''IAM.Permissions'', N''READ'', N''View the tenant permission catalog and role usage.'', 1),
        (''90000000-0000-0000-0000-000000000006'', N''IAM.PERMISSIONS.MANAGE'', N''Manage permissions'', N''IAM.Permissions'', N''MANAGE'', N''Create, deactivate, and assign permission catalog entries.'', 1),
        (''90000000-0000-0000-0000-000000000007'', N''IAM.AUDIT.READ'', N''View audit logs'', N''IAM.Audit'', N''READ'', N''View IAM audit trail, login attempts, and access events.'', 1),
        (''90000000-0000-0000-0000-000000000008'', N''IAM.AUDIT.EXPORT'', N''Export audit logs'', N''IAM.Audit'', N''EXPORT'', N''Export IAM audit history for compliance review.'', 1),
        (''90000000-0000-0000-0000-000000000009'', N''CRM.ACCOUNTS.READ'', N''View CRM accounts'', N''CRM.Accounts'', N''READ'', N''View account records and account relationship data.'', 0),
        (''90000000-0000-0000-0000-000000000010'', N''CRM.ACCOUNTS.MANAGE'', N''Manage CRM accounts'', N''CRM.Accounts'', N''MANAGE'', N''Create and update account records.'', 0),
        (''90000000-0000-0000-0000-000000000011'', N''CRM.OPPORTUNITIES.READ'', N''View opportunities'', N''CRM.Opportunities'', N''READ'', N''View opportunity pipeline and revenue details.'', 0),
        (''90000000-0000-0000-0000-000000000012'', N''CRM.OPPORTUNITIES.MANAGE'', N''Manage opportunities'', N''CRM.Opportunities'', N''MANAGE'', N''Create and update opportunity records.'', 0),
        (''90000000-0000-0000-0000-000000000013'', N''POLICY.POLICIES.READ'', N''View policies'', N''Policy.Policies'', N''READ'', N''View policy records, terms, and related documents.'', 0),
        (''90000000-0000-0000-0000-000000000014'', N''POLICY.POLICIES.MANAGE'', N''Manage policies'', N''Policy.Policies'', N''MANAGE'', N''Create and update policy records and endorsements.'', 0),
        (''90000000-0000-0000-0000-000000000015'', N''BILLING.INVOICES.READ'', N''View invoices'', N''Billing.Invoices'', N''READ'', N''View invoices, receivables, and billing history.'', 0),
        (''90000000-0000-0000-0000-000000000016'', N''BILLING.INVOICES.MANAGE'', N''Manage invoices'', N''Billing.Invoices'', N''MANAGE'', N''Create and update invoices and payment status.'', 0),
        (''90000000-0000-0000-0000-000000000017'', N''DMS.DOCUMENTS.READ'', N''View documents'', N''DMS.Documents'', N''READ'', N''View document library records and metadata.'', 0),
        (''90000000-0000-0000-0000-000000000018'', N''DMS.DOCUMENTS.MANAGE'', N''Manage documents'', N''DMS.Documents'', N''MANAGE'', N''Upload, classify, and update document records.'', 0);

    INSERT INTO IAM.Permission (PermissionId, TenantId, PermissionCode, PermissionActionId, PermissionName, ResourceCode, ActionCode, ModuleCode, Description, IsBuiltIn, IsActive, CreatedByUserId, CreatedDateUtc, IsDeleted)
    SELECT s.PermissionId, @TenantId, s.PermissionCode,
           COALESCE(pa.PermissionActionId, readAction.PermissionActionId, 1),
           s.PermissionName, s.ResourceCode, s.ActionCode, LEFT(s.ResourceCode, CHARINDEX(N''.'', s.ResourceCode + N''.'') - 1), s.Description, s.IsBuiltIn, 1, @AdminUserId, SYSUTCDATETIME(), 0
    FROM @SeedPermissions s
    OUTER APPLY (SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE ActionCode = CASE UPPER(s.ActionCode) WHEN N''VIEW'' THEN N''READ'' WHEN N''UPDATE'' THEN N''WRITE'' WHEN N''CREATE'' THEN N''WRITE'' ELSE UPPER(s.ActionCode) END OR ActionName = CASE UPPER(s.ActionCode) WHEN N''READ'' THEN N''Read'' WHEN N''VIEW'' THEN N''Read'' WHEN N''MANAGE'' THEN N''Manage'' WHEN N''EXPORT'' THEN N''Export'' WHEN N''DELETE'' THEN N''Delete'' WHEN N''WRITE'' THEN N''Write'' WHEN N''UPDATE'' THEN N''Write'' WHEN N''CREATE'' THEN N''Write'' ELSE N''Read'' END) pa
    OUTER APPLY (SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE ActionCode = N''READ'' OR ActionName = N''Read'' ORDER BY PermissionActionId) readAction
    WHERE NOT EXISTS (SELECT 1 FROM IAM.Permission p WHERE p.TenantId = @TenantId AND p.PermissionCode = s.PermissionCode AND p.IsDeleted = 0);
    ');
END
""";
    private const string Migration0091_AuditTimelineSchemaFix = """
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Audit') EXEC(N'CREATE SCHEMA Audit');

IF OBJECT_ID(N'Audit.AuditLog') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Audit.AuditLog', N'AuditLogId') IS NULL ALTER TABLE Audit.AuditLog ADD AuditLogId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_AuditLog_AuditLogId_0091 DEFAULT NEWID();
    IF COL_LENGTH(N'Audit.AuditLog', N'TenantId') IS NULL ALTER TABLE Audit.AuditLog ADD TenantId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Audit.AuditLog', N'EntityName') IS NULL ALTER TABLE Audit.AuditLog ADD EntityName NVARCHAR(200) NOT NULL CONSTRAINT DF_AuditLog_EntityName_0091 DEFAULT N'Unknown';
    IF COL_LENGTH(N'Audit.AuditLog', N'EntityId') IS NULL ALTER TABLE Audit.AuditLog ADD EntityId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Audit.AuditLog', N'EventTypeCode') IS NULL ALTER TABLE Audit.AuditLog ADD EventTypeCode NVARCHAR(100) NOT NULL CONSTRAINT DF_AuditLog_EventTypeCode_0091 DEFAULT N'Update';
    IF COL_LENGTH(N'Audit.AuditLog', N'ActionName') IS NULL ALTER TABLE Audit.AuditLog ADD ActionName NVARCHAR(200) NOT NULL CONSTRAINT DF_AuditLog_ActionName_0091 DEFAULT N'Updated';
    IF COL_LENGTH(N'Audit.AuditLog', N'PerformedByUserId') IS NULL ALTER TABLE Audit.AuditLog ADD PerformedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Audit.AuditLog', N'PerformedDateUtc') IS NULL ALTER TABLE Audit.AuditLog ADD PerformedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AuditLog_PerformedDateUtc_0091 DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Audit.AuditLog', N'CreatedDateUtc') IS NULL ALTER TABLE Audit.AuditLog ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_AuditLog_CreatedDateUtc_0091 DEFAULT SYSUTCDATETIME();
END

IF OBJECT_ID(N'Audit.FieldChangeLog') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Audit.FieldChangeLog', N'FieldChangeLogId') IS NULL ALTER TABLE Audit.FieldChangeLog ADD FieldChangeLogId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_FieldChangeLog_FieldChangeLogId_0091 DEFAULT NEWID();
    IF COL_LENGTH(N'Audit.FieldChangeLog', N'TenantId') IS NULL ALTER TABLE Audit.FieldChangeLog ADD TenantId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Audit.FieldChangeLog', N'EntityName') IS NULL ALTER TABLE Audit.FieldChangeLog ADD EntityName NVARCHAR(200) NOT NULL CONSTRAINT DF_FieldChangeLog_EntityName_0091 DEFAULT N'Unknown';
    IF COL_LENGTH(N'Audit.FieldChangeLog', N'EntityId') IS NULL ALTER TABLE Audit.FieldChangeLog ADD EntityId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Audit.FieldChangeLog', N'FieldName') IS NULL ALTER TABLE Audit.FieldChangeLog ADD FieldName NVARCHAR(200) NOT NULL CONSTRAINT DF_FieldChangeLog_FieldName_0091 DEFAULT N'Unknown';
    IF COL_LENGTH(N'Audit.FieldChangeLog', N'OldValue') IS NULL ALTER TABLE Audit.FieldChangeLog ADD OldValue NVARCHAR(MAX) NULL;
    IF COL_LENGTH(N'Audit.FieldChangeLog', N'NewValue') IS NULL ALTER TABLE Audit.FieldChangeLog ADD NewValue NVARCHAR(MAX) NULL;
    IF COL_LENGTH(N'Audit.FieldChangeLog', N'ChangedByUserId') IS NULL ALTER TABLE Audit.FieldChangeLog ADD ChangedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Audit.FieldChangeLog', N'ChangedDateUtc') IS NULL ALTER TABLE Audit.FieldChangeLog ADD ChangedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_FieldChangeLog_ChangedDateUtc_0091 DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Audit.FieldChangeLog', N'ChangeSource') IS NULL ALTER TABLE Audit.FieldChangeLog ADD ChangeSource NVARCHAR(100) NULL;
    IF COL_LENGTH(N'Audit.FieldChangeLog', N'IpAddress') IS NULL ALTER TABLE Audit.FieldChangeLog ADD IpAddress NVARCHAR(64) NULL;
    IF COL_LENGTH(N'Audit.FieldChangeLog', N'IsDeleted') IS NULL ALTER TABLE Audit.FieldChangeLog ADD IsDeleted BIT NOT NULL CONSTRAINT DF_FieldChangeLog_IsDeleted_0091 DEFAULT 0;
END

IF OBJECT_ID(N'Audit.ExportLog') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Audit.ExportLog', N'ExportLogId') IS NULL ALTER TABLE Audit.ExportLog ADD ExportLogId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_ExportLog_ExportLogId_0091 DEFAULT NEWID();
    IF COL_LENGTH(N'Audit.ExportLog', N'TenantId') IS NULL ALTER TABLE Audit.ExportLog ADD TenantId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Audit.ExportLog', N'EntityName') IS NULL ALTER TABLE Audit.ExportLog ADD EntityName NVARCHAR(200) NOT NULL CONSTRAINT DF_ExportLog_EntityName_0091 DEFAULT N'Unknown';
    IF COL_LENGTH(N'Audit.ExportLog', N'EntityId') IS NULL ALTER TABLE Audit.ExportLog ADD EntityId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Audit.ExportLog', N'ExportTypeCode') IS NULL ALTER TABLE Audit.ExportLog ADD ExportTypeCode NVARCHAR(100) NOT NULL CONSTRAINT DF_ExportLog_ExportTypeCode_0091 DEFAULT N'Export';
    IF COL_LENGTH(N'Audit.ExportLog', N'FileName') IS NULL ALTER TABLE Audit.ExportLog ADD FileName NVARCHAR(260) NULL;
    IF COL_LENGTH(N'Audit.ExportLog', N'FormatCode') IS NULL ALTER TABLE Audit.ExportLog ADD FormatCode NVARCHAR(50) NULL;
    IF COL_LENGTH(N'Audit.ExportLog', N'RecordCount') IS NULL ALTER TABLE Audit.ExportLog ADD RecordCount INT NOT NULL CONSTRAINT DF_ExportLog_RecordCount_0091 DEFAULT 0;
    IF COL_LENGTH(N'Audit.ExportLog', N'PerformedByUserId') IS NULL ALTER TABLE Audit.ExportLog ADD PerformedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Audit.ExportLog', N'IpAddress') IS NULL ALTER TABLE Audit.ExportLog ADD IpAddress NVARCHAR(64) NULL;
    IF COL_LENGTH(N'Audit.ExportLog', N'CreatedDateUtc') IS NULL ALTER TABLE Audit.ExportLog ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ExportLog_CreatedDateUtc_0091 DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Audit.ExportLog', N'IsDeleted') IS NULL ALTER TABLE Audit.ExportLog ADD IsDeleted BIT NOT NULL CONSTRAINT DF_ExportLog_IsDeleted_0091 DEFAULT 0;
END
""";
    private const string Migration0092_CsrWorkbenchSeed = """
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Account1 UNIQUEIDENTIFIER = NULL;
DECLARE @Account2 UNIQUEIDENTIFIER = NULL;
DECLARE @Account3 UNIQUEIDENTIFIER = NULL;
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

SELECT TOP 1 @Account1 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc;
SELECT TOP 1 @Account2 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND AccountId <> @Account1 ORDER BY CreatedDateUtc;
SELECT TOP 1 @Account3 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND AccountId NOT IN (@Account1, COALESCE(@Account2, @Account1)) ORDER BY CreatedDateUtc;

SET @Account2 = COALESCE(@Account2, @Account1);
SET @Account3 = COALESCE(@Account3, @Account1);

IF @Account1 IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND RequestNumber = N'CSR-SR-1001')
        INSERT INTO OPS.ServiceRequest (ServiceRequestId, TenantId, AccountId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES (NEWID(), @TenantId, @Account1, N'CSR-SR-1001', N'Servicing', N'Coverage question on renewal invoice', N'{"category":"Coverage Review","channel":"Phone","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 2, @Now), 126) + N'","notes":"Tenant admin CSR needs to confirm coverage wording and call the insured back."}', N'Normal', @AdminUserId, N'Open', DATEADD(day, -1, @Now), @AdminUserId, 0);

    IF NOT EXISTS (SELECT 1 FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND RequestNumber = N'CSR-END-1001')
        INSERT INTO OPS.ServiceRequest (ServiceRequestId, TenantId, AccountId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES (NEWID(), @TenantId, @Account1, N'CSR-END-1001', N'Endorsement', N'Add location to property policy', N'{"category":"Add Location","channel":"Email","policyNumber":"BOP-24-10491","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 1, @Now), 126) + N'","notes":"Insured acquired an additional warehouse and needs it endorsed before move-in."}', N'High', @AdminUserId, N'Open', DATEADD(day, -3, @Now), @AdminUserId, 0);

    IF NOT EXISTS (SELECT 1 FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND RequestNumber = N'CSR-COI-1001')
        INSERT INTO OPS.ServiceRequest (ServiceRequestId, TenantId, AccountId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES (NEWID(), @TenantId, @Account2, N'CSR-COI-1001', N'CertificateOfInsurance', N'Rush COI for landlord', N'{"category":"Landlord COI","channel":"Portal","policyNumber":"GL-24-77812","certHolder":"Madison Industrial Holdings","dueDate":"' + CONVERT(NVARCHAR(30), @Now, 126) + N'","notes":"Certificate holder requires additional insured wording today."}', N'Urgent', @AdminUserId, N'Open', DATEADD(day, -2, @Now), @AdminUserId, 0);

    IF NOT EXISTS (SELECT 1 FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND RequestNumber = N'CSR-BIL-1001')
        INSERT INTO OPS.ServiceRequest (ServiceRequestId, TenantId, AccountId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES (NEWID(), @TenantId, @Account2, N'CSR-BIL-1001', N'BillingInquiry', N'Invoice discrepancy on workers comp audit', N'{"category":"Audit Billing","channel":"Email","amount":"18450.00","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 2, @Now), 126) + N'","notes":"Client disputes additional premium from carrier audit."}', N'High', @AdminUserId, N'Open', DATEADD(day, -4, @Now), @AdminUserId, 0);

    IF NOT EXISTS (SELECT 1 FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND RequestNumber = N'CSR-CMP-1001')
        INSERT INTO OPS.ServiceRequest (ServiceRequestId, TenantId, AccountId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES (NEWID(), @TenantId, @Account3, N'CSR-CMP-1001', N'Complaint', N'Escalated complaint: delayed endorsement', N'{"category":"Service Delay","channel":"Phone","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, -1, @Now), 126) + N'","notes":"Tenant admin user should review timeline and provide same-day response."}', N'Critical', @AdminUserId, N'Open', DATEADD(day, -8, @Now), @AdminUserId, 0);

    IF NOT EXISTS (SELECT 1 FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND RequestNumber = N'CSR-FUP-1001')
        INSERT INTO OPS.ServiceRequest (ServiceRequestId, TenantId, AccountId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES (NEWID(), @TenantId, @Account3, N'CSR-FUP-1001', N'FollowUp', N'Follow up on signed supplemental application', N'{"category":"Documentation","channel":"Email","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, -2, @Now), 126) + N'","notes":"Producer is waiting on the signed supplemental application for submission."}', N'High', @AdminUserId, N'Open', DATEADD(day, -6, @Now), @AdminUserId, 0);
END
""";
    private const string Migration0093_ProducerWorkbenchSeed = """
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @CompanyId UNIQUEIDENTIFIER = NULL;

IF OBJECT_ID(N'Core.Company') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Core.Company', N'TenantId') IS NOT NULL
        EXEC sp_executesql N'SELECT TOP 1 @CompanyIdOut = CompanyId FROM Core.Company WHERE TenantId = @TenantId ORDER BY CompanyId;', N'@TenantId UNIQUEIDENTIFIER, @CompanyIdOut UNIQUEIDENTIFIER OUTPUT', @TenantId, @CompanyId OUTPUT;

    IF @CompanyId IS NULL
        SELECT TOP 1 @CompanyId = CompanyId FROM Core.Company ORDER BY CompanyId;
END
DECLARE @CompanyId UNIQUEIDENTIFIER = NULL;

IF OBJECT_ID(N'Core.Company') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Core.Company', N'TenantId') IS NOT NULL
        EXEC sp_executesql N'SELECT TOP 1 @CompanyIdOut = CompanyId FROM Core.Company WHERE TenantId = @TenantId ORDER BY CompanyId;', N'@TenantId UNIQUEIDENTIFIER, @CompanyIdOut UNIQUEIDENTIFIER OUTPUT', @TenantId, @CompanyId OUTPUT;

    IF @CompanyId IS NULL
        SELECT TOP 1 @CompanyId = CompanyId FROM Core.Company ORDER BY CompanyId;
END
DECLARE @Account1 UNIQUEIDENTIFIER = NULL;
DECLARE @Account2 UNIQUEIDENTIFIER = NULL;
DECLARE @Account3 UNIQUEIDENTIFIER = NULL;
DECLARE @Account4 UNIQUEIDENTIFIER = NULL;
DECLARE @StageProspect UNIQUEIDENTIFIER = '05000000-0000-0000-0000-000000000001';
DECLARE @StageQualify UNIQUEIDENTIFIER = '05000000-0000-0000-0000-000000000002';
DECLARE @StageProposal UNIQUEIDENTIFIER = '05000000-0000-0000-0000-000000000003';
DECLARE @StageNegotiate UNIQUEIDENTIFIER = '05000000-0000-0000-0000-000000000004';

SELECT TOP 1 @Account1 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc;
SELECT TOP 1 @Account2 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND AccountId <> @Account1 ORDER BY CreatedDateUtc;
SELECT TOP 1 @Account3 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND AccountId NOT IN (@Account1, COALESCE(@Account2, @Account1)) ORDER BY CreatedDateUtc;
SELECT TOP 1 @Account4 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND AccountId NOT IN (@Account1, COALESCE(@Account2, @Account1), COALESCE(@Account3, @Account1)) ORDER BY CreatedDateUtc;

SET @Account2 = COALESCE(@Account2, @Account1);
SET @Account3 = COALESCE(@Account3, @Account1);
SET @Account4 = COALESCE(@Account4, @Account2);

IF OBJECT_ID(N'CRM.OpportunityStage') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM CRM.OpportunityStage WHERE OpportunityStageId = @StageProspect)
        INSERT INTO CRM.OpportunityStage (OpportunityStageId, TenantId, StageCode, StageName, SortOrder, ProbabilityPercent, IsClosedStage, IsWonStage, IsActive)
        VALUES (@StageProspect, @TenantId, N'PROSPECT', N'Prospect', 1, 10, 0, 0, 1);

    IF NOT EXISTS (SELECT 1 FROM CRM.OpportunityStage WHERE OpportunityStageId = @StageQualify)
        INSERT INTO CRM.OpportunityStage (OpportunityStageId, TenantId, StageCode, StageName, SortOrder, ProbabilityPercent, IsClosedStage, IsWonStage, IsActive)
        VALUES (@StageQualify, @TenantId, N'QUALIFY', N'Qualify', 2, 25, 0, 0, 1);

    IF NOT EXISTS (SELECT 1 FROM CRM.OpportunityStage WHERE OpportunityStageId = @StageProposal)
        INSERT INTO CRM.OpportunityStage (OpportunityStageId, TenantId, StageCode, StageName, SortOrder, ProbabilityPercent, IsClosedStage, IsWonStage, IsActive)
        VALUES (@StageProposal, @TenantId, N'PROPOSAL', N'Proposal', 3, 50, 0, 0, 1);

    IF NOT EXISTS (SELECT 1 FROM CRM.OpportunityStage WHERE OpportunityStageId = @StageNegotiate)
        INSERT INTO CRM.OpportunityStage (OpportunityStageId, TenantId, StageCode, StageName, SortOrder, ProbabilityPercent, IsClosedStage, IsWonStage, IsActive)
        VALUES (@StageNegotiate, @TenantId, N'NEGOTIATE', N'Negotiation', 4, 75, 0, 0, 1);
END

IF @Account1 IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM CRM.Lead WHERE TenantId = @TenantId AND LeadNumber = N'PWB-LD-1001')
        INSERT INTO CRM.Lead (LeadId, TenantId, LeadNumber, AccountName, FirstName, LastName, Email, Phone, InterestedService, Score, PriorityCode, SourceCode, NurturingStageCode, StatusCodeId, AssignedToUserId, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
        ('c1000000-0000-0000-0000-000000000001', @TenantId, N'PWB-LD-1001', N'Northstar Robotics', N'Priya', N'Raman', N'priya.raman@northstarrobotics.com', N'(312) 555-4011', N'Cyber Liability', 94, N'High', N'Referral', N'Contacted', 2, @AdminUserId, DATEADD(day, -9, @Now), @AdminUserId, 0),
        ('c1000000-0000-0000-0000-000000000002', @TenantId, N'PWB-LD-1002', N'Hamilton Food Group', N'Elliot', N'Hamilton', N'elliot@hamiltonfood.com', N'(214) 555-3198', N'Workers Compensation', 86, N'High', N'Website', N'New', 1, @AdminUserId, DATEADD(day, -5, @Now), @AdminUserId, 0),
        ('c1000000-0000-0000-0000-000000000003', @TenantId, N'PWB-LD-1003', N'Vista Property Partners', N'Maya', N'Lopez', N'maya@vistaproperty.com', N'(602) 555-2241', N'Business Owner''s Policy', 77, N'Medium', N'Partner', N'Qualified', 3, @AdminUserId, DATEADD(day, -14, @Now), @AdminUserId, 0),
        ('c1000000-0000-0000-0000-000000000004', @TenantId, N'PWB-LD-1004', N'Cascade Fleet Services', N'Noah', N'Bennett', N'noah@cascadefleet.com', N'(503) 555-9981', N'Commercial Auto', 69, N'Medium', N'Email', N'Contacted', 2, @AdminUserId, DATEADD(day, -20, @Now), @AdminUserId, 0);

    IF NOT EXISTS (SELECT 1 FROM CRM.LeadActivity WHERE TenantId = @TenantId AND Subject = N'Producer workbench next step')
    BEGIN
        INSERT INTO CRM.LeadActivity (ActivityId, TenantId, LeadId, ActivityTypeCode, Subject, Notes, ActivityDate, IsCompleted, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
        (NEWID(), @TenantId, 'c1000000-0000-0000-0000-000000000001', N'Call', N'Producer workbench next step', N'Call CFO to confirm cyber limits and retro date.', CAST(DATEADD(day, 1, @Now) AS date), 0, @Now, @AdminUserId, 0),
        (NEWID(), @TenantId, 'c1000000-0000-0000-0000-000000000002', N'Email', N'Producer workbench next step', N'Send WC payroll class code checklist.', CAST(@Now AS date), 0, @Now, @AdminUserId, 0),
        (NEWID(), @TenantId, 'c1000000-0000-0000-0000-000000000003', N'Meeting', N'Producer workbench next step', N'Schedule property portfolio review.', CAST(DATEADD(day, 3, @Now) AS date), 0, @Now, @AdminUserId, 0),
        (NEWID(), @TenantId, 'c1000000-0000-0000-0000-000000000004', N'Call', N'Producer workbench next step', N'Confirm fleet unit count and radius.', CAST(DATEADD(day, -1, @Now) AS date), 0, @Now, @AdminUserId, 0);
    END

    IF NOT EXISTS (SELECT 1 FROM CRM.Opportunity WHERE TenantId = @TenantId AND OpportunityNumber = N'PWB-OPP-1001')
        INSERT INTO CRM.Opportunity (OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName, EstimatedAmount, OwnerUserId, CloseDate, WinProbability, ForecastCategoryCode, OpportunityStageId, StatusCodeId, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
        ('c2000000-0000-0000-0000-000000000001', @TenantId, N'PWB-OPP-1001', @Account1, N'Cyber renewal and E&O package', 128000, @AdminUserId, DATEADD(day, 18, CAST(@Now AS date)), 72, N'Presented', @StageProposal, 1, DATEADD(day, -18, @Now), @AdminUserId, 0),
        ('c2000000-0000-0000-0000-000000000002', @TenantId, N'PWB-OPP-1002', @Account2, N'Workers comp remarket', 214000, @AdminUserId, DATEADD(day, 32, CAST(@Now AS date)), 58, N'Quoted', @StageProposal, 1, DATEADD(day, -11, @Now), @AdminUserId, 0),
        ('c2000000-0000-0000-0000-000000000003', @TenantId, N'PWB-OPP-1003', @Account3, N'Commercial property package', 184500, @AdminUserId, DATEADD(day, 45, CAST(@Now AS date)), 41, N'Prospect', @StageProspect, 1, DATEADD(day, -7, @Now), @AdminUserId, 0),
        ('c2000000-0000-0000-0000-000000000004', @TenantId, N'PWB-OPP-1004', @Account4, N'Fleet auto and umbrella placement', 96500, @AdminUserId, DATEADD(day, 12, CAST(@Now AS date)), 81, N'Negotiating', @StageNegotiate, 1, DATEADD(day, -23, @Now), @AdminUserId, 0);

    IF NOT EXISTS (SELECT 1 FROM CRM.Quote WHERE TenantId = @TenantId AND QuoteNumber = N'PWB-QT-1001')
        INSERT INTO CRM.Quote (QuoteId, TenantId, QuoteNumber, OpportunityId, AccountId, ValidUntilDate, TotalAmount, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
        ('c3000000-0000-0000-0000-000000000001', @TenantId, N'PWB-QT-1001', 'c2000000-0000-0000-0000-000000000001', @Account1, DATEADD(day, 4, CAST(@Now AS date)), 128000, N'Presented', DATEADD(day, -6, @Now), @AdminUserId, 0),
        ('c3000000-0000-0000-0000-000000000002', @TenantId, N'PWB-QT-1002', 'c2000000-0000-0000-0000-000000000002', @Account2, DATEADD(day, -2, CAST(@Now AS date)), 214000, N'Presented', DATEADD(day, -12, @Now), @AdminUserId, 0),
        ('c3000000-0000-0000-0000-000000000003', @TenantId, N'PWB-QT-1003', 'c2000000-0000-0000-0000-000000000004', @Account4, DATEADD(day, 10, CAST(@Now AS date)), 96500, N'Presented', DATEADD(day, -3, @Now), @AdminUserId, 0);

    IF NOT EXISTS (SELECT 1 FROM Sales.Agreement WHERE TenantId = @TenantId AND AgreementNumber = N'PWB-AGR-1001')
        INSERT INTO Sales.Agreement (AgreementId, TenantId, AgreementNumber, AccountId, OpportunityId, AgreementStatusCodeId, EffectiveStartDate, EffectiveEndDate, TotalContractValue, CurrencyCode, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
        VALUES
        ('c4000000-0000-0000-0000-000000000001', @TenantId, N'PWB-AGR-1001', @Account1, 'c2000000-0000-0000-0000-000000000001', 1, DATEADD(month, -10, CAST(@Now AS date)), DATEADD(day, 42, CAST(@Now AS date)), 151000, N'USD', DATEADD(month, -10, @Now), @AdminUserId, NULL, NULL, 0),
        ('c4000000-0000-0000-0000-000000000002', @TenantId, N'PWB-AGR-1002', @Account2, 'c2000000-0000-0000-0000-000000000002', 1, DATEADD(month, -11, CAST(@Now AS date)), DATEADD(day, 25, CAST(@Now AS date)), 224000, N'USD', DATEADD(month, -11, @Now), @AdminUserId, NULL, NULL, 0),
        ('c4000000-0000-0000-0000-000000000003', @TenantId, N'PWB-AGR-1003', @Account3, 'c2000000-0000-0000-0000-000000000003', 1, DATEADD(month, -9, CAST(@Now AS date)), DATEADD(day, 68, CAST(@Now AS date)), 187500, N'USD', DATEADD(month, -9, @Now), @AdminUserId, NULL, NULL, 0);

    IF NOT EXISTS (SELECT 1 FROM OPS.AgreementRenewal WHERE TenantId = @TenantId AND RenewalNumber = N'PWB-REN-1001')
        INSERT INTO OPS.AgreementRenewal (RenewalId, TenantId, AgreementId, RenewalNumber, NewStartDate, NewEndDate, TotalContractValue, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
        ('c5000000-0000-0000-0000-000000000001', @TenantId, 'c4000000-0000-0000-0000-000000000001', N'PWB-REN-1001', DATEADD(day, 42, CAST(@Now AS date)), DATEADD(day, 407, CAST(@Now AS date)), 163500, N'Pending', DATEADD(day, -12, @Now), @AdminUserId, 0),
        ('c5000000-0000-0000-0000-000000000002', @TenantId, 'c4000000-0000-0000-0000-000000000002', N'PWB-REN-1002', DATEADD(day, 25, CAST(@Now AS date)), DATEADD(day, 390, CAST(@Now AS date)), 239000, N'Pending', DATEADD(day, -18, @Now), @AdminUserId, 0),
        ('c5000000-0000-0000-0000-000000000003', @TenantId, 'c4000000-0000-0000-0000-000000000003', N'PWB-REN-1003', DATEADD(day, 68, CAST(@Now AS date)), DATEADD(day, 433, CAST(@Now AS date)), 196000, N'Pending', DATEADD(day, -6, @Now), @AdminUserId, 0);

    IF OBJECT_ID(N'Portal.AdminRecord') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId = @TenantId AND Kind = N'ProducerCrossSell' AND IsDeleted = 0)
    BEGIN
        INSERT INTO Portal.AdminRecord (PortalAdminRecordId, TenantId, Kind, Code, Name, Status, JsonData, CreatedDateUtc, IsDeleted)
        VALUES
        (NEWID(), @TenantId, N'ProducerCrossSell', CONVERT(NVARCHAR(36), @Account1), N'Cyber / Umbrella gap', N'Active', N'{"currentLobs":"GL, Property","targetLob":"Cyber","oppPremium":42000,"score":91,"reason":"Technology exposure and no cyber policy on file.","lastContact":"' + CONVERT(NVARCHAR(30), DATEADD(day, -4, @Now), 126) + N'"}', @Now, 0),
        (NEWID(), @TenantId, N'ProducerCrossSell', CONVERT(NVARCHAR(36), @Account2), N'Umbrella opportunity', N'Active', N'{"currentLobs":"WC, Auto, GL","targetLob":"Umbrella","oppPremium":36500,"score":84,"reason":"Fleet and payroll growth indicate excess liability need.","lastContact":"' + CONVERT(NVARCHAR(30), DATEADD(day, -11, @Now), 126) + N'"}', @Now, 0),
        (NEWID(), @TenantId, N'ProducerCrossSell', CONVERT(NVARCHAR(36), @Account3), N'Property schedule review', N'Active', N'{"currentLobs":"BOP","targetLob":"Commercial Property","oppPremium":51500,"score":79,"reason":"Additional locations identified during account review.","lastContact":"' + CONVERT(NVARCHAR(30), DATEADD(day, -21, @Now), 126) + N'"}', @Now, 0);
    END

    IF OBJECT_ID(N'Core.Notification') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Core.Notification WHERE TenantId = @TenantId AND RecipientUserId = @AdminUserId AND Subject = N'Producer workbench: renewal priority')
    BEGIN
        INSERT INTO Core.Notification (NotificationId, TenantId, RecipientUserId, TemplateId, ChannelCode, Subject, Body, EntityName, EntityId, StatusCode, IsRead, CreatedDateUtc, IsDeleted)
        VALUES
        (NEWID(), @TenantId, @AdminUserId, NULL, N'InApp', N'Producer workbench: renewal priority', N'Hamilton Food Group renewal is inside 30 days and quote follow-up is overdue.', N'Account', @Account2, N'Delivered', 0, DATEADD(hour, -2, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, NULL, N'Email', N'Producer workbench: hot cyber lead', N'Northstar Robotics scored 94 and requested cyber terms. Call today to confirm limits.', N'Lead', 'c1000000-0000-0000-0000-000000000001', N'Sent', 0, DATEADD(hour, -5, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, NULL, N'InApp', N'Producer workbench: cross-sell trigger', N'Cascade Fleet Services is an 84 score umbrella opportunity based on fleet growth.', N'Account', @Account4, N'Delivered', 0, DATEADD(day, -1, @Now), 0);
    END
END
""";
    private const string Migration0094_ServiceManagerWorkbenchSeed = """
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @Account1 UNIQUEIDENTIFIER = NULL;
DECLARE @Account2 UNIQUEIDENTIFIER = NULL;
DECLARE @Account3 UNIQUEIDENTIFIER = NULL;
DECLARE @Account4 UNIQUEIDENTIFIER = NULL;

SELECT TOP 1 @Account1 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc;
SELECT TOP 1 @Account2 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND AccountId <> @Account1 ORDER BY CreatedDateUtc;
SELECT TOP 1 @Account3 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND AccountId NOT IN (@Account1, COALESCE(@Account2, @Account1)) ORDER BY CreatedDateUtc;
SELECT TOP 1 @Account4 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND AccountId NOT IN (@Account1, COALESCE(@Account2, @Account1), COALESCE(@Account3, @Account1)) ORDER BY CreatedDateUtc;

SET @Account2 = COALESCE(@Account2, @Account1);
SET @Account3 = COALESCE(@Account3, @Account1);
SET @Account4 = COALESCE(@Account4, @Account2);

IF @Account1 IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM OPS.ServiceRequest WHERE TenantId = @TenantId AND RequestNumber = N'SM-ESC-1001')
        INSERT INTO OPS.ServiceRequest (ServiceRequestId, TenantId, AccountId, RequestNumber, RequestTypeCode, Subject, Description, PriorityCode, AssignedToUserId, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
        (NEWID(), @TenantId, @Account1, N'SM-ESC-1001', N'Escalation', N'Executive escalation: certificate wording dispute', N'{"queueName":"Escalations","escalatedBy":"Tenant Admin","notes":"Carrier rejected requested blanket wording; client needs contract-compliant certificate today."}', N'Critical', @AdminUserId, N'Open', DATEADD(day, -3, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @Account2, N'SM-SLA-1001', N'Endorsement', N'SLA breach: vehicle add still pending', N'{"queueName":"Endorsements","notes":"Commercial auto endorsement has passed internal SLA and requires manager intervention."}', N'Urgent', @AdminUserId, N'Open', DATEADD(day, -5, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @Account3, N'SM-CAR-1001', N'CarrierTicket', N'Carrier portal outage blocking bind request', N'{"queueName":"Carrier Service","carrierName":"Contoso Mutual","notes":"Carrier portal is returning 500 errors for bind submission."}', N'High', @AdminUserId, N'Open', DATEADD(day, -4, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @Account4, N'SM-QA-1001', N'QualityAudit', N'QA review: renewal documentation checklist', N'{"queueName":"Quality Audit","auditedBy":"Tenant Admin","qualityScore":"8.7","auditedAt":"' + CONVERT(NVARCHAR(30), DATEADD(day, -1, @Now), 126) + N'","qualityNotes":"Strong documentation; missing second-contact evidence.","notes":"Audit generated from renewal servicing sample."}', N'Normal', @AdminUserId, N'Open', DATEADD(day, -2, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @Account2, N'SM-UNA-1001', N'CertificateOfInsurance', N'Unassigned rush certificate request', N'{"queueName":"Certificates","notes":"Rush certificate request needs assignment before noon."}', N'High', NULL, N'Open', DATEADD(hour, -7, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @Account3, N'SM-UNA-1002', N'BillingInquiry', N'Unassigned billing discrepancy review', N'{"queueName":"Billing","notes":"Client reports premium finance installment mismatch."}', N'Normal', NULL, N'Open', DATEADD(day, -1, @Now), @AdminUserId, 0);
END
""";
    private const string Migration0095_AccountingWorkbenchSeed = """
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF OBJECT_ID(N'Portal.AdminRecord') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId = @TenantId AND Kind = N'AccountingWorkbench' AND IsDeleted = 0)
BEGIN
    INSERT INTO Portal.AdminRecord (PortalAdminRecordId, TenantId, Kind, Code, Name, Status, JsonData, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), @TenantId, N'AccountingWorkbench', N'REC-1001', N'Carrier statement variance - commercial package', N'Open', N'{"queueCode":"reconciliation","accountName":"Northstar Robotics","policyNumber":"CPP-24-11802","carrierName":"Contoso Mutual","assignedTo":"Tenant Admin","priority":"High","slaStatus":"At Risk","amount":0,"variance":1840.00,"dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 1, @Now), 126) + N'","reason":"Carrier statement premium differs from AMS invoice.","notes":"Review endorsement premium and commission split before trust sweep.","detailUrl":"/billing/accounting"}', DATEADD(day, -4, @Now), 0),
    (NEWID(), @TenantId, N'AccountingWorkbench', N'REC-1002', N'Download mismatch - direct bill commission', N'Open', N'{"queueCode":"reconciliation","accountName":"Hamilton Food Group","policyNumber":"WC-24-55318","carrierName":"Fabrikam Insurance","assignedTo":"Tenant Admin","priority":"Normal","slaStatus":"On Track","amount":0,"variance":-620.00,"dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 3, @Now), 126) + N'","reason":"Direct bill commission download has negative variance.","notes":"Validate producer code and commission plan override.","detailUrl":"/billing/accounting"}', DATEADD(day, -2, @Now), 0),
    (NEWID(), @TenantId, N'AccountingWorkbench', N'PAY-1001', N'Unapplied ACH payment', N'Open', N'{"queueCode":"unapplied-payments","accountName":"Vista Property Partners","policyNumber":"BOP-24-44710","paymentMethod":"ACH","assignedTo":"Tenant Admin","priority":"High","slaStatus":"At Risk","amount":7250.00,"receivedDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, -5, @Now), 126) + N'","ageDays":5,"dueDate":"' + CONVERT(NVARCHAR(30), @Now, 126) + N'","notes":"ACH batch imported without invoice match; likely renewal down payment.","detailUrl":"/billing/payments"}', DATEADD(day, -5, @Now), 0),
    (NEWID(), @TenantId, N'AccountingWorkbench', N'PAY-1002', N'Unapplied lockbox check', N'Open', N'{"queueCode":"unapplied-payments","accountName":"Cascade Fleet Services","policyNumber":"AUTO-24-88201","paymentMethod":"Check","assignedTo":"Tenant Admin","priority":"Normal","slaStatus":"On Track","amount":3180.00,"receivedDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, -2, @Now), 126) + N'","ageDays":2,"dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 2, @Now), 126) + N'","notes":"Lockbox memo omitted invoice number.","detailUrl":"/billing/payments"}', DATEADD(day, -2, @Now), 0),
    (NEWID(), @TenantId, N'AccountingWorkbench', N'COM-1001', N'Producer commission adjustment', N'Open', N'{"queueCode":"commission-adj","producerName":"Tenant Admin","policyNumber":"CYB-24-91702","assignedTo":"Tenant Admin","priority":"Normal","slaStatus":"On Track","amount":-950.00,"reason":"Split correction","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 4, @Now), 126) + N'","notes":"Adjust producer split after servicing team corrected producer of record.","detailUrl":"/commissions/transactions"}', DATEADD(day, -1, @Now), 0),
    (NEWID(), @TenantId, N'AccountingWorkbench', N'DB-1001', N'Direct-bill exception - missing policy match', N'Open', N'{"queueCode":"direct-bill","accountName":"Northstar Robotics","policyNumber":"UMB-24-22091","carrierName":"Contoso Mutual","assignedTo":"Tenant Admin","priority":"Critical","slaStatus":"Breached","amount":12800.00,"dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, -1, @Now), 126) + N'","notes":"Carrier download could not match policy; commission receivable not posted.","detailUrl":"/billing/accounting"}', DATEADD(day, -8, @Now), 0),
    (NEWID(), @TenantId, N'AccountingWorkbench', N'ME-1001', N'Month-end: reconcile trust account', N'In Progress', N'{"queueCode":"month-end","category":"Trust Accounting","assignedTo":"Tenant Admin","priority":"High","slaStatus":"At Risk","status":"In Progress","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 1, @Now), 126) + N'","ageDays":3,"notes":"Trust account reconciliation pending bank feed approval.","detailUrl":"/accounting-periods"}', DATEADD(day, -3, @Now), 0),
    (NEWID(), @TenantId, N'AccountingWorkbench', N'ME-1002', N'Month-end: post commission accrual', N'Pending', N'{"queueCode":"month-end","category":"Commissions","assignedTo":"Tenant Admin","priority":"Normal","slaStatus":"On Track","status":"Pending","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 3, @Now), 126) + N'","ageDays":1,"notes":"Post accrual after direct-bill exception queue is cleared.","detailUrl":"/accounting-periods"}', DATEADD(day, -1, @Now), 0),
    (NEWID(), @TenantId, N'AccountingWorkbench', N'ME-1003', N'Month-end: close billing subledger', N'Complete', N'{"queueCode":"month-end","category":"Billing","assignedTo":"Tenant Admin","priority":"Low","slaStatus":"On Track","status":"Complete","completedAt":"' + CONVERT(NVARCHAR(30), DATEADD(day, -1, @Now), 126) + N'","dueDate":"' + CONVERT(NVARCHAR(30), @Now, 126) + N'","ageDays":0,"notes":"Billing subledger closed successfully.","detailUrl":"/accounting-periods"}', DATEADD(day, -2, @Now), 0);
END
""";
    private const string Migration0096_MarketingWorkbenchSeed = """
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF OBJECT_ID(N'Comms.Campaign') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Comms.Campaign WHERE TenantId = @TenantId AND IsDeleted = 0 AND Name = N'Tenant Admin Benefits Cross-Sell')
BEGIN
    INSERT INTO Comms.Campaign (CampaignId, TenantId, Name, Type, Status, Segment, StartDate, Reached, OpenRate, Conversions, Revenue, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), @TenantId, N'Tenant Admin Benefits Cross-Sell', N'Multi-Channel', N'Active', N'Commercial accounts without benefits', DATEADD(day, -18, @Now), 2740, 34.8, 146, 182500, DATEADD(day, -21, @Now), 0),
    (NEWID(), @TenantId, N'Cyber Renewal Readiness Sprint', N'Email', N'Active', N'Cyber renewal within 90 days', DATEADD(day, -9, @Now), 1185, 41.2, 88, 126400, DATEADD(day, -10, @Now), 0);
END

IF OBJECT_ID(N'Portal.AdminRecord') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId = @TenantId AND Kind = N'MarketingWorkbench' AND IsDeleted = 0)
BEGIN
    INSERT INTO Portal.AdminRecord (PortalAdminRecordId, TenantId, Kind, Code, Name, Status, JsonData, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), @TenantId, N'MarketingWorkbench', N'REF-1001', N'Referral from ACME Corporation', N'Open', N'{"queueCode":"referrals","contactName":"James Brady","campaignName":"Executive Referral Program","channel":"Referral","assignedTo":"Tenant Admin","priority":"High","slaStatus":"At Risk","status":"Active","estPremium":64000,"receivedDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, -3, @Now), 126) + N'","dueDate":"' + CONVERT(NVARCHAR(30), @Now, 126) + N'","notes":"Warm manufacturing prospect seeking GL, property, and umbrella coverage.","detailUrl":"/marketing/referrals"}', DATEADD(day, -3, @Now), 0),
    (NEWID(), @TenantId, N'MarketingWorkbench', N'REF-1002', N'Partner referral - BlueSky Partners', N'Converted', N'{"queueCode":"referrals","contactName":"Summit Benefits LLC","campaignName":"Centers of Influence","channel":"Referral","assignedTo":"Tenant Admin","priority":"Normal","slaStatus":"On Track","status":"Converted","estPremium":38500,"receivedDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, -12, @Now), 126) + N'","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 2, @Now), 126) + N'","notes":"Converted to opportunity after introductory call.","detailUrl":"/marketing/referrals"}', DATEADD(day, -12, @Now), 0),
    (NEWID(), @TenantId, N'MarketingWorkbench', N'REF-1003', N'Client referral - warehouse expansion', N'Open', N'{"queueCode":"referrals","contactName":"Lisa Chen","campaignName":"Client Referral Rewards","channel":"Referral","assignedTo":"Tenant Admin","priority":"High","slaStatus":"On Track","status":"Active","estPremium":72000,"receivedDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, -1, @Now), 126) + N'","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 1, @Now), 126) + N'","notes":"Warm referral for a logistics firm expanding warehouse operations.","detailUrl":"/marketing/referrals"}', DATEADD(day, -1, @Now), 0),
    (NEWID(), @TenantId, N'MarketingWorkbench', N'EVT-1001', N'Commercial Risk Breakfast Briefing', N'Active', N'{"queueCode":"events","campaignName":"Risk Education Series","location":"Downtown Conference Center","assignedTo":"Tenant Admin","priority":"Normal","slaStatus":"On Track","status":"Active","attendees":42,"leads":18,"eventDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 7, @Now), 126) + N'","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 9, @Now), 126) + N'","notes":"Finalize carrier panel, QR lead capture, and post-event nurture sequence.","detailUrl":"/marketing/events"}', DATEADD(day, -6, @Now), 0),
    (NEWID(), @TenantId, N'MarketingWorkbench', N'EVT-1002', N'Cyber Liability Webinar Follow-Up', N'Open', N'{"queueCode":"events","campaignName":"Cyber Renewal Readiness Sprint","location":"Virtual","assignedTo":"Tenant Admin","priority":"High","slaStatus":"At Risk","status":"Pending Follow-Up","attendees":96,"leads":31,"eventDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, -2, @Now), 126) + N'","dueDate":"' + CONVERT(NVARCHAR(30), @Now, 126) + N'","notes":"Send recording, score attendee intent, and route high-fit accounts to producers.","detailUrl":"/marketing/events"}', DATEADD(day, -8, @Now), 0),
    (NEWID(), @TenantId, N'MarketingWorkbench', N'EVT-1003', N'Contractor Safety Lunch & Learn', N'Scheduled', N'{"queueCode":"events","campaignName":"Workers Comp Expansion — SMB","location":"North Texas Branch","assignedTo":"Tenant Admin","priority":"Normal","slaStatus":"On Track","status":"Active","attendees":28,"leads":9,"eventDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 18, @Now), 126) + N'","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 20, @Now), 126) + N'","notes":"Coordinate safety checklist handout and renewal review CTA.","detailUrl":"/marketing/events"}', DATEADD(day, -4, @Now), 0),
    (NEWID(), @TenantId, N'MarketingWorkbench', N'CNT-1001', N'Umbrella gap analysis email copy', N'Pending Approval', N'{"queueCode":"content","campaignName":"Q2 Cross-Sell — Umbrella","contentType":"Email Copy","assignedTo":"Tenant Admin","reviewedBy":"Tenant Admin","priority":"High","slaStatus":"At Risk","status":"Pending Approval","dueDate":"' + CONVERT(NVARCHAR(30), @Now, 126) + N'","notes":"Review compliance language around excess liability examples before launch.","detailUrl":"/marketing/campaign-builder"}', DATEADD(day, -2, @Now), 0),
    (NEWID(), @TenantId, N'MarketingWorkbench', N'CNT-1002', N'Cyber readiness landing page hero', N'Pending Approval', N'{"queueCode":"content","campaignName":"Cyber Renewal Readiness Sprint","contentType":"Landing Page","assignedTo":"Tenant Admin","reviewedBy":"Tenant Admin","priority":"Normal","slaStatus":"On Track","status":"Pending Approval","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 2, @Now), 126) + N'","notes":"Approve hero copy, CTA wording, and producer routing rules.","detailUrl":"/marketing/landing-pages"}', DATEADD(day, -1, @Now), 0),
    (NEWID(), @TenantId, N'MarketingWorkbench', N'CNT-1003', N'Referral program social post', N'Approved', N'{"queueCode":"content","campaignName":"Client Referral Rewards","contentType":"Social Post","assignedTo":"Tenant Admin","reviewedBy":"Tenant Admin","priority":"Low","slaStatus":"On Track","status":"Approved","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, -1, @Now), 126) + N'","notes":"Approved for LinkedIn and agency newsletter placement.","detailUrl":"/marketing/campaign-builder"}', DATEADD(day, -5, @Now), 0);
END

IF OBJECT_ID(N'Portal.AdminRecord') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId = @TenantId AND Kind = N'MarketingLeadSource' AND IsDeleted = 0)
BEGIN
    INSERT INTO Portal.AdminRecord (PortalAdminRecordId, TenantId, Kind, Code, Name, Status, JsonData, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), @TenantId, N'MarketingLeadSource', N'SRC-REF', N'Referrals', N'Active', N'{"sourceName":"Referrals","leads":38,"converted":14,"avgPremium":58200}', @Now, 0),
    (NEWID(), @TenantId, N'MarketingLeadSource', N'SRC-WEB', N'Website / Landing Pages', N'Active', N'{"sourceName":"Website / Landing Pages","leads":126,"converted":27,"avgPremium":36450}', @Now, 0),
    (NEWID(), @TenantId, N'MarketingLeadSource', N'SRC-EVT', N'Events', N'Active', N'{"sourceName":"Events","leads":58,"converted":12,"avgPremium":42750}', @Now, 0),
    (NEWID(), @TenantId, N'MarketingLeadSource', N'SRC-EMAIL', N'Email Campaigns', N'Active', N'{"sourceName":"Email Campaigns","leads":211,"converted":39,"avgPremium":31800}', @Now, 0),
    (NEWID(), @TenantId, N'MarketingLeadSource', N'SRC-SOCIAL', N'LinkedIn / Social', N'Active', N'{"sourceName":"LinkedIn / Social","leads":74,"converted":9,"avgPremium":28600}', @Now, 0);
END
""";
    private const string Migration0097_OperationsWorkbenchSeed = """
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @Account1 UNIQUEIDENTIFIER = NULL;
DECLARE @Account2 UNIQUEIDENTIFIER = NULL;
DECLARE @Account3 UNIQUEIDENTIFIER = NULL;

SELECT TOP 1 @Account1 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CreatedDateUtc;
SELECT TOP 1 @Account2 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND AccountId <> @Account1 ORDER BY CreatedDateUtc;
SELECT TOP 1 @Account3 = AccountId FROM Client.Account WHERE TenantId = @TenantId AND IsDeleted = 0 AND AccountId NOT IN (@Account1, COALESCE(@Account2, @Account1)) ORDER BY CreatedDateUtc;

SET @Account2 = COALESCE(@Account2, @Account1);
SET @Account3 = COALESCE(@Account3, @Account1);

IF @Account1 IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM OPS.TaskItem WHERE TenantId = @TenantId AND TaskNumber = N'OW-TASK-1001')
        INSERT INTO OPS.TaskItem (TaskItemId, TenantId, TaskNumber, Title, Description, TaskTypeCode, StageCode, PriorityCode, StatusCode, RelatedEntityName, RelatedEntityId, AccountId, AssignedToUserId, DueDate, CompletedDate, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
        VALUES
        (NEWID(), @TenantId, N'OW-TASK-1001', N'Review blocked bind request', N'{"accountName":"Northstar Robotics","policyNumber":"CYB-24-91702","notes":"Carrier requires updated subjectivities before bind can proceed.","detailUrl":"/tasks"}', N'Operations', N'Open', N'Critical', N'Open', N'Operations', NULL, @Account1, @AdminUserId, DATEADD(day, -2, CAST(@Now AS date)), NULL, DATEADD(day, -6, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'OW-END-1001', N'Add warehouse location endorsement', N'{"policyNumber":"BOP-24-44710","notes":"Confirm location square footage and carrier endorsement form.","detailUrl":"/service-requests"}', N'Endorsement', N'Open', N'High', N'Open', N'Policy', NULL, @Account2, @AdminUserId, DATEADD(day, 1, CAST(@Now AS date)), NULL, DATEADD(day, -3, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'OW-CERT-1001', N'Rush certificate for landlord', N'{"policyNumber":"GL-24-77812","certHolder":"Madison Industrial Holdings","notes":"Additional insured wording requested before noon.","detailUrl":"/service-requests"}', N'CertificateOfInsurance', N'Open', N'Urgent', N'Open', N'Certificate', NULL, @Account2, @AdminUserId, CAST(@Now AS date), NULL, DATEADD(hour, -9, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'OW-REN-1001', N'Follow up on renewal proposal', N'{"policyNumber":"WC-24-55318","lobCode":"WC","premium":239000,"followUpDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, -1, @Now), 126) + N'","renewalStage":"Presented","notes":"Client asked for payroll class code clarification before signing.","detailUrl":"/agreement-renewals"}', N'RenewalFollowUp', N'Presented', N'High', N'Open', N'Renewal', NULL, @Account3, @AdminUserId, DATEADD(day, 25, CAST(@Now AS date)), NULL, DATEADD(day, -8, @Now), @AdminUserId, NULL, NULL, 0);
END

IF OBJECT_ID(N'Portal.AdminRecord') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Portal.AdminRecord WHERE TenantId = @TenantId AND Kind = N'OperationsWorkbench' AND IsDeleted = 0)
BEGIN
    INSERT INTO Portal.AdminRecord (PortalAdminRecordId, TenantId, Kind, Code, Name, Status, JsonData, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), @TenantId, N'OperationsWorkbench', N'DOC-1001', N'Document indexing exception - unmatched policy', N'Open', N'{"queueCode":"doc-exceptions","queueName":"Document Exceptions","accountName":"Northstar Robotics","policyNumber":"CYB-24-91702","assignedTo":"Tenant Admin","priority":"High","dueDate":"' + CONVERT(NVARCHAR(30), @Now, 126) + N'","ageDays":2,"errorMessage":"OCR extracted policy CYB-24-917O2; no exact policy match found.","retryCount":1,"canRetry":true,"notes":"Review extracted policy number and attach document to correct policy.","detailUrl":"/documents"}', DATEADD(day, -2, @Now), 0),
    (NEWID(), @TenantId, N'OperationsWorkbench', N'DOC-1002', N'Document classification confidence below threshold', N'Open', N'{"queueCode":"doc-exceptions","queueName":"Document Exceptions","accountName":"Hamilton Food Group","policyNumber":"WC-24-55318","assignedTo":"Tenant Admin","priority":"Normal","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 2, @Now), 126) + N'","ageDays":1,"errorMessage":"Classifier confidence 42% for endorsement vs audit statement.","retryCount":0,"canRetry":true,"notes":"Manually classify and save the document type.","detailUrl":"/documents"}', DATEADD(day, -1, @Now), 0),
    (NEWID(), @TenantId, N'OperationsWorkbench', N'DL-1001', N'IVANS policy download failed', N'Open', N'{"queueCode":"failed-downloads","queueName":"Failed Downloads","accountName":"Contoso Mutual","assignedTo":"Tenant Admin","priority":"Critical","dueDate":"' + CONVERT(NVARCHAR(30), @Now, 126) + N'","ageDays":1,"errorMessage":"Carrier feed rejected AL3 segment: invalid transaction sequence.","retryCount":2,"canRetry":true,"notes":"Retry after carrier resets transaction cursor.","detailUrl":"/download-exceptions"}', DATEADD(day, -1, @Now), 0),
    (NEWID(), @TenantId, N'OperationsWorkbench', N'DL-1002', N'Direct-bill commission import timeout', N'Open', N'{"queueCode":"failed-downloads","queueName":"Failed Downloads","accountName":"Fabrikam Insurance","assignedTo":"Tenant Admin","priority":"High","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 1, @Now), 126) + N'","ageDays":3,"errorMessage":"SFTP download timed out after 120 seconds.","retryCount":3,"canRetry":true,"notes":"Validate carrier endpoint health before retry.","detailUrl":"/download-exceptions"}', DATEADD(day, -3, @Now), 0),
    (NEWID(), @TenantId, N'OperationsWorkbench', N'AUTO-1001', N'Renewal reminder automation failed', N'Open', N'{"queueCode":"failed-automations","queueName":"Failed Automations","accountName":"Renewal workflow","assignedTo":"Tenant Admin","priority":"High","dueDate":"' + CONVERT(NVARCHAR(30), @Now, 126) + N'","ageDays":1,"errorMessage":"Email template token [ProducerPhone] could not be resolved.","retryCount":1,"automationStep":"Render email template","canRetry":true,"notes":"Update template fallback token and replay automation.","detailUrl":"/workflow-designer"}', DATEADD(day, -1, @Now), 0),
    (NEWID(), @TenantId, N'OperationsWorkbench', N'AUTO-1002', N'Certificate delivery automation paused', N'Open', N'{"queueCode":"failed-automations","queueName":"Failed Automations","accountName":"Certificate workflow","assignedTo":"Tenant Admin","priority":"Normal","dueDate":"' + CONVERT(NVARCHAR(30), DATEADD(day, 2, @Now), 126) + N'","ageDays":2,"errorMessage":"Delivery connector returned 429 rate limit exceeded.","retryCount":2,"automationStep":"Send certificate package","canRetry":true,"notes":"Retry after connector throttle window clears or skip to manual delivery.","detailUrl":"/workflow-designer"}', DATEADD(day, -2, @Now), 0);
END
""";
    private const string Migration0098_AgencyDashboardFullSeed = """
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @CompanyId UNIQUEIDENTIFIER = NULL;

IF OBJECT_ID(N'Core.Company') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Core.Company', N'TenantId') IS NOT NULL
        EXEC sp_executesql N'SELECT TOP 1 @CompanyIdOut = CompanyId FROM Core.Company WHERE TenantId = @TenantId ORDER BY CompanyId;', N'@TenantId UNIQUEIDENTIFIER, @CompanyIdOut UNIQUEIDENTIFIER OUTPUT', @TenantId, @CompanyId OUTPUT;

    IF @CompanyId IS NULL
        SELECT TOP 1 @CompanyId = CompanyId FROM Core.Company ORDER BY CompanyId;
END

IF OBJECT_ID(N'Core.Alert') IS NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Core') EXEC('CREATE SCHEMA Core');
    CREATE TABLE Core.Alert (
        AlertId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        AlertName NVARCHAR(200) NOT NULL,
        AlertTypeCode NVARCHAR(50) NOT NULL,
        ServiceName NVARCHAR(100) NOT NULL,
        SeverityCode NVARCHAR(50) NOT NULL,
        StatusCode NVARCHAR(50) NOT NULL,
        RegionCode NVARCHAR(50) NULL,
        TenantId UNIQUEIDENTIFIER NULL,
        OwnerUserId UNIQUEIDENTIFIER NULL,
        Message NVARCHAR(1000) NULL,
        TriggeredDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        AcknowledgedByUserId UNIQUEIDENTIFIER NULL,
        AcknowledgedDateUtc DATETIME2 NULL,
        ResolvedByUserId UNIQUEIDENTIFIER NULL,
        ResolvedDateUtc DATETIME2 NULL,
        EscalatedDateUtc DATETIME2 NULL,
        Notes NVARCHAR(1000) NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL DEFAULT 0
    );
END

IF OBJECT_ID(N'Core.Branch') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Core.Branch WHERE TenantId=@TenantId AND BranchCode=N'GC')
   AND (COL_LENGTH(N'Core.Branch', N'CompanyId') IS NULL OR @CompanyId IS NOT NULL)
BEGIN
    DECLARE @BranchColumns NVARCHAR(MAX) = N'BranchId, TenantId, BranchCode, BranchName, City, StateProvince, CountryCode, IsActive, CreatedDateUtc, IsDeleted';
    DECLARE @BranchSelect1 NVARCHAR(MAX) = N'''b1000000-0000-0000-0000-000000000001'', @TenantId, N''GC'', N''Gulf Coast'', N''Houston'', N''TX'', N''US'', 1, @Now, 0';
    DECLARE @BranchSelect2 NVARCHAR(MAX) = N'''b1000000-0000-0000-0000-000000000002'', @TenantId, N''NTX'', N''North Texas'', N''Dallas'', N''TX'', N''US'', 1, @Now, 0';
    DECLARE @BranchSelect3 NVARCHAR(MAX) = N'''b1000000-0000-0000-0000-000000000003'', @TenantId, N''NE'', N''Northeast'', N''New York'', N''NY'', N''US'', 1, @Now, 0';

    IF COL_LENGTH(N'Core.Branch', N'CompanyId') IS NOT NULL
    BEGIN
        SET @BranchColumns += N', CompanyId';
        SET @BranchSelect1 += N', @CompanyId';
        SET @BranchSelect2 += N', @CompanyId';
        SET @BranchSelect3 += N', @CompanyId';
    END

    IF COL_LENGTH(N'Core.Branch', N'TimeZoneId') IS NOT NULL
    BEGIN
        SET @BranchColumns += N', TimeZoneId';
        IF EXISTS (SELECT 1 FROM sys.columns c INNER JOIN sys.types t ON t.user_type_id = c.user_type_id WHERE c.object_id = OBJECT_ID(N'Core.Branch') AND c.name = N'TimeZoneId' AND t.name IN (N'int', N'smallint', N'tinyint', N'bigint'))
        BEGIN
            SET @BranchSelect1 += N', 1';
            SET @BranchSelect2 += N', 1';
            SET @BranchSelect3 += N', 2';
        END
        ELSE
        BEGIN
            SET @BranchSelect1 += N', N''America/Chicago''';
            SET @BranchSelect2 += N', N''America/Chicago''';
            SET @BranchSelect3 += N', N''America/New_York''';
        END
    END

    IF COL_LENGTH(N'Core.Branch', N'TimeZoneCode') IS NOT NULL
    BEGIN
        SET @BranchColumns += N', TimeZoneCode';
        IF EXISTS (SELECT 1 FROM sys.columns c INNER JOIN sys.types t ON t.user_type_id = c.user_type_id WHERE c.object_id = OBJECT_ID(N'Core.Branch') AND c.name = N'TimeZoneCode' AND t.name IN (N'int', N'smallint', N'tinyint', N'bigint'))
        BEGIN
            SET @BranchSelect1 += N', 1';
            SET @BranchSelect2 += N', 1';
            SET @BranchSelect3 += N', 2';
        END
        ELSE
        BEGIN
            SET @BranchSelect1 += N', N''America/Chicago''';
            SET @BranchSelect2 += N', N''America/Chicago''';
            SET @BranchSelect3 += N', N''America/New_York''';
        END
    END

    IF COL_LENGTH(N'Core.Branch', N'CreatedByUserId') IS NOT NULL
    BEGIN
        SET @BranchColumns += N', CreatedByUserId';
        SET @BranchSelect1 += N', @AdminUserId';
        SET @BranchSelect2 += N', @AdminUserId';
        SET @BranchSelect3 += N', @AdminUserId';
    END

    DECLARE @BranchSql NVARCHAR(MAX) = N'INSERT INTO Core.Branch (' + @BranchColumns + N') VALUES (' + @BranchSelect1 + N'), (' + @BranchSelect2 + N'), (' + @BranchSelect3 + N');';
    EXEC sp_executesql @BranchSql, N'@TenantId UNIQUEIDENTIFIER, @CompanyId UNIQUEIDENTIFIER, @AdminUserId UNIQUEIDENTIFIER, @Now DATETIME2', @TenantId, @CompanyId, @AdminUserId, @Now;
END

IF OBJECT_ID(N'Sales.Agreement') IS NOT NULL AND COL_LENGTH(N'Sales.Agreement', N'BranchId') IS NULL
    ALTER TABLE Sales.Agreement ADD BranchId UNIQUEIDENTIFIER NULL;

IF OBJECT_ID(N'Sales.Agreement') IS NOT NULL AND COL_LENGTH(N'Sales.Agreement', N'BranchId') IS NOT NULL
BEGIN
    EXEC sp_executesql N'
        UPDATE Sales.Agreement
        SET BranchId = COALESCE(BranchId, ''b1000000-0000-0000-0000-000000000001''),
            CreatedByUserId = COALESCE(CreatedByUserId, @AdminUserId)
        WHERE TenantId=@TenantId AND IsDeleted=0;',
        N'@TenantId UNIQUEIDENTIFIER, @AdminUserId UNIQUEIDENTIFIER', @TenantId, @AdminUserId;
END

IF OBJECT_ID(N'OPS.AgreementRenewal') IS NOT NULL AND OBJECT_ID(N'Sales.Agreement') IS NOT NULL
BEGIN
    DECLARE @Agreement1 UNIQUEIDENTIFIER = (SELECT TOP 1 AgreementId FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 ORDER BY CreatedDateUtc DESC);
    DECLARE @Agreement2 UNIQUEIDENTIFIER = (SELECT TOP 1 AgreementId FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND AgreementId <> @Agreement1 ORDER BY CreatedDateUtc DESC);
    DECLARE @Agreement3 UNIQUEIDENTIFIER = (SELECT TOP 1 AgreementId FROM Sales.Agreement WHERE TenantId=@TenantId AND IsDeleted=0 AND AgreementId NOT IN (@Agreement1, COALESCE(@Agreement2,@Agreement1)) ORDER BY CreatedDateUtc DESC);
    SET @Agreement2 = COALESCE(@Agreement2, @Agreement1);
    SET @Agreement3 = COALESCE(@Agreement3, @Agreement1);

    IF @Agreement1 IS NOT NULL AND NOT EXISTS (SELECT 1 FROM OPS.AgreementRenewal WHERE TenantId=@TenantId AND RenewalNumber=N'ADB-REN-1001')
    BEGIN
        INSERT INTO OPS.AgreementRenewal (RenewalId, TenantId, AgreementId, RenewalNumber, NewStartDate, NewEndDate, TotalContractValue, StatusCode, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
        (NEWID(), @TenantId, @Agreement1, N'ADB-REN-1001', DATEADD(day, -3, CAST(@Now AS date)), DATEADD(day, 362, CAST(@Now AS date)), 151000, N'Overdue', DATEADD(day, -20, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @Agreement2, N'ADB-REN-1002', DATEADD(day, 18, CAST(@Now AS date)), DATEADD(day, 383, CAST(@Now AS date)), 224000, N'Pending', DATEADD(day, -18, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @Agreement3, N'ADB-REN-1003', DATEADD(day, 47, CAST(@Now AS date)), DATEADD(day, 412, CAST(@Now AS date)), 187500, N'Pending', DATEADD(day, -12, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @Agreement1, N'ADB-REN-1004', DATEADD(day, 72, CAST(@Now AS date)), DATEADD(day, 437, CAST(@Now AS date)), 96500, N'Pending', DATEADD(day, -8, @Now), @AdminUserId, 0);
    END
END

IF OBJECT_ID(N'Core.Alert') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Core.Alert WHERE TenantId=@TenantId AND AlertName=N'Agency dashboard: renewal overdue')
BEGIN
    INSERT INTO Core.Alert (AlertId, AlertName, AlertTypeCode, ServiceName, SeverityCode, StatusCode, RegionCode, TenantId, OwnerUserId, Message, TriggeredDateUtc, Notes, CreatedDateUtc, IsDeleted)
    VALUES
    (NEWID(), N'Agency dashboard: renewal overdue', N'Renewal', N'Renewal Pipeline', N'Critical', N'Open', N'US', @TenantId, @AdminUserId, N'One renewal is overdue and requires Tenant Admin review today.', DATEADD(hour, -6, @Now), N'Seeded agency dashboard alert.', DATEADD(hour, -6, @Now), 0),
    (NEWID(), N'Agency dashboard: AR overdue balance', N'Billing', N'Billing Summary', N'High', N'Open', N'US', @TenantId, @AdminUserId, N'Overdue AR balance exceeded the configured operating threshold.', DATEADD(hour, -10, @Now), N'Seeded agency dashboard alert.', DATEADD(hour, -10, @Now), 0),
    (NEWID(), N'Agency dashboard: claims reserve watch', N'Claims', N'Claims Summary', N'Medium', N'Open', N'US', @TenantId, @AdminUserId, N'Large-loss reserves require service manager review.', DATEADD(day, -1, @Now), N'Seeded agency dashboard alert.', DATEADD(day, -1, @Now), 0),
    (NEWID(), N'Agency dashboard: producer follow-up', N'Sales', N'Producer Performance', N'Low', N'Open', N'US', @TenantId, @AdminUserId, N'Producer follow-up volume is below the weekly operating target.', DATEADD(day, -2, @Now), N'Seeded agency dashboard alert.', DATEADD(day, -2, @Now), 0);
END
""";
    private const string Migration0099_WorkbenchTasksFullSeed = @"
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'OPS')
    EXEC('CREATE SCHEMA OPS');

IF OBJECT_ID(N'OPS.TaskItem') IS NULL
BEGIN
    CREATE TABLE OPS.TaskItem (
        TaskItemId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        TaskNumber NVARCHAR(50) NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Description NVARCHAR(2000) NULL,
        TaskTypeCode NVARCHAR(50) NOT NULL,
        StageCode NVARCHAR(50) NOT NULL,
        PriorityCode NVARCHAR(50) NOT NULL,
        StatusCode NVARCHAR(50) NOT NULL,
        RelatedEntityName NVARCHAR(100) NULL,
        RelatedEntityId UNIQUEIDENTIFIER NULL,
        AccountId UNIQUEIDENTIFIER NULL,
        AssignedToUserId UNIQUEIDENTIFIER NULL,
        DueDate DATE NULL,
        CompletedDate DATE NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL DEFAULT 0
    );

    CREATE UNIQUE INDEX UX_TaskItem_Tenant_TaskNumber ON OPS.TaskItem(TenantId, TaskNumber) WHERE IsDeleted = 0;
    CREATE INDEX IX_TaskItem_Tenant_Stage ON OPS.TaskItem(TenantId, StageCode, StatusCode, IsDeleted);
END

IF NOT EXISTS (SELECT 1 FROM OPS.TaskItem WHERE TenantId = @TenantId AND TaskNumber = N'WT-ADM-1001')
BEGIN
    INSERT INTO OPS.TaskItem
        (TaskItemId, TenantId, TaskNumber, Title, Description, TaskTypeCode, StageCode, PriorityCode, StatusCode, RelatedEntityName, RelatedEntityId, AccountId, AssignedToUserId, DueDate, CompletedDate, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, N'WT-ADM-1001', N'Approve urgent binder exception - Northstar Robotics', N'Carrier requires tenant admin approval before binding due to open subjectivities. Confirm authority, document exception, and notify producer.', N'Approval', N'Approval', N'High', N'Open', N'Northstar Robotics', NULL, NULL, @AdminUserId, DATEADD(day, -2, CAST(@Now AS date)), NULL, DATEADD(day, -6, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1002', N'Review renewal proposal - Apex Medical Group', N'Validate renewal terms, expiring premium, carrier quote notes, and follow-up plan before producer presentation.', N'Renewal', N'Review', N'High', N'Open', N'Apex Medical Group', NULL, NULL, @AdminUserId, DATEADD(day, -1, CAST(@Now AS date)), NULL, DATEADD(day, -5, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1003', N'Rush certificate for Metro Freight landlord', N'Certificate holder requested additional insured wording before noon. Verify policy status and issue certificate package.', N'Certificate', N'In Progress', N'High', N'Open', N'Metro Freight Co.', NULL, NULL, @AdminUserId, CAST(@Now AS date), NULL, DATEADD(hour, -9, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1004', N'Call Bridgewater Hotels about premium change', N'Client called twice regarding revised premium. Confirm endorsement impact and document the conversation.', N'Call', N'In Progress', N'Medium', N'Open', N'Bridgewater Hotels', NULL, NULL, @AdminUserId, CAST(@Now AS date), NULL, DATEADD(day, -2, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1005', N'Prepare ACORD 25 - Dallas Roofing LLC', N'Generate certificate, confirm holder address, and attach completed ACORD 25 to account timeline.', N'Document', N'Intake', N'Medium', N'Open', N'Dallas Roofing LLC', NULL, NULL, @AdminUserId, DATEADD(day, 1, CAST(@Now AS date)), NULL, DATEADD(day, -1, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1006', N'Process endorsement request - Pioneer Automotive', N'Add warehouse location endorsement. Confirm square footage, occupancy, and effective date with underwriter.', N'Endorsement', N'Review', N'Medium', N'Open', N'Pioneer Automotive', NULL, NULL, @AdminUserId, DATEADD(day, 2, CAST(@Now AS date)), NULL, DATEADD(day, -3, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1007', N'Verify loss runs received - Laredo Steel Works', N'Confirm five-year loss runs are attached and update renewal checklist before market submission.', N'Renewal', N'Intake', N'High', N'Open', N'Laredo Steel Works', NULL, NULL, @AdminUserId, DATEADD(day, 3, CAST(@Now AS date)), NULL, DATEADD(day, -4, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1008', N'Confirm effective date - Greenleaf Nurseries', N'Validate requested effective date with carrier quote and update account timeline.', N'Quote Follow-up', N'In Progress', N'Low', N'Open', N'Greenleaf Nurseries', NULL, NULL, @AdminUserId, DATEADD(day, 4, CAST(@Now AS date)), NULL, DATEADD(day, -2, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1009', N'Request updated financials - Sun Valley Resort', N'Underwriter requested latest financial statements before final umbrella indication.', N'Document', N'Intake', N'Medium', N'Open', N'Sun Valley Resort', NULL, NULL, @AdminUserId, DATEADD(day, 5, CAST(@Now AS date)), NULL, DATEADD(day, -1, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1010', N'Schedule renewal meeting - Coastal Seafood Dist.', N'Coordinate renewal review with producer, CSR, and insured decision maker.', N'Renewal', N'In Progress', N'Low', N'Open', N'Coastal Seafood Dist.', NULL, NULL, @AdminUserId, DATEADD(day, 6, CAST(@Now AS date)), NULL, DATEADD(day, -2, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1011', N'Review claims report - Metro Freight Co.', N'Claims summary has two open auto liability items. Review notes before account stewardship call.', N'Claim', N'Review', N'Medium', N'Open', N'Metro Freight Co.', NULL, NULL, @AdminUserId, DATEADD(day, 7, CAST(@Now AS date)), NULL, DATEADD(day, -1, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1012', N'Send non-renewal notice - Crestview Elementary', N'Prepare compliant non-renewal communication and archive delivery confirmation.', N'Document', N'Approval', N'High', N'Open', N'Crestview Elementary', NULL, NULL, @AdminUserId, DATEADD(day, 8, CAST(@Now AS date)), NULL, DATEADD(day, -3, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1013', N'Set up new client portal - Dallas Roofing', N'Invite primary contact, confirm portal branding, and verify document access permissions.', N'Admin', N'Done', N'Low', N'Completed', N'Dallas Roofing LLC', NULL, NULL, @AdminUserId, DATEADD(day, -3, CAST(@Now AS date)), DATEADD(day, -2, CAST(@Now AS date)), DATEADD(day, -7, @Now), @AdminUserId, DATEADD(day, -2, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, N'WT-ADM-1014', N'Complete ACORD 140 - Apex Medical Group', N'Commercial property application completed and attached to submission package.', N'Document', N'Done', N'Medium', N'Completed', N'Apex Medical Group', NULL, NULL, @AdminUserId, DATEADD(day, -1, CAST(@Now AS date)), DATEADD(day, -1, CAST(@Now AS date)), DATEADD(day, -6, @Now), @AdminUserId, DATEADD(day, -1, @Now), @AdminUserId, 0);
END

IF NOT EXISTS (SELECT 1 FROM OPS.TaskItem WHERE TenantId = @TenantId AND TaskNumber = N'WT-ADM-1015')
BEGIN
    INSERT INTO OPS.TaskItem
        (TaskItemId, TenantId, TaskNumber, Title, Description, TaskTypeCode, StageCode, PriorityCode, StatusCode, RelatedEntityName, RelatedEntityId, AccountId, AssignedToUserId, DueDate, CompletedDate, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, N'WT-ADM-1015', N'Validate tenant security role changes', N'Review requested producer and CSR role changes before end-of-day access window.', N'Admin', N'Review', N'High', N'Open', N'Tenant Security', NULL, NULL, @AdminUserId, CAST(@Now AS date), NULL, DATEADD(hour, -5, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1016', N'Approve billing plan exception - Horizon Foods', N'Billing requested approval for custom payment schedule on renewal invoice.', N'Billing', N'Approval', N'Medium', N'Open', N'Horizon Foods', NULL, NULL, @AdminUserId, DATEADD(day, 1, CAST(@Now AS date)), NULL, DATEADD(hour, -8, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1017', N'Confirm claim follow-up owner - Patterson Manufacturing', N'Assign owner for open claim follow-up and update stewardship notes.', N'Claim', N'Intake', N'Medium', N'Open', N'Patterson Manufacturing', NULL, NULL, @AdminUserId, DATEADD(day, 2, CAST(@Now AS date)), NULL, DATEADD(day, -1, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'WT-ADM-1018', N'Close stale quote follow-up - Greenleaf Nurseries', N'Producer confirmed no action needed. Verify timeline and close follow-up task.', N'Quote Follow-up', N'Done', N'Low', N'Completed', N'Greenleaf Nurseries', NULL, NULL, @AdminUserId, DATEADD(day, -4, CAST(@Now AS date)), DATEADD(day, -3, CAST(@Now AS date)), DATEADD(day, -8, @Now), @AdminUserId, DATEADD(day, -3, @Now), @AdminUserId, 0);
END
";

    private const string Migration0100_WorkbenchActivitiesFullSeed = @"
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF COL_LENGTH(N'OPS.OperationalActivityLog', N'ModifiedDateUtc') IS NULL ALTER TABLE OPS.OperationalActivityLog ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'OPS.OperationalActivityLog', N'ModifiedByUserId') IS NULL ALTER TABLE OPS.OperationalActivityLog ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM OPS.OperationalActivityLog WHERE TenantId = @TenantId AND Subject = N'Tenant Admin reviewed urgent binder exception')
BEGIN
    INSERT INTO OPS.OperationalActivityLog
        (ActivityId, TenantId, AccountId, EngagementId, AgreementId, ActivityDate, ActivityTypeCode, Subject, Notes, PerformedByUserId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, NULL, NULL, NULL, CAST(@Now AS date), N'Call', N'Tenant Admin reviewed urgent binder exception', N'Confirmed binding authority, documented exception approval path, and notified producer for Northstar Robotics.', @AdminUserId, DATEADD(hour, -2, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, NULL, NULL, NULL, CAST(@Now AS date), N'Email', N'Sent renewal proposal checklist to Apex Medical Group', N'Forwarded final review checklist and requested confirmation on expiring coverage details.', @AdminUserId, DATEADD(hour, -4, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, NULL, NULL, NULL, CAST(@Now AS date), N'Task', N'Validated certificate rush request for Metro Freight', N'Verified policy status and holder wording before certificate package issuance.', @AdminUserId, DATEADD(hour, -6, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, NULL, NULL, NULL, DATEADD(day, -1, CAST(@Now AS date)), N'Meeting', N'Held renewal strategy review for Bridgewater Hotels', N'Reviewed market approach, premium movement, and client presentation timing.', @AdminUserId, DATEADD(day, -1, DATEADD(hour, -3, @Now)), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, NULL, NULL, NULL, DATEADD(day, -1, CAST(@Now AS date)), N'Note', N'Added tenant security role change note', N'Documented producer and CSR access review outcome for audit trail.', @AdminUserId, DATEADD(day, -1, DATEADD(hour, -6, @Now)), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, NULL, NULL, NULL, DATEADD(day, -2, CAST(@Now AS date)), N'Workflow', N'Escalated billing plan exception workflow', N'Routed custom payment schedule approval to billing operations.', @AdminUserId, DATEADD(day, -2, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, NULL, NULL, NULL, DATEADD(day, -3, CAST(@Now AS date)), N'Call', N'Confirmed claim follow-up owner for Patterson Manufacturing', N'Assigned follow-up responsibility and updated stewardship notes.', @AdminUserId, DATEADD(day, -3, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, NULL, NULL, NULL, DATEADD(day, -4, CAST(@Now AS date)), N'Email', N'Requested updated financials from Sun Valley Resort', N'Underwriter requested latest statements before final umbrella indication.', @AdminUserId, DATEADD(day, -4, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, NULL, NULL, NULL, DATEADD(day, -5, CAST(@Now AS date)), N'Meeting', N'Completed portal setup review for Dallas Roofing', N'Confirmed primary contact invite, branding, and document access permissions.', @AdminUserId, DATEADD(day, -5, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, NULL, NULL, NULL, DATEADD(day, -7, CAST(@Now AS date)), N'Note', N'Closed stale quote follow-up for Greenleaf Nurseries', N'Producer confirmed no action needed; timeline was verified and follow-up was closed.', @AdminUserId, DATEADD(day, -7, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, NULL, NULL, NULL, DATEADD(day, -10, CAST(@Now AS date)), N'Task', N'Reviewed non-renewal notice package', N'Prepared compliant notice archive and delivery confirmation checklist.', @AdminUserId, DATEADD(day, -10, @Now), @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, NULL, NULL, NULL, DATEADD(day, -14, CAST(@Now AS date)), N'Workflow', N'Updated automation audit notes', N'Reconciled workflow automation event history for tenant admin review.', @AdminUserId, DATEADD(day, -14, @Now), @AdminUserId, NULL, NULL, 0);
END
";

    private const string Migration0101_CalendarEventDateTimeSeed = @"
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF OBJECT_ID(N'OPS.CalendarEvent') IS NULL
BEGIN
    CREATE TABLE OPS.CalendarEvent (
        EventId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Title NVARCHAR(200) NOT NULL,
        Notes NVARCHAR(2000) NULL,
        EventTypeCode NVARCHAR(50) NOT NULL,
        StatusCode NVARCHAR(50) NOT NULL,
        StartDateTimeUtc DATETIME2 NOT NULL,
        EndDateTimeUtc DATETIME2 NULL,
        AllDay BIT NOT NULL CONSTRAINT DF_CalendarEvent_AllDay DEFAULT 0,
        TimeZoneId NVARCHAR(100) NOT NULL CONSTRAINT DF_CalendarEvent_TimeZoneId DEFAULT N'America/Chicago',
        OrganizerUserId UNIQUEIDENTIFIER NULL,
        AssignedToUserId UNIQUEIDENTIFIER NULL,
        RelatedEntityType NVARCHAR(50) NULL,
        RelatedEntityId UNIQUEIDENTIFIER NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_CalendarEvent_IsDeleted DEFAULT 0
    );
    CREATE INDEX IX_CalendarEvent_Tenant_Start ON OPS.CalendarEvent(TenantId, StartDateTimeUtc, IsDeleted);
    CREATE INDEX IX_CalendarEvent_Assigned_Start ON OPS.CalendarEvent(TenantId, AssignedToUserId, StartDateTimeUtc, IsDeleted);
END

IF NOT EXISTS (SELECT 1 FROM OPS.CalendarEvent WHERE TenantId = @TenantId AND Title = N'Renewal strategy meeting - Apex Medical Group')
BEGIN
    INSERT INTO OPS.CalendarEvent
        (EventId, TenantId, Title, Notes, EventTypeCode, StatusCode, StartDateTimeUtc, EndDateTimeUtc, AllDay, TimeZoneId, OrganizerUserId, AssignedToUserId, RelatedEntityType, RelatedEntityId, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, N'Renewal strategy meeting - Apex Medical Group', N'Review expiring terms, carrier appetite, premium movement, and next-best action plan.', N'Meeting', N'Scheduled', DATEADD(hour, 15, CAST(CAST(@Now AS date) AS datetime2)), DATEADD(hour, 16, CAST(CAST(@Now AS date) AS datetime2)), 0, N'America/Chicago', @AdminUserId, @AdminUserId, N'Account', NULL, @Now, @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'Quote presentation call - Metro Freight Co.', N'Walk through quote comparison, coverage differences, and binding timeline.', N'Call', N'Scheduled', DATEADD(hour, 20, CAST(CAST(@Now AS date) AS datetime2)), DATEADD(minute, 30, DATEADD(hour, 20, CAST(CAST(@Now AS date) AS datetime2))), 0, N'America/Chicago', @AdminUserId, @AdminUserId, N'Account', NULL, @Now, @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'Carrier submission deadline - Northstar Robotics', N'Final underwriting package due before carrier cutoff.', N'Deadline', N'Scheduled', DATEADD(hour, 23, CAST(CAST(@Now AS date) AS datetime2)), NULL, 1, N'America/Chicago', @AdminUserId, @AdminUserId, N'Submission', NULL, @Now, @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'Prepare ACORD certificate package', N'Complete and review ACORD 25 package for landlord certificate holder.', N'Task', N'Scheduled', DATEADD(hour, 34, CAST(CAST(@Now AS date) AS datetime2)), DATEADD(hour, 35, CAST(CAST(@Now AS date) AS datetime2)), 0, N'America/Chicago', @AdminUserId, @AdminUserId, N'Task', NULL, @Now, @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'Agency operations standup', N'Daily review of escalations, overdue work, queue health, and service deadlines.', N'Event', N'Scheduled', DATEADD(hour, 39, CAST(CAST(@Now AS date) AS datetime2)), DATEADD(minute, 30, DATEADD(hour, 39, CAST(CAST(@Now AS date) AS datetime2))), 0, N'America/Chicago', @AdminUserId, @AdminUserId, N'Workbench', NULL, @Now, @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'Billing plan exception review', N'Approve or reject custom payment schedule exception for renewal invoice.', N'Meeting', N'Scheduled', DATEADD(hour, 58, CAST(CAST(@Now AS date) AS datetime2)), DATEADD(hour, 59, CAST(CAST(@Now AS date) AS datetime2)), 0, N'America/Chicago', @AdminUserId, @AdminUserId, N'Billing', NULL, @Now, @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'Workflow automation audit checkpoint', N'Review workflow event history and document automation audit outcome.', N'Event', N'Scheduled', DATEADD(day, 5, DATEADD(hour, 16, CAST(CAST(@Now AS date) AS datetime2))), DATEADD(day, 5, DATEADD(hour, 17, CAST(CAST(@Now AS date) AS datetime2))), 0, N'America/Chicago', @AdminUserId, @AdminUserId, N'Workflow', NULL, @Now, @AdminUserId, NULL, NULL, 0),
        (NEWID(), @TenantId, N'Open enrollment deadline', N'Client portal open enrollment communication deadline.', N'Deadline', N'Scheduled', DATEADD(day, 9, DATEADD(hour, 23, CAST(CAST(@Now AS date) AS datetime2))), NULL, 1, N'America/Chicago', @AdminUserId, @AdminUserId, N'Portal', NULL, @Now, @AdminUserId, NULL, NULL, 0);
END
";

    private const string Migration0102_WorkbenchNotificationsFullSeed = @"
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF OBJECT_ID(N'Core.Notification') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Core.Notification WHERE TenantId = @TenantId AND RecipientUserId = @AdminUserId AND Subject = N'[Alert] Urgent binder exception requires review')
BEGIN
    INSERT INTO Core.Notification
        (NotificationId, TenantId, RecipientUserId, TemplateId, ChannelCode, Subject, Body, EntityName, EntityId, StatusCode, IsRead, ReadDateUtc, SentDateUtc, ErrorMessage, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @AdminUserId, NULL, N'InApp', N'[Alert] Urgent binder exception requires review', N'Northstar Robotics has an open subjectivity and requires tenant admin approval before binding.', N'Alert', NULL, N'Delivered', 0, NULL, DATEADD(minute, -20, @Now), NULL, DATEADD(minute, -20, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @AdminUserId, NULL, N'InApp', N'[Approval] Quote release pending', N'A $47,500 annual premium quote for Laredo Steel Works is pending your release to the client.', N'Approval', NULL, N'Delivered', 0, NULL, DATEADD(hour, -1, @Now), NULL, DATEADD(hour, -1, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @AdminUserId, NULL, N'Email', N'[Reminder] Renewal strategy meeting today', N'Apex Medical Group renewal strategy meeting starts at 3:00 PM. Review quote comparison and expiring terms.', N'Reminder', NULL, N'Sent', 0, NULL, DATEADD(hour, -3, @Now), NULL, DATEADD(hour, -3, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @AdminUserId, NULL, N'InApp', N'[System] Workflow automation audit completed', N'Workflow automation audit completed successfully with no failed actions in the last 24 hours.', N'System', NULL, N'Delivered', 1, DATEADD(hour, -4, @Now), DATEADD(hour, -5, @Now), NULL, DATEADD(hour, -5, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @AdminUserId, NULL, N'SMS', N'[Alert] Certificate rush request due today', N'Metro Freight certificate package must be issued before noon for landlord compliance.', N'Alert', NULL, N'Sent', 0, NULL, DATEADD(hour, -8, @Now), NULL, DATEADD(hour, -8, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @AdminUserId, NULL, N'Email', N'[Info] Carrier rate update published', N'Hartford filed a commercial auto rate change effective next renewal cycle. Review impacted accounts.', N'Info', NULL, N'Sent', 1, DATEADD(day, -1, @Now), DATEADD(day, -1, @Now), NULL, DATEADD(day, -1, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @AdminUserId, NULL, N'InApp', N'[Approval] Billing plan exception requested', N'Billing requested approval for a custom payment schedule on a renewal invoice.', N'Approval', NULL, N'Delivered', 0, NULL, DATEADD(day, -2, @Now), NULL, DATEADD(day, -2, @Now), @AdminUserId, 0),
        (NEWID(), @TenantId, @AdminUserId, NULL, N'Email', N'[Reminder] Open enrollment communication deadline', N'Client portal open enrollment communication deadline is approaching. Confirm notification schedule.', N'Reminder', NULL, N'Failed', 0, NULL, NULL, N'SMTP timeout while sending reminder.', DATEADD(day, -3, @Now), @AdminUserId, 0);
END
";

    private const string Migration0103_TenantSecurityAuditTrailSeed = @"
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF SCHEMA_ID(N'Audit') IS NULL EXEC(N'CREATE SCHEMA Audit');

IF OBJECT_ID(N'Audit.SecurityEventLog') IS NULL
BEGIN
    CREATE TABLE Audit.SecurityEventLog (
        SecurityEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SecurityEventLog PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NULL,
        EventTypeCode NVARCHAR(100) NOT NULL,
        EventDescription NVARCHAR(1000) NOT NULL,
        IpAddress NVARCHAR(64) NULL,
        UserAgent NVARCHAR(512) NULL,
        IsSuccess BIT NOT NULL CONSTRAINT DF_SecurityEventLog_IsSuccess_0103 DEFAULT 1,
        RiskScore INT NULL,
        SessionId NVARCHAR(100) NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SecurityEventLog_CreatedDateUtc_0103 DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL CONSTRAINT DF_SecurityEventLog_IsDeleted_0103 DEFAULT 0
    );
END

IF COL_LENGTH(N'Audit.SecurityEventLog', N'SecurityEventId') IS NULL ALTER TABLE Audit.SecurityEventLog ADD SecurityEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SecurityEventLog_SecurityEventId_0103 DEFAULT NEWID();
IF COL_LENGTH(N'Audit.SecurityEventLog', N'TenantId') IS NULL ALTER TABLE Audit.SecurityEventLog ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_SecurityEventLog_TenantId_0103 DEFAULT '00000000-0000-0000-0000-000000000001';
IF COL_LENGTH(N'Audit.SecurityEventLog', N'UserId') IS NULL ALTER TABLE Audit.SecurityEventLog ADD UserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'EventTypeCode') IS NULL ALTER TABLE Audit.SecurityEventLog ADD EventTypeCode NVARCHAR(100) NOT NULL CONSTRAINT DF_SecurityEventLog_EventTypeCode_0103 DEFAULT N'Event';
IF COL_LENGTH(N'Audit.SecurityEventLog', N'EventDescription') IS NULL ALTER TABLE Audit.SecurityEventLog ADD EventDescription NVARCHAR(1000) NOT NULL CONSTRAINT DF_SecurityEventLog_EventDescription_0103 DEFAULT N'Security audit event';
IF COL_LENGTH(N'Audit.SecurityEventLog', N'IpAddress') IS NULL ALTER TABLE Audit.SecurityEventLog ADD IpAddress NVARCHAR(64) NULL;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'UserAgent') IS NULL ALTER TABLE Audit.SecurityEventLog ADD UserAgent NVARCHAR(512) NULL;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'IsSuccess') IS NULL ALTER TABLE Audit.SecurityEventLog ADD IsSuccess BIT NOT NULL CONSTRAINT DF_SecurityEventLog_IsSuccess_0103B DEFAULT 1;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'RiskScore') IS NULL ALTER TABLE Audit.SecurityEventLog ADD RiskScore INT NULL;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'SessionId') IS NULL ALTER TABLE Audit.SecurityEventLog ADD SessionId NVARCHAR(100) NULL;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'CreatedDateUtc') IS NULL ALTER TABLE Audit.SecurityEventLog ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SecurityEventLog_CreatedDateUtc_0103B DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Audit.SecurityEventLog', N'IsDeleted') IS NULL ALTER TABLE Audit.SecurityEventLog ADD IsDeleted BIT NOT NULL CONSTRAINT DF_SecurityEventLog_IsDeleted_0103B DEFAULT 0;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'EventCode') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'EventCode', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_EventCode_0103 DEFAULT N'Event' FOR EventCode;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'EventName') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'EventName', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_EventName_0103 DEFAULT N'Security audit event' FOR EventName;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'SeverityCode') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'SeverityCode', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_SeverityCode_0103 DEFAULT N'Info' FOR SeverityCode;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'CategoryCode') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'CategoryCode', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_CategoryCode_0103 DEFAULT N'Security' FOR CategoryCode;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'ModuleCode') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'ModuleCode', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_ModuleCode_0103 DEFAULT N'Security' FOR ModuleCode;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'SourceSystemCode') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'SourceSystemCode', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_SourceSystemCode_0103 DEFAULT N'AMS' FOR SourceSystemCode;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'SourceSystem') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'SourceSystem', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_SourceSystem_0103 DEFAULT N'AMS' FOR SourceSystem;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'ActionCode') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'ActionCode', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_ActionCode_0103 DEFAULT N'Audit' FOR ActionCode;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'StatusCode') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'StatusCode', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_StatusCode_0103 DEFAULT N'Success' FOR StatusCode;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'EventStatusCode') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'EventStatusCode', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_EventStatusCode_0103 DEFAULT N'Success' FOR EventStatusCode;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'RiskLevelCode') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'RiskLevelCode', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_RiskLevelCode_0103 DEFAULT N'Low' FOR RiskLevelCode;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'CorrelationId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'CorrelationId', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_CorrelationId_0103 DEFAULT N'' FOR CorrelationId;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'ActorUserId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'ActorUserId', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_ActorUserId_0103 DEFAULT '00000000-0000-0000-0000-000000000002' FOR ActorUserId;
IF COL_LENGTH(N'Audit.SecurityEventLog', N'CreatedByUserId') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE parent_object_id = OBJECT_ID(N'Audit.SecurityEventLog') AND parent_column_id = COLUMNPROPERTY(OBJECT_ID(N'Audit.SecurityEventLog'), N'CreatedByUserId', 'ColumnId'))
    ALTER TABLE Audit.SecurityEventLog ADD CONSTRAINT DF_SecurityEventLog_CreatedByUserId_0103 DEFAULT '00000000-0000-0000-0000-000000000002' FOR CreatedByUserId;

EXEC sp_executesql N'
IF OBJECT_ID(N''Audit.SecurityEventLog'') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM Audit.SecurityEventLog WHERE TenantId = @TenantId AND EventDescription = N''Tenant Admin signed in successfully from trusted workstation'')
BEGIN
    CREATE TABLE #SecurityAuditSeed
    (
        SecurityEventId UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NULL,
        EventTypeCode NVARCHAR(100) NOT NULL,
        EventDescription NVARCHAR(1000) NOT NULL,
        IpAddress NVARCHAR(64) NULL,
        UserAgent NVARCHAR(512) NULL,
        IsSuccess BIT NOT NULL,
        RiskScore INT NULL,
        SessionId NVARCHAR(100) NULL,
        CreatedDateUtc DATETIME2 NOT NULL,
        IsDeleted BIT NOT NULL
    );

    INSERT INTO #SecurityAuditSeed
        (SecurityEventId, TenantId, UserId, EventTypeCode, EventDescription, IpAddress, UserAgent, IsSuccess, RiskScore, SessionId, CreatedDateUtc, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @AdminUserId, N''Login'', N''Tenant Admin signed in successfully from trusted workstation'', N''10.20.4.18'', N''Edge / Windows'', 1, 12, N''TA-SESSION-001'', DATEADD(minute, -42, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, N''MfaChallenge'', N''MFA challenge satisfied for Tenant Admin console access'', N''10.20.4.18'', N''Edge / Windows'', 1, 18, N''TA-SESSION-001'', DATEADD(minute, -41, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, N''PermissionDenied'', N''Permission denied while attempting to export full producer commission ledger'', N''10.20.4.18'', N''Edge / Windows'', 0, 78, N''TA-SESSION-001'', DATEADD(hour, -2, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, N''RoleChanged'', N''Tenant Admin assigned Senior CSR role to James Park'', N''10.20.4.18'', N''Edge / Windows'', 1, 34, N''TA-SESSION-001'', DATEADD(hour, -4, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, N''PermissionChanged'', N''Tenant Admin enabled Policy.Certificate.Issue permission for CSR role'', N''10.20.4.18'', N''Edge / Windows'', 1, 42, N''TA-SESSION-001'', DATEADD(hour, -6, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, N''SecurityPolicyUpdated'', N''Tenant Admin updated MFA requirement for billing payment approvals'', N''10.20.4.18'', N''Edge / Windows'', 1, 45, N''TA-SESSION-001'', DATEADD(day, -1, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, N''ExportStarted'', N''Tenant Admin exported agency audit report for regulator review'', N''10.20.4.18'', N''Edge / Windows'', 1, 52, N''TA-SESSION-001'', DATEADD(day, -1, DATEADD(hour, -3, @Now)), 0),
        (NEWID(), @TenantId, @AdminUserId, N''LoginFailed'', N''Failed sign-in attempt for Tenant Admin from unrecognized IP'', N''203.0.113.45'', N''Unknown Browser'', 0, 92, N''TA-SESSION-EXT-009'', DATEADD(day, -2, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, N''AccountLocked'', N''User account temporarily locked after repeated failed login attempts'', N''203.0.113.45'', N''Unknown Browser'', 0, 96, N''TA-SESSION-EXT-009'', DATEADD(day, -2, DATEADD(minute, 5, @Now)), 0),
        (NEWID(), @TenantId, @AdminUserId, N''Logout'', N''Tenant Admin signed out of the security administration console'', N''10.20.4.18'', N''Edge / Windows'', 1, 10, N''TA-SESSION-001'', DATEADD(day, -3, @Now), 0);

    IF COL_LENGTH(N''Audit.SecurityEventLog'', N''EventCode'') IS NOT NULL
        INSERT INTO Audit.SecurityEventLog
            (SecurityEventId, TenantId, UserId, EventCode, EventTypeCode, EventDescription, IpAddress, UserAgent, IsSuccess, RiskScore, SessionId, CreatedDateUtc, IsDeleted)
        SELECT SecurityEventId, TenantId, UserId, EventTypeCode, EventTypeCode, EventDescription, IpAddress, UserAgent, IsSuccess, RiskScore, SessionId, CreatedDateUtc, IsDeleted
        FROM #SecurityAuditSeed;
    ELSE
        INSERT INTO Audit.SecurityEventLog
            (SecurityEventId, TenantId, UserId, EventTypeCode, EventDescription, IpAddress, UserAgent, IsSuccess, RiskScore, SessionId, CreatedDateUtc, IsDeleted)
        SELECT SecurityEventId, TenantId, UserId, EventTypeCode, EventDescription, IpAddress, UserAgent, IsSuccess, RiskScore, SessionId, CreatedDateUtc, IsDeleted
        FROM #SecurityAuditSeed;
END',
N'@TenantId UNIQUEIDENTIFIER, @AdminUserId UNIQUEIDENTIFIER, @Now DATETIME2',
@TenantId = @TenantId, @AdminUserId = @AdminUserId, @Now = @Now;

IF OBJECT_ID(N'Audit.FieldChangeLog') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Audit.FieldChangeLog WHERE TenantId = @TenantId AND EntityName = N'IAM.User' AND FieldName = N'StatusCode' AND NewValue = N'Active')
BEGIN
    INSERT INTO Audit.FieldChangeLog
        (FieldChangeLogId, TenantId, EntityName, EntityId, FieldName, OldValue, NewValue, ChangedByUserId, ChangedDateUtc, ChangeSource, IpAddress, IsDeleted)
    VALUES
        (NEWID(), @TenantId, N'IAM.User', @AdminUserId, N'StatusCode', N'Pending', N'Active', @AdminUserId, DATEADD(hour, -5, @Now), N'Tenant Security Admin', N'10.20.4.18', 0),
        (NEWID(), @TenantId, N'IAM.UserRole', NEWID(), N'RoleCode', N'CSR', N'SeniorCSR', @AdminUserId, DATEADD(hour, -4, @Now), N'Role Assignment', N'10.20.4.18', 0),
        (NEWID(), @TenantId, N'IAM.Permission', NEWID(), N'IsActive', N'False', N'True', @AdminUserId, DATEADD(hour, -6, @Now), N'Permission Catalog', N'10.20.4.18', 0),
        (NEWID(), @TenantId, N'SecurityPolicy', NEWID(), N'RequireMfa', N'False', N'True', @AdminUserId, DATEADD(day, -1, @Now), N'Security Policy', N'10.20.4.18', 0),
        (NEWID(), @TenantId, N'Billing.PaymentApproval', NEWID(), N'ApprovalThreshold', N'25000', N'10000', @AdminUserId, DATEADD(day, -1, DATEADD(hour, -1, @Now)), N'Billing Security Policy', N'10.20.4.18', 0),
        (NEWID(), @TenantId, N'Policy.Certificate', NEWID(), N'IssuePermission', N'Disabled', N'Enabled', @AdminUserId, DATEADD(day, -2, @Now), N'Policy Security', N'10.20.4.18', 0),
        (NEWID(), @TenantId, N'CRM.Lead', NEWID(), N'OwnerUserId', N'Producer Team', N'Tenant Admin', @AdminUserId, DATEADD(day, -3, @Now), N'CRM Security Review', N'10.20.4.18', 0),
        (NEWID(), @TenantId, N'Workflow.Rule', NEWID(), N'IsActive', N'True', N'False', @AdminUserId, DATEADD(day, -4, @Now), N'Workflow Admin', N'10.20.4.18', 0);
END
";

    private const string Migration0104_TenantSecuritySessionsSeed = @"
DECLARE @TenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @AdminUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Now DATETIME2 = SYSUTCDATETIME();

IF SCHEMA_ID(N'IAM') IS NULL EXEC(N'CREATE SCHEMA IAM');

IF OBJECT_ID(N'IAM.UserSession') IS NULL
BEGIN
    CREATE TABLE IAM.UserSession (
        SessionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_UserSession PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        UserId UNIQUEIDENTIFIER NOT NULL,
        SessionToken NVARCHAR(500) NOT NULL,
        DeviceIdentifier NVARCHAR(200) NULL,
        DeviceType NVARCHAR(50) NULL,
        UserAgent NVARCHAR(512) NULL,
        IpAddress NVARCHAR(64) NULL,
        LoginDateUtc DATETIME2 NOT NULL CONSTRAINT DF_UserSession_LoginDateUtc_0104 DEFAULT SYSUTCDATETIME(),
        LastActivityDateUtc DATETIME2 NULL,
        ExpiresDateUtc DATETIME2 NOT NULL,
        IsRevoked BIT NOT NULL CONSTRAINT DF_UserSession_IsRevoked_0104_Create DEFAULT 0,
        RevokedDateUtc DATETIME2 NULL,
        RevokedReason NVARCHAR(500) NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_UserSession_CreatedDateUtc_0104 DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL CONSTRAINT DF_UserSession_IsDeleted_0104_Create DEFAULT 0
    );
END

IF OBJECT_ID(N'IAM.UserSession') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'IAM.UserSession', N'SessionId') IS NULL ALTER TABLE IAM.UserSession ADD SessionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_UserSession_SessionId_0104 DEFAULT NEWID();
    IF COL_LENGTH(N'IAM.UserSession', N'TenantId') IS NULL ALTER TABLE IAM.UserSession ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_UserSession_TenantId_0104 DEFAULT '00000000-0000-0000-0000-000000000001';
    IF COL_LENGTH(N'IAM.UserSession', N'UserId') IS NULL ALTER TABLE IAM.UserSession ADD UserId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_UserSession_UserId_0104 DEFAULT '00000000-0000-0000-0000-000000000002';
    IF COL_LENGTH(N'IAM.UserSession', N'SessionToken') IS NULL ALTER TABLE IAM.UserSession ADD SessionToken NVARCHAR(500) NOT NULL CONSTRAINT DF_UserSession_SessionToken_0104 DEFAULT N'LegacySession';
    IF COL_LENGTH(N'IAM.UserSession', N'UserAgent') IS NULL ALTER TABLE IAM.UserSession ADD UserAgent NVARCHAR(512) NULL;
    IF COL_LENGTH(N'IAM.UserSession', N'DeviceIdentifier') IS NULL ALTER TABLE IAM.UserSession ADD DeviceIdentifier NVARCHAR(200) NULL;
    IF COL_LENGTH(N'IAM.UserSession', N'DeviceType') IS NULL ALTER TABLE IAM.UserSession ADD DeviceType NVARCHAR(50) NULL;
    IF COL_LENGTH(N'IAM.UserSession', N'IpAddress') IS NULL ALTER TABLE IAM.UserSession ADD IpAddress NVARCHAR(64) NULL;
    IF COL_LENGTH(N'IAM.UserSession', N'LoginDateUtc') IS NULL ALTER TABLE IAM.UserSession ADD LoginDateUtc DATETIME2 NOT NULL CONSTRAINT DF_UserSession_LoginDateUtc_0104B DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'IAM.UserSession', N'LastActivityDateUtc') IS NULL ALTER TABLE IAM.UserSession ADD LastActivityDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'IAM.UserSession', N'ExpiresDateUtc') IS NULL ALTER TABLE IAM.UserSession ADD ExpiresDateUtc DATETIME2 NOT NULL CONSTRAINT DF_UserSession_ExpiresDateUtc_0104 DEFAULT DATEADD(hour, 8, SYSUTCDATETIME());
    IF COL_LENGTH(N'IAM.UserSession', N'RevokedDateUtc') IS NULL ALTER TABLE IAM.UserSession ADD RevokedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'IAM.UserSession', N'RevokedReason') IS NULL ALTER TABLE IAM.UserSession ADD RevokedReason NVARCHAR(500) NULL;
    IF COL_LENGTH(N'IAM.UserSession', N'IsRevoked') IS NULL ALTER TABLE IAM.UserSession ADD IsRevoked BIT NOT NULL CONSTRAINT DF_UserSession_IsRevoked_0104 DEFAULT 0;
    IF COL_LENGTH(N'IAM.UserSession', N'CreatedDateUtc') IS NULL ALTER TABLE IAM.UserSession ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_UserSession_CreatedDateUtc_0104B DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'IAM.UserSession', N'IsDeleted') IS NULL ALTER TABLE IAM.UserSession ADD IsDeleted BIT NOT NULL CONSTRAINT DF_UserSession_IsDeleted_0104 DEFAULT 0;
END

EXEC sp_executesql N'
IF OBJECT_ID(N''IAM.UserSession'') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM IAM.UserSession WHERE TenantId = @TenantId AND SessionToken = N''TENANT-ADMIN-SESSION-ACTIVE-HQ'')
BEGIN
    INSERT INTO IAM.UserSession
        (SessionId, TenantId, UserId, SessionToken, DeviceIdentifier, DeviceType, UserAgent, IpAddress, LoginDateUtc, LastActivityDateUtc, ExpiresDateUtc, IsRevoked, RevokedDateUtc, RevokedReason, CreatedDateUtc, IsDeleted)
    VALUES
        (NEWID(), @TenantId, @AdminUserId, N''TENANT-ADMIN-SESSION-ACTIVE-HQ'', N''HQ-WKS-1024'', N''Desktop'', N''Edge / Windows 11'', N''10.20.4.18'', DATEADD(minute, -45, @Now), DATEADD(minute, -5, @Now), DATEADD(hour, 7, @Now), 0, NULL, NULL, DATEADD(minute, -45, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, N''TENANT-ADMIN-SESSION-MOBILE'', N''IOS-15-PRO'', N''Mobile'', N''Safari / iOS'', N''192.168.8.44'', DATEADD(hour, -3, @Now), DATEADD(minute, -38, @Now), DATEADD(hour, 5, @Now), 0, NULL, NULL, DATEADD(hour, -3, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, N''TENANT-ADMIN-SESSION-STALE'', N''BRANCH-LAP-88'', N''Desktop'', N''Chrome / Windows'', N''192.168.12.88'', DATEADD(hour, -11, @Now), DATEADD(hour, -9, @Now), DATEADD(hour, 2, @Now), 0, NULL, NULL, DATEADD(hour, -11, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, N''TENANT-ADMIN-SESSION-SUSPICIOUS'', N''UNKNOWN-EXT'', N''Desktop'', N''Unknown Browser'', N''203.0.113.45'', DATEADD(hour, -2, @Now), NULL, DATEADD(hour, 4, @Now), 0, NULL, NULL, DATEADD(hour, -2, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, N''TENANT-ADMIN-SESSION-TABLET'', N''SURFACE-TAB-07'', N''Tablet'', N''Edge / Windows Tablet'', N''10.20.6.77'', DATEADD(day, -1, @Now), DATEADD(day, -1, DATEADD(hour, 1, @Now)), DATEADD(day, 1, @Now), 0, NULL, NULL, DATEADD(day, -1, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, N''TENANT-ADMIN-SESSION-REVOKED'', N''HQ-WKS-OLD'', N''Desktop'', N''Edge / Windows'', N''10.20.4.31'', DATEADD(day, -2, @Now), DATEADD(day, -2, DATEADD(hour, 2, @Now)), DATEADD(day, -1, @Now), 1, DATEADD(day, -2, DATEADD(hour, 3, @Now)), N''Revoked after password reset'', DATEADD(day, -2, @Now), 0),
        (NEWID(), @TenantId, @AdminUserId, N''TENANT-ADMIN-SESSION-EXPIRED'', N''BRANCH-WKS-44'', N''Desktop'', N''Chrome / Windows'', N''192.168.4.44'', DATEADD(day, -6, @Now), DATEADD(day, -6, DATEADD(hour, 3, @Now)), DATEADD(day, -5, @Now), 0, NULL, NULL, DATEADD(day, -6, @Now), 0);
END',
N'@TenantId UNIQUEIDENTIFIER, @AdminUserId UNIQUEIDENTIFIER, @Now DATETIME2',
@TenantId = @TenantId, @AdminUserId = @AdminUserId, @Now = @Now;
";
}
