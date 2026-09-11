using Legal.Application.Features.Intelligence.Science;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

// ── POLOXI Scientific Reasoning (Mathematics V1) unit tests ────────────────────────────────────────
// These pin the deterministic C#-only verification layer and convergence policy. The core invariant
// under test: discovery confidence NEVER promotes an unverified claim to PROVEN — only deterministic
// verification does.
public sealed class MathVerificationTests
{
    private static readonly DeterministicMathVerifier Verifier = new();

    private static ProofObligation Obligation(ObligationVerificationMethod method) =>
        new()
        {
            ObligationId = "O1",
            Statement = "claim",
            VerificationMethod = method,
            DiscoveryConfidence = 0.99,
        };

    [Fact]
    public void Verify_ScalarEquality_HoldsIsVerified()
    {
        var result = Verifier.Verify(
            Obligation(ObligationVerificationMethod.Algebraic),
            new MathVerificationRequest
            {
                NormalizedClaim = "2 + 2 == 4",
                Method = ObligationVerificationMethod.Algebraic,
                ExpectedRelation = ExpectedRelation.Equal,
                Left = 4d,
                Right = 4d,
            });

        Assert.Equal(ObligationStatus.Verified, result.Status);
        Assert.Equal(CounterexampleStatus.NoneFound, result.CounterexampleStatus);
    }

    [Fact]
    public void Verify_ScalarEquality_FailsIsRefuted()
    {
        var result = Verifier.Verify(
            Obligation(ObligationVerificationMethod.Numeric),
            new MathVerificationRequest
            {
                Method = ObligationVerificationMethod.Numeric,
                NormalizedClaim = "3 == 4",
                ExpectedRelation = ExpectedRelation.Equal,
                Left = 3d,
                Right = 4d,
            });

        Assert.Equal(ObligationStatus.Refuted, result.Status);
        Assert.Equal(CounterexampleStatus.Found, result.CounterexampleStatus);
    }

    [Fact]
    public void Verify_LogicalMethod_StaysOpen()
    {
        var result = Verifier.Verify(
            Obligation(ObligationVerificationMethod.Logical),
            new MathVerificationRequest
            {
                Method = ObligationVerificationMethod.Logical,
                NormalizedClaim = "for all n, P(n)",
            });

        // High discovery confidence must NOT upgrade an undecidable claim.
        Assert.Equal(ObligationStatus.Open, result.Status);
    }

    [Fact]
    public void Verify_CounterexampleSearch_NoneFoundStaysUnresolved()
    {
        var result = Verifier.Verify(
            Obligation(ObligationVerificationMethod.CounterexampleCheck),
            new MathVerificationRequest
            {
                Method = ObligationVerificationMethod.CounterexampleCheck,
                NormalizedClaim = "all values >= 0",
                ExpectedRelation = ExpectedRelation.GreaterThan,
                Right = -1d,
                TestInputs = [1d, 2d, 3d],
            });

        // Absence of a counterexample never proves the claim.
        Assert.Equal(ObligationStatus.Unresolved, result.Status);
        Assert.Equal(CounterexampleStatus.NoneFound, result.CounterexampleStatus);
    }

    [Fact]
    public void Verify_CounterexampleSearch_FindsViolation()
    {
        var result = Verifier.Verify(
            Obligation(ObligationVerificationMethod.CounterexampleCheck),
            new MathVerificationRequest
            {
                Method = ObligationVerificationMethod.CounterexampleCheck,
                NormalizedClaim = "all values > 0",
                ExpectedRelation = ExpectedRelation.GreaterThan,
                Right = 0d,
                TestInputs = [1d, -5d, 3d],
            });

        Assert.Equal(ObligationStatus.Refuted, result.Status);
        Assert.Equal(CounterexampleStatus.Found, result.CounterexampleStatus);
    }

