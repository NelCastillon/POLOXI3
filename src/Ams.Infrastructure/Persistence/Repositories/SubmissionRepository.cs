using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Submissions;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class SubmissionRepository : ISubmissionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public SubmissionRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    private static async Task EnsureEnterpriseWorkflowSchemaAsync(System.Data.IDbConnection cn, Guid? tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Submissions') EXEC(N'CREATE SCHEMA Submissions');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Core') EXEC(N'CREATE SCHEMA Core');

IF OBJECT_ID(N'Core.CarrierMarketSuggestionPreference', N'U') IS NULL
BEGIN
    CREATE TABLE Core.CarrierMarketSuggestionPreference
    (
        CarrierMarketSuggestionPreferenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Core_CarrierMarketSuggestionPreference PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        CarrierId UNIQUEIDENTIFIER NOT NULL,
        LineOfBusiness NVARCHAR(100) NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_SortOrder DEFAULT 500,
        IsActive BIT NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_IsActive DEFAULT 1,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_IsDeleted DEFAULT 0
    );
END;

IF OBJECT_ID(N'Core.CarrierMarketSuggestionPreference', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'TenantId') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_TenantId_Ensure DEFAULT '00000000-0000-0000-0000-000000000001';
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'CarrierId') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD CarrierId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_CarrierId_Ensure DEFAULT '00000000-0000-0000-0000-000000000000';
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'LineOfBusiness') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD LineOfBusiness NVARCHAR(100) NULL;
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'SortOrder') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD SortOrder INT NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_SortOrder_Ensure DEFAULT 500;
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'IsActive') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD IsActive BIT NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_IsActive_Ensure DEFAULT 1;
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'CreatedDateUtc') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_Created_Ensure DEFAULT SYSUTCDATETIME();
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'CreatedByUserId') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'ModifiedDateUtc') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD ModifiedDateUtc DATETIME2 NULL;
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'ModifiedByUserId') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
    IF COL_LENGTH(N'Core.CarrierMarketSuggestionPreference', N'IsDeleted') IS NULL ALTER TABLE Core.CarrierMarketSuggestionPreference ADD IsDeleted BIT NOT NULL CONSTRAINT DF_Core_CarrierMarketSuggestionPreference_IsDeleted_Ensure DEFAULT 0;
END;

IF OBJECT_ID(N'Core.CarrierMarketSuggestionPreference', N'U') IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Core.CarrierMarketSuggestionPreference') AND name = N'UX_Core_CarrierMarketSuggestionPreference_Default')
        EXEC(N'CREATE UNIQUE INDEX UX_Core_CarrierMarketSuggestionPreference_Default ON Core.CarrierMarketSuggestionPreference(TenantId, CarrierId) WHERE LineOfBusiness IS NULL AND IsDeleted = 0;');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Core.CarrierMarketSuggestionPreference') AND name = N'UX_Core_CarrierMarketSuggestionPreference_Line')
        EXEC(N'CREATE UNIQUE INDEX UX_Core_CarrierMarketSuggestionPreference_Line ON Core.CarrierMarketSuggestionPreference(TenantId, CarrierId, LineOfBusiness) WHERE LineOfBusiness IS NOT NULL AND IsDeleted = 0;');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Core.CarrierMarketSuggestionPreference') AND name = N'IX_Core_CarrierMarketSuggestionPreference_Tenant_Sort')
        EXEC(N'CREATE INDEX IX_Core_CarrierMarketSuggestionPreference_Tenant_Sort ON Core.CarrierMarketSuggestionPreference(TenantId, LineOfBusiness, IsActive, SortOrder, IsDeleted);');
END;

IF OBJECT_ID(N'Submissions.SubmissionIntakeQuestion', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.SubmissionIntakeQuestion
    (
        IntakeQuestionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SubmissionIntakeQuestion PRIMARY KEY DEFAULT NEWID(),
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        QuestionCode NVARCHAR(100) NOT NULL,
        QuestionText NVARCHAR(500) NOT NULL,
        HelpText NVARCHAR(1000) NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_SubmissionIntakeQuestion_IsRequired DEFAULT 1,
        AnswerText NVARCHAR(2000) NULL,
        IsAnswered BIT NOT NULL CONSTRAINT DF_SubmissionIntakeQuestion_IsAnswered DEFAULT 0,
        AnsweredByUserId UNIQUEIDENTIFIER NULL,
        AnsweredDateUtc DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionIntakeQuestion_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        ModifiedDateUtc DATETIME2 NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionIntakeQuestion_IsDeleted DEFAULT 0
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionIntakeQuestion') AND name = N'UX_SubmissionIntakeQuestion_Submission_Code')
    EXEC(N'CREATE UNIQUE INDEX UX_SubmissionIntakeQuestion_Submission_Code ON Submissions.SubmissionIntakeQuestion(SubmissionId, QuestionCode) WHERE IsDeleted = 0;');

IF OBJECT_ID(N'Submissions.SubmissionDocumentRequirement', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.SubmissionDocumentRequirement
    (
        DocumentRequirementId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SubmissionDocumentRequirement PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        LineOfBusiness NVARCHAR(100) NOT NULL,
        CategoryCode NVARCHAR(100) NOT NULL,
        DisplayName NVARCHAR(200) NOT NULL,
        IsRequired BIT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_IsRequired DEFAULT 1,
        SortOrder INT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_SortOrder DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionDocumentRequirement_IsDeleted DEFAULT 0
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.SubmissionDocumentRequirement') AND name = N'UX_SubmissionDocumentRequirement_Tenant_Lob_Code')
    EXEC(N'CREATE UNIQUE INDEX UX_SubmissionDocumentRequirement_Tenant_Lob_Code ON Submissions.SubmissionDocumentRequirement(TenantId, LineOfBusiness, CategoryCode) WHERE IsDeleted = 0;');

IF OBJECT_ID(N'Submissions.SubmissionMarketDocument', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.SubmissionMarketDocument
    (
        SubmissionMarketDocumentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_SubmissionMarketDocument PRIMARY KEY DEFAULT NEWID(),
        SubmissionMarketId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        DocumentId UNIQUEIDENTIFIER NOT NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionMarketDocument_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionMarketDocument_IsDeleted DEFAULT 0
    );
END;

IF COL_LENGTH(N'Submissions.SubmissionMarket', N'ReasonCode') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD ReasonCode NVARCHAR(80) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'Notes') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD Notes NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'NextActionDateUtc') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD NextActionDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'SubmittedDateUtc') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD SubmittedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'SubmittedByUserId') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD SubmittedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'UnderwriterName') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD UnderwriterName NVARCHAR(200) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'UnderwriterEmail') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD UnderwriterEmail NVARCHAR(320) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'UnderwriterPhone') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD UnderwriterPhone NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'DueDateUtc') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD DueDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'RequestedCoverageSummary') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD RequestedCoverageSummary NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'RequestedLimits') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD RequestedLimits NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'SubmissionMethodCode') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD SubmissionMethodCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'FollowUpTaskId') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD FollowUpTaskId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.SubmissionMarket', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.SubmissionMarket ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

IF COL_LENGTH(N'Submissions.Quote', N'SubmissionMarketId') IS NULL ALTER TABLE Submissions.Quote ADD SubmissionMarketId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'QuoteRequestDateUtc') IS NULL ALTER TABLE Submissions.Quote ADD QuoteRequestDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Quote', N'QuoteReceivedDateUtc') IS NULL ALTER TABLE Submissions.Quote ADD QuoteReceivedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Quote', N'ResponseVersion') IS NULL ALTER TABLE Submissions.Quote ADD ResponseVersion INT NOT NULL CONSTRAINT DF_Quote_ResponseVersion DEFAULT 1;
IF COL_LENGTH(N'Submissions.Quote', N'ResponseSourceCode') IS NULL ALTER TABLE Submissions.Quote ADD ResponseSourceCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'CarrierReferenceNumber') IS NULL ALTER TABLE Submissions.Quote ADD CarrierReferenceNumber NVARCHAR(100) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'RequestedByUserId') IS NULL ALTER TABLE Submissions.Quote ADD RequestedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'ReceivedByUserId') IS NULL ALTER TABLE Submissions.Quote ADD ReceivedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'EffectiveDate') IS NULL ALTER TABLE Submissions.Quote ADD EffectiveDate DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Quote', N'CoverageForms') IS NULL ALTER TABLE Submissions.Quote ADD CoverageForms NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'IsBindable') IS NULL ALTER TABLE Submissions.Quote ADD IsBindable BIT NOT NULL CONSTRAINT DF_Quote_IsBindable DEFAULT 0;
IF COL_LENGTH(N'Submissions.Quote', N'CommissionPercent') IS NULL ALTER TABLE Submissions.Quote ADD CommissionPercent DECIMAL(9,4) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'Subjectivities') IS NULL ALTER TABLE Submissions.Quote ADD Subjectivities NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'Exclusions') IS NULL ALTER TABLE Submissions.Quote ADD Exclusions NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'CarrierRating') IS NULL ALTER TABLE Submissions.Quote ADD CarrierRating NVARCHAR(80) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'PaymentTerms') IS NULL ALTER TABLE Submissions.Quote ADD PaymentTerms NVARCHAR(200) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'MinimumEarnedPremium') IS NULL ALTER TABLE Submissions.Quote ADD MinimumEarnedPremium DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'TaxesAndFees') IS NULL ALTER TABLE Submissions.Quote ADD TaxesAndFees DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'BrokerFee') IS NULL ALTER TABLE Submissions.Quote ADD BrokerFee DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'TriaIncluded') IS NULL ALTER TABLE Submissions.Quote ADD TriaIncluded BIT NULL;
IF COL_LENGTH(N'Submissions.Quote', N'QuoteDocumentId') IS NULL ALTER TABLE Submissions.Quote ADD QuoteDocumentId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'IsSelected') IS NULL ALTER TABLE Submissions.Quote ADD IsSelected BIT NOT NULL CONSTRAINT DF_Quote_IsSelected DEFAULT 0;
IF COL_LENGTH(N'Submissions.Quote', N'IsRecommended') IS NULL ALTER TABLE Submissions.Quote ADD IsRecommended BIT NOT NULL CONSTRAINT DF_Quote_IsRecommended DEFAULT 0;
IF COL_LENGTH(N'Submissions.Quote', N'SelectedByUserId') IS NULL ALTER TABLE Submissions.Quote ADD SelectedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Quote', N'SelectedDateUtc') IS NULL ALTER TABLE Submissions.Quote ADD SelectedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Quote', N'SelectionReason') IS NULL ALTER TABLE Submissions.Quote ADD SelectionReason NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'RecommendationScore') IS NULL ALTER TABLE Submissions.Quote ADD RecommendationScore INT NOT NULL CONSTRAINT DF_Quote_RecommendationScore DEFAULT 0;
IF COL_LENGTH(N'Submissions.Quote', N'RecommendationReason') IS NULL ALTER TABLE Submissions.Quote ADD RecommendationReason NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.Quote', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.Quote ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Quote', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.Quote ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

IF OBJECT_ID(N'Submissions.QuoteRevision', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.QuoteRevision
    (
        QuoteRevisionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_QuoteRevision PRIMARY KEY DEFAULT NEWID(),
        QuoteId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        SubmissionMarketId UNIQUEIDENTIFIER NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        ResponseVersion INT NOT NULL,
        Status NVARCHAR(50) NOT NULL,
        AnnualPremium DECIMAL(18,2) NOT NULL,
        Deductible DECIMAL(18,2) NULL,
        [Limit] DECIMAL(18,2) NULL,
        CommissionPercent DECIMAL(9,4) NULL,
        TaxesAndFees DECIMAL(18,2) NULL,
        BrokerFee DECIMAL(18,2) NULL,
        MinimumEarnedPremium DECIMAL(18,2) NULL,
        EffectiveDate DATETIME2 NULL,
        ExpiresDateUtc DATETIME2 NOT NULL,
        CoverageForms NVARCHAR(2000) NULL,
        Subjectivities NVARCHAR(2000) NULL,
        Exclusions NVARCHAR(2000) NULL,
        CarrierRating NVARCHAR(80) NULL,
        PaymentTerms NVARCHAR(200) NULL,
        IsBindable BIT NOT NULL CONSTRAINT DF_QuoteRevision_IsBindable DEFAULT 0,
        CoverageNotes NVARCHAR(1000) NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_QuoteRevision_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_QuoteRevision_IsDeleted DEFAULT 0
    );
END;

IF COL_LENGTH(N'Submissions.Proposal', N'DeliveryMethod') IS NULL ALTER TABLE Submissions.Proposal ADD DeliveryMethod NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'Recipient') IS NULL ALTER TABLE Submissions.Proposal ADD Recipient NVARCHAR(320) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'SentDateUtc') IS NULL ALTER TABLE Submissions.Proposal ADD SentDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'SentByUserId') IS NULL ALTER TABLE Submissions.Proposal ADD SentByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'ClientDecision') IS NULL ALTER TABLE Submissions.Proposal ADD ClientDecision NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'DecisionNotes') IS NULL ALTER TABLE Submissions.Proposal ADD DecisionNotes NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'DecisionDateUtc') IS NULL ALTER TABLE Submissions.Proposal ADD DecisionDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'DecidedByUserId') IS NULL ALTER TABLE Submissions.Proposal ADD DecidedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'DocumentId') IS NULL ALTER TABLE Submissions.Proposal ADD DocumentId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'CustomIntroduction') IS NULL ALTER TABLE Submissions.Proposal ADD CustomIntroduction NVARCHAR(2000) NULL;

IF OBJECT_ID(N'Submissions.ProposalQuote', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.ProposalQuote
    (
        ProposalQuoteId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProposalQuote PRIMARY KEY DEFAULT NEWID(),
        ProposalId UNIQUEIDENTIFIER NOT NULL,
        QuoteId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SortOrder INT NOT NULL CONSTRAINT DF_ProposalQuote_SortOrder DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalQuote_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL CONSTRAINT DF_ProposalQuote_IsDeleted DEFAULT 0
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalQuote') AND name = N'UX_ProposalQuote_Proposal_Quote')
    EXEC(N'CREATE UNIQUE INDEX UX_ProposalQuote_Proposal_Quote ON Submissions.ProposalQuote(ProposalId, QuoteId) WHERE IsDeleted = 0;');

IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'RelatedEntityName') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD RelatedEntityName NVARCHAR(100) NULL;
IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'RelatedEntityId') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD RelatedEntityId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.SubmissionActionLog', N'ActionSource') IS NULL ALTER TABLE Submissions.SubmissionActionLog ADD ActionSource NVARCHAR(50) NULL;

IF @TenantId IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Submissions.SubmissionDocumentRequirement WHERE TenantId = @TenantId AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionDocumentRequirement (TenantId, LineOfBusiness, CategoryCode, DisplayName, IsRequired, SortOrder)
    SELECT @TenantId, lob.LineOfBusiness, req.CategoryCode, req.DisplayName, 1, req.SortOrder
    FROM (VALUES (N'Application', N'Application', 10), (N'LossRuns', N'Loss runs', 20), (N'ExposureSchedules', N'Exposure schedules', 30), (N'PriorPolicies', N'Prior policies', 40), (N'Financials', N'Financials', 50), (N'ACORD', N'ACORD forms', 60)) req(CategoryCode, DisplayName, SortOrder)
    CROSS JOIN (SELECT DISTINCT COALESCE(NULLIF(LineOfBusiness, N''), N'General Liability') AS LineOfBusiness FROM Submissions.Submission WHERE TenantId = @TenantId AND IsDeleted = 0) lob;
END;";

        await cn.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private async Task EnsureDefaultIntakeAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, tenantId, cancellationToken);
        const string sql = @"
INSERT INTO Submissions.SubmissionIntakeQuestion (IntakeQuestionId, SubmissionId, TenantId, QuestionCode, QuestionText, HelpText, IsRequired, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @SubmissionId, @TenantId, src.QuestionCode, src.QuestionText, src.HelpText, src.IsRequired, SYSUTCDATETIME(), 0
FROM (VALUES
    (N'OperationsDescription', N'Operations description complete', N'Confirm operations, locations, exposures, and risk narrative are complete.', CAST(1 AS bit)),
    (N'CoverageNeeds', N'Coverage needs confirmed', N'Confirm limits, deductibles, forms, and requested coverage enhancements.', CAST(1 AS bit)),
    (N'LossHistoryReviewed', N'Loss history reviewed', N'Confirm loss runs and known claim explanations have been reviewed.', CAST(1 AS bit)),
    (N'ExposureDataValidated', N'Exposure data validated', N'Confirm schedules, payroll, sales, vehicles, properties, and other exposure bases are complete.', CAST(1 AS bit)),
    (N'ProducerPreference', N'Producer preference documented', N'Capture producer/client preference that may influence recommendation scoring.', CAST(0 AS bit))
) src(QuestionCode, QuestionText, HelpText, IsRequired)
WHERE NOT EXISTS (SELECT 1 FROM Submissions.SubmissionIntakeQuestion q WHERE q.SubmissionId = @SubmissionId AND q.QuestionCode = src.QuestionCode AND q.IsDeleted = 0);";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private static async Task RecordOpportunityWorkflowAsync(System.Data.IDbConnection cn, Guid submissionId, Guid tenantId, string stageName, string eventType, string eventTitle, string eventDetail, string relatedEntityName, Guid? relatedEntityId, Guid? userId, CancellationToken cancellationToken)
    {
        const string sql = @"
DECLARE @OpportunityId UNIQUEIDENTIFIER;
SELECT @OpportunityId = OpportunityId FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

IF @OpportunityId IS NOT NULL AND @OpportunityId <> '00000000-0000-0000-0000-000000000000'
BEGIN
    DECLARE @StageId UNIQUEIDENTIFIER = (SELECT TOP 1 OpportunityStageId FROM CRM.OpportunityStage WHERE TenantId = @TenantId AND StageName = @StageName AND IsActive = 1 ORDER BY SortOrder, StageName);

    UPDATE CRM.Opportunity
    SET StageName = @StageName,
        OpportunityStageId = COALESCE(@StageId, OpportunityStageId),
        ForecastCategoryCode = CASE WHEN @StageName IN (N'Won', N'Bound', N'Closed Won') THEN N'Closed Won' WHEN @StageName IN (N'Lost', N'Declined', N'Closed Lost') THEN N'Closed Lost' ELSE COALESCE(NULLIF(ForecastCategoryCode, N''), N'Pipeline') END,
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @UserId
    WHERE OpportunityId = @OpportunityId AND TenantId = @TenantId AND IsDeleted = 0
      AND COALESCE(StageName, N'') NOT IN (N'Closed Won', N'Closed Lost');

    INSERT INTO CRM.OpportunityWorkflowEvent (WorkflowEventId, TenantId, OpportunityId, EventType, EventTitle, EventDetail, RelatedEntityName, RelatedEntityId, EventDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @OpportunityId, @EventType, @EventTitle, @EventDetail, @RelatedEntityName, @RelatedEntityId, SYSUTCDATETIME(), SYSUTCDATETIME(), @UserId, 0);
END;";

        await cn.ExecuteAsync(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId, StageName = stageName, EventType = eventType, EventTitle = eventTitle, EventDetail = eventDetail, RelatedEntityName = relatedEntityName, RelatedEntityId = relatedEntityId, UserId = userId }, cancellationToken: cancellationToken));
    }

    // ── Submission Register ───────────────────────────────────────────

    private const string SubmissionColumns = @"
        s.SubmissionId, s.TenantId, s.AccountId, a.AccountName, s.OpportunityId, o.OpportunityName,
        s.SubmissionNumber, s.LineOfBusiness, s.Status, s.Priority,
        s.AssignedToUserId, u.FullName AS AssignedToUserName,
        s.EffectiveDate, s.ExpirationDate, s.TargetPremium,
        s.MarketCount, s.QuoteCount, s.CreatedDateUtc, s.ModifiedDateUtc";

    public async Task<PagedResult<SubmissionDto>> SearchAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT s.SubmissionId,
           s.TenantId,
           s.AccountId,
           a.AccountName,
           s.OpportunityId,
           COALESCE(o.OpportunityName, s.SubmissionNumber) AS OpportunityName,
           s.SubmissionNumber,
           s.LineOfBusiness,
           s.Status,
           s.Priority,
           s.AssignedToUserId,
           COALESCE(u.FullName, u.DisplayName, u.UserName) AS AssignedToUserName,
           s.EffectiveDate,
           s.ExpirationDate,
           s.TargetPremium,
           (SELECT COUNT(1) FROM Submissions.SubmissionMarket sm WHERE sm.SubmissionId = s.SubmissionId AND sm.IsDeleted = 0) AS MarketCount,
           (SELECT COUNT(1) FROM Submissions.Quote q WHERE q.SubmissionId = s.SubmissionId AND q.IsDeleted = 0) AS QuoteCount,
           s.CreatedDateUtc,
           s.ModifiedDateUtc
    FROM   Submissions.Submission s
    JOIN   Client.Account a ON a.AccountId = s.AccountId
    LEFT JOIN CRM.Opportunity o ON o.OpportunityId = s.OpportunityId
    LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
    WHERE  s.TenantId = @TenantId
      AND  s.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = '' OR s.SubmissionNumber LIKE '%' + @SearchTerm + '%' OR s.LineOfBusiness LIKE '%' + @SearchTerm + '%' OR a.AccountName LIKE '%' + @SearchTerm + '%' OR o.OpportunityName LIKE '%' + @SearchTerm + '%')
),
Filtered AS
(
    SELECT *
    FROM Cte
    WHERE (@Status IS NULL OR @Status = '' OR Status = @Status)
      AND (@LineOfBusiness IS NULL OR @LineOfBusiness = '' OR LineOfBusiness = @LineOfBusiness)
)
SELECT * FROM Filtered
ORDER BY CreatedDateUtc DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

