IF OBJECT_ID(N'Submissions.Proposal', N'U') IS NOT NULL
   AND NOT EXISTS
   (
	   SELECT 1
	   FROM sys.indexes
	   WHERE object_id = OBJECT_ID(N'Submissions.Proposal')
		 AND name = N'IX_Proposal_Tenant_Submission'
   )
BEGIN
	CREATE INDEX IX_Proposal_Tenant_Submission
		ON Submissions.Proposal(TenantId, SubmissionId, IsDeleted)
		INCLUDE (Title, Status);
END;

IF OBJECT_ID(N'Submissions.CarrierTransmission', N'U') IS NOT NULL
   AND NOT EXISTS
   (
	   SELECT 1
	   FROM sys.indexes
	   WHERE object_id = OBJECT_ID(N'Submissions.CarrierTransmission')
		 AND name = N'IX_CarrierTransmission_Tenant_Submission'
   )
BEGIN
	CREATE INDEX IX_CarrierTransmission_Tenant_Submission
		ON Submissions.CarrierTransmission(TenantId, SubmissionId, IsDeleted)
		INCLUDE (StatusCode, Recipient, Subject, ExternalReferenceNumber);
END;