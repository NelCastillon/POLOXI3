using System.Net.Http.Json;
using Ams.Application.Common.Dtos;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<MarketingWorkbenchDto?> GetMarketingWorkbenchAsync(Guid tenantId, Guid? userId = null, bool teamScope = false, string? branchId = null, string? teamId = null, CancellationToken ct = default)
    {
        var url = $"api/workbench/marketing?tenantId={tenantId}&teamScope={teamScope}";
        if (userId.HasValue) url += $"&userId={userId.Value}";
        if (!string.IsNullOrWhiteSpace(branchId)) url += $"&branchId={Uri.EscapeDataString(branchId)}";
        if (!string.IsNullOrWhiteSpace(teamId)) url += $"&teamId={Uri.EscapeDataString(teamId)}";
        return _httpClient.GetFromJsonAsync<MarketingWorkbenchDto>(url, ct);
    }

    public async Task ApproveMarketingWorkbenchContentAsync(Guid tenantId, Guid itemId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/workbench/marketing/content/{itemId}/approve?tenantId={tenantId}", null, ct);
        response.EnsureSuccessStatusCode();
    }
}