;WITH Cte AS
(
    SELECT s.LineOfBusiness, s.Status
    FROM   Submissions.Submission s
    JOIN   Client.Account a ON a.AccountId = s.AccountId
    LEFT JOIN CRM.Opportunity o ON o.OpportunityId = s.OpportunityId
    WHERE  s.TenantId = @TenantId
      AND  s.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = '' OR s.SubmissionNumber LIKE '%' + @SearchTerm + '%' OR s.LineOfBusiness LIKE '%' + @SearchTerm + '%' OR a.AccountName LIKE '%' + @SearchTerm + '%' OR o.OpportunityName LIKE '%' + @SearchTerm + '%')
),
Filtered AS
(
    SELECT *
    FROM Cte
    WHERE (@Status IS NULL OR @Status = '' OR Status = @Status)
      AND (@LineOfBusiness IS NULL OR @LineOfBusiness = '' OR LineOfBusiness = @LineOfBusiness)
)
SELECT COUNT(1) FROM Filtered;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId       = tenantId,
            SearchTerm     = searchTerm,
            Status         = status,
            LineOfBusiness = lineOfBusiness,
            Offset         = (Math.Max(pageNumber, 1) - 1) * pageSize,
            PageSize       = pageSize,
        }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<SubmissionDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<SubmissionDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<SubmissionDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT s.SubmissionId,
       s.TenantId,
       s.AccountId,
       a.AccountName,
       s.OpportunityId,
       COALESCE(o.OpportunityName, s.SubmissionNumber) AS OpportunityName,
       s.SubmissionNumber,
       s.LineOfBusiness,
       s.Status,
       s.Priority,
       s.AssignedToUserId,
       COALESCE(u.FullName, u.DisplayName, u.UserName) AS AssignedToUserName,
       s.EffectiveDate,
       s.ExpirationDate,
       s.TargetPremium,
       (SELECT COUNT(1) FROM Submissions.SubmissionMarket sm WHERE sm.SubmissionId = s.SubmissionId AND sm.IsDeleted = 0) AS MarketCount,
       (SELECT COUNT(1) FROM Submissions.Quote q WHERE q.SubmissionId = s.SubmissionId AND q.IsDeleted = 0) AS QuoteCount,
       s.CreatedDateUtc,
       s.ModifiedDateUtc
FROM   Submissions.Submission s
JOIN   Client.Account a ON a.AccountId = s.AccountId
LEFT JOIN CRM.Opportunity o ON o.OpportunityId = s.OpportunityId
LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
WHERE  s.SubmissionId = @Id AND s.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<SubmissionDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreateAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
INSERT INTO Submissions.Submission
    (SubmissionId, TenantId, AccountId, OpportunityId, SubmissionNumber, LineOfBusiness, Status, Priority,
     AssignedToUserId, EffectiveDate, ExpirationDate, TargetPremium, MarketCount, QuoteCount,
     CreatedDateUtc, IsDeleted)
