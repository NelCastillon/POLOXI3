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
    // Assigned when a support edge cannot ground its proposition because the source evidence was not admitted
    // (for example, it lacks a traceable source span). The edge is preserved for audit but proves nothing.
    public const string Insufficient = "INSUFFICIENT";
}

// T11 span-presence admission policy. An LLM-proposed evidence item may only be admitted as decision
// evidence when it is anchored to a passage that carries a traceable source span; otherwise it is recorded
// in a non-admitted state and its support edges are downgraded so it can never establish a proposition.
// Kept as a pure, side-effect-free policy so it is unit-testable without a database.
public static class LegalEvidenceAdmissionPolicy
{
    public const string NonAdmittedStateCode = LegalEvidenceStates.Invalidated;
    public const string NonAdmittedRationale =
        "Excluded: source passage lacks a traceable source span; evidence is not admissible to establish this proposition.";

    public static bool IsAdmissible(Guid? passageId, IReadOnlySet<Guid> spannedPassageIds)
        => passageId is Guid id && spannedPassageIds.Contains(id);

    public static string ResolveEvidenceStateCode(Guid? passageId, IReadOnlySet<Guid> spannedPassageIds)
        => IsAdmissible(passageId, spannedPassageIds) ? LegalEvidenceStates.Proposed : NonAdmittedStateCode;

    public static string ResolveRelationshipTypeCode(bool evidenceAdmitted, string proposedRelationshipTypeCode)
        => evidenceAdmitted ? proposedRelationshipTypeCode : LegalDocumentRelationshipTypes.Insufficient;

    public static string? ResolveRelationshipRationale(bool evidenceAdmitted, string? proposedRationale)
        => evidenceAdmitted ? proposedRationale : NonAdmittedRationale;

    // Phase 2 promotion gate. Evidence recorded in a non-admitted state (for example, INVALIDATED because its
    // source passage lacks a traceable span) must never be treated as a promotable candidate for independent
    // verification. Non-admitted evidence remains persisted for audit but is excluded from the SUPPLIED -> VERIFIED
    // path so it can never be promoted to VERIFIED / decision-authorized state.
    public static bool CanBePromoted(string? evidenceStateCode)
        => !string.IsNullOrWhiteSpace(evidenceStateCode)
           && !string.Equals(evidenceStateCode, NonAdmittedStateCode, StringComparison.OrdinalIgnoreCase);
}

// Disposition of an intake-time proposition-binding decision (blueprint §5/§12.3/§14).
public enum LegalPropositionBindingDisposition
{
    // No existing matter proposition matched: insert a new, non-authoritative issue proposition.
    NewIssue,
    // A confident match was found and the new evidence agrees: reuse the existing proposition (SUPPORTS edge).
    ReuseSupport,
    // A confident match was found but the new evidence conflicts: reuse the existing proposition, transition it
    // to DISPUTED and record a CONTRADICTS edge. The prior proposition text is never overwritten.
    ReuseContradict
}

// Outcome of resolving one LLM-proposed fact against the matter's existing canonical propositions.
public sealed record LegalPropositionBindingDecision(
    LegalPropositionBindingDisposition Disposition,
    Guid? MatchedPropositionId,
    string RelationshipTypeCode,
    string? MatchedFactStateCode,
    string? MatchedPropositionText = null)
{
    public bool IsReuse => Disposition != LegalPropositionBindingDisposition.NewIssue;
    public bool IsContradiction => Disposition == LegalPropositionBindingDisposition.ReuseContradict;
}

// Existing canonical proposition candidate presented to the binding policy (DB-shaped but DB-free).
public sealed record LegalExistingProposition(Guid PropositionId, string PropositionText, string FactStateCode);

// Intake-time proposition binding/matching policy (blueprint §5 "Proposed links to known canonical proposition
// IDs only, or NEW_ISSUE_PROPOSAL when no valid match", §12.3, §14). Matches an LLM-proposed fact against the
// matter's existing propositions so intake reuses canonical identity instead of duplicating (T04), preserves
// contradictions as DISPUTED without overwriting (T12), and emits a new issue when no valid match exists
// (T18/T19/T31). Kept as a pure, side-effect-free, unit-testable policy mirroring LegalEvidenceAdmissionPolicy.
public static class LegalPropositionBindingPolicy
{
    // Conservative reuse threshold: we favor NEW_ISSUE over an incorrect merge so distinct facts are never
    // collapsed and matter history is preserved. Below this Jaccard similarity, a proposed fact is a new issue.
    public const double ReuseSimilarityThreshold = 0.5;

