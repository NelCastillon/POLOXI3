SET NOCOUNT ON;
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'POLOXI.Legal_DecisionResearchNeed', N'AtomicPropositionId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionResearchNeed ADD AtomicPropositionId UNIQUEIDENTIFIER NULL;

IF COL_LENGTH(N'POLOXI.Legal_AuthoritySearchPlan', N'AtomicPropositionId') IS NULL
	ALTER TABLE POLOXI.Legal_AuthoritySearchPlan ADD AtomicPropositionId UNIQUEIDENTIFIER NULL;

IF COL_LENGTH(N'POLOXI.Legal_AuthoritySearchResult', N'PropositionSelectionScore') IS NULL
	ALTER TABLE POLOXI.Legal_AuthoritySearchResult ADD PropositionSelectionScore DECIMAL(9,6) NULL;

IF COL_LENGTH(N'POLOXI.Legal_AuthoritySearchResult', N'PropositionSelectionRank') IS NULL
	ALTER TABLE POLOXI.Legal_AuthoritySearchResult ADD PropositionSelectionRank INT NULL;

IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidenceAttachment', N'AtomicPropositionId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionEvidenceAttachment ADD AtomicPropositionId UNIQUEIDENTIFIER NULL;

IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidenceAttachment', N'LegalSearchPlanId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionEvidenceAttachment ADD LegalSearchPlanId UNIQUEIDENTIFIER NULL;

IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidenceAttachment', N'NormalizedAuthorityId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionEvidenceAttachment ADD NormalizedAuthorityId UNIQUEIDENTIFIER NULL;

IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidenceAttachment', N'PropositionSelectionScore') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionEvidenceAttachment ADD PropositionSelectionScore DECIMAL(9,6) NULL;

IF COL_LENGTH(N'POLOXI.Legal_DecisionEvidenceAttachment', N'PropositionSelectionRank') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionEvidenceAttachment ADD PropositionSelectionRank INT NULL;

EXEC(N'
UPDATE POLOXI.Legal_DecisionResearchNeed
SET AtomicPropositionId = DecisionResearchNeedId
WHERE AtomicPropositionId IS NULL;

UPDATE searchPlan
SET AtomicPropositionId = COALESCE(researchNeed.AtomicPropositionId, searchPlan.DecisionResearchNeedId)
FROM POLOXI.Legal_AuthoritySearchPlan AS searchPlan
LEFT JOIN POLOXI.Legal_DecisionResearchNeed AS researchNeed
	ON researchNeed.DecisionResearchNeedId = searchPlan.DecisionResearchNeedId
WHERE searchPlan.AtomicPropositionId IS NULL;

UPDATE evidenceAttachment
SET AtomicPropositionId = COALESCE(researchNeed.AtomicPropositionId, evidenceAttachment.DecisionResearchNeedId)
FROM POLOXI.Legal_DecisionEvidenceAttachment AS evidenceAttachment
LEFT JOIN POLOXI.Legal_DecisionResearchNeed AS researchNeed
	ON researchNeed.DecisionResearchNeedId = evidenceAttachment.DecisionResearchNeedId
WHERE evidenceAttachment.AtomicPropositionId IS NULL;
');

IF NOT EXISTS
(
	SELECT 1
	FROM sys.indexes
	WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionResearchNeed', N'U')
		AND name = N'IX_Legal_DecisionResearchNeed_AtomicProposition'
)
	AND NOT EXISTS
	(
		SELECT 1
		FROM sys.stats
		WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionResearchNeed', N'U')
			AND name = N'IX_Legal_DecisionResearchNeed_AtomicProposition'
	)
	EXEC(N'CREATE INDEX IX_Legal_DecisionResearchNeed_AtomicProposition
		ON POLOXI.Legal_DecisionResearchNeed (DecisionSessionId, AtomicPropositionId)
		WHERE IsDeleted = 0 AND AtomicPropositionId IS NOT NULL;');

IF NOT EXISTS
(
	SELECT 1
	FROM sys.indexes
	WHERE object_id = OBJECT_ID(N'POLOXI.Legal_AuthoritySearchPlan', N'U')
		AND name = N'IX_Legal_AuthoritySearchPlan_AtomicProposition'
)
	AND NOT EXISTS
	(
		SELECT 1
		FROM sys.stats
		WHERE object_id = OBJECT_ID(N'POLOXI.Legal_AuthoritySearchPlan', N'U')
			AND name = N'IX_Legal_AuthoritySearchPlan_AtomicProposition'
	)
	EXEC(N'CREATE INDEX IX_Legal_AuthoritySearchPlan_AtomicProposition
		ON POLOXI.Legal_AuthoritySearchPlan (DecisionSessionId, AtomicPropositionId, CreatedDateUtc DESC)
		WHERE IsDeleted = 0 AND AtomicPropositionId IS NOT NULL;');

IF NOT EXISTS
(
	SELECT 1
	FROM sys.indexes
	WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionEvidenceAttachment', N'U')
		AND name = N'IX_Legal_DecisionEvidenceAttachment_PropositionSource'
)
	AND NOT EXISTS
	(
		SELECT 1
		FROM sys.stats
		WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionEvidenceAttachment', N'U')
			AND name = N'IX_Legal_DecisionEvidenceAttachment_PropositionSource'
	)
	EXEC(N'CREATE INDEX IX_Legal_DecisionEvidenceAttachment_PropositionSource
		ON POLOXI.Legal_DecisionEvidenceAttachment (DecisionSessionId, AtomicPropositionId, IsAuthoritative)
		INCLUDE (DecisionResearchNeedId, DecisionEvidenceId, LegalSearchPlanId, NormalizedAuthorityId, PropositionSelectionScore, PropositionSelectionRank, PassageRef)
		WHERE IsDeleted = 0 AND AtomicPropositionId IS NOT NULL;');

COMMIT TRANSACTION;
