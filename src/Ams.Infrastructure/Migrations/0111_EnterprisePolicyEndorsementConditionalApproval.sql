SET NOCOUNT ON;
SET XACT_ABORT ON;

IF OBJECT_ID(N'Policy.EndorsementTypeWorkflowRule',N'U') IS NULL OR OBJECT_ID(N'Policy.EndorsementTypeProfile',N'U') IS NULL
	THROW 52640,N'The enterprise endorsement workflow catalog must exist before conditional approval is configured.',1;

DECLARE @ApprovalRuleJson NVARCHAR(MAX)=N'{"conditionCode":"ApprovalRequired","actionLabel":"Submit for Approval","instruction":"Complete the review and route this endorsement to an authorized approver.","confirmationTitle":"Submit for Approval","confirmationMessage":"Confirm that the policy changes, effective date, supporting documents, and financial impact have been reviewed.","requiresNotes":false,"notesLabel":"Reviewer notes","notesPlaceholder":"Add an optional review summary for the approver."}';
DECLARE @CarrierBypassRuleJson NVARCHAR(MAX)=N'{"conditionCode":"ApprovalNotRequiredCarrier","actionLabel":"Submit to Carrier","instruction":"No internal approval is required. Continue to the configured carrier workflow.","confirmationTitle":"Submit to Carrier","confirmationMessage":"Confirm the endorsement is complete and ready for carrier processing.","requiresNotes":false,"notesLabel":"Processing notes","notesPlaceholder":"Add optional processing notes."}';
DECLARE @PolicyBypassRuleJson NVARCHAR(MAX)=N'{"conditionCode":"ApprovalNotRequiredPolicy","actionLabel":"Apply Policy Change","instruction":"No internal or carrier approval is required. Apply the configured policy update.","confirmationTitle":"Apply Policy Change","confirmationMessage":"Confirm the endorsement is complete and ready to update the policy.","requiresNotes":false,"notesLabel":"Processing notes","notesPlaceholder":"Add optional processing notes."}';

UPDATE workflowRule
SET RuleJson=@ApprovalRuleJson,RequiresApproval=0,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
FROM Policy.EndorsementTypeWorkflowRule workflowRule
JOIN Policy.EndorsementTypeProfile profile ON profile.TenantId=workflowRule.TenantId AND profile.EndorsementTypeId=workflowRule.EndorsementTypeId AND profile.IsActive=1 AND profile.IsDeleted=0
WHERE workflowRule.FromStatusCode=N'InReview' AND workflowRule.ToStatusCode=N'PendingApproval' AND workflowRule.IsDeleted=0;

INSERT Policy.EndorsementTypeWorkflowRule
(EndorsementTypeWorkflowRuleId,TenantId,EndorsementTypeId,FromStatusCode,ToStatusCode,RequiredPermissionCode,RequiresApproval,RequiresCarrierDispatch,RequiresAccountingWork,RequiresCommissionWork,RequiresDocumentWork,RequiresCertificateReview,RequiresPolicyVersion,RuleJson,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),profile.TenantId,profile.EndorsementTypeId,N'InReview',N'SubmittedToCarrier',N'ENDORSEMENT_MANAGE',0,1,0,0,0,0,0,@CarrierBypassRuleJson,1,61,SYSUTCDATETIME(),0
FROM Policy.EndorsementTypeProfile profile
WHERE profile.IsActive=1 AND profile.IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM Policy.EndorsementTypeWorkflowRule existing WHERE existing.TenantId=profile.TenantId AND existing.EndorsementTypeId=profile.EndorsementTypeId AND existing.FromStatusCode=N'InReview' AND existing.ToStatusCode=N'SubmittedToCarrier' AND existing.IsDeleted=0);

INSERT Policy.EndorsementTypeWorkflowRule
(EndorsementTypeWorkflowRuleId,TenantId,EndorsementTypeId,FromStatusCode,ToStatusCode,RequiredPermissionCode,RequiresApproval,RequiresCarrierDispatch,RequiresAccountingWork,RequiresCommissionWork,RequiresDocumentWork,RequiresCertificateReview,RequiresPolicyVersion,RuleJson,IsActive,SortOrder,CreatedDateUtc,IsDeleted)
SELECT NEWID(),profile.TenantId,profile.EndorsementTypeId,N'InReview',N'PolicyUpdated',N'ENDORSEMENT_MANAGE',0,0,profile.RequiresAccountingWork,profile.RequiresCommissionWork,0,0,profile.RequiresPolicyVersion,@PolicyBypassRuleJson,1,62,SYSUTCDATETIME(),0
FROM Policy.EndorsementTypeProfile profile
WHERE profile.IsActive=1 AND profile.IsDeleted=0
AND NOT EXISTS(SELECT 1 FROM Policy.EndorsementTypeWorkflowRule existing WHERE existing.TenantId=profile.TenantId AND existing.EndorsementTypeId=profile.EndorsementTypeId AND existing.FromStatusCode=N'InReview' AND existing.ToStatusCode=N'PolicyUpdated' AND existing.IsDeleted=0);

UPDATE workflowRule
SET RuleJson=CASE workflowRule.ToStatusCode WHEN N'SubmittedToCarrier' THEN @CarrierBypassRuleJson WHEN N'PolicyUpdated' THEN @PolicyBypassRuleJson END,
	RequiresCarrierDispatch=CASE WHEN workflowRule.ToStatusCode=N'SubmittedToCarrier' THEN 1 ELSE 0 END,
	RequiresAccountingWork=CASE WHEN workflowRule.ToStatusCode=N'PolicyUpdated' THEN profile.RequiresAccountingWork ELSE 0 END,
	RequiresCommissionWork=CASE WHEN workflowRule.ToStatusCode=N'PolicyUpdated' THEN profile.RequiresCommissionWork ELSE 0 END,
	RequiresPolicyVersion=CASE WHEN workflowRule.ToStatusCode=N'PolicyUpdated' THEN profile.RequiresPolicyVersion ELSE 0 END,
	IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
FROM Policy.EndorsementTypeWorkflowRule workflowRule
JOIN Policy.EndorsementTypeProfile profile ON profile.TenantId=workflowRule.TenantId AND profile.EndorsementTypeId=workflowRule.EndorsementTypeId AND profile.IsActive=1 AND profile.IsDeleted=0
WHERE workflowRule.FromStatusCode=N'InReview' AND workflowRule.ToStatusCode IN(N'SubmittedToCarrier',N'PolicyUpdated') AND workflowRule.IsDeleted=0;
