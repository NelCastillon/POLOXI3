using Legal.Application.Features.Intelligence;

namespace Legal.Application.Features.Intelligence.Decision.Channels;

// ─────────────────────────────────────────────────────────────────────────────────────────────────
// WideChannelSignalScorer — ENFORCED folding of verified channel contributions into the Wide2
// candidate competition.
//
// Channel contributions project (via migration 0368 lineage) into typed DecisionBranchSignals that are
// keyed by DECISION-SESSION GUIDs (DecisionBranchId / DecisionCandidateId). Wide2 lives in a SEPARATE
// identity universe (WideBranchId / WideCandidateId) and has NO DecisionSession, so those GUIDs cannot
// be matched by equality. The ONE stable join across the two universes is the normalized DisplayName:
// the caller supplies the DecisionSession's GUID→DisplayName tables so this scorer can translate each
// signal onto the Wide2 candidate (direct candidate signal) or the Wide2 candidate(s) whose branch
// scores reference that branch (branch signal).
//
// The δ math is NOT reinvented here — DecisionBranchSignal.SupportDelta already carries the signed value
// ChannelScoringFormula produced. This scorer only aggregates those signed deltas per Wide2 candidate,
// folds the net push onto CompositeScore (clamped to [0,1]), and re-ranks. Reopen-only signals (Human
// Intelligence challenges) carry a zero delta and therefore never move a score — they are surfaced as
// transparency, never as a silent mutation.
// ─────────────────────────────────────────────────────────────────────────────────────────────────
public static class WideChannelSignalScorer
{
    // Result of an enforced fold: the re-ranked candidates plus a per-candidate audit of the applied δ.
    public sealed record Result(
        IReadOnlyList<WideCandidateDto> Candidates,
        IReadOnlyList<CandidateAdjustment> Adjustments,
        bool WinnerChanged)
    {
        public bool AnyApplied => Adjustments.Any(a => a.NetDelta != 0.0);
    }

    // One candidate's enforced adjustment: the net signed δ folded in, and the before/after composite.
    public sealed record CandidateAdjustment(
        Guid WideCandidateId,
        string DisplayName,
        double NetDelta,
        decimal PreviousComposite,
        decimal NewComposite,
        int PreviousRank,
        int NewRank,
        int SignalCount);

