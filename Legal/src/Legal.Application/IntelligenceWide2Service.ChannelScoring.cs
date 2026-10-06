using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision.Channels;
using Microsoft.Extensions.Logging;

namespace Legal.Application;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// ENFORCED channel-scoring fold for the dynamic Wide2 pipeline.
//
// Verified Decision Channel contributions (Document Evidence, Legal Authority, Human Intelligence,
// Investigation, Decision Contract, External Research — and Media/Machine propositions that converge
// through the same proposition funnel) are persisted as qualitative source-truth and project (via
// migration 0368 lineage) into typed, signed DecisionBranchSignals keyed by DECISION-SESSION GUIDs.
//
// The Wide2 UI pipeline competes a flat candidate universe in its OWN identity space and has no decision
// session, so this adapter bridges the two universes by normalized DisplayName: it loads the matter's
// latest decision session, projects the channel signals for it, builds the session's GUID→DisplayName
// tables, and hands everything to the pure WideChannelSignalScorer to fold the signed δ onto the Wide2
// candidates and re-rank. POLOXI's δ math (ChannelScoringFormula) is the single source of truth — this
// path never invents a number. Fully fail-soft: no projection service, no session, no contributions, or
// any error leaves the candidates exactly as competed.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public sealed partial class IntelligenceWide2Service
{
    private async Task<IReadOnlyCollection<WideCandidateDto>> ApplyChannelContributionsAsync(
        WideSearchRequest request,
        IReadOnlyCollection<WideCandidateDto> candidates,
        CancellationToken cancellationToken)
    {
        // Gate: enforced folding only runs on a legal EVALUATE run that carries a matter and produced a
        // competed candidate pool, and only when the projection service is wired. Anything else is a no-op.
        if (channelProjectionService is null
            || !IsLegalDecisionEvaluationRun
            || candidates is null || candidates.Count == 0
            || request.DecisionMatterId is not { } matterId || matterId == Guid.Empty)
        {
            return candidates ?? [];
        }

        try
        {
            // The channel universe recompetes per DecisionSessionId. Use the matter's authoritative
            // LatestSessionId — the SAME pointer the read-only Channel Scoring LPI endpoint resolves — so
            // the enforced fold and the displayed transparency trace always describe the same session.
            var matter = await legalDecisionRepository.GetMatterAsync(request.TenantId, matterId, cancellationToken);
            if (matter?.LatestSessionId is not { } sessionId)
                return candidates;

            var signals = await channelProjectionService.ProjectForSessionAsync(request.TenantId, sessionId, cancellationToken);
            if (signals.Count == 0)
                return candidates;

            // Build the GUID→DisplayName translation tables from the decision session: the ONLY stable
            // join from the decision-session identity space to the Wide2 candidate/branch display names.
            var session = await legalDecisionRepository.GetSessionAsync(request.TenantId, sessionId, cancellationToken);
            if (session is null)
                return candidates;

            var candidateNames = session.Candidates
                .Where(c => !string.IsNullOrWhiteSpace(c.DisplayName))
                .GroupBy(c => c.DecisionCandidateId)
                .ToDictionary(g => g.Key, g => g.First().DisplayName);
            var branchNames = session.Branches
                .Where(b => !string.IsNullOrWhiteSpace(b.DisplayName))
                .GroupBy(b => b.DecisionBranchId)
                .ToDictionary(g => g.Key, g => g.First().DisplayName);

            var ordered = candidates.OrderBy(c => c.RankNumber).ToList();
            var result = WideChannelSignalScorer.Fold(ordered, signals, candidateNames, branchNames);
            if (!result.AnyApplied)
                return candidates;

            logger.LogInformation(
                "Wide2 enforced channel scoring for matter {MatterId}: {SignalCount} signal(s) folded, {AdjustedCount} candidate(s) adjusted, winner changed: {WinnerChanged}.",
                matterId, signals.Count, result.Adjustments.Count, result.WinnerChanged);
            return result.Candidates;
        }
        catch (Exception ex)
        {
            // Fail-soft: channel folding must never break the decision. Degrade to the unchanged ranking.
            logger.LogWarning(ex, "Wide2 enforced channel scoring failed for matter {MatterId}; delivering unchanged candidate ranking.", matterId);
            return candidates;
        }
    }
}
