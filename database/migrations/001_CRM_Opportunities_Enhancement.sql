-- ============================================================================
-- AMS CRM Opportunities Enhancement Migration
-- ============================================================================
-- This script adds enhanced opportunity functionality to support:
-- 1. Detailed opportunity tracking with stages and forecasts
-- 2. Opportunity board (Kanban) view
-- 3. Pipeline analytics and visualization
-- ============================================================================

-- ============================================================================
-- SECTION 1: Verify and Enhance CRM.Opportunity Table
-- ============================================================================

-- Check if CRM.Opportunity table needs to be enhanced with additional columns
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'CRM' AND TABLE_NAME = 'Opportunity' AND COLUMN_NAME = 'Stage')
BEGIN
    ALTER TABLE CRM.Opportunity ADD Stage NVARCHAR(50) NOT NULL DEFAULT 'Qualified';
END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'CRM' AND TABLE_NAME = 'Opportunity' AND COLUMN_NAME = 'Description')
BEGIN
    ALTER TABLE CRM.Opportunity ADD Description NVARCHAR(MAX) NULL;
END;

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'CRM' AND TABLE_NAME = 'Opportunity' AND COLUMN_NAME = 'CampaignId')
BEGIN
    ALTER TABLE CRM.Opportunity ADD CampaignId UNIQUEIDENTIFIER NULL;
END;

-- ============================================================================
-- SECTION 2: Create Lookup Tables for Opportunity Stages and Forecasts
-- ============================================================================

-- OpportunityStage table is assumed to exist with the following structure:
-- [OpportunityStageId], [TenantId], [StageCode], [StageName], [SortOrder], 
-- [ProbabilityPercent], [IsClosedStage], [IsWonStage], [IsActive]

-- Verify OpportunityStage table exists (if not, create it)
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'CRM' AND TABLE_NAME = 'OpportunityStage')
BEGIN
    CREATE TABLE CRM.OpportunityStage (
        OpportunityStageId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        StageCode NVARCHAR(50) NOT NULL,
        StageName NVARCHAR(100) NOT NULL,
        SortOrder INT NOT NULL,
        ProbabilityPercent TINYINT NOT NULL DEFAULT 50,
        IsClosedStage BIT NOT NULL DEFAULT 0,
        IsWonStage BIT NOT NULL DEFAULT 0,
        IsActive BIT NOT NULL DEFAULT 1,
        CONSTRAINT UK_OpportunityStage_TenantCode UNIQUE(TenantId, StageCode)
    );

    CREATE INDEX IX_OpportunityStage_TenantId ON CRM.OpportunityStage(TenantId);
    CREATE INDEX IX_OpportunityStage_Code ON CRM.OpportunityStage(StageCode);
END;

-- Create ForecastCategory lookup table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'CRM' AND TABLE_NAME = 'ForecastCategory')
BEGIN
    CREATE TABLE CRM.ForecastCategory (
        CategoryId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        Code NVARCHAR(50) NOT NULL,
        Label NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        ForecastPercent INT DEFAULT 50,
        IsActive BIT NOT NULL DEFAULT 1,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc DATETIME2 NULL,
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT UK_ForecastCategory_TenantCode UNIQUE(TenantId, Code)
    );

    CREATE INDEX IX_ForecastCategory_TenantId ON CRM.ForecastCategory(TenantId);
    CREATE INDEX IX_ForecastCategory_Code ON CRM.ForecastCategory(Code);
END;

-- Create OpportunityActivity table for tracking changes and activities
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'CRM' AND TABLE_NAME = 'OpportunityActivity')
BEGIN
    CREATE TABLE CRM.OpportunityActivity (
        ActivityId UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        OpportunityId UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ActivityType NVARCHAR(50) NOT NULL, -- 'StageChanged', 'AmountUpdated', 'OwnerAssigned', 'Note'
        Description NVARCHAR(MAX) NULL,
        OldValue NVARCHAR(MAX) NULL,
        NewValue NVARCHAR(MAX) NULL,
        CreatedByUserId UNIQUEIDENTIFIER NOT NULL,
        CreatedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL DEFAULT 0,
        CONSTRAINT FK_OpportunityActivity_Opportunity FOREIGN KEY(OpportunityId) REFERENCES CRM.Opportunity(OpportunityId)
    );

    CREATE INDEX IX_OpportunityActivity_OpportunityId ON CRM.OpportunityActivity(OpportunityId);
    CREATE INDEX IX_OpportunityActivity_TenantId ON CRM.OpportunityActivity(TenantId);
    CREATE INDEX IX_OpportunityActivity_ActivityType ON CRM.OpportunityActivity(ActivityType);
