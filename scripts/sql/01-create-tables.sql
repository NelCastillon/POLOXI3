-- ============================================================================
-- AMS Database Schema - Core CRM Tables
-- Created for Lead Scoring, Lead Assignment, and Lead Follow-up functionality
-- ============================================================================

-- ============================================================================
-- 1. LEADS TABLE
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Leads')
BEGIN
    CREATE TABLE [dbo].[Leads] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [LeadNumber] VARCHAR(50) NOT NULL,
        [FirstName] NVARCHAR(100) NOT NULL,
        [LastName] NVARCHAR(100) NOT NULL,
        [Email] NVARCHAR(255),
        [Phone] NVARCHAR(20),
        [AccountName] NVARCHAR(255),
        [InterestedService] NVARCHAR(255),
        [Score] INT,
        [PriorityCode] VARCHAR(50),
        [SourceCode] VARCHAR(50),
        [NurturingStageCode] VARCHAR(50),
        [StatusCode] INT DEFAULT 1,
        [AssignedToUserId] UNIQUEIDENTIFIER,
        [QualifiedDate] DATETIME2,
        [CreatedDateUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedByUserId] UNIQUEIDENTIFIER,
        [ModifiedDateUtc] DATETIME2,
        [ModifiedByUserId] UNIQUEIDENTIFIER,
        [IsDeleted] BIT DEFAULT 0,

        -- Indexes
        INDEX [IX_Leads_TenantId] NONCLUSTERED ([TenantId]),
        INDEX [IX_Leads_LeadNumber] NONCLUSTERED ([LeadNumber]),
        INDEX [IX_Leads_Email] NONCLUSTERED ([Email]),
        INDEX [IX_Leads_Score] NONCLUSTERED ([Score]),
        INDEX [IX_Leads_SourceCode] NONCLUSTERED ([SourceCode]),
        INDEX [IX_Leads_AssignedToUserId] NONCLUSTERED ([AssignedToUserId]),
        INDEX [IX_Leads_StatusCode] NONCLUSTERED ([StatusCode]),
        INDEX [IX_Leads_CreatedDateUtc] NONCLUSTERED ([CreatedDateUtc] DESC)
    );

    CREATE UNIQUE INDEX [UX_Leads_TenantId_LeadNumber] 
        ON [dbo].[Leads]([TenantId], [LeadNumber]);

    PRINT 'Table [Leads] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [Leads] already exists.';
END;

-- ============================================================================
-- 2. USERS TABLE (Producers/Team Members)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Users')
BEGIN
    CREATE TABLE [dbo].[Users] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [UserName] NVARCHAR(100) NOT NULL,
        [Email] NVARCHAR(255) NOT NULL,
        [FullName] NVARCHAR(255) NOT NULL,
        [DisplayName] NVARCHAR(255),
        [JobTitle] NVARCHAR(100),
        [Department] NVARCHAR(100),
        [UserTypeCode] VARCHAR(50) DEFAULT 'Internal',
        [StatusCode] VARCHAR(50) DEFAULT 'Active',
        [PhoneNumber] NVARCHAR(20),
        [TimeZoneCode] VARCHAR(50),
        [LocaleCode] VARCHAR(10),
        [BranchId] UNIQUEIDENTIFIER,
        [MfaEnabled] BIT DEFAULT 0,
        [IsLockedOut] BIT DEFAULT 0,
        [LockoutEndDateUtc] DATETIME2,
        [FailedLoginAttempts] INT DEFAULT 0,
        [LastLoginDateUtc] DATETIME2,
        [PasswordChangedDateUtc] DATETIME2,
        [CreatedDateUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedByUserId] UNIQUEIDENTIFIER,
        [ModifiedDateUtc] DATETIME2,
        [ModifiedByUserId] UNIQUEIDENTIFIER,
        [IsDeleted] BIT DEFAULT 0,

        -- Indexes
        INDEX [IX_Users_TenantId] NONCLUSTERED ([TenantId]),
        INDEX [IX_Users_Email] NONCLUSTERED ([Email]),
        INDEX [IX_Users_StatusCode] NONCLUSTERED ([StatusCode]),
        INDEX [IX_Users_UserTypeCode] NONCLUSTERED ([UserTypeCode])
    );

    CREATE UNIQUE INDEX [UX_Users_TenantId_UserName] 
        ON [dbo].[Users]([TenantId], [UserName]);

    PRINT 'Table [Users] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [Users] already exists.';
END;