VALUES
    (@SubmissionId, @TenantId, @AccountId, @OpportunityId,
     'SUB-' + FORMAT(GETUTCDATE(), 'yyyyMMdd') + '-' + RIGHT('0000' + CAST(NEXT VALUE FOR Submissions.SubmissionSeq AS VARCHAR), 4),
     @LineOfBusiness, 'Draft', @Priority,
     @AssignedToUserId, @EffectiveDate, @ExpirationDate, @TargetPremium, 0, 0,
     GETUTCDATE(), 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            SubmissionId     = id,
            request.TenantId,
            request.AccountId,
            request.OpportunityId,
            request.LineOfBusiness,
            request.Priority,
            request.AssignedToUserId,
            request.EffectiveDate,
            request.ExpirationDate,
            request.TargetPremium,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateAsync(Guid id, UpdateSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.Submission
SET    LineOfBusiness  = @LineOfBusiness,
       Status          = @Status,
       Priority        = @Priority,
       EffectiveDate   = @EffectiveDate,
       ExpirationDate  = @ExpirationDate,
       TargetPremium   = @TargetPremium,
       AssignedToUserId = @AssignedToUserId,
       ModifiedDateUtc = GETUTCDATE()
WHERE  SubmissionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            Id = id,
            request.LineOfBusiness,
            request.Status,
            request.Priority,
            request.EffectiveDate,
            request.ExpirationDate,
            request.TargetPremium,
            request.AssignedToUserId,
        }, cancellationToken: cancellationToken));
    }

    public async Task AssignAsync(Guid id, AssignSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.Submission
SET    AssignedToUserId = @AssignedToUserId,
       ModifiedDateUtc  = GETUTCDATE()
WHERE  SubmissionId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = id, request.AssignedToUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SubmissionActivityDto>> GetActivitiesAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT al.ActionLogId AS ActivityId,
       al.SubmissionId,
       al.TenantId,
       al.ActionCode,
       CASE al.ActionCode
           WHEN N'Note' THEN N'Note added'
           WHEN N'SubmitToMarket' THEN N'Submitted to market'
           WHEN N'RequestQuote' THEN N'Quote requested'
           WHEN N'Decline' THEN N'Submission declined'
           WHEN N'Copy' THEN N'Submission copied'
           WHEN N'Assign' THEN N'Submission assigned'
           WHEN N'FollowUpTask' THEN N'Follow-up task created'
           WHEN N'DocumentAttached' THEN N'Document attached'
           ELSE al.ActionCode
       END AS Title,
       al.Notes,
       NULL AS CreatedByName,
       al.CreatedDateUtc
FROM Submissions.SubmissionActionLog al
WHERE al.SubmissionId = @SubmissionId
  AND al.IsDeleted = 0
ORDER BY al.CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<SubmissionActivityDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> AddNoteAsync(Guid submissionId, AddSubmissionNoteRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0)
    THROW 52010, 'Submission was not found for note creation.', 1;

DECLARE @ActionLogId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (@ActionLogId, @SubmissionId, @TenantId, N'Note', @Notes, SYSUTCDATETIME(), 0);

UPDATE Submissions.Submission
SET ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @CreatedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

SELECT @ActionLogId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { SubmissionId = submissionId, request.TenantId, request.Notes, request.CreatedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT DocumentId, TenantId, DocumentTypeCode, CategoryCode, EntityName, EntityId, FileName, StoragePath, ContentType, FileSizeBytes, VersionNumber, StatusCode, RetentionDate, Description, Tags, UploadedByName, CreatedDateUtc, ModifiedDateUtc
FROM DMS.Document
WHERE TenantId = @TenantId
  AND EntityName = N'Submission'
  AND EntityId = @SubmissionId
  AND IsDeleted = 0
ORDER BY CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<DocumentDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<SubmissionTaskDto>> GetTasksAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT t.TaskItemId,
       t.TenantId,
       t.TaskNumber,
       t.Title,
       t.Description,
       t.TaskTypeCode,
       t.StageCode,
       t.PriorityCode,
       t.StatusCode,
       t.AssignedToUserId,
       COALESCE(u.FullName, u.DisplayName, u.UserName) AS AssignedToUserName,
       t.DueDate,
       t.CreatedDateUtc
FROM OPS.TaskItem t
LEFT JOIN IAM.[User] u ON u.UserId = t.AssignedToUserId
WHERE t.TenantId = @TenantId
  AND t.RelatedEntityName = N'Submission'
  AND t.RelatedEntityId = @SubmissionId
  AND t.IsDeleted = 0
ORDER BY CASE WHEN t.StatusCode IN (N'Completed', N'Closed') THEN 1 ELSE 0 END, t.DueDate ASC, t.CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<SubmissionTaskDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> CreateFollowUpTaskAsync(Guid submissionId, CreateSubmissionFollowUpTaskRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @AccountId UNIQUEIDENTIFIER;
SELECT @AccountId = AccountId
FROM Submissions.Submission
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

IF @AccountId IS NULL
    THROW 52011, 'Submission was not found for follow-up task creation.', 1;

DECLARE @TaskItemId UNIQUEIDENTIFIER = NEWID();
DECLARE @TaskNumber NVARCHAR(50) = CONCAT(N'TASK-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), @TaskItemId), N'-', N''), 6));

INSERT INTO OPS.TaskItem
    (TaskItemId, TenantId, TaskNumber, Title, Description, TaskTypeCode, StageCode, PriorityCode, StatusCode,
     RelatedEntityName, RelatedEntityId, AccountId, AssignedToUserId, DueDate, CompletedDate,
     CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
VALUES
    (@TaskItemId, @TenantId, @TaskNumber, @Title, @Description, N'FollowUp', N'Submission', @PriorityCode, N'Open',
     N'Submission', @SubmissionId, @AccountId, @AssignedToUserId, @DueDate, NULL,
     SYSUTCDATETIME(), @CreatedByUserId, NULL, NULL, 0);

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'FollowUpTask', CONCAT(N'Follow-up task created: ', @Title), SYSUTCDATETIME(), 0);

UPDATE Submissions.Submission
SET ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @CreatedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

SELECT @TaskItemId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
        {
            SubmissionId = submissionId,
            request.TenantId,
            request.Title,
            request.Description,
            request.PriorityCode,
            request.AssignedToUserId,
            request.DueDate,
            request.CreatedByUserId
        }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SubmissionLineDto>> GetLinesAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT sl.SubmissionLineId,
       sl.TenantId,
       sl.SubmissionId,
       sl.OpportunityId,
       sl.OpportunityLineId,
       sl.LineOfBusiness,
       CAST(NULL AS NVARCHAR(200)) AS Carrier,
       sl.TargetPremium,
       CAST(NULL AS NVARCHAR(50)) AS Priority,
       CAST(CASE WHEN ROW_NUMBER() OVER (ORDER BY sl.TargetPremium DESC, sl.CreatedDateUtc) = 1 THEN 1 ELSE 0 END AS bit) AS IsPrimary,
       s.EffectiveDate AS TargetEffectiveDate
FROM Submissions.SubmissionLine sl
JOIN Submissions.Submission s ON s.SubmissionId = sl.SubmissionId
WHERE sl.SubmissionId = @SubmissionId AND sl.IsDeleted = 0
ORDER BY IsPrimary DESC, sl.LineOfBusiness;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<SubmissionLineDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<SubmissionIntakeQuestionDto>> GetIntakeAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        var submission = await GetByIdAsync(submissionId, cancellationToken) ?? throw new InvalidOperationException("Submission was not found for intake.");
        await EnsureDefaultIntakeAsync(submissionId, submission.TenantId, cancellationToken);

        const string sql = @"
SELECT IntakeQuestionId, SubmissionId, TenantId, QuestionCode, QuestionText, COALESCE(HelpText, N'') AS HelpText,
       IsRequired, AnswerText, IsAnswered, AnsweredByUserId, AnsweredDateUtc
FROM Submissions.SubmissionIntakeQuestion
WHERE SubmissionId = @SubmissionId AND IsDeleted = 0
ORDER BY IsRequired DESC, QuestionText;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<SubmissionIntakeQuestionDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task UpdateIntakeQuestionAsync(Guid submissionId, Guid intakeQuestionId, UpdateSubmissionIntakeQuestionRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
UPDATE Submissions.SubmissionIntakeQuestion
SET AnswerText = @AnswerText,
    IsAnswered = @IsAnswered,
    AnsweredByUserId = @AnsweredByUserId,
    AnsweredDateUtc = CASE WHEN @IsAnswered = 1 THEN SYSUTCDATETIME() ELSE NULL END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE IntakeQuestionId = @IntakeQuestionId AND SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

IF @@ROWCOUNT = 0 THROW 52012, 'Submission intake question was not found.', 1;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'IntakeUpdated', N'Underwriting intake answer updated.', SYSUTCDATETIME(), @AnsweredByUserId, N'SubmissionIntakeQuestion', @IntakeQuestionId, N'User', 0);";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { SubmissionId = submissionId, IntakeQuestionId = intakeQuestionId, request.TenantId, request.AnswerText, request.IsAnswered, request.AnsweredByUserId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SubmissionDocumentChecklistDto>> GetDocumentChecklistAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, tenantId, cancellationToken);
        const string sql = @"
DECLARE @LineOfBusiness NVARCHAR(100) = (SELECT TOP 1 LineOfBusiness FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0);

SELECT r.DocumentRequirementId AS ChecklistItemId,
       @SubmissionId AS SubmissionId,
       r.TenantId,
       r.CategoryCode,
       r.DisplayName,
       r.IsRequired,
       CAST(CASE WHEN d.DocumentId IS NULL THEN 0 ELSE 1 END AS bit) AS IsSatisfied,
       d.DocumentId,
       d.FileName,
       d.CreatedDateUtc AS UploadedDateUtc
FROM Submissions.SubmissionDocumentRequirement r
OUTER APPLY (
    SELECT TOP 1 DocumentId, FileName, CreatedDateUtc
    FROM DMS.Document d
    WHERE d.TenantId = @TenantId
      AND d.EntityName = N'Submission'
      AND d.EntityId = @SubmissionId
      AND d.IsDeleted = 0
      AND (d.CategoryCode = r.CategoryCode OR d.DocumentTypeCode = r.CategoryCode OR d.Tags LIKE N'%' + r.DisplayName + N'%')
    ORDER BY d.CreatedDateUtc DESC
) d
WHERE r.TenantId = @TenantId
  AND r.IsDeleted = 0
  AND r.LineOfBusiness = COALESCE(@LineOfBusiness, r.LineOfBusiness)
ORDER BY r.SortOrder, r.DisplayName;";
        return (await cn.QueryAsync<SubmissionDocumentChecklistDto>(new CommandDefinition(sql, new { SubmissionId = submissionId, TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<SubmissionReadinessDto> GetReadinessAsync(Guid submissionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var intake = await GetIntakeAsync(submissionId, cancellationToken);
        var checklist = await GetDocumentChecklistAsync(submissionId, tenantId, cancellationToken);
        var blockingReasons = intake.Where(q => q.IsRequired && !q.IsAnswered).Select(q => $"Missing intake: {q.QuestionText}")
            .Concat(checklist.Where(d => d.IsRequired && !d.IsSatisfied).Select(d => $"Missing document: {d.DisplayName}"))
            .ToArray();

        return new SubmissionReadinessDto
        {
            SubmissionId = submissionId,
            RequiredQuestionCount = intake.Count(q => q.IsRequired),
            AnsweredRequiredQuestionCount = intake.Count(q => q.IsRequired && q.IsAnswered),
            RequiredDocumentCount = checklist.Count(d => d.IsRequired),
            SatisfiedRequiredDocumentCount = checklist.Count(d => d.IsRequired && d.IsSatisfied),
            IsReadyForMarketing = blockingReasons.Length == 0,
            BlockingReasons = blockingReasons
        };
    }

    public async Task<IReadOnlyList<SubmissionTaskTemplateDto>> GetTaskTemplatesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string seedSql = @"
DECLARE @Templates TABLE (TaskTypeCode NVARCHAR(50), TaskTypeName NVARCHAR(100), Description NVARCHAR(500), SortOrder INT);
INSERT INTO @Templates VALUES
(N'MissingInformation', N'Missing information', N'Collect missing intake or underwriting details.', 110),
(N'CarrierFollowUp', N'Carrier follow-up', N'Follow up with carrier for response or terms.', 120),
(N'QuoteReview', N'Quote review', N'Review received quote terms and compare options.', 130),
(N'ProposalFollowUp', N'Proposal follow-up', N'Follow up with client on delivered proposal.', 140),
(N'BindRequest', N'Bind request', N'Coordinate binding request and subjectivities.', 150),
(N'SubjectivitiesFollowUp', N'Subjectivities follow-up', N'Collect and clear quote subjectivities.', 160),
(N'DocumentCollection', N'Document collection', N'Collect required submission or post-bind documents.', 170);

INSERT INTO OPS.TaskType (TaskTypeId, TenantId, TaskTypeCode, TaskTypeName, Description, SortOrder, IsActive, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @TenantId, t.TaskTypeCode, t.TaskTypeName, t.Description, t.SortOrder, 1, SYSUTCDATETIME(), 0
FROM @Templates t
WHERE NOT EXISTS (SELECT 1 FROM OPS.TaskType x WHERE x.TenantId = @TenantId AND x.TaskTypeCode = t.TaskTypeCode AND x.IsDeleted = 0);

SELECT TaskTypeCode, TaskTypeName AS DisplayName, COALESCE(Description, N'') AS Description,
       CASE WHEN TaskTypeCode IN (N'BindRequest', N'QuoteReview') THEN N'High' ELSE N'Medium' END AS PriorityCode,
       CASE WHEN TaskTypeCode IN (N'CarrierFollowUp', N'ProposalFollowUp') THEN 3 WHEN TaskTypeCode = N'BindRequest' THEN 1 ELSE 5 END AS DefaultDueDays
FROM OPS.TaskType
WHERE TenantId = @TenantId AND IsDeleted = 0 AND TaskTypeCode IN (N'MissingInformation', N'CarrierFollowUp', N'QuoteReview', N'ProposalFollowUp', N'BindRequest', N'SubjectivitiesFollowUp', N'DocumentCollection')
ORDER BY SortOrder, TaskTypeName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<SubmissionTaskTemplateDto>(new CommandDefinition(seedSql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<SubmissionMetricsDto> GetMetricsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, tenantId, cancellationToken);
        const string sql = @"
SELECT COUNT(DISTINCT s.SubmissionId)
FROM Submissions.Submission s
LEFT JOIN Submissions.SubmissionIntakeQuestion q ON q.SubmissionId = s.SubmissionId AND q.IsDeleted = 0 AND q.IsRequired = 1 AND q.IsAnswered = 0
WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.Status NOT IN (N'Bound', N'Declined', N'Withdrawn') AND (s.Status IN (N'Draft', N'New') OR q.IntakeQuestionId IS NOT NULL);

SELECT COUNT(1)
FROM Submissions.Submission s
WHERE s.TenantId = @TenantId AND s.IsDeleted = 0 AND s.Status IN (N'Ready', N'Ready for Market');

SELECT COUNT(1)
FROM Submissions.SubmissionMarket sm
JOIN Submissions.Submission s ON s.SubmissionId = sm.SubmissionId
WHERE s.TenantId = @TenantId AND sm.IsDeleted = 0 AND sm.Status IN (N'Sent', N'Submitted', N'In Review');

SELECT COUNT(1)
FROM Submissions.Quote q
JOIN Submissions.Submission s ON s.SubmissionId = q.SubmissionId
WHERE s.TenantId = @TenantId AND q.IsDeleted = 0 AND q.Status IN (N'Received', N'Presented', N'Accepted') AND q.ExpiresDateUtc BETWEEN SYSUTCDATETIME() AND DATEADD(day, 14, SYSUTCDATETIME());

SELECT COUNT(1)
FROM Submissions.Proposal p
WHERE p.TenantId = @TenantId AND p.IsDeleted = 0 AND p.Status IN (N'Sent', N'Delivered', N'Pending Decision');

SELECT COUNT(1)
FROM OPS.TaskItem
WHERE TenantId = @TenantId AND IsDeleted = 0 AND TaskTypeCode = N'BindRequest' AND StatusCode NOT IN (N'Completed', N'Closed', N'Done');";

        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
        return new SubmissionMetricsDto
        {
            PendingIntake = await multi.ReadSingleAsync<int>(),
            ReadyForMarket = await multi.ReadSingleAsync<int>(),
            MarketsAwaitingResponse = await multi.ReadSingleAsync<int>(),
            QuotesExpiringSoon = await multi.ReadSingleAsync<int>(),
            ProposalsPendingDecision = await multi.ReadSingleAsync<int>(),
            BindRequestsPending = await multi.ReadSingleAsync<int>()
        };
    }

    public async Task<IReadOnlyList<PolicyCreationSourceDto>> GetPolicyCreationSourcesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF OBJECT_ID(N'Submissions.PolicyCreationSource', N'U') IS NULL
BEGIN
    SELECT CAST(N'00000000-0000-0000-0000-000000000001' AS UNIQUEIDENTIFIER) AS PolicyCreationSourceId,
           @TenantId AS TenantId,
           N'QuoteBound' AS SourceCode,
           N'Quote Bound' AS SourceName,
           N'Policy is created from an accepted or selected quote.' AS Description,
           CAST(1 AS bit) AS RequiresQuote,
           CAST(1 AS bit) AS RequiresSubmission,
           CAST(1 AS bit) AS RequiresAccount,
           CAST(0 AS bit) AS RequiresReason,
           CAST(0 AS bit) AS RequiresPolicyNumber,
           CAST(0 AS bit) AS AllowsDirectPolicyEntry,
           CAST(0 AS bit) AS IsImportSource,
           CAST(0 AS bit) AS IsConversionSource,
           CAST(1 AS bit) AS IsDefault,
           CAST(1 AS bit) AS IsActive,
           10 AS SortOrder
    UNION ALL
    SELECT CAST(N'00000000-0000-0000-0000-000000000002' AS UNIQUEIDENTIFIER),
           @TenantId,
           N'AlreadyBound',
           N'Already Bound Outside System',
           N'Carrier or broker already bound coverage outside the platform.',
           CAST(0 AS bit),
           CAST(0 AS bit),
           CAST(1 AS bit),
           CAST(1 AS bit),
           CAST(1 AS bit),
           CAST(1 AS bit),
           CAST(0 AS bit),
           CAST(0 AS bit),
           CAST(0 AS bit),
           CAST(1 AS bit),
           20;
    RETURN;
END;

SELECT PolicyCreationSourceId,
       TenantId,
       SourceCode,
       SourceName,
       Description,
       RequiresQuote,
       RequiresSubmission,
       RequiresAccount,
       RequiresReason,
       RequiresPolicyNumber,
       AllowsDirectPolicyEntry,
       IsImportSource,
       IsConversionSource,
       IsDefault,
       IsActive,
       SortOrder
FROM Submissions.PolicyCreationSource
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND IsActive = 1
ORDER BY SortOrder, SourceName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<PolicyCreationSourceDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<PolicyBindStatusDto>> GetPolicyBindStatusesAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF OBJECT_ID(N'Submissions.PolicyBindStatus', N'U') IS NULL
BEGIN
    SELECT CAST(N'00000000-0000-0000-0000-000000000004' AS UNIQUEIDENTIFIER) AS PolicyBindStatusId,
           @TenantId AS TenantId,
           N'Bound' AS StatusCode,
           N'Bound' AS StatusName,
           N'Bind transaction created the policy and completed the bind workflow.' AS Description,
           CAST(1 AS bit) AS IsTerminal,
           CAST(1 AS bit) AS CreatesPolicy,
           CAST(0 AS bit) AS IsDefault,
           CAST(1 AS bit) AS IsActive,
           40 AS SortOrder;
    RETURN;
END;

SELECT PolicyBindStatusId,
       TenantId,
       StatusCode,
       StatusName,
       Description,
       IsTerminal,
       CreatesPolicy,
       IsDefault,
       IsActive,
       SortOrder
FROM Submissions.PolicyBindStatus
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND IsActive = 1
ORDER BY SortOrder, StatusName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<PolicyBindStatusDto>(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<PolicyBindTransactionDto>> GetPolicyBindTransactionsAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
IF OBJECT_ID(N'Submissions.PolicyBindTransaction', N'U') IS NULL
BEGIN
    SELECT TOP 0
           CAST(NULL AS UNIQUEIDENTIFIER) AS PolicyBindTransactionId,
           CAST(NULL AS UNIQUEIDENTIFIER) AS TenantId,
           CAST(NULL AS UNIQUEIDENTIFIER) AS SubmissionId,
           CAST(N'' AS NVARCHAR(50)) AS SubmissionNumber,
           CAST(NULL AS UNIQUEIDENTIFIER) AS QuoteId,
           CAST(NULL AS NVARCHAR(80)) AS QuoteNumber,
           CAST(NULL AS UNIQUEIDENTIFIER) AS PolicyId,
           CAST(NULL AS NVARCHAR(80)) AS PolicyNumber,
           CAST(NULL AS UNIQUEIDENTIFIER) AS AccountId,
           CAST(N'' AS NVARCHAR(200)) AS AccountName,
           CAST(NULL AS UNIQUEIDENTIFIER) AS CarrierId,
           CAST(N'' AS NVARCHAR(200)) AS CarrierName,
           CAST(N'' AS NVARCHAR(50)) AS PolicySourceCode,
           CAST(N'' AS NVARCHAR(100)) AS PolicySourceName,
           CAST(N'' AS NVARCHAR(50)) AS BindStatusCode,
           CAST(N'' AS NVARCHAR(100)) AS BindStatusName,
           CAST(NULL AS NVARCHAR(500)) AS BindReason,
           CAST(NULL AS NVARCHAR(1000)) AS Notes,
           CAST(0 AS DECIMAL(18,2)) AS AnnualPremium,
           CAST(SYSUTCDATETIME() AS DATETIME2) AS EffectiveDate,
           CAST(SYSUTCDATETIME() AS DATETIME2) AS ExpirationDate,
           CAST(NULL AS UNIQUEIDENTIFIER) AS RequestedByUserId,
           CAST(SYSUTCDATETIME() AS DATETIME2) AS RequestedDateUtc,
           CAST(NULL AS UNIQUEIDENTIFIER) AS ApprovedByUserId,
           CAST(NULL AS DATETIME2) AS ApprovedDateUtc,
           CAST(NULL AS UNIQUEIDENTIFIER) AS BoundByUserId,
           CAST(NULL AS DATETIME2) AS BoundDateUtc,
           CAST(SYSUTCDATETIME() AS DATETIME2) AS CreatedDateUtc;
    RETURN;
END;

SELECT pbt.PolicyBindTransactionId,
       pbt.TenantId,
       pbt.SubmissionId,
       s.SubmissionNumber,
       pbt.QuoteId,
       q.QuoteNumber,
       pbt.PolicyId,
       COALESCE(bp.PolicyNumber, pbt.PolicyNumber) AS PolicyNumber,
       pbt.AccountId,
        COALESCE(a.AccountName, s.SubmissionNumber, N'Account') AS AccountName,
       pbt.CarrierId,
       COALESCE(c.CarrierName, N'Carrier') AS CarrierName,
       pbt.PolicySourceCode,
       COALESCE(pcs.SourceName, pbt.PolicySourceCode) AS PolicySourceName,
       pbt.BindStatusCode,
       COALESCE(pbs.StatusName, pbt.BindStatusCode) AS BindStatusName,
       pbt.BindReason,
       pbt.Notes,
       pbt.AnnualPremium,
       CAST(pbt.EffectiveDate AS DATETIME2) AS EffectiveDate,
       CAST(pbt.ExpirationDate AS DATETIME2) AS ExpirationDate,
       pbt.RequestedByUserId,
       pbt.RequestedDateUtc,
       pbt.ApprovedByUserId,
       pbt.ApprovedDateUtc,
       pbt.BoundByUserId,
       pbt.BoundDateUtc,
       pbt.CreatedDateUtc
FROM Submissions.PolicyBindTransaction pbt
INNER JOIN Submissions.Submission s ON s.SubmissionId = pbt.SubmissionId
LEFT JOIN Submissions.Quote q ON q.QuoteId = pbt.QuoteId AND q.IsDeleted = 0
LEFT JOIN Submissions.BoundPolicy bp ON bp.PolicyId = pbt.PolicyId AND bp.IsDeleted = 0
LEFT JOIN Client.Account a ON a.AccountId = pbt.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = pbt.CarrierId
LEFT JOIN Submissions.PolicyCreationSource pcs ON pcs.TenantId = pbt.TenantId AND pcs.SourceCode = pbt.PolicySourceCode AND pcs.IsDeleted = 0
LEFT JOIN Submissions.PolicyBindStatus pbs ON pbs.TenantId = pbt.TenantId AND pbs.StatusCode = pbt.BindStatusCode AND pbs.IsDeleted = 0
WHERE pbt.SubmissionId = @SubmissionId
  AND pbt.IsDeleted = 0
ORDER BY pbt.CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<PolicyBindTransactionDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<SubmissionActionResult> SubmitToMarketAsync(Guid id, SubmitSubmissionToMarketRequest request, CancellationToken cancellationToken = default)
    {
        var readiness = await GetReadinessAsync(id, request.TenantId, cancellationToken);
        if (!readiness.IsReadyForMarketing)
            throw new InvalidOperationException("Submission is not ready for marketing: " + string.Join("; ", readiness.BlockingReasons));

        const string sql = @"
DECLARE @CarrierId UNIQUEIDENTIFIER = COALESCE(@CarrierIdIn, (SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CarrierName));
IF @CarrierId IS NULL THROW 52000, 'No carrier is available for this tenant.', 1;

DECLARE @MarketId UNIQUEIDENTIFIER = (SELECT TOP 1 SubmissionMarketId FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND CarrierId = @CarrierId AND IsDeleted = 0);
IF @MarketId IS NULL
BEGIN
    SET @MarketId = NEWID();
    INSERT INTO Submissions.SubmissionMarket (SubmissionMarketId, SubmissionId, CarrierId, Status, AppetiteScore, IsRecommended, DeclineReason, AddedDateUtc, RespondedDateUtc, TenantId, IsDeleted)
    VALUES (@MarketId, @SubmissionId, @CarrierId, N'Submitted', 80, 1, NULL, SYSUTCDATETIME(), NULL, @TenantId, 0);
END
ELSE
BEGIN
    UPDATE Submissions.SubmissionMarket
    SET Status = N'Submitted', DeclineReason = NULL, RespondedDateUtc = NULL, TenantId = COALESCE(TenantId, @TenantId)
    WHERE SubmissionMarketId = @MarketId;
END

UPDATE Submissions.SubmissionMarket
SET SubmittedDateUtc = SYSUTCDATETIME(),
    SubmittedByUserId = NULL,
    Notes = COALESCE(@Notes, Notes),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionMarketId = @MarketId;

INSERT INTO Submissions.SubmissionMarketDocument (SubmissionMarketDocumentId, SubmissionMarketId, SubmissionId, TenantId, DocumentId, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @MarketId, @SubmissionId, @TenantId, d.DocumentId, SYSUTCDATETIME(), 0
FROM DMS.Document d
WHERE d.TenantId = @TenantId AND d.EntityName = N'Submission' AND d.EntityId = @SubmissionId AND d.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Submissions.SubmissionMarketDocument md WHERE md.SubmissionMarketId = @MarketId AND md.DocumentId = d.DocumentId AND md.IsDeleted = 0);

IF OBJECT_ID(N'Submissions.SubmissionMarketDispatch', N'U') IS NOT NULL
BEGIN
    DECLARE @DispatchChannelCode NVARCHAR(50) = N'InternalQueue';
    DECLARE @DispatchRecipient NVARCHAR(500) = NULL;
    DECLARE @DispatchSubjectTemplate NVARCHAR(300) = N'Submission {SubmissionNumber} ready for market review';
    DECLARE @DispatchMaxAttempts INT = 3;

    IF OBJECT_ID(N'Agency.CarrierSetting', N'U') IS NOT NULL
    BEGIN
        SELECT @DispatchChannelCode = COALESCE(NULLIF(carrierChannel.SettingValue, N''), NULLIF(defaultChannel.SettingValue, N''), @DispatchChannelCode),
               @DispatchRecipient = COALESCE(NULLIF(carrierEmail.SettingValue, N''), NULLIF(defaultRecipient.SettingValue, N'')),
               @DispatchSubjectTemplate = COALESCE(NULLIF(subjectTemplate.SettingValue, N''), @DispatchSubjectTemplate),
               @DispatchMaxAttempts = COALESCE(TRY_CONVERT(INT, maxAttempts.SettingValue), @DispatchMaxAttempts)
        FROM (SELECT 1 AS Seed) seed
        OUTER APPLY (SELECT TOP 1 SettingValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = @CarrierId AND SettingCode = N'SUBMIT_TO_MARKET_DEFAULT_CHANNEL' AND IsActive = 1 AND IsDeleted = 0) carrierChannel
        OUTER APPLY (SELECT TOP 1 SettingValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId IS NULL AND SettingCode = N'SUBMIT_TO_MARKET_DEFAULT_CHANNEL' AND IsActive = 1 AND IsDeleted = 0) defaultChannel
        OUTER APPLY (SELECT TOP 1 SettingValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId = @CarrierId AND SettingCode = N'SUBMIT_TO_MARKET_EMAIL' AND IsActive = 1 AND IsDeleted = 0) carrierEmail
        OUTER APPLY (SELECT TOP 1 SettingValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId IS NULL AND SettingCode = N'SUBMIT_TO_MARKET_DEFAULT_RECIPIENT' AND IsActive = 1 AND IsDeleted = 0) defaultRecipient
        OUTER APPLY (SELECT TOP 1 SettingValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId IS NULL AND SettingCode = N'SUBMIT_TO_MARKET_SUBJECT_TEMPLATE' AND IsActive = 1 AND IsDeleted = 0) subjectTemplate
        OUTER APPLY (SELECT TOP 1 SettingValue FROM Agency.CarrierSetting WHERE TenantId = @TenantId AND CarrierId IS NULL AND SettingCode = N'SUBMIT_TO_MARKET_MAX_ATTEMPTS' AND IsActive = 1 AND IsDeleted = 0) maxAttempts;
    END;

    INSERT INTO Submissions.SubmissionMarketDispatch
        (SubmissionMarketDispatchId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, DispatchChannelCode, DispatchStatusCode, Recipient, Subject, PayloadJson, AttemptCount, MaxAttemptCount, NextAttemptDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
    SELECT NEWID(), @TenantId, @SubmissionId, @MarketId, @CarrierId,
           @DispatchChannelCode,
           N'Pending',
           @DispatchRecipient,
           LEFT(REPLACE(@DispatchSubjectTemplate, N'{SubmissionNumber}', COALESCE(submission.SubmissionNumber, N'')), 300),
           CONCAT(N'{',
               N'""tenantId"":""', CONVERT(NVARCHAR(36), @TenantId), N'"",',
               N'""submissionId"":""', CONVERT(NVARCHAR(36), @SubmissionId), N'"",',
               N'""submissionMarketId"":""', CONVERT(NVARCHAR(36), @MarketId), N'"",',
               N'""carrierId"":""', CONVERT(NVARCHAR(36), @CarrierId), N'"",',
               N'""submissionNumber"":""', STRING_ESCAPE(COALESCE(submission.SubmissionNumber, N''), 'json'), N'"",',
               N'""lineOfBusiness"":""', STRING_ESCAPE(COALESCE(submission.LineOfBusiness, N''), 'json'), N'"",',
               N'""notes"":""', STRING_ESCAPE(COALESCE(@Notes, N''), 'json'), N'"",',
               N'""documentIds"":', COALESCE(documentPayload.DocumentIdsJson, N'[]'),
           N'}'),
           0, @DispatchMaxAttempts, SYSUTCDATETIME(), SYSUTCDATETIME(), NULL, 0
    FROM Submissions.Submission submission
    OUTER APPLY
    (
        SELECT CONCAT(N'[', STRING_AGG(CONCAT(N'""', CONVERT(NVARCHAR(36), d.DocumentId), N'""'), N','), N']') AS DocumentIdsJson
        FROM Submissions.SubmissionMarketDocument d
        WHERE d.SubmissionMarketId = @MarketId
          AND d.IsDeleted = 0
    ) documentPayload
    WHERE submission.SubmissionId = @SubmissionId
      AND submission.TenantId = @TenantId
      AND submission.IsDeleted = 0
      AND NOT EXISTS
      (
          SELECT 1
          FROM Submissions.SubmissionMarketDispatch existing
          WHERE existing.SubmissionMarketId = @MarketId
            AND existing.IsDeleted = 0
      );
END;

UPDATE Submissions.Submission
SET Status = CASE WHEN Status IN (N'Bound', N'Declined', N'Withdrawn') THEN Status ELSE N'Marketing' END,
    MarketCount = (SELECT COUNT(1) FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'MarketSubmitted', COALESCE(@Notes, N'Submitted to market.'), SYSUTCDATETIME(), N'SubmissionMarket', @MarketId, N'User', 0);

SELECT @MarketId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        var marketId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { SubmissionId = id, request.TenantId, CarrierIdIn = request.CarrierId, request.Notes }, cancellationToken: cancellationToken));
        await RecordOpportunityWorkflowAsync(cn, id, request.TenantId, "Marketing", "Market Submitted", "Market Submitted", request.Notes ?? "Submission package sent to market.", "SubmissionMarket", marketId, null, cancellationToken);
        return new SubmissionActionResult(marketId, "Submission sent to market.");
    }

    public async Task<SubmissionActionResult> RequestQuoteAsync(Guid id, RequestSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @CarrierId UNIQUEIDENTIFIER = COALESCE(
    (SELECT CarrierId FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND SubmissionId = @SubmissionId AND IsDeleted = 0),
    @CarrierIdIn,
    (SELECT TOP 1 CarrierId FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND IsDeleted = 0 ORDER BY IsRecommended DESC, AddedDateUtc DESC),
    (SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND IsDeleted = 0 ORDER BY CarrierName));
IF @CarrierId IS NULL THROW 52001, 'No carrier is available for quote request.', 1;

DECLARE @MarketId UNIQUEIDENTIFIER = (SELECT TOP 1 SubmissionMarketId FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND SubmissionId = @SubmissionId AND IsDeleted = 0);

IF @MarketId IS NULL
    SET @MarketId = (SELECT TOP 1 SubmissionMarketId FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND CarrierId = @CarrierId AND IsDeleted = 0 ORDER BY IsRecommended DESC, AddedDateUtc DESC);

IF @MarketId IS NULL
BEGIN
    SET @MarketId = NEWID();
    INSERT INTO Submissions.SubmissionMarket
        (SubmissionMarketId, SubmissionId, CarrierId, Status, AppetiteScore, IsRecommended, AddedDateUtc, IsDeleted, TenantId, Notes)
    VALUES
        (@MarketId, @SubmissionId, @CarrierId, N'In Review', 65, 0, SYSUTCDATETIME(), 0, @TenantId, N'Added from current market quote request.');
END;

UPDATE Submissions.SubmissionMarket
SET Status = CASE WHEN Status IN (N'Bound', N'Declined') THEN Status ELSE N'In Review' END,
    SubmittedDateUtc = COALESCE(SubmittedDateUtc, SYSUTCDATETIME()),
    SubmittedByUserId = COALESCE(SubmittedByUserId, @RequestedByUserId),
    Notes = COALESCE(@CoverageNotes, Notes),
    DueDateUtc = COALESCE(DueDateUtc, DATEADD(day, 14, SYSUTCDATETIME())),
    RequestedCoverageSummary = COALESCE(NULLIF(@CoverageNotes, N''), RequestedCoverageSummary),
    RequestedLimits = COALESCE(RequestedLimits, CONCAT(N'Deductible: ', COALESCE(CONVERT(nvarchar(50), @Deductible), N'Not specified'), N'; Limit: ', COALESCE(CONVERT(nvarchar(50), @Limit), N'Not specified'))),
    SubmissionMethodCode = COALESCE(SubmissionMethodCode, N'InternalQueue'),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0;

UPDATE Submissions.Submission
SET Status = N'Marketing',
    QuoteCount = (SELECT COUNT(1) FROM Submissions.Quote WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    TargetPremium = COALESCE(TargetPremium, @AnnualPremium),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'QuoteRequested', COALESCE(@CoverageNotes, N'Quote requested.'), SYSUTCDATETIME(), N'SubmissionMarket', @MarketId, N'User', 0);

SELECT @MarketId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        var marketId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { SubmissionId = id, request.TenantId, request.SubmissionMarketId, CarrierIdIn = request.CarrierId, request.AnnualPremium, request.Deductible, request.Limit, request.CoverageNotes, request.RequestedByUserId, request.CarrierReferenceNumber }, cancellationToken: cancellationToken));
        await RecordOpportunityWorkflowAsync(cn, id, request.TenantId, "Marketing", "Quote Requested", "Quote Requested", request.CoverageNotes ?? "Quote requested from market.", "SubmissionMarket", marketId, null, cancellationToken);
        return new SubmissionActionResult(marketId, "Quote requested from market.");
    }

    public async Task<SubmissionActionResult> CopyAsync(Guid id, CopySubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @NewSubmissionId UNIQUEIDENTIFIER = NEWID();
INSERT INTO Submissions.Submission (SubmissionId, TenantId, AccountId, OpportunityId, SubmissionNumber, LineOfBusiness, Status, Priority, AssignedToUserId, EffectiveDate, ExpirationDate, TargetPremium, MarketCount, QuoteCount, CreatedDateUtc, IsDeleted)
SELECT @NewSubmissionId, TenantId, AccountId, OpportunityId,
       N'SUB-' + CONVERT(NVARCHAR(8), SYSUTCDATETIME(), 112) + N'-' + RIGHT(REPLACE(CONVERT(NVARCHAR(36), @NewSubmissionId), N'-', N''), 6),
       COALESCE(NULLIF(@LineOfBusiness, N''), LineOfBusiness),
       N'New',
       COALESCE(NULLIF(@Priority, N''), Priority),
       AssignedToUserId,
       COALESCE(@EffectiveDate, DATEADD(year, 1, EffectiveDate)),
       DATEADD(year, 1, COALESCE(@EffectiveDate, DATEADD(year, 1, EffectiveDate))),
       TargetPremium,
       0,
       0,
       SYSUTCDATETIME(),
       0
FROM Submissions.Submission
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

IF @@ROWCOUNT = 0 THROW 52002, 'Submission was not found for copy.', 1;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @NewSubmissionId, @TenantId, N'Copy', N'Copied from source submission.', SYSUTCDATETIME(), 0);

SELECT @NewSubmissionId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var newId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { SubmissionId = id, request.TenantId, request.EffectiveDate, request.LineOfBusiness, request.Priority }, cancellationToken: cancellationToken));
        return new SubmissionActionResult(newId, "Submission copied.");
    }

    public async Task<SubmissionActionResult> DeclineAsync(Guid id, DeclineSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.Submission
SET Status = N'Declined', ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

IF @@ROWCOUNT = 0 THROW 52003, 'Submission was not found for decline.', 1;

UPDATE Submissions.SubmissionMarket
SET Status = CASE WHEN Status IN (N'Bound', N'Declined') THEN Status ELSE N'Declined' END,
    DeclineReason = COALESCE(NULLIF(DeclineReason, N''), @Reason),
    RespondedDateUtc = COALESCE(RespondedDateUtc, SYSUTCDATETIME())
WHERE SubmissionId = @SubmissionId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'Decline', @Reason, SYSUTCDATETIME(), 0);

SELECT @SubmissionId;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var declinedId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { SubmissionId = id, request.TenantId, request.Reason }, cancellationToken: cancellationToken));
        await RecordOpportunityWorkflowAsync(cn, id, request.TenantId, "Lost", "Submission Declined", "Submission Declined", request.Reason, "Submission", id, null, cancellationToken);
        return new SubmissionActionResult(declinedId, "Submission declined.");
    }

    public async Task<SubmissionActionResult> CreatePolicyAsync(Guid id, CreatePolicyFromSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var submission = await GetByIdAsync(id, cancellationToken) ?? throw new InvalidOperationException("Submission was not found for policy creation.");
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        await EnsurePolicyCreationSourceSchemaAsync(cn, request.TenantId, cancellationToken);

        var source = await GetPolicyCreationSourceSettingsAsync(cn, request.TenantId, request.PolicySourceCode, cancellationToken);
        var sourceCode = source.SourceCode;
        var sourceReason = Normalize(request.PolicySourceReason);
        var sourceNotes = Normalize(request.PolicySourceNotes);
        var policyNumber = Normalize(request.PolicyNumber);
        var effectiveDate = request.EffectiveDate ?? submission.EffectiveDate;
        var expirationDate = request.ExpirationDate ?? submission.ExpirationDate;
        if (expirationDate <= effectiveDate)
        {
            throw new InvalidOperationException("Policy expiration date must be after the effective date.");
        }

        if (source.RequiresReason && string.IsNullOrWhiteSpace(sourceReason))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a reason.");
        }

        if (source.RequiresPolicyNumber && string.IsNullOrWhiteSpace(policyNumber))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a policy number.");
        }

        QuoteComparisonDto? quote = null;
        if (request.QuoteId.HasValue)
        {
            quote = await GetQuoteByIdAsync(request.QuoteId.Value, cancellationToken);
            if (quote is null || quote.SubmissionId != id)
            {
                throw new InvalidOperationException("Selected quote was not found for this submission.");
            }
        }
        else if (source.RequiresQuote)
        {
            var quotes = await GetQuoteComparisonAsync(id, cancellationToken);
            quote = quotes
                .Where(q => q.Status is "Accepted" or "Presented" or "Selected" or "Bound")
                .OrderByDescending(q => q.Status == "Accepted")
                .ThenByDescending(q => q.IsSelected)
                .ThenByDescending(q => q.AnnualPremium)
                .FirstOrDefault();

            if (quote is null)
            {
                throw new InvalidOperationException("Create Policy with Quote Bound requires an accepted, selected, or presented quote. Use a non-quote policy source and provide a reason for direct policy creation.");
            }
        }

        var carrierId = request.CarrierId ?? quote?.CarrierId;
        if (!carrierId.HasValue)
        {
            carrierId = await cn.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(@"
SELECT TOP 1 CarrierId
FROM Submissions.SubmissionMarket
WHERE SubmissionId = @SubmissionId AND IsDeleted = 0
ORDER BY IsRecommended DESC, AddedDateUtc DESC;", new { SubmissionId = id }, cancellationToken: cancellationToken));
        }

        if (!carrierId.HasValue)
        {
            throw new InvalidOperationException("Create Policy requires a carrier. Select a carrier market or add a market before creating the policy.");
        }

        var annualPremium = request.AnnualPremium ?? quote?.AnnualPremium ?? submission.TargetPremium;
        if (annualPremium is null or <= 0)
        {
            throw new InvalidOperationException("Create Policy requires an annual premium greater than zero.");
        }

        var quoteId = quote?.QuoteId ?? Guid.Empty;
        var policyId = await BindPolicyAsync(new BindPolicyRequest(id, quoteId, request.TenantId, submission.AccountId, carrierId.Value, annualPremium.Value, effectiveDate, expirationDate, policyNumber, sourceCode, sourceReason, sourceNotes), cancellationToken);
        var message = source.RequiresQuote ? "Policy created from selected quote." : $"Policy created using {source.SourceName}.";
        return new SubmissionActionResult(policyId, message);
    }

    private sealed record PolicyCreationSourceSettings(string SourceCode, string SourceName, bool RequiresQuote, bool RequiresSubmission, bool RequiresAccount, bool RequiresReason, bool RequiresPolicyNumber, bool AllowsDirectPolicyEntry, bool IsImportSource, bool IsConversionSource);

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task EnsurePolicyCreationSourceSchemaAsync(System.Data.IDbConnection connection, Guid tenantId, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'Submissions.BoundPolicy', N'U') IS NOT NULL
BEGIN
    IF COL_LENGTH(N'Submissions.BoundPolicy', N'PolicySourceCode') IS NULL ALTER TABLE Submissions.BoundPolicy ADD PolicySourceCode NVARCHAR(50) NOT NULL CONSTRAINT DF_BoundPolicy_PolicySourceCode_Runtime DEFAULT N'QuoteBound';
    IF COL_LENGTH(N'Submissions.BoundPolicy', N'PolicySourceReason') IS NULL ALTER TABLE Submissions.BoundPolicy ADD PolicySourceReason NVARCHAR(500) NULL;
    IF COL_LENGTH(N'Submissions.BoundPolicy', N'PolicySourceNotes') IS NULL ALTER TABLE Submissions.BoundPolicy ADD PolicySourceNotes NVARCHAR(1000) NULL;
    IF COL_LENGTH(N'Submissions.BoundPolicy', N'PolicyBindTransactionId') IS NULL ALTER TABLE Submissions.BoundPolicy ADD PolicyBindTransactionId UNIQUEIDENTIFIER NULL;
