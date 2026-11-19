SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Add canonical PI evidence types and concepts for policy, rehabilitation, and medical liens.

DECLARE @PiPackId UNIQUEIDENTIFIER =
(
	SELECT TOP 1 DecisionDomainPackId
	FROM POLOXI.Legal_DecisionDomainPack
	WHERE PackCode = N'PERSONAL_INJURY' AND TenantId IS NULL AND IsDeleted = 0
	ORDER BY IsDefault DESC, SortOrder
);

IF @PiPackId IS NULL
	THROW 50001, 'PERSONAL_INJURY domain pack must exist before migration 0498.', 1;

-- ── Evidence types ──
MERGE POLOXI.Legal_DecisionDomainPackEvidenceType AS target
USING (VALUES
	(N'INSURANCE_POLICY',   N'Insurance Policy / Declarations', N'INSURANCE', N'Policy, declarations page, endorsements, coverage and limits.', 110),
	(N'TREATMENT_RECORD',   N'Treatment / Rehabilitation Record', N'INJURY',  N'Physical therapy, rehabilitation, and ongoing treatment records.', 120),
	(N'MEDICAL_LIEN',       N'Medical Lien / Subrogation',      N'DAMAGES',   N'Medical liens, subrogation and reimbursement claims against recovery.', 130)
) AS source (EvidenceTypeCode, Name, DimensionCode, Description, SortOrder)
ON target.DecisionDomainPackId = @PiPackId AND target.EvidenceTypeCode = source.EvidenceTypeCode AND target.TenantId IS NULL
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, EvidenceTypeCode, Name, DimensionCode, Description, SortOrder, TenantId)
	VALUES (@PiPackId, source.EvidenceTypeCode, source.Name, source.DimensionCode, source.Description, source.SortOrder, NULL);

-- ── Semantic concepts ──
MERGE POLOXI.Legal_DecisionDomainConcept AS target
USING (VALUES
	(N'TREATMENT_RECORD_COURSE', N'INJURY', N'Treatment and rehabilitation course', N'The course, extent, and reasonableness of treatment and rehabilitation for the claimed injury.', N'MIXED', N'MATTER_DOCUMENT', N'MEDICAL_CAUSATION', NULL, NULL, 0, 1, 120),
	(N'MEDICAL_LIEN_SUBROGATION', N'DAMAGES', N'Medical lien and subrogation', N'Liens, subrogation, and reimbursement interests asserted against the matter recovery.', N'MIXED', N'MATTER_DOCUMENT', N'DAMAGES_QUANTUM', NULL, NULL, 0, 1, 130)
) AS source (ConceptCode, DimensionCode, Name, Description, ConceptKindCode, SourceClassCode, VerificationProfileCode, JurisdictionCode, MatterTypeCode, IsRequiredCoverage, IsFallbackEligible, SortOrder)
ON target.DecisionDomainPackId = @PiPackId AND target.ConceptCode = source.ConceptCode AND target.TenantId IS NULL
WHEN MATCHED THEN UPDATE SET
	DimensionCode = source.DimensionCode, Name = source.Name, Description = source.Description,
	ConceptKindCode = source.ConceptKindCode, SourceClassCode = source.SourceClassCode,
	VerificationProfileCode = source.VerificationProfileCode, JurisdictionCode = source.JurisdictionCode,
	MatterTypeCode = source.MatterTypeCode, IsRequiredCoverage = source.IsRequiredCoverage,
	IsFallbackEligible = source.IsFallbackEligible, SortOrder = source.SortOrder, IsActive = 1,
	IsDeleted = 0,
	ModifiedDateUtc = SYSUTCDATETIME()
WHEN NOT MATCHED BY TARGET THEN
	INSERT (DecisionDomainPackId, ConceptCode, DimensionCode, Name, Description, ConceptKindCode, SourceClassCode, VerificationProfileCode, JurisdictionCode, MatterTypeCode, IsRequiredCoverage, IsFallbackEligible, SortOrder, TenantId)
	VALUES (@PiPackId, source.ConceptCode, source.DimensionCode, source.Name, source.Description, source.ConceptKindCode, source.SourceClassCode, source.VerificationProfileCode, source.JurisdictionCode, source.MatterTypeCode, source.IsRequiredCoverage, source.IsFallbackEligible, source.SortOrder, NULL);

COMMIT TRANSACTION;
