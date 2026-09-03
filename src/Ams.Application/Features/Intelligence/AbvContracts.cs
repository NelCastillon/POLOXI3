using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Intelligence;

// ── POLOXI ABV (Actionable Business Value) Action Layer — canonical contracts ─────────────────
// Truth != Action: ABV runs only AFTER convergence. The LLM proposes intent ONLY (from the
// Domain-Pack taxonomy); impact, urgency, owner, and next action resolve deterministically from
// Domain-Pack configuration with explicit provenance. Unsupported values stay null — POLOXI
// never manufactures business numbers, SLAs, or owners.

// Provenance of each ABV field: sounds-precise-but-speculative is prevented by making the source
// of every business assertion explicit.
public enum AbvSource{Derived,Evidence,BusinessPolicy,DomainConfiguration}

public enum AbvImpactTier{Low,Medium,High,Critical}
public enum AbvPriority{Low,Medium,High,Critical}
public enum AbvResolutionStatus{Resolved,NotConverged,IntentRejected,Failed}
public enum AbvActionabilityStatus{ReadyForReview,BlockedNotConverged,BlockedNoIntent,BlockedFailed}

// ── Domain Pack configuration (database-backed; POLOXI Core owns no business taxonomy) ────────
public sealed record AbvDomainPack(Guid AbvDomainPackId,string PackCode,string Name,
    IReadOnlyList<AbvIntentDefinition> Intents,
    IReadOnlyList<AbvUrgencyPolicyRule> UrgencyPolicies,
    IReadOnlyList<AbvOwnerMappingRule> OwnerMappings,
    IReadOnlyList<AbvActionDefinition> Actions);

public sealed record AbvIntentDefinition(string IntentCode,string Name,string? Description);
public sealed record AbvUrgencyPolicyRule(string PolicyCode,string? IntentCode,AbvImpactTier ImpactTier,AbvPriority Priority,int? SlaHours);
public sealed record AbvOwnerMappingRule(string? IntentCode,AbvImpactTier? ImpactTier,string OwnerRole);
public sealed record AbvActionDefinition(string IntentCode,string ActionCode,string Name,string? NextStep,string? PlaybookCode,bool ExecutionAllowed,bool HumanApprovalRequired);

// ── LLM intent proposal (the only LLM-produced part of ABV) ───────────────────────────────────
public sealed record AbvIntentProposal
{
    public required string IntentCode{get;init;}
    public string? Rationale{get;init;}
    public IReadOnlyList<string> SupportingDimensionIds{get;init;}=[];
    // May name a metric only if it appears in the composite; deterministic validation enforces this.
    public string? ProposedMetricAtRisk{get;init;}
}

// ── Resolved ABV vectors, each with provenance ─────────────────────────────────────────────────
public sealed record AbvIntent(string IntentCode,string Name,string? Rationale,AbvSource Source,IReadOnlyList<string> SupportingDimensionIds);
// EstimatedExposure stays null unless quantified by admitted evidence — a feature, not a failure.
public sealed record AbvImpact(AbvImpactTier Tier,string? MetricAtRisk,decimal? EstimatedExposure,AbvSource Source,IReadOnlyList<string> EvidenceIds);
public sealed record AbvUrgency(AbvPriority Priority,int? SlaHours,string? PolicyCode,AbvSource Source);
public sealed record AbvExecutionPath(string? OwnerRole,string? ActionCode,string? NextStep,string? PlaybookCode,AbvSource Source);
public sealed record AbvActionability(AbvActionabilityStatus Status,bool ExecutionAllowed,bool HumanApprovalRequired);

// ── Request / outcome ──────────────────────────────────────────────────────────────────────────
public sealed record AbvResolutionRequest(Guid TenantId,Guid UserId,Guid? AmbiguityRunId,InterpretationComposite Composite,string CorrelationId)
{
    // Null/empty = default Domain Pack; otherwise a specific pack code.
    [StringLength(50)]public string? DomainPackCode{get;init;}
    [StringLength(100)]public string? ModelCode{get;init;}
}

public sealed record AbvResolutionOutcome
{
    public required Guid AbvResolutionId{get;init;}
    public required AbvResolutionStatus Status{get;init;}
    public AbvIntent? Intent{get;init;}
    public AbvImpact? Impact{get;init;}
    public AbvUrgency? Urgency{get;init;}
    public AbvExecutionPath? ExecutionPath{get;init;}
    public required AbvActionability Actionability{get;init;}
    public string? FailureMessage{get;init;}
}
