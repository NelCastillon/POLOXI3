using System.ComponentModel.DataAnnotations;

namespace Legal.Application.Features.Intelligence.Decision;

public static class LegalDocumentProcessingStates
{
    public const string Received = "RECEIVED";
    public const string Quarantined = "QUARANTINED";
    public const string Processing = "PROCESSING";
    public const string Processed = "PROCESSED";
    public const string EnrichmentPending = "ENRICHMENT_PENDING";
    public const string Enriched = "ENRICHED";
    public const string Failed = "FAILED";
}

public sealed record LegalDocumentSecurityScanResult(
    string StatusCode,
    string? QuarantineReference = null);

public static class LegalDocumentExtractionMethods
{
    public const string NativeText = "NATIVE_TEXT";
    public const string AzureDocumentIntelligence = "AZURE_DOCUMENT_INTELLIGENCE";
    public const string TableExtraction = "TABLE_EXTRACTION";
    public const string Metadata = "METADATA";
    public const string ImageAnalysis = "IMAGE_ANALYSIS";
    public const string ManualEntry = "MANUAL_ENTRY";
    public const string LegacySearchProjection = "LEGACY_SEARCH_PROJECTION";
}

public static class LegalEvidenceStates
{
    public const string Proposed = "PROPOSED";
    public const string Verified = "VERIFIED";
    public const string Disputed = "DISPUTED";
    public const string Invalidated = "INVALIDATED";
}

public static class LegalFactStates
{
    public const string Alleged = "ALLEGED";
    public const string Supported = "SUPPORTED";
    public const string Disputed = "DISPUTED";
    public const string Established = "ESTABLISHED";
    public const string Invalidated = "INVALIDATED";
}

public static class LegalDocumentRelationshipTypes
{
    public const string Supports = "SUPPORTS";
    public const string Contradicts = "CONTRADICTS";
    public const string Qualifies = "QUALIFIES";
    public const string DerivedFrom = "DERIVED_FROM";
    public const string RelatedTo = "RELATED_TO";
}

public static class DecisionRetrievalStages
{
    public const string Ingestion = "INGESTION";
    public const string MatterContext = "MATTER_CONTEXT";
    public const string DecisionResearch = "DECISION_RESEARCH";
}

public static class DecisionResearchRouteCodes
{
    public const string MatterCorpus = "MATTER_CORPUS";
    public const string LegalAuthority = "LEGAL_AUTHORITY";
    public const string NoneDerived = "NONE_DERIVED";
    public const string LegacyProjection = "LEGACY_PROJECTION";
}

public sealed record LegalDocumentIntakeRequest(
    Guid TenantId,
    Guid UserId,
    Guid MatterId,
    [Required, StringLength(500)] string FileName,
    [Required, StringLength(200)] string ContentType,
    [Range(1, long.MaxValue)] long FileSizeBytes,
    [Required, StringLength(120)] string CorrelationId)
{
    [StringLength(60)] public string? DomainPackCode { get; init; }
    [StringLength(80)] public string? DocumentTypeCode { get; init; }
}

public sealed record LegalDocumentDto(
    Guid LegalDocumentId,
    Guid MatterId,
    string FileName,
    string ContentType,
    string StatusCode,
    string? DocumentTypeCode,
    string? DomainPackCode,
    DateTime CreatedDateUtc,
    IReadOnlyCollection<LegalDocumentVersionDto> Versions);

public sealed record LegalDocumentVersionDto(
    Guid LegalDocumentVersionId,
    int VersionNumber,
    string Sha256Hash,
    string StorageReference,
    long FileSizeBytes,
    string MalwareStatusCode,
    string ProcessingStatusCode,
    string? ExtractionProviderCode,
    string? ExtractionModelCode,
    string? ExtractionModelVersion,
    DateTime CreatedDateUtc);

public sealed record LegalDocumentPassageDto(
    Guid LegalDocumentPassageId,
    Guid LegalDocumentVersionId,
    int? PageNumber,
    string? SectionPath,
    int SequenceNumber,
    string Text,
    string ExtractionMethodCode,
    decimal? ExtractionConfidence,
    string? BoundingRegionJson,
    string? SourceSpanJson,
    string EpistemicStateCode);

