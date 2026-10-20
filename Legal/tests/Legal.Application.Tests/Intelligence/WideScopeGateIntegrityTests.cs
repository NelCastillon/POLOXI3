using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// Regression coverage for migration 0445 (Domain Pack L2 -> L3 scope-gate integrity).
public sealed class WideScopeGateIntegrityTests
{
    // ── L2 review batching: no silent truncation, full coverage ──

    [Fact]
    public void Batcher_Never_Drops_A_Line_And_Respects_Budget()
    {
        var items = Enumerable.Range(1, 353).Select(i => $"- EVD_TARGET_{i:000} \"Target {i}\" |mt:MVA |det 0.10: Name terms in matter: x").ToArray();
        var batches = L2ReviewBatcher.Split(items, s => s, budgetChars: 24000, fixedOverheadChars: 3600);

        Assert.True(batches.Count > 1);
        Assert.Equal(353, batches.Sum(b => b.Items.Count));
        Assert.Equal(items, batches.SelectMany(b => b.Items).ToArray());
        Assert.All(batches, b => Assert.True(b.SerializedChars <= 24000 - 3600, $"batch {b.SerializedChars} chars exceeds budget"));
    }

    [Fact]
    public void Batcher_Keeps_Oversized_Single_Line_In_Its_Own_Batch()
    {
        var items = new[] { "short", new string('x', 5000), "short2" };
        var batches = L2ReviewBatcher.Split(items, s => s, budgetChars: 1000, fixedOverheadChars: 0);

        Assert.Equal(3, batches.Sum(b => b.Items.Count));
        Assert.Contains(batches, b => b.Items.Count == 1 && b.Items[0].Length == 5000);
    }

    [Fact]
    public void Batcher_Secondary_Item_Cap_Applies()
    {
        var items = Enumerable.Range(1, 23).Select(i => $"line {i}").ToArray();
        var batches = L2ReviewBatcher.Split(items, s => s, budgetChars: 100000, fixedOverheadChars: 0, maxItemsPerBatch: 10);

        Assert.Equal(3, batches.Count);
        Assert.Equal(23, batches.Sum(b => b.Items.Count));
    }

    [Fact]
    public void Missing_Returns_Submitted_Ids_Not_Returned_Case_Insensitively()
    {
        var missing = L2ReviewBatcher.Missing(["EVD_A", "EVD_B", "EVD_C"], ["evd_a"]);
        Assert.Equal(["EVD_B", "EVD_C"], missing);
    }

    // ── Scope admission: integrity rules ──

    private static readonly MatterEvidenceScopeDecider.Thresholds T = new(OutThreshold: 0.35m, PendingReviewFloor: 0.10m, MinimumMappedPassages: 2, DecisivePassageScore: 0.60m);

    private static MatterEvidenceScopeDecider.Inputs Base(
        bool direct = false, bool required = false, bool prioritized = false, int passages = 0, decimal maxPassage = 0m,
        bool matterType = false, decimal? blended = null, decimal? llm = null, bool reviewEnabled = true, bool unresolved = false)
        => new(direct, required, prioritized, passages, maxPassage, matterType, blended, llm, reviewEnabled, unresolved);

    [Fact]
    public void Unreviewed_Target_Is_Review_Failed_Never_Admitted()
    {
        var d = MatterEvidenceScopeDecider.Decide(Base(blended: 0.9m, llm: null, unresolved: true), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.ReviewFailed, d.StatusCode);
    }

