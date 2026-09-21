using Legal.Application.Abstractions.Intelligence;
using Legal.Application.Features.Intelligence.Decision;
using Legal.Application.Features.Intelligence.Decision.Core;
using Legal.Application.Features.Intelligence.Epistemic;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

[Collection("ResearchLoopSerial")]
public sealed class IndependentEvidenceVerificationBenchmarkTests
{
    private const string Proposition =
        "employees worked overtime hours for which they were not compensated";

    [Fact]
    public async Task PositiveControl_MatterDocument_SupportsPropositionAndGrantsAuthority()
    {
        var result = await DeterministicEvidenceVerificationFixture.Pipeline().VerifyAsync(Request(
            title: "Payroll and time records",
            text: "The records show employees worked overtime hours for which they were not compensated.",
            sourceType: EvidenceSourceType.MatterDocument));

        Assert.Equal(PropositionSupportState.Supported, result.PropositionSupport.State);
        Assert.Equal(EvidenceSupportDisposition.Supported, result.Disposition);
        Assert.True(result.IsVerified);
        Assert.True(result.IsDecisionAuthorized);
        Assert.Empty(result.BlockingReasons);
    }

    [Fact]
    public async Task CandidatePreScreen_UnrelatedPassageRejectsSemanticSpendWithoutGrantingAuthority()
    {
        var result = await DeterministicEvidenceVerificationFixture.Pipeline().VerifyAsync(Request(
            title: "Unrelated business record",
            text: "Quarterly inventory reconciliation lists warehouse equipment and replacement parts."));

        Assert.Equal(PropositionSupportState.NotEvaluated, result.PropositionSupport.State);
        Assert.Equal(EvidenceSupportDisposition.Unverifiable, result.Disposition);
        Assert.False(result.IsVerified);
        Assert.False(result.IsDecisionAuthorized);
        Assert.Equal(1, result.Telemetry.PreScreenRejectedCount);
        Assert.Equal(0, result.Telemetry.SemanticVerificationCount);
    }

    [Fact]
    public async Task CandidatePreScreen_PlausiblePassageAllowsIndependentSemanticVerification()
    {
        var result = await DeterministicEvidenceVerificationFixture.Pipeline().VerifyAsync(Request());

        Assert.Equal(0, result.Telemetry.PreScreenRejectedCount);
        Assert.Equal(1, result.Telemetry.SemanticVerificationCount);
        Assert.True(result.IsDecisionAuthorized);
    }

    [Fact]
    public async Task MatterDocument_SamePassageDifferentPropositionsProduceIndependentOutcomes()
    {
        const string declaration = "I typically worked about 55 hours every week.";
        var verifier = new DeterministicPropositionSupportVerifier();
        var passage = Passed() with { SupportingPassage = declaration };

        var statement = await verifier.VerifyAsync(
            Request(text: declaration, sourceType: EvidenceSourceType.Declaration) with
            { Proposition = "I typically worked about 55 hours every week" }, passage);
        var legalConclusion = await verifier.VerifyAsync(
            Request(text: declaration, sourceType: EvidenceSourceType.Declaration) with
            { Proposition = "The employer violated the Fair Labor Standards Act" }, passage);

        Assert.Equal(PropositionSupportState.Supported, statement.State);
        Assert.NotEqual(PropositionSupportState.Supported, legalConclusion.State);
    }

    [Fact]
    public async Task ContradictoryMatterDocumentsRemainIndependentGroundedEvidence()
    {
        var verifier = new DeterministicPropositionSupportVerifier();
        var declaration = await verifier.VerifyAsync(
            Request(text: "The employee worked 55 hours.", sourceType: EvidenceSourceType.Declaration) with
            { Proposition = "The employee worked 55 hours" },
            Passed() with { SupportingPassage = "The employee worked 55 hours." });
        var payroll = await verifier.VerifyAsync(
            Request(text: "The payroll record shows 40 hours.", sourceType: EvidenceSourceType.BusinessRecord) with
            { Proposition = "The payroll record shows 40 hours" },
            Passed() with { SupportingPassage = "The payroll record shows 40 hours." });

        Assert.Equal(PropositionSupportState.Supported, declaration.State);
        Assert.Equal(PropositionSupportState.Supported, payroll.State);
        Assert.NotEqual(declaration.Proposition, payroll.Proposition);
    }

