-- ============================================================================
-- AMS Database Seed Data - CRM Tables
-- Comprehensive test data for Lead Scoring, Assignment, and Follow-up
-- ============================================================================

-- ============================================================================
-- SECTION 1: INSERT TENANTS
-- ============================================================================
PRINT 'Inserting Tenant data...';

DECLARE @DefaultTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000099';

IF NOT EXISTS (SELECT 1 FROM [dbo].[Tenants] WHERE [Id] = @DefaultTenantId)
BEGIN
    INSERT INTO [dbo].[Tenants] 
        ([Id], [TenantCode], [TenantName], [StatusCode], [CreatedDateUtc])
    VALUES 
        (@DefaultTenantId, 'DEFAULT', 'Default Tenant', 'Active', GETUTCDATE()),
        (NEWID(), 'ACME', 'ACME Insurance Corp', 'Active', GETUTCDATE()),
        (NEWID(), 'GLOBAL', 'Global Brokerage Ltd', 'Active', GETUTCDATE());

    PRINT '✓ Tenants inserted';
END
ELSE
BEGIN
    PRINT '• Tenants already exist';
END;

-- ============================================================================
-- SECTION 2: INSERT USERS (Producers/Team Members)
-- ============================================================================
PRINT 'Inserting User data...';

DECLARE @UserId1 UNIQUEIDENTIFIER = NEWID();
DECLARE @UserId2 UNIQUEIDENTIFIER = NEWID();
DECLARE @UserId3 UNIQUEIDENTIFIER = NEWID();
DECLARE @UserId4 UNIQUEIDENTIFIER = NEWID();
DECLARE @UserId5 UNIQUEIDENTIFIER = NEWID();

-- Save IDs for later use
SELECT @UserId1 = Id FROM [dbo].[Users] WHERE [TenantId] = @DefaultTenantId AND [UserName] = 'jspencer' IF @@ROWCOUNT = 0 SET @UserId1 = NEWID();
SELECT @UserId2 = Id FROM [dbo].[Users] WHERE [TenantId] = @DefaultTenantId AND [UserName] = 'ahayes' IF @@ROWCOUNT = 0 SET @UserId2 = NEWID();

IF NOT EXISTS (SELECT 1 FROM [dbo].[Users] WHERE [TenantId] = @DefaultTenantId AND [UserName] = 'jspencer')
BEGIN
    INSERT INTO [dbo].[Users] 
        ([Id], [TenantId], [UserName], [Email], [FullName], [DisplayName], [JobTitle], [Department], [UserTypeCode], [StatusCode], [PhoneNumber], [TimeZoneCode], [CreatedDateUtc], [CreatedByUserId])
    VALUES 
        (@UserId1, @DefaultTenantId, 'jspencer', 'john.spencer@agencybinder.com', 'John Spencer', 'John S.', 'Senior Producer', 'Sales', 'Internal', 'Active', '555-0101', 'America/Chicago', GETUTCDATE(), @SystemUserId),
        (@UserId2, @DefaultTenantId, 'ahayes', 'amanda.hayes@agencybinder.com', 'Amanda Hayes', 'Amanda H.', 'Producer', 'Sales', 'Internal', 'Active', '555-0102', 'America/Chicago', GETUTCDATE(), @SystemUserId),
        (@UserId3, @DefaultTenantId, 'rmitchell', 'ryan.mitchell@agencybinder.com', 'Ryan Mitchell', 'Ryan M.', 'Account Executive', 'Sales', 'Internal', 'Active', '555-0103', 'America/New_York', GETUTCDATE(), @SystemUserId),
        (@UserId4, @DefaultTenantId, 'jbrown', 'jessica.brown@agencybinder.com', 'Jessica Brown', 'Jessica B.', 'Producer', 'Sales', 'Internal', 'Active', '555-0104', 'America/Los_Angeles', GETUTCDATE(), @SystemUserId),
        (@UserId5, @DefaultTenantId, 'tanderson', 'thomas.anderson@agencybinder.com', 'Thomas Anderson', 'Tom A.', 'Senior Producer', 'Sales', 'Internal', 'Active', '555-0105', 'America/Chicago', GETUTCDATE(), @SystemUserId);

    PRINT '✓ Users inserted';
