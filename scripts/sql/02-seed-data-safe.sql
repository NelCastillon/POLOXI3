-- ============================================================================
-- AMS Database Seed Data - CRM Tables (With Duplicate Prevention)
-- Comprehensive test data for Lead Scoring, Assignment, and Follow-up
-- Safe to run multiple times - checks for existing data before inserting
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

-- ============================================================================
-- CONFIGURATION SECTION
-- ============================================================================
PRINT '========================================';
PRINT 'AMS Seed Data Script - Starting';
PRINT 'Time: ' + CONVERT(VARCHAR, GETUTCDATE(), 121);
PRINT '========================================';
PRINT '';

-- Define IDs for consistent reference
DECLARE @DefaultTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000099';

-- Counter variables
DECLARE @TenantInsertCount INT = 0;
DECLARE @UserInsertCount INT = 0;
DECLARE @AccountInsertCount INT = 0;
DECLARE @LeadInsertCount INT = 0;
DECLARE @RuleInsertCount INT = 0;
DECLARE @ActivityInsertCount INT = 0;
DECLARE @MetricInsertCount INT = 0;





-- ============================================================================
-- SECTION 5: CHECK AND CREATE LEAD SCORING RULES
-- ============================================================================
PRINT '--- SECTION 5: Lead Scoring Rules ---';

-- Define scoring rules table variable
DECLARE @RulesToInsert TABLE (
    RuleName NVARCHAR(255),
    RuleType VARCHAR(50),
    Points INT,
    Description NVARCHAR(MAX),
    Condition NVARCHAR(MAX)
);



-- Check for existing scoring rules
DECLARE @ExistingRuleCount INT;
SELECT @ExistingRuleCount = COUNT(*) 
FROM [CRM].[LeadScoringRules] 
WHERE [TenantId] = @DefaultTenantId;


-- ============================================================================
-- SECTION 6: CHECK AND CREATE LEAD ASSIGNMENT RULES
-- ============================================================================
PRINT '--- SECTION 6: Lead Assignment Rules ---';



-- ============================================================================
-- FINAL SUMMARY
-- ============================================================================
PRINT '';
PRINT '========================================';
PRINT 'Seed Data Script Completed Successfully!';
PRINT '========================================';
PRINT '';
PRINT 'Summary of Operations:';
PRINT '  • Tenants: ' + CAST(@TenantInsertCount AS VARCHAR) + ' inserted';
PRINT '  • Users: ' + CAST(@UserInsertCount AS VARCHAR) + ' inserted';
PRINT '  • Accounts: ' + CAST(@AccountInsertCount AS VARCHAR) + ' inserted';
PRINT '  • Leads: ' + CAST(@LeadInsertCount AS VARCHAR) + ' inserted';
PRINT '  • Scoring Rules: ' + CAST(@RuleInsertCount AS VARCHAR) + ' inserted';
PRINT '  • Lead Activities: ' + CAST(@ActivityInsertCount AS VARCHAR) + ' inserted';
PRINT '  • Quality Metrics: ' + CAST(@MetricInsertCount AS VARCHAR) + ' inserted';
PRINT '';

-- Final database statistics
PRINT 'Current Database Statistics:';
PRINT '  • Total Tenants: ' + CAST((SELECT COUNT(*) FROM [CRM].[Tenants]) AS VARCHAR);
PRINT '  • Total Users: ' + CAST((SELECT COUNT(*) FROM [CRM].[Users]) AS VARCHAR);
PRINT '  • Total Accounts: ' + CAST((SELECT COUNT(*) FROM [CRM].[Accounts]) AS VARCHAR);
PRINT '  • Total Leads: ' + CAST((SELECT COUNT(*) FROM [CRM].[Leads]) AS VARCHAR);
PRINT '  • Total Scoring Rules: ' + CAST((SELECT COUNT(*) FROM [CRM].[LeadScoringRules]) AS VARCHAR);
PRINT '  • Total Assignment Rules: ' + CAST((SELECT COUNT(*) FROM [CRM].[LeadAssignmentRules]) AS VARCHAR);
PRINT '  • Total Lead Activities: ' + CAST((SELECT COUNT(*) FROM [CRM].[LeadActivities]) AS VARCHAR);
PRINT '  • Total Quality Metrics: ' + CAST((SELECT COUNT(*) FROM [CRM].[LeadQualityMetrics]) AS VARCHAR);
PRINT '';
PRINT '========================================';
PRINT 'Time Completed: ' + CONVERT(VARCHAR, GETUTCDATE(), 121);
PRINT '========================================';

SET NOCOUNT OFF;