    // Tokens hinting the proposed fact conflicts with (rather than corroborates) an existing proposition.
    private static readonly string[] ContradictionCues =
    [
        "not", "no ", "never", "deny", "denied", "denies", "dispute", "disputed", "reject", "rejected",
        "false", "incorrect", "contrary", "refute", "refuted", "without", "absence", "failed to", "did not"
    ];

    public static LegalPropositionBindingDecision Resolve(
        string proposedPropositionText,
        string proposedFactStateCode,
        IReadOnlyCollection<LegalExistingProposition> existingPropositions)
    {
        var proposedTokens = Tokenize(proposedPropositionText);
        LegalExistingProposition? best = null;
        var bestScore = 0d;
        if (proposedTokens.Count > 0)
        {
            foreach (var existing in existingPropositions)
            {
                var score = Jaccard(proposedTokens, Tokenize(existing.PropositionText));
                if (score > bestScore)
                {
                    bestScore = score;
                    best = existing;
                }
            }
        }

        if (best is null || bestScore < ReuseSimilarityThreshold)
            return new LegalPropositionBindingDecision(
                LegalPropositionBindingDisposition.NewIssue, null, LegalDocumentRelationshipTypes.Supports, null);

        var contradicts = ContradictionPolarity(proposedPropositionText) != ContradictionPolarity(best.PropositionText)
            || IsDisputedState(proposedFactStateCode);
        return contradicts
            ? new LegalPropositionBindingDecision(
                LegalPropositionBindingDisposition.ReuseContradict, best.PropositionId,
                LegalDocumentRelationshipTypes.Contradicts, best.FactStateCode, best.PropositionText)
            : new LegalPropositionBindingDecision(
                LegalPropositionBindingDisposition.ReuseSupport, best.PropositionId,
                LegalDocumentRelationshipTypes.Supports, best.FactStateCode, best.PropositionText);
    }

    // A contradiction reuse transitions the matched proposition to DISPUTED, but never downgrades a stronger,
    // already-established or already-disputed state, and never overwrites the proposition text.
    public static string ResolveReusedFactStateCode(string existingFactStateCode)
        => string.Equals(existingFactStateCode, LegalFactStates.Established, StringComparison.OrdinalIgnoreCase)
           || string.Equals(existingFactStateCode, LegalFactStates.Disputed, StringComparison.OrdinalIgnoreCase)
            ? existingFactStateCode
            : LegalFactStates.Disputed;

    private static bool IsDisputedState(string? factStateCode)
        => string.Equals(factStateCode, LegalFactStates.Disputed, StringComparison.OrdinalIgnoreCase)
           || string.Equals(factStateCode, LegalFactStates.Invalidated, StringComparison.OrdinalIgnoreCase);

    private static bool ContradictionPolarity(string text)
    {
        var lowered = " " + text.ToLowerInvariant() + " ";
        return ContradictionCues.Any(cue => lowered.Contains(cue, StringComparison.Ordinal));
    }

    private static HashSet<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return [];
        return text
            .ToLowerInvariant()
            .Split([' ', '\t', '\n', '\r', '.', ',', ';', ':', '(', ')', '"', '\'', '/', '-'], StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Length >= 4)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0)
            return 0;
        var intersection = a.Count(b.Contains);
        var union = a.Count + b.Count - intersection;
        return union == 0 ? 0 : (double)intersection / union;
    }
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
    // Optional explicit CHAT model for Stage 1 semantic enrichment. When null, the feature-policy
    // default route is used; when set, it overrides the deployment selected for proposition extraction.
    [StringLength(80)] public string? ModelCode { get; init; }
    // When true, intake performs only the deterministic prepare steps (validate, scan, store, persist,
    // extract text). The metered Stage 1 semantic enrichment and Continuous Decision Integrity (CDC)
    // re-check are deferred to explicit activation on the Disambiguate & Answer path.
    public bool PrepareOnly { get; init; }
}

// Summary of a matter's corpus enrichment/activation state. Prepared versions have extracted text but
// no derived propositions yet; activation enriches them and re-runs Continuous Decision Integrity.
public sealed record LegalMatterCorpusActivationStatus(
    Guid MatterId,
    int TotalVersions,
    int EnrichedVersions,
    int PendingVersions,
    int ActivatedThisCall)
{
    public bool IsComplete => PendingVersions == 0;
}

// A prepared-but-not-activated document version: extracted text exists, but Stage 1 semantic
// enrichment (atomic propositions) has not yet produced any evidence rows for it.
public sealed record LegalPendingCorpusVersion(
    Guid LegalDocumentId,
    Guid LegalDocumentVersionId,
    string FileName,
    string Sha256Hash,
    string? DocumentTypeCode);

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

