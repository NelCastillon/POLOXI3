using System.Net.Http.Json;
using Ams.Application.Common.Dtos;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<OperationsWorkbenchDto?> GetOperationsWorkbenchAsync(Guid tenantId, Guid? userId = null, bool myItemsOnly = false, string? assigneeFilter = null, CancellationToken ct = default)
    {
        var url = $"api/workbench/operations?tenantId={tenantId}&myItemsOnly={myItemsOnly}";
        if (userId.HasValue) url += $"&userId={userId.Value}";
        if (!string.IsNullOrWhiteSpace(assigneeFilter)) url += $"&assigneeFilter={Uri.EscapeDataString(assigneeFilter)}";
        return _httpClient.GetFromJsonAsync<OperationsWorkbenchDto>(url, ct);
    }

    public async Task RetryOperationsWorkbenchItemAsync(Guid tenantId, Guid itemId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/workbench/operations/items/{itemId}/retry?tenantId={tenantId}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task SkipOperationsWorkbenchStepAsync(Guid tenantId, Guid itemId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/workbench/operations/items/{itemId}/skip?tenantId={tenantId}", null, ct);
        response.EnsureSuccessStatusCode();
    }
}
