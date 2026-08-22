namespace Ams.Application.Features.Intelligence;

// ── V3.0 Evidence-Guided Adaptive Narrowing Policy ──────────────────────────────────────────
// POLOXI's directional objective: reduce the reasoning space, candidate space, and unresolved
// uncertainty as efficiently as possible while preserving the ability to recover from premature
// narrowing. Narrow by default; expand ONLY when newly grounded evidence demonstrates the current
// space may be incomplete; resume narrowing after every expansion.
//
// The LLM proposes; evidence validates; THIS policy decides — every method is deterministic and
// zero-LLM. Encoded invariants:
//   1. No candidate is eliminated solely because of missing evidence (missing evidence ⇒ WATCH ⇒ investigate).
//   2. Hard invalidation requires deterministic constraint evidence (existing PRUNED/constraint-violation path).
//   3. Every expansion requires evidence-based justification (discovery admission gate + budget).
//   4. Resolved branches may reopen if dependent evidence materially changes (reversible uncertainty).
//   5. Candidate-space and reasoning-space changes are independently auditable (transition provenance).
internal static class WideNarrowingPolicy
{
    internal sealed record BranchNarrowingResult(IReadOnlyCollection<Guid> ResolvedBranchIds,IReadOnlyCollection<Guid> ReopenedBranchIds,IReadOnlyCollection<WideNarrowingTransitionDto> Transitions);

    internal sealed record ExpansionResult(IReadOnlyCollection<string> AdmittedNames,IReadOnlyCollection<string> RejectedNames,IReadOnlyCollection<WideNarrowingTransitionDto> Transitions);

    internal sealed record CandidateNarrowingResult(IReadOnlyDictionary<string,string> CandidateStates,IReadOnlyCollection<WideNarrowingTransitionDto> Transitions);

    // Branch narrowing: a branch is RESOLVED (removed from investigation attention, kept in the
    // answer path) only when evidence coverage is adequate AND its remaining measured information
    // value is below the floor — "don't keep investigating Premium". A branch is never resolved
    // for merely lacking evidence. Already-resolved branches REOPEN when their evidence support
    // materially changes (upstream evidence invalidation propagation).
    internal static BranchNarrowingResult EvaluateBranches(
        IReadOnlyCollection<WideBranchRecord> branches,
        IReadOnlyDictionary<Guid,decimal> adjustedInformationValues,
        IReadOnlyDictionary<Guid,decimal> supportAtResolution,
        WideConfiguration configuration)
    {
        var resolved=new List<Guid>();
        var reopened=new List<Guid>();
        var transitions=new List<WideNarrowingTransitionDto>();
        foreach(var branch in branches)
        {
            if(branch.BranchStateCode==WideBranchStates.Pruned)continue;
            if(supportAtResolution.TryGetValue(branch.WideBranchId,out var priorSupport))
            {
                // Invariant 4 — reopen trigger: dependent evidence materially changed.
                if(Math.Abs(branch.EvidenceSupport-priorSupport)>=configuration.NarrowingReopenSupportDelta)
                {
                    reopened.Add(branch.WideBranchId);
                    transitions.Add(new("BRANCH",branch.DisplayName,WideBranchStates.Resolved,branch.BranchStateCode,
                        $"Reopened: evidence support moved {priorSupport:P0} → {branch.EvidenceSupport:P0} (≥ {configuration.NarrowingReopenSupportDelta:P0} reopen delta); prior resolution conclusion is stale."));
                }
                continue;
            }
            if(branch.BranchStateCode is not(WideBranchStates.Active or WideBranchStates.Secondary))continue;
            // Resolution requires BOTH adequate evidence coverage AND a measured low remaining IV.
            // A branch the estimator never scored keeps its attention — absence of an estimate is
            // uncertainty, not resolution.
            if(branch.EvidenceSupport<configuration.NarrowingBranchCoverageFloor)continue;
            if(!adjustedInformationValues.TryGetValue(branch.WideBranchId,out var informationValue))continue;
            if(informationValue>=configuration.NarrowingInformationValueFloor)continue;
            resolved.Add(branch.WideBranchId);
            transitions.Add(new("BRANCH",branch.DisplayName,branch.BranchStateCode,WideBranchStates.Resolved,
                $"Resolved: evidence support {branch.EvidenceSupport:P0} ≥ {configuration.NarrowingBranchCoverageFloor:P0} coverage floor and remaining information value {informationValue:P0} < {configuration.NarrowingInformationValueFloor:P0}; investigation attention shifts elsewhere."));
        }
        return new(resolved,reopened,transitions);
    }

