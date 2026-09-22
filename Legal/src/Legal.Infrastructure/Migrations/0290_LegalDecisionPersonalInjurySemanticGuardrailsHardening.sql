SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Forward-only hardening for databases where 0289 was already applied.
-- Fresh databases receive the final definitions from 0289; this migration safely converges existing ones.

IF COL_LENGTH(N'POLOXI.Legal_DecisionDomainConceptRelation', N'SourceDecisionDomainConceptId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionDomainConceptRelation ADD SourceDecisionDomainConceptId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'POLOXI.Legal_DecisionDomainConceptRelation', N'TargetDecisionDomainConceptId') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionDomainConceptRelation ADD TargetDecisionDomainConceptId UNIQUEIDENTIFIER NULL;
GO

UPDATE relation
SET SourceDecisionDomainConceptId = sourceConcept.DecisionDomainConceptId,
	TargetDecisionDomainConceptId = targetConcept.DecisionDomainConceptId,
	ModifiedDateUtc = SYSUTCDATETIME()
FROM POLOXI.Legal_DecisionDomainConceptRelation relation
CROSS APPLY
(
	SELECT TOP 1 concept.DecisionDomainConceptId
	FROM POLOXI.Legal_DecisionDomainConcept concept
	WHERE concept.DecisionDomainPackId = relation.DecisionDomainPackId
	  AND concept.ConceptCode = relation.SourceConceptCode
	  AND concept.IsDeleted = 0
	  AND (concept.TenantId = relation.TenantId OR concept.TenantId IS NULL)
	ORDER BY CASE WHEN concept.TenantId = relation.TenantId THEN 0 ELSE 1 END, concept.VersionNumber DESC
) sourceConcept
CROSS APPLY
(
	SELECT TOP 1 concept.DecisionDomainConceptId
	FROM POLOXI.Legal_DecisionDomainConcept concept
	WHERE concept.DecisionDomainPackId = relation.DecisionDomainPackId
	  AND concept.ConceptCode = relation.TargetConceptCode
	  AND concept.IsDeleted = 0
	  AND (concept.TenantId = relation.TenantId OR concept.TenantId IS NULL)
	ORDER BY CASE WHEN concept.TenantId = relation.TenantId THEN 0 ELSE 1 END, concept.VersionNumber DESC
) targetConcept
WHERE relation.IsDeleted = 0;

IF EXISTS
(
	SELECT 1 FROM POLOXI.Legal_DecisionDomainConceptRelation
	WHERE IsDeleted = 0 AND (SourceDecisionDomainConceptId IS NULL OR TargetDecisionDomainConceptId IS NULL)
)
	THROW 50002, 'A semantic guardrail relation references a missing source or target concept.', 1;

-- Collapse accidental duplicate global/tenant concepts before adding scope-correct unique indexes.
;WITH RankedConcept AS
(
	SELECT DecisionDomainConceptId,
		ROW_NUMBER() OVER
		(
			PARTITION BY DecisionDomainPackId, ConceptCode, ISNULL(TenantId, '00000000-0000-0000-0000-000000000000')
			ORDER BY VersionNumber DESC, CreatedDateUtc, DecisionDomainConceptId
		) AS DuplicateRank,
		FIRST_VALUE(DecisionDomainConceptId) OVER
		(
			PARTITION BY DecisionDomainPackId, ConceptCode, ISNULL(TenantId, '00000000-0000-0000-0000-000000000000')
			ORDER BY VersionNumber DESC, CreatedDateUtc, DecisionDomainConceptId
		) AS KeeperId
	FROM POLOXI.Legal_DecisionDomainConcept
	WHERE IsDeleted = 0
)
UPDATE branch
SET DecisionDomainConceptId = ranked.KeeperId
FROM POLOXI.Legal_DecisionBranch branch
INNER JOIN RankedConcept ranked ON ranked.DecisionDomainConceptId = branch.DecisionDomainConceptId
WHERE ranked.DuplicateRank > 1;

;WITH RankedConcept AS
(
	SELECT DecisionDomainConceptId,
		ROW_NUMBER() OVER
		(
			PARTITION BY DecisionDomainPackId, ConceptCode, ISNULL(TenantId, '00000000-0000-0000-0000-000000000000')
			ORDER BY VersionNumber DESC, CreatedDateUtc, DecisionDomainConceptId
		) AS DuplicateRank,
		FIRST_VALUE(DecisionDomainConceptId) OVER
		(
			PARTITION BY DecisionDomainPackId, ConceptCode, ISNULL(TenantId, '00000000-0000-0000-0000-000000000000')
			ORDER BY VersionNumber DESC, CreatedDateUtc, DecisionDomainConceptId
		) AS KeeperId
	FROM POLOXI.Legal_DecisionDomainConcept
	WHERE IsDeleted = 0
)
UPDATE relation
SET SourceDecisionDomainConceptId = ranked.KeeperId
FROM POLOXI.Legal_DecisionDomainConceptRelation relation
INNER JOIN RankedConcept ranked ON ranked.DecisionDomainConceptId = relation.SourceDecisionDomainConceptId
WHERE ranked.DuplicateRank > 1;

;WITH RankedConcept AS
(
	SELECT DecisionDomainConceptId,
		ROW_NUMBER() OVER
		(
			PARTITION BY DecisionDomainPackId, ConceptCode, ISNULL(TenantId, '00000000-0000-0000-0000-000000000000')
			ORDER BY VersionNumber DESC, CreatedDateUtc, DecisionDomainConceptId
		) AS DuplicateRank,
		FIRST_VALUE(DecisionDomainConceptId) OVER
		(
			PARTITION BY DecisionDomainPackId, ConceptCode, ISNULL(TenantId, '00000000-0000-0000-0000-000000000000')
			ORDER BY VersionNumber DESC, CreatedDateUtc, DecisionDomainConceptId
		) AS KeeperId
	FROM POLOXI.Legal_DecisionDomainConcept
	WHERE IsDeleted = 0
)
UPDATE relation
SET TargetDecisionDomainConceptId = ranked.KeeperId
FROM POLOXI.Legal_DecisionDomainConceptRelation relation
INNER JOIN RankedConcept ranked ON ranked.DecisionDomainConceptId = relation.TargetDecisionDomainConceptId
WHERE ranked.DuplicateRank > 1;

;WITH RankedConcept AS
(
	SELECT DecisionDomainConceptId,
		ROW_NUMBER() OVER
		(
			PARTITION BY DecisionDomainPackId, ConceptCode, ISNULL(TenantId, '00000000-0000-0000-0000-000000000000')
			ORDER BY VersionNumber DESC, CreatedDateUtc, DecisionDomainConceptId
		) AS DuplicateRank
	FROM POLOXI.Legal_DecisionDomainConcept
	WHERE IsDeleted = 0
)
UPDATE concept
SET IsDeleted = 1, IsActive = 0, ModifiedDateUtc = SYSUTCDATETIME()
FROM POLOXI.Legal_DecisionDomainConcept concept
INNER JOIN RankedConcept ranked ON ranked.DecisionDomainConceptId = concept.DecisionDomainConceptId
WHERE ranked.DuplicateRank > 1;

IF EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainConcept') AND name = N'UX_Legal_DecisionDomainConcept_PackCodeScope')
	DROP INDEX UX_Legal_DecisionDomainConcept_PackCodeScope ON POLOXI.Legal_DecisionDomainConcept;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainConcept') AND name = N'UX_Legal_DecisionDomainConcept_GlobalCode')
	CREATE UNIQUE INDEX UX_Legal_DecisionDomainConcept_GlobalCode
		ON POLOXI.Legal_DecisionDomainConcept (DecisionDomainPackId, ConceptCode)
		WHERE TenantId IS NULL AND IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainConcept') AND name = N'UX_Legal_DecisionDomainConcept_TenantCode')
	CREATE UNIQUE INDEX UX_Legal_DecisionDomainConcept_TenantCode
		ON POLOXI.Legal_DecisionDomainConcept (DecisionDomainPackId, TenantId, ConceptCode)
		WHERE TenantId IS NOT NULL AND IsDeleted = 0;

-- Collapse duplicate relation edges, then enforce endpoint and edge integrity.
;WITH RankedRelation AS
(
	SELECT DecisionDomainConceptRelationId,
		ROW_NUMBER() OVER
		(
			PARTITION BY DecisionDomainPackId, SourceConceptCode, TargetConceptCode, RelationTypeCode,
				ISNULL(TenantId, '00000000-0000-0000-0000-000000000000')
			ORDER BY CreatedDateUtc, DecisionDomainConceptRelationId
		) AS DuplicateRank
	FROM POLOXI.Legal_DecisionDomainConceptRelation
	WHERE IsDeleted = 0
)
UPDATE relation
SET IsDeleted = 1, IsActive = 0, ModifiedDateUtc = SYSUTCDATETIME()
FROM POLOXI.Legal_DecisionDomainConceptRelation relation
INNER JOIN RankedRelation ranked ON ranked.DecisionDomainConceptRelationId = relation.DecisionDomainConceptRelationId
WHERE ranked.DuplicateRank > 1;

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Legal_DecisionDomainConceptRelation_Source')
	ALTER TABLE POLOXI.Legal_DecisionDomainConceptRelation ADD CONSTRAINT FK_Legal_DecisionDomainConceptRelation_Source
		FOREIGN KEY (SourceDecisionDomainConceptId) REFERENCES POLOXI.Legal_DecisionDomainConcept (DecisionDomainConceptId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Legal_DecisionDomainConceptRelation_Target')
	ALTER TABLE POLOXI.Legal_DecisionDomainConceptRelation ADD CONSTRAINT FK_Legal_DecisionDomainConceptRelation_Target
		FOREIGN KEY (TargetDecisionDomainConceptId) REFERENCES POLOXI.Legal_DecisionDomainConcept (DecisionDomainConceptId);

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Legal_DecisionDomainConceptRelation_ActiveEndpoints')
	ALTER TABLE POLOXI.Legal_DecisionDomainConceptRelation ADD CONSTRAINT CK_Legal_DecisionDomainConceptRelation_ActiveEndpoints
		CHECK (IsDeleted = 1 OR (SourceDecisionDomainConceptId IS NOT NULL AND TargetDecisionDomainConceptId IS NOT NULL));

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainConceptRelation') AND name = N'UX_Legal_DecisionDomainConceptRelation_GlobalEdge')
	CREATE UNIQUE INDEX UX_Legal_DecisionDomainConceptRelation_GlobalEdge
		ON POLOXI.Legal_DecisionDomainConceptRelation (DecisionDomainPackId, SourceConceptCode, TargetConceptCode, RelationTypeCode)
		WHERE TenantId IS NULL AND IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionDomainConceptRelation') AND name = N'UX_Legal_DecisionDomainConceptRelation_TenantEdge')
	CREATE UNIQUE INDEX UX_Legal_DecisionDomainConceptRelation_TenantEdge
		ON POLOXI.Legal_DecisionDomainConceptRelation (DecisionDomainPackId, TenantId, SourceConceptCode, TargetConceptCode, RelationTypeCode)
		WHERE TenantId IS NOT NULL AND IsDeleted = 0;

COMMIT TRANSACTION;
