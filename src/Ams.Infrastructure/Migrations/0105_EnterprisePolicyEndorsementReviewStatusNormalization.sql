SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Policy.PolicyEndorsement', N'U') IS NOT NULL
BEGIN
	UPDATE Policy.PolicyEndorsement
	SET Status = N'InReview',
		WorkflowStage = CASE
			WHEN WorkflowStage IN (N'PendingReview', N'Pending Review', N'In Review') THEN N'InReview'
			ELSE WorkflowStage
		END,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE IsDeleted = 0
	  AND Status IN (N'PendingReview', N'Pending Review', N'In Review');
END;