public sealed record LegalEvidenceItemDto(
    Guid LegalEvidenceItemId,
    Guid MatterId,
    Guid LegalDocumentVersionId,
    Guid? LegalDocumentPassageId,
    string EvidenceTypeCode,
    string DimensionCode,
    string Summary,
    string EvidenceStateCode,
    decimal? Confidence,
    string GenerationOriginCode,
    string? DomainConceptCode,
    string? VerificationProfileCode);

public sealed record LegalFactPropositionDto(
    Guid LegalFactPropositionId,
    Guid MatterId,
    string PropositionText,
    string FactStateCode,
    string GenerationOriginCode,
    decimal? Confidence,
    bool IsDecisionAuthoritative,
    IReadOnlyCollection<LegalPropositionSupportDto> Support);

public sealed record LegalPropositionSupportDto(
    Guid LegalPropositionSupportId,
    Guid LegalFactPropositionId,
    Guid LegalEvidenceItemId,
    string RelationshipTypeCode,
    string? AssessmentReason);

public sealed record LegalMatterContextItem(
    Guid MatterId,
    Guid? LegalDocumentId,
    Guid? LegalDocumentVersionId,
    Guid? PassageId,
    Guid? EvidenceItemId,
    Guid? FactPropositionId,
    string Title,
    string Text,
    string SourceReference,
    int? PageNumber,
    string ExtractionMethodCode,
    string EvidenceStateCode,
    string FactStateCode,
    bool IsDecisionAuthoritative,
    decimal RelevanceScore,
    string? DocumentTypeCode,
    string? DimensionCode);

public sealed record LegalMatterContextResult(
    bool Enabled,
    string SourceRouteCode,
    IReadOnlyCollection<LegalMatterContextItem> Items,
    int CandidateCount,
    int FilteredCount,
    string? NotRunReason = null);

public sealed record DecisionResearchRoute(
    string RouteCode,
    string SourceClassCode,
    string ResearchNeedTypeCode,
    bool RetrievalRequired,
    string Reason,
    string? Jurisdiction,
    IReadOnlyCollection<string> AuthorityKinds,
    DateTime? AuthorityCutoffDate,
    IReadOnlyCollection<string> DocumentTypeCodes);

public sealed record DecisionRetrievalTelemetry(
    Guid DecisionRetrievalTelemetryId,
    Guid? DecisionSessionId,
    Guid? MatterId,
    string StageCode,
    string EventCode,
    string RouteCode,
    bool Enabled,
    int CandidateCount,
    int FilteredCount,
    int ReturnedCount,
    string? ResearchNeedTypeCode,
    string? SourceClassCode,
    string? Jurisdiction,
    string? DetailJson,
    long DurationMilliseconds);

public sealed record DecisionRetrievalTelemetryDto(
    Guid DecisionRetrievalTelemetryId,
    Guid? DecisionSessionId,
    Guid? MatterId,
    string StageCode,
    string EventCode,
    string RouteCode,
    bool Enabled,
    int CandidateCount,
    int FilteredCount,
    int ReturnedCount,
    string? ResearchNeedTypeCode,
    string? SourceClassCode,
    string? Jurisdiction,
    long DurationMilliseconds,
    DateTime CreatedDateUtc);

public sealed record DecisionRetrievalArchitectureSettings(
    bool Stage1SemanticEnrichmentEnabled,
    bool Stage2MatterContextEnabled,
    int Stage2MaximumItems,
    int Stage2MaximumCharacters,
    bool Stage2LegacyProjectionFallbackEnabled,
    bool Stage3AuthoritativeRoutingEnabled,
    bool LegacyUnconditionalRetrievalEnabled,
    bool TelemetryEnabled);

public sealed record DocumentExtractionRequest(
    Guid TenantId,
    Guid DocumentId,
    Guid DocumentVersionId,
    string FileName,
    string ContentType,
    Stream Content,
    string CorrelationId,
    bool PreferNativeText = true,
    IReadOnlyCollection<int>? PageNumbers = null);

