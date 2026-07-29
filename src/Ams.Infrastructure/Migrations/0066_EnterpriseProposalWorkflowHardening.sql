SET XACT_ABORT ON;
GO

IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'ActionCode') IS NULL
	ALTER TABLE Submissions.SubmissionReadinessRequirement ADD ActionCode NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.SubmissionReadinessRequirement', N'ActionLabel') IS NULL
	ALTER TABLE Submissions.SubmissionReadinessRequirement ADD ActionLabel NVARCHAR(150) NULL;
GO

IF OBJECT_ID(N'Submissions.ProposalReadinessFactor', N'U') IS NOT NULL
BEGIN
	INSERT INTO Submissions.SubmissionReadinessRequirement
		(ReadinessRequirementId, TenantId, LineOfBusiness, CarrierId, StateCode, ChannelCode, ScopeCode, RequirementCode, RequirementTypeCode, DisplayName, Description, IsRequired, BlocksSubmit, AllowsWaiver, RequiresEvidence, EvidencePrompt, ApprovalRoleCode, ActionCode, ActionLabel, ScoreWeight, SortOrder, IsActive, CreatedDateUtc, CreatedByUserId, ModifiedDateUtc, ModifiedByUserId, IsDeleted)
	SELECT NEWID(), factor.TenantId, N'All', NULL, NULL, NULL, N'Proposal', factor.FactorCode, N'QuoteData', factor.DisplayName, factor.Instructions, factor.IsRequired, factor.IsRequired, 0, 0, NULL, NULL, factor.ActionCode, factor.ActionLabel, 10, factor.SortOrder, factor.IsActive, factor.CreatedDateUtc, factor.CreatedByUserId, factor.ModifiedDateUtc, factor.ModifiedByUserId, factor.IsDeleted
	FROM Submissions.ProposalReadinessFactor factor
	WHERE factor.IsDeleted = 0
	  AND NOT EXISTS (SELECT 1 FROM Submissions.SubmissionReadinessRequirement existing WHERE existing.TenantId = factor.TenantId AND existing.LineOfBusiness = N'All' AND existing.RequirementCode = factor.FactorCode AND existing.IsDeleted = 0);

	DROP TABLE Submissions.ProposalReadinessFactor;
END;
GO

INSERT INTO Submissions.SubmissionReadinessRequirement
	(ReadinessRequirementId, TenantId, LineOfBusiness, ScopeCode, RequirementCode, RequirementTypeCode, DisplayName, Description, IsRequired, BlocksSubmit, AllowsWaiver, RequiresEvidence, ActionCode, ActionLabel, ScoreWeight, SortOrder, IsActive, CreatedDateUtc, IsDeleted)
SELECT NEWID(), tenant.TenantId, N'All', N'Proposal', source.FactorCode, N'QuoteData', source.DisplayName, source.Instructions, 1, 1, 0, 0, source.ActionCode, source.ActionLabel, 10, source.SortOrder, 1, SYSUTCDATETIME(), 0
FROM Core.Tenant tenant
CROSS JOIN (VALUES
	(N'ApprovedStatus', N'Approved status', N'Select Approved for Presentation and save the quote.', N'QuoteReview', N'Review Quote Terms', 10),
	(N'CurrentExpiration', N'Current expiration', N'Enter a future carrier expiration date.', N'QuoteReview', N'Review Quote Terms', 20),
	(N'PositivePremium', N'Positive premium', N'Enter an annual premium greater than zero.', N'QuoteReview', N'Review Quote Terms', 30),
	(N'CarrierMarket', N'Carrier market', N'Link the quote to its carrier market.', N'QuoteReview', N'Review Quote Terms', 40),
	(N'Deductible', N'Deductible', N'Enter the carrier deductible.', N'QuoteReview', N'Review Quote Terms', 50),
	(N'CoverageLimit', N'Coverage limit', N'Enter the quoted coverage limit.', N'QuoteReview', N'Review Quote Terms', 60),
	(N'CoverageDetails', N'Coverage details', N'Enter coverage forms or coverage notes.', N'QuoteReview', N'Review Quote Terms', 70),
	(N'InternalReview', N'Internal review', N'Open Review Quote Terms, verify the information, and save.', N'QuoteReview', N'Review Quote Terms', 80),
	(N'CarrierQuoteDocument', N'Carrier quote document', N'Select the carrier-issued quote document.', N'QuoteReview', N'Review Quote Terms', 90)
) source(FactorCode, DisplayName, Instructions, ActionCode, ActionLabel, SortOrder)
WHERE tenant.IsDeleted = 0
  AND NOT EXISTS
  (
	SELECT 1 FROM Submissions.SubmissionReadinessRequirement existing
	WHERE existing.TenantId = tenant.TenantId AND existing.LineOfBusiness = N'All' AND existing.RequirementCode = source.FactorCode AND existing.IsDeleted = 0
  );
