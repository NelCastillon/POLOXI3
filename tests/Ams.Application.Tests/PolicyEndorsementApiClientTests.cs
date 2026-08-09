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

    [Fact]
    public async Task ApprovalAndRfiMethods_UseTenantWorkflowRoutesAndConcurrencyTokens()
    {
        var requests = new List<CapturedRequest>();
        var tenantId = Guid.NewGuid();
        var endorsementId = Guid.NewGuid();
        var informationRequestId = Guid.NewGuid();
        var rowVersion = new byte[] { 7, 8, 9 };
        var handler = new StubHandler(requests,
            Json(HttpStatusCode.OK, "[]"),
            Json(HttpStatusCode.OK, $"{{\"id\":\"{informationRequestId}\"}}"),
            new HttpResponseMessage(HttpStatusCode.NoContent),
            new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://ams.test/") });

        await client.GetPolicyEndorsementApprovalInboxAsync(tenantId);
        await client.RequestPolicyEndorsementInformationAsync(endorsementId, new RequestPolicyEndorsementInformationRequest { TenantId = tenantId, RequestDetails = "Provide the signed request.", EndorsementRowVersion = rowVersion });
        await client.RespondToPolicyEndorsementInformationRequestAsync(endorsementId, informationRequestId, new RespondPolicyEndorsementInformationRequest { TenantId = tenantId, ResponseDetails = "Signed request attached.", EndorsementRowVersion = rowVersion, InformationRequestRowVersion = rowVersion });
        await client.ResubmitPolicyEndorsementInformationRequestAsync(endorsementId, informationRequestId, new ResubmitPolicyEndorsementInformationRequest { TenantId = tenantId, Notes = "Ready for review.", EndorsementRowVersion = rowVersion, InformationRequestRowVersion = rowVersion });

        Assert.Collection(requests,
            request => Assert.Equal($"api/policy-endorsements/approval-inbox?tenantId={tenantId}", request.Path),
            request => Assert.Equal($"api/policy-endorsements/{endorsementId}/information-requests", request.Path),
            request => Assert.Equal($"api/policy-endorsements/{endorsementId}/information-requests/{informationRequestId}/response", request.Path),
            request => Assert.Equal($"api/policy-endorsements/{endorsementId}/information-requests/{informationRequestId}/resubmission", request.Path));
        Assert.All(requests.Skip(1), request => Assert.Contains(Convert.ToBase64String(rowVersion), request.Body));
        Assert.Equal(2, CountOccurrences(requests[2].Body, Convert.ToBase64String(rowVersion)));
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    [Fact]
    public async Task CatalogMethods_UseTenantAndPolicyScopedRoutesAndPreserveProfileConcurrencyToken()
    {
        var requests = new List<CapturedRequest>();
        var tenantId = Guid.NewGuid();
        var policyId = Guid.NewGuid();
        var endorsementTypeId = Guid.NewGuid();
        var typeCode = "Additional Insured/Add";
        var rowVersion = new byte[] { 4, 5, 6 };
        var handler = new StubHandler(requests,
            Json(HttpStatusCode.OK, "{}"),
            Json(HttpStatusCode.OK, "{}"),
            Json(HttpStatusCode.OK, "[]"),
            Json(HttpStatusCode.OK, "{}"),
            new HttpResponseMessage(HttpStatusCode.NoContent),
            new HttpResponseMessage(HttpStatusCode.NoContent));
        var client = new ApiClient(new HttpClient(handler) { BaseAddress = new Uri("https://ams.test/") });

        await client.GetPolicyEndorsementCatalogAsync(tenantId, "Commercial Auto");
        await client.GetPolicyEndorsementTypeCatalogAsync(tenantId, typeCode);
        await client.GetAvailablePolicyEndorsementTypesAsync(tenantId, policyId);
        await client.GetPolicyEndorsementRequirementsAsync(tenantId, policyId, typeCode);
        await client.UpdatePolicyEndorsementTypeProfileAsync(endorsementTypeId, new UpdatePolicyEndorsementTypeProfileRequest { TenantId = tenantId, RowVersion = rowVersion });
        await client.ReplacePolicyEndorsementTypeConfigurationAsync(endorsementTypeId, new ReplacePolicyEndorsementTypeConfigurationRequest { TenantId = tenantId });

        Assert.Collection(requests,
            request => Assert.Equal($"api/policy-endorsements/catalog?tenantId={tenantId}&lineOfBusinessCode=Commercial%20Auto", request.Path),
            request => Assert.Equal($"api/policy-endorsements/catalog/Additional%20Insured%2FAdd?tenantId={tenantId}", request.Path),
            request => Assert.Equal($"api/policy-endorsements/policies/{policyId}/available-types?tenantId={tenantId}", request.Path),
            request => Assert.Equal($"api/policy-endorsements/types/Additional%20Insured%2FAdd/requirements?tenantId={tenantId}&policyId={policyId}", request.Path),
            request => Assert.Equal($"api/policy-endorsements/catalog/{endorsementTypeId}/profile", request.Path),
            request => Assert.Equal($"api/policy-endorsements/catalog/{endorsementTypeId}/configuration", request.Path));
        Assert.Contains(Convert.ToBase64String(rowVersion), requests[4].Body);
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
