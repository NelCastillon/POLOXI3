using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Persistence;

public interface ILegalDocumentCorpusRepository
{
    Task<DecisionRetrievalArchitectureSettings> GetRetrievalArchitectureSettingsAsync(
        CancellationToken cancellationToken = default);

    Task MarkProcessingFailedAsync(
        Guid tenantId,
        Guid userId,
        Guid documentVersionId,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken = default);

    Task<Guid?> GetDocumentMatterIdAsync(
        Guid tenantId,
        Guid documentVersionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<DecisionRetrievalTelemetryDto>> GetRetrievalTelemetryAsync(
        Guid tenantId,
        Guid? matterId,
        Guid? decisionSessionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LegalMatterContextItem>> SearchRoutedMatterContextAsync(
        Guid tenantId,
        Guid matterId,
        string query,
        IReadOnlyCollection<string> documentTypeCodes,
        int maximumItems,
        IReadOnlyCollection<float>? queryEmbedding = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LegalDocumentDto>> GetMatterDocumentsAsync(
        Guid tenantId,
        Guid matterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LegalDocumentPassageDto>> GetDocumentPassagesAsync(
        Guid tenantId,
        Guid documentVersionId,
        CancellationToken cancellationToken = default);

    // Passages for a document version that still lack a persisted embedding vector, so activation can
    // generate them in bounded batches. Returns only rows with non-empty text.
    Task<IReadOnlyCollection<LegalPassageEmbeddingCandidate>> GetPassagesMissingEmbeddingAsync(
        Guid tenantId,
        Guid documentVersionId,
        CancellationToken cancellationToken = default);

    // Persists a generated embedding vector (JSON-serialized) plus model/provenance for a single passage.
    Task SavePassageEmbeddingAsync(
        Guid tenantId,
        Guid legalDocumentPassageId,
        string embeddingJson,
        string embeddingModelCode,
        CancellationToken cancellationToken = default);

    Task<LegalMatterEvidenceGraphDto> GetMatterEvidenceGraphAsync(
        Guid tenantId,
        Guid matterId,
        CancellationToken cancellationToken = default);

    // Hard-deletes document-derived evidence for a matter in a single transaction. When
    // request.DocumentIds is null/empty, every supporting document for the matter is purged
    // (full clean slate); otherwise only the specified documents and their derived rows are
    // removed. Document-evidence only: the matter and non-document data are preserved. Returns
    // per-table removed counts.
    Task<LegalMatterDocumentPurgeResult> PurgeMatterDocumentEvidenceAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        LegalMatterDocumentPurgeRequest request,
        CancellationToken cancellationToken = default);

    // Appends a new immutable source anchor (POLOXI.Legal_SourceAssertion). The row is append-only:
    // corrections supersede an existing anchor rather than mutating it. Returns the new anchor id.
    Task<Guid> AppendSourceAssertionAsync(
        Guid tenantId,
        Guid userId,
        LegalSourceAssertionCreateRequest request,
        CancellationToken cancellationToken = default);

    // Enrichment/activation state for a matter: how many clean document versions have extracted text,
    // and how many of those still lack any derived evidence (i.e. are prepared but not yet activated).
    Task<LegalMatterCorpusActivationStatus> GetMatterActivationStatusAsync(
        Guid tenantId,
        Guid matterId,
        CancellationToken cancellationToken = default);

    // Returns up to <paramref name="maximumVersions"/> prepared-but-not-activated versions (extracted
    // text present, no evidence rows yet) so activation can enrich them in observable chunks.
    Task<IReadOnlyCollection<LegalPendingCorpusVersion>> GetPendingCorpusVersionsAsync(
        Guid tenantId,
        Guid matterId,
        int maximumVersions,
        CancellationToken cancellationToken = default);

    // Records the terminal semantic-activation outcome for a version so it is not re-read on every
    // activation pass. statusCode is one of N'ENRICHED', N'NO_EVIDENCE', N'FAILED'. ENRICHED/NO_EVIDENCE
    // are terminal (version is skipped thereafter); FAILED leaves the version eligible for retry.
    Task SetVersionSemanticActivationStatusAsync(
        Guid tenantId,
        Guid userId,
        Guid documentVersionId,
        string statusCode,
        CancellationToken cancellationToken = default);

    // evidence items → fact propositions → support edges) for a matter so the Document
    // Intelligence workspace renders real DB-backed content. Idempotent: no-op if documents exist.
    Task<int> GenerateRandomTestCorpusAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LegalMatterContextItem>> SearchMatterContextAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        string query,
        int maximumItems,
        int maximumCharacters,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<LegalMatterContextItem>> SearchLegacyProjectionAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        string query,
        int maximumItems,
        CancellationToken cancellationToken = default);

    Task<Guid> CreateDocumentAsync(
        Guid documentId,
        LegalDocumentIntakeRequest request,
        string sha256Hash,
        string storageReference,
        string malwareStatusCode,
        CancellationToken cancellationToken = default);

    Task<Guid?> FindActiveDocumentByHashAsync(
        Guid tenantId,
        Guid matterId,
        string sha256Hash,
        CancellationToken cancellationToken = default);

    Task PurgeDocumentAsync(
        Guid tenantId,
        Guid userId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task SaveExtractionAsync(
        Guid tenantId,
        Guid userId,
        Guid documentVersionId,
        string correlationId,
        DocumentExtractionResult extraction,
        IReadOnlyCollection<LegalDocumentPassageDto> passages,
        CancellationToken cancellationToken = default);

    Task SaveSemanticProposalAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        Guid documentId,
        Guid documentVersionId,
        LegalDocumentSemanticProposal proposal,
        CancellationToken cancellationToken = default);

    // Persists the domain-specific entities/events extracted for a document version (migration 0373).
    // Idempotent per version: replaces the version's prior extraction rows. Advisory; fail-soft callers.
    Task SaveDocumentDomainExtractionAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        Guid documentId,
        Guid documentVersionId,
        string? domainPackCode,
        IReadOnlyCollection<DocumentDomainEntityPersistence> entities,
        IReadOnlyCollection<DocumentDomainEventPersistence> events,
        CancellationToken cancellationToken = default);

    // Matter-scoped read of extracted domain entities/events (for API/UI provenance surfaces).
    Task<IReadOnlyCollection<DocumentDomainEntityDto>> GetMatterDomainEntitiesAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<DocumentDomainEventDto>> GetMatterDomainEventsAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default);