END
ELSE
BEGIN
    PRINT '• Users already exist';
END;

-- ============================================================================
-- SECTION 3: INSERT ACCOUNTS
-- ============================================================================
PRINT 'Inserting Account data...';

IF NOT EXISTS (SELECT 1 FROM [dbo].[Accounts] WHERE [TenantId] = @DefaultTenantId AND [AccountNumber] = 'ACC-001')
BEGIN
    INSERT INTO [dbo].[Accounts] 
        ([Id], [TenantId], [AccountNumber], [AccountName], [AccountTypeCode], [MainEmail], [MainPhone], [CreatedDateUtc], [CreatedByUserId])
    VALUES 
        (NEWID(), @DefaultTenantId, 'ACC-001', 'Tech Innovations Inc', 'Corporate', 'contact@techinnovations.com', '555-2001', GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'ACC-002', 'Global Solutions Ltd', 'Corporate', 'info@globalsolutions.com', '555-2002', GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'ACC-003', 'Premier Group Holdings', 'Enterprise', 'admin@premiergroup.com', '555-2003', GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'ACC-004', 'Enterprise Systems Corp', 'Corporate', 'support@enterprises.com', '555-2004', GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'ACC-005', 'Digital Ventures LLC', 'Startup', 'hello@digitalventures.com', '555-2005', GETUTCDATE(), @SystemUserId);

    PRINT '✓ Accounts inserted';
END
ELSE
BEGIN
    PRINT '• Accounts already exist';
END;

-- ============================================================================
-- SECTION 4: INSERT LEADS
-- ============================================================================
PRINT 'Inserting Lead data...';

