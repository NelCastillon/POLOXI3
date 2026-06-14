-- ============================================================
-- AMS Enterprise Platform – Full Schema Migration
-- ============================================================

-- ── Schemas ─────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Core')     EXEC('CREATE SCHEMA Core');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'IAM')      EXEC('CREATE SCHEMA IAM');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'CRM')      EXEC('CREATE SCHEMA CRM');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Client')   EXEC('CREATE SCHEMA Client');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'OPS')      EXEC('CREATE SCHEMA OPS');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Billing')  EXEC('CREATE SCHEMA Billing');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Finance')  EXEC('CREATE SCHEMA Finance');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Commission') EXEC('CREATE SCHEMA Commission');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Workflow') EXEC('CREATE SCHEMA Workflow');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'DMS')      EXEC('CREATE SCHEMA DMS');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Audit')    EXEC('CREATE SCHEMA Audit');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Assistant') EXEC('CREATE SCHEMA Assistant');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Commercial') EXEC('CREATE SCHEMA Commercial');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Submissions') EXEC('CREATE SCHEMA Submissions');
GO

-- ============================================================
-- SUBMISSIONS: Reference Data
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Submissions.SubmissionReferenceOption'))
CREATE TABLE Submissions.SubmissionReferenceOption (
    SubmissionReferenceOptionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId                    UNIQUEIDENTIFIER NOT NULL,
    OptionGroup                 NVARCHAR(50)     NOT NULL,
    OptionCode                  NVARCHAR(100)    NOT NULL,
    OptionName                  NVARCHAR(150)    NOT NULL,
    Description                 NVARCHAR(500)    NULL,
    IsDefault                   BIT              NOT NULL DEFAULT 0,
    IsActive                    BIT              NOT NULL DEFAULT 1,
    SortOrder                   INT              NOT NULL DEFAULT 0,
    CreatedDateUtc              DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc             DATETIME2        NULL,
    IsDeleted                   BIT              NOT NULL DEFAULT 0,
    CONSTRAINT UQ_SubmissionReferenceOption_Tenant_Group_Code UNIQUE (TenantId, OptionGroup, OptionCode)
);

DECLARE @SubmissionSeedTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @SubmissionSeedTenantId AND OptionGroup = 'SubmissionStatus')
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@SubmissionSeedTenantId, 'SubmissionStatus', 'New', 'New', 'New submission intake record.', 1, 10),
        (@SubmissionSeedTenantId, 'SubmissionStatus', 'In Review', 'In Review', 'Submission is in underwriting or carrier review.', 0, 20),
        (@SubmissionSeedTenantId, 'SubmissionStatus', 'Quoted', 'Quoted', 'Submission has one or more quotes.', 0, 30),
        (@SubmissionSeedTenantId, 'SubmissionStatus', 'Bound', 'Bound', 'Submission has been bound into policy workflow.', 0, 40),
        (@SubmissionSeedTenantId, 'SubmissionStatus', 'Declined', 'Declined', 'Submission was declined by underwriting or market.', 0, 80),
        (@SubmissionSeedTenantId, 'SubmissionStatus', 'Withdrawn', 'Withdrawn', 'Submission was withdrawn by client or producer.', 0, 90);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @SubmissionSeedTenantId AND OptionGroup = 'LineOfBusiness')
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@SubmissionSeedTenantId, 'LineOfBusiness', 'General Liability', 'General Liability', 'Commercial general liability placement.', 1, 10),
        (@SubmissionSeedTenantId, 'LineOfBusiness', 'Commercial Property', 'Commercial Property', 'Commercial property placement.', 0, 20),
        (@SubmissionSeedTenantId, 'LineOfBusiness', 'Commercial Auto', 'Commercial Auto', 'Commercial automobile placement.', 0, 30),
        (@SubmissionSeedTenantId, 'LineOfBusiness', 'Workers Comp', 'Workers Comp', 'Workers compensation placement.', 0, 40),
        (@SubmissionSeedTenantId, 'LineOfBusiness', 'Umbrella / Excess', 'Umbrella / Excess', 'Umbrella or excess liability placement.', 0, 50),
        (@SubmissionSeedTenantId, 'LineOfBusiness', 'Professional Liability', 'Professional Liability', 'Professional liability placement.', 0, 60),
        (@SubmissionSeedTenantId, 'LineOfBusiness', 'Home / Dwelling', 'Home / Dwelling', 'Personal home or dwelling placement.', 0, 70),
        (@SubmissionSeedTenantId, 'LineOfBusiness', 'Personal Auto', 'Personal Auto', 'Personal automobile placement.', 0, 80);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @SubmissionSeedTenantId AND OptionGroup = 'ApplicationStatus')
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@SubmissionSeedTenantId, 'ApplicationStatus', 'Draft', 'Draft', 'Application package is being drafted.', 1, 10),
        (@SubmissionSeedTenantId, 'ApplicationStatus', 'Submitted', 'Submitted', 'Application has been submitted.', 0, 20),
        (@SubmissionSeedTenantId, 'ApplicationStatus', 'Under Review', 'Under Review', 'Application is under review.', 0, 30),
        (@SubmissionSeedTenantId, 'ApplicationStatus', 'Requirements Pending', 'Requirements Pending', 'Additional requirements are pending.', 0, 40),
        (@SubmissionSeedTenantId, 'ApplicationStatus', 'Approved', 'Approved', 'Application is approved for quote workflow.', 0, 50),
        (@SubmissionSeedTenantId, 'ApplicationStatus', 'Rejected', 'Rejected', 'Application was rejected.', 0, 90);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @SubmissionSeedTenantId AND OptionGroup = 'QuoteStatus')
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@SubmissionSeedTenantId, 'QuoteStatus', 'Pending', 'Pending', 'Quote is pending market response.', 1, 10),
        (@SubmissionSeedTenantId, 'QuoteStatus', 'Accepted', 'Accepted', 'Quote has been accepted or presented.', 0, 20),
        (@SubmissionSeedTenantId, 'QuoteStatus', 'Declined', 'Declined', 'Quote has been declined.', 0, 80),
        (@SubmissionSeedTenantId, 'QuoteStatus', 'Expired', 'Expired', 'Quote has expired.', 0, 90);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @SubmissionSeedTenantId AND OptionGroup = 'DeclineType')
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@SubmissionSeedTenantId, 'DeclineType', 'Carrier', 'Carrier', 'Carrier or market declined the submission.', 1, 10),
        (@SubmissionSeedTenantId, 'DeclineType', 'Internal', 'Internal', 'Agency or underwriting team declined the submission.', 0, 20),
        (@SubmissionSeedTenantId, 'DeclineType', 'Withdrawn', 'Withdrawn', 'Client or producer withdrew the submission.', 0, 30);
