using Legal.Application.Features.Intelligence.Decision.Core;

namespace Legal.Application.Tests.Intelligence;

internal static class DeterministicEvidenceVerificationFixture
{
    public static IIndependentEvidenceVerificationPipeline Pipeline(
        EvidenceVerificationFactor? failedFactor = null,
        bool semanticAmbiguous = false,
        IPoloxiVerificationDeepener? poloxiDeepener = null) => new IndependentEvidenceVerificationPipeline(
        new DeterministicEvidenceSourceClassifier(),
        new VerificationProfileProvider(),
        new DeterministicTestIdentityVerifier(failedFactor == EvidenceVerificationFactor.Identity),
        new DeterministicTestCitationVerifier(failedFactor == EvidenceVerificationFactor.Citation),
        new DeterministicTestPassageVerifier(failedFactor == EvidenceVerificationFactor.Passage),
        new DeterministicVerificationCandidatePreScreen(),
        new DeterministicTestSemanticVerifier(failedFactor, semanticAmbiguous),
        poloxiDeepener ?? new DisabledPoloxiVerificationDeepener(),
        new DeterministicAuthorityEvidenceVerifier(),
        new EvidenceVerificationAggregator());

    private sealed class DeterministicTestIdentityVerifier(bool forceFailure) : IIdentityEvidenceVerifier
    {
        public EvidenceVerificationFactor Factor => EvidenceVerificationFactor.Identity;
        public Task<VerificationCheckResult> VerifyAsync(EvidenceVerificationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result(!forceFailure && !string.IsNullOrWhiteSpace(request.SourceRef) && !string.IsNullOrWhiteSpace(request.SourceTitle),
                "TEST_IDENTITY", request));
    }

    private sealed class DeterministicTestSemanticVerifier(
        EvidenceVerificationFactor? failedFactor,
        bool ambiguous) : ISemanticEvidenceVerifier
    {
        private readonly DeterministicPropositionSupportVerifier _proposition = new();
        private readonly DeterministicHoldingEvidenceVerifier _holding = new();

        public async Task<SemanticVerificationResult> VerifyAsync(
            EvidenceVerificationRequest request,
            VerificationProfile profile,
            VerificationCheckResult passage,
            CancellationToken cancellationToken = default)
        {
            var proposition = await _proposition.VerifyAsync(request, passage, cancellationToken);
            if (failedFactor == EvidenceVerificationFactor.PropositionSupport)
                proposition = proposition with { State = PropositionSupportState.Unsupported, ReasonCode = "TEST_PROPOSITION_FAILURE" };
            var holding = profile.RequireHolding
                ? await _holding.VerifyAsync(request, passage, cancellationToken)
                : VerificationCheckResult.NotApplicable("HOLDING_NOT_REQUIRED", "Holding is not required.");
            if (failedFactor == EvidenceVerificationFactor.Holding)
                holding = Result(false, "TEST_HOLDING_FAILURE", request);
            var statementRole = profile.RequireStatementRole
                ? Result(failedFactor != EvidenceVerificationFactor.StatementRole, "TEST_STATEMENT_ROLE", request)
                : VerificationCheckResult.NotApplicable("STATEMENT_ROLE_NOT_REQUIRED", "Statement role is not required.");
            return new SemanticVerificationResult
            {
                PropositionSupport = proposition,
                StatementRole = statementRole,
                Holding = holding,
                Ambiguous = ambiguous,
                CallCount = 1,
            };
        }
    }

    private sealed class DeterministicTestCitationVerifier(bool forceFailure) : ICitationEvidenceVerifier
    {
        public EvidenceVerificationFactor Factor => EvidenceVerificationFactor.Citation;
        public Task<VerificationCheckResult> VerifyAsync(EvidenceVerificationRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result(!forceFailure && Uri.TryCreate(request.SourceRef, UriKind.Absolute, out _), "TEST_CITATION", request));
    }

    private sealed class DeterministicTestPassageVerifier(bool forceFailure) : IPassageEvidenceVerifier
    {
        public EvidenceVerificationFactor Factor => EvidenceVerificationFactor.Passage;
        public Task<VerificationCheckResult> VerifyAsync(EvidenceVerificationRequest request, CancellationToken cancellationToken = default)
        {
            var passed = !forceFailure && !string.IsNullOrWhiteSpace(request.SourceText)
                && !DecisionCoreMath.PassageEchoesTitle(request.SourceTitle, request.SourceText);
            return Task.FromResult(Result(passed, "TEST_PASSAGE", request) with
            {
                SupportingPassage = passed ? request.SourceText : null,
            });
        }
    }

    private static VerificationCheckResult Result(bool passed, string code, EvidenceVerificationRequest request) => new()
    {
        State = passed ? VerificationCheckState.Passed : VerificationCheckState.Failed,
        ReasonCode = code,
        Reason = code,
        SourceRef = request.SourceRef,
        VerificationMethod = "DETERMINISTIC_TEST_DOUBLE",
    };
}
