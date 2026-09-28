SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- ────────────────────────────────────────────────────────────────────────────────────────────────
-- POLOXI Legal — Document Intelligence seed corpus for the Harper v. Voss Grocery matter.
--
-- Populates the canonical document-intelligence graph (documents → versions → passages →
-- evidence items → fact propositions → proposition support edges) with a small but complete,
-- source-traceable dataset so the Document Intelligence workspace tab renders real DB-backed
-- content instead of an empty state. All names/records are fictional test data.
--
-- Idempotent: guarded by deterministic ids and NOT EXISTS checks. Runs only when the target matter
-- already exists (seeded in 0318).
-- ────────────────────────────────────────────────────────────────────────────────────────────────

DECLARE @Tenant UNIQUEIDENTIFIER = N'DA988708-62B2-4C14-956E-0BAA43C9E902';
DECLARE @User   UNIQUEIDENTIFIER = N'00000000-0000-0000-0000-000000000001';
DECLARE @Now    DATETIME2        = SYSUTCDATETIME();
DECLARE @PIM    UNIQUEIDENTIFIER = N'A3000002-0000-0000-0000-000000000001';

-- Only seed when the Harper v. Voss matter exists on this tenant.
IF OBJECT_ID(N'POLOXI.Legal_MatterDocument', N'U') IS NOT NULL
   AND EXISTS (SELECT 1 FROM POLOXI.Legal_DecisionMatter WHERE DecisionMatterId=@PIM AND TenantId=@Tenant AND IsDeleted=0)
