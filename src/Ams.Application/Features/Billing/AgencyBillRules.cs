namespace Ams.Application.Features.Billing;

public static class AgencyBillRules
{
    public static IReadOnlyList<decimal> SplitInstallments(decimal totalAmount, int installmentCount)
    {
        if (totalAmount <= 0) throw new ArgumentOutOfRangeException(nameof(totalAmount));
        if (installmentCount is < 1 or > 120) throw new ArgumentOutOfRangeException(nameof(installmentCount));

        var baseAmount = Math.Floor(totalAmount * 100m / installmentCount) / 100m;
        var amounts = Enumerable.Repeat(baseAmount, installmentCount).ToArray();
        amounts[^1] += totalAmount - amounts.Sum();
        return amounts;
    }

    public static string DetermineReceivableStatus(decimal balanceAmount, decimal originalAmount)
        => balanceAmount <= 0 ? "Paid" : balanceAmount < originalAmount ? "PartiallyPaid" : "Open";

    public static string DetermineInstallmentStatus(decimal balanceAmount, decimal installmentAmount, DateOnly dueDate, DateOnly asOfDate)
        => balanceAmount <= 0 ? "Paid" : balanceAmount < installmentAmount ? "PartiallyPaid" : dueDate < asOfDate ? "PastDue" : "Scheduled";

    public static string DetermineDelinquencyStage(DateOnly dueDate, DateOnly asOfDate, int firstNoticeDays, int finalNoticeDays, int referralDays)
    {
        if (firstNoticeDays < 0 || finalNoticeDays < firstNoticeDays || referralDays < finalNoticeDays)
            throw new ArgumentOutOfRangeException(nameof(firstNoticeDays), "Delinquency thresholds must be ascending.");

        var daysPastDue = asOfDate.DayNumber - dueDate.DayNumber;
        if (daysPastDue >= referralDays) return "CancellationReview";
        if (daysPastDue >= finalNoticeDays) return "Late2";
        if (daysPastDue >= firstNoticeDays) return "Late1";
        return "Current";
    }

    public static void EnsureAllocationAllowed(decimal allocationAmount, decimal paymentAvailable, decimal receivableBalance, decimal? installmentBalance)
    {
        if (allocationAmount <= 0) throw new ArgumentOutOfRangeException(nameof(allocationAmount));
        if (allocationAmount > paymentAvailable) throw new InvalidOperationException("Allocation exceeds the payment's available amount.");
        if (allocationAmount > receivableBalance) throw new InvalidOperationException("Allocation exceeds the receivable balance.");
        if (installmentBalance.HasValue && allocationAmount > installmentBalance.Value) throw new InvalidOperationException("Allocation exceeds the installment balance.");
    }

    public static bool CanCompleteReconciliation(decimal varianceAmount, decimal tolerance)
    {
        if (tolerance < 0) throw new ArgumentOutOfRangeException(nameof(tolerance));
        return Math.Abs(varianceAmount) <= tolerance;
    }

    public static bool IsReferralDecision(string decisionCode)
        => decisionCode is "Approved" or "Rejected" or "Withdrawn";
}
