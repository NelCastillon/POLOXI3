using Ams.Application.Abstractions.Intelligence;

namespace Ams.Application.Features.Intelligence.Abv;

// POLOXI ABV deterministic governance. The LLM proposes intent; THIS layer decides everything of
// business value from Domain-Pack configuration, with explicit provenance. No fabrication: values
// that the composite/config cannot support remain null. Impact tier is DERIVED from the converged
// composite; urgency/owner/action are resolved from configuration.
public sealed class AbvGovernanceEngine:IAbvGovernanceEngine
{
    public AbvIntent? AcceptIntent(AbvIntentProposal proposal,InterpretationComposite composite,AbvDomainPack pack)
    {
        // Reject any proposal outside the pack taxonomy (the LLM may only choose, never invent).
        var definition=pack.Intents.FirstOrDefault(x=>string.Equals(x.IntentCode,proposal.IntentCode,StringComparison.OrdinalIgnoreCase));
        if(definition is null)return null;
        // Keep only supporting ids that actually exist in the composite (drop hallucinated ids).
        var validIds=new HashSet<string>(composite.Dimensions.Select(d=>d.NodeId),StringComparer.Ordinal);
        var supporting=proposal.SupportingDimensionIds.Where(validIds.Contains).Distinct(StringComparer.Ordinal).ToArray();
        return new(definition.IntentCode,definition.Name,proposal.Rationale,AbvSource.Derived,supporting);
    }

    public AbvImpact ResolveImpact(AbvIntent intent,InterpretationComposite composite,AbvDomainPack pack)
    {
        // Deterministic tier derived from the converged composite's decision weight. EstimatedExposure
        // stays null unless admitted evidence quantified it — POLOXI never manufactures numbers.
        var maxWeight=composite.Dimensions.Count==0?0m:composite.Dimensions.Max(d=>d.Weight);
        var tier=maxWeight>=0.75m?AbvImpactTier.Critical
            :maxWeight>=0.5m?AbvImpactTier.High
            :maxWeight>=0.25m?AbvImpactTier.Medium
            :AbvImpactTier.Low;
        // A metric-at-risk is only admissible when the composite explicitly names it.
        var metric=intent.SupportingDimensionIds
            .Select(id=>composite.Dimensions.FirstOrDefault(d=>d.NodeId==id))
            .FirstOrDefault(d=>d is not null&&!string.IsNullOrWhiteSpace(d.MetricOrObservation))?.MetricOrObservation;
        return new(tier,metric,null,AbvSource.Derived,[]);
    }

    public AbvUrgency ResolveUrgency(AbvIntent intent,AbvImpact impact,AbvDomainPack pack)
    {
        var tierCode=impact.Tier;
        // Prefer an intent-specific policy; fall back to the tier-only policy. SLAs come only from config.
        var rule=pack.UrgencyPolicies
            .Where(p=>p.ImpactTier==tierCode&&string.Equals(p.IntentCode,intent.IntentCode,StringComparison.OrdinalIgnoreCase))
            .Concat(pack.UrgencyPolicies.Where(p=>p.ImpactTier==tierCode&&p.IntentCode is null))
            .FirstOrDefault();
        if(rule is null)return new(MapPriority(tierCode),null,null,AbvSource.Derived);
        return new(rule.Priority,rule.SlaHours,rule.PolicyCode,AbvSource.BusinessPolicy);
    }

    public AbvExecutionPath ResolveExecutionPath(AbvIntent intent,AbvImpact impact,AbvDomainPack pack)
    {
        // Owner: most specific mapping wins (intent+tier > intent > tier > generic default).
        var owner=pack.OwnerMappings
            .Where(m=>MatchOwner(m,intent.IntentCode,impact.Tier))
            .OrderByDescending(m=>OwnerSpecificity(m))
            .FirstOrDefault();
        var action=pack.Actions.FirstOrDefault(a=>string.Equals(a.IntentCode,intent.IntentCode,StringComparison.OrdinalIgnoreCase));
        return new(owner?.OwnerRole,action?.ActionCode,action?.NextStep,action?.PlaybookCode,AbvSource.DomainConfiguration);
    }

    public AbvActionability ResolveActionability(AbvResolutionStatus status,AbvExecutionPath? executionPath,AbvDomainPack pack)
    {
        // Phase 1: never auto-execute. Actionability gate reflects whether a human-reviewable action exists.
        return status switch
        {
            AbvResolutionStatus.NotConverged=>new(AbvActionabilityStatus.BlockedNotConverged,false,true),
            AbvResolutionStatus.IntentRejected=>new(AbvActionabilityStatus.BlockedNoIntent,false,true),
            AbvResolutionStatus.Failed=>new(AbvActionabilityStatus.BlockedFailed,false,true),
            _=>new(AbvActionabilityStatus.ReadyForReview,false,true)
        };
    }

    private static AbvPriority MapPriority(AbvImpactTier tier)=>tier switch
    {
        AbvImpactTier.Critical=>AbvPriority.Critical,
        AbvImpactTier.High=>AbvPriority.High,
        AbvImpactTier.Medium=>AbvPriority.Medium,
        _=>AbvPriority.Low
    };

    private static bool MatchOwner(AbvOwnerMappingRule m,string intentCode,AbvImpactTier tier)
    {
        var intentOk=m.IntentCode is null||string.Equals(m.IntentCode,intentCode,StringComparison.OrdinalIgnoreCase);
        var tierOk=m.ImpactTier is null||m.ImpactTier==tier;
        return intentOk&&tierOk;
    }

    private static int OwnerSpecificity(AbvOwnerMappingRule m)=>(m.IntentCode is null?0:2)+(m.ImpactTier is null?0:1);
}
