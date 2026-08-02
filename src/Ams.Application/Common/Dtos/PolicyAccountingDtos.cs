namespace Ams.Application.Common.Dtos;

public sealed record PolicyAccountingDashboardDto(
    Guid PolicyId,
    Guid PolicyTermId,
    string PolicyNumber,
    string BillingTypeCode,
    string StatusCode,
    string CurrencyCode,
    decimal PremiumAmount,
    decimal FeeAmount,
    decimal TaxAmount,
    decimal InvoiceAmount,
    decimal OutstandingBalance,
    decimal CommissionRatePct,
    decimal CommissionAmount,
    decimal CarrierPayableAmount,
    int InstallmentCount,
    Guid? InvoiceId,
    Guid? AgencyBillReceivableId,
    Guid? CarrierPayableId,
    Guid? CommissionExpectedReceivableId,
    Guid? JournalEntryId,
    DateTime? SynchronizedDateUtc)
{
    public IReadOnlyList<PolicyAccountingInvoiceDto> Invoices { get; init; } = [];
    public IReadOnlyList<PolicyAccountingInstallmentDto> Installments { get; init; } = [];
    public IReadOnlyList<PolicyAccountingCommissionSplitDto> CommissionSplits { get; init; } = [];
}

public sealed record PolicyAccountingInvoiceDto(Guid InvoiceId, string InvoiceNumber, DateOnly InvoiceDate, DateOnly DueDate, decimal TotalAmount, decimal BalanceAmount, string StatusCode, Guid? DeliveryDispatchId, string? DeliveryStatusCode, string? DeliveryRecipient, DateTime? DeliveredDateUtc, string? DeliveryErrorMessage);
public sealed record PolicyAccountingInstallmentDto(Guid AgencyBillInstallmentId, int InstallmentNumber, DateOnly DueDate, decimal InstallmentAmount, decimal AllocatedAmount, decimal BalanceAmount, string StatusCode);
public sealed record PolicyAccountingCommissionSplitDto(Guid PolicyCommissionSplitId, Guid? PayeeId, string PayeeTypeCode, decimal SplitPercent, decimal SplitAmount, DateOnly ExpectedDate, string StatusCode);
public sealed record RemitCarrierPayableRequest(Guid TenantId, decimal Amount, DateOnly RemittanceDate, string? ReferenceNumber, Guid? UserId);
public sealed record EmailPolicyInvoiceRequest(Guid TenantId, string Recipient, Guid? UserId);
public sealed record InvoiceDeliveryDispatchDto(Guid DeliveryDispatchId, Guid InvoiceId, string Recipient, string StatusCode, DateTime CreatedDateUtc);
