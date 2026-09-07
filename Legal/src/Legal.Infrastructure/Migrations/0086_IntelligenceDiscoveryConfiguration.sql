SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Config TABLE(SettingKey NVARCHAR(200),Value NVARCHAR(2000),DataType NVARCHAR(50),Name NVARCHAR(200),Description NVARCHAR(1000));
	INSERT @Config VALUES
	(N'Intelligence.Similarity.ExactAttributeScore',N'0.90',N'Decimal',N'Exact attribute similarity score',N'Score assigned when both primary and secondary configured attributes match.'),
	(N'Intelligence.Similarity.PrimaryAttributeScore',N'0.80',N'Decimal',N'Primary attribute similarity score',N'Score assigned when the primary configured attribute matches.'),
	(N'Intelligence.Similarity.RelatedAttributeScore',N'0.70',N'Decimal',N'Related attribute similarity score',N'Score assigned when a related configured attribute matches.'),
	(N'Intelligence.Similarity.FixedAmountTolerance',N'25000',N'Decimal',N'Fixed amount similarity tolerance',N'Amount tolerance used when the source premium or incurred amount is zero.'),
	(N'Intelligence.Similarity.SubmissionAmountTolerancePercent',N'0.35',N'Decimal',N'Submission amount tolerance percent',N'Percentage variance allowed when comparing submission target premiums.'),
	(N'Intelligence.Similarity.ClaimAmountTolerancePercent',N'0.50',N'Decimal',N'Claim amount tolerance percent',N'Percentage variance allowed when comparing claim incurred amounts.'),
	(N'Intelligence.Similarity.ExpirationDays',N'7',N'Integer',N'Similarity projection expiration',N'Days before a synchronized similarity projection expires unless refreshed.');

	MERGE Core.ConfigurationSetting target USING @Config source
	ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey
	WHEN MATCHED THEN UPDATE SET ModuleCode=N'Intelligence',DefaultValue=source.Value,DataTypeCode=source.DataType,Description=source.Name+N'. '+source.Description,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,IsDeleted,CreatedDateUtc)
	VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.Value,source.Value,source.DataType,source.Name+N'. '+source.Description,0,0,0,SYSUTCDATETIME());
END;
