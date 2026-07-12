using Ams.Application.Features.Payments;

namespace Ams.Application.Abstractions.Services;

public interface IPaymentProcessorGateway
{
    string ProviderCode { get; }
    Task<PaymentProcessorGatewayResult> CreateCheckoutSessionAsync(PaymentProcessorGatewayContext context, CreateCheckoutSessionRequest request, CancellationToken cancellationToken = default);
    Task<PaymentProcessorGatewayResult> TokenizePaymentMethodAsync(PaymentProcessorGatewayContext context, TokenizePaymentMethodRequest request, CancellationToken cancellationToken = default);
    Task<PaymentProcessorGatewayResult> ChargeAsync(PaymentProcessorGatewayContext context, ChargePaymentRequest request, string? tokenReference, CancellationToken cancellationToken = default);
    Task<PaymentProcessorGatewayResult> RefundAsync(PaymentProcessorGatewayContext context, RefundPaymentRequest request, string? providerOperationId, CancellationToken cancellationToken = default);
    Task<PaymentProcessorGatewayResult> VoidAsync(PaymentProcessorGatewayContext context, VoidPaymentRequest request, string? providerOperationId, CancellationToken cancellationToken = default);
    Task<PaymentProcessorGatewayResult> VerifyWebhookAsync(PaymentProcessorGatewayContext context, PaymentWebhookIngestRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentSettlementBatchDto>> PollSettlementsAsync(PaymentProcessorGatewayContext context, DateTime fromDateUtc, DateTime toDateUtc, CancellationToken cancellationToken = default);
}

public sealed class PaymentProcessorGatewayContext
{
    public Guid TenantId { get; init; }
    public Guid CredentialId { get; init; }
    public string ProviderCode { get; init; } = string.Empty;
    public string Environment { get; init; } = PaymentProcessorEnvironment.Sandbox.ToString();
    public string? ProcessorEndpointUrl { get; init; }
    public IReadOnlyDictionary<PaymentCredentialSecretKind, string> Secrets { get; init; } = new Dictionary<PaymentCredentialSecretKind, string>();
}

public sealed class PaymentProcessorGatewayResult
{
    public bool IsSuccess { get; init; }
    public string StatusCode { get; init; } = PaymentProcessorOperationStatus.Pending.ToString();
    public string? ProviderOperationId { get; init; }
    public string? ProviderStatus { get; init; }
    public string? CheckoutUrl { get; init; }
    public string? TokenReference { get; init; }
    public string? FailureCode { get; init; }
    public string? FailureMessage { get; init; }
    public DateTime? CompletedDateUtc { get; init; }
    public static PaymentProcessorGatewayResult Success(string statusCode, string? providerOperationId = null, string? providerStatus = null, string? checkoutUrl = null, string? tokenReference = null)
        => new() { IsSuccess = true, StatusCode = statusCode, ProviderOperationId = providerOperationId, ProviderStatus = providerStatus, CheckoutUrl = checkoutUrl, TokenReference = tokenReference, CompletedDateUtc = DateTime.UtcNow };
    public static PaymentProcessorGatewayResult Failure(string failureCode, string failureMessage, string? providerStatus = null)
        => new() { IsSuccess = false, StatusCode = PaymentProcessorOperationStatus.Failed.ToString(), FailureCode = failureCode, FailureMessage = failureMessage, ProviderStatus = providerStatus, CompletedDateUtc = DateTime.UtcNow };
}
