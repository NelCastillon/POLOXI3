SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal Decision Intelligence — bind Matter to a Domain Pack + expand PI matter taxonomy.
-- Adds DomainPackCode to POLOXI.Legal_DecisionMatter so a matter declares which practice-area Domain
-- Pack governs its semantics (e.g. PERSONAL_INJURY), complementing the PracticeAreaCode from 0283.
-- Also expands the PERSONAL_INJURY Domain Pack matter-type taxonomy to the full target list
-- (truck / motorcycle / pedestrian / bicycle) — all DB-backed, global defaults use TenantId NULL.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Domain Pack binding on the Matter aggregate. ──
IF COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'DomainPackCode') IS NULL
	ALTER TABLE POLOXI.Legal_DecisionMatter ADD DomainPackCode NVARCHAR(60) NULL;

GO

IF NOT EXISTS(SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'POLOXI.Legal_DecisionMatter') AND name = N'IX_Legal_DecisionMatter_DomainPack')
   AND COL_LENGTH(N'POLOXI.Legal_DecisionMatter', N'DomainPackCode') IS NOT NULL
	CREATE INDEX IX_Legal_DecisionMatter_DomainPack
		ON POLOXI.Legal_DecisionMatter (TenantId, DomainPackCode, ModifiedDateUtc DESC, CreatedDateUtc DESC)
		WHERE IsDeleted = 0;

GO

-- ── Expand the PERSONAL_INJURY Domain Pack matter-type taxonomy. ──
DECLARE @PiPackId UNIQUEIDENTIFIER =
	(SELECT TOP 1 DecisionDomainPackId
	   FROM POLOXI.Legal_DecisionDomainPack
	  WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0
	  ORDER BY IsDefault DESC, SortOrder);

IF @PiPackId IS NOT NULL
BEGIN
	MERGE POLOXI.Legal_DecisionDomainPackMatterType AS target
	USING (VALUES
		(N'Truck Accident',      N'Truck Accident',      N'Commercial truck collisions.',           15),
		(N'Motorcycle Accident', N'Motorcycle Accident', N'Motorcycle collisions.',                 16),
		(N'Pedestrian Accident', N'Pedestrian Accident', N'Pedestrian-struck injuries.',            17),
		(N'Bicycle Accident',    N'Bicycle Accident',    N'Bicyclist-struck injuries.',             18)
	) AS source (MatterTypeCode, Name, Description, SortOrder)
	ON target.DecisionDomainPackId = @PiPackId AND target.MatterTypeCode = source.MatterTypeCode AND target.TenantId IS NULL
	WHEN NOT MATCHED BY TARGET THEN
		INSERT (DecisionDomainPackId, MatterTypeCode, Name, Description, SortOrder, TenantId)
		VALUES (@PiPackId, source.MatterTypeCode, source.Name, source.Description, source.SortOrder, NULL);
END

GO

COMMIT TRANSACTION;
