using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Payments;
using System.Text.Json;

namespace Ams.Application;

public sealed class PaymentPlatformService : IPaymentPlatformService
{
    private readonly IPaymentPlatformRepository _repository;
    private readonly IReadOnlyDictionary<string, IPaymentProcessorGateway> _gateways;

    public PaymentPlatformService(IPaymentPlatformRepository repository, IEnumerable<IPaymentProcessorGateway> gateways)
    {
        _repository = repository;
        _gateways = gateways.ToDictionary(x => x.ProviderCode, StringComparer.OrdinalIgnoreCase);
    }

    public Task<PagedResult<PaymentGatewayCredentialDto>> SearchCredentialsAsync(Guid tenantId, string? providerCode, string? environment, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _repository.SearchCredentialsAsync(tenantId, providerCode, environment, pageNumber, pageSize, cancellationToken);

    public Task<Guid> UpsertCredentialAsync(UpsertPaymentGatewayCredentialRequest request, CancellationToken cancellationToken = default)
    {
        if (request.TenantId == Guid.Empty) throw new ArgumentException("Tenant is required.", nameof(request));
        if (request.IntegrationConfigItemId == Guid.Empty) throw new ArgumentException("Integration configuration item is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ProviderCode)) throw new ArgumentException("Provider code is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.Environment)) throw new ArgumentException("Environment is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.DisplayName)) throw new ArgumentException("Display name is required.", nameof(request));
        EnsureGateway(request.ProviderCode);
        return _repository.UpsertCredentialAsync(request, cancellationToken);
    }

    public Task<PagedResult<PaymentMethodTokenDto>> SearchPaymentMethodTokensAsync(Guid tenantId, Guid? accountId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _repository.SearchPaymentMethodTokensAsync(tenantId, accountId, pageNumber, pageSize, cancellationToken);

    public async Task<Guid> TokenizePaymentMethodAsync(TokenizePaymentMethodRequest request, CancellationToken cancellationToken = default)
    {
        var credential = await GetCredentialAsync(request.PaymentGatewayCredentialId, request.TenantId, cancellationToken);
        var gateway = EnsureGateway(credential.ProviderCode);
        var result = await gateway.TokenizePaymentMethodAsync(await CreateContextAsync(credential, cancellationToken), request, cancellationToken);
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.TokenReference)) throw new InvalidOperationException(result.FailureMessage ?? "Payment method tokenization failed.");
        return await _repository.CreatePaymentMethodTokenAsync(request, result.TokenReference!, cancellationToken);
    }

    public async Task<PaymentProcessorOperationDto> CreateCheckoutSessionAsync(CreateCheckoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        var credential = await GetCredentialAsync(request.PaymentGatewayCredentialId, request.TenantId, cancellationToken);
        var gateway = EnsureGateway(credential.ProviderCode);
        var operation = CreateOperation(request.TenantId, null, credential, PaymentProcessorOperationType.Checkout, request.Amount, request.CurrencyCode, request);
        var operationId = await _repository.CreateOperationAsync(operation, cancellationToken);
        operation.PaymentProcessorOperationId = operationId;
        var result = await gateway.CreateCheckoutSessionAsync(await CreateContextAsync(credential, cancellationToken), request, cancellationToken);
        ApplyResult(operation, result);
        await _repository.UpdateOperationAsync(operation, cancellationToken);
        return operation;
    }

    public async Task<PaymentProcessorOperationDto> ChargeAsync(ChargePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var credential = await GetCredentialAsync(request.PaymentGatewayCredentialId, request.TenantId, cancellationToken);
        var gateway = EnsureGateway(credential.ProviderCode);
        var token = request.PaymentMethodTokenId is { } tokenId ? await _repository.GetPaymentMethodTokenByIdAsync(tokenId, cancellationToken) : null;
        if (token is not null)
        {
            if (token.TenantId != request.TenantId || token.AccountId != request.AccountId || token.PaymentGatewayCredentialId != request.PaymentGatewayCredentialId || !token.IsActive)
            {
                throw new InvalidOperationException("Payment method token does not belong to the requested tenant, account, or gateway credential.");
            }
        }
        var operation = CreateOperation(request.TenantId, null, credential, PaymentProcessorOperationType.Charge, request.Amount, request.CurrencyCode, request);
        var operationId = await _repository.CreateOperationAsync(operation, cancellationToken);
        operation.PaymentProcessorOperationId = operationId;
        var result = await gateway.ChargeAsync(await CreateContextAsync(credential, cancellationToken), request, token?.TokenReference, cancellationToken);
        ApplyResult(operation, result);
        await _repository.UpdateOperationAsync(operation, cancellationToken);
        return operation;
    }

