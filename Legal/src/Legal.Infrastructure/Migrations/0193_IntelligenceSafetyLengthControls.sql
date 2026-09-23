-- 0193_IntelligenceSafetyLengthControls
-- Seeds the platform-level MAXIMUM_INPUT_LENGTH and MAXIMUM_OUTPUT_LENGTH safety controls.
-- These controls are referenced by AiProviderRouter.RecordViolationAsync when an AI request or
-- response exceeds the configured length. Without the seed row, recording a length violation
-- throws "Required safety control MAXIMUM_INPUT_LENGTH is not active." instead of the intended
-- AiSafetyViolationException about the exceeded length. Keeps the database as the source of truth
-- for AI safety controls.

IF OBJECT_ID(N'AI.Legal_SafetyControl', N'U') IS NOT NULL
BEGIN
	DECLARE @LengthSafety TABLE(ControlCode NVARCHAR(120),DisplayName NVARCHAR(200),Description NVARCHAR(2000),ControlTypeCode NVARCHAR(50),StageCode NVARCHAR(50),ActionCode NVARCHAR(50),RequiresReview BIT,SortOrder INT);
	INSERT @LengthSafety VALUES
	(N'MAXIMUM_INPUT_LENGTH',N'Maximum input length',N'Reject AI requests whose combined system and user prompt exceeds the configured maximum input character length.',N'INPUT_VALIDATION',N'PRE_EXECUTION',N'BLOCK',0,9),
	(N'MAXIMUM_OUTPUT_LENGTH',N'Maximum output length',N'Reject AI responses whose content exceeds the configured maximum output character length.',N'OUTPUT_VALIDATION',N'POST_EXECUTION',N'REJECT',0,10);
	MERGE AI.Legal_SafetyControl AS target USING @LengthSafety AS source ON target.TenantId IS NULL AND target.ControlCode=source.ControlCode
	WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,Description=source.Description,ControlTypeCode=source.ControlTypeCode,EnforcementStageCode=source.StageCode,ViolationActionCode=source.ActionCode,RequiresHumanReview=source.RequiresReview,SortOrder=source.SortOrder,IsActive=1,IsDeleted=0,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(TenantId,ControlCode,DisplayName,Description,ControlTypeCode,EnforcementStageCode,ConfigurationJson,ViolationActionCode,RequiresHumanReview,SortOrder,IsActive,CreatedDateUtc,IsDeleted) VALUES(NULL,source.ControlCode,source.DisplayName,source.Description,source.ControlTypeCode,source.StageCode,N'{"enabled":true}',source.ActionCode,source.RequiresReview,source.SortOrder,1,SYSUTCDATETIME(),0);
END;
