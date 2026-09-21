using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Decision.Core;

public sealed class StructuredLlmSemanticEvidenceVerifier(
    ILegalDecisionRepository repository,
    ILegalDecisionAiProvider aiProvider,
    ISemanticVerificationCache cache) : ISemanticEvidenceVerifier
{
    public const string PromptCode = "EVIDENCE_SEMANTIC_VERIFY";
    private const string VerifierVersion = "STRUCTURED_SEMANTIC_LLM_V1";
    private const int MaxSourceCharacters = 10_000;
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<SemanticVerificationResult> VerifyAsync(
        EvidenceVerificationRequest request,
        VerificationProfile profile,
        VerificationCheckResult passage,
        CancellationToken cancellationToken = default)
    {
        if (passage.State != VerificationCheckState.Passed || string.IsNullOrWhiteSpace(passage.SupportingPassage))
            return NotEvaluated(request.Proposition, "SEMANTIC_PASSAGE_UNAVAILABLE");

        var settings = (await repository.GetCoreSettingsAsync(cancellationToken)).Verification;
        if (!settings.Enabled || !settings.SemanticVerificationEnabled)
            return Unverifiable(request.Proposition, "SEMANTIC_VERIFICATION_DISABLED");

        var prompt = await repository.GetPromptAsync(PromptCode, cancellationToken)
            ?? throw new InvalidOperationException($"The '{PromptCode}' decision prompt is not configured.");
        var routes = await repository.GetModelRoutesAsync(cancellationToken);
        var route = routes.OrderBy(r => r.Priority).FirstOrDefault()
            ?? throw new InvalidOperationException("No active Legal Decision AI route is configured.");
        route = route with { MaxOutputTokens = Math.Min(route.MaxOutputTokens, settings.MaxOutputTokensPerEvidence), Temperature = 0m };

        var sourceText = passage.SupportingPassage.Length <= MaxSourceCharacters
            ? passage.SupportingPassage
            : passage.SupportingPassage[..MaxSourceCharacters];
        var estimatedInputTokens = (prompt.SystemPrompt.Length + prompt.UserPromptTemplate.Length
            + request.Proposition.Length + sourceText.Length + 3) / 4;
        if (estimatedInputTokens > settings.MaxInputTokensPerEvidence)
            return Unverifiable(request.Proposition, VerificationFailureCodes.VerificationBudgetExhausted);
        var cacheKey = CreateCacheKey(sourceText, request, profile, prompt.OutputSchemaJson);
        if (settings.CacheEnabled && cache.TryGet(cacheKey, out var cached))
            return cached with { CacheHit = true, CallCount = 0, InputTokenCount = 0, OutputTokenCount = 0, Duration = TimeSpan.Zero };
        var payload = JsonSerializer.Serialize(new
        {
            verificationContract = new
            {
                allowedSupportStates = new[] { "SUPPORTED", "PARTIALLY_SUPPORTED", "UNSUPPORTED", "CONTRADICTED", "UNVERIFIABLE" },
                sourceType = EvidenceSourceTypeCodes.ToCode(request.DeclaredSourceType ?? EvidenceSourceType.Unknown),
                requireStatementRole = profile.RequireStatementRole,
                requireHolding = profile.RequireHolding,
            },
            targetProposition = request.Proposition,
            source = new
            {
                untrustedData = true,
                sourceRef = request.SourceRef,
                sourceTitle = request.SourceTitle,
                passage = sourceText,
            },
        });
        var userPrompt = prompt.UserPromptTemplate.Replace("{{ARTIFACT}}", payload, StringComparison.Ordinal);
        var result = await aiProvider.GenerateAsync(new DecisionAiRequest(
            route, PromptCode, prompt.SystemPrompt, userPrompt, prompt.OutputSchemaJson,
            request.CorrelationId ?? request.DecisionEvidenceId.ToString("N")), cancellationToken);

        var proposal = Parse(result);
        var totalInputTokens = result.InputTokenCount;
        var totalOutputTokens = result.OutputTokenCount;
        var totalDuration = result.Duration;
        var callCount = 1;
        if (proposal is null && settings.AllowSchemaRepair && settings.MaxSchemaRepairAttempts > 0)
        {
            var repairPrompt = $"The previous response was invalid. Return only JSON matching the supplied schema. Do not add commentary.\n\n{userPrompt}";
            var repaired = await aiProvider.GenerateAsync(new DecisionAiRequest(
                route, PromptCode, prompt.SystemPrompt, repairPrompt, prompt.OutputSchemaJson,
                $"{request.CorrelationId ?? request.DecisionEvidenceId.ToString("N")}:repair"), cancellationToken);
            proposal = Parse(repaired);
            totalInputTokens += repaired.InputTokenCount;
            totalOutputTokens += repaired.OutputTokenCount;
            totalDuration += repaired.Duration;
            callCount++;
        }
        if (proposal is null || !TrySupportState(proposal.PropositionSupport?.State, out var supportState))
            return Invalid(request.Proposition, totalInputTokens, totalOutputTokens, totalDuration, callCount, "SEMANTIC_OUTPUT_INVALID");

        var returnedPassage = proposal.PropositionSupport?.SupportingPassage;
        if (!string.IsNullOrWhiteSpace(returnedPassage)
            && !Normalize(sourceText).Contains(Normalize(returnedPassage), StringComparison.Ordinal))
            return Invalid(request.Proposition, totalInputTokens, totalOutputTokens, totalDuration, callCount, "SEMANTIC_PASSAGE_NOT_GROUNDED");

        var roleKnown = Enum.TryParse<LegalStatementRole>(proposal.StatementRole?.State, true, out var role)
            && role != LegalStatementRole.Unknown;
        var holdingState = ParseCheckState(proposal.Holding?.State);
        var method = VerifierVersion;
        var semantic = new SemanticVerificationResult
        {
            PropositionSupport = new PropositionSupportResult
            {
                State = supportState,
                Proposition = request.Proposition,
                SupportingPassage = returnedPassage,
                SupportedComponents = proposal.PropositionSupport?.SupportedComponents ?? [],
                UnsupportedComponents = proposal.PropositionSupport?.UnsupportedComponents ?? [],
                ContradictedComponents = proposal.PropositionSupport?.ContradictedComponents ?? [],
                ReasonCode = proposal.PropositionSupport?.ReasonCode ?? "SEMANTIC_REASON_NOT_PROVIDED",
                Reason = proposal.PropositionSupport?.Explanation,
                VerificationMethod = method,
                VerifierId = PromptCode,
                VerifierVersion = VerifierVersion,
            },
            StatementRole = new VerificationCheckResult
            {
                State = roleKnown ? VerificationCheckState.Passed : VerificationCheckState.Inconclusive,
                ReasonCode = proposal.StatementRole?.ReasonCode ?? (roleKnown ? "STATEMENT_ROLE_CLASSIFIED" : "STATEMENT_ROLE_UNKNOWN"),
                Reason = proposal.StatementRole?.Explanation,
                VerifiedValue = roleKnown ? role.ToString() : null,
                SourceRef = request.SourceRef,
                SupportingPassage = returnedPassage,
                VerificationMethod = method,
                VerifierId = PromptCode,
                VerifierVersion = VerifierVersion,
            },
            Holding = new VerificationCheckResult
            {
                State = holdingState,
                ReasonCode = proposal.Holding?.ReasonCode ?? "HOLDING_RESULT_NOT_PROVIDED",
                Reason = proposal.Holding?.Explanation,
                VerifiedValue = proposal.Holding?.State,
                SourceRef = request.SourceRef,
                SupportingPassage = returnedPassage,
                VerificationMethod = method,
                VerifierId = PromptCode,
                VerifierVersion = VerifierVersion,
            },
            Ambiguous = proposal.Ambiguous || supportState is PropositionSupportState.PartiallySupported or PropositionSupportState.Unverifiable,
            InputTokenCount = totalInputTokens,
            OutputTokenCount = totalOutputTokens,
            Duration = totalDuration,
            CallCount = callCount,
        };
        if (settings.CacheEnabled)
            cache.Store(cacheKey, semantic);
        return semantic;
    }

    private static SemanticVerificationResult Invalid(
        string proposition, int inputTokens, int outputTokens, TimeSpan duration, int callCount, string reasonCode) => new()
    {
        PropositionSupport = new PropositionSupportResult { State = PropositionSupportState.Error, Proposition = proposition, ReasonCode = reasonCode, VerificationMethod = "STRUCTURED_SEMANTIC_LLM_V1" },
        StatementRole = new VerificationCheckResult { State = VerificationCheckState.Error, ReasonCode = reasonCode, VerificationMethod = "STRUCTURED_SEMANTIC_LLM_V1" },
        Holding = new VerificationCheckResult { State = VerificationCheckState.Error, ReasonCode = reasonCode, VerificationMethod = "STRUCTURED_SEMANTIC_LLM_V1" },
        InputTokenCount = inputTokens,
        OutputTokenCount = outputTokens,
        Duration = duration,
        CallCount = callCount,
    };

    private static SemanticVerificationResult NotEvaluated(string proposition, string reasonCode) => new()
    {
        PropositionSupport = PropositionSupportResult.NotEvaluated(proposition, reasonCode, "Semantic verification requires a grounded passage."),
        StatementRole = VerificationCheckResult.NotEvaluated(reasonCode, "Semantic verification requires a grounded passage."),
        Holding = VerificationCheckResult.NotEvaluated(reasonCode, "Semantic verification requires a grounded passage."),
    };

    private static SemanticVerificationResult Unverifiable(string proposition, string reasonCode) => new()
    {
        PropositionSupport = new PropositionSupportResult
        {
            State = PropositionSupportState.Unverifiable,
            Proposition = proposition,
            ReasonCode = reasonCode,
            Reason = "The bounded semantic verifier could not run under the active verification policy.",
            VerificationMethod = "VERIFICATION_POLICY",
        },
        StatementRole = VerificationCheckResult.NotEvaluated(reasonCode, "Semantic verification did not run."),
        Holding = VerificationCheckResult.NotEvaluated(reasonCode, "Semantic verification did not run."),
    };

    private static bool TrySupportState(string? value, out PropositionSupportState state) =>
        Enum.TryParse(value?.Replace("_", string.Empty), true, out state)
        && state is PropositionSupportState.Supported or PropositionSupportState.PartiallySupported
            or PropositionSupportState.Unsupported or PropositionSupportState.Contradicted or PropositionSupportState.Unverifiable;

    private static VerificationCheckState ParseCheckState(string? value) => value?.ToUpperInvariant() switch
    {
        "PASS" or "PASSED" => VerificationCheckState.Passed,
        "FAIL" or "FAILED" => VerificationCheckState.Failed,
        "INCONCLUSIVE" => VerificationCheckState.Inconclusive,
        "NOT_APPLICABLE" or "N/A" => VerificationCheckState.NotApplicable,
        _ => VerificationCheckState.Error,
    };

    private static string Normalize(string value) =>
        string.Join(' ', value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();

    private static SemanticProposal? Parse(DecisionAiResult result)
    {
        var json = string.IsNullOrWhiteSpace(result.StructuredOutputJson) ? result.Content : result.StructuredOutputJson;
        try { return JsonSerializer.Deserialize<SemanticProposal>(json, JsonOptions); }
        catch (JsonException) { return null; }
    }

    private static string CreateCacheKey(
        string sourceText,
        EvidenceVerificationRequest request,
        VerificationProfile profile,
        string? schema)
    {
        var value = $"{VerifierVersion}\n{profile.ProfileCode}\n{profile.Version}\n{schema}\n{request.SourceProvider}\n{request.SourceVersion}\n{request.ExtractionVersion}\n{Normalize(request.Proposition)}\n{Normalize(sourceText)}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private sealed record SemanticProposal
    {
        public SemanticSupportProposal? PropositionSupport { get; init; }
        public SemanticFactorProposal? StatementRole { get; init; }
        public SemanticFactorProposal? Holding { get; init; }
        public bool Ambiguous { get; init; }
    }

    private sealed record SemanticSupportProposal
    {
        public string? State { get; init; }
        public string? SupportingPassage { get; init; }
        public IReadOnlyList<string> SupportedComponents { get; init; } = [];
        public IReadOnlyList<string> UnsupportedComponents { get; init; } = [];
        public IReadOnlyList<string> ContradictedComponents { get; init; } = [];
        public string? ReasonCode { get; init; }
        public string? Explanation { get; init; }
    }

    private sealed record SemanticFactorProposal
    {
        public string? State { get; init; }
        public string? ReasonCode { get; init; }
        public string? Explanation { get; init; }
    }
}
