SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- Judz Citator — authority treatment & good-law validity.
-- Records how later authorities have TREATED a cited authority (overruled, reversed, vacated,
-- criticized, distinguished, questioned, limited, followed, ...) and lets the app compute a current
-- validity flag (Good Law / Caution / Negative / Unknown) so attorneys can see whether cited authority
-- is still reliable. Treatment codes and flag levels are DB-backed reference data (no hardcoded
-- operational vocabularies). Treatments are append-only history; superseding is via IsDeleted, never
-- in-place rewrite. POLOXI schema, standard base/audit fields.
-- ────────────────────────────────────────────────────────────────────────────────────────────────

IF SCHEMA_ID(N'POLOXI') IS NULL
	EXEC(N'CREATE SCHEMA POLOXI AUTHORIZATION dbo;');

GO

-- ── Reference: flag levels (overall validity signal shown to the user). ──
IF OBJECT_ID(N'POLOXI.Legal_CitationFlagLevel',N'U') IS NULL
CREATE TABLE POLOXI.Legal_CitationFlagLevel
(
	FlagLevelCode   NVARCHAR(30) NOT NULL CONSTRAINT PK_Legal_CitationFlagLevel PRIMARY KEY,
	DisplayName     NVARCHAR(60) NOT NULL,
	Description     NVARCHAR(300) NULL,
	ColorHex        NVARCHAR(9) NOT NULL,          -- badge color
	SeverityOrder   INT NOT NULL,                  -- higher = worse; used to pick the worst treatment
	IsDefault       BIT NOT NULL CONSTRAINT DF_Legal_CitationFlagLevel_IsDefault DEFAULT 0
);

GO

-- ── Reference: treatment codes (how a later authority treated the cited one). ──
IF OBJECT_ID(N'POLOXI.Legal_CitationTreatmentCode',N'U') IS NULL
CREATE TABLE POLOXI.Legal_CitationTreatmentCode
(
	TreatmentCode   NVARCHAR(40) NOT NULL CONSTRAINT PK_Legal_CitationTreatmentCode PRIMARY KEY,
	DisplayName     NVARCHAR(80) NOT NULL,
	Description     NVARCHAR(300) NULL,
	PolarityCode    NVARCHAR(20) NOT NULL,         -- Positive | Neutral | Negative | Fatal
	SeverityRank    INT NOT NULL,                  -- 0 best .. higher worse
	FlagLevelCode   NVARCHAR(30) NOT NULL,         -- the flag this treatment implies on its own
	IsActive        BIT NOT NULL CONSTRAINT DF_Legal_CitationTreatmentCode_IsActive DEFAULT 1,
	CONSTRAINT FK_Legal_CitationTreatmentCode_Flag FOREIGN KEY (FlagLevelCode)
		REFERENCES POLOXI.Legal_CitationFlagLevel (FlagLevelCode)
);

GO

-- ── Operational: one treatment record (a citing authority treating a cited authority). ──
IF OBJECT_ID(N'POLOXI.Legal_CitationTreatment',N'U') IS NULL
CREATE TABLE POLOXI.Legal_CitationTreatment
(
	CitationTreatmentId     UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Legal_CitationTreatment PRIMARY KEY DEFAULT NEWID(),
	CitedAuthorityIdentity  NVARCHAR(400) NOT NULL,   -- normalized identity/name of the authority whose validity is in question
	CitedAuthorityName      NVARCHAR(400) NULL,       -- display name as cited
	CitingAuthorityName     NVARCHAR(400) NULL,       -- the later authority that treated it
	CitingAuthorityRef      NVARCHAR(400) NULL,       -- citation/locator of the treating authority
	TreatmentCode           NVARCHAR(40) NOT NULL,
	FlagLevelCode           NVARCHAR(30) NOT NULL,
	TreatmentDate           DATE NULL,                -- when the treatment occurred (decision date)
	Jurisdiction            NVARCHAR(120) NULL,
	SourceProviderCode      NVARCHAR(60) NULL,        -- origin of this treatment record (manual, future citator feed)
	Notes                   NVARCHAR(1000) NULL,
	TenantId                UNIQUEIDENTIFIER NOT NULL,
	CreatedDateUtc          DATETIME2 NOT NULL CONSTRAINT DF_Legal_CitationTreatment_Created DEFAULT SYSUTCDATETIME(),
	CreatedByUserId         UNIQUEIDENTIFIER NULL,
	ModifiedDateUtc         DATETIME2 NULL,
	ModifiedByUserId        UNIQUEIDENTIFIER NULL,
	IsDeleted               BIT NOT NULL CONSTRAINT DF_Legal_CitationTreatment_IsDeleted DEFAULT 0,
	RowVersion              ROWVERSION,
	CONSTRAINT FK_Legal_CitationTreatment_Code FOREIGN KEY (TreatmentCode)
		REFERENCES POLOXI.Legal_CitationTreatmentCode (TreatmentCode),
	CONSTRAINT FK_Legal_CitationTreatment_Flag FOREIGN KEY (FlagLevelCode)
		REFERENCES POLOXI.Legal_CitationFlagLevel (FlagLevelCode)
);