    Task PersistRetrievalTelemetryAsync(
        Guid tenantId,
        Guid userId,
        DecisionRetrievalTelemetry telemetry,
        CancellationToken cancellationToken = default);

    // ── Enterprise evidence-upload provenance layer (additive; never mutates canonical document rows) ──

    // Opens a new upload batch (one "add evidence" action) and returns its id. BatchNumber is generated.
    Task<Guid> CreateUploadBatchAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        StartUploadBatchCommand command,
        CancellationToken cancellationToken = default);

    // Returns the matter's upload batches (newest first) for the enterprise upload dashboard.
    Task<IReadOnlyCollection<LegalUploadBatchDto>> GetUploadBatchesAsync(
        Guid tenantId,
        Guid matterId,
        CancellationToken cancellationToken = default);

    // Idempotency guard: returns an existing completed upload operation for the same (matter, key) when
    // the request fingerprint matches; throws when the same key is reused for a different file. Returns
    // null when the key has not been seen (a new operation should proceed).
    Task<LegalUploadOperationLookup?> FindUploadOperationAsync(
        Guid tenantId,
        Guid matterId,
        string idempotencyKey,
        string requestFingerprint,
        CancellationToken cancellationToken = default);

    // Records a completed upload operation result (idempotency ledger) and returns its id.
    Task<Guid> RecordUploadOperationAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        Guid? batchId,
        string idempotencyKey,
        string requestFingerprint,
        string fileName,
        string? declaredContentType,
        long expectedLength,
        Guid resultDocumentId,
        Guid resultDocumentVersionId,
        bool contentReused,
        string sha256Hash,
        string? correlationId,
        CancellationToken cancellationToken = default);

    // Records a legal provenance occurrence of a document version within a matter. Physical content is
    // deduplicated; provenance is preserved as a distinct occurrence. Returns the new occurrence id.
    Task<Guid> CreateEvidenceOccurrenceAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        Guid documentId,
        Guid documentVersionId,
        Guid? batchId,
        Guid? uploadOperationId,
        bool contentReused,
        EvidenceSourceDescriptor source,
        CancellationToken cancellationToken = default);

    // Returns the provenance occurrences for a matter (newest first).
    Task<IReadOnlyCollection<LegalEvidenceOccurrenceDto>> GetEvidenceOccurrencesAsync(
        Guid tenantId,
        Guid matterId,
        CancellationToken cancellationToken = default);

    // Increments the rolling report counters on an upload batch as each file is processed.
    Task IncrementUploadBatchCountersAsync(
        Guid tenantId,
        Guid batchId,
        UploadBatchCounterDelta delta,
        CancellationToken cancellationToken = default);

    // Sets the number of files a batch expects to process (declared at open time) so the dashboard can
    // show discovered-vs-accepted progress. Tenant-scoped and idempotent.
    Task SetUploadBatchDiscoveredAsync(
        Guid tenantId,
        Guid batchId,
        int filesDiscovered,
        CancellationToken cancellationToken = default);

    // Transitions a batch to a terminal status (default CLOSED) once its files are processed. Idempotent
    // and tenant-scoped; closing never discards counters recorded by in-flight files.
    Task CloseUploadBatchAsync(
        Guid tenantId,
        Guid userId,
        Guid batchId,
        string statusCode,
        CancellationToken cancellationToken = default);

    // ── Deterministic processing-operation ledger (wraps Legal_DocumentProcessingRun; never replaces it) ──

    // Reuse-or-record: given a deterministic operation identity, returns an existing completed operation
    // (RETRY reuse) when present, otherwise records a new PENDING operation and returns it as a miss.
    // Fail-soft callers treat any exception as "run the effect" so the ledger never blocks processing.
    Task<LegalProcessingOperationLookup> AcquireProcessingOperationAsync(
        Guid tenantId,
        Guid userId,
        LegalProcessingOperationRequest request,
        CancellationToken cancellationToken = default);

    // Marks a processing operation COMPLETE with its result identity/hash so a later identical request reuses it.
    Task CompleteProcessingOperationAsync(
        Guid tenantId,
        Guid userId,
        Guid processingOperationId,
        string? resultEntityTypeCode,
        Guid? resultEntityId,
        string? resultHash,
        CancellationToken cancellationToken = default);

    // Marks a processing operation FAILED with an error code/message; a retry may re-run the effect.
    Task FailProcessingOperationAsync(
        Guid tenantId,
        Guid userId,
        Guid processingOperationId,
        string errorCode,
        string errorMessage,
        CancellationToken cancellationToken = default);

    // ── Document family grouping (email + attachments, container + children) ──

    // Creates a container family for a matter (e.g. an email or archive) and returns its id.
    Task<Guid> CreateDocumentFamilyAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        string? familyLabel,
        string containerTypeCode,
        CancellationToken cancellationToken = default);

    // Attaches an occurrence to a family with its container position (depth/ordinal) and optional parent.
    Task LinkOccurrenceToFamilyAsync(
        Guid tenantId,
        Guid userId,
        Guid occurrenceId,
        Guid familyId,
        Guid? parentOccurrenceId,
        int familyDepth,
        int familyOrdinal,
        CancellationToken cancellationToken = default);

    // ── Evidence lineage (independent-source identity; advisory, resolved after physical dedup) ──

    // Creates a lineage group representing one underlying independent source and returns its id.
    Task<Guid> CreateEvidenceLineageGroupAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        string? lineageLabel,
        string? originDescription,
        string independenceBasisCode,
        CancellationToken cancellationToken = default);

    // Adds an occurrence to a lineage group as ORIGINAL (primary source) or DERIVATIVE (quotes/restates
    // it, adding no independent weight). Idempotent per (group, occurrence).
    Task<Guid> AddEvidenceLineageMemberAsync(
        Guid tenantId,
        Guid userId,
        Guid lineageGroupId,
        Guid occurrenceId,
        string roleCode,
        string? derivationNote,
        CancellationToken cancellationToken = default);

    // Returns the lineage groups (with members) for a matter so POLOXI/UI can weigh independent support.
    Task<IReadOnlyCollection<LegalEvidenceLineageGroupDto>> GetEvidenceLineageAsync(
        Guid tenantId,
        Guid matterId,
        CancellationToken cancellationToken = default);
}
