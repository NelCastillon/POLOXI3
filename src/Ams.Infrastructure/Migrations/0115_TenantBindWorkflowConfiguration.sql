SET XACT_ABORT ON;
GO

CREATE OR ALTER PROCEDURE Submissions.EnsureTenantBindWorkflowConfiguration
	@TenantId UNIQUEIDENTIFIER
AS
BEGIN
	SET NOCOUNT ON;
	SET XACT_ABORT ON;

	IF NOT EXISTS (SELECT 1 FROM Core.Tenant WHERE TenantId = @TenantId AND IsDeleted = 0)
		THROW 52120, 'Tenant was not found for bind workflow configuration.', 1;

	DECLARE @StatusSeed TABLE
	(
		StatusCode NVARCHAR(50),
		StatusName NVARCHAR(100),
		Description NVARCHAR(500),
		IsTerminal BIT,
		CreatesPolicy BIT,
		IsDefault BIT,
		SortOrder INT
	);

	INSERT INTO @StatusSeed VALUES
	(N'Draft', N'Draft', N'Bind request is being prepared.', 0, 0, 1, 10),
	(N'Ready', N'Ready', N'All blocking readiness checks have passed.', 0, 0, 0, 20),
	(N'Submitted', N'Submitted', N'Bind request has been sent to the carrier.', 0, 0, 0, 30),
	(N'Received', N'Received', N'Carrier acknowledged receipt.', 0, 0, 0, 40),
	(N'UnderReview', N'Under Review', N'Carrier is reviewing the bind request.', 0, 0, 0, 50),
	(N'NeedInformation', N'Need Information', N'Carrier requires additional information.', 0, 0, 0, 60),
	(N'PendingPayment', N'Pending Payment', N'Required payment has not been verified.', 0, 0, 0, 70),
	(N'PendingApproval', N'Pending Approval', N'Agency approval is required.', 0, 0, 0, 80),
	(N'Approved', N'Approved', N'Carrier approved the request but coverage is not yet confirmed bound.', 0, 0, 0, 90),
	(N'Rejected', N'Rejected', N'Carrier rejected the bind request.', 1, 0, 0, 100),
	(N'Expired', N'Expired', N'Bind request expired.', 1, 0, 0, 110),
	(N'Withdrawn', N'Withdrawn', N'Agency withdrew the bind request.', 1, 0, 0, 120),
	(N'BinderReceived', N'Carrier Binder Received', N'Carrier binder was received and requires producer review.', 0, 0, 0, 130),
	(N'BinderAccepted', N'Binder Accepted', N'Producer accepted the verified binder.', 0, 0, 0, 140),
	(N'PolicyGenerationQueued', N'Policy Generation Queued', N'Policy generation is queued for background processing.', 0, 0, 0, 150),
	(N'PolicyCreated', N'Policy Created', N'AMS policy record was generated after producer binder acceptance.', 1, 0, 0, 160),
	(N'Bound', N'Bound', N'Legacy carrier-confirmed status retained without automatic policy creation.', 0, 0, 0, 170),
	(N'Cancelled', N'Cancelled', N'Bind request was cancelled.', 1, 0, 0, 180);

	MERGE Submissions.PolicyBindStatus AS target
	USING (SELECT @TenantId AS TenantId, s.* FROM @StatusSeed s) AS source
	ON target.TenantId = source.TenantId AND target.StatusCode = source.StatusCode
	WHEN MATCHED THEN UPDATE SET
		StatusName = source.StatusName,
		Description = source.Description,
		IsTerminal = source.IsTerminal,
		CreatesPolicy = source.CreatesPolicy,
		IsDefault = source.IsDefault,
		IsActive = 1,
		SortOrder = source.SortOrder,
		IsDeleted = 0,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT
		(TenantId, StatusCode, StatusName, Description, IsTerminal, CreatesPolicy, IsDefault, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
		VALUES
		(source.TenantId, source.StatusCode, source.StatusName, source.Description, source.IsTerminal, source.CreatesPolicy, source.IsDefault, 1, source.SortOrder, SYSUTCDATETIME(), 0);

	DECLARE @TransitionSeed TABLE
	(
		FromStatusCode NVARCHAR(50),
		ToStatusCode NVARCHAR(50),
		RequiresValidation BIT,
		RequiresApproval BIT,
		RequiresCarrierResponse BIT
	);

	INSERT INTO @TransitionSeed VALUES
	(N'Draft', N'Ready', 1, 1, 0),
	(N'Draft', N'PendingApproval', 0, 0, 0),
	(N'Draft', N'Cancelled', 0, 0, 0),
	(N'PendingApproval', N'Ready', 1, 1, 0),
	(N'PendingApproval', N'Draft', 0, 0, 0),
	(N'Ready', N'Submitted', 1, 1, 0),
	(N'Ready', N'Cancelled', 0, 0, 0),
	(N'Submitted', N'Received', 0, 0, 1),
	(N'Submitted', N'UnderReview', 0, 0, 1),
	(N'Submitted', N'NeedInformation', 0, 0, 1),
	(N'Submitted', N'Rejected', 0, 0, 1),
	(N'Submitted', N'Withdrawn', 0, 0, 0),
	(N'Received', N'UnderReview', 0, 0, 1),
	(N'Received', N'NeedInformation', 0, 0, 1),
	(N'Received', N'Approved', 0, 0, 1),
	(N'Received', N'Bound', 0, 0, 1),
	(N'UnderReview', N'NeedInformation', 0, 0, 1),
	(N'UnderReview', N'PendingPayment', 0, 0, 1),
	(N'UnderReview', N'Approved', 0, 0, 1),
	(N'UnderReview', N'Rejected', 0, 0, 1),
	(N'UnderReview', N'Bound', 0, 0, 1),
	(N'NeedInformation', N'Ready', 1, 1, 0),
	(N'NeedInformation', N'Submitted', 1, 1, 0),
	(N'PendingPayment', N'Approved', 0, 0, 1),
	(N'PendingPayment', N'BinderReceived', 0, 0, 1),
	(N'Approved', N'BinderReceived', 0, 0, 1),
	(N'Approved', N'Rejected', 0, 0, 1),
	(N'Received', N'BinderReceived', 0, 0, 1),
	(N'UnderReview', N'BinderReceived', 0, 0, 1),
	(N'BinderReceived', N'BinderAccepted', 0, 0, 0),
	(N'BinderReceived', N'NeedInformation', 0, 0, 0),
	(N'BinderReceived', N'Rejected', 0, 0, 0),
	(N'BinderAccepted', N'PolicyGenerationQueued', 0, 0, 0),
	(N'PolicyGenerationQueued', N'PolicyCreated', 0, 0, 0);

	MERGE Submissions.BindStatusTransition AS target
	USING (SELECT @TenantId AS TenantId, s.* FROM @TransitionSeed s) AS source
	ON target.TenantId = source.TenantId
	   AND target.FromStatusCode = source.FromStatusCode
	   AND target.ToStatusCode = source.ToStatusCode
	WHEN MATCHED THEN UPDATE SET
		RequiresValidation = source.RequiresValidation,
		RequiresApproval = source.RequiresApproval,
		RequiresCarrierResponse = source.RequiresCarrierResponse,
		IsActive = 1,
		IsDeleted = 0,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT
		(TenantId, FromStatusCode, ToStatusCode, RequiresValidation, RequiresApproval, RequiresCarrierResponse, IsActive, CreatedDateUtc, IsDeleted)
		VALUES
		(source.TenantId, source.FromStatusCode, source.ToStatusCode, source.RequiresValidation, source.RequiresApproval, source.RequiresCarrierResponse, 1, SYSUTCDATETIME(), 0);

	UPDATE Submissions.BindStatusTransition
	SET IsActive = 0,
		IsDeleted = 1,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHERE TenantId = @TenantId
	  AND ToStatusCode = N'Bound'
	  AND IsDeleted = 0;

	DECLARE @OptionSeed TABLE
	(
		OptionGroup NVARCHAR(100),
		OptionCode NVARCHAR(100),
		OptionName NVARCHAR(200),
		Description NVARCHAR(500),
		SortOrder INT
	);

	INSERT INTO @OptionSeed VALUES
	(N'BindMethod', N'CarrierApi', N'Carrier API', N'Submit through a configured carrier integration.', 10),
	(N'BindMethod', N'Email', N'Email', N'Send the binder package by email.', 20),
	(N'BindMethod', N'CarrierPortal', N'Carrier Portal', N'Submit through the carrier portal.', 30),
	(N'BindMethod', N'Phone', N'Phone / Verbal', N'Request binding by phone with required written follow-up.', 40),
	(N'BindMethod', N'Manual', N'Manual', N'Externally coordinated manual bind request.', 50),
	(N'BindingAuthority', N'Carrier', N'Carrier Authority', N'Carrier or authorized underwriter must confirm binding.', 10),
	(N'BindingAuthority', N'Mga', N'MGA Authority', N'Authorized managing general agent confirms binding.', 20),
	(N'BindingAuthority', N'Wholesaler', N'Wholesaler Authority', N'Authorized wholesaler confirms binding.', 30),
	(N'BindingAuthority', N'Agency', N'Agency Binding Authority', N'Agency authority applies subject to configured limits.', 40),
	(N'BindApprovalReason', N'PremiumThreshold', N'Premium Threshold', N'Premium exceeds the agency approval threshold.', 10),
	(N'BindApprovalReason', N'CommissionOverride', N'Commission Override', N'Commission terms require approval.', 20),
	(N'BindApprovalReason', N'AuthorityException', N'Authority Exception', N'Binding authority exception requires approval.', 30),
	(N'BindCarrierMessageType', N'CarrierResponse', N'Carrier Response', N'General response from the carrier or authorized market.', 10),
	(N'BindCarrierMessageType', N'NeedInformation', N'Need Information', N'Carrier requested additional underwriting information.', 20),
	(N'BindCarrierMessageType', N'BindingDecision', N'Binding Decision', N'Carrier communicated an approval, rejection, or bind confirmation.', 30),
	(N'BinderReviewStatus', N'PendingReview', N'Pending Review', N'Carrier binder awaits producer verification.', 10),
	(N'BinderReviewStatus', N'Accepted', N'Accepted', N'Producer accepted the verified binder.', 20),
	(N'BinderReviewStatus', N'Rejected', N'Rejected', N'Producer rejected the carrier binder.', 30),
	(N'BinderReviewStatus', N'CorrectionRequested', N'Correction Requested', N'Producer requested corrected carrier terms.', 40),
	(N'PolicyGenerationStatus', N'Queued', N'Queued', N'Policy generation is waiting for the worker.', 10),
	(N'PolicyGenerationStatus', N'Processing', N'Processing', N'Policy generation is in progress.', 20),
	(N'PolicyGenerationStatus', N'Completed', N'Completed', N'Policy generation completed successfully.', 30),
	(N'PolicyGenerationStatus', N'Failed', N'Failed', N'Policy generation requires retry or intervention.', 40);

	MERGE Submissions.SubmissionReferenceOption AS target
	USING (SELECT @TenantId AS TenantId, s.* FROM @OptionSeed s) AS source
	ON target.TenantId = source.TenantId
	   AND target.OptionGroup = source.OptionGroup
	   AND target.OptionCode = source.OptionCode
	WHEN MATCHED THEN UPDATE SET
		OptionName = source.OptionName,
		Description = source.Description,
		SortOrder = source.SortOrder,
		IsActive = 1,
		IsDeleted = 0,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT
		(TenantId, OptionGroup, OptionCode, OptionName, Description, SortOrder, IsActive, CreatedDateUtc, IsDeleted)
		VALUES
		(source.TenantId, source.OptionGroup, source.OptionCode, source.OptionName, source.Description, source.SortOrder, 1, SYSUTCDATETIME(), 0);

	DECLARE @TaskSeed TABLE
	(
		TaskCode NVARCHAR(100),
		Title NVARCHAR(200),
		Description NVARCHAR(1000),
		TaskTypeCode NVARCHAR(80),
		PriorityCode NVARCHAR(50),
		DueDays INT,
		SortOrder INT
	);

	INSERT INTO @TaskSeed VALUES
	(N'DeliverPolicy', N'Deliver Policy', N'Deliver generated policy documents to the insured.', N'PolicyDelivery', N'High', 2, 10),
	(N'CollectPayment', N'Collect Payment', N'Collect any outstanding policy payment.', N'PaymentCollection', N'High', 3, 20),
	(N'IssueCertificate', N'Issue Certificate', N'Issue required evidence of insurance or certificates.', N'CertificateIssuance', N'Normal', 3, 30),
	(N'PolicyFollowUp', N'Policy Follow Up', N'Confirm policy delivery and outstanding requirements.', N'PolicyFollowUp', N'Normal', 7, 40),
	(N'RenewalReminder', N'Renewal Reminder', N'Review the upcoming policy renewal.', N'Renewal', N'Normal', 300, 50);

	MERGE Submissions.PolicyGenerationTaskTemplate AS target
	USING (SELECT @TenantId AS TenantId, s.* FROM @TaskSeed s) AS source
	ON target.TenantId = source.TenantId AND target.TaskCode = source.TaskCode
	WHEN MATCHED THEN UPDATE SET
		Title = source.Title,
		Description = source.Description,
		TaskTypeCode = source.TaskTypeCode,
		PriorityCode = source.PriorityCode,
		DueDays = source.DueDays,
		SortOrder = source.SortOrder,
		IsActive = 1,
		IsDeleted = 0,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT
		(TenantId, TaskCode, Title, Description, TaskTypeCode, PriorityCode, DueDays, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
		VALUES
		(source.TenantId, source.TaskCode, source.Title, source.Description, source.TaskTypeCode, source.PriorityCode, source.DueDays, 1, source.SortOrder, SYSUTCDATETIME(), 0);

	DECLARE @RequirementSeed TABLE
	(
		RequirementCode NVARCHAR(100),
		RequirementName NVARCHAR(200),
		RequirementTypeCode NVARCHAR(50),
		DocumentCategoryCode NVARCHAR(100),
		SortOrder INT
	);

	INSERT INTO @RequirementSeed VALUES
	(N'SignedApplication', N'Signed Application', N'Document', N'SignedApplication', 10),
	(N'SignedProposal', N'Signed Proposal', N'Document', N'SignedProposal', 20),
	(N'AcordForms', N'ACORD Forms', N'Document', N'ACORD', 30),
	(N'SupplementalForms', N'Supplemental Forms', N'Document', N'Supplemental', 40),
	(N'LossRuns', N'Loss Runs', N'Document', N'LossRuns', 50),
	(N'Photos', N'Photos', N'Document', N'Photos', 60),
	(N'Inspection', N'Inspection', N'Underwriting', N'Inspection', 70),
	(N'PaymentAuthorization', N'Payment Authorization', N'Payment', N'PaymentAuthorization', 80),
	(N'FinancingAgreement', N'Financing Agreement', N'Payment', N'FinancingAgreement', 90),
	(N'CarrierQuestionnaire', N'Carrier Questionnaire', N'Underwriting', N'CarrierQuestionnaire', 100),
	(N'ProposalAccepted', N'Proposal Accepted', N'Compliance', NULL, 110),
	(N'QuoteBindable', N'Quote Is Bindable', N'Underwriting', NULL, 120),
	(N'EffectiveDateValid', N'Effective Date Valid', N'Compliance', NULL, 130),
	(N'UnderwritingComplete', N'Underwriting Questions Complete', N'Underwriting', NULL, 140),
	(N'ProducerLicensed', N'Producer Licensed', N'Compliance', NULL, 150),
	(N'CarrierAppointment', N'Carrier Appointment Active', N'Compliance', NULL, 160),
	(N'AgencyAppointment', N'Agency Appointment Active', N'Compliance', NULL, 170),
	(N'PremiumCalculated', N'Premium Calculated', N'Financial', NULL, 180),
	(N'TaxesCalculated', N'Taxes And Fees Calculated', N'Financial', NULL, 190),
	(N'RequiredNotes', N'Required Notes Entered', N'Compliance', NULL, 200),
	(N'RequiredActivities', N'Required Activities Complete', N'Activity', NULL, 210),
	(N'ComplianceClear', N'Compliance And DNC Clear', N'Compliance', NULL, 220),
	(N'OutstandingInspection', N'No Pending Inspection', N'Underwriting', NULL, 230),
	(N'OutstandingSurvey', N'No Pending Survey', N'Underwriting', NULL, 240),
	(N'OutstandingMvr', N'No Pending MVR', N'Underwriting', NULL, 250),
	(N'OutstandingClaimsReview', N'No Pending Claims Review', N'Underwriting', NULL, 260),
	(N'DownPaymentVerified', N'Down Payment Verified', N'Payment', NULL, 270);

	MERGE Submissions.BindRequirement AS target
	USING (SELECT @TenantId AS TenantId, s.* FROM @RequirementSeed s) AS source
	ON target.TenantId = source.TenantId
	   AND target.CarrierId IS NULL
	   AND target.LineOfBusiness IS NULL
	   AND target.RequirementCode = source.RequirementCode
	WHEN MATCHED THEN UPDATE SET
		RequirementName = source.RequirementName,
		RequirementTypeCode = source.RequirementTypeCode,
		DocumentCategoryCode = source.DocumentCategoryCode,
		IsRequired = 1,
		BlocksSubmission = 1,
		IsActive = 1,
		SortOrder = source.SortOrder,
		IsDeleted = 0,
		ModifiedDateUtc = SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT
		(TenantId, RequirementCode, RequirementName, RequirementTypeCode, DocumentCategoryCode, IsRequired, BlocksSubmission, IsActive, SortOrder, CreatedDateUtc, IsDeleted)
		VALUES
		(source.TenantId, source.RequirementCode, source.RequirementName, source.RequirementTypeCode, source.DocumentCategoryCode, 1, 1, 1, source.SortOrder, SYSUTCDATETIME(), 0);
END;
GO

DECLARE @TenantId UNIQUEIDENTIFIER;
DECLARE TenantCursor CURSOR LOCAL FAST_FORWARD FOR
	SELECT TenantId FROM Core.Tenant WHERE IsDeleted = 0;

OPEN TenantCursor;
FETCH NEXT FROM TenantCursor INTO @TenantId;
WHILE @@FETCH_STATUS = 0
BEGIN
	EXEC Submissions.EnsureTenantBindWorkflowConfiguration @TenantId = @TenantId;
	FETCH NEXT FROM TenantCursor INTO @TenantId;
END;
CLOSE TenantCursor;
DEALLOCATE TenantCursor;
GO