END;

-- ============================================================
-- COMMERCIAL: Plans
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commercial.Plan'))
CREATE TABLE Commercial.[Plan] (
    PlanId                UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    PlanCode              NVARCHAR(50)     NOT NULL,
    PlanName              NVARCHAR(200)    NOT NULL,
    BillingFrequency      NVARCHAR(50)     NOT NULL DEFAULT 'Monthly',
    BasePrice             DECIMAL(18,2)    NOT NULL DEFAULT 0,
    IncludedUsers         INT              NOT NULL DEFAULT 0,
    IncludedStorageGb     DECIMAL(10,2)    NOT NULL DEFAULT 0,
    IncludedApiCallsPerDay INT             NOT NULL DEFAULT 0,
    IsEnterprise          BIT              NOT NULL DEFAULT 0,
    IsActive              BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc       DATETIME2        NULL,
    CreatedByUserId       UNIQUEIDENTIFIER NULL,
    IsDeleted             BIT              NOT NULL DEFAULT 0,
    CONSTRAINT UQ_Commercial_Plan_PlanCode UNIQUE (PlanCode)
);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commercial.Plan') AND name = 'IsEnterprise')
    ALTER TABLE Commercial.[Plan] ADD IsEnterprise BIT NOT NULL DEFAULT 0;

