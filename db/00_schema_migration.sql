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
GO

-- ============================================================
-- 2.1  PLATFORM CORE
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.Tenant'))
CREATE TABLE Core.Tenant (
    TenantId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantCode        NVARCHAR(50)     NOT NULL,
    TenantName        NVARCHAR(200)    NOT NULL,
    PlanCode          NVARCHAR(50)     NOT NULL DEFAULT 'Standard',
    IsActive          BIT              NOT NULL DEFAULT 1,
    Locale            NVARCHAR(20)     NOT NULL DEFAULT 'en-US',
    CurrencyCode      NVARCHAR(10)     NOT NULL DEFAULT 'USD',
    TimeZoneId        NVARCHAR(100)    NOT NULL DEFAULT 'UTC',
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
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

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.Feature'))
CREATE TABLE Core.Feature (
    FeatureId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    FeatureCode       NVARCHAR(100)    NOT NULL,
    FeatureName       NVARCHAR(200)    NOT NULL,
    IsEnabled         BIT              NOT NULL DEFAULT 1
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.TenantFeature'))
CREATE TABLE Core.TenantFeature (
    TenantFeatureId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    FeatureCode       NVARCHAR(100)    NOT NULL,
    IsEnabled         BIT              NOT NULL DEFAULT 1,
    EnabledDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME()
);

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
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

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
-- 2.7  ACCOUNTING AND FINANCE
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.GLAccount'))
CREATE TABLE Finance.GLAccount (
    GLAccountId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    AccountCode       NVARCHAR(50)     NOT NULL,
    AccountName       NVARCHAR(200)    NOT NULL,
    AccountTypeCode   NVARCHAR(50)     NOT NULL,
    ParentGLAccountId UNIQUEIDENTIFIER NULL,
    IsActive          BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.JournalEntry'))
CREATE TABLE Finance.JournalEntry (
    JournalEntryId    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    JournalNumber     NVARCHAR(50)     NOT NULL,
    EntryDate         DATE             NOT NULL,
    Description       NVARCHAR(500)    NOT NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
    PostedDateUtc     DATETIME2        NULL,
    PostedByUserId    UNIQUEIDENTIFIER NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.JournalEntryLine'))
CREATE TABLE Finance.JournalEntryLine (
    LineId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    JournalEntryId    UNIQUEIDENTIFIER NOT NULL,
    GLAccountId       UNIQUEIDENTIFIER NOT NULL,
    DebitAmount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
    CreditAmount      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    Description       NVARCHAR(300)    NULL,
    LineOrder         INT              NOT NULL DEFAULT 1
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.BankReconciliation'))
CREATE TABLE Finance.BankReconciliation (
    ReconciliationId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    BankAccountCode   NVARCHAR(50)     NOT NULL,
    StatementDate     DATE             NOT NULL,
    StatementBalance  DECIMAL(18,2)    NOT NULL,
    BookBalance       DECIMAL(18,2)    NOT NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Open',
    ReconciledDateUtc DATETIME2        NULL,
    ReconciledByUserId UNIQUEIDENTIFIER NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- 2.8  COMMISSION MANAGEMENT  (CommissionPlan already exists)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commission.CommissionPlan'))
CREATE TABLE Commission.CommissionPlan (
    CommissionPlanId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    PlanCode          NVARCHAR(50)     NOT NULL,
    PlanName          NVARCHAR(200)    NOT NULL,
    EffectiveStartDate DATE            NOT NULL,
    EffectiveEndDate  DATE             NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commission.CommissionPayee'))
CREATE TABLE Commission.CommissionPayee (
    PayeeId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    UserId            UNIQUEIDENTIFIER NOT NULL,
    CommissionPlanId  UNIQUEIDENTIFIER NOT NULL,
    PayeeTypeCode     NVARCHAR(50)     NOT NULL DEFAULT 'SalesRep',
    SplitPercentage   DECIMAL(8,4)     NOT NULL DEFAULT 100,
    EffectiveDate     DATE             NOT NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commission.CommissionTransaction'))
CREATE TABLE Commission.CommissionTransaction (
    TransactionId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    PayeeId           UNIQUEIDENTIFIER NOT NULL,
    CommissionPlanId  UNIQUEIDENTIFIER NOT NULL,
    SourceEntityName  NVARCHAR(100)    NOT NULL,
    SourceEntityId    UNIQUEIDENTIFIER NOT NULL,
    TransactionDate   DATE             NOT NULL,
    GrossAmount       DECIMAL(18,2)    NOT NULL,
    CommissionRate    DECIMAL(8,4)     NOT NULL,
    CommissionAmount  DECIMAL(18,2)    NOT NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    PayoutId          UNIQUEIDENTIFIER NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commission.CommissionPayout'))
CREATE TABLE Commission.CommissionPayout (
    PayoutId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    PayeeId           UNIQUEIDENTIFIER NOT NULL,
    PayoutDate        DATE             NOT NULL,
    TotalAmount       DECIMAL(18,2)    NOT NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    ProcessedDateUtc  DATETIME2        NULL,
    Notes             NVARCHAR(500)    NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── 2.8 extended: plan versioning ───────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commission.CommissionPlanVersion'))
CREATE TABLE Commission.CommissionPlanVersion (
    PlanVersionId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    CommissionPlanId      UNIQUEIDENTIFIER NOT NULL,
    VersionNumber         INT              NOT NULL DEFAULT 1,
    PlanName              NVARCHAR(200)    NOT NULL,
    BaseRatePct           DECIMAL(10,4)    NOT NULL DEFAULT 0,
    EffectiveStartDate    DATE             NOT NULL,
    EffectiveEndDate      DATE             NULL,
    StatusCode            NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
    CreatedByUserId       UNIQUEIDENTIFIER NULL,
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc       DATETIME2        NULL,
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

-- ── 2.8 extended: split and override rules ───────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commission.CommissionSplitRule'))
CREATE TABLE Commission.CommissionSplitRule (
    SplitRuleId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    CommissionPlanId      UNIQUEIDENTIFIER NOT NULL,
    RuleName              NVARCHAR(200)    NOT NULL,
    SplitTypeCode         NVARCHAR(50)     NOT NULL DEFAULT 'Percentage',
    PayeeId               UNIQUEIDENTIFIER NULL,
    SplitPct              DECIMAL(10,4)    NOT NULL DEFAULT 0,
    OverrideRatePct       DECIMAL(10,4)    NULL,
    Priority              INT              NOT NULL DEFAULT 1,
    EffectiveStartDate    DATE             NOT NULL,
    EffectiveEndDate      DATE             NULL,
    StatusCode            NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc       DATETIME2        NULL,
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

-- ── 2.8 extended: calculation engine results ─────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commission.CommissionCalculationResult'))
CREATE TABLE Commission.CommissionCalculationResult (
    CalculationResultId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    TransactionId         UNIQUEIDENTIFIER NOT NULL,
    PayeeId               UNIQUEIDENTIFIER NOT NULL,
    CommissionPlanId      UNIQUEIDENTIFIER NOT NULL,
    BaseAmount            DECIMAL(18,2)    NOT NULL DEFAULT 0,
    RatePct               DECIMAL(10,4)    NOT NULL DEFAULT 0,
    SplitPct              DECIMAL(10,4)    NOT NULL DEFAULT 100,
    CalculatedAmount      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    AdjustedAmount        DECIMAL(18,2)    NULL,
    StatusCode            NVARCHAR(50)     NOT NULL DEFAULT 'Calculated',
    CalculatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc       DATETIME2        NULL,
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

-- ── 2.8 extended: clawbacks and reversals ────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commission.CommissionClawback'))
CREATE TABLE Commission.CommissionClawback (
    ClawbackId            UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    PayeeId               UNIQUEIDENTIFIER NOT NULL,
    OriginalTransactionId UNIQUEIDENTIFIER NOT NULL,
    ClawbackDate          DATE             NOT NULL,
    Amount                DECIMAL(18,2)    NOT NULL,
    ReasonCode            NVARCHAR(50)     NOT NULL DEFAULT 'Reversal',
    Notes                 NVARCHAR(MAX)    NULL,
    ApprovedByUserId      UNIQUEIDENTIFIER NULL,
    ApprovedDateUtc       DATETIME2        NULL,
    StatusCode            NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc       DATETIME2        NULL,
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

-- ── 2.8 extended: payout batches ─────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commission.CommissionPayoutBatch'))
CREATE TABLE Commission.CommissionPayoutBatch (
    PayoutBatchId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    BatchReference        NVARCHAR(100)    NOT NULL,
    PayPeriodStart        DATE             NOT NULL,
    PayPeriodEnd          DATE             NOT NULL,
    TotalAmount           DECIMAL(18,2)    NOT NULL DEFAULT 0,
    PayoutCount           INT              NOT NULL DEFAULT 0,
    StatusCode            NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    ProcessedByUserId     UNIQUEIDENTIFIER NULL,
    ProcessedDateUtc      DATETIME2        NULL,
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc       DATETIME2        NULL,
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

-- ── 2.8 extended: dispute handling ───────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commission.CommissionDispute'))
CREATE TABLE Commission.CommissionDispute (
    DisputeId             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    PayeeId               UNIQUEIDENTIFIER NOT NULL,
    TransactionId         UNIQUEIDENTIFIER NULL,
    DisputeDate           DATE             NOT NULL,
    DisputeReason         NVARCHAR(MAX)    NOT NULL DEFAULT '',
    DisputedAmount        DECIMAL(18,2)    NOT NULL,
    Resolution            NVARCHAR(MAX)    NULL,
    ResolvedByUserId      UNIQUEIDENTIFIER NULL,
    ResolvedDateUtc       DATETIME2        NULL,
    StatusCode            NVARCHAR(50)     NOT NULL DEFAULT 'Open',
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc       DATETIME2        NULL,
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

-- ── 2.8 extended: payout statements ──────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commission.CommissionPayoutStatement'))
CREATE TABLE Commission.CommissionPayoutStatement (
    StatementId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    PayeeId               UNIQUEIDENTIFIER NOT NULL,
    PayoutBatchId         UNIQUEIDENTIFIER NULL,
    StatementDate         DATE             NOT NULL,
    GrossEarnings         DECIMAL(18,2)    NOT NULL DEFAULT 0,
    TotalClawbacks        DECIMAL(18,2)    NOT NULL DEFAULT 0,
    NetPayout             DECIMAL(18,2)    NOT NULL DEFAULT 0,
    CurrencyCode          NVARCHAR(10)     NOT NULL DEFAULT 'USD',
    StatusCode            NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
    IssuedDateUtc         DATETIME2        NULL,
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc       DATETIME2        NULL,
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

-- ── 2.8 extended: commission accounting integration ──────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Commission.CommissionAccrualEntry'))
CREATE TABLE Commission.CommissionAccrualEntry (
    AccrualEntryId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    TransactionId         UNIQUEIDENTIFIER NOT NULL,
    GLAccountId           UNIQUEIDENTIFIER NULL,
    AccrualDate           DATE             NOT NULL,
    AccruedAmount         DECIMAL(18,2)    NOT NULL DEFAULT 0,
    ReversalDate          DATE             NULL,
    ReversedAmount        DECIMAL(18,2)    NULL,
    JournalEntryId        UNIQUEIDENTIFIER NULL,
    StatusCode            NVARCHAR(50)     NOT NULL DEFAULT 'Accrued',
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc       DATETIME2        NULL,
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

-- ── 2.8 seed data ────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Commission.CommissionPlanVersion WHERE TenantId = '00000000-0000-0000-0000-000000000001')
INSERT INTO Commission.CommissionPlanVersion (PlanVersionId, TenantId, CommissionPlanId, VersionNumber, PlanName, BaseRatePct, EffectiveStartDate, StatusCode)
VALUES ('b1000001-2800-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001',
        '00000000-0000-0000-0000-000000000001', 1, 'Standard Plan v1', 10.0000, '2025-01-01', 'Active');

IF NOT EXISTS (SELECT 1 FROM Commission.CommissionSplitRule WHERE TenantId = '00000000-0000-0000-0000-000000000001')
INSERT INTO Commission.CommissionSplitRule (SplitRuleId, TenantId, CommissionPlanId, RuleName, SplitTypeCode, SplitPct, Priority, EffectiveStartDate, StatusCode)
VALUES ('b2000001-2800-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001',
        '00000000-0000-0000-0000-000000000001', 'Primary Rep Split', 'Percentage', 70.0000, 1, '2025-01-01', 'Active');

IF NOT EXISTS (SELECT 1 FROM Commission.CommissionPayoutBatch WHERE TenantId = '00000000-0000-0000-0000-000000000001')
INSERT INTO Commission.CommissionPayoutBatch (PayoutBatchId, TenantId, BatchReference, PayPeriodStart, PayPeriodEnd, TotalAmount, PayoutCount, StatusCode)
VALUES ('b3000001-2800-0000-0000-000000000001', '00000000-0000-0000-0000-000000000001',
        'BATCH-2025-001', '2025-01-01', '2025-01-31', 5000.00, 3, 'Pending');

-- ============================================================
-- 2.9  WORKFLOW AND APPROVAL ENGINE
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Workflow.WorkflowInstance'))
CREATE TABLE Workflow.WorkflowInstance (
    WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    TargetEntityName   NVARCHAR(200)    NOT NULL,
    TargetEntityId     UNIQUEIDENTIFIER NOT NULL,
    StatusCodeId       INT              NOT NULL DEFAULT 1,
    SubmittedDateUtc   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Workflow.ApprovalStep'))
CREATE TABLE Workflow.ApprovalStep (
    ApprovalStepId    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    WorkflowInstanceId UNIQUEIDENTIFIER NOT NULL,
    StepOrder         INT              NOT NULL,
    ApproverUserId    UNIQUEIDENTIFIER NOT NULL,
    DelegatedToUserId UNIQUEIDENTIFIER NULL,
    DecisionCode      NVARCHAR(50)     NULL,
    DecisionNotes     NVARCHAR(500)    NULL,
    DecisionDateUtc   DATETIME2        NULL,
    DueByDateUtc      DATETIME2        NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── 2.9 extended: approval routing rules ────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Workflow.WorkflowApprovalRoute'))
CREATE TABLE Workflow.WorkflowApprovalRoute (
    RouteId               UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NULL,
    WorkflowDefinitionId  UNIQUEIDENTIFIER NOT NULL,
    StepOrder             INT              NOT NULL DEFAULT 1,
    StepName              NVARCHAR(200)    NOT NULL,
    ApproverUserId        UNIQUEIDENTIFIER NULL,
    ApproverRoleCode      NVARCHAR(100)    NULL,
    ApproverGroupId       UNIQUEIDENTIFIER NULL,
    ThresholdMinAmount    DECIMAL(18,2)    NULL,
    ThresholdMaxAmount    DECIMAL(18,2)    NULL,
    RequireAllApprovers   BIT              NOT NULL DEFAULT 0,
    IsActive              BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc       DATETIME2        NULL,
    CreatedByUserId       UNIQUEIDENTIFIER NULL,
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

-- ── 2.9 extended: delegated approval assignments ─────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Workflow.WorkflowApprovalDelegation'))
CREATE TABLE Workflow.WorkflowApprovalDelegation (
    DelegationId             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId                 UNIQUEIDENTIFIER NOT NULL,
    DelegatorUserId          UNIQUEIDENTIFIER NOT NULL,
    DelegateUserId           UNIQUEIDENTIFIER NOT NULL,
    WorkflowDefinitionId     UNIQUEIDENTIFIER NULL,
    DelegationStartDateUtc   DATETIME2        NOT NULL,
    DelegationEndDateUtc     DATETIME2        NOT NULL,
    Reason                   NVARCHAR(500)    NULL,
    IsActive                 BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc           DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId          UNIQUEIDENTIFIER NULL,
    IsDeleted                BIT              NOT NULL DEFAULT 0
);

-- ── 2.9 extended: SLA rules per workflow definition ──────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Workflow.WorkflowSlaRule'))
CREATE TABLE Workflow.WorkflowSlaRule (
    SlaRuleId             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NULL,
    WorkflowDefinitionId  UNIQUEIDENTIFIER NOT NULL,
    StepOrder             INT              NULL,
    SlaHours              INT              NOT NULL DEFAULT 24,
    EscalationUserId      UNIQUEIDENTIFIER NULL,
    EscalationRoleCode    NVARCHAR(100)    NULL,
    EscalationMessage     NVARCHAR(500)    NULL,
    IsActive              BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc       DATETIME2        NULL,
    CreatedByUserId       UNIQUEIDENTIFIER NULL,
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

-- ── 2.9 extended: SLA escalation events ──────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Workflow.WorkflowSlaEscalation'))
CREATE TABLE Workflow.WorkflowSlaEscalation (
    EscalationId              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId                  UNIQUEIDENTIFIER NOT NULL,
    WorkflowInstanceId        UNIQUEIDENTIFIER NOT NULL,
    ApprovalStepId            UNIQUEIDENTIFIER NULL,
    SlaRuleId                 UNIQUEIDENTIFIER NULL,
    EscalatedToUserId         UNIQUEIDENTIFIER NULL,
    EscalationDateUtc         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    BreachHours               INT              NOT NULL DEFAULT 0,
    NotificationSentDateUtc   DATETIME2        NULL,
    StatusCode                NVARCHAR(50)     NOT NULL DEFAULT 'Open',
    ResolvedDateUtc           DATETIME2        NULL,
    CreatedDateUtc            DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted                 BIT              NOT NULL DEFAULT 0
);

-- ── 2.9 extended: full approval history / audit trail ────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Workflow.WorkflowApprovalHistory'))
CREATE TABLE Workflow.WorkflowApprovalHistory (
    HistoryId             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    WorkflowInstanceId    UNIQUEIDENTIFIER NOT NULL,
    ApprovalStepId        UNIQUEIDENTIFIER NULL,
    ActorUserId           UNIQUEIDENTIFIER NULL,
    ActionCode            NVARCHAR(50)     NOT NULL DEFAULT 'Submitted',
    Notes                 NVARCHAR(500)    NULL,
    PreviousStatusCode    NVARCHAR(50)     NULL,
    NewStatusCode         NVARCHAR(50)     NULL,
    IsDelegated           BIT              NOT NULL DEFAULT 0,
    DelegatedByUserId     UNIQUEIDENTIFIER NULL,
    ActionDateUtc         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

-- ── 2.9 extended: maker-checker control rules ────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Workflow.WorkflowMakerCheckerRule'))
CREATE TABLE Workflow.WorkflowMakerCheckerRule (
    MakerCheckerRuleId    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NULL,
    EntityName            NVARCHAR(200)    NOT NULL,
    OperationCode         NVARCHAR(100)    NOT NULL DEFAULT 'Create',
    RequiresDifferentUser BIT              NOT NULL DEFAULT 1,
    MakerRoleCode         NVARCHAR(100)    NULL,
    CheckerRoleCode       NVARCHAR(100)    NULL,
    WorkflowDefinitionId  UNIQUEIDENTIFIER NULL,
    IsActive              BIT              NOT NULL DEFAULT 1,
    IsSystemDefined       BIT              NOT NULL DEFAULT 0,
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc       DATETIME2        NULL,
    CreatedByUserId       UNIQUEIDENTIFIER NULL,
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

-- ── 2.9 extended: rejection and rework loop requests ─────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Workflow.WorkflowReworkRequest'))
CREATE TABLE Workflow.WorkflowReworkRequest (
    ReworkRequestId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId              UNIQUEIDENTIFIER NOT NULL,
    WorkflowInstanceId    UNIQUEIDENTIFIER NOT NULL,
    ApprovalStepId        UNIQUEIDENTIFIER NULL,
    RequestedByUserId     UNIQUEIDENTIFIER NULL,
    RejectionReason       NVARCHAR(MAX)    NOT NULL DEFAULT '',
    ReworkInstructions    NVARCHAR(MAX)    NULL,
    ReturnToStepOrder     INT              NULL,
    StatusCode            NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    ResubmittedByUserId   UNIQUEIDENTIFIER NULL,
    ResubmittedDateUtc    DATETIME2        NULL,
    ResolvedDateUtc       DATETIME2        NULL,
    CreatedDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc       DATETIME2        NULL,
    IsDeleted             BIT              NOT NULL DEFAULT 0
);

-- ── 2.9 seed data ────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM Workflow.WorkflowApprovalRoute WHERE WorkflowDefinitionId = (SELECT TOP 1 WorkflowDefinitionId FROM Workflow.WorkflowDefinition WHERE WorkflowCode = 'INVOICE_APPROVAL'))
    INSERT INTO Workflow.WorkflowApprovalRoute (TenantId, WorkflowDefinitionId, StepOrder, StepName, ApproverRoleCode, ThresholdMinAmount, IsActive)
    SELECT NULL, WorkflowDefinitionId, 1, 'Finance Manager Approval', 'FINANCE_MANAGER', 10000.00, 1
    FROM Workflow.WorkflowDefinition WHERE WorkflowCode = 'INVOICE_APPROVAL';

IF NOT EXISTS (SELECT 1 FROM Workflow.WorkflowSlaRule WHERE WorkflowDefinitionId = (SELECT TOP 1 WorkflowDefinitionId FROM Workflow.WorkflowDefinition WHERE WorkflowCode = 'INVOICE_APPROVAL'))
    INSERT INTO Workflow.WorkflowSlaRule (TenantId, WorkflowDefinitionId, StepOrder, SlaHours, EscalationRoleCode, EscalationMessage, IsActive)
    SELECT NULL, WorkflowDefinitionId, 1, 48, 'FINANCE_DIRECTOR', 'Invoice approval SLA breached – escalating to Finance Director', 1
    FROM Workflow.WorkflowDefinition WHERE WorkflowCode = 'INVOICE_APPROVAL';

IF NOT EXISTS (SELECT 1 FROM Workflow.WorkflowMakerCheckerRule WHERE EntityName = 'Finance.Invoice')
    INSERT INTO Workflow.WorkflowMakerCheckerRule (TenantId, EntityName, OperationCode, RequiresDifferentUser, MakerRoleCode, CheckerRoleCode, IsActive, IsSystemDefined)
    VALUES (NULL, 'Finance.Invoice', 'Approve', 1, 'INVOICE_CREATOR', 'INVOICE_APPROVER', 1, 1);

IF NOT EXISTS (SELECT 1 FROM Workflow.WorkflowMakerCheckerRule WHERE EntityName = 'Billing.Payment')
    INSERT INTO Workflow.WorkflowMakerCheckerRule (TenantId, EntityName, OperationCode, RequiresDifferentUser, MakerRoleCode, CheckerRoleCode, IsActive, IsSystemDefined)
    VALUES (NULL, 'Billing.Payment', 'Approve', 1, 'PAYMENT_INITIATOR', 'PAYMENT_APPROVER', 1, 1);

-- ============================================================
-- 2.10  DOCUMENT MANAGEMENT
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('DMS.Document'))
CREATE TABLE DMS.Document (
    DocumentId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    DocumentTypeCode  NVARCHAR(100)    NOT NULL,
    EntityName        NVARCHAR(100)    NULL,
    EntityId          UNIQUEIDENTIFIER NULL,
    FileName          NVARCHAR(500)    NOT NULL,
    StoragePath       NVARCHAR(1000)   NOT NULL,
    ContentType       NVARCHAR(200)    NULL,
    FileSizeBytes     BIGINT           NULL,
    VersionNumber     INT              NOT NULL DEFAULT 1,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    RetentionDate     DATE             NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- 2.11  AUDIT  (AuditLog already exists)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Audit.AuditLog'))
CREATE TABLE Audit.AuditLog (
    AuditLogId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    EntityName        NVARCHAR(200)    NOT NULL,
    EntityId          UNIQUEIDENTIFIER NOT NULL,
    EventTypeCode     NVARCHAR(100)    NOT NULL,
    ActionName        NVARCHAR(300)    NOT NULL,
    PerformedByUserId UNIQUEIDENTIFIER NULL,
    OldValues         NVARCHAR(MAX)    NULL,
    NewValues         NVARCHAR(MAX)    NULL,
    IpAddress         NVARCHAR(50)     NULL,
    PerformedDateUtc  DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- 2.13  VIRTUAL ASSISTANT  (AssistantConversation already exists)
-- ============================================================

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Assistant.AssistantConversation'))
CREATE TABLE Assistant.AssistantConversation (
    AssistantConversationId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    UserId            UNIQUEIDENTIFIER NOT NULL,
    ContextEntityName NVARCHAR(200)    NULL,
    ContextEntityId   UNIQUEIDENTIFIER NULL,
    StartedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Assistant.AssistantMessage'))
CREATE TABLE Assistant.AssistantMessage (
    MessageId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    ConversationId    UNIQUEIDENTIFIER NOT NULL,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    Role              NVARCHAR(20)     NOT NULL DEFAULT 'user',
    Content           NVARCHAR(MAX)    NOT NULL,
    SentDateUtc       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- Safe column migrations (idempotent ALTER TABLE)
-- ============================================================

-- Core.Tenant
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Tenant') AND name = 'PlanCode')
    ALTER TABLE Core.Tenant ADD PlanCode NVARCHAR(50) NOT NULL DEFAULT 'Standard';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Tenant') AND name = 'Locale')
    ALTER TABLE Core.Tenant ADD Locale NVARCHAR(20) NOT NULL DEFAULT 'en-US';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Tenant') AND name = 'CurrencyCode')
    ALTER TABLE Core.Tenant ADD CurrencyCode NVARCHAR(10) NOT NULL DEFAULT 'USD';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Tenant') AND name = 'TimeZoneId')
    ALTER TABLE Core.Tenant ADD TimeZoneId NVARCHAR(100) NOT NULL DEFAULT 'UTC';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Tenant') AND name = 'ModifiedDateUtc')
    ALTER TABLE Core.Tenant ADD ModifiedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Tenant') AND name = 'IsDeleted')
    ALTER TABLE Core.Tenant ADD IsDeleted BIT NOT NULL DEFAULT 0;

-- Billing.Payment
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Billing.Payment') AND name = 'Amount')
    ALTER TABLE Billing.Payment ADD Amount DECIMAL(18,2) NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Billing.Payment') AND name = 'StatusCode')
    ALTER TABLE Billing.Payment ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Applied';

-- Billing.ExpenseEntry
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Billing.ExpenseEntry') AND name = 'Amount')
    ALTER TABLE Billing.ExpenseEntry ADD Amount DECIMAL(18,2) NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Billing.ExpenseEntry') AND name = 'StatusCode')
    ALTER TABLE Billing.ExpenseEntry ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Draft';

-- Billing.TimeEntry
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Billing.TimeEntry') AND name = 'StatusCode')
    ALTER TABLE Billing.TimeEntry ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Draft';

-- OPS.Engagement
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OPS.Engagement') AND name = 'StatusCode')
    ALTER TABLE OPS.Engagement ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Active';

-- OPS.EngagementTask
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OPS.EngagementTask') AND name = 'StatusCode')
    ALTER TABLE OPS.EngagementTask ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Open';

-- Finance.JournalEntry
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Finance.JournalEntry') AND name = 'StatusCode')
    ALTER TABLE Finance.JournalEntry ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Draft';

-- Commission.CommissionPayout
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commission.CommissionPayout') AND name = 'StatusCode')
    ALTER TABLE Commission.CommissionPayout ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Pending';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commission.CommissionPayout') AND name = 'TotalAmount')
    ALTER TABLE Commission.CommissionPayout ADD TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0;

-- Commission.CommissionTransaction
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commission.CommissionTransaction') AND name = 'StatusCode')
    ALTER TABLE Commission.CommissionTransaction ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Pending';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commission.CommissionTransaction') AND name = 'GrossAmount')
    ALTER TABLE Commission.CommissionTransaction ADD GrossAmount DECIMAL(18,2) NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commission.CommissionTransaction') AND name = 'CommissionAmount')
    ALTER TABLE Commission.CommissionTransaction ADD CommissionAmount DECIMAL(18,2) NOT NULL DEFAULT 0;

-- Audit.AuditLog
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Audit.AuditLog') AND name = 'CreatedDateUtc')
    ALTER TABLE Audit.AuditLog ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- IAM.[User]
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.[User]') AND name = 'FullName')
    ALTER TABLE IAM.[User] ADD FullName NVARCHAR(300) NOT NULL DEFAULT '';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.[User]') AND name = 'StatusCode')
    ALTER TABLE IAM.[User] ADD StatusCode NVARCHAR(50) NOT NULL DEFAULT 'Active';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.[User]') AND name = 'MfaEnabled')
    ALTER TABLE IAM.[User] ADD MfaEnabled BIT NOT NULL DEFAULT 0;

-- IAM.Role
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.Role') AND name = 'RoleTypeCode')
    ALTER TABLE IAM.Role ADD RoleTypeCode NVARCHAR(50) NOT NULL DEFAULT 'Internal';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.Role') AND name = 'Description')
    ALTER TABLE IAM.Role ADD Description NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.Role') AND name = 'CreatedDateUtc')
    ALTER TABLE IAM.Role ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.Role') AND name = 'ModifiedDateUtc')
    ALTER TABLE IAM.Role ADD ModifiedDateUtc DATETIME2 NULL;

-- IAM.RoleBundle
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.RoleBundle') AND name = 'BundleId')
    ALTER TABLE IAM.RoleBundle ADD BundleId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.RoleBundle') AND name = 'Description')
    ALTER TABLE IAM.RoleBundle ADD Description NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.RoleBundle') AND name = 'SortOrder')
    ALTER TABLE IAM.RoleBundle ADD SortOrder INT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.RoleBundle') AND name = 'ModifiedDateUtc')
    ALTER TABLE IAM.RoleBundle ADD ModifiedDateUtc DATETIME2 NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.RoleBundle') AND name = 'ModifiedByUserId')
    ALTER TABLE IAM.RoleBundle ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.RoleBundle') AND name = 'IsDeleted')
    ALTER TABLE IAM.RoleBundle ADD IsDeleted BIT NOT NULL DEFAULT 0;

-- IAM.BundleRole
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.BundleRole') AND name = 'BundleId')
    ALTER TABLE IAM.BundleRole ADD BundleId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.BundleRole') AND name = 'IsDeleted')
    ALTER TABLE IAM.BundleRole ADD IsDeleted BIT NOT NULL DEFAULT 0;

-- IAM.BundleUser
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.BundleUser') AND name = 'BundleId')
    ALTER TABLE IAM.BundleUser ADD BundleId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('IAM.BundleUser') AND name = 'IsDeleted')
    ALTER TABLE IAM.BundleUser ADD IsDeleted BIT NOT NULL DEFAULT 0;

-- Core.Branch
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Branch') AND name = 'CreatedDateUtc')
    ALTER TABLE Core.Branch ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Core.Branch') AND name = 'ModifiedDateUtc')
    ALTER TABLE Core.Branch ADD ModifiedDateUtc DATETIME2 NULL;

-- CRM.Lead
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Lead') AND name = 'CreatedDateUtc')
    ALTER TABLE CRM.Lead ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Lead') AND name = 'ModifiedDateUtc')
    ALTER TABLE CRM.Lead ADD ModifiedDateUtc DATETIME2 NULL;

-- CRM.Opportunity
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Opportunity') AND name = 'CreatedDateUtc')
    ALTER TABLE CRM.Opportunity ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Opportunity') AND name = 'ModifiedDateUtc')
    ALTER TABLE CRM.Opportunity ADD ModifiedDateUtc DATETIME2 NULL;

-- CRM.Quote
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Quote') AND name = 'CreatedDateUtc')
    ALTER TABLE CRM.Quote ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Quote') AND name = 'ModifiedDateUtc')
    ALTER TABLE CRM.Quote ADD ModifiedDateUtc DATETIME2 NULL;

-- Client.Account
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'CreatedDateUtc')
    ALTER TABLE Client.Account ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'ModifiedDateUtc')
    ALTER TABLE Client.Account ADD ModifiedDateUtc DATETIME2 NULL;

-- Client.Contact
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Contact') AND name = 'CreatedDateUtc')
    ALTER TABLE Client.Contact ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Contact') AND name = 'ModifiedDateUtc')
    ALTER TABLE Client.Contact ADD ModifiedDateUtc DATETIME2 NULL;

-- Finance.Agreement
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Finance.Agreement') AND name = 'CreatedDateUtc')
    ALTER TABLE Finance.Agreement ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Finance.Agreement') AND name = 'ModifiedDateUtc')
    ALTER TABLE Finance.Agreement ADD ModifiedDateUtc DATETIME2 NULL;

-- OPS.Engagement
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OPS.Engagement') AND name = 'CreatedDateUtc')
    ALTER TABLE OPS.Engagement ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OPS.Engagement') AND name = 'ModifiedDateUtc')
    ALTER TABLE OPS.Engagement ADD ModifiedDateUtc DATETIME2 NULL;

-- OPS.EngagementMilestone
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OPS.EngagementMilestone') AND name = 'CreatedDateUtc')
    ALTER TABLE OPS.EngagementMilestone ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- OPS.EngagementTask
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OPS.EngagementTask') AND name = 'CreatedDateUtc')
    ALTER TABLE OPS.EngagementTask ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- OPS.IssueTracker
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('OPS.IssueTracker') AND name = 'CreatedDateUtc')
    ALTER TABLE OPS.IssueTracker ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- Billing.TimeEntry
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Billing.TimeEntry') AND name = 'CreatedDateUtc')
    ALTER TABLE Billing.TimeEntry ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- Billing.ExpenseEntry
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Billing.ExpenseEntry') AND name = 'CreatedDateUtc')
    ALTER TABLE Billing.ExpenseEntry ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- Finance.Invoice
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Finance.Invoice') AND name = 'CreatedDateUtc')
    ALTER TABLE Finance.Invoice ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Finance.Invoice') AND name = 'ModifiedDateUtc')
    ALTER TABLE Finance.Invoice ADD ModifiedDateUtc DATETIME2 NULL;

-- Billing.Payment
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Billing.Payment') AND name = 'CreatedDateUtc')
    ALTER TABLE Billing.Payment ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- Billing.CollectionsNote
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Billing.CollectionsNote') AND name = 'CreatedDateUtc')
    ALTER TABLE Billing.CollectionsNote ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- Finance.GLAccount
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Finance.GLAccount') AND name = 'CreatedDateUtc')
    ALTER TABLE Finance.GLAccount ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- Finance.JournalEntry
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Finance.JournalEntry') AND name = 'CreatedDateUtc')
    ALTER TABLE Finance.JournalEntry ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- Finance.BankReconciliation
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Finance.BankReconciliation') AND name = 'CreatedDateUtc')
    ALTER TABLE Finance.BankReconciliation ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- Commission.CommissionPlan
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commission.CommissionPlan') AND name = 'CreatedDateUtc')
    ALTER TABLE Commission.CommissionPlan ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commission.CommissionPlan') AND name = 'ModifiedDateUtc')
    ALTER TABLE Commission.CommissionPlan ADD ModifiedDateUtc DATETIME2 NULL;

-- Commission.CommissionPayee
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commission.CommissionPayee') AND name = 'CreatedDateUtc')
    ALTER TABLE Commission.CommissionPayee ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- Commission.CommissionTransaction
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commission.CommissionTransaction') AND name = 'CreatedDateUtc')
    ALTER TABLE Commission.CommissionTransaction ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- Commission.CommissionPayout
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Commission.CommissionPayout') AND name = 'CreatedDateUtc')
    ALTER TABLE Commission.CommissionPayout ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- Workflow.WorkflowInstance
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Workflow.WorkflowInstance') AND name = 'CreatedDateUtc')
    ALTER TABLE Workflow.WorkflowInstance ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- Workflow.ApprovalStep
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Workflow.ApprovalStep') AND name = 'CreatedDateUtc')
    ALTER TABLE Workflow.ApprovalStep ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- DMS.Document
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('DMS.Document') AND name = 'CreatedDateUtc')
    ALTER TABLE DMS.Document ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

-- Assistant.AssistantConversation
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Assistant.AssistantConversation') AND name = 'CreatedDateUtc')
    ALTER TABLE Assistant.AssistantConversation ADD CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME();

GO

-- ============================================================ 
-- Seed default Tenant
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Core.Tenant WHERE TenantId = '00000000-0000-0000-0000-000000000001')
    INSERT INTO Core.Tenant (TenantId, TenantCode, TenantName, PlanCode, CurrencyCode, TimeZoneId)
    VALUES ('00000000-0000-0000-0000-000000000001', 'DEFAULT', 'Default Tenant', 'Enterprise', 'USD', 'UTC');
GO

-- ============================================================
-- 2.1  PLATFORM CORE  –  Extended Engine Tables
-- ============================================================

-- ── White-label branding per tenant ─────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.TenantBranding'))
CREATE TABLE Core.TenantBranding (
    BrandingId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    WhiteLabelName    NVARCHAR(200)    NULL,
    LogoUrl           NVARCHAR(1000)   NULL,
    FaviconUrl        NVARCHAR(1000)   NULL,
    PrimaryColor      NVARCHAR(20)     NULL DEFAULT '#0d6efd',
    SecondaryColor    NVARCHAR(20)     NULL DEFAULT '#6c757d',
    AccentColor       NVARCHAR(20)     NULL DEFAULT '#198754',
    CustomDomain      NVARCHAR(300)    NULL,
    CustomCssUrl      NVARCHAR(1000)   NULL,
    SupportEmail      NVARCHAR(300)    NULL,
    SupportPhone      NVARCHAR(50)     NULL,
    FooterText        NVARCHAR(500)    NULL,
    IsActive          BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Notification engine: reusable templates ──────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.NotificationTemplate'))
CREATE TABLE Core.NotificationTemplate (
    TemplateId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NULL,
    TemplateCode      NVARCHAR(100)    NOT NULL,
    TemplateName      NVARCHAR(200)    NOT NULL,
    ChannelCode       NVARCHAR(50)     NOT NULL DEFAULT 'Email',
    SubjectTemplate   NVARCHAR(500)    NULL,
    BodyTemplate      NVARCHAR(MAX)    NOT NULL,
    IsSystemTemplate  BIT              NOT NULL DEFAULT 0,
    IsActive          BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Notification engine: outbound notifications ──────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.Notification'))
CREATE TABLE Core.Notification (
    NotificationId    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    RecipientUserId   UNIQUEIDENTIFIER NOT NULL,
    TemplateId        UNIQUEIDENTIFIER NULL,
    ChannelCode       NVARCHAR(50)     NOT NULL DEFAULT 'InApp',
    Subject           NVARCHAR(500)    NULL,
    Body              NVARCHAR(MAX)    NOT NULL,
    EntityName        NVARCHAR(200)    NULL,
    EntityId          UNIQUEIDENTIFIER NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    IsRead            BIT              NOT NULL DEFAULT 0,
    ReadDateUtc       DATETIME2        NULL,
    SentDateUtc       DATETIME2        NULL,
    ErrorMessage      NVARCHAR(1000)   NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Notification engine: per-user channel preferences ────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.NotificationPreference'))
CREATE TABLE Core.NotificationPreference (
    PreferenceId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    UserId            UNIQUEIDENTIFIER NOT NULL,
    EventTypeCode     NVARCHAR(100)    NOT NULL,
    EmailEnabled      BIT              NOT NULL DEFAULT 1,
    SmsEnabled        BIT              NOT NULL DEFAULT 0,
    PushEnabled       BIT              NOT NULL DEFAULT 0,
    InAppEnabled      BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Reporting engine: report definitions ─────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.ReportDefinition'))
CREATE TABLE Core.ReportDefinition (
    ReportDefinitionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NULL,
    ReportCode         NVARCHAR(100)    NOT NULL,
    ReportName         NVARCHAR(200)    NOT NULL,
    Description        NVARCHAR(500)    NULL,
    ModuleCode         NVARCHAR(100)    NOT NULL,
    ReportTypeCode     NVARCHAR(50)     NOT NULL DEFAULT 'Tabular',
    QueryTemplate      NVARCHAR(MAX)    NULL,
    DefaultParameters  NVARCHAR(MAX)    NULL,
    OutputFormats      NVARCHAR(200)    NOT NULL DEFAULT 'PDF,Excel,CSV',
    IsSystemReport     BIT              NOT NULL DEFAULT 0,
    IsActive           BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc    DATETIME2        NULL,
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Reporting engine: scheduled report runs ──────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.ReportSchedule'))
CREATE TABLE Core.ReportSchedule (
    ReportScheduleId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    ReportDefinitionId UNIQUEIDENTIFIER NOT NULL,
    ScheduleName       NVARCHAR(200)    NOT NULL,
    CronExpression     NVARCHAR(100)    NOT NULL,
    Parameters         NVARCHAR(MAX)    NULL,
    RecipientUserIds   NVARCHAR(MAX)    NULL,
    OutputFormat       NVARCHAR(50)     NOT NULL DEFAULT 'PDF',
    IsActive           BIT              NOT NULL DEFAULT 1,
    LastRunDateUtc     DATETIME2        NULL,
    NextRunDateUtc     DATETIME2        NULL,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Reporting engine: execution history ──────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.ReportExecution'))
CREATE TABLE Core.ReportExecution (
    ReportExecutionId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    ReportDefinitionId UNIQUEIDENTIFIER NOT NULL,
    ReportScheduleId   UNIQUEIDENTIFIER NULL,
    Parameters         NVARCHAR(MAX)    NULL,
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Queued',
    OutputFormat       NVARCHAR(50)     NOT NULL DEFAULT 'PDF',
    StoragePath        NVARCHAR(1000)   NULL,
    FileSizeBytes      BIGINT           NULL,
    [RowCount]           INT              NULL,
    StartedDateUtc     DATETIME2        NULL,
    CompletedDateUtc   DATETIME2        NULL,
    ErrorMessage       NVARCHAR(1000)   NULL,
    RequestedByUserId  UNIQUEIDENTIFIER NULL,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Configuration management: scoped key-value store ─────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.ConfigurationSetting'))
CREATE TABLE Core.ConfigurationSetting (
    SettingId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NULL,
    ScopeCode         NVARCHAR(50)     NOT NULL DEFAULT 'Tenant',
    ScopeEntityId     UNIQUEIDENTIFIER NULL,
    SettingKey        NVARCHAR(200)    NOT NULL,
    SettingValue      NVARCHAR(MAX)    NULL,
    DataTypeCode      NVARCHAR(50)     NOT NULL DEFAULT 'String',
    DefaultValue      NVARCHAR(MAX)    NULL,
    Description       NVARCHAR(500)    NULL,
    IsEncrypted       BIT              NOT NULL DEFAULT 0,
    IsReadOnly        BIT              NOT NULL DEFAULT 0,
    ModuleCode        NVARCHAR(100)    NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    ModifiedByUserId  UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Localization: supported locale catalogue ─────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Core.SupportedLocale'))
CREATE TABLE Core.SupportedLocale (
    LocaleId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    LocaleCode        NVARCHAR(20)     NOT NULL,
    LocaleName        NVARCHAR(200)    NOT NULL,
    NativeName        NVARCHAR(200)    NULL,
    CurrencyCode      NVARCHAR(10)     NOT NULL DEFAULT 'USD',
    CurrencySymbol    NVARCHAR(10)     NULL,
    DateFormat        NVARCHAR(50)     NULL DEFAULT 'MM/dd/yyyy',
    TimeFormat        NVARCHAR(50)     NULL DEFAULT 'hh:mm tt',
    NumberFormat      NVARCHAR(50)     NULL,
    IsRtl             BIT              NOT NULL DEFAULT 0,
    IsActive          BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Workflow engine: workflow definition / template ──────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Workflow.WorkflowDefinition'))
CREATE TABLE Workflow.WorkflowDefinition (
    WorkflowDefinitionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NULL,
    WorkflowCode      NVARCHAR(100)    NOT NULL,
    WorkflowName      NVARCHAR(200)    NOT NULL,
    Description       NVARCHAR(500)    NULL,
    TargetEntityName  NVARCHAR(200)    NOT NULL,
    TriggerTypeCode   NVARCHAR(50)     NOT NULL DEFAULT 'Manual',
    StepDefinitions   NVARCHAR(MAX)    NULL,
    ThresholdAmount   DECIMAL(18,2)    NULL,
    IsActive          BIT              NOT NULL DEFAULT 1,
    IsSystemDefined   BIT              NOT NULL DEFAULT 0,
    Version           INT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Audit engine: granular field-level change log ────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Audit.FieldChangeLog'))
CREATE TABLE Audit.FieldChangeLog (
    FieldChangeLogId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    EntityName        NVARCHAR(200)    NOT NULL,
    EntityId          UNIQUEIDENTIFIER NOT NULL,
    FieldName         NVARCHAR(200)    NOT NULL,
    OldValue          NVARCHAR(MAX)    NULL,
    NewValue          NVARCHAR(MAX)    NULL,
    ChangedByUserId   UNIQUEIDENTIFIER NULL,
    ChangedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ChangeSource      NVARCHAR(100)    NULL,
    IpAddress         NVARCHAR(50)     NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Audit engine: security event log ────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Audit.SecurityEventLog'))
CREATE TABLE Audit.SecurityEventLog (
    SecurityEventId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    UserId            UNIQUEIDENTIFIER NULL,
    EventTypeCode     NVARCHAR(100)    NOT NULL,
    EventDescription  NVARCHAR(500)    NOT NULL,
    IpAddress         NVARCHAR(50)     NULL,
    UserAgent         NVARCHAR(500)    NULL,
    IsSuccess         BIT              NOT NULL DEFAULT 1,
    RiskScore         INT              NULL,
    SessionId         NVARCHAR(200)    NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Audit engine: data export log ───────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Audit.ExportLog'))
CREATE TABLE Audit.ExportLog (
    ExportLogId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    ExportedByUserId  UNIQUEIDENTIFIER NOT NULL,
    EntityName        NVARCHAR(200)    NOT NULL,
    ExportFormat      NVARCHAR(50)     NOT NULL DEFAULT 'CSV',
    RecordCount       INT              NULL,
    FilterParameters  NVARCHAR(MAX)    NULL,
    StoragePath       NVARCHAR(1000)   NULL,
    ExportedDateUtc   DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Document engine: secure share links ─────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('DMS.DocumentShareLink'))
CREATE TABLE DMS.DocumentShareLink (
    ShareLinkId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    DocumentId        UNIQUEIDENTIFIER NOT NULL,
    Token             NVARCHAR(500)    NOT NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NOT NULL,
    ExpiresDateUtc    DATETIME2        NOT NULL,
    MaxAccessCount    INT              NULL,
    AccessCount       INT              NOT NULL DEFAULT 0,
    RequiresPin       BIT              NOT NULL DEFAULT 0,
    PinHash           NVARCHAR(200)    NULL,
    IsRevoked         BIT              NOT NULL DEFAULT 0,
    RevokedDateUtc    DATETIME2        NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── IAM: user session / device tracking ─────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.UserSession'))
CREATE TABLE IAM.UserSession (
    SessionId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    UserId              UNIQUEIDENTIFIER NOT NULL,
    SessionToken        NVARCHAR(500)    NOT NULL,
    DeviceIdentifier    NVARCHAR(200)    NULL,
    DeviceType          NVARCHAR(100)    NULL,
    UserAgent           NVARCHAR(500)    NULL,
    IpAddress           NVARCHAR(50)     NULL,
    LoginDateUtc        DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    LastActivityDateUtc DATETIME2        NULL,
    ExpiresDateUtc      DATETIME2        NOT NULL,
    IsRevoked           BIT              NOT NULL DEFAULT 0,
    RevokedDateUtc      DATETIME2        NULL,
    RevokedReason       NVARCHAR(200)    NULL,
    CreatedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted           BIT              NOT NULL DEFAULT 0
);

GO

-- ============================================================
-- 2.1  PLATFORM CORE – Seed: Locales
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Core.SupportedLocale WHERE LocaleCode = 'en-US')
    INSERT INTO Core.SupportedLocale (LocaleCode, LocaleName, NativeName, CurrencyCode, CurrencySymbol, DateFormat, TimeFormat, IsRtl)
    VALUES ('en-US', 'English (United States)', 'English (United States)', 'USD', '$', 'MM/dd/yyyy', 'hh:mm tt', 0);

IF NOT EXISTS (SELECT 1 FROM Core.SupportedLocale WHERE LocaleCode = 'en-GB')
    INSERT INTO Core.SupportedLocale (LocaleCode, LocaleName, NativeName, CurrencyCode, CurrencySymbol, DateFormat, TimeFormat, IsRtl)
    VALUES ('en-GB', 'English (United Kingdom)', 'English (United Kingdom)', 'GBP', N'£', 'dd/MM/yyyy', 'HH:mm', 0);

IF NOT EXISTS (SELECT 1 FROM Core.SupportedLocale WHERE LocaleCode = 'es-ES')
    INSERT INTO Core.SupportedLocale (LocaleCode, LocaleName, NativeName, CurrencyCode, CurrencySymbol, DateFormat, TimeFormat, IsRtl)
    VALUES ('es-ES', 'Spanish (Spain)', N'Español (España)', 'EUR', N'€', 'dd/MM/yyyy', 'HH:mm', 0);

IF NOT EXISTS (SELECT 1 FROM Core.SupportedLocale WHERE LocaleCode = 'fr-FR')
    INSERT INTO Core.SupportedLocale (LocaleCode, LocaleName, NativeName, CurrencyCode, CurrencySymbol, DateFormat, TimeFormat, IsRtl)
    VALUES ('fr-FR', 'French (France)', N'Français (France)', 'EUR', N'€', 'dd/MM/yyyy', 'HH:mm', 0);

IF NOT EXISTS (SELECT 1 FROM Core.SupportedLocale WHERE LocaleCode = 'de-DE')
    INSERT INTO Core.SupportedLocale (LocaleCode, LocaleName, NativeName, CurrencyCode, CurrencySymbol, DateFormat, TimeFormat, IsRtl)
    VALUES ('de-DE', 'German (Germany)', N'Deutsch (Deutschland)', 'EUR', N'€', 'dd.MM.yyyy', 'HH:mm', 0);

IF NOT EXISTS (SELECT 1 FROM Core.SupportedLocale WHERE LocaleCode = 'pt-BR')
    INSERT INTO Core.SupportedLocale (LocaleCode, LocaleName, NativeName, CurrencyCode, CurrencySymbol, DateFormat, TimeFormat, IsRtl)
    VALUES ('pt-BR', 'Portuguese (Brazil)', N'Português (Brasil)', 'BRL', N'R$', 'dd/MM/yyyy', 'HH:mm', 0);

IF NOT EXISTS (SELECT 1 FROM Core.SupportedLocale WHERE LocaleCode = 'ja-JP')
    INSERT INTO Core.SupportedLocale (LocaleCode, LocaleName, NativeName, CurrencyCode, CurrencySymbol, DateFormat, TimeFormat, IsRtl)
    VALUES ('ja-JP', 'Japanese (Japan)', N'日本語 (日本)', 'JPY', N'¥', 'yyyy/MM/dd', 'HH:mm', 0);

IF NOT EXISTS (SELECT 1 FROM Core.SupportedLocale WHERE LocaleCode = 'ar-SA')
    INSERT INTO Core.SupportedLocale (LocaleCode, LocaleName, NativeName, CurrencyCode, CurrencySymbol, DateFormat, TimeFormat, IsRtl)
    VALUES ('ar-SA', 'Arabic (Saudi Arabia)', N'العربية (المملكة العربية السعودية)', 'SAR', N'ر.س', 'dd/MM/yyyy', 'hh:mm tt', 1);

-- ============================================================
-- 2.1  PLATFORM CORE – Seed: Notification Templates
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Core.NotificationTemplate WHERE TemplateCode = 'USER_WELCOME')
    INSERT INTO Core.NotificationTemplate (TemplateCode, TemplateName, ChannelCode, SubjectTemplate, BodyTemplate, IsSystemTemplate)
    VALUES ('USER_WELCOME', 'User Welcome', 'Email', 'Welcome to {{TenantName}}', 'Hello {{FullName}}, your account on {{TenantName}} is ready. Please sign in to get started.', 1);

IF NOT EXISTS (SELECT 1 FROM Core.NotificationTemplate WHERE TemplateCode = 'APPROVAL_REQUEST')
    INSERT INTO Core.NotificationTemplate (TemplateCode, TemplateName, ChannelCode, SubjectTemplate, BodyTemplate, IsSystemTemplate)
    VALUES ('APPROVAL_REQUEST', 'Approval Required', 'Email', 'Action Required: {{EntityName}} Approval', 'You have a pending approval for {{EntityName}} #{{EntityNumber}}. Please review and take action.', 1);

IF NOT EXISTS (SELECT 1 FROM Core.NotificationTemplate WHERE TemplateCode = 'APPROVAL_APPROVED')
    INSERT INTO Core.NotificationTemplate (TemplateCode, TemplateName, ChannelCode, SubjectTemplate, BodyTemplate, IsSystemTemplate)
    VALUES ('APPROVAL_APPROVED', 'Item Approved', 'Email', '{{EntityName}} #{{EntityNumber}} Approved', 'Your {{EntityName}} #{{EntityNumber}} has been approved.', 1);

IF NOT EXISTS (SELECT 1 FROM Core.NotificationTemplate WHERE TemplateCode = 'APPROVAL_REJECTED')
    INSERT INTO Core.NotificationTemplate (TemplateCode, TemplateName, ChannelCode, SubjectTemplate, BodyTemplate, IsSystemTemplate)
    VALUES ('APPROVAL_REJECTED', 'Item Rejected', 'Email', '{{EntityName}} #{{EntityNumber}} Rejected', 'Your {{EntityName}} #{{EntityNumber}} has been rejected. Notes: {{DecisionNotes}}', 1);

IF NOT EXISTS (SELECT 1 FROM Core.NotificationTemplate WHERE TemplateCode = 'INVOICE_DUE_REMINDER')
    INSERT INTO Core.NotificationTemplate (TemplateCode, TemplateName, ChannelCode, SubjectTemplate, BodyTemplate, IsSystemTemplate)
    VALUES ('INVOICE_DUE_REMINDER', 'Invoice Due Reminder', 'Email', 'Invoice {{InvoiceNumber}} Due {{DueDate}}', 'Invoice {{InvoiceNumber}} for {{Amount}} is due on {{DueDate}}. Please arrange payment.', 1);

IF NOT EXISTS (SELECT 1 FROM Core.NotificationTemplate WHERE TemplateCode = 'INVOICE_OVERDUE')
    INSERT INTO Core.NotificationTemplate (TemplateCode, TemplateName, ChannelCode, SubjectTemplate, BodyTemplate, IsSystemTemplate)
    VALUES ('INVOICE_OVERDUE', 'Invoice Overdue', 'Email', 'Overdue: Invoice {{InvoiceNumber}}', 'Invoice {{InvoiceNumber}} for {{Amount}} is now overdue. Outstanding balance: {{BalanceAmount}}.', 1);

IF NOT EXISTS (SELECT 1 FROM Core.NotificationTemplate WHERE TemplateCode = 'PASSWORD_RESET')
    INSERT INTO Core.NotificationTemplate (TemplateCode, TemplateName, ChannelCode, SubjectTemplate, BodyTemplate, IsSystemTemplate)
    VALUES ('PASSWORD_RESET', 'Password Reset', 'Email', 'Reset Your Password', 'Click the link to reset your password: {{ResetLink}}. Link expires in 30 minutes.', 1);

IF NOT EXISTS (SELECT 1 FROM Core.NotificationTemplate WHERE TemplateCode = 'MFA_CODE')
    INSERT INTO Core.NotificationTemplate (TemplateCode, TemplateName, ChannelCode, SubjectTemplate, BodyTemplate, IsSystemTemplate)
    VALUES ('MFA_CODE', 'MFA Verification Code', 'Email', 'Your Verification Code', 'Your one-time verification code is: {{Code}}. This code expires in 10 minutes.', 1);

-- ============================================================
-- 2.1  PLATFORM CORE – Seed: Workflow Definitions
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Workflow.WorkflowDefinition WHERE WorkflowCode = 'INVOICE_APPROVAL')
    INSERT INTO Workflow.WorkflowDefinition (WorkflowCode, WorkflowName, Description, TargetEntityName, TriggerTypeCode, ThresholdAmount, IsSystemDefined)
    VALUES ('INVOICE_APPROVAL', 'Invoice Approval', 'Automatic approval workflow for invoices exceeding threshold', 'Finance.Invoice', 'Automatic', 10000.00, 1);

IF NOT EXISTS (SELECT 1 FROM Workflow.WorkflowDefinition WHERE WorkflowCode = 'CONTRACT_APPROVAL')
    INSERT INTO Workflow.WorkflowDefinition (WorkflowCode, WorkflowName, Description, TargetEntityName, TriggerTypeCode, IsSystemDefined)
    VALUES ('CONTRACT_APPROVAL', 'Contract Approval', 'Manual approval workflow for client agreements', 'Finance.Agreement', 'Manual', 1);

IF NOT EXISTS (SELECT 1 FROM Workflow.WorkflowDefinition WHERE WorkflowCode = 'COMMISSION_PAYOUT_APPROVAL')
    INSERT INTO Workflow.WorkflowDefinition (WorkflowCode, WorkflowName, Description, TargetEntityName, TriggerTypeCode, IsSystemDefined)
    VALUES ('COMMISSION_PAYOUT_APPROVAL', 'Commission Payout Approval', 'Approval workflow for commission payouts', 'Commission.CommissionPayout', 'Automatic', 5000.00, 1);

IF NOT EXISTS (SELECT 1 FROM Workflow.WorkflowDefinition WHERE WorkflowCode = 'EXPENSE_APPROVAL')
    INSERT INTO Workflow.WorkflowDefinition (WorkflowCode, WorkflowName, Description, TargetEntityName, TriggerTypeCode, IsSystemDefined)
    VALUES ('EXPENSE_APPROVAL', 'Expense Approval', 'Approval workflow for expense entries', 'Billing.ExpenseEntry', 'Automatic', 500.00, 1);

-- ============================================================
-- 2.1  PLATFORM CORE – Seed: Configuration Settings
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Core.ConfigurationSetting WHERE SettingKey = 'Platform.MaxFileUploadMb' AND ScopeCode = 'Platform')
    INSERT INTO Core.ConfigurationSetting (ScopeCode, SettingKey, SettingValue, DataTypeCode, DefaultValue, Description, ModuleCode)
    VALUES ('Platform', 'Platform.MaxFileUploadMb', '50', 'Integer', '50', 'Maximum file upload size in megabytes', 'DMS');

IF NOT EXISTS (SELECT 1 FROM Core.ConfigurationSetting WHERE SettingKey = 'Platform.SessionTimeoutMinutes' AND ScopeCode = 'Platform')
    INSERT INTO Core.ConfigurationSetting (ScopeCode, SettingKey, SettingValue, DataTypeCode, DefaultValue, Description, ModuleCode)
    VALUES ('Platform', 'Platform.SessionTimeoutMinutes', '60', 'Integer', '60', 'Default session timeout in minutes', 'IAM');

IF NOT EXISTS (SELECT 1 FROM Core.ConfigurationSetting WHERE SettingKey = 'Platform.MfaRequired' AND ScopeCode = 'Platform')
    INSERT INTO Core.ConfigurationSetting (ScopeCode, SettingKey, SettingValue, DataTypeCode, DefaultValue, Description, ModuleCode)
    VALUES ('Platform', 'Platform.MfaRequired', 'false', 'Boolean', 'false', 'Require MFA for all platform users', 'IAM');

IF NOT EXISTS (SELECT 1 FROM Core.ConfigurationSetting WHERE SettingKey = 'Platform.DefaultCurrencyCode' AND ScopeCode = 'Platform')
    INSERT INTO Core.ConfigurationSetting (ScopeCode, SettingKey, SettingValue, DataTypeCode, DefaultValue, Description, ModuleCode)
    VALUES ('Platform', 'Platform.DefaultCurrencyCode', 'USD', 'String', 'USD', 'Default platform currency code', 'Core');

IF NOT EXISTS (SELECT 1 FROM Core.ConfigurationSetting WHERE SettingKey = 'Platform.DefaultLocale' AND ScopeCode = 'Platform')
    INSERT INTO Core.ConfigurationSetting (ScopeCode, SettingKey, SettingValue, DataTypeCode, DefaultValue, Description, ModuleCode)
    VALUES ('Platform', 'Platform.DefaultLocale', 'en-US', 'String', 'en-US', 'Default platform locale code', 'Core');

IF NOT EXISTS (SELECT 1 FROM Core.ConfigurationSetting WHERE SettingKey = 'Platform.DefaultTimeZone' AND ScopeCode = 'Platform')
    INSERT INTO Core.ConfigurationSetting (ScopeCode, SettingKey, SettingValue, DataTypeCode, DefaultValue, Description, ModuleCode)
    VALUES ('Platform', 'Platform.DefaultTimeZone', 'UTC', 'String', 'UTC', 'Default platform time zone identifier (IANA/Windows)', 'Core');

IF NOT EXISTS (SELECT 1 FROM Core.ConfigurationSetting WHERE SettingKey = 'Platform.InvoiceApprovalThreshold' AND ScopeCode = 'Platform')
    INSERT INTO Core.ConfigurationSetting (ScopeCode, SettingKey, SettingValue, DataTypeCode, DefaultValue, Description, ModuleCode)
    VALUES ('Platform', 'Platform.InvoiceApprovalThreshold', '10000', 'Decimal', '10000', 'Invoice amount that triggers approval workflow', 'Billing');

IF NOT EXISTS (SELECT 1 FROM Core.ConfigurationSetting WHERE SettingKey = 'Platform.DocumentRetentionDays' AND ScopeCode = 'Platform')
    INSERT INTO Core.ConfigurationSetting (ScopeCode, SettingKey, SettingValue, DataTypeCode, DefaultValue, Description, ModuleCode)
    VALUES ('Platform', 'Platform.DocumentRetentionDays', '2555', 'Integer', '2555', 'Default document retention period in days (7 years)', 'DMS');

IF NOT EXISTS (SELECT 1 FROM Core.ConfigurationSetting WHERE SettingKey = 'Platform.EnableAuditFieldChanges' AND ScopeCode = 'Platform')
    INSERT INTO Core.ConfigurationSetting (ScopeCode, SettingKey, SettingValue, DataTypeCode, DefaultValue, Description, ModuleCode)
    VALUES ('Platform', 'Platform.EnableAuditFieldChanges', 'true', 'Boolean', 'true', 'Enable granular field-level change tracking', 'Audit');

-- ============================================================
-- 2.1  PLATFORM CORE – Seed: System Report Definitions
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Core.ReportDefinition WHERE ReportCode = 'RPT_TENANT_SUMMARY')
    INSERT INTO Core.ReportDefinition (ReportCode, ReportName, Description, ModuleCode, ReportTypeCode, OutputFormats, IsSystemReport)
    VALUES ('RPT_TENANT_SUMMARY', 'Tenant Summary', 'Summary of all active tenants and their plan/usage metrics', 'Core', 'Tabular', 'PDF,Excel,CSV', 1);

IF NOT EXISTS (SELECT 1 FROM Core.ReportDefinition WHERE ReportCode = 'RPT_INVOICE_AGING')
    INSERT INTO Core.ReportDefinition (ReportCode, ReportName, Description, ModuleCode, ReportTypeCode, OutputFormats, IsSystemReport)
    VALUES ('RPT_INVOICE_AGING', 'Invoice Aging Report', 'Outstanding invoice aging buckets: current, 30, 60, 90+ days', 'Finance', 'Tabular', 'PDF,Excel,CSV', 1);

IF NOT EXISTS (SELECT 1 FROM Core.ReportDefinition WHERE ReportCode = 'RPT_COMMISSION_SUMMARY')
    INSERT INTO Core.ReportDefinition (ReportCode, ReportName, Description, ModuleCode, ReportTypeCode, OutputFormats, IsSystemReport)
    VALUES ('RPT_COMMISSION_SUMMARY', 'Commission Summary', 'Commission transactions and payouts by payee and period', 'Commission', 'Tabular', 'PDF,Excel,CSV', 1);

IF NOT EXISTS (SELECT 1 FROM Core.ReportDefinition WHERE ReportCode = 'RPT_ENGAGEMENT_STATUS')
    INSERT INTO Core.ReportDefinition (ReportCode, ReportName, Description, ModuleCode, ReportTypeCode, OutputFormats, IsSystemReport)
    VALUES ('RPT_ENGAGEMENT_STATUS', 'Engagement Status Report', 'Status of all active and recently closed engagements', 'OPS', 'Tabular', 'PDF,Excel,CSV', 1);

IF NOT EXISTS (SELECT 1 FROM Core.ReportDefinition WHERE ReportCode = 'RPT_AUDIT_TRAIL')
    INSERT INTO Core.ReportDefinition (ReportCode, ReportName, Description, ModuleCode, ReportTypeCode, OutputFormats, IsSystemReport)
    VALUES ('RPT_AUDIT_TRAIL', 'Audit Trail Report', 'Full audit trail with entity, user, action and timestamps', 'Audit', 'Tabular', 'PDF,Excel,CSV', 1);

-- Seed default branding for default tenant
IF NOT EXISTS (SELECT 1 FROM Core.TenantBranding WHERE TenantId = '00000000-0000-0000-0000-000000000001')
    INSERT INTO Core.TenantBranding (TenantId, WhiteLabelName, PrimaryColor, SecondaryColor, AccentColor, SupportEmail, FooterText)
    VALUES ('00000000-0000-0000-0000-000000000001', 'AMS Enterprise Platform', '#0d6efd', '#6c757d', '#198754', 'support@ams.local', N'© AMS Enterprise Platform. All rights reserved.');

GO

-- ============================================================
-- 2.2  IDENTITY AND ACCESS MANAGEMENT – Extended Engine Tables
-- ============================================================

-- ── User Groups (internal teams, partner orgs, client groups) 
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.UserGroup'))
CREATE TABLE IAM.UserGroup (
    UserGroupId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    GroupCode         NVARCHAR(100)    NOT NULL,
    GroupName         NVARCHAR(200)    NOT NULL,
    GroupTypeCode     NVARCHAR(50)     NOT NULL DEFAULT 'Internal',
    Description       NVARCHAR(500)    NULL,
    ManagerUserId     UNIQUEIDENTIFIER NULL,
    ParentGroupId     UNIQUEIDENTIFIER NULL,
    IsActive          BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── User Group Membership ────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.UserGroupMember'))
CREATE TABLE IAM.UserGroupMember (
    MemberId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    UserGroupId       UNIQUEIDENTIFIER NOT NULL,
    UserId            UNIQUEIDENTIFIER NOT NULL,
    JoinedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    RemovedDateUtc    DATETIME2        NULL,
    IsActive          BIT              NOT NULL DEFAULT 1,
    AddedByUserId     UNIQUEIDENTIFIER NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── External User Profile (client portal / broker / partner / producer) ──
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.ExternalUserProfile'))
CREATE TABLE IAM.ExternalUserProfile (
    ExternalProfileId    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId             UNIQUEIDENTIFIER NOT NULL,
    UserId               UNIQUEIDENTIFIER NOT NULL,
    ExternalUserTypeCode NVARCHAR(50)     NOT NULL DEFAULT 'Client',
    OrganizationName     NVARCHAR(300)    NULL,
    LicenseNumber        NVARCHAR(100)    NULL,
    LicenseState         NVARCHAR(50)     NULL,
    LicenseExpiryDate    DATE             NULL,
    NpnNumber            NVARCHAR(50)     NULL,
    TaxId                NVARCHAR(50)     NULL,
    PortalAccessEnabled  BIT              NOT NULL DEFAULT 0,
    PortalLastLoginDateUtc DATETIME2      NULL,
    SsoSubjectId         NVARCHAR(500)    NULL,
    SsoProvider          NVARCHAR(100)    NULL,
    CreatedDateUtc       DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc      DATETIME2        NULL,
    CreatedByUserId      UNIQUEIDENTIFIER NULL,
    IsDeleted            BIT              NOT NULL DEFAULT 0
);

-- ── SSO Configuration per tenant ────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.SsoConfiguration'))
CREATE TABLE IAM.SsoConfiguration (
    SsoConfigId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    ProviderTypeCode  NVARCHAR(50)     NOT NULL DEFAULT 'AzureAD',
    ProviderName      NVARCHAR(200)    NOT NULL,
    MetadataUrl       NVARCHAR(1000)   NULL,
    ClientId          NVARCHAR(500)    NULL,
    ClientSecretHash  NVARCHAR(500)    NULL,
    TenantDomain      NVARCHAR(300)    NULL,
    IsEnabled         BIT              NOT NULL DEFAULT 0,
    RequireSso        BIT              NOT NULL DEFAULT 0,
    AllowLocalLogin   BIT              NOT NULL DEFAULT 1,
    SsoAttributeMap   NVARCHAR(MAX)    NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── MFA Devices / Factors registered per user ───────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.MfaDevice'))
CREATE TABLE IAM.MfaDevice (
    MfaDeviceId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    UserId            UNIQUEIDENTIFIER NOT NULL,
    DeviceTypeCode    NVARCHAR(50)     NOT NULL DEFAULT 'TOTP',
    DeviceName        NVARCHAR(200)    NOT NULL,
    PhoneNumber       NVARCHAR(50)     NULL,
    EmailAddress      NVARCHAR(300)    NULL,
    SecretKeyHash     NVARCHAR(500)    NULL,
    IsVerified        BIT              NOT NULL DEFAULT 0,
    IsActive          BIT              NOT NULL DEFAULT 1,
    LastUsedDateUtc   DATETIME2        NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Field-level security policy ──────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.FieldSecurityPolicy'))
CREATE TABLE IAM.FieldSecurityPolicy (
    PolicyId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    RoleId            UNIQUEIDENTIFIER NOT NULL,
    EntityName        NVARCHAR(200)    NOT NULL,
    FieldName         NVARCHAR(200)    NOT NULL,
    CanRead           BIT              NOT NULL DEFAULT 1,
    CanWrite          BIT              NOT NULL DEFAULT 0,
    IsHidden          BIT              NOT NULL DEFAULT 0,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Record-level security policy ─────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.RecordSecurityPolicy'))
CREATE TABLE IAM.RecordSecurityPolicy (
    PolicyId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    RoleId            UNIQUEIDENTIFIER NOT NULL,
    EntityName        NVARCHAR(200)    NOT NULL,
    PolicyTypeCode    NVARCHAR(50)     NOT NULL DEFAULT 'Owner',
    FilterExpression  NVARCHAR(MAX)    NULL,
    IsActive          BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Privileged Access Management (PAM) ──────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.PrivilegedAccessRequest'))
CREATE TABLE IAM.PrivilegedAccessRequest (
    RequestId              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId               UNIQUEIDENTIFIER NOT NULL,
    RequestedByUserId      UNIQUEIDENTIFIER NOT NULL,
    TargetRoleId           UNIQUEIDENTIFIER NOT NULL,
    JustificationText      NVARCHAR(MAX)    NOT NULL,
    RequestedStartDateUtc  DATETIME2        NOT NULL,
    RequestedEndDateUtc    DATETIME2        NOT NULL,
    ApprovedByUserId       UNIQUEIDENTIFIER NULL,
    ApprovalDateUtc        DATETIME2        NULL,
    GrantedStartDateUtc    DATETIME2        NULL,
    GrantedEndDateUtc      DATETIME2        NULL,
    StatusCode             NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    RevokedByUserId        UNIQUEIDENTIFIER NULL,
    RevokedDateUtc         DATETIME2        NULL,
    RevokedReason          NVARCHAR(500)    NULL,
    CreatedDateUtc         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted              BIT              NOT NULL DEFAULT 0
);

-- ── Segregation of Duties rules ──────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.SegregationOfDutyRule'))
CREATE TABLE IAM.SegregationOfDutyRule (
    SodRuleId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NULL,
    RuleCode          NVARCHAR(100)    NOT NULL,
    RuleName          NVARCHAR(200)    NOT NULL,
    Description       NVARCHAR(500)    NULL,
    RoleACode         NVARCHAR(100)    NOT NULL,
    RoleBCode         NVARCHAR(100)    NOT NULL,
    SeverityCode      NVARCHAR(50)     NOT NULL DEFAULT 'Hard',
    IsActive          BIT              NOT NULL DEFAULT 1,
    IsSystemDefined   BIT              NOT NULL DEFAULT 0,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── User Access Review / Certification ──────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('IAM.UserAccessReview'))
CREATE TABLE IAM.UserAccessReview (
    ReviewId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    ReviewCycleCode   NVARCHAR(50)     NOT NULL DEFAULT 'Annual',
    ReviewerUserId    UNIQUEIDENTIFIER NOT NULL,
    SubjectUserId     UNIQUEIDENTIFIER NOT NULL,
    RoleId            UNIQUEIDENTIFIER NOT NULL,
    DecisionCode      NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    DecisionNotes     NVARCHAR(500)    NULL,
    ReviewedDateUtc   DATETIME2        NULL,
    DueByDateUtc      DATETIME2        NOT NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

GO

-- ============================================================
-- 2.2  IAM – Seed: SSO Configuration placeholder for default tenant
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM IAM.SsoConfiguration WHERE TenantId = '00000000-0000-0000-0000-000000000001')
    INSERT INTO IAM.SsoConfiguration (TenantId, ProviderTypeCode, ProviderName, IsEnabled, RequireSso, AllowLocalLogin)
    VALUES ('00000000-0000-0000-0000-000000000001', 'AzureAD', 'Azure Active Directory', 0, 0, 1);

-- ============================================================
-- 2.2  IAM – Seed: Segregation of Duty rules (system-defined, code-based)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM IAM.SegregationOfDutyRule WHERE RuleCode = 'SOD_INVOICE_CREATE_APPROVE')
    INSERT INTO IAM.SegregationOfDutyRule (TenantId, RuleCode, RuleName, Description, RoleACode, RoleBCode, SeverityCode, IsSystemDefined)
    VALUES (NULL, 'SOD_INVOICE_CREATE_APPROVE', 'Invoice Create vs Approve', 'A user cannot both create and approve invoices', 'INVOICE_CREATOR', 'INVOICE_APPROVER', 'Hard', 1);

IF NOT EXISTS (SELECT 1 FROM IAM.SegregationOfDutyRule WHERE RuleCode = 'SOD_PAYMENT_INITIATE_APPROVE')
    INSERT INTO IAM.SegregationOfDutyRule (TenantId, RuleCode, RuleName, Description, RoleACode, RoleBCode, SeverityCode, IsSystemDefined)
    VALUES (NULL, 'SOD_PAYMENT_INITIATE_APPROVE', 'Payment Initiate vs Approve', 'A user cannot both initiate and approve payments', 'PAYMENT_INITIATOR', 'PAYMENT_APPROVER', 'Hard', 1);

IF NOT EXISTS (SELECT 1 FROM IAM.SegregationOfDutyRule WHERE RuleCode = 'SOD_COMMISSION_CALC_APPROVE')
    INSERT INTO IAM.SegregationOfDutyRule (TenantId, RuleCode, RuleName, Description, RoleACode, RoleBCode, SeverityCode, IsSystemDefined)
    VALUES (NULL, 'SOD_COMMISSION_CALC_APPROVE', 'Commission Calculate vs Approve', 'A user cannot calculate and approve their own commission', 'COMMISSION_CALCULATOR', 'COMMISSION_APPROVER', 'Hard', 1);

IF NOT EXISTS (SELECT 1 FROM IAM.SegregationOfDutyRule WHERE RuleCode = 'SOD_GL_ENTRY_POST')
    INSERT INTO IAM.SegregationOfDutyRule (TenantId, RuleCode, RuleName, Description, RoleACode, RoleBCode, SeverityCode, IsSystemDefined)
    VALUES (NULL, 'SOD_GL_ENTRY_POST', 'GL Entry Create vs Post', 'A user cannot create and post the same journal entry', 'GL_ENTRY_CREATOR', 'GL_ENTRY_POSTER', 'Hard', 1);

IF NOT EXISTS (SELECT 1 FROM IAM.SegregationOfDutyRule WHERE RuleCode = 'SOD_USER_CREATE_ROLE_ASSIGN')
    INSERT INTO IAM.SegregationOfDutyRule (TenantId, RuleCode, RuleName, Description, RoleACode, RoleBCode, SeverityCode, IsSystemDefined)
    VALUES (NULL, 'SOD_USER_CREATE_ROLE_ASSIGN', 'User Create vs Role Assign', 'A user cannot create new users and also assign them roles', 'USER_ADMIN', 'ROLE_ADMIN', 'Soft', 1);

GO

-- ============================================================
-- 2.3  CRM AND SALES – Extended Engine Tables
-- ============================================================

-- ── Lead Source master catalogue ─────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('CRM.LeadSource'))
CREATE TABLE CRM.LeadSource (
    LeadSourceId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NULL,
    SourceCode        NVARCHAR(100)    NOT NULL,
    SourceName        NVARCHAR(200)    NOT NULL,
    Description       NVARCHAR(500)    NULL,
    IsActive          BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Lead Activity log (calls, emails, meetings, demos, notes) ─
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('CRM.LeadActivity'))
CREATE TABLE CRM.LeadActivity (
    ActivityId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    LeadId            UNIQUEIDENTIFIER NULL,
    OpportunityId     UNIQUEIDENTIFIER NULL,
    ActivityTypeCode  NVARCHAR(50)     NOT NULL DEFAULT 'Note',
    Subject           NVARCHAR(300)    NOT NULL,
    Notes             NVARCHAR(MAX)    NULL,
    ActivityDate      DATE             NOT NULL,
    DurationMinutes   INT              NULL,
    OutcomeCode       NVARCHAR(50)     NULL,
    IsCompleted       BIT              NOT NULL DEFAULT 0,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Quote Lines ──────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('CRM.QuoteLine'))
CREATE TABLE CRM.QuoteLine (
    QuoteLineId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    QuoteId           UNIQUEIDENTIFIER NOT NULL,
    LineOrder         INT              NOT NULL DEFAULT 1,
    ItemCode          NVARCHAR(100)    NULL,
    Description       NVARCHAR(500)    NOT NULL,
    Quantity          DECIMAL(10,4)    NOT NULL DEFAULT 1,
    UnitPrice         DECIMAL(18,2)    NOT NULL DEFAULT 0,
    DiscountPercent   DECIMAL(8,4)     NOT NULL DEFAULT 0,
    TaxPercent        DECIMAL(8,4)     NOT NULL DEFAULT 0,
    LineTotal         DECIMAL(18,2)    NOT NULL DEFAULT 0,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Pricing Rules (discount schedules, segment pricing, approval thresholds) ─
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('CRM.PricingRule'))
CREATE TABLE CRM.PricingRule (
    PricingRuleId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    RuleCode          NVARCHAR(100)    NOT NULL,
    RuleName          NVARCHAR(200)    NOT NULL,
    RuleTypeCode      NVARCHAR(50)     NOT NULL DEFAULT 'Discount',
    ServiceCode       NVARCHAR(100)    NULL,
    SegmentCode       NVARCHAR(50)     NULL,
    MinQuantity       DECIMAL(10,4)    NULL,
    MaxQuantity       DECIMAL(10,4)    NULL,
    DiscountPercent   DECIMAL(8,4)     NOT NULL DEFAULT 0,
    AdjustedUnitPrice DECIMAL(18,2)    NULL,
    EffectiveStartDate DATE            NOT NULL,
    EffectiveEndDate  DATE             NULL,
    RequiresApproval  BIT              NOT NULL DEFAULT 0,
    Priority          INT              NOT NULL DEFAULT 10,
    IsActive          BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Sales Forecast entries (pipeline/commit/best-case by period) ─
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('CRM.ForecastEntry'))
CREATE TABLE CRM.ForecastEntry (
    ForecastEntryId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    OpportunityId     UNIQUEIDENTIFIER NULL,
    OwnerUserId       UNIQUEIDENTIFIER NULL,
    ForecastPeriod    NVARCHAR(20)     NOT NULL,
    ForecastAmount    DECIMAL(18,2)    NOT NULL DEFAULT 0,
    PipelineAmount    DECIMAL(18,2)    NOT NULL DEFAULT 0,
    CategoryCode      NVARCHAR(50)     NOT NULL DEFAULT 'Pipeline',
    CloseDate         DATE             NULL,
    WinProbability    DECIMAL(5,2)     NOT NULL DEFAULT 0,
    Notes             NVARCHAR(500)    NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- 2.3  CRM AND SALES – Safe column migrations
-- ============================================================

-- CRM.Lead – lead origin / source tracking
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Lead') AND name = 'SourceCode')
    ALTER TABLE CRM.Lead ADD SourceCode NVARCHAR(100) NULL;

-- CRM.Lead – nurturing stage and qualification date
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Lead') AND name = 'QualifiedDate')
    ALTER TABLE CRM.Lead ADD QualifiedDate DATE NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Lead') AND name = 'NurturingStageCode')
    ALTER TABLE CRM.Lead ADD NurturingStageCode NVARCHAR(50) NULL DEFAULT 'New';

-- CRM.Opportunity – pipeline forecasting fields
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Opportunity') AND name = 'WinProbability')
    ALTER TABLE CRM.Opportunity ADD WinProbability DECIMAL(5,2) NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Opportunity') AND name = 'ForecastCategoryCode')
    ALTER TABLE CRM.Opportunity ADD ForecastCategoryCode NVARCHAR(50) NULL DEFAULT 'Pipeline';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Opportunity') AND name = 'LeadId')
    ALTER TABLE CRM.Opportunity ADD LeadId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Opportunity') AND name = 'CloseDate')
    ALTER TABLE CRM.Opportunity ADD CloseDate DATE NULL;

-- CRM.Quote – modifieddate tracking
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('CRM.Quote') AND name = 'ModifiedDateUtc')
    ALTER TABLE CRM.Quote ADD ModifiedDateUtc DATETIME2 NULL;

GO

-- ============================================================
-- 2.3  CRM AND SALES – Seed: Lead Sources
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM CRM.LeadSource WHERE SourceCode = 'WEB')
    INSERT INTO CRM.LeadSource (TenantId, SourceCode, SourceName, Description)
    VALUES (NULL, 'WEB', 'Website', 'Inbound lead from company website');

IF NOT EXISTS (SELECT 1 FROM CRM.LeadSource WHERE SourceCode = 'REFERRAL')
    INSERT INTO CRM.LeadSource (TenantId, SourceCode, SourceName, Description)
    VALUES (NULL, 'REFERRAL', 'Referral', 'Referred by existing client or partner');

IF NOT EXISTS (SELECT 1 FROM CRM.LeadSource WHERE SourceCode = 'MARKETING')
    INSERT INTO CRM.LeadSource (TenantId, SourceCode, SourceName, Description)
    VALUES (NULL, 'MARKETING', 'Marketing Campaign', 'Generated by marketing campaign');

IF NOT EXISTS (SELECT 1 FROM CRM.LeadSource WHERE SourceCode = 'SOCIAL')
    INSERT INTO CRM.LeadSource (TenantId, SourceCode, SourceName, Description)
    VALUES (NULL, 'SOCIAL', 'Social Media', 'Inbound from social media channel');

IF NOT EXISTS (SELECT 1 FROM CRM.LeadSource WHERE SourceCode = 'PARTNER')
    INSERT INTO CRM.LeadSource (TenantId, SourceCode, SourceName, Description)
    VALUES (NULL, 'PARTNER', 'Partner Channel', 'Sourced through partner or broker network');

IF NOT EXISTS (SELECT 1 FROM CRM.LeadSource WHERE SourceCode = 'EVENT')
    INSERT INTO CRM.LeadSource (TenantId, SourceCode, SourceName, Description)
    VALUES (NULL, 'EVENT', 'Event / Tradeshow', 'Met at industry event or tradeshow');

IF NOT EXISTS (SELECT 1 FROM CRM.LeadSource WHERE SourceCode = 'COLD')
    INSERT INTO CRM.LeadSource (TenantId, SourceCode, SourceName, Description)
    VALUES (NULL, 'COLD', 'Cold Outreach', 'Proactive cold call or email campaign');

IF NOT EXISTS (SELECT 1 FROM CRM.LeadSource WHERE SourceCode = 'OTHER')
    INSERT INTO CRM.LeadSource (TenantId, SourceCode, SourceName, Description)
    VALUES (NULL, 'OTHER', 'Other', 'Other or unspecified source');

-- ============================================================
-- 2.3  CRM AND SALES – Seed: Sample Pricing Rules
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM CRM.PricingRule WHERE RuleCode = 'VOL_DISCOUNT_10')
    INSERT INTO CRM.PricingRule (TenantId, RuleCode, RuleName, RuleTypeCode, MinQuantity, DiscountPercent, EffectiveStartDate, RequiresApproval, Priority)
    VALUES ('00000000-0000-0000-0000-000000000001', 'VOL_DISCOUNT_10', 'Volume Discount 10%', 'Discount', 10, 10.0000, CAST(GETDATE() AS DATE), 0, 10);

IF NOT EXISTS (SELECT 1 FROM CRM.PricingRule WHERE RuleCode = 'VOL_DISCOUNT_20')
    INSERT INTO CRM.PricingRule (TenantId, RuleCode, RuleName, RuleTypeCode, MinQuantity, DiscountPercent, EffectiveStartDate, RequiresApproval, Priority)
    VALUES ('00000000-0000-0000-0000-000000000001', 'VOL_DISCOUNT_20', 'Volume Discount 20%', 'Discount', 50, 20.0000, CAST(GETDATE() AS DATE), 1, 20);

IF NOT EXISTS (SELECT 1 FROM CRM.PricingRule WHERE RuleCode = 'ENT_SEGMENT_PRICE')
    INSERT INTO CRM.PricingRule (TenantId, RuleCode, RuleName, RuleTypeCode, SegmentCode, DiscountPercent, EffectiveStartDate, RequiresApproval, Priority)
    VALUES ('00000000-0000-0000-0000-000000000001', 'ENT_SEGMENT_PRICE', 'Enterprise Segment Discount', 'Discount', 'Enterprise', 15.0000, CAST(GETDATE() AS DATE), 1, 5);

GO

-- ============================================================
-- 2.4  CLIENT AND ACCOUNT MANAGEMENT – Extended Engine Tables
-- ============================================================

-- ── Account activity / interaction notes ─────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Client.AccountNote'))
CREATE TABLE Client.AccountNote (
    AccountNoteId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    NoteText          NVARCHAR(MAX)    NOT NULL,
    NoteTypeCode      NVARCHAR(50)     NOT NULL DEFAULT 'General',
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Account segment master catalogue ─────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Client.AccountSegment'))
CREATE TABLE Client.AccountSegment (
    SegmentId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NULL,
    SegmentCode       NVARCHAR(50)     NOT NULL,
    SegmentName       NVARCHAR(200)    NOT NULL,
    Description       NVARCHAR(500)    NULL,
    IsActive          BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Portal user invitation tracking ─────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Client.PortalInvite'))
CREATE TABLE Client.PortalInvite (
    PortalInviteId    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId          UNIQUEIDENTIFIER NOT NULL,
    ContactId         UNIQUEIDENTIFIER NOT NULL,
    AccountId         UNIQUEIDENTIFIER NOT NULL,
    InviteToken       NVARCHAR(500)    NOT NULL,
    InviteEmail       NVARCHAR(300)    NOT NULL,
    StatusCode        NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    SentDateUtc       DATETIME2        NULL,
    ExpiresDateUtc    DATETIME2        NOT NULL,
    AcceptedDateUtc   DATETIME2        NULL,
    CreatedByUserId   UNIQUEIDENTIFIER NULL,
    CreatedDateUtc    DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    IsDeleted         BIT              NOT NULL DEFAULT 0
);

-- ── Account ownership transfer log ──────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Client.AccountOwnerHistory'))
CREATE TABLE Client.AccountOwnerHistory (
    HistoryId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId            UNIQUEIDENTIFIER NOT NULL,
    AccountId           UNIQUEIDENTIFIER NOT NULL,
    PreviousOwnerUserId UNIQUEIDENTIFIER NULL,
    NewOwnerUserId      UNIQUEIDENTIFIER NULL,
    ChangedDateUtc      DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ChangedByUserId     UNIQUEIDENTIFIER NULL,
    Notes               NVARCHAR(500)    NULL,
    IsDeleted           BIT              NOT NULL DEFAULT 0
);

-- ============================================================
-- 2.4  CLIENT – Safe column migrations
-- ============================================================

-- Client.Account – hierarchy, lifecycle, classification
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'ParentAccountId')
    ALTER TABLE Client.Account ADD ParentAccountId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'LifecycleStageCode')
    ALTER TABLE Client.Account ADD LifecycleStageCode NVARCHAR(50) NULL DEFAULT 'Active';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'Industry')
    ALTER TABLE Client.Account ADD Industry NVARCHAR(100) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'Website')
    ALTER TABLE Client.Account ADD Website NVARCHAR(500) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Account') AND name = 'AnnualRevenue')
    ALTER TABLE Client.Account ADD AnnualRevenue DECIMAL(18,2) NULL;

-- Client.Contact – hierarchy, contact role flags, portal access
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Contact') AND name = 'ParentContactId')
    ALTER TABLE Client.Contact ADD ParentContactId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Contact') AND name = 'IsKeyContact')
    ALTER TABLE Client.Contact ADD IsKeyContact BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Contact') AND name = 'IsServiceContact')
    ALTER TABLE Client.Contact ADD IsServiceContact BIT NOT NULL DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Contact') AND name = 'PreferredContactMethod')
    ALTER TABLE Client.Contact ADD PreferredContactMethod NVARCHAR(50) NULL DEFAULT 'Email';

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('Client.Contact') AND name = 'LastPortalLoginDateUtc')
    ALTER TABLE Client.Contact ADD LastPortalLoginDateUtc DATETIME2 NULL;

GO

-- ============================================================
-- 2.4  CLIENT – Seed: Account Segments
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Client.AccountSegment WHERE SegmentCode = 'ENTERPRISE')
    INSERT INTO Client.AccountSegment (TenantId, SegmentCode, SegmentName, Description)
    VALUES (NULL, 'ENTERPRISE', 'Enterprise', 'Large enterprise accounts with 1000+ employees');

IF NOT EXISTS (SELECT 1 FROM Client.AccountSegment WHERE SegmentCode = 'MID_MARKET')
    INSERT INTO Client.AccountSegment (TenantId, SegmentCode, SegmentName, Description)
    VALUES (NULL, 'MID_MARKET', 'Mid-Market', 'Mid-market accounts with 100–999 employees');

IF NOT EXISTS (SELECT 1 FROM Client.AccountSegment WHERE SegmentCode = 'SMB')
    INSERT INTO Client.AccountSegment (TenantId, SegmentCode, SegmentName, Description)
    VALUES (NULL, 'SMB', 'Small & Medium Business', 'SMB accounts with fewer than 100 employees');

IF NOT EXISTS (SELECT 1 FROM Client.AccountSegment WHERE SegmentCode = 'STARTUP')
    INSERT INTO Client.AccountSegment (TenantId, SegmentCode, SegmentName, Description)
    VALUES (NULL, 'STARTUP', 'Startup', 'Early-stage startup companies');

IF NOT EXISTS (SELECT 1 FROM Client.AccountSegment WHERE SegmentCode = 'NON_PROFIT')
    INSERT INTO Client.AccountSegment (TenantId, SegmentCode, SegmentName, Description)
    VALUES (NULL, 'NON_PROFIT', 'Non-Profit', 'Non-profit and charitable organizations');

IF NOT EXISTS (SELECT 1 FROM Client.AccountSegment WHERE SegmentCode = 'GOVERNMENT')
    INSERT INTO Client.AccountSegment (TenantId, SegmentCode, SegmentName, Description)
    VALUES (NULL, 'GOVERNMENT', 'Government', 'Federal, state, and local government entities');

GO

-- ============================================================
-- 2.6  BILLING ENGINE – Extended Engine Tables
-- ============================================================

-- ── Rate Card header ─────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Billing.RateCard'))
CREATE TABLE Billing.RateCard (
    RateCardId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    RateCardCode       NVARCHAR(50)     NOT NULL,
    RateCardName       NVARCHAR(200)    NOT NULL,
    EffectiveStartDate DATE             NOT NULL,
    EffectiveEndDate   DATE             NULL,
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    Description        NVARCHAR(500)    NULL,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc    DATETIME2        NULL,
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Rate Card Lines (per role / service billable rates) ──────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Billing.RateCardLine'))
CREATE TABLE Billing.RateCardLine (
    RateCardLineId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    RateCardId         UNIQUEIDENTIFIER NOT NULL,
    RoleCode           NVARCHAR(100)    NULL,
    ServiceCode        NVARCHAR(100)    NULL,
    Description        NVARCHAR(300)    NULL,
    HourlyRate         DECIMAL(18,2)    NOT NULL DEFAULT 0,
    DailyRate          DECIMAL(18,2)    NULL,
    EffectiveStartDate DATE             NOT NULL,
    EffectiveEndDate   DATE             NULL,
    IsActive           BIT              NOT NULL DEFAULT 1,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Draft / Pre-bill batch for billing review ────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Billing.PrebillBatch'))
CREATE TABLE Billing.PrebillBatch (
    PrebillBatchId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    BatchNumber        NVARCHAR(50)     NOT NULL,
    AccountId          UNIQUEIDENTIFIER NULL,
    BillingPeriodStart DATE             NOT NULL,
    BillingPeriodEnd   DATE             NOT NULL,
    TotalAmount        DECIMAL(18,2)    NOT NULL DEFAULT 0,
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Draft',
    ReviewedByUserId   UNIQUEIDENTIFIER NULL,
    ReviewedDateUtc    DATETIME2        NULL,
    ApprovedByUserId   UNIQUEIDENTIFIER NULL,
    ApprovedDateUtc    DATETIME2        NULL,
    Notes              NVARCHAR(MAX)    NULL,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc    DATETIME2        NULL,
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Invoice Lines (itemised billing lines) ───────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.InvoiceLine'))
CREATE TABLE Finance.InvoiceLine (
    InvoiceLineId      UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    InvoiceId          UNIQUEIDENTIFIER NOT NULL,
    LineOrder          INT              NOT NULL DEFAULT 1,
    ItemCode           NVARCHAR(100)    NULL,
    Description        NVARCHAR(500)    NOT NULL,
    Quantity           DECIMAL(10,4)    NOT NULL DEFAULT 1,
    UnitPrice          DECIMAL(18,2)    NOT NULL DEFAULT 0,
    DiscountPercent    DECIMAL(8,4)     NOT NULL DEFAULT 0,
    TaxPercent         DECIMAL(8,4)     NOT NULL DEFAULT 0,
    LineTotal          DECIMAL(18,2)    NOT NULL DEFAULT 0,
    SourceEntityName   NVARCHAR(100)    NULL,
    SourceEntityId     UNIQUEIDENTIFIER NULL,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Recurring Billing Schedule ───────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Billing.RecurringBillingSchedule'))
CREATE TABLE Billing.RecurringBillingSchedule (
    ScheduleId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    AccountId          UNIQUEIDENTIFIER NOT NULL,
    AgreementId        UNIQUEIDENTIFIER NULL,
    ScheduleName       NVARCHAR(200)    NOT NULL,
    FrequencyCode      NVARCHAR(50)     NOT NULL DEFAULT 'Monthly',
    BillingAmount      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    StartDate          DATE             NOT NULL,
    EndDate            DATE             NULL,
    NextBillingDate    DATE             NOT NULL,
    LastBillingDate    DATE             NULL,
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    Description        NVARCHAR(500)    NULL,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc    DATETIME2        NULL,
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Milestone-triggered billing link ─────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Billing.MilestoneBillingLink'))
CREATE TABLE Billing.MilestoneBillingLink (
    LinkId             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    MilestoneId        UNIQUEIDENTIFIER NOT NULL,
    InvoiceId          UNIQUEIDENTIFIER NULL,
    BillingAmount      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    TriggeredDateUtc   DATETIME2        NULL,
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    Notes              NVARCHAR(500)    NULL,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Retainer account (balance tracking) ─────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Billing.RetainerAccount'))
CREATE TABLE Billing.RetainerAccount (
    RetainerAccountId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    AccountId          UNIQUEIDENTIFIER NOT NULL,
    AgreementId        UNIQUEIDENTIFIER NULL,
    RetainerName       NVARCHAR(200)    NOT NULL,
    TotalAmount        DECIMAL(18,2)    NOT NULL DEFAULT 0,
    UsedAmount         DECIMAL(18,2)    NOT NULL DEFAULT 0,
    RemainingAmount    DECIMAL(18,2)    NOT NULL DEFAULT 0,
    PeriodStart        DATE             NOT NULL,
    PeriodEnd          DATE             NULL,
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc    DATETIME2        NULL,
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Retainer drawdown (usage against retainer balance) ───────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Billing.RetainerDrawdown'))
CREATE TABLE Billing.RetainerDrawdown (
    DrawdownId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    RetainerAccountId  UNIQUEIDENTIFIER NOT NULL,
    InvoiceId          UNIQUEIDENTIFIER NULL,
    DrawdownDate       DATE             NOT NULL,
    Amount             DECIMAL(18,2)    NOT NULL DEFAULT 0,
    Description        NVARCHAR(500)    NULL,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Billing adjustments and write-offs ──────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.BillingAdjustment'))
CREATE TABLE Finance.BillingAdjustment (
    AdjustmentId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    InvoiceId          UNIQUEIDENTIFIER NOT NULL,
    AccountId          UNIQUEIDENTIFIER NOT NULL,
    AdjustmentTypeCode NVARCHAR(50)     NOT NULL DEFAULT 'Credit',
    AdjustmentDate     DATE             NOT NULL,
    Amount             DECIMAL(18,2)    NOT NULL DEFAULT 0,
    Reason             NVARCHAR(500)    NOT NULL,
    ApprovedByUserId   UNIQUEIDENTIFIER NULL,
    ApprovedDateUtc    DATETIME2        NULL,
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── AR Aging snapshot (periodic outstanding bucket totals) ───
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Billing.ArAgingSnapshot'))
CREATE TABLE Billing.ArAgingSnapshot (
    SnapshotId         UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    AccountId          UNIQUEIDENTIFIER NOT NULL,
    SnapshotDate       DATE             NOT NULL,
    CurrentAmount      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    Days30Amount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
    Days60Amount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
    Days90Amount       DECIMAL(18,2)    NOT NULL DEFAULT 0,
    Days90PlusAmount   DECIMAL(18,2)    NOT NULL DEFAULT 0,
    TotalOutstanding   DECIMAL(18,2)    NOT NULL DEFAULT 0,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Delinquency tracking and escalation ─────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Billing.DelinquencyFlag'))
CREATE TABLE Billing.DelinquencyFlag (
    DelinquencyFlagId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    AccountId          UNIQUEIDENTIFIER NOT NULL,
    InvoiceId          UNIQUEIDENTIFIER NULL,
    FlagDate           DATE             NOT NULL,
    DaysOverdue        INT              NOT NULL DEFAULT 0,
    OverdueAmount      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    SeverityCode       NVARCHAR(50)     NOT NULL DEFAULT 'Low',
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Open',
    ResolvedDate       DATE             NULL,
    Notes              NVARCHAR(500)    NULL,
    AssignedToUserId   UNIQUEIDENTIFIER NULL,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

GO

-- ============================================================
-- 2.6  BILLING ENGINE – Seed: Default Rate Card
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Billing.RateCard WHERE RateCardCode = 'STANDARD' AND TenantId = '00000000-0000-0000-0000-000000000001')
BEGIN
    DECLARE @RcId UNIQUEIDENTIFIER = NEWID();
    INSERT INTO Billing.RateCard (RateCardId, TenantId, RateCardCode, RateCardName, EffectiveStartDate, StatusCode, Description)
    VALUES (@RcId, '00000000-0000-0000-0000-000000000001', 'STANDARD', 'Standard Rate Card', CAST(GETDATE() AS DATE), 'Active', 'Default standard billable rates');

    INSERT INTO Billing.RateCardLine (TenantId, RateCardId, RoleCode, Description, HourlyRate, DailyRate, EffectiveStartDate)
    VALUES ('00000000-0000-0000-0000-000000000001', @RcId, 'ANALYST',       'Analyst',              100.00,  800.00, CAST(GETDATE() AS DATE));
    INSERT INTO Billing.RateCardLine (TenantId, RateCardId, RoleCode, Description, HourlyRate, DailyRate, EffectiveStartDate)
    VALUES ('00000000-0000-0000-0000-000000000001', @RcId, 'CONSULTANT',    'Consultant',           150.00, 1200.00, CAST(GETDATE() AS DATE));
    INSERT INTO Billing.RateCardLine (TenantId, RateCardId, RoleCode, Description, HourlyRate, DailyRate, EffectiveStartDate)
    VALUES ('00000000-0000-0000-0000-000000000001', @RcId, 'SR_CONSULTANT', 'Senior Consultant',    200.00, 1600.00, CAST(GETDATE() AS DATE));
    INSERT INTO Billing.RateCardLine (TenantId, RateCardId, RoleCode, Description, HourlyRate, DailyRate, EffectiveStartDate)
    VALUES ('00000000-0000-0000-0000-000000000001', @RcId, 'PRINCIPAL',     'Principal Consultant', 275.00, 2200.00, CAST(GETDATE() AS DATE));
END;

GO

-- ============================================================
-- 2.7  ACCOUNTING AND FINANCE – Extended Engine Tables
-- ============================================================

-- ── AP: Vendor catalogue ─────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.Vendor'))
CREATE TABLE Finance.Vendor (
    VendorId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    VendorCode         NVARCHAR(50)     NOT NULL,
    VendorName         NVARCHAR(300)    NOT NULL,
    ContactName        NVARCHAR(200)    NULL,
    Email              NVARCHAR(300)    NULL,
    Phone              NVARCHAR(50)     NULL,
    PaymentTermsCode   NVARCHAR(50)     NOT NULL DEFAULT 'Net30',
    CurrencyCode       NVARCHAR(10)     NOT NULL DEFAULT 'USD',
    TaxId              NVARCHAR(50)     NULL,
    VendorTypeCode     NVARCHAR(50)     NOT NULL DEFAULT 'Supplier',
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc    DATETIME2        NULL,
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── AP: Accounts Payable invoice header ──────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.ApInvoice'))
CREATE TABLE Finance.ApInvoice (
    ApInvoiceId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    VendorId           UNIQUEIDENTIFIER NOT NULL,
    InvoiceNumber      NVARCHAR(100)    NOT NULL,
    InvoiceDate        DATE             NOT NULL,
    DueDate            DATE             NOT NULL,
    TotalAmount        DECIMAL(18,2)    NOT NULL DEFAULT 0,
    PaidAmount         DECIMAL(18,2)    NOT NULL DEFAULT 0,
    BalanceAmount      DECIMAL(18,2)    NOT NULL DEFAULT 0,
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Open',
    GLAccountId        UNIQUEIDENTIFIER NULL,
    AgreementId        UNIQUEIDENTIFIER NULL,
    Notes              NVARCHAR(500)    NULL,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc    DATETIME2        NULL,
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── AP: Invoice line items ────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.ApInvoiceLine'))
CREATE TABLE Finance.ApInvoiceLine (
    ApInvoiceLineId    UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    ApInvoiceId        UNIQUEIDENTIFIER NOT NULL,
    LineOrder          INT              NOT NULL DEFAULT 1,
    Description        NVARCHAR(500)    NOT NULL,
    Quantity           DECIMAL(10,4)    NOT NULL DEFAULT 1,
    UnitPrice          DECIMAL(18,2)    NOT NULL DEFAULT 0,
    LineTotal          DECIMAL(18,2)    NOT NULL DEFAULT 0,
    GLAccountId        UNIQUEIDENTIFIER NULL,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── AP: Payment disbursement ──────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.ApPayment'))
CREATE TABLE Finance.ApPayment (
    ApPaymentId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    VendorId           UNIQUEIDENTIFIER NOT NULL,
    ApInvoiceId        UNIQUEIDENTIFIER NULL,
    PaymentDate        DATE             NOT NULL,
    Amount             DECIMAL(18,2)    NOT NULL DEFAULT 0,
    PaymentMethodCode  NVARCHAR(50)     NOT NULL DEFAULT 'ACH',
    ReferenceNumber    NVARCHAR(100)    NULL,
    Notes              NVARCHAR(500)    NULL,
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Issued',
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc    DATETIME2        NULL,
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── GL: Accounting periods / fiscal calendar ──────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.AccountingPeriod'))
CREATE TABLE Finance.AccountingPeriod (
    AccountingPeriodId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    PeriodName         NVARCHAR(100)    NOT NULL,
    FiscalYear         INT              NOT NULL,
    PeriodNumber       INT              NOT NULL,
    StartDate          DATE             NOT NULL,
    EndDate            DATE             NOT NULL,
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Open',
    ClosedDateUtc      DATETIME2        NULL,
    ClosedByUserId     UNIQUEIDENTIFIER NULL,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── GL: Period close checklist entries ───────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.PeriodCloseEntry'))
CREATE TABLE Finance.PeriodCloseEntry (
    PeriodCloseEntryId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    AccountingPeriodId UNIQUEIDENTIFIER NOT NULL,
    TaskDescription    NVARCHAR(500)    NOT NULL,
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    CompletedByUserId  UNIQUEIDENTIFIER NULL,
    CompletedDateUtc   DATETIME2        NULL,
    Notes              NVARCHAR(500)    NULL,
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Finance: Deferred revenue schedule header ─────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.DeferredRevenueSchedule'))
CREATE TABLE Finance.DeferredRevenueSchedule (
    DeferredRevenueScheduleId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId                  UNIQUEIDENTIFIER NOT NULL,
    AccountId                 UNIQUEIDENTIFIER NOT NULL,
    InvoiceId                 UNIQUEIDENTIFIER NULL,
    AgreementId               UNIQUEIDENTIFIER NULL,
    TotalAmount               DECIMAL(18,2)    NOT NULL DEFAULT 0,
    RecognizedAmount          DECIMAL(18,2)    NOT NULL DEFAULT 0,
    RemainingAmount           DECIMAL(18,2)    NOT NULL DEFAULT 0,
    StartDate                 DATE             NOT NULL,
    EndDate                   DATE             NULL,
    FrequencyCode             NVARCHAR(50)     NOT NULL DEFAULT 'Monthly',
    StatusCode                NVARCHAR(50)     NOT NULL DEFAULT 'Active',
    GLAccountId               UNIQUEIDENTIFIER NULL,
    DeferredGLAccountId       UNIQUEIDENTIFIER NULL,
    CreatedDateUtc            DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc           DATETIME2        NULL,
    CreatedByUserId           UNIQUEIDENTIFIER NULL,
    IsDeleted                 BIT              NOT NULL DEFAULT 0
);

-- ── Finance: Deferred revenue recognition line ────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.DeferredRevenueRecognition'))
CREATE TABLE Finance.DeferredRevenueRecognition (
    RecognitionId             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId                  UNIQUEIDENTIFIER NOT NULL,
    DeferredRevenueScheduleId UNIQUEIDENTIFIER NOT NULL,
    RecognitionDate           DATE             NOT NULL,
    Amount                    DECIMAL(18,2)    NOT NULL DEFAULT 0,
    JournalEntryId            UNIQUEIDENTIFIER NULL,
    StatusCode                NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    CreatedDateUtc            DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId           UNIQUEIDENTIFIER NULL,
    IsDeleted                 BIT              NOT NULL DEFAULT 0
);

-- ── Finance: Bad debt and write-off processing ────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.BadDebtEntry'))
CREATE TABLE Finance.BadDebtEntry (
    BadDebtEntryId     UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    AccountId          UNIQUEIDENTIFIER NOT NULL,
    InvoiceId          UNIQUEIDENTIFIER NULL,
    WriteOffDate       DATE             NOT NULL,
    Amount             DECIMAL(18,2)    NOT NULL DEFAULT 0,
    Reason             NVARCHAR(500)    NOT NULL,
    GLAccountId        UNIQUEIDENTIFIER NULL,
    ApprovedByUserId   UNIQUEIDENTIFIER NULL,
    ApprovedDateUtc    DATETIME2        NULL,
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Pending',
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── AR: Cash receipt entry ────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.CashReceiptEntry'))
CREATE TABLE Finance.CashReceiptEntry (
    CashReceiptEntryId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId           UNIQUEIDENTIFIER NOT NULL,
    AccountId          UNIQUEIDENTIFIER NOT NULL,
    InvoiceId          UNIQUEIDENTIFIER NULL,
    ReceiptDate        DATE             NOT NULL,
    Amount             DECIMAL(18,2)    NOT NULL DEFAULT 0,
    PaymentMethodCode  NVARCHAR(50)     NOT NULL DEFAULT 'ACH',
    ReferenceNumber    NVARCHAR(100)    NULL,
    GLAccountId        UNIQUEIDENTIFIER NULL,
    BankAccountCode    NVARCHAR(50)     NULL,
    Notes              NVARCHAR(500)    NULL,
    StatusCode         NVARCHAR(50)     NOT NULL DEFAULT 'Posted',
    CreatedDateUtc     DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc    DATETIME2        NULL,
    CreatedByUserId    UNIQUEIDENTIFIER NULL,
    IsDeleted          BIT              NOT NULL DEFAULT 0
);

-- ── Finance: Trial balance snapshot ──────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Finance.TrialBalanceSnapshot'))
CREATE TABLE Finance.TrialBalanceSnapshot (
    TrialBalanceSnapshotId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId               UNIQUEIDENTIFIER NOT NULL,
    SnapshotDate           DATE             NOT NULL,
    AccountingPeriodId     UNIQUEIDENTIFIER NULL,
    GLAccountId            UNIQUEIDENTIFIER NOT NULL,
    AccountCode            NVARCHAR(50)     NOT NULL,
    AccountName            NVARCHAR(200)    NOT NULL,
    DebitBalance           DECIMAL(18,2)    NOT NULL DEFAULT 0,
    CreditBalance          DECIMAL(18,2)    NOT NULL DEFAULT 0,
    NetBalance             DECIMAL(18,2)    NOT NULL DEFAULT 0,
    CreatedDateUtc         DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    CreatedByUserId        UNIQUEIDENTIFIER NULL,
    IsDeleted              BIT              NOT NULL DEFAULT 0
);

GO

-- ============================================================
-- 2.7  ACCOUNTING AND FINANCE – Seed: Accounting Periods (current + next fiscal year)
-- ============================================================
DECLARE @FY INT = YEAR(GETDATE());

IF NOT EXISTS (SELECT 1 FROM Finance.AccountingPeriod WHERE TenantId = '00000000-0000-0000-0000-000000000001' AND FiscalYear = @FY AND PeriodNumber = 1)
BEGIN
    INSERT INTO Finance.AccountingPeriod (TenantId, PeriodName, FiscalYear, PeriodNumber, StartDate, EndDate, StatusCode, CreatedByUserId)
    VALUES
    ('00000000-0000-0000-0000-000000000001', CAST(@FY AS NVARCHAR) + '-01', @FY,  1, CAST(CAST(@FY AS NVARCHAR) + '-01-01' AS DATE), CAST(CAST(@FY AS NVARCHAR) + '-01-31' AS DATE), 'Closed', NULL),
    ('00000000-0000-0000-0000-000000000001', CAST(@FY AS NVARCHAR) + '-02', @FY,  2, CAST(CAST(@FY AS NVARCHAR) + '-02-01' AS DATE), CAST(CAST(@FY AS NVARCHAR) + '-02-28' AS DATE), 'Closed', NULL),
    ('00000000-0000-0000-0000-000000000001', CAST(@FY AS NVARCHAR) + '-03', @FY,  3, CAST(CAST(@FY AS NVARCHAR) + '-03-01' AS DATE), CAST(CAST(@FY AS NVARCHAR) + '-03-31' AS DATE), 'Closed', NULL),
    ('00000000-0000-0000-0000-000000000001', CAST(@FY AS NVARCHAR) + '-04', @FY,  4, CAST(CAST(@FY AS NVARCHAR) + '-04-01' AS DATE), CAST(CAST(@FY AS NVARCHAR) + '-04-30' AS DATE), 'Closed', NULL),
    ('00000000-0000-0000-0000-000000000001', CAST(@FY AS NVARCHAR) + '-05', @FY,  5, CAST(CAST(@FY AS NVARCHAR) + '-05-01' AS DATE), CAST(CAST(@FY AS NVARCHAR) + '-05-31' AS DATE), 'Closed', NULL),
    ('00000000-0000-0000-0000-000000000001', CAST(@FY AS NVARCHAR) + '-06', @FY,  6, CAST(CAST(@FY AS NVARCHAR) + '-06-01' AS DATE), CAST(CAST(@FY AS NVARCHAR) + '-06-30' AS DATE), 'Closed', NULL),
    ('00000000-0000-0000-0000-000000000001', CAST(@FY AS NVARCHAR) + '-07', @FY,  7, CAST(CAST(@FY AS NVARCHAR) + '-07-01' AS DATE), CAST(CAST(@FY AS NVARCHAR) + '-07-31' AS DATE), 'Open',   NULL),
    ('00000000-0000-0000-0000-000000000001', CAST(@FY AS NVARCHAR) + '-08', @FY,  8, CAST(CAST(@FY AS NVARCHAR) + '-08-01' AS DATE), CAST(CAST(@FY AS NVARCHAR) + '-08-31' AS DATE), 'Open',   NULL),
    ('00000000-0000-0000-0000-000000000001', CAST(@FY AS NVARCHAR) + '-09', @FY,  9, CAST(CAST(@FY AS NVARCHAR) + '-09-01' AS DATE), CAST(CAST(@FY AS NVARCHAR) + '-09-30' AS DATE), 'Open',   NULL),
    ('00000000-0000-0000-0000-000000000001', CAST(@FY AS NVARCHAR) + '-10', @FY, 10, CAST(CAST(@FY AS NVARCHAR) + '-10-01' AS DATE), CAST(CAST(@FY AS NVARCHAR) + '-10-31' AS DATE), 'Open',   NULL),
    ('00000000-0000-0000-0000-000000000001', CAST(@FY AS NVARCHAR) + '-11', @FY, 11, CAST(CAST(@FY AS NVARCHAR) + '-11-01' AS DATE), CAST(CAST(@FY AS NVARCHAR) + '-11-30' AS DATE), 'Open',   NULL),
    ('00000000-0000-0000-0000-000000000001', CAST(@FY AS NVARCHAR) + '-12', @FY, 12, CAST(CAST(@FY AS NVARCHAR) + '-12-01' AS DATE), CAST(CAST(@FY AS NVARCHAR) + '-12-31' AS DATE), 'Open',   NULL);
END;

GO

-- ============================================================
-- 2.7  ACCOUNTING AND FINANCE – Seed: Sample Vendor
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Finance.Vendor WHERE VendorCode = 'VENDOR-001' AND TenantId = '00000000-0000-0000-0000-000000000001')
    INSERT INTO Finance.Vendor (TenantId, VendorCode, VendorName, PaymentTermsCode, CurrencyCode, VendorTypeCode, StatusCode)
    VALUES ('00000000-0000-0000-0000-000000000001', 'VENDOR-001', 'General Supplies Co.', 'Net30', 'USD', 'Supplier', 'Active');

GO
