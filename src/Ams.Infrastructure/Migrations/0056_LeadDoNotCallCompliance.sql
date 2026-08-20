IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'CRM') EXEC(N'CREATE SCHEMA CRM');
GO

IF OBJECT_ID(N'CRM.PhoneComplianceReference', N'U') IS NULL
BEGIN
	CREATE TABLE CRM.PhoneComplianceReference
	(
		PhoneComplianceReferenceId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CRM_PhoneComplianceReference PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ReferenceTypeCode NVARCHAR(50) NOT NULL,
		Code NVARCHAR(50) NOT NULL,
		Name NVARCHAR(150) NOT NULL,
		Description NVARCHAR(500) NULL,
		SortOrder INT NOT NULL CONSTRAINT DF_PhoneComplianceReference_SortOrder DEFAULT 0,
		IsActive BIT NOT NULL CONSTRAINT DF_PhoneComplianceReference_IsActive DEFAULT 1,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PhoneComplianceReference_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PhoneComplianceReference_IsDeleted DEFAULT 0
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CRM.PhoneComplianceReference') AND name = N'UX_PhoneComplianceReference_Tenant_Type_Code')
	CREATE UNIQUE INDEX UX_PhoneComplianceReference_Tenant_Type_Code
		ON CRM.PhoneComplianceReference(TenantId, ReferenceTypeCode, Code) WHERE IsDeleted = 0;
GO

IF OBJECT_ID(N'CRM.PhoneComplianceProfile', N'U') IS NULL
BEGIN
	CREATE TABLE CRM.PhoneComplianceProfile
	(
		PhoneComplianceProfileId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CRM_PhoneComplianceProfile PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		NormalizedPhoneNumber NVARCHAR(20) NOT NULL,
		DisplayPhoneNumber NVARCHAR(50) NOT NULL,
		CountryCode NVARCHAR(3) NOT NULL CONSTRAINT DF_PhoneComplianceProfile_CountryCode DEFAULT N'US',
		OverallStatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PhoneComplianceProfile_OverallStatus DEFAULT N'Unknown',
		IsCallAllowed BIT NOT NULL CONSTRAINT DF_PhoneComplianceProfile_IsCallAllowed DEFAULT 0,
		IsSmsAllowed BIT NOT NULL CONSTRAINT DF_PhoneComplianceProfile_IsSmsAllowed DEFAULT 0,
		LastEvaluatedDateUtc DATETIME2 NULL,
		NextScreeningDueDateUtc DATETIME2 NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PhoneComplianceProfile_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PhoneComplianceProfile_IsDeleted DEFAULT 0
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CRM.PhoneComplianceProfile') AND name = N'UX_PhoneComplianceProfile_Tenant_Phone')
	CREATE UNIQUE INDEX UX_PhoneComplianceProfile_Tenant_Phone
		ON CRM.PhoneComplianceProfile(TenantId, NormalizedPhoneNumber) WHERE IsDeleted = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CRM.PhoneComplianceProfile') AND name = N'IX_PhoneComplianceProfile_ScreeningDue')
	CREATE INDEX IX_PhoneComplianceProfile_ScreeningDue
		ON CRM.PhoneComplianceProfile(NextScreeningDueDateUtc, TenantId) INCLUDE (NormalizedPhoneNumber, OverallStatusCode) WHERE IsDeleted = 0;
GO

IF OBJECT_ID(N'CRM.PhoneEntityLink', N'U') IS NULL
BEGIN
	CREATE TABLE CRM.PhoneEntityLink
	(
		PhoneEntityLinkId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CRM_PhoneEntityLink PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PhoneComplianceProfileId UNIQUEIDENTIFIER NOT NULL,
		EntityTypeCode NVARCHAR(50) NOT NULL,
		EntityId UNIQUEIDENTIFIER NOT NULL,
		IsPrimary BIT NOT NULL CONSTRAINT DF_PhoneEntityLink_IsPrimary DEFAULT 0,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PhoneEntityLink_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PhoneEntityLink_IsDeleted DEFAULT 0,
		CONSTRAINT FK_PhoneEntityLink_Profile FOREIGN KEY (PhoneComplianceProfileId) REFERENCES CRM.PhoneComplianceProfile(PhoneComplianceProfileId)
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CRM.PhoneEntityLink') AND name = N'UX_PhoneEntityLink_Entity_Profile')
	CREATE UNIQUE INDEX UX_PhoneEntityLink_Entity_Profile
		ON CRM.PhoneEntityLink(TenantId, EntityTypeCode, EntityId) WHERE IsDeleted = 0;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CRM.PhoneEntityLink') AND name = N'IX_PhoneEntityLink_Profile')
	CREATE INDEX IX_PhoneEntityLink_Profile ON CRM.PhoneEntityLink(TenantId, PhoneComplianceProfileId) WHERE IsDeleted = 0;
GO

IF OBJECT_ID(N'CRM.PhoneSuppression', N'U') IS NULL
BEGIN
	CREATE TABLE CRM.PhoneSuppression
	(
		PhoneSuppressionId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CRM_PhoneSuppression PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PhoneComplianceProfileId UNIQUEIDENTIFIER NOT NULL,
		SourceCode NVARCHAR(50) NOT NULL,
		ReasonCode NVARCHAR(50) NOT NULL,
		ChannelCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PhoneSuppression_Channel DEFAULT N'Call',
		PurposeCode NVARCHAR(50) NULL,
		JurisdictionCode NVARCHAR(20) NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PhoneSuppression_Status DEFAULT N'Active',
		EffectiveDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PhoneSuppression_EffectiveDateUtc DEFAULT SYSUTCDATETIME(),
		ExpirationDateUtc DATETIME2 NULL,
		RequestedDateUtc DATETIME2 NULL,
		Notes NVARCHAR(1000) NULL,
		EvidenceReference NVARCHAR(500) NULL,
		RevokedDateUtc DATETIME2 NULL,
		RevokedByUserId UNIQUEIDENTIFIER NULL,
		RevocationReason NVARCHAR(500) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PhoneSuppression_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PhoneSuppression_IsDeleted DEFAULT 0,
		CONSTRAINT FK_PhoneSuppression_Profile FOREIGN KEY (PhoneComplianceProfileId) REFERENCES CRM.PhoneComplianceProfile(PhoneComplianceProfileId)
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CRM.PhoneSuppression') AND name = N'IX_PhoneSuppression_Active')
	CREATE INDEX IX_PhoneSuppression_Active
		ON CRM.PhoneSuppression(TenantId, PhoneComplianceProfileId, ChannelCode, StatusCode, EffectiveDateUtc, ExpirationDateUtc) WHERE IsDeleted = 0;
GO

IF OBJECT_ID(N'CRM.PhoneConsent', N'U') IS NULL
BEGIN
	CREATE TABLE CRM.PhoneConsent
	(
		PhoneConsentId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CRM_PhoneConsent PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PhoneComplianceProfileId UNIQUEIDENTIFIER NOT NULL,
		ConsentTypeCode NVARCHAR(50) NOT NULL,
		ChannelCode NVARCHAR(50) NOT NULL,
		PurposeCode NVARCHAR(50) NOT NULL,
		LegalBasisCode NVARCHAR(50) NOT NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PhoneConsent_Status DEFAULT N'Granted',
		CapturedDateUtc DATETIME2 NOT NULL,
		EffectiveDateUtc DATETIME2 NOT NULL,
		ExpirationDateUtc DATETIME2 NULL,
		EvidenceTypeCode NVARCHAR(50) NOT NULL,
		EvidenceReference NVARCHAR(500) NOT NULL,
		ConsentText NVARCHAR(2000) NULL,
		ApprovedByUserId UNIQUEIDENTIFIER NULL,
		ApprovedDateUtc DATETIME2 NULL,
		RevokedDateUtc DATETIME2 NULL,
		RevokedByUserId UNIQUEIDENTIFIER NULL,
		RevocationReason NVARCHAR(500) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PhoneConsent_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PhoneConsent_IsDeleted DEFAULT 0,
		CONSTRAINT FK_PhoneConsent_Profile FOREIGN KEY (PhoneComplianceProfileId) REFERENCES CRM.PhoneComplianceProfile(PhoneComplianceProfileId)
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CRM.PhoneConsent') AND name = N'IX_PhoneConsent_Eligibility')
	CREATE INDEX IX_PhoneConsent_Eligibility
		ON CRM.PhoneConsent(TenantId, PhoneComplianceProfileId, ChannelCode, PurposeCode, StatusCode, EffectiveDateUtc, ExpirationDateUtc) WHERE IsDeleted = 0;
GO

IF OBJECT_ID(N'CRM.PhoneScreeningBatch', N'U') IS NULL
BEGIN
	CREATE TABLE CRM.PhoneScreeningBatch
	(
		PhoneScreeningBatchId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CRM_PhoneScreeningBatch PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		ProviderCode NVARCHAR(50) NOT NULL,
		SourceCode NVARCHAR(50) NOT NULL,
		FileName NVARCHAR(260) NULL,
		StatusCode NVARCHAR(50) NOT NULL CONSTRAINT DF_PhoneScreeningBatch_Status DEFAULT N'Pending',
		RequestedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PhoneScreeningBatch_RequestedDateUtc DEFAULT SYSUTCDATETIME(),
		StartedDateUtc DATETIME2 NULL,
		CompletedDateUtc DATETIME2 NULL,
		TotalRecords INT NOT NULL CONSTRAINT DF_PhoneScreeningBatch_Total DEFAULT 0,
		MatchedRecords INT NOT NULL CONSTRAINT DF_PhoneScreeningBatch_Matched DEFAULT 0,
		FailedRecords INT NOT NULL CONSTRAINT DF_PhoneScreeningBatch_Failed DEFAULT 0,
		ErrorDetails NVARCHAR(2000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PhoneScreeningBatch_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PhoneScreeningBatch_IsDeleted DEFAULT 0
	);
END;
GO

IF OBJECT_ID(N'CRM.PhoneScreeningResult', N'U') IS NULL
BEGIN
	CREATE TABLE CRM.PhoneScreeningResult
	(
		PhoneScreeningResultId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CRM_PhoneScreeningResult PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PhoneComplianceProfileId UNIQUEIDENTIFIER NOT NULL,
		PhoneScreeningBatchId UNIQUEIDENTIFIER NULL,
		ProviderCode NVARCHAR(50) NOT NULL,
		RegistryCode NVARCHAR(50) NOT NULL,
		JurisdictionCode NVARCHAR(20) NULL,
		ResultCode NVARCHAR(50) NOT NULL,
		ScreenedDateUtc DATETIME2 NOT NULL,
		ValidThroughDateUtc DATETIME2 NULL,
		ProviderReference NVARCHAR(200) NULL,
		RawResponseHash NVARCHAR(128) NULL,
		ErrorDetails NVARCHAR(1000) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PhoneScreeningResult_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PhoneScreeningResult_IsDeleted DEFAULT 0,
		CONSTRAINT FK_PhoneScreeningResult_Profile FOREIGN KEY (PhoneComplianceProfileId) REFERENCES CRM.PhoneComplianceProfile(PhoneComplianceProfileId),
		CONSTRAINT FK_PhoneScreeningResult_Batch FOREIGN KEY (PhoneScreeningBatchId) REFERENCES CRM.PhoneScreeningBatch(PhoneScreeningBatchId)
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CRM.PhoneScreeningResult') AND name = N'IX_PhoneScreeningResult_Profile_Date')
	CREATE INDEX IX_PhoneScreeningResult_Profile_Date
		ON CRM.PhoneScreeningResult(TenantId, PhoneComplianceProfileId, ScreenedDateUtc DESC) WHERE IsDeleted = 0;
GO

IF OBJECT_ID(N'CRM.PhoneComplianceDecisionAudit', N'U') IS NULL
BEGIN
	CREATE TABLE CRM.PhoneComplianceDecisionAudit
	(
		PhoneComplianceDecisionAuditId UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_CRM_PhoneComplianceDecisionAudit PRIMARY KEY DEFAULT NEWID(),
		TenantId UNIQUEIDENTIFIER NOT NULL,
		PhoneComplianceProfileId UNIQUEIDENTIFIER NOT NULL,
		LeadId UNIQUEIDENTIFIER NULL,
		LeadContactId UNIQUEIDENTIFIER NULL,
		ChannelCode NVARCHAR(50) NOT NULL,
		PurposeCode NVARCHAR(50) NOT NULL,
		DecisionCode NVARCHAR(50) NOT NULL,
		ReasonCode NVARCHAR(50) NOT NULL,
		DecisionSummary NVARCHAR(1000) NOT NULL,
		EvaluatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PhoneComplianceDecisionAudit_EvaluatedDateUtc DEFAULT SYSUTCDATETIME(),
		EvaluatedByUserId UNIQUEIDENTIFIER NULL,
		CorrelationId NVARCHAR(100) NULL,
		CreatedDateUtc DATETIME2 NOT NULL CONSTRAINT DF_PhoneComplianceDecisionAudit_CreatedDateUtc DEFAULT SYSUTCDATETIME(),
		CreatedByUserId UNIQUEIDENTIFIER NULL,
		ModifiedDateUtc DATETIME2 NULL,
		ModifiedByUserId UNIQUEIDENTIFIER NULL,
		IsDeleted BIT NOT NULL CONSTRAINT DF_PhoneComplianceDecisionAudit_IsDeleted DEFAULT 0,
		CONSTRAINT FK_PhoneComplianceDecisionAudit_Profile FOREIGN KEY (PhoneComplianceProfileId) REFERENCES CRM.PhoneComplianceProfile(PhoneComplianceProfileId)
	);
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CRM.PhoneComplianceDecisionAudit') AND name = N'IX_PhoneComplianceDecisionAudit_Lead_Date')
	CREATE INDEX IX_PhoneComplianceDecisionAudit_Lead_Date
		ON CRM.PhoneComplianceDecisionAudit(TenantId, LeadId, EvaluatedDateUtc DESC) WHERE IsDeleted = 0;
GO

;WITH ExistingPhones AS
(
	SELECT l.TenantId, l.Phone AS DisplayPhoneNumber,
		   REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(l.Phone, N'+', N''), N'(', N''), N')', N''), N'-', N''), N' ', N''), N'.', N''), CHAR(9), N''), N'/', N'') AS DigitsPhone
	FROM CRM.Lead l
	WHERE l.IsDeleted = 0 AND NULLIF(LTRIM(RTRIM(l.Phone)), N'') IS NOT NULL
	UNION
	SELECT c.TenantId, c.Phone,
		   REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(c.Phone, N'+', N''), N'(', N''), N')', N''), N'-', N''), N' ', N''), N'.', N''), CHAR(9), N''), N'/', N'')
	FROM CRM.LeadContact c
	WHERE c.IsDeleted = 0 AND NULLIF(LTRIM(RTRIM(c.Phone)), N'') IS NOT NULL
), NormalizedPhones AS
(
	SELECT TenantId, MAX(DisplayPhoneNumber) AS DisplayPhoneNumber,
		   CASE WHEN LEN(DigitsPhone) = 10 THEN N'+1' + DigitsPhone ELSE N'+' + DigitsPhone END AS NormalizedPhoneNumber
	FROM ExistingPhones
	WHERE LEN(DigitsPhone) BETWEEN 10 AND 15 AND DigitsPhone NOT LIKE N'%[^0-9]%'
	GROUP BY TenantId, CASE WHEN LEN(DigitsPhone) = 10 THEN N'+1' + DigitsPhone ELSE N'+' + DigitsPhone END
)
INSERT INTO CRM.PhoneComplianceProfile
(PhoneComplianceProfileId, TenantId, NormalizedPhoneNumber, DisplayPhoneNumber, CountryCode, OverallStatusCode,
 IsCallAllowed, IsSmsAllowed, NextScreeningDueDateUtc, CreatedDateUtc, IsDeleted)
