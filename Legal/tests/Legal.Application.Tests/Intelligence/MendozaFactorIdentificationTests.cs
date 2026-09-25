using System.Reflection;
using System.Runtime.CompilerServices;
using Legal.Application;
using Legal.Application.Features.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ────────────────────────────────────────────────────────────────────────────────────────────────
// Mendoza — Correct Legal Factor Identification (zero-cost regression).
//
// Reproduces the reported defect with the SAVED Mendoza-style semantic material (no live LLM / no
// paid API): competing OUTCOMES (Confidential Settlement, Plaintiff Verdict, Defendant Verdict,
// Continued Litigation) were leaking into the shared dependency (factor) forest, and every candidate
// was wired to every factor via an unconditional all-to-all DEPENDS_ON mapping.
//
// The tests exercise the REAL adapter seam (IntelligenceWide2Service.BuildGateProposal +
// BuildGateEnrichment), run the shared normalization gate, and project the existing Factor Inventory,
// asserting the corrected structure. The service's heavy constructor is bypassed with
// GetUninitializedObject because only the pure deterministic projection logic is under test.
// ────────────────────────────────────────────────────────────────────────────────────────────────
public sealed class MendozaFactorIdentificationTests
{
    // Competing outcomes the tribunal could reach — these must remain CANDIDATES, never factors.
    private static readonly string[] MendozaOutcomes =
    [
        "Confidential Settlement",
        "Plaintiff Verdict",
        "Defendant Verdict",
        "Continued Litigation",
        "Limitations Dismissal",
    ];

    // Genuine shared dependency propositions (procedural / legal / factual sub-issues). Note that some
    // deliberately share a single term with an outcome ("Settlement agreement enforceability" shares
    // "settlement") to prove the subset guard does not misclassify a real sub-issue as an outcome.
    private static readonly (string Code, string Label, string Interpretation)[] MendozaDependencies =
    [
        ("B1", "Settlement agreement enforceability", "Whether an executed settlement agreement is enforceable, including consideration and release."),
        ("B2", "Confidentiality clause", "Whether the parties agreed to and executed a confidentiality clause."),
        ("B3", "Applicable limitations period rule", "Which statute of limitations governs and its length."),
        ("B4", "Accrual and commencement date", "When the cause of action accrued and the limitations clock began."),
        ("B5", "Filing and service compliance", "Whether the complaint was timely filed and served."),
        ("B6", "Medical lien resolution", "Whether outstanding medical liens have been resolved from any payment."),
        ("B7", "Comparative fault apportionment", "How comparative fault is apportioned between the parties."),
    ];