public sealed record DocumentExtractionResult(
    string ProviderCode,
    string ModelCode,
    string ModelVersion,
    DateTime ProcessedDateUtc,
    IReadOnlyCollection<DocumentExtractedPage> Pages,
    IReadOnlyCollection<DocumentExtractedSection> Sections,
    IReadOnlyCollection<DocumentExtractedTable> Tables,
    string RawResultReference,
    IReadOnlyCollection<int>? FallbackPageNumbers = null,
    IReadOnlyCollection<DocumentExtractedFigure>? Figures = null)
{
    public IReadOnlyCollection<int> PagesRequiringFallback => FallbackPageNumbers ?? [];
    public IReadOnlyCollection<DocumentExtractedFigure> ExtractedFigures => Figures ?? [];
}

public sealed record DocumentExtractedPage(
    int PageNumber,
    string Text,
    string ExtractionMethodCode,
    decimal? Confidence,
    decimal? Width,
    decimal? Height,
    string? Unit,
    IReadOnlyCollection<DocumentExtractedParagraph> Paragraphs,
    IReadOnlyCollection<DocumentExtractedLine>? Lines = null,
    IReadOnlyCollection<DocumentExtractedWord>? Words = null,
    IReadOnlyCollection<DocumentExtractedSelectionMark>? SelectionMarks = null,
    bool IsNativeTextReliable = true)
{
    public IReadOnlyCollection<DocumentExtractedLine> ExtractedLines => Lines ?? [];
    public IReadOnlyCollection<DocumentExtractedWord> ExtractedWords => Words ?? [];
    public IReadOnlyCollection<DocumentExtractedSelectionMark> ExtractedSelectionMarks => SelectionMarks ?? [];
}

public sealed record DocumentExtractedParagraph(
    int SequenceNumber,
    string Text,
    string? Role,
    string? BoundingRegionJson,
    decimal? Confidence,
    string? SourceSpanJson = null);

public sealed record DocumentExtractedFigure(
    string FigureId,
    int? PageNumber,
    string? Caption,
    string? BoundingRegionJson,
    string? SourceSpanJson,
    string ContentJson);

public sealed record DocumentExtractedLine(
    int SequenceNumber,
    string Text,
    string? PolygonJson,
    string? SourceSpanJson);

public sealed record DocumentExtractedWord(
    int SequenceNumber,
    string Text,
    decimal? Confidence,
    string? PolygonJson,
    string? SourceSpanJson);

public sealed record DocumentExtractedSelectionMark(
    int SequenceNumber,
    string StateCode,
    decimal? Confidence,
    string? PolygonJson,
    string? SourceSpanJson);

public sealed record DocumentExtractedSection(
    string SectionPath,
    string? Title,
    int? StartPageNumber,
    int? EndPageNumber,
    string Text,
    string? SourceSpanJson = null);

public sealed record DocumentExtractedTable(
    int? PageNumber,
    int RowCount,
    int ColumnCount,
    string ContentJson,
    string? BoundingRegionJson,
    string? SourceSpanJson = null);

public sealed record LegalDocumentSemanticProposal(
    string? DocumentTypeCode,
    decimal? ClassificationConfidence,
    IReadOnlyCollection<LegalEvidenceSemanticProposal> EvidenceItems,
    IReadOnlyCollection<LegalFactSemanticProposal> FactPropositions,
    IReadOnlyCollection<LegalSemanticRelationshipProposal> Relationships,
    IReadOnlyCollection<string> Ambiguities,
    IReadOnlyCollection<string> Unknowns);

public sealed record LegalEvidenceSemanticProposal(
    string ProposalKey,
    Guid? PassageId,
    string EvidenceTypeCode,
    string DimensionCode,
    string Summary,
    decimal? Confidence,
    string? DomainConceptCode,
    string? VerificationProfileCode);

public sealed record LegalFactSemanticProposal(
    string ProposalKey,
    string PropositionText,
    string FactStateCode,
    decimal? Confidence);

public sealed record LegalSemanticRelationshipProposal(
    string SourceProposalKey,
    string TargetProposalKey,
    string RelationshipTypeCode,
    string? Rationale);
