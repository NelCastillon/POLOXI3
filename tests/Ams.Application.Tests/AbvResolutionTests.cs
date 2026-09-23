using Ams.Application.Features.Intelligence;
using Ams.Application.Features.Intelligence.Abv;
using Xunit;

namespace Ams.Application.Tests;

// Deterministic tests for the POLOXI ABV (Actionable Business Value) governance layer: taxonomy-
// bounded intent acceptance, derived impact, config-driven urgency/owner/action, provenance, and
// the actionability gate. The LLM proposes intent; POLOXI decides all business value deterministically.
public sealed class AbvResolutionTests
{
    private readonly AbvGovernanceEngine _governance=new();

    private static AbvDomainPack Pack()=>new(
        Guid.NewGuid(),"GENERIC","Generic Business Actions",
        [new("ACT","Act",null),new("ESCALATE","Escalate",null),new("INVESTIGATE","Investigate",null),new("MONITOR","Monitor",null)],
        [
            new("POL-CRIT",null,AbvImpactTier.Critical,AbvPriority.Critical,24),
            new("POL-HIGH",null,AbvImpactTier.High,AbvPriority.High,72),
            new("POL-MED",null,AbvImpactTier.Medium,AbvPriority.Medium,168),
            new("POL-LOW",null,AbvImpactTier.Low,AbvPriority.Low,null)
        ],
        [
            new("ESCALATE",null,"Operations Leadership"),
            new(null,AbvImpactTier.Critical,"Operations Leadership"),
            new(null,null,"Operations Analyst")
        ],
        [
            new("ACT","REVIEW_RESPONSE","Review Proposed Response","Review the finding.","PB-ACT",false,true),
            new("ESCALATE","ESCALATE_TO_OWNER","Escalate To Owner","Route to owner.","PB-ESC",false,true)
        ]);

    private static InterpretationComposite Composite(bool converged,decimal weight=0.6m,string metric="Retention rate")=>new()
    {
        Objective="Decide response",
        Dimensions=[new("d1","Churn signal",SemanticRole.CompetingInterpretation,PreferenceDirection.Unknown,metric,"evidence",weight)],
        HardConstraints=[],Preferences=[],Interactions=[],Uncertainties=[],
        IsConverged=converged
    };

    [Fact]
    public void AcceptIntent_UnknownCode_IsRejected()
    {
        var intent=_governance.AcceptIntent(new(){IntentCode="TELEPORT"},Composite(true),Pack());
        Assert.Null(intent);
    }

    [Fact]
    public void AcceptIntent_KnownCode_DropsHallucinatedSupportingIds()
    {
        var intent=_governance.AcceptIntent(new(){IntentCode="act",SupportingDimensionIds=["d1","ghost"]},Composite(true),Pack());
        Assert.NotNull(intent);
        Assert.Equal("ACT",intent!.IntentCode);
        Assert.Equal(["d1"],intent.SupportingDimensionIds);
        Assert.Equal(AbvSource.Derived,intent.Source);
    }

    [Theory]
    [InlineData(0.8,AbvImpactTier.Critical)]
    [InlineData(0.6,AbvImpactTier.High)]
    [InlineData(0.3,AbvImpactTier.Medium)]
    [InlineData(0.1,AbvImpactTier.Low)]
    public void ResolveImpact_DerivesTierFromWeight(double weight,AbvImpactTier expected)
    {
        var pack=Pack();
        var intent=_governance.AcceptIntent(new(){IntentCode="ACT",SupportingDimensionIds=["d1"]},Composite(true,(decimal)weight),pack)!;
        var impact=_governance.ResolveImpact(intent,Composite(true,(decimal)weight),pack);
        Assert.Equal(expected,impact.Tier);
    }

    [Fact]
    public void ResolveImpact_NeverFabricatesExposure()
    {
        var pack=Pack();
        var intent=_governance.AcceptIntent(new(){IntentCode="ACT",SupportingDimensionIds=["d1"]},Composite(true),pack)!;
        var impact=_governance.ResolveImpact(intent,Composite(true),pack);
        Assert.Null(impact.EstimatedExposure);
        Assert.Equal("Retention rate",impact.MetricAtRisk);
    }

    [Fact]
    public void ResolveUrgency_UsesConfiguredPolicyAndSla()
    {
        var pack=Pack();
        var intent=_governance.AcceptIntent(new(){IntentCode="ACT",SupportingDimensionIds=["d1"]},Composite(true,0.8m),pack)!;
        var impact=_governance.ResolveImpact(intent,Composite(true,0.8m),pack);
        var urgency=_governance.ResolveUrgency(intent,impact,pack);
        Assert.Equal(AbvPriority.Critical,urgency.Priority);
        Assert.Equal(24,urgency.SlaHours);
        Assert.Equal("POL-CRIT",urgency.PolicyCode);
        Assert.Equal(AbvSource.BusinessPolicy,urgency.Source);
    }

    [Fact]
    public void ResolveExecutionPath_PrefersIntentSpecificOwner()
    {
        var pack=Pack();
        var intent=_governance.AcceptIntent(new(){IntentCode="ESCALATE",SupportingDimensionIds=["d1"]},Composite(true),pack)!;
        var impact=_governance.ResolveImpact(intent,Composite(true),pack);
        var path=_governance.ResolveExecutionPath(intent,impact,pack);
        Assert.Equal("Operations Leadership",path.OwnerRole);
        Assert.Equal("ESCALATE_TO_OWNER",path.ActionCode);
        Assert.Equal(AbvSource.DomainConfiguration,path.Source);
    }

    [Fact]
    public void ResolveExecutionPath_FallsBackToGenericOwner()
    {
        var pack=Pack();
        var intent=_governance.AcceptIntent(new(){IntentCode="ACT",SupportingDimensionIds=["d1"]},Composite(true,0.3m),pack)!;
        var impact=_governance.ResolveImpact(intent,Composite(true,0.3m),pack);
        var path=_governance.ResolveExecutionPath(intent,impact,pack);
        Assert.Equal("Operations Analyst",path.OwnerRole);
    }

    [Fact]
    public void ResolveActionability_NeverAllowsAutoExecution()
    {
        var gate=_governance.ResolveActionability(AbvResolutionStatus.Resolved,null,Pack());
        Assert.Equal(AbvActionabilityStatus.ReadyForReview,gate.Status);
        Assert.False(gate.ExecutionAllowed);
        Assert.True(gate.HumanApprovalRequired);
    }

    [Fact]
    public void ResolveActionability_BlocksWhenNotConverged()
    {
        var gate=_governance.ResolveActionability(AbvResolutionStatus.NotConverged,null,Pack());
        Assert.Equal(AbvActionabilityStatus.BlockedNotConverged,gate.Status);
        Assert.False(gate.ExecutionAllowed);
    }

    [Fact]
    public void Parse_InvalidJson_ReturnsNull()=>Assert.Null(AbvResolutionEngine.Parse("{ not json"));

    [Fact]
    public void Parse_MissingIntentCode_ReturnsNull()=>Assert.Null(AbvResolutionEngine.Parse("{\"rationale\":\"x\"}"));

    [Fact]
    public void Parse_ValidProposal_Succeeds()
    {
        var parsed=AbvResolutionEngine.Parse("{\"intentCode\":\"ACT\",\"supportingDimensionIds\":[\"d1\"]}");
        Assert.NotNull(parsed);
        Assert.Equal("ACT",parsed!.IntentCode);
    }
}
