using System.Diagnostics;
using System.Text.Json;
using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Abstractions.Persistence;
using Legal.Application.Abstractions.Services;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Microsoft.Extensions.Logging;

namespace Legal.Application;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal Decision Intelligence orchestrator (/legal/decision). Implements the Core loop from
// the 1–61 placement map as extensible stages:
//   Intake → Query/Decision Contract → Hierarchy/Candidate Discovery (LLM proposal) →
//   Candidate × Branch Competition → Uncertainty/Entropy → Information Value/ADV → Adaptive
//   Narrowing (branch states + frontier) → Decision-Directed Retrieval/Evidence → Candidate
//   Recompetition → Flip Points → Convergence/Terminal State → Answer Assembly → Persistence/Audit.
// The DECISION is the primary object; POLOXI Core owns the authoritative state and all scoring (§3),
// the LLM only proposes semantics. Everything configurable comes from POLOXI.Legal_Decision* tables.
// ─────────────────────────────────────────────────────────────────────────────────────────────
public sealed class LegalDecisionService(
    ILegalDecisionRepository repository,
    ILegalDecisionAiProvider aiProvider,
    ILegalDecisionRetriever retriever,
    IDependencyPropagationService propagationService,
    ILegalDecisionImpactMapper impactMapper,
    Features.Intelligence.Epistemic.IEpistemicDecisionBridge epistemicBridge,
    Abstractions.Persistence.IDecisionGovernanceRepository governanceRepository,
    ILogger<LegalDecisionService> logger) : ILegalDecisionService
{
    private const string DiscoveryPromptCode = "DECISION_DISCOVERY";
    private const string AnswerPromptCode = "DECISION_ANSWER";
    private const string GraphPromptCode = "DECISION_GRAPH";
    private const string VerifyPromptCode = "DECISION_VERIFY";

    public async Task<IReadOnlyCollection<DecisionModelOptionDto>> GetModelsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var routes = await repository.GetModelRoutesAsync(cancellationToken);
        return routes
            .Where(r => !string.IsNullOrWhiteSpace(r.ModelCode))
            .GroupBy(r => r.ModelCode, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderBy(r => r.Priority).First())
            .Select(r => new DecisionModelOptionDto(r.ModelCode, r.DeploymentName, r.ProviderTypeCode))
            .ToArray();
    }

    public async Task<IReadOnlyCollection<DecisionContextDto>> GetContextsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => await repository.GetContextsAsync(cancellationToken);

    public Task<IReadOnlyCollection<DecisionMatterDto>> GetMattersAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetMattersAsync(tenantId, cancellationToken);

    public Task<DecisionMatterDto?> GetMatterAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => repository.GetMatterAsync(tenantId, decisionMatterId, cancellationToken);

    public Task<Guid> CreateMatterAsync(Guid tenantId, Guid userId, DecisionMatterCreateRequest request, CancellationToken cancellationToken = default)
        => repository.CreateMatterAsync(tenantId, userId, request, cancellationToken);

    public Task<IReadOnlyCollection<DecisionTimelineEventDto>> GetTimelineAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
        => repository.GetSessionTimelineAsync(tenantId, decisionSessionId, cancellationToken);

    public Task<IReadOnlyCollection<DecisionSessionSummaryDto>> GetMatterSessionsAsync(Guid tenantId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => repository.GetMatterSessionsAsync(tenantId, decisionMatterId, cancellationToken);

    public Task<bool> UpdateMatterAsync(Guid tenantId, Guid userId, Guid decisionMatterId, DecisionMatterUpdateRequest request, CancellationToken cancellationToken = default)
        => repository.UpdateMatterAsync(tenantId, userId, decisionMatterId, request, cancellationToken);

    public Task<bool> UpdateMatterStatusAsync(Guid tenantId, Guid userId, Guid decisionMatterId, string statusCode, CancellationToken cancellationToken = default)
    {
        if (!DecisionMatterStatusCodes.IsValid(statusCode))
            throw new ArgumentException($"'{statusCode}' is not a valid matter status.", nameof(statusCode));
        var normalized = statusCode.ToUpperInvariant();
        return repository.UpdateMatterStatusAsync(tenantId, userId, decisionMatterId, normalized, cancellationToken);
    }

    public Task<bool> DeleteMatterAsync(Guid tenantId, Guid userId, Guid decisionMatterId, CancellationToken cancellationToken = default)
        => repository.DeleteMatterAsync(tenantId, userId, decisionMatterId, cancellationToken);

    public Task<DecisionMatterFacetsDto> GetMatterFacetsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => repository.GetMatterFacetsAsync(tenantId, cancellationToken);

    public async Task<DecisionSearchResponse> DecideAsync(DecisionSearchRequest request, CancellationToken cancellationToken = default)
    {
        var timer = Stopwatch.StartNew();
        var sessionId = Guid.NewGuid();
        var settings = await repository.GetCoreSettingsAsync(cancellationToken);
        var v2Settings = await repository.GetV2SettingsAsync(cancellationToken);
        var useGraph = request.UseDependencyGraph ?? v2Settings.UseDependencyGraphDefault;
        var route = await ResolveRouteAsync(request.ModelCode, cancellationToken);
        var contextCode = string.IsNullOrWhiteSpace(request.ContextCode) ? DecisionContexts.General : request.ContextCode!.Trim().ToUpperInvariant();
        var events = new List<DecisionEventPersistence>();
        var sequence = 0;
        void Record(string type, string stage, object? payload = null) =>
            events.Add(new DecisionEventPersistence(Guid.NewGuid(), null, sequence++, type, stage, payload is null ? null : JsonSerializer.Serialize(payload), null));

        // A continued session folds the user's clarification answer into the effective query (§7 loop),
        // so the proposal layer re-competes candidates with the disambiguating detail in hand.
        var hasClarification = !string.IsNullOrWhiteSpace(request.ClarificationAnswer);
        var hasCounterfactual = !string.IsNullOrWhiteSpace(request.CounterfactualAssumption);
        var effectiveQuery = request.Query;
        if (hasClarification)
            effectiveQuery += $"\n\nClarification ({request.ClarificationTarget ?? "detail"}): {request.ClarificationAnswer}";
        if (hasCounterfactual)
            effectiveQuery += $"\n\nCounterfactual assumption (treat as established for this analysis): {request.CounterfactualAssumption}";

        Record("SESSION_STARTED", "INTAKE", new { request.Query, contextCode, route.ModelCode, request.UsePoloxiEngine, hasClarification, hasCounterfactual });

        if (!request.UsePoloxiEngine)
            return await ComposeDirectAnswerAsync(request, sessionId, route, contextCode, events, timer, cancellationToken);

        // ── Candidate Discovery (LLM proposal only) ─────────────────────────────────────────────
        var discoveryPrompt = await repository.GetPromptAsync(DiscoveryPromptCode, cancellationToken)
            ?? throw new InvalidOperationException($"The '{DiscoveryPromptCode}' decision prompt is not configured in POLOXI.Legal_DecisionPrompt.");
        var discoveryUser = discoveryPrompt.UserPromptTemplate
            .Replace("{{QUERY}}", effectiveQuery)
            .Replace("{{CONTEXT}}", contextCode);
        var discovery = await aiProvider.GenerateAsync(
            new DecisionAiRequest(route, "DECISION_DISCOVERY", discoveryPrompt.SystemPrompt, discoveryUser, discoveryPrompt.OutputSchemaJson, request.CorrelationId),
            cancellationToken);
        var llmCalls = 1;
        Record("CANDIDATES_PROPOSED", "DISCOVERY", new { discovery.InputTokenCount, discovery.OutputTokenCount });

        var proposal = ParseProposal(discovery.StructuredOutputJson ?? discovery.Content, settings.MaxCandidates);
        if (proposal.Count == 0)
            throw new InvalidOperationException("The decision proposal layer returned no candidate outcomes.");

        // ── Candidate × Branch competition + deterministic Core scoring ──────────────────────────
        var candidates = ScoreCandidates(proposal, settings, sessionId, request.TenantId, out var branches, cancellationToken);
        Record("CANDIDATES_SCORED", "COMPETITION", new { candidateCount = candidates.Count });

        // Bounded adaptive deepening telemetry: emit an observable event only when the deepening pass
        // actually materialized sub-branches (LevelNumber > 1), so a deepened run is traceable and the
        // seeded regression matter mirrors real runtime behavior. Flat runs (the common case) skip it.
        var maxDepthReached = branches.Count == 0 ? 0 : branches.Max(b => b.LevelNumber);
        if (maxDepthReached > 1)
            Record("BRANCH_DEEPENED", "COMPETITION", new
            {
                depth = maxDepthReached,
                deepenedBranchCount = branches.Count(b => b.LevelNumber > 1),
                deepeningFlip = settings.ThresholdDeepeningFlip,
                maxDepth = settings.MaxDepth
            });

        // ── Uncertainty / Entropy (uncertainty signal only, not truth) ──────────────────────────
        var orderedScores = candidates.Select(c => (double)c.CompositeScore).OrderByDescending(x => x).ToArray();
        var distribution = DecisionCoreMath.Distribution(orderedScores);
        var entropy = DecisionCoreMath.NormalizedEntropy(distribution);
        var margin = DecisionCoreMath.Margin(orderedScores);

        // ── Decision-Directed Retrieval / Evidence (§13,§14) ────────────────────────────────────
        var evidence = await RetrieveEvidenceAsync(contextCode, branches, sessionId, request.TenantId, request.MaximumResults, cancellationToken);
        Record("EVIDENCE_RETRIEVED", "RETRIEVAL", new { evidenceCount = evidence.Count });

        // ── Frontier + Flip Points (§11,§30) ────────────────────────────────────────────────────
        var flipPoints = BuildFlipPoints(branches, candidates, sessionId, request.TenantId);

        // ── Convergence / Terminal State (§32,§33,§34) ──────────────────────────────────────────
        var winner = candidates.OrderBy(c => c.RankOrder).FirstOrDefault();
        var frontierOpen = branches.Any(b => b.IsOnFrontier);
        var maxAvailableAdv = branches.Count == 0 ? 0d : branches.Max(b => (double)b.AdvScore);
        var (statusCode, terminalState, reason) = ResolveTerminalState(settings, margin, entropy, frontierOpen, maxAvailableAdv);

        // ── User clarification (§7): an ambiguous, high-entropy tie the engine cannot break by itself is
        // resolved by asking the user once. A session already carrying a clarification answer never re-asks.
        string? clarificationQuestion = null;
        string? clarificationTarget = null;
        if (!hasClarification && statusCode != DecisionStatusCodes.DecisionReady && entropy >= 0.85 && margin < 0.05)
        {
            var pivot = branches.Where(b => b.IsOnFrontier).OrderByDescending(b => b.FlipPotential).FirstOrDefault()
                ?? branches.OrderByDescending(b => b.FlipPotential).FirstOrDefault();
            if (pivot is not null)
            {
                statusCode = DecisionStatusCodes.UserClarificationRequired;
                terminalState = DecisionStatusCodes.UserClarificationRequired;
                reason = "AMBIGUOUS_TIE_NEEDS_USER_INPUT";
                clarificationTarget = pivot.DisplayName;
                clarificationQuestion = string.IsNullOrWhiteSpace(pivot.Interpretation)
                    ? $"To decide between the leading outcomes, can you clarify '{pivot.DisplayName}'?"
                    : $"To decide between the leading outcomes, can you clarify '{pivot.DisplayName}'? {pivot.Interpretation}";
            }
        }
        // The run always terminates here, but that is NOT the same as the decision converging. A run
        // can complete while the decision remains provisional with research still open. The timeline
        // phase reflects the DECISION state (not the run) so a provisional run is never mislabeled as
        // "CONVERGENCE". This is a presentation label only; it does not affect scoring or the pipeline.
        var terminalPhase = statusCode switch
        {
            DecisionStatusCodes.DecisionReady => "CONVERGENCE",
            DecisionStatusCodes.ProvisionalDecision => "PROVISIONAL_RESEARCH_REMAINS",
            DecisionStatusCodes.UserClarificationRequired => "CLARIFICATION_REQUIRED",
            DecisionStatusCodes.ResearchExhausted => "RESEARCH_EXHAUSTED",
            _ => "TERMINAL_STATE"
        };
        Record("TERMINAL_STATE", terminalPhase, new { statusCode, terminalState, margin, entropy });

        // ── Answer Assembly (§37): composer reads the structured artifact only ──────────────────
        string? finalAnswer = null;
        if (statusCode == DecisionStatusCodes.DecisionReady || statusCode == DecisionStatusCodes.ResearchExhausted || statusCode == DecisionStatusCodes.ProvisionalDecision)
        {
            finalAnswer = await ComposeAnswerAsync(request, route, candidates, branches, flipPoints, margin, entropy, cancellationToken);
            llmCalls++;
            Record("ANSWER_COMPOSED", "ANSWER", null);
        }

        timer.Stop();

        // ── Next Best Action (§ next action): the single highest-impact open investigation, derived
        // deterministically from the decision frontier. Null when nothing open can move the decision. ──
        var nextAction = BuildNextBestAction(branches, statusCode);
        var readiness = BuildReadiness(candidates, branches, evidence, margin, entropy, statusCode);

        var persistence = new DecisionSessionPersistence(
            sessionId, request.TenantId, request.UserId, request.Query, contextCode, route.ModelCode, true,
            statusCode, terminalState, reason, winner?.DecisionCandidateId,
            (decimal)0, (decimal)entropy, (decimal)margin,
            branches.Count == 0 ? 0 : branches.Max(b => b.LevelNumber), llmCalls, timer.ElapsedMilliseconds,
            finalAnswer, clarificationQuestion, clarificationTarget, request.CorrelationId,
            candidates, branches, evidence, flipPoints, events)
        {
            MatterId = request.MatterId,
            NextBestActionText = nextAction?.Title,
            NextBestActionImpactCode = nextAction?.ImpactCode,
            NextBestActionRationale = nextAction?.Rationale,
            CounterfactualAssumption = request.CounterfactualAssumption
        };
        await repository.PersistSessionAsync(persistence, cancellationToken);

        // ── POLOXI Legal V2 (dependency-aware) — runs only when the per-session toggle is on. It
        // proposes a typed legal dependency graph, runs an INDEPENDENT verifier, propagates any
        // invalidation deterministically, runs the strongest-losing-side gate, and computes the
        // dependency-constrained readiness verdict. Persisted after the session row (FK dependency). ──
        DecisionV2Result? v2 = null;
        Features.Intelligence.Decision.DecisionGovernanceVerdictDto? governanceVerdict = null;
        if (useGraph)
        {
            try
            {
                v2 = await RunDependencyGraphAsync(request, route, sessionId, contextCode, effectiveQuery,
                    candidates, branches, v2Settings, winner, cancellationToken);
                await repository.PersistGraphAsync(v2.Persistence, cancellationToken);

                // EA-6/EA-7: advisory epistemic governance overlay. Projects the V2 graph into
                // authoritative EA claims (verify + authority gate), runs readiness + output audit, and
                // records a NON-DESTRUCTIVE governance verdict. Advisory by default: it never changes the
                // V1/V2 verdict; all claims stay visible for reference. Strictly non-blocking.
                try
                {
                    var epistemicContext = new Features.Intelligence.Epistemic.EpistemicDecisionContext
                    {
                        SessionId = sessionId,
                        TenantId = request.TenantId,
                        MatterId = request.MatterId,
                        ActorUserId = request.UserId,
                        ProposedByModel = route.ModelCode,
                        PromptRunId = request.CorrelationId,
                        Nodes = v2.Nodes.ToArray(),
                        Edges = v2.Edges.ToArray(),
                    };
                    var governance = await epistemicBridge.ProjectAndGovernAsync(epistemicContext, cancellationToken);
                    if (governance.Executed)
                    {
                        governanceVerdict = await PersistGovernanceVerdictAsync(
                            request, sessionId, v2, governance, cancellationToken);
                        logger.LogInformation(
                            "EA-7 governance for session {SessionId}: {Projected} claim(s), {Authorized} authorized, ready={Ready}, output-clean={Clean}, mode={Mode}, override={Override}.",
                            sessionId, governance.ProjectedClaimCount, governance.AuthorizedClaimCount,
                            governance.Readiness?.IsReady, governance.OutputAudit?.IsClean,
                            Features.Intelligence.Epistemic.EpistemicOverrideModes.ToCode(governance.OverrideMode),
                            governance.OverrideApplied);
                    }
                }
                catch (Exception epistemicEx)
                {
                    logger.LogWarning(epistemicEx, "EA-7 epistemic governance failed for session {SessionId}; decision unaffected.", sessionId);
                }
            }
            catch (Exception ex)
            {
                // V2 is advisory; a graph/verifier failure must never block the V1 decision (§ optional).
                logger.LogWarning(ex, "V2 dependency graph failed for session {SessionId}; returning V1 result.", sessionId);
                v2 = null;
            }
        }

        return BuildResponse(persistence, nextAction, readiness, useGraph, v2, governanceVerdict);
    }

    // ── Direct LLM answer path (POLOXI Engine off) ──────────────────────────────────────────────
    private async Task<Features.Intelligence.Decision.DecisionGovernanceVerdictDto?> PersistGovernanceVerdictAsync(
        DecisionSearchRequest request,
        Guid sessionId,
        DecisionV2Result v2,
        Features.Intelligence.Epistemic.EpistemicGovernanceResult governance,
        CancellationToken cancellationToken)
    {
        var v2Ready = v2.ReadinessVerdict?.Satisfied ?? true;
        var eaReady = governance.EaReady;
        var outputClean = governance.OutputAudit?.IsClean ?? true;
        // Effective readiness is the V2 verdict unless a stronger mode actually downgraded it.
        var effectiveReady = governance.OverrideApplied ? false : v2Ready;

        var blockers = governance.Readiness?.Blockers.Select(b => b.Reason).ToArray() ?? [];
        var violations = governance.OutputAudit?.Violations.Select(v => v.Reason).ToArray() ?? [];
        var claims = governance.InvolvedClaims
            .Select(c => new Features.Intelligence.Decision.GovernanceClaimDto(
                c.ClaimId, c.Text,
                Features.Intelligence.Epistemic.ClaimCodes.ToCode(c.VerificationState),
                Features.Intelligence.Epistemic.ClaimCodes.ToCode(c.DecisionAuthority),
                c.IsAuthorized, c.IsEssential, c.Annotation))
            .ToArray();
        var narrative = governance.AuditNarrative.ToArray();
        var modeCode = Features.Intelligence.Epistemic.EpistemicOverrideModes.ToCode(governance.OverrideMode);

        var persistence = new Abstractions.Persistence.DecisionGovernanceVerdictPersistence(
            Guid.NewGuid(), sessionId, request.MatterId, modeCode,
            v2Ready, eaReady, governance.Readiness?.Enforced ?? false, outputClean, effectiveReady,
            governance.OverrideApplied, governance.ProjectedClaimCount, governance.AuthorizedClaimCount,
            blockers.Length, violations.Length,
            JsonSerializer.Serialize(blockers), JsonSerializer.Serialize(violations),
            JsonSerializer.Serialize(claims), JsonSerializer.Serialize(narrative),
            request.TenantId, request.UserId);

        await governanceRepository.UpsertVerdictAsync(persistence, cancellationToken);

        return new Features.Intelligence.Decision.DecisionGovernanceVerdictDto(
            modeCode, v2Ready, eaReady, outputClean, effectiveReady, governance.OverrideApplied,
            governance.ProjectedClaimCount, governance.AuthorizedClaimCount,
            blockers, violations, claims, narrative);
    }

    private async Task<DecisionSearchResponse> ComposeDirectAnswerAsync(DecisionSearchRequest request, Guid sessionId, DecisionModelRouteDto route, string contextCode, List<DecisionEventPersistence> events, Stopwatch timer, CancellationToken cancellationToken)
    {
        var answerPrompt = await repository.GetPromptAsync(AnswerPromptCode, cancellationToken)
            ?? throw new InvalidOperationException($"The '{AnswerPromptCode}' decision prompt is not configured.");
        var user = answerPrompt.UserPromptTemplate.Replace("{{ARTIFACT}}", "{}").Replace("{{QUERY}}", request.Query);
        var result = await aiProvider.GenerateAsync(new DecisionAiRequest(route, "DECISION_ANSWER", answerPrompt.SystemPrompt, user, null, request.CorrelationId), cancellationToken);
        timer.Stop();
        var persistence = new DecisionSessionPersistence(
            sessionId, request.TenantId, request.UserId, request.Query, contextCode, route.ModelCode, false,
            DecisionStatusCodes.DecisionReady, DecisionStatusCodes.DecisionReady, "LLM_ONLY", null,
            0, 0, 0, 0, 1, timer.ElapsedMilliseconds, result.Content, null, null, request.CorrelationId,
            [], [], [], [], events)
        {
            MatterId = request.MatterId,
            CounterfactualAssumption = request.CounterfactualAssumption
        };
        await repository.PersistSessionAsync(persistence, cancellationToken);
        return BuildResponse(persistence, null, []);
    }

    // ── Read-back: project a persisted session (and its V2 graph, if any) into a response ──────────
    // Lets the cockpit open a saved/seeded session (e.g. the 0215 vertical slice) without re-running
    // the LLM. The V2 fields are populated only when a persisted dependency graph exists.
    public async Task<DecisionSearchResponse?> GetSessionResultAsync(Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken = default)
    {
        var session = await repository.GetSessionAsync(tenantId, decisionSessionId, cancellationToken);
        if (session is null)
            return null;

        var nextAction = session.NextBestActionText is null ? null : new DecisionNextActionDto(
            session.NextBestActionText, session.NextBestActionImpactCode ?? "MEDIUM",
            session.NextBestActionRationale ?? string.Empty, null, 0m, 0m);
        var readiness = BuildReadiness(
            session.Candidates, session.Branches, session.Evidence,
            (double)session.DecisionMargin, (double)session.CandidateEntropy, session.StatusCode);

        var graph = await repository.GetGraphAsync(tenantId, decisionSessionId, cancellationToken);
        if (graph is null)
            return await HydrateClosedLoopAsync(BuildResponse(session, nextAction, readiness), tenantId, decisionSessionId, cancellationToken);

        var nodeDtos = graph.Nodes
            .Select(n => new DecisionGraphNodeDto(n.NodeId, n.NodeKind, n.NodeCode, n.DisplayName, n.Statement, n.Support, n.IsEssential, n.IsSatisfied, n.VerificationStatus, n.SortOrder))
            .ToArray();
        var edgeDtos = graph.Edges
            .Select(e => new DecisionGraphEdgeDto(e.EdgeId, e.RelationCode, e.SourceNodeKind, e.SourceNodeId, e.TargetNodeKind, e.TargetNodeId, e.SupportWeight, e.Materiality, e.IsEssential, e.IsDispositive, e.VerificationStatus, e.VerificationNotes, e.PropagatedStateCode))
            .ToArray();

        DecisionLosingSideTestDto? losingDto = graph.LosingSideTest is { } l
            ? new DecisionLosingSideTestDto(l.ChallengerCandidateId, l.StrongestCaseSummary, l.ChallengerStrength, l.WinnerStrength, l.WinnerSurvived)
            : null;

        var blockers = string.IsNullOrWhiteSpace(graph.ReadinessBlockersJson)
            ? Array.Empty<string>()
            : (JsonSerializer.Deserialize<string[]>(graph.ReadinessBlockersJson) ?? []);
        var predicate = blockers.Length == 0
            ? new[] { new DecisionReadinessItemDto("Dependency-constrained readiness", graph.ReadinessSatisfied, graph.ReadinessSatisfied ? "All essential dependencies satisfied and verified." : null) }
            : blockers.Select(b => new DecisionReadinessItemDto(b, false, null)).ToArray();
        var verdict = new DecisionReadinessVerdictDto(graph.ReadinessSatisfied, blockers, predicate);

        var v2 = new DecisionV2Result(graph, nodeDtos, edgeDtos, losingDto, verdict);
        return await HydrateClosedLoopAsync(BuildResponse(session, nextAction, readiness, usedDependencyGraph: true, v2), tenantId, decisionSessionId, cancellationToken);
    }

    // Hydrate the persisted V2.1 closed-loop readback (last recompetition + latest open research
    // need) so a page reload or an idempotent replay shows the same closed-loop state.
    private async Task<DecisionSearchResponse> HydrateClosedLoopAsync(
        DecisionSearchResponse response, Guid tenantId, Guid decisionSessionId, CancellationToken cancellationToken)
    {
        var recompetition = await repository.GetLatestRecompetitionAsync(tenantId, decisionSessionId, cancellationToken);
        var researchNeed = await repository.GetLatestOpenResearchNeedAsync(tenantId, decisionSessionId, cancellationToken);
        if (recompetition is null && researchNeed is null)
            return response;

        var recompetitionDto = recompetition is null ? null : new DecisionRecompetitionDto(
            recompetition.DecisionRecompetitionId, recompetition.PreviousWinnerCandidateId, recompetition.CurrentWinnerCandidateId,
            recompetition.WinnerChanged, recompetition.PreviousEntropy, recompetition.CurrentEntropy,
            recompetition.PreviousMargin, recompetition.CurrentMargin, recompetition.ReopenedBranchCount,
            recompetition.ReasonCode, DateTime.UtcNow);

        var researchNeedDto = researchNeed is null ? null : new DecisionResearchNeedDto(
            researchNeed.DecisionResearchNeedId, researchNeed.DecisionBranchId, researchNeed.IssueLabel, researchNeed.PropositionToResolve,
            researchNeed.AuthorityKind, researchNeed.RequiredEvidenceKind, researchNeed.WhyDecisionRelevant, researchNeed.ExpectedDiscrimination,
            researchNeed.CurrentUncertainty, researchNeed.InformationValue, researchNeed.FalsificationCondition, researchNeed.StatusCode);

        return response with { LastRecompetition = recompetitionDto, PendingResearchNeed = researchNeedDto };
    }

    // ── POLOXI Legal V2.1 — synchronous closed loop ─────────────────────────────────────────────
    // verification change → dependency propagation → domain-neutral signals → Candidate×Branch
    // recompetition → frontier/IV recalculation → ResearchNeed → readiness/audit. POLOXI stays the
    // sole scorer: the graph only supplies signals; DecisionRecompetition + DecisionCoreMath re-rank.
    public async Task<DecisionClosedLoopResultDto> ApplyVerificationChangeAsync(
        Guid tenantId, Guid userId, Guid decisionSessionId, DecisionVerificationChangeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await repository.GetSessionAsync(tenantId, decisionSessionId, cancellationToken)
            ?? throw new InvalidOperationException($"Decision session {decisionSessionId} was not found.");

        var idempotencyKey = string.IsNullOrWhiteSpace(request.IdempotencyKey)
            ? $"{request.EdgeId:N}:{request.NewStatus}"
            : request.IdempotencyKey!;

        // Idempotency: a retried event must not run the loop twice (§36).
        var existing = await repository.GetDependencyEventAsync(tenantId, decisionSessionId, idempotencyKey, cancellationToken);
        if (existing is not null)
        {
            var already = await GetSessionResultAsync(tenantId, decisionSessionId, cancellationToken)
                ?? throw new InvalidOperationException("Session result unavailable after idempotent replay.");
            return new DecisionClosedLoopResultDto(
                decisionSessionId, Applied: false, AlreadyProcessed: true,
                DependencyImpact.Empty, already.LastRecompetition, already.PendingResearchNeed, already,
                ["This verification change was already processed; returning the existing decision state."]);
        }

        var v21 = await repository.GetV21SettingsAsync(cancellationToken);
        var v2Settings = await repository.GetV2SettingsAsync(cancellationToken);
        var coreSettings = await repository.GetCoreSettingsAsync(cancellationToken);

        var graph = await repository.GetGraphAsync(tenantId, decisionSessionId, cancellationToken)
            ?? throw new InvalidOperationException("This session has no dependency graph; the closed loop requires V2 graph data.");

        var audit = new List<string>();

        // 1) Deterministic dependency propagation (graph produces impact only).
        var outcome = v21.UseDependencyPropagation
            ? propagationService.Apply(graph, request.EdgeId, request.NewStatus, v2Settings.PropagationMaxDepth)
            : new DependencyPropagationOutcome(DependencyImpact.Empty, DependencyPropagationService.BuildModel(graph), null, EdgeFound: true);

        if (!outcome.EdgeFound)
            throw new InvalidOperationException($"Edge {request.EdgeId} was not found in the session graph.");

        var impact = outcome.Impact;
        audit.Add($"Edge {request.EdgeId} verification changed {outcome.PreviousStatus ?? "UNKNOWN"} → {request.NewStatus}; {impact.AffectedBranchIds.Count} branch(es) and {impact.AffectedCandidateIds.Count} candidate(s) affected.");

        // 2) Persist the edge verification change (authoritative graph state).
        var changedEdge = graph.Edges.First(e => e.EdgeId == request.EdgeId);
        await repository.UpdateEdgeVerificationAsync(tenantId, userId, decisionSessionId,
            [changedEdge with { VerificationStatus = request.NewStatus, VerificationNotes = request.Notes }], cancellationToken);

        // 3) Persist the dependency event (idempotency + audit substrate).
        var dependencyEventId = Guid.NewGuid();
        await repository.PersistDependencyEventAsync(new DecisionDependencyEventPersistence(
            dependencyEventId, decisionSessionId, tenantId, userId, session.MatterId, request.EdgeId, idempotencyKey,
            outcome.PreviousStatus, request.NewStatus, JsonSerializer.Serialize(impact),
            impact.AffectedBranchIds.Count, impact.AffectedCandidateIds.Count, impact.RecompetitionRequired), cancellationToken);

        DecisionRecompetitionDto? recompetitionDto = null;
        DecisionResearchNeedDto? researchNeedDto = null;

        // 4) Candidate×Branch recompetition — POLOXI re-scores (only when enabled and requested).
        if (request.RunClosedLoop && v21.UseGraphDrivenRecompetition && impact.RecompetitionRequired)
        {
            var signals = impactMapper.Map(impact, graph, session.Branches.ToList(), session.Candidates.ToList());
            if (signals.Count > 0)
            {
                // Loop-safety: only branches under the reopen cap may be reopened this session.
                var reopenAllowed = new HashSet<Guid>();
                foreach (var bid in signals.Where(s => s.ReopenRequested && s.BranchId is not null).Select(s => s.BranchId!.Value).Distinct())
                {
                    var reopens = await repository.CountBranchReopensAsync(tenantId, decisionSessionId, bid, cancellationToken);
                    if (reopens < v21.LoopMaxReopensPerBranch)
                        reopenAllowed.Add(bid);
                }

                var result = DecisionRecompetition.Run(
                    session.Candidates.ToList(), session.Branches.ToList(), signals, coreSettings, reopenAllowed);

                var infoGain = Math.Abs(result.CurrentEntropy - result.PreviousEntropy);
                audit.Add($"Recompetition: entropy {result.PreviousEntropy:F3}→{result.CurrentEntropy:F3}, margin {result.PreviousMargin:F3}→{result.CurrentMargin:F3}, {result.ReopenedBranchCount} branch(es) reopened.");
                if (result.WinnerChanged)
                    audit.Add("Leadership flip: the dependency change overturned the previous winning candidate.");
                if (infoGain < v21.LoopNoInformationGainEpsilon && !result.WinnerChanged)
                    audit.Add("No material information gain from this recompetition (below epsilon).");

                // Persist re-ranked candidates + branch/frontier state (POLOXI authoritative state).
                await repository.ReplaceCandidatesAsync(tenantId, userId, decisionSessionId, result.Candidates, cancellationToken);
                await repository.ReplaceBranchesAsync(tenantId, userId, decisionSessionId, result.Branches, cancellationToken);
                await repository.UpdateSessionOutcomeAsync(tenantId, userId, decisionSessionId, session.StatusCode,
                    (decimal)result.CurrentEntropy, (decimal)result.CurrentMargin, result.CurrentWinnerId, cancellationToken);

                var recompetitionId = Guid.NewGuid();
                var reasonCode = result.WinnerChanged ? "WINNER_FLIP" : "SUPPORT_CHANGED";
                await repository.PersistRecompetitionAsync(new DecisionRecompetitionPersistence(
                    recompetitionId, decisionSessionId, tenantId, userId, dependencyEventId,
                    result.PreviousWinnerId, result.CurrentWinnerId, result.WinnerChanged,
                    (decimal)result.PreviousEntropy, (decimal)result.CurrentEntropy,
                    (decimal)result.PreviousMargin, (decimal)result.CurrentMargin, result.ReopenedBranchCount,
                    JsonSerializer.Serialize(session.Candidates.Select(c => new { c.CandidateCode, c.RankOrder, c.CompositeScore })),
                    JsonSerializer.Serialize(result.Candidates.Select(c => new { c.CandidateCode, c.RankOrder, c.CompositeScore })),
                    reasonCode), cancellationToken);

                recompetitionDto = new DecisionRecompetitionDto(
                    recompetitionId, result.PreviousWinnerId, result.CurrentWinnerId, result.WinnerChanged,
                    (decimal)result.PreviousEntropy, (decimal)result.CurrentEntropy,
                    (decimal)result.PreviousMargin, (decimal)result.CurrentMargin, result.ReopenedBranchCount,
                    reasonCode, DateTime.UtcNow);

                // 5) Frontier / Information-Value snapshot (when graph frontier signals are enabled).
                if (v21.UseGraphFrontierSignals)
                {
                    var openFrontier = result.Branches.Where(b => b.IsOnFrontier).ToList();
                    var top = openFrontier.OrderByDescending(b => b.InformationValue).FirstOrDefault();
                    await repository.PersistFrontierSnapshotAsync(new DecisionFrontierSnapshotPersistence(
                        Guid.NewGuid(), decisionSessionId, tenantId, userId, recompetitionId,
                        (decimal)result.CurrentEntropy, (decimal)result.CurrentMargin, openFrontier.Count,
                        top?.DecisionBranchId, top?.InformationValue ?? 0m,
                        JsonSerializer.Serialize(openFrontier.Select(b => new { b.BranchCode, b.InformationValue, b.FlipPotential }))), cancellationToken);
                    audit.Add($"Frontier recalculated: {openFrontier.Count} open branch(es) remain.");
                }

                // 6) Outcome-directed ResearchNeed from the highest-IV frontier (bounded per session).
                var researchCount = await repository.CountResearchNeedsAsync(tenantId, decisionSessionId, cancellationToken);
                if (researchCount < v21.LoopMaxResearchActions)
                {
                    var need = DecisionResearchNeedFactory.Create(
                        result.Branches, impact, decisionSessionId, tenantId, userId, session.MatterId, dependencyEventId);
                    if (need is not null)
                    {
                        await repository.PersistResearchNeedAsync(need, cancellationToken);
                        researchNeedDto = new DecisionResearchNeedDto(
                            need.DecisionResearchNeedId, need.DecisionBranchId, need.IssueLabel, need.PropositionToResolve,
                            need.AuthorityKind, need.RequiredEvidenceKind, need.WhyDecisionRelevant, need.ExpectedDiscrimination,
                            need.CurrentUncertainty, need.InformationValue, need.FalsificationCondition, need.StatusCode);
                        audit.Add($"Next investigation selected: {need.IssueLabel}.");
                    }
                }
                else
                {
                    audit.Add("Research action budget for this session is exhausted; no new ResearchNeed generated.");
                }
            }
            else
            {
                audit.Add("Impact produced no signals that map to authoritative branches/candidates; no recompetition run.");
            }
        }
        else if (!impact.RecompetitionRequired)
        {
            audit.Add("Dependency change did not require a recompetition (no essential dependency crossed a threshold).");
        }

        var decision = await GetSessionResultAsync(tenantId, decisionSessionId, cancellationToken)
            ?? throw new InvalidOperationException("Session result unavailable after closed-loop execution.");
        decision = decision with { LastRecompetition = recompetitionDto, PendingResearchNeed = researchNeedDto };

        return new DecisionClosedLoopResultDto(
            decisionSessionId, Applied: true, AlreadyProcessed: false,
            impact, recompetitionDto, researchNeedDto, decision, audit);
    }

    private async Task<DecisionModelRouteDto> ResolveRouteAsync(string? modelCode, CancellationToken cancellationToken)
    {
        var routes = await repository.GetModelRoutesAsync(cancellationToken);
        if (routes.Count == 0)
            throw new InvalidOperationException("No active AI routes are configured in POLOXI.Legal_DecisionModelRoute.");
        if (!string.IsNullOrWhiteSpace(modelCode))
        {
            var match = routes.Where(r => string.Equals(r.ModelCode, modelCode, StringComparison.OrdinalIgnoreCase)).OrderBy(r => r.Priority).FirstOrDefault();
            if (match is not null)
                return match;
        }
        return routes.OrderBy(r => r.Priority).First();
    }

    private static IReadOnlyList<ProposedCandidate> ParseProposal(string json, int maxCandidates)
    {
        var results = new List<ProposedCandidate>();
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("candidates", out var candidatesNode) || candidatesNode.ValueKind != JsonValueKind.Array)
                return results;
            foreach (var c in candidatesNode.EnumerateArray())
            {
                var branches = new List<ProposedBranch>();
                if (c.TryGetProperty("branches", out var branchNode) && branchNode.ValueKind == JsonValueKind.Array)
                    foreach (var b in branchNode.EnumerateArray())
                        branches.Add(ParseBranch(b));
                results.Add(new ProposedCandidate(
                    GetString(c, "displayName"), GetString(c, "outcome"),
                    GetNumber(c, "legalSupport"), GetNumber(c, "factSupport"), GetNumber(c, "evidenceSupport"),
                    GetNumber(c, "authoritySupport"), GetNumber(c, "verification"),
                    GetNumber(c, "discrimination"), GetNumber(c, "rankingImpact"), branches));
                if (results.Count >= maxCandidates)
                    break;
            }
        }
        catch (JsonException)
        {
            // Non-JSON proposal ⇒ no candidates; the caller surfaces this as a failed proposal.
        }
        return results;
    }

    // Recursively parses a proposed branch and any nested sub-branches. The discovery LLM may return
    // a coarse branch with a "subBranches" (or "branches") array of decisive sub-questions; these feed
    // the bounded adaptive-deepening pass. When absent, Children is empty and nothing deepens.
    private static ProposedBranch ParseBranch(JsonElement b)
    {
        var children = new List<ProposedBranch>();
        if ((b.TryGetProperty("subBranches", out var childNode) || b.TryGetProperty("branches", out childNode))
            && childNode.ValueKind == JsonValueKind.Array)
            foreach (var child in childNode.EnumerateArray())
                children.Add(ParseBranch(child));
        return new ProposedBranch(
            GetString(b, "displayName"), GetString(b, "interpretation"),
            GetNumber(b, "decisionRelevance"), GetNumber(b, "flipPotential"), GetNumber(b, "evidenceAvailability"),
            children);
    }

    private List<DecisionCandidatePersistence> ScoreCandidates(IReadOnlyList<ProposedCandidate> proposal, DecisionCoreSettings settings, Guid sessionId, Guid tenantId, out List<DecisionBranchPersistence> branches, CancellationToken cancellationToken)
    {
        branches = [];
        var scored = new List<DecisionCandidatePersistence>();
        var index = 0;
        foreach (var p in proposal)
        {
            var composite = DecisionCoreMath.CompositeScore(p.LegalSupport, p.FactSupport, p.EvidenceSupport, p.AuthoritySupport, p.Verification);
            var ceiling = DecisionCoreMath.CertaintyCeiling(p.LegalSupport, p.FactSupport, p.EvidenceSupport, p.AuthoritySupport);
            // Redundancy penalty / diversity vs the other proposed candidates (§8).
            var redundancy = proposal.Where(o => !ReferenceEquals(o, p)).Select(o => DecisionCoreMath.Similarity(p.DisplayName + " " + p.Outcome, o.DisplayName + " " + o.Outcome)).DefaultIfEmpty(0d).Max();
            var candidateId = Guid.NewGuid();
            var uncertainty = 1d - p.Verification;
            scored.Add(new DecisionCandidatePersistence(
                candidateId, $"C{index + 1}", p.DisplayName, p.Outcome,
                (decimal)DecisionCoreMath.Clamp01(p.LegalSupport), (decimal)DecisionCoreMath.Clamp01(p.FactSupport),
                (decimal)DecisionCoreMath.Clamp01(p.EvidenceSupport), (decimal)DecisionCoreMath.Clamp01(p.AuthoritySupport),
                (decimal)DecisionCoreMath.Clamp01(p.Verification), (decimal)DecisionCoreMath.Clamp01(uncertainty),
                (decimal)DecisionCoreMath.Clamp01(p.Discrimination), (decimal)DecisionCoreMath.Clamp01(p.RankingImpact),
                (decimal)DecisionCoreMath.Clamp01(1d - redundancy), (decimal)DecisionCoreMath.Clamp01(redundancy),
                (decimal)Math.Min(composite, ceiling), (decimal)ceiling, 0, false, false));

            var branchIndex = 0;
            foreach (var b in p.Branches)
            {
                MaterializeBranch(b, settings, branches, parentBranchId: null, parentCode: $"C{index + 1}", level: 1, sortSeed: branchIndex);
                branchIndex++;
            }
            index++;
        }

        // Rank + mark winner (§28 strongest-losing-side handled by ranking; single pass for the slice).
        var ranked = scored.OrderByDescending(c => c.CompositeScore).ToList();
        for (var i = 0; i < ranked.Count; i++)
            ranked[i] = ranked[i] with { RankOrder = i + 1, IsWinner = i == 0 };
        return ranked;
    }

    // Bounded adaptive deepening (§ deepening loop). Scores a proposed branch, appends it to the flat
    // persistence list, then — only when the branch is genuinely worth deepening — recurses into its
    // proposed sub-branches. The gate is deterministic and mirrors POLOXI frontier semantics:
    //   deepen iff the branch is on the frontier, its FlipPotential >= ThresholdDeepeningFlip, the next
    //   level is still <= MaxDepth, and the LLM actually proposed sub-branches.
    // When no sub-branches were proposed (the common case), this behaves identically to the prior flat
    // single-pass materialization, so existing sessions and golden masters are unaffected.
    internal static void MaterializeBranch(
        ProposedBranch b, DecisionCoreSettings settings, List<DecisionBranchPersistence> branches,
        Guid? parentBranchId, string parentCode, int level, int sortSeed)
    {
        var u = 1d - b.EvidenceAvailability;
        var iv = DecisionCoreMath.InformationValue(settings, u, b.DecisionRelevance, b.FlipPotential, b.EvidenceAvailability, novelty: 1d, redundancyPenalty: 0d);
        const double cost = 1d;
        var adv = DecisionCoreMath.LegalAdv(iv, b.DecisionRelevance, b.FlipPotential, cost);
        var onFrontier = DecisionCoreMath.IsOnFrontier(settings, DecisionBranchStates.Active, b.DecisionRelevance, b.FlipPotential);
        var branchId = Guid.NewGuid();
        var branchCode = $"{parentCode}.B{sortSeed + 1}";
        branches.Add(new DecisionBranchPersistence(
            branchId, parentBranchId, level, branchCode, b.DisplayName, b.Interpretation,
            DecisionBranchStates.Active, (decimal)iv, (decimal)DecisionCoreMath.Clamp01(b.DecisionRelevance),
            (decimal)DecisionCoreMath.Clamp01(b.FlipPotential), (decimal)DecisionCoreMath.Clamp01(b.EvidenceAvailability),
            (decimal)adv, (decimal)cost, onFrontier, onFrontier ? null : "BELOW_FRONTIER_THRESHOLD", sortSeed));

        // Deepening gate: bounded by MaxDepth, driven by frontier membership and flip potential.
        var shouldDeepen = b.Children.Count > 0
            && onFrontier
            && b.FlipPotential >= (double)settings.ThresholdDeepeningFlip
            && level < settings.MaxDepth;
        if (!shouldDeepen)
            return;

        var childIndex = 0;
        foreach (var child in b.Children)
        {
            MaterializeBranch(child, settings, branches, parentBranchId: branchId, parentCode: branchCode, level: level + 1, sortSeed: childIndex);
            childIndex++;
        }
    }


    private async Task<List<DecisionEvidencePersistence>> RetrieveEvidenceAsync(string contextCode, IReadOnlyList<DecisionBranchPersistence> branches, Guid sessionId, Guid tenantId, int maxResults, CancellationToken cancellationToken)
    {
        var evidence = new List<DecisionEvidencePersistence>();
        var target = branches.Where(b => b.IsOnFrontier).OrderByDescending(b => b.AdvScore).FirstOrDefault() ?? branches.OrderByDescending(b => b.AdvScore).FirstOrDefault();
        if (target is null)
            return evidence;
        try
        {
            var objective = string.IsNullOrWhiteSpace(target.Interpretation) ? target.DisplayName : target.Interpretation!;
            var sources = await retriever.RetrieveAsync(new DecisionRetrievalRequest(contextCode, objective, Math.Clamp(maxResults, 1, 10)), cancellationToken);
            foreach (var s in sources)
            {
                // Retrieved source ≠ verified evidence (§14): factors default modestly and remain UNVERIFIED.
                var ev = DecisionCoreMath.EvidenceVerificationValue(0.6, 0.6, 0.5, 0.6, 0.6);
                evidence.Add(new DecisionEvidencePersistence(
                    Guid.NewGuid(), target.DecisionBranchId, s.SourceRef, s.Title, s.Snippet,
                    0.6m, 0.6m, 0.5m, 0.6m, 0.6m, (decimal)ev, "UNVERIFIED"));
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Decision-directed retrieval failed for session {SessionId}; continuing without external evidence.", sessionId);
        }
        return evidence;
    }

    internal static List<DecisionFlipPointPersistence> BuildFlipPoints(IReadOnlyList<DecisionBranchPersistence> branches, IReadOnlyList<DecisionCandidatePersistence> candidates, Guid sessionId, Guid tenantId)
    {
        // FlipsWinner is defined STRICTLY from candidate identity: a branch can only flip the winner
        // if the candidate it belongs to is different from the current winner. A high-flip-potential
        // branch that belongs to the winner itself is important but is NOT a winner flip (it must never
        // render as "Winner: Deny → for Deny"). Never derive FlipsWinner from FlipPotential/ADV/text.
        var winnerCode = candidates.OrderBy(c => c.RankOrder).FirstOrDefault()?.CandidateCode;
        return branches
            .Where(b => b.IsOnFrontier && b.FlipPotential > 0)
            .OrderByDescending(b => b.FlipPotential)
            .Take(6)
            .Select(b =>
            {
                // A branch belongs to a candidate via its code prefix ("<CandidateCode>.<...>").
                var branchCandidateCode = b.BranchCode.Split('.', 2)[0];
                var belongsToWinner = winnerCode is not null
                    && string.Equals(branchCandidateCode, winnerCode, StringComparison.OrdinalIgnoreCase);
                // A flip that changes the winner is, at minimum, a swap of the top two candidates,
                // i.e. an ordinal rank displacement of 1. RankDelta must never be 0 when the winner
                // changes, otherwise "Δrank 0 · flips winner" is self-contradictory. A branch owned by
                // the winner can never be a winner flip regardless of how high its flip potential is.
                var winnerChanges = !belongsToWinner && b.FlipPotential >= 0.5m;
                var rankDelta = winnerChanges ? 1 : 0;
                return new DecisionFlipPointPersistence(
                    Guid.NewGuid(), b.DecisionBranchId,
                    $"Resolving '{b.DisplayName}' could change the outcome.",
                    b.Cost, winnerChanges, rankDelta);
            })
            .ToList();
    }

    // Deterministic confidence ceiling for the natural-language composer. The composer may explain the
    // decision state but must never sound more confident than it. Language such as "clear" or "decisive"
    // is only warranted when the margin is wide AND uncertainty is contained; a narrow margin or high
    // entropy caps the wording at "narrow" regardless of which outcome leads.
    internal static string DescribeConfidence(double margin, double entropy)
    {
        if (margin >= 0.15 && entropy < 0.60)
            return "clear — the leader clearly separates from the alternatives; you may state a firm conclusion";
        if (margin >= 0.10 && entropy < 0.75)
            return "moderate — the leader has a meaningful but not decisive edge; avoid the word 'clear'";
        return "narrow — the current leader holds only a narrow advantage over the competing outcome; do not use words like 'clear', 'decisive', or 'strong'";
    }

    // Terminal-state classifier. Core invariant (§32-34):
    //   RESEARCH_EXHAUSTED ⇒ no executable, sufficiently valuable research action remains.
    // Exhaustion is decided ONLY by whether the frontier still offers ADV above the configured floor.
    internal static (string StatusCode, string TerminalState, string Reason) ResolveTerminalState(DecisionCoreSettings settings, double margin, double entropy, bool frontierOpen, double maxAvailableAdv)
    {
        // Genuinely converged: no critical frontier, a clear margin, and contained uncertainty.
        if (!frontierOpen && margin > 0.10 && entropy < 0.60)
            return (DecisionStatusCodes.DecisionReady, DecisionStatusCodes.DecisionReady, "NO_CRITICAL_FRONTIER_AND_CLEAR_MARGIN");

        // A high-value research action still exists on the frontier: research is NOT exhausted.
        // Emit a provisional (leading-outcome) decision so we compose an honest answer while
        // signalling that the highest-value investigation remains open.
        var executableResearchRemains = frontierOpen && maxAvailableAdv >= settings.ThresholdResearchExhaustionAdv;
        if (executableResearchRemains)
            return (DecisionStatusCodes.ProvisionalDecision, DecisionStatusCodes.ProvisionalDecision, "LEADING_OUTCOME_WITH_OPEN_HIGH_VALUE_FRONTIER");

        // No frontier action clears the value floor ⇒ nothing worthwhile left to investigate.
        if (maxAvailableAdv < settings.ThresholdResearchExhaustionAdv)
            return (DecisionStatusCodes.ResearchExhausted, DecisionStatusCodes.ResearchExhausted, "MAX_AVAILABLE_ADV_BELOW_THRESHOLD");

        // Frontier closed with no high-value action but uncertainty not fully contained: converged single pass.
        return (DecisionStatusCodes.DecisionReady, DecisionStatusCodes.DecisionReady, "CONVERGED_SINGLE_PASS");
    }

    private async Task<string?> ComposeAnswerAsync(DecisionSearchRequest request, DecisionModelRouteDto route, IReadOnlyList<DecisionCandidatePersistence> candidates, IReadOnlyList<DecisionBranchPersistence> branches, IReadOnlyList<DecisionFlipPointPersistence> flipPoints, double margin, double entropy, CancellationToken cancellationToken)
    {
        var answerPrompt = await repository.GetPromptAsync(AnswerPromptCode, cancellationToken);
        if (answerPrompt is null)
            return null;
        var artifact = new
        {
            winner = candidates.FirstOrDefault(c => c.IsWinner)?.DisplayName,
            margin,
            entropy,
            // Authoritative confidence ceiling: the composer must never sound more confident than the
            // structured decision state. This descriptor is derived deterministically from margin and
            // entropy so language like "clear" is only permitted when the numbers actually support it.
            confidence = DescribeConfidence(margin, entropy),
            candidates = candidates.Select(c => new { c.DisplayName, c.Outcome, c.CompositeScore, c.RankOrder }),
            frontier = branches.Where(b => b.IsOnFrontier).Select(b => new { b.DisplayName, b.FlipPotential, b.DecisionRelevance }),
            flipPoints = flipPoints.Select(f => new { f.Description, f.ChangeCost, f.WinnerChanges })
        };
        var user = answerPrompt.UserPromptTemplate
            .Replace("{{ARTIFACT}}", JsonSerializer.Serialize(artifact))
            .Replace("{{QUERY}}", request.Query);
        var result = await aiProvider.GenerateAsync(new DecisionAiRequest(route, "DECISION_ANSWER", answerPrompt.SystemPrompt, user, null, request.CorrelationId), cancellationToken);
        return result.Content;
    }

    private static DecisionSearchResponse BuildResponse(DecisionSessionPersistence p, DecisionNextActionDto? nextAction, IReadOnlyCollection<DecisionReadinessItemDto> readiness,
        bool usedDependencyGraph = false, DecisionV2Result? v2 = null, Features.Intelligence.Decision.DecisionGovernanceVerdictDto? governanceVerdict = null)
        => new(
            p.DecisionSessionId, p.QueryText, p.StatusCode, p.TerminalStateCode, p.TerminationReason,
            p.DepthReached, p.LlmCallCount, p.CandidateEntropy, p.DecisionMargin, p.ContractCompleteness,
            p.FinalAnswer, p.WinnerCandidateId,
            p.Candidates.Select(c => new DecisionCandidateDto(c.DecisionCandidateId, c.CandidateCode, c.DisplayName, c.Outcome, c.LegalSupport, c.FactSupport, c.EvidenceSupport, c.AuthoritySupport, c.Verification, c.Uncertainty, c.Discrimination, c.RankingImpact, c.Diversity, c.RedundancyPenalty, c.CompositeScore, c.DecisionSupportCeiling, c.RankOrder, c.IsWinner, c.IsEliminated)).ToArray(),
            p.Branches.Select(b => new DecisionBranchDto(b.DecisionBranchId, b.ParentDecisionBranchId, b.LevelNumber, b.BranchCode, b.DisplayName, b.Interpretation, b.BranchStateCode, b.InformationValue, b.DecisionRelevance, b.FlipPotential, b.EvidenceAvailability, b.AdvScore, b.IsOnFrontier, b.StopReason, b.SortOrder)).ToArray(),
            p.Evidence.Select(e => new DecisionEvidenceDto(e.DecisionEvidenceId, e.DecisionBranchId, e.SourceRef, e.SourceTitle, e.Snippet, e.VerificationValue, e.VerificationStatus)).ToArray(),
            p.FlipPoints.Select(f => new DecisionFlipPointDto(f.DecisionFlipPointId, f.DecisionBranchId, f.Description, f.ChangeCost, f.WinnerChanges, f.RankDelta)).ToArray(),
            p.DurationMs)
        {
            ClarificationQuestion = p.ClarificationQuestion,
            ClarificationTarget = p.ClarificationTarget,
            MatterId = p.MatterId,
            CounterfactualAssumption = p.CounterfactualAssumption,
            NextBestAction = nextAction,
            Readiness = readiness,
            UsedDependencyGraph = usedDependencyGraph,
            GraphNodes = v2?.Nodes ?? [],
            GraphEdges = v2?.Edges ?? [],
            LosingSideTest = v2?.LosingSideTest,
            ReadinessVerdict = v2?.ReadinessVerdict,
            GovernanceVerdict = governanceVerdict
        };

    // ── Next Best Action: pick the open (frontier / active) branch with the highest information value.
    // Impact class scales with flip potential — a branch that can overturn the winner is VERY HIGH. ──
    private static DecisionNextActionDto? BuildNextBestAction(IReadOnlyCollection<DecisionBranchPersistence> branches, string statusCode)
    {
        var candidate = branches
            .Where(b => b.IsOnFrontier || string.Equals(b.BranchStateCode, DecisionBranchStates.Active, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(b => b.InformationValue)
            .ThenByDescending(b => b.FlipPotential)
            .FirstOrDefault();

        // When the decision is ready, or nothing remains on the frontier, surface a
        // confirm/monitor action instead of hiding the panel so the attorney always has a
        // clear next step (e.g. lock in the conclusion, monitor for new facts).
        if (statusCode == DecisionStatusCodes.DecisionReady || candidate is null)
            return new DecisionNextActionDto(
                "Confirm and monitor the current conclusion",
                "LOW",
                "The decision is ready: no open item on the frontier can currently overturn the winner. Lock in the conclusion and monitor for new facts or authority that could reopen the analysis.",
                candidate?.DecisionBranchId,
                candidate?.InformationValue ?? 0m,
                candidate?.FlipPotential ?? 0m);

        var impact = candidate.FlipPotential switch
        {
            >= 0.66m => "VERY HIGH",
            >= 0.40m => "HIGH",
            >= 0.20m => "MEDIUM",
            _ => "LOW"
        };
        var rationale = candidate.FlipPotential >= 0.40m
            ? $"Resolving '{candidate.DisplayName}' could overturn the current winner; it has the highest remaining information value on the decision frontier."
            : $"'{candidate.DisplayName}' carries the highest remaining information value and best reduces residual uncertainty.";
        return new DecisionNextActionDto(
            $"Investigate {candidate.DisplayName}",
            impact,
            rationale,
            candidate.DecisionBranchId,
            candidate.InformationValue,
            candidate.FlipPotential);
    }

    // ── Decision readiness checklist: shown separately from outcome strength. Each item is derived
    // deterministically from the current authoritative state (not an LLM opinion). ──
    private static IReadOnlyCollection<DecisionReadinessItemDto> BuildReadiness(
        IReadOnlyCollection<DecisionCandidatePersistence> candidates,
        IReadOnlyCollection<DecisionBranchPersistence> branches,
        IReadOnlyCollection<DecisionEvidencePersistence> evidence,
        double margin, double entropy, string statusCode)
    {
        var winner = candidates.OrderBy(c => c.RankOrder).FirstOrDefault();
        var alternative = candidates.OrderBy(c => c.RankOrder).Skip(1).FirstOrDefault();
        var verifiedEvidence = evidence.Count(e => e.VerificationValue >= 0.5m);
        // Single authoritative high-impact frontier count (POLOXI owns the frontier). V2 readiness
        // consumes this same definition so the two panels can never disagree.
        var openFrontier = CountHighImpactFrontier(branches);
        // Winner-separation is decided deterministically on the UNROUNDED margin against a single
        // authoritative threshold. The detail line exposes higher precision so a value that rounds to
        // "0.05" in a two-decimal display can never look like it contradicts the separation label
        // (e.g. 0.0497 vs 0.052 both display as "0.05" but sit on opposite sides of the threshold).
        const double separationThreshold = 0.05;
        var marginSeparates = margin >= separationThreshold;
        var uncertaintyContained = entropy < 0.85;
        return new[]
        {
            new DecisionReadinessItemDto("A leading outcome is identified", winner is not null, winner?.DisplayName),
            new DecisionReadinessItemDto(
                marginSeparates ? "Winner separates from the alternative" : "Winner does not clearly separate from the alternative",
                marginSeparates, $"Decision margin {margin:0.####} (threshold {separationThreshold:0.####})"),
            new DecisionReadinessItemDto(
                uncertaintyContained ? "Uncertainty is contained" : "Uncertainty is not contained",
                uncertaintyContained, $"Candidate entropy {entropy:0.####}"),
            new DecisionReadinessItemDto("Strongest opposition considered", alternative is not null, alternative?.DisplayName),
            new DecisionReadinessItemDto("Supporting evidence verified", verifiedEvidence > 0, $"{verifiedEvidence} verified source(s)"),
            new DecisionReadinessItemDto(
                openFrontier == 0 ? "No high-impact unresolved dependency" : $"{openFrontier} high-impact unresolved dependency",
                openFrontier == 0,
                openFrontier == 0 ? null : "Resolve open frontier branches before final reliance")
        };
    }

    // The single authoritative "high-impact open frontier" definition. POLOXI owns the decision
    // frontier; both the ordinary readiness panel and the V2 dependency-readiness gate consume this
    // same count so they can never independently reconstruct (and disagree about) frontier state.
    internal static int CountHighImpactFrontier(IReadOnlyCollection<DecisionBranchPersistence> branches)
        => branches.Count(b => b.IsOnFrontier && b.FlipPotential >= 0.40m);

    // ── POLOXI Legal V2 — dependency-aware decision graph orchestration ──────────────────────────
    // Proposes a typed graph (DECISION_GRAPH), runs an INDEPENDENT verifier (DECISION_VERIFY),
    // propagates invalidation deterministically in Core, runs the strongest-losing-side gate, and
    // computes the dependency-constrained readiness verdict. Everything scored/decided here is Core.
    // Builds a compact posture context line from the structured matter posture fields, injected
    // into the DECISION_GRAPH proposal so the typed graph reflects the decision being asked NOW.
    private static string BuildPostureContext(DecisionSearchRequest request)
    {
        var parts = new List<string>(2);
        if (!string.IsNullOrWhiteSpace(request.Posture))
            parts.Add($"Procedural posture: {request.Posture.Trim()}.");
        if (!string.IsNullOrWhiteSpace(request.MotionTarget))
            parts.Add($"Motion target: {request.MotionTarget.Trim()}.");
        return parts.Count == 0 ? string.Empty : "Decision posture context — " + string.Join(" ", parts);
    }

    private async Task<DecisionV2Result> RunDependencyGraphAsync(
        DecisionSearchRequest request,
        DecisionModelRouteDto route,
        Guid sessionId,
        string contextCode,
        string effectiveQuery,
        IReadOnlyList<DecisionCandidatePersistence> candidates,
        IReadOnlyList<DecisionBranchPersistence> branches,
        DecisionV2Settings v2Settings,
        DecisionCandidatePersistence? winner,
        CancellationToken cancellationToken)
    {
        // 1. Graph proposal (LLM proposes typed nodes/edges; Core assigns identity and owns state).
        var graphPrompt = await repository.GetPromptAsync(GraphPromptCode, cancellationToken)
            ?? throw new InvalidOperationException($"The '{GraphPromptCode}' decision prompt is not configured.");
        var candidateArtifact = JsonSerializer.Serialize(new
        {
            candidates = candidates.Select(c => new { code = c.CandidateCode, c.DisplayName, c.Outcome, c.CompositeScore }),
            branches = branches.Select(b => new { b.BranchCode, b.DisplayName, b.Interpretation, b.FlipPotential })
        });
        // Structured posture context (from the matter) grounds the proposal in the decision being
        // asked NOW: which motion, and what it targets. Appended only; empty when not supplied.
        var postureContext = BuildPostureContext(request);
        var graphQuery = string.IsNullOrEmpty(postureContext) ? effectiveQuery : $"{effectiveQuery}\n\n{postureContext}";
        var graphUser = graphPrompt.UserPromptTemplate
            .Replace("{{QUERY}}", graphQuery)
            .Replace("{{CONTEXT}}", contextCode)
            .Replace("{{ARTIFACT}}", candidateArtifact);
        var graphResult = await aiProvider.GenerateAsync(
            new DecisionAiRequest(route, "DECISION_GRAPH", graphPrompt.SystemPrompt, graphUser, graphPrompt.OutputSchemaJson, request.CorrelationId),
            cancellationToken);

        var (model, nodeCodeToId, edgeCodeToId) = ParseGraphProposal(graphResult.StructuredOutputJson ?? graphResult.Content, candidates);
        if (model.Nodes.Count == 0)
            throw new InvalidOperationException("The V2 graph proposal returned no nodes.");

        // 2. Independent verification: a distinct role assesses each edge; Core applies the verdicts.
        var verifyPrompt = await repository.GetPromptAsync(VerifyPromptCode, cancellationToken)
            ?? throw new InvalidOperationException($"The '{VerifyPromptCode}' decision prompt is not configured.");
        var edgeArtifact = JsonSerializer.Serialize(new
        {
            edges = model.Edges.Select(e => new
            {
                edgeCode = edgeCodeToId.First(kv => kv.Value == e.Id).Key,
                e.Relation,
                source = NodeLabel(model, e.SourceId),
                target = NodeLabel(model, e.TargetId),
                e.IsEssential,
                e.IsDispositive
            })
        });
        var verifyUser = verifyPrompt.UserPromptTemplate
            .Replace("{{ARTIFACT}}", edgeArtifact)
            .Replace("{{QUERY}}", effectiveQuery);
        var verifyResult = await aiProvider.GenerateAsync(
            new DecisionAiRequest(route, "DECISION_VERIFY", verifyPrompt.SystemPrompt, verifyUser, verifyPrompt.OutputSchemaJson, request.CorrelationId),
            cancellationToken);
        ApplyVerification(verifyResult.StructuredOutputJson ?? verifyResult.Content, model, edgeCodeToId);

        // 3. Deterministic Core: recompute node support, then propagate any invalidation downstream.
        foreach (var node in model.Nodes.Values)
            DecisionGraph.RecomputeSupport(model, node);
        DecisionGraph.PropagateInvalidation(model, v2Settings.PropagationMaxDepth);

        // 4. Strongest-losing-side gate: winner must survive the strongest permissible opposing candidate.
        var losingSide = BuildLosingSideTest(candidates, winner);

        // 5. Dependency-constrained readiness verdict (hard gate; margin/entropy are not inputs).
        var openHighImpact = CountHighImpactFrontier(branches);
        var verdict = DecisionGraph.EvaluateReadiness(
            model,
            winnerExists: winner is not null,
            authorityVerifiedFractionRequired: v2Settings.ReadinessMinAuthorityVerified,
            maxHighImpactFrontier: v2Settings.ReadinessMaxHighImpactFrontier,
            openHighImpactFrontierCount: openHighImpact,
            losingSide: losingSide,
            losingSideMargin: v2Settings.ReadinessLosingSideMargin);

        // 6. Map the working model back to persistence + DTOs.
        var nodeSnapshots = model.Nodes.Values
            .OrderBy(n => n.Kind).ThenBy(n => n.SortOrder)
            .Select(n => new DecisionGraphNodePersistence(
                n.Id, n.Kind, n.Code, n.DisplayName, n.Statement, (decimal)n.Support,
                n.IsEssential, n.IsSatisfied, n.VerificationStatus, n.SortOrder)
            {
                CandidateId = n.Kind == DecisionGraphNodeKinds.Strategy ? ResolveStrategyCandidate(n, candidates) : null
            })
            .ToArray();
        var edgeSnapshots = model.Edges
            .Select(e => new DecisionGraphEdgePersistence(
                e.Id, e.Relation, e.SourceKind, e.SourceId, e.TargetKind, e.TargetId,
                (decimal)e.SupportWeight, (decimal)e.Materiality, e.IsEssential, e.IsDispositive,
                e.VerificationStatus, null, e.PropagatedStateCode))
            .ToArray();

        DecisionLosingSideTestPersistence? losingPersistence = losingSide is null ? null : new(
            Guid.NewGuid(), winner?.DecisionCandidateId,
            losingSide.ChallengerCandidateId, losingSide.StrongestCaseSummary,
            losingSide.ChallengerStrength, losingSide.WinnerStrength, losingSide.WinnerSurvived);

        var blockersJson = JsonSerializer.Serialize(verdict.Blockers);
        var persistence = new DecisionGraphPersistence(
            sessionId, request.TenantId, request.UserId, verdict.Satisfied, blockersJson,
            nodeSnapshots, edgeSnapshots, losingPersistence);

        var nodeDtos = nodeSnapshots
            .Select(n => new DecisionGraphNodeDto(n.NodeId, n.NodeKind, n.NodeCode, n.DisplayName, n.Statement, n.Support, n.IsEssential, n.IsSatisfied, n.VerificationStatus, n.SortOrder))
            .ToArray();
        var edgeDtos = edgeSnapshots
            .Select(e => new DecisionGraphEdgeDto(e.EdgeId, e.RelationCode, e.SourceNodeKind, e.SourceNodeId, e.TargetNodeKind, e.TargetNodeId, e.SupportWeight, e.Materiality, e.IsEssential, e.IsDispositive, e.VerificationStatus, e.VerificationNotes, e.PropagatedStateCode))
            .ToArray();

        return new DecisionV2Result(persistence, nodeDtos, edgeDtos, losingSide, verdict);
    }

    // Parse the DECISION_GRAPH proposal into a Core working model, assigning deterministic ids.
    private static (DecisionGraph.Model Model, Dictionary<string, Guid> NodeCodeToId, Dictionary<string, Guid> EdgeCodeToId) ParseGraphProposal(string json, IReadOnlyList<DecisionCandidatePersistence> candidates)
    {
        var model = new DecisionGraph.Model();
        var nodeCodeToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        var edgeCodeToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

        // Seed candidate nodes so strategy→candidate edges have a valid target.
        var candidateCodeToId = new Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
        foreach (var c in candidates)
        {
            model.Nodes[c.DecisionCandidateId] = new DecisionGraph.Node
            {
                Id = c.DecisionCandidateId,
                Kind = DecisionGraphNodeKinds.Candidate,
                Code = c.CandidateCode,
                DisplayName = c.DisplayName,
                Support = (double)c.CompositeScore,
                IsSatisfied = c.IsWinner,
                VerificationStatus = DecisionVerificationStates.Unverified
            };
            candidateCodeToId[c.CandidateCode] = c.DecisionCandidateId;
            nodeCodeToId[c.CandidateCode] = c.DecisionCandidateId;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            var sort = 0;
            if (root.TryGetProperty("nodes", out var nodesNode) && nodesNode.ValueKind == JsonValueKind.Array)
                foreach (var n in nodesNode.EnumerateArray())
                {
                    var code = GetString(n, "code");
                    if (string.IsNullOrWhiteSpace(code) || nodeCodeToId.ContainsKey(code))
                        continue;
                    var kind = MapNodeKind(GetString(n, "kind"));
                    if (kind is null)
                        continue;
                    var id = Guid.NewGuid();
                    model.Nodes[id] = new DecisionGraph.Node
                    {
                        Id = id,
                        Kind = kind,
                        Code = code,
                        DisplayName = string.IsNullOrWhiteSpace(GetString(n, "displayName")) ? code : GetString(n, "displayName"),
                        Statement = GetString(n, "statement"),
                        Support = DecisionCoreMath.Clamp01(GetNumber(n, "support")),
                        IsEssential = GetBool(n, "isEssential"),
                        VerificationStatus = DecisionVerificationStates.Unverified,
                        SortOrder = sort++
                    };
                    nodeCodeToId[code] = id;
                }

            if (root.TryGetProperty("edges", out var edgesNode) && edgesNode.ValueKind == JsonValueKind.Array)
                foreach (var e in edgesNode.EnumerateArray())
                {
                    var sourceCode = GetString(e, "sourceCode");
                    var targetCode = GetString(e, "targetCode");
                    if (!nodeCodeToId.TryGetValue(sourceCode, out var sourceId) || !nodeCodeToId.TryGetValue(targetCode, out var targetId))
                        continue;
                    var relation = MapRelation(GetString(e, "relation"));
                    if (relation is null)
                        continue;
                    var id = Guid.NewGuid();
                    model.Edges.Add(new DecisionGraph.Edge
                    {
                        Id = id,
                        Relation = relation,
                        SourceKind = model.Nodes[sourceId].Kind,
                        SourceId = sourceId,
                        TargetKind = model.Nodes[targetId].Kind,
                        TargetId = targetId,
                        SupportWeight = DecisionCoreMath.Clamp01(GetNumberOrDefault(e, "supportWeight", 0.6)),
                        Materiality = DecisionCoreMath.Clamp01(GetNumberOrDefault(e, "materiality", 0.5)),
                        IsEssential = GetBool(e, "isEssential"),
                        IsDispositive = GetBool(e, "isDispositive"),
                        VerificationStatus = DecisionVerificationStates.Unverified
                    });
                    edgeCodeToId[$"{sourceCode}->{targetCode}"] = id;
                }
        }
        catch (JsonException)
        {
            // Malformed proposal ⇒ empty graph; the caller treats this as a V2 failure and falls back.
        }

        return (model, nodeCodeToId, edgeCodeToId);
    }

    private static void ApplyVerification(string json, DecisionGraph.Model model, Dictionary<string, Guid> edgeCodeToId)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("assessments", out var assessments) || assessments.ValueKind != JsonValueKind.Array)
                return;
            foreach (var a in assessments.EnumerateArray())
            {
                var edgeCode = GetString(a, "edgeCode");
                if (!edgeCodeToId.TryGetValue(edgeCode, out var edgeId))
                    continue;
                var status = GetString(a, "status").Trim().ToUpperInvariant();
                var edge = model.Edges.FirstOrDefault(e => e.Id == edgeId);
                if (edge is null)
                    continue;
                edge.VerificationStatus = status == DecisionVerificationStates.Invalidated
                    ? DecisionVerificationStates.Invalidated
                    : DecisionVerificationStates.Verified;
            }
        }
        catch (JsonException)
        {
            // No verification opinion ⇒ edges stay UNVERIFIED; readiness will block on material authority.
        }
    }

    // Strongest-losing-side: the strongest non-winner candidate becomes the challenger; the winner
    // survives only if its composite strength exceeds the challenger's (Core owns the comparison).
    private static DecisionLosingSideTestDto? BuildLosingSideTest(IReadOnlyList<DecisionCandidatePersistence> candidates, DecisionCandidatePersistence? winner)
    {
        if (winner is null)
            return null;
        var challenger = candidates
            .Where(c => c.DecisionCandidateId != winner.DecisionCandidateId)
            .OrderByDescending(c => c.CompositeScore)
            .FirstOrDefault();
        var winnerStrength = winner.CompositeScore;
        var challengerStrength = challenger?.CompositeScore ?? 0m;
        var survived = winnerStrength > challengerStrength;
        var summary = challenger is null
            ? "No opposing candidate; winner is uncontested."
            : $"Strongest opposing outcome '{challenger.DisplayName}' ({challenger.Outcome}); winner leads by {winnerStrength - challengerStrength:0.00}.";
        return new DecisionLosingSideTestDto(challenger?.DecisionCandidateId, summary, challengerStrength, winnerStrength, survived);
    }

    private static string NodeLabel(DecisionGraph.Model model, Guid nodeId)
        => model.Nodes.TryGetValue(nodeId, out var n) ? $"{n.Kind}:{n.DisplayName}" : nodeId.ToString();

    private static Guid? ResolveStrategyCandidate(DecisionGraph.Node strategy, IReadOnlyList<DecisionCandidatePersistence> candidates)
        => null;

    private static string? MapNodeKind(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        "fact" => DecisionGraphNodeKinds.Fact,
        "proposition" => DecisionGraphNodeKinds.Proposition,
        "element" => DecisionGraphNodeKinds.Element,
        "strategy" => DecisionGraphNodeKinds.Strategy,
        "burden" => DecisionGraphNodeKinds.Burden,
        "procedure" => DecisionGraphNodeKinds.Procedure,
        _ => null
    };

    private static string? MapRelation(string relation) => relation.Trim().ToUpperInvariant() switch
    {
        DecisionGraphRelations.Supports => DecisionGraphRelations.Supports,
        DecisionGraphRelations.Requires => DecisionGraphRelations.Requires,
        DecisionGraphRelations.Satisfies => DecisionGraphRelations.Satisfies,
        DecisionGraphRelations.Establishes => DecisionGraphRelations.Establishes,
        DecisionGraphRelations.DependsOn => DecisionGraphRelations.DependsOn,
        DecisionGraphRelations.Contradicts => DecisionGraphRelations.Contradicts,
        _ => null
    };

    private static bool GetBool(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.True;

    private static double GetNumberOrDefault(JsonElement element, string name, double fallback)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : fallback;

    private sealed record DecisionV2Result(
        DecisionGraphPersistence Persistence,
        IReadOnlyCollection<DecisionGraphNodeDto> Nodes,
        IReadOnlyCollection<DecisionGraphEdgeDto> Edges,
        DecisionLosingSideTestDto? LosingSideTest,
        DecisionReadinessVerdictDto ReadinessVerdict);

    private static string GetString(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static double GetNumber(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var number) ? number : 0d;

    private sealed record ProposedCandidate(string DisplayName, string Outcome, double LegalSupport, double FactSupport, double EvidenceSupport, double AuthoritySupport, double Verification, double Discrimination, double RankingImpact, IReadOnlyList<ProposedBranch> Branches);
    internal sealed record ProposedBranch(string DisplayName, string Interpretation, double DecisionRelevance, double FlipPotential, double EvidenceAvailability, IReadOnlyList<ProposedBranch> Children);
}