END;

-- ============================================================================
-- SECTION 3: Seed Opportunity Stages
-- ============================================================================

-- Insert default stages for all tenants (using placeholder tenant ID)
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TOP 1 TenantId FROM [Platform].Tenant WHERE IsDeleted = 0);

IF @TenantId IS NOT NULL
BEGIN
    -- Check if stages already exist
    IF NOT EXISTS (SELECT 1 FROM CRM.OpportunityStage WHERE TenantId = @TenantId AND Code = 'Qualified')
    BEGIN
        INSERT INTO CRM.OpportunityStage (TenantId, Code, Label, Description, StageOrder)
        VALUES
            (@TenantId, 'Qualified', 'Qualified', 'Lead has been qualified and opportunity created', 0),
            (@TenantId, 'Proposal', 'Proposal', 'Proposal or quote has been sent to prospect', 1),
            (@TenantId, 'Negotiation', 'Negotiation', 'In active negotiation phase', 2),
            (@TenantId, 'ClosedWon', 'Closed Won', 'Deal has been successfully closed', 3),
            (@TenantId, 'ClosedLost', 'Closed Lost', 'Deal was lost to competitor or did not proceed', 4);
    END;
END;

-- ============================================================================
-- SECTION 4: Seed Forecast Categories
-- ============================================================================

IF @TenantId IS NOT NULL
BEGIN
    -- Check if forecast categories already exist
    IF NOT EXISTS (SELECT 1 FROM CRM.ForecastCategory WHERE TenantId = @TenantId AND Code = 'Pipeline')
    BEGIN
        INSERT INTO CRM.ForecastCategory (TenantId, Code, Label, Description, ForecastPercent)
        VALUES
            (@TenantId, 'Pipeline', 'Pipeline', 'Opportunities in the pipeline', 10),
            (@TenantId, 'BestCase', 'Best Case', 'Best case scenario for closing', 50),
            (@TenantId, 'CommitmentForecast', 'Commitment', 'Committed forecast', 75),
            (@TenantId, 'Forecast', 'Forecast', 'Expected forecast', 100),
            (@TenantId, 'Omitted', 'Omitted', 'Omitted from forecast', 0);
    END;
END;

-- ============================================================================
-- SECTION 5: Sample Data (Optional - For Testing/Demo)
-- ============================================================================

