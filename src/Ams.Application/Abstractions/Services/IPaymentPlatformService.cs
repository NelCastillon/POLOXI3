using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Payments;

namespace Ams.Application.Abstractions.Services;

public interface IPaymentPlatformService
{
    Task<PagedResult<PaymentGatewayCredentialDto>> SearchCredentialsAsync(Guid tenantId, string? providerCode, string? environment, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<Guid> UpsertCredentialAsync(UpsertPaymentGatewayCredentialRequest request, CancellationToken cancellationToken = default);

    Task<PagedResult<PaymentMethodTokenDto>> SearchPaymentMethodTokensAsync(Guid tenantId, Guid? accountId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<Guid> TokenizePaymentMethodAsync(TokenizePaymentMethodRequest request, CancellationToken cancellationToken = default);

    Task<PaymentProcessorOperationDto> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, CancellationToken cancellationToken = default);
    Task<PaymentProcessorOperationDto> ChargeAsync(ChargePaymentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentProcessorOperationDto> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default);
    Task<PaymentProcessorOperationDto> VoidAsync(VoidPaymentRequest request, CancellationToken cancellationToken = default);

    Task<Guid> IngestWebhookAsync(PaymentWebhookIngestRequest request, CancellationToken cancellationToken = default);
    Task<PagedResult<PaymentWebhookEventDto>> SearchWebhookEventsAsync(Guid tenantId, string? providerCode, bool? isProcessed, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);

    Task<PagedResult<PaymentProcessorOperationDto>> SearchOperationsAsync(Guid tenantId, string? providerCode, string? statusCode, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<PagedResult<PaymentSettlementBatchDto>> SearchSettlementBatchesAsync(Guid tenantId, string? providerCode, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
    Task<int> ProcessDueRetriesAsync(int maxCount, CancellationToken cancellationToken = default);
    Task<int> PollSettlementsAsync(int maxCredentials, CancellationToken cancellationToken = default);
}