    [Theory]
    [InlineData("6/8", ScientificAnswerType.ExactFraction, "3/4")]
    [InlineData("4/2", ScientificAnswerType.ExactFraction, "2")]
    [InlineData("-2/-4", ScientificAnswerType.ExactFraction, "1/2")]
    [InlineData("3.0", ScientificAnswerType.Numeric, "3")]
    [InlineData("TRUE", ScientificAnswerType.Boolean, "TRUE")]
    [InlineData("proven", ScientificAnswerType.Boolean, "TRUE")]
    [InlineData("{3, 1, 2, 2}", ScientificAnswerType.Set, "{1,2,3}")]
    public void Canonicalize_NormalizesAnswers(string raw, ScientificAnswerType type, string expected)
    {
        Assert.Equal(expected, Verifier.Canonicalize(raw, type));
    }

    [Fact]
    public void Aggregate_ReportsPluralityAndAgreement()
    {
        var result = Verifier.Aggregate(
        [
            new CandidateAnswer { StrategyName = "induction", CanonicalForm = "42" },
            new CandidateAnswer { StrategyName = "direct", CanonicalForm = "42" },
            new CandidateAnswer { StrategyName = "contradiction", CanonicalForm = "7" },
        ]);

        Assert.Equal("42", result.PluralityCanonicalForm);
        Assert.Equal(2d / 3d, result.AgreementRatio, precision: 6);
    }

    [Fact]
    public void Aggregate_EmptyInput_ReturnsZeroAgreement()
    {
        var result = Verifier.Aggregate([]);
        Assert.Null(result.PluralityCanonicalForm);
        Assert.Equal(0d, result.AgreementRatio);
    }

    [Theory]
    [InlineData("2 + 2 == 4")]
    [InlineData("6/8 == 3/4")]
    [InlineData("1/2 + 1/4 == 3/4")]
    [InlineData("2^10 == 1024")]
    [InlineData("(3 + 5) * 2 == 16")]
    [InlineData("0.5 == 1/2")]
    [InlineData("3 < 4")]
    [InlineData("10 >= 10")]
    public void Verify_ClaimExpression_ExactEvaluation_IsVerified(string claim)
    {
        var result = Verifier.Verify(
            Obligation(ObligationVerificationMethod.Algebraic),
            new MathVerificationRequest
            {
                NormalizedClaim = claim,
                Method = ObligationVerificationMethod.Algebraic,
            });

        Assert.Equal(ObligationStatus.Verified, result.Status);
        Assert.Equal(CounterexampleStatus.NoneFound, result.CounterexampleStatus);
    }

    [Theory]
    [InlineData("2 + 2 == 5")]
    [InlineData("6/8 == 2/3")]
    [InlineData("3 > 4")]
    public void Verify_ClaimExpression_ExactEvaluation_IsRefuted(string claim)
    {
        var result = Verifier.Verify(
            Obligation(ObligationVerificationMethod.Algebraic),
            new MathVerificationRequest
            {
                NormalizedClaim = claim,
                Method = ObligationVerificationMethod.Algebraic,
            });

        Assert.Equal(ObligationStatus.Refuted, result.Status);
        Assert.Equal(CounterexampleStatus.Found, result.CounterexampleStatus);
    }

    [Fact]
    public void Verify_UnparseableClaim_WithoutOperands_StaysOpen()
    {
        var result = Verifier.Verify(
            Obligation(ObligationVerificationMethod.Algebraic),
            new MathVerificationRequest
            {
                NormalizedClaim = "f(x) is continuous",
                Method = ObligationVerificationMethod.Algebraic,
            });

        Assert.Equal(ObligationStatus.Open, result.Status);
    }

    [Theory]
    [InlineData("1/2 + 1/4", "3/4")]
    [InlineData("2/4", "1/2")]
    [InlineData("(1 + 1)/4", "1/2")]
    public void Canonicalize_ExactFractionExpression_ReducesExactly(string raw, string expected)
    {
        Assert.Equal(expected, Verifier.Canonicalize(raw, ScientificAnswerType.ExactFraction));
    }