SELECT NEWID(), p.TenantId, p.NormalizedPhoneNumber, p.DisplayPhoneNumber, N'US', N'PendingScreening',
	   0, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 0
FROM NormalizedPhones p
WHERE NOT EXISTS
(
	SELECT 1 FROM CRM.PhoneComplianceProfile existing
	WHERE existing.TenantId = p.TenantId AND existing.NormalizedPhoneNumber = p.NormalizedPhoneNumber AND existing.IsDeleted = 0
);
GO

;WITH LeadPhones AS
(
	SELECT l.LeadId, l.TenantId,
		   CASE WHEN LEN(d.DigitsPhone) = 10 THEN N'+1' + d.DigitsPhone ELSE N'+' + d.DigitsPhone END AS NormalizedPhoneNumber
	FROM CRM.Lead l
	CROSS APPLY (SELECT REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(l.Phone, N'+', N''), N'(', N''), N')', N''), N'-', N''), N' ', N''), N'.', N''), CHAR(9), N''), N'/', N'') AS DigitsPhone) d
	WHERE l.IsDeleted = 0 AND LEN(d.DigitsPhone) BETWEEN 10 AND 15 AND d.DigitsPhone NOT LIKE N'%[^0-9]%'
)
INSERT INTO CRM.PhoneEntityLink
(PhoneEntityLinkId, TenantId, PhoneComplianceProfileId, EntityTypeCode, EntityId, IsPrimary, CreatedDateUtc, IsDeleted)
SELECT NEWID(), l.TenantId, p.PhoneComplianceProfileId, N'Lead', l.LeadId, 1, SYSUTCDATETIME(), 0
FROM LeadPhones l
JOIN CRM.PhoneComplianceProfile p ON p.TenantId = l.TenantId AND p.NormalizedPhoneNumber = l.NormalizedPhoneNumber AND p.IsDeleted = 0
WHERE NOT EXISTS
(
	SELECT 1 FROM CRM.PhoneEntityLink existing
	WHERE existing.TenantId = l.TenantId AND existing.EntityTypeCode = N'Lead' AND existing.EntityId = l.LeadId
	  AND existing.PhoneComplianceProfileId = p.PhoneComplianceProfileId AND existing.IsDeleted = 0
);
GO

