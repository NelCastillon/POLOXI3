-- ============================================================================
-- AMS Database Seed Data - CRM Pages Only
-- Lead Scoring, Lead Assignment, and Lead Follow-up Pages
-- Safe to run multiple times - checks for existing data before inserting
-- ============================================================================

SET NOCOUNT ON;
SET XACT_ABORT ON;

PRINT '========================================';
PRINT 'AMS CRM Seed Data - 3 Pages Only';
PRINT 'Time: ' + CONVERT(VARCHAR, GETUTCDATE(), 121);
PRINT '========================================';
PRINT '';

-- ============================================================================
-- CONFIGURATION
-- ============================================================================
DECLARE @DefaultTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000099';

-- ============================================================================
-- 1. INSERT TENANTS (if not exists)
-- ============================================================================
PRINT '--- 1. Tenants ---';

IF NOT EXISTS (SELECT 1 FROM [dbo].[Tenants] WHERE [Id] = @DefaultTenantId)
BEGIN
    INSERT INTO [dbo].[Tenants] ([Id], [TenantCode], [TenantName], [StatusCode], [CreatedDateUtc])
    VALUES (@DefaultTenantId, 'DEFAULT', 'Default Tenant', 'Active', GETUTCDATE());
    PRINT '✓ Default tenant inserted';
END
ELSE
    PRINT '• Tenant already exists';

PRINT '';

-- ============================================================================
-- 2. INSERT USERS (Producers)
-- ============================================================================
PRINT '--- 2. Users (Producers) ---';

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [TenantId] = @DefaultTenantId AND [UserName] = 'jspencer')
BEGIN
    INSERT INTO [dbo].[Users] 
        ([Id], [TenantId], [UserName], [Email], [FullName], [DisplayName], [JobTitle], [Department], [UserTypeCode], [StatusCode], [PhoneNumber], [TimeZoneCode], [CreatedDateUtc], [CreatedByUserId])
    VALUES
        (NEWID(), @DefaultTenantId, 'jspencer', 'john.spencer@agencybinder.com', 'John Spencer', 'John S.', 'Senior Producer', 'Sales', 'Internal', 'Active', '555-0101', 'America/Chicago', GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'ahayes', 'amanda.hayes@agencybinder.com', 'Amanda Hayes', 'Amanda H.', 'Producer', 'Sales', 'Internal', 'Active', '555-0102', 'America/Chicago', GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'rmitchell', 'ryan.mitchell@agencybinder.com', 'Ryan Mitchell', 'Ryan M.', 'Account Executive', 'Sales', 'Internal', 'Active', '555-0103', 'America/New_York', GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'jbrown', 'jessica.brown@agencybinder.com', 'Jessica Brown', 'Jessica B.', 'Producer', 'Sales', 'Internal', 'Active', '555-0104', 'America/Los_Angeles', GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'tanderson', 'thomas.anderson@agencybinder.com', 'Thomas Anderson', 'Tom A.', 'Senior Producer', 'Sales', 'Internal', 'Active', '555-0105', 'America/Chicago', GETUTCDATE(), @SystemUserId);
    PRINT '✓ 5 producers inserted';
END
ELSE
    PRINT '• Producers already exist';

PRINT '';

-- ============================================================================
-- 3. INSERT LEADS (for Lead Scoring & Assignment pages)
-- ============================================================================
PRINT '--- 3. Leads ---';

