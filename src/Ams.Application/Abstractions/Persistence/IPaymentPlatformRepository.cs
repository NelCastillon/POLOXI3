using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Payments;

namespace Ams.Application.Abstractions.Persistence;

public interface IPaymentPlatformRepository
{
    Task<PaymentGatewayCredentialDto?> GetCredentialByIdAsync(Guid credentialId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<PaymentCredentialSecretKind, string>> GetCredentialSecretsAsync(Guid credentialId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentGatewayCredentialDto>> GetActiveCredentialsAsync(string? providerCode, int maxCount, CancellationToken cancellationToken = default);
    Task<PagedResult<PaymentGatewayCredentialDto>> SearchCredentialsAsync(Guid tenantId, string? providerCode, string? environment, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<Guid> UpsertCredentialAsync(UpsertPaymentGatewayCredentialRequest request, CancellationToken cancellationToken = default);

    Task<Guid> CreatePaymentMethodTokenAsync(TokenizePaymentMethodRequest request, string tokenReference, CancellationToken cancellationToken = default);
    Task<PagedResult<PaymentMethodTokenDto>> SearchPaymentMethodTokensAsync(Guid tenantId, Guid? accountId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<PaymentMethodTokenDto?> GetPaymentMethodTokenByIdAsync(Guid paymentMethodTokenId, CancellationToken cancellationToken = default);

    Task<Guid> CreateOperationAsync(PaymentProcessorOperationDto operation, CancellationToken cancellationToken = default);
    Task UpdateOperationAsync(PaymentProcessorOperationDto operation, CancellationToken cancellationToken = default);
    Task<PaymentProcessorOperationDto?> GetOperationByIdAsync(Guid operationId, CancellationToken cancellationToken = default);
    Task<PagedResult<PaymentProcessorOperationDto>> SearchOperationsAsync(Guid tenantId, string? providerCode, string? statusCode, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PaymentProcessorOperationDto>> GetDueRetryOperationsAsync(int maxCount, CancellationToken cancellationToken = default);

    Task<Guid> CreateWebhookEventAsync(PaymentWebhookIngestRequest request, CancellationToken cancellationToken = default);
    Task MarkWebhookEventProcessedAsync(Guid webhookEventId, string? processingError, CancellationToken cancellationToken = default);
    Task<PagedResult<PaymentWebhookEventDto>> SearchWebhookEventsAsync(Guid tenantId, string? providerCode, bool? isProcessed, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<Guid> CreateSettlementBatchAsync(PaymentSettlementBatchDto settlementBatch, CancellationToken cancellationToken = default);
    Task<PagedResult<PaymentSettlementBatchDto>> SearchSettlementBatchesAsync(Guid tenantId, string? providerCode, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
}
