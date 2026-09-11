using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.Science;
using Microsoft.Extensions.Logging;

namespace Legal.Application;

// ── POLOXI Math V1 — reasoning orchestrator (runnable slice) ────────────────────────────────────────
// Linear 8-stage discovery pipeline for the Mathematics domain pack. Each stage resolves its system
// prompt from the user-managed registry (IPromptCatalog, base MATH_* codes) and calls the governed AI
// router with the matching MathContractSchemas output schema, then deserializes the tolerant proposal
// DTO and maps it into domain records via MathContractMapper.
//
// CORE INVARIANT: the LLM only PROPOSES. Answer ACCEPTANCE is owned by deterministic C# — obligations
// are decided by DeterministicMathVerifier, candidates ranked by VerificationWeightedCompetition, and
// the final outcome classified by ScientificConvergencePolicy. Discovery confidence NEVER promotes an
// unverified claim to PROVEN. A stage that fails or returns nothing degrades gracefully (empty state),
// so the pipeline still yields an honest UNRESOLVED/INSUFFICIENT_FORMALIZATION rather than throwing.
public sealed class MathReasoningService(
    IAiProviderRouter aiProviderRouter,
    IPromptCatalog promptCatalog,
    ILogger<MathReasoningService> logger,
    IMathReasoningRepository? repository = null) : IMathReasoningService
{
    private const string FeatureCode = "INTELLIGENCE_MATH_SOLVE";
    private const string ModuleCode = "Intelligence";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private static readonly IMathVerifier Verifier = new DeterministicMathVerifier();
    private static readonly VerificationWeightedCompetition Competition = new();
    private static readonly ScientificConvergencePolicy ConvergencePolicy = new();
    private static readonly ClaimCertaintyCeiling CertaintyCeiling = new();
    private static readonly PriorArtGate PriorArt = new();

    // Seed of well-known results used for the novelty/prior-art gate. Deterministic + conservative: only a
    // small curated set is matched so genuine discoveries are not falsely flagged. Extendable via config.
    private static readonly IReadOnlyCollection<string> KnownPriorArt = [];

    // Set to true when any stage fails because no AI model route is configured for this tenant/feature.
    // Scoped lifetime makes this safe: a fresh instance is created per request.
    private bool _modelRouteUnavailable;

    public async Task<MathSolveResponse> SolveAsync(MathSolveRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.Problem))
            throw new ArgumentException("A problem statement is required.", nameof(request));
        if (request.UserId == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated user is required for math reasoning.");

        var correlationId = string.IsNullOrWhiteSpace(request.CorrelationId) ? Guid.NewGuid().ToString("N") : request.CorrelationId;

        _modelRouteUnavailable = false;

        var startedAt = System.Diagnostics.Stopwatch.GetTimestamp();

        // Stage 1 — problem contract.
        var contract = await RunStageAsync<MathProblemContractProposal>(
            request, IntelligencePromptCodes.MathProblemContract, MathContractSchemas.ProblemContractSchema,
            $"Problem:\n{request.Problem}", correlationId, cancellationToken) ?? new MathProblemContractProposal { Statement = request.Problem };
        var domainContract = MathContractMapper.ToContract(contract, request.Problem);

        // Stage 2 — competing strategies (incl. at least one refutation).
        var strategy = await RunStageAsync<MathStrategyProposal>(
            request, IntelligencePromptCodes.MathStrategyProposal, MathContractSchemas.StrategyProposalSchema,
            BuildContractContext(request, contract), correlationId, cancellationToken) ?? new MathStrategyProposal();
        var candidates = MathContractMapper.ToCandidates(strategy);

        // Stage 3 — proof dependency graph + obligations + a proposed canonical answer.
        var derivation = await RunStageAsync<MathSolutionDerivationProposal>(
            request, IntelligencePromptCodes.MathSolutionDerivation, MathContractSchemas.SolutionDerivationSchema,
            BuildStrategyContext(request, contract, strategy), correlationId, cancellationToken) ?? new MathSolutionDerivationProposal();
        var nodes = MathContractMapper.ToNodes(derivation);
        var edges = MathContractMapper.ToEdges(derivation);
        var obligations = MathContractMapper.ToObligations(derivation);
        var claims = MathContractMapper.ToClaims(derivation);

        // Stage 4 — step verification (LLM prepares; deterministic C# decides).
        _ = await RunStageAsync<MathStepVerificationProposal>(
            request, IntelligencePromptCodes.MathStepVerification, MathContractSchemas.StepVerificationSchema,
            BuildDerivationContext(request, derivation), correlationId, cancellationToken);

        // Deterministic verification: the ONLY authority that can accept/refute an obligation.
        var verifiedObligations = VerifyObligations(obligations, derivation);

        // Stage 5 — counterexample search (refutation candidates with machine-checkable witnesses).
        _ = await RunStageAsync<MathCounterexampleSearchProposal>(
            request, IntelligencePromptCodes.MathCounterexampleSearch, MathContractSchemas.CounterexampleSearchSchema,
            BuildDerivationContext(request, derivation), correlationId, cancellationToken);

        // Stage 6 — final answer extraction in canonical, comparable form.
        var answer = await RunStageAsync<MathAnswerProposal>(
            request, IntelligencePromptCodes.MathAnswerExtraction, MathContractSchemas.AnswerExtractionSchema,
            BuildDerivationContext(request, derivation), correlationId, cancellationToken) ?? derivation.Answer;
        var canonicalAnswer = CanonicalizeAnswer(answer);

        // Stage 7 — self-consistency clustering (raises DISCOVERY confidence; never verification).
        var selfConsistency = await RunStageAsync<MathSelfConsistencyProposal>(
            request, IntelligencePromptCodes.MathSelfConsistency, MathContractSchemas.SelfConsistencySchema,
            BuildDerivationContext(request, derivation), correlationId, cancellationToken) ?? new MathSelfConsistencyProposal();
        var aggregate = Verifier.Aggregate(MathContractMapper.ToCandidateAnswers(selfConsistency));

        // Assemble the derivation state and classify the outcome deterministically.
        var verificationStatus = DeriveVerificationStatus(verifiedObligations);
        var rankedCandidates = ApplyCandidateVerification(candidates, verifiedObligations, verificationStatus);
        var discoveryConfidence = ComputeDiscoveryConfidence(rankedCandidates, aggregate.AgreementRatio);

        var state = new ProofDerivationState
        {
            Contract = domainContract,
            Nodes = nodes,
            Edges = edges,
            Obligations = verifiedObligations,
            Candidates = rankedCandidates,
            Claims = claims,
            CanonicalAnswer = canonicalAnswer,
            DiscoveryConfidence = discoveryConfidence,
            VerificationStatus = verificationStatus,
        };
        var outcome = ConvergencePolicy.Evaluate(state, aggregate.AgreementRatio);

        // Deterministic epistemic guards (P0 #1/#2/#3): the certainty ceiling flags claims asserted more
        // strongly than their essential obligations support, and prior-art detection flags rediscoveries.
        var ceilingResults = CertaintyCeiling.Evaluate(state);
        var priorArtMatches = PriorArt.DetectRediscovery(state, KnownPriorArt);
        var claimWarnings = ceilingResults
            .Where(r => r.ClaimOutrunsProof && r.Warning is not null)
            .Select(r => r.Warning!)
            .Concat(priorArtMatches.Where(m => m.Warning is not null).Select(m => m.Warning!))
            .ToArray();

        // Multidimensional epistemic state (R/E/V/F): research priority and evidence never masquerade as
        // mathematical verification, which is derived strictly from the deterministic obligation results.
        var epistemic = ComputeEpistemicState(verifiedObligations, rankedCandidates, aggregate.AgreementRatio, discoveryConfidence);

        // First-class research-state sections so negative knowledge is not buried under a final answer.
        var verifiedFacts = verifiedObligations
            .Where(o => o.Status == ObligationStatus.Verified)
            .Select(o => o.Statement)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
        var openObligations = verifiedObligations
            .Where(o => o.Status is ObligationStatus.Open or ObligationStatus.Unresolved)
            .Select(o => o.Statement)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToArray();
        var eliminatedRoutes = verifiedObligations
            .Where(o => o.Status == ObligationStatus.Refuted)
            .Select(o => $"{o.Statement} (refuted)")
            .Concat(priorArtMatches.Select(m => $"{m.CandidateName} (prior art: {m.MatchedPriorArt})"))
            .ToArray();
        var survivingCandidates = rankedCandidates
            .Where(c => c.VerificationStatus != VerificationStatus.Refuted)
            .Select(c => $"{c.Name} [{c.MathObjectType}] — {c.VerificationStatus}")
            .ToArray();
        var nextTest = Competition.NextToInvestigate(rankedCandidates) is { } next
            ? $"Investigate '{next.Name}' — highest-information unverified candidate."
            : openObligations.Length > 0 ? $"Resolve open obligation: {openObligations[0]}." : null;

        // Stage 8 — communicate the ALREADY-resolved result honestly (outcome is fixed here, not by the LLM).
        var composer = await RunStageAsync<MathAnswerComposerProposal>(
            request, IntelligencePromptCodes.MathAnswerComposer, MathContractSchemas.AnswerComposerSchema,
            BuildComposerContext(request, outcome, canonicalAnswer, answer), correlationId, cancellationToken);

        var response = new MathSolveResponse
        {
            Outcome = outcome.ToString(),
            FinalAnswer = FirstNonEmpty(composer?.FinalAnswer, answer?.AnswerValue, canonicalAnswer),
            CanonicalAnswer = canonicalAnswer,
            SolutionSummary = composer?.SolutionSummary,
            KeySteps = composer?.KeySteps ?? [],
            RemainingUncertainty = composer?.RemainingUncertainty,
            DiscoveryConfidence = discoveryConfidence,
            VerificationStatus = verificationStatus.ToString(),
            SelfConsistencyAgreement = aggregate.AgreementRatio,
            Contract = contract,
            Obligations = verifiedObligations.Select(ToObligationResult).ToArray(),
            Candidates = rankedCandidates.Select(ToCandidateResult).ToArray(),
            ModelAvailable = !_modelRouteUnavailable,
            ResearchPriority = epistemic.ResearchPriority,
            EvidenceSupport = epistemic.EvidenceSupport,
            MathematicalVerification = epistemic.MathematicalVerification,
            FalsificationCoverage = epistemic.FalsificationCoverage,
            EpistemicStateLabel = epistemic.StateLabel,
            ClaimWarnings = claimWarnings,
            VerifiedFacts = verifiedFacts,
            EliminatedRoutes = eliminatedRoutes,
            SurvivingCandidates = survivingCandidates,
            OpenObligations = openObligations,
            NextHighestInformationTest = nextTest,
            CorrelationId = correlationId,
        };

        var durationMs = (long)System.Diagnostics.Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        await PersistRunAsync(request, response, durationMs, cancellationToken);

        return response;
    }

    // Runs a single LLM stage and deserializes its proposal. Returns null (never throws) when the model
    // is unavailable or returns unparseable content, so the pipeline degrades to an honest UNRESOLVED.
    private async Task<T?> RunStageAsync<T>(
        MathSolveRequest request,
        string promptCode,
        string outputSchemaJson,
        string userPrompt,
        string correlationId,
        CancellationToken cancellationToken) where T : class
    {
        try
        {
            var systemPrompt = await promptCatalog.GetSystemPromptAsync(request.TenantId, promptCode, cancellationToken);
            var executionContext = new AiExecutionContext(ModuleCode, null, null, request.Problem, "MATH_REASONING", null, correlationId, promptCode);
            var result = await aiProviderRouter.GenerateAsync(
                request.TenantId, FeatureCode, systemPrompt, userPrompt, outputSchemaJson, correlationId,
                executionContext, string.IsNullOrWhiteSpace(request.ModelCode) ? null : request.ModelCode.Trim(), cancellationToken);

            var json = string.IsNullOrWhiteSpace(result.StructuredOutputJson) ? result.Content : result.StructuredOutputJson;
            if (string.IsNullOrWhiteSpace(json))
                return null;
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch (Exception ex) when (ex is AiProviderUnavailableException or JsonException)
        {
            if (ex is AiProviderUnavailableException)
                _modelRouteUnavailable = true;
            logger.LogWarning(ex, "Math stage {PromptCode} did not produce a usable result; degrading gracefully.", promptCode);
            return null;
        }
    }

    private static IReadOnlyList<ProofObligation> VerifyObligations(IReadOnlyList<ProofObligation> obligations, MathSolutionDerivationProposal derivation)
    {
        if (obligations.Count == 0)
            return obligations;
        var proposalsById = derivation.Obligations
            .Where(o => !string.IsNullOrWhiteSpace(o.Id))
            .GroupBy(o => o.Id!)
            .ToDictionary(g => g.Key, g => g.First());

        return obligations
            .Select(o => proposalsById.TryGetValue(o.ObligationId, out var proposal)
                ? Verifier.Verify(o, MathContractMapper.ToVerificationRequest(proposal))
                : o)
            .ToArray();
    }

    // The derivation's verification status: refuted if any obligation is refuted; verified only when at
    // least one obligation exists and all resolved obligations are verified (none open/unresolved).
    private static VerificationStatus DeriveVerificationStatus(IReadOnlyList<ProofObligation> obligations)
    {
        if (obligations.Count == 0)
            return VerificationStatus.Unverified;
        if (obligations.Any(o => o.Status == ObligationStatus.Refuted))
            return VerificationStatus.Refuted;
        var anyVerified = obligations.Any(o => o.Status == ObligationStatus.Verified);
        var anyOpen = obligations.Any(o => o.Status is ObligationStatus.Open or ObligationStatus.Unresolved);
        if (anyVerified && !anyOpen)
            return VerificationStatus.Verified;
        if (obligations.Any(o => o.Status == ObligationStatus.Conditional) && !anyOpen)
            return VerificationStatus.Conditional;
        return VerificationStatus.Unverified;
    }

    // Propagate the derivation-level verification status to the leading candidate, then rank. Verification
    // dominates discovery confidence (VerificationWeightedCompetition), so a plausible-but-unverified
    // strategy can never outrank a verified one.
    private static IReadOnlyList<ScientificCandidate> ApplyCandidateVerification(
        IReadOnlyList<ScientificCandidate> candidates,
        IReadOnlyList<ProofObligation> obligations,
        VerificationStatus derivationStatus)
    {
        if (candidates.Count == 0)
            return candidates;
        var leaderId = candidates.OrderByDescending(c => c.DiscoveryConfidence).First().Id;
        var updated = candidates
            .Select(c => c.Id == leaderId ? c with { VerificationStatus = derivationStatus } : c)
            .ToArray();
        return Competition.Rank(updated);
    }

    private static double ComputeDiscoveryConfidence(IReadOnlyList<ScientificCandidate> candidates, double agreementRatio)
    {
        var leaderConfidence = candidates.Count == 0 ? 0d : candidates.Max(c => c.DiscoveryConfidence);
        return Math.Clamp(Math.Max(leaderConfidence, agreementRatio), 0d, 1d);
    }

    // Derives the four orthogonal epistemic dimensions from the deterministic state. Verification is the
    // fraction of essential-checkable obligations that passed — never inflated by discovery/agreement.
    private static EpistemicState ComputeEpistemicState(
        IReadOnlyList<ProofObligation> obligations,
        IReadOnlyList<ScientificCandidate> candidates,
        double agreementRatio,
        double discoveryConfidence)
    {
        var researchPriority = candidates.Count == 0 ? 0d : candidates.Max(c => c.DiscoveryConfidence);
        var evidenceSupport = Math.Clamp(Math.Max(agreementRatio, discoveryConfidence), 0d, 1d);

        var essential = obligations.Where(o => o.IsEssential).ToArray();
        var verificationBasis = essential.Length > 0 ? essential : obligations;
        var mathematicalVerification = verificationBasis.Count == 0
            ? 0d
            : (double)verificationBasis.Count(o => o.Status == ObligationStatus.Verified) / verificationBasis.Count;

        var searched = obligations.Count == 0
            ? 0d
            : (double)obligations.Count(o => o.CounterexampleStatus != CounterexampleStatus.NotSearched) / obligations.Count;

        return new EpistemicState(researchPriority, evidenceSupport, mathematicalVerification, searched);
    }

    private string? CanonicalizeAnswer(MathAnswerProposal? answer)
    {
        if (answer is null || string.IsNullOrWhiteSpace(answer.AnswerValue))
            return NullIfEmpty(answer?.CanonicalForm);
        var answerType = MathContractMapper.ParseAnswerType(answer.AnswerType);
        return Verifier.Canonicalize(answer.AnswerValue!, answerType);
    }

    private async Task PersistRunAsync(MathSolveRequest request, MathSolveResponse response, long durationMs, CancellationToken cancellationToken)
    {
        if (repository is null)
            return;

        try
        {
            var record = new MathExecutionRecord
            {
                TenantId = request.TenantId,
                UserId = request.UserId,
                ProblemText = request.Problem,
                CorrelationId = response.CorrelationId,
                OutcomeCode = response.Outcome,
                VerificationStatusCode = response.VerificationStatus,
                FinalAnswer = response.FinalAnswer,
                CanonicalAnswer = response.CanonicalAnswer,
                SolutionSummary = response.SolutionSummary,
                RemainingUncertainty = response.RemainingUncertainty,
                DiscoveryConfidence = (decimal)Math.Clamp(response.DiscoveryConfidence, 0d, 1d),
                SelfConsistencyAgreement = (decimal)Math.Clamp(response.SelfConsistencyAgreement, 0d, 1d),
                ResearchPriority = (decimal)Math.Clamp(response.ResearchPriority, 0d, 1d),
                EvidenceSupport = (decimal)Math.Clamp(response.EvidenceSupport, 0d, 1d),
                MathematicalVerification = (decimal)Math.Clamp(response.MathematicalVerification, 0d, 1d),
                FalsificationCoverage = (decimal)Math.Clamp(response.FalsificationCoverage, 0d, 1d),
                EpistemicStateLabel = NullIfEmpty(response.EpistemicStateLabel),
                ModelCode = NullIfEmpty(request.ModelCode),
                DurationMilliseconds = durationMs,
                Obligations = response.Obligations.Select((o, index) => new MathObligationRecord
                {
                    ObligationKey = o.ObligationId,
                    Statement = o.Statement,
                    VerificationMethodCode = o.VerificationMethod,
                    StatusCode = o.Status,
                    CounterexampleStatusCode = o.CounterexampleStatus,
                    DiscoveryConfidence = (decimal)Math.Clamp(o.DiscoveryConfidence, 0d, 1d),
                    VerificationNote = o.VerificationNote,
                    SortOrder = index,
                }).ToArray(),
                Candidates = response.Candidates.Select((c, index) => new MathCandidateRecord
                {
                    CandidateKey = c.Id,
                    ObjectTypeCode = c.ObjectType,
                    Name = c.Name,
                    Description = c.Description,
                    DiscoveryConfidence = (decimal)Math.Clamp(c.DiscoveryConfidence, 0d, 1d),
                    VerificationStatusCode = c.VerificationStatus,
                    SortOrder = index,
                }).ToArray(),
            };

            await repository.SaveMathExecutionAsync(record, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to persist Math run {CorrelationId}; the response is unaffected.", response.CorrelationId);
        }
    }

    private static MathObligationResult ToObligationResult(ProofObligation o) => new(
        o.ObligationId, o.Statement, o.VerificationMethod.ToString(), o.Status.ToString(),
        o.CounterexampleStatus.ToString(), o.DiscoveryConfidence, o.VerificationNote);

    private static MathCandidateResult ToCandidateResult(ScientificCandidate c) => new(
        c.Id, c.ObjectType.ToString(), c.Name, c.Description, c.DiscoveryConfidence, c.VerificationStatus.ToString());

    private static string BuildContractContext(MathSolveRequest request, MathProblemContractProposal contract) =>
        $"Problem:\n{request.Problem}\n\nProblem contract:\n{Serialize(contract)}";

    private static string BuildStrategyContext(MathSolveRequest request, MathProblemContractProposal contract, MathStrategyProposal strategy) =>
        $"Problem:\n{request.Problem}\n\nProblem contract:\n{Serialize(contract)}\n\nStrategies:\n{Serialize(strategy)}";

    private static string BuildDerivationContext(MathSolveRequest request, MathSolutionDerivationProposal derivation) =>
        $"Problem:\n{request.Problem}\n\nDerivation:\n{Serialize(derivation)}";

    private static string BuildComposerContext(MathSolveRequest request, ScientificOutcome outcome, string? canonicalAnswer, MathAnswerProposal? answer) =>
        $"Problem:\n{request.Problem}\n\nResolved outcome (authoritative, do not change): {outcome}\nCanonical answer: {canonicalAnswer ?? "(none)"}\nExtracted answer:\n{Serialize(answer)}";

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))?.Trim();
}
