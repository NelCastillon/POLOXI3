using System.Net;
using System.Text;
using System.Text.Json;
using Ams.Application.Features.PolicyEndorsements;
using Ams.Web.Services;
using Xunit;

namespace Ams.Application.Tests;

public sealed class PolicyEndorsementApiClientTests
{
    [Fact]
    public async Task TransactionalMethods_UseWorkflowRoutesAndPreserveConcurrencyTokens()
    {
        var requests = new List<CapturedRequest>();
        var id = Guid.NewGuid();
        var approvalId = Guid.NewGuid();
        var handler = new StubHandler(requests,
            Json(HttpStatusCode.Created, $"{{\"id\":\"{id}\"}}"),
            new HttpResponseMessage(HttpStatusCode.NoContent),
            new HttpResponseMessage(HttpStatusCode.NoContent),
            new HttpResponseMessage(HttpStatusCode.NoContent),
            Json(HttpStatusCode.Created, $"{{\"id\":\"{id}\"}}"));
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://ams.test/") });
        var rowVersion = new byte[] { 1, 2, 3 };

        await client.CreatePolicyEndorsementTransactionAsync(new CreatePolicyEndorsementTransactionRequest());
        await client.SavePolicyEndorsementDraftAsync(id, new SavePolicyEndorsementDraftRequest { RowVersion = rowVersion });
        await client.TransitionPolicyEndorsementAsync(id, new TransitionPolicyEndorsementRequest { RowVersion = rowVersion });
        await client.DecidePolicyEndorsementApprovalAsync(id, approvalId, new DecidePolicyEndorsementApprovalRequest { EndorsementRowVersion = rowVersion, ApprovalRowVersion = rowVersion });
        await client.ReversePolicyEndorsementAsync(id, new ReversePolicyEndorsementRequest { RowVersion = rowVersion });

        Assert.Collection(requests,
            request => Assert.Equal("api/policy-endorsements/transactions", request.Path),
            request => Assert.Equal($"api/policy-endorsements/{id}/draft", request.Path),
            request => Assert.Equal($"api/policy-endorsements/{id}/transitions", request.Path),
            request => Assert.Equal($"api/policy-endorsements/{id}/approvals/{approvalId}/decision", request.Path),
            request => Assert.Equal($"api/policy-endorsements/{id}/reversal", request.Path));
        Assert.Contains(Convert.ToBase64String(rowVersion), requests[1].Body);
        Assert.Contains(Convert.ToBase64String(rowVersion), requests[2].Body);
        Assert.Contains(Convert.ToBase64String(rowVersion), requests[3].Body);
        Assert.Contains(Convert.ToBase64String(rowVersion), requests[4].Body);
    }

    [Fact]
    public async Task Workspace_UsesPolicyScopedRoute()
    {
        var requests = new List<CapturedRequest>();
        var handler = new StubHandler(requests, Json(HttpStatusCode.OK, "{}"));
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://ams.test/") });
        var tenantId = Guid.NewGuid();
        var policyId = Guid.NewGuid();

        await client.GetPolicyEndorsementWorkspaceAsync(tenantId, policyId);

        Assert.Equal($"api/policy-endorsements/policies/{policyId}/workspace?tenantId={tenantId}", Assert.Single(requests).Path);
    }

    private static HttpResponseMessage Json(HttpStatusCode statusCode, string json)
        => new(statusCode) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    private sealed record CapturedRequest(string Path, string Body);

    private sealed class StubHandler(List<CapturedRequest> requests, params HttpResponseMessage[] responses) : HttpMessageHandler
    {
        private int _index;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            requests.Add(new CapturedRequest(request.RequestUri!.PathAndQuery.TrimStart('/'), request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));
            return responses[_index++];
        }
    }
}
