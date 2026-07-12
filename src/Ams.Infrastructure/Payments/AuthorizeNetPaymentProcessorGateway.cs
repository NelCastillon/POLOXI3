using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Features.Payments;

namespace Ams.Infrastructure.Payments;

public sealed class AuthorizeNetPaymentProcessorGateway : IPaymentProcessorGateway
{
    public string ProviderCode => PaymentProcessorCodes.AuthorizeNet;

    public Task<PaymentProcessorGatewayResult> CreateCheckoutSessionAsync(PaymentProcessorGatewayContext context, CreateCheckoutSessionRequest request, CancellationToken cancellationToken = default)
    {
        return CreateAcceptHostedSessionAsync(context, request, cancellationToken);
    }

    public Task<PaymentProcessorGatewayResult> TokenizePaymentMethodAsync(PaymentProcessorGatewayContext context, TokenizePaymentMethodRequest request, CancellationToken cancellationToken = default)
        => Task.FromResult(PaymentProcessorGatewayResult.Success(PaymentProcessorOperationStatus.Succeeded.ToString(), tokenReference: request.ProviderToken));

    public async Task<PaymentProcessorGatewayResult> ChargeAsync(PaymentProcessorGatewayContext context, ChargePaymentRequest request, string? tokenReference, CancellationToken cancellationToken = default)
    {
        var loginId = RequiredSecret(context, PaymentCredentialSecretKind.LoginId, PaymentCredentialSecretKind.ApiKey);
        var transactionKey = RequiredSecret(context, PaymentCredentialSecretKind.TransactionKey, PaymentCredentialSecretKind.ApiSecret);
        var payload = new
        {
            createTransactionRequest = new
            {
                merchantAuthentication = new { name = loginId, transactionKey },
                transactionRequest = new
                {
                    transactionType = request.Capture ? "authCaptureTransaction" : "authOnlyTransaction",
                    amount = request.Amount.ToString("0.00"),
                    profile = string.IsNullOrWhiteSpace(tokenReference) ? null : new { paymentProfile = new { paymentProfileId = tokenReference } },
                    order = new { invoiceNumber = request.InvoiceId?.ToString("N"), description = request.Description ?? "AMS premium payment" }
                }
            }
        };
        var result = await PostAuthorizeNetAsync(context, payload, cancellationToken);
        return result.IsSuccess
            ? PaymentProcessorGatewayResult.Success(PaymentProcessorOperationStatus.Succeeded.ToString(), result.TransactionId, result.ResponseCode)
            : PaymentProcessorGatewayResult.Failure(result.ErrorCode ?? "authnet_error", result.ErrorMessage ?? "Authorize.Net transaction failed.", result.ResponseCode);
    }

