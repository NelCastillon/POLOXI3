using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.PremiumFinance;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PremiumFinanceWorkbenchDto?> GetPremiumFinanceWorkbenchAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PremiumFinanceWorkbenchDto>($"api/premium-finance/workbench?tenantId={tenantId}", cancellationToken);

    public Task<PremiumFinanceDetailDto?> GetPremiumFinanceDetailAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PremiumFinanceDetailDto>($"api/premium-finance/requests/{requestId}?tenantId={tenantId}", cancellationToken);

    public Task<PremiumFinanceSourceDto?> GetPremiumFinanceSourceAsync(Guid tenantId, string sourceTypeCode, Guid sourceId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PremiumFinanceSourceDto>($"api/premium-finance/sources/{Uri.EscapeDataString(sourceTypeCode)}/{sourceId}?tenantId={tenantId}", cancellationToken);

    public async Task<Guid> CreatePremiumFinanceRequestAsync(CreatePremiumFinanceRequest request, CancellationToken cancellationToken = default)
        => await PostForIdAsync("api/premium-finance/requests", request, cancellationToken);

    public async Task UpdatePremiumFinanceRequestAsync(Guid requestId, UpdatePremiumFinanceRequest request, CancellationToken cancellationToken = default)
        => await SendAsync(HttpMethod.Put, $"api/premium-finance/requests/{requestId}", request, cancellationToken);

    public async Task UpdatePremiumFinanceStatusAsync(Guid requestId, UpdatePremiumFinanceStatusRequest request, CancellationToken cancellationToken = default)
        => await SendAsync(HttpMethod.Patch, $"api/premium-finance/requests/{requestId}/status", request, cancellationToken);

    public async Task<Guid> AddPremiumFinanceQuoteOptionAsync(AddPremiumFinanceQuoteOptionRequest request, CancellationToken cancellationToken = default)
        => await PostForIdAsync("api/premium-finance/quote-options", request, cancellationToken);

    public async Task SelectPremiumFinanceQuoteOptionAsync(Guid requestId, SelectPremiumFinanceQuoteOptionRequest request, CancellationToken cancellationToken = default)
        => await SendAsync(HttpMethod.Post, $"api/premium-finance/requests/{requestId}/select-option", request, cancellationToken);

    public async Task<Guid> SubmitPremiumFinanceApplicationAsync(SubmitPremiumFinanceApplicationRequest request, CancellationToken cancellationToken = default)
        => await PostForIdAsync("api/premium-finance/applications", request, cancellationToken);

    public async Task UpdatePremiumFinanceAgreementAsync(UpdatePremiumFinanceAgreementRequest request, CancellationToken cancellationToken = default)
        => await SendAsync(HttpMethod.Patch, $"api/premium-finance/agreements/{request.FinanceAgreementId}", request, cancellationToken);

    public async Task ReplacePremiumFinancePaymentScheduleAsync(ReplacePremiumFinancePaymentScheduleRequest request, CancellationToken cancellationToken = default)
        => await SendAsync(HttpMethod.Put, $"api/premium-finance/agreements/{request.FinanceAgreementId}/payment-schedule", request, cancellationToken);

    public async Task<Guid> AddPremiumFinanceActivityAsync(AddPremiumFinanceActivityRequest request, CancellationToken cancellationToken = default)
        => await PostForIdAsync("api/premium-finance/activities", request, cancellationToken);

    public async Task<Guid> LinkPremiumFinanceDocumentAsync(LinkPremiumFinanceDocumentRequest request, CancellationToken cancellationToken = default)
        => await PostForIdAsync("api/premium-finance/documents", request, cancellationToken);

    public async Task<Guid> UpsertPremiumFinanceProviderAsync(UpsertPremiumFinanceProviderRequest request, CancellationToken cancellationToken = default)
        => await PostForIdAsync("api/premium-finance/providers", request, cancellationToken);

    public async Task CancelPremiumFinanceRequestAsync(CancelPremiumFinanceRequest request, CancellationToken cancellationToken = default)
        => await SendAsync(HttpMethod.Post, $"api/premium-finance/requests/{request.PremiumFinanceRequestId}/cancel", request, cancellationToken);

    private async Task<Guid> PostForIdAsync<T>(string url, T request, CancellationToken cancellationToken)
    {
        var response = await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    private async Task SendAsync<T>(HttpMethod method, string url, T request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(method, url) { Content = JsonContent.Create(request) };
        using var response = await _httpClient.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
