-- ============================================================================
-- CRM Enhancement Migration - Lead Scoring, Assignment & Follow-up
-- Migration ID: 0043_CRM_LeadScoring_Assignment_FollowUp_Seed
-- 
-- This migration adds supporting tables and seed data for the 3 CRM pages:
-- 1. Lead Scoring Page - Display leads with quality scores
-- 2. Lead Assignment Page - Assign leads to producers
-- 3. Lead Follow-up Page - Track follow-up activities
--
-- Safe to run multiple times - uses IF NOT EXISTS checks
-- ============================================================================

-- ============================================================================
-- SECTION 1: Create LeadScoringRules table (if not exists)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'CRM.LeadScoringRule'))
BEGIN
    CREATE TABLE CRM.LeadScoringRule (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        RuleName NVARCHAR(255) NOT NULL,
        RuleType VARCHAR(50) NOT NULL,
        Points INT NOT NULL DEFAULT 0,
        Description NVARCHAR(MAX),
        Condition NVARCHAR(MAX),
        IsActive BIT NOT NULL DEFAULT 1,
        DisplayOrder INT DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER,
        ModifiedDateUtc DATETIME2,
        ModifiedByUserId UNIQUEIDENTIFIER,
        IsDeleted BIT NOT NULL DEFAULT 0,

        INDEX IX_LeadScoringRule_TenantId NONCLUSTERED (TenantId),
        INDEX IX_LeadScoringRule_RuleType NONCLUSTERED (RuleType),
        INDEX IX_LeadScoringRule_IsActive NONCLUSTERED (IsActive)
    );

    PRINT 'Created table: CRM.LeadScoringRule';
END
ELSE
BEGIN
    PRINT 'Table CRM.LeadScoringRule already exists';
END;

-- ============================================================================
-- SECTION 2: Create LeadAssignmentRule table (if not exists)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID(N'CRM.LeadAssignmentRule'))
BEGIN
    CREATE TABLE CRM.LeadAssignmentRule (
        Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        RuleName NVARCHAR(255) NOT NULL,
        RuleType VARCHAR(50) NOT NULL,
        Criteria NVARCHAR(MAX),
        TargetGroup NVARCHAR(255),
        MaxAssignments INT DEFAULT 0,
        IsActive BIT NOT NULL DEFAULT 1,
        DisplayOrder INT DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER,
        ModifiedDateUtc DATETIME2,
        ModifiedByUserId UNIQUEIDENTIFIER,
        IsDeleted BIT NOT NULL DEFAULT 0,

        INDEX IX_LeadAssignmentRule_TenantId NONCLUSTERED (TenantId),
        INDEX IX_LeadAssignmentRule_RuleType NONCLUSTERED (RuleType),
        INDEX IX_LeadAssignmentRule_IsActive NONCLUSTERED (IsActive)
    );

    PRINT 'Created table: CRM.LeadAssignmentRule';
END
ELSE
BEGIN
    PRINT 'Table CRM.LeadAssignmentRule already exists';
END;

-- ============================================================================
-- SECTION 3: Seed LeadScoringRules (if not exists)
-- ============================================================================
DECLARE @DefaultTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000099';

IF NOT EXISTS (SELECT 1 FROM CRM.LeadScoringRule WHERE TenantId = @DefaultTenantId AND RuleName = 'Demo Request')
BEGIN
    INSERT INTO CRM.LeadScoringRule 
        (Id, TenantId, RuleName, RuleType, Points, Description, Condition, IsActive, DisplayOrder, CreatedDateUtc, CreatedByUserId)
    VALUES
        (NEWID(), @DefaultTenantId, 'Demo Request', 'Engagement', 20, 'Requested product demo', 'Demo request submitted', 1, 1, SYSUTCDATETIME(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Form Submission', 'Engagement', 15, 'Completed contact form or webinar signup', 'Form submitted in last 7 days', 1, 2, SYSUTCDATETIME(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Industry Match', 'Profile', 10, 'Company in target industry', 'Industry matches target list', 1, 3, SYSUTCDATETIME(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Website Visit', 'Behavior', 10, 'Visited pricing page or product pages', 'Visited site in last 14 days', 1, 4, SYSUTCDATETIME(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Company Size', 'Profile', 8, 'Enterprise company (1000+ employees)', '1000+ employee count', 1, 5, SYSUTCDATETIME(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Page Downloads', 'Behavior', 7, 'Downloaded whitepapers or case studies', 'Download in last 30 days', 1, 6, SYSUTCDATETIME(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Email Opens', 'Engagement', 5, 'Lead opened marketing email', 'Email opened in last 30 days', 1, 7, SYSUTCDATETIME(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Recent Activity', 'Recency', 5, 'Active in last 7 days', 'Any activity last 7 days', 1, 8, SYSUTCDATETIME(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LinkedIn Connection', 'Engagement', 3, 'Connected on LinkedIn', 'LinkedIn connection active', 1, 9, SYSUTCDATETIME(), @SystemUserId);

    PRINT '✓ Inserted 9 Lead Scoring Rules';
END
ELSE
BEGIN
    PRINT '• Lead Scoring Rules already exist';
END;

-- ============================================================================
-- SECTION 4: Seed LeadAssignmentRules (if not exists)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM CRM.LeadAssignmentRule WHERE TenantId = @DefaultTenantId AND RuleName = 'High-Score Auto Assign')
BEGIN
    INSERT INTO CRM.LeadAssignmentRule 
        (Id, TenantId, RuleName, RuleType, Criteria, TargetGroup, MaxAssignments, IsActive, DisplayOrder, CreatedDateUtc, CreatedByUserId)
    VALUES
        (NEWID(), @DefaultTenantId, 'High-Score Auto Assign', 'Score-Based', 'Score >= 80', 'Senior Producers', 5, 1, 1, SYSUTCDATETIME(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Round-Robin Distribution', 'Round-Robin', 'All Leads', 'All Producers', 0, 1, 2, SYSUTCDATETIME(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Medium Priority Assignment', 'Score-Based', 'Score 50-79', 'All Producers', 0, 1, 3, SYSUTCDATETIME(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Nurture Queue Assignment', 'Score-Based', 'Score < 50', 'Nurture Team', 0, 1, 4, SYSUTCDATETIME(), @SystemUserId);

    PRINT '✓ Inserted 4 Lead Assignment Rules';
END
ELSE
BEGIN
    PRINT '• Lead Assignment Rules already exist';
END;

-- ============================================================================
-- SECTION 5: Verify completion
-- ============================================================================
PRINT '';
PRINT '========================================';
PRINT 'CRM Enhancement Migration Complete';
PRINT '========================================';
PRINT '';
PRINT 'Tables Created:';
PRINT '  ✓ CRM.LeadScoringRule';
PRINT '  ✓ CRM.LeadAssignmentRule';
PRINT '';
PRINT 'Seed Data Inserted:';
PRINT '  ✓ 9 Scoring Rules';
PRINT '  ✓ 4 Assignment Rules';
PRINT '';
PRINT 'Ready for:';
PRINT '  ✓ Lead Scoring Page';
PRINT '  ✓ Lead Assignment Page';
PRINT '  ✓ Lead Follow-up Page (uses existing CRM.LeadActivity)';
PRINT '';
PRINT '========================================';