    public async Task<PaymentProcessorGatewayResult> RefundAsync(PaymentProcessorGatewayContext context, RefundPaymentRequest request, string? providerOperationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerOperationId)) return PaymentProcessorGatewayResult.Failure("missing_provider_operation", "Provider transaction id is required for refund.");
        var loginId = RequiredSecret(context, PaymentCredentialSecretKind.LoginId, PaymentCredentialSecretKind.ApiKey);
        var transactionKey = RequiredSecret(context, PaymentCredentialSecretKind.TransactionKey, PaymentCredentialSecretKind.ApiSecret);
        var payload = new
        {
            createTransactionRequest = new
            {
                merchantAuthentication = new { name = loginId, transactionKey },
                transactionRequest = new { transactionType = "refundTransaction", amount = request.Amount.ToString("0.00"), refTransId = providerOperationId }
            }
        };
        var result = await PostAuthorizeNetAsync(context, payload, cancellationToken);
        return result.IsSuccess
            ? PaymentProcessorGatewayResult.Success(PaymentProcessorOperationStatus.Refunded.ToString(), result.TransactionId, result.ResponseCode)
            : PaymentProcessorGatewayResult.Failure(result.ErrorCode ?? "authnet_error", result.ErrorMessage ?? "Authorize.Net refund failed.", result.ResponseCode);
    }

    public async Task<PaymentProcessorGatewayResult> VoidAsync(PaymentProcessorGatewayContext context, VoidPaymentRequest request, string? providerOperationId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerOperationId)) return PaymentProcessorGatewayResult.Failure("missing_provider_operation", "Provider transaction id is required for void.");
        var loginId = RequiredSecret(context, PaymentCredentialSecretKind.LoginId, PaymentCredentialSecretKind.ApiKey);
        var transactionKey = RequiredSecret(context, PaymentCredentialSecretKind.TransactionKey, PaymentCredentialSecretKind.ApiSecret);
        var payload = new
        {
            createTransactionRequest = new
            {
                merchantAuthentication = new { name = loginId, transactionKey },
                transactionRequest = new { transactionType = "voidTransaction", refTransId = providerOperationId }
            }
        };
        var result = await PostAuthorizeNetAsync(context, payload, cancellationToken);
        return result.IsSuccess
            ? PaymentProcessorGatewayResult.Success(PaymentProcessorOperationStatus.Voided.ToString(), result.TransactionId, result.ResponseCode)
            : PaymentProcessorGatewayResult.Failure(result.ErrorCode ?? "authnet_error", result.ErrorMessage ?? "Authorize.Net void failed.", result.ResponseCode);
    }

    public Task<PaymentProcessorGatewayResult> VerifyWebhookAsync(PaymentProcessorGatewayContext context, PaymentWebhookIngestRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.PayloadJson)) return Task.FromResult(PaymentProcessorGatewayResult.Failure("empty_payload", "Webhook payload is required."));
        var signatureKey = RequiredSecret(context, PaymentCredentialSecretKind.WebhookSecret, PaymentCredentialSecretKind.TransactionKey, PaymentCredentialSecretKind.ApiSecret);

        if (!VerifyAuthorizeNetSignature(request.PayloadJson, request.SignatureHeader, signatureKey))
        {
            return Task.FromResult(PaymentProcessorGatewayResult.Failure("invalid_signature", "Authorize.Net webhook signature verification failed."));
        }

        return Task.FromResult(PaymentProcessorGatewayResult.Success(PaymentProcessorOperationStatus.Succeeded.ToString(), request.ProviderEventId, request.EventType));
    }

    public Task<IReadOnlyList<PaymentSettlementBatchDto>> PollSettlementsAsync(PaymentProcessorGatewayContext context, DateTime fromDateUtc, DateTime toDateUtc, CancellationToken cancellationToken = default)
        => PollAuthorizeNetSettlementsAsync(context, fromDateUtc, toDateUtc, cancellationToken);

    private static async Task<PaymentProcessorGatewayResult> CreateAcceptHostedSessionAsync(PaymentProcessorGatewayContext context, CreateCheckoutSessionRequest request, CancellationToken cancellationToken)
    {
        var loginId = RequiredSecret(context, PaymentCredentialSecretKind.LoginId, PaymentCredentialSecretKind.ApiKey);
        var transactionKey = RequiredSecret(context, PaymentCredentialSecretKind.TransactionKey, PaymentCredentialSecretKind.ApiSecret);
        var payload = new
        {
            getHostedPaymentPageRequest = new
            {
                merchantAuthentication = new { name = loginId, transactionKey },
                transactionRequest = new
                {
                    transactionType = "authCaptureTransaction",
                    amount = request.Amount.ToString("0.00"),
                    order = new { invoiceNumber = request.InvoiceId?.ToString("N"), description = request.Description ?? "AMS premium payment" }
                },
                hostedPaymentSettings = new
                {
                    setting = new[]
                    {
                        new { settingName = "hostedPaymentReturnOptions", settingValue = JsonSerializer.Serialize(new { showReceipt = false, url = request.SuccessUrl, urlText = "Continue", cancelUrl = request.CancelUrl, cancelUrlText = "Cancel" }) },
                        new { settingName = "hostedPaymentButtonOptions", settingValue = JsonSerializer.Serialize(new { text = "Pay" }) }
                    }
                }
            }
        };
        var result = await PostAuthorizeNetRawAsync(context, payload, cancellationToken);
        if (!result.IsSuccess) return PaymentProcessorGatewayResult.Failure(result.ErrorCode ?? "authnet_error", result.ErrorMessage ?? "Authorize.Net Accept Hosted token request failed.", result.ResponseCode);
        var token = Read(result.Root, "token");
        if (string.IsNullOrWhiteSpace(token)) return PaymentProcessorGatewayResult.Failure("missing_accept_hosted_token", "Authorize.Net did not return an Accept Hosted token.", result.ResponseCode);
        var baseUrl = string.Equals(context.Environment, PaymentProcessorEnvironment.Production.ToString(), StringComparison.OrdinalIgnoreCase)
            ? "https://accept.authorize.net/payment/payment"
            : "https://test.authorize.net/payment/payment";
        return PaymentProcessorGatewayResult.Success(PaymentProcessorOperationStatus.Pending.ToString(), token, "created", $"{baseUrl}?token={Uri.EscapeDataString(token)}");
    }

    private static async Task<IReadOnlyList<PaymentSettlementBatchDto>> PollAuthorizeNetSettlementsAsync(PaymentProcessorGatewayContext context, DateTime fromDateUtc, DateTime toDateUtc, CancellationToken cancellationToken)
    {
        var loginId = RequiredSecret(context, PaymentCredentialSecretKind.LoginId, PaymentCredentialSecretKind.ApiKey);
        var transactionKey = RequiredSecret(context, PaymentCredentialSecretKind.TransactionKey, PaymentCredentialSecretKind.ApiSecret);
        var payload = new
        {
            getSettledBatchListRequest = new
            {
                merchantAuthentication = new { name = loginId, transactionKey },
                includeStatistics = true,
                firstSettlementDate = fromDateUtc.ToString("O"),
                lastSettlementDate = toDateUtc.ToString("O")
            }
        };
        var result = await PostAuthorizeNetRawAsync(context, payload, cancellationToken);
        if (!result.IsSuccess) return [];
        if (!result.Root.TryGetProperty("batchList", out var list) || list.ValueKind != JsonValueKind.Array) return [];
        return list.EnumerateArray().Select(batch =>
        {
            var gross = ReadDecimal(batch, "settlementAmount") ?? 0m;
            return new PaymentSettlementBatchDto
            {
                TenantId = context.TenantId,
                PaymentGatewayCredentialId = context.CredentialId,
                ProviderCode = PaymentProcessorCodes.AuthorizeNet,
                SettlementBatchReference = Read(batch, "batchId") ?? $"authnet-{Guid.NewGuid():N}",
                SettlementDateUtc = ReadDate(batch, "settlementTimeUTC") ?? DateTime.UtcNow,
                GrossAmount = gross,
                FeeAmount = 0m,
                NetAmount = gross,
                StatusCode = Read(batch, "settlementState") ?? PaymentProcessorOperationStatus.Settled.ToString(),
                CreatedDateUtc = DateTime.UtcNow
            };
        }).ToList();
    }

    private static async Task<AuthorizeNetResult> PostAuthorizeNetAsync(PaymentProcessorGatewayContext context, object payload, CancellationToken cancellationToken)
    {
        var result = await PostAuthorizeNetRawAsync(context, payload, cancellationToken);
        if (!result.IsSuccess) return new(false, null, result.ResponseCode, result.ErrorCode, result.ErrorMessage);
        var transactionResponse = result.Root.TryGetProperty("transactionResponse", out var tr) ? tr : default;
        var responseCode = Read(transactionResponse, "responseCode");
        var transactionId = Read(transactionResponse, "transId");
        var isSuccess = responseCode == "1" || responseCode == "4";
        var error = transactionResponse.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array && errors.GetArrayLength() > 0 ? errors[0] : default;
        return new(isSuccess, transactionId, responseCode, Read(error, "errorCode"), Read(error, "errorText"));
    }

    private static async Task<AuthorizeNetRawResult> PostAuthorizeNetRawAsync(PaymentProcessorGatewayContext context, object payload, CancellationToken cancellationToken)
    {
        var baseUrl = string.Equals(context.Environment, PaymentProcessorEnvironment.Production.ToString(), StringComparison.OrdinalIgnoreCase)
            ? "https://api.authorize.net/xml/v1/request.api"
            : "https://apitest.authorize.net/xml/v1/request.api";
        using var http = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, baseUrl)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        using var response = await http.SendAsync(request, cancellationToken);
        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode) return new(false, default, response.StatusCode.ToString(), "http_error", json);
        using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
        var root = document.RootElement.Clone();
        var messages = root.TryGetProperty("messages", out var m) ? m : default;
        var resultCode = Read(messages, "resultCode");
        var isSuccess = string.Equals(resultCode, "Ok", StringComparison.OrdinalIgnoreCase);
        var message = messages.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.Array && msg.GetArrayLength() > 0 ? msg[0] : default;
        return new(isSuccess, root, resultCode, Read(message, "code"), Read(message, "text"));
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
    private static decimal? ReadDecimal(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.TryGetDecimal(out var result) ? result : null;
    private static DateTime? ReadDate(JsonElement element, string name) => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && DateTime.TryParse(value.GetString(), out var result) ? result.ToUniversalTime() : null;
    private static bool VerifyAuthorizeNetSignature(string payload, string? signatureHeader, string signatureKey)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(signatureKey)) return false;
        var supplied = signatureHeader.Trim();
        const string sha512Prefix = "sha512=";
        if (supplied.StartsWith(sha512Prefix, StringComparison.OrdinalIgnoreCase)) supplied = supplied[sha512Prefix.Length..];
        using var hmac = new HMACSHA512(Encoding.UTF8.GetBytes(signatureKey));
        var expected = Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        return CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(supplied.ToLowerInvariant()));
    }
    private sealed record AuthorizeNetResult(bool IsSuccess, string? TransactionId, string? ResponseCode, string? ErrorCode, string? ErrorMessage);
    private sealed record AuthorizeNetRawResult(bool IsSuccess, JsonElement Root, string? ResponseCode, string? ErrorCode, string? ErrorMessage);
}