END;

IF OBJECT_ID(N'Submissions.PolicyCreationSource', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.PolicyCreationSource
    (
        PolicyCreationSourceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_PolicyCreationSource_Runtime PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SourceCode NVARCHAR(50) NOT NULL,
        SourceName NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        RequiresQuote BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresQuote_Runtime DEFAULT 0,
        RequiresSubmission BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresSubmission_Runtime DEFAULT 0,
        RequiresAccount BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresAccount_Runtime DEFAULT 1,
        RequiresReason BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresReason_Runtime DEFAULT 1,
        RequiresPolicyNumber BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresPolicyNumber_Runtime DEFAULT 1,
        AllowsDirectPolicyEntry BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_AllowsDirect_Runtime DEFAULT 1,
        IsImportSource BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsImport_Runtime DEFAULT 0,
        IsConversionSource BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsConversion_Runtime DEFAULT 0,
        IsDefault BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsDefault_Runtime DEFAULT 0,
        IsActive BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsActive_Runtime DEFAULT 1,
        SortOrder INT NOT NULL CONSTRAINT DF_PolicyCreationSource_SortOrder_Runtime DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyCreationSource_Created_Runtime DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsDeleted_Runtime DEFAULT 0
    );
END;

IF COL_LENGTH(N'Submissions.PolicyCreationSource', N'RequiresSubmission') IS NULL ALTER TABLE Submissions.PolicyCreationSource ADD RequiresSubmission BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresSubmission_RuntimeB DEFAULT 0;
IF COL_LENGTH(N'Submissions.PolicyCreationSource', N'RequiresAccount') IS NULL ALTER TABLE Submissions.PolicyCreationSource ADD RequiresAccount BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_RequiresAccount_RuntimeB DEFAULT 1;
IF COL_LENGTH(N'Submissions.PolicyCreationSource', N'AllowsDirectPolicyEntry') IS NULL ALTER TABLE Submissions.PolicyCreationSource ADD AllowsDirectPolicyEntry BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_AllowsDirect_RuntimeB DEFAULT 1;
IF COL_LENGTH(N'Submissions.PolicyCreationSource', N'IsImportSource') IS NULL ALTER TABLE Submissions.PolicyCreationSource ADD IsImportSource BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsImport_RuntimeB DEFAULT 0;
IF COL_LENGTH(N'Submissions.PolicyCreationSource', N'IsConversionSource') IS NULL ALTER TABLE Submissions.PolicyCreationSource ADD IsConversionSource BIT NOT NULL CONSTRAINT DF_PolicyCreationSource_IsConversion_RuntimeB DEFAULT 0;

IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyCreationSource WHERE TenantId = @TenantId AND SourceCode = N'QuoteBound' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyCreationSource (TenantId, SourceCode, SourceName, Description, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'QuoteBound', N'Quote Bound', N'Policy is created from an accepted or selected quote.', 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 10, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyCreationSource WHERE TenantId = @TenantId AND SourceCode = N'AlreadyBound' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyCreationSource (TenantId, SourceCode, SourceName, Description, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'AlreadyBound', N'Already Bound Outside System', N'Carrier or broker already bound coverage outside the platform.', 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 20, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyCreationSource WHERE TenantId = @TenantId AND SourceCode = N'ManualEntry' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyCreationSource (TenantId, SourceCode, SourceName, Description, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'ManualEntry', N'Manual Policy Entry', N'Policy is manually entered with required audit reason and policy details.', 0, 0, 1, 1, 1, 1, 0, 0, 0, 1, 30, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyCreationSource WHERE TenantId = @TenantId AND SourceCode = N'Imported' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyCreationSource (TenantId, SourceCode, SourceName, Description, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'Imported', N'Imported Policy', N'Policy is imported from a carrier, conversion, or data migration source.', 0, 0, 1, 1, 1, 1, 1, 1, 0, 1, 40, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyCreationSource WHERE TenantId = @TenantId AND SourceCode = N'BOR' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyCreationSource (TenantId, SourceCode, SourceName, Description, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'BOR', N'Broker of Record / Takeover', N'Policy is entered after a broker-of-record or book-of-business takeover.', 0, 0, 1, 1, 1, 1, 0, 1, 0, 1, 50, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyCreationSource WHERE TenantId = @TenantId AND SourceCode = N'RenewalImport' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyCreationSource (TenantId, SourceCode, SourceName, Description, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'RenewalImport', N'Renewal Import', N'Renewal policy was imported from carrier, prior AMS, or external renewal file.', 0, 0, 1, 1, 1, 1, 1, 1, 0, 1, 60, SYSUTCDATETIME(), 0);

UPDATE Submissions.PolicyCreationSource
SET SourceName = N'Renewal Import',
    Description = N'Renewal policy was imported from carrier, prior AMS, or external renewal file.',
    RequiresQuote = 0,
    RequiresSubmission = 0,
    RequiresAccount = 1,
    RequiresReason = 1,
    RequiresPolicyNumber = 1,
    AllowsDirectPolicyEntry = 1,
    IsImportSource = 1,
    IsConversionSource = 1,
    IsDefault = 0,
    IsActive = 1,
    SortOrder = 60,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE TenantId = @TenantId
  AND SourceCode = N'RenewalImport'
  AND IsDeleted = 0;", new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    private static async Task<PolicyCreationSourceSettings> GetPolicyCreationSourceSettingsAsync(System.Data.IDbConnection connection, Guid tenantId, string? sourceCode, CancellationToken cancellationToken)
    {
        var requestedCode = Normalize(sourceCode) ?? "QuoteBound";
        var settings = await connection.QuerySingleOrDefaultAsync<PolicyCreationSourceSettings>(new CommandDefinition(@"
IF OBJECT_ID(N'Submissions.PolicyCreationSource', N'U') IS NULL
BEGIN
    SELECT @SourceCode AS SourceCode, @SourceCode AS SourceName, CAST(CASE WHEN @SourceCode = N'QuoteBound' THEN 1 ELSE 0 END AS bit) AS RequiresQuote, CAST(CASE WHEN @SourceCode = N'QuoteBound' THEN 1 ELSE 0 END AS bit) AS RequiresSubmission, CAST(1 AS bit) AS RequiresAccount, CAST(CASE WHEN @SourceCode = N'QuoteBound' THEN 0 ELSE 1 END AS bit) AS RequiresReason, CAST(CASE WHEN @SourceCode = N'QuoteBound' THEN 0 ELSE 1 END AS bit) AS RequiresPolicyNumber, CAST(CASE WHEN @SourceCode = N'QuoteBound' THEN 0 ELSE 1 END AS bit) AS AllowsDirectPolicyEntry, CAST(0 AS bit) AS IsImportSource, CAST(0 AS bit) AS IsConversionSource;
    RETURN;
END;

SELECT TOP 1 SourceCode, SourceName, RequiresQuote, RequiresSubmission, RequiresAccount, RequiresReason, RequiresPolicyNumber, AllowsDirectPolicyEntry, IsImportSource, IsConversionSource
FROM Submissions.PolicyCreationSource
WHERE TenantId = @TenantId AND SourceCode = @SourceCode AND IsDeleted = 0 AND IsActive = 1;", new { TenantId = tenantId, SourceCode = requestedCode }, cancellationToken: cancellationToken));

        return settings ?? new PolicyCreationSourceSettings("QuoteBound", "Quote Bound", true, true, true, false, false, false, false, false);
    }

    private static async Task EnsurePolicyBindTransactionSchemaAsync(System.Data.IDbConnection connection, Guid tenantId, CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'Submissions.PolicyBindStatus', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.PolicyBindStatus
    (
        PolicyBindStatusId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_PolicyBindStatus_Runtime PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        StatusCode NVARCHAR(50) NOT NULL,
        StatusName NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        IsTerminal BIT NOT NULL CONSTRAINT DF_PolicyBindStatus_IsTerminal_Runtime DEFAULT 0,
        CreatesPolicy BIT NOT NULL CONSTRAINT DF_PolicyBindStatus_CreatesPolicy_Runtime DEFAULT 0,
        IsDefault BIT NOT NULL CONSTRAINT DF_PolicyBindStatus_IsDefault_Runtime DEFAULT 0,
        IsActive BIT NOT NULL CONSTRAINT DF_PolicyBindStatus_IsActive_Runtime DEFAULT 1,
        SortOrder INT NOT NULL CONSTRAINT DF_PolicyBindStatus_SortOrder_Runtime DEFAULT 0,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyBindStatus_Created_Runtime DEFAULT SYSUTCDATETIME(),
        IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyBindStatus_IsDeleted_Runtime DEFAULT 0
    );
END;

IF OBJECT_ID(N'Submissions.PolicyBindTransaction', N'U') IS NULL
BEGIN
    CREATE TABLE Submissions.PolicyBindTransaction
    (
        PolicyBindTransactionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_PolicyBindTransaction_Runtime PRIMARY KEY DEFAULT NEWID(),
        TenantId UNIQUEIDENTIFIER NOT NULL,
        SubmissionId UNIQUEIDENTIFIER NOT NULL,
        QuoteId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_PolicyBindTransaction_QuoteId_Runtime DEFAULT '00000000-0000-0000-0000-000000000000',
        PolicyId UNIQUEIDENTIFIER NULL,
        AccountId UNIQUEIDENTIFIER NOT NULL,
        CarrierId UNIQUEIDENTIFIER NOT NULL,
        PolicySourceCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PolicyBindTransaction_Source_Runtime DEFAULT N'QuoteBound',
        BindStatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PolicyBindTransaction_Status_Runtime DEFAULT N'Bound',
        PolicyNumber NVARCHAR(80) NULL,
        AnnualPremium DECIMAL(18,2) NOT NULL,
        EffectiveDate DATE NOT NULL,
        ExpirationDate DATE NOT NULL,
        BindReason NVARCHAR(500) NULL,
        Notes NVARCHAR(1000) NULL,
        RequestedByUserId UNIQUEIDENTIFIER NULL,
        RequestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyBindTransaction_Requested_Runtime DEFAULT SYSUTCDATETIME(),
        ApprovedByUserId UNIQUEIDENTIFIER NULL,
        ApprovedDateUtc DATETIME2 NULL,
        BoundByUserId UNIQUEIDENTIFIER NULL,
        BoundDateUtc DATETIME2 NULL,
        CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PolicyBindTransaction_Created_Runtime DEFAULT SYSUTCDATETIME(),
        CreatedByUserId UNIQUEIDENTIFIER NULL,
        ModifiedDateUtc DATETIME2 NULL,
        ModifiedByUserId UNIQUEIDENTIFIER NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_PolicyBindTransaction_IsDeleted_Runtime DEFAULT 0
    );
END;

IF OBJECT_ID(N'Submissions.BoundPolicy', N'U') IS NOT NULL AND COL_LENGTH(N'Submissions.BoundPolicy', N'PolicyBindTransactionId') IS NULL
    ALTER TABLE Submissions.BoundPolicy ADD PolicyBindTransactionId UNIQUEIDENTIFIER NULL;

IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindStatus WHERE TenantId = @TenantId AND StatusCode = N'Draft' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyBindStatus (TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'Draft', N'Draft', N'Bind transaction has been started but not submitted for execution.', 0, 0, 1, 1, 10, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindStatus WHERE TenantId = @TenantId AND StatusCode = N'PendingApproval' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyBindStatus (TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'PendingApproval', N'Pending Approval', N'Bind transaction requires internal approval before policy creation.', 0, 0, 0, 1, 20, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindStatus WHERE TenantId = @TenantId AND StatusCode = N'ReadyToBind' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyBindStatus (TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'ReadyToBind', N'Ready to Bind', N'Bind transaction passed validation and is ready to create the policy.', 0, 0, 0, 1, 30, SYSUTCDATETIME(), 0);
IF NOT EXISTS (SELECT 1 FROM Submissions.PolicyBindStatus WHERE TenantId = @TenantId AND StatusCode = N'Bound' AND IsDeleted = 0)
    INSERT INTO Submissions.PolicyBindStatus (TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
    VALUES (@TenantId, N'Bound', N'Bound', N'Bind transaction created the policy and completed the bind workflow.', 1, 1, 0, 1, 40, SYSUTCDATETIME(), 0);", new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }

    // ── Markets ───────────────────────────────────────────────────────

    private const string MarketColumns = "sm.SubmissionMarketId, sm.SubmissionId, sm.CarrierId, c.CarrierName, sm.Status, sm.AppetiteScore, sm.IsRecommended, sm.DeclineReason, sm.AddedDateUtc, sm.RespondedDateUtc";

    public async Task<IReadOnlyList<SubmissionMarketDto>> GetMarketsAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT sm.SubmissionMarketId, sm.SubmissionId, sm.CarrierId, c.CarrierName,
       sm.Status, sm.AppetiteScore, sm.IsRecommended, sm.DeclineReason, sm.AddedDateUtc, sm.RespondedDateUtc,
       sm.UnderwriterName, sm.UnderwriterEmail, sm.UnderwriterPhone, sm.DueDateUtc,
       sm.RequestedCoverageSummary, sm.RequestedLimits, sm.SubmissionMethodCode, sm.FollowUpTaskId, sm.SubmittedDateUtc,
       q.QuoteId AS LatestQuoteId, q.QuoteNumber AS LatestQuoteNumber, q.Status AS LatestQuoteStatus,
       q.QuoteReceivedDateUtc AS LatestQuoteReceivedDateUtc
FROM   Submissions.SubmissionMarket sm
JOIN   Core.Carrier                 c  ON c.CarrierId = sm.CarrierId
OUTER APPLY
(
    SELECT TOP 1 QuoteId, QuoteNumber, Status, QuoteReceivedDateUtc, QuotedDateUtc
    FROM Submissions.Quote q
    WHERE q.SubmissionMarketId = sm.SubmissionMarketId
      AND q.IsDeleted = 0
    ORDER BY q.ResponseVersion DESC, q.QuoteReceivedDateUtc DESC, q.QuotedDateUtc DESC, q.CreatedDateUtc DESC
) q
WHERE  sm.SubmissionId = @SubmissionId AND sm.IsDeleted = 0
ORDER BY sm.AppetiteScore DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, null, cancellationToken);
        return (await cn.QueryAsync<SubmissionMarketDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<IReadOnlyList<SubmissionMarketDto>> GetMarketSuggestionsAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @HasAppetiteRule BIT = CASE WHEN OBJECT_ID(N'Core.AppetiteRule', N'U') IS NOT NULL THEN 1 ELSE 0 END;

DECLARE @Appetite TABLE
(
    CarrierId UNIQUEIDENTIFIER NOT NULL,
    LineOfBusiness NVARCHAR(100) NOT NULL,
    AppetiteScore INT NOT NULL
);

IF @HasAppetiteRule = 1
BEGIN
    INSERT INTO @Appetite (CarrierId, LineOfBusiness, AppetiteScore)
    EXEC(N'
        SELECT CarrierId, LineOfBusiness, AppetiteScore
        FROM Core.AppetiteRule
        WHERE IsDeleted = 0;');
END;

;WITH SubmissionContext AS
(
    SELECT SubmissionId, TenantId, LineOfBusiness
    FROM Submissions.Submission
    WHERE SubmissionId = @SubmissionId
      AND IsDeleted = 0
),
CarrierMarkets AS
(
    SELECT c.CarrierId,
           c.CarrierName,
           s.LineOfBusiness,
           COALESCE(MAX(ar.AppetiteScore), 65) AS AppetiteScore,
           CAST(CASE WHEN COALESCE(MAX(ar.AppetiteScore), 65) >= 60 THEN 1 ELSE 0 END AS bit) AS IsRecommended,
           COALESCE(MIN(linePref.SortOrder), MIN(defaultPref.SortOrder), 500) AS SortOrder
    FROM SubmissionContext s
    INNER JOIN Core.Carrier c ON c.TenantId = s.TenantId
        AND c.IsDeleted = 0
        AND c.IsActive = 1
    LEFT JOIN @Appetite ar ON ar.CarrierId = c.CarrierId
        AND ar.LineOfBusiness = s.LineOfBusiness
    LEFT JOIN Core.CarrierMarketSuggestionPreference linePref ON linePref.TenantId = s.TenantId
        AND linePref.CarrierId = c.CarrierId
        AND linePref.LineOfBusiness = s.LineOfBusiness
        AND linePref.IsActive = 1
        AND linePref.IsDeleted = 0
    LEFT JOIN Core.CarrierMarketSuggestionPreference defaultPref ON defaultPref.TenantId = s.TenantId
        AND defaultPref.CarrierId = c.CarrierId
        AND defaultPref.LineOfBusiness IS NULL
        AND defaultPref.IsActive = 1
        AND defaultPref.IsDeleted = 0
    WHERE NOT EXISTS
    (
        SELECT 1
        FROM Submissions.SubmissionMarket existing
        WHERE existing.SubmissionId = s.SubmissionId
          AND existing.CarrierId = c.CarrierId
          AND existing.IsDeleted = 0
    )
    GROUP BY c.CarrierId, c.CarrierName, s.LineOfBusiness
)
SELECT TOP 10 CarrierId,
       CarrierName,
       LineOfBusiness,
       AppetiteScore,
       IsRecommended,
       CAST(NULL AS NVARCHAR(500)) AS DeclineReason,
       SYSUTCDATETIME() AS AddedDateUtc,
       CAST(NULL AS DATETIME2) AS RespondedDateUtc,
       NEWID() AS SubmissionMarketId,
       @SubmissionId AS SubmissionId,
       N'Current Market' AS Status
FROM CarrierMarkets
ORDER BY IsRecommended DESC, AppetiteScore DESC, SortOrder, CarrierName;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, null, cancellationToken);
        return (await cn.QueryAsync<SubmissionMarketDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> AddMarketAsync(AddSubmissionMarketRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @TenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Submissions.Submission WHERE SubmissionId = @SubmissionId AND IsDeleted = 0);
IF @TenantId IS NULL THROW 52021, 'Submission was not found for market add.', 1;

INSERT INTO Submissions.SubmissionMarket
    (SubmissionMarketId, SubmissionId, CarrierId, Status, AppetiteScore, IsRecommended, AddedDateUtc, TenantId, IsDeleted)
VALUES
    (@SubmissionMarketId, @SubmissionId, @CarrierId, 'Pending', 0, 0, GETUTCDATE(), @TenantId, 0);

UPDATE Submissions.Submission
SET    MarketCount     = MarketCount + 1,
       ModifiedDateUtc = GETUTCDATE()
WHERE  SubmissionId = @SubmissionId;";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, null, cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            SubmissionMarketId = id,
            request.SubmissionId,
            request.CarrierId,
        }, cancellationToken: cancellationToken));
        return id;
    }

    public async Task UpdateMarketStatusAsync(Guid submissionMarketId, UpdateSubmissionMarketStatusRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.SubmissionMarket
SET    Status           = @Status,
       DeclineReason    = @DeclineReason,
       RespondedDateUtc = GETUTCDATE()
WHERE  SubmissionMarketId = @Id AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = submissionMarketId, request.Status, request.DeclineReason }, cancellationToken: cancellationToken));
    }

    public async Task UpdateMarketPackageAsync(UpdateSubmissionMarketPackageRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;
SELECT @SubmissionId = SubmissionId FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0;
IF @SubmissionId IS NULL THROW 52013, 'Submission market was not found.', 1;

UPDATE Submissions.SubmissionMarket
SET Status = @Status,
    ReasonCode = @ReasonCode,
    DeclineReason = CASE WHEN @Status IN (N'Declined', N'Blocked') THEN COALESCE(@Notes, DeclineReason) ELSE DeclineReason END,
    Notes = @Notes,
    NextActionDateUtc = @NextActionDateUtc,
    UnderwriterName = @UnderwriterName,
    UnderwriterEmail = @UnderwriterEmail,
    UnderwriterPhone = @UnderwriterPhone,
    DueDateUtc = @DueDateUtc,
    RequestedCoverageSummary = @RequestedCoverageSummary,
    RequestedLimits = @RequestedLimits,
    SubmissionMethodCode = @SubmissionMethodCode,
    FollowUpTaskId = @FollowUpTaskId,
    RespondedDateUtc = CASE WHEN @Status IN (N'Declined', N'Quoted', N'Blocked') THEN SYSUTCDATETIME() ELSE RespondedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE SubmissionMarketId = @SubmissionMarketId;

UPDATE Submissions.SubmissionMarketDocument SET IsDeleted = 1 WHERE SubmissionMarketId = @SubmissionMarketId;

INSERT INTO Submissions.SubmissionMarketDocument (SubmissionMarketDocumentId, SubmissionMarketId, SubmissionId, TenantId, DocumentId, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), @SubmissionMarketId, @SubmissionId, @TenantId, value, SYSUTCDATETIME(), @ModifiedByUserId, 0
FROM STRING_SPLIT(@DocumentIdsCsv, N',')
WHERE TRY_CONVERT(uniqueidentifier, value) IS NOT NULL;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, CASE WHEN @Status = N'Declined' THEN N'MarketDeclined' WHEN @Status = N'Blocked' THEN N'MarketBlocked' ELSE N'MarketUpdated' END,
        COALESCE(@Notes, CONCAT(N'Market status updated to ', @Status)), SYSUTCDATETIME(), @ModifiedByUserId, N'SubmissionMarket', @SubmissionMarketId, N'User', 0);";
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            request.TenantId,
            request.SubmissionMarketId,
            request.Status,
            request.ReasonCode,
            request.Notes,
            request.NextActionDateUtc,
            request.UnderwriterName,
            request.UnderwriterEmail,
            request.UnderwriterPhone,
            request.DueDateUtc,
            request.RequestedCoverageSummary,
            request.RequestedLimits,
            request.SubmissionMethodCode,
            request.FollowUpTaskId,
            request.ModifiedByUserId,
            DocumentIdsCsv = string.Join(',', request.DocumentIds ?? [])
        }, cancellationToken: cancellationToken));
    }

    public async Task RemoveMarketAsync(Guid submissionMarketId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE Submissions.SubmissionMarket SET IsDeleted = 1 WHERE SubmissionMarketId = @Id;

UPDATE Submissions.Submission
SET    MarketCount     = CASE WHEN MarketCount > 0 THEN MarketCount - 1 ELSE 0 END,
       ModifiedDateUtc = GETUTCDATE()
WHERE  SubmissionId = (SELECT SubmissionId FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @Id);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new { Id = submissionMarketId }, cancellationToken: cancellationToken));
    }

    // ── Quotes ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<QuoteComparisonDto>> GetQuoteComparisonAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT q.QuoteId, q.SubmissionId, q.SubmissionMarketId, q.CarrierId, c.CarrierName,
       q.QuoteNumber, q.Status, q.AnnualPremium, q.EffectiveDate, q.Deductible, q.Limit, q.CoverageForms,
       q.CommissionPercent, q.Subjectivities, q.Exclusions, q.CarrierRating, q.PaymentTerms,
       q.MinimumEarnedPremium, q.TaxesAndFees, q.BrokerFee, q.TriaIncluded,
       q.IsBindable,
       q.QuoteDocumentId, d.FileName AS QuoteDocumentFileName,
       q.IsSelected, q.IsRecommended, q.RecommendationScore, q.RecommendationReason,
       q.QuoteRequestDateUtc, q.QuoteReceivedDateUtc, q.ResponseVersion, q.ResponseSourceCode,
       q.CarrierReferenceNumber, q.RequestedByUserId, q.ReceivedByUserId,
       q.CoverageNotes, q.QuotedDateUtc, q.ExpiresDateUtc
