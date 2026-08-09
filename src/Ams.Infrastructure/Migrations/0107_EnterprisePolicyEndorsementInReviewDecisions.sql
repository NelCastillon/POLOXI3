SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Policy.EndorsementTypeWorkflowRule', N'U') IS NOT NULL
   AND OBJECT_ID(N'Policy.EndorsementTypeProfile', N'U') IS NOT NULL
BEGIN
	DECLARE @NeedMoreInfoRuleJson NVARCHAR(MAX) = N'{"actionLabel":"Request More Information","instruction":"Return the endorsement to the servicing user with actionable details about the missing information or documents.","confirmationTitle":"Request More Information","confirmationMessage":"Describe exactly what must be supplied before review can continue.","requiresNotes":true,"notesLabel":"Required information","notesPlaceholder":"List the missing information, documents, corrections, or decisions required."}';
	DECLARE @PendingApprovalRuleJson NVARCHAR(MAX) = N'{"actionLabel":"Submit for Approval","instruction":"Complete the review and route the endorsement to an authorized approver.","confirmationTitle":"Submit for Approval","confirmationMessage":"Confirm that the requested policy changes, effective date, supporting documents, and financial impact have been reviewed.","requiresNotes":false,"notesLabel":"Reviewer notes","notesPlaceholder":"Add an optional review summary for the approver."}';

	INSERT Policy.EndorsementTypeWorkflowRule
	(
		EndorsementTypeWorkflowRuleId,TenantId,EndorsementTypeId,FromStatusCode,ToStatusCode,
		RequiredPermissionCode,RequiresApproval,RequiresCarrierDispatch,RequiresAccountingWork,
		RequiresCommissionWork,RequiresDocumentWork,RequiresCertificateReview,RequiresPolicyVersion,
		RuleJson,IsActive,SortOrder,CreatedDateUtc,IsDeleted
	)
	SELECT NEWID(),profile.TenantId,profile.EndorsementTypeId,N'InReview',decision.ToStatusCode,
		   N'ENDORSEMENT_MANAGE',0,0,0,0,0,0,0,decision.RuleJson,1,decision.SortOrder,SYSUTCDATETIME(),0
	FROM Policy.EndorsementTypeProfile profile
	CROSS APPLY
	(
		VALUES
			(N'NeedMoreInfo',@NeedMoreInfoRuleJson,40),
			(N'PendingApproval',@PendingApprovalRuleJson,60)
	) decision(ToStatusCode,RuleJson,SortOrder)
	WHERE profile.IsActive=1
	  AND profile.IsDeleted=0
	  AND NOT EXISTS
	  (
		  SELECT 1
		  FROM Policy.EndorsementTypeWorkflowRule existing
		  WHERE existing.TenantId=profile.TenantId
			AND existing.EndorsementTypeId=profile.EndorsementTypeId
			AND existing.FromStatusCode=N'InReview'
			AND existing.ToStatusCode=decision.ToStatusCode
			AND existing.IsDeleted=0
	  );

	UPDATE workflowRule
	SET RuleJson=CASE workflowRule.ToStatusCode
			WHEN N'NeedMoreInfo' THEN @NeedMoreInfoRuleJson
			WHEN N'PendingApproval' THEN @PendingApprovalRuleJson
		END,
		RequiredPermissionCode=N'ENDORSEMENT_MANAGE',
		RequiresApproval=0,
		IsActive=1,
		ModifiedDateUtc=SYSUTCDATETIME()
	FROM Policy.EndorsementTypeWorkflowRule workflowRule
	JOIN Policy.EndorsementTypeProfile profile
	  ON profile.TenantId=workflowRule.TenantId
	 AND profile.EndorsementTypeId=workflowRule.EndorsementTypeId
	 AND profile.IsActive=1
	 AND profile.IsDeleted=0
	WHERE workflowRule.FromStatusCode=N'InReview'
	  AND workflowRule.ToStatusCode IN(N'NeedMoreInfo',N'PendingApproval')
	  AND workflowRule.IsDeleted=0;
END;