    // Folds the projected signals onto the candidates. Fail-soft: empty/degenerate inputs return the
    // candidates unchanged with no adjustments so the pipeline behaves exactly as before.
    //
    //   signals                — DecisionBranchSignals projected for the matter's latest decision session.
    //   decisionCandidateNames — DecisionCandidateId → DisplayName (from DecisionSessionPersistence.Candidates).
    //   decisionBranchNames    — DecisionBranchId    → DisplayName (from DecisionSessionPersistence.Branches).
    public static Result Fold(
        IReadOnlyList<WideCandidateDto> candidates,
        IReadOnlyList<DecisionBranchSignal> signals,
        IReadOnlyDictionary<Guid, string> decisionCandidateNames,
        IReadOnlyDictionary<Guid, string> decisionBranchNames)
    {
        if (candidates is null || candidates.Count == 0 || signals is null || signals.Count == 0)
            return new Result(candidates ?? [], [], false);

        // Net signed δ and contributing-signal count per Wide2 candidate (keyed by normalized DisplayName).
        var deltaByName = new Dictionary<string, (double Delta, int Count)>(StringComparer.OrdinalIgnoreCase);

        // Branch DisplayName → the Wide2 candidates whose branch scores reference that branch. A branch
        // signal pushes every candidate that competes on that branch.
        var candidatesByBranchName = new Dictionary<string, List<WideCandidateDto>>(StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            foreach (var branch in candidate.BranchScores)
            {
                if (string.IsNullOrWhiteSpace(branch.BranchDisplayName))
                    continue;
                if (!candidatesByBranchName.TryGetValue(branch.BranchDisplayName, out var list))
                    candidatesByBranchName[branch.BranchDisplayName] = list = [];
                list.Add(candidate);
            }
        }

        var candidateByName = candidates
            .GroupBy(c => c.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var signal in signals)
        {
            // Reopen-only signals (e.g. Human Intelligence challenges) carry no δ: they request POLOXI to
            // re-verify, never silently move a score. Honor that boundary here.
            if (signal.SupportDelta == 0.0)
                continue;

            // Direct candidate signal: translate the decision-candidate GUID to its name, then match the
            // Wide2 candidate by name.
            if (signal.CandidateId is { } candidateId
                && decisionCandidateNames.TryGetValue(candidateId, out var candidateName)
                && candidateByName.ContainsKey(candidateName))
            {
                Accumulate(deltaByName, candidateName, signal.SupportDelta);
                continue;
            }

            // Branch signal: translate the decision-branch GUID to its name, then push every Wide2
            // candidate competing on a branch of that name.
            if (signal.BranchId is { } branchId
                && decisionBranchNames.TryGetValue(branchId, out var branchName)
                && candidatesByBranchName.TryGetValue(branchName, out var affected))
            {
                foreach (var candidate in affected)
                    Accumulate(deltaByName, candidate.DisplayName, signal.SupportDelta);
            }
        }

        if (deltaByName.Count == 0)
            return new Result(candidates, [], false);

        var previousWinnerId = candidates.OrderBy(c => c.RankNumber).First().WideCandidateId;
        var previousRankById = candidates.ToDictionary(c => c.WideCandidateId, c => c.RankNumber);

        // Fold the net δ onto each candidate's composite (clamped to [0,1] — composite is a normalized
        // score). Candidates with no signal keep their original composite untouched.
        var folded = candidates
            .Select(candidate =>
            {
                if (!deltaByName.TryGetValue(candidate.DisplayName, out var acc) || acc.Delta == 0.0)
                    return (Candidate: candidate, Delta: 0.0, Count: 0, NewComposite: candidate.CompositeScore);
                var updated = Clamp01(candidate.CompositeScore + (decimal)acc.Delta);
                return (Candidate: candidate with { CompositeScore = updated }, acc.Delta, acc.Count, NewComposite: updated);
            })
            .ToList();

        // Re-rank by the folded composite (descending); ties keep the prior rank order for determinism.
        var reranked = folded
            .OrderByDescending(x => x.NewComposite)
            .ThenBy(x => previousRankById[x.Candidate.WideCandidateId])
            .Select((x, index) => (x.Candidate, x.Delta, x.Count, NewRank: index + 1))
            .ToList();

        var adjustedCandidates = reranked
            .Select(x => x.Candidate with { RankNumber = x.NewRank })
            .ToList();

        var adjustments = reranked
            .Where(x => x.Delta != 0.0)
            .Select(x => new CandidateAdjustment(
                x.Candidate.WideCandidateId,
                x.Candidate.DisplayName,
                x.Delta,
                PreviousComposite: candidates.First(c => c.WideCandidateId == x.Candidate.WideCandidateId).CompositeScore,
                NewComposite: x.Candidate.CompositeScore,
                PreviousRank: previousRankById[x.Candidate.WideCandidateId],
                NewRank: x.NewRank,
                SignalCount: x.Count))
            .ToList();

        var newWinnerId = adjustedCandidates.OrderBy(c => c.RankNumber).First().WideCandidateId;
        return new Result(adjustedCandidates, adjustments, newWinnerId != previousWinnerId);
    }

    private static void Accumulate(Dictionary<string, (double Delta, int Count)> map, string name, double delta)
    {
        if (map.TryGetValue(name, out var acc))
            map[name] = (acc.Delta + delta, acc.Count + 1);
        else
            map[name] = (delta, 1);
    }

    private static decimal Clamp01(decimal value) => value < 0m ? 0m : value > 1m ? 1m : value;
}