;WITH ContactPhones AS
(
	SELECT c.ContactId, c.TenantId, c.IsPrimary,
		   CASE WHEN LEN(d.DigitsPhone) = 10 THEN N'+1' + d.DigitsPhone ELSE N'+' + d.DigitsPhone END AS NormalizedPhoneNumber
	FROM CRM.LeadContact c
	CROSS APPLY (SELECT REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(c.Phone, N'+', N''), N'(', N''), N')', N''), N'-', N''), N' ', N''), N'.', N''), CHAR(9), N''), N'/', N'') AS DigitsPhone) d
	WHERE c.IsDeleted = 0 AND LEN(d.DigitsPhone) BETWEEN 10 AND 15 AND d.DigitsPhone NOT LIKE N'%[^0-9]%'
)
INSERT INTO CRM.PhoneEntityLink
(PhoneEntityLinkId, TenantId, PhoneComplianceProfileId, EntityTypeCode, EntityId, IsPrimary, CreatedDateUtc, IsDeleted)
SELECT NEWID(), c.TenantId, p.PhoneComplianceProfileId, N'LeadContact', c.ContactId, c.IsPrimary, SYSUTCDATETIME(), 0
FROM ContactPhones c
JOIN CRM.PhoneComplianceProfile p ON p.TenantId = c.TenantId AND p.NormalizedPhoneNumber = c.NormalizedPhoneNumber AND p.IsDeleted = 0
WHERE NOT EXISTS
(
	SELECT 1 FROM CRM.PhoneEntityLink existing
	WHERE existing.TenantId = c.TenantId AND existing.EntityTypeCode = N'LeadContact' AND existing.EntityId = c.ContactId
	  AND existing.PhoneComplianceProfileId = p.PhoneComplianceProfileId AND existing.IsDeleted = 0
);
GO

