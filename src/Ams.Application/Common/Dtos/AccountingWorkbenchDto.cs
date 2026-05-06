namespace Ams.Application.Common.Dtos;

public sealed class AccountingWorkbenchDto
{
    public AccountingWorkbenchCountsDto Counts { get; set; } = new();
    public List<AccountingWorkbenchItemDto> Reconciliation { get; set; } = [];
    public List<AccountingWorkbenchItemDto> ArAging { get; set; } = [];
    public List<AccountingWorkbenchItemDto> UnappliedPayments { get; set; } = [];
    public List<AccountingWorkbenchItemDto> CommissionAdjustments { get; set; } = [];
    public List<AccountingWorkbenchItemDto> DirectBillExceptions { get; set; } = [];
    public List<AccountingWorkbenchItemDto> MonthEnd { get; set; } = [];
}

public sealed class AccountingWorkbenchCountsDto
{
    public int ReconciliationItems { get; set; }
    public decimal ReconciliationAmount { get; set; }
    public int ArOverdue { get; set; }
    public decimal ArAmount { get; set; }
    public int UnappliedPayments { get; set; }
    public decimal UnappliedAmount { get; set; }
    public int CommissionAdj { get; set; }
    public int DirectBillExceptions { get; set; }
    public int MonthEndOpen { get; set; }
    public int MonthEndComplete { get; set; }
}

public sealed class AccountingWorkbenchItemDto
{
    public Guid ItemId { get; set; }
    public string QueueCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string RefNumber { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string CarrierName { get; set; } = string.Empty;
    public string ProducerName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string AssignedTo { get; set; } = string.Empty;
    public string Priority { get; set; } = "Normal";
    public string SlaStatus { get; set; } = "On Track";
    public string Status { get; set; } = "Open";
    public string AgingBucket { get; set; } = "Current";
    public string PaymentMethod { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal Variance { get; set; }
    public DateTime DueDate { get; set; } = DateTime.Today;
    public DateTime? ReceivedDate { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int AgeDays { get; set; }
    public string? Notes { get; set; }
    public string DetailUrl { get; set; } = "/workbench/accounting";
}