    public async Task<PaymentProcessorOperationDto> RefundAsync(RefundPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var credential = await GetCredentialAsync(request.PaymentGatewayCredentialId, request.TenantId, cancellationToken);
        var gateway = EnsureGateway(credential.ProviderCode);
        var original = await FindLatestProviderOperationAsync(request.TenantId, request.PaymentId, cancellationToken);
        var operation = CreateOperation(request.TenantId, request.PaymentId, credential, PaymentProcessorOperationType.Refund, request.Amount, "USD", request);
        var operationId = await _repository.CreateOperationAsync(operation, cancellationToken);
        operation.PaymentProcessorOperationId = operationId;
        var result = await gateway.RefundAsync(await CreateContextAsync(credential, cancellationToken), request, original?.ProviderOperationId, cancellationToken);
        ApplyResult(operation, result);
        await _repository.UpdateOperationAsync(operation, cancellationToken);
        return operation;
    }

    public async Task<PaymentProcessorOperationDto> VoidAsync(VoidPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var credential = await GetCredentialAsync(request.PaymentGatewayCredentialId, request.TenantId, cancellationToken);
        var gateway = EnsureGateway(credential.ProviderCode);
        var original = await FindLatestProviderOperationAsync(request.TenantId, request.PaymentId, cancellationToken);
        var operation = CreateOperation(request.TenantId, request.PaymentId, credential, PaymentProcessorOperationType.Void, null, "USD", request);
        var operationId = await _repository.CreateOperationAsync(operation, cancellationToken);
        operation.PaymentProcessorOperationId = operationId;
        var result = await gateway.VoidAsync(await CreateContextAsync(credential, cancellationToken), request, original?.ProviderOperationId, cancellationToken);
        ApplyResult(operation, result);
        await _repository.UpdateOperationAsync(operation, cancellationToken);
        return operation;
    }

    public async Task<Guid> IngestWebhookAsync(PaymentWebhookIngestRequest request, CancellationToken cancellationToken = default)
    {
        var eventId = await _repository.CreateWebhookEventAsync(request, cancellationToken);
        try
        {
            var credentials = await _repository.SearchCredentialsAsync(request.TenantId, request.ProviderCode, request.Environment, 1, 1, cancellationToken);
            var credential = credentials.Items.FirstOrDefault();
            if (credential is null) throw new InvalidOperationException("Webhook credential was not found.");
            var gateway = EnsureGateway(request.ProviderCode);
            var result = await gateway.VerifyWebhookAsync(await CreateContextAsync(credential, cancellationToken), request, cancellationToken);
            await _repository.MarkWebhookEventProcessedAsync(eventId, result.IsSuccess ? null : result.FailureMessage, cancellationToken);
        }
        catch (Exception ex)
        {
            await _repository.MarkWebhookEventProcessedAsync(eventId, ex.Message, cancellationToken);
            throw;
        }
        return eventId;
    }

