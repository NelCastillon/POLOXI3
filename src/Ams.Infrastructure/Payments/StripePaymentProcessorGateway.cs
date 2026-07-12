using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Payments;

namespace Ams.Infrastructure.Payments;

public sealed class StripePaymentProcessorGateway : IPaymentProcessorGateway
{
    public string ProviderCode => PaymentProcessorCodes.Stripe;

    public async Task<PaymentProcessorGatewayResult> CreateCheckoutSessionAsync(PaymentProcessorGatewayContext context, CreateCheckoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        var secret = RequiredSecret(context, PaymentCredentialSecretKind.ApiSecret, PaymentCredentialSecretKind.ApiKey);
        using var http = CreateStripeClient(secret);
        var form = new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["success_url"] = request.SuccessUrl,
            ["cancel_url"] = request.CancelUrl,
            ["line_items[0][quantity]"] = "1",
            ["line_items[0][price_data][currency]"] = request.CurrencyCode.ToLowerInvariant(),
            ["line_items[0][price_data][unit_amount]"] = ToMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture),
            ["line_items[0][price_data][product_data][name]"] = string.IsNullOrWhiteSpace(request.Description) ? "AMS premium payment" : request.Description!,
            ["metadata[tenantId]"] = request.TenantId.ToString(),
            ["metadata[accountId]"] = request.AccountId.ToString()
        };
        if (request.InvoiceId is { } invoiceId) form["metadata[invoiceId]"] = invoiceId.ToString();
        var result = await PostStripeAsync(http, "checkout/sessions", form, cancellationToken);
        return result.IsSuccess
            ? PaymentProcessorGatewayResult.Success(PaymentProcessorOperationStatus.Pending.ToString(), result.Id, result.Status, result.Url)
            : PaymentProcessorGatewayResult.Failure(result.ErrorCode ?? "stripe_error", result.ErrorMessage ?? "Stripe checkout session failed.", result.Status);
    }

    public Task<PaymentProcessorGatewayResult> TokenizePaymentMethodAsync(PaymentProcessorGatewayContext context, TokenizePaymentMethodRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(PaymentProcessorGatewayResult.Success(PaymentProcessorOperationStatus.Succeeded.ToString(), tokenReference: request.ProviderToken));

    public async Task<PaymentProcessorGatewayResult> ChargeAsync(PaymentProcessorGatewayContext context, ChargePaymentRequest request, string? tokenReference, CancellationToken cancellationToken = default)
    {
        var secret = RequiredSecret(context, PaymentCredentialSecretKind.ApiSecret, PaymentCredentialSecretKind.ApiKey);
        using var http = CreateStripeClient(secret);
        var form = new Dictionary<string, string>
        {
            ["amount"] = ToMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture),
            ["currency"] = request.CurrencyCode.ToLowerInvariant(),
            ["capture_method"] = request.Capture ? "automatic" : "manual",
            ["description"] = string.IsNullOrWhiteSpace(request.Description) ? "AMS premium payment" : request.Description!,
            ["metadata[tenantId]"] = request.TenantId.ToString(),
            ["metadata[accountId]"] = request.AccountId.ToString()
        };
        if (!string.IsNullOrWhiteSpace(tokenReference)) form["payment_method"] = tokenReference!;
        if (request.InvoiceId is { } invoiceId) form["metadata[invoiceId]"] = invoiceId.ToString();
        var result = await PostStripeAsync(http, "payment_intents", form, cancellationToken);
        return result.IsSuccess
            ? PaymentProcessorGatewayResult.Success(StripeStatusToAms(result.Status), result.Id, result.Status)
            : PaymentProcessorGatewayResult.Failure(result.ErrorCode ?? "stripe_error", result.ErrorMessage ?? "Stripe charge failed.", result.Status);
    }

    public async Task<PaymentProcessorGatewayResult> RefundAsync(PaymentProcessorGatewayContext context, RefundPaymentRequest request, string? providerOperationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerOperationId)) return PaymentProcessorGatewayResult.Failure("missing_provider_operation", "Provider operation id is required for refund.");
        var secret = RequiredSecret(context, PaymentCredentialSecretKind.ApiSecret, PaymentCredentialSecretKind.ApiKey);
        using var http = CreateStripeClient(secret);
        var form = new Dictionary<string, string>
        {
            ["payment_intent"] = providerOperationId!,
            ["amount"] = ToMinorUnits(request.Amount).ToString(CultureInfo.InvariantCulture),
            ["metadata[tenantId]"] = request.TenantId.ToString(),
            ["metadata[paymentId]"] = request.PaymentId.ToString()
        };
        if (!string.IsNullOrWhiteSpace(request.Reason)) form["metadata[reason]"] = request.Reason!;
        var result = await PostStripeAsync(http, "refunds", form, cancellationToken);
        return result.IsSuccess
            ? PaymentProcessorGatewayResult.Success(PaymentProcessorOperationStatus.Refunded.ToString(), result.Id, result.Status)
            : PaymentProcessorGatewayResult.Failure(result.ErrorCode ?? "stripe_error", result.ErrorMessage ?? "Stripe refund failed.", result.Status);
    }

    public async Task<PaymentProcessorGatewayResult> VoidAsync(PaymentProcessorGatewayContext context, VoidPaymentRequest request, string? providerOperationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerOperationId)) return PaymentProcessorGatewayResult.Failure("missing_provider_operation", "Provider operation id is required for void.");
        var secret = RequiredSecret(context, PaymentCredentialSecretKind.ApiSecret, PaymentCredentialSecretKind.ApiKey);
        using var http = CreateStripeClient(secret);
        var result = await PostStripeAsync(http, $"payment_intents/{Uri.EscapeDataString(providerOperationId!)}/cancel", new Dictionary<string, string>(), cancellationToken);
        return result.IsSuccess
            ? PaymentProcessorGatewayResult.Success(PaymentProcessorOperationStatus.Voided.ToString(), result.Id, result.Status)
            : PaymentProcessorGatewayResult.Failure(result.ErrorCode ?? "stripe_error", result.ErrorMessage ?? "Stripe void failed.", result.Status);
    }

    public Task<PaymentProcessorGatewayResult> VerifyWebhookAsync(PaymentProcessorGatewayContext context, PaymentWebhookIngestRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PayloadJson)) return Task.FromResult(PaymentProcessorGatewayResult.Failure("empty_payload", "Webhook payload is required."));
        if (!context.Secrets.TryGetValue(PaymentCredentialSecretKind.WebhookSecret, out var webhookSecret) || string.IsNullOrWhiteSpace(webhookSecret))
        {
            return Task.FromResult(PaymentProcessorGatewayResult.Failure("missing_webhook_secret", "Stripe webhook secret is required for signature verification."));
        }

        if (!VerifyStripeSignature(request.PayloadJson, request.SignatureHeader, webhookSecret))
        {
            return Task.FromResult(PaymentProcessorGatewayResult.Failure("invalid_signature", "Stripe webhook signature verification failed."));
        }

        return Task.FromResult(PaymentProcessorGatewayResult.Success(PaymentProcessorOperationStatus.Succeeded.ToString(), request.ProviderEventId, request.EventType));
    }

    public Task<IReadOnlyList<PaymentSettlementBatchDto>> PollSettlementsAsync(PaymentProcessorGatewayContext context, DateTime fromDateUtc, DateTime toDateUtc, CancellationToken cancellationToken = default)
    {
        return PollStripeSettlementsAsync(context, fromDateUtc, toDateUtc, cancellationToken);
    }

    private static HttpClient CreateStripeClient(string secret)
    {
        var http = new HttpClient { BaseAddress = new Uri("https://api.stripe.com/v1/") };
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", secret);
        return http;
    }

    private static async Task<StripeApiResult> PostStripeAsync(HttpClient http, string path, Dictionary<string, string> form, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(path, new FormUrlEncodedContent(form), cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var root = document.RootElement;
        if (!response.IsSuccessStatusCode)
        {
            var error = root.TryGetProperty("error", out var e) ? e : default;
            return new(false, null, null, null, Read(error, "code"), Read(error, "message"));
        }
        return new(true, Read(root, "id"), Read(root, "status"), Read(root, "url"), null, null);
    }

    private static async Task<IReadOnlyList<PaymentSettlementBatchDto>> PollStripeSettlementsAsync(PaymentProcessorGatewayContext context, DateTime fromDateUtc, DateTime toDateUtc, CancellationToken cancellationToken)
    {
        var secret = RequiredSecret(context, PaymentCredentialSecretKind.ApiSecret, PaymentCredentialSecretKind.ApiKey);
        using var http = CreateStripeClient(secret);
        var availableOnGte = new DateTimeOffset(fromDateUtc).ToUnixTimeSeconds();
        var availableOnLte = new DateTimeOffset(toDateUtc).ToUnixTimeSeconds();
        using var response = await http.GetAsync($"balance_transactions?limit=100&available_on[gte]={availableOnGte}&available_on[lte]={availableOnLte}", cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return [];
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        if (!document.RootElement.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array) return [];

        var batches = data.EnumerateArray()
            .Where(x => Read(x, "type") is "charge" or "payment")
            .GroupBy(x => DateTimeOffset.FromUnixTimeSeconds(ReadLong(x, "available_on") ?? availableOnGte).UtcDateTime.Date)
            .Select(g => new PaymentSettlementBatchDto
            {
                TenantId = context.TenantId,
                PaymentGatewayCredentialId = context.CredentialId,
                ProviderCode = PaymentProcessorCodes.Stripe,
                SettlementBatchReference = $"stripe-{g.Key:yyyyMMdd}",
                SettlementDateUtc = g.Key,
                GrossAmount = g.Sum(x => MinorToMajor(ReadLong(x, "amount") ?? 0)),
                FeeAmount = g.Sum(x => MinorToMajor(ReadLong(x, "fee") ?? 0)),
                NetAmount = g.Sum(x => MinorToMajor(ReadLong(x, "net") ?? 0)),
                StatusCode = PaymentProcessorOperationStatus.Settled.ToString(),
                CreatedDateUtc = DateTime.UtcNow
            })
            .ToList();
        return batches;
    }

    private static string RequiredSecret(PaymentProcessorGatewayContext context, params PaymentCredentialSecretKind[] kinds)
    {
        foreach (var kind in kinds)
        {
            if (context.Secrets.TryGetValue(kind, out var value) && !string.IsNullOrWhiteSpace(value)) return value;
        }
        throw new InvalidOperationException($"{context.ProviderCode} credential is missing required secret.");
    }

    private static string? Read(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    private static long? ReadLong(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.TryGetInt64(out var result) ? result : null;
    private static long ToMinorUnits(decimal amount) => decimal.ToInt64(decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero));
    private static decimal MinorToMajor(long amount) => decimal.Round(amount / 100m, 2, MidpointRounding.AwayFromZero);
    private static string StripeStatusToAms(string? status) => status is "succeeded" ? PaymentProcessorOperationStatus.Succeeded.ToString() : status is "requires_action" or "requires_confirmation" or "requires_payment_method" ? PaymentProcessorOperationStatus.RequiresAction.ToString() : PaymentProcessorOperationStatus.Pending.ToString();
    private static bool VerifyStripeSignature(string payload, string? signatureHeader, string webhookSecret)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader)) return false;
        var parts = signatureHeader.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Split('=', 2))
            .Where(x => x.Length == 2)
            .GroupBy(x => x[0], StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.Select(v => v[1]).ToArray(), StringComparer.OrdinalIgnoreCase);
        if (!parts.TryGetValue("t", out var timestamps) || timestamps.Length == 0) return false;
        if (!parts.TryGetValue("v1", out var signatures) || signatures.Length == 0) return false;

        var signedPayload = $"{timestamps[0]}.{payload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(webhookSecret));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(signedPayload))).ToLowerInvariant();
        return signatures.Any(signature => CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(signature.ToLowerInvariant())));
    }
    private sealed record StripeApiResult(bool IsSuccess, string? Id, string? Status, string? Url, string? ErrorCode, string? ErrorMessage);
}
