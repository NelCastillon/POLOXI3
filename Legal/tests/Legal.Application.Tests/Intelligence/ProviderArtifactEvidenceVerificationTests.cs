using Legal.Application.Features.Intelligence.Decision.Core;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class ProviderArtifactEvidenceVerificationTests
{
    private const string Proposition = "Covered employers must pay overtime compensation for hours worked beyond forty in a workweek.";
    private const string Passage = "Covered employers must pay overtime compensation for hours worked beyond forty in a workweek, subject to the exemptions stated in this part.";

    [Fact]
    public async Task OfficialEcfrArtifact_CompletesVerificationWithoutPlaywright_AndAuthorizesEvidence()
    {
        var inspector = new RejectingInspector();
        var pipeline = BuildPipeline(inspector);

        var result = await pipeline.VerifyAsync(Request("https://www.ecfr.gov/current/title-29/subtitle-B/chapter-V/subchapter-B/part-778"));

        Assert.Equal(0, inspector.CallCount);
        Assert.Equal(VerificationCheckState.Passed, result.Identity.State);
        Assert.Equal("SOURCE_IDENTITY_PROVIDER_VERIFIED", result.Identity.ReasonCode);
        Assert.Equal(VerificationCheckState.Passed, result.Citation.State);
        Assert.Equal(VerificationCheckState.Passed, result.Passage.State);
        Assert.Equal(PropositionSupportState.Supported, result.PropositionSupport.State);
        Assert.Equal(VerificationCheckState.Passed, result.Authority.State);
        Assert.True(result.IsVerified);
        Assert.True(result.IsDecisionAuthorized);
    }

    [Fact]
    public async Task SpoofedProviderArtifact_FallsBackToInspector_AndCannotGainAuthority()
    {
        var inspector = new RejectingInspector();
        var pipeline = BuildPipeline(inspector);

        var result = await pipeline.VerifyAsync(Request("https://attacker.example/fake-ecfr"));

        Assert.Equal(1, inspector.CallCount);
        Assert.Equal(VerificationCheckState.Failed, result.Identity.State);
        Assert.False(result.IsVerified);
        Assert.False(result.IsDecisionAuthorized);
        Assert.Equal(0, result.Telemetry.SemanticVerificationCount);
    }

    private static EvidenceVerificationRequest Request(string sourceRef) => new(
        Guid.NewGuid(), Guid.NewGuid(), Proposition, sourceRef, "29 CFR Part 778 — Overtime Compensation", Passage,
        EvidenceSourceType.Regulation, "US", new DateOnly(2024, 1, 1), new DateOnly(2025, 1, 1), true)
    {
        SourceProvider = "ECFR",
        SourceVersion = "SEARCH_API_V1",
        ProviderIdentityVerified = true,
        PassageRef = sourceRef,
        ExtractionVersion = "SEARCH_API_V1",
    };

    private static IndependentEvidenceVerificationPipeline BuildPipeline(IWebSourceInspector inspector) => new(
        new DeterministicEvidenceSourceClassifier(),
        new VerificationProfileProvider(),
        new PlaywrightIdentityEvidenceVerifier(inspector),
        new PlaywrightCitationEvidenceVerifier(inspector),
        new PlaywrightPassageEvidenceVerifier(inspector),
        new DeterministicVerificationCandidatePreScreen(),
        new SupportedSemanticVerifier(),
        new DisabledPoloxiVerificationDeepener(),
        new DeterministicAuthorityEvidenceVerifier(),
        new EvidenceVerificationAggregator());

    private sealed class RejectingInspector : IWebSourceInspector
    {
        public int CallCount { get; private set; }

        public Task<WebSourceInspectionResult> InspectAsync(string sourceRef, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new WebSourceInspectionResult
            {
                Attempted = true,
                Resolved = false,
                FailureReason = "PLAYWRIGHT_BROWSER_UNAVAILABLE:PlaywrightException",
                VerificationMethod = "TEST_REJECTING_INSPECTOR",
            });
        }
    }

    private sealed class SupportedSemanticVerifier : ISemanticEvidenceVerifier
    {
        public Task<SemanticVerificationResult> VerifyAsync(
            EvidenceVerificationRequest request,
            VerificationProfile profile,
            VerificationCheckResult passage,
            CancellationToken cancellationToken = default) => Task.FromResult(new SemanticVerificationResult
            {
                PropositionSupport = new PropositionSupportResult
                {
                    State = PropositionSupportState.Supported,
                    Proposition = request.Proposition,
                    SupportingPassage = passage.SupportingPassage,
                    PassageRef = passage.PassageRef,
                    SupportedComponents = [request.Proposition],
                    ReasonCode = "TEST_SUPPORTED",
                    VerificationMethod = "DETERMINISTIC_POSITIVE_CONTROL",
                },
                StatementRole = VerificationCheckResult.NotApplicable("STATEMENT_ROLE_NOT_REQUIRED", "Not required for regulation profile."),
                Holding = VerificationCheckResult.NotApplicable("HOLDING_NOT_REQUIRED", "Not required for regulation profile."),
                CallCount = 1,
            });
    }
}
