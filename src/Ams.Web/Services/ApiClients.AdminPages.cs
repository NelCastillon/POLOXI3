using System.Net.Http.Json;
using Ams.Application.Common.Dtos;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    // -- Admin Pages: Departments -------------------------------
    public Task<IReadOnlyList<DepartmentDto>?> GetDepartmentsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<DepartmentDto>>($"api/admin/departments?tenantId={tenantId}", cancellationToken);

    public Task<DepartmentDto?> GetDepartmentByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<DepartmentDto>($"api/admin/departments/{id}", cancellationToken);

    public async Task<Guid> CreateDepartmentAsync(DepartmentDto department, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/admin/departments?tenantId={tenantId}", department, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
        return result;
    }

    public async Task UpdateDepartmentAsync(Guid id, DepartmentDto department, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/admin/departments/{id}", department, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteDepartmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/admin/departments/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Admin Pages: Teams -------------------------------------
    public Task<IReadOnlyList<DepartmentTeamDto>?> GetTeamsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<DepartmentTeamDto>>($"api/admin/teams?tenantId={tenantId}", cancellationToken);

    public Task<DepartmentTeamDto?> GetTeamByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<DepartmentTeamDto>($"api/admin/teams/{id}", cancellationToken);

    public async Task<Guid> CreateTeamAsync(DepartmentTeamDto team, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/admin/teams?tenantId={tenantId}", team, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
        return result;
    }

    public async Task UpdateTeamAsync(Guid id, DepartmentTeamDto team, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/admin/teams/{id}", team, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTeamAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/admin/teams/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Admin Pages: Staff (Producers & CSRs) ------------------
    public Task<IReadOnlyList<ProducerStaffDto>?> GetStaffAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<ProducerStaffDto>>($"api/admin/staff?tenantId={tenantId}", cancellationToken);

    public Task<IReadOnlyList<ProducerStaffDto>?> GetExpiringLicensesAsync(Guid tenantId, int days = 30, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<ProducerStaffDto>>($"api/admin/staff/expiring-licenses?tenantId={tenantId}&days={days}", cancellationToken);

    public Task<ProducerStaffDto?> GetStaffByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<ProducerStaffDto>($"api/admin/staff/{id}", cancellationToken);

    public async Task<Guid> CreateStaffAsync(ProducerStaffDto staff, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/admin/staff?tenantId={tenantId}", staff, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
        return result;
    }

    public async Task UpdateStaffAsync(Guid id, ProducerStaffDto staff, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/admin/staff/{id}", staff, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteStaffAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/admin/staff/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
