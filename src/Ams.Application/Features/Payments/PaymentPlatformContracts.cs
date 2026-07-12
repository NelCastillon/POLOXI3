using System.ComponentModel.DataAnnotations;

namespace Ams.Application.Features.Payments;

public static class PaymentProcessorCodes
{
    public const string Stripe = "Stripe";
    public const string AuthorizeNet = "Authorize.Net";
    public const string Ach = "ACH";
}

public enum PaymentProcessorEnvironment
{
    Sandbox = 1,
    Production = 2
}

public enum PaymentProcessorOperationType
{
    Checkout = 1,
    Tokenize = 2,
    Charge = 3,
    Refund = 4,
    Void = 5,
    Webhook = 6,
    Settlement = 7,
    Retry = 8
}

public enum PaymentProcessorOperationStatus
{
    Pending = 1,
    RequiresAction = 2,
    Succeeded = 3,
    Failed = 4,
    Voided = 5,
    Refunded = 6,
    Settled = 7
}

public enum PaymentCredentialSecretKind
{
    ApiKey = 1,
    ApiSecret = 2,
    PublishableKey = 3,
    WebhookSecret = 4,
    LoginId = 5,
    TransactionKey = 6,
    ClientKey = 7,
    MerchantId = 8
}

public sealed class PaymentGatewayCredentialDto
{
    public Guid PaymentGatewayCredentialId { get; set; }
    public Guid TenantId { get; set; }
    public Guid IntegrationConfigItemId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? ProcessorEndpointUrl { get; set; }
    public bool IsActive { get; set; }
    public bool HasApiKey { get; set; }
    public bool HasApiSecret { get; set; }
    public bool HasWebhookSecret { get; set; }
    public bool HasLoginId { get; set; }
    public bool HasTransactionKey { get; set; }
    public bool HasClientKey { get; set; }
    public bool HasMerchantId { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? ModifiedDateUtc { get; set; }
}

public sealed class PaymentMethodTokenDto
{
    public Guid PaymentMethodTokenId { get; set; }
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid PaymentGatewayCredentialId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string TokenReference { get; set; } = string.Empty;
    public string PaymentMethodType { get; set; } = string.Empty;
    public string? Last4 { get; set; }
    public string? Brand { get; set; }
    public int? ExpirationMonth { get; set; }
    public int? ExpirationYear { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class PaymentProcessorOperationDto
{
    public Guid PaymentProcessorOperationId { get; set; }
    public Guid TenantId { get; set; }
    public Guid? PaymentId { get; set; }
    public Guid PaymentGatewayCredentialId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string OperationType { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public decimal? Amount { get; set; }
    public string CurrencyCode { get; set; } = "USD";
    public string? ProviderOperationId { get; set; }
    public string? ProviderStatus { get; set; }
    public string? CheckoutUrl { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
    public string? RequestPayloadJson { get; set; }
    public int RetryCount { get; set; }
    public DateTime? NextRetryDateUtc { get; set; }
    public DateTime CreatedDateUtc { get; set; }
    public DateTime? CompletedDateUtc { get; set; }
}

public sealed class PaymentWebhookEventDto
{
    public Guid PaymentWebhookEventId { get; set; }
    public Guid TenantId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string Environment { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public string ProviderEventId { get; set; } = string.Empty;
    public bool IsProcessed { get; set; }
    public DateTime ReceivedDateUtc { get; set; }
    public DateTime? ProcessedDateUtc { get; set; }
    public string? ProcessingError { get; set; }
}

public sealed class PaymentSettlementBatchDto
{
    public Guid PaymentSettlementBatchId { get; set; }
    public Guid TenantId { get; set; }
    public Guid PaymentGatewayCredentialId { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string SettlementBatchReference { get; set; } = string.Empty;
    public DateTime SettlementDateUtc { get; set; }
    public decimal GrossAmount { get; set; }
    public decimal FeeAmount { get; set; }
    public decimal NetAmount { get; set; }
    public string StatusCode { get; set; } = string.Empty;
    public DateTime CreatedDateUtc { get; set; }
}

public sealed class UpsertPaymentGatewayCredentialRequest
{
    public Guid TenantId { get; set; }
    public Guid IntegrationConfigItemId { get; set; }
    [Required, StringLength(80)] public string ProviderCode { get; set; } = PaymentProcessorCodes.Stripe;
    [Required, StringLength(20)] public string Environment { get; set; } = PaymentProcessorEnvironment.Sandbox.ToString();
    [Required, StringLength(200)] public string DisplayName { get; set; } = string.Empty;
    [StringLength(500)] public string? ProcessorEndpointUrl { get; set; }
    [StringLength(4000)] public string? ApiKey { get; set; }
    [StringLength(4000)] public string? ApiSecret { get; set; }
    [StringLength(4000)] public string? PublishableKey { get; set; }
    [StringLength(4000)] public string? WebhookSecret { get; set; }
    [StringLength(4000)] public string? LoginId { get; set; }
    [StringLength(4000)] public string? TransactionKey { get; set; }
    [StringLength(4000)] public string? ClientKey { get; set; }
    [StringLength(4000)] public string? MerchantId { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid? ModifiedByUserId { get; set; }
}

public sealed class CreateCheckoutSessionRequest
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid PaymentGatewayCredentialId { get; set; }
    [Range(0.01, 999999999999)] public decimal Amount { get; set; }
    [Required, StringLength(3, MinimumLength = 3)] public string CurrencyCode { get; set; } = "USD";
    [Required, StringLength(500)] public string SuccessUrl { get; set; } = string.Empty;
    [Required, StringLength(500)] public string CancelUrl { get; set; } = string.Empty;
    [StringLength(500)] public string? Description { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class TokenizePaymentMethodRequest
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid PaymentGatewayCredentialId { get; set; }
    [Required, StringLength(200)] public string ProviderToken { get; set; } = string.Empty;
    [Required, StringLength(40)] public string PaymentMethodType { get; set; } = "Card";
    [StringLength(4)] public string? Last4 { get; set; }
    [StringLength(40)] public string? Brand { get; set; }
    [Range(1, 12)] public int? ExpirationMonth { get; set; }
    [Range(2000, 3000)] public int? ExpirationYear { get; set; }
    public bool IsDefault { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class ChargePaymentRequest
{
    public Guid TenantId { get; set; }
    public Guid AccountId { get; set; }
    public Guid? InvoiceId { get; set; }
    public Guid PaymentGatewayCredentialId { get; set; }
    public Guid? PaymentMethodTokenId { get; set; }
    [Range(0.01, 999999999999)] public decimal Amount { get; set; }
    [Required, StringLength(3, MinimumLength = 3)] public string CurrencyCode { get; set; } = "USD";
    [StringLength(100)] public string? ReferenceNumber { get; set; }
    [StringLength(500)] public string? Description { get; set; }
    public bool Capture { get; set; } = true;
    public Guid? CreatedByUserId { get; set; }
}

public sealed class RefundPaymentRequest
{
    public Guid TenantId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid PaymentGatewayCredentialId { get; set; }
    [Range(0.01, 999999999999)] public decimal Amount { get; set; }
    [StringLength(500)] public string? Reason { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class VoidPaymentRequest
{
    public Guid TenantId { get; set; }
    public Guid PaymentId { get; set; }
    public Guid PaymentGatewayCredentialId { get; set; }
    [StringLength(500)] public string? Reason { get; set; }
    public Guid? CreatedByUserId { get; set; }
}

public sealed class PaymentWebhookIngestRequest
{
    public Guid TenantId { get; set; }
    [Required, StringLength(80)] public string ProviderCode { get; set; } = string.Empty;
    [Required, StringLength(20)] public string Environment { get; set; } = PaymentProcessorEnvironment.Sandbox.ToString();
    [Required, StringLength(200)] public string ProviderEventId { get; set; } = string.Empty;
    [Required, StringLength(120)] public string EventType { get; set; } = string.Empty;
    [Required] public string PayloadJson { get; set; } = string.Empty;
    [StringLength(1000)] public string? SignatureHeader { get; set; }
}

public sealed class ProcessPaymentRetryRequest
{
    public Guid TenantId { get; set; }
    public Guid PaymentProcessorOperationId { get; set; }
}

public sealed class PollPaymentSettlementsRequest
{
    public Guid TenantId { get; set; }
    public Guid PaymentGatewayCredentialId { get; set; }
    public DateTime FromDateUtc { get; set; }
    public DateTime ToDateUtc { get; set; }
}