IF NOT EXISTS (SELECT 1 FROM [dbo].[Leads] WHERE [TenantId] = @DefaultTenantId AND [LeadNumber] = 'LEAD-001')
BEGIN
    INSERT INTO [dbo].[Leads]
        ([Id], [TenantId], [LeadNumber], [FirstName], [LastName], [Email], [Phone], [AccountName], [InterestedService], [Score], [PriorityCode], [SourceCode], [NurturingStageCode], [StatusCode], [AssignedToUserId], [QualifiedDate], [CreatedDateUtc], [CreatedByUserId])
    VALUES
        -- High-score leads (80+)
        (NEWID(), @DefaultTenantId, 'LEAD-001', 'Sarah', 'Anderson', 'sarah@techinnovations.com', '555-3001', 'Tech Innovations Inc', 'General Liability', 85, 'High', 'Web', 'Active', 1, NULL, NULL, DATEADD(DAY, -2, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-002', 'Jennifer', 'Martinez', 'jennifer@premiergroup.com', '555-3002', 'Premier Group Holdings', 'Property', 91, 'High', 'Direct', 'Active', 1, NULL, NULL, DATEADD(DAY, -1, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-003', 'Robert', 'Jackson', 'robert@innovationhub.com', '555-3003', 'Innovation Hub Co', 'Commercial Auto', 88, 'High', 'Organic', 'Active', 1, NULL, NULL, DATEADD(HOUR, -12, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-004', 'Rachel', 'Santos', 'rachel@nextgen.com', '555-3004', 'NextGen Holdings', 'Workers Comp', 89, 'High', 'Organic', 'Active', 1, NULL, NULL, DATEADD(HOUR, -2, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-005', 'Michelle', 'Brown', 'michelle@alliancecap.com', '555-3005', 'Alliance Capital', 'Umbrella Liability', 84, 'High', 'Partner', 'Active', 1, NULL, NULL, DATEADD(HOUR, -18, GETUTCDATE()), @SystemUserId),

        -- Medium-score leads (50-79)
        (NEWID(), @DefaultTenantId, 'LEAD-006', 'Michael', 'Chen', 'michael@globalsolutions.com', '555-3006', 'Global Solutions Ltd', 'General Liability', 72, 'Medium', 'Referral', 'Active', 1, NULL, NULL, DATEADD(DAY, -1, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-007', 'David', 'Thompson', 'david@enterprises.com', '555-3007', 'Enterprise Systems Corp', 'Property', 65, 'Medium', 'Partner', 'Active', 1, NULL, NULL, DATEADD(DAY, -3, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-008', 'Emily', 'Watson', 'emily@digitalventures.com', '555-3008', 'Digital Ventures LLC', 'Cyber Liability', 78, 'Medium', 'Web', 'Active', 1, NULL, NULL, DATEADD(DAY, -1, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-009', 'Lisa', 'Graham', 'lisa@strategic.com', '555-3009', 'Strategic Partners Inc', 'Errors & Omissions', 73, 'Medium', 'Direct', 'Active', 1, NULL, NULL, DATEADD(DAY, -2, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-010', 'Christopher', 'Davis', 'chris@summit.com', '555-3010', 'Summit Industries', 'Commercial Auto', 76, 'Medium', 'Web', 'Active', 1, NULL, NULL, DATEADD(DAY, -1, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-011', 'Marcus', 'Taylor', 'marcus@zenith.com', '555-3011', 'Zenith Corp', 'Workers Comp', 75, 'Medium', 'Referral', 'Active', 1, NULL, NULL, DATEADD(DAY, -3, GETUTCDATE()), @SystemUserId),

        -- Low-score leads (<50)
        (NEWID(), @DefaultTenantId, 'LEAD-012', 'Amanda', 'Price', 'amanda@catalyst.com', '555-3012', 'Catalyst Group', 'General Liability', 62, 'Low', 'Web', 'Nurture', 1, NULL, NULL, DATEADD(DAY, -4, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-013', 'James', 'Mitchell', 'james@future-ent.com', '555-3013', 'Future Enterprises', 'Property', 81, 'High', 'Referral', 'Active', 1, NULL, NULL, DATEADD(HOUR, -3, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-014', 'Kevin', 'Wilson', 'kevin@proworks.com', '555-3014', 'ProWorks Solutions', 'Commercial Auto', 68, 'Low', 'Web', 'Nurture', 1, NULL, NULL, DATEADD(DAY, -5, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-015', 'Victoria', 'Kim', 'victoria@horizon.com', '555-3015', 'Horizon Ventures', 'General Liability', 64, 'Medium', 'Web', 'Nurture', 1, NULL, NULL, DATEADD(DAY, -6, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-016', 'Patricia', 'Johnson', 'patricia@innovate.com', '555-3016', 'Innovate Systems', 'Cyber Liability', 55, 'Low', 'Referral', 'Nurture', 1, NULL, NULL, DATEADD(DAY, -7, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-017', 'Charles', 'Williams', 'charles@venture.com', '555-3017', 'Venture Capital Group', 'Directors & Officers', 71, 'Medium', 'Direct', 'Active', 1, NULL, NULL, DATEADD(DAY, -4, GETUTCDATE()), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LEAD-018', 'Diana', 'Moore', 'diana@strategic.com', '555-3018', 'Strategic Consulting', 'Professional Liability', 48, 'Low', 'Web', 'Nurture', 1, NULL, NULL, DATEADD(DAY, -5, GETUTCDATE()), @SystemUserId);

    PRINT '✓ Leads inserted (18 leads)';
END
ELSE
BEGIN
    PRINT '• Leads already exist';
END;

-- ============================================================================
-- SECTION 5: INSERT LEAD SCORING RULES
-- ============================================================================
PRINT 'Inserting Lead Scoring Rules...';

IF NOT EXISTS (SELECT 1 FROM [dbo].[LeadScoringRules] WHERE [TenantId] = @DefaultTenantId AND [RuleName] = 'Email Opens')
BEGIN
    INSERT INTO [dbo].[LeadScoringRules]
        ([Id], [TenantId], [RuleName], [RuleType], [Points], [Description], [Condition], [IsActive], [DisplayOrder], [CreatedDateUtc], [CreatedByUserId])
    VALUES
        (NEWID(), @DefaultTenantId, 'Email Opens', 'Engagement', 5, 'Lead opened marketing email', 'Email opened in last 30 days', 1, 1, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Website Visit', 'Behavior', 10, 'Visited pricing page or product pages', 'Visited site in last 14 days', 1, 2, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Form Submission', 'Engagement', 15, 'Completed contact form or webinar signup', 'Form submitted in last 7 days', 1, 3, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Company Size', 'Profile', 8, 'Enterprise company (1000+ employees)', '1000+ employee count', 1, 4, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Industry Match', 'Profile', 10, 'Company in target industry', 'Industry matches target list', 1, 5, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Recent Activity', 'Recency', 5, 'Active in last 7 days', 'Any activity last 7 days', 1, 6, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'LinkedIn Connection', 'Engagement', 3, 'Connected on LinkedIn', 'LinkedIn connection active', 1, 7, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Page Downloads', 'Behavior', 7, 'Downloaded whitepapers or case studies', 'Download in last 30 days', 1, 8, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Demo Request', 'Engagement', 20, 'Requested product demo', 'Demo request submitted', 1, 9, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Positive Sentiment', 'Profile', 6, 'Previous positive interaction', 'Good engagement history', 0, 10, GETUTCDATE(), @SystemUserId);

    PRINT '✓ Lead Scoring Rules inserted (10 rules)';
END
ELSE
BEGIN
    PRINT '• Lead Scoring Rules already exist';
END;

-- ============================================================================
-- SECTION 6: INSERT LEAD ASSIGNMENT RULES
-- ============================================================================
PRINT 'Inserting Lead Assignment Rules...';

IF NOT EXISTS (SELECT 1 FROM [dbo].[LeadAssignmentRules] WHERE [TenantId] = @DefaultTenantId AND [RuleName] = 'High-Score Auto Assign')
BEGIN
    INSERT INTO [dbo].[LeadAssignmentRules]
        ([Id], [TenantId], [RuleName], [RuleType], [Criteria], [TargetGroup], [MaxAssignments], [IsActive], [DisplayOrder], [CreatedDateUtc], [CreatedByUserId])
    VALUES
        (NEWID(), @DefaultTenantId, 'High-Score Auto Assign', 'Score-Based', 'Score >= 80', 'Senior Producers', 5, 1, 1, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Round-Robin Distribution', 'Round-Robin', 'All Leads', 'All Producers', 0, 1, 2, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Medium Priority Assignment', 'Score-Based', 'Score 50-79', 'All Producers', 0, 1, 3, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Nurture Queue Assignment', 'Score-Based', 'Score < 50', 'Nurture Team', 0, 1, 4, GETUTCDATE(), @SystemUserId),
        (NEWID(), @DefaultTenantId, 'Territory Routing', 'Territory-Based', 'Match Territory', 'Assigned Territory', 0, 0, 5, GETUTCDATE(), @SystemUserId);

    PRINT '✓ Lead Assignment Rules inserted (5 rules)';
END
ELSE
BEGIN
    PRINT '• Lead Assignment Rules already exist';
END;

-- ============================================================================
-- SECTION 7: INSERT LEAD ACTIVITIES (Follow-ups)
-- ============================================================================
PRINT 'Inserting Lead Activities (Follow-ups)...';

DECLARE @LeadCount INT;
SELECT @LeadCount = COUNT(*) FROM [dbo].[Leads] WHERE [TenantId] = @DefaultTenantId;

IF @LeadCount > 0 AND NOT EXISTS (SELECT 1 FROM [dbo].[LeadActivities] WHERE [TenantId] = @DefaultTenantId)
BEGIN
    INSERT INTO [dbo].[LeadActivities]
        ([Id], [TenantId], [LeadId], [ActivityType], [ContactMethod], [Subject], [Description], [ScheduledDateUtc], [Priority], [StatusCode], [AssignedToUserId], [CreatedDateUtc], [CreatedByUserId])
    SELECT TOP 10
        NEWID(), @DefaultTenantId, [Id], 'Phone Call', 'Phone', 'Initial Qualification Call', 'Call to qualify lead and understand needs', DATEADD(DAY, 1, GETUTCDATE()), 'High', 'Pending', @UserId1, GETUTCDATE(), @SystemUserId
    FROM [dbo].[Leads]
    WHERE [TenantId] = @DefaultTenantId AND [Score] >= 80
    ORDER BY [Score] DESC;

    INSERT INTO [dbo].[LeadActivities]
        ([Id], [TenantId], [LeadId], [ActivityType], [ContactMethod], [Subject], [Description], [ScheduledDateUtc], [Priority], [StatusCode], [AssignedToUserId], [CreatedDateUtc], [CreatedByUserId])
    SELECT TOP 5
        NEWID(), @DefaultTenantId, [Id], 'Email', 'Email', 'Send Product Information', 'Send customized product overview and pricing', DATEADD(DAY, 2, GETUTCDATE()), 'Medium', 'Pending', @UserId2, GETUTCDATE(), @SystemUserId
    FROM [dbo].[Leads]
    WHERE [TenantId] = @DefaultTenantId AND [Score] BETWEEN 50 AND 79
    ORDER BY [Score] DESC;

    PRINT '✓ Lead Activities inserted';
END
ELSE IF @LeadCount = 0
BEGIN
    PRINT '⚠ No leads found - skipping Lead Activities';
END
ELSE
BEGIN
    PRINT '• Lead Activities already exist';
END;

-- ============================================================================
-- SECTION 8: INSERT LEAD QUALITY METRICS
-- ============================================================================
PRINT 'Inserting Lead Quality Metrics...';

IF NOT EXISTS (SELECT 1 FROM [dbo].[LeadQualityMetrics] WHERE [TenantId] = @DefaultTenantId)
BEGIN
    INSERT INTO [dbo].[LeadQualityMetrics]
        ([Id], [TenantId], [LeadId], [CompletedActivities], [TotalActivities], [TimeToFirstContact], [ResponseRate], [ConversionProbability], [LastScoringDateUtc], [LastActivityDateUtc], [ModifiedDateUtc])
    SELECT TOP 15
        NEWID(), @DefaultTenantId, [Id], 
        ABS(CHECKSUM(NewId())) % 5,
        ABS(CHECKSUM(NewId())) % 10 + 1,
        ABS(CHECKSUM(NewId())) % 48,
        CONVERT(DECIMAL(5,2), (ABS(CHECKSUM(NewId())) % 100) / 100.0),
        CONVERT(DECIMAL(5,2), (ABS(CHECKSUM(NewId())) % 100) / 100.0),
        GETUTCDATE(),
        DATEADD(HOUR, -ABS(CHECKSUM(NewId())) % 72, GETUTCDATE()),
        GETUTCDATE()
    FROM [dbo].[Leads]
    WHERE [TenantId] = @DefaultTenantId
    ORDER BY [Score] DESC;

    PRINT '✓ Lead Quality Metrics inserted';
END
ELSE
BEGIN
    PRINT '• Lead Quality Metrics already exist';
END;

PRINT '';
PRINT '========================================';
PRINT 'Seed Data Insertion Complete!';
PRINT '========================================';
PRINT 'Summary:';
PRINT '  • Tenants: 3';
PRINT '  • Users (Producers): 5';
PRINT '  • Accounts: 5';
PRINT '  • Leads: 18';
PRINT '  • Scoring Rules: 10';
PRINT '  • Assignment Rules: 5';
PRINT '  • Follow-up Activities: 15';
PRINT '  • Quality Metrics: 15';
PRINT '========================================';