GO

IF OBJECT_ID(N'IX_Legal_CitationTreatment_CitedIdentity',N'IX') IS NULL
	CREATE INDEX IX_Legal_CitationTreatment_CitedIdentity
		ON POLOXI.Legal_CitationTreatment (TenantId, CitedAuthorityIdentity)
		WHERE IsDeleted = 0;

GO

-- ── Seed: flag levels. ──
MERGE POLOXI.Legal_CitationFlagLevel AS tgt
USING (VALUES
	(N'GoodLaw',  N'Good Law',          N'No negative treatment on record.',                      N'#198754', 0, 1),
	(N'Caution',  N'Caution',           N'Questioned, criticized, limited, or distinguished.',    N'#fd7e14', 10, 0),
	(N'Negative', N'Negative Treatment', N'Overruled, reversed, vacated, or superseded.',          N'#dc3545', 20, 0),
	(N'Unknown',  N'Unknown',           N'No citator data available for this authority.',         N'#6c757d', 5, 0)
) AS src (FlagLevelCode, DisplayName, Description, ColorHex, SeverityOrder, IsDefault)
ON tgt.FlagLevelCode = src.FlagLevelCode
WHEN NOT MATCHED BY TARGET THEN
	INSERT (FlagLevelCode, DisplayName, Description, ColorHex, SeverityOrder, IsDefault)
	VALUES (src.FlagLevelCode, src.DisplayName, src.Description, src.ColorHex, src.SeverityOrder, src.IsDefault);

GO

-- ── Seed: treatment codes. ──
MERGE POLOXI.Legal_CitationTreatmentCode AS tgt
USING (VALUES
	(N'Followed',      N'Followed',      N'Later authority followed this authority.',          N'Positive', 0,  N'GoodLaw'),
	(N'Affirmed',      N'Affirmed',      N'Affirmed on appeal.',                               N'Positive', 0,  N'GoodLaw'),
	(N'Cited',         N'Cited',         N'Cited without negative treatment.',                 N'Neutral',  1,  N'GoodLaw'),
	(N'Explained',     N'Explained',     N'Explained by a later authority.',                   N'Neutral',  2,  N'GoodLaw'),
	(N'Distinguished', N'Distinguished', N'Distinguished on its facts.',                       N'Negative', 10, N'Caution'),
	(N'Limited',       N'Limited',       N'Limited in scope by a later authority.',            N'Negative', 11, N'Caution'),
	(N'Questioned',    N'Questioned',    N'Validity questioned by a later authority.',         N'Negative', 12, N'Caution'),
	(N'Criticized',    N'Criticized',    N'Criticized by a later authority.',                  N'Negative', 13, N'Caution'),
	(N'Superseded',    N'Superseded',    N'Superseded by statute or rule.',                    N'Fatal',    20, N'Negative'),
	(N'Vacated',       N'Vacated',       N'Vacated.',                                          N'Fatal',    21, N'Negative'),
	(N'Reversed',      N'Reversed',      N'Reversed on appeal.',                               N'Fatal',    22, N'Negative'),
	(N'Overruled',     N'Overruled',     N'Overruled by a later authority.',                   N'Fatal',    23, N'Negative')
) AS src (TreatmentCode, DisplayName, Description, PolarityCode, SeverityRank, FlagLevelCode)
ON tgt.TreatmentCode = src.TreatmentCode
WHEN NOT MATCHED BY TARGET THEN
	INSERT (TreatmentCode, DisplayName, Description, PolarityCode, SeverityRank, FlagLevelCode)
	VALUES (src.TreatmentCode, src.DisplayName, src.Description, src.PolarityCode, src.SeverityRank, src.FlagLevelCode);

GO

COMMIT TRANSACTION;
