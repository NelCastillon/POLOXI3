-- ============================================================================
-- AMS CRM Seed Data - Lead Scoring, Assignment & Follow-up Pages
-- For Existing Database with LeadScoringRules & LeadAssignmentRule Tables
-- Safe to run multiple times - Checks for existing data
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT '========================================';
PRINT 'CRM Seed Data Script - Starting';
PRINT 'Time: ' + CONVERT(VARCHAR, GETUTCDATE(), 121);
PRINT '========================================';
PRINT '';

DECLARE @DefaultTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000099';
DECLARE @InsertCount INT = 0;

-- ============================================================================
-- VERIFY EXISTING TABLES
-- ============================================================================
PRINT '--- Verifying Existing Tables ---';

DECLARE @LeadScoringRulesExists BIT = 0;
DECLARE @LeadAssignmentRuleExists BIT = 0;
DECLARE @LeadsExists BIT = 0;
DECLARE @UsersExists BIT = 0;
DECLARE @LeadActivitiesExists BIT = 0;

SELECT @LeadScoringRulesExists = 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeadScoringRules';
SELECT @LeadAssignmentRuleExists = 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeadAssignmentRule';
SELECT @LeadsExists = 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Leads';
SELECT @UsersExists = 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Users';
SELECT @LeadActivitiesExists = 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'LeadActivities';

PRINT '✓ LeadScoringRules Table: ' + CASE WHEN @LeadScoringRulesExists = 1 THEN 'EXISTS' ELSE 'MISSING' END;
PRINT '✓ LeadAssignmentRule Table: ' + CASE WHEN @LeadAssignmentRuleExists = 1 THEN 'EXISTS' ELSE 'MISSING' END;
PRINT '✓ Leads Table: ' + CASE WHEN @LeadsExists = 1 THEN 'EXISTS' ELSE 'MISSING' END;
PRINT '✓ Users Table: ' + CASE WHEN @UsersExists = 1 THEN 'EXISTS' ELSE 'MISSING' END;
PRINT '✓ LeadActivities Table: ' + CASE WHEN @LeadActivitiesExists = 1 THEN 'EXISTS' ELSE 'MISSING' END;
PRINT '';

-- ============================================================================
-- SECTION 1: ENSURE LEAD SCORING RULES (for Lead Scoring page)
-- ============================================================================
IF @LeadScoringRulesExists = 1
BEGIN
    PRINT '--- Lead Scoring Rules ---';

    DECLARE @ExistingScoringRulesCount INT;
    SELECT @ExistingScoringRulesCount = COUNT(*) FROM [LeadScoringRules];

    IF @ExistingScoringRulesCount = 0
    BEGIN
        PRINT 'Inserting Lead Scoring Rules...';

        INSERT INTO [LeadScoringRules] 
            ([Name], [Description], [Value], [IsActive])
        VALUES
            ('Demo Request', 'Requested product demo', 20, 1),
            ('Form Submission', 'Completed contact form or webinar signup', 15, 1),
            ('Industry Match', 'Company in target industry', 10, 1),
            ('Website Visit', 'Visited pricing or product pages', 10, 1),
            ('Company Size', 'Enterprise company (1000+ employees)', 8, 1),
            ('Page Downloads', 'Downloaded whitepapers or case studies', 7, 1),
            ('Email Opens', 'Lead opened marketing email', 5, 1),
            ('Recent Activity', 'Active in last 7 days', 5, 1),
            ('LinkedIn Connection', 'Connected on LinkedIn', 3, 1);

        SET @InsertCount = @@ROWCOUNT;
        PRINT '✓ Inserted ' + CAST(@InsertCount AS VARCHAR) + ' scoring rules';
    END
    ELSE
    BEGIN
        PRINT '• ' + CAST(@ExistingScoringRulesCount AS VARCHAR) + ' scoring rules already exist';
    END;

    PRINT '';
END
ELSE
BEGIN
    PRINT '⚠ LeadScoringRules table does not exist - skipping';
    PRINT '';
END;

