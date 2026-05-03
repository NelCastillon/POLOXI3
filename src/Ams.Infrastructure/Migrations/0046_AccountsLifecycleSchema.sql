-- ============================================================
-- MIGRATION 0046: ACCOUNTS LIFECYCLE SCHEMA
-- Creates comprehensive account management and relationships
-- ============================================================

-- ============================================================
-- ENSURE CLIENT SCHEMA EXISTS
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'Client')
BEGIN
    EXEC('CREATE SCHEMA Client');
END
GO

-- ============================================================
-- ACCOUNT RELATIONSHIPS TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'AccountRelationship')
BEGIN
    CREATE TABLE Client.AccountRelationship (
        RelationshipId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL,

        -- Source and Related Accounts
        SourceAccountId         UNIQUEIDENTIFIER NOT NULL,
        RelatedAccountId        UNIQUEIDENTIFIER NOT NULL,

        -- Relationship Type
        RelationshipType        NVARCHAR(100)    NOT NULL,  -- 'Parent', 'Subsidiary', 'Partner', 'Affiliate', etc.
        Description             NVARCHAR(500)    NULL,

        -- Status
        IsActive                BIT              NOT NULL DEFAULT 1,

        -- Audit
        CreatedDateUtc          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId         UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc         DATETIME2        NULL,
        ModifiedByUserId        UNIQUEIDENTIFIER NULL,
        IsDeleted               BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_AccountRelationship_TenantId 
        ON Client.AccountRelationship(TenantId, IsDeleted);

    CREATE NONCLUSTERED INDEX IX_AccountRelationship_SourceAccount 
        ON Client.AccountRelationship(SourceAccountId, IsDeleted);

    CREATE NONCLUSTERED INDEX IX_AccountRelationship_RelatedAccount 
        ON Client.AccountRelationship(RelatedAccountId, IsDeleted);
END
GO

-- ============================================================
-- CONTACT ROLE TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'ContactRole')
BEGIN
    CREATE TABLE Client.ContactRole (
        ContactRoleId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL,

        -- Role Information
        RoleName                NVARCHAR(100)    NOT NULL,
        Description             NVARCHAR(500)    NULL,

        -- Status
        IsActive                BIT              NOT NULL DEFAULT 1,
        IsDefault               BIT              NOT NULL DEFAULT 0,

        -- Audit
        CreatedDateUtc          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId         UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc         DATETIME2        NULL,
        ModifiedByUserId        UNIQUEIDENTIFIER NULL,
        IsDeleted               BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_ContactRole_TenantId 
        ON Client.ContactRole(TenantId, IsDeleted);
END
GO

-- ============================================================
-- ACCOUNT HIERARCHY (Child Relationships) TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'AccountHierarchy')
BEGIN
    CREATE TABLE Client.AccountHierarchy (
        HierarchyId             UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL,

        -- Parent-Child Relationship
        ParentAccountId         UNIQUEIDENTIFIER NOT NULL,
        ChildAccountId          UNIQUEIDENTIFIER NOT NULL,

        -- Hierarchy Level
        HierarchyLevel          INT              NOT NULL DEFAULT 1,

        -- Status
        IsActive                BIT              NOT NULL DEFAULT 1,

        -- Audit
        CreatedDateUtc          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId         UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc         DATETIME2        NULL,
        ModifiedByUserId        UNIQUEIDENTIFIER NULL,
        IsDeleted               BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_AccountHierarchy_TenantId 
        ON Client.AccountHierarchy(TenantId, IsDeleted);

    CREATE NONCLUSTERED INDEX IX_AccountHierarchy_ParentAccount 
        ON Client.AccountHierarchy(ParentAccountId, IsDeleted);

    CREATE NONCLUSTERED INDEX IX_AccountHierarchy_ChildAccount 
        ON Client.AccountHierarchy(ChildAccountId, IsDeleted);

    CREATE UNIQUE NONCLUSTERED INDEX IX_AccountHierarchy_Unique 
        ON Client.AccountHierarchy(ParentAccountId, ChildAccountId, TenantId) 
        WHERE IsDeleted = 0;
END
GO

-- ============================================================
-- ACCOUNT TIMELINE/ACTIVITY TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'AccountActivity')
BEGIN
    CREATE TABLE Client.AccountActivity (
        ActivityId              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL,

        -- Account Reference
        AccountId               UNIQUEIDENTIFIER NOT NULL,

        -- Activity Information
        ActivityType            NVARCHAR(100)    NOT NULL,  -- 'AccountChange', 'ContactAdded', 'Opportunity', 'QuoteCreated', etc.
        Title                   NVARCHAR(255)    NOT NULL,
        Description             NVARCHAR(MAX)    NULL,

        -- Related Entity
        RelatedEntityType       NVARCHAR(100)    NULL,      -- 'Contact', 'Opportunity', 'Quote', 'Policy', etc.
        RelatedEntityId         UNIQUEIDENTIFIER NULL,

        -- Metadata
        Metadata                NVARCHAR(MAX)    NULL,      -- JSON metadata if needed

        -- Audit
        CreatedDateUtc          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId         UNIQUEIDENTIFIER NULL,
        IsDeleted               BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_AccountActivity_TenantId 
        ON Client.AccountActivity(TenantId, IsDeleted);

    CREATE NONCLUSTERED INDEX IX_AccountActivity_AccountId 
        ON Client.AccountActivity(AccountId, IsDeleted, CreatedDateUtc DESC);

    CREATE NONCLUSTERED INDEX IX_AccountActivity_CreatedDate 
        ON Client.AccountActivity(CreatedDateUtc DESC);
END
GO

-- ============================================================
-- ACCOUNT SERVICE PROVIDER TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'AccountServiceProvider')
BEGIN
    CREATE TABLE Client.AccountServiceProvider (
        ProviderId              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL,

        -- Account Reference
        AccountId               UNIQUEIDENTIFIER NOT NULL,

        -- Provider Information
        ProviderName            NVARCHAR(255)    NOT NULL,
        ServiceType             NVARCHAR(100)    NOT NULL,  -- 'Insurance', 'Legal', 'Accounting', etc.

        -- Contact Information
        ContactName             NVARCHAR(255)    NULL,
        ContactEmail            NVARCHAR(255)    NULL,
        ContactPhone            NVARCHAR(20)     NULL,

        -- Status
        IsActive                BIT              NOT NULL DEFAULT 1,

        -- Audit
        CreatedDateUtc          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId         UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc         DATETIME2        NULL,
        ModifiedByUserId        UNIQUEIDENTIFIER NULL,
        IsDeleted               BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_AccountServiceProvider_TenantId 
        ON Client.AccountServiceProvider(TenantId, IsDeleted);

    CREATE NONCLUSTERED INDEX IX_AccountServiceProvider_AccountId 
        ON Client.AccountServiceProvider(AccountId, IsDeleted);
END
GO

-- ============================================================
-- EXTENDED ACCOUNT ATTRIBUTES TABLE
-- ============================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'AccountExtended')
BEGIN
    CREATE TABLE Client.AccountExtended (
        AccountExtendedId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId                UNIQUEIDENTIFIER NOT NULL,

        -- Account Reference
        AccountId               UNIQUEIDENTIFIER NOT NULL,

        -- Extended Information
        ParentAccountId         UNIQUEIDENTIFIER NULL,
        OwnerUserId             UNIQUEIDENTIFIER NULL,
        OwnerUserName           NVARCHAR(255)    NULL,

        -- Account Metadata
        LifecycleStage          NVARCHAR(100)    NULL,      -- 'Prospect', 'Customer', 'Inactive', 'Won', 'Lost'
        Segment                 NVARCHAR(100)    NULL,      -- 'Enterprise', 'MidMarket', 'SMB'
        Industry                NVARCHAR(100)    NULL,
        NumberOfEmployees       INT              NULL,
        AnnualRevenue           DECIMAL(18, 2)   NULL,
        Website                 NVARCHAR(500)    NULL,
        TaxId                   NVARCHAR(50)     NULL,
        NaicsCode               NVARCHAR(50)     NULL,

        -- Risk Assessment
        RenewalRisk             NVARCHAR(50)     NULL,      -- 'Low', 'Medium', 'High', 'Critical'
        ChurnRisk               DECIMAL(5, 2)    NULL,      -- Probability percentage
        HealthScore             INT              NULL,      -- 0-100

        -- Statistics
        TotalPremium            DECIMAL(18, 2)   NOT NULL DEFAULT 0,
        OpenClaims              INT              NOT NULL DEFAULT 0,
        ActivePolicies          INT              NOT NULL DEFAULT 0,
        OpenOpportunities       INT              NOT NULL DEFAULT 0,

        -- Dates
        FirstPolicyDate         DATETIME2        NULL,
        LastActivityDate        DATETIME2        NULL,

        -- Audit
        CreatedDateUtc          DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId         UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc         DATETIME2        NULL,
        ModifiedByUserId        UNIQUEIDENTIFIER NULL,
        IsDeleted               BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_AccountExtended_TenantId 
        ON Client.AccountExtended(TenantId, IsDeleted);

    CREATE NONCLUSTERED INDEX IX_AccountExtended_AccountId 
        ON Client.AccountExtended(AccountId, IsDeleted);

    CREATE NONCLUSTERED INDEX IX_AccountExtended_OwnerUserId 
        ON Client.AccountExtended(OwnerUserId, IsDeleted) WHERE IsDeleted = 0;

    CREATE NONCLUSTERED INDEX IX_AccountExtended_Segment 
        ON Client.AccountExtended(Segment, IsDeleted) WHERE IsDeleted = 0;

    CREATE NONCLUSTERED INDEX IX_AccountExtended_RenewalRisk 
        ON Client.AccountExtended(RenewalRisk, IsDeleted) WHERE IsDeleted = 0;
END
GO

-- ============================================================
-- SEED DATA
-- ============================================================

-- Insert Default Contact Roles
IF NOT EXISTS (SELECT 1 FROM Client.ContactRole WHERE RoleName = 'Decision Maker')
BEGIN
    INSERT INTO Client.ContactRole (TenantId, RoleName, Description, IsActive, IsDefault, CreatedDateUtc)
    SELECT TOP 1 TenantId, 'Decision Maker', 'Senior executive responsible for policy decisions', 1, 1, GETUTCDATE()
    FROM [Tenants].Tenant;
END

IF NOT EXISTS (SELECT 1 FROM Client.ContactRole WHERE RoleName = 'Finance Contact')
BEGIN
    INSERT INTO Client.ContactRole (TenantId, RoleName, Description, IsActive, IsDefault, CreatedDateUtc)
    SELECT TOP 1 TenantId, 'Finance Contact', 'Responsible for billing and payment matters', 1, 0, GETUTCDATE()
    FROM [Tenants].Tenant;
END

IF NOT EXISTS (SELECT 1 FROM Client.ContactRole WHERE RoleName = 'Operations Contact')
BEGIN
    INSERT INTO Client.ContactRole (TenantId, RoleName, Description, IsActive, IsDefault, CreatedDateUtc)
    SELECT TOP 1 TenantId, 'Operations Contact', 'Point of contact for operational matters', 1, 0, GETUTCDATE()
    FROM [Tenants].Tenant;
END

IF NOT EXISTS (SELECT 1 FROM Client.ContactRole WHERE RoleName = 'HR Contact')
BEGIN
    INSERT INTO Client.ContactRole (TenantId, RoleName, Description, IsActive, IsDefault, CreatedDateUtc)
    SELECT TOP 1 TenantId, 'HR Contact', 'Human Resources or benefits administrator', 1, 0, GETUTCDATE()
    FROM [Tenants].Tenant;
END

-- Add indexes for foreign keys if Account table exists
IF EXISTS (SELECT * FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'Account')
BEGIN
    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AccountRelationship_SourceAccount')
    BEGIN
        ALTER TABLE Client.AccountRelationship
        ADD CONSTRAINT FK_AccountRelationship_SourceAccount
        FOREIGN KEY (SourceAccountId) REFERENCES Client.Account(AccountId);
    END

    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AccountRelationship_RelatedAccount')
    BEGIN
        ALTER TABLE Client.AccountRelationship
        ADD CONSTRAINT FK_AccountRelationship_RelatedAccount
        FOREIGN KEY (RelatedAccountId) REFERENCES Client.Account(AccountId);
    END

    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AccountHierarchy_ParentAccount')
    BEGIN
        ALTER TABLE Client.AccountHierarchy
        ADD CONSTRAINT FK_AccountHierarchy_ParentAccount
        FOREIGN KEY (ParentAccountId) REFERENCES Client.Account(AccountId);
    END

    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AccountHierarchy_ChildAccount')
    BEGIN
        ALTER TABLE Client.AccountHierarchy
        ADD CONSTRAINT FK_AccountHierarchy_ChildAccount
        FOREIGN KEY (ChildAccountId) REFERENCES Client.Account(AccountId);
    END

    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AccountActivity_Account')
    BEGIN
        ALTER TABLE Client.AccountActivity
        ADD CONSTRAINT FK_AccountActivity_Account
        FOREIGN KEY (AccountId) REFERENCES Client.Account(AccountId);
    END

    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AccountServiceProvider_Account')
    BEGIN
        ALTER TABLE Client.AccountServiceProvider
        ADD CONSTRAINT FK_AccountServiceProvider_Account
        FOREIGN KEY (AccountId) REFERENCES Client.Account(AccountId);
    END

    IF NOT EXISTS (SELECT * FROM sys.foreign_keys WHERE name = 'FK_AccountExtended_Account')
    BEGIN
        ALTER TABLE Client.AccountExtended
        ADD CONSTRAINT FK_AccountExtended_Account
        FOREIGN KEY (AccountId) REFERENCES Client.Account(AccountId);
    END
END
GO

PRINT 'Migration 0046 completed successfully'
