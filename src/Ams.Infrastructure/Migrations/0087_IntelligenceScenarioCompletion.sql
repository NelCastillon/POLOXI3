SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Core.ConfigurationSetting',N'U') IS NOT NULL
BEGIN
	DECLARE @Config TABLE(SettingKey NVARCHAR(200),Value NVARCHAR(2000),DataType NVARCHAR(50),Name NVARCHAR(200),Description NVARCHAR(1000));
	INSERT @Config VALUES
	(N'Intelligence.Recommendation.AllowedConditionOperators',N'["EQUALS","NOT_EQUALS","GREATER_THAN","GREATER_THAN_OR_EQUAL","LESS_THAN","LESS_THAN_OR_EQUAL","IS_EMPTY","IS_NOT_EMPTY","CONTAINS","IN"]',N'JSON',N'Recommendation condition operators',N'Allowlisted operators accepted by the constrained recommendation condition evaluator.'),
	(N'Intelligence.Compliance.AllowedEvaluationOperators',N'["EQUALS","NOT_EQUALS","GREATER_THAN","GREATER_THAN_OR_EQUAL","LESS_THAN","LESS_THAN_OR_EQUAL","IS_EMPTY","IS_NOT_EMPTY","CONTAINS","IN"]',N'JSON',N'Compliance evaluation operators',N'Allowlisted operators accepted by the constrained compliance evaluator.'),
	(N'Intelligence.Workflow.ImprovementMinimumOccurrences',N'3',N'Integer',N'Workflow improvement occurrence threshold',N'Minimum repeated authoritative approval or delay occurrences before an advisory workflow improvement signal is created.'),
	(N'Intelligence.Renewal.HighRiskRetentionProbability',N'50',N'Decimal',N'High-risk renewal probability',N'Retention probability below which renewal intelligence assigns high priority.'),
	(N'Intelligence.Renewal.MediumRiskRetentionProbability',N'70',N'Decimal',N'Medium-risk renewal probability',N'Retention probability below which renewal intelligence assigns medium priority.'),
	(N'Intelligence.Producer.ProductivityWindowDays',N'30',N'Integer',N'Producer productivity window',N'Rolling period used for database-backed producer pipeline and activity metrics.'),
	(N'Intelligence.Producer.CrossSellMinimumActivePolicies',N'1',N'Integer',N'Producer cross-sell policy threshold',N'Maximum active policy count used to identify accounts that may warrant an advisory cross-sell review.'),
	(N'Intelligence.Customer.LifetimeValuePolicyYears',N'3',N'Integer',N'Customer lifetime-value policy window',N'Policy-history window used to summarize customer tenure and premium trends without predicting regulated outcomes.'),
	(N'Intelligence.Safety.MaximumInputCharacters',N'20000',N'Integer',N'Maximum AI input characters',N'Maximum input length enforced before governed AI reasoning execution.'),
	(N'Intelligence.Safety.MaximumOutputCharacters',N'20000',N'Integer',N'Maximum AI output characters',N'Maximum output length enforced during governed AI output validation.'),
	(N'Intelligence.PromptRegistry.ExecutableSource',N'DMS.AiPromptDefinition',N'String',N'Executable prompt source',N'Authoritative executable prompt source while AI.PromptDefinition provides the cross-capability registry and governance view.');

	MERGE Core.ConfigurationSetting target USING @Config source
	ON target.TenantId IS NULL AND target.ScopeCode=N'Platform' AND target.SettingKey=source.SettingKey
	WHEN MATCHED THEN UPDATE SET ModuleCode=N'Intelligence',DefaultValue=source.Value,DataTypeCode=source.DataType,Description=source.Name+N'. '+source.Description,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(SettingId,TenantId,ScopeCode,ModuleCode,SettingKey,SettingValue,DefaultValue,DataTypeCode,Description,IsEncrypted,IsReadOnly,IsDeleted,CreatedDateUtc)
	VALUES(NEWID(),NULL,N'Platform',N'Intelligence',source.SettingKey,source.Value,source.Value,source.DataType,source.Name+N'. '+source.Description,0,0,0,SYSUTCDATETIME());