    [Fact]
    public void Single_Weak_Passage_Is_Pending_Review_Not_In_Scope()
    {
        // 0448: a score at/below the zero-score ceiling (0.05) is OUT_OF_SCOPE; the weak-passage pending rule
        // applies to genuinely ambiguous (non-zero) scores.
        var d = MatterEvidenceScopeDecider.Decide(Base(passages: 1, maxPassage: 0.30m, blended: 0.12m, llm: 0.12m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.PendingReview, d.StatusCode);
        Assert.Equal(MatterEvidenceScopeStatusCodes.OutOfScope, MatterEvidenceScopeDecider.Decide(Base(passages: 1, maxPassage: 0.30m, blended: 0.05m, llm: 0.05m), T).StatusCode);
    }

    [Fact]
    public void Single_Decisive_Passage_Admits()
    {
        var d = MatterEvidenceScopeDecider.Decide(Base(passages: 1, maxPassage: 0.75m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.InScope, d.StatusCode);
    }

    [Fact]
    public void Passage_Floor_Admits()
    {
        var d = MatterEvidenceScopeDecider.Decide(Base(passages: 2, maxPassage: 0.20m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.InScope, d.StatusCode);
    }

    [Fact]
    public void Direct_Document_Admits_Even_When_Review_Unresolved()
    {
        var d = MatterEvidenceScopeDecider.Decide(Base(direct: true, unresolved: true), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.InScope, d.StatusCode);
    }

    [Fact]
    public void Score_Without_Llm_Review_Does_Not_Admit_When_Review_Enabled()
    {
        var d = MatterEvidenceScopeDecider.Decide(Base(blended: 0.50m, llm: null, reviewEnabled: true, unresolved: false), T);
        Assert.NotEqual(MatterEvidenceScopeStatusCodes.InScope, d.StatusCode);
    }

    [Fact]
    public void Low_Score_No_Signal_Is_Out_Of_Scope_And_Band_Is_Pending()
    {
        Assert.Equal(MatterEvidenceScopeStatusCodes.OutOfScope, MatterEvidenceScopeDecider.Decide(Base(blended: 0.05m, llm: 0.05m), T).StatusCode);
        Assert.Equal(MatterEvidenceScopeStatusCodes.PendingReview, MatterEvidenceScopeDecider.Decide(Base(blended: 0.20m, llm: 0.20m), T).StatusCode);
    }

    // ── 0446 arbitration: PRIORITIZE / matter-type / blended are ordering signals, never overrides ──

    [Fact]
    public void Prioritized_Target_With_Low_Llm_Score_Is_Pending_Review_Not_In_Scope()
    {
        var d = MatterEvidenceScopeDecider.Decide(Base(prioritized: true, blended: 0.80m, llm: 0.10m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.PendingReview, d.StatusCode);
        Assert.Contains("PRIORITIZE", d.Reason);
    }

    [Fact]
    public void Matter_Type_Match_With_Low_Llm_Score_Is_Pending_Review()
    {
        var d = MatterEvidenceScopeDecider.Decide(Base(matterType: true, blended: 0.60m, llm: 0.05m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.PendingReview, d.StatusCode);
    }

    [Fact]
    public void High_Blended_With_Low_Llm_Score_Is_Pending_Review()
    {
        var d = MatterEvidenceScopeDecider.Decide(Base(blended: 0.90m, llm: 0.10m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.PendingReview, d.StatusCode);
    }

    [Fact]
    public void Prioritized_Target_Admits_When_Llm_Does_Not_Reject()
    {
        var d = MatterEvidenceScopeDecider.Decide(Base(prioritized: true, blended: 0.50m, llm: 0.50m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.InScope, d.StatusCode);
    }

    [Fact]
    public void Prioritized_Target_Without_Llm_Score_Is_Not_Admitted_When_Review_Enabled()
    {
        Assert.Equal(MatterEvidenceScopeStatusCodes.ReviewFailed, MatterEvidenceScopeDecider.Decide(Base(prioritized: true, blended: 0.80m, llm: null, unresolved: true), T).StatusCode);
        Assert.Equal(MatterEvidenceScopeStatusCodes.PendingReview, MatterEvidenceScopeDecider.Decide(Base(prioritized: true, blended: 0.80m, llm: null, unresolved: false), T).StatusCode);
    }

    [Fact]
    public void Prioritized_Target_Admits_When_Review_Disabled()
    {
        var d = MatterEvidenceScopeDecider.Decide(Base(prioritized: true, reviewEnabled: false), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.InScope, d.StatusCode);
    }

    [Fact]
    public void Explicit_Exceptions_Admit_Against_Low_Llm_Score()
    {
        Assert.Equal(MatterEvidenceScopeStatusCodes.InScope, MatterEvidenceScopeDecider.Decide(Base(direct: true, llm: 0.05m), T).StatusCode);
        Assert.Equal(MatterEvidenceScopeStatusCodes.InScope, MatterEvidenceScopeDecider.Decide(Base(required: true, llm: 0.05m), T).StatusCode);
        // 0447: keyword/lexical passages (even above the floor or decisive score) are NOT an exception; only a
        // passage directly classified as this evidence type is.
        Assert.NotEqual(MatterEvidenceScopeStatusCodes.InScope, MatterEvidenceScopeDecider.Decide(Base(passages: 2, llm: 0.05m), T).StatusCode);
        Assert.NotEqual(MatterEvidenceScopeStatusCodes.InScope, MatterEvidenceScopeDecider.Decide(Base(passages: 1, maxPassage: 0.70m, llm: 0.05m), T).StatusCode);
        Assert.Equal(MatterEvidenceScopeStatusCodes.InScope, MatterEvidenceScopeDecider.Decide(new(false, false, false, 1, 1m, false, null, 0.05m, true, false, DirectPassageCount: 1), T).StatusCode);
    }

    [Fact]
    public void Missing_Evidence_Target_Stays_In_Scope_Without_Document()
    {
        // In-scope investigation target with no uploaded document: admitted on relevance, not on evidence.
        var d = MatterEvidenceScopeDecider.Decide(Base(direct: false, passages: 0, blended: 0.70m, llm: 0.70m), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.InScope, d.StatusCode);
    }

    // ── 0446 saved matter context coherence (advisory, non-mutating) ──

    [Fact]
    public void Elena_Saved_Posture_Is_Flagged_Incoherent()
    {
        var warnings = MatterContextCoherence.Evaluate(new("Personal Injury", "Copyright", "the class certification", "partial summary judgment", "Personal Injury - Target: the class certification"));
        Assert.Contains(warnings, w => w.StartsWith("SUBTYPE_INCOHERENT"));
        Assert.Contains(warnings, w => w.Contains("MotionTarget"));
        Assert.Contains(warnings, w => w.Contains("Posture"));
    }

    [Fact]
    public void Coherent_Pi_Posture_Has_No_Warnings()
    {
        var warnings = MatterContextCoherence.Evaluate(new("Personal Injury", "Motor Vehicle", "the negligence claim", "summary judgment for the movant", null));
        Assert.Empty(warnings);
    }

    [Fact]
    public void Non_Pi_Matter_Is_Not_Evaluated()
    {
        Assert.Empty(MatterContextCoherence.Evaluate(new("Class Action", "Securities", "the class certification", null, null)));
    }

    // ── Elena Vasquez (side-impact motor vehicle collision, California): deterministic exclusions ──

    private static readonly DecisionDomainPackIncidentFamilyDto[] Families =
    [
        new("MOTOR_VEHICLE", "Motor Vehicle", null, "%motor%vehicle%,%auto%,%car%accident%,%collision%,%mva%", 1),
        new("PREMISES", "Premises Liability", null, "%premises%,%slip%,%trip%,%fall%", 2),
        new("GOVERNMENT_ENTITY", "Government Entity", null, "%government%,%public%entity%,%city%of%,%county%of%", 3),
    ];

    [Fact]
    public void Side_Impact_Collision_Resolves_To_Motor_Vehicle_Only()
    {
        var families = DomainPackScopeEligibility.ResolveMatterFamilies(Families, ["Personal Injury", "Motor Vehicle Accident", "Side-impact collision"]);
        Assert.Contains("MOTOR_VEHICLE", families);
        Assert.DoesNotContain("PREMISES", families);
        Assert.DoesNotContain("GOVERNMENT_ENTITY", families);
    }

    [Fact]
    public void Premises_Evidence_Is_Excluded_For_Motor_Vehicle_Matter_And_Untagged_Stays_Eligible()
    {
        var families = DomainPackScopeEligibility.ResolveMatterFamilies(Families, ["Motor Vehicle Accident"]);
        var openObvious = new DecisionDomainPackEvidenceTypeDto("OPEN_OBVIOUS_PHOTO", "Open and Obvious Photo", "LIABILITY", null, null, false, null, null, "PREMISES");
        var medical = new DecisionDomainPackEvidenceTypeDto("MEDICAL_RECORDS", "Medical Records", "DAMAGES", null, null, true, null, null, null);
        var police = new DecisionDomainPackEvidenceTypeDto("POLICE_REPORT", "Police Report", "LIABILITY", null, null, false, null, null, "MOTOR_VEHICLE,PREMISES");

        var excluded = DomainPackScopeEligibility.EvaluateEvidenceType(openObvious, families);
        Assert.False(excluded.IsEligible);
        Assert.Equal(DomainPackScopeEligibility.ExclusionIncidentFamily, excluded.ExclusionCode);
        Assert.True(DomainPackScopeEligibility.EvaluateEvidenceType(medical, families).IsEligible);
        Assert.True(DomainPackScopeEligibility.EvaluateEvidenceType(police, families).IsEligible);
    }

    [Fact]
    public void Generic_Comparative_Fault_Threshold_Is_Superseded_In_California()
    {
        var generic = new DecisionDomainConceptDto(Guid.NewGuid(), "COMPARATIVE_FAULT_THRESHOLD", "LIABILITY", "Comparative Fault Threshold", null, "RULE", "STATUTE", null, null, null, false, true, 1, 1);
        var pure = new DecisionDomainConceptDto(Guid.NewGuid(), "CA_PURE_COMPARATIVE_FAULT", "LIABILITY", "California Pure Comparative Fault", null, "RULE", "CASELAW", null, "CA", null, false, true, 2, 1);
        var relations = new[] { new DecisionDomainConceptRelationDto(Guid.NewGuid(), "CA_PURE_COMPARATIVE_FAULT", "COMPARATIVE_FAULT_THRESHOLD", "SUPERSEDES", null, null, "CA", null, true, 1) };
        var byCode = new Dictionary<string, DecisionDomainConceptDto>(StringComparer.OrdinalIgnoreCase) { [generic.ConceptCode] = generic, [pure.ConceptCode] = pure };

        var ca = DomainPackScopeEligibility.EvaluateConcept(generic, "CA", relations, byCode);
        Assert.False(ca.IsEligible);
        Assert.Equal(DomainPackScopeEligibility.ExclusionSuperseded, ca.ExclusionCode);

        Assert.True(DomainPackScopeEligibility.EvaluateConcept(generic, "TX", relations, byCode).IsEligible);
        Assert.True(DomainPackScopeEligibility.EvaluateConcept(generic, null, relations, byCode).IsEligible);
        Assert.True(DomainPackScopeEligibility.EvaluateConcept(pure, "CA", relations, byCode).IsEligible);
        Assert.False(DomainPackScopeEligibility.EvaluateConcept(pure, "TX", relations, byCode).IsEligible);
    }

    [Fact]
    public void Required_Coverage_Concept_Is_Never_Excluded_By_Jurisdiction()
    {
        var required = new DecisionDomainConceptDto(Guid.NewGuid(), "STATUTE_OF_LIMITATIONS", "PROCEDURE", "SOL", null, "RULE", "STATUTE", null, "NY", null, true, true, 1, 1);
        Assert.True(DomainPackScopeEligibility.EvaluateConcept(required, "CA", [], new Dictionary<string, DecisionDomainConceptDto>()).IsEligible);
    }

    // ── 0447 Chloe Nakamura audit: structured subtype PREMISES / Slip and Fall vs. collision narrative ──

    private static readonly DecisionDomainPackEvidenceTypeDto Seatbelt = new("SEATBELT_HELMET_USE_RECORD", "Seatbelt / Helmet Use Record", "LIABILITY", null, null, false, null, null, "MOTOR_VEHICLE");
    private static readonly DecisionDomainPackEvidenceTypeDto Sr1 = new("DMV_ACCIDENT_REPORT_SR1", "DMV Accident Report SR-1", "LIABILITY", null, null, false, null, null, "MOTOR_VEHICLE");
    private static readonly DecisionDomainPackEvidenceTypeDto SafetyAudit = new("SAFETY_AUDIT_REPORT", "Safety Audit Report", "LIABILITY", null, null, false, null, null, "PREMISES");
    private static readonly DecisionDomainPackEvidenceTypeDto Ownership = new("PROPERTY_OWNERSHIP_RECORD", "Property Ownership Record", "LIABILITY", null, null, false, null, null, "PREMISES");
    private static readonly DecisionDomainPackEvidenceTypeDto PedestrianSignal = new("PEDESTRIAN_SIGNAL_RECORD", "Pedestrian Signal Record", "LIABILITY", null, null, false, null, null, "MOTOR_VEHICLE");

    [Fact]
    public void Correctly_Classified_Motor_Vehicle_Matter_Has_No_Conflict_And_Excludes_Premises()
    {
        var scope = DomainPackScopeEligibility.ResolveMatterFamilyScope(Families,
            ["Personal Injury", "Motor Vehicle Accident"],
            ["Plaintiff was injured in a rear-end collision allegedly caused by the defendant."]);
        Assert.False(scope.IsConflict);
        Assert.Null(scope.ConflictReason);
        Assert.Equal(["MOTOR_VEHICLE"], scope.Effective.OrderBy(f => f));
        Assert.True(DomainPackScopeEligibility.EvaluateEvidenceType(Seatbelt, scope.Effective).IsEligible);
        Assert.True(DomainPackScopeEligibility.EvaluateEvidenceType(Sr1, scope.Effective).IsEligible);
        Assert.False(DomainPackScopeEligibility.EvaluateEvidenceType(SafetyAudit, scope.Effective).IsEligible);
        Assert.False(DomainPackScopeEligibility.EvaluateEvidenceType(Ownership, scope.Effective).IsEligible);
    }

    [Fact]
    public void Conflicting_Metadata_Suspends_Family_Exclusions_And_Surfaces_Conflict()
    {
        var scope = DomainPackScopeEligibility.ResolveMatterFamilyScope(Families,
            ["Personal Injury", "Premises Liability", "Slip and Fall"],
            ["Chloe Nakamura was injured in a collision allegedly caused by Basalt Components.", "Liability is supported by the police report and an independent witness."]);
        Assert.True(scope.IsConflict);
        Assert.StartsWith(DomainPackScopeEligibility.IncidentFamilyConflict, scope.ConflictReason);
        Assert.Contains("PREMISES", scope.Structured);
        Assert.Contains("MOTOR_VEHICLE", scope.Narrative);
        // Neither side wins: both families stay eligible so every tagged target reaches LLM review.
        foreach (var target in new[] { Seatbelt, Sr1, SafetyAudit, Ownership, PedestrianSignal })
            Assert.True(DomainPackScopeEligibility.EvaluateEvidenceType(target, scope.Effective).IsEligible, target.EvidenceTypeCode);
    }

    [Fact]
    public void Narrative_Alone_Or_Structured_Alone_Is_Not_A_Conflict()
    {
        Assert.False(DomainPackScopeEligibility.ResolveMatterFamilyScope(Families, ["Premises Liability"], [null, ""]).IsConflict);
        Assert.False(DomainPackScopeEligibility.ResolveMatterFamilyScope(Families, ["Personal Injury"], ["injured in a collision"]).IsConflict);
        Assert.False(DomainPackScopeEligibility.ResolveMatterFamilyScope(Families, ["Motor Vehicle Accident"], ["car accident at a premises parking lot"]).IsConflict);
    }

    [Fact]
    public void Pedestrian_Signal_Record_Lexical_Passage_Cannot_Override_Llm_Rejection()
    {
        // Live audit: LLM 0.02 ("plainly inapplicable"), one KEYWORD/LEXICAL passage hit, final IN_SCOPE.
        // 0448: at/below the zero-score ceiling a lexical hit is not a reopening trigger -> OUT_OF_SCOPE (auditable).
        var d = MatterEvidenceScopeDecider.Decide(Base(passages: 1, maxPassage: 0.65m, blended: 0.30m, llm: 0.02m), T);
        Assert.NotEqual(MatterEvidenceScopeStatusCodes.InScope, d.StatusCode);
        Assert.Equal(MatterEvidenceScopeStatusCodes.OutOfScope, d.StatusCode);
        Assert.Contains("lexical", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Multiple_Lexical_Passages_Cannot_Override_Llm_Rejection()
    {
        var d = MatterEvidenceScopeDecider.Decide(Base(passages: 3, maxPassage: 0.50m, blended: 0.40m, llm: 0.05m), T);
        Assert.NotEqual(MatterEvidenceScopeStatusCodes.InScope, d.StatusCode);
    }

    [Fact]
    public void Directly_Classified_Passage_Is_An_Auditable_Exception_To_Llm_Rejection()
    {
        var d = MatterEvidenceScopeDecider.Decide(new(false, false, false, 1, 1m, false, 0.30m, 0.02m, true, false, DirectPassageCount: 1), T);
        Assert.Equal(MatterEvidenceScopeStatusCodes.InScope, d.StatusCode);
        Assert.Contains("classified", d.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Low_Llm_Without_Competing_Signal_Stays_In_Score_Bands()
    {
        Assert.Equal(MatterEvidenceScopeStatusCodes.OutOfScope, MatterEvidenceScopeDecider.Decide(Base(blended: 0.02m, llm: 0.02m), T).StatusCode);
        Assert.Equal(MatterEvidenceScopeStatusCodes.PendingReview, MatterEvidenceScopeDecider.Decide(Base(blended: 0.15m, llm: 0.15m), T).StatusCode);
    }
}