-- ============================================================================
-- SECTION 2: ENSURE LEAD ASSIGNMENT RULES (for Lead Assignment page)
-- ============================================================================
IF @LeadAssignmentRuleExists = 1
BEGIN
    PRINT '--- Lead Assignment Rules ---';

    DECLARE @ExistingAssignmentRulesCount INT;
    SELECT @ExistingAssignmentRulesCount = COUNT(*) FROM [LeadAssignmentRule];

    IF @ExistingAssignmentRulesCount = 0
    BEGIN
        PRINT 'Inserting Lead Assignment Rules...';

        INSERT INTO [LeadAssignmentRule]
            ([Name], [Description], [RuleType], [Criteria], [IsActive])
        VALUES
            ('High-Score Auto Assign', 'Automatically assign leads with score >= 80 to senior producers', 'Score-Based', 'Score >= 80', 1),
            ('Round-Robin Distribution', 'Distribute all leads equally among available producers', 'Round-Robin', 'All Leads', 1),
            ('Medium Priority Assignment', 'Assign leads with score 50-79 to all producers', 'Score-Based', 'Score 50-79', 1),
            ('Nurture Queue Assignment', 'Assign low-score leads to nurture team for follow-up', 'Score-Based', 'Score < 50', 1);

        SET @InsertCount = @@ROWCOUNT;
        PRINT '✓ Inserted ' + CAST(@InsertCount AS VARCHAR) + ' assignment rules';
    END
    ELSE
    BEGIN
        PRINT '• ' + CAST(@ExistingAssignmentRulesCount AS VARCHAR) + ' assignment rules already exist';
    END;

    PRINT '';
END
ELSE
BEGIN
    PRINT '⚠ LeadAssignmentRule table does not exist - skipping';
    PRINT '';
END;

-- ============================================================================
-- SECTION 3: SEED LEADS (if table exists)
-- ============================================================================
IF @LeadsExists = 1
BEGIN
    PRINT '--- Leads Data ---';

    DECLARE @ExistingLeadsCount INT;
    SELECT @ExistingLeadsCount = COUNT(*) FROM [Leads] WHERE [Id] IS NOT NULL;

    IF @ExistingLeadsCount = 0
    BEGIN
        PRINT 'Inserting Leads...';

        INSERT INTO [Leads]
            ([FirstName], [LastName], [Email], [Phone], [Company], [Score], [Status], [Source])
        VALUES
            -- High Priority (80+)
            ('Sarah', 'Anderson', 'sarah@techinnovations.com', '555-3001', 'Tech Innovations Inc', 85, 'New', 'Web'),
            ('Jennifer', 'Martinez', 'jennifer@premiergroup.com', '555-3002', 'Premier Group Holdings', 91, 'New', 'Direct'),
            ('Robert', 'Jackson', 'robert@innovationhub.com', '555-3003', 'Innovation Hub Co', 88, 'New', 'Organic'),
            ('Rachel', 'Santos', 'rachel@nextgen.com', '555-3004', 'NextGen Holdings', 89, 'New', 'Organic'),
            ('Michelle', 'Brown', 'michelle@alliancecap.com', '555-3005', 'Alliance Capital', 84, 'New', 'Partner'),
            ('James', 'Mitchell', 'james@future-ent.com', '555-3013', 'Future Enterprises', 81, 'New', 'Referral'),

            -- Medium Priority (50-79)
            ('Michael', 'Chen', 'michael@globalsolutions.com', '555-3006', 'Global Solutions Ltd', 72, 'New', 'Referral'),
            ('David', 'Thompson', 'david@enterprises.com', '555-3007', 'Enterprise Systems Corp', 65, 'New', 'Partner'),
            ('Emily', 'Watson', 'emily@digitalventures.com', '555-3008', 'Digital Ventures LLC', 78, 'New', 'Web'),
            ('Lisa', 'Graham', 'lisa@strategic.com', '555-3009', 'Strategic Partners Inc', 73, 'New', 'Direct'),
            ('Christopher', 'Davis', 'chris@summit.com', '555-3010', 'Summit Industries', 76, 'New', 'Web'),
            ('Marcus', 'Taylor', 'marcus@zenith.com', '555-3011', 'Zenith Corp', 75, 'New', 'Referral'),
            ('Charles', 'Williams', 'charles@venture.com', '555-3017', 'Venture Capital Group', 71, 'New', 'Direct'),

            -- Low Priority (<50)
            ('Amanda', 'Price', 'amanda@catalyst.com', '555-3012', 'Catalyst Group', 62, 'Nurture', 'Web'),
            ('Kevin', 'Wilson', 'kevin@proworks.com', '555-3014', 'ProWorks Solutions', 68, 'Nurture', 'Web'),
            ('Victoria', 'Kim', 'victoria@horizon.com', '555-3015', 'Horizon Ventures', 64, 'Nurture', 'Web'),
            ('Patricia', 'Johnson', 'patricia@innovate.com', '555-3016', 'Innovate Systems', 55, 'Nurture', 'Referral'),
            ('Diana', 'Moore', 'diana@strategic.com', '555-3018', 'Strategic Consulting', 48, 'Nurture', 'Web');

        SET @InsertCount = @@ROWCOUNT;
        PRINT '✓ Inserted ' + CAST(@InsertCount AS VARCHAR) + ' leads';
    END
    ELSE
    BEGIN
        PRINT '• ' + CAST(@ExistingLeadsCount AS VARCHAR) + ' leads already exist';
    END;

    PRINT '';