    private static IntelligenceWide2Service NewService()
    {
        var service = (IntelligenceWide2Service)RuntimeHelpers.GetUninitializedObject(typeof(IntelligenceWide2Service));
        var matter = MatterContextSnapshot.Empty with
        {
            MatterId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            DomainPackCode = "PI_SETTLEMENT",
            PracticeAreaCode = "PI",
            Decision =
            [
                // A supplied assertion — relevant to agreement status, NOT proof of an executed agreement.
                new MatterContextField("Settlement Status", "Agreed", MatterFieldProvenance.Supplied),
            ],
        };
        typeof(IntelligenceWide2Service).GetField("_matterContext", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(service, matter);
        typeof(IntelligenceWide2Service).GetField("_decisionIntent", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(service, new DecisionIntentResolver.Resolution(DecisionIntent.Evaluate, "evaluate", false));
        return service;
    }

    private static IReadOnlyCollection<WideBranchRecord> BuildSurvivors()
    {
        var tenant = Guid.NewGuid();
        var exec = Guid.NewGuid();
        var records = new List<WideBranchRecord>();
        var sort = 0;

        // Genuine dependency sub-issues (survive into the factor forest).
        foreach (var (code, label, interp) in MendozaDependencies)
            records.Add(new WideBranchRecord(
                Guid.NewGuid(), exec, null, tenant, 1, code, label, interp,
                null, null, "Grounded", 0, 0m, true, null, false, null, sort++));

        // Competing OUTCOMES that (in the defect) leaked into the dependency forest — must be excluded.
        foreach (var outcome in MendozaOutcomes)
            records.Add(new WideBranchRecord(
                Guid.NewGuid(), exec, null, tenant, 1, $"OUT{sort}", outcome, "Competing outcome disposition.",
                null, null, "Grounded", 0, 0m, true, null, false, null, sort++));

        return records;
    }

    private static IReadOnlyCollection<WideInterpretiveResultDto> NoInterpretive() => [];

    // Drive the real adapter: BuildGateProposal (private) -> BuildGateEnrichment (private) -> shared gate.
    private static (LegalDecisionService.LegalDecisionRegistrationPlan Plan, int OutcomeNodeCount) RunAdapterGate(
        IntelligenceWide2Service service,
        IReadOnlyCollection<string> candidateUniverse,
        IReadOnlyCollection<WideBranchRecord> survivors)
    {
        var t = typeof(IntelligenceWide2Service);

        var buildProposal = t.GetMethod("BuildGateProposal", BindingFlags.NonPublic | BindingFlags.Instance)!;
        object?[] args = [candidateUniverse, survivors, NoInterpretive(), 0];
        var proposal = buildProposal.Invoke(service, args)!;
        var outcomeNodeCount = (int)args[3]!;

        var buildEnrichment = t.GetMethod("BuildGateEnrichment", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var enrichment = buildEnrichment.Invoke(service, [proposal, survivors])!;

        var run = typeof(LegalDecisionService).GetMethod("RunNormalizationGate", BindingFlags.NonPublic | BindingFlags.Static)!;
        var gateResult = run.Invoke(null, [proposal, enrichment])!;
        var plan = (LegalDecisionService.LegalDecisionRegistrationPlan)
            gateResult.GetType().GetProperty("Plan")!.GetValue(gateResult)!;

        return (plan, outcomeNodeCount);
    }

    private static WideFactorInventoryDto Project(
        IntelligenceWide2Service service,
        LegalDecisionService.LegalDecisionRegistrationPlan plan)
    {
        var matter = (MatterContextSnapshot?)typeof(IntelligenceWide2Service)
            .GetField("_matterContext", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(service);
        return IntelligenceWide2Service.ProjectFactorInventory(
            plan, matter, domainPackResolved: false, domainPackCode: "PI_SETTLEMENT", anyCandidateDelivered: true);
    }

    [Fact]
    public void CompetingOutcomes_AreNotRegisteredAsDependencyFactors()
    {
        var service = NewService();
        var (plan, _) = RunAdapterGate(service, MendozaOutcomes, BuildSurvivors());

        var factorLabels = plan.Dependencies.Select(d => d.Label).ToArray();
        foreach (var outcome in MendozaOutcomes)
            Assert.DoesNotContain(outcome, factorLabels, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SharedFactorForest_ContainsOnlyGenuineDependencies_RegisteredOnce()
    {
        var service = NewService();
        var (plan, _) = RunAdapterGate(service, MendozaOutcomes, BuildSurvivors());

        // Exactly the seven genuine sub-issues, each registered once.
        Assert.Equal(MendozaDependencies.Length, plan.Dependencies.Count);
        Assert.Equal(
            plan.Dependencies.Select(d => d.DependencyId).Distinct(StringComparer.OrdinalIgnoreCase).Count(),
            plan.Dependencies.Count);
    }

    [Fact]
    public void ConfidentialSettlement_ReceivesRelevantSettlementFactors()
    {
        var service = NewService();
        var (plan, _) = RunAdapterGate(service, MendozaOutcomes, BuildSurvivors());

        var candidate = plan.Candidates.Single(c =>
            string.Equals(c.DisplayName, "Confidential Settlement", StringComparison.OrdinalIgnoreCase));
        var linkedFactors = plan.Edges
            .Where(e => string.Equals(e.CandidateSemanticId, candidate.RepresentativeSemanticId, StringComparison.OrdinalIgnoreCase))
            .Select(e => plan.Dependencies.First(d => string.Equals(d.DependencyId, e.DependencyId, StringComparison.OrdinalIgnoreCase)).Label)
            .ToArray();

        Assert.Contains("Settlement agreement enforceability", linkedFactors, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("Confidentiality clause", linkedFactors, StringComparer.OrdinalIgnoreCase);
        // It must NOT be linked to an unrelated limitations sub-issue.
        Assert.DoesNotContain("Accrual and commencement date", linkedFactors, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void LimitationsDismissal_ReceivesRelevantLimitationsFactors()
    {
        var service = NewService();
        var (plan, _) = RunAdapterGate(service, MendozaOutcomes, BuildSurvivors());

        var candidate = plan.Candidates.Single(c =>
            string.Equals(c.DisplayName, "Limitations Dismissal", StringComparison.OrdinalIgnoreCase));
        var linkedFactors = plan.Edges
            .Where(e => string.Equals(e.CandidateSemanticId, candidate.RepresentativeSemanticId, StringComparison.OrdinalIgnoreCase))
            .Select(e => plan.Dependencies.First(d => string.Equals(d.DependencyId, e.DependencyId, StringComparison.OrdinalIgnoreCase)).Label)
            .ToArray();

        Assert.Contains("Applicable limitations period rule", linkedFactors, StringComparer.OrdinalIgnoreCase);
        // A pure settlement confidentiality clause is not a limitations factor.
        Assert.DoesNotContain("Confidentiality clause", linkedFactors, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void NoAutomaticAllToAllMapping_And_EveryEdgeHasRationale()
    {
        var service = NewService();
        var survivors = BuildSurvivors();
        var (plan, _) = RunAdapterGate(service, MendozaOutcomes, survivors);

        // All-to-all would be candidates × factors. The corrected mapping must be strictly fewer.
        var allToAll = plan.Candidates.Count * plan.Dependencies.Count;
        Assert.True(plan.Edges.Count < allToAll,
            $"Expected relevance-gated edges (< {allToAll}) but got the all-to-all count {plan.Edges.Count}.");

        // Every edge carries a semantic relation type and a recorded rationale.
        Assert.All(plan.Edges, e =>
        {
            Assert.False(string.IsNullOrWhiteSpace(e.RelationType));
            Assert.NotEqual("DEPENDS_ON", e.RelationType, StringComparer.OrdinalIgnoreCase);
            Assert.False(string.IsNullOrWhiteSpace(e.Rationale));
        });
    }

    [Fact]
    public void FactorInventory_ProjectsCorrectedPlan_WithoutOutcomeFactors()
    {
        var service = NewService();
        var (plan, _) = RunAdapterGate(service, MendozaOutcomes, BuildSurvivors());

        var inventory = Project(service, plan);

        // One factor per genuine dependency; no competing outcome present as a factor.
        Assert.Equal(plan.Dependencies.Count, inventory.Factors.Count);
        foreach (var outcome in MendozaOutcomes)
            Assert.DoesNotContain(outcome, inventory.Factors.Select(f => f.FactorName), StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void SuppliedSettlementStatus_DoesNotBecomeVerifiedEvidence()
    {
        var service = NewService();
        var (plan, _) = RunAdapterGate(service, MendozaOutcomes, BuildSurvivors());

        var inventory = Project(service, plan);

        // No factor may be reported as VERIFIED — the dynamic path attaches no admitted evidence, and a
        // supplied "Settlement Status = Agreed" assertion is at most SUPPLIED, never verified.
        Assert.All(inventory.Factors, f =>
            Assert.NotEqual("VERIFIED", f.VerificationStatus, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void IncompatibleFactorsWithoutMatterValue_StayMissingAndExplicit()
    {
        var service = NewService();
        var (plan, _) = RunAdapterGate(service, MendozaOutcomes, BuildSurvivors());

        var inventory = Project(service, plan);

        // The limitations accrual factor has no semantically-compatible supplied matter value, so it must
        // report MISSING with explicit missing information rather than being fabricated from "Agreed".
        var accrual = inventory.Factors.Single(f =>
            string.Equals(f.FactorName, "Accrual and commencement date", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("MISSING", accrual.Availability, StringComparer.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(accrual.MissingInformation));
    }
}
