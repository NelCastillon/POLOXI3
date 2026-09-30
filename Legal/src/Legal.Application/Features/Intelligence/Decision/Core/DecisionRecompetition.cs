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
        // candidate that owns each affected branch — branch code prefix "C{n}."). Each bundle keeps the
        // legacy UNTARGETED sum (TargetSignal == null) separate from per-dimension TARGETED sums so a
        // typed Evidence/Authority/etc. signal moves ONLY its dimension. Summation happens before a
        // single Clamp01 (deterministic and order-independent), preserving idempotency.
        var candidateDelta = new Dictionary<Guid, DeltaBundle>();
        var branchDelta = new Dictionary<Guid, DeltaBundle>();
        var reopenBranchIds = new HashSet<Guid>();

        foreach (var s in signals)
        {
            if (s.BranchId is { } bid)
            {
                Accumulate(branchDelta, bid, s);
                if (s.ReopenRequested && reopenAllowedBranchIds.Contains(bid))
                    reopenBranchIds.Add(bid);
            }
            if (s.CandidateId is { } cid)
                Accumulate(candidateDelta, cid, s);
        }

        // Fold branch deltas into their owning candidate by branch-code prefix (C{n}.Bxx).
        var candidateByCode = candidates.ToDictionary(c => c.CandidateCode, c => c.DecisionCandidateId, StringComparer.OrdinalIgnoreCase);
        foreach (var b in branches)
        {
            if (!branchDelta.TryGetValue(b.DecisionBranchId, out var bundle) || bundle.IsNegligible)
                continue;
            var dot = b.BranchCode.IndexOf('.');
            var code = dot > 0 ? b.BranchCode[..dot] : b.BranchCode;
            if (candidateByCode.TryGetValue(code, out var cid))
                Fold(candidateDelta, cid, bundle);
        }

        // Re-score candidates. The UNTARGETED delta keeps the legacy coupling (Verification + Authority,
        // Evidence at half weight); TARGETED deltas add to only their named dimension. The authoritative
        // composite + ceiling are then recomputed by Core math — unchanged.
        var rescored = new List<DecisionCandidatePersistence>(candidates.Count);
        foreach (var c in candidates)
        {
            var bundle = candidateDelta.TryGetValue(c.DecisionCandidateId, out var b) ? b : DeltaBundle.Empty;

            var legacy = bundle.Untargeted;
            var verification = DecisionCoreMath.Clamp01((double)c.Verification + legacy + bundle.Verification);
            var authority = DecisionCoreMath.Clamp01((double)c.AuthoritySupport + legacy + bundle.Authority);
            var evidence = DecisionCoreMath.Clamp01((double)c.EvidenceSupport + (legacy * 0.5) + bundle.Evidence);
            var fact = DecisionCoreMath.Clamp01((double)c.FactSupport + bundle.Fact);
            var legal = DecisionCoreMath.Clamp01((double)c.LegalSupport + bundle.Legal);
            var composite = DecisionCoreMath.CompositeScore(legal, fact, evidence, authority, verification);
            var ceiling = DecisionCoreMath.CertaintyCeiling(legal, fact, evidence, authority);
            var uncertainty = DecisionCoreMath.Clamp01(1d - verification);
            rescored.Add(c with
            {
                LegalSupport = (decimal)legal,
                FactSupport = (decimal)fact,
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

    // Per-target aggregation of signed deltas. Untargeted (TargetSignal == null) preserves the legacy
    // V+A+E coupling; each dimension field accumulates only its typed signals. Summing before Clamp01
    // keeps recompetition deterministic and order-independent.
    private readonly record struct DeltaBundle(
        double Untargeted, double Verification, double Authority, double Evidence, double Fact, double Legal)
    {
        public static readonly DeltaBundle Empty = default;

        public bool IsNegligible =>
            Math.Abs(Untargeted) < 1e-9 && Math.Abs(Verification) < 1e-9 && Math.Abs(Authority) < 1e-9
            && Math.Abs(Evidence) < 1e-9 && Math.Abs(Fact) < 1e-9 && Math.Abs(Legal) < 1e-9;

        public DeltaBundle Add(DecisionSignalTarget? target, double delta) => target switch
        {
            null => this with { Untargeted = Untargeted + delta },
            DecisionSignalTarget.Verification => this with { Verification = Verification + delta },
            DecisionSignalTarget.Authority => this with { Authority = Authority + delta },
            DecisionSignalTarget.Evidence => this with { Evidence = Evidence + delta },
            DecisionSignalTarget.Fact => this with { Fact = Fact + delta },
            DecisionSignalTarget.Legal => this with { Legal = Legal + delta },
            _ => this with { Untargeted = Untargeted + delta },
        };

        public DeltaBundle Merge(DeltaBundle other) => new(
            Untargeted + other.Untargeted, Verification + other.Verification, Authority + other.Authority,
            Evidence + other.Evidence, Fact + other.Fact, Legal + other.Legal);
    }

    private static void Accumulate(Dictionary<Guid, DeltaBundle> map, Guid key, DecisionBranchSignal signal)
        => map[key] = (map.TryGetValue(key, out var existing) ? existing : DeltaBundle.Empty)
            .Add(signal.TargetSignal, signal.SupportDelta);

    private static void Fold(Dictionary<Guid, DeltaBundle> map, Guid key, DeltaBundle bundle)
        => map[key] = (map.TryGetValue(key, out var existing) ? existing : DeltaBundle.Empty).Merge(bundle);
}