END
ELSE
BEGIN
    PRINT '⚠ Leads table does not exist - skipping';
    PRINT '';
END;

-- ============================================================================
-- SECTION 4: SEED LEAD ACTIVITIES (if table exists)
-- ============================================================================
IF @LeadActivitiesExists = 1
BEGIN
    PRINT '--- Lead Activities (Follow-ups) ---';

    DECLARE @ExistingActivitiesCount INT;
    SELECT @ExistingActivitiesCount = COUNT(*) FROM [LeadActivities];

    IF @ExistingActivitiesCount = 0 AND @LeadsExists = 1
    BEGIN
        PRINT 'Inserting Lead Activities...';

        DECLARE @FirstLeadId UNIQUEIDENTIFIER;
        SELECT @FirstLeadId = MIN([Id]) FROM [Leads] WHERE [Score] >= 85;

        IF @FirstLeadId IS NOT NULL
        BEGIN
            -- High priority calls for high-score leads
            INSERT INTO [LeadActivities]
                ([LeadId], [ActivityType], [Subject], [Description], [ScheduledDate], [Priority], [Status])
            SELECT TOP 6
                [Id],
                'Phone Call',
                'Initial Qualification Call',
                'Call to qualify lead and understand needs',
                DATEADD(DAY, 1, GETUTCDATE()),
                'High',
                'Pending'
            FROM [Leads]
            WHERE [Score] >= 85
            ORDER BY [Score] DESC;

            -- Medium priority emails for medium-score leads
            INSERT INTO [LeadActivities]
                ([LeadId], [ActivityType], [Subject], [Description], [ScheduledDate], [Priority], [Status])
            SELECT TOP 8
                [Id],
                'Email',
                'Send Product Information',
                'Send customized product overview and pricing',
                DATEADD(DAY, 2, GETUTCDATE()),
                'Medium',
                'Pending'
            FROM [Leads]
            WHERE [Score] BETWEEN 65 AND 79
            ORDER BY [Score] DESC;

            -- Low priority nurture for low-score leads
            INSERT INTO [LeadActivities]
                ([LeadId], [ActivityType], [Subject], [Description], [ScheduledDate], [Priority], [Status])
            SELECT TOP 5
                [Id],
                'Email',
                'Nurture Campaign - Educational Content',
                'Send educational content to nurture lead',
                DATEADD(DAY, 3, GETUTCDATE()),
                'Low',
                'Pending'
            FROM [Leads]
            WHERE [Score] < 65 AND [Status] = 'Nurture'
            ORDER BY [Score] ASC;

            SET @InsertCount = @@ROWCOUNT;
            PRINT '✓ Inserted lead activities';
        END
        ELSE
        BEGIN
            PRINT '⚠ No high-score leads found - skipping activities';
        END;
    END
    ELSE IF @ExistingActivitiesCount > 0
    BEGIN
        PRINT '• ' + CAST(@ExistingActivitiesCount AS VARCHAR) + ' lead activities already exist';
    END
    ELSE
    BEGIN
        PRINT '⚠ Leads table does not exist - cannot create activities';
    END;

    PRINT '';
END
ELSE
BEGIN
    PRINT '⚠ LeadActivities table does not exist - skipping';
    PRINT '';
END;

-- ============================================================================
-- FINAL SUMMARY
-- ============================================================================
PRINT '========================================';
PRINT 'Seed Data Script Completed';
PRINT '========================================';
PRINT '';
PRINT 'Data Summary:';

IF @LeadScoringRulesExists = 1
    PRINT '  • LeadScoringRules: ' + CAST((SELECT COUNT(*) FROM [LeadScoringRules]) AS VARCHAR);

IF @LeadAssignmentRuleExists = 1
    PRINT '  • LeadAssignmentRules: ' + CAST((SELECT COUNT(*) FROM [LeadAssignmentRule]) AS VARCHAR);

IF @LeadsExists = 1
    PRINT '  • Leads: ' + CAST((SELECT COUNT(*) FROM [Leads]) AS VARCHAR);

IF @LeadActivitiesExists = 1
    PRINT '  • LeadActivities: ' + CAST((SELECT COUNT(*) FROM [LeadActivities]) AS VARCHAR);

PRINT '';
PRINT 'Ready for:';
PRINT '  ✓ Lead Scoring Page (/crm/leads/scoring)';
PRINT '  ✓ Lead Assignment Page (/crm/leads/assignment)';
PRINT '  ✓ Lead Follow-up Page (/crm/leads/follow-up)';
PRINT '';
PRINT '========================================';

SET NOCOUNT OFF;