-- ============================================================
-- COMMERCIAL: Plan Features, Limits, and Add-Ons
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commercial.PlanFeature'))
CREATE TABLE Commercial.PlanFeature (
    PlanFeatureId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    PlanId          UNIQUEIDENTIFIER NOT NULL,
    FeatureCode     NVARCHAR(100)    NOT NULL,
    FeatureName     NVARCHAR(200)    NOT NULL,
    IsIncluded      BIT              NOT NULL DEFAULT 1,
    Notes           NVARCHAR(500)    NOT NULL DEFAULT '',
    CreatedDateUtc  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_PlanFeature_Plan FOREIGN KEY (PlanId) REFERENCES Commercial.[Plan] (PlanId)
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commercial.PlanLimit'))
CREATE TABLE Commercial.PlanLimit (
    PlanLimitId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    PlanId          UNIQUEIDENTIFIER NOT NULL,
    MetricTypeCode  NVARCHAR(100)    NOT NULL,
    LimitValue      DECIMAL(18,4)    NOT NULL DEFAULT 0,
    LimitUnit       NVARCHAR(50)     NOT NULL DEFAULT 'Count',
    PeriodCode      NVARCHAR(50)     NOT NULL DEFAULT 'Monthly',
    IsEnforced      BIT              NOT NULL DEFAULT 1,
    Notes           NVARCHAR(500)    NOT NULL DEFAULT '',
    CreatedDateUtc  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_PlanLimit_Plan FOREIGN KEY (PlanId) REFERENCES Commercial.[Plan] (PlanId)
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commercial.PlanAddOn'))
CREATE TABLE Commercial.PlanAddOn (
    PlanAddOnId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    PlanId           UNIQUEIDENTIFIER NOT NULL,
    AddOnCode        NVARCHAR(50)     NOT NULL,
    AddOnName        NVARCHAR(200)    NOT NULL,
    Price            DECIMAL(18,2)    NOT NULL DEFAULT 0,
    BillingFrequency NVARCHAR(50)     NOT NULL DEFAULT 'Monthly',
    Description      NVARCHAR(500)    NOT NULL DEFAULT '',
    IsActive         BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted        BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_PlanAddOn_Plan FOREIGN KEY (PlanId) REFERENCES Commercial.[Plan] (PlanId)
);

-- ============================================================
-- COMMERCIAL: Subscriptions
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commercial.Subscription'))
CREATE TABLE Commercial.Subscription (
    SubscriptionId    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    PlanId            UNIQUEIDENTIFIER NOT NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    RenewalType       NVARCHAR(50)     NOT NULL DEFAULT 'Auto',
    BillingCycle      NVARCHAR(50)     NOT NULL DEFAULT 'Monthly',
    BaseAmount        DECIMAL(18,2)    NOT NULL DEFAULT 0,
    StartDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    EndDateUtc        DATETIME2        NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0,
    CONSTRAINT FK_Subscription_Plan FOREIGN KEY (PlanId) REFERENCES Commercial.[Plan] (PlanId)
);

-- ============================================================
-- 2.1  PLATFORM CORE
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.Tenant'))
CREATE TABLE Core.Tenant (
    TenantId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantCode        NVARCHAR(50)     NOT NULL,
    TenantName        NVARCHAR(200)    NOT NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    PlanCode          NVARCHAR(50)     NOT NULL DEFAULT 'Standard',
    RegionCode        NVARCHAR(100)    NOT NULL DEFAULT '',
    IsolationMode     NVARCHAR(50)     NOT NULL DEFAULT 'Shared',
    PrimaryDomain     NVARCHAR(253)    NULL,
    ActiveUsers       INT              NOT NULL DEFAULT 0,
    IsActive          BIT              NOT NULL DEFAULT 1,
    Locale                   NVARCHAR(20)     NOT NULL DEFAULT 'en-US',
    CurrencyCode             NVARCHAR(10)     NOT NULL DEFAULT 'USD',
    TimeZoneId               NVARCHAR(100)    NOT NULL DEFAULT 'UTC',
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    GoLiveDateUtc     DATETIME2        NULL,
    ModifiedDateUtc   DATETIME2        NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.Branch'))
CREATE TABLE Core.Branch (
    BranchId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    BranchCode        NVARCHAR(50)     NOT NULL,
    BranchName        NVARCHAR(200)    NOT NULL,
    City              NVARCHAR(100)    NULL,
    StateProvince     NVARCHAR(100)    NULL,
    CountryCode       NVARCHAR(10)     NULL,
    IsActive          BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.TenantDomain'))
CREATE TABLE Core.TenantDomain (
    TenantDomainId    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    DomainName        NVARCHAR(253)    NOT NULL,
    IsPrimary         BIT              NOT NULL DEFAULT 0,
    SslStatusCode     NVARCHAR(50)     NOT NULL DEFAULT 'None',
    VerificationStatusCode NVARCHAR(50) NOT NULL DEFAULT 'Pending',
    VerificationToken NVARCHAR(500)    NULL,
    VerifiedDateUtc   DATETIME2        NULL,
    RedirectTarget    NVARCHAR(500)    NULL,
    SslExpiresDateUtc DATETIME2        NULL,
    IsActive          BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    Notes             NVARCHAR(1000)   NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF COL_LENGTH('Core.TenantDomain', 'Notes') IS NULL
    ALTER TABLE Core.TenantDomain ADD Notes NVARCHAR(1000) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.Feature'))
CREATE TABLE Core.Feature (
    FeatureId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    FeatureCode       NVARCHAR(100)    NOT NULL,
    FeatureName       NVARCHAR(200)    NOT NULL,
    Module            NVARCHAR(100)    NOT NULL DEFAULT '',
    TypeCode          NVARCHAR(50)     NOT NULL DEFAULT 'Toggle',
    DefaultEnabled    BIT              NOT NULL DEFAULT 0,
    IsEnabled         BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL
);

-- Add missing columns to Core.Feature if table already exists
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.Feature'))
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Feature') AND name = 'Module')
        ALTER TABLE Core.Feature ADD Module NVARCHAR(100) NOT NULL DEFAULT '';
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Feature') AND name = 'TypeCode')
        ALTER TABLE Core.Feature ADD TypeCode NVARCHAR(50) NOT NULL DEFAULT 'Toggle';
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Feature') AND name = 'DefaultEnabled')
        ALTER TABLE Core.Feature ADD DefaultEnabled BIT NOT NULL DEFAULT 0;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Feature') AND name = 'CreatedDateUtc')
        ALTER TABLE Core.Feature ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Feature') AND name = 'ModifiedDateUtc')
        ALTER TABLE Core.Feature ADD ModifiedDateUtc DATETIME2 NULL;
END;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.TenantFeature'))
CREATE TABLE Core.TenantFeature (
    TenantFeatureId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    FeatureCode       NVARCHAR(100)    NOT NULL,
    IsEnabled         BIT              NOT NULL DEFAULT 1,
    EffectiveStartUtc DATETIME2        NULL,
    EffectiveEndUtc   DATETIME2        NULL,
    SourceType        NVARCHAR(50)     NOT NULL DEFAULT 'Override',
    EnabledDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CONSTRAINT UQ_TenantFeature_Tenant_Code UNIQUE (TenantId, FeatureCode)
);

-- Add missing columns to Core.TenantFeature if table already exists
IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.TenantFeature'))
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.TenantFeature') AND name = 'EffectiveStartUtc')
        ALTER TABLE Core.TenantFeature ADD EffectiveStartUtc DATETIME2 NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.TenantFeature') AND name = 'EffectiveEndUtc')
        ALTER TABLE Core.TenantFeature ADD EffectiveEndUtc DATETIME2 NULL;
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.TenantFeature') AND name = 'SourceType')
        ALTER TABLE Core.TenantFeature ADD SourceType NVARCHAR(50) NOT NULL DEFAULT 'Override';
    IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.TenantFeature') AND name = 'ModifiedDateUtc')
        ALTER TABLE Core.TenantFeature ADD ModifiedDateUtc DATETIME2 NULL;
END;

-- ============================================================
-- 2.2  IDENTITY AND ACCESS MANAGEMENT
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.Role'))
CREATE TABLE IAM.Role (
    RoleId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    RoleCode          NVARCHAR(100)    NOT NULL,
    RoleName          NVARCHAR(200)    NOT NULL,
    RoleTypeCode      NVARCHAR(50)     NOT NULL DEFAULT 'Internal',
    Description       NVARCHAR(500)    NULL,
    SortOrder         INT              NOT NULL DEFAULT 0,
    IsBuiltIn         BIT              NOT NULL DEFAULT 0,
    IsSystemRole      BIT              NOT NULL DEFAULT 0,
    IsActive          BIT              NOT NULL DEFAULT 1,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    ModifiedDateUtc   DATETIME2        NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.Role') AND name = 'SortOrder')
    ALTER TABLE IAM.Role ADD SortOrder INT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.Role') AND name = 'IsBuiltIn')
    ALTER TABLE IAM.Role ADD IsBuiltIn BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.Role') AND name = 'IsSystemRole')
    ALTER TABLE IAM.Role ADD IsSystemRole BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.Role') AND name = 'CreatedByUserId')
    ALTER TABLE IAM.Role ADD CreatedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.Role') AND name = 'ModifiedByUserId')
    ALTER TABLE IAM.Role ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.[User]'))
CREATE TABLE IAM.[User] (
    UserId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    BranchId          UNIQUEIDENTIFIER NULL,
    UserName          NVARCHAR(200)    NOT NULL,
    Email             NVARCHAR(300)    NOT NULL,
    FullName          NVARCHAR(300)    NOT NULL,
    UserTypeCode      NVARCHAR(50)     NOT NULL DEFAULT 'Internal',
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    MfaEnabled        BIT              NOT NULL DEFAULT 0,
    LastLoginDateUtc  DATETIME2        NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.UserRole'))
CREATE TABLE IAM.UserRole (
    UserRoleId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    UserId            UNIQUEIDENTIFIER NOT NULL,
    RoleId            UNIQUEIDENTIFIER NOT NULL,
    AssignedDateUtc   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    AssignedByUserId  UNIQUEIDENTIFIER NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.Permission'))
CREATE TABLE IAM.Permission (
    PermissionId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    PermissionCode    NVARCHAR(200)    NOT NULL,
    ModuleCode        NVARCHAR(100)    NOT NULL,
    Description       NVARCHAR(500)    NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.RolePermission'))
CREATE TABLE IAM.RolePermission (
    RolePermissionId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    RoleId            UNIQUEIDENTIFIER NOT NULL,
    PermissionCode    NVARCHAR(200)    NOT NULL
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.UserPermission'))
CREATE TABLE IAM.UserPermission (
    UserPermissionId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    UserId                UNIQUEIDENTIFIER NOT NULL,
    PermissionId          UNIQUEIDENTIFIER NOT NULL,
    IsGranted             BIT              NOT NULL DEFAULT 1,
    GrantedByUserId       UNIQUEIDENTIFIER NULL,
    GrantedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    EffectiveStartDateUtc DATETIME2        NULL,
    ExpiresDateUtc        DATETIME2        NULL,
    Reason                NVARCHAR(500)    NULL,
    ApprovedByUserId      UNIQUEIDENTIFIER NULL,
    ModifiedByUserId      UNIQUEIDENTIFIER NULL,
    ModifiedDateUtc       DATETIME2        NULL,
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.UserPermissionScope'))
CREATE TABLE IAM.UserPermissionScope (
    UserPermissionScopeId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    UserPermissionId      UNIQUEIDENTIFIER NOT NULL,
    ScopeTypeCode         NVARCHAR(100)    NOT NULL,
    ScopeValue            NVARCHAR(500)    NOT NULL,
    CreatedByUserId       UNIQUEIDENTIFIER NULL,
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.RoleBundle'))
CREATE TABLE IAM.RoleBundle (
    BundleId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    BundleCode        NVARCHAR(100)    NOT NULL,
    BundleName        NVARCHAR(200)    NOT NULL,
    Description       NVARCHAR(500)    NULL,
    IsSystemBundle    BIT              NOT NULL DEFAULT 0,
    IsActive          BIT              NOT NULL DEFAULT 1,
    SortOrder         INT              NOT NULL DEFAULT 0,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.BundleRole'))
CREATE TABLE IAM.BundleRole (
    BundleRoleId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    BundleId          UNIQUEIDENTIFIER NOT NULL,
    RoleId            UNIQUEIDENTIFIER NOT NULL,
    AssignedDateUtc   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.BundleUser'))
CREATE TABLE IAM.BundleUser (
    BundleUserId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    BundleId          UNIQUEIDENTIFIER NOT NULL,
    UserId            UNIQUEIDENTIFIER NOT NULL,
    AssignedByUserId  UNIQUEIDENTIFIER NULL,
    AssignedDateUtc   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- 2.3  CRM AND SALES  (Lead & Opportunity already exist)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('CRM.Lead'))
CREATE TABLE CRM.Lead (
    LeadId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    LeadNumber        NVARCHAR(50)     NOT NULL,
    AccountName       NVARCHAR(200)    NULL,
    FirstName         NVARCHAR(150)    NOT NULL,
    LastName          NVARCHAR(150)    NOT NULL,
    Email             NVARCHAR(300)    NULL,
    Phone             NVARCHAR(50)     NULL,
    InterestedService NVARCHAR(200)    NULL,
    AccountId         UNIQUEIDENTIFIER NULL,
    Score             INT              NULL,
    PriorityCode      NVARCHAR(50)     NULL,
    AssignedToUserId  UNIQUEIDENTIFIER NULL,
    StatusCodeId      INT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('CRM.Lead'))
AND COL_LENGTH('CRM.Lead', 'AccountId') IS NULL
    ALTER TABLE CRM.Lead ADD AccountId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('CRM.Opportunity'))
CREATE TABLE CRM.Opportunity (
    OpportunityId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    OpportunityNumber NVARCHAR(50)     NOT NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    OpportunityName   NVARCHAR(300)    NOT NULL,
    EstimatedAmount   DECIMAL(18,2)    NOT NULL DEFAULT 0,
    OwnerUserId       UNIQUEIDENTIFIER NULL,
    CloseDate         DATE             NULL,
    StatusCodeId      INT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('CRM.Quote'))
CREATE TABLE CRM.Quote (
    QuoteId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    QuoteNumber       NVARCHAR(50)     NOT NULL,
    OpportunityId     UNIQUEIDENTIFIER NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    TotalAmount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
    ValidUntilDate    DATE             NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- 2.4  CLIENT AND ACCOUNT MANAGEMENT  (Account already exists)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Client.Account'))
CREATE TABLE Client.Account (
    AccountId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    AccountNumber     NVARCHAR(50)     NOT NULL,
    AccountName       NVARCHAR(300)    NOT NULL,
    AccountTypeCode   NVARCHAR(50)     NOT NULL,
    MainEmail         NVARCHAR(300)    NULL,
    MainPhone         NVARCHAR(50)     NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    SegmentCode       NVARCHAR(50)     NULL,
    OwnerUserId       UNIQUEIDENTIFIER NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Client.Contact'))
CREATE TABLE Client.Contact (
    ContactId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    FirstName         NVARCHAR(150)    NOT NULL,
    LastName          NVARCHAR(150)    NOT NULL,
    Email             NVARCHAR(300)    NULL,
    Phone             NVARCHAR(50)     NULL,
    JobTitle          NVARCHAR(200)    NULL,
    ContactTypeCode   NVARCHAR(50)     NOT NULL DEFAULT 'Primary',
    IsBillingContact  BIT              NOT NULL DEFAULT 0,
    IsPortalUser      BIT              NOT NULL DEFAULT 0,
    IsKeyContact      BIT              NOT NULL DEFAULT 0,
    IsServiceContact  BIT              NOT NULL DEFAULT 0,
    ParentContactId   UNIQUEIDENTIFIER NULL,
    PreferredContactMethod NVARCHAR(50) NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- CLIENT: Account Configuration Reference Data
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Client.AccountReferenceOption'))
CREATE TABLE Client.AccountReferenceOption (
    AccountReferenceOptionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId                 UNIQUEIDENTIFIER NOT NULL,
    OptionGroup              NVARCHAR(50)     NOT NULL,
    OptionCode               NVARCHAR(50)     NOT NULL,
    OptionName               NVARCHAR(100)    NOT NULL,
    Description              NVARCHAR(500)    NULL,
    IsDefault                BIT              NOT NULL DEFAULT 0,
    IsActive                 BIT              NOT NULL DEFAULT 1,
    SortOrder                INT              NOT NULL DEFAULT 0,
    CreatedDateUtc           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc          DATETIME2        NULL,
    IsDeleted                BIT              NOT NULL DEFAULT 0,
    CONSTRAINT UQ_AccountReferenceOption_Tenant_Group_Code UNIQUE (TenantId, OptionGroup, OptionCode)
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Client.AccountType'))
CREATE TABLE Client.AccountType (
    AccountTypeId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId        UNIQUEIDENTIFIER NOT NULL,
    TypeCode        NVARCHAR(50)     NOT NULL,
    TypeName        NVARCHAR(100)    NOT NULL,
    Category        NVARCHAR(50)     NULL,
    Description     NVARCHAR(500)    NULL,
    IsDefault       BIT              NOT NULL DEFAULT 0,
    IsActive        BIT              NOT NULL DEFAULT 1,
    SortOrder       INT              NOT NULL DEFAULT 0,
    CreatedDateUtc  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc DATETIME2        NULL,
    IsDeleted       BIT              NOT NULL DEFAULT 0,
    CONSTRAINT UQ_AccountType_Tenant_Code UNIQUE (TenantId, TypeCode)
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Client.RelationshipType'))
CREATE TABLE Client.RelationshipType (
    RelationshipTypeId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    TypeCode           NVARCHAR(50)     NOT NULL,
    TypeName           NVARCHAR(100)    NOT NULL,
    IsBidirectional    BIT              NOT NULL DEFAULT 0,
    InverseTypeCode    NVARCHAR(50)     NULL,
    Description        NVARCHAR(500)    NULL,
    IsActive           BIT              NOT NULL DEFAULT 1,
    SortOrder          INT              NOT NULL DEFAULT 0,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc    DATETIME2        NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0,
    CONSTRAINT UQ_RelationshipType_Tenant_Code UNIQUE (TenantId, TypeCode)
);

DECLARE @SeedTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

IF NOT EXISTS (SELECT 1 FROM Client.AccountReferenceOption WHERE TenantId = @SeedTenantId AND OptionGroup = 'Status')
BEGIN
    INSERT INTO Client.AccountReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@SeedTenantId, 'Status', 'Active', 'Active', 'Active customer or managed account.', 1, 10),
        (@SeedTenantId, 'Status', 'Prospect', 'Prospect', 'Prospective customer in pipeline.', 0, 20),
        (@SeedTenantId, 'Status', 'Inactive', 'Inactive', 'Inactive or archived account.', 0, 90),
        (@SeedTenantId, 'Status', 'Suspended', 'Suspended', 'Temporarily suspended account.', 0, 95);
END;

IF NOT EXISTS (SELECT 1 FROM Client.AccountReferenceOption WHERE TenantId = @SeedTenantId AND OptionGroup = 'Segment')
BEGIN
    INSERT INTO Client.AccountReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@SeedTenantId, 'Segment', 'Enterprise', 'Enterprise', 'Large strategic account.', 0, 10),
        (@SeedTenantId, 'Segment', 'Key Account', 'Key Account', 'High-value retained account.', 0, 20),
        (@SeedTenantId, 'Segment', 'Mid-Market', 'Mid-Market', 'Mid-market account segment.', 1, 30),
        (@SeedTenantId, 'Segment', 'SMB', 'SMB', 'Small and midsize business account.', 0, 40),
        (@SeedTenantId, 'Segment', 'Startup', 'Startup', 'Early-stage growth account.', 0, 50);
END;

IF NOT EXISTS (SELECT 1 FROM Client.AccountReferenceOption WHERE TenantId = @SeedTenantId AND OptionGroup = 'LifecycleStage')
BEGIN
    INSERT INTO Client.AccountReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@SeedTenantId, 'LifecycleStage', 'Lead', 'Lead', 'New account lead.', 0, 10),
        (@SeedTenantId, 'LifecycleStage', 'Prospect', 'Prospect', 'Qualified sales prospect.', 1, 20),
        (@SeedTenantId, 'LifecycleStage', 'Customer', 'Customer', 'Active customer relationship.', 0, 30),
        (@SeedTenantId, 'LifecycleStage', 'Renewal', 'Renewal', 'Renewal management stage.', 0, 40),
        (@SeedTenantId, 'LifecycleStage', 'At Risk', 'At Risk', 'Account needs retention attention.', 0, 80),
        (@SeedTenantId, 'LifecycleStage', 'Inactive', 'Inactive', 'Inactive lifecycle stage.', 0, 90);
END;

IF NOT EXISTS (SELECT 1 FROM Client.AccountType WHERE TenantId = @SeedTenantId)
BEGIN
    INSERT INTO Client.AccountType (TenantId, TypeCode, TypeName, Category, Description, IsDefault, SortOrder)
    VALUES
        (@SeedTenantId, 'Commercial', 'Commercial', 'Commercial', 'Commercial insurance or business account.', 1, 10),
        (@SeedTenantId, 'Personal', 'Personal', 'Personal', 'Personal lines account.', 0, 20),
        (@SeedTenantId, 'Non-Profit', 'Non-Profit', 'Commercial', 'Non-profit organization account.', 0, 30),
        (@SeedTenantId, 'Government', 'Government', 'Government', 'Public sector account.', 0, 40),
        (@SeedTenantId, 'Partner', 'Partner', 'Commercial', 'Partner or referral account.', 0, 50);
END;

IF NOT EXISTS (SELECT 1 FROM Client.RelationshipType WHERE TenantId = @SeedTenantId)
BEGIN
    INSERT INTO Client.RelationshipType (TenantId, TypeCode, TypeName, IsBidirectional, InverseTypeCode, Description, SortOrder)
    VALUES
        (@SeedTenantId, 'Parent', 'Parent', 0, 'Subsidiary', 'Parent company account relationship.', 10),
        (@SeedTenantId, 'Subsidiary', 'Subsidiary', 0, 'Parent', 'Subsidiary account relationship.', 20),
        (@SeedTenantId, 'Related', 'Related', 1, 'Related', 'Related account relationship.', 30),
        (@SeedTenantId, 'Partner', 'Partner', 1, 'Partner', 'Partner account relationship.', 40),
        (@SeedTenantId, 'Referred By', 'Referred By', 0, 'Referred', 'Referral source relationship.', 50);
END;

-- ============================================================
-- 2.5  POLICY / SERVICE / ENGAGEMENT / OPERATIONS
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.Agreement'))
CREATE TABLE Finance.Agreement (
    AgreementId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    AgreementNumber   NVARCHAR(50)     NOT NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    OpportunityId     UNIQUEIDENTIFIER NULL,
    EffectiveStartDate DATE            NOT NULL,
    EffectiveEndDate   DATE            NULL,
    TotalContractValue DECIMAL(18,2)   NULL,
    StatusCodeId      INT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('OPS.Engagement'))
CREATE TABLE OPS.Engagement (
    EngagementId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    EngagementNumber  NVARCHAR(50)     NOT NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    AgreementId       UNIQUEIDENTIFIER NULL,
    EngagementName    NVARCHAR(300)    NOT NULL,
    EngagementTypeCode NVARCHAR(50)    NOT NULL DEFAULT 'Project',
    OwnerUserId       UNIQUEIDENTIFIER NULL,
    StartDate         DATE             NULL,
    EndDate           DATE             NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('OPS.EngagementMilestone'))
CREATE TABLE OPS.EngagementMilestone (
    MilestoneId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    EngagementId      UNIQUEIDENTIFIER NOT NULL,
    MilestoneName     NVARCHAR(300)    NOT NULL,
    DueDate           DATE             NULL,
    CompletedDate     DATE             NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('OPS.EngagementTask'))
CREATE TABLE OPS.EngagementTask (
    TaskId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    EngagementId      UNIQUEIDENTIFIER NOT NULL,
    MilestoneId       UNIQUEIDENTIFIER NULL,
    TaskTitle         NVARCHAR(300)    NOT NULL,
    AssignedToUserId  UNIQUEIDENTIFIER NULL,
    DueDate           DATE             NULL,
    CompletedDate     DATE             NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Open',
    Priority          NVARCHAR(20)     NOT NULL DEFAULT 'Medium',
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('OPS.IssueTracker'))
CREATE TABLE OPS.IssueTracker (
    IssueId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    EngagementId      UNIQUEIDENTIFIER NULL,
    AccountId         UNIQUEIDENTIFIER NULL,
    IssueNumber       NVARCHAR(50)     NOT NULL,
    Title             NVARCHAR(300)    NOT NULL,
    Description       NVARCHAR(MAX)    NULL,
    SeverityCode      NVARCHAR(50)     NOT NULL DEFAULT 'Medium',
    AssignedToUserId  UNIQUEIDENTIFIER NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Open',
    ResolvedDate      DATE             NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('OPS.AgreementAmendment'))
CREATE TABLE OPS.AgreementAmendment (
    AmendmentId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    AgreementId       UNIQUEIDENTIFIER NOT NULL,
    AmendmentNumber   NVARCHAR(50)     NOT NULL,
    AmendmentTypeCode NVARCHAR(50)     NOT NULL DEFAULT 'Amendment',
    EffectiveDate     DATE             NOT NULL,
    Description       NVARCHAR(MAX)    NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('OPS.AgreementRenewal'))
CREATE TABLE OPS.AgreementRenewal (
    RenewalId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    AgreementId       UNIQUEIDENTIFIER NOT NULL,
    RenewalNumber     NVARCHAR(50)     NOT NULL,
    NewStartDate      DATE             NOT NULL,
    NewEndDate        DATE             NULL,
    TotalContractValue DECIMAL(18,2)   NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    ProcessedByUserId UNIQUEIDENTIFIER NULL,
    ProcessedDateUtc  DATETIME2        NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('OPS.ServiceRequest'))
CREATE TABLE OPS.ServiceRequest (
    ServiceRequestId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    AgreementId       UNIQUEIDENTIFIER NULL,
    EngagementId      UNIQUEIDENTIFIER NULL,
    RequestNumber     NVARCHAR(50)     NOT NULL,
    RequestTypeCode   NVARCHAR(50)     NOT NULL DEFAULT 'Servicing',
    Subject           NVARCHAR(300)    NOT NULL,
    Description       NVARCHAR(MAX)    NULL,
    PriorityCode      NVARCHAR(50)     NOT NULL DEFAULT 'Medium',
    AssignedToUserId  UNIQUEIDENTIFIER NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Open',
    ResolvedDate      DATE             NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('OPS.OperationalActivityLog'))
CREATE TABLE OPS.OperationalActivityLog (
    ActivityId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    AccountId         UNIQUEIDENTIFIER NULL,
    EngagementId      UNIQUEIDENTIFIER NULL,
    AgreementId       UNIQUEIDENTIFIER NULL,
    ActivityDate      DATE             NOT NULL,
    ActivityTypeCode  NVARCHAR(100)    NOT NULL,
    Subject           NVARCHAR(300)    NOT NULL,
    Notes             NVARCHAR(MAX)    NULL,
    PerformedByUserId UNIQUEIDENTIFIER NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- 2.6  TIME, EXPENSE, BILLING, AND COLLECTIONS
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Billing.TimeEntry'))
CREATE TABLE Billing.TimeEntry (
    TimeEntryId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    EngagementId      UNIQUEIDENTIFIER NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    UserId            UNIQUEIDENTIFIER NOT NULL,
    EntryDate         DATE             NOT NULL,
    Hours             DECIMAL(8,2)     NOT NULL,
    BillableHours     DECIMAL(8,2)     NOT NULL DEFAULT 0,
    RateAmount        DECIMAL(18,2)    NOT NULL DEFAULT 0,
    Description       NVARCHAR(500)    NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
    InvoiceId         UNIQUEIDENTIFIER NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Billing.ExpenseEntry'))
CREATE TABLE Billing.ExpenseEntry (
    ExpenseId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    EngagementId      UNIQUEIDENTIFIER NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    UserId            UNIQUEIDENTIFIER NOT NULL,
    ExpenseDate       DATE             NOT NULL,
    CategoryCode      NVARCHAR(100)    NOT NULL,
    Amount            DECIMAL(18,2)    NOT NULL,
    Description       NVARCHAR(500)    NULL,
    IsBillable        BIT              NOT NULL DEFAULT 1,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
    InvoiceId         UNIQUEIDENTIFIER NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.Invoice'))
CREATE TABLE Finance.Invoice (
    InvoiceId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    InvoiceNumber     NVARCHAR(50)     NOT NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    AgreementId       UNIQUEIDENTIFIER NULL,
    TotalAmount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
    BalanceAmount     DECIMAL(18,2)    NOT NULL DEFAULT 0,
    InvoiceDate       DATE             NOT NULL,
    DueDate           DATE             NOT NULL,
    StatusCodeId      INT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Billing.Payment'))
CREATE TABLE Billing.Payment (
    PaymentId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    InvoiceId         UNIQUEIDENTIFIER NULL,
    PaymentDate       DATE             NOT NULL,
    Amount            DECIMAL(18,2)    NOT NULL,
    PaymentMethodCode NVARCHAR(50)     NOT NULL DEFAULT 'ACH',
    ReferenceNumber   NVARCHAR(100)    NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Applied',
    Notes             NVARCHAR(500)    NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Billing.CollectionsNote'))
CREATE TABLE Billing.CollectionsNote (
    CollectionsNoteId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    InvoiceId         UNIQUEIDENTIFIER NULL,
    NoteDate          DATE             NOT NULL,
    NoteText          NVARCHAR(MAX)    NOT NULL,
    ActionCode        NVARCHAR(100)    NOT NULL DEFAULT 'CallMade',
    NextFollowUpDate  DATE             NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- 2.7  ACCOUNTING AND FINANCE – Safe column migrations
-- ============================================================

-- Finance.Invoice
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Finance.Invoice') AND name = 'ModifiedByUserId')
    ALTER TABLE Finance.Invoice ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

-- Commission.CommissionPlan
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commission.CommissionPlan') AND name = 'ModifiedByUserId')
    ALTER TABLE Commission.CommissionPlan ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

-- Commission.CommissionPayee
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commission.CommissionPayee') AND name = 'ModifiedByUserId')
    ALTER TABLE Commission.CommissionPayee ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

-- Commission.CommissionTransaction
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commission.CommissionTransaction') AND name = 'ModifiedByUserId')
    ALTER TABLE Commission.CommissionTransaction ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

-- Billing.RateCard
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Billing.RateCard') AND name = 'ModifiedByUserId')
    ALTER TABLE Billing.RateCard ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

-- Billing.RateCardLine
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Billing.RateCardLine') AND name = 'ModifiedByUserId')
    ALTER TABLE Billing.RateCardLine ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

-- Billing.PrebillBatch
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Billing.PrebillBatch') AND name = 'ModifiedByUserId')
    ALTER TABLE Billing.PrebillBatch ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

-- Finance.TrialBalanceSnapshot
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Finance.TrialBalanceSnapshot') AND name = 'ModifiedByUserId')
    ALTER TABLE Finance.TrialBalanceSnapshot ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

-- Core.Alert
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Alert') AND name = 'ModifiedByUserId')
    ALTER TABLE Core.Alert ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

-- DMS.Document
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DMS.Document') AND name = 'ModifiedByUserId')
    ALTER TABLE DMS.Document ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

GO

-- ============================================================
-- IAM: PermissionAction Table
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.PermissionAction'))
CREATE TABLE IAM.PermissionAction (
    PermissionActionId   INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
    ActionName           NVARCHAR(100) NOT NULL UNIQUE,
    Description          NVARCHAR(200) NULL
);

-- ============================================================
-- IAM: Add PermissionActionId to Permission Table
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.Permission') AND name = 'PermissionActionId')
ALTER TABLE IAM.Permission ADD PermissionActionId INT NOT NULL DEFAULT 1;

ALTER TABLE IAM.Permission
    ADD CONSTRAINT FK_Permission_PermissionAction FOREIGN KEY (PermissionActionId)
    REFERENCES IAM.PermissionAction(PermissionActionId);

GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.Permission') AND name = 'PermissionName')
ALTER TABLE IAM.Permission ADD PermissionName NVARCHAR(200) NULL;
-- Optionally, update existing rows if needed:
-- UPDATE IAM.Permission SET PermissionName = PermissionCode WHERE PermissionName IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.RolePermission') AND name = 'PermissionId')
ALTER TABLE IAM.RolePermission ADD PermissionId UNIQUEIDENTIFIER NULL;
-- Optionally, update existing rows if needed:
-- UPDATE IAM.RolePermission SET PermissionId = (SELECT PermissionId FROM IAM.Permission WHERE IAM.Permission.PermissionCode = IAM.RolePermission.PermissionCode) WHERE PermissionId IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.[User]') AND name = 'FirstName')
ALTER TABLE IAM.[User] ADD FirstName NVARCHAR(150) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.[User]') AND name = 'LastName')
ALTER TABLE IAM.[User] ADD LastName NVARCHAR(150) NULL;
-- Optionally, update existing rows if needed:
-- UPDATE IAM.[User] SET FirstName = PARSENAME(REPLACE(FullName, ' ', '.'), 2), LastName = PARSENAME(REPLACE(FullName, ' ', '.'), 1) WHERE FirstName IS NULL OR LastName IS NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.LoginAttempt') AND name = 'LastName')
ALTER TABLE IAM.LoginAttempt ADD LastName NVARCHAR(150) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.LoginAttempt') AND name = 'FailureReason')
ALTER TABLE IAM.LoginAttempt ADD FailureReason NVARCHAR(500) NULL;