-- ============================================================================
-- 3. LEAD_SCORING_RULES TABLE
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LeadScoringRules')
BEGIN
    CREATE TABLE [dbo].[LeadScoringRules] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [RuleName] NVARCHAR(255) NOT NULL,
        [RuleType] VARCHAR(50) NOT NULL,
        [Points] INT NOT NULL DEFAULT 0,
        [Description] NVARCHAR(MAX),
        [Condition] NVARCHAR(MAX),
        [IsActive] BIT DEFAULT 1,
        [DisplayOrder] INT DEFAULT 0,
        [CreatedDateUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedByUserId] UNIQUEIDENTIFIER,
        [ModifiedDateUtc] DATETIME2,
        [ModifiedByUserId] UNIQUEIDENTIFIER,
        [IsDeleted] BIT DEFAULT 0,

        -- Indexes
        INDEX [IX_LeadScoringRules_TenantId] NONCLUSTERED ([TenantId]),
        INDEX [IX_LeadScoringRules_RuleType] NONCLUSTERED ([RuleType]),
        INDEX [IX_LeadScoringRules_IsActive] NONCLUSTERED ([IsActive])
    );

    PRINT 'Table [LeadScoringRules] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [LeadScoringRules] already exists.';
END;

-- ============================================================================
-- 4. LEAD_ACTIVITIES TABLE (Follow-ups)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LeadActivities')
BEGIN
    CREATE TABLE [dbo].[LeadActivities] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [LeadId] UNIQUEIDENTIFIER NOT NULL,
        [ActivityType] VARCHAR(50) NOT NULL,
        [ContactMethod] VARCHAR(50),
        [Subject] NVARCHAR(255),
        [Description] NVARCHAR(MAX),
        [ScheduledDateUtc] DATETIME2,
        [CompletedDateUtc] DATETIME2,
        [Priority] VARCHAR(50) DEFAULT 'Medium',
        [StatusCode] VARCHAR(50) DEFAULT 'Pending',
        [AssignedToUserId] UNIQUEIDENTIFIER,
        [CreatedDateUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedByUserId] UNIQUEIDENTIFIER,
        [ModifiedDateUtc] DATETIME2,
        [ModifiedByUserId] UNIQUEIDENTIFIER,
        [IsDeleted] BIT DEFAULT 0,

        -- Indexes
        INDEX [IX_LeadActivities_TenantId] NONCLUSTERED ([TenantId]),
        INDEX [IX_LeadActivities_LeadId] NONCLUSTERED ([LeadId]),
        INDEX [IX_LeadActivities_ActivityType] NONCLUSTERED ([ActivityType]),
        INDEX [IX_LeadActivities_StatusCode] NONCLUSTERED ([StatusCode]),
        INDEX [IX_LeadActivities_ScheduledDateUtc] NONCLUSTERED ([ScheduledDateUtc]),
        INDEX [IX_LeadActivities_AssignedToUserId] NONCLUSTERED ([AssignedToUserId]),
        INDEX [IX_LeadActivities_Priority] NONCLUSTERED ([Priority])
    );

    -- Foreign Key
    ALTER TABLE [dbo].[LeadActivities]
    ADD CONSTRAINT [FK_LeadActivities_Leads]
        FOREIGN KEY ([LeadId]) REFERENCES [dbo].[Leads]([Id]);

    PRINT 'Table [LeadActivities] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [LeadActivities] already exists.';
END;

-- ============================================================================
-- 5. LEAD_ASSIGNMENT_RULES TABLE
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LeadAssignmentRules')
BEGIN
    CREATE TABLE [dbo].[LeadAssignmentRules] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [RuleName] NVARCHAR(255) NOT NULL,
        [RuleType] VARCHAR(50) NOT NULL,
        [Criteria] NVARCHAR(MAX),
        [TargetGroup] NVARCHAR(255),
        [MaxAssignments] INT DEFAULT 0,
        [IsActive] BIT DEFAULT 1,
        [DisplayOrder] INT DEFAULT 0,
        [CreatedDateUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedByUserId] UNIQUEIDENTIFIER,
        [ModifiedDateUtc] DATETIME2,
        [ModifiedByUserId] UNIQUEIDENTIFIER,
        [IsDeleted] BIT DEFAULT 0,

        -- Indexes
        INDEX [IX_LeadAssignmentRules_TenantId] NONCLUSTERED ([TenantId]),
        INDEX [IX_LeadAssignmentRules_RuleType] NONCLUSTERED ([RuleType]),
        INDEX [IX_LeadAssignmentRules_IsActive] NONCLUSTERED ([IsActive])
    );

    PRINT 'Table [LeadAssignmentRules] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [LeadAssignmentRules] already exists.';
