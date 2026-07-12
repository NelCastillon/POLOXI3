using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Payments;

namespace Ams.Infrastructure.Payments;

public sealed class AchPaymentProcessorGateway : IPaymentProcessorGateway
{
    private const string AdapterContractVersion = "ams-ach-adapter.v1";

    public string ProviderCode => PaymentProcessorCodes.Ach;

    public Task<PaymentProcessorGatewayResult> CreateCheckoutSessionAsync(PaymentProcessorGatewayContext context, CreateCheckoutSessionRequest request, CancellationToken cancellationToken = default)
        => PostOperationAsync(context, "checkout-sessions", CreateEnvelope(context, "checkout", new
        {
            accountId = request.AccountId,
            invoiceId = request.InvoiceId,
            amount = request.Amount,
            currencyCode = request.CurrencyCode,
            successUrl = request.SuccessUrl,
            cancelUrl = request.CancelUrl,
            description = request.Description
        }, request.AccountId, request.InvoiceId, request.Amount, request.CurrencyCode), requireProviderOperationId: true, cancellationToken);

    public Task<PaymentProcessorGatewayResult> TokenizePaymentMethodAsync(PaymentProcessorGatewayContext context, TokenizePaymentMethodRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(PaymentProcessorGatewayResult.Success(PaymentProcessorOperationStatus.Succeeded.ToString(), tokenReference: request.ProviderToken));

    public Task<PaymentProcessorGatewayResult> ChargeAsync(PaymentProcessorGatewayContext context, ChargePaymentRequest request, string? tokenReference, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tokenReference)) return Task.FromResult(PaymentProcessorGatewayResult.Failure("missing_ach_token", "ACH token reference is required."));
        return PostOperationAsync(context, "debits", CreateEnvelope(context, "debit", new
        {
            accountId = request.AccountId,
            invoiceId = request.InvoiceId,
            amount = request.Amount,
            currencyCode = request.CurrencyCode,
            referenceNumber = request.ReferenceNumber,
            description = request.Description,
            capture = request.Capture,
            tokenReference
        }, request.AccountId, request.InvoiceId, request.Amount, request.CurrencyCode), requireProviderOperationId: true, cancellationToken);
    }

    public Task<PaymentProcessorGatewayResult> RefundAsync(PaymentProcessorGatewayContext context, RefundPaymentRequest request, string? providerOperationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerOperationId)) return Task.FromResult(PaymentProcessorGatewayResult.Failure("missing_provider_operation", "Provider operation id is required for ACH refund."));
        return PostOperationAsync(context, "credits", CreateEnvelope(context, "credit", new
        {
            paymentId = request.PaymentId,
            amount = request.Amount,
            reason = request.Reason,
            providerOperationId
        }, paymentId: request.PaymentId, amount: request.Amount), requireProviderOperationId: true, cancellationToken);
    }

    public Task<PaymentProcessorGatewayResult> VoidAsync(PaymentProcessorGatewayContext context, VoidPaymentRequest request, string? providerOperationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerOperationId)) return Task.FromResult(PaymentProcessorGatewayResult.Failure("missing_provider_operation", "Provider operation id is required for ACH void."));
        return PostOperationAsync(context, "voids", CreateEnvelope(context, "void", new
        {
            paymentId = request.PaymentId,
            reason = request.Reason,
            providerOperationId
        }, paymentId: request.PaymentId), requireProviderOperationId: false, cancellationToken);
    }

    public Task<PaymentProcessorGatewayResult> VerifyWebhookAsync(PaymentProcessorGatewayContext context, PaymentWebhookIngestRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PayloadJson)) return Task.FromResult(PaymentProcessorGatewayResult.Failure("empty_payload", "Webhook payload is required."));
        if (context.Secrets.TryGetValue(PaymentCredentialSecretKind.WebhookSecret, out var secret) && !string.IsNullOrWhiteSpace(secret) && !VerifySignature(request.PayloadJson, request.SignatureHeader, secret))
        {
            return Task.FromResult(PaymentProcessorGatewayResult.Failure("invalid_signature", "ACH webhook signature verification failed."));
        }
        return Task.FromResult(PaymentProcessorGatewayResult.Success(PaymentProcessorOperationStatus.Succeeded.ToString(), request.ProviderEventId, request.EventType));
    }

    public async Task<IReadOnlyList<PaymentSettlementBatchDto>> PollSettlementsAsync(PaymentProcessorGatewayContext context, DateTime fromDateUtc, DateTime toDateUtc, CancellationToken cancellationToken = default)
    {
        var endpoint = Endpoint(context);
        if (endpoint is null) return [];
        using var http = CreateClient(context, endpoint);
        using var response = await http.GetAsync($"settlements?fromDateUtc={Uri.EscapeDataString(fromDateUtc.ToString("O"))}&toDateUtc={Uri.EscapeDataString(toDateUtc.ToString("O"))}", cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(json)) return [];
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var rows = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray().ToList()
            : root.TryGetProperty("settlements", out var settlements) && settlements.ValueKind == JsonValueKind.Array
                ? settlements.EnumerateArray().ToList()
                : [];
        return rows.Select(row => new PaymentSettlementBatchDto()
        {
            TenantId = context.TenantId,
            PaymentGatewayCredentialId = context.CredentialId,
            ProviderCode = PaymentProcessorCodes.Ach,
            SettlementBatchReference = Read(row, "settlementBatchReference") ?? Read(row, "batchId") ?? $"ach-{Guid.NewGuid():N}",
            SettlementDateUtc = ReadDate(row, "settlementDateUtc") ?? DateTime.UtcNow,
            GrossAmount = ReadDecimal(row, "grossAmount") ?? 0m,
            FeeAmount = ReadDecimal(row, "feeAmount") ?? 0m,
            NetAmount = ReadDecimal(row, "netAmount") ?? 0m,
            StatusCode = Read(row, "statusCode") ?? PaymentProcessorOperationStatus.Settled.ToString(),
            CreatedDateUtc = DateTime.UtcNow
        }).ToList();
    }

    private static async Task<PaymentProcessorGatewayResult> PostOperationAsync(PaymentProcessorGatewayContext context, string path, object payload, bool requireProviderOperationId, CancellationToken cancellationToken)
    {
        var endpoint = Endpoint(context);
        if (endpoint is null) return PaymentProcessorGatewayResult.Failure("ach_processor_not_configured", "ACH processor endpoint URL is required.");
        using var http = CreateClient(context, endpoint);
        var json = JsonSerializer.Serialize(payload);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        AddSignatureHeader(context, content, json);
        using var response = await http.PostAsync(path, content, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return PaymentProcessorGatewayResult.Failure("ach_processor_error", string.IsNullOrWhiteSpace(responseJson) ? response.ReasonPhrase ?? "ACH processor request failed." : responseJson, response.StatusCode.ToString());
        return ParseResult(responseJson, requireProviderOperationId);
    }

    private static object CreateEnvelope(PaymentProcessorGatewayContext context, string operation, object payload, Guid? accountId = null, Guid? invoiceId = null, decimal? amount = null, string? currencyCode = null, Guid? paymentId = null)
        => new
        {
            contractVersion = AdapterContractVersion,
            operation,
            idempotencyKey = CreateIdempotencyKey(context, operation, accountId, invoiceId, paymentId, amount, currencyCode),
            tenantId = context.TenantId,
            credentialId = context.CredentialId,
            providerCode = context.ProviderCode,
            environment = context.Environment,
            accountId,
            invoiceId,
            paymentId,
            amount,
            currencyCode,
            payload
        };

    private static string CreateIdempotencyKey(PaymentProcessorGatewayContext context, string operation, Guid? accountId, Guid? invoiceId, Guid? paymentId, decimal? amount, string? currencyCode)
        => string.Join(':', new[]
        {
            AdapterContractVersion,
            context.TenantId.ToString("N"),
            context.CredentialId.ToString("N"),
            operation,
            accountId?.ToString("N") ?? "no-account",
            invoiceId?.ToString("N") ?? paymentId?.ToString("N") ?? "no-source",
            amount?.ToString("0.00") ?? "no-amount",
            currencyCode ?? "no-currency"
        });

    private static PaymentProcessorGatewayResult ParseResult(string json, bool requireProviderOperationId)
    {
        if (string.IsNullOrWhiteSpace(json)) return PaymentProcessorGatewayResult.Failure("empty_processor_response", "ACH processor returned an empty response.");
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var success = ReadBool(root, "isSuccess") ?? ReadBool(root, "success") ?? true;
        if (!success) return PaymentProcessorGatewayResult.Failure(Read(root, "failureCode") ?? Read(root, "errorCode") ?? "ach_processor_declined", Read(root, "failureMessage") ?? Read(root, "errorMessage") ?? "ACH processor declined the request.", Read(root, "providerStatus") ?? Read(root, "status"));
        var providerOperationId = Read(root, "providerOperationId") ?? Read(root, "id");
        var checkoutUrl = Read(root, "checkoutUrl");
        var tokenReference = Read(root, "tokenReference");
        if (requireProviderOperationId && string.IsNullOrWhiteSpace(providerOperationId) && string.IsNullOrWhiteSpace(checkoutUrl) && string.IsNullOrWhiteSpace(tokenReference))
        {
            return PaymentProcessorGatewayResult.Failure("missing_processor_reference", "ACH adapter response must include providerOperationId, checkoutUrl, or tokenReference for successful operations.");
        }
        return PaymentProcessorGatewayResult.Success(Read(root, "statusCode") ?? PaymentProcessorOperationStatus.Pending.ToString(), providerOperationId, Read(root, "providerStatus") ?? Read(root, "status"), checkoutUrl, tokenReference);
    }

    private static Uri? Endpoint(PaymentProcessorGatewayContext context)
        => Uri.TryCreate(context.ProcessorEndpointUrl, UriKind.Absolute, out var endpoint) && endpoint.Scheme is "https" ? endpoint : null;

    private static HttpClient CreateClient(PaymentProcessorGatewayContext context, Uri endpoint)
    {
        var http = new HttpClient { BaseAddress = endpoint.AbsoluteUri.EndsWith('/') ? endpoint : new Uri(endpoint.AbsoluteUri + "/") };
        if (context.Secrets.TryGetValue(PaymentCredentialSecretKind.ApiKey, out var apiKey) && !string.IsNullOrWhiteSpace(apiKey))
        {
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        }
        if (context.Secrets.TryGetValue(PaymentCredentialSecretKind.MerchantId, out var merchantId) && !string.IsNullOrWhiteSpace(merchantId))
        {
            http.DefaultRequestHeaders.Add("X-AMS-Merchant-Id", merchantId);
        }
        return http;
    }

    private static void AddSignatureHeader(PaymentProcessorGatewayContext context, HttpContent content, string payload)
    {
        if (!context.Secrets.TryGetValue(PaymentCredentialSecretKind.ApiSecret, out var secret) || string.IsNullOrWhiteSpace(secret)) return;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        content.Headers.Add("X-AMS-Signature", Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant());
    }

    private static bool VerifySignature(string payload, string? signatureHeader, string secret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader)) return false;
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signatureHeader.Trim().ToLowerInvariant()));
    }

    private static string? Read(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static bool? ReadBool(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : null;
    private static decimal? ReadDecimal(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.TryGetDecimal(out var result) ? result : null;
    private static DateTime? ReadDate(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var result) ? result.ToUniversalTime() : null;
}
