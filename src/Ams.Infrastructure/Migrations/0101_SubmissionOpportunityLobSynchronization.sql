IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'CRM') EXEC(N'CREATE SCHEMA CRM');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Submissions') EXEC(N'CREATE SCHEMA Submissions');
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'Agency') EXEC(N'CREATE SCHEMA Agency');

IF OBJECT_ID(N'CRM.OpportunityLine', N'U') IS NOT NULL
   AND COL_LENGTH(N'CRM.OpportunityLine', N'LobId') IS NULL
	ALTER TABLE CRM.OpportunityLine ADD LobId UNIQUEIDENTIFIER NULL;

IF OBJECT_ID(N'Submissions.Submission', N'U') IS NOT NULL
   AND COL_LENGTH(N'Submissions.Submission', N'LobId') IS NULL
	ALTER TABLE Submissions.Submission ADD LobId UNIQUEIDENTIFIER NULL;

IF OBJECT_ID(N'Submissions.Submission', N'U') IS NOT NULL
   AND COL_LENGTH(N'Submissions.Submission', N'RiskState') IS NULL
	ALTER TABLE Submissions.Submission ADD RiskState NVARCHAR(2) NULL;

IF OBJECT_ID(N'Submissions.Submission', N'U') IS NOT NULL
   AND COL_LENGTH(N'Submissions.Submission', N'NamedInsured') IS NULL
	ALTER TABLE Submissions.Submission ADD NamedInsured NVARCHAR(200) NULL;

IF OBJECT_ID(N'Submissions.Submission', N'U') IS NOT NULL
   AND COL_LENGTH(N'Submissions.Submission', N'CsrUserId') IS NULL
	ALTER TABLE Submissions.Submission ADD CsrUserId UNIQUEIDENTIFIER NULL;

IF OBJECT_ID(N'Submissions.Submission', N'U') IS NOT NULL
   AND COL_LENGTH(N'Submissions.Submission', N'Description') IS NULL
	ALTER TABLE Submissions.Submission ADD Description NVARCHAR(2000) NULL;

IF OBJECT_ID(N'Submissions.Submission', N'U') IS NOT NULL
   AND COL_LENGTH(N'Submissions.Submission', N'InternalNotes') IS NULL
	ALTER TABLE Submissions.Submission ADD InternalNotes NVARCHAR(4000) NULL;

IF OBJECT_ID(N'Submissions.Submission', N'U') IS NOT NULL
   AND COL_LENGTH(N'Submissions.Submission', N'IsRush') IS NULL
	ALTER TABLE Submissions.Submission ADD IsRush BIT NOT NULL CONSTRAINT DF_Submission_IsRush_0101 DEFAULT 0;

IF OBJECT_ID(N'Submissions.SubmissionLine', N'U') IS NOT NULL
   AND COL_LENGTH(N'Submissions.SubmissionLine', N'LobId') IS NULL
	ALTER TABLE Submissions.SubmissionLine ADD LobId UNIQUEIDENTIFIER NULL;

