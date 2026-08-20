SET XACT_ABORT ON;

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Policy') EXEC(N'CREATE SCHEMA Policy');
GO

IF OBJECT_ID(N'Compliance.PolicyDocument', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Compliance.PolicyDocument', N'PolicyId') IS NULL
		ALTER TABLE Compliance.PolicyDocument ADD PolicyId UNIQUEIDENTIFIER NULL;

	EXEC(N'
		UPDATE document
		SET PolicyId = policy.PolicyId,
			ModifiedDateUtc = COALESCE(document.ModifiedDateUtc, SYSUTCDATETIME())
		FROM Compliance.PolicyDocument document
		INNER JOIN Submissions.BoundPolicy policy
			ON policy.TenantId = document.TenantId
		   AND policy.PolicyNumber = document.PolicyCode
		   AND policy.IsDeleted = 0
		WHERE document.PolicyId IS NULL
		  AND document.IsDeleted = 0;');

	IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Compliance.PolicyDocument') AND name = N'IX_CompliancePolicyDocument_Policy')
		EXEC(N'CREATE INDEX IX_CompliancePolicyDocument_Policy ON Compliance.PolicyDocument(TenantId, PolicyId, IsActive, IsDeleted) INCLUDE (PolicyCode, PolicyTitle, PolicyTypeCode, Version, StatusCode);');
END;
GO

IF OBJECT_ID(N'OPS.OperationalActivityLog', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'OPS.OperationalActivityLog', N'PolicyId') IS NULL ALTER TABLE OPS.OperationalActivityLog ADD PolicyId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'OPS.OperationalActivityLog', N'PolicyTransactionId') IS NULL ALTER TABLE OPS.OperationalActivityLog ADD PolicyTransactionId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'OPS.OperationalActivityLog', N'ChannelCode') IS NULL ALTER TABLE OPS.OperationalActivityLog ADD ChannelCode NVARCHAR(80) NULL;
	IF COL_LENGTH(N'OPS.OperationalActivityLog', N'OutcomeCode') IS NULL ALTER TABLE OPS.OperationalActivityLog ADD OutcomeCode NVARCHAR(80) NULL;
	IF COL_LENGTH(N'OPS.OperationalActivityLog', N'StatusCode') IS NULL ALTER TABLE OPS.OperationalActivityLog ADD StatusCode NVARCHAR(80) NOT NULL CONSTRAINT DF_OperationalActivityLog_Status_0071 DEFAULT N'Completed';

	IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'OPS.OperationalActivityLog') AND name = N'IX_OperationalActivityLog_Policy')
		EXEC(N'CREATE INDEX IX_OperationalActivityLog_Policy ON OPS.OperationalActivityLog(TenantId, PolicyId, ActivityDate DESC, CreatedDateUtc DESC) INCLUDE (ActivityTypeCode, Subject, ChannelCode, OutcomeCode, StatusCode) WHERE IsDeleted = 0;');
END;
GO

IF OBJECT_ID(N'Comms.MessageThread', N'U') IS NOT NULL
BEGIN
	IF COL_LENGTH(N'Comms.MessageThread', N'PolicyId') IS NULL ALTER TABLE Comms.MessageThread ADD PolicyId UNIQUEIDENTIFIER NULL;
	IF COL_LENGTH(N'Comms.MessageThread', N'PolicyTransactionId') IS NULL ALTER TABLE Comms.MessageThread ADD PolicyTransactionId UNIQUEIDENTIFIER NULL;

	IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Comms.MessageThread') AND name = N'IX_MessageThread_Policy')
		EXEC(N'CREATE INDEX IX_MessageThread_Policy ON Comms.MessageThread(TenantId, PolicyId, LastActivityAt DESC) INCLUDE (ThreadId, Channel, Subject, Status, MessageCount) WHERE IsDeleted = 0;');
END;
GO

IF OBJECT_ID(N'Policy.PolicyLifecycleOption', N'U') IS NOT NULL
BEGIN
	IF OBJECT_ID(N'tempdb..#PolicyServicingTenants') IS NOT NULL DROP TABLE #PolicyServicingTenants;
	CREATE TABLE #PolicyServicingTenants (TenantId UNIQUEIDENTIFIER NOT NULL PRIMARY KEY);

	IF OBJECT_ID(N'Core.Tenant', N'U') IS NOT NULL
		INSERT INTO #PolicyServicingTenants (TenantId)
		SELECT TenantId FROM Core.Tenant WHERE IsDeleted = 0;

	IF OBJECT_ID(N'Submissions.BoundPolicy', N'U') IS NOT NULL
		INSERT INTO #PolicyServicingTenants (TenantId)
		SELECT DISTINCT TenantId FROM Submissions.BoundPolicy policy
		WHERE policy.IsDeleted = 0
		  AND NOT EXISTS (SELECT 1 FROM #PolicyServicingTenants tenant WHERE tenant.TenantId = policy.TenantId);

	IF OBJECT_ID(N'tempdb..#PolicyServicingOptions') IS NOT NULL DROP TABLE #PolicyServicingOptions;
	CREATE TABLE #PolicyServicingOptions
	(
		OptionGroupCode NVARCHAR(80) NOT NULL,
		OptionCode NVARCHAR(80) NOT NULL,
		DisplayName NVARCHAR(160) NOT NULL,
		Description NVARCHAR(500) NULL,
		IsTerminal BIT NOT NULL,
		IsPremiumBearing BIT NOT NULL,
		RequiresDocument BIT NOT NULL,
		IsDefault BIT NOT NULL,
		SortOrder INT NOT NULL
	);

	INSERT INTO #PolicyServicingOptions VALUES
	(N'PolicyTransactionType', N'Expiration', N'Expiration', N'Closes an expired policy term without deleting policy history.', 1, 0, 0, 0, 110),
	(N'PolicyTransactionType', N'Archive', N'Archive', N'Archives a completed policy lifecycle while preserving all history.', 1, 0, 0, 0, 120),
	(N'PolicyTransactionType', N'Refund', N'Refund', N'Refund transaction linked to policy billing and accounting.', 0, 1, 1, 0, 130),
	(N'PolicyDocumentRole', N'Certificate', N'Certificate', N'Certificate of insurance generated for a policy holder.', 0, 0, 0, 0, 70),
	(N'PolicyDocumentRole', N'FirstNoticeOfLoss', N'First Notice of Loss', N'First notice of loss linked to a policy claim.', 0, 0, 0, 0, 80),
	(N'PolicyDocumentRole', N'RewriteEvidence', N'Rewrite Evidence', N'Rewrite quote, binder, or issued policy evidence.', 0, 0, 0, 0, 90),
	(N'CertificateType', N'ACORD25', N'ACORD 25 - Certificate of Liability Insurance', N'Liability certificate.', 0, 0, 0, 1, 10),
	(N'CertificateType', N'ACORD27', N'ACORD 27 - Evidence of Property Insurance', N'Property evidence form.', 0, 0, 0, 0, 20),
	(N'CertificateType', N'ACORD28', N'ACORD 28 - Evidence of Commercial Property Insurance', N'Commercial property evidence form.', 0, 0, 0, 0, 30),
	(N'CommunicationChannel', N'Email', N'Email', N'Tenant email delivery provider.', 0, 0, 0, 1, 10),
	(N'CommunicationChannel', N'SMS', N'SMS', N'Tenant SMS delivery provider.', 0, 0, 0, 0, 20),
	(N'CommunicationChannel', N'Phone', N'Phone', N'Phone communication log.', 0, 0, 0, 0, 30),
	(N'CommunicationChannel', N'Portal', N'Portal', N'Client portal communication.', 0, 0, 0, 0, 40),
	(N'CommunicationChannel', N'Letter', N'Letter', N'Printed correspondence.', 0, 0, 0, 0, 50),
	(N'PolicyActivityType', N'Call', N'Call', N'Policy servicing call.', 0, 0, 0, 0, 10),
	(N'PolicyActivityType', N'Email', N'Email', N'Policy servicing email.', 0, 0, 0, 1, 20),
	(N'PolicyActivityType', N'Note', N'Note', N'Policy servicing note.', 0, 0, 0, 0, 30),
	(N'PolicyActivityType', N'Meeting', N'Meeting', N'Policy servicing meeting.', 0, 0, 0, 0, 40),
	(N'PolicyActivityType', N'Renewal', N'Renewal', N'Renewal servicing activity.', 0, 0, 0, 0, 50),
	(N'PolicyActivityType', N'Claim', N'Claim', N'Claim servicing activity.', 0, 0, 0, 0, 60),
	(N'PolicyActivityType', N'Endorsement', N'Endorsement', N'Endorsement servicing activity.', 0, 0, 0, 0, 70),
	(N'PolicyActivityOutcome', N'Completed', N'Completed', N'Activity completed.', 1, 0, 0, 1, 10),
	(N'PolicyActivityOutcome', N'Pending', N'Pending', N'Activity is pending.', 0, 0, 0, 0, 20),
	(N'PolicyActivityOutcome', N'FollowUpRequired', N'Follow-Up Required', N'Additional servicing action is required.', 0, 0, 0, 0, 30),
	(N'PolicyActivityOutcome', N'NoAnswer', N'No Answer', N'Contact attempt was not answered.', 1, 0, 0, 0, 40),
	(N'PolicyActivityOutcome', N'LeftVoicemail', N'Left Voicemail', N'A voicemail was left.', 1, 0, 0, 0, 50);

	MERGE Policy.PolicyLifecycleOption AS target
	USING
	(
		SELECT tenant.TenantId, optionRow.*
		FROM #PolicyServicingTenants tenant
		CROSS JOIN #PolicyServicingOptions optionRow
	) AS source
	ON target.TenantId = source.TenantId
	   AND target.OptionGroupCode = source.OptionGroupCode
	   AND target.OptionCode = source.OptionCode
	   AND target.IsDeleted = 0
	WHEN MATCHED THEN UPDATE SET
		DisplayName = source.DisplayName,
		Description = source.Description,
		IsTerminal = source.IsTerminal,
		IsPremiumBearing = source.IsPremiumBearing,
		RequiresDocument = source.RequiresDocument,
		IsDefault = source.IsDefault,
		IsActive = 1,
		SortOrder = source.SortOrder,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT
		(PolicyLifecycleOptionId, TenantId, OptionGroupCode, OptionCode, DisplayName, Description, IsTerminal, IsPremiumBearing, RequiresDocument, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
	VALUES
		(NEWID(), source.TenantId, source.OptionGroupCode, source.OptionCode, source.DisplayName, source.Description, source.IsTerminal, source.IsPremiumBearing, source.RequiresDocument, source.IsDefault, 1, source.SortOrder, SYSUTCDATETIME(), 0);
END;
GO
