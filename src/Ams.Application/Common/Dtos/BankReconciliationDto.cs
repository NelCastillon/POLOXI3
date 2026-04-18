namespace Ams.Application.Common.Dtos;

public sealed class BankReconciliationDto
{
    public Guid ReconciliationId { get; set; }
    public Guid TenantId { get; set; }
    public string BankAccountCode { get; set; } = string.Empty;
    public DateOnly StatementDate { get; set; }
    public decimal StatementBalance { get; set; }
    public decimal BookBalance { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime? ReconciledDateUtc { get; set; }
    public Guid? ReconciledByUserId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}
