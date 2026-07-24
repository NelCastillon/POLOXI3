using Ams.Application.Features.Claims;
using Xunit;

namespace Ams.Application.Tests;

public sealed class ClaimRulesTests
{
    [Fact]
    public void ValidateLossDates_Throws_When_Reported_Before_Loss()
    {
        var loss = new DateOnly(2025, 2, 10);

        var exception = Assert.Throws<InvalidOperationException>(() => ClaimRules.ValidateLossDates(loss, loss.AddDays(-1)));

        Assert.Contains("cannot precede", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateLossDates_Throws_When_Loss_Is_In_Future()
    {
        var future = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        var exception = Assert.Throws<InvalidOperationException>(() => ClaimRules.ValidateLossDates(future, future));

        Assert.Contains("future", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ValidateLossDates_Allows_Same_Day_Reporting()
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        ClaimRules.ValidateLossDates(date, date);
    }

    [Theory]
    [InlineData("ReserveSet")]
    [InlineData("ReserveRelease")]
    [InlineData("Payment")]
    [InlineData("Recovery")]
    public void IsFinancialTransactionType_Accepts_Supported_Types(string type)
        => Assert.True(ClaimRules.IsFinancialTransactionType(type));

    [Theory]
    [InlineData("")]
    [InlineData("Reserve")]
    [InlineData("Adjustment")]
    [InlineData("payment")]
    public void IsFinancialTransactionType_Rejects_Unsupported_Types(string type)
        => Assert.False(ClaimRules.IsFinancialTransactionType(type));

    [Fact]
    public void CalculateFinancialTotals_Derives_Posted_Ledger_And_Ignores_Reversed_Rows()
    {
        (string Type, decimal Amount, string Status)[] entries =
        [
            ("ReserveSet", 100_000m, "Posted"),
            ("ReserveRelease", 20_000m, "Posted"),
            ("Payment", 15_000m, "Posted"),
            ("Recovery", 5_000m, "Posted"),
            ("Payment", 50_000m, "Reversed")
        ];

        var totals = ClaimRules.CalculateFinancialTotals(entries);

        Assert.Equal(80_000m, totals.Reserves);
        Assert.Equal(15_000m, totals.Paid);
        Assert.Equal(5_000m, totals.Recoveries);
        Assert.Equal(90_000m, totals.Incurred);
    }

    [Fact]
    public void ComputeImportHash_Is_Deterministic_And_Content_Sensitive()
    {
        const string content = "ClaimNumber,Incurred\nA-1,100.00";

        var first = ClaimRules.ComputeImportHash(content);
        var second = ClaimRules.ComputeImportHash(content);
        var changed = ClaimRules.ComputeImportHash(content + "\nA-2,25.00");

        Assert.Equal(first, second);
        Assert.NotEqual(first, changed);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public void ComputeImportHash_Throws_For_Null_Content()
        => Assert.Throws<ArgumentNullException>(() => ClaimRules.ComputeImportHash(null!));
}
