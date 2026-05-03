-- ============================================================
-- MIGRATION 0047: CRM EXTENDED SCHEMA
-- Adds tables not yet present in the existing CRM schema.
--
-- EXISTING (do NOT recreate):
--   CRM.Lead, CRM.LeadActivity, CRM.Opportunity,
--   CRM.Quote, CRM.QuoteLine, CRM.ForecastEntry, CRM.PricingRule
--
-- NEW (added here, idempotent guards on every object):
--   CRM.CustomerSegment, CRM.SegmentationRule,
--   CRM.LeadScoringRule,
--   CRM.PriceClass, CRM.MarketAppetite, CRM.CarrierMapping
-- ============================================================

-- ── Customer Segments ──────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('CRM') AND name = 'CustomerSegment')
BEGIN
    CREATE TABLE CRM.CustomerSegment (
        SegmentId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        SegmentName         NVARCHAR(200)    NOT NULL,
        Description         NVARCHAR(500)    NULL,
        SegmentType         NVARCHAR(100)    NOT NULL DEFAULT 'Static', -- 'Static','Dynamic'
        MemberCount         INT              NOT NULL DEFAULT 0,
        IsActive            BIT              NOT NULL DEFAULT 1,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_CustomerSegment_TenantId
        ON CRM.CustomerSegment (TenantId, IsDeleted);
END
GO

-- ── Segmentation Rules ─────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('CRM') AND name = 'SegmentationRule')
BEGIN
    CREATE TABLE CRM.SegmentationRule (
        RuleId              UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        SegmentId           UNIQUEIDENTIFIER NOT NULL,
        Field               NVARCHAR(100)    NOT NULL,
        Operator            NVARCHAR(50)     NOT NULL,  -- 'Equals','Contains','GreaterThan', etc.
        Value               NVARCHAR(500)    NOT NULL,
        LogicConnector      NVARCHAR(10)     NOT NULL DEFAULT 'AND',
        SortOrder           INT              NOT NULL DEFAULT 0,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_SegmentationRule_SegmentId
        ON CRM.SegmentationRule (SegmentId, IsDeleted);
END
GO

-- ── Lead Scoring Rules ─────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('CRM') AND name = 'LeadScoringRule')
BEGIN
    CREATE TABLE CRM.LeadScoringRule (
        ScoringRuleId       UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        RuleName            NVARCHAR(200)    NOT NULL,
        Field               NVARCHAR(100)    NOT NULL,
        Operator            NVARCHAR(50)     NOT NULL,
        Value               NVARCHAR(500)    NOT NULL,
        Points              INT              NOT NULL DEFAULT 0,
        IsActive            BIT              NOT NULL DEFAULT 1,
        SortOrder           INT              NOT NULL DEFAULT 0,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_LeadScoringRule_TenantId
        ON CRM.LeadScoringRule (TenantId, IsDeleted);
END
GO

-- ── Price Classes ──────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('CRM') AND name = 'PriceClass')
BEGIN
    CREATE TABLE CRM.PriceClass (
        PriceClassId        UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        ClassName           NVARCHAR(200)    NOT NULL,
        LineOfBusiness      NVARCHAR(100)    NULL,
        Description         NVARCHAR(500)    NULL,
        BaseRate            DECIMAL(18,4)    NULL,
        MinPremium          DECIMAL(18,2)    NULL,
        MaxPremium          DECIMAL(18,2)    NULL,
        IsActive            BIT              NOT NULL DEFAULT 1,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_PriceClass_TenantId
        ON CRM.PriceClass (TenantId, IsDeleted);
END
GO

-- ── Market Appetite ────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('CRM') AND name = 'MarketAppetite')
BEGIN
    CREATE TABLE CRM.MarketAppetite (
        AppetiteId          UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        CarrierName         NVARCHAR(200)    NOT NULL,
        LineOfBusiness      NVARCHAR(100)    NOT NULL,
        AppetiteLevel       NVARCHAR(50)     NOT NULL DEFAULT 'Preferred', -- 'Preferred','Acceptable','Avoid','Declined'
        MinPremium          DECIMAL(18,2)    NULL,
        MaxPremium          DECIMAL(18,2)    NULL,
        Notes               NVARCHAR(500)    NULL,
        IsActive            BIT              NOT NULL DEFAULT 1,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_MarketAppetite_TenantId
        ON CRM.MarketAppetite (TenantId, IsDeleted);
END
GO

-- ── Carrier Mapping (CRM) ─────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE schema_id = SCHEMA_ID('CRM') AND name = 'CarrierMapping')
BEGIN
    CREATE TABLE CRM.CarrierMapping (
        MappingId           UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        TenantId            UNIQUEIDENTIFIER NOT NULL,
        CarrierName         NVARCHAR(200)    NOT NULL,
        InternalCode        NVARCHAR(50)     NULL,
        ExternalCode        NVARCHAR(50)     NULL,
        LineOfBusiness      NVARCHAR(100)    NULL,
        DownloadFormat      NVARCHAR(50)     NULL, -- 'AL3','IVANS','Custom'
        IsActive            BIT              NOT NULL DEFAULT 1,
        Notes               NVARCHAR(500)    NULL,
        CreatedDateUtc      DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
        CreatedByUserId     UNIQUEIDENTIFIER NULL,
        IsDeleted           BIT              NOT NULL DEFAULT 0
    );

    CREATE NONCLUSTERED INDEX IX_CrmCarrierMapping_TenantId
        ON CRM.CarrierMapping (TenantId, IsDeleted);
END
GO

-- ═══════════════════════════════════════════════════════════
-- SEED DATA (all idempotent)
-- ═══════════════════════════════════════════════════════════
DECLARE @SeedTenant UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';

