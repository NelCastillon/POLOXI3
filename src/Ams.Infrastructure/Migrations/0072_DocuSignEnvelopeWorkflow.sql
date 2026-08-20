SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'DMS') EXEC(N'CREATE SCHEMA DMS');
GO

IF OBJECT_ID(N'DMS.ESignRequest', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'DMS.ESignRequest', N'PolicyId') IS NULL ALTER TABLE DMS.ESignRequest ADD PolicyId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'DMS.ESignRequest', N'ProviderCode') IS NULL ALTER TABLE DMS.ESignRequest ADD ProviderCode NVARCHAR(50) NOT NULL CONSTRAINT DF_ESignRequest_ProviderCode_0072 DEFAULT N'DocuSign';
	IF COL_LENGTH(N'DMS.ESignRequest', N'ProviderEnvelopeId') IS NULL ALTER TABLE DMS.ESignRequest ADD ProviderEnvelopeId NVARCHAR(200) NULL;
	IF COL_LENGTH(N'DMS.ESignRequest', N'IdempotencyKey') IS NULL ALTER TABLE DMS.ESignRequest ADD IdempotencyKey NVARCHAR(200) NULL;
	IF COL_LENGTH(N'DMS.ESignRequest', N'ProviderStatus') IS NULL ALTER TABLE DMS.ESignRequest ADD ProviderStatus NVARCHAR(80) NULL;
	IF COL_LENGTH(N'DMS.ESignRequest', N'LastProviderEventDateUtc') IS NULL ALTER TABLE DMS.ESignRequest ADD LastProviderEventDateUtc DATETIME2 NULL;
	IF COL_LENGTH(N'DMS.ESignRequest', N'CreatedByUserId') IS NULL ALTER TABLE DMS.ESignRequest ADD CreatedByUserId UNIQUEIDENTIFIER NULL;

	IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.ESignRequest') AND name=N'UX_ESignRequest_Tenant_Idempotency')
		EXEC(N'CREATE UNIQUE INDEX UX_ESignRequest_Tenant_Idempotency ON DMS.ESignRequest(TenantId, IdempotencyKey) WHERE IdempotencyKey IS NOT NULL AND IsDeleted=0;');
	IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id=OBJECT_ID(N'DMS.ESignRequest') AND name=N'IX_ESignRequest_Policy')
		EXEC(N'CREATE INDEX IX_ESignRequest_Policy ON DMS.ESignRequest(TenantId, PolicyId, SentDate DESC) INCLUDE (DocumentId, Status, ProviderEnvelopeId) WHERE IsDeleted=0;');
END;
GO

