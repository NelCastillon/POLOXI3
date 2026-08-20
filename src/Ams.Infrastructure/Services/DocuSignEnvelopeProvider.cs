using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Documents;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ams.Infrastructure.Services;

public sealed class DocuSignEnvelopeProvider(HttpClient httpClient) : IESignEnvelopeProvider
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ESignEnvelopeDispatchResult> SendAsync(
        ESignDispatchWorkItem workItem,
        Stream documentContent,
        CancellationToken cancellationToken = default)
    {
        Validate(workItem);

        var privateKey = ResolvePrivateKey(workItem.SecretReference);
        var accessToken = await RequestAccessTokenAsync(workItem, privateKey, cancellationToken);

        using var buffer = new MemoryStream();
        await documentContent.CopyToAsync(buffer, cancellationToken);
        var request = new
        {
            emailSubject = $"Signature requested: {workItem.FileName}",
            emailBlurb = workItem.Message,
            status = "sent",
            documents = new[]
            {
                new
                {
                    documentBase64 = Convert.ToBase64String(buffer.ToArray()),
                    name = workItem.FileName,
                    fileExtension = GetFileExtension(workItem.FileName, workItem.ContentType),
                    documentId = "1"
                }
            },
            recipients = new
            {
                signers = new[]
                {
                    new
                    {
                        email = workItem.SignerEmail,
                        name = workItem.SignerName,
                        recipientId = "1",
                        routingOrder = "1",
                        tabs = new
                        {
                            signHereTabs = new[]
                            {
                                new { documentId = "1", pageNumber = "1", xPosition = "100", yPosition = "700" }
                            }
                        }
                    }
                }
            },
            customFields = new
            {
                textCustomFields = new[]
                {
                    new { name = "AmsESignRequestId", value = workItem.ESignRequestId.ToString("D"), show = "false" },
                    new { name = "AmsTenantId", value = workItem.TenantId.ToString("D"), show = "false" }
                }
            }
        };

        var endpoint = $"{workItem.ApiBaseUri.TrimEnd('/')}/v2.1/accounts/{Uri.EscapeDataString(workItem.AccountId)}/envelopes";
        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(request, options: JsonOptions)
        };
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        message.Headers.TryAddWithoutValidation("X-DocuSign-Idempotency-Key", workItem.IdempotencyKey);

        using var response = await httpClient.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw CreateProviderException(response.StatusCode, payload, response.Headers.RetryAfter);

        using var json = JsonDocument.Parse(payload);
        var root = json.RootElement;
        var envelopeId = root.TryGetProperty("envelopeId", out var envelopeElement) ? envelopeElement.GetString() : null;
        var status = root.TryGetProperty("status", out var statusElement) ? statusElement.GetString() : null;
        if (string.IsNullOrWhiteSpace(envelopeId))
            throw new ESignProviderException("INVALID_PROVIDER_RESPONSE", "DocuSign did not return an envelope identifier.", false);

        return new ESignEnvelopeDispatchResult(envelopeId, status ?? "sent", "1");
    }

    private async Task<string> RequestAccessTokenAsync(ESignDispatchWorkItem workItem, string privateKey, CancellationToken cancellationToken)
    {
        var assertion = CreateJwtAssertion(workItem, privateKey);
        var endpoint = $"{workItem.OAuthBaseUri.TrimEnd('/')}/oauth/token";
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "urn:ietf:params:oauth:grant-type:jwt-bearer",
            ["assertion"] = assertion
        });
        using var response = await httpClient.PostAsync(endpoint, content, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
            throw CreateProviderException(response.StatusCode, payload, response.Headers.RetryAfter);

        using var json = JsonDocument.Parse(payload);
        var token = json.RootElement.TryGetProperty("access_token", out var tokenElement) ? tokenElement.GetString() : null;
        return !string.IsNullOrWhiteSpace(token)
            ? token
            : throw new ESignProviderException("INVALID_AUTH_RESPONSE", "DocuSign did not return an access token.", false);
    }

    private static string CreateJwtAssertion(ESignDispatchWorkItem workItem, string privateKey)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var audience = new Uri(workItem.OAuthBaseUri).Host;
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new { alg = "RS256", typ = "JWT" }));
        var claims = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new
        {
            iss = workItem.IntegrationKey,
            sub = workItem.UserId,
            aud = audience,
            iat = now,
            exp = now + 3600,
            scope = "signature impersonation"
        }));
        var signingInput = $"{header}.{claims}";

        try
        {
            using var rsa = RSA.Create();
            rsa.ImportFromPem(privateKey);
            var signature = rsa.SignData(Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            return $"{signingInput}.{Base64UrlEncode(signature)}";
        }
        catch (CryptographicException exception)
        {
            throw new ESignProviderException("INVALID_PRIVATE_KEY", "The configured DocuSign RSA private key is invalid.", false, innerException: exception);
        }
    }

    private static ESignProviderException CreateProviderException(HttpStatusCode statusCode, string payload, RetryConditionHeaderValue? retryAfter)
    {
        var errorCode = $"DOCUSIGN_{(int)statusCode}";
        var message = $"DocuSign returned HTTP {(int)statusCode}.";
        try
        {
            using var json = JsonDocument.Parse(payload);
            if (json.RootElement.TryGetProperty("errorCode", out var codeElement) || json.RootElement.TryGetProperty("error", out codeElement))
                errorCode = codeElement.GetString() ?? errorCode;
            if (json.RootElement.TryGetProperty("message", out var messageElement) || json.RootElement.TryGetProperty("error_description", out messageElement))
                message = messageElement.GetString() ?? message;
        }
        catch (JsonException)
        {
            if (!string.IsNullOrWhiteSpace(payload))
                message = payload.Length <= 1000 ? payload : payload[..1000];
        }

        var retryable = statusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.TooManyRequests || (int)statusCode >= 500;
        var retryAtUtc = retryAfter?.Date?.UtcDateTime;
        if (retryAtUtc is null && retryAfter?.Delta is { } delta)
            retryAtUtc = DateTime.UtcNow.Add(delta);
        return new ESignProviderException(errorCode, message, retryable, retryAtUtc);
    }

    private static string ResolvePrivateKey(string secretReference)
    {
        var value = Environment.GetEnvironmentVariable(secretReference);
        if (string.IsNullOrWhiteSpace(value))
            throw new ESignProviderException("SECRET_NOT_FOUND", $"The DocuSign secret reference '{secretReference}' could not be resolved.", false);
        if (File.Exists(value))
            value = File.ReadAllText(value);
        return value.Replace("\\n", "\n", StringComparison.Ordinal);
    }

    private static void Validate(ESignDispatchWorkItem workItem)
    {
        if (string.IsNullOrWhiteSpace(workItem.AccountId) || string.IsNullOrWhiteSpace(workItem.IntegrationKey) ||
            string.IsNullOrWhiteSpace(workItem.UserId) || string.IsNullOrWhiteSpace(workItem.OAuthBaseUri) ||
            string.IsNullOrWhiteSpace(workItem.ApiBaseUri) || string.IsNullOrWhiteSpace(workItem.SecretReference))
            throw new ESignProviderException("CONFIGURATION_REQUIRED", "DocuSign configuration is incomplete for this tenant.", false);
        if (!Uri.TryCreate(workItem.OAuthBaseUri, UriKind.Absolute, out _) || !Uri.TryCreate(workItem.ApiBaseUri, UriKind.Absolute, out _))
            throw new ESignProviderException("INVALID_CONFIGURATION", "DocuSign OAuth and API base URIs must be absolute.", false);
    }

    private static string GetFileExtension(string fileName, string? contentType)
    {
        var extension = Path.GetExtension(fileName).TrimStart('.');
        if (!string.IsNullOrWhiteSpace(extension))
            return extension;
        return contentType?.ToLowerInvariant() switch
        {
            "application/pdf" => "pdf",
            "application/msword" => "doc",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => "docx",
            _ => "pdf"
        };
    }

    private static string Base64UrlEncode(byte[] value)
        => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