-- Lead Scoring Rules
IF NOT EXISTS (SELECT 1 FROM CRM.LeadScoringRule WHERE TenantId = @SeedTenant)
BEGIN
    INSERT INTO CRM.LeadScoringRule (TenantId, RuleName, Field, Operator, Value, Points, SortOrder)
    VALUES
        (@SeedTenant, 'Has Email',             'Email',          'IsNotEmpty', '',            10, 1),
        (@SeedTenant, 'Has Phone',             'Phone',          'IsNotEmpty', '',            10, 2),
        (@SeedTenant, 'Web Source',            'Source',         'Equals',     'Web',         15, 3),
        (@SeedTenant, 'Referral Source',       'Source',         'Equals',     'Referral',    25, 4),
        (@SeedTenant, 'High Premium Estimate', 'EstPremium',     'GreaterThan','10000',        20, 5),
        (@SeedTenant, 'Commercial LOB',        'LineOfBusiness', 'Equals',     'Commercial',  20, 6);
END
GO

-- Customer Segments
IF NOT EXISTS (SELECT 1 FROM CRM.CustomerSegment WHERE TenantId = @SeedTenant)
BEGIN
    INSERT INTO CRM.CustomerSegment (TenantId, SegmentName, Description, SegmentType, MemberCount)
    VALUES
        (@SeedTenant, 'High-Value Accounts',  'Accounts with premium > $50k',      'Dynamic', 0),
        (@SeedTenant, 'SMB Prospects',        'Small and mid-market prospects',     'Dynamic', 0),
        (@SeedTenant, 'Renewal Targets',      'Policies expiring within 90 days',   'Dynamic', 0),
        (@SeedTenant, 'At-Risk Clients',      'Low engagement score accounts',      'Dynamic', 0),
        (@SeedTenant, 'Cross-Sell Ready',     'Clients with single LOB coverage',   'Static',  0);
END
GO

-- Market Appetite
IF NOT EXISTS (SELECT 1 FROM CRM.MarketAppetite WHERE TenantId = @SeedTenant)
BEGIN
    INSERT INTO CRM.MarketAppetite (TenantId, CarrierName, LineOfBusiness, AppetiteLevel, MinPremium, MaxPremium, Notes)
    VALUES
        (@SeedTenant, 'Travelers',         'Commercial Property', 'Preferred',   5000,  500000, 'Strong appetite for mid-market'),
        (@SeedTenant, 'Travelers',         'General Liability',   'Preferred',   2500,  250000, NULL),
        (@SeedTenant, 'Hartford',          'Workers Comp',        'Preferred',   1000,  100000, 'Preferred for manufacturing'),
        (@SeedTenant, 'Hartford',          'Commercial Auto',     'Acceptable',  2000,  200000, NULL),
        (@SeedTenant, 'Chubb',            'Professional Liability','Preferred', 10000, 1000000, 'E&O and D&O specialist'),
        (@SeedTenant, 'Markel',            'Excess & Surplus',    'Preferred',  15000, 5000000, 'Hard-to-place risks'),
        (@SeedTenant, 'State Auto',        'Personal Lines',      'Acceptable',   500,   25000, NULL),
        (@SeedTenant, 'Employers Mutual',  'Workers Comp',        'Avoid',        NULL,    NULL, 'Current moratorium');
END
GO

-- Price Classes
IF NOT EXISTS (SELECT 1 FROM CRM.PriceClass WHERE TenantId = @SeedTenant)
BEGIN
    INSERT INTO CRM.PriceClass (TenantId, ClassName, LineOfBusiness, Description, BaseRate, MinPremium, MaxPremium)
    VALUES
        (@SeedTenant, 'Standard Commercial', 'Commercial Property',     'Standard market risk',        0.0045, 2500,   500000),
        (@SeedTenant, 'Preferred Commercial','Commercial Property',     'Low-hazard, preferred risk',  0.0032, 5000,   500000),
        (@SeedTenant, 'Standard GL',         'General Liability',       'Standard liability class',    0.0085, 1500,   250000),
        (@SeedTenant, 'Contractors GL',      'General Liability',       'Artisan / contractor class',  0.0120, 2500,   100000),
        (@SeedTenant, 'Standard WC',         'Workers Compensation',    'Standard classification',     0.0220, 1000,   100000),
        (@SeedTenant, 'Professional E&O',    'Professional Liability',  'Errors & Omissions',          0.0150, 5000,  1000000);
END
GO

-- Carrier Mappings
IF NOT EXISTS (SELECT 1 FROM CRM.CarrierMapping WHERE TenantId = @SeedTenant)
BEGIN
    INSERT INTO CRM.CarrierMapping (TenantId, CarrierName, InternalCode, ExternalCode, LineOfBusiness, DownloadFormat, Notes)
    VALUES
        (@SeedTenant, 'Travelers',       'TRV',  'TRV001', 'Commercial Property', 'AL3',    NULL),
        (@SeedTenant, 'Travelers',       'TRV',  'TRV001', 'General Liability',   'AL3',    NULL),
        (@SeedTenant, 'Hartford',        'HFD',  'HFD002', 'Workers Comp',        'AL3',    NULL),
        (@SeedTenant, 'Hartford',        'HFD',  'HFD002', 'Commercial Auto',     'AL3',    NULL),
        (@SeedTenant, 'Chubb',          'CHB',  'CHB003', 'Professional Liability','IVANS', NULL),
        (@SeedTenant, 'Markel',          'MKL',  'MKL004', 'Excess & Surplus',    'Custom', 'Custom XML format'),
        (@SeedTenant, 'State Auto',      'STA',  'STA005', 'Personal Lines',      'AL3',    NULL),
        (@SeedTenant, 'Employers Mutual','EMP',  'EMP006', 'Workers Comp',        'IVANS',  NULL);
END
GO
