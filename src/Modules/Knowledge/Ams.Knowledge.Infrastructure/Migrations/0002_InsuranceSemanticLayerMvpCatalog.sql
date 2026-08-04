SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @SystemUserId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000002';
DECLARE @Now DATETIME2(7) = SYSUTCDATETIME();

DECLARE @Schemes TABLE (ConceptSchemeId UNIQUEIDENTIFIER PRIMARY KEY, SchemeCode VARCHAR(100), Name NVARCHAR(200), Description NVARCHAR(1000));
INSERT INTO @Schemes VALUES
('a1000000-0000-0000-0000-000000000001','INSURANCE_PRODUCT',N'Insurance Product',N'Canonical insurance product and line hierarchy.'),
('a1000000-0000-0000-0000-000000000002','POLICY_STATUS',N'Policy Status',N'Canonical policy lifecycle statuses.'),
('a1000000-0000-0000-0000-000000000003','CLAIM_CAUSE_OF_LOSS',N'Claim Cause of Loss',N'Canonical claim cause-of-loss vocabulary.'),
('a1000000-0000-0000-0000-000000000004','COVERAGE_TYPE',N'Coverage Type',N'Canonical insurance coverage vocabulary.'),
('a1000000-0000-0000-0000-000000000005','PARTY_ROLE',N'Party Role',N'Canonical insurance party roles.'),
('a1000000-0000-0000-0000-000000000006','DOCUMENT_TYPE',N'Document Type',N'Canonical insurance document classifications.'),
('a1000000-0000-0000-0000-000000000007','CARRIER_PRODUCT',N'Carrier Product',N'Canonical carrier product classifications.'),
('a1000000-0000-0000-0000-000000000008','BUSINESS_CLASSIFICATION',N'Business Classification',N'Canonical insured business classifications.'),
('a1000000-0000-0000-0000-000000000009','WORKFLOW_ACTION',N'Workflow Action',N'Canonical insurance workflow actions.'),
('a1000000-0000-0000-0000-000000000010','ACCOUNTING_TRANSACTION',N'Accounting Transaction',N'Canonical insurance accounting transactions.');
MERGE knowledge.ConceptScheme AS target
USING @Schemes AS source ON target.TenantId IS NULL AND target.SchemeCode = source.SchemeCode AND target.IsDeleted = 0
WHEN MATCHED AND target.IsSystemDefined = 1 THEN UPDATE SET Name = source.Name, Description = source.Description, AuthorityCode = 'AGENCYBINDER', StatusCode = 'PUBLISHED', ModifiedByUserId = @SystemUserId, ModifiedDateUtc = @Now
WHEN NOT MATCHED THEN INSERT (ConceptSchemeId, SchemeCode, Name, Description, AuthorityCode, VersionLabel, StatusCode, TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
VALUES (source.ConceptSchemeId, source.SchemeCode, source.Name, source.Description, 'AGENCYBINDER', '1.0', 'PUBLISHED', NULL, 1, @SystemUserId, @Now, 0);

DECLARE @Concepts TABLE
(
	KnowledgeConceptId UNIQUEIDENTIFIER PRIMARY KEY,
	ConceptSchemeId UNIQUEIDENTIFIER,
	ConceptCode VARCHAR(100),
	ConceptTypeCode VARCHAR(50),
	PreferredLabel NVARCHAR(250),
	Definition NVARCHAR(1000),
	ParentConceptId UNIQUEIDENTIFIER NULL,
	IsAbstract BIT,
	IsSelectable BIT
);
INSERT INTO @Concepts VALUES
('b1000000-0000-0000-0000-000000000001','a1000000-0000-0000-0000-000000000001','PRODUCT.INSURANCE',N'INSURANCE_PRODUCT',N'Insurance Product',N'A product that transfers or manages insurable risk.',NULL,1,0),
('b1000000-0000-0000-0000-000000000002','a1000000-0000-0000-0000-000000000001','PRODUCT.PERSONAL_LINES',N'INSURANCE_PRODUCT',N'Personal Lines',N'Insurance products primarily covering individuals and households.','b1000000-0000-0000-0000-000000000001',1,0),
('b1000000-0000-0000-0000-000000000003','a1000000-0000-0000-0000-000000000001','PRODUCT.COMMERCIAL_LINES',N'INSURANCE_PRODUCT',N'Commercial Lines',N'Insurance products primarily covering business operations and exposures.','b1000000-0000-0000-0000-000000000001',1,0),
('b1000000-0000-0000-0000-000000000004','a1000000-0000-0000-0000-000000000001','LOB.PERSONAL_AUTO',N'LINE_OF_BUSINESS',N'Personal Auto',N'Automobile insurance for personal vehicles and household drivers.','b1000000-0000-0000-0000-000000000002',0,1),
('b1000000-0000-0000-0000-000000000005','a1000000-0000-0000-0000-000000000001','LOB.COMMERCIAL_AUTO',N'LINE_OF_BUSINESS',N'Commercial Auto',N'Automobile insurance for vehicles used in business operations.','b1000000-0000-0000-0000-000000000003',0,1),
('b1000000-0000-0000-0000-000000000006','a1000000-0000-0000-0000-000000000001','LOB.COMMERCIAL_GENERAL_LIABILITY',N'LINE_OF_BUSINESS',N'Commercial General Liability',N'Liability insurance for common business premises, operations, products, and completed operations exposures.','b1000000-0000-0000-0000-000000000003',0,1),
('b1000000-0000-0000-0000-000000000101','a1000000-0000-0000-0000-000000000004','COVERAGE.MEDICAL_PAYMENTS',N'COVERAGE',N'Medical Payments Coverage',N'Coverage for eligible medical expenses subject to policy terms.',NULL,0,1),
('b1000000-0000-0000-0000-000000000102','a1000000-0000-0000-0000-000000000004','COVERAGE.CYBER',N'COVERAGE',N'Cyber Coverage',N'Coverage addressing specified cyber, privacy, and network security risks.',NULL,0,1),
('b1000000-0000-0000-0000-000000000201','a1000000-0000-0000-0000-000000000005','PARTY_ROLE.ADDITIONAL_INSURED',N'PARTY_ROLE',N'Additional Insured',N'A party afforded insured status under applicable policy terms or endorsement.',NULL,0,1),
('b1000000-0000-0000-0000-000000000301','a1000000-0000-0000-0000-000000000006','DOCUMENT.INSURANCE_DOCUMENT',N'DOCUMENT_TYPE',N'Insurance Document',N'An insurance-related document classification root.',NULL,1,0),
('b1000000-0000-0000-0000-000000000302','a1000000-0000-0000-0000-000000000006','DOCUMENT.BINDER',N'DOCUMENT_TYPE',N'Binder',N'Temporary evidence of insurance pending issuance of the policy.','b1000000-0000-0000-0000-000000000301',0,1),
('b1000000-0000-0000-0000-000000000303','a1000000-0000-0000-0000-000000000006','DOCUMENT.DECLARATIONS',N'DOCUMENT_TYPE',N'Declarations',N'Policy declarations identifying key insured, coverage, limits, and term information.','b1000000-0000-0000-0000-000000000301',0,1),
('b1000000-0000-0000-0000-000000000304','a1000000-0000-0000-0000-000000000006','DOCUMENT.LOSS_RUN',N'DOCUMENT_TYPE',N'Loss Run',N'A report of historical claims and loss activity.','b1000000-0000-0000-0000-000000000301',0,1),
('b1000000-0000-0000-0000-000000000305','a1000000-0000-0000-0000-000000000006','DOCUMENT.APPLICATION',N'DOCUMENT_TYPE',N'Application',N'An insurance application or submission form.','b1000000-0000-0000-0000-000000000301',0,1),
('b1000000-0000-0000-0000-000000000306','a1000000-0000-0000-0000-000000000006','DOCUMENT.ENDORSEMENT',N'DOCUMENT_TYPE',N'Endorsement',N'A document modifying policy terms.','b1000000-0000-0000-0000-000000000301',0,1),
('b1000000-0000-0000-0000-000000000307','a1000000-0000-0000-0000-000000000006','DOCUMENT.CERTIFICATE',N'DOCUMENT_TYPE',N'Certificate of Insurance',N'A certificate summarizing specified insurance information.','b1000000-0000-0000-0000-000000000301',0,1),
('b1000000-0000-0000-0000-000000000308','a1000000-0000-0000-0000-000000000006','DOCUMENT.INVOICE',N'DOCUMENT_TYPE',N'Invoice',N'An insurance billing document.','b1000000-0000-0000-0000-000000000301',0,1),
('b1000000-0000-0000-0000-000000000309','a1000000-0000-0000-0000-000000000006','DOCUMENT.INSPECTION',N'DOCUMENT_TYPE',N'Inspection',N'An inspection report or record.','b1000000-0000-0000-0000-000000000301',0,1),
('b1000000-0000-0000-0000-000000000310','a1000000-0000-0000-0000-000000000006','DOCUMENT.CANCELLATION_NOTICE',N'DOCUMENT_TYPE',N'Cancellation Notice',N'A notice of policy cancellation.','b1000000-0000-0000-0000-000000000301',0,1),
('b1000000-0000-0000-0000-000000000311','a1000000-0000-0000-0000-000000000006','DOCUMENT.REINSTATEMENT_NOTICE',N'DOCUMENT_TYPE',N'Reinstatement Notice',N'A notice documenting policy reinstatement.','b1000000-0000-0000-0000-000000000301',0,1);
INSERT INTO knowledge.KnowledgeConcept
(KnowledgeConceptId, ConceptSchemeId, ConceptCode, ConceptTypeCode, PreferredLabel, NormalizedPreferredLabel, Definition, ParentConceptId, IsAbstract, IsSelectable, StatusCode, EffectiveFromUtc, EffectiveToUtc, VersionNumber, SupersedesConceptId, TenantId, IsSystemDefined, OwnerUserId, BusinessStewardUserId, TechnicalStewardUserId, DefinitionSource, LicensingNotes, CreatedByUserId, CreatedDateUtc, IsDeleted)
SELECT source.KnowledgeConceptId, source.ConceptSchemeId, source.ConceptCode, source.ConceptTypeCode, source.PreferredLabel, UPPER(source.PreferredLabel), source.Definition, source.ParentConceptId, source.IsAbstract, source.IsSelectable, 'PUBLISHED', @Now, NULL, 1, NULL, NULL, 1, @SystemUserId, @SystemUserId, @SystemUserId, N'AgencyBinder standard semantic catalog', N'AgencyBinder-defined concept; no external standards alignment is implied.', @SystemUserId, @Now, 0
FROM @Concepts source
WHERE NOT EXISTS (SELECT 1 FROM knowledge.KnowledgeConcept target WHERE target.ConceptSchemeId = source.ConceptSchemeId AND target.ConceptCode = source.ConceptCode AND target.VersionNumber = 1);

DECLARE @Labels TABLE (KnowledgeConceptId UNIQUEIDENTIFIER, Label NVARCHAR(250), LabelTypeCode VARCHAR(30), Source NVARCHAR(100), PRIMARY KEY (KnowledgeConceptId, Label));
INSERT INTO @Labels VALUES
('b1000000-0000-0000-0000-000000000004',N'Personal Automobile',N'ALTERNATIVE',N'AgencyBinder'),
('b1000000-0000-0000-0000-000000000005',N'Business Auto',N'ALTERNATIVE',N'AgencyBinder'),
('b1000000-0000-0000-0000-000000000005',N'Commercial Vehicle',N'ALTERNATIVE',N'AgencyBinder'),
('b1000000-0000-0000-0000-000000000005',N'Business Vehicle Insurance',N'ALTERNATIVE',N'AgencyBinder'),
('b1000000-0000-0000-0000-000000000005',N'Fleet Insurance',N'ALTERNATIVE',N'AgencyBinder'),
('b1000000-0000-0000-0000-000000000006',N'CGL',N'ABBREVIATION',N'AgencyBinder'),
('b1000000-0000-0000-0000-000000000101',N'Med Pay',N'ALTERNATIVE',N'AgencyBinder'),
('b1000000-0000-0000-0000-000000000101',N'Medical Expense',N'ALTERNATIVE',N'AgencyBinder'),
('b1000000-0000-0000-0000-000000000201',N'AI',N'ABBREVIATION',N'AgencyBinder'),
('b1000000-0000-0000-0000-000000000201',N'Addl Insured',N'ALTERNATIVE',N'AgencyBinder'),
('b1000000-0000-0000-0000-000000000304',N'Loss Runs',N'ALTERNATIVE',N'AgencyBinder'),
('b1000000-0000-0000-0000-000000000307',N'COI',N'ABBREVIATION',N'AgencyBinder');
INSERT INTO knowledge.ConceptLabel
(ConceptLabelId, KnowledgeConceptId, Label, NormalizedLabel, LabelTypeCode, LanguageCode, Source, IsSearchable, IsDeprecated, TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
SELECT NEWID(), source.KnowledgeConceptId, source.Label, UPPER(source.Label), source.LabelTypeCode, 'en-US', source.Source, 1, 0, NULL, 1, @SystemUserId, @Now, 0
FROM @Labels source
WHERE EXISTS (SELECT 1 FROM knowledge.KnowledgeConcept concept WHERE concept.KnowledgeConceptId = source.KnowledgeConceptId)
  AND NOT EXISTS (SELECT 1 FROM knowledge.ConceptLabel target WHERE target.KnowledgeConceptId = source.KnowledgeConceptId AND target.LanguageCode = 'en-US' AND target.NormalizedLabel = UPPER(source.Label) AND target.IsDeleted = 0);

INSERT INTO knowledge.ConceptLabel
(ConceptLabelId, KnowledgeConceptId, Label, NormalizedLabel, LabelTypeCode, LanguageCode, Source, IsSearchable, IsDeprecated, TenantId, IsSystemDefined, CreatedByUserId, CreatedDateUtc, IsDeleted)
SELECT NEWID(), concept.KnowledgeConceptId, concept.PreferredLabel, concept.NormalizedPreferredLabel, 'PREFERRED', 'en-US', N'AgencyBinder', 1, 0, NULL, 1, @SystemUserId, @Now, 0
FROM knowledge.KnowledgeConcept concept
WHERE concept.IsSystemDefined = 1 AND concept.VersionNumber = 1 AND concept.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM knowledge.ConceptLabel label WHERE label.KnowledgeConceptId = concept.KnowledgeConceptId AND label.LabelTypeCode = 'PREFERRED' AND label.LanguageCode = 'en-US' AND label.IsDeleted = 0 AND label.IsDeprecated = 0);

INSERT INTO knowledge.ConceptVersion
(ConceptVersionId, KnowledgeConceptId, VersionNumber, StatusCode, SnapshotJson, ChangeReason, CreatedByUserId, CreatedDateUtc)
SELECT NEWID(), concept.KnowledgeConceptId, concept.VersionNumber, concept.StatusCode,
	   (SELECT concept.KnowledgeConceptId, concept.ConceptSchemeId, concept.ConceptCode, concept.ConceptTypeCode, concept.PreferredLabel, concept.Definition, concept.ParentConceptId, concept.StatusCode, concept.EffectiveFromUtc, concept.VersionNumber FOR JSON PATH, WITHOUT_ARRAY_WRAPPER),
	   N'Initial AgencyBinder semantic catalog publication.', @SystemUserId, @Now
FROM knowledge.KnowledgeConcept concept
WHERE concept.IsSystemDefined = 1 AND concept.VersionNumber = 1 AND concept.IsDeleted = 0
  AND NOT EXISTS (SELECT 1 FROM knowledge.ConceptVersion version WHERE version.KnowledgeConceptId = concept.KnowledgeConceptId AND version.VersionNumber = concept.VersionNumber);

;WITH Hierarchy AS
(
	SELECT concept.KnowledgeConceptId AS AncestorConceptId, concept.KnowledgeConceptId AS DescendantConceptId, 0 AS Depth
	FROM knowledge.KnowledgeConcept concept
	WHERE concept.IsDeleted = 0 AND concept.StatusCode = 'PUBLISHED'
	UNION ALL
	SELECT hierarchy.AncestorConceptId, child.KnowledgeConceptId, hierarchy.Depth + 1
	FROM Hierarchy hierarchy
	INNER JOIN knowledge.KnowledgeConcept child ON child.ParentConceptId = hierarchy.DescendantConceptId
	WHERE child.IsDeleted = 0 AND child.StatusCode = 'PUBLISHED'
)
MERGE knowledge.ConceptHierarchyClosure AS target
USING (SELECT AncestorConceptId, DescendantConceptId, MIN(Depth) AS Depth FROM Hierarchy GROUP BY AncestorConceptId, DescendantConceptId) AS source
ON source.AncestorConceptId = target.AncestorConceptId AND source.DescendantConceptId = target.DescendantConceptId
WHEN MATCHED THEN UPDATE SET Depth = source.Depth, RefreshedDateUtc = @Now
WHEN NOT MATCHED THEN INSERT (AncestorConceptId, DescendantConceptId, Depth, RefreshedDateUtc) VALUES (source.AncestorConceptId, source.DescendantConceptId, source.Depth, @Now);