FROM   Submissions.Quote q
JOIN   Core.Carrier      c ON c.CarrierId = q.CarrierId
LEFT JOIN DMS.Document d ON d.DocumentId = q.QuoteDocumentId AND d.IsDeleted = 0
WHERE  q.SubmissionId = @SubmissionId AND q.IsDeleted = 0
ORDER BY q.IsSelected DESC, q.RecommendationScore DESC, q.AnnualPremium ASC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, null, cancellationToken);
        return (await cn.QueryAsync<QuoteComparisonDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<QuoteComparisonDto?> GetQuoteByIdAsync(Guid quoteId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT q.QuoteId, q.SubmissionId, q.SubmissionMarketId, q.CarrierId, c.CarrierName,
       q.QuoteNumber, q.Status, q.AnnualPremium, q.EffectiveDate, q.Deductible, q.Limit, q.CoverageForms,
       q.CommissionPercent, q.Subjectivities, q.Exclusions, q.CarrierRating, q.PaymentTerms,
       q.MinimumEarnedPremium, q.TaxesAndFees, q.BrokerFee, q.TriaIncluded,
       q.IsBindable,
       q.QuoteDocumentId, d.FileName AS QuoteDocumentFileName,
       q.IsSelected, q.IsRecommended, q.RecommendationScore, q.RecommendationReason,
       q.QuoteRequestDateUtc, q.QuoteReceivedDateUtc, q.ResponseVersion, q.ResponseSourceCode,
       q.CarrierReferenceNumber, q.RequestedByUserId, q.ReceivedByUserId,
       q.CoverageNotes, q.QuotedDateUtc, q.ExpiresDateUtc
FROM   Submissions.Quote q
JOIN   Core.Carrier      c ON c.CarrierId = q.CarrierId
LEFT JOIN DMS.Document d ON d.DocumentId = q.QuoteDocumentId AND d.IsDeleted = 0
WHERE  q.QuoteId = @QuoteId AND q.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, null, cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<QuoteComparisonDto>(new CommandDefinition(sql, new { QuoteId = quoteId }, cancellationToken: cancellationToken));
    }

    public async Task<SubmissionActionResult> RecordQuoteResponseAsync(Guid submissionId, RecordSubmissionQuoteResponseRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
DECLARE @CarrierId UNIQUEIDENTIFIER;
DECLARE @QuoteId UNIQUEIDENTIFIER = COALESCE(@QuoteIdIn, NEWID());
DECLARE @ExistingQuoteId UNIQUEIDENTIFIER;

SELECT @CarrierId = CarrierId
FROM Submissions.SubmissionMarket
WHERE SubmissionMarketId = @SubmissionMarketId
  AND SubmissionId = @SubmissionId
  AND IsDeleted = 0;

IF @CarrierId IS NULL THROW 52017, 'Submission market was not found for quote response.', 1;

IF @QuoteIdIn IS NOT NULL AND NOT EXISTS (SELECT 1 FROM Submissions.Quote WHERE QuoteId = @QuoteIdIn AND SubmissionId = @SubmissionId AND SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0)
    THROW 52018, 'Quote response does not belong to the selected market request.', 1;

SET @ExistingQuoteId = (SELECT TOP 1 QuoteId FROM Submissions.Quote WHERE QuoteId = @QuoteId AND IsDeleted = 0);

IF @ExistingQuoteId IS NULL
BEGIN
    INSERT INTO Submissions.Quote
        (QuoteId, SubmissionId, SubmissionMarketId, CarrierId, QuoteNumber, Status, AnnualPremium, Deductible, [Limit], CommissionPercent,
         Subjectivities, Exclusions, CarrierRating, PaymentTerms, MinimumEarnedPremium, TaxesAndFees, BrokerFee, TriaIncluded,
         EffectiveDate, CoverageForms, IsBindable, QuoteDocumentId, CoverageNotes, QuotedDateUtc, ExpiresDateUtc, QuoteRequestDateUtc, QuoteReceivedDateUtc, ResponseVersion,
         ResponseSourceCode, CarrierReferenceNumber, ReceivedByUserId, CreatedDateUtc, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
    VALUES
        (@QuoteId, @SubmissionId, @SubmissionMarketId, @CarrierId, N'QT-' + CONVERT(NVARCHAR(8), SYSUTCDATETIME(), 112) + N'-' + RIGHT(REPLACE(CONVERT(NVARCHAR(36), @QuoteId), N'-', N''), 6),
         @Status, @AnnualPremium, @Deductible, @Limit, @CommissionPercent, @Subjectivities, @Exclusions, @CarrierRating, @PaymentTerms,
         @MinimumEarnedPremium, @TaxesAndFees, @BrokerFee, @TriaIncluded, @EffectiveDate, @CoverageForms, @IsBindable, @QuoteDocumentId, @CoverageNotes, SYSUTCDATETIME(), @ExpiresDateUtc,
         (SELECT COALESCE(SubmittedDateUtc, AddedDateUtc) FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId),
         SYSUTCDATETIME(), 1, COALESCE(NULLIF(@ResponseSourceCode, N''), N'ManualEntry'), @CarrierReferenceNumber, @ReceivedByUserId,
         SYSUTCDATETIME(), SYSUTCDATETIME(), @ReceivedByUserId, 0);
END
ELSE
BEGIN
    UPDATE Submissions.Quote
    SET Status = @Status,
        AnnualPremium = @AnnualPremium,
        Deductible = @Deductible,
        [Limit] = @Limit,
        CommissionPercent = @CommissionPercent,
        Subjectivities = @Subjectivities,
        Exclusions = @Exclusions,
        CarrierRating = @CarrierRating,
        PaymentTerms = @PaymentTerms,
        MinimumEarnedPremium = @MinimumEarnedPremium,
        TaxesAndFees = @TaxesAndFees,
        BrokerFee = @BrokerFee,
        TriaIncluded = @TriaIncluded,
        EffectiveDate = @EffectiveDate,
        CoverageForms = @CoverageForms,
        IsBindable = @IsBindable,
        QuoteDocumentId = @QuoteDocumentId,
        CoverageNotes = @CoverageNotes,
        ExpiresDateUtc = @ExpiresDateUtc,
        QuoteReceivedDateUtc = COALESCE(QuoteReceivedDateUtc, SYSUTCDATETIME()),
        ResponseVersion = ResponseVersion + 1,
        ResponseSourceCode = COALESCE(NULLIF(@ResponseSourceCode, N''), ResponseSourceCode, N'ManualEntry'),
        CarrierReferenceNumber = COALESCE(NULLIF(@CarrierReferenceNumber, N''), CarrierReferenceNumber),
        ReceivedByUserId = COALESCE(@ReceivedByUserId, ReceivedByUserId),
        ModifiedDateUtc = SYSUTCDATETIME(),
        ModifiedByUserId = @ReceivedByUserId
    WHERE QuoteId = @QuoteId;
END;

UPDATE Submissions.Quote
SET RecommendationScore = CONVERT(int, ROUND(
        (CASE WHEN NULLIF(AnnualPremium, 0) IS NULL THEN 0 ELSE 35 END) +
        (CASE WHEN CarrierRating IN (N'A++', N'A+', N'A', N'A-') THEN 20 WHEN CarrierRating LIKE N'B%' THEN 10 ELSE 5 END) +
        (CASE WHEN COALESCE(NULLIF(Subjectivities, N''), N'') = N'' THEN 15 ELSE 5 END) +
        (CASE WHEN COALESCE(CommissionPercent, 0) >= 10 THEN 10 ELSE 5 END) +
        (CASE WHEN ExpiresDateUtc > DATEADD(day, 14, SYSUTCDATETIME()) THEN 10 ELSE 2 END) +
        (CASE WHEN COALESCE(TriaIncluded, 0) = 1 THEN 10 ELSE 5 END), 0)),
    RecommendationReason = CONCAT(N'Premium, carrier rating, subjectivity burden, commission, expiration risk, and coverage breadth scored on ', CONVERT(nvarchar(10), SYSUTCDATETIME(), 120), N'.')
WHERE QuoteId = @QuoteId;

INSERT INTO Submissions.QuoteRevision
    (QuoteRevisionId, QuoteId, SubmissionId, SubmissionMarketId, TenantId, ResponseVersion, Status, AnnualPremium, Deductible, [Limit], CommissionPercent,
     TaxesAndFees, BrokerFee, MinimumEarnedPremium, EffectiveDate, ExpiresDateUtc, CoverageForms, Subjectivities, Exclusions, CarrierRating, PaymentTerms,
     IsBindable, CoverageNotes, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), QuoteId, SubmissionId, SubmissionMarketId, @TenantId, ResponseVersion, Status, AnnualPremium, Deductible, [Limit], CommissionPercent,
       TaxesAndFees, BrokerFee, MinimumEarnedPremium, EffectiveDate, ExpiresDateUtc, CoverageForms, Subjectivities, Exclusions, CarrierRating, PaymentTerms,
       IsBindable, CoverageNotes, SYSUTCDATETIME(), @ReceivedByUserId, 0
FROM Submissions.Quote
WHERE QuoteId = @QuoteId
  AND NOT EXISTS (SELECT 1 FROM Submissions.QuoteRevision existing WHERE existing.QuoteId = Submissions.Quote.QuoteId AND existing.ResponseVersion = Submissions.Quote.ResponseVersion AND existing.IsDeleted = 0);

UPDATE Submissions.SubmissionMarket
SET Status = CASE WHEN @Status IN (N'Declined', N'Rejected') THEN N'Declined' ELSE N'Quoted' END,
    RespondedDateUtc = COALESCE(RespondedDateUtc, SYSUTCDATETIME()),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ReceivedByUserId
WHERE SubmissionMarketId = @SubmissionMarketId AND IsDeleted = 0;

UPDATE Submissions.Submission
SET Status = CASE WHEN @Status IN (N'Declined', N'Rejected') THEN Status ELSE N'Quoting' END,
    QuoteCount = (SELECT COUNT(1) FROM Submissions.Quote WHERE SubmissionId = @SubmissionId AND IsDeleted = 0),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ReceivedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'QuoteResponseRecorded', CONCAT(N'Carrier quote response recorded as ', @Status, N'.'), SYSUTCDATETIME(), @ReceivedByUserId, N'Quote', @QuoteId, N'User', 0);

SELECT @QuoteId;";
        var quoteId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new
        {
            SubmissionId = submissionId,
            request.TenantId,
            request.SubmissionMarketId,
            QuoteIdIn = request.QuoteId,
            request.Status,
            request.AnnualPremium,
            request.EffectiveDate,
            request.Deductible,
            request.Limit,
            request.CommissionPercent,
            request.CoverageForms,
            request.Subjectivities,
            request.Exclusions,
            request.CarrierRating,
            request.PaymentTerms,
            request.MinimumEarnedPremium,
            request.TaxesAndFees,
            request.BrokerFee,
            request.TriaIncluded,
            request.IsBindable,
            request.QuoteDocumentId,
            request.CoverageNotes,
            request.ExpiresDateUtc,
            request.ResponseSourceCode,
            request.CarrierReferenceNumber,
            request.ReceivedByUserId
        }, cancellationToken: cancellationToken));
        await RecordOpportunityWorkflowAsync(cn, submissionId, request.TenantId, "Quoting", "Quote Response Recorded", "Quote Response Recorded", request.CoverageNotes ?? "Carrier quote response recorded.", "Quote", quoteId, request.ReceivedByUserId, cancellationToken);
        return new SubmissionActionResult(quoteId, "Carrier quote response recorded.");
    }

    public async Task UpdateQuoteAsync(Guid quoteId, UpdateSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @CarrierId UNIQUEIDENTIFIER;
DECLARE @MarketId UNIQUEIDENTIFIER;
SELECT @SubmissionId = SubmissionId, @CarrierId = CarrierId, @MarketId = SubmissionMarketId FROM Submissions.Quote WHERE QuoteId = @QuoteId AND IsDeleted = 0;
IF @SubmissionId IS NULL THROW 52014, 'Quote was not found.', 1;

IF @SubmissionMarketId IS NOT NULL
BEGIN
    IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @SubmissionMarketId AND SubmissionId = @SubmissionId AND CarrierId = @CarrierId AND IsDeleted = 0)
        THROW 52016, 'Quote market request does not match this quote.', 1;
    SET @MarketId = @SubmissionMarketId;
END;

IF @MarketId IS NULL
    SET @MarketId = (SELECT TOP 1 SubmissionMarketId FROM Submissions.SubmissionMarket WHERE SubmissionId = @SubmissionId AND CarrierId = @CarrierId AND IsDeleted = 0 ORDER BY IsRecommended DESC, AddedDateUtc DESC);

