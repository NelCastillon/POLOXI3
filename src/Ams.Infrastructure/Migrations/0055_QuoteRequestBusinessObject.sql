IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Submissions') EXEC(N'CREATE SCHEMA Submissions');
GO

IF OBJECT_ID(N'Submissions.QuoteRequest', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.QuoteRequest
	(
		QuoteRequestId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Submissions_QuoteRequest PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		SubmissionId UNIQUEIDENTIFIER NOT NULL,
		SubmissionMarketId UNIQUEIDENTIFIER NOT NULL,
		CarrierId UNIQUEIDENTIFIER NOT NULL,
		QuoteRequestActionCode NVARCHAR(50) NOT NULL,
		QuoteRequestReasonCode NVARCHAR(80) NULL,
		QuoteRequestMethodCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequest_Method_0055 DEFAULT N'ManualUnderwriter',
		QuoteRequestScopeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequest_Scope_0055 DEFAULT N'Package',
		RequestedPremium DECIMAL(18,2) NULL,
		Premium DECIMAL(18,2) NULL,
		CommissionPercent DECIMAL(9,4) NULL,
		QuoteNumber NVARCHAR(80) NULL,
		ExpirationDateUtc DATETIME2 NULL,
		CoverageNotes NVARCHAR(1000) NULL,
		CarrierReferenceNumber NVARCHAR(120) NULL,
		RequestVersion INT NOT NULL CONSTRAINT DF_QuoteRequest_RequestVersion_0055 DEFAULT 1,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequest_Status_0055 DEFAULT N'PendingDispatch',
		RequestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_QuoteRequest_RequestedDateUtc_0055 DEFAULT SYSUTCDATETIME(),
		RequestedByUserId UNIQUEIDENTIFIER NULL,
		ClosedDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_QuoteRequest_CreatedDateUtc_0055 DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_QuoteRequest_IsDeleted_0055 DEFAULT 0
	);
END;
GO

IF COL_LENGTH(N'Submissions.QuoteRequest', N'QuoteRequestId') IS NULL RETURN;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'TenantId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD TenantId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_QuoteRequest_Tenant_0055 DEFAULT '00000000-0000-0000-0000-000000000001';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'SubmissionId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD SubmissionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_QuoteRequest_Submission_0055 DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'SubmissionMarketId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD SubmissionMarketId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_QuoteRequest_Market_0055 DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'CarrierId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD CarrierId UNIQUEIDENTIFIER NOT NULL CONSTRAINT DF_QuoteRequest_Carrier_0055 DEFAULT '00000000-0000-0000-0000-000000000000';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'QuoteRequestActionCode') IS NULL ALTER TABLE Submissions.QuoteRequest ADD QuoteRequestActionCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequest_Action_0055 DEFAULT N'InitialRequest';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'QuoteRequestReasonCode') IS NULL ALTER TABLE Submissions.QuoteRequest ADD QuoteRequestReasonCode NVARCHAR(80) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'QuoteRequestScopeCode') IS NULL ALTER TABLE Submissions.QuoteRequest ADD QuoteRequestScopeCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequest_ScopeB_0055 DEFAULT N'Package';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'RequestedPremium') IS NULL ALTER TABLE Submissions.QuoteRequest ADD RequestedPremium DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'Premium') IS NULL ALTER TABLE Submissions.QuoteRequest ADD Premium DECIMAL(18,2) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'CommissionPercent') IS NULL ALTER TABLE Submissions.QuoteRequest ADD CommissionPercent DECIMAL(9,4) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'QuoteNumber') IS NULL ALTER TABLE Submissions.QuoteRequest ADD QuoteNumber NVARCHAR(80) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'ExpirationDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD ExpirationDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'CoverageNotes') IS NULL ALTER TABLE Submissions.QuoteRequest ADD CoverageNotes NVARCHAR(1000) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'CarrierReferenceNumber') IS NULL ALTER TABLE Submissions.QuoteRequest ADD CarrierReferenceNumber NVARCHAR(120) NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'RequestVersion') IS NULL ALTER TABLE Submissions.QuoteRequest ADD RequestVersion INT NOT NULL CONSTRAINT DF_QuoteRequest_RequestVersionB_0055 DEFAULT 1;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'StatusCode') IS NULL ALTER TABLE Submissions.QuoteRequest ADD StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_QuoteRequest_StatusB_0055 DEFAULT N'PendingDispatch';
IF COL_LENGTH(N'Submissions.QuoteRequest', N'RequestedDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD RequestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_QuoteRequest_RequestedDateUtcB_0055 DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Submissions.QuoteRequest', N'RequestedByUserId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD RequestedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'ClosedDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD ClosedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'CreatedDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_QuoteRequest_CreatedDateUtcB_0055 DEFAULT SYSUTCDATETIME();
IF COL_LENGTH(N'Submissions.QuoteRequest', N'CreatedByUserId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD CreatedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'ModifiedDateUtc') IS NULL ALTER TABLE Submissions.QuoteRequest ADD ModifiedDateUtc DATETIME2 NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'ModifiedByUserId') IS NULL ALTER TABLE Submissions.QuoteRequest ADD ModifiedByUserId UNIQUEIDENTIFIER NULL;
IF COL_LENGTH(N'Submissions.QuoteRequest', N'IsDeleted') IS NULL ALTER TABLE Submissions.QuoteRequest ADD IsDeleted BIT NOT NULL CONSTRAINT DF_QuoteRequest_IsDeletedB_0055 DEFAULT 0;
GO

IF COL_LENGTH(N'Submissions.Quote', N'QuoteRequestId') IS NULL ALTER TABLE Submissions.Quote ADD QuoteRequestId UNIQUEIDENTIFIER NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.QuoteRequest') AND name = N'UX_QuoteRequest_Market_Version')
	CREATE UNIQUE INDEX UX_QuoteRequest_Market_Version ON Submissions.QuoteRequest(SubmissionMarketId, RequestVersion) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.QuoteRequest') AND name = N'IX_QuoteRequest_Submission')
	CREATE INDEX IX_QuoteRequest_Submission ON Submissions.QuoteRequest(TenantId, SubmissionId, IsDeleted, RequestedDateUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.QuoteRequest') AND name = N'IX_QuoteRequest_Market_Status')
	CREATE INDEX IX_QuoteRequest_Market_Status ON Submissions.QuoteRequest(SubmissionMarketId, StatusCode, IsDeleted, RequestedDateUtc DESC);
GO

IF OBJECT_ID(N'Submissions.SubmissionReferenceOption', N'U') IS NOT NULL
BEGIN
	INSERT INTO Submissions.SubmissionReferenceOption (TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
	SELECT tenants.TenantId, seed.OptionGroup, seed.OptionCode, seed.OptionName, seed.Description, seed.IsDefault, seed.SortOrder
	FROM (SELECT DISTINCT TenantId FROM Submissions.Submission WHERE IsDeleted = 0) tenants
	CROSS JOIN (VALUES
		(N'QuoteRequestStatus', N'Draft', N'Draft', N'Quote request is being prepared and has not been dispatched.', CAST(0 AS bit), 5),
		(N'QuoteRequestStatus', N'ValidationRequired', N'Validation Required', N'Quote request is blocked until required submission information is completed.', CAST(0 AS bit), 8),
		(N'QuoteRequestStatus', N'PendingDispatch', N'Pending Dispatch', N'Quote request was created and is waiting for dispatch.', CAST(1 AS bit), 10),
		(N'QuoteRequestStatus', N'Open', N'Open', N'Legacy open quote request awaiting market response.', CAST(0 AS bit), 12),
		(N'QuoteRequestStatus', N'Submitted', N'Submitted', N'Quote request has been submitted to the market.', CAST(0 AS bit), 20),
		(N'QuoteRequestStatus', N'Acknowledged', N'Acknowledged', N'Market acknowledged the quote request.', CAST(0 AS bit), 30),
		(N'QuoteRequestStatus', N'UnderReview', N'Under Review', N'Market is underwriting or reviewing the quote request.', CAST(0 AS bit), 40),
		(N'QuoteRequestStatus', N'MoreInformationRequired', N'More Information Required', N'Market requested more information before quoting.', CAST(0 AS bit), 50),
		(N'QuoteRequestStatus', N'Referred', N'Referred', N'Quote request was referred to underwriting.', CAST(0 AS bit), 60),
		(N'QuoteRequestStatus', N'Quoted', N'Quoted', N'Market returned quote terms and a Quote record may exist.', CAST(0 AS bit), 70),
		(N'QuoteRequestStatus', N'Received', N'Received', N'Legacy status for market returned quote terms.', CAST(0 AS bit), 72),
		(N'QuoteRequestStatus', N'Declined', N'Declined', N'Market declined to quote.', CAST(0 AS bit), 80),
		(N'QuoteRequestStatus', N'Withdrawn', N'Withdrawn', N'Quote request was withdrawn before response.', CAST(0 AS bit), 90),
		(N'QuoteRequestStatus', N'Expired', N'Expired', N'Quote request or response expired.', CAST(0 AS bit), 100),
		(N'QuoteRequestStatus', N'Cancelled', N'Cancelled', N'Quote request was cancelled.', CAST(0 AS bit), 110),
		(N'QuoteRequestStatus', N'Failed', N'Failed', N'Quote request dispatch or processing failed.', CAST(0 AS bit), 120),
		(N'QuoteRequestStatus', N'Closed', N'Closed', N'Quote request was closed by a replacement request or workflow action.', CAST(0 AS bit), 130),
		(N'QuoteRequestStatus', N'No Response', N'No Response', N'Market did not respond before the follow-up due date.', CAST(0 AS bit), 140),
		(N'QuoteRequestMethod', N'ApiRating', N'API Rating', N'Personal-lines or comparative-rater API path where request quote submits and rates in one workflow.', CAST(1 AS bit), 10),
		(N'QuoteRequestMethod', N'MgaPortal', N'MGA Portal', N'MGA, wholesaler, or carrier portal path where AMS tracks portal submission and quote response.', CAST(0 AS bit), 20),
		(N'QuoteRequestMethod', N'Email', N'Email', N'Email path where AMS tracks a quote request sent to the market or underwriter by email.', CAST(0 AS bit), 30),
		(N'QuoteRequestMethod', N'ManualUnderwriter', N'Manual Underwriter', N'Manual commercial underwriting path where an underwriter reviews before quote terms are returned.', CAST(0 AS bit), 40),
		(N'SubmissionMethod', N'ApiRating', N'API Rating', N'Submission and quote request are sent through a carrier or comparative rater API.', CAST(0 AS bit), 5),
		(N'SubmissionMethod', N'MgaPortal', N'MGA Portal', N'Submission package is delivered through an MGA, wholesaler, or carrier portal.', CAST(0 AS bit), 18),
		(N'SubmissionMethod', N'ManualUnderwriter', N'Manual Underwriter', N'Submission package is tracked through manual underwriter review.', CAST(0 AS bit), 45)
	) seed(OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
	WHERE NOT EXISTS
	(
		SELECT 1
		FROM Submissions.SubmissionReferenceOption existing
		WHERE existing.TenantId = tenants.TenantId
		  AND existing.OptionGroup = seed.OptionGroup
		  AND existing.OptionCode = seed.OptionCode
		  AND existing.IsDeleted = 0
	);
END;
GO

IF OBJECT_ID(N'Submissions.QuoteRequestHistory', N'U') IS NOT NULL
BEGIN
	INSERT INTO Submissions.QuoteRequest
		(QuoteRequestId, TenantId, SubmissionId, SubmissionMarketId, CarrierId, QuoteRequestActionCode, QuoteRequestReasonCode, QuoteRequestMethodCode, QuoteRequestScopeCode,
		 RequestedPremium, CoverageNotes, RequestVersion, StatusCode, RequestedDateUtc, RequestedByUserId, ClosedDateUtc, CreatedDateUtc, CreatedByUserId,
		 ModifiedDateUtc, ModifiedByUserId, IsDeleted)
	SELECT NEWID(), h.TenantId, h.SubmissionId, h.SubmissionMarketId, h.CarrierId, h.QuoteRequestActionCode, h.QuoteRequestReasonCode,
		   COALESCE(NULLIF(sm.SubmissionMethodCode, N''), N'ManualUnderwriter'), COALESCE(NULLIF(h.QuoteRequestScopeCode, N''), N'Package'), h.RequestedPremium, h.CoverageNotes, h.RequestVersion, h.StatusCode,
		   h.RequestedDateUtc, h.RequestedByUserId, CASE WHEN h.StatusCode IN (N'Closed', N'Declined', N'Received', N'Expired', N'No Response') THEN h.ModifiedDateUtc ELSE NULL END,
		   h.CreatedDateUtc, h.CreatedByUserId, h.ModifiedDateUtc, h.ModifiedByUserId, h.IsDeleted
	FROM Submissions.QuoteRequestHistory h
	LEFT JOIN Submissions.SubmissionMarket sm ON sm.SubmissionMarketId = h.SubmissionMarketId AND sm.IsDeleted = 0
	WHERE h.IsDeleted = 0
	  AND NOT EXISTS
	  (
		  SELECT 1
		  FROM Submissions.QuoteRequest existing
		  WHERE existing.SubmissionMarketId = h.SubmissionMarketId
			AND existing.RequestVersion = h.RequestVersion
			AND existing.IsDeleted = 0
	  );
END;
GO

UPDATE q
SET QuoteRequestId = qr.QuoteRequestId,
	ModifiedDateUtc = COALESCE(q.ModifiedDateUtc, SYSUTCDATETIME())
FROM Submissions.Quote q
OUTER APPLY
(
	SELECT TOP 1 QuoteRequestId
	FROM Submissions.QuoteRequest qr
	WHERE qr.SubmissionMarketId = q.SubmissionMarketId
	  AND qr.IsDeleted = 0
	ORDER BY qr.RequestVersion DESC, qr.RequestedDateUtc DESC
) qr
WHERE q.QuoteRequestId IS NULL
  AND q.SubmissionMarketId IS NOT NULL
  AND qr.QuoteRequestId IS NOT NULL;
GO

UPDATE qr
SET StatusCode = CASE
		WHEN q.Status IN (N'Declined') THEN N'Declined'
		WHEN q.QuoteReceivedDateUtc IS NOT NULL OR q.Status IN (N'Received', N'Presented', N'Accepted', N'Bound', N'Selected') THEN N'Received'
		ELSE qr.StatusCode
	END,
	Premium = COALESCE(qr.Premium, q.AnnualPremium),
	CommissionPercent = COALESCE(qr.CommissionPercent, q.CommissionPercent),
	QuoteNumber = COALESCE(qr.QuoteNumber, q.QuoteNumber),
	ExpirationDateUtc = COALESCE(qr.ExpirationDateUtc, q.ExpiresDateUtc),
	CarrierReferenceNumber = COALESCE(qr.CarrierReferenceNumber, q.CarrierReferenceNumber),
	ClosedDateUtc = CASE WHEN q.QuoteReceivedDateUtc IS NOT NULL THEN COALESCE(qr.ClosedDateUtc, q.QuoteReceivedDateUtc) ELSE qr.ClosedDateUtc END,
	ModifiedDateUtc = SYSUTCDATETIME()
FROM Submissions.QuoteRequest qr
INNER JOIN Submissions.Quote q ON q.QuoteRequestId = qr.QuoteRequestId AND q.IsDeleted = 0
WHERE qr.IsDeleted = 0;
GO