IF OBJECT_ID(N'DMS.ESignProviderConfiguration', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.ESignProviderConfiguration
	(
		ESignProviderConfigurationId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ESignProviderConfiguration PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ProviderCode NVARCHAR(50) NOT NULL,
		AccountId NVARCHAR(200) NULL,
		IntegrationKey NVARCHAR(200) NULL,
		UserId NVARCHAR(200) NULL,
		OAuthBaseUri NVARCHAR(500) NULL,
		ApiBaseUri NVARCHAR(500) NULL,
		SecretReference NVARCHAR(500) NULL,
		ConnectHmacSecretReference NVARCHAR(500) NULL,
		IsEnabled BIT NOT NULL CONSTRAINT DF_ESignProviderConfiguration_IsEnabled DEFAULT 0,
		IsConfigured BIT NOT NULL CONSTRAINT DF_ESignProviderConfiguration_IsConfigured DEFAULT 0,
		MaxAttempts INT NOT NULL CONSTRAINT DF_ESignProviderConfiguration_MaxAttempts DEFAULT 5,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ESignProviderConfiguration_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ESignProviderConfiguration_IsDeleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_ESignProviderConfiguration_Tenant_Provider ON DMS.ESignProviderConfiguration(TenantId, ProviderCode) WHERE IsDeleted=0;
END;
GO

IF OBJECT_ID(N'DMS.ESignSigner', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.ESignSigner
	(
		ESignSignerId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ESignSigner PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ESignRequestId UNIQUEIDENTIFIER NOT NULL,
		RoutingOrder INT NOT NULL,
		SignerName NVARCHAR(200) NOT NULL,
		SignerEmail NVARCHAR(254) NOT NULL,
		ProviderRecipientId NVARCHAR(200) NULL,
		StatusCode NVARCHAR(80) NOT NULL,
		ViewedDateUtc DATETIME2 NULL,
		SignedDateUtc DATETIME2 NULL,
		DeclinedDateUtc DATETIME2 NULL,
		DeclineReason NVARCHAR(1000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ESignSigner_Created DEFAULT SYSUTCDATETIME(),
		IsDeleted BIT NOT NULL CONSTRAINT DF_ESignSigner_IsDeleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_ESignSigner_Request_Routing ON DMS.ESignSigner(TenantId, ESignRequestId, RoutingOrder) WHERE IsDeleted=0;
END;
GO

IF OBJECT_ID(N'DMS.ESignEnvelopeEvent', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.ESignEnvelopeEvent
	(
		ESignEnvelopeEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ESignEnvelopeEvent PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ESignRequestId UNIQUEIDENTIFIER NOT NULL,
		ProviderEventId NVARCHAR(200) NULL,
		EventTypeCode NVARCHAR(80) NOT NULL,
		ProviderStatus NVARCHAR(80) NULL,
		PayloadJson NVARCHAR(MAX) NULL,
		IsSignatureVerified BIT NOT NULL CONSTRAINT DF_ESignEnvelopeEvent_Verified DEFAULT 0,
		OccurredDateUtc DATETIME2 NOT NULL,
		ReceivedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ESignEnvelopeEvent_Received DEFAULT SYSUTCDATETIME()
	);
	CREATE UNIQUE INDEX UX_ESignEnvelopeEvent_ProviderEvent ON DMS.ESignEnvelopeEvent(TenantId, ProviderEventId) WHERE ProviderEventId IS NOT NULL;
	CREATE INDEX IX_ESignEnvelopeEvent_Request ON DMS.ESignEnvelopeEvent(TenantId, ESignRequestId, OccurredDateUtc DESC);
END;
GO

IF OBJECT_ID(N'DMS.ESignDispatch', N'U') IS NULL
BEGIN
	CREATE TABLE DMS.ESignDispatch
	(
		ESignDispatchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ESignDispatch PRIMARY KEY,
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ESignRequestId UNIQUEIDENTIFIER NOT NULL,
		StatusCode NVARCHAR(80) NOT NULL,
		AttemptCount INT NOT NULL CONSTRAINT DF_ESignDispatch_AttemptCount DEFAULT 0,
		MaxAttempts INT NOT NULL CONSTRAINT DF_ESignDispatch_MaxAttempts DEFAULT 5,
		NextAttemptDateUtc DATETIME2 NULL,
		ClaimedBy NVARCHAR(200) NULL,
		ClaimExpiresDateUtc DATETIME2 NULL,
		LastAttemptDateUtc DATETIME2 NULL,
		CompletedDateUtc DATETIME2 NULL,
		ErrorCode NVARCHAR(100) NULL,
		ErrorMessage NVARCHAR(2000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ESignDispatch_Created DEFAULT SYSUTCDATETIME(),
		IsDeleted BIT NOT NULL CONSTRAINT DF_ESignDispatch_IsDeleted DEFAULT 0
	);
	CREATE UNIQUE INDEX UX_ESignDispatch_Request ON DMS.ESignDispatch(TenantId, ESignRequestId) WHERE IsDeleted=0;
	CREATE INDEX IX_ESignDispatch_Queue ON DMS.ESignDispatch(StatusCode, NextAttemptDateUtc, CreatedDateUtc) INCLUDE (TenantId, ESignRequestId, AttemptCount, MaxAttempts) WHERE IsDeleted=0;
END;
GO