UPDATE Submissions.Quote
SET SubmissionMarketId = COALESCE(@MarketId, SubmissionMarketId),
    Status = @Status,
    AnnualPremium = @AnnualPremium,
    EffectiveDate = @EffectiveDate,
    Deductible = @Deductible,
    [Limit] = @Limit,
    CoverageForms = @CoverageForms,
    CommissionPercent = @CommissionPercent,
    Subjectivities = @Subjectivities,
    Exclusions = @Exclusions,
    CarrierRating = @CarrierRating,
    PaymentTerms = @PaymentTerms,
    MinimumEarnedPremium = @MinimumEarnedPremium,
    TaxesAndFees = @TaxesAndFees,
    BrokerFee = @BrokerFee,
    TriaIncluded = @TriaIncluded,
    IsBindable = @IsBindable,
    QuoteDocumentId = @QuoteDocumentId,
    CoverageNotes = @CoverageNotes,
    ExpiresDateUtc = @ExpiresDateUtc,
    QuoteRequestDateUtc = COALESCE(QuoteRequestDateUtc, (SELECT SubmittedDateUtc FROM Submissions.SubmissionMarket WHERE SubmissionMarketId = @MarketId), CreatedDateUtc),
    QuoteReceivedDateUtc = CASE WHEN @Status IN (N'Received', N'Presented', N'Accepted', N'Bound', N'Selected') THEN COALESCE(QuoteReceivedDateUtc, SYSUTCDATETIME()) ELSE QuoteReceivedDateUtc END,
    ResponseVersion = CASE WHEN @Status = N'Revision' THEN ResponseVersion + 1 ELSE ResponseVersion END,
    ResponseSourceCode = COALESCE(NULLIF(@ResponseSourceCode, N''), CASE WHEN @Status IN (N'Received', N'Presented', N'Accepted', N'Bound', N'Selected') THEN N'ManualEntry' ELSE ResponseSourceCode END),
    CarrierReferenceNumber = COALESCE(NULLIF(@CarrierReferenceNumber, N''), CarrierReferenceNumber),
    ReceivedByUserId = COALESCE(@ReceivedByUserId, ReceivedByUserId, CASE WHEN @Status IN (N'Received', N'Presented', N'Accepted', N'Bound', N'Selected') THEN @ModifiedByUserId ELSE NULL END),
    RecommendationScore = CONVERT(int, ROUND(
        (CASE WHEN NULLIF(@AnnualPremium, 0) IS NULL THEN 0 ELSE 35 END) +
        (CASE WHEN @CarrierRating IN (N'A++', N'A+', N'A', N'A-') THEN 20 WHEN @CarrierRating LIKE N'B%' THEN 10 ELSE 5 END) +
        (CASE WHEN COALESCE(NULLIF(@Subjectivities, N''), N'') = N'' THEN 15 ELSE 5 END) +
        (CASE WHEN COALESCE(@CommissionPercent, 0) >= 10 THEN 10 ELSE 5 END) +
        (CASE WHEN @ExpiresDateUtc > DATEADD(day, 14, SYSUTCDATETIME()) THEN 10 ELSE 2 END) +
        (CASE WHEN COALESCE(@TriaIncluded, 0) = 1 THEN 10 ELSE 5 END), 0)),
    RecommendationReason = CONCAT(N'Premium, carrier rating, subjectivity burden, commission, expiration risk, and coverage breadth scored on ', CONVERT(nvarchar(10), SYSUTCDATETIME(), 120), N'.'),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE QuoteId = @QuoteId;

UPDATE Submissions.SubmissionMarket
SET Status = CASE
        WHEN @Status IN (N'Accepted', N'Bound', N'Selected') THEN N'Quoted'
        WHEN @Status IN (N'Received', N'Presented') THEN N'Quoted'
        WHEN @Status IN (N'Declined', N'Rejected') THEN N'Declined'
        WHEN @Status IN (N'Requested', N'Revision') THEN N'In Review'
        ELSE Status
    END,
    RespondedDateUtc = CASE WHEN @Status IN (N'Received', N'Presented', N'Accepted', N'Bound', N'Selected', N'Declined', N'Rejected') THEN COALESCE(RespondedDateUtc, SYSUTCDATETIME()) ELSE RespondedDateUtc END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE SubmissionMarketId = @MarketId AND IsDeleted = 0;

UPDATE Submissions.Submission
SET Status = CASE WHEN @Status IN (N'Received', N'Presented', N'Accepted') THEN N'Quoting' ELSE Status END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'QuoteUpdated', CONCAT(N'Quote updated to ', @Status, N'.'), SYSUTCDATETIME(), @ModifiedByUserId, N'Quote', @QuoteId, N'User', 0);";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { QuoteId = quoteId, request.TenantId, request.SubmissionMarketId, request.Status, request.AnnualPremium, request.EffectiveDate, request.Deductible, request.Limit, request.CoverageForms, request.CommissionPercent, request.Subjectivities, request.Exclusions, request.CarrierRating, request.PaymentTerms, request.MinimumEarnedPremium, request.TaxesAndFees, request.BrokerFee, request.TriaIncluded, request.IsBindable, request.QuoteDocumentId, request.CoverageNotes, request.ExpiresDateUtc, request.ModifiedByUserId, request.ResponseSourceCode, request.CarrierReferenceNumber, request.ReceivedByUserId }, cancellationToken: cancellationToken));
    }

    public async Task SelectQuoteAsync(Guid submissionId, SelectSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM Submissions.Quote WHERE QuoteId = @QuoteId AND SubmissionId = @SubmissionId AND IsDeleted = 0)
    THROW 52015, 'Quote was not found for selection.', 1;

DECLARE @SelectedMarketId UNIQUEIDENTIFIER = (SELECT SubmissionMarketId FROM Submissions.Quote WHERE QuoteId = @QuoteId AND SubmissionId = @SubmissionId AND IsDeleted = 0);

UPDATE Submissions.Quote
SET IsSelected = 0,
    IsRecommended = 0,
    Status = CASE WHEN Status IN (N'Bound', N'Accepted') THEN Status ELSE N'Rejected' END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND QuoteId <> @QuoteId AND IsDeleted = 0;

UPDATE Submissions.Quote
SET IsSelected = 1,
    IsRecommended = @IsRecommended,
    Status = CASE WHEN Status = N'Bound' THEN Status ELSE N'Accepted' END,
    QuoteReceivedDateUtc = COALESCE(QuoteReceivedDateUtc, SYSUTCDATETIME()),
    ResponseSourceCode = COALESCE(NULLIF(ResponseSourceCode, N''), N'ManualEntry'),
    SelectedByUserId = @SelectedByUserId,
    SelectedDateUtc = SYSUTCDATETIME(),
    SelectionReason = @Reason,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE QuoteId = @QuoteId;

UPDATE Submissions.SubmissionMarket
SET Status = CASE WHEN Status = N'Bound' THEN Status ELSE N'Quoted' END,
    RespondedDateUtc = COALESCE(RespondedDateUtc, SYSUTCDATETIME()),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @SelectedByUserId
WHERE SubmissionMarketId = @SelectedMarketId AND IsDeleted = 0;

UPDATE sm
SET Status = CASE WHEN sm.Status IN (N'Bound', N'Declined') THEN sm.Status ELSE N'Quoted' END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @SelectedByUserId
FROM Submissions.SubmissionMarket sm
INNER JOIN Submissions.Quote q ON q.SubmissionMarketId = sm.SubmissionMarketId AND q.SubmissionId = @SubmissionId AND q.QuoteId <> @QuoteId AND q.IsDeleted = 0
WHERE sm.IsDeleted = 0;

UPDATE Submissions.Submission
SET Status = N'Proposal', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @SelectedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'QuoteSelected', @Reason, SYSUTCDATETIME(), @SelectedByUserId, N'Quote', @QuoteId, N'User', 0);";
        await cn.ExecuteAsync(new CommandDefinition(sql, new { SubmissionId = submissionId, request.TenantId, request.QuoteId, request.IsRecommended, request.Reason, request.SelectedByUserId }, cancellationToken: cancellationToken));
    }

    // ── Proposals ─────────────────────────────────────────────────────

    public async Task<ProposalDto?> GetProposalByIdAsync(Guid proposalId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT ProposalId, SubmissionId, TenantId, Title, Status, PdfUrl, HtmlContent, CreatedDateUtc, GeneratedDateUtc
FROM   Submissions.Proposal
WHERE  ProposalId = @ProposalId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<ProposalDto>(new CommandDefinition(sql, new { ProposalId = proposalId }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<ProposalWorkflowDto>> GetProposalsAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT p.ProposalId,
       p.SubmissionId,
       p.TenantId,
       p.Title,
       p.Status,
       p.DeliveryMethod,
       p.Recipient,
       p.SentDateUtc,
       p.ClientDecision,
       p.DecisionNotes,
       p.DecisionDateUtc,
       p.DocumentId,
       d.FileName AS DocumentFileName
FROM Submissions.Proposal p
LEFT JOIN DMS.Document d ON d.DocumentId = p.DocumentId AND d.IsDeleted = 0
WHERE p.SubmissionId = @SubmissionId AND p.IsDeleted = 0
ORDER BY p.CreatedDateUtc DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, null, cancellationToken);
        return (await cn.QueryAsync<ProposalWorkflowDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken))).AsList();
    }

    public async Task<Guid> GenerateProposalAsync(GenerateProposalRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @QuoteScope TABLE (QuoteId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY, SortOrder INT NOT NULL);

INSERT INTO @QuoteScope (QuoteId, SortOrder)
SELECT q.QuoteId, ROW_NUMBER() OVER (ORDER BY q.IsSelected DESC, q.IsRecommended DESC, q.RecommendationScore DESC, q.AnnualPremium ASC)
FROM Submissions.Quote q
WHERE q.SubmissionId = @SubmissionId
  AND q.IsDeleted = 0
  AND EXISTS (SELECT 1 FROM STRING_SPLIT(@QuoteIdsCsv, N',') s WHERE TRY_CONVERT(uniqueidentifier, s.value) = q.QuoteId);

IF NOT EXISTS (SELECT 1 FROM @QuoteScope)
BEGIN
    INSERT INTO @QuoteScope (QuoteId, SortOrder)
    SELECT q.QuoteId, ROW_NUMBER() OVER (ORDER BY q.IsSelected DESC, q.IsRecommended DESC, q.RecommendationScore DESC, q.AnnualPremium ASC)
    FROM Submissions.Quote q
    WHERE q.SubmissionId = @SubmissionId AND q.IsDeleted = 0;
END;

DECLARE @QuoteRows NVARCHAR(MAX);
SELECT @QuoteRows = STRING_AGG(CONCAT(N'<tr><td>', c.CarrierName, N'</td><td>', q.QuoteNumber, N'</td><td>', FORMAT(q.AnnualPremium, N'C'), N'</td><td>', COALESCE(q.CarrierRating, N''), N'</td><td>', CONVERT(nvarchar(20), q.ExpiresDateUtc, 101), N'</td><td>', COALESCE(q.CoverageNotes, N''), N'</td></tr>'), N'')
FROM @QuoteScope qs
JOIN Submissions.Quote q ON q.QuoteId = qs.QuoteId
JOIN Core.Carrier c ON c.CarrierId = q.CarrierId;

DECLARE @Html NVARCHAR(MAX) = CONCAT(
    N'<html><body><h1>', @Title, N'</h1>',
    CASE WHEN NULLIF(@CustomIntroduction, N'') IS NULL THEN N'' ELSE CONCAT(N'<p>', @CustomIntroduction, N'</p>') END,
    N'<p>Prepared proposal package for selected submission quote options.</p>',
    N'<table><thead><tr><th>Carrier</th><th>Quote</th><th>Annual Premium</th><th>Rating</th><th>Expires</th><th>Coverage Notes</th></tr></thead><tbody>',
    COALESCE(@QuoteRows, N'<tr><td colspan=""6"">No quote options were available.</td></tr>'),
    N'</tbody></table></body></html>');

INSERT INTO Submissions.Proposal
    (ProposalId, SubmissionId, TenantId, Title, Status, PdfUrl, HtmlContent, CustomIntroduction, CreatedDateUtc, GeneratedDateUtc, IsDeleted)
VALUES
    (@ProposalId, @SubmissionId, @TenantId, @Title, N'Generated', CONCAT(N'dms://proposal/', CONVERT(nvarchar(36), @ProposalId)), @Html, @CustomIntroduction, SYSUTCDATETIME(), SYSUTCDATETIME(), 0);

INSERT INTO Submissions.ProposalQuote (ProposalQuoteId, ProposalId, QuoteId, SubmissionId, TenantId, SortOrder, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @ProposalId, QuoteId, @SubmissionId, @TenantId, SortOrder, SYSUTCDATETIME(), 0
FROM @QuoteScope;

UPDATE Submissions.Submission
SET Status = N'Proposal', ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'ProposalGenerated', CONCAT(@Title, N' (', (SELECT COUNT(1) FROM @QuoteScope), N' quote option(s)).'), SYSUTCDATETIME(), N'Proposal', @ProposalId, N'User', 0);";
        var id = Guid.NewGuid();
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            ProposalId   = id,
            request.SubmissionId,
            request.TenantId,
            request.Title,
            request.CustomIntroduction,
            QuoteIdsCsv = string.Join(',', request.QuoteIds ?? []),
        }, cancellationToken: cancellationToken));
        await RecordOpportunityWorkflowAsync(cn, request.SubmissionId, request.TenantId, "Proposal", "Proposal Generated", "Proposal Generated", request.Title, "Proposal", id, null, cancellationToken);
        return id;
    }

    public async Task DeliverProposalAsync(Guid proposalId, ProposalDeliveryRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;
UPDATE Submissions.Proposal
SET Status = N'Sent',
    DeliveryMethod = @DeliveryMethod,
    Recipient = @Recipient,
    SentDateUtc = SYSUTCDATETIME(),
    SentByUserId = @SentByUserId,
    @SubmissionId = SubmissionId
WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0;

IF @SubmissionId IS NULL THROW 52016, 'Proposal was not found for delivery.', 1;

UPDATE Submissions.Submission
SET Status = CASE WHEN Status IN (N'Bound', N'Declined', N'Withdrawn') THEN Status ELSE N'Proposal Sent' END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @SentByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'ProposalDelivered', CONCAT(N'Proposal sent by ', @DeliveryMethod, N' to ', @Recipient), SYSUTCDATETIME(), @SentByUserId, N'Proposal', @ProposalId, N'User', 0);

SELECT @SubmissionId;";
        var submissionId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { ProposalId = proposalId, request.TenantId, request.DeliveryMethod, request.Recipient, request.SentByUserId }, cancellationToken: cancellationToken));
        await RecordOpportunityWorkflowAsync(cn, submissionId, request.TenantId, "Proposal", "Proposal Delivered", "Proposal Delivered", $"Proposal sent by {request.DeliveryMethod} to {request.Recipient}.", "Proposal", proposalId, request.SentByUserId, cancellationToken);
    }

    public async Task RecordProposalDecisionAsync(Guid proposalId, ProposalDecisionRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;
UPDATE Submissions.Proposal
SET Status = CASE WHEN @Decision = N'Accepted' THEN N'Accepted' WHEN @Decision = N'Rejected' THEN N'Rejected' WHEN @Decision = N'Needs revision' THEN N'Needs Revision' ELSE N'Pending Decision' END,
    ClientDecision = @Decision,
    DecisionNotes = @DecisionNotes,
    DecisionDateUtc = SYSUTCDATETIME(),
    DecidedByUserId = @DecidedByUserId,
    @SubmissionId = SubmissionId
WHERE ProposalId = @ProposalId AND TenantId = @TenantId AND IsDeleted = 0;

IF @SubmissionId IS NULL THROW 52017, 'Proposal was not found for decision.', 1;

IF @Decision = N'Accepted'
BEGIN
    UPDATE Submissions.Quote SET Status = CASE WHEN IsSelected = 1 THEN N'Accepted' ELSE N'Rejected' END WHERE SubmissionId = @SubmissionId AND IsDeleted = 0;
END;

UPDATE Submissions.Submission
SET Status = CASE
        WHEN @Decision = N'Accepted' THEN N'Bind Requested'
        WHEN @Decision = N'Rejected' THEN N'Declined'
        WHEN @Decision = N'Needs revision' THEN N'Proposal Revision'
        ELSE N'Proposal'
    END,
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @DecidedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0 AND Status NOT IN (N'Bound', N'Withdrawn');

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, CreatedByUserId, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'ProposalDecision', CONCAT(@Decision, N'. ', COALESCE(@DecisionNotes, N'')), SYSUTCDATETIME(), @DecidedByUserId, N'Proposal', @ProposalId, N'User', 0);

SELECT @SubmissionId;";
        var submissionId = await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(sql, new { ProposalId = proposalId, request.TenantId, request.Decision, request.DecisionNotes, DecidedByUserId = request.DecidedByUserId }, cancellationToken: cancellationToken));
        var stageName = string.Equals(request.Decision, "Rejected", StringComparison.OrdinalIgnoreCase) ? "Lost" : "Proposal";
        await RecordOpportunityWorkflowAsync(cn, submissionId, request.TenantId, stageName, "Proposal Decision", "Proposal Decision", $"{request.Decision}. {request.DecisionNotes}".Trim(), "Proposal", proposalId, request.DecidedByUserId, cancellationToken);
    }

    // ── Appetite ──────────────────────────────────────────────────────

    public async Task<IReadOnlyList<AppetiteMatchDto>> SearchAppetiteAsync(AppetiteSearchRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT c.CarrierId, c.CarrierName, ar.LineOfBusiness,
       ar.AppetiteScore AS MatchScore,
       CASE
           WHEN ar.AppetiteScore >= 80 THEN 'Strong'
           WHEN ar.AppetiteScore >= 60 THEN 'Moderate'
           ELSE 'Weak'
       END AS MatchLevel,
       NULL AS Notes
FROM   Core.AppetiteRule ar
JOIN   Core.Carrier      c ON c.CarrierId = ar.CarrierId AND c.IsDeleted = 0
WHERE  ar.TenantId      = @TenantId
  AND  ar.IsDeleted     = 0
  AND  ar.LineOfBusiness = @LineOfBusiness
  AND  (@State IS NULL OR @State = '' OR ar.AllowedStates LIKE '%' + @State + '%' OR ar.AllowedStates IS NULL)
ORDER BY ar.AppetiteScore DESC;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = (await cn.QueryAsync<AppetiteMatchDto>(new CommandDefinition(sql, new
        {
            request.TenantId,
            request.LineOfBusiness,
            request.State,
        }, cancellationToken: cancellationToken))).AsList();
        return rows;
    }

    // ── Bind & Issue ──────────────────────────────────────────────────

    public async Task<PagedResult<PolicyRegisterDto>> SearchPoliciesAsync(Guid tenantId, string? searchTerm, string? status, string? lineOfBusiness, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS
(
    SELECT p.PolicyId,
           p.SubmissionId,
           p.QuoteId,
           p.TenantId,
           p.AccountId,
           COALESCE(a.AccountName, s.SubmissionNumber, p.PolicyNumber) AS AccountName,
           N'Commercial' AS AccountType,
           p.CarrierId,
           COALESCE(c.CarrierName, N'Bound Carrier') AS CarrierName,
           p.PolicyNumber,
           CASE WHEN p.Status = N'Bound' THEN N'Active' ELSE p.Status END AS Status,
           COALESCE(NULLIF(s.LineOfBusiness, N''), N'General Liability') AS LineOfBusiness,
           COALESCE(NULLIF(s.Priority, N''), N'Normal') AS Priority,
           p.AnnualPremium,
           p.AnnualPremium AS WrittenPremium,
           p.EffectiveDate,
           p.ExpirationDate,
           p.BoundDateUtc,
             COALESCE(NULLIF(p.PolicySourceCode, N''), N'ManualEntry') AS PolicySourceCode,
             COALESCE(pcs.SourceName, p.PolicySourceCode, N'Manual Entry') AS PolicySourceName,
             p.PolicySourceReason,
             p.PolicySourceNotes,
             p.PolicyBindTransactionId,
             COALESCE(pbt.BindStatusCode, N'Bound') AS BindStatusCode,
             COALESCE(pbs.StatusName, pbt.BindStatusCode, N'Bound') AS BindStatusName,
           s.AssignedToUserId,
           COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS AssignedToUserName,
           COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS ProducerName,
           COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS CsrName,
           N'HQ' AS Branch,
           (SELECT COUNT(1) FROM Compliance.PolicyDocument d WHERE d.TenantId = p.TenantId AND d.IsDeleted = 0 AND d.PolicyCode = p.PolicyNumber) AS DocumentCount,
           (SELECT COUNT(1) FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0) AS ActivityCount,
           (SELECT COUNT(1) FROM Policy.PolicyEndorsement e WHERE e.TenantId = p.TenantId AND e.PolicyNumber = p.PolicyNumber AND e.IsDeleted = 0) AS EndorsementCount,
            COALESCE(NULLIF(lastRenewal.Notes, N''), CASE
                WHEN DATEDIFF(day, SYSUTCDATETIME(), p.ExpirationDate) BETWEEN 0 AND 90 THEN N'Pre-Renewal'
                WHEN p.ExpirationDate < SYSUTCDATETIME() THEN N'Expired'
                ELSE N'Not Started'
            END) AS RenewalStage,
            COALESCE(lastAction.Notes, pbt.Notes, CONCAT(N'Policy bound ', CONVERT(nvarchar(10), p.BoundDateUtc, 101), CASE WHEN s.SubmissionNumber IS NULL THEN N' from account policy intake' ELSE CONCAT(N' from submission ', s.SubmissionNumber) END)) AS LastAction
    FROM   Submissions.BoundPolicy p
    LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
    LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
    LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
    LEFT JOIN Submissions.PolicyBindTransaction pbt ON pbt.PolicyBindTransactionId = p.PolicyBindTransactionId AND pbt.IsDeleted = 0
    LEFT JOIN Submissions.PolicyCreationSource pcs ON pcs.TenantId = p.TenantId AND pcs.SourceCode = p.PolicySourceCode AND pcs.IsDeleted = 0
    LEFT JOIN Submissions.PolicyBindStatus pbs ON pbs.TenantId = p.TenantId AND pbs.StatusCode = pbt.BindStatusCode AND pbs.IsDeleted = 0
    LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
    OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 ORDER BY al.CreatedDateUtc DESC) lastAction
    OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 AND al.ActionCode = N'RenewalStage' ORDER BY al.CreatedDateUtc DESC) lastRenewal
    WHERE  p.TenantId = @TenantId
      AND  p.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = N'' OR p.PolicyNumber LIKE N'%' + @SearchTerm + N'%' OR a.AccountName LIKE N'%' + @SearchTerm + N'%' OR c.CarrierName LIKE N'%' + @SearchTerm + N'%' OR s.LineOfBusiness LIKE N'%' + @SearchTerm + N'%')
),
Filtered AS
(
    SELECT *
    FROM Cte
    WHERE (@Status IS NULL OR @Status = N'' OR Status = @Status)
      AND (@LineOfBusiness IS NULL OR @LineOfBusiness = N'' OR LineOfBusiness = @LineOfBusiness)
)
SELECT * FROM Filtered
ORDER BY BoundDateUtc DESC, ExpirationDate ASC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

