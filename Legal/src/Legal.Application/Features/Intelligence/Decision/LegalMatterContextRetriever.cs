using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;

namespace Legal.Application.Features.Intelligence.Decision;

public sealed class LegalMatterContextRetriever(ILegalDocumentCorpusRepository corpusRepository) : ILegalMatterContextRetriever
{
    public async Task<LegalMatterContextResult> RetrieveAsync(
        Guid tenantId,
        Guid userId,
        Guid matterId,
        string query,
        DecisionRetrievalArchitectureSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!settings.Stage2MatterContextEnabled)
            return new(false, DecisionResearchRouteCodes.NoneDerived, [], 0, 0, "STAGE_DISABLED");

        var authoritative = await corpusRepository.SearchMatterContextAsync(
            tenantId, userId, matterId, query, settings.Stage2MaximumItems,
            settings.Stage2MaximumCharacters, cancellationToken);
        if (authoritative.Count > 0)
            return new(true, DecisionResearchRouteCodes.MatterCorpus, authoritative, authoritative.Count, 0);

        if (!settings.Stage2LegacyProjectionFallbackEnabled)
            return new(true, DecisionResearchRouteCodes.MatterCorpus, [], 0, 0, "NO_AUTHORIZED_MATTER_CONTEXT");

        var legacy = await corpusRepository.SearchLegacyProjectionAsync(
            tenantId, userId, matterId, query, settings.Stage2MaximumItems, cancellationToken);
        return new(true, DecisionResearchRouteCodes.LegacyProjection, legacy, legacy.Count, 0,
            legacy.Count == 0 ? "NO_AUTHORIZED_MATTER_CONTEXT" : null);
    }
}