DECLARE @GlobalTenantId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000001';
DECLARE @References TABLE
(
	ReferenceTypeCode NVARCHAR(50),
	Code NVARCHAR(50),
	Name NVARCHAR(150),
	Description NVARCHAR(500),
	SortOrder INT
);

INSERT INTO @References VALUES
(N'Channel', N'Call', N'Voice Call', N'Outbound voice telephone calls.', 10),
(N'Channel', N'Sms', N'SMS / Text', N'Outbound SMS and text messages.', 20),
(N'Purpose', N'Marketing', N'Marketing', N'Solicitation, cross-sell, campaigns, and promotional outreach.', 10),
(N'Purpose', N'QuoteFollowUp', N'Quote Follow-up', N'Follow-up regarding an insurance quote or application.', 20),
(N'Purpose', N'Service', N'Customer Service', N'Non-marketing service communication.', 30),
(N'Purpose', N'Renewal', N'Renewal', N'Policy renewal communication.', 40),
(N'Purpose', N'Claims', N'Claims', N'Claim-related communication.', 50),
(N'ConsentType', N'ExpressWritten', N'Express Written Consent', N'Affirmative written consent for the stated channel and purpose.', 10),
(N'ConsentType', N'Express', N'Express Consent', N'Affirmative consent for the stated channel and purpose.', 20),
(N'ConsentType', N'Implied', N'Implied Consent', N'Consent inferred only where the documented legal basis permits it.', 30),
(N'SuppressionSource', N'InternalRequest', N'Internal DNC Request', N'Consumer requested that the agency stop calling.', 10),
(N'SuppressionSource', N'FederalRegistry', N'Federal Registry', N'National Do Not Call registry match.', 20),
(N'SuppressionSource', N'StateRegistry', N'State Registry', N'State Do Not Call registry match.', 30),
(N'SuppressionSource', N'Provider', N'Compliance Provider', N'External compliance provider suppression result.', 40),
(N'SuppressionSource', N'InvalidNumber', N'Invalid Number', N'Invalid, disconnected, or otherwise unreachable phone number.', 50),
(N'SuppressionReason', N'ConsumerRequest', N'Consumer Request', N'Consumer directly requested suppression.', 10),
(N'SuppressionReason', N'RegistryMatch', N'Registry Match', N'Phone number matched an applicable registry.', 20),
(N'SuppressionReason', N'ConsentRevoked', N'Consent Revoked', N'Previously documented consent was revoked.', 30),
(N'SuppressionReason', N'WrongParty', N'Wrong Party', N'Phone number belongs to another party.', 40),
(N'LegalBasis', N'PriorExpressWrittenConsent', N'Prior Express Written Consent', N'Documented prior express written consent for the stated purpose.', 10),
(N'LegalBasis', N'PriorExpressConsent', N'Prior Express Consent', N'Documented prior express consent for the stated purpose.', 20),
(N'LegalBasis', N'ExistingBusinessRelationship', N'Existing Business Relationship', N'Existing business relationship where legally applicable.', 30),
(N'LegalBasis', N'NonTelemarketingService', N'Non-Telemarketing Service', N'Non-promotional service purpose where legally applicable.', 40),
(N'EvidenceType', N'SignedDocument', N'Signed Document', N'Signed paper or electronic consent document.', 10),
(N'EvidenceType', N'WebForm', N'Web Form', N'Consent captured through a web form.', 20),
(N'EvidenceType', N'RecordedCall', N'Recorded Call', N'Consent captured in a recorded call.', 30),
(N'EvidenceType', N'Email', N'Email', N'Consent evidenced by an email record.', 40),
(N'EvidenceType', N'Portal', N'Portal', N'Consent captured through the customer portal.', 50),
(N'ScreeningProvider', N'ManualImport', N'Manual Registry Import', N'Provider-neutral registry result import.', 10),
(N'ScreeningResult', N'Clear', N'Clear', N'No applicable registry match was found.', 10),
(N'ScreeningResult', N'Matched', N'Matched', N'An applicable registry match was found.', 20),
(N'ScreeningResult', N'Unknown', N'Unknown', N'Screening has not completed or was inconclusive.', 30),
(N'ScreeningResult', N'Failed', N'Failed', N'Screening failed and requires review.', 40);

