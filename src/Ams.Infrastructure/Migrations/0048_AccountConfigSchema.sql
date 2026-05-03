-- ============================================================
-- Migration 0048: Account Configuration Tables
-- Creates: Client.RelationshipType, Client.ContactType,
--          Client.AccountCustomField, Client.HouseholdSetting,
--          Client.CommercialEntitySetting
-- ============================================================

-- ── Client.RelationshipType ─────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'RelationshipType')
BEGIN
    CREATE TABLE Client.RelationshipType (
        RelationshipTypeId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        TypeCode            NVARCHAR(80)     NOT NULL,
        TypeName            NVARCHAR(200)    NOT NULL,
        IsBidirectional     BIT              NOT NULL DEFAULT 0,
        InverseTypeCode     NVARCHAR(80)     NULL,
        Description         NVARCHAR(500)    NULL,
        IsActive            BIT              NOT NULL DEFAULT 1,
        SortOrder           INT              NOT NULL DEFAULT 0,
        IsDeleted           BIT              NOT NULL DEFAULT 0,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc     DATETIME2        NULL
    );

    CREATE INDEX IX_RelationshipType_Tenant
        ON Client.RelationshipType (TenantId, IsDeleted);
END
GO

-- ── Client.ContactType ──────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'ContactType')
BEGIN
    CREATE TABLE Client.ContactType (
        ContactTypeId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        TypeCode        NVARCHAR(80)     NOT NULL,
        TypeName        NVARCHAR(200)    NOT NULL,
        Description     NVARCHAR(500)    NULL,
        IsDefault       BIT              NOT NULL DEFAULT 0,
        IsActive        BIT              NOT NULL DEFAULT 1,
        SortOrder       INT              NOT NULL DEFAULT 0,
        IsDeleted       BIT              NOT NULL DEFAULT 0,
        CreatedDateUtc  DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc DATETIME2        NULL
    );

    CREATE INDEX IX_ContactType_Tenant
        ON Client.ContactType (TenantId, IsDeleted);
END
GO

-- ── Client.AccountCustomField ───────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'AccountCustomField')
BEGIN
    CREATE TABLE Client.AccountCustomField (
        CustomFieldId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId        UNIQUEIDENTIFIER NOT NULL,
        FieldCode       NVARCHAR(80)     NOT NULL,
        FieldName       NVARCHAR(200)    NOT NULL,
        EntityType      NVARCHAR(80)     NOT NULL,   -- 'Account', 'Household', 'Contact'
        FieldType       NVARCHAR(80)     NOT NULL,   -- 'Text', 'Number', 'Date', 'Dropdown', 'Checkbox'
        DefaultValue    NVARCHAR(500)    NULL,
        DropdownOptions NVARCHAR(2000)   NULL,
        IsRequired      BIT              NOT NULL DEFAULT 0,
        IsSearchable    BIT              NOT NULL DEFAULT 0,
        IsActive        BIT              NOT NULL DEFAULT 1,
        SortOrder       INT              NOT NULL DEFAULT 0,
        IsDeleted       BIT              NOT NULL DEFAULT 0,
        CreatedDateUtc  DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc DATETIME2        NULL
    );

    CREATE INDEX IX_AccountCustomField_Tenant
        ON Client.AccountCustomField (TenantId, EntityType, IsDeleted);
END
GO

-- ── Client.HouseholdSetting ─────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'HouseholdSetting')
BEGIN
    CREATE TABLE Client.HouseholdSetting (
        HouseholdSettingId  UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        SettingKey          NVARCHAR(100)    NOT NULL,
        SettingValue        NVARCHAR(500)    NULL,
        SettingType         NVARCHAR(50)     NOT NULL DEFAULT 'String',  -- 'String', 'Boolean', 'Number'
        Description         NVARCHAR(500)    NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc     DATETIME2        NULL
    );

    CREATE INDEX IX_HouseholdSetting_Tenant
        ON Client.HouseholdSetting (TenantId, IsDeleted);

    -- Seed default settings for existing tenants
    INSERT INTO Client.HouseholdSetting (HouseholdSettingId, TenantId, SettingKey, SettingValue, SettingType, Description, CreatedDateUtc)
    SELECT
        NEWID(), t.TenantId, s.SettingKey, s.DefaultValue, s.SettingType, s.Description, GETUTCDATE()
    FROM [Tenants].Tenant t
    CROSS JOIN (VALUES
        ('AutoGroupHouseholds',    'true',    'Boolean', 'Automatically group accounts into households'),
        ('HouseholdNameFormat',    'Primary', 'String',  'How to derive the household display name'),
        ('MinMembersToGroup',      '2',       'Number',  'Minimum members required to form a household'),
        ('AllowManualOverride',    'true',    'Boolean', 'Allow agents to manually assign household membership')
    ) AS s(SettingKey, DefaultValue, SettingType, Description)
    WHERE NOT EXISTS (
        SELECT 1 FROM Client.HouseholdSetting hs
        WHERE hs.TenantId = t.TenantId AND hs.SettingKey = s.SettingKey
    );
END
GO

