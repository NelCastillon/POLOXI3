namespace Ams.Application.Common.Dtos;

public sealed class CommissionPayoutStatementDto
{
    public Guid StatementId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PayeeId { get; set; }
    public Guid? PayoutBatchId { get; set; }
    public DateOnly StatementDate { get; set; }
    public decimal GrossEarnings { get; set; }
    public decimal TotalClawbacks { get; set; }
    public decimal NetPayout { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public DateTime? IssuedDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