GO

IF NOT EXISTS
(
	SELECT 1
	FROM sys.indexes
	WHERE object_id = OBJECT_ID(N'Submissions.Proposal')
	  AND name = N'UX_Proposal_SubmissionVersion'
)
BEGIN
	IF EXISTS
	(
		SELECT 1
		FROM Submissions.Proposal
		WHERE IsDeleted = 0
		GROUP BY TenantId, SubmissionId, VersionNumber
		HAVING COUNT_BIG(*) > 1
	)
		THROW 52220, 'Duplicate active proposal versions must be resolved before workflow hardening can be applied.', 1;

	CREATE UNIQUE INDEX UX_Proposal_SubmissionVersion
		ON Submissions.Proposal(TenantId, SubmissionId, VersionNumber)
		WHERE IsDeleted = 0;
END;
GO

;WITH DuplicatePrimary AS
(
	SELECT ProposalRecipientId,
		   ROW_NUMBER() OVER
		   (
			   PARTITION BY TenantId, ProposalId
			   ORDER BY ModifiedDateUtc DESC, CreatedDateUtc DESC, ProposalRecipientId
		   ) AS PrimaryOrder
	FROM Submissions.ProposalRecipient
	WHERE IsPrimary = 1
	  AND IsDeleted = 0
)
UPDATE recipient
SET IsPrimary = 0,
	ModifiedDateUtc = SYSUTCDATETIME()
FROM Submissions.ProposalRecipient recipient
INNER JOIN DuplicatePrimary duplicate ON duplicate.ProposalRecipientId = recipient.ProposalRecipientId
WHERE duplicate.PrimaryOrder > 1;
GO

IF NOT EXISTS
(
	SELECT 1
	FROM sys.indexes
	WHERE object_id = OBJECT_ID(N'Submissions.ProposalRecipient')
	  AND name = N'UX_ProposalRecipient_Primary'
)
BEGIN
	CREATE UNIQUE INDEX UX_ProposalRecipient_Primary
		ON Submissions.ProposalRecipient(TenantId, ProposalId)
		WHERE IsPrimary = 1 AND IsDeleted = 0;
END;
GO

IF NOT EXISTS
(
	SELECT 1
	FROM sys.indexes
	WHERE object_id = OBJECT_ID(N'Submissions.ProposalDeliveryDispatch')
	  AND name = N'UX_ProposalDeliveryDispatch_Active'
)
BEGIN
	IF EXISTS
	(
		SELECT 1
		FROM Submissions.ProposalDeliveryDispatch
		WHERE IsDeleted = 0
		  AND StatusCode IN (N'Queued', N'Processing', N'Configuration Required', N'Sent')
		GROUP BY TenantId, ProposalId
		HAVING COUNT_BIG(*) > 1
	)
		THROW 52221, 'Duplicate active proposal deliveries must be resolved before workflow hardening can be applied.', 1;

	CREATE UNIQUE INDEX UX_ProposalDeliveryDispatch_Active
		ON Submissions.ProposalDeliveryDispatch(TenantId, ProposalId)
		WHERE IsDeleted = 0 AND StatusCode IN (N'Queued', N'Processing', N'Configuration Required', N'Sent');
END;
GO
