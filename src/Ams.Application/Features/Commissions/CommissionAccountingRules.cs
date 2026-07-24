using System.Security.Cryptography;
using System.Text;

namespace Ams.Application.Features.Commissions;

public static class CommissionAccountingRules
{
    public static string NormalizePolicyNumber(string? policyNumber)
        => string.Concat((policyNumber ?? string.Empty).Where(char.IsLetterOrDigit)).ToUpperInvariant();

    public static bool IsCandidateMatch(string? statementPolicyNumber, string? expectedPolicyNumber, decimal receivedAmount, decimal expectedAmount, DateOnly? transactionDate, DateOnly? effectiveDate, decimal amountTolerance, int dateToleranceDays)
    {
        if (amountTolerance < 0) throw new ArgumentOutOfRangeException(nameof(amountTolerance));
        if (dateToleranceDays < 0) throw new ArgumentOutOfRangeException(nameof(dateToleranceDays));
        if (NormalizePolicyNumber(statementPolicyNumber) != NormalizePolicyNumber(expectedPolicyNumber) || string.IsNullOrWhiteSpace(statementPolicyNumber)) return false;
        if (Math.Abs(receivedAmount - expectedAmount) > amountTolerance) return false;
        return transactionDate is null || effectiveDate is null || Math.Abs(transactionDate.Value.DayNumber - effectiveDate.Value.DayNumber) <= dateToleranceDays;
    }

    public static decimal CalculateAllocatedPayable(decimal reconciledAmount, decimal splitPercentage)
    {
        if (splitPercentage is < 0 or > 100) throw new ArgumentOutOfRangeException(nameof(splitPercentage));
        return Math.Round(reconciledAmount * splitPercentage / 100m, 2, MidpointRounding.AwayFromZero);
    }

    public static string ComputeImportHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }

    public static string DetermineImportStatus(int validationErrorCount)
    {
        if (validationErrorCount < 0) throw new ArgumentOutOfRangeException(nameof(validationErrorCount));
        return validationErrorCount == 0 ? "Validated" : "Failed";
    }

    public static bool RequiresVarianceException(decimal varianceAmount) => varianceAmount != 0m;
}