    public Task<PagedResult<PaymentWebhookEventDto>> SearchWebhookEventsAsync(Guid tenantId, string? providerCode, bool? isProcessed, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _repository.SearchWebhookEventsAsync(tenantId, providerCode, isProcessed, pageNumber, pageSize, cancellationToken);

    public Task<PagedResult<PaymentProcessorOperationDto>> SearchOperationsAsync(Guid tenantId, string? providerCode, string? statusCode, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _repository.SearchOperationsAsync(tenantId, providerCode, statusCode, pageNumber, pageSize, cancellationToken);

    public Task<PagedResult<PaymentSettlementBatchDto>> SearchSettlementBatchesAsync(Guid tenantId, string? providerCode, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _repository.SearchSettlementBatchesAsync(tenantId, providerCode, pageNumber, pageSize, cancellationToken);

    public async Task<int> ProcessDueRetriesAsync(int maxCount, CancellationToken cancellationToken = default)
    {
        var due = await _repository.GetDueRetryOperationsAsync(maxCount, cancellationToken);
        var processed = 0;
        foreach (var operation in due)
        {
            await ReplayOperationAsync(operation, cancellationToken);
            await _repository.UpdateOperationAsync(operation, cancellationToken);
            processed++;
        }
        return processed;
    }

    public async Task<int> PollSettlementsAsync(int maxCredentials, CancellationToken cancellationToken = default)
    {
        var processed = 0;
        foreach (var provider in _gateways.Keys)
        {
            var credentials = await _repository.GetActiveCredentialsAsync(provider, maxCredentials, cancellationToken);
            foreach (var credential in credentials)
            {
                var gateway = EnsureGateway(credential.ProviderCode);
                var batches = await gateway.PollSettlementsAsync(await CreateContextAsync(credential, cancellationToken), DateTime.UtcNow.AddDays(-2), DateTime.UtcNow, cancellationToken);
                foreach (var batch in batches)
                {
                    await _repository.CreateSettlementBatchAsync(batch, cancellationToken);
                    processed++;
                }
            }
        }
        return processed;
    }

    private async Task<PaymentGatewayCredentialDto> GetCredentialAsync(Guid credentialId, Guid tenantId, CancellationToken cancellationToken)
    {
        if (credentialId == Guid.Empty) throw new ArgumentException("Payment gateway credential is required.");
        var credential = await _repository.GetCredentialByIdAsync(credentialId, cancellationToken) ?? throw new InvalidOperationException("Payment gateway credential was not found.");
        if (tenantId != Guid.Empty && credential.TenantId != tenantId) throw new InvalidOperationException("Payment gateway credential does not belong to the tenant.");
        if (!credential.IsActive) throw new InvalidOperationException("Payment gateway credential is inactive.");
        return credential;
    }

    private IPaymentProcessorGateway EnsureGateway(string providerCode)
    {
        if (_gateways.TryGetValue(providerCode, out var gateway)) return gateway;
        throw new InvalidOperationException($"Payment processor provider '{providerCode}' is not registered.");
    }

    private async Task<PaymentProcessorGatewayContext> CreateContextAsync(PaymentGatewayCredentialDto credential, CancellationToken cancellationToken)
        => new()
        {
            TenantId = credential.TenantId,
            CredentialId = credential.PaymentGatewayCredentialId,
            ProviderCode = credential.ProviderCode,
            Environment = credential.Environment,
            ProcessorEndpointUrl = credential.ProcessorEndpointUrl,
            Secrets = await _repository.GetCredentialSecretsAsync(credential.PaymentGatewayCredentialId, cancellationToken)
        };

    private async Task<PaymentProcessorOperationDto?> FindLatestProviderOperationAsync(Guid tenantId, Guid paymentId, CancellationToken cancellationToken)
    {
        var operations = await _repository.SearchOperationsAsync(tenantId, null, null, 1, 50, cancellationToken);
        return operations.Items.FirstOrDefault(x => x.PaymentId == paymentId && !string.IsNullOrWhiteSpace(x.ProviderOperationId) && x.StatusCode != PaymentProcessorOperationStatus.Failed.ToString());
    }

    private async Task ReplayOperationAsync(PaymentProcessorOperationDto operation, CancellationToken cancellationToken)
    {
        operation.RetryCount++;
        if (string.IsNullOrWhiteSpace(operation.RequestPayloadJson))
        {
            MarkRetryFailure(operation, "missing_replay_payload", "Automatic retry replay requires a persisted provider request payload.");
            return;
        }

        try
        {
            var credential = await GetCredentialAsync(operation.PaymentGatewayCredentialId, operation.TenantId, cancellationToken);
            var gateway = EnsureGateway(credential.ProviderCode);
            var context = await CreateContextAsync(credential, cancellationToken);
            PaymentProcessorGatewayResult result = operation.OperationType switch
            {
                nameof(PaymentProcessorOperationType.Checkout) => await gateway.CreateCheckoutSessionAsync(context, JsonSerializer.Deserialize<CreateCheckoutSessionRequest>(operation.RequestPayloadJson) ?? throw new InvalidOperationException("Checkout replay payload is invalid."), cancellationToken),
                nameof(PaymentProcessorOperationType.Charge) => await ReplayChargeAsync(gateway, context, operation.RequestPayloadJson, cancellationToken),
                nameof(PaymentProcessorOperationType.Refund) => await gateway.RefundAsync(context, JsonSerializer.Deserialize<RefundPaymentRequest>(operation.RequestPayloadJson) ?? throw new InvalidOperationException("Refund replay payload is invalid."), await FindLatestProviderOperationIdAsync(operation, cancellationToken), cancellationToken),
                nameof(PaymentProcessorOperationType.Void) => await gateway.VoidAsync(context, JsonSerializer.Deserialize<VoidPaymentRequest>(operation.RequestPayloadJson) ?? throw new InvalidOperationException("Void replay payload is invalid."), await FindLatestProviderOperationIdAsync(operation, cancellationToken), cancellationToken),
                _ => PaymentProcessorGatewayResult.Failure("unsupported_retry_operation", $"Retry is not supported for operation type '{operation.OperationType}'.")
            };
            ApplyResult(operation, result);
        }
        catch (Exception ex)
        {
            MarkRetryFailure(operation, "retry_replay_failed", ex.Message);
        }
    }

    private async Task<PaymentProcessorGatewayResult> ReplayChargeAsync(IPaymentProcessorGateway gateway, PaymentProcessorGatewayContext context, string payloadJson, CancellationToken cancellationToken)
    {
        var request = JsonSerializer.Deserialize<ChargePaymentRequest>(payloadJson) ?? throw new InvalidOperationException("Charge replay payload is invalid.");
        var token = request.PaymentMethodTokenId is { } tokenId ? await _repository.GetPaymentMethodTokenByIdAsync(tokenId, cancellationToken) : null;
        return await gateway.ChargeAsync(context, request, token?.TokenReference, cancellationToken);
    }

    private async Task<string?> FindLatestProviderOperationIdAsync(PaymentProcessorOperationDto operation, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(operation.ProviderOperationId)) return operation.ProviderOperationId;
        if (operation.PaymentId is null) return null;
        var original = await FindLatestProviderOperationAsync(operation.TenantId, operation.PaymentId.Value, cancellationToken);
        return original?.ProviderOperationId;
    }

    private static void MarkRetryFailure(PaymentProcessorOperationDto operation, string failureCode, string failureMessage)
    {
        operation.StatusCode = PaymentProcessorOperationStatus.Failed.ToString();
        operation.FailureCode = failureCode;
        operation.FailureMessage = failureMessage;
        operation.NextRetryDateUtc = DateTime.UtcNow.AddMinutes(Math.Min(240, Math.Pow(2, operation.RetryCount + 1) * 10));
    }

    private static PaymentProcessorOperationDto CreateOperation(Guid tenantId, Guid? paymentId, PaymentGatewayCredentialDto credential, PaymentProcessorOperationType type, decimal? amount, string currencyCode, object request)
        => new()
        {
            TenantId = tenantId,
            PaymentId = paymentId,
            PaymentGatewayCredentialId = credential.PaymentGatewayCredentialId,
            ProviderCode = credential.ProviderCode,
            OperationType = type.ToString(),
            StatusCode = PaymentProcessorOperationStatus.Pending.ToString(),
            Amount = amount,
            CurrencyCode = string.IsNullOrWhiteSpace(currencyCode) ? "USD" : currencyCode.ToUpperInvariant(),
            RequestPayloadJson = JsonSerializer.Serialize(request),
            CreatedDateUtc = DateTime.UtcNow
        };

    private static void ApplyResult(PaymentProcessorOperationDto operation, PaymentProcessorGatewayResult result)
    {
        operation.StatusCode = result.StatusCode;
        operation.ProviderOperationId = result.ProviderOperationId;
        operation.ProviderStatus = result.ProviderStatus;
        operation.CheckoutUrl = result.CheckoutUrl;
        operation.FailureCode = result.FailureCode;
        operation.FailureMessage = result.FailureMessage;
        operation.CompletedDateUtc = result.CompletedDateUtc;
        if (!result.IsSuccess)
        {
            operation.NextRetryDateUtc = DateTime.UtcNow.AddMinutes(Math.Min(120, Math.Pow(2, operation.RetryCount + 1) * 5));
        }
    }
}
