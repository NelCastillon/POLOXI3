using System.Text;
using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.Science;
using Microsoft.Extensions.Logging;

namespace Legal.Application;

// ── POLOXI Formalization Gate — orchestrator ──────────────────────────────────────────────────────────
// Runs the single MATH_FORMALIZATION_GATE stage: resolves the system prompt from the user-managed
// registry (IPromptCatalog) and calls the governed AI router with the ProofContract output schema, then
// deserializes the tolerant DTO. This implements the Research → Formalize → Math step: it converts a
// surviving research idea/hypothesis into a clean Proof Contract (Assumptions ⇒ Claim) the Math Solver
// can attack directly.
//
// CORE INVARIANT: the LLM only PROPOSES the contract; nothing here is proven. When the model route is
// unavailable or returns unparseable content, the gate degrades gracefully to an echo contract so the
// caller still gets an honest, non-throwing result.
public sealed class FormalizationService(
    IAiProviderRouter aiProviderRouter,
    IPromptCatalog promptCatalog,
    ILogger<FormalizationService> logger) : IFormalizationService
{
    // Reuse the Math pack's feature policy/route — the gate is part of the Mathematics domain pack.
    private const string FeatureCode = "INTELLIGENCE_MATH_SOLVE";
    private const string ModuleCode = "Intelligence";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<FormalizationResponse> FormalizeAsync(FormalizationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.ResearchIdea))
            throw new ArgumentException("A research idea to formalize is required.", nameof(request));
        if (request.UserId == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated user is required for formalization.");

        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId;

        var modelAvailable = true;
        ProofContract? proposal = null;
        try
        {
            var systemPrompt = await promptCatalog.GetSystemPromptAsync(request.TenantId, IntelligencePromptCodes.MathFormalizationGate, cancellationToken);
            var userPrompt = BuildUserPrompt(request);
            var executionContext = new AiExecutionContext(ModuleCode, null, null, request.ResearchIdea, "MATH_FORMALIZATION", null, correlationId, IntelligencePromptCodes.MathFormalizationGate);
            var result = await aiProviderRouter.GenerateAsync(
                request.TenantId, FeatureCode, systemPrompt, userPrompt, FormalizationContractSchemas.ProofContractSchema, correlationId,
                executionContext, string.IsNullOrWhiteSpace(request.ModelCode) ? null : request.ModelCode.Trim(), cancellationToken);

            var json = string.IsNullOrWhiteSpace(result.StructuredOutputJson) ? result.Content : result.StructuredOutputJson;
            if (!string.IsNullOrWhiteSpace(json))
                proposal = JsonSerializer.Deserialize<ProofContract>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is AiProviderUnavailableException or JsonException)
        {
            if (ex is AiProviderUnavailableException)
                modelAvailable = false;
            logger.LogWarning(ex, "Formalization gate did not produce a usable result; degrading gracefully.");
        }

        var contract = Normalize(proposal, request.ResearchIdea);

        return new FormalizationResponse
        {
            Contract = contract,
            MathSolverHandoff = BuildMathSolverHandoff(contract),
            ModelAvailable = modelAvailable,
            CorrelationId = correlationId,
        };
    }

    private static string BuildUserPrompt(FormalizationRequest request)
    {
        var builder = new StringBuilder();
        builder.Append("Research idea / surviving proposition to formalize:\n").Append(request.ResearchIdea.Trim());
        if (!string.IsNullOrWhiteSpace(request.ResearchContext))
            builder.Append("\n\nAdditional research context (evidence / prior art / candidate comparison):\n").Append(request.ResearchContext.Trim());
        return builder.ToString();
    }

    // Fills a defensible echo contract when the model is unavailable or returned nothing, so the gate is
    // always honest and never throws. The echo is clearly marked CONJECTURE with zero confidence.
    private static ProofContract Normalize(ProofContract? proposal, string researchIdea)
    {
        if (proposal is null || string.IsNullOrWhiteSpace(proposal.Statement))
        {
            return new ProofContract
            {
                CandidateId = "UNFORMALIZED",
                ObjectType = "CONJECTURE",
                Statement = researchIdea.Trim(),
                Target = "Formalize this research idea into a precise mathematical claim.",
                ExpectedReturns = ["INCONCLUSIVE"],
                Confidence = 0d,
            };
        }

        return proposal with
        {
            CandidateId = string.IsNullOrWhiteSpace(proposal.CandidateId) ? "CANDIDATE_1" : proposal.CandidateId.Trim(),
            ObjectType = string.IsNullOrWhiteSpace(proposal.ObjectType) ? "CONJECTURE" : proposal.ObjectType.Trim(),
            ExpectedReturns = proposal.ExpectedReturns.Count > 0
                ? proposal.ExpectedReturns
                : ["PROVED", "DISPROVED", "COUNTEREXAMPLE", "REDUCED_TO_LEMMAS", "INCONCLUSIVE"],
            Confidence = Math.Clamp(proposal.Confidence, 0d, 1d),
        };
    }

    // Composes a single, self-contained problem statement for the Math Solver from the Proof Contract —
    // the precise handoff object, not the raw research narrative.
    private static string BuildMathSolverHandoff(ProofContract contract)
    {
        var builder = new StringBuilder();
        builder.Append("Prove or disprove the following exact statement under the stated assumptions.\n\n");
        builder.Append("CLAIM: ").Append(contract.Statement).Append('\n');

        if (contract.Assumptions.Count > 0)
        {
            builder.Append("\nASSUMPTIONS:\n");
            foreach (var assumption in contract.Assumptions)
                builder.Append("- ").Append(assumption).Append('\n');
        }

        if (contract.Definitions.Count > 0)
        {
            builder.Append("\nDEFINITIONS:\n");
            foreach (var definition in contract.Definitions)
                builder.Append("- ").Append(definition).Append('\n');
        }

        if (contract.Dependencies.Count > 0)
        {
            builder.Append("\nDEPENDENCIES (may be used):\n");
            foreach (var dependency in contract.Dependencies)
                builder.Append("- ").Append(dependency).Append('\n');
        }

        if (contract.AllowedTools.Count > 0)
            builder.Append("\nALLOWED TOOLS: ").Append(string.Join(", ", contract.AllowedTools)).Append('\n');

        if (contract.ForbiddenAssumptions.Count > 0)
        {
            builder.Append("\nFORBIDDEN (circular) ASSUMPTIONS:\n");
            foreach (var forbidden in contract.ForbiddenAssumptions)
                builder.Append("- ").Append(forbidden).Append('\n');
        }

        if (!string.IsNullOrWhiteSpace(contract.ProofStandard))
            builder.Append("\nWHAT COUNTS AS PROOF: ").Append(contract.ProofStandard).Append('\n');

        if (!string.IsNullOrWhiteSpace(contract.Falsification))
            builder.Append("\nWHAT WOULD FALSIFY IT: ").Append(contract.Falsification).Append('\n');

        return builder.ToString().TrimEnd();
    }
}
