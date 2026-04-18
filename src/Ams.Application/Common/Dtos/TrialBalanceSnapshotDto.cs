namespace Ams.Application.Common.Dtos;

public sealed class TrialBalanceSnapshotDto
{
    public Guid TrialBalanceSnapshotId { get; set; }
    public Guid TenantId { get; set; }
    public DateOnly SnapshotDate { get; set; }
    public Guid? AccountingPeriodId { get; set; }
    public Guid GLAccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal DebitBalance { get; set; }
    public decimal CreditBalance { get; set; }
    public decimal NetBalance { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
