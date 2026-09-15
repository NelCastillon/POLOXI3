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

        // ── Uncertainty / Entropy (uncertainty signal only, not truth) ──────────────────────────
        var orderedScores = candidates.Select(c => (double)c.CompositeScore).OrderByDescending(x => x).ToArray();
        var distribution = DecisionCoreMath.Distribution(orderedScores);
        var entropy = DecisionCoreMath.NormalizedEntropy(distribution);
        var margin = DecisionCoreMath.Margin(orderedScores);

        // ── Decision-Directed Retrieval / Evidence (§13,§14) ────────────────────────────────────
        var evidence = await RetrieveEvidenceAsync(contextCode, branches, sessionId, request.TenantId, request.MaximumResults, cancellationToken);
        Record("EVIDENCE_RETRIEVED", "RETRIEVAL", new { evidenceCount = evidence.Count });

        // ── Frontier + Flip Points (§11,§30) ────────────────────────────────────────────────────
        var flipPoints = BuildFlipPoints(branches, sessionId, request.TenantId);

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
        Record("TERMINAL_STATE", "CONVERGENCE", new { statusCode, terminalState, margin, entropy });

        // ── Answer Assembly (§37): composer reads the structured artifact only ──────────────────
        string? finalAnswer = null;
        if (statusCode == DecisionStatusCodes.DecisionReady || statusCode == DecisionStatusCodes.ResearchExhausted)
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
        if (useGraph)
        {
            try
            {
                v2 = await RunDependencyGraphAsync(request, route, sessionId, contextCode, effectiveQuery,
                    candidates, branches, v2Settings, winner, cancellationToken);
                await repository.PersistGraphAsync(v2.Persistence, cancellationToken);
            }
            catch (Exception ex)
            {
                // V2 is advisory; a graph/verifier failure must never block the V1 decision (§ optional).
                logger.LogWarning(ex, "V2 dependency graph failed for session {SessionId}; returning V1 result.", sessionId);
                v2 = null;
            }
        }

        return BuildResponse(persistence, nextAction, readiness, useGraph, v2);
    }

    // ── Direct LLM answer path (POLOXI Engine off) ──────────────────────────────────────────────
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
            return BuildResponse(session, nextAction, readiness);

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
        return BuildResponse(session, nextAction, readiness, usedDependencyGraph: true, v2);
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
                        branches.Add(new ProposedBranch(
                            GetString(b, "displayName"), GetString(b, "interpretation"),
                            GetNumber(b, "decisionRelevance"), GetNumber(b, "flipPotential"), GetNumber(b, "evidenceAvailability")));
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
                var u = 1d - b.EvidenceAvailability;
                var iv = DecisionCoreMath.InformationValue(settings, u, b.DecisionRelevance, b.FlipPotential, b.EvidenceAvailability, novelty: 1d, redundancyPenalty: 0d);
                const double cost = 1d;
                var adv = DecisionCoreMath.LegalAdv(iv, b.DecisionRelevance, b.FlipPotential, cost);
                var onFrontier = DecisionCoreMath.IsOnFrontier(settings, DecisionBranchStates.Active, b.DecisionRelevance, b.FlipPotential);
                branches.Add(new DecisionBranchPersistence(
                    Guid.NewGuid(), null, 1, $"C{index + 1}.B{branchIndex + 1}", b.DisplayName, b.Interpretation,
                    DecisionBranchStates.Active, (decimal)iv, (decimal)DecisionCoreMath.Clamp01(b.DecisionRelevance),
                    (decimal)DecisionCoreMath.Clamp01(b.FlipPotential), (decimal)DecisionCoreMath.Clamp01(b.EvidenceAvailability),
                    (decimal)adv, (decimal)cost, onFrontier, onFrontier ? null : "BELOW_FRONTIER_THRESHOLD", branchIndex));
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

    private static List<DecisionFlipPointPersistence> BuildFlipPoints(IReadOnlyList<DecisionBranchPersistence> branches, Guid sessionId, Guid tenantId)
        => branches
            .Where(b => b.IsOnFrontier && b.FlipPotential > 0)
            .OrderByDescending(b => b.FlipPotential)
            .Take(6)
            .Select(b => new DecisionFlipPointPersistence(
                Guid.NewGuid(), b.DecisionBranchId,
                $"Resolving '{b.DisplayName}' could change the outcome.",
                b.Cost, b.FlipPotential >= 0.5m, 0))
            .ToList();

    private static (string StatusCode, string TerminalState, string Reason) ResolveTerminalState(DecisionCoreSettings settings, double margin, double entropy, bool frontierOpen, double maxAvailableAdv)
    {
        if (!frontierOpen && margin > 0.10 && entropy < 0.60)
            return (DecisionStatusCodes.DecisionReady, DecisionStatusCodes.DecisionReady, "NO_CRITICAL_FRONTIER_AND_CLEAR_MARGIN");
        if (maxAvailableAdv < settings.ThresholdResearchExhaustionAdv)
            return (DecisionStatusCodes.ResearchExhausted, DecisionStatusCodes.ResearchExhausted, "MAX_AVAILABLE_ADV_BELOW_THRESHOLD");
        if (entropy >= 0.85)
            return (DecisionStatusCodes.ResearchExhausted, DecisionStatusCodes.ResearchExhausted, "HIGH_CANDIDATE_ENTROPY");
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

    private static DecisionSearchResponse BuildResponse(DecisionSessionPersistence p, DecisionNextActionDto? nextAction, IReadOnlyCollection<DecisionReadinessItemDto> readiness, bool usedDependencyGraph = false, DecisionV2Result? v2 = null)
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
            ReadinessVerdict = v2?.ReadinessVerdict
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
        var openFrontier = branches.Count(b => b.IsOnFrontier);
        return new[]
        {
            new DecisionReadinessItemDto("A leading outcome is identified", winner is not null, winner?.DisplayName),
            new DecisionReadinessItemDto("Winner separates from the alternative", margin >= 0.05, $"Decision margin {margin:0.###}"),
            new DecisionReadinessItemDto("Uncertainty is contained", entropy < 0.85, $"Candidate entropy {entropy:0.###}"),
            new DecisionReadinessItemDto("Strongest opposition considered", alternative is not null, alternative?.DisplayName),
            new DecisionReadinessItemDto("Supporting evidence verified", verifiedEvidence > 0, $"{verifiedEvidence} verified source(s)"),
            new DecisionReadinessItemDto(
                openFrontier == 0 ? "No high-impact unresolved dependency" : $"{openFrontier} high-impact unresolved dependency",
                openFrontier == 0,
                openFrontier == 0 ? null : "Resolve open frontier branches before final reliance")
        };
    }

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
        var openHighImpact = branches.Count(b => b.IsOnFrontier && b.FlipPotential >= 0.40m);
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
    private sealed record ProposedBranch(string DisplayName, string Interpretation, double DecisionRelevance, double FlipPotential, double EvidenceAvailability);
}