MERGE CRM.PhoneComplianceReference AS target
USING @References AS source
ON target.TenantId = @GlobalTenantId
AND target.ReferenceTypeCode = source.ReferenceTypeCode
AND target.Code = source.Code
AND target.IsDeleted = 0
WHEN MATCHED THEN
	UPDATE SET Name = source.Name, Description = source.Description, SortOrder = source.SortOrder, IsActive = 1, ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
	INSERT (PhoneComplianceReferenceId, TenantId, ReferenceTypeCode, Code, Name, Description, SortOrder, IsActive, CreatedDateUtc, IsDeleted)
	VALUES (NEWID(), @GlobalTenantId, source.ReferenceTypeCode, source.Code, source.Name, source.Description, source.SortOrder, 1, SYSUTCDATETIME(), 0);
GO

IF OBJECT_ID(N'Core.ConfigurationSetting', N'U') IS NOT NULL
BEGIN
	MERGE Core.ConfigurationSetting AS target
	USING
	(
		SELECT N'Dnc.ScreeningWorker.Enabled' AS SettingKey, N'false' AS SettingValue, N'Boolean' AS DataTypeCode, N'Enable due phone registry screening after a screening provider is configured.' AS Description
		UNION ALL SELECT N'Dnc.ScreeningWorker.PollIntervalSeconds', N'300', N'Integer', N'Polling interval for due phone compliance screenings.'
		UNION ALL SELECT N'Dnc.ScreeningWorker.BatchSize', N'100', N'Integer', N'Maximum due phone profiles processed in one screening cycle.'
		UNION ALL SELECT N'Dnc.ScreeningWorker.ProviderCode', N'', N'Text', N'Configured screening provider code; must match a registered provider implementation.'
	) AS source
	ON target.TenantId IS NULL AND target.ScopeCode = N'Platform' AND target.SettingKey = source.SettingKey AND target.IsDeleted = 0
	WHEN MATCHED THEN UPDATE SET Description = source.Description, DataTypeCode = source.DataTypeCode, ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED THEN
		INSERT (SettingId, TenantId, ScopeCode, SettingKey, SettingValue, DataTypeCode, DefaultValue, Description, IsEncrypted, IsReadOnly, ModuleCode, CreatedDateUtc, IsDeleted)
		VALUES (NEWID(), NULL, N'Platform', source.SettingKey, source.SettingValue, source.DataTypeCode, source.SettingValue, source.Description, 0, 0, N'CRMCompliance', SYSUTCDATETIME(), 0);
END;
GO