    [Theory]
    [InlineData("x^2 >= 0")]
    [InlineData("x^2 + 1 > 0")]
    [InlineData("2*x == x + x")]
    public void Verify_CounterexampleSampling_NoViolation_StaysUnresolved(string claim)
    {
        var result = Verifier.Verify(
            Obligation(ObligationVerificationMethod.CounterexampleCheck),
            new MathVerificationRequest
            {
                Method = ObligationVerificationMethod.CounterexampleCheck,
                NormalizedClaim = claim,
            });

        Assert.Equal(ObligationStatus.Unresolved, result.Status);
        Assert.Equal(CounterexampleStatus.NoneFound, result.CounterexampleStatus);
    }

    [Theory]
    [InlineData("x^2 > 0")]
    [InlineData("x^2 == x")]
    [InlineData("x >= 0")]
    public void Verify_CounterexampleSampling_FindsViolation_IsRefuted(string claim)
    {
        var result = Verifier.Verify(
            Obligation(ObligationVerificationMethod.CounterexampleCheck),
            new MathVerificationRequest
            {
                Method = ObligationVerificationMethod.CounterexampleCheck,
                NormalizedClaim = claim,
            });

        Assert.Equal(ObligationStatus.Refuted, result.Status);
        Assert.Equal(CounterexampleStatus.Found, result.CounterexampleStatus);
    }

    // ── Gap #3: multi-variable counterexample sampling ───────────────────────
    [Theory]
    [InlineData("x*y == y*x")]
    [InlineData("x + y == y + x")]
    [InlineData("(x + y)^2 == x^2 + 2*x*y + y^2")]
    public void Verify_MultiVariableSampling_NoViolation_StaysUnresolved(string claim)
    {
        var result = Verifier.Verify(
            Obligation(ObligationVerificationMethod.CounterexampleCheck),
            new MathVerificationRequest
            {
                Method = ObligationVerificationMethod.CounterexampleCheck,
                NormalizedClaim = claim,
            });

        Assert.Equal(ObligationStatus.Unresolved, result.Status);
        Assert.Equal(CounterexampleStatus.NoneFound, result.CounterexampleStatus);
    }

    [Theory]
    [InlineData("x*y == x + y")]
    [InlineData("x + y == x - y")]
    [InlineData("x^2 + y^2 == (x + y)^2")]
    public void Verify_MultiVariableSampling_FindsViolation_IsRefuted(string claim)
    {
        var result = Verifier.Verify(
            Obligation(ObligationVerificationMethod.CounterexampleCheck),
            new MathVerificationRequest
            {
                Method = ObligationVerificationMethod.CounterexampleCheck,
                NormalizedClaim = claim,
            });

        Assert.Equal(ObligationStatus.Refuted, result.Status);
        Assert.Equal(CounterexampleStatus.Found, result.CounterexampleStatus);
    }

    // ── Gap #4: richer expression / statement canonicalization ───────────────
    [Theory]
    [InlineData("1/2 + 1/4", "3/4")]
    [InlineData("6/8", "3/4")]
    [InlineData("(3 + 5) * 2", "16")]
    public void Canonicalize_Expression_ReducesConstantExactly(string raw, string expected)
    {
        Assert.Equal(expected, Verifier.Canonicalize(raw, ScientificAnswerType.Expression));
    }

    [Theory]
    [InlineData("x+y", "x + y")]
    [InlineData("a*b  -  c", "a * b - c")]
    public void Canonicalize_Expression_NormalizesOperatorSpacing(string raw, string expected)
    {
        Assert.Equal(expected, Verifier.Canonicalize(raw, ScientificAnswerType.Expression));
    }

    [Theory]
    [InlineData("2 + 2 == 4", "TRUE")]
    [InlineData("3 > 4", "FALSE")]
    [InlineData("6/8 == 3/4", "TRUE")]
    public void Canonicalize_Statement_DecidesConstantRelation(string raw, string expected)
    {
        Assert.Equal(expected, Verifier.Canonicalize(raw, ScientificAnswerType.Statement));
    }
}