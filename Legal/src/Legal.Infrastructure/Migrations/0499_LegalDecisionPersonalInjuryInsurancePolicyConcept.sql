SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

DECLARE @PiPackId UNIQUEIDENTIFIER =
(
	SELECT TOP 1 DecisionDomainPackId
	FROM POLOXI.Legal_DecisionDomainPack
	WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0
	ORDER BY IsDefault DESC, SortOrder
);

IF @PiPackId IS NULL
	THROW 50001, 'PERSONAL_INJURY domain pack must exist before migration 0499.', 1;

-- INSURANCE_POLICY is already an evidence type in INSURANCE_COVERAGE (migration 0405).
-- Match that dimension without changing its canonical evidence-type configuration.
MERGE POLOXI.Legal_DecisionDomainConcept AS target
USING (VALUES
	(N'INSURANCE_POLICY_COVERAGE', N'INSURANCE_COVERAGE', N'Insurance policy coverage and limits',
	 N'Policy existence, term, insured, applicable coverages, conditions, exclusions and limits; policy existence does not establish liability.',
	 N'MIXED', N'MATTER_DOCUMENT', NULL, NULL, NULL, 0, 1, 611)
) AS source (ConceptCode, DimensionCode, Name, Description, ConceptKindCode, SourceClassCode, VerificationProfileCode, JurisdictionCode, MatterTypeCode, IsRequiredCoverage, IsFallbackEligible, SortOrder)
ON target.DecisionDomainPackId = @PiPackId AND target.ConceptCode = source.ConceptCode AND target.TenantId IS NULL
WHEN MATCHED THEN UPDATE SET
	DimensionCode = source.DimensionCode, Name = source.Name, Description = source.Description,
	ConceptKindCode = source.ConceptKindCode, SourceClassCode = source.SourceClassCode,
	VerificationProfileCode = source.VerificationProfileCode, JurisdictionCode = source.JurisdictionCode,
	MatterTypeCode = source.MatterTypeCode, IsRequiredCoverage = source.IsRequiredCoverage,
	IsFallbackEligible = source.IsFallbackEligible, SortOrder = source.SortOrder, IsActive = 1,
	IsDeleted = 0, ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, ConceptCode, DimensionCode, Name, Description, ConceptKindCode, SourceClassCode, VerificationProfileCode, JurisdictionCode, MatterTypeCode, IsRequiredCoverage, IsFallbackEligible, SortOrder, TenantId)
	VALUES (@PiPackId, source.ConceptCode, source.DimensionCode, source.Name, source.Description, source.ConceptKindCode, source.SourceClassCode, source.VerificationProfileCode, source.JurisdictionCode, source.MatterTypeCode, source.IsRequiredCoverage, source.IsFallbackEligible, source.SortOrder, NULL);

-- Previously attempted policy versions may carry a terminal ENRICHED/NO_EVIDENCE marker
-- despite having only the document-intake anchor. Queue ONLY these clean, text-extracted,
-- intake-only insurance policies for the existing user-initiated activation flow. No model
-- call occurs in this migration and versions with derived evidence are never reset.
UPDATE version
SET SemanticActivationStatusCode = NULL,
	SemanticActivatedDateUtc = NULL,
	ModifiedDateUtc = SYSUTCDATETIME()
FROM POLOXI.Legal_MatterDocumentVersion version
INNER JOIN POLOXI.Legal_MatterDocument document ON document.LegalDocumentId = version.LegalDocumentId
INNER JOIN POLOXI.Legal_DecisionMatter matter ON matter.DecisionMatterId = document.DecisionMatterId
WHERE document.DocumentTypeCode = N'INSURANCE_POLICY'
	AND (document.DecisionDomainPackId = @PiPackId OR
	   (document.DecisionDomainPackId IS NULL AND matter.DomainPackCode = N'PERSONAL_INJURY'))
  AND document.TenantId = version.TenantId
	AND matter.TenantId = version.TenantId AND matter.IsDeleted = 0
  AND document.IsDeleted = 0 AND version.IsDeleted = 0
  AND version.SemanticActivationStatusCode IN (N'ENRICHED', N'NO_EVIDENCE')
  AND version.MalwareStatusCode IN (N'CLEAN', N'NOT_DETECTED', N'PASSED')
  AND EXISTS (SELECT 1 FROM POLOXI.Legal_DocumentPassage passage
			  WHERE passage.LegalDocumentVersionId = version.LegalDocumentVersionId
				AND passage.TenantId = version.TenantId AND passage.IsDeleted = 0)
  AND NOT EXISTS (SELECT 1 FROM POLOXI.Legal_MatterEvidenceItem evidence
				  WHERE evidence.LegalDocumentVersionId = version.LegalDocumentVersionId
					AND evidence.TenantId = version.TenantId AND evidence.IsDeleted = 0
					AND evidence.GenerationOriginCode <> N'DOCUMENT_INTAKE');

COMMIT TRANSACTION;
