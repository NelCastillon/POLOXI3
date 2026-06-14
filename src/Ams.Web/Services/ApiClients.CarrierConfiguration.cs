using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.CarrierConfig;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    public Task<PagedResult<MgaWholesalerDto>?> SearchMgaWholesalersAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<MgaWholesalerDto>>($"api/carriers/mgas?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);
    public async Task<Guid> CreateMgaWholesalerAsync(CreateMgaWholesalerRequest request, CancellationToken ct = default) { var r = await _httpClient.PostAsJsonAsync("api/carriers/mgas", request, ct); r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id; }
    public async Task UpdateMgaWholesalerAsync(Guid id, UpdateMgaWholesalerRequest request, CancellationToken ct = default) { (await _httpClient.PutAsJsonAsync($"api/carriers/mgas/{id}", request, ct)).EnsureSuccessStatusCode(); }
    public async Task DeleteMgaWholesalerAsync(Guid id, CancellationToken ct = default) { (await _httpClient.DeleteAsync($"api/carriers/mgas/{id}", ct)).EnsureSuccessStatusCode(); }

    public Task<PagedResult<CarrierContactDto>?> SearchCarrierContactsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CarrierContactDto>>($"api/carriers/contacts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);
    public async Task<Guid> CreateCarrierContactAsync(CreateCarrierContactRequest request, CancellationToken ct = default) { var r = await _httpClient.PostAsJsonAsync("api/carriers/contacts", request, ct); r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id; }
    public async Task UpdateCarrierContactAsync(Guid id, UpdateCarrierContactRequest request, CancellationToken ct = default) { (await _httpClient.PutAsJsonAsync($"api/carriers/contacts/{id}", request, ct)).EnsureSuccessStatusCode(); }
    public async Task DeleteCarrierContactAsync(Guid id, CancellationToken ct = default) { (await _httpClient.DeleteAsync($"api/carriers/contacts/{id}", ct)).EnsureSuccessStatusCode(); }

    public Task<PagedResult<CarrierAppointmentDto>?> SearchCarrierAppointmentsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CarrierAppointmentDto>>($"api/carriers/appointments?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);
    public async Task<Guid> CreateCarrierAppointmentAsync(CreateCarrierAppointmentRequest request, CancellationToken ct = default) { var r = await _httpClient.PostAsJsonAsync("api/carriers/appointments", request, ct); r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id; }
    public async Task UpdateCarrierAppointmentAsync(Guid id, UpdateCarrierAppointmentRequest request, CancellationToken ct = default) { (await _httpClient.PutAsJsonAsync($"api/carriers/appointments/{id}", request, ct)).EnsureSuccessStatusCode(); }
    public async Task DeleteCarrierAppointmentAsync(Guid id, CancellationToken ct = default) { (await _httpClient.DeleteAsync($"api/carriers/appointments/{id}", ct)).EnsureSuccessStatusCode(); }

    public Task<PagedResult<CarrierPerformanceDto>?> SearchCarrierPerformanceAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CarrierPerformanceDto>>($"api/carriers/performance?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);
    public async Task<Guid> CreateCarrierPerformanceAsync(CreateCarrierPerformanceRequest request, CancellationToken ct = default) { var r = await _httpClient.PostAsJsonAsync("api/carriers/performance", request, ct); r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id; }
    public async Task UpdateCarrierPerformanceAsync(Guid id, UpdateCarrierPerformanceRequest request, CancellationToken ct = default) { (await _httpClient.PutAsJsonAsync($"api/carriers/performance/{id}", request, ct)).EnsureSuccessStatusCode(); }
    public async Task DeleteCarrierPerformanceAsync(Guid id, CancellationToken ct = default) { (await _httpClient.DeleteAsync($"api/carriers/performance/{id}", ct)).EnsureSuccessStatusCode(); }

    public Task<PagedResult<CarrierSettingDto>?> SearchCarrierSettingsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 100, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CarrierSettingDto>>($"api/carriers/settings?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);
    public async Task<Guid> CreateCarrierSettingAsync(CreateCarrierSettingRequest request, CancellationToken ct = default) { var r = await _httpClient.PostAsJsonAsync("api/carriers/settings", request, ct); r.EnsureSuccessStatusCode(); return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id; }
    public async Task UpdateCarrierSettingAsync(Guid id, UpdateCarrierSettingRequest request, CancellationToken ct = default) { (await _httpClient.PutAsJsonAsync($"api/carriers/settings/{id}", request, ct)).EnsureSuccessStatusCode(); }
    public async Task DeleteCarrierSettingAsync(Guid id, CancellationToken ct = default) { (await _httpClient.DeleteAsync($"api/carriers/settings/{id}", ct)).EnsureSuccessStatusCode(); }
}
