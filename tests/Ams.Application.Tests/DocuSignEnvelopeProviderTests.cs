using Ams.Application.Abstractions.Services;
using Ams.Application.Features.Documents;
using Ams.Infrastructure.Services;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Xunit;

namespace Ams.Application.Tests;

public sealed class DocuSignEnvelopeProviderTests
{
    [Fact]
    public async Task SendAsync_AuthenticatesAndCreatesEnvelope()
    {
        var requests = new List<CapturedRequest>();
        using var handler = new StubHandler(requests,
            Json(HttpStatusCode.OK, """{"access_token":"access-token"}"""),
            Json(HttpStatusCode.Created, """{"envelopeId":"envelope-123","status":"sent"}"""));
        using var client = new HttpClient(handler);
        var provider = new DocuSignEnvelopeProvider(client);
        var secretName = SetPrivateKeyEnvironmentVariable();

        try
        {
            var result = await provider.SendAsync(CreateWorkItem(secretName), new MemoryStream(Encoding.UTF8.GetBytes("test document")));

            Assert.Equal("envelope-123", result.ProviderEnvelopeId);
            Assert.Equal("sent", result.ProviderStatus);
            Assert.Equal("1", result.ProviderRecipientId);
            Assert.Equal(2, requests.Count);
            Assert.Equal("https://account-d.docusign.com/oauth/token", requests[0].Uri.ToString());
            Assert.Contains("urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Ajwt-bearer", requests[0].Content);
            Assert.Equal("https://demo.docusign.net/restapi/v2.1/accounts/account-1/envelopes", requests[1].Uri.ToString());
            Assert.Equal("Bearer", requests[1].AuthorizationScheme);
            Assert.Equal("access-token", requests[1].AuthorizationParameter);
            Assert.Equal("request-key", requests[1].IdempotencyKey);

            using var envelope = JsonDocument.Parse(requests[1].Content);
            Assert.Equal("signer@example.com", envelope.RootElement.GetProperty("recipients").GetProperty("signers")[0].GetProperty("email").GetString());
            Assert.Equal("1", envelope.RootElement.GetProperty("recipients").GetProperty("signers")[0].GetProperty("tabs").GetProperty("signHereTabs")[0].GetProperty("documentId").GetString());
        }
        finally
        {
            Environment.SetEnvironmentVariable(secretName, null);
        }
    }

    [Fact]
    public async Task SendAsync_WhenDocuSignIsRateLimited_ThrowsRetryableProviderException()
    {
        var responses = new[]
        {
            Json(HttpStatusCode.OK, """{"access_token":"access-token"}"""),
            Json(HttpStatusCode.TooManyRequests, """{"errorCode":"HOURLY_APIINVOCATION_LIMIT_EXCEEDED","message":"Try again later."}""", TimeSpan.FromMinutes(2))
        };
        using var handler = new StubHandler([], responses);
        using var client = new HttpClient(handler);
        var provider = new DocuSignEnvelopeProvider(client);
        var secretName = SetPrivateKeyEnvironmentVariable();

        try
        {
            var exception = await Assert.ThrowsAsync<ESignProviderException>(() =>
                provider.SendAsync(CreateWorkItem(secretName), new MemoryStream([1, 2, 3])));

            Assert.Equal("HOURLY_APIINVOCATION_LIMIT_EXCEEDED", exception.ErrorCode);
            Assert.True(exception.IsRetryable);
            Assert.NotNull(exception.RetryAtUtc);
        }
        finally
        {
            Environment.SetEnvironmentVariable(secretName, null);
        }
    }

    private static ESignDispatchWorkItem CreateWorkItem(string secretName) => new(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "policy.pdf", "documents/policy.pdf", "application/pdf",
        "Test Signer", "signer@example.com", "Please sign", "request-key", 1, 5, "account-1", "integration-key", "user-1",
        "https://account-d.docusign.com", "https://demo.docusign.net/restapi", secretName);

    private static string SetPrivateKeyEnvironmentVariable()
    {
        using var rsa = RSA.Create(2048);
        var name = $"AMS_TEST_DOCUSIGN_KEY_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(name, rsa.ExportRSAPrivateKeyPem());
        return name;
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string content, TimeSpan? retryAfter = null)
    {
        var response = new HttpResponseMessage(statusCode) { Content = new StringContent(content, Encoding.UTF8, "application/json") };
        if (retryAfter is not null)
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(retryAfter.Value);
        return response;
    }

    private sealed record CapturedRequest(Uri Uri, string Content, string? AuthorizationScheme, string? AuthorizationParameter, string? IdempotencyKey);

    private sealed class StubHandler(List<CapturedRequest> requests, params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _index;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add(new CapturedRequest(
                request.RequestUri!,
                request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken),
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues("X-DocuSign-Idempotency-Key", out var values) ? values.Single() : null));
            return responses[_index++];
        }
    }
}
