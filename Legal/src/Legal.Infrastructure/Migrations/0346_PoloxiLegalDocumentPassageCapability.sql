SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRANSACTION;

-- Corrective seed: uploaded Matter Corpus documents are projected into the live search index
-- (AI.Legal_SearchDocument) by LegalDocumentSearchProjectionDispatcher with
-- EntityTypeCode = N'LEGAL_DOCUMENT_PASSAGE' and ModuleCode = N'LEGAL'. However, the POLOXI live
-- grounding path (IntelligenceRepository.ExecutePoloxiBranchAsync via GroundBranchAsync) only
-- retrieves evidence for entity types that exist as an active POLOXI.Legal_Capability with
-- ExecutionHandlerCode = N'AUTHORIZED_SEARCH_DOCUMENT'. Migration 0139 seeded capabilities for
-- Submission/Policy/Claim/Account/Document/Task only, so no branch could ever query uploaded legal
-- documents. This left every decision run reporting "Documents: AVAILABLE / Evidence: NONE".
-- This migration idempotently seeds the missing platform capability so uploaded corpus passages
-- become first-class enterprise evidence in the POLOXI decision pipeline.
IF OBJECT_ID(N'POLOXI.Legal_Capability',N'U') IS NOT NULL
BEGIN
	DECLARE @Capabilities TABLE(CapabilityCode NVARCHAR(120),DisplayName NVARCHAR(200),Description NVARCHAR(1000),EntityTypeCode NVARCHAR(100),ModuleCode NVARCHAR(100),ApprovedTermsJson NVARCHAR(MAX),SupportsRecency BIT,MinimumConfidence DECIMAL(5,4),SortOrder INT);
	INSERT @Capabilities VALUES
	(N'SEARCH_LEGAL_DOCUMENT_PASSAGES',N'Matter corpus document evidence',N'Authorized legal matter document passages uploaded to the Matter Corpus and projected into the enterprise search index for grounding.',N'LEGAL_DOCUMENT_PASSAGE',N'LEGAL',N'["document","documents","passage","exhibit","record","statute","authority","evidence","filing","brief","contract","agreement","deposition","transcript"]',1,.5000,5);

	MERGE POLOXI.Legal_Capability target USING @Capabilities source ON target.TenantId IS NULL AND target.CapabilityCode=source.CapabilityCode AND target.IsDeleted=0
	WHEN MATCHED THEN UPDATE SET DisplayName=source.DisplayName,Description=source.Description,EntityTypeCode=source.EntityTypeCode,ModuleCode=source.ModuleCode,ExecutionHandlerCode=N'AUTHORIZED_SEARCH_DOCUMENT',ApprovedTermsJson=source.ApprovedTermsJson,SupportsRecency=source.SupportsRecency,MinimumConfidence=source.MinimumConfidence,SortOrder=source.SortOrder,IsActive=1,ModifiedDateUtc=SYSUTCDATETIME()
	WHEN NOT MATCHED THEN INSERT(CapabilityId,TenantId,CapabilityCode,DisplayName,Description,EntityTypeCode,ModuleCode,ExecutionHandlerCode,ApprovedTermsJson,SupportsRecency,MinimumConfidence,SortOrder,IsActive,CreatedDateUtc,IsDeleted) VALUES(NEWID(),NULL,source.CapabilityCode,source.DisplayName,source.Description,source.EntityTypeCode,source.ModuleCode,N'AUTHORIZED_SEARCH_DOCUMENT',source.ApprovedTermsJson,source.SupportsRecency,source.MinimumConfidence,source.SortOrder,1,SYSUTCDATETIME(),0);
END

COMMIT;
