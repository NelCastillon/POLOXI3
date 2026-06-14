namespace Ams.Application.Common.Dtos;

public sealed class CommissionLedgerRowDto
{
    public Guid CommissionId { get; set; }
    public Guid TenantId { get; set; }
    public string PolicyNumber { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public string BusinessType { get; set; } = string.Empty;
    public string Producer { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string LineOfBusiness { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public decimal GrossAmount { get; set; }
    public decimal CommissionPct { get; set; }
    public decimal AgencyAmount { get; set; }
    public decimal ProducerAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public string StatementNumber { get; set; } = string.Empty;
    public string PayoutBatch { get; set; } = string.Empty;
    public DateOnly TransactionDate { get; set; }
    public DateOnly? PaidDate { get; set; }
}
