using Ams.Application.Features.Commissions;
using Xunit;

namespace Ams.Application.Tests;

public sealed class CommissionAccountingRulesTests
{
    [Theory]
    [InlineData("POL-2025 001", "pol2025001")]
    [InlineData(" AB/12-34 ", "ab1234")]
    public void NormalizePolicyNumber_RemovesFormattingAndNormalizesCase(string source, string equivalent)
        => Assert.Equal(CommissionAccountingRules.NormalizePolicyNumber(source), CommissionAccountingRules.NormalizePolicyNumber(equivalent));

    [Fact]
    public void IsCandidateMatch_RequiresPolicyAmountAndDateWithinTolerance()
    {
        var matches = CommissionAccountingRules.IsCandidateMatch("POL-100", "pol 100", 100.25m, 100m, new DateOnly(2026, 3, 10), new DateOnly(2026, 3, 1), 0.50m, 15);
        var amountRejected = CommissionAccountingRules.IsCandidateMatch("POL-100", "POL100", 101m, 100m, null, null, 0.50m, 15);
        var tenantUnrelatedPolicyRejected = CommissionAccountingRules.IsCandidateMatch("POL-100", "POL200", 100m, 100m, null, null, 0.50m, 15);

        Assert.True(matches);
        Assert.False(amountRejected);
        Assert.False(tenantUnrelatedPolicyRejected);
    }

    [Theory]
    [InlineData(1000, 70, 700)]
    [InlineData(-1000, 70, -700)]
    [InlineData(100.01, 33.3333, 33.34)]
    public void CalculateAllocatedPayable_PreservesCommissionAndChargebackSigns(decimal reconciled, decimal split, decimal expected)
        => Assert.Equal(expected, CommissionAccountingRules.CalculateAllocatedPayable(reconciled, split));

    [Fact]
    public void CalculateAllocatedPayable_RejectsInvalidPersistedSplit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CommissionAccountingRules.CalculateAllocatedPayable(100m, -1m));
        Assert.Throws<ArgumentOutOfRangeException>(() => CommissionAccountingRules.CalculateAllocatedPayable(100m, 101m));
    }

    [Fact]
    public void ComputeImportHash_IsDeterministicAndContentSensitive()
    {
        var first = CommissionAccountingRules.ComputeImportHash("Policy,Amount\nP1,10");
        var duplicate = CommissionAccountingRules.ComputeImportHash("Policy,Amount\nP1,10");
        var changed = CommissionAccountingRules.ComputeImportHash("Policy,Amount\nP1,11");

        Assert.Equal(first, duplicate);
        Assert.NotEqual(first, changed);
    }

    [Theory]
    [InlineData(0, "Validated")]
    [InlineData(1, "Failed")]
    [InlineData(25, "Failed")]
    public void DetermineImportStatus_BlocksStatementsWithValidationErrors(int errorCount, string expected)
        => Assert.Equal(expected, CommissionAccountingRules.DetermineImportStatus(errorCount));

    [Theory]
    [InlineData(0, false)]
    [InlineData(0.01, true)]
    [InlineData(-0.01, true)]
    public void RequiresVarianceException_TracksPositiveAndNegativeDifferences(decimal variance, bool expected)
        => Assert.Equal(expected, CommissionAccountingRules.RequiresVarianceException(variance));
}