IF OBJECT_ID(N'Agency.LineOfBusiness', N'U') IS NOT NULL
BEGIN
	EXEC(N'
		UPDATE line
		SET LobId = lob.LobId
		FROM CRM.OpportunityLine line
		INNER JOIN Agency.LineOfBusiness lob
			ON lob.TenantId = line.TenantId
		   AND lob.IsDeleted = 0
		   AND (lob.LobName = line.LineOfBusiness OR lob.LobCode = line.LineOfBusiness)
		WHERE line.LobId IS NULL
		  AND line.IsDeleted = 0;

		UPDATE submission
		SET LobId = COALESCE(primaryLine.LobId, lob.LobId)
		FROM Submissions.Submission submission
		LEFT JOIN CRM.Opportunity opportunity
			ON opportunity.TenantId = submission.TenantId
		   AND opportunity.OpportunityId = submission.OpportunityId
		   AND opportunity.IsDeleted = 0
		LEFT JOIN CRM.OpportunityLine primaryLine
			ON primaryLine.TenantId = submission.TenantId
		   AND primaryLine.OpportunityLineId = opportunity.PrimaryOpportunityLineId
		   AND primaryLine.IsDeleted = 0
		LEFT JOIN Agency.LineOfBusiness lob
			ON lob.TenantId = submission.TenantId
		   AND lob.IsDeleted = 0
		   AND (lob.LobName = submission.LineOfBusiness OR lob.LobCode = submission.LineOfBusiness)
		WHERE submission.LobId IS NULL
		  AND submission.IsDeleted = 0;

		UPDATE submissionLine
		SET LobId = COALESCE(opportunityLine.LobId, lob.LobId)
		FROM Submissions.SubmissionLine submissionLine
		LEFT JOIN CRM.OpportunityLine opportunityLine
			ON opportunityLine.TenantId = submissionLine.TenantId
		   AND opportunityLine.OpportunityLineId = submissionLine.OpportunityLineId
		   AND opportunityLine.IsDeleted = 0
		LEFT JOIN Agency.LineOfBusiness lob
			ON lob.TenantId = submissionLine.TenantId
		   AND lob.IsDeleted = 0
		   AND (lob.LobName = submissionLine.LineOfBusiness OR lob.LobCode = submissionLine.LineOfBusiness)
		WHERE submissionLine.LobId IS NULL
		  AND submissionLine.IsDeleted = 0;');
END;

IF OBJECT_ID(N'CRM.OpportunityLine', N'U') IS NOT NULL
   AND OBJECT_ID(N'Agency.LineOfBusiness', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_OpportunityLine_LineOfBusiness_0101')
	EXEC(N'ALTER TABLE CRM.OpportunityLine WITH CHECK ADD CONSTRAINT FK_OpportunityLine_LineOfBusiness_0101 FOREIGN KEY (LobId) REFERENCES Agency.LineOfBusiness(LobId);');

IF OBJECT_ID(N'Submissions.Submission', N'U') IS NOT NULL
   AND OBJECT_ID(N'Agency.LineOfBusiness', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_Submission_LineOfBusiness_0101')
	EXEC(N'ALTER TABLE Submissions.Submission WITH CHECK ADD CONSTRAINT FK_Submission_LineOfBusiness_0101 FOREIGN KEY (LobId) REFERENCES Agency.LineOfBusiness(LobId);');

IF OBJECT_ID(N'Submissions.SubmissionLine', N'U') IS NOT NULL
   AND OBJECT_ID(N'Agency.LineOfBusiness', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_SubmissionLine_LineOfBusiness_0101')
	EXEC(N'ALTER TABLE Submissions.SubmissionLine WITH CHECK ADD CONSTRAINT FK_SubmissionLine_LineOfBusiness_0101 FOREIGN KEY (LobId) REFERENCES Agency.LineOfBusiness(LobId);');

IF OBJECT_ID(N'CRM.OpportunityLine', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'CRM.OpportunityLine') AND name = N'IX_OpportunityLine_Lob_0101')
	EXEC(N'CREATE INDEX IX_OpportunityLine_Lob_0101 ON CRM.OpportunityLine(TenantId, LobId, IsDeleted, IsPrimary);');

IF OBJECT_ID(N'Submissions.Submission', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.Submission') AND name = N'IX_Submission_Lob_0101')
	EXEC(N'CREATE INDEX IX_Submission_Lob_0101 ON Submissions.Submission(TenantId, LobId, IsDeleted);');

IF OBJECT_ID(N'Submissions.Submission', N'U') IS NOT NULL
   AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'Submissions.Submission') AND name = N'IX_Submission_Csr_0101')
	EXEC(N'CREATE INDEX IX_Submission_Csr_0101 ON Submissions.Submission(TenantId, CsrUserId, IsDeleted);');

IF OBJECT_ID(N'Submissions.SubmissionReferenceOption', N'U') IS NOT NULL
BEGIN
	INSERT INTO Submissions.SubmissionReferenceOption
		(SubmissionReferenceOptionId, TenantId, OptionGroup, OptionCode, OptionName, Description, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
	SELECT NEWID(), tenant.TenantId, optionValue.OptionGroup, optionValue.OptionCode, optionValue.OptionName, optionValue.Description, optionValue.IsDefault, 1, optionValue.SortOrder, SYSUTCDATETIME(), 0
	FROM
	(
		SELECT TenantId FROM Core.Tenant WHERE IsDeleted = 0
		UNION
		SELECT DISTINCT TenantId FROM Submissions.Submission WHERE IsDeleted = 0
	) tenant
	CROSS JOIN
	(
		VALUES
			(N'SubmissionPriority', N'Low', N'Low', N'Low-priority submission.', CAST(0 AS bit), 10),
			(N'SubmissionPriority', N'Standard', N'Standard', N'Standard-priority submission.', CAST(1 AS bit), 20),
			(N'SubmissionPriority', N'High', N'High', N'High-priority submission.', CAST(0 AS bit), 30),
			(N'SubmissionPriority', N'Urgent', N'Urgent', N'Urgent submission requiring accelerated handling.', CAST(0 AS bit), 40),
			(N'RiskState', N'AL', N'Alabama', N'US state or jurisdiction.', CAST(0 AS bit), 10),
			(N'RiskState', N'AK', N'Alaska', N'US state or jurisdiction.', CAST(0 AS bit), 20),
			(N'RiskState', N'AZ', N'Arizona', N'US state or jurisdiction.', CAST(0 AS bit), 30),
			(N'RiskState', N'AR', N'Arkansas', N'US state or jurisdiction.', CAST(0 AS bit), 40),
			(N'RiskState', N'CA', N'California', N'US state or jurisdiction.', CAST(0 AS bit), 50),
			(N'RiskState', N'CO', N'Colorado', N'US state or jurisdiction.', CAST(0 AS bit), 60),
			(N'RiskState', N'CT', N'Connecticut', N'US state or jurisdiction.', CAST(0 AS bit), 70),
			(N'RiskState', N'DE', N'Delaware', N'US state or jurisdiction.', CAST(0 AS bit), 80),
			(N'RiskState', N'FL', N'Florida', N'US state or jurisdiction.', CAST(0 AS bit), 90),
			(N'RiskState', N'GA', N'Georgia', N'US state or jurisdiction.', CAST(0 AS bit), 100),
			(N'RiskState', N'HI', N'Hawaii', N'US state or jurisdiction.', CAST(0 AS bit), 110),
			(N'RiskState', N'ID', N'Idaho', N'US state or jurisdiction.', CAST(0 AS bit), 120),
			(N'RiskState', N'IL', N'Illinois', N'US state or jurisdiction.', CAST(0 AS bit), 130),
			(N'RiskState', N'IN', N'Indiana', N'US state or jurisdiction.', CAST(0 AS bit), 140),
			(N'RiskState', N'IA', N'Iowa', N'US state or jurisdiction.', CAST(0 AS bit), 150),
			(N'RiskState', N'KS', N'Kansas', N'US state or jurisdiction.', CAST(0 AS bit), 160),
			(N'RiskState', N'KY', N'Kentucky', N'US state or jurisdiction.', CAST(0 AS bit), 170),
			(N'RiskState', N'LA', N'Louisiana', N'US state or jurisdiction.', CAST(0 AS bit), 180),
			(N'RiskState', N'ME', N'Maine', N'US state or jurisdiction.', CAST(0 AS bit), 190),
			(N'RiskState', N'MD', N'Maryland', N'US state or jurisdiction.', CAST(0 AS bit), 200),
			(N'RiskState', N'MA', N'Massachusetts', N'US state or jurisdiction.', CAST(0 AS bit), 210),
			(N'RiskState', N'MI', N'Michigan', N'US state or jurisdiction.', CAST(0 AS bit), 220),
			(N'RiskState', N'MN', N'Minnesota', N'US state or jurisdiction.', CAST(0 AS bit), 230),
			(N'RiskState', N'MS', N'Mississippi', N'US state or jurisdiction.', CAST(0 AS bit), 240),
			(N'RiskState', N'MO', N'Missouri', N'US state or jurisdiction.', CAST(0 AS bit), 250),
			(N'RiskState', N'MT', N'Montana', N'US state or jurisdiction.', CAST(0 AS bit), 260),
			(N'RiskState', N'NE', N'Nebraska', N'US state or jurisdiction.', CAST(0 AS bit), 270),
			(N'RiskState', N'NV', N'Nevada', N'US state or jurisdiction.', CAST(0 AS bit), 280),
			(N'RiskState', N'NH', N'New Hampshire', N'US state or jurisdiction.', CAST(0 AS bit), 290),
			(N'RiskState', N'NJ', N'New Jersey', N'US state or jurisdiction.', CAST(0 AS bit), 300),
			(N'RiskState', N'NM', N'New Mexico', N'US state or jurisdiction.', CAST(0 AS bit), 310),
			(N'RiskState', N'NY', N'New York', N'US state or jurisdiction.', CAST(0 AS bit), 320),
			(N'RiskState', N'NC', N'North Carolina', N'US state or jurisdiction.', CAST(0 AS bit), 330),
			(N'RiskState', N'ND', N'North Dakota', N'US state or jurisdiction.', CAST(0 AS bit), 340),
			(N'RiskState', N'OH', N'Ohio', N'US state or jurisdiction.', CAST(0 AS bit), 350),
			(N'RiskState', N'OK', N'Oklahoma', N'US state or jurisdiction.', CAST(0 AS bit), 360),
			(N'RiskState', N'OR', N'Oregon', N'US state or jurisdiction.', CAST(0 AS bit), 370),
			(N'RiskState', N'PA', N'Pennsylvania', N'US state or jurisdiction.', CAST(0 AS bit), 380),
			(N'RiskState', N'RI', N'Rhode Island', N'US state or jurisdiction.', CAST(0 AS bit), 390),
			(N'RiskState', N'SC', N'South Carolina', N'US state or jurisdiction.', CAST(0 AS bit), 400),
			(N'RiskState', N'SD', N'South Dakota', N'US state or jurisdiction.', CAST(0 AS bit), 410),
			(N'RiskState', N'TN', N'Tennessee', N'US state or jurisdiction.', CAST(0 AS bit), 420),
			(N'RiskState', N'TX', N'Texas', N'US state or jurisdiction.', CAST(1 AS bit), 430),
			(N'RiskState', N'UT', N'Utah', N'US state or jurisdiction.', CAST(0 AS bit), 440),
			(N'RiskState', N'VT', N'Vermont', N'US state or jurisdiction.', CAST(0 AS bit), 450),
			(N'RiskState', N'VA', N'Virginia', N'US state or jurisdiction.', CAST(0 AS bit), 460),
			(N'RiskState', N'WA', N'Washington', N'US state or jurisdiction.', CAST(0 AS bit), 470),
			(N'RiskState', N'WV', N'West Virginia', N'US state or jurisdiction.', CAST(0 AS bit), 480),
			(N'RiskState', N'WI', N'Wisconsin', N'US state or jurisdiction.', CAST(0 AS bit), 490),
			(N'RiskState', N'WY', N'Wyoming', N'US state or jurisdiction.', CAST(0 AS bit), 500)
	) optionValue(OptionGroup, OptionCode, OptionName, Description, IsDefault, SortOrder)
	WHERE NOT EXISTS
	(
		SELECT 1
		FROM Submissions.SubmissionReferenceOption existing
		WHERE existing.TenantId = tenant.TenantId
		  AND existing.OptionGroup = optionValue.OptionGroup
		  AND existing.OptionCode = optionValue.OptionCode
		  AND existing.IsDeleted = 0
	);
END;