END;

-- ============================================================================
-- 6. LEAD_ASSIGNMENT_HISTORY TABLE
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LeadAssignmentHistory')
BEGIN
    CREATE TABLE [dbo].[LeadAssignmentHistory] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [LeadId] UNIQUEIDENTIFIER NOT NULL,
        [AssignedToUserId] UNIQUEIDENTIFIER NOT NULL,
        [AssignmentMethod] VARCHAR(50),
        [AssignmentDateUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [Notes] NVARCHAR(MAX),
        [CreatedByUserId] UNIQUEIDENTIFIER,

        -- Indexes
        INDEX [IX_LeadAssignmentHistory_TenantId] NONCLUSTERED ([TenantId]),
        INDEX [IX_LeadAssignmentHistory_LeadId] NONCLUSTERED ([LeadId]),
        INDEX [IX_LeadAssignmentHistory_AssignedToUserId] NONCLUSTERED ([AssignedToUserId]),
        INDEX [IX_LeadAssignmentHistory_AssignmentDateUtc] NONCLUSTERED ([AssignmentDateUtc] DESC)
    );

    -- Foreign Key
    ALTER TABLE [dbo].[LeadAssignmentHistory]
    ADD CONSTRAINT [FK_LeadAssignmentHistory_Leads]
        FOREIGN KEY ([LeadId]) REFERENCES [dbo].[Leads]([Id]);

    PRINT 'Table [LeadAssignmentHistory] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [LeadAssignmentHistory] already exists.';
END;

-- ============================================================================
-- 7. LEAD_QUALITY_METRICS TABLE
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LeadQualityMetrics')
BEGIN
    CREATE TABLE [dbo].[LeadQualityMetrics] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [LeadId] UNIQUEIDENTIFIER NOT NULL,
        [CompletedActivities] INT DEFAULT 0,
        [TotalActivities] INT DEFAULT 0,
        [TimeToFirstContact] INT,
        [ResponseRate] DECIMAL(5,2),
        [ConversionProbability] DECIMAL(5,2),
        [LastScoringDateUtc] DATETIME2,
        [LastActivityDateUtc] DATETIME2,
        [ModifiedDateUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),

        -- Indexes
        INDEX [IX_LeadQualityMetrics_TenantId] NONCLUSTERED ([TenantId]),
        INDEX [IX_LeadQualityMetrics_LeadId] NONCLUSTERED ([LeadId])
    );

    PRINT 'Table [LeadQualityMetrics] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [LeadQualityMetrics] already exists.';
END;

-- ============================================================================
-- 8. ACCOUNTS TABLE
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Accounts')
BEGIN
    CREATE TABLE [dbo].[Accounts] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [TenantId] UNIQUEIDENTIFIER NOT NULL,
        [AccountNumber] VARCHAR(50) NOT NULL,
        [AccountName] NVARCHAR(255) NOT NULL,
        [AccountTypeCode] VARCHAR(50),
        [MainEmail] NVARCHAR(255),
        [MainPhone] NVARCHAR(20),
        [CreatedDateUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [CreatedByUserId] UNIQUEIDENTIFIER,
        [ModifiedDateUtc] DATETIME2,
        [ModifiedByUserId] UNIQUEIDENTIFIER,
        [IsDeleted] BIT DEFAULT 0,

        -- Indexes
        INDEX [IX_Accounts_TenantId] NONCLUSTERED ([TenantId]),
        INDEX [IX_Accounts_AccountNumber] NONCLUSTERED ([AccountNumber])
    );

    PRINT 'Table [Accounts] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [Accounts] already exists.';
END;

-- ============================================================================
-- 9. TENANTS TABLE (If not exists)
-- ============================================================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'Tenants')
BEGIN
    CREATE TABLE [dbo].[Tenants] (
        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
        [TenantCode] VARCHAR(50) NOT NULL UNIQUE,
        [TenantName] NVARCHAR(255) NOT NULL,
        [StatusCode] VARCHAR(50) DEFAULT 'Active',
        [CreatedDateUtc] DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
        [IsDeleted] BIT DEFAULT 0,

        INDEX [IX_Tenants_TenantCode] NONCLUSTERED ([TenantCode]),
        INDEX [IX_Tenants_StatusCode] NONCLUSTERED ([StatusCode])
    );

    PRINT 'Table [Tenants] created successfully.';
END
ELSE
BEGIN
    PRINT 'Table [Tenants] already exists.';
END;

PRINT '========================================';
PRINT 'All tables created successfully!';
PRINT '========================================';