-- ── Client.CommercialEntitySetting ──────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'CommercialEntitySetting')
BEGIN
    CREATE TABLE Client.CommercialEntitySetting (
        CommercialEntitySettingId   UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId                    UNIQUEIDENTIFIER NOT NULL,
        SettingKey                  NVARCHAR(100)    NOT NULL,
        SettingValue                NVARCHAR(500)    NULL,
        SettingType                 NVARCHAR(50)     NOT NULL DEFAULT 'String',  -- 'String', 'Boolean', 'Number'
        Description                 NVARCHAR(500)    NULL,
        IsDeleted                   BIT              NOT NULL DEFAULT 0,
        CreatedDateUtc              DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        ModifiedDateUtc             DATETIME2        NULL
    );

    CREATE INDEX IX_CommercialEntitySetting_Tenant
        ON Client.CommercialEntitySetting (TenantId, IsDeleted);

    -- Seed default settings for existing tenants
    INSERT INTO Client.CommercialEntitySetting (CommercialEntitySettingId, TenantId, SettingKey, SettingValue, SettingType, Description, CreatedDateUtc)
    SELECT
        NEWID(), t.TenantId, s.SettingKey, s.DefaultValue, s.SettingType, s.Description, GETUTCDATE()
    FROM [Tenants].Tenant t
    CROSS JOIN (VALUES
        ('RequireFEIN',            'false',   'Boolean', 'Require Federal Employer Identification Number for commercial accounts'),
        ('RequireNAICSCode',       'false',   'Boolean', 'Require NAICS industry classification code'),
        ('RequireDBAName',         'false',   'Boolean', 'Require Doing Business As name'),
        ('DefaultEntityType',      'LLC',     'String',  'Default legal entity type for new commercial accounts'),
        ('EnableRiskScoring',      'true',    'Boolean', 'Enable automatic risk scoring for commercial entities')
    ) AS s(SettingKey, DefaultValue, SettingType, Description)
    WHERE NOT EXISTS (
        SELECT 1 FROM Client.CommercialEntitySetting ces
        WHERE ces.TenantId = t.TenantId AND ces.SettingKey = s.SettingKey
    );
END
GO

-- ── Seed default RelationshipTypes ──────────────────────────
IF EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'RelationshipType')
   AND NOT EXISTS (SELECT 1 FROM Client.RelationshipType WHERE TypeCode = 'SUBSIDIARY')
BEGIN
    INSERT INTO Client.RelationshipType (RelationshipTypeId, TenantId, TypeCode, TypeName, IsBidirectional, InverseTypeCode, Description, SortOrder, IsActive, IsDeleted, CreatedDateUtc)
    SELECT NEWID(), t.TenantId, s.TypeCode, s.TypeName, s.IsBidirectional, s.InverseTypeCode, s.Description, s.SortOrder, 1, 0, GETUTCDATE()
    FROM [Tenants].Tenant t
    CROSS JOIN (VALUES
        ('SUBSIDIARY',  'Subsidiary',     0, 'PARENT_OF',   'Parent company owns or controls this entity',      1),
        ('PARENT_OF',   'Parent Of',      0, 'SUBSIDIARY',  'This entity owns or controls the related account', 2),
        ('AFFILIATE',   'Affiliate',      1, 'AFFILIATE',   'Commonly owned or controlled entities',            3),
        ('PARTNER',     'Partner',        1, 'PARTNER',     'Business partnership relationship',                4),
        ('FRANCHISOR',  'Franchisor',     0, 'FRANCHISEE',  'Grants franchise rights to the related entity',    5),
        ('FRANCHISEE',  'Franchisee',     0, 'FRANCHISOR',  'Operates under a franchise agreement',             6)
    ) AS s(TypeCode, TypeName, IsBidirectional, InverseTypeCode, Description, SortOrder);
END
GO

-- ── Seed default ContactTypes ────────────────────────────────
IF EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('Client') AND name = 'ContactType')
   AND NOT EXISTS (SELECT 1 FROM Client.ContactType WHERE TypeCode = 'PRIMARY')
BEGIN
    INSERT INTO Client.ContactType (ContactTypeId, TenantId, TypeCode, TypeName, Description, IsDefault, SortOrder, IsActive, IsDeleted, CreatedDateUtc)
    SELECT NEWID(), t.TenantId, s.TypeCode, s.TypeName, s.Description, s.IsDefault, s.SortOrder, 1, 0, GETUTCDATE()
    FROM [Tenants].Tenant t
    CROSS JOIN (VALUES
        ('PRIMARY',   'Primary Contact',   'Main point of contact for the account',            1, 1),
        ('BILLING',   'Billing Contact',   'Responsible for billing and payment matters',      0, 2),
        ('CLAIMS',    'Claims Contact',    'Point of contact for claims-related matters',      0, 3),
        ('RENEWAL',   'Renewal Contact',   'Contact for policy renewal communications',        0, 4),
        ('SECONDARY', 'Secondary Contact', 'Additional contact for general correspondence',    0, 5)
    ) AS s(TypeCode, TypeName, Description, IsDefault, SortOrder);
END
GO

PRINT 'Migration 0048 completed successfully'