    [Fact]
    public async Task RepeatedDeterministicVerification_HasStableDispositionFactorsAndCost()
    {
        var pipeline = DeterministicEvidenceVerificationFixture.Pipeline();
        var request = Request();
        var runs = new List<EvidenceVerificationResult>();
        for (var i = 0; i < 10; i++)
            runs.Add(await pipeline.VerifyAsync(request));

        Assert.Single(runs.Select(r => r.Disposition).Distinct());
        Assert.Single(runs.Select(r => r.IsDecisionAuthorized).Distinct());
        Assert.Single(runs.Select(r => r.Telemetry.SemanticVerificationCount).Distinct());
        Assert.Equal(0, runs.Sum(r => r.Telemetry.PoloxiDeepeningCount));
    }

    [Theory]
    [InlineData(false, true, 0)]
    [InlineData(true, false, 0)]
    [InlineData(true, true, 1)]
    public async Task PoloxiDeepening_RequiresAmbiguousDecisionMaterialEvidence(
        bool decisionMaterial,
        bool ambiguous,
        int expectedCalls)
    {
        var deepener = new RecordingPoloxiDeepener();
        var result = await DeterministicEvidenceVerificationFixture
            .Pipeline(semanticAmbiguous: ambiguous, poloxiDeepener: deepener)
            .VerifyAsync(Request(decisionMaterial: decisionMaterial));

        Assert.Equal(expectedCalls, deepener.CallCount);
        Assert.Equal(expectedCalls, result.Telemetry.PoloxiDeepeningCount);
    }

    [Fact]
    public async Task AblationMatrix_CurrentIndependentSignalGateAndClosedLoop_BlockAdversarialAuthority()
    {
        var positiveSource = new DecisionRetrievedSource(
            "https://example.test/payroll", "Payroll records",
            "employees worked overtime hours for which they were not compensated");
        var adversarialSource = new DecisionRetrievedSource(
            "https://example.test/order", "Executive Order and Federal Contractors",
            "The order prohibits federal contractors from hiring permanent replacements for striking employees.");

        var current = new BenchmarkMetric(
            "CURRENT",
            PositiveAuthority: IsLegacyAuthorized(positiveSource) ? 1 : 0,
            AdversarialAuthority: IsLegacyAuthorized(adversarialSource) ? 1 : 0,
            DownstreamMutations: 0);

        var pipeline = DeterministicEvidenceVerificationFixture.Pipeline();
        var independentPositive = await pipeline.VerifyAsync(Request(
            sourceRef: positiveSource.SourceRef, title: positiveSource.Title,
            text: positiveSource.Snippet, sourceType: EvidenceSourceType.MatterDocument));
        var independentAdversarial = await pipeline.VerifyAsync(Request(
            sourceRef: adversarialSource.SourceRef, title: adversarialSource.Title,
            text: adversarialSource.Snippet, sourceType: EvidenceSourceType.MatterDocument));
        var independent = new BenchmarkMetric(
            "INDEPENDENT",
            independentPositive.IsDecisionAuthorized ? 1 : 0,
            independentAdversarial.IsDecisionAuthorized ? 1 : 0,
            DownstreamMutations: 0);

        var branchId = Guid.NewGuid();
        var signalGate = new VerifiedDecisionSignalService();
        var positiveSignals = signalGate.Project([Signal(branchId, independentPositive)]);
        var adversarialSignals = signalGate.Project([Signal(branchId, independentAdversarial)]);
        var gated = new BenchmarkMetric(
            "SIGNAL_GATE",
            PositiveAuthority: positiveSignals.Count,
            AdversarialAuthority: adversarialSignals.Count,
            DownstreamMutations: 0);

        var positiveRepo = RollbackFixture.SeededRepository(out _);
        await RollbackFixture.Service(positiveRepo, new DependencyPropagationService())
            .RunResearchLoopAsync(positiveRepo.Session.TenantId, positiveRepo.Session.DecisionSessionId, default);
        var blockedRepo = RollbackFixture.SeededRepository(out _);
        await RollbackFixture.Service(
                blockedRepo,
                new DependencyPropagationService(),
                DeterministicEvidenceVerificationFixture.Pipeline(EvidenceVerificationFactor.Passage))
            .RunResearchLoopAsync(blockedRepo.Session.TenantId, blockedRepo.Session.DecisionSessionId, default);
        var closedLoop = new BenchmarkMetric(
            "CLOSED_LOOP",
            PositiveAuthority: positiveRepo.DependencyEvents.Count,
            AdversarialAuthority: blockedRepo.DependencyEvents.Count,
            DownstreamMutations: positiveRepo.PersistRecompetitionCount + blockedRepo.PersistRecompetitionCount);

        var metrics = new[] { current, independent, gated, closedLoop };
        Assert.All(metrics, metric => Assert.Equal(1, metric.PositiveAuthority));
        Assert.All(metrics, metric => Assert.Equal(0, metric.AdversarialAuthority));
        Assert.Equal(1, closedLoop.DownstreamMutations);
    }