;WITH Cte AS
(
    SELECT CASE WHEN p.Status = N'Bound' THEN N'Active' ELSE p.Status END AS Status,
           COALESCE(NULLIF(s.LineOfBusiness, N''), N'General Liability') AS LineOfBusiness
    FROM   Submissions.BoundPolicy p
    LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
    LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
    LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
    WHERE  p.TenantId = @TenantId
      AND  p.IsDeleted = 0
      AND  (@SearchTerm IS NULL OR @SearchTerm = N'' OR p.PolicyNumber LIKE N'%' + @SearchTerm + N'%' OR a.AccountName LIKE N'%' + @SearchTerm + N'%' OR c.CarrierName LIKE N'%' + @SearchTerm + N'%' OR s.LineOfBusiness LIKE N'%' + @SearchTerm + N'%')
),
Filtered AS
(
    SELECT *
    FROM Cte
    WHERE (@Status IS NULL OR @Status = N'' OR Status = @Status)
      AND (@LineOfBusiness IS NULL OR @LineOfBusiness = N'' OR LineOfBusiness = @LineOfBusiness)
)
SELECT COUNT(1) FROM Filtered;";

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new
        {
            TenantId       = tenantId,
            SearchTerm     = searchTerm,
            Status         = status,
            LineOfBusiness = lineOfBusiness,
            Offset         = (Math.Max(pageNumber, 1) - 1) * pageSize,
            PageSize       = pageSize,
        }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<PolicyRegisterDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<PolicyRegisterDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }

    public async Task<PolicyRegisterDto?> GetPolicyByIdAsync(Guid policyId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT TOP 1 p.PolicyId,
       p.SubmissionId,
       p.QuoteId,
       p.TenantId,
       p.AccountId,
       COALESCE(a.AccountName, s.SubmissionNumber, p.PolicyNumber) AS AccountName,
       N'Commercial' AS AccountType,
       p.CarrierId,
       COALESCE(c.CarrierName, N'Bound Carrier') AS CarrierName,
       p.PolicyNumber,
       CASE WHEN p.Status = N'Bound' THEN N'Active' ELSE p.Status END AS Status,
       COALESCE(NULLIF(s.LineOfBusiness, N''), N'General Liability') AS LineOfBusiness,
       COALESCE(NULLIF(s.Priority, N''), N'Normal') AS Priority,
       p.AnnualPremium,
       p.AnnualPremium AS WrittenPremium,
       p.EffectiveDate,
       p.ExpirationDate,
       p.BoundDateUtc,
        COALESCE(NULLIF(p.PolicySourceCode, N''), N'ManualEntry') AS PolicySourceCode,
        COALESCE(pcs.SourceName, p.PolicySourceCode, N'Manual Entry') AS PolicySourceName,
        p.PolicySourceReason,
        p.PolicySourceNotes,
        p.PolicyBindTransactionId,
        COALESCE(pbt.BindStatusCode, N'Bound') AS BindStatusCode,
        COALESCE(pbs.StatusName, pbt.BindStatusCode, N'Bound') AS BindStatusName,
       s.AssignedToUserId,
       COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS AssignedToUserName,
       COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS ProducerName,
       COALESCE(u.FullName, u.DisplayName, u.UserName, N'Tenant Admin') AS CsrName,
       N'HQ' AS Branch,
       (SELECT COUNT(1) FROM Compliance.PolicyDocument d WHERE d.TenantId = p.TenantId AND d.IsDeleted = 0 AND d.PolicyCode = p.PolicyNumber) AS DocumentCount,
       (SELECT COUNT(1) FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0) AS ActivityCount,
       (SELECT COUNT(1) FROM Policy.PolicyEndorsement e WHERE e.TenantId = p.TenantId AND e.PolicyNumber = p.PolicyNumber AND e.IsDeleted = 0) AS EndorsementCount,
       COALESCE(NULLIF(lastRenewal.Notes, N''), CASE
           WHEN DATEDIFF(day, SYSUTCDATETIME(), p.ExpirationDate) BETWEEN 0 AND 90 THEN N'Pre-Renewal'
           WHEN p.ExpirationDate < SYSUTCDATETIME() THEN N'Expired'
           ELSE N'Not Started'
       END) AS RenewalStage,
       COALESCE(lastAction.Notes, pbt.Notes, CONCAT(N'Policy bound ', CONVERT(nvarchar(10), p.BoundDateUtc, 101), CASE WHEN s.SubmissionNumber IS NULL THEN N' from account policy intake' ELSE CONCAT(N' from submission ', s.SubmissionNumber) END)) AS LastAction
FROM Submissions.BoundPolicy p
LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
LEFT JOIN Submissions.PolicyBindTransaction pbt ON pbt.PolicyBindTransactionId = p.PolicyBindTransactionId AND pbt.IsDeleted = 0
LEFT JOIN Submissions.PolicyCreationSource pcs ON pcs.TenantId = p.TenantId AND pcs.SourceCode = p.PolicySourceCode AND pcs.IsDeleted = 0
LEFT JOIN Submissions.PolicyBindStatus pbs ON pbs.TenantId = p.TenantId AND pbs.StatusCode = pbt.BindStatusCode AND pbs.IsDeleted = 0
LEFT JOIN IAM.[User] u ON u.UserId = s.AssignedToUserId
OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 ORDER BY al.CreatedDateUtc DESC) lastAction
OUTER APPLY (SELECT TOP 1 al.Notes FROM Submissions.SubmissionActionLog al WHERE al.TenantId = p.TenantId AND al.SubmissionId = p.SubmissionId AND al.IsDeleted = 0 AND al.ActionCode = N'RenewalStage' ORDER BY al.CreatedDateUtc DESC) lastRenewal
WHERE p.PolicyId = @PolicyId AND p.IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PolicyRegisterDto>(new CommandDefinition(sql, new { PolicyId = policyId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> CreatePolicyRegisterAsync(UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default)
    {
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsurePolicyCreationSourceSchemaAsync(cn, request.TenantId, cancellationToken);
        await EnsurePolicyBindTransactionSchemaAsync(cn, request.TenantId, cancellationToken);

        var source = await GetPolicyCreationSourceSettingsAsync(cn, request.TenantId, request.PolicySourceCode, cancellationToken);
        var sourceReason = Normalize(request.PolicySourceReason);
        var sourceNotes = Normalize(request.Notes);

        if (source.RequiresQuote)
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires quote-bound submission workflow. Use Create Policy from a submission quote.");
        }

        if (source.RequiresSubmission && (!request.SubmissionId.HasValue || request.SubmissionId.Value == Guid.Empty))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a parent submission.");
        }

        if (source.RequiresAccount && request.AccountId == Guid.Empty)
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires an account.");
        }

        if (source.RequiresReason && string.IsNullOrWhiteSpace(sourceReason))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a reason.");
        }

        if (source.RequiresPolicyNumber && string.IsNullOrWhiteSpace(request.PolicyNumber))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a policy number.");
        }

        if (request.ExpirationDate <= request.EffectiveDate)
        {
            throw new InvalidOperationException("Policy expiration date must be after the effective date.");
        }

        if (request.AnnualPremium <= 0)
        {
            throw new InvalidOperationException("Policy annual premium must be greater than zero.");
        }

        const string carrierSql = @"
DECLARE @CarrierId UNIQUEIDENTIFIER = (SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND CarrierName = @CarrierName AND IsDeleted = 0 ORDER BY CreatedDateUtc);
IF @CarrierId IS NULL
BEGIN
    SET @CarrierId = NEWID();
    INSERT INTO Core.Carrier (CarrierId, TenantId, CarrierCode, CarrierName, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@CarrierId, @TenantId, LEFT(REPLACE(UPPER(@CarrierName), N' ', N''), 50), @CarrierName, 1, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END;
SELECT @CarrierId;";
        var carrierId = request.CarrierId ?? await cn.ExecuteScalarAsync<Guid>(new CommandDefinition(carrierSql, new
        {
            request.TenantId,
            request.CarrierName,
            request.ModifiedByUserId,
        }, cancellationToken: cancellationToken));

        var id = await BindPolicyAsync(new BindPolicyRequest(
            SubmissionId: request.SubmissionId,
            QuoteId: request.QuoteId,
            TenantId: request.TenantId,
            AccountId: request.AccountId,
            CarrierId: carrierId,
            AnnualPremium: request.AnnualPremium,
            EffectiveDate: request.EffectiveDate,
            ExpirationDate: request.ExpirationDate,
            PolicyNumber: request.PolicyNumber,
            PolicySourceCode: source.SourceCode,
            PolicySourceReason: sourceReason ?? "Policy created from policy register.",
            PolicySourceNotes: sourceNotes,
            RequestedByUserId: request.ModifiedByUserId,
            ApprovedByUserId: request.ModifiedByUserId,
            BoundByUserId: request.ModifiedByUserId), cancellationToken);

        return id;
    }

    public async Task UpdatePolicyRegisterAsync(Guid policyId, UpsertPolicyRegisterRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @CarrierId UNIQUEIDENTIFIER = (SELECT TOP 1 CarrierId FROM Core.Carrier WHERE TenantId = @TenantId AND CarrierName = @CarrierName AND IsDeleted = 0 ORDER BY CreatedDateUtc);
IF @CarrierId IS NULL
BEGIN
    SET @CarrierId = NEWID();
    INSERT INTO Core.Carrier (CarrierId, TenantId, CarrierCode, CarrierName, IsActive, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (@CarrierId, @TenantId, LEFT(REPLACE(UPPER(@CarrierName), N' ', N''), 50), @CarrierName, 1, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END;

UPDATE Submissions.BoundPolicy
SET PolicyNumber = @PolicyNumber,
    CarrierId = @CarrierId,
    Status = CASE WHEN @Status = N'Active' THEN N'Bound' ELSE @Status END,
    AnnualPremium = @AnnualPremium,
    EffectiveDate = @EffectiveDate,
    ExpirationDate = @ExpirationDate,
    @SubmissionId = SubmissionId
WHERE PolicyId = @PolicyId AND TenantId = @TenantId AND IsDeleted = 0;

UPDATE Submissions.Submission
SET AccountId = @AccountId,
    LineOfBusiness = @LineOfBusiness,
    Status = CASE WHEN @Status = N'Active' THEN N'Bound' ELSE @Status END,
    EffectiveDate = @EffectiveDate,
    ExpirationDate = @ExpirationDate,
    TargetPremium = NULLIF(@AnnualPremium, 0),
    ModifiedDateUtc = SYSUTCDATETIME(),
    ModifiedByUserId = @ModifiedByUserId
WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
VALUES (NEWID(), @SubmissionId, @TenantId, N'PolicyUpdated', CONCAT(N'Policy edited from register. ', COALESCE(@Notes, N'')), SYSUTCDATETIME(), 0);";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PolicyId = policyId,
            request.TenantId,
            request.AccountId,
            request.PolicyNumber,
            request.CarrierName,
            request.LineOfBusiness,
            request.Status,
            request.EffectiveDate,
            request.ExpirationDate,
            request.AnnualPremium,
            request.Notes,
            request.ModifiedByUserId,
        }, cancellationToken: cancellationToken));
    }

    public async Task<SubmissionActionResult> ExecutePolicyRegisterActionAsync(Guid policyId, PolicyRegisterActionRequest request, CancellationToken cancellationToken = default)
    {
        const string sql = @"
DECLARE @SubmissionId UNIQUEIDENTIFIER;
DECLARE @QuoteId UNIQUEIDENTIFIER;
DECLARE @AccountId UNIQUEIDENTIFIER;
DECLARE @CarrierId UNIQUEIDENTIFIER;
DECLARE @PolicyNumber NVARCHAR(50);
DECLARE @AccountName NVARCHAR(200);
DECLARE @LineOfBusiness NVARCHAR(100);
DECLARE @CarrierName NVARCHAR(200);
DECLARE @AnnualPremium DECIMAL(18,2);
DECLARE @EffectiveDate DATETIME2;
DECLARE @ExpirationDate DATETIME2;

SELECT @SubmissionId = p.SubmissionId,
       @QuoteId = p.QuoteId,
       @AccountId = p.AccountId,
       @CarrierId = p.CarrierId,
       @PolicyNumber = p.PolicyNumber,
       @AccountName = COALESCE(a.AccountName, p.PolicyNumber),
       @LineOfBusiness = COALESCE(NULLIF(s.LineOfBusiness, N''), N'General Liability'),
       @CarrierName = COALESCE(c.CarrierName, N'Carrier'),
       @AnnualPremium = p.AnnualPremium,
       @EffectiveDate = p.EffectiveDate,
       @ExpirationDate = p.ExpirationDate
FROM Submissions.BoundPolicy p
LEFT JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.IsDeleted = 0
LEFT JOIN Client.Account a ON a.AccountId = p.AccountId
LEFT JOIN Core.Carrier c ON c.CarrierId = p.CarrierId
WHERE p.PolicyId = @PolicyId AND p.TenantId = @TenantId AND p.IsDeleted = 0;

IF @PolicyNumber IS NULL THROW 51000, 'Policy was not found.', 1;

DECLARE @ActionCode NVARCHAR(80) = REPLACE(@Action, N' ', N'');
DECLARE @Message NVARCHAR(500) = CONCAT(@Action, N' completed for ', @PolicyNumber, N'.');

IF @Action = N'Cancel Policy'
BEGIN
    UPDATE Submissions.BoundPolicy SET Status = N'Cancelled' WHERE PolicyId = @PolicyId AND TenantId = @TenantId AND IsDeleted = 0;
    UPDATE Submissions.Submission SET Status = N'Cancelled', ModifiedDateUtc = SYSUTCDATETIME(), ModifiedByUserId = @ModifiedByUserId WHERE SubmissionId = @SubmissionId AND TenantId = @TenantId AND IsDeleted = 0;
    INSERT INTO Policy.PolicyCancellation (CancellationId, TenantId, PolicyId, AccountId, CancellationNumber, PolicyNumber, AccountName, LineOfBusiness, Carrier, CancellationReason, CancellationType, RequestType, RequestDateUtc, EffectiveDate, CancellationDate, ReturnPremium, PremiumDue, Status, Priority, RequestedByName, AssignedToName, Notes, WorkflowStage, DueDate, IsUrgent, IsArchived, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @PolicyId, @AccountId, CONCAT(N'CAN-', FORMAT(SYSUTCDATETIME(), N'yyyy'), N'-', FORMAT(ISNULL((SELECT COUNT(1) + 1 FROM Policy.PolicyCancellation WHERE TenantId = @TenantId), 1), N'0000')), @PolicyNumber, @AccountName, @LineOfBusiness, @CarrierName, COALESCE(NULLIF(@Notes, N''), N'Policy cancelled from register'), N'Pro-Rata', N'Cancellation', SYSUTCDATETIME(), COALESCE(@ActionDate, SYSUTCDATETIME()), COALESCE(@ActionDate, SYSUTCDATETIME()), 0, 0, N'Pending', N'Normal', N'Current User', N'Current User', @Notes, N'Cancellation Intake', DATEADD(day, 7, SYSUTCDATETIME()), 0, 0, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END
ELSE IF @Action = N'Renew'
BEGIN
    DECLARE @RenewalPolicyId UNIQUEIDENTIFIER = NEWID();
    DECLARE @RenewalEffective DATETIME2 = COALESCE(@ActionDate, @ExpirationDate);
    DECLARE @RenewalPremium DECIMAL(18,2) = COALESCE(NULLIF(@Premium, 0), @AnnualPremium);

    IF @SubmissionId IS NULL
    BEGIN
        DECLARE @RenewalSourceCode NVARCHAR(50) = N'RenewalImport';
        DECLARE @RenewalPolicyNumber NVARCHAR(80) = CONCAT(@PolicyNumber, N'-REN-', FORMAT(GETUTCDATE(), 'yyMMdd'));
        DECLARE @RenewalReason NVARCHAR(500) = LEFT(COALESCE(NULLIF(@Notes, N''), CONCAT(N'Direct account-origin renewal created from ', @PolicyNumber, N'.')), 500);
        DECLARE @RenewalBindTransactionId UNIQUEIDENTIFIER = NEWID();

        IF NOT EXISTS
        (
            SELECT 1
            FROM Submissions.PolicyCreationSource
            WHERE TenantId = @TenantId
              AND SourceCode = @RenewalSourceCode
              AND RequiresQuote = 0
              AND RequiresSubmission = 0
              AND AllowsDirectPolicyEntry = 1
              AND IsActive = 1
              AND IsDeleted = 0
        )
            THROW 51000, 'Direct policy renewal source configuration is missing or inactive.', 1;

        INSERT INTO Submissions.PolicyBindTransaction
            (PolicyBindTransactionId, TenantId, SubmissionId, QuoteId, PolicyId, AccountId, CarrierId,
             PolicySourceCode, BindStatusCode, PolicyNumber, AnnualPremium, EffectiveDate, ExpirationDate,
             BindReason, Notes, RequestedByUserId, RequestedDateUtc, ApprovedByUserId, ApprovedDateUtc,
             BoundByUserId, BoundDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
        VALUES
            (@RenewalBindTransactionId, @TenantId, NULL, NULL, @RenewalPolicyId, @AccountId, @CarrierId,
             @RenewalSourceCode, N'Bound', @RenewalPolicyNumber, @RenewalPremium, @RenewalEffective, DATEADD(year, 1, @RenewalEffective),
             @RenewalReason, @Notes, @ModifiedByUserId, SYSUTCDATETIME(), @ModifiedByUserId, SYSUTCDATETIME(),
             @ModifiedByUserId, SYSUTCDATETIME(), SYSUTCDATETIME(), @ModifiedByUserId, 0);

        INSERT INTO Submissions.BoundPolicy
            (PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId, PolicyNumber, Status, AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc, PolicySourceCode, PolicySourceReason, PolicySourceNotes, PolicyBindTransactionId, IsDeleted)
        VALUES
            (@RenewalPolicyId, NULL, NULL, @TenantId, @AccountId, @CarrierId, @RenewalPolicyNumber, N'Bound', @RenewalPremium, @RenewalEffective, DATEADD(year, 1, @RenewalEffective), SYSUTCDATETIME(), @RenewalSourceCode, @RenewalReason, @Notes, @RenewalBindTransactionId, 0);
    END
    ELSE
    BEGIN
        DECLARE @RenewalQuoteId UNIQUEIDENTIFIER = NEWID();
        INSERT INTO Submissions.Quote (QuoteId, SubmissionId, CarrierId, QuoteNumber, Status, AnnualPremium, CoverageNotes, QuotedDateUtc, ExpiresDateUtc, CreatedDateUtc, IsDeleted)
        VALUES (@RenewalQuoteId, @SubmissionId, @CarrierId, CONCAT(N'QT-REN-', FORMAT(GETUTCDATE(), 'yyyyMMdd'), N'-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), @RenewalPolicyId), N'-', N''), 6)), N'Presented', @RenewalPremium, @Notes, SYSUTCDATETIME(), DATEADD(day, 30, SYSUTCDATETIME()), SYSUTCDATETIME(), 0);
        INSERT INTO Submissions.BoundPolicy (PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId, PolicyNumber, Status, AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc, IsDeleted)
        VALUES (@RenewalPolicyId, @SubmissionId, @RenewalQuoteId, @TenantId, @AccountId, @CarrierId, CONCAT(@PolicyNumber, N'-REN-', FORMAT(GETUTCDATE(), 'yyMMdd')), N'Pending', @RenewalPremium, @RenewalEffective, DATEADD(year, 1, @RenewalEffective), SYSUTCDATETIME(), 0);
    END

    SET @Message = CONCAT(N'Renewal policy created for ', @PolicyNumber, N'.');
END
ELSE IF @Action = N'Endorse'
BEGIN
    INSERT INTO Policy.PolicyEndorsement (EndorsementId, TenantId, PolicyId, AccountId, EndorsementNumber, PolicyNumber, AccountName, LineOfBusiness, Carrier, EndorsementType, Description, EffectiveDate, RequestedDateUtc, PremiumDelta, Status, Priority, RequestedByName, AssignedToName, WorkflowStage, IsUrgent, IsArchived, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @PolicyId, @AccountId, CONCAT(N'END-', FORMAT(SYSUTCDATETIME(), N'yyyy'), N'-', FORMAT(ISNULL((SELECT COUNT(1) + 1 FROM Policy.PolicyEndorsement WHERE TenantId = @TenantId), 1), N'0000')), @PolicyNumber, @AccountName, @LineOfBusiness, @CarrierName, N'Change Endorsement', COALESCE(NULLIF(@Notes, N''), N'Policy endorsement requested from register'), COALESCE(@ActionDate, SYSUTCDATETIME()), COALESCE(@ActionDate, SYSUTCDATETIME()), COALESCE(@Premium, 0), N'Pending', N'Normal', N'Current User', N'Current User', N'Intake', 0, 0, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END
ELSE IF @Action = N'Add Document'
BEGIN
    INSERT INTO Compliance.PolicyDocument (PolicyDocumentId, TenantId, PolicyCode, PolicyTitle, PolicyTypeCode, Version, EffectiveDateUtc, IsActive, StatusCode, Description, CreatedDateUtc, CreatedByUserId, IsDeleted)
    VALUES (NEWID(), @TenantId, @PolicyNumber, COALESCE(NULLIF(@DocumentTitle, N''), CONCAT(N'Policy Document - ', @PolicyNumber)), N'Policy', N'1.0', COALESCE(@ActionDate, SYSUTCDATETIME()), 1, N'Published', @Notes, SYSUTCDATETIME(), @ModifiedByUserId, 0);
END

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @SubmissionId, @TenantId, @ActionCode, COALESCE(NULLIF(@Notes, N''), @Message), SYSUTCDATETIME(), 0
WHERE @SubmissionId IS NOT NULL;

SELECT @Message;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsurePolicyCreationSourceSchemaAsync(cn, request.TenantId, cancellationToken);
        await EnsurePolicyBindTransactionSchemaAsync(cn, request.TenantId, cancellationToken);
        var message = await cn.QuerySingleAsync<string>(new CommandDefinition(sql, new
        {
            PolicyId = policyId,
            request.TenantId,
            request.Action,
            ActionDate = request.EffectiveDate,
            request.Premium,
            request.DocumentTitle,
            request.Notes,
            request.ModifiedByUserId,
        }, cancellationToken: cancellationToken));
        return new SubmissionActionResult(policyId, message);
    }

    public async Task<PolicyBindDto?> GetPolicyBySubmissionAsync(Guid submissionId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId,
       PolicyNumber, Status, AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc
FROM   Submissions.BoundPolicy
WHERE  SubmissionId = @SubmissionId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<PolicyBindDto>(new CommandDefinition(sql, new { SubmissionId = submissionId }, cancellationToken: cancellationToken));
    }

    public async Task<Guid> BindPolicyAsync(BindPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var submissionId = request.SubmissionId is { } sid && sid != Guid.Empty ? sid : (Guid?)null;
        var quoteId = request.QuoteId is { } qid && qid != Guid.Empty ? qid : (Guid?)null;
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureEnterpriseWorkflowSchemaAsync(cn, request.TenantId, cancellationToken);
        await EnsurePolicyCreationSourceSchemaAsync(cn, request.TenantId, cancellationToken);
        await EnsurePolicyBindTransactionSchemaAsync(cn, request.TenantId, cancellationToken);

        var source = await GetPolicyCreationSourceSettingsAsync(cn, request.TenantId, request.PolicySourceCode, cancellationToken);
        if (source.RequiresAccount && request.AccountId == Guid.Empty)
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires an account.");
        }

        if (source.RequiresSubmission && !submissionId.HasValue)
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a parent submission.");
        }

        if (source.RequiresQuote && !quoteId.HasValue)
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a quote.");
        }

        if (source.RequiresReason && string.IsNullOrWhiteSpace(request.PolicySourceReason))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a reason.");
        }

        if (source.RequiresPolicyNumber && string.IsNullOrWhiteSpace(request.PolicyNumber))
        {
            throw new InvalidOperationException($"Policy source '{source.SourceName}' requires a policy number.");
        }

        if (request.ExpirationDate <= request.EffectiveDate)
        {
            throw new InvalidOperationException("Policy expiration date must be after the effective date.");
        }

        if (request.AnnualPremium <= 0)
        {
            throw new InvalidOperationException("Policy annual premium must be greater than zero.");
        }

        const string sql = @"
DECLARE @PolicyBindTransactionId UNIQUEIDENTIFIER = NEWID();
DECLARE @RequestedDateUtc DATETIME2 = SYSUTCDATETIME();
DECLARE @BoundDateUtc DATETIME2 = SYSUTCDATETIME();

INSERT INTO Submissions.PolicyBindTransaction
    (PolicyBindTransactionId, TenantId, SubmissionId, QuoteId, PolicyId, AccountId, CarrierId,
     PolicySourceCode, BindStatusCode, PolicyNumber, AnnualPremium, EffectiveDate, ExpirationDate,
     BindReason, Notes, RequestedByUserId, RequestedDateUtc, ApprovedByUserId, ApprovedDateUtc,
     BoundByUserId, BoundDateUtc, CreatedDateUtc, CreatedByUserId, IsDeleted)
VALUES
    (@PolicyBindTransactionId, @TenantId, @SubmissionId, @QuoteId, @PolicyId, @AccountId, @CarrierId,
     @PolicySourceCode, @BindStatusCode, @PolicyNumber, @AnnualPremium, @EffectiveDate, @ExpirationDate,
     @PolicySourceReason, @PolicySourceNotes, @RequestedByUserId, @RequestedDateUtc, @ApprovedByUserId,
     CASE WHEN @ApprovedByUserId IS NULL THEN NULL ELSE @RequestedDateUtc END,
     @BoundByUserId, CASE WHEN @BindStatusCode = N'Bound' THEN @BoundDateUtc ELSE NULL END, @RequestedDateUtc, @RequestedByUserId, 0);

INSERT INTO Submissions.BoundPolicy
    (PolicyId, SubmissionId, QuoteId, TenantId, AccountId, CarrierId,
     PolicyNumber, Status, AnnualPremium, EffectiveDate, ExpirationDate, BoundDateUtc, PolicySourceCode, PolicySourceReason, PolicySourceNotes, PolicyBindTransactionId, IsDeleted)
VALUES
    (@PolicyId, @SubmissionId, @QuoteId, @TenantId, @AccountId, @CarrierId,
     COALESCE(NULLIF(@PolicyNumber, N''), 'POL-' + FORMAT(GETUTCDATE(), 'yyyyMMdd') + '-' + RIGHT('00000' + CAST(NEXT VALUE FOR Submissions.PolicySeq AS VARCHAR), 5)),
     'Bound', @AnnualPremium, @EffectiveDate, @ExpirationDate, @BoundDateUtc, @PolicySourceCode, @PolicySourceReason, @PolicySourceNotes, @PolicyBindTransactionId, 0);

UPDATE pbt
SET PolicyNumber = bp.PolicyNumber,
    ModifiedDateUtc = SYSUTCDATETIME()
FROM Submissions.PolicyBindTransaction pbt
INNER JOIN Submissions.BoundPolicy bp ON bp.PolicyId = pbt.PolicyId
WHERE pbt.PolicyBindTransactionId = @PolicyBindTransactionId;

UPDATE Submissions.Submission
SET    Status          = 'Bound',
       ModifiedDateUtc = GETUTCDATE()
WHERE  SubmissionId = @SubmissionId AND @SubmissionId IS NOT NULL;";
        const string postBindSql = @"
UPDATE Submissions.Quote
SET Status = CASE WHEN QuoteId = @QuoteId THEN N'Bound' ELSE N'Rejected' END,
    IsSelected = CASE WHEN QuoteId = @QuoteId THEN 1 ELSE 0 END,
    IsRecommended = CASE WHEN QuoteId = @QuoteId THEN 1 ELSE 0 END,
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND IsDeleted = 0 AND @SubmissionId IS NOT NULL AND @QuoteId IS NOT NULL AND @QuoteId <> '00000000-0000-0000-0000-000000000000';

UPDATE Submissions.SubmissionMarket
SET Status = CASE WHEN CarrierId = @CarrierId THEN N'Bound' ELSE CASE WHEN Status IN (N'Declined', N'Blocked') THEN Status ELSE N'Not Selected' END END,
    RespondedDateUtc = COALESCE(RespondedDateUtc, SYSUTCDATETIME()),
    ModifiedDateUtc = SYSUTCDATETIME()
WHERE SubmissionId = @SubmissionId AND IsDeleted = 0 AND @SubmissionId IS NOT NULL;

INSERT INTO Submissions.SubmissionActionLog (ActionLogId, SubmissionId, TenantId, ActionCode, Notes, CreatedDateUtc, RelatedEntityName, RelatedEntityId, ActionSource, IsDeleted)
SELECT NEWID(), @SubmissionId, @TenantId, N'PolicyBound', CONCAT(N'Policy created. Source: ', @PolicySourceCode, N'. ', COALESCE(@PolicySourceReason, N''), CASE WHEN NULLIF(@PolicySourceNotes, N'') IS NULL THEN N'' ELSE CONCAT(N' Notes: ', @PolicySourceNotes) END), SYSUTCDATETIME(), CASE WHEN @QuoteId IS NULL OR @QuoteId = '00000000-0000-0000-0000-000000000000' THEN N'Policy' ELSE N'Quote' END, CASE WHEN @QuoteId IS NULL OR @QuoteId = '00000000-0000-0000-0000-000000000000' THEN @PolicyId ELSE @QuoteId END, N'User', 0
WHERE @SubmissionId IS NOT NULL;

INSERT INTO OPS.TaskItem (TaskItemId, TenantId, TaskNumber, Title, Description, TaskTypeCode, StageCode, PriorityCode, StatusCode, RelatedEntityName, RelatedEntityId, AccountId, DueDate, CreatedDateUtc, IsDeleted)
SELECT NEWID(), @TenantId, CONCAT(N'TASK-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', RIGHT(REPLACE(CONVERT(NVARCHAR(36), NEWID()), N'-', N''), 6)),
       v.Title, v.Description, N'DocumentCollection', N'PostBind', N'High', N'Open', CASE WHEN @SubmissionId IS NULL THEN N'Policy' ELSE N'Submission' END, COALESCE(@SubmissionId, @PolicyId), @AccountId, DATEADD(day, 7, CONVERT(date, SYSUTCDATETIME())), SYSUTCDATETIME(), 0
FROM (VALUES (N'Collect binder', N'Attach the binder document.'), (N'Collect policy', N'Attach issued policy.'), (N'Collect invoice', N'Attach invoice.'), (N'Collect certificates', N'Attach certificates.'), (N'Collect evidence of insurance', N'Attach evidence of insurance.'), (N'Collect endorsements', N'Attach required endorsements.')) v(Title, Description);";
        var id = Guid.NewGuid();
        await cn.ExecuteAsync(new CommandDefinition(sql, new
        {
            PolicyId       = id,
            SubmissionId = submissionId,
            QuoteId = quoteId,
            request.TenantId,
            request.AccountId,
            request.CarrierId,
            request.AnnualPremium,
            request.EffectiveDate,
            request.ExpirationDate,
            request.PolicyNumber,
            PolicySourceCode = source.SourceCode,
            request.PolicySourceReason,
            request.PolicySourceNotes,
            request.RequestedByUserId,
            request.ApprovedByUserId,
            request.BoundByUserId,
            request.BindStatusCode,
        }, cancellationToken: cancellationToken));
        await cn.ExecuteAsync(new CommandDefinition(postBindSql, new { SubmissionId = submissionId, QuoteId = quoteId, request.TenantId, request.AccountId, request.CarrierId }, cancellationToken: cancellationToken));
        if (submissionId.HasValue)
        {
            await RecordOpportunityWorkflowAsync(cn, submissionId.Value, request.TenantId, "Won", "Policy Bound", "Policy Bound", "Policy bound from selected quote.", "BoundPolicy", id, null, cancellationToken);
        }
        return id;
    }
}

