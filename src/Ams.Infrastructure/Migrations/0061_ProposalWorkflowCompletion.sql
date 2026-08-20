SET XACT_ABORT ON;
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Portal') EXEC(N'CREATE SCHEMA Portal');
GO

IF COL_LENGTH(N'Submissions.Proposal', N'DeliveryStatus') IS NULL
	ALTER TABLE Submissions.Proposal ADD DeliveryStatus NVARCHAR(50) NULL;
IF COL_LENGTH(N'Submissions.Proposal', N'LastDeliveryDispatchId') IS NULL
	ALTER TABLE Submissions.Proposal ADD LastDeliveryDispatchId UNIQUEIDENTIFIER NULL;
GO

IF OBJECT_ID(N'Submissions.ProposalDeliveryProvider', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ProposalDeliveryProvider
	(
		ProposalDeliveryProviderId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProposalDeliveryProvider PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		DeliveryMethodCode NVARCHAR(50) NOT NULL,
		ProviderCode NVARCHAR(100) NOT NULL,
		HandlerCode NVARCHAR(50) NOT NULL,
		DisplayName NVARCHAR(150) NOT NULL,
		EndpointUri NVARCHAR(1000) NULL,
		SenderAddress NVARCHAR(320) NULL,
		SecretReference NVARCHAR(500) NULL,
		ConfigurationJson NVARCHAR(MAX) NULL,
		IsConfigured BIT NOT NULL CONSTRAINT DF_ProposalDeliveryProvider_Configured DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_ProposalDeliveryProvider_Active DEFAULT 1,
		MaxAttempts INT NOT NULL CONSTRAINT DF_ProposalDeliveryProvider_MaxAttempts DEFAULT 5,
		RetryDelaySeconds INT NOT NULL CONSTRAINT DF_ProposalDeliveryProvider_RetryDelay DEFAULT 300,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalDeliveryProvider_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ProposalDeliveryProvider_Deleted DEFAULT 0,
		CONSTRAINT CK_ProposalDeliveryProvider_Handler CHECK (HandlerCode IN (N'Smtp', N'Portal', N'ESignature', N'Manual')),
		CONSTRAINT CK_ProposalDeliveryProvider_MaxAttempts CHECK (MaxAttempts BETWEEN 1 AND 25),
		CONSTRAINT CK_ProposalDeliveryProvider_RetryDelay CHECK (RetryDelaySeconds BETWEEN 10 AND 86400),
		CONSTRAINT CK_ProposalDeliveryProvider_ConfigurationJson CHECK (ConfigurationJson IS NULL OR ISJSON(ConfigurationJson) = 1)
	);
END;
GO

IF OBJECT_ID(N'Portal.ProposalDelivery', N'U') IS NULL
BEGIN
	CREATE TABLE Portal.ProposalDelivery
	(
		PortalProposalDeliveryId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_PortalProposalDelivery PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		AccountId UNIQUEIDENTIFIER NOT NULL,
		ContactId UNIQUEIDENTIFIER NOT NULL,
		SubmissionId UNIQUEIDENTIFIER NOT NULL,
		ProposalId UNIQUEIDENTIFIER NOT NULL,
		ProposalDeliveryDispatchId UNIQUEIDENTIFIER NOT NULL,
		Title NVARCHAR(200) NOT NULL,
		HtmlContent NVARCHAR(MAX) NULL,
		DocumentId UNIQUEIDENTIFIER NULL,
		PublishedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PortalProposalDelivery_Published DEFAULT SYSUTCDATETIME(),
		FirstViewedDateUtc DATETIME2 NULL,
		LastViewedDateUtc DATETIME2 NULL,
		ViewCount INT NOT NULL CONSTRAINT DF_PortalProposalDelivery_ViewCount DEFAULT 0,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PortalProposalDelivery_Status DEFAULT N'Published',
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PortalProposalDelivery_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PortalProposalDelivery_Deleted DEFAULT 0,
		CONSTRAINT CK_PortalProposalDelivery_Status CHECK (StatusCode IN (N'Published', N'Viewed', N'Accepted', N'Withdrawn'))
	);
END;
GO

IF OBJECT_ID(N'Submissions.ProposalDeliveryDispatch', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ProposalDeliveryDispatch
	(
		ProposalDeliveryDispatchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ProposalDeliveryDispatch PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		SubmissionId UNIQUEIDENTIFIER NOT NULL,
		ProposalId UNIQUEIDENTIFIER NOT NULL,
		ProposalDeliveryProviderId UNIQUEIDENTIFIER NULL,
		DeliveryMethodCode NVARCHAR(50) NOT NULL,
		Recipient NVARCHAR(320) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_ProposalDeliveryDispatch_Status DEFAULT N'Queued',
		AttemptCount INT NOT NULL CONSTRAINT DF_ProposalDeliveryDispatch_Attempts DEFAULT 0,
		MaxAttempts INT NOT NULL CONSTRAINT DF_ProposalDeliveryDispatch_MaxAttempts DEFAULT 5,
		NextAttemptDateUtc DATETIME2 NULL,
		ClaimedDateUtc DATETIME2 NULL,
		ClaimedBy NVARCHAR(200) NULL,
		CompletedDateUtc DATETIME2 NULL,
		ExternalDeliveryId NVARCHAR(500) NULL,
		ErrorCode NVARCHAR(100) NULL,
		ErrorMessage NVARCHAR(2000) NULL,
		RequestJson NVARCHAR(MAX) NULL,
		ResponseJson NVARCHAR(MAX) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ProposalDeliveryDispatch_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ProposalDeliveryDispatch_Deleted DEFAULT 0,
		CONSTRAINT CK_ProposalDeliveryDispatch_Status CHECK (StatusCode IN (N'Queued', N'Processing', N'Configuration Required', N'Delivered', N'Failed', N'Cancelled')),
		CONSTRAINT CK_ProposalDeliveryDispatch_Attempts CHECK (AttemptCount >= 0 AND MaxAttempts BETWEEN 1 AND 25),
		CONSTRAINT CK_ProposalDeliveryDispatch_RequestJson CHECK (RequestJson IS NULL OR ISJSON(RequestJson) = 1),
		CONSTRAINT CK_ProposalDeliveryDispatch_ResponseJson CHECK (ResponseJson IS NULL OR ISJSON(ResponseJson) = 1)
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalDeliveryProvider') AND name = N'UX_ProposalDeliveryProvider_Tenant_Method')
	CREATE UNIQUE INDEX UX_ProposalDeliveryProvider_Tenant_Method ON Submissions.ProposalDeliveryProvider(TenantId, DeliveryMethodCode) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalDeliveryDispatch') AND name = N'IX_ProposalDeliveryDispatch_WorkQueue')
	CREATE INDEX IX_ProposalDeliveryDispatch_WorkQueue ON Submissions.ProposalDeliveryDispatch(StatusCode, NextAttemptDateUtc, CreatedDateUtc) INCLUDE (TenantId, ProposalId, ProposalDeliveryProviderId, AttemptCount, MaxAttempts) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ProposalDeliveryDispatch') AND name = N'IX_ProposalDeliveryDispatch_Proposal')
	CREATE INDEX IX_ProposalDeliveryDispatch_Proposal ON Submissions.ProposalDeliveryDispatch(TenantId, ProposalId, CreatedDateUtc DESC) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Portal.ProposalDelivery') AND name = N'UX_PortalProposalDelivery_Dispatch')
	CREATE UNIQUE INDEX UX_PortalProposalDelivery_Dispatch ON Portal.ProposalDelivery(ProposalDeliveryDispatchId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Portal.ProposalDelivery') AND name = N'IX_PortalProposalDelivery_Contact')
	CREATE INDEX IX_PortalProposalDelivery_Contact ON Portal.ProposalDelivery(TenantId, ContactId, StatusCode, PublishedDateUtc DESC) WHERE IsDeleted = 0;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProposalDeliveryDispatch_Proposal')
	ALTER TABLE Submissions.ProposalDeliveryDispatch WITH CHECK ADD CONSTRAINT FK_ProposalDeliveryDispatch_Proposal FOREIGN KEY (ProposalId) REFERENCES Submissions.Proposal(ProposalId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProposalDeliveryDispatch_Submission')
	ALTER TABLE Submissions.ProposalDeliveryDispatch WITH CHECK ADD CONSTRAINT FK_ProposalDeliveryDispatch_Submission FOREIGN KEY (SubmissionId) REFERENCES Submissions.Submission(SubmissionId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ProposalDeliveryDispatch_Provider')
	ALTER TABLE Submissions.ProposalDeliveryDispatch WITH CHECK ADD CONSTRAINT FK_ProposalDeliveryDispatch_Provider FOREIGN KEY (ProposalDeliveryProviderId) REFERENCES Submissions.ProposalDeliveryProvider(ProposalDeliveryProviderId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PortalProposalDelivery_Dispatch')
	ALTER TABLE Portal.ProposalDelivery WITH CHECK ADD CONSTRAINT FK_PortalProposalDelivery_Dispatch FOREIGN KEY (ProposalDeliveryDispatchId) REFERENCES Submissions.ProposalDeliveryDispatch(ProposalDeliveryDispatchId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_PortalProposalDelivery_Proposal')
	ALTER TABLE Portal.ProposalDelivery WITH CHECK ADD CONSTRAINT FK_PortalProposalDelivery_Proposal FOREIGN KEY (ProposalId) REFERENCES Submissions.Proposal(ProposalId);
GO

DECLARE @SeedUserId UNIQUEIDENTIFIER = COALESCE((SELECT TOP 1 UserId FROM IAM.[User] WHERE IsDeleted = 0 ORDER BY CreatedDateUtc), '00000000-0000-0000-0000-000000000002');
DECLARE @Tenants TABLE (TenantId UNIQUEIDENTIFIER PRIMARY KEY);
INSERT INTO @Tenants(TenantId)
SELECT TenantId FROM Core.Tenant WHERE IsDeleted = 0
UNION SELECT DISTINCT TenantId FROM Submissions.Proposal;

INSERT INTO Submissions.ProposalDeliveryProvider
	(ProposalDeliveryProviderId, TenantId, DeliveryMethodCode, ProviderCode, HandlerCode, DisplayName, IsConfigured, IsActive, MaxAttempts, RetryDelaySeconds, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), t.TenantId, v.DeliveryMethodCode, v.ProviderCode, v.HandlerCode, v.DisplayName, v.IsConfigured, 1, v.MaxAttempts, v.RetryDelaySeconds, SYSUTCDATETIME(), @SeedUserId, 0
FROM @Tenants t
CROSS JOIN (VALUES
	(N'Email', N'SMTP', N'Smtp', N'Email (SMTP)', CAST(0 AS bit), 5, 300),
	(N'Portal', N'AMS_PORTAL', N'Portal', N'AMS Customer Portal', CAST(1 AS bit), 5, 60),
	(N'ESignature', N'EXTERNAL_ESIGN', N'ESignature', N'E-Signature Provider', CAST(0 AS bit), 5, 300),
	(N'InPerson', N'MANUAL_PRESENTATION', N'Manual', N'In-Person Presentation', CAST(1 AS bit), 1, 10)
) v(DeliveryMethodCode, ProviderCode, HandlerCode, DisplayName, IsConfigured, MaxAttempts, RetryDelaySeconds)
WHERE NOT EXISTS
(
	SELECT 1
	FROM Submissions.ProposalDeliveryProvider p
	WHERE p.TenantId = t.TenantId
	  AND p.DeliveryMethodCode = v.DeliveryMethodCode
	  AND p.IsDeleted = 0
);

UPDATE p
SET DeliveryStatus = CASE
		WHEN p.Status IN (N'Delivered', N'Presented', N'Accepted', N'Declined') THEN N'Delivered'
		WHEN p.DeliveryMethod IS NOT NULL THEN N'Configuration Required'
		ELSE p.DeliveryStatus
	END
FROM Submissions.Proposal p
WHERE p.IsDeleted = 0
  AND p.DeliveryStatus IS NULL;

INSERT INTO Submissions.ProposalDeliveryDispatch
	(ProposalDeliveryDispatchId, TenantId, SubmissionId, ProposalId, ProposalDeliveryProviderId, DeliveryMethodCode, Recipient, StatusCode, AttemptCount, MaxAttempts, CompletedDateUtc, ExternalDeliveryId, ResponseJson, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), p.TenantId, p.SubmissionId, p.ProposalId, provider.ProposalDeliveryProviderId,
	   COALESCE(NULLIF(p.DeliveryMethod, N''), N'InPerson'), COALESCE(NULLIF(p.Recipient, N''), N'Legacy delivery'),
	   N'Delivered', 1, COALESCE(provider.MaxAttempts, 1), COALESCE(p.SentDateUtc, p.ModifiedDateUtc, p.CreatedDateUtc),
	   CONCAT(N'legacy:', CONVERT(nvarchar(36), p.ProposalId)), N'{"source":"legacy-proposal-status"}',
	   COALESCE(p.SentDateUtc, p.ModifiedDateUtc, p.CreatedDateUtc), p.SentByUserId, 0
FROM Submissions.Proposal p
LEFT JOIN Submissions.ProposalDeliveryProvider provider
  ON provider.TenantId = p.TenantId
 AND provider.DeliveryMethodCode = COALESCE(NULLIF(p.DeliveryMethod, N''), N'InPerson')
 AND provider.IsDeleted = 0
WHERE p.IsDeleted = 0
  AND p.Status IN (N'Delivered', N'Presented', N'Accepted', N'Declined')
  AND NOT EXISTS
  (
	  SELECT 1 FROM Submissions.ProposalDeliveryDispatch d
	  WHERE d.TenantId = p.TenantId AND d.ProposalId = p.ProposalId AND d.IsDeleted = 0
  );

UPDATE p
SET LastDeliveryDispatchId = d.ProposalDeliveryDispatchId
FROM Submissions.Proposal p
CROSS APPLY
(
	SELECT TOP 1 x.ProposalDeliveryDispatchId
	FROM Submissions.ProposalDeliveryDispatch x
	WHERE x.TenantId = p.TenantId AND x.ProposalId = p.ProposalId AND x.IsDeleted = 0
	ORDER BY x.CreatedDateUtc DESC
) d
WHERE p.LastDeliveryDispatchId IS NULL;
GO
