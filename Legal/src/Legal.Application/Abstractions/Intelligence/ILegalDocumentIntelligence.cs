using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Abstractions.Intelligence;

public interface IDocumentExtractionProvider
{
    Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILegalDocumentSearchProjectionDispatcher
{
    Task<int> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken = default);
}

// Phase 2 Continuous Decision Integrity: claims material PROCESSED change events and runs the
// authoritative reevaluation (DecisionReevaluationService) automatically. Driven by a hosted worker.
public interface IDecisionReevaluationDispatcher
{
    Task<int> ProcessBatchAsync(int batchSize, CancellationToken cancellationToken = default);
}

public interface ILegalDocumentExtractionRouter
{
    Task<DocumentExtractionResult> ExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILegalDocumentIntakeValidator
{
    Task ValidateAsync(
        LegalDocumentIntakeRequest request,
        Stream content,
        CancellationToken cancellationToken = default);
}

public interface ILegalDocumentBinaryStore
{
    Task<string> StoreImmutableAsync(
        Guid tenantId,
        Guid documentId,
        string fileName,
        Stream content,
        CancellationToken cancellationToken = default);
}

public interface ILegalDocumentSecurityScanner
{
    Task<LegalDocumentSecurityScanResult> ScanAsync(
        string fileName,
        string contentType,
        Stream content,
        CancellationToken cancellationToken = default);
}

public interface INativeDocumentTextProvider
{
    Task<DocumentExtractionResult?> TryExtractAsync(
        DocumentExtractionRequest request,
        CancellationToken cancellationToken = default);
}

public interface ILegalDocumentIntakeService
{
    Task<LegalDocumentDto> IngestAsync(
        LegalDocumentIntakeRequest request,
        Stream content,
        CancellationToken cancellationToken = default);
}

// Activates prepared corpus documents on the Disambiguate & Answer path: runs Stage 1 semantic
// enrichment (atomic propositions) and Continuous Decision Integrity for versions that were uploaded
// prepare-only. Processes at most one batch per call so the UI can drive observable progress.
public interface ILegalMatterCorpusActivationService
{
    Task<LegalMatterCorpusActivationStatus> GetStatusAsync(
        Guid tenantId,
        Guid matterId,
        CancellationToken cancellationToken = default);

    Task<LegalMatterCorpusActivationStatus> ActivateAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        string? modelCode,
        int batchSize,
        CancellationToken cancellationToken = default);
}

public interface ILegalDocumentSemanticInterpreter
{
    Task<LegalDocumentSemanticProposal> InterpretAsync(
        Guid tenantId,
        Guid matterId,
        Guid documentId,
        Guid documentVersionId,
        string? domainPackCode,
        IReadOnlyCollection<DecisionDomainConceptDto> domainConcepts,
        IReadOnlyCollection<LegalDocumentPassageDto> passages,
        string correlationId,
        string? modelCodeOverride = null,
        CancellationToken cancellationToken = default);
}

public interface ILegalMatterContextRetriever
{
    Task<LegalMatterContextResult> RetrieveAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        string query,
        DecisionRetrievalArchitectureSettings settings,
        CancellationToken cancellationToken = default);
}

public interface IDecisionResearchSourceRouter
{
    DecisionResearchRoute Route(
        DecisionResearchNeedPersistence researchNeed,
        string? jurisdiction,
        DateTime? authorityCutoffDate = null);
}
