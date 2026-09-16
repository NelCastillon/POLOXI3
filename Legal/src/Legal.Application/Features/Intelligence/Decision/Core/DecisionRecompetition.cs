using Legal.Application.Features.Intelligence.Decision;

namespace Legal.Application.Features.Intelligence.Decision.Core;

// ─────────────────────────────────────────────────────────────────────────────────────────────
// POLOXI Legal V2.1 — Candidate × Branch recompetition (§8, §9, §10, §11, §12).
//
// The ONLY place candidates are (re)scored after a dependency change. POLOXI Core stays authoritative:
// it consumes domain-neutral signals (support deltas + reopen requests keyed by branch/candidate id)
// and re-runs the same composite scoring / entropy / margin / frontier / IV math used on the first
// pass. The graph never assigns a candidate score — it only supplies the deltas.
//
// Deterministic and idempotent: applying the same signal set to the same inputs yields the same
// ranking. Reopen requests move a branch back to ACTIVE (bounded by loop-safety limits, enforced by
// the orchestrator via MaxReopensPerBranch).
// ─────────────────────────────────────────────────────────────────────────────────────────────
public static class DecisionRecompetition
{
    public sealed record Result(
        IReadOnlyList<DecisionCandidatePersistence> Candidates,
        IReadOnlyList<DecisionBranchPersistence> Branches,
        double PreviousEntropy,
        double CurrentEntropy,
        double PreviousMargin,
        double CurrentMargin,
        Guid? PreviousWinnerId,
        Guid? CurrentWinnerId,
        bool WinnerChanged,
        int ReopenedBranchCount);

    public static Result Run(
        IReadOnlyList<DecisionCandidatePersistence> candidates,
        IReadOnlyList<DecisionBranchPersistence> branches,
        IReadOnlyList<DecisionBranchSignal> signals,
        DecisionCoreSettings settings,
        ISet<Guid> reopenAllowedBranchIds)
    {
        var prevOrdered = candidates.OrderByDescending(c => c.CompositeScore).Select(c => (double)c.CompositeScore).ToArray();
        var prevDist = DecisionCoreMath.Distribution(prevOrdered);
        var previousEntropy = DecisionCoreMath.NormalizedEntropy(prevDist);
        var previousMargin = DecisionCoreMath.Margin(prevOrdered);
        var previousWinnerId = candidates.FirstOrDefault(c => c.IsWinner)?.DecisionCandidateId;

        // Aggregate signed support deltas per candidate (via direct candidate signals and via the
        // candidate that owns each affected branch — branch code prefix "C{n}.").
        var candidateDelta = new Dictionary<Guid, double>();
        var branchDelta = new Dictionary<Guid, double>();
        var reopenBranchIds = new HashSet<Guid>();

        foreach (var s in signals)
        {
            if (s.BranchId is { } bid)
            {
                branchDelta[bid] = branchDelta.TryGetValue(bid, out var bd) ? bd + s.SupportDelta : s.SupportDelta;
                if (s.ReopenRequested && reopenAllowedBranchIds.Contains(bid))
                    reopenBranchIds.Add(bid);
            }
            if (s.CandidateId is { } cid)
                candidateDelta[cid] = candidateDelta.TryGetValue(cid, out var cd) ? cd + s.SupportDelta : s.SupportDelta;
        }

        // Fold branch deltas into their owning candidate by branch-code prefix (C{n}.Bxx).
        var candidateByCode = candidates.ToDictionary(c => c.CandidateCode, c => c.DecisionCandidateId, StringComparer.OrdinalIgnoreCase);
        foreach (var b in branches)
        {
            if (!branchDelta.TryGetValue(b.DecisionBranchId, out var d) || Math.Abs(d) < 1e-9)
                continue;
            var dot = b.BranchCode.IndexOf('.');
            var code = dot > 0 ? b.BranchCode[..dot] : b.BranchCode;
            if (candidateByCode.TryGetValue(code, out var cid))
                candidateDelta[cid] = candidateDelta.TryGetValue(cid, out var cd) ? cd + d : d;
        }

        // Re-score candidates. The delta adjusts verification/authority support (the dependency-backed
        // dimensions), then the authoritative composite + ceiling are recomputed by Core math.
        var rescored = new List<DecisionCandidatePersistence>(candidates.Count);
        foreach (var c in candidates)
        {
            var delta = candidateDelta.TryGetValue(c.DecisionCandidateId, out var d) ? d : 0d;
            var verification = DecisionCoreMath.Clamp01((double)c.Verification + delta);
            var authority = DecisionCoreMath.Clamp01((double)c.AuthoritySupport + delta);
            var evidence = DecisionCoreMath.Clamp01((double)c.EvidenceSupport + (delta * 0.5));
            var composite = DecisionCoreMath.CompositeScore((double)c.LegalSupport, (double)c.FactSupport, evidence, authority, verification);
            var ceiling = DecisionCoreMath.CertaintyCeiling((double)c.LegalSupport, (double)c.FactSupport, evidence, authority);
            var uncertainty = DecisionCoreMath.Clamp01(1d - verification);
            rescored.Add(c with
            {
                Verification = (decimal)verification,
                AuthoritySupport = (decimal)authority,
                EvidenceSupport = (decimal)evidence,
                Uncertainty = (decimal)uncertainty,
                CompositeScore = (decimal)Math.Min(composite, ceiling),
                DecisionSupportCeiling = (decimal)ceiling
            });
        }

        var ranked = rescored.OrderByDescending(c => c.CompositeScore).ToList();
        for (var i = 0; i < ranked.Count; i++)
            ranked[i] = ranked[i] with { RankOrder = i + 1, IsWinner = i == 0 };

        var curOrdered = ranked.Select(c => (double)c.CompositeScore).ToArray();
        var curDist = DecisionCoreMath.Distribution(curOrdered);
        var currentEntropy = DecisionCoreMath.NormalizedEntropy(curDist);
        var currentMargin = DecisionCoreMath.Margin(curOrdered);
        var currentWinnerId = ranked.FirstOrDefault()?.DecisionCandidateId;

        // Apply branch reopen + recompute IV/frontier for affected branches.
        var updatedBranches = new List<DecisionBranchPersistence>(branches.Count);
        foreach (var b in branches)
        {
            var state = b.BranchStateCode;
            if (reopenBranchIds.Contains(b.DecisionBranchId))
                state = DecisionBranchStates.Reopened;

            var u = 1d - (double)b.EvidenceAvailability;
            var iv = DecisionCoreMath.InformationValue(settings, u, (double)b.DecisionRelevance, (double)b.FlipPotential, (double)b.EvidenceAvailability, novelty: 1d, redundancyPenalty: 0d);
            var adv = DecisionCoreMath.LegalAdv(iv, (double)b.DecisionRelevance, (double)b.FlipPotential, (double)b.Cost);
            var onFrontier = DecisionCoreMath.IsOnFrontier(settings, state, (double)b.DecisionRelevance, (double)b.FlipPotential);
            updatedBranches.Add(b with
            {
                BranchStateCode = state,
                InformationValue = (decimal)iv,
                AdvScore = (decimal)adv,
                IsOnFrontier = onFrontier,
                StopReason = onFrontier ? null : "BELOW_FRONTIER_THRESHOLD"
            });
        }

        return new Result(
            ranked, updatedBranches,
            previousEntropy, currentEntropy, previousMargin, currentMargin,
            previousWinnerId, currentWinnerId,
            WinnerChanged: previousWinnerId != currentWinnerId,
            ReopenedBranchCount: reopenBranchIds.Count);
    }
}
