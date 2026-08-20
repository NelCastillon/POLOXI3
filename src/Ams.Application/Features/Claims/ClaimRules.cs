using System.Security.Cryptography;
using System.Text;

namespace Ams.Application.Features.Claims;

public static class ClaimRules
{
    public static void ValidateLossDates(DateOnly dateOfLoss, DateOnly dateReported)
    {
        if (dateReported < dateOfLoss) throw new InvalidOperationException("Reported date cannot precede the loss date.");
        if (dateOfLoss > DateOnly.FromDateTime(DateTime.UtcNow)) throw new InvalidOperationException("Loss date cannot be in the future.");
    }

    public static bool IsFinancialTransactionType(string typeCode) => typeCode is "ReserveSet" or "ReserveRelease" or "Payment" or "Recovery";

    public static (decimal Reserves, decimal Paid, decimal Recoveries, decimal Incurred) CalculateFinancialTotals(IEnumerable<(string Type, decimal Amount, string Status)> entries)
    {
        var posted = entries.Where(x => x.Status == "Posted").ToList();
        var reserves = posted.Sum(x => x.Type == "ReserveSet" ? x.Amount : x.Type == "ReserveRelease" ? -x.Amount : 0m);
        var paid = posted.Sum(x => x.Type == "Payment" ? x.Amount : 0m);
        var recoveries = posted.Sum(x => x.Type == "Recovery" ? x.Amount : 0m);
        return (reserves, paid, recoveries, reserves + paid - recoveries);
    }

    public static string ComputeImportHash(string content)
    {
        ArgumentNullException.ThrowIfNull(content);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content)));
    }
}