// ── Document Intelligence workspace read (matter-scoped evidence ↔ proposition graph) ──
// Source-traceable evidence: EvidenceItem joined to its originating document + passage so the
// UI can show the source citation (file, page, passage text) alongside the assertion.
public sealed record LegalEvidenceGraphItemDto(
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
    string? VerificationProfileCode,
    Guid LegalDocumentId,
    string DocumentFileName,
    string? DocumentTypeCode,
    int DocumentVersionNumber,
    int? PageNumber,
    string? SectionPath,
    string? PassageText,
    decimal? ExtractionConfidence);

// Fact proposition with its evidence support edges (SUPPORTS / CONTRADICTS / CONTEXT / INSUFFICIENT).
public sealed record LegalEvidenceGraphPropositionDto(
    Guid LegalFactPropositionId,
    Guid MatterId,
    string PropositionText,
    string FactStateCode,
    string GenerationOriginCode,
    decimal? Confidence,
    bool IsDecisionAuthoritative,
    IReadOnlyCollection<LegalPropositionSupportDto> Support);

// Aggregate matter-level payload for the Document Intelligence tab.
public sealed record LegalMatterEvidenceGraphDto(
    Guid MatterId,
    int DocumentCount,
    IReadOnlyCollection<LegalEvidenceGraphItemDto> Evidence,
    IReadOnlyCollection<LegalEvidenceGraphPropositionDto> Propositions);

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
    string? DetailJson,
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
    bool TelemetryEnabled,
    bool HybridSemanticScoringEnabled,
    // ── Phase E1: configurable hybrid retrieval weights and fusion ──────────────────────────────────
    // Defaults preserve the original hard-coded 0.65/0.35/0.05/0.05 behavior exactly. VectorWeight +
    // KeywordWeight must sum to 1.0 (validated/normalized in the settings loader).
    double VectorWeight = 0.65,
    double KeywordWeight = 0.35,
    double AuthoritativeBoost = 0.05,
    double VerifiedBoost = 0.05,
    int InitialCandidateLimit = 50,
    int RerankLimit = 10,
    double MinimumCandidateScore = 0.0,
    HybridFusionStrategy FusionStrategy = HybridFusionStrategy.WeightedScore,
    // ── Phase E2: proposition-first retrieval & dual-direction evidence search ──────────────────────
    // Both default to false so Stage 1 candidate-passage retrieval and current ranking are unchanged.
    // When PropositionQueryRetrievalEnabled is on, the matter-corpus semantic query is derived from the
    // atomic proposition rather than the free-text SearchQuery alone. DualDirectionRetrievalEnabled
    // additionally requests counter-oriented passages (evidence that could rebut the proposition) and
    // requires PropositionQueryRetrievalEnabled.
    bool PropositionQueryRetrievalEnabled = false,
    bool DualDirectionRetrievalEnabled = false);

// A passage that still lacks a persisted embedding vector — the unit of work for embedding generation.
public sealed record LegalPassageEmbeddingCandidate(
    Guid LegalDocumentPassageId,
    string PassageText);

// ── Phase E1: retrieval score transparency ────────────────────────────────────────────────────────
// The hybrid retrieval score is no longer an opaque single number. Every component is surfaced so a
// passage's rank can be audited and explained. This is a RETRIEVAL RANK ("should we inspect this
// passage?") and must never be treated as an evidence relation or a POLOXI decision effect.
public sealed record PassageRetrievalScore(
    double VectorSimilarity,
    double KeywordScore,
    double SemanticLexicalBlend,
    double AuthorityBoost,
    double VerificationBoost,
    double FinalRetrievalRank,
    string RetrievalVersion,
    string FusionStrategy);

// Fusion strategy for combining vector and keyword signals. WeightedScore is the established
// production baseline; ReciprocalRankFusion is scaffolded for future benchmarking (Phase E3) and is
// not wired into the production path yet.
public enum HybridFusionStrategy
{
    WeightedScore = 0,
    ReciprocalRankFusion = 1
}

// ── Phase E2: proposition-first retrieval ──────────────────────────────────────────────────────────
// Retrieval orientation for a proposition-derived query. SUPPORT retrieves passages that could
// corroborate the proposition; COUNTER retrieves passages that could rebut it. Counter retrieval is
// only requested when DualDirectionRetrievalEnabled is on.
public enum RetrievalDirection
{
    Support = 0,
    Counter = 1
}

// A first-class retrieval query built from an atomic proposition (Stage 2 groundwork). It carries the
// proposition text, the resolved embedding/keyword query text, the intended orientation, and any
// structured search concepts extracted upstream. This is retrieval INTENT only — it never asserts an
// evidence relation and never modifies a POLOXI decision effect.
public sealed record PropositionRetrievalQuery(
    string PropositionText,
    string QueryText,
    RetrievalDirection Direction,
    IReadOnlyList<string> SearchConcepts);


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