BEGIN
	-- Deterministic document ids.
	DECLARE @DocReport  UNIQUEIDENTIFIER = N'A3000042-0001-0000-0000-000000000001';
	DECLARE @DocMedical UNIQUEIDENTIFIER = N'A3000042-0002-0000-0000-000000000001';
	DECLARE @DocPolicy  UNIQUEIDENTIFIER = N'A3000042-0003-0000-0000-000000000001';

	DECLARE @VerReport  UNIQUEIDENTIFIER = N'A3000042-0001-0000-0000-000000000101';
	DECLARE @VerMedical UNIQUEIDENTIFIER = N'A3000042-0002-0000-0000-000000000101';
	DECLARE @VerPolicy  UNIQUEIDENTIFIER = N'A3000042-0003-0000-0000-000000000101';

	-- Passage ids.
	DECLARE @PasReport1  UNIQUEIDENTIFIER = N'A3000042-0001-0000-0000-000000000201';
	DECLARE @PasReport2  UNIQUEIDENTIFIER = N'A3000042-0001-0000-0000-000000000202';
	DECLARE @PasMedical1 UNIQUEIDENTIFIER = N'A3000042-0002-0000-0000-000000000201';
	DECLARE @PasMedical2 UNIQUEIDENTIFIER = N'A3000042-0002-0000-0000-000000000202';
	DECLARE @PasPolicy1  UNIQUEIDENTIFIER = N'A3000042-0003-0000-0000-000000000201';

	-- Evidence item ids.
	DECLARE @EviSpillDuration UNIQUEIDENTIFIER = N'A3000042-0001-0000-0000-000000000301';
	DECLARE @EviEmployeePass  UNIQUEIDENTIFIER = N'A3000042-0001-0000-0000-000000000302';
	DECLARE @EviFracture      UNIQUEIDENTIFIER = N'A3000042-0002-0000-0000-000000000301';
	DECLARE @EviTreatment     UNIQUEIDENTIFIER = N'A3000042-0002-0000-0000-000000000302';
	DECLARE @EviPolicyLimit   UNIQUEIDENTIFIER = N'A3000042-0003-0000-0000-000000000301';

	-- Fact proposition ids.
	DECLARE @PropNotice    UNIQUEIDENTIFIER = N'A3000042-0000-0001-0000-000000000401';
	DECLARE @PropCausation UNIQUEIDENTIFIER = N'A3000042-0000-0002-0000-000000000402';
	DECLARE @PropCoverage  UNIQUEIDENTIFIER = N'A3000042-0000-0003-0000-000000000403';

	-- 1) Documents.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterDocument WHERE LegalDocumentId=@DocReport)
		INSERT POLOXI.Legal_MatterDocument (LegalDocumentId, DecisionMatterId, DocumentControlNumber, FileName, ContentType, DocumentTypeCode, StatusCode, CurrentVersionNumber, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@DocReport, @PIM, N'HV-DOC-0001', N'Voss-Store-Incident-Report.pdf', N'application/pdf', N'POLICE_COLLISION_REPORT', N'ENRICHED', 1, @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterDocument WHERE LegalDocumentId=@DocMedical)
		INSERT POLOXI.Legal_MatterDocument (LegalDocumentId, DecisionMatterId, DocumentControlNumber, FileName, ContentType, DocumentTypeCode, StatusCode, CurrentVersionNumber, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@DocMedical, @PIM, N'HV-DOC-0002', N'Harper-Orthopedic-Records.pdf', N'application/pdf', N'MEDICAL_RECORD', N'ENRICHED', 1, @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterDocument WHERE LegalDocumentId=@DocPolicy)
		INSERT POLOXI.Legal_MatterDocument (LegalDocumentId, DecisionMatterId, DocumentControlNumber, FileName, ContentType, DocumentTypeCode, StatusCode, CurrentVersionNumber, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@DocPolicy, @PIM, N'HV-DOC-0003', N'Voss-Grocery-CGL-Policy.pdf', N'application/pdf', N'INSURANCE_POLICY', N'ENRICHED', 1, @Tenant, DATEADD(DAY,-2,@Now), @User);

	-- 2) Versions.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterDocumentVersion WHERE LegalDocumentVersionId=@VerReport)
		INSERT POLOXI.Legal_MatterDocumentVersion (LegalDocumentVersionId, LegalDocumentId, VersionNumber, Sha256Hash, StorageReference, FileSizeBytes, MalwareStatusCode, ProcessingStatusCode, NativeTextAvailable, ExtractionProviderCode, ExtractionModelCode, ExtractionModelVersion, ProcessedDateUtc, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@VerReport, @DocReport, 1, N'11111111111111111111111111111111111111111111111111111111aaaa0001', N'seed://harper-voss/incident-report/v1', 248311, N'CLEAN', N'PROCESSED', 1, N'AZURE_DOCUMENT_INTELLIGENCE', N'prebuilt-document', N'2024-07-31', DATEADD(DAY,-2,@Now), @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterDocumentVersion WHERE LegalDocumentVersionId=@VerMedical)
		INSERT POLOXI.Legal_MatterDocumentVersion (LegalDocumentVersionId, LegalDocumentId, VersionNumber, Sha256Hash, StorageReference, FileSizeBytes, MalwareStatusCode, ProcessingStatusCode, NativeTextAvailable, ExtractionProviderCode, ExtractionModelCode, ExtractionModelVersion, ProcessedDateUtc, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@VerMedical, @DocMedical, 1, N'22222222222222222222222222222222222222222222222222222222aaaa0002', N'seed://harper-voss/medical-records/v1', 512884, N'CLEAN', N'PROCESSED', 1, N'AZURE_DOCUMENT_INTELLIGENCE', N'prebuilt-document', N'2024-07-31', DATEADD(DAY,-2,@Now), @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterDocumentVersion WHERE LegalDocumentVersionId=@VerPolicy)
		INSERT POLOXI.Legal_MatterDocumentVersion (LegalDocumentVersionId, LegalDocumentId, VersionNumber, Sha256Hash, StorageReference, FileSizeBytes, MalwareStatusCode, ProcessingStatusCode, NativeTextAvailable, ExtractionProviderCode, ExtractionModelCode, ExtractionModelVersion, ProcessedDateUtc, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@VerPolicy, @DocPolicy, 1, N'33333333333333333333333333333333333333333333333333333333aaaa0003', N'seed://harper-voss/cgl-policy/v1', 883120, N'CLEAN', N'PROCESSED', 1, N'AZURE_DOCUMENT_INTELLIGENCE', N'prebuilt-document', N'2024-07-31', DATEADD(DAY,-2,@Now), @Tenant, DATEADD(DAY,-2,@Now), @User);

	-- 3) Passages.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DocumentPassage WHERE LegalDocumentPassageId=@PasReport1)
		INSERT POLOXI.Legal_DocumentPassage (LegalDocumentPassageId, LegalDocumentVersionId, PageNumber, SectionPath, SequenceNumber, PassageText, ExtractionMethodCode, ExtractionConfidence, EpistemicStateCode, ContentHash, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@PasReport1, @VerReport, 1, N'Incident Narrative', 1, N'Store surveillance footage shows the leaked refrigerant water was present on the dairy-aisle floor for approximately 38 minutes before the customer fall at 14:12.', N'AZURE_DOCUMENT_INTELLIGENCE', 0.9600, N'VERIFIED', N'p11111111111111111111111111111111111111111111111111111111000001', @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DocumentPassage WHERE LegalDocumentPassageId=@PasReport2)
		INSERT POLOXI.Legal_DocumentPassage (LegalDocumentPassageId, LegalDocumentVersionId, PageNumber, SectionPath, SequenceNumber, PassageText, ExtractionMethodCode, ExtractionConfidence, EpistemicStateCode, ContentHash, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@PasReport2, @VerReport, 2, N'Employee Observations', 2, N'Two store associates are recorded walking past the spill without placing a warning cone or initiating cleanup during the 38-minute window.', N'AZURE_DOCUMENT_INTELLIGENCE', 0.9100, N'VERIFIED', N'p11111111111111111111111111111111111111111111111111111111000002', @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DocumentPassage WHERE LegalDocumentPassageId=@PasMedical1)
		INSERT POLOXI.Legal_DocumentPassage (LegalDocumentPassageId, LegalDocumentVersionId, PageNumber, SectionPath, SequenceNumber, PassageText, ExtractionMethodCode, ExtractionConfidence, EpistemicStateCode, ContentHash, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@PasMedical1, @VerMedical, 3, N'Diagnosis', 1, N'Imaging confirms a displaced left distal radius fracture consistent with a fall onto an outstretched hand.', N'AZURE_DOCUMENT_INTELLIGENCE', 0.9400, N'VERIFIED', N'p22222222222222222222222222222222222222222222222222222222000001', @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DocumentPassage WHERE LegalDocumentPassageId=@PasMedical2)
		INSERT POLOXI.Legal_DocumentPassage (LegalDocumentPassageId, LegalDocumentVersionId, PageNumber, SectionPath, SequenceNumber, PassageText, ExtractionMethodCode, ExtractionConfidence, EpistemicStateCode, ContentHash, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@PasMedical2, @VerMedical, 5, N'Treatment Plan', 2, N'Patient underwent open reduction and internal fixation followed by a course of occupational therapy.', N'AZURE_DOCUMENT_INTELLIGENCE', 0.9200, N'VERIFIED', N'p22222222222222222222222222222222222222222222222222222222000002', @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_DocumentPassage WHERE LegalDocumentPassageId=@PasPolicy1)
		INSERT POLOXI.Legal_DocumentPassage (LegalDocumentPassageId, LegalDocumentVersionId, PageNumber, SectionPath, SequenceNumber, PassageText, ExtractionMethodCode, ExtractionConfidence, EpistemicStateCode, ContentHash, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@PasPolicy1, @VerPolicy, 1, N'Declarations', 1, N'Commercial General Liability declarations list a per-occurrence limit of $1,000,000 and a general aggregate limit of $2,000,000 for the named insured Voss Grocery Markets, LLC.', N'AZURE_DOCUMENT_INTELLIGENCE', 0.9800, N'VERIFIED', N'p33333333333333333333333333333333333333333333333333333333000001', @Tenant, DATEADD(DAY,-2,@Now), @User);

	-- 4) Evidence items.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterEvidenceItem WHERE LegalEvidenceItemId=@EviSpillDuration)
		INSERT POLOXI.Legal_MatterEvidenceItem (LegalEvidenceItemId, DecisionMatterId, LegalDocumentVersionId, LegalDocumentPassageId, EvidenceTypeCode, DimensionCode, Summary, EvidenceStateCode, Confidence, GenerationOriginCode, DomainConceptCode, VerificationProfileCode, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@EviSpillDuration, @PIM, @VerReport, @PasReport1, N'ACCIDENT_REPORT', N'LIABILITY_FACTS', N'Hazard present ~38 minutes before fall (surveillance-timed).', N'VERIFIED', 0.9500, N'DYNAMIC_LLM', N'CONSTRUCTIVE_NOTICE', N'LIABILITY_FACTS', @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterEvidenceItem WHERE LegalEvidenceItemId=@EviEmployeePass)
		INSERT POLOXI.Legal_MatterEvidenceItem (LegalEvidenceItemId, DecisionMatterId, LegalDocumentVersionId, LegalDocumentPassageId, EvidenceTypeCode, DimensionCode, Summary, EvidenceStateCode, Confidence, GenerationOriginCode, DomainConceptCode, VerificationProfileCode, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@EviEmployeePass, @PIM, @VerReport, @PasReport2, N'ACCIDENT_REPORT', N'LIABILITY_FACTS', N'Two employees passed the spill without remediation.', N'VERIFIED', 0.9000, N'DYNAMIC_LLM', N'BREACH_OF_DUTY', N'LIABILITY_FACTS', @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterEvidenceItem WHERE LegalEvidenceItemId=@EviFracture)
		INSERT POLOXI.Legal_MatterEvidenceItem (LegalEvidenceItemId, DecisionMatterId, LegalDocumentVersionId, LegalDocumentPassageId, EvidenceTypeCode, DimensionCode, Summary, EvidenceStateCode, Confidence, GenerationOriginCode, DomainConceptCode, VerificationProfileCode, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@EviFracture, @PIM, @VerMedical, @PasMedical1, N'MEDICAL_RECORDS', N'MEDICAL_CAUSATION', N'Displaced left distal radius fracture consistent with the fall mechanism.', N'VERIFIED', 0.9300, N'DYNAMIC_LLM', N'INJURY_CAUSATION', N'MEDICAL_CAUSATION', @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterEvidenceItem WHERE LegalEvidenceItemId=@EviTreatment)
		INSERT POLOXI.Legal_MatterEvidenceItem (LegalEvidenceItemId, DecisionMatterId, LegalDocumentVersionId, LegalDocumentPassageId, EvidenceTypeCode, DimensionCode, Summary, EvidenceStateCode, Confidence, GenerationOriginCode, DomainConceptCode, VerificationProfileCode, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@EviTreatment, @PIM, @VerMedical, @PasMedical2, N'MEDICAL_RECORDS', N'DAMAGES_QUANTUM', N'ORIF surgery plus occupational therapy documents treatment scope.', N'VERIFIED', 0.9100, N'DYNAMIC_LLM', N'SPECIAL_DAMAGES', N'DAMAGES_QUANTUM', @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterEvidenceItem WHERE LegalEvidenceItemId=@EviPolicyLimit)
		INSERT POLOXI.Legal_MatterEvidenceItem (LegalEvidenceItemId, DecisionMatterId, LegalDocumentVersionId, LegalDocumentPassageId, EvidenceTypeCode, DimensionCode, Summary, EvidenceStateCode, Confidence, GenerationOriginCode, DomainConceptCode, VerificationProfileCode, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@EviPolicyLimit, @PIM, @VerPolicy, @PasPolicy1, N'INSURANCE_COVERAGE', N'INSURANCE_COVERAGE', N'CGL per-occurrence limit $1M / aggregate $2M for the defendant.', N'VERIFIED', 0.9800, N'DYNAMIC_LLM', N'POLICY_LIMITS', NULL, @Tenant, DATEADD(DAY,-2,@Now), @User);

	-- 5) Fact propositions.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterFactProposition WHERE LegalFactPropositionId=@PropNotice)
		INSERT POLOXI.Legal_MatterFactProposition (LegalFactPropositionId, DecisionMatterId, PropositionText, FactStateCode, GenerationOriginCode, Confidence, IsDecisionAuthoritative, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@PropNotice, @PIM, N'Voss Grocery had constructive notice of the hazardous spill and failed to remediate it.', N'SUPPORTED', N'DYNAMIC_LLM', 0.9200, 1, @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterFactProposition WHERE LegalFactPropositionId=@PropCausation)
		INSERT POLOXI.Legal_MatterFactProposition (LegalFactPropositionId, DecisionMatterId, PropositionText, FactStateCode, GenerationOriginCode, Confidence, IsDecisionAuthoritative, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@PropCausation, @PIM, N'The fall caused Plaintiff''s displaced distal radius fracture requiring surgical repair.', N'SUPPORTED', N'DYNAMIC_LLM', 0.9000, 1, @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterFactProposition WHERE LegalFactPropositionId=@PropCoverage)
		INSERT POLOXI.Legal_MatterFactProposition (LegalFactPropositionId, DecisionMatterId, PropositionText, FactStateCode, GenerationOriginCode, Confidence, IsDecisionAuthoritative, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@PropCoverage, @PIM, N'Applicable CGL coverage provides at least $1,000,000 per occurrence for this claim.', N'ESTABLISHED', N'DYNAMIC_LLM', 0.9700, 1, @Tenant, DATEADD(DAY,-2,@Now), @User);

	-- 6) Proposition support edges.
	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterPropositionSupport WHERE LegalFactPropositionId=@PropNotice AND LegalEvidenceItemId=@EviSpillDuration AND RelationshipTypeCode=N'SUPPORTS')
		INSERT POLOXI.Legal_MatterPropositionSupport (LegalFactPropositionId, LegalEvidenceItemId, RelationshipTypeCode, AssessmentReason, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@PropNotice, @EviSpillDuration, N'SUPPORTS', N'38-minute hazard duration establishes constructive notice.', @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterPropositionSupport WHERE LegalFactPropositionId=@PropNotice AND LegalEvidenceItemId=@EviEmployeePass AND RelationshipTypeCode=N'SUPPORTS')
		INSERT POLOXI.Legal_MatterPropositionSupport (LegalFactPropositionId, LegalEvidenceItemId, RelationshipTypeCode, AssessmentReason, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@PropNotice, @EviEmployeePass, N'SUPPORTS', N'Employees passing the spill corroborates failure to remediate.', @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterPropositionSupport WHERE LegalFactPropositionId=@PropCausation AND LegalEvidenceItemId=@EviFracture AND RelationshipTypeCode=N'SUPPORTS')
		INSERT POLOXI.Legal_MatterPropositionSupport (LegalFactPropositionId, LegalEvidenceItemId, RelationshipTypeCode, AssessmentReason, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@PropCausation, @EviFracture, N'SUPPORTS', N'Imaging ties the fracture mechanism to the fall.', @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterPropositionSupport WHERE LegalFactPropositionId=@PropCausation AND LegalEvidenceItemId=@EviTreatment AND RelationshipTypeCode=N'SUPPORTS')
		INSERT POLOXI.Legal_MatterPropositionSupport (LegalFactPropositionId, LegalEvidenceItemId, RelationshipTypeCode, AssessmentReason, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@PropCausation, @EviTreatment, N'SUPPORTS', N'Surgical repair documents the injury severity and scope.', @Tenant, DATEADD(DAY,-2,@Now), @User);

	IF NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterPropositionSupport WHERE LegalFactPropositionId=@PropCoverage AND LegalEvidenceItemId=@EviPolicyLimit AND RelationshipTypeCode=N'SUPPORTS')
		INSERT POLOXI.Legal_MatterPropositionSupport (LegalFactPropositionId, LegalEvidenceItemId, RelationshipTypeCode, AssessmentReason, TenantId, CreatedDateUtc, CreatedByUserId)
		VALUES (@PropCoverage, @EviPolicyLimit, N'SUPPORTS', N'Declarations page states the applicable per-occurrence limit.', @Tenant, DATEADD(DAY,-2,@Now), @User);
END

COMMIT TRANSACTION;
