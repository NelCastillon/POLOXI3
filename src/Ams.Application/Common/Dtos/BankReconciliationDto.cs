namespace Ams.Application.Common.Dtos;

public sealed class BankReconciliationDto
{
    public Guid BankReconciliationId { get; set; }
    public Guid TenantId { get; set; }
    public string BankAccountNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public DateOnly BankStatementDate { get; set; }
    public decimal BankBalance { get; set; }
    public decimal BookBalance { get; set; }
    public decimal OutstandingDeposits { get; set; }
    public decimal OutstandingChecks { get; set; }
    public decimal Discrepancy { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}