    [Fact]
    public async Task AdversarialTitleEcho_FailsPassageAndShortCircuitsDownstreamFactors()
    {
        var result = await DeterministicEvidenceVerificationFixture.Pipeline().VerifyAsync(Request(
            title: "Proposed Rule on Overtime Pay",
            text: "Proposed Rule on Overtime Pay",
            sourceType: EvidenceSourceType.Regulation));

        Assert.Equal(VerificationCheckState.Failed, result.Passage.State);
        Assert.Equal(PropositionSupportState.NotEvaluated, result.PropositionSupport.State);
        Assert.Equal(VerificationCheckState.NotEvaluated, result.Authority.State);
        Assert.False(result.IsVerified);
        Assert.False(result.IsDecisionAuthorized);
        Assert.Contains(result.BlockingReasons, reason => reason.StartsWith("PASSAGE:FAILED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AdversarialUnrelatedPassage_DoesNotGainPositiveAuthority()
    {
        var result = await DeterministicEvidenceVerificationFixture.Pipeline().VerifyAsync(Request(
            title: "Executive Order and Federal Contractors",
            text: "The order prohibits federal contractors from hiring permanent replacements for striking employees.",
            sourceType: EvidenceSourceType.MatterDocument));

        Assert.Equal(PropositionSupportState.Unsupported, result.PropositionSupport.State);
        Assert.Equal(EvidenceSupportDisposition.Unsupported, result.Disposition);
        Assert.False(result.IsVerified);
        Assert.False(result.IsDecisionAuthorized);
        Assert.Contains(result.BlockingReasons,
            reason => reason.StartsWith("PROPOSITION_SUPPORT:UNSUPPORTED", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MissingCitation_CaseLawShortCircuitsBeforePassageAndSupport()
    {
        var result = await DeterministicEvidenceVerificationFixture.Pipeline().VerifyAsync(Request(
            sourceRef: "not-an-absolute-uri",
            title: "Acme Logistics v. Delgado",
            text: "We hold that employees worked overtime hours for which they were not compensated.",
            sourceType: EvidenceSourceType.CaseLaw));

        Assert.Equal(VerificationCheckState.Failed, result.Citation.State);
        Assert.Equal(VerificationCheckState.NotEvaluated, result.Passage.State);
        Assert.Equal(PropositionSupportState.NotEvaluated, result.PropositionSupport.State);
        Assert.False(result.IsDecisionAuthorized);
        Assert.Contains("CITATION:FAILED", result.BlockingReasons);
    }

    [Theory]
    [InlineData(EvidenceVerificationFactor.Identity)]
    [InlineData(EvidenceVerificationFactor.Provenance)]
    [InlineData(EvidenceVerificationFactor.Citation)]
    [InlineData(EvidenceVerificationFactor.Passage)]
    [InlineData(EvidenceVerificationFactor.PropositionSupport)]
    [InlineData(EvidenceVerificationFactor.StatementRole)]
    [InlineData(EvidenceVerificationFactor.Holding)]
    [InlineData(EvidenceVerificationFactor.Authority)]
    public void RequiredFactorAblation_IndependentlyBlocksDecisionAuthority(EvidenceVerificationFactor ablated)
    {
        var request = Request(
            title: "Acme Logistics v. Delgado",
            text: "We hold that employees worked overtime hours for which they were not compensated.",
            sourceType: EvidenceSourceType.CaseLaw);
        var passed = Passed();
        var proposition = new PropositionSupportResult
        {
            State = ablated == EvidenceVerificationFactor.PropositionSupport
                ? PropositionSupportState.Unsupported
                : PropositionSupportState.Supported,
            Proposition = Proposition,
            ReasonCode = ablated == EvidenceVerificationFactor.PropositionSupport
                ? "ABLATION_PROPOSITION_SUPPORT"
                : "BENCHMARK_SUPPORTED",
            VerificationMethod = "CONTROLLED_BENCHMARK",
        };

        VerificationCheckResult Factor(EvidenceVerificationFactor factor) =>
            factor == ablated ? Failed($"ABLATION_{EvidenceVerificationCodes.Factor(factor)}") : passed;

        var result = new EvidenceVerificationAggregator().Aggregate(
            request,
            EvidenceSourceType.CaseLaw,
            new VerificationProfile("CASE_LAW_BENCHMARK", true, true, true, true, true, true, true, true),
            Factor(EvidenceVerificationFactor.Identity),
            Factor(EvidenceVerificationFactor.Provenance),
            Factor(EvidenceVerificationFactor.Citation),
            Factor(EvidenceVerificationFactor.Passage),
            proposition,
            Factor(EvidenceVerificationFactor.StatementRole),
            Factor(EvidenceVerificationFactor.Holding),
            Factor(EvidenceVerificationFactor.Authority));

        Assert.False(result.IsVerified);
        Assert.False(result.IsDecisionAuthorized);
        Assert.NotEmpty(result.BlockingReasons);
    }

    [Fact]
    public void CompleteRequiredFactorControl_GrantsDecisionAuthority()
    {
        var passed = Passed();
        var result = new EvidenceVerificationAggregator().Aggregate(
            Request(sourceType: EvidenceSourceType.CaseLaw),
            EvidenceSourceType.CaseLaw,
            new VerificationProfile("CASE_LAW_BENCHMARK", true, true, true, true, true, true, true, true),
            passed, passed, passed, passed,
            new PropositionSupportResult
            {
                State = PropositionSupportState.Supported,
                Proposition = Proposition,
                ReasonCode = "BENCHMARK_SUPPORTED",
                VerificationMethod = "CONTROLLED_BENCHMARK",
            },
            passed, passed, passed);

        Assert.True(result.IsVerified);
        Assert.True(result.IsDecisionAuthorized);
        Assert.Empty(result.BlockingReasons);
    }

    private static EvidenceVerificationRequest Request(
        string sourceRef = "https://example.test/source",
        string title = "Source",
        string text = "employees worked overtime hours for which they were not compensated",
        EvidenceSourceType sourceType = EvidenceSourceType.MatterDocument,
        bool decisionMaterial = false) => new(
            Guid.NewGuid(), null, Proposition, sourceRef, title, text, sourceType,
            Jurisdiction: "US", AuthorityDate: new DateOnly(2024, 1, 1), CutoffDate: new DateOnly(2025, 1, 1),
            DecisionMaterial: decisionMaterial);

    private static VerificationCheckResult Passed() => new()
    {
        State = VerificationCheckState.Passed,
        ReasonCode = "BENCHMARK_PASSED",
        VerificationMethod = "CONTROLLED_BENCHMARK",
    };

    private static VerificationCheckResult Failed(string reasonCode) => new()
    {
        State = VerificationCheckState.Failed,
        ReasonCode = reasonCode,
        VerificationMethod = "CONTROLLED_BENCHMARK",
    };

    private static bool IsLegacyAuthorized(DecisionRetrievedSource source) =>
        LegalDecisionService.VerifyRetrievedSource(source, Proposition, branchId: null).VerificationStatus
        == DecisionVerificationStates.Verified;

    private static DecisionSupportSignal Signal(Guid branchId, EvidenceVerificationResult result) => new()
    {
        DecisionSessionId = Guid.NewGuid(),
        Statement = Proposition,
        NormalizedStatement = Proposition,
        Origin = DecisionSupportOrigin.RetrievedEvidence,
        VerificationState = result.IsDecisionAuthorized
            ? DecisionSupportVerificationState.Supported
            : DecisionSupportVerificationState.Unverified,
        RequiresVerification = true,
        VerificationStrength = result.IsDecisionAuthorized ? 1m : 0m,
        DecisionImpact = 1m,
        SourceBranchId = branchId,
    };

    private sealed record BenchmarkMetric(
        string Mode,
        int PositiveAuthority,
        int AdversarialAuthority,
        int DownstreamMutations);

    private sealed class RecordingPoloxiDeepener : IPoloxiVerificationDeepener
    {
        public int CallCount { get; private set; }

        public Task<SemanticVerificationResult> DeepenAsync(
            PoloxiVerificationContract contract,
            SemanticVerificationResult current,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            Assert.False(contract.AllowExternalRetrieval);
            Assert.Equal(1, contract.MaxDeepeningRounds);
            Assert.Contains(PropositionSupportState.PartiallySupported, contract.AllowedOutcomes);
            return Task.FromResult(current with { Deepened = true, Ambiguous = false });
        }
    }
}