public sealed class SubmissionReferenceOptionRepository : ISubmissionReferenceOptionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public SubmissionReferenceOptionRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<SubmissionReferenceOptionDto>> GetAllAsync(Guid tenantId, string? optionGroup = null, CancellationToken cancellationToken = default)
    {
        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        await EnsureReferenceDataAsync(connection, tenantId, cancellationToken);

        const string sql = @"
SELECT SubmissionReferenceOptionId, TenantId, OptionGroup, OptionCode, OptionName, Description,
       IsDefault, IsActive, SortOrder, CreatedDateUtc
FROM Submissions.SubmissionReferenceOption
WHERE TenantId = @TenantId
  AND IsDeleted = 0
  AND (@OptionGroup IS NULL OR @OptionGroup = '' OR OptionGroup = @OptionGroup)
ORDER BY OptionGroup, SortOrder, OptionName;";

        var items = await connection.QueryAsync<SubmissionReferenceOptionDto>(new CommandDefinition(sql, new
        {
            TenantId = tenantId,
            OptionGroup = optionGroup,
        }, cancellationToken: cancellationToken));
        return items.AsList();
    }

    private static async Task EnsureReferenceDataAsync(System.Data.IDbConnection connection, Guid tenantId, CancellationToken cancellationToken)
    {
        const string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'Submissions') EXEC('CREATE SCHEMA Submissions');

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('Submissions.SubmissionReferenceOption'))
CREATE TABLE Submissions.SubmissionReferenceOption (
    SubmissionReferenceOptionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    TenantId                    UNIQUEIDENTIFIER NOT NULL,
    OptionGroup                 NVARCHAR(50)     NOT NULL,
    OptionCode                  NVARCHAR(100)    NOT NULL,
    OptionName                  NVARCHAR(150)    NOT NULL,
    Description                 NVARCHAR(500)    NULL,
    IsDefault                   BIT              NOT NULL DEFAULT 0,
    IsActive                    BIT              NOT NULL DEFAULT 1,
    SortOrder                   INT              NOT NULL DEFAULT 0,
    CreatedDateUtc              DATETIME2        NOT NULL DEFAULT SYSUTCDATETIME(),
    ModifiedDateUtc             DATETIME2        NULL,
    IsDeleted                   BIT              NOT NULL DEFAULT 0,
    CONSTRAINT UQ_SubmissionReferenceOption_Tenant_Group_Code UNIQUE (TenantId, OptionGroup, OptionCode)
);

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'SubmissionStatus' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'SubmissionStatus', 'New', 'New', 'New submission intake record.', 1, 10),
        (@TenantId, 'SubmissionStatus', 'In Review', 'In Review', 'Submission is in underwriting or carrier review.', 0, 20),
        (@TenantId, 'SubmissionStatus', 'Quoted', 'Quoted', 'Submission has one or more quotes.', 0, 30),
        (@TenantId, 'SubmissionStatus', 'Bound', 'Bound', 'Submission has been bound into policy workflow.', 0, 40),
        (@TenantId, 'SubmissionStatus', 'Declined', 'Declined', 'Submission was declined by underwriting or market.', 0, 80),
        (@TenantId, 'SubmissionStatus', 'Withdrawn', 'Withdrawn', 'Submission was withdrawn by client or producer.', 0, 90);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'LineOfBusiness' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'LineOfBusiness', 'General Liability', 'General Liability', 'Commercial general liability placement.', 1, 10),
        (@TenantId, 'LineOfBusiness', 'Commercial Property', 'Commercial Property', 'Commercial property placement.', 0, 20),
        (@TenantId, 'LineOfBusiness', 'Commercial Auto', 'Commercial Auto', 'Commercial automobile placement.', 0, 30),
        (@TenantId, 'LineOfBusiness', 'Workers Comp', 'Workers Comp', 'Workers compensation placement.', 0, 40),
        (@TenantId, 'LineOfBusiness', 'Umbrella / Excess', 'Umbrella / Excess', 'Umbrella or excess liability placement.', 0, 50),
        (@TenantId, 'LineOfBusiness', 'Professional Liability', 'Professional Liability', 'Professional liability placement.', 0, 60),
        (@TenantId, 'LineOfBusiness', 'Home / Dwelling', 'Home / Dwelling', 'Personal home or dwelling placement.', 0, 70),
        (@TenantId, 'LineOfBusiness', 'Personal Auto', 'Personal Auto', 'Personal automobile placement.', 0, 80);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'ApplicationStatus' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'ApplicationStatus', 'Draft', 'Draft', 'Application package is being drafted.', 1, 10),
        (@TenantId, 'ApplicationStatus', 'Submitted', 'Submitted', 'Application has been submitted.', 0, 20),
        (@TenantId, 'ApplicationStatus', 'Under Review', 'Under Review', 'Application is under review.', 0, 30),
        (@TenantId, 'ApplicationStatus', 'Requirements Pending', 'Requirements Pending', 'Additional requirements are pending.', 0, 40),
        (@TenantId, 'ApplicationStatus', 'Approved', 'Approved', 'Application is approved for quote workflow.', 0, 50),
        (@TenantId, 'ApplicationStatus', 'Rejected', 'Rejected', 'Application was rejected.', 0, 90);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'QuoteStatus' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'QuoteStatus', 'Pending', 'Pending', 'Quote is pending market response.', 1, 10),
        (@TenantId, 'QuoteStatus', 'Accepted', 'Accepted', 'Quote has been accepted or presented.', 0, 20),
        (@TenantId, 'QuoteStatus', 'Declined', 'Declined', 'Quote has been declined.', 0, 80),
        (@TenantId, 'QuoteStatus', 'Expired', 'Expired', 'Quote has expired.', 0, 90);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'MarketStatus' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'MarketStatus', 'Draft', 'Draft', 'Market request is being prepared.', 1, 10),
        (@TenantId, 'MarketStatus', 'Sent', 'Sent', 'Market request has been sent.', 0, 20),
        (@TenantId, 'MarketStatus', 'In Review', 'In Review', 'Carrier is reviewing the request.', 0, 30),
        (@TenantId, 'MarketStatus', 'Awaiting Info', 'Awaiting Info', 'Carrier requested additional information.', 0, 40),
        (@TenantId, 'MarketStatus', 'Declined', 'Declined', 'Carrier declined the request.', 0, 70),
        (@TenantId, 'MarketStatus', 'Quoted', 'Quoted', 'Carrier provided quote terms.', 0, 80),
        (@TenantId, 'MarketStatus', 'No Response', 'No Response', 'Carrier has not responded by the due date.', 0, 90);
END;

INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
SELECT @TenantId, 'QuoteStatus', v.Code, v.Name, v.Description, 0, v.SortOrder
FROM (VALUES
    ('Received', 'Received', 'Carrier quote response has been received.', 30),
    ('Proposed', 'Proposed', 'Quote was proposed to the client.', 40),
    ('Selected', 'Selected', 'Quote was selected for proposal or bind.', 50),
    ('Bound', 'Bound', 'Quote was bound into a policy.', 60),
    ('Lost', 'Lost', 'Quote was lost or not selected.', 70)
) v(Code, Name, Description, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption existing WHERE existing.TenantId = @TenantId AND existing.OptionGroup = 'QuoteStatus' AND existing.OptionCode = v.Code AND existing.IsDeleted = 0);

INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
SELECT @TenantId, 'MarketStatus', v.Code, v.Name, v.Description, 0, v.SortOrder
FROM (VALUES
    ('Selected', 'Selected', 'Market has been selected for submission.', 15),
    ('Blocked', 'Blocked', 'Market request is blocked pending resolution.', 60),
    ('Bound', 'Bound', 'Market quote has been bound.', 85),
    ('Not Selected', 'Not Selected', 'Market was not selected for placement.', 95)
) v(Code, Name, Description, SortOrder)
WHERE NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption existing WHERE existing.TenantId = @TenantId AND existing.OptionGroup = 'MarketStatus' AND existing.OptionCode = v.Code AND existing.IsDeleted = 0);

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'SubmissionMethod' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'SubmissionMethod', 'Email', 'Email', 'Submission package is delivered by email.', 1, 10),
        (@TenantId, 'SubmissionMethod', 'Portal', 'Portal', 'Submission package is delivered through a carrier portal.', 0, 20),
        (@TenantId, 'SubmissionMethod', 'API', 'API', 'Submission package is delivered through an API integration.', 0, 30),
        (@TenantId, 'SubmissionMethod', 'Download', 'Download', 'Submission package is prepared for manual download.', 0, 40),
        (@TenantId, 'SubmissionMethod', 'InternalQueue', 'Internal Queue', 'Submission package is queued for internal processing.', 0, 50);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'ProposalDeliveryMethod' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'ProposalDeliveryMethod', 'Email', 'Email', 'Proposal is delivered by email.', 1, 10),
        (@TenantId, 'ProposalDeliveryMethod', 'Portal', 'Portal', 'Proposal is delivered through a client portal.', 0, 20),
        (@TenantId, 'ProposalDeliveryMethod', 'Download', 'Download', 'Proposal is generated for manual download.', 0, 30);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'ProposalClientDecision' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'ProposalClientDecision', 'Pending', 'Pending', 'Client decision has not been received.', 1, 10),
        (@TenantId, 'ProposalClientDecision', 'Accepted', 'Accepted', 'Client accepted the proposal.', 0, 20),
        (@TenantId, 'ProposalClientDecision', 'Rejected', 'Rejected', 'Client rejected the proposal.', 0, 30),
        (@TenantId, 'ProposalClientDecision', 'Needs revision', 'Needs revision', 'Client requested a proposal revision.', 0, 40);
END;

IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReferenceOption WHERE TenantId = @TenantId AND OptionGroup = 'DeclineType' AND IsDeleted = 0)
BEGIN
    INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
    VALUES
        (@TenantId, 'DeclineType', 'Carrier', 'Carrier', 'Carrier or market declined the submission.', 1, 10),
        (@TenantId, 'DeclineType', 'Internal', 'Internal', 'Agency or underwriting team declined the submission.', 0, 20),
        (@TenantId, 'DeclineType', 'Withdrawn', 'Withdrawn', 'Client or producer withdrew the submission.', 0, 30);
END;";

        await connection.ExecuteAsync(new CommandDefinition(sql, new { TenantId = tenantId }, cancellationToken: cancellationToken));
    }
}
