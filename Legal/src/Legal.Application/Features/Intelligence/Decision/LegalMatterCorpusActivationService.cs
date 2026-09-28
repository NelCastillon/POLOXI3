using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace Legal.Application.Features.Intelligence.Decision;

// Activates prepared corpus documents on the Disambiguate & Answer path. Upload persists documents in a
// prepare-only state (extracted text, no derived propositions). This service enriches the pending
// versions (Stage 1 semantic interpretation) and runs Continuous Decision Integrity for each, one
// bounded batch per call so the UI can poll and render observable progress.
public sealed class LegalMatterCorpusActivationService(
    ILegalDocumentCorpusRepository corpusRepository,
    ILegalDocumentSemanticInterpreter semanticInterpreter,
    IMatterChangeProcessor matterChangeProcessor,
    ILogger<LegalMatterCorpusActivationService> logger) : ILegalMatterCorpusActivationService
{
    public Task<LegalMatterCorpusActivationStatus> GetStatusAsync(Guid tenantId, Guid matterId, CancellationToken cancellationToken = default)
        => corpusRepository.GetMatterActivationStatusAsync(tenantId, matterId, cancellationToken);

    public async Task<LegalMatterCorpusActivationStatus> ActivateAsync(Guid tenantId, Guid userId, Guid matterId, string? modelCode, int batchSize, CancellationToken cancellationToken = default)
    {
        var settings = await corpusRepository.GetRetrievalArchitectureSettingsAsync(cancellationToken);
        var pending = await corpusRepository.GetPendingCorpusVersionsAsync(
            tenantId, matterId, Math.Clamp(batchSize, 1, 25), cancellationToken);

        var activated = 0;
        var correlationId = Guid.NewGuid().ToString("N");
        foreach (var version in pending)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (settings.Stage1SemanticEnrichmentEnabled)
                {
                    var passages = await corpusRepository.GetDocumentPassagesAsync(
                        tenantId, version.LegalDocumentVersionId, cancellationToken);
                    if (passages.Count > 0)
                    {
                        var proposal = await semanticInterpreter.InterpretAsync(
                            tenantId, matterId, version.LegalDocumentId, version.LegalDocumentVersionId,
                            null, [], passages, correlationId, modelCode, cancellationToken);
                        await corpusRepository.SaveSemanticProposalAsync(
                            tenantId, userId, matterId, version.LegalDocumentId,
                            version.LegalDocumentVersionId, proposal, cancellationToken);
                    }
                }

                // Continuous Decision Integrity — evaluate the newly activated document for material
                // impact on existing conclusions. Idempotent on the source version.
                await matterChangeProcessor.ProcessDocumentChangeAsync(
                    tenantId, userId, matterId, version.LegalDocumentId, version.LegalDocumentVersionId,
                    version.Sha256Hash, version.FileName, DateTime.UtcNow, cancellationToken);

                activated++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Corpus activation failed for document version {VersionId}; will retry on next activation pass.", version.LegalDocumentVersionId);
            }
        }

        var status = await corpusRepository.GetMatterActivationStatusAsync(tenantId, matterId, cancellationToken);
        return status with { ActivatedThisCall = activated };
    }
}