-- Create a stored procedure to insert sample opportunities (optional)
IF OBJECT_ID('CRM.sp_InsertSampleOpportunities', 'P') IS NULL
BEGIN
    EXEC ('
    CREATE PROCEDURE CRM.sp_InsertSampleOpportunities
        @TenantId UNIQUEIDENTIFIER,
        @AccountId UNIQUEIDENTIFIER = NULL,
        @NumberOfOpportunities INT = 10
    AS
    BEGIN
        SET NOCOUNT ON;

        DECLARE @i INT = 0;
        DECLARE @OpportunityId UNIQUEIDENTIFIER;
        DECLARE @AccountCount INT;
        DECLARE @CurrentAccountId UNIQUEIDENTIFIER;
        DECLARE @RandomStageIndex INT;
        DECLARE @RandomAmount DECIMAL(12,2);
        DECLARE @Stages TABLE (StageId INT, Code NVARCHAR(50), Label NVARCHAR(100));
        DECLARE @StageCount INT = 0;

        -- Get available stages
        INSERT INTO @Stages (StageId, Code, Label)
        SELECT ROW_NUMBER() OVER (ORDER BY StageOrder), Code, Label 
        FROM CRM.OpportunityStage 
        WHERE TenantId = @TenantId AND IsActive = 1 AND IsDeleted = 0;

        SET @StageCount = @@ROWCOUNT;

        -- If no account specified, get one
        IF @AccountId IS NULL
        BEGIN
            SELECT TOP 1 @CurrentAccountId = AccountId 
            FROM Client.Account 
            WHERE TenantId = @TenantId AND IsDeleted = 0;
        END
        ELSE
        BEGIN
            SET @CurrentAccountId = @AccountId;
        END;

        -- Insert sample opportunities
        WHILE @i < @NumberOfOpportunities
        BEGIN
            SET @OpportunityId = NEWID();
            SET @RandomAmount = ABS(CHECKSUM(NEWID())) % 500000 + 10000;
            SET @RandomStageIndex = (ABS(CHECKSUM(NEWID())) % @StageCount) + 1;

            INSERT INTO CRM.Opportunity 
            (OpportunityId, TenantId, OpportunityNumber, AccountId, OpportunityName, 
             EstimatedAmount, OwnerUserId, CloseDate, WinProbability, ForecastCategoryCode, 
             Stage, Description, CreatedDateUtc, CreatedByUserId, IsDeleted)
            SELECT 
                @OpportunityId,
                @TenantId,
                ''OPP-'' + CONVERT(VARCHAR(20), SYSDATETIME(), 121) + ''-'' + CONVERT(VARCHAR(10), @i),
                @CurrentAccountId,
                ''Opportunity #'' + CONVERT(VARCHAR(10), @i),
                @RandomAmount,
                NULL,
                DATEADD(DAY, ABS(CHECKSUM(NEWID())) % 90, GETUTCDATE()),
                ABS(CHECKSUM(NEWID())) % 100,
                ''Pipeline'',
                Code,
                ''Sample opportunity for testing'',
                GETUTCDATE(),
                (SELECT TOP 1 UserId FROM [Identity].User WHERE TenantId = @TenantId AND IsDeleted = 0),
                0
            FROM @Stages 
            WHERE StageId = @RandomStageIndex;

            SET @i = @i + 1;
        END;
    END
    ');
END;

GO

-- ============================================================================
-- SECTION 6: Helper Views for Reporting
-- ============================================================================

-- Create view for opportunity pipeline analysis
IF OBJECT_ID('CRM.vw_OpportunityPipelineAnalysis', 'V') IS NOT NULL
    DROP VIEW CRM.vw_OpportunityPipelineAnalysis;

GO

CREATE VIEW CRM.vw_OpportunityPipelineAnalysis AS
SELECT 
    o.OpportunityId,
    o.TenantId,
    o.OpportunityNumber,
    o.OpportunityName,
    a.AccountName,
    o.EstimatedAmount,
    (o.EstimatedAmount * ISNULL(fc.ForecastPercent, 50) / 100.0) AS WeightedAmount,
    o.WinProbability,
    o.Stage,
    o.ForecastCategoryCode,
    os.StageName AS StageName,
    fc.Label AS ForecastCategoryName,
    o.CloseDate,
    DATEDIFF(DAY, GETUTCDATE(), o.CloseDate) AS DaysToClose,
    o.OwnerUserId,
    COALESCE(u.FirstName + ' ' + u.LastName, 'Unknown') AS OwnerName,
    o.CreatedDateUtc,
    o.ModifiedDateUtc
FROM CRM.Opportunity o
LEFT JOIN Client.Account a ON a.AccountId = o.AccountId
LEFT JOIN CRM.OpportunityStage os ON os.StageCode = o.Stage AND os.TenantId = o.TenantId
LEFT JOIN CRM.ForecastCategory fc ON fc.Code = o.ForecastCategoryCode AND fc.TenantId = o.TenantId
LEFT JOIN [Identity].[User] u ON u.UserId = o.OwnerUserId
WHERE o.IsDeleted = 0;

GO

-- ============================================================================
-- SECTION 7: Migration Version Tracking
-- ============================================================================

IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'Platform' AND TABLE_NAME = 'MigrationHistory')
BEGIN
    CREATE TABLE Platform.MigrationHistory (
        MigrationId INT PRIMARY KEY IDENTITY(1,1),
        MigrationName NVARCHAR(255) NOT NULL,
        ExecutedDateUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
    );
END;

-- Record this migration
IF NOT EXISTS (SELECT 1 FROM Platform.MigrationHistory WHERE MigrationName = 'CRM_Opportunities_Enhancement')
BEGIN
    INSERT INTO Platform.MigrationHistory (MigrationName) 
    VALUES ('CRM_Opportunities_Enhancement');

    PRINT 'Migration completed successfully: CRM_Opportunities_Enhancement';
END;

-- ============================================================================
-- SECTION 8: Verification and Summary
-- ============================================================================

PRINT '';
PRINT '=== CRM Opportunities Enhancement Migration Summary ===';
PRINT '';
PRINT 'Tables created/modified:';
SELECT 'CRM.OpportunityStage' AS TableName, COUNT(*) AS RecordCount FROM CRM.OpportunityStage WHERE IsDeleted = 0;
SELECT 'CRM.ForecastCategory' AS TableName, COUNT(*) AS RecordCount FROM CRM.ForecastCategory WHERE IsDeleted = 0;
SELECT 'CRM.OpportunityActivity' AS TableName, COUNT(*) AS RecordCount FROM CRM.OpportunityActivity WHERE IsDeleted = 0;
SELECT 'CRM.Opportunity (enhanced)' AS TableName, COUNT(*) AS RecordCount FROM CRM.Opportunity WHERE IsDeleted = 0;
PRINT '';
PRINT 'Migration completed successfully!';
