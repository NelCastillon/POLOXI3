IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Submissions') EXEC(N'CREATE SCHEMA Submissions');

IF OBJECT_ID(N'Submissions.QuoteLine', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.QuoteLine
	(
		QuoteLineId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_QuoteLine PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		QuoteId UNIQUEIDENTIFIER NOT NULL,
		SubmissionId UNIQUEIDENTIFIER NOT NULL,
		SubmissionLineId UNIQUEIDENTIFIER NULL,
		OpportunityLineId UNIQUEIDENTIFIER NULL,
		LineOfBusiness NVARCHAR(100) NOT NULL,
		QuotedPremium DECIMAL(18,2) NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_QuotedPremium DEFAULT 0,
		Deductible DECIMAL(18,2) NULL,
		[Limit] DECIMAL(18,2) NULL,
		CommissionPercent DECIMAL(9,4) NULL,
		CoverageForms NVARCHAR(2000) NULL,
		Subjectivities NVARCHAR(2000) NULL,
		Exclusions NVARCHAR(2000) NULL,
		PaymentTerms NVARCHAR(200) NULL,
		MinimumEarnedPremium DECIMAL(18,2) NULL,
		TaxesAndFees DECIMAL(18,2) NULL,
		BrokerFee DECIMAL(18,2) NULL,
		TriaIncluded BIT NULL,
		IsBindable BIT NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_IsBindable DEFAULT 0,
		CoverageNotes NVARCHAR(1000) NULL,
		Status NVARCHAR(50) NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_Status DEFAULT N'Quoted',
		SortOrder INT NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_SortOrder DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_IsDeleted DEFAULT 0
	);
END;

IF COL_LENGTH(N'Submissions.QuoteLine', N'SubmissionLineId') IS NULL ALTER TABLE Submissions.QuoteLine ADD SubmissionLineId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'Deductible') IS NULL ALTER TABLE Submissions.QuoteLine ADD Deductible DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'Limit') IS NULL ALTER TABLE Submissions.QuoteLine ADD [Limit] DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'CommissionPercent') IS NULL ALTER TABLE Submissions.QuoteLine ADD CommissionPercent DECIMAL(9,4) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'CoverageForms') IS NULL ALTER TABLE Submissions.QuoteLine ADD CoverageForms NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'Subjectivities') IS NULL ALTER TABLE Submissions.QuoteLine ADD Subjectivities NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'Exclusions') IS NULL ALTER TABLE Submissions.QuoteLine ADD Exclusions NVARCHAR(2000) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'PaymentTerms') IS NULL ALTER TABLE Submissions.QuoteLine ADD PaymentTerms NVARCHAR(200) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'MinimumEarnedPremium') IS NULL ALTER TABLE Submissions.QuoteLine ADD MinimumEarnedPremium DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'TaxesAndFees') IS NULL ALTER TABLE Submissions.QuoteLine ADD TaxesAndFees DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'BrokerFee') IS NULL ALTER TABLE Submissions.QuoteLine ADD BrokerFee DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'TriaIncluded') IS NULL ALTER TABLE Submissions.QuoteLine ADD TriaIncluded BIT NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'IsBindable') IS NULL ALTER TABLE Submissions.QuoteLine ADD IsBindable BIT NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_IsBindable DEFAULT 0;
IF COL_LENGTH(N'Submissions.QuoteLine', N'CoverageNotes') IS NULL ALTER TABLE Submissions.QuoteLine ADD CoverageNotes NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'SortOrder') IS NULL ALTER TABLE Submissions.QuoteLine ADD SortOrder INT NOT NULL CONSTRAINT DF_SubmissionsQuoteLine_SortOrder DEFAULT 0;
IF COL_LENGTH(N'Submissions.QuoteLine', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.QuoteLine ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.QuoteLine ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.QuoteLine', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.QuoteLine ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;

EXEC(N'
UPDATE ql
SET SubmissionLineId = sl.SubmissionLineId,
	ModifiedDateUtc = COALESCE(ql.ModifiedDateUtc, SYSUTCDATETIME())
FROM Submissions.QuoteLine ql
JOIN Submissions.SubmissionLine sl
  ON sl.SubmissionId = ql.SubmissionId
 AND sl.OpportunityLineId = ql.OpportunityLineId
 AND sl.IsDeleted = 0
WHERE ql.SubmissionLineId IS NULL
  AND ql.OpportunityLineId IS NOT NULL
  AND ql.IsDeleted = 0;

;WITH DuplicateLines AS
(
	SELECT QuoteLineId,
		   ROW_NUMBER() OVER
		   (
			   PARTITION BY QuoteId, SubmissionLineId
			   ORDER BY COALESCE(ModifiedDateUtc, CreatedDateUtc) DESC, CreatedDateUtc DESC, QuoteLineId
		   ) AS DuplicateOrder
	FROM Submissions.QuoteLine
	WHERE SubmissionLineId IS NOT NULL
	  AND IsDeleted = 0
)
UPDATE ql
SET IsDeleted = 1,
	ModifiedDateUtc = SYSUTCDATETIME()
FROM Submissions.QuoteLine ql
JOIN DuplicateLines duplicate ON duplicate.QuoteLineId = ql.QuoteLineId
WHERE duplicate.DuplicateOrder > 1;

UPDATE ql
SET Deductible = COALESCE(ql.Deductible, q.Deductible),
	[Limit] = COALESCE(ql.[Limit], q.[Limit]),
	CommissionPercent = COALESCE(ql.CommissionPercent, q.CommissionPercent),
	CoverageForms = COALESCE(NULLIF(ql.CoverageForms, N''''), q.CoverageForms),
	Subjectivities = COALESCE(NULLIF(ql.Subjectivities, N''''), q.Subjectivities),
	Exclusions = COALESCE(NULLIF(ql.Exclusions, N''''), q.Exclusions),
	PaymentTerms = COALESCE(NULLIF(ql.PaymentTerms, N''''), q.PaymentTerms),
	MinimumEarnedPremium = COALESCE(ql.MinimumEarnedPremium, q.MinimumEarnedPremium),
	TaxesAndFees = COALESCE(ql.TaxesAndFees, q.TaxesAndFees),
	BrokerFee = COALESCE(ql.BrokerFee, q.BrokerFee),
	TriaIncluded = COALESCE(ql.TriaIncluded, q.TriaIncluded),
	IsBindable = CASE WHEN ql.IsBindable = 1 THEN 1 ELSE COALESCE(q.IsBindable, 0) END,
	CoverageNotes = COALESCE(NULLIF(ql.CoverageNotes, N''''), q.CoverageNotes),
	ModifiedDateUtc = COALESCE(ql.ModifiedDateUtc, SYSUTCDATETIME())
FROM Submissions.QuoteLine ql
JOIN Submissions.Quote q ON q.QuoteId = ql.QuoteId AND q.IsDeleted = 0
WHERE ql.IsDeleted = 0;
');

IF EXISTS (SELECT 1 FROM Submissions.Submission WHERE SubmissionId = 'e1000000-0000-0000-0000-000000000011' AND IsDeleted = 0)
BEGIN
	DECLARE @SeedTenantId UNIQUEIDENTIFIER = (SELECT TenantId FROM Submissions.Submission WHERE SubmissionId = 'e1000000-0000-0000-0000-000000000011');

	IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionLine WHERE SubmissionLineId = 'e3100000-0000-0000-0000-000000000011' AND IsDeleted = 0)
		INSERT INTO Submissions.SubmissionLine
			(SubmissionLineId, TenantId, SubmissionId, OpportunityId, OpportunityLineId, LineOfBusiness, TargetPremium, CreatedDateUtc, IsDeleted)
		VALUES
			('e3100000-0000-0000-0000-000000000011', @SeedTenantId, 'e1000000-0000-0000-0000-000000000011', NULL, NULL, N'Commercial Property', 88500, SYSUTCDATETIME(), 0);

	IF NOT EXISTS (SELECT 1 FROM Submissions.SubmissionLine WHERE SubmissionLineId = 'e3100000-0000-0000-0000-000000000012' AND IsDeleted = 0)
		INSERT INTO Submissions.SubmissionLine
			(SubmissionLineId, TenantId, SubmissionId, OpportunityId, OpportunityLineId, LineOfBusiness, TargetPremium, CreatedDateUtc, IsDeleted)
		VALUES
			('e3100000-0000-0000-0000-000000000012', @SeedTenantId, 'e1000000-0000-0000-0000-000000000011', NULL, NULL, N'General Liability', 40000, SYSUTCDATETIME(), 0);
END;

EXEC(N';WITH EligibleLines AS
(
	SELECT q.QuoteId,
		   q.SubmissionId,
		   s.TenantId,
		   sl.SubmissionLineId,
		   sl.OpportunityLineId,
		   sl.LineOfBusiness,
		   sl.TargetPremium,
		   ROW_NUMBER() OVER (PARTITION BY q.QuoteId ORDER BY sl.LineOfBusiness, sl.SubmissionLineId) AS SortOrder,
		   SUM(CASE WHEN sl.TargetPremium > 0 THEN sl.TargetPremium ELSE 0 END) OVER (PARTITION BY q.QuoteId) AS TotalTargetPremium,
		   COUNT(1) OVER (PARTITION BY q.QuoteId) AS LineCount,
		   q.Status,
		   q.AnnualPremium,
		   q.Deductible,
		   q.[Limit],
		   q.CommissionPercent,
		   q.CoverageForms,
		   q.Subjectivities,
		   q.Exclusions,
		   q.PaymentTerms,
		   q.MinimumEarnedPremium,
		   q.TaxesAndFees,
		   q.BrokerFee,
		   q.TriaIncluded,
		   q.IsBindable,
		   q.CoverageNotes,
		   q.CreatedDateUtc,
		   q.ModifiedByUserId
	FROM Submissions.Quote q
	JOIN Submissions.Submission s ON s.SubmissionId = q.SubmissionId AND s.IsDeleted = 0
	JOIN Submissions.SubmissionLine sl ON sl.SubmissionId = q.SubmissionId AND sl.TenantId = s.TenantId AND sl.IsDeleted = 0
	WHERE q.IsDeleted = 0
	  AND (q.SubmissionMarketId IS NULL OR NOT EXISTS
	  (
		  SELECT 1
		  FROM Submissions.SubmissionMarketLine sml
		  WHERE sml.SubmissionMarketId = q.SubmissionMarketId
			AND sml.IsDeleted = 0
	  ) OR EXISTS
	  (
		  SELECT 1
		  FROM Submissions.SubmissionMarketLine sml
		  WHERE sml.SubmissionMarketId = q.SubmissionMarketId
			AND sml.SubmissionLineId = sl.SubmissionLineId
			AND sml.IsDeleted = 0
	  ))
)
INSERT INTO Submissions.QuoteLine
	(QuoteLineId, TenantId, QuoteId, SubmissionId, SubmissionLineId, OpportunityLineId, LineOfBusiness, QuotedPremium,
	 Deductible, [Limit], CommissionPercent, CoverageForms, Subjectivities, Exclusions, PaymentTerms, MinimumEarnedPremium,
	 TaxesAndFees, BrokerFee, TriaIncluded, IsBindable, CoverageNotes, Status, SortOrder, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), line.TenantId, line.QuoteId, line.SubmissionId, line.SubmissionLineId, line.OpportunityLineId, line.LineOfBusiness,
	   ROUND(CASE WHEN line.TotalTargetPremium > 0 THEN line.AnnualPremium * line.TargetPremium / line.TotalTargetPremium
				  ELSE line.AnnualPremium / NULLIF(line.LineCount, 0) END, 2),
	   line.Deductible, line.[Limit], line.CommissionPercent, line.CoverageForms, line.Subjectivities, line.Exclusions,
	   line.PaymentTerms, line.MinimumEarnedPremium, line.TaxesAndFees, line.BrokerFee, line.TriaIncluded, line.IsBindable,
	   line.CoverageNotes, line.Status, line.SortOrder, COALESCE(line.CreatedDateUtc, SYSUTCDATETIME()), line.ModifiedByUserId, 0
FROM EligibleLines line
WHERE NOT EXISTS
(
	SELECT 1
	FROM Submissions.QuoteLine existing
	WHERE existing.QuoteId = line.QuoteId
	  AND existing.SubmissionLineId = line.SubmissionLineId
	  AND existing.IsDeleted = 0
);
');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.QuoteLine') AND name = N'UX_SubmissionsQuoteLine_Quote_SubmissionLine')
	EXEC(N'CREATE UNIQUE INDEX UX_SubmissionsQuoteLine_Quote_SubmissionLine ON Submissions.QuoteLine(QuoteId, SubmissionLineId) WHERE IsDeleted = 0 AND SubmissionLineId IS NOT NULL;');

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.QuoteLine') AND name = N'IX_SubmissionsQuoteLine_SubmissionLine')
	EXEC(N'CREATE INDEX IX_SubmissionsQuoteLine_SubmissionLine ON Submissions.QuoteLine(SubmissionLineId, QuoteId, IsDeleted);');
