-- ============================================================
-- MIGRATION 0049: WORKFLOW RELATIONSHIP ALIGNMENT
-- Aligns CRM workflow hierarchy:
-- Lead -> Account -> Opportunity -> Submission -> Quote
-- ============================================================

-- Lead may qualify into zero or one account.
IF COL_LENGTH('CRM.Lead', 'AccountId') IS NULL
BEGIN
    ALTER TABLE CRM.Lead
        ADD AccountId UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Lead_AccountId' AND object_id = OBJECT_ID('CRM.Lead'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Lead_AccountId
        ON CRM.Lead (AccountId)
        WHERE AccountId IS NOT NULL;
END
GO

-- Submission belongs to an opportunity; account remains denormalized for fast register/search and consistency with existing UI.
IF COL_LENGTH('Submissions.Submission', 'OpportunityId') IS NULL
BEGIN
    ALTER TABLE Submissions.Submission
        ADD OpportunityId UNIQUEIDENTIFIER NULL;
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Submission_OpportunityId' AND object_id = OBJECT_ID('Submissions.Submission'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Submission_OpportunityId
        ON Submissions.Submission (OpportunityId, IsDeleted)
        WHERE OpportunityId IS NOT NULL;
END
GO
