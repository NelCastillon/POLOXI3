SET XACT_ABORT ON;
GO

IF OBJECT_ID(N'Submissions.ClientAcceptance', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ClientAcceptance
	(
		ClientAcceptanceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ClientAcceptance PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		AccountId UNIQUEIDENTIFIER NOT NULL,
		SubmissionId UNIQUEIDENTIFIER NOT NULL,
		ProposalId UNIQUEIDENTIFIER NOT NULL,
		ProposalVersionNumber INT NOT NULL,
		QuoteId UNIQUEIDENTIFIER NOT NULL,
		QuoteNumber NVARCHAR(100) NOT NULL,
		QuoteFingerprint CHAR(64) NOT NULL,
		DecisionCode NVARCHAR(50) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL,
		DecisionNotes NVARCHAR(2000) NULL,
		AuthorizationMethodCode NVARCHAR(50) NOT NULL,
		AuthorizationReference NVARCHAR(500) NULL,
		AuthorizationDocumentId UNIQUEIDENTIFIER NULL,
		ESignRequestId UNIQUEIDENTIFIER NULL,
		AuthorizedByName NVARCHAR(200) NOT NULL,
		AuthorizedByTitle NVARCHAR(150) NOT NULL,
		AuthorityBasisCode NVARCHAR(50) NOT NULL,
		AuthorizedDateUtc DATETIME2 NOT NULL,
		SignerEmail NVARCHAR(320) NULL,
		SignerIpAddress NVARCHAR(64) NULL,
		UserAgent NVARCHAR(1000) NULL,
		CustomerAuthorizationId UNIQUEIDENTIFIER NULL,
		PolicyBindTransactionId UNIQUEIDENTIFIER NULL,
		IdempotencyKey NVARCHAR(100) NOT NULL,
		VersionNumber BIGINT NOT NULL CONSTRAINT DF_ClientAcceptance_Version DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ClientAcceptance_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ClientAcceptance_Deleted DEFAULT 0,
		CONSTRAINT CK_ClientAcceptance_ProposalVersion CHECK (ProposalVersionNumber > 0),
		CONSTRAINT CK_ClientAcceptance_Decision CHECK (DecisionCode IN (N'Accepted', N'Declined', N'ChangesRequested', N'Deferred', N'LegacyIncomplete')),
		CONSTRAINT CK_ClientAcceptance_Status CHECK (StatusCode IN (N'Accepted', N'Declined', N'ChangesRequested', N'Deferred', N'Withdrawn', N'BindRequested', N'CarrierBound', N'LegacyIncomplete')),
		CONSTRAINT CK_ClientAcceptance_DecisionStatus CHECK ((DecisionCode = N'Accepted' AND StatusCode IN (N'Accepted', N'Withdrawn', N'BindRequested', N'CarrierBound')) OR (DecisionCode <> N'Accepted' AND StatusCode = DecisionCode)),
		CONSTRAINT CK_ClientAcceptance_Fingerprint CHECK (LEN(QuoteFingerprint) = 64),
		CONSTRAINT CK_ClientAcceptance_Version CHECK (VersionNumber > 0)
	);
END;
GO

IF OBJECT_ID(N'Submissions.ClientAcceptanceCoverageElection', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ClientAcceptanceCoverageElection
	(
		ClientAcceptanceCoverageElectionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ClientAcceptanceCoverageElection PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ClientAcceptanceId UNIQUEIDENTIFIER NOT NULL,
		QuoteLineId UNIQUEIDENTIFIER NOT NULL,
		SubmissionLineId UNIQUEIDENTIFIER NOT NULL,
		LineOfBusiness NVARCHAR(100) NOT NULL,
		ElectionCode NVARCHAR(50) NOT NULL,
		QuotedPremium DECIMAL(18,2) NOT NULL,
		[Limit] DECIMAL(18,2) NULL,
		Deductible DECIMAL(18,2) NULL,
		CoverageForms NVARCHAR(2000) NULL,
		Subjectivities NVARCHAR(2000) NULL,
		Exclusions NVARCHAR(2000) NULL,
		PaymentTerms NVARCHAR(200) NULL,
		TriaIncluded BIT NULL,
		ElectionNotes NVARCHAR(1000) NULL,
		SortOrder INT NOT NULL CONSTRAINT DF_ClientAcceptanceElection_Sort DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ClientAcceptanceElection_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ClientAcceptanceElection_Deleted DEFAULT 0,
		CONSTRAINT CK_ClientAcceptanceElection_Code CHECK (ElectionCode IN (N'Accepted', N'Rejected', N'OptionalAccepted', N'OptionalRejected')),
		CONSTRAINT CK_ClientAcceptanceElection_Premium CHECK (QuotedPremium >= 0),
		CONSTRAINT CK_ClientAcceptanceElection_Limit CHECK ([Limit] IS NULL OR [Limit] >= 0),
		CONSTRAINT CK_ClientAcceptanceElection_Deductible CHECK (Deductible IS NULL OR Deductible >= 0)
	);
END;
GO

IF OBJECT_ID(N'Submissions.ClientAcceptanceConsent', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ClientAcceptanceConsent
	(
		ClientAcceptanceConsentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ClientAcceptanceConsent PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ClientAcceptanceId UNIQUEIDENTIFIER NOT NULL,
		ConsentCode NVARCHAR(100) NOT NULL,
		ConsentVersion NVARCHAR(50) NOT NULL,
		IsAccepted BIT NOT NULL,
		AttestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ClientAcceptanceConsent_Attested DEFAULT SYSUTCDATETIME(),
		EvidenceDocumentId UNIQUEIDENTIFIER NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ClientAcceptanceConsent_Created DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_ClientAcceptanceConsent_Deleted DEFAULT 0
	);
END;
GO

IF OBJECT_ID(N'Submissions.ClientAcceptanceAuditEvent', N'U') IS NULL
BEGIN
	CREATE TABLE Submissions.ClientAcceptanceAuditEvent
	(
		ClientAcceptanceAuditEventId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_ClientAcceptanceAuditEvent PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ClientAcceptanceId UNIQUEIDENTIFIER NOT NULL,
		EventCode NVARCHAR(100) NOT NULL,
		EventDetail NVARCHAR(2000) NULL,
		DataJson NVARCHAR(MAX) NULL,
		EventDateUtc DATETIME2 NOT NULL CONSTRAINT DF_ClientAcceptanceAudit_Date DEFAULT SYSUTCDATETIME(),
		ActorUserId UNIQUEIDENTIFIER NULL,
		CONSTRAINT CK_ClientAcceptanceAudit_Json CHECK (DataJson IS NULL OR ISJSON(DataJson) = 1)
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ClientAcceptance') AND name = N'UX_ClientAcceptance_Tenant_Idempotency')
	CREATE UNIQUE INDEX UX_ClientAcceptance_Tenant_Idempotency ON Submissions.ClientAcceptance(TenantId, IdempotencyKey);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ClientAcceptance') AND name = N'UX_ClientAcceptance_ActiveProposal')
	CREATE UNIQUE INDEX UX_ClientAcceptance_ActiveProposal ON Submissions.ClientAcceptance(TenantId, ProposalId) WHERE IsDeleted = 0 AND StatusCode IN (N'Accepted', N'BindRequested', N'CarrierBound');
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ClientAcceptance') AND name = N'IX_ClientAcceptance_Submission')
	CREATE INDEX IX_ClientAcceptance_Submission ON Submissions.ClientAcceptance(TenantId, SubmissionId, CreatedDateUtc DESC) INCLUDE (ProposalId, QuoteId, StatusCode, PolicyBindTransactionId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ClientAcceptanceCoverageElection') AND name = N'UX_ClientAcceptanceElection_Line')
	CREATE UNIQUE INDEX UX_ClientAcceptanceElection_Line ON Submissions.ClientAcceptanceCoverageElection(ClientAcceptanceId, QuoteLineId) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ClientAcceptanceConsent') AND name = N'UX_ClientAcceptanceConsent_Code')
	CREATE UNIQUE INDEX UX_ClientAcceptanceConsent_Code ON Submissions.ClientAcceptanceConsent(ClientAcceptanceId, ConsentCode) WHERE IsDeleted = 0;
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.ClientAcceptanceAuditEvent') AND name = N'IX_ClientAcceptanceAudit_Acceptance')
	CREATE INDEX IX_ClientAcceptanceAudit_Acceptance ON Submissions.ClientAcceptanceAuditEvent(TenantId, ClientAcceptanceId, EventDateUtc DESC);
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ClientAcceptanceElection_ClientAcceptance')
	ALTER TABLE Submissions.ClientAcceptanceCoverageElection WITH CHECK ADD CONSTRAINT FK_ClientAcceptanceElection_ClientAcceptance FOREIGN KEY (ClientAcceptanceId) REFERENCES Submissions.ClientAcceptance(ClientAcceptanceId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ClientAcceptanceConsent_ClientAcceptance')
	ALTER TABLE Submissions.ClientAcceptanceConsent WITH CHECK ADD CONSTRAINT FK_ClientAcceptanceConsent_ClientAcceptance FOREIGN KEY (ClientAcceptanceId) REFERENCES Submissions.ClientAcceptance(ClientAcceptanceId);
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_ClientAcceptanceAudit_ClientAcceptance')
	ALTER TABLE Submissions.ClientAcceptanceAuditEvent WITH CHECK ADD CONSTRAINT FK_ClientAcceptanceAudit_ClientAcceptance FOREIGN KEY (ClientAcceptanceId) REFERENCES Submissions.ClientAcceptance(ClientAcceptanceId);
IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_ClientAcceptance_DecisionStatus' AND parent_object_id = OBJECT_ID(N'Submissions.ClientAcceptance'))
	ALTER TABLE Submissions.ClientAcceptance WITH CHECK ADD CONSTRAINT CK_ClientAcceptance_DecisionStatus CHECK ((DecisionCode = N'Accepted' AND StatusCode IN (N'Accepted', N'Withdrawn', N'BindRequested', N'CarrierBound')) OR (DecisionCode <> N'Accepted' AND StatusCode = DecisionCode));
GO

IF OBJECT_ID(N'IAM.Permission', N'U') IS NOT NULL
BEGIN
	DECLARE @ReadActionId INT = (SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE UPPER(ActionCode) = N'READ' OR UPPER(ActionName) = N'READ' ORDER BY PermissionActionId);
	DECLARE @WriteActionId INT = (SELECT TOP 1 PermissionActionId FROM Master.PermissionAction WHERE UPPER(ActionCode) IN (N'WRITE', N'MANAGE') OR UPPER(ActionName) IN (N'WRITE', N'MANAGE') ORDER BY PermissionActionId);
	SET @WriteActionId = COALESCE(@WriteActionId, @ReadActionId);
	DECLARE @PermissionTenantId UNIQUEIDENTIFIER = (SELECT TOP 1 TenantId FROM Core.Tenant WHERE IsDeleted = 0 ORDER BY TenantId);
	DECLARE @ClientAcceptancePermissions TABLE (PermissionCode NVARCHAR(200) NOT NULL PRIMARY KEY, PermissionName NVARCHAR(200) NOT NULL, ActionCode NVARCHAR(100) NOT NULL, PermissionActionId INT NULL, Description NVARCHAR(500) NOT NULL);
	INSERT INTO @ClientAcceptancePermissions (PermissionCode, PermissionName, ActionCode, PermissionActionId, Description) VALUES
		(N'CLIENT_ACCEPTANCE_VIEW', N'View Client Acceptance', N'Read', @ReadActionId, N'View client acceptance records and evidence.'),
		(N'CLIENT_ACCEPTANCE_RECORD', N'Record Client Acceptance', N'Write', @WriteActionId, N'Record validated client decisions, elections, and consent.'),
		(N'CLIENT_ACCEPTANCE_WITHDRAW', N'Withdraw Client Acceptance', N'Manage', @WriteActionId, N'Withdraw acceptance before carrier confirmation with an audit reason.');

	UPDATE existing
	SET PermissionName = source.PermissionName,
		ResourceCode = N'ClientAcceptance',
		ActionCode = source.ActionCode,
		PermissionActionId = COALESCE(source.PermissionActionId, existing.PermissionActionId),
		ModuleCode = N'Submissions',
		Description = source.Description,
		IsBuiltIn = 1,
		IsActive = 1,
		IsDeleted = 0,
		ModifiedDateUtc = SYSUTCDATETIME()
	FROM IAM.Permission existing
	INNER JOIN @ClientAcceptancePermissions source ON source.PermissionCode = existing.PermissionCode;

	INSERT INTO IAM.Permission (PermissionId, TenantId, PermissionCode, PermissionName, ResourceCode, ActionCode, PermissionActionId, ModuleCode, Description, IsBuiltIn, IsActive, CreatedDateUtc, IsDeleted)
	SELECT NEWID(), @PermissionTenantId, source.PermissionCode, source.PermissionName, N'ClientAcceptance', source.ActionCode, source.PermissionActionId, N'Submissions', source.Description, 1, 1, SYSUTCDATETIME(), 0
	FROM @ClientAcceptancePermissions source
	WHERE @PermissionTenantId IS NOT NULL
	  AND NOT EXISTS (SELECT 1 FROM IAM.Permission existing WHERE existing.PermissionCode = source.PermissionCode);
END;
GO

IF OBJECT_ID(N'Submissions.SubmissionReferenceOption', N'U') IS NOT NULL
BEGIN
	INSERT INTO Submissions.SubmissionReferenceOption
		(SubmissionReferenceOptionId, TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder, IsActive, CreatedDateUtc, IsDeleted)
	SELECT NEWID(), t.TenantId, v.OptionGroup, v.OptionCode, v.OptionName, v.Description, v.IsDefault, v.SortOrder, 1, SYSUTCDATETIME(), 0
	FROM Core.Tenant t
	CROSS JOIN (VALUES
		(N'ClientAcceptanceDecision', N'Accepted', N'Accept', N'Client accepts the selected quote and elected coverage.', CAST(1 AS bit), 10),
		(N'ClientAcceptanceDecision', N'Declined', N'Decline', N'Client declines the proposal.', CAST(0 AS bit), 20),
		(N'ClientAcceptanceDecision', N'ChangesRequested', N'Request Changes', N'Client requests revised terms.', CAST(0 AS bit), 30),
		(N'ClientAcceptanceDecision', N'Deferred', N'Defer', N'Client defers the decision.', CAST(0 AS bit), 40),
		(N'CoverageElection', N'Accepted', N'Accept Coverage', N'Accept the quoted coverage line.', CAST(1 AS bit), 10),
		(N'CoverageElection', N'Rejected', N'Reject Coverage', N'Reject the quoted coverage line.', CAST(0 AS bit), 20),
		(N'CoverageElection', N'OptionalAccepted', N'Accept Optional Coverage', N'Accept an optional coverage line.', CAST(0 AS bit), 30),
		(N'CoverageElection', N'OptionalRejected', N'Reject Optional Coverage', N'Reject an optional coverage line.', CAST(0 AS bit), 40),
		(N'AuthorityBasis', N'NamedInsured', N'Named Insured', N'Signer is the named insured.', CAST(1 AS bit), 10),
		(N'AuthorityBasis', N'Officer', N'Company Officer', N'Signer is an authorized company officer.', CAST(0 AS bit), 20),
		(N'AuthorityBasis', N'AuthorizedRepresentative', N'Authorized Representative', N'Signer has documented authority.', CAST(0 AS bit), 30),
		(N'ClientAcceptanceConsent', N'TermsReviewed', N'Terms Reviewed', N'Client confirms review of limits, deductibles, exclusions, and subjectivities.', CAST(1 AS bit), 10),
		(N'ClientAcceptanceConsent', N'CoverageElectionConfirmed', N'Coverage Elections Confirmed', N'Client confirms accepted and rejected coverage elections.', CAST(1 AS bit), 20),
		(N'ClientAcceptanceConsent', N'ElectronicConsent', N'Electronic Records Consent', N'Client consents to electronic records and signatures.', CAST(1 AS bit), 30),
		(N'ClientAcceptanceConsent', N'AuthorizationToBind', N'Authorization to Bind', N'Client authorizes the agency to request binding.', CAST(1 AS bit), 40)
	) v(OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
	WHERE t.IsDeleted = 0
	  AND NOT EXISTS
	  (
		  SELECT 1 FROM Submissions.SubmissionReferenceOption o
		  WHERE o.TenantId = t.TenantId AND o.OptionGroup = v.OptionGroup AND o.OptionCode = v.OptionCode AND o.IsDeleted = 0
	  );
END;
GO

-- Historical proposal decisions are intentionally not promoted to compliant acceptances.
-- Missing signer authority, consent, election, and immutable quote evidence must be recaptured.
INSERT INTO Submissions.ClientAcceptance
	(ClientAcceptanceId, TenantId, AccountId, SubmissionId, ProposalId, ProposalVersionNumber, QuoteId, QuoteNumber, QuoteFingerprint,
	 DecisionCode, StatusCode, DecisionNotes, AuthorizationMethodCode, AuthorizedByName, AuthorizedByTitle, AuthorityBasisCode,
	 AuthorizedDateUtc, CustomerAuthorizationId, IdempotencyKey, CreatedDateUtc, CreatedByUserId, IsDeleted)
SELECT NEWID(), p.TenantId, s.AccountId, p.SubmissionId, p.ProposalId, p.VersionNumber, ca.QuoteId, q.QuoteNumber,
	   CONVERT(char(64), HASHBYTES('SHA2_256', CONCAT(q.QuoteId, N'|', q.ModifiedDateUtc, N'|', q.AnnualPremium, N'|', q.ExpiresDateUtc)), 2),
	   N'LegacyIncomplete', N'LegacyIncomplete', COALESCE(p.DecisionNotes, N'Legacy acceptance requires compliant recapture.'),
	   ca.AuthorizationMethodCode, COALESCE(NULLIF(ca.AuthorizedByName, N''), N'Unknown legacy signer'), N'Legacy recapture required', N'AuthorizedRepresentative',
	   ca.AuthorizedDateUtc, ca.CustomerAuthorizationId, CONCAT(N'legacy-', CONVERT(nvarchar(36), p.ProposalId)), SYSUTCDATETIME(), ca.CreatedByUserId, 0
FROM Submissions.Proposal p
INNER JOIN Submissions.Submission s ON s.SubmissionId = p.SubmissionId AND s.TenantId = p.TenantId AND s.IsDeleted = 0
INNER JOIN Submissions.CustomerAuthorization ca ON ca.ProposalId = p.ProposalId AND ca.TenantId = p.TenantId AND ca.IsDeleted = 0
INNER JOIN Submissions.Quote q ON q.QuoteId = ca.QuoteId AND q.SubmissionId = p.SubmissionId AND q.IsDeleted = 0
WHERE p.ClientDecision = N'Accepted' AND p.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM Submissions.ClientAcceptance x WHERE x.TenantId = p.TenantId AND x.ProposalId = p.ProposalId AND x.IsDeleted = 0);
GO