END;

IF OBJECT_ID(N'AI.PromptDefinition',N'U') IS NOT NULL AND OBJECT_ID(N'DMS.AiPromptDefinition',N'U') IS NOT NULL
BEGIN
	DECLARE @DocumentCapabilityId UNIQUEIDENTIFIER=(SELECT TOP(1) IntelligenceCapabilityId FROM AI.IntelligenceCapability WHERE CapabilityCode=N'DOCUMENT_INTELLIGENCE' AND TenantId IS NULL AND IsDeleted=0);
	IF @DocumentCapabilityId IS NOT NULL
	BEGIN
		MERGE AI.PromptDefinition target USING
		(
			SELECT prompt.TenantId,@DocumentCapabilityId IntelligenceCapabilityId,prompt.PromptCode,prompt.VersionLabel,CONVERT(nvarchar(200),prompt.PromptCode) DisplayName,prompt.SystemPrompt SystemInstructions,N'{}' InputSchemaJson,prompt.OutputSchemaJson,CASE WHEN prompt.StatusCode=N'APPROVED' AND (prompt.ApprovedByUserId IS NULL OR prompt.ApprovedDateUtc IS NULL) THEN N'DRAFT' ELSE prompt.StatusCode END StatusCode,CASE WHEN prompt.StatusCode=N'APPROVED' AND prompt.ApprovedByUserId IS NOT NULL AND prompt.ApprovedDateUtc IS NOT NULL THEN prompt.ApprovedByUserId END ApprovedByUserId,CASE WHEN prompt.StatusCode=N'APPROVED' AND prompt.ApprovedByUserId IS NOT NULL AND prompt.ApprovedDateUtc IS NOT NULL THEN prompt.ApprovedDateUtc END ApprovedDateUtc,prompt.EffectiveFromUtc,prompt.EffectiveToUtc,prompt.CreatedDateUtc,CAST(NULL AS uniqueidentifier) CreatedByUserId
			FROM DMS.AiPromptDefinition prompt
		) source
		ON ((target.TenantId=source.TenantId) OR (target.TenantId IS NULL AND source.TenantId IS NULL)) AND target.PromptCode=source.PromptCode AND target.VersionLabel=source.VersionLabel AND target.IsDeleted=0
		WHEN MATCHED AND target.StatusCode<>N'APPROVED' THEN UPDATE SET IntelligenceCapabilityId=source.IntelligenceCapabilityId,DisplayName=source.DisplayName,SystemInstructions=source.SystemInstructions,InputSchemaJson=source.InputSchemaJson,OutputSchemaJson=source.OutputSchemaJson,StatusCode=source.StatusCode,ApprovedByUserId=source.ApprovedByUserId,ApprovedDateUtc=source.ApprovedDateUtc,EffectiveFromUtc=source.EffectiveFromUtc,EffectiveToUtc=source.EffectiveToUtc,ModifiedDateUtc=SYSUTCDATETIME()
		WHEN NOT MATCHED THEN INSERT(PromptDefinitionId,TenantId,IntelligenceCapabilityId,PromptCode,VersionLabel,DisplayName,SystemInstructions,InputSchemaJson,OutputSchemaJson,StatusCode,ApprovedByUserId,ApprovedDateUtc,EffectiveFromUtc,EffectiveToUtc,CreatedDateUtc,CreatedByUserId,IsDeleted)
		VALUES(NEWID(),source.TenantId,source.IntelligenceCapabilityId,source.PromptCode,source.VersionLabel,source.DisplayName,source.SystemInstructions,source.InputSchemaJson,source.OutputSchemaJson,source.StatusCode,source.ApprovedByUserId,source.ApprovedDateUtc,source.EffectiveFromUtc,source.EffectiveToUtc,source.CreatedDateUtc,source.CreatedByUserId,0);
	END;
END;