IF NOT EXISTS (SELECT 1 FROM [dbo].[Leads] WHERE [TenantId] = @DefaultTenantId AND [LeadNumber] = 'LEAD-001')
BEGIN
    INSERT INTO [dbo].[Leads]
        ([Id], [TenantId], [LeadNumber], [FirstName], [LastName], [Email], [Phone], [AccountName], [InterestedService], [Score], [PriorityCode], [SourceCode], [NurturingStageCode], [StatusCode], [CreatedDateUtc], [CreatedByUserId])
    VALUES
        -- HIGH PRIORITY (Score 80+)
        (NEWID(), @DefaultTenantId, 'LEAD-001', 'Sarah', 'Anderson', 'sarah@techinnovations.com', '555-3001', 'Tech Innovations Inc', 'General Liability', 85, 'High', 'Web', 'Active', 1, DATEADD(DAY, -2, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-002', 'Jennifer', 'Martinez', 'jennifer@premiergroup.com', '555-3002', 'Premier Group Holdings', 'Property', 91, 'High', 'Direct', 'Active', 1, DATEADD(DAY, -1, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-003', 'Robert', 'Jackson', 'robert@innovationhub.com', '555-3003', 'Innovation Hub Co', 'Commercial Auto', 88, 'High', 'Organic', 'Active', 1, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-004', 'Rachel', 'Santos', 'rachel@nextgen.com', '555-3004', 'NextGen Holdings', 'Workers Comp', 89, 'High', 'Organic', 'Active', 1, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-005', 'Michelle', 'Brown', 'michelle@alliancecap.com', '555-3005', 'Alliance Capital', 'Umbrella Liability', 84, 'High', 'Partner', 'Active', 1, DATEADD(DAY, -1, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-013', 'James', 'Mitchell', 'james@future-ent.com', '555-3013', 'Future Enterprises', 'Property', 81, 'High', 'Referral', 'Active', 1, DATEADD(HOUR, -3, GETUTCDATE()), @SystemUserId),

        -- MEDIUM PRIORITY (Score 50-79)
        (NEWID(), @DefaultTenantId, 'LEAD-006', 'Michael', 'Chen', 'michael@globalsolutions.com', '555-3006', 'Global Solutions Ltd', 'General Liability', 72, 'Medium', 'Referral', 'Active', 1, DATEADD(DAY, -1, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-007', 'David', 'Thompson', 'david@enterprises.com', '555-3007', 'Enterprise Systems Corp', 'Property', 65, 'Medium', 'Partner', 'Active', 1, DATEADD(DAY, -3, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-008', 'Emily', 'Watson', 'emily@digitalventures.com', '555-3008', 'Digital Ventures LLC', 'Cyber Liability', 78, 'Medium', 'Web', 'Active', 1, DATEADD(DAY, -1, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-009', 'Lisa', 'Graham', 'lisa@strategic.com', '555-3009', 'Strategic Partners Inc', 'Errors & Omissions', 73, 'Medium', 'Direct', 'Active', 1, DATEADD(DAY, -2, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-010', 'Christopher', 'Davis', 'chris@summit.com', '555-3010', 'Summit Industries', 'Commercial Auto', 76, 'Medium', 'Web', 'Active', 1, DATEADD(DAY, -1, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-011', 'Marcus', 'Taylor', 'marcus@zenith.com', '555-3011', 'Zenith Corp', 'Workers Comp', 75, 'Medium', 'Referral', 'Active', 1, DATEADD(DAY, -3, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-017', 'Charles', 'Williams', 'charles@venture.com', '555-3017', 'Venture Capital Group', 'Directors & Officers', 71, 'Medium', 'Direct', 'Active', 1, DATEADD(DAY, -4, GETUTCDATE()), @SystemUserId),

        -- LOW PRIORITY (Score <50)
        (NEWID(), @DefaultTenantId, 'LEAD-012', 'Amanda', 'Price', 'amanda@catalyst.com', '555-3012', 'Catalyst Group', 'General Liability', 62, 'Low', 'Web', 'Nurture', 1, DATEADD(DAY, -4, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-014', 'Kevin', 'Wilson', 'kevin@proworks.com', '555-3014', 'ProWorks Solutions', 'Commercial Auto', 68, 'Low', 'Web', 'Nurture', 1, DATEADD(DAY, -5, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-015', 'Victoria', 'Kim', 'victoria@horizon.com', '555-3015', 'Horizon Ventures', 'General Liability', 64, 'Medium', 'Web', 'Nurture', 1, DATEADD(DAY, -6, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-016', 'Patricia', 'Johnson', 'patricia@innovate.com', '555-3016', 'Innovate Systems', 'Cyber Liability', 55, 'Low', 'Referral', 'Nurture', 1, DATEADD(DAY, -7, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-018', 'Diana', 'Moore', 'diana@strategic.com', '555-3018', 'Strategic Consulting', 'Professional Liability', 48, 'Low', 'Web', 'Nurture', 1, DATEADD(DAY, -5, GETUTCDATE()), @SystemUserId);

    PRINT '✓ 18 leads inserted';
END
ELSE
    PRINT '• Leads already exist';

PRINT '';

-- ============================================================================
-- 4. INSERT LEAD SCORING RULES (for Lead Scoring page)
-- ============================================================================
PRINT '--- 4. Lead Scoring Rules ---';

IF NOT EXISTS (SELECT 1 FROM [dbo].[LeadScoringRules] WHERE [TenantId] = @DefaultTenantId AND [RuleName] = 'Email Opens')
BEGIN
    INSERT INTO [dbo].[LeadScoringRules]
        ([Id], [TenantId], [RuleName], [RuleType], [Points], [Description], [Condition], [IsActive], [DisplayOrder], [CreatedDateUtc], [CreatedByUserId])
    VALUES
        (NEWID(), @DefaultTenantId, 'Demo Request', 'Engagement', 20, 'Requested product demo', 'Demo request submitted', 1, 1, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Form Submission', 'Engagement', 15, 'Completed contact form or webinar signup', 'Form submitted in last 7 days', 1, 2, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Industry Match', 'Profile', 10, 'Company in target industry', 'Industry matches target list', 1, 3, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Website Visit', 'Behavior', 10, 'Visited pricing page or product pages', 'Visited site in last 14 days', 1, 4, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Company Size', 'Profile', 8, 'Enterprise company (1000+ employees)', '1000+ employee count', 1, 5, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Page Downloads', 'Behavior', 7, 'Downloaded whitepapers or case studies', 'Download in last 30 days', 1, 6, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Email Opens', 'Engagement', 5, 'Lead opened marketing email', 'Email opened in last 30 days', 1, 7, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Recent Activity', 'Recency', 5, 'Active in last 7 days', 'Any activity last 7 days', 1, 8, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LinkedIn Connection', 'Engagement', 3, 'Connected on LinkedIn', 'LinkedIn connection active', 1, 9, GETUTCDATE(), @SystemUserId);

    PRINT '✓ 9 scoring rules inserted';
END
ELSE
    PRINT '• Scoring rules already exist';

PRINT '';

-- ============================================================================
-- 5. INSERT LEAD ASSIGNMENT RULES (for Lead Assignment page)
-- ============================================================================
PRINT '--- 5. Lead Assignment Rules ---';

IF NOT EXISTS (SELECT 1 FROM [dbo].[LeadAssignmentRules] WHERE [TenantId] = @DefaultTenantId AND [RuleName] = 'High-Score Auto Assign')
BEGIN
    INSERT INTO [dbo].[LeadAssignmentRules]
        ([Id], [TenantId], [RuleName], [RuleType], [Criteria], [TargetGroup], [MaxAssignments], [IsActive], [DisplayOrder], [CreatedDateUtc], [CreatedByUserId])
    VALUES
        (NEWID(), @DefaultTenantId, 'High-Score Auto Assign', 'Score-Based', 'Score >= 80', 'Senior Producers', 5, 1, 1, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Round-Robin Distribution', 'Round-Robin', 'All Leads', 'All Producers', 0, 1, 2, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Medium Priority Assignment', 'Score-Based', 'Score 50-79', 'All Producers', 0, 1, 3, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Nurture Queue Assignment', 'Score-Based', 'Score < 50', 'Nurture Team', 0, 1, 4, GETUTCDATE(), @SystemUserId);

    PRINT '✓ 4 assignment rules inserted';
END
ELSE
    PRINT '• Assignment rules already exist';

PRINT '';

-- ============================================================================
-- 6. INSERT LEAD ACTIVITIES (for Lead Follow-up page)
-- ============================================================================
PRINT '--- 6. Lead Activities (Follow-ups) ---';

IF NOT EXISTS (SELECT 1 FROM [dbo].[LeadActivities] WHERE [TenantId] = @DefaultTenantId)
BEGIN
    DECLARE @FirstUserId UNIQUEIDENTIFIER;
    SELECT @FirstUserId = MIN([Id]) FROM [dbo].[Users] WHERE [TenantId] = @DefaultTenantId;

    IF @FirstUserId IS NOT NULL
    BEGIN
        -- High priority call follow-ups for high-score leads
        INSERT INTO [dbo].[LeadActivities]
            ([Id], [TenantId], [LeadId], [ActivityType], [ContactMethod], [Subject], [Description], [ScheduledDateUtc], [Priority], [StatusCode], [AssignedToUserId], [CreatedDateUtc], [CreatedByUserId])
        SELECT TOP 6
            NEWID(), 
            @DefaultTenantId, 
            [Id], 
            'Phone Call', 
            'Phone', 
            'Initial Qualification Call', 
            'Call to qualify lead and understand insurance needs', 
            DATEADD(DAY, 1, GETUTCDATE()), 
            'High', 
            'Pending', 
            @FirstUserId, 
            GETUTCDATE(), 
            @SystemUserId
        FROM [dbo].[Leads]
        WHERE [TenantId] = @DefaultTenantId AND [Score] >= 85
        ORDER BY [Score] DESC;

        -- Medium priority email follow-ups for medium-score leads
        INSERT INTO [dbo].[LeadActivities]
            ([Id], [TenantId], [LeadId], [ActivityType], [ContactMethod], [Subject], [Description], [ScheduledDateUtc], [Priority], [StatusCode], [AssignedToUserId], [CreatedDateUtc], [CreatedByUserId])
        SELECT TOP 8
            NEWID(), 
            @DefaultTenantId, 
            [Id], 
            'Email', 
            'Email', 
            'Send Product Information', 
            'Send customized product overview and pricing details', 
            DATEADD(DAY, 2, GETUTCDATE()), 
            'Medium', 
            'Pending', 
            @FirstUserId, 
            GETUTCDATE(), 
            @SystemUserId
        FROM [dbo].[Leads]
        WHERE [TenantId] = @DefaultTenantId AND [Score] BETWEEN 65 AND 79
        ORDER BY [Score] DESC;

        -- Low priority nurture follow-ups for low-score leads
        INSERT INTO [dbo].[LeadActivities]
            ([Id], [TenantId], [LeadId], [ActivityType], [ContactMethod], [Subject], [Description], [ScheduledDateUtc], [Priority], [StatusCode], [AssignedToUserId], [CreatedDateUtc], [CreatedByUserId])
        SELECT TOP 5
            NEWID(), 
            @DefaultTenantId, 
            [Id], 
            'Email', 
            'Email', 
            'Nurture Campaign - Educational Content', 
            'Send educational content to nurture lead', 
            DATEADD(DAY, 3, GETUTCDATE()), 
            'Low', 
            'Pending', 
            @FirstUserId, 
            GETUTCDATE(), 
            @SystemUserId
        FROM [dbo].[Leads]
        WHERE [TenantId] = @DefaultTenantId AND [Score] < 65 AND [NurturingStageCode] = 'Nurture'
        ORDER BY [CreatedDateUtc] DESC;

        PRINT '✓ 19 lead follow-up activities inserted';
    END
    ELSE
    BEGIN
        PRINT '⚠ No users found - skipping lead activities';
    END
END
ELSE
    PRINT '• Lead activities already exist';

PRINT '';

-- ============================================================================
-- 7. FINAL SUMMARY
-- ============================================================================
PRINT '========================================';
PRINT 'Seed Data Completed!';
PRINT '========================================';
PRINT '';
PRINT 'Database Summary:';
PRINT '  • Tenants: ' + CAST((SELECT COUNT(*) FROM [dbo].[Tenants]) AS VARCHAR);
PRINT '  • Users (Producers): ' + CAST((SELECT COUNT(*) FROM [dbo].[Users] WHERE [TenantId] = @DefaultTenantId) AS VARCHAR);
PRINT '  • Leads: ' + CAST((SELECT COUNT(*) FROM [dbo].[Leads] WHERE [TenantId] = @DefaultTenantId) AS VARCHAR);
PRINT '  • Scoring Rules: ' + CAST((SELECT COUNT(*) FROM [dbo].[LeadScoringRules] WHERE [TenantId] = @DefaultTenantId) AS VARCHAR);
PRINT '  • Assignment Rules: ' + CAST((SELECT COUNT(*) FROM [dbo].[LeadAssignmentRules] WHERE [TenantId] = @DefaultTenantId) AS VARCHAR);
PRINT '  • Follow-up Activities: ' + CAST((SELECT COUNT(*) FROM [dbo].[LeadActivities] WHERE [TenantId] = @DefaultTenantId) AS VARCHAR);
PRINT '';
PRINT 'Ready for:';
PRINT '  ✓ Lead Scoring Page';
PRINT '  ✓ Lead Assignment Page';
PRINT '  ✓ Lead Follow-up Page';
PRINT '========================================';

SET NOCOUNT OFF;
