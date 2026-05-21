using System.ComponentModel.DataAnnotations;
using Ams.Application.Common.Validation;

namespace Ams.Application.Features.Finance;

public class CreateGLAccountRequest
{
    [Required] public Guid TenantId { get; set; }
    [Required, StringLength(50)] public string AccountCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string AccountName { get; set; } = string.Empty;
    [Required, StringLength(50)] public string AccountTypeCode { get; set; } = "Asset";
    [StringLength(500)] public string? Description { get; set; }
    public Guid? ParentGLAccountId { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateGLAccountRequest : CreateGLAccountRequest
{
    [Required] public Guid GLAccountId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateVendorRequest
{
    [Required] public Guid TenantId { get; set; }
    [Required, StringLength(50)] public string VendorCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string VendorName { get; set; } = string.Empty;
    [StringLength(150)] public string? ContactName { get; set; }
    [AmsEmailAddress, StringLength(254)] public string? Email { get; set; }
    [AmsPhone, StringLength(50)] public string? Phone { get; set; }
    [Required, StringLength(50)] public string PaymentTermsCode { get; set; } = "Net30";
    [Required, StringLength(3)] public string CurrencyCode { get; set; } = "USD";
    [StringLength(50)] public string? TaxId { get; set; }
    [Required, StringLength(80)] public string VendorTypeCode { get; set; } = "Supplier";
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Active";
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateVendorRequest : CreateVendorRequest
{
    [Required] public Guid VendorId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateJournalEntryRequest
{
    [Required] public Guid TenantId { get; set; }
    [Required, StringLength(50)] public string EntryNumber { get; set; } = string.Empty;
    [Required] public DateOnly EntryDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Required, StringLength(1000)] public string Description { get; set; } = string.Empty;
    [Range(0, 100000000)] public decimal TotalDebit { get; set; }
    [Range(0, 100000000)] public decimal TotalCredit { get; set; }
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Draft";
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateJournalEntryRequest : CreateJournalEntryRequest
{
    [Required] public Guid JournalEntryId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateAccountingPeriodRequest
{
    [Required] public Guid TenantId { get; set; }
    [Required, StringLength(50)] public string PeriodCode { get; set; } = string.Empty;
    [Required, StringLength(150)] public string PeriodName { get; set; } = string.Empty;
    [Required] public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date);
    [Required] public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.Date);
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Open";
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateAccountingPeriodRequest : CreateAccountingPeriodRequest
{
    [Required] public Guid AccountingPeriodId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateBankReconciliationRequest
{
    [Required] public Guid TenantId { get; set; }
    [Required, StringLength(50)] public string BankAccountNumber { get; set; } = string.Empty;
    [Required, StringLength(150)] public string BankName { get; set; } = string.Empty;
    [Required] public DateOnly BankStatementDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Range(0, 100000000)] public decimal BankBalance { get; set; }
    [Range(0, 100000000)] public decimal BookBalance { get; set; }
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Pending";
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateBankReconciliationRequest : CreateBankReconciliationRequest
{
    [Required] public Guid BankReconciliationId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateApInvoiceRequest
{
    [Required] public Guid TenantId { get; set; }
    [RequiredGuid(ErrorMessage = "Vendor is required.")] public Guid VendorId { get; set; }
    [Required, StringLength(80)] public string InvoiceNumber { get; set; } = string.Empty;
    [Required] public DateOnly InvoiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Required] public DateOnly DueDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Range(0.01, 100000000)] public decimal Amount { get; set; }
    [Range(0, 100000000)] public decimal AmountPaid { get; set; }
    [Range(0, 100000000)] public decimal TaxAmount { get; set; }
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Open";
    public Guid? GLAccountId { get; set; }
    public Guid? AgreementId { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateApInvoiceRequest : CreateApInvoiceRequest
{
    [Required] public Guid ApInvoiceId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateApPaymentRequest
{
    [Required] public Guid TenantId { get; set; }
    [RequiredGuid(ErrorMessage = "Vendor is required.")] public Guid VendorId { get; set; }
    public Guid? ApInvoiceId { get; set; }
    [Required] public DateOnly PaymentDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Range(0.01, 100000000)] public decimal Amount { get; set; }
    [Required, StringLength(50)] public string PaymentMethodCode { get; set; } = "ACH";
    [StringLength(100)] public string? ReferenceNumber { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Pending";
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateApPaymentRequest : CreateApPaymentRequest
{
    [Required] public Guid ApPaymentId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreatePeriodCloseEntryRequest
{
    [Required] public Guid TenantId { get; set; }
    [RequiredGuid(ErrorMessage = "Accounting Period is required.")] public Guid AccountingPeriodId { get; set; }
    [Required, StringLength(500)] public string TaskDescription { get; set; } = string.Empty;
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Open";
    public Guid? CompletedByUserId { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdatePeriodCloseEntryRequest : CreatePeriodCloseEntryRequest
{
    [Required] public Guid PeriodCloseEntryId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateDeferredRevenueScheduleRequest
{
    [Required] public Guid TenantId { get; set; }
    [RequiredGuid(ErrorMessage = "Account is required.")] public Guid AccountId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid? AgreementId { get; set; }
    [Range(0.01, 100000000)] public decimal TotalAmount { get; set; }
    [Range(0, 100000000)] public decimal RecognizedAmount { get; set; }
    [Required] public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? EndDate { get; set; }
    [Required, StringLength(50)] public string FrequencyCode { get; set; } = "Monthly";
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Active";
    public Guid? GLAccountId { get; set; }
    public Guid? DeferredGLAccountId { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateDeferredRevenueScheduleRequest : CreateDeferredRevenueScheduleRequest
{
    [Required] public Guid DeferredRevenueScheduleId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateDeferredRevenueRecognitionRequest
{
    [Required] public Guid TenantId { get; set; }
    [RequiredGuid(ErrorMessage = "Deferred Revenue Schedule is required.")] public Guid DeferredRevenueScheduleId { get; set; }
    [Required] public DateOnly RecognitionDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Range(0.01, 100000000)] public decimal Amount { get; set; }
    public Guid? JournalEntryId { get; set; }
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Pending";
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateDeferredRevenueRecognitionRequest : CreateDeferredRevenueRecognitionRequest
{
    [Required] public Guid RecognitionId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateCashReceiptEntryRequest
{
    [Required] public Guid TenantId { get; set; }
    [RequiredGuid(ErrorMessage = "Account is required.")] public Guid AccountId { get; set; }
    public Guid? InvoiceId { get; set; }
    [Required] public DateOnly ReceiptDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [Range(0.01, 100000000)] public decimal Amount { get; set; }
    [Required, StringLength(50)] public string PaymentMethodCode { get; set; } = "ACH";
    [StringLength(100)] public string? ReferenceNumber { get; set; }
    public Guid? GLAccountId { get; set; }
    [StringLength(80)] public string? BankAccountCode { get; set; }
    [StringLength(1000)] public string? Notes { get; set; }
    [Required, StringLength(50)] public string StatusCode { get; set; } = "Pending";
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateCashReceiptEntryRequest : CreateCashReceiptEntryRequest
{
    [Required] public Guid CashReceiptEntryId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public class CreateTrialBalanceSnapshotRequest
{
    [Required] public Guid TenantId { get; set; }
    [Required] public DateOnly SnapshotDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public Guid? AccountingPeriodId { get; set; }
    [RequiredGuid(ErrorMessage = "GL Account is required.")] public Guid GLAccountId { get; set; }
    [Required, StringLength(50)] public string AccountCode { get; set; } = string.Empty;
    [Required, StringLength(200)] public string AccountName { get; set; } = string.Empty;
    [Range(0, 100000000)] public decimal DebitBalance { get; set; }
    [Range(0, 100000000)] public decimal CreditBalance { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class UpdateTrialBalanceSnapshotRequest : CreateTrialBalanceSnapshotRequest
{
    [Required] public Guid TrialBalanceSnapshotId { get; set; }
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class RequiredGuidAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is Guid guid && guid != Guid.Empty;
}
