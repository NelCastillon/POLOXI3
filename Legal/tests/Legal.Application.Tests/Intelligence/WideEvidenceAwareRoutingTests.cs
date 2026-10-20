using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Regression coverage for migration 0448 (post-L2 evidence-aware routing, zero-score cleanup, non-mutating
// classification proposals). The batched L2 reviewer (0445) is untouched by these contracts.
public sealed class WideEvidenceAwareRoutingTests
{
    // ── Zero-score cleanup (MatterEvidenceScopeDecider) ──

    private static readonly MatterEvidenceScopeDecider.Thresholds T = new(OutThreshold: 0.35m, PendingReviewFloor: 0.10m, MinimumMappedPassages: 2, DecisivePassageScore: 0.60m, ZeroScoreCeiling: 0.05m);

    private static MatterEvidenceScopeDecider.Inputs Scope(
        bool direct = false, bool required = false, bool prioritized = false, int passages = 0, decimal maxPassage = 0m,
        bool matterType = false, decimal? blended = null, decimal? llm = null, int directPassages = 0)
        => new(direct, required, prioritized, passages, maxPassage, matterType, blended, llm, ReviewEnabled: true, ReviewUnresolved: false, directPassages);

    [Fact]
    public void Zero_Score_With_Only_Lexical_Passages_And_Blended_Is_Out_Of_Scope()
    {
        var d = MatterEvidenceScopeDecider.Decide(Scope(passages: 3, maxPassage: 0.40m, blended: 0.45m, llm: 0.00m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.OutOfScope, d.StatusCode);
    }

    [Fact]
    public void Zero_Score_Without_Any_Signal_Is_Out_Of_Scope()
    {
        var d = MatterEvidenceScopeDecider.Decide(Scope(blended: 0.12m, llm: 0.03m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.OutOfScope, d.StatusCode);
    }

    [Fact]
    public void Zero_Score_With_Prioritize_Trigger_Stays_Pending()
    {
        var d = MatterEvidenceScopeDecider.Decide(Scope(prioritized: true, llm: 0.00m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.PendingReview, d.StatusCode);
    }

    [Fact]
    public void Zero_Score_With_Matter_Type_Trigger_Stays_Pending()
    {
        var d = MatterEvidenceScopeDecider.Decide(Scope(matterType: true, llm: 0.02m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.PendingReview, d.StatusCode);
    }

    [Theory]
    [InlineData(true, false, 0)]
    [InlineData(false, true, 0)]
    [InlineData(false, false, 1)]
    public void Zero_Score_With_Concrete_Evidence_Or_Required_Trigger_Is_In_Scope(bool direct, bool required, int directPassages)
    {
        var d = MatterEvidenceScopeDecider.Decide(Scope(direct: direct, required: required, directPassages: directPassages, llm: 0.00m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.InScope, d.StatusCode);
    }

    [Fact]
    public void Low_But_Non_Zero_Score_With_Lexical_Passage_Remains_Pending()
    {
        var d = MatterEvidenceScopeDecider.Decide(Scope(passages: 1, maxPassage: 0.30m, blended: 0.15m, llm: 0.15m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.PendingReview, d.StatusCode);
    }

    // ── Independent routing (MatterEvidenceRouting) ──

    private static readonly MatterEvidenceRouting.Thresholds R = new(ImmediateRelevance: 0.60m, EvidenceAwareRoutingEnabled: true);

    private static MatterEvidenceRouting.Inputs Route(
        string kind = "EVIDENCE_TYPE", string status = MatterEvidenceScopeStatusCodes.InScope, bool participates = true,
        bool direct = false, int directPassages = 0, int verified = 0, bool required = false, bool prioritized = false,
        decimal? llm = null, decimal? blended = null)
        => new(kind, status, participates, direct, directPassages, verified, required, prioritized, llm, blended);

    [Fact]
    public void Absent_Low_Relevance_Evidence_Type_Is_Deferred()
    {
        var r = MatterEvidenceRouting.Decide(Route(llm: 0.40m, blended: 0.50m), R);
        Assert.Equal(MatterEvidenceAvailabilityCodes.Absent, r.AvailabilityCode);
        Assert.Equal(MatterEvidenceVerificationCodes.Unknown, r.VerificationCode);
        Assert.Equal(MatterEvidenceExecutionPriorityCodes.Deferred, r.PriorityCode);
        Assert.True(r.IsDeferred);
        Assert.False(r.RunsL3);
    }

    [Fact]
    public void Absent_High_Relevance_Evidence_Type_Is_Immediate()
    {
        var r = MatterEvidenceRouting.Decide(Route(llm: 0.75m), R);
        Assert.Equal(MatterEvidenceAvailabilityCodes.Absent, r.AvailabilityCode);
        Assert.True(r.RunsL3);
        Assert.False(r.IsDeferred);
    }

    [Fact]
    public void Lexical_Passages_Do_Not_Establish_Availability()
    {
        // No direct document, no directly classified passage -> ABSENT even if keyword passages were mapped upstream.
        var r = MatterEvidenceRouting.Decide(Route(directPassages: 0, llm: 0.30m), R);
        Assert.Equal(MatterEvidenceAvailabilityCodes.Absent, r.AvailabilityCode);
    }

    [Fact]
    public void Direct_Document_Is_Present_And_Never_Deferred()
    {
        var r = MatterEvidenceRouting.Decide(Route(direct: true, llm: 0.10m), R);
        Assert.Equal(MatterEvidenceAvailabilityCodes.Present, r.AvailabilityCode);
        Assert.Equal(MatterEvidenceVerificationCodes.Unverified, r.VerificationCode);
        Assert.True(r.RunsL3);
    }

    [Fact]
    public void Direct_Passage_Is_Referenced_And_Never_Deferred()
    {
        var r = MatterEvidenceRouting.Decide(Route(directPassages: 2, llm: 0.10m), R);
        Assert.Equal(MatterEvidenceAvailabilityCodes.Referenced, r.AvailabilityCode);
        Assert.True(r.RunsL3);
    }

    [Fact]
    public void Verification_Requires_Verified_Assertions_Not_Document_Presence()
    {
        var unverified = MatterEvidenceRouting.Decide(Route(direct: true, verified: 0), R);
        var verified = MatterEvidenceRouting.Decide(Route(direct: true, verified: 1), R);
        Assert.Equal(MatterEvidenceVerificationCodes.Unverified, unverified.VerificationCode);
        Assert.Equal(MatterEvidenceVerificationCodes.Verified, verified.VerificationCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Required_And_Prioritized_Absent_Targets_Are_Never_Deferred(bool required, bool prioritized)
    {
        var r = MatterEvidenceRouting.Decide(Route(required: required, prioritized: prioritized, llm: 0.10m), R);
        Assert.True(r.RunsL3);
        Assert.False(r.IsDeferred);
    }

    [Fact]
    public void Concepts_Are_Not_Applicable_For_Availability_And_Always_Immediate()
    {
        var r = MatterEvidenceRouting.Decide(Route(kind: "CONCEPT", llm: 0.05m), R);
        Assert.Equal(MatterEvidenceAvailabilityCodes.NotApplicable, r.AvailabilityCode);
        Assert.Equal(MatterEvidenceExecutionPriorityCodes.Immediate, r.PriorityCode);
    }

    [Fact]
    public void Non_Participating_Status_Is_Dormant_But_Still_Carries_Availability()
    {
        var r = MatterEvidenceRouting.Decide(Route(status: MatterEvidenceScopeStatusCodes.PendingReview, participates: false, directPassages: 1), R);
        Assert.Equal(MatterEvidenceExecutionPriorityCodes.Dormant, r.PriorityCode);
        Assert.Equal(MatterEvidenceAvailabilityCodes.Referenced, r.AvailabilityCode);
        Assert.False(r.RunsL3);
    }

    [Fact]
    public void Routing_Switch_Off_Restores_L3_Pass_For_Absent_Low_Relevance()
    {
        var r = MatterEvidenceRouting.Decide(Route(llm: 0.20m), new(0.60m, EvidenceAwareRoutingEnabled: false));
        Assert.True(r.RunsL3);
        Assert.False(r.IsDeferred);
    }

    // ── Non-mutating classification proposals (MatterClassificationProposer) ──

    private static readonly DecisionDomainPackIncidentFamilyDto[] Families =
    [
        new("MVA", "Motor Vehicle Collision", null, "%vehicle%,%collision%,%motor%", 10),
        new("FIDUCIARY", "Breach of Fiduciary Duty", null, "%fiduciary%", 20),
    ];

    private static MatterClassificationProposer.Inputs Conflict(string? motionTarget = null, string? disposition = null)
    {
        var scope = DomainPackScopeEligibility.ResolveMatterFamilyScope(Families,
            ["Personal Injury", "Breach of Fiduciary Duty", null, "Other"],
            ["Plaintiff was rear-ended in a motor vehicle collision on the interstate."]);
        return new("Personal Injury", "Breach of Fiduciary Duty", null, "Other", motionTarget, disposition, null, scope, Families, null);
    }

    [Fact]
    public void Conflict_Produces_Proposed_Rows_And_Keeps_Saved_Values_As_CurrentValue()
    {
        var input = Conflict();
        Assert.True(input.FamilyScope.IsConflict);

        var proposals = MatterClassificationProposer.Propose(input);

        Assert.Contains(proposals, p => p.FieldCode == MatterClassificationFieldCodes.IncidentFamily && p.ProposedValue == "MVA" && p.CurrentValue == "FIDUCIARY");
        Assert.Contains(proposals, p => p.FieldCode == MatterClassificationFieldCodes.Subtype && p.CurrentValue == "Breach of Fiduciary Duty" && p.ProposedValue == "Motor Vehicle Collision");
        Assert.Contains(proposals, p => p.FieldCode == MatterClassificationFieldCodes.IncidentTypeCode && p.CurrentValue == "Other");
        Assert.All(proposals, p => Assert.InRange(p.Confidence, 0.01m, 0.95m));
        // Inputs are an immutable record: the saved matter fields are untouched by construction.
        Assert.Equal("Breach of Fiduciary Duty", input.Subtype);
        Assert.Equal("Other", input.IncidentTypeCode);
    }

    [Fact]
    public void No_Conflict_Produces_No_Proposals()
    {
        var scope = DomainPackScopeEligibility.ResolveMatterFamilyScope(Families, ["Motor Vehicle Accident"], ["A collision at the intersection."]);
        var proposals = MatterClassificationProposer.Propose(new("Personal Injury", "Motor Vehicle Accident", null, null, null, null, null, scope, Families, null));
        Assert.Empty(proposals);
        Assert.Null(MatterClassificationProposer.DecisionContextNote(new("Personal Injury", "Motor Vehicle Accident", null, null, "Summary judgment", null, null, scope, Families, null)));
    }

    [Fact]
    public void Decision_Context_Note_Reports_Saved_Contract_Without_Proposing_A_Replacement()
    {
        var note = MatterClassificationProposer.DecisionContextNote(Conflict(motionTarget: "Summary judgment on all claims", disposition: "Grant"));
        Assert.NotNull(note);
        Assert.Equal(MatterClassificationFieldCodes.DecisionContext, note!.FieldCode);
        Assert.Contains("Summary judgment", note.CurrentValue);
        Assert.Null(note.ProposedValue);
    }

    [Fact]
    public void Decision_Context_Note_Is_Absent_When_No_Procedural_Request_Saved()
    {
        Assert.Null(MatterClassificationProposer.DecisionContextNote(Conflict()));
    }
}
