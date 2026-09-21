using System.Diagnostics;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Infrastructure.Intelligence;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// Self-contained AI provider for the POLOXI Legal Decision module. It owns the decision module's
// route→context mapping and delegates the raw Azure OpenAI transport to the shared IAiProvider so
// there is a single, well-tested HTTP/auth path. The decision module keeps its own abstraction
// (ILegalDecisionAiProvider) and its own DB-backed routes/prompts, isolated from /legal/search.
// ─────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LegalDecisionAiProvider(IAiProvider transport) : ILegalDecisionAiProvider
{
    public async Task<DecisionAiResult> GenerateAsync(DecisionAiRequest request, CancellationToken cancellationToken = default)
    {
        var route = request.Route;
        var context = new AiProviderContext(
            TenantId: Guid.Empty,
            ProviderCode: "LEGAL_DECISION",
            ProviderTypeCode: route.ProviderTypeCode,
            ModelCode: route.ModelCode,
            DeploymentName: route.DeploymentName,
            EndpointReference: route.EndpointReference,
            CredentialReference: route.CredentialReference,
            ApiVersion: route.ApiVersion,
            TimeoutSeconds: route.TimeoutSeconds);

        var timer = Stopwatch.StartNew();
        var result = await transport.GenerateAsync(
            new AiGenerationRequest(
                context,
                request.FeatureCode,
                request.SystemPrompt,
                request.UserPrompt,
                request.OutputSchemaJson,
                route.Temperature,
                route.MaxOutputTokens,
                request.CorrelationId),
            cancellationToken);
        timer.Stop();

        return new DecisionAiResult(
            result.Content,
            result.StructuredOutputJson,
            result.InputTokenCount,
            result.OutputTokenCount,
            result.ProviderRequestId,
            result.Duration == default ? timer.Elapsed : result.Duration);
    }
}

// Decision-directed retriever (§13). Reuses the authoritative legal sources through ILegalRetriever
// when the LEGAL context is selected; fail-soft (returns empty) so a retrieval error never breaks
// the decision pipeline. GENERAL context returns no external evidence (decision runs on LLM proposal).
public sealed class LegalDecisionRetriever(ILegalRetriever legalRetriever, IIntelligenceWideRepository wideRepository) : ILegalDecisionRetriever
{
    public async Task<IReadOnlyCollection<DecisionRetrievedSource>> RetrieveAsync(DecisionRetrievalRequest request, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(request.ContextCode, DecisionContexts.Legal, StringComparison.OrdinalIgnoreCase))
            return [];

        try
        {
            var configuration = await wideRepository.GetLegalGroundingConfigurationAsync(Guid.Empty, cancellationToken);
            var snippets = await legalRetriever.SearchAsync(request.Objective, configuration, LegalAuthorityKind.Any, cancellationToken);
            return snippets
                .Take(Math.Max(1, request.MaximumResults))
                .Select(s => new DecisionRetrievedSource(s.Url, s.Title, s.Snippet)
                {
                    SourceType = s.AuthorityKind?.ToUpperInvariant() switch
                    {
                        "CASE" or "CASE_LAW" => Legal.Application.Features.Intelligence.Decision.Core.EvidenceSourceType.CaseLaw,
                        "STATUTE" => Legal.Application.Features.Intelligence.Decision.Core.EvidenceSourceType.Statute,
                        "REGULATION" => Legal.Application.Features.Intelligence.Decision.Core.EvidenceSourceType.Regulation,
                        _ => null,
                    },
                    SourceProvider = s.SourceProvider,
                    SourceVersion = s.SourceVersion,
                    ProviderIdentityVerified = s.ProviderIdentityVerified,
                })
                .ToArray();
        }
        catch
        {
            return [];
        }
    }
}