    // Expansion gate (Invariant 3): a newly discovered candidate name enters the universe only when
    // attested by at least the configured number of DISTINCT independent evidence hosts, within the
    // per-round admission budget. Rejected discoveries are recorded — disclosed, never hidden — and
    // may qualify in a later round when more evidence arrives.
    internal static ExpansionResult EvaluateExpansion(
        IReadOnlyCollection<string> discoveredNames,
        IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge,
        WideConfiguration configuration)
    {
        if(discoveredNames.Count==0)return new([],[],[]);
        var attested=discoveredNames
            .Select(name=>(Name:name,Support:CountDistinctHosts(name,knowledge)))
            .OrderByDescending(item=>item.Support)
            .ThenBy(item=>item.Name,StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var admitted=new List<string>();
        var rejected=new List<string>();
        var transitions=new List<WideNarrowingTransitionDto>();
        foreach(var(name,support)in attested)
        {
            if(support>=configuration.NarrowingDiscoveryMinimumSupport&&admitted.Count<configuration.MaximumCandidateAdmissionsPerRound)
            {
                admitted.Add(name);
                transitions.Add(new("CANDIDATE",name,WideCandidateStates.NewlyDiscovered,WideCandidateStates.Admitted,
                    $"Admitted: discovery evidence from {support} distinct hosts ≥ {configuration.NarrowingDiscoveryMinimumSupport} required; joins the next round's competition basis."));
            }
            else
            {
                rejected.Add(name);
                var reason=support<configuration.NarrowingDiscoveryMinimumSupport
                    ?$"Not admitted: only {support} distinct attesting host(s), below the {configuration.NarrowingDiscoveryMinimumSupport}-host discovery threshold. Recorded, not discarded."
                    :$"Not admitted: per-round admission budget ({configuration.MaximumCandidateAdmissionsPerRound}) exhausted. Eligible for a later round.";
                transitions.Add(new("CANDIDATE",name,WideCandidateStates.NewlyDiscovered,WideCandidateStates.DiscoveredNotAdmitted,reason));
            }
        }
        return new(admitted,rejected,transitions);
    }

    // Candidate narrowing: viability, not score alone. A candidate far behind the leader is DEFERRED
    // only when its evidence coverage is adequate — a low score with thin coverage means POLOXI does
    // not know enough yet, so the candidate goes to WATCH (Invariant 1: Missing Evidence ⇒
    // Uncertainty ⇒ Investigate, never Missing Evidence ⇒ Low Score ⇒ Eliminate).
    internal static CandidateNarrowingResult EvaluateCandidates(
        IReadOnlyDictionary<string,decimal> candidateSignals,
        IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge,
        IReadOnlyDictionary<string,string> previousStates,
        WideConfiguration configuration)
    {
        var states=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        var transitions=new List<WideNarrowingTransitionDto>();
        if(candidateSignals.Count==0)return new(states,transitions);
        var leaderSignal=Math.Max(candidateSignals.Values.Max(),.0001m);
        var maxHosts=Math.Max(configuration.NarrowingDiscoveryMinimumSupport,1);
        foreach(var(name,signal)in candidateSignals)
        {
            var hostSupport=CountDistinctHosts(name,knowledge);
            var coverage=Math.Clamp((decimal)hostSupport/maxHosts,0,1);
            var relativeGap=Math.Clamp((leaderSignal-signal)/leaderSignal,0,1);
            string state;
            string reason;
            if(coverage<configuration.NarrowingCandidateCoverageFloor)
            {
                // Invariant 1: insufficient coverage NEVER defers — it demands investigation.
                state=WideCandidateStates.Watch;
                reason=$"Watch: evidence coverage {coverage:P0} below {configuration.NarrowingCandidateCoverageFloor:P0} floor — signal {signal:P0} may be low only because data is missing; investigate, never eliminate.";
            }
            else if(relativeGap>=configuration.NarrowingCandidateScoreGap)
            {
                state=WideCandidateStates.Deferred;
                reason=$"Deferred: {relativeGap:P0} behind the leader with {coverage:P0} evidence coverage and no unresolved high-value gap — uncompetitive on current evidence; reversible if evidence changes.";
            }
            else
            {
                state=WideCandidateStates.Active;
                reason=$"Active: within {configuration.NarrowingCandidateScoreGap:P0} of the leader (gap {relativeGap:P0}) with adequate coverage.";
            }
            states[name]=state;
            var previous=previousStates.GetValueOrDefault(name,WideCandidateStates.Active);
            if(!string.Equals(previous,state,StringComparison.OrdinalIgnoreCase))
                transitions.Add(new("CANDIDATE",name,previous,state,reason));
        }
        return new(states,transitions);
    }

    // Per-round directional trend statement. Precedence: reopening beats expansion beats
    // convergence beats narrowing — the strongest structural event names the round.
    internal static string ComputeTrend(
        int activeBranchesBefore,int activeBranchesAfter,
        int candidatesBefore,int candidatesAfter,
        int reopenedCount,int admittedCount,
        decimal? normalizedEntropyAfter,decimal convergenceTrigger,decimal? actualInformationGain)
    {
        if(reopenedCount>0)return WideNarrowingTrends.Reopened;
        if(admittedCount>0&&candidatesAfter>candidatesBefore)return WideNarrowingTrends.Expansion;
        if(normalizedEntropyAfter is not null&&normalizedEntropyAfter.Value<convergenceTrigger)return WideNarrowingTrends.Converged;
        if(activeBranchesAfter<activeBranchesBefore||candidatesAfter<candidatesBefore||(actualInformationGain??0m)>0m)return WideNarrowingTrends.Narrowing;
        return WideNarrowingTrends.Stable;
    }

    // Distinct independent evidence hosts attesting a name (same qualified-name relaxation as the
    // V2.6.1 evidence-diversity fix: "Raleigh, North Carolina" also matches snippets saying "Raleigh").
    private static int CountDistinctHosts(string candidate,IReadOnlyCollection<WideExternalKnowledgeSnippet> knowledge)
    {
        var primary=candidate.Split(',','(','\u2013','\u2014')[0].Trim();
        var keys=primary.Length>=4&&!string.Equals(primary,candidate,StringComparison.OrdinalIgnoreCase)
            ?new[]{candidate,primary}:[candidate];
        var hosts=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var snippet in knowledge)
        {
            var matched=false;
            foreach(var key in keys)
                if(snippet.Title?.Contains(key,StringComparison.OrdinalIgnoreCase)==true
                    ||snippet.Snippet?.Contains(key,StringComparison.OrdinalIgnoreCase)==true){matched=true;break;}
            if(!matched)continue;
            var host=Uri.TryCreate(snippet.Url,UriKind.Absolute,out var uri)?uri.Host:snippet.Url??string.Empty;
            if(!string.IsNullOrWhiteSpace(host))hosts.Add(host);
        }
        return hosts.Count;
    }
}
