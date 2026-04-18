using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AccountNotes;
using Ams.Application.Features.Accounts;
using Ams.Application.Features.Compliance;
using Ams.Application.Features.Contacts;
using Ams.Application.Features.Documents;
using Ams.Application.Features.Engagements;
using Ams.Application.Features.Forecast;
using Ams.Application.Features.Audit;
using Ams.Application.Features.Governance;
using Ams.Application.Features.Iam;
using Ams.Application.Features.LeadActivities;
using Ams.Application.Features.Leads;
using Ams.Application.Features.Opportunities;
using Ams.Application.Features.Operations;
using Ams.Application.Features.PortalInvites;
using Ams.Application.Features.PricingRules;
using Ams.Application.Features.Quotes;
using Ams.Application.Features.Security;
using Ams.Application.Features.Sod;

namespace Ams.Web.Services;

public sealed class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // ── Dashboard ────────────────────────────────────────────
    public Task<DashboardKpiDto?> GetDashboardKpiAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<DashboardKpiDto>($"api/dashboard?tenantId={tenantId}", cancellationToken);

    // ── Platform Core ────────────────────────────────────────
    public Task<PagedResult<TenantDto>?> SearchTenantsAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<TenantDto>>($"api/tenants?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<BranchDto>?> SearchBranchesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<BranchDto>>($"api/branches?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    // ── IAM ──────────────────────────────────────────────────
    public Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<UserDto>($"api/users/{userId}", cancellationToken);

    public Task<UserProfileDto?> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<UserProfileDto>($"api/users/{userId}/profile", cancellationToken);

    public async Task UpdateUserProfileAsync(Guid userId, UpdateUserProfileRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/users/{userId}/profile", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<UserDto>?> SearchUsersAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<UserDto>>($"api/users?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/users", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateUserAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/users/{userId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetUserActiveAsync(Guid userId, bool isActive, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var url = isActive ? $"api/users/{userId}/activate" : $"api/users/{userId}/deactivate";
        var response = await _httpClient.PatchAsync($"{url}?modifiedByUserId={modifiedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task LockUserAsync(Guid userId, DateTime? lockoutEnd = null, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/users/{userId}/lock?lockoutEnd={lockoutEnd:O}&modifiedByUserId={modifiedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UnlockUserAsync(Guid userId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/users/{userId}/unlock?modifiedByUserId={modifiedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<IEnumerable<UserPermissionDto>?> GetUserDirectPermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IEnumerable<UserPermissionDto>>($"api/users/{userId}/permissions", cancellationToken);

    public async Task<Guid> GrantUserPermissionAsync(Guid userId, GrantUserPermissionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/users/{userId}/permissions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task RevokeUserPermissionAsync(Guid userId, Guid permissionId, Guid? revokedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/users/{userId}/permissions/{permissionId}?revokedByUserId={revokedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // Permission Overrides (tenant-scoped)
    public Task<PagedResult<UserPermissionDto>?> SearchPermissionOverridesAsync(Guid tenantId, Guid? userId = null, Guid? permissionId = null, bool? isGranted = null, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<UserPermissionDto>>($"api/iam/permission-overrides?tenantId={tenantId}&userId={userId}&permissionId={permissionId}&isGranted={isGranted}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> GrantPermissionOverrideAsync(GrantUserPermissionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/iam/permission-overrides", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdatePermissionOverrideAsync(Guid id, UpdateUserPermissionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/iam/permission-overrides/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RevokePermissionOverrideAsync(Guid id, Guid? revokedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/iam/permission-overrides/{id}?revokedByUserId={revokedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<List<UserPermissionScopeDto>?> GetPermissionScopesAsync(Guid overrideId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<UserPermissionScopeDto>>($"api/iam/permission-overrides/{overrideId}/scopes", cancellationToken);

    public async Task<Guid> AddPermissionScopeAsync(Guid overrideId, AddPermissionScopeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/iam/permission-overrides/{overrideId}/scopes", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task RemovePermissionScopeAsync(Guid overrideId, Guid scopeId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/iam/permission-overrides/{overrideId}/scopes/{scopeId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<List<PermissionConflictDto>?> ValidatePermissionConflictsAsync(Guid tenantId, Guid? userId = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<PermissionConflictDto>>($"api/iam/permission-overrides/conflicts?tenantId={tenantId}&userId={userId}", cancellationToken);

    public Task<List<PermissionScopePreviewDto>?> PreviewEffectiveScopeAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<PermissionScopePreviewDto>>($"api/iam/permission-overrides/effective-scope?tenantId={tenantId}&userId={userId}", cancellationToken);

    public Task<RoleDto?> GetRoleByIdAsync(Guid roleId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<RoleDto>($"api/roles/{roleId}", cancellationToken);

    public Task<PagedResult<RoleDto>?> SearchRolesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<RoleDto>>($"api/roles?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateRoleAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/roles", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateRoleAsync(Guid roleId, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/roles/{roleId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetRoleActiveAsync(Guid roleId, bool isActive, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var url = isActive ? $"api/roles/{roleId}/activate" : $"api/roles/{roleId}/deactivate";
        var response = await _httpClient.PatchAsync($"{url}?modifiedByUserId={modifiedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // Permissions
    public Task<PermissionDto?> GetPermissionByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PermissionDto>($"api/iam/permissions/{id}", cancellationToken);

    public Task<IEnumerable<RolePermissionDto>?> GetPermissionRolesAsync(Guid permissionId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IEnumerable<RolePermissionDto>>($"api/iam/permissions/{permissionId}/roles", cancellationToken);

    public Task<IEnumerable<UserPermissionDto>?> GetPermissionDirectUsersAsync(Guid permissionId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IEnumerable<UserPermissionDto>>($"api/iam/permissions/{permissionId}/direct-users", cancellationToken);

    public Task<PagedResult<PermissionDto>?> SearchPermissionsAsync(Guid tenantId, string? searchTerm = null, string? resourceCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PermissionDto>>($"api/iam/permissions?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&resourceCode={Uri.EscapeDataString(resourceCode ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreatePermissionAsync(CreatePermissionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/iam/permissions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task DeactivatePermissionAsync(Guid permissionId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/iam/permissions/{permissionId}/deactivate?modifiedByUserId={modifiedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // Role-Permission assignments
    public Task<IEnumerable<RolePermissionDto>?> GetRolePermissionsAsync(Guid roleId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IEnumerable<RolePermissionDto>>($"api/iam/roles/{roleId}/permissions", cancellationToken);

    public Task<RolePermissionMatrixDto?> GetRolePermissionMatrixAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<RolePermissionMatrixDto>($"api/iam/matrix?tenantId={tenantId}", cancellationToken);

    public async Task<Guid> AssignPermissionToRoleAsync(AssignRolePermissionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/iam/role-permissions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task RevokePermissionFromRoleAsync(RevokeRolePermissionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "api/iam/role-permissions") { Content = JsonContent.Create(request) }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // User-Role assignments
    public Task<PagedResult<UserRoleDto>?> SearchUserRolesAsync(Guid tenantId, Guid? userId = null, Guid? roleId = null, bool? isActive = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<UserRoleDto>>($"api/iam/user-roles?tenantId={tenantId}&userId={userId}&roleId={roleId}&isActive={isActive}", cancellationToken);

    public async Task<Guid> AssignUserRoleAsync(AssignUserRoleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/iam/user-roles", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task RevokeUserRoleAsync(RevokeUserRoleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Delete, "api/iam/user-roles") { Content = JsonContent.Create(request) }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<IEnumerable<EffectivePermissionDto>?> GetEffectivePermissionsAsync(Guid userId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IEnumerable<EffectivePermissionDto>>($"api/iam/users/{userId}/effective-permissions", cancellationToken);

    // User Scopes
    public Task<PagedResult<UserScopeDto>?> SearchUserScopesAsync(Guid tenantId, Guid? userId = null, string? scopeTypeCode = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<UserScopeDto>>($"api/iam/user-scopes?tenantId={tenantId}&userId={userId}&scopeTypeCode={Uri.EscapeDataString(scopeTypeCode ?? string.Empty)}", cancellationToken);

    public Task<IEnumerable<UserScopeDto>?> GetUserScopesAsync(Guid userId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IEnumerable<UserScopeDto>>($"api/iam/users/{userId}/scopes", cancellationToken);

    public async Task<Guid> AssignUserScopeAsync(AssignUserScopeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/iam/user-scopes", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task RevokeUserScopeAsync(Guid userScopeId, Guid? revokedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/iam/user-scopes/{userScopeId}?revokedByUserId={revokedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // Role Bundles
    public Task<PagedResult<RoleBundleDto>?> SearchRoleBundlesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<RoleBundleDto>>($"api/iam/role-bundles?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<RoleBundleDto?> GetRoleBundleByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<RoleBundleDto>($"api/iam/role-bundles/{id}", cancellationToken);

    public async Task<Guid> CreateRoleBundleAsync(CreateRoleBundleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/iam/role-bundles", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateRoleBundleAsync(Guid id, UpdateRoleBundleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/iam/role-bundles/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetRoleBundleActiveAsync(Guid id, bool activate, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var action = activate ? "activate" : "deactivate";
        var response = await _httpClient.PatchAsync($"api/iam/role-bundles/{id}/{action}?modifiedByUserId={modifiedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<IEnumerable<BundleRoleDto>?> GetBundleRolesAsync(Guid bundleId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IEnumerable<BundleRoleDto>>($"api/iam/role-bundles/{bundleId}/roles", cancellationToken);

    public async Task SetBundleRolesAsync(Guid bundleId, SetBundleRolesRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/iam/role-bundles/{bundleId}/roles", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // Security Policies
    public Task<PagedResult<SecurityPolicyDto>?> SearchSecurityPoliciesAsync(Guid tenantId, string? searchTerm = null, string? resourceCode = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<SecurityPolicyDto>>($"api/iam/security-policies?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&resourceCode={Uri.EscapeDataString(resourceCode ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateSecurityPolicyAsync(CreateSecurityPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/iam/security-policies", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task DeactivateSecurityPolicyAsync(Guid policyId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/iam/security-policies/{policyId}/deactivate?modifiedByUserId={modifiedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // ── CRM ──────────────────────────────────────────────────
    public async Task<Guid> CreateLeadAsync(CreateLeadRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/leads", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<LeadDto>?> SearchLeadsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<LeadDto>>($"api/leads?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<OpportunityDto>?> SearchOpportunitiesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<OpportunityDto>>($"api/opportunities?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    // ── Client & Account ─────────────────────────────────────
    public async Task<Guid> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/accounts", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<AccountDto>?> SearchAccountsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AccountDto>>($"api/accounts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<IReadOnlyList<ContactDto>?> GetAccountContactsAsync(Guid accountId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<ContactDto>>($"api/accounts/{accountId}/contacts", cancellationToken);

    public async Task<Guid> CreateContactAsync(CreateContactRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/contacts", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<ContactDto>?> SearchContactsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ContactDto>>($"api/contacts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<ContactDto>?> GetContactsByAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ContactDto>>($"api/contacts/by-account/{accountId}", cancellationToken);

    public async Task<Guid> CreateAccountNoteAsync(CreateAccountNoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/client/account-notes", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<AccountNoteDto>?> SearchAccountNotesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AccountNoteDto>>($"api/client/account-notes?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<AccountSegmentDto>?> SearchAccountSegmentsAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AccountSegmentDto>>($"api/client/segments?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreatePortalInviteAsync(CreatePortalInviteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/client/portal-invites", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<PortalInviteDto>?> SearchPortalInvitesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PortalInviteDto>>($"api/client/portal-invites?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<AccountOwnerHistoryDto>?> SearchAccountOwnershipAsync(Guid tenantId, Guid? accountId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AccountOwnerHistoryDto>>($"api/client/account-ownership?tenantId={tenantId}&accountId={accountId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    // ── Operations ───────────────────────────────────────────
    public Task<PagedResult<AgreementDto>?> SearchAgreementsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AgreementDto>>($"api/agreements?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<EngagementDto>?> SearchEngagementsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<EngagementDto>>($"api/engagements?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<EngagementTaskDto>?> SearchEngagementTasksAsync(Guid tenantId, Guid? engagementId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<EngagementTaskDto>>($"api/engagements/tasks?tenantId={tenantId}&engagementId={engagementId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<EngagementMilestoneDto>?> SearchEngagementMilestonesAsync(Guid tenantId, Guid? engagementId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<EngagementMilestoneDto>>($"api/ops/milestones?tenantId={tenantId}&engagementId={engagementId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateEngagementMilestoneAsync(CreateEngagementMilestoneRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ops/milestones", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<ServiceIssueDto>?> SearchServiceIssuesAsync(Guid tenantId, Guid? engagementId = null, Guid? accountId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ServiceIssueDto>>($"api/ops/issues?tenantId={tenantId}&engagementId={engagementId}&accountId={accountId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateServiceIssueAsync(CreateServiceIssueRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ops/issues", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<AgreementAmendmentDto>?> SearchAgreementAmendmentsAsync(Guid tenantId, Guid? agreementId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AgreementAmendmentDto>>($"api/ops/amendments?tenantId={tenantId}&agreementId={agreementId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateAgreementAmendmentAsync(CreateAgreementAmendmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ops/amendments", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<AgreementRenewalDto>?> SearchAgreementRenewalsAsync(Guid tenantId, Guid? agreementId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AgreementRenewalDto>>($"api/ops/renewals?tenantId={tenantId}&agreementId={agreementId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateAgreementRenewalAsync(CreateAgreementRenewalRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ops/renewals", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<ServiceRequestDto>?> SearchServiceRequestsAsync(Guid tenantId, Guid? accountId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ServiceRequestDto>>($"api/ops/service-requests?tenantId={tenantId}&accountId={accountId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateServiceRequestAsync(CreateServiceRequestRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ops/service-requests", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<OperationalActivityLogDto>?> SearchOperationalActivitiesAsync(Guid tenantId, Guid? accountId = null, Guid? engagementId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<OperationalActivityLogDto>>($"api/ops/activities?tenantId={tenantId}&accountId={accountId}&engagementId={engagementId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateOperationalActivityAsync(CreateOperationalActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ops/activities", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    // ── Billing ──────────────────────────────────────────────
    public Task<PagedResult<InvoiceDto>?> SearchInvoicesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<InvoiceDto>>($"api/invoices?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<TimeEntryDto>?> SearchTimeEntriesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<TimeEntryDto>>($"api/timeentries?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<ExpenseEntryDto>?> SearchExpensesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ExpenseEntryDto>>($"api/expenses?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<PaymentDto>?> SearchPaymentsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PaymentDto>>($"api/payments?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    // ── Billing extended engine ───────────────────────────────
    public Task<PagedResult<RateCardDto>?> SearchRateCardsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<RateCardDto>>($"api/billing/rate-cards?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<RateCardLineDto>?> SearchRateCardLinesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<RateCardLineDto>>($"api/billing/rate-card-lines?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<PrebillBatchDto>?> SearchPrebillBatchesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PrebillBatchDto>>($"api/billing/prebill?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<InvoiceLineDto>?> SearchInvoiceLinesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<InvoiceLineDto>>($"api/billing/invoice-lines?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<RecurringBillingScheduleDto>?> SearchRecurringBillingSchedulesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<RecurringBillingScheduleDto>>($"api/billing/recurring?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<MilestoneBillingLinkDto>?> SearchMilestoneBillingLinksAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<MilestoneBillingLinkDto>>($"api/billing/milestone-billing?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<RetainerAccountDto>?> SearchRetainerAccountsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<RetainerAccountDto>>($"api/billing/retainers?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<RetainerDrawdownDto>?> SearchRetainerDrawdownsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<RetainerDrawdownDto>>($"api/billing/retainer-drawdowns?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<BillingAdjustmentDto>?> SearchBillingAdjustmentsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<BillingAdjustmentDto>>($"api/billing/adjustments?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<ArAgingSnapshotDto>?> SearchArAgingSnapshotsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ArAgingSnapshotDto>>($"api/billing/ar-aging?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<DelinquencyFlagDto>?> SearchDelinquencyFlagsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DelinquencyFlagDto>>($"api/billing/delinquency?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CollectionsNoteDto>?> SearchCollectionsNotesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CollectionsNoteDto>>($"api/billing/collections?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    // ── Finance ──────────────────────────────────────────────
    public Task<PagedResult<GLAccountDto>?> SearchGLAccountsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<GLAccountDto>>($"api/finance/glaccounts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<JournalEntryDto>?> SearchJournalEntriesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<JournalEntryDto>>($"api/finance/journalentries?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<VendorDto>?> SearchVendorsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<VendorDto>>($"api/finance/vendors?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<ApInvoiceDto>?> SearchApInvoicesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ApInvoiceDto>>($"api/finance/ap-invoices?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<ApInvoiceLineDto>?> SearchApInvoiceLinesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ApInvoiceLineDto>>($"api/finance/ap-invoice-lines?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<ApPaymentDto>?> SearchApPaymentsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ApPaymentDto>>($"api/finance/ap-payments?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<AccountingPeriodDto>?> SearchAccountingPeriodsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AccountingPeriodDto>>($"api/finance/accounting-periods?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<PeriodCloseEntryDto>?> SearchPeriodCloseEntriesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PeriodCloseEntryDto>>($"api/finance/period-close?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<DeferredRevenueScheduleDto>?> SearchDeferredRevenueSchedulesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DeferredRevenueScheduleDto>>($"api/finance/deferred-revenue?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<DeferredRevenueRecognitionDto>?> SearchDeferredRevenueRecognitionsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DeferredRevenueRecognitionDto>>($"api/finance/deferred-revenue-recognition?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<BadDebtEntryDto>?> SearchBadDebtEntriesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<BadDebtEntryDto>>($"api/finance/bad-debt?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CashReceiptEntryDto>?> SearchCashReceiptEntriesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CashReceiptEntryDto>>($"api/finance/cash-receipts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<TrialBalanceSnapshotDto>?> SearchTrialBalanceSnapshotsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<TrialBalanceSnapshotDto>>($"api/finance/trial-balance?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<BankReconciliationDto>?> SearchBankReconciliationsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<BankReconciliationDto>>($"api/finance/bank-reconciliation?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<IReadOnlyList<JournalEntryLineDto>?> GetJournalEntryLinesAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<JournalEntryLineDto>>($"api/finance/journal-entry-lines?journalEntryId={journalEntryId}", cancellationToken);

    // ── Commission ───────────────────────────────────────────
    public Task<PagedResult<CommissionPlanDto>?> SearchCommissionPlansAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionPlanDto>>($"api/commissionplans?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CommissionPayeeDto>?> SearchCommissionPayeesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionPayeeDto>>($"api/commissions/payees?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CommissionTransactionDto>?> SearchCommissionTransactionsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionTransactionDto>>($"api/commissions/transactions?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CommissionPayoutDto>?> SearchCommissionPayoutsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionPayoutDto>>($"api/commissions/payouts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CommissionPlanVersionDto>?> SearchCommissionPlanVersionsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionPlanVersionDto>>($"api/commissions/plan-versions?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CommissionSplitRuleDto>?> SearchCommissionSplitRulesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionSplitRuleDto>>($"api/commissions/split-rules?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CommissionCalculationResultDto>?> SearchCommissionCalculationResultsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionCalculationResultDto>>($"api/commissions/calculations?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CommissionClawbackDto>?> SearchCommissionClawbacksAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionClawbackDto>>($"api/commissions/clawbacks?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CommissionPayoutBatchDto>?> SearchCommissionPayoutBatchesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionPayoutBatchDto>>($"api/commissions/payout-batches?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CommissionDisputeDto>?> SearchCommissionDisputesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionDisputeDto>>($"api/commissions/disputes?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CommissionPayoutStatementDto>?> SearchCommissionPayoutStatementsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionPayoutStatementDto>>($"api/commissions/payout-statements?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CommissionAccrualEntryDto>?> SearchCommissionAccrualEntriesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionAccrualEntryDto>>($"api/commissions/accruals?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    // ── Workflow & Approval ──────────────────────────────────
    public Task<PagedResult<WorkflowInstanceDto>?> SearchWorkflowAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<WorkflowInstanceDto>>($"api/workflow?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    // ── Documents ────────────────────────────────────────────
    public Task<PagedResult<DocumentDto>?> SearchDocumentsAsync(Guid tenantId, string? categoryCode = null, string? entityName = null, Guid? entityId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DocumentDto>>($"api/documents?tenantId={tenantId}&categoryCode={Uri.EscapeDataString(categoryCode ?? string.Empty)}&entityName={Uri.EscapeDataString(entityName ?? string.Empty)}&entityId={entityId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<DocumentDto?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<DocumentDto>($"api/documents/{documentId}", cancellationToken);

    public async Task<Guid> CreateDocumentAsync(CreateDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/documents", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateDocumentMetadataAsync(UpdateDocumentMetadataRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync("api/documents/metadata", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ArchiveDocumentAsync(Guid documentId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/documents/{documentId}/archive?modifiedByUserId={modifiedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // Document versions
    public Task<IReadOnlyList<DocumentVersionDto>?> GetDocumentVersionsAsync(Guid documentId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<DocumentVersionDto>>($"api/documents/{documentId}/versions", cancellationToken);

    public async Task<Guid> CreateDocumentVersionAsync(CreateDocumentVersionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/documents/versions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    // Document share links
    public Task<IReadOnlyList<DocumentShareLinkDto>?> GetDocumentShareLinksAsync(Guid documentId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<DocumentShareLinkDto>>($"api/documents/{documentId}/share-links", cancellationToken);

    public async Task<Guid> CreateDocumentShareLinkAsync(CreateDocumentShareLinkRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/documents/share-links", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task RevokeDocumentShareLinkAsync(Guid shareLinkId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/documents/share-links/{shareLinkId}/revoke", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // Document access log
    public Task<IReadOnlyList<DocumentAccessLogDto>?> GetDocumentAccessLogAsync(Guid documentId, int top = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<DocumentAccessLogDto>>($"api/documents/{documentId}/access-log?top={top}", cancellationToken);

    public Task<IReadOnlyList<DocumentDto>?> GetDocumentsByEntityAsync(Guid tenantId, string entityName, Guid entityId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<DocumentDto>>($"api/documents/by-entity?tenantId={tenantId}&entityName={Uri.EscapeDataString(entityName)}&entityId={entityId}", cancellationToken);

    public async Task RenameDocumentAsync(RenameDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync("api/documents/rename", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteDocumentAsync(Guid documentId, Guid? deletedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/documents/{documentId}?deletedByUserId={deletedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // ── Audit ────────────────────────────────────────────────
    public Task<PagedResult<AuditLogDto>?> SearchAuditAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AuditLogDto>>($"api/audit?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<AuditLogDto>?> GetEntityHistoryAsync(Guid tenantId, string entityName, Guid entityId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AuditLogDto>>($"api/audit/entity-history?tenantId={tenantId}&entityName={Uri.EscapeDataString(entityName)}&entityId={entityId}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<PagedResult<FieldChangeLogDto>?> SearchFieldChangesAsync(Guid tenantId, string? entityName = null, Guid? entityId = null, string? fieldName = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<FieldChangeLogDto>>($"api/audit/field-changes?tenantId={tenantId}&entityName={Uri.EscapeDataString(entityName ?? string.Empty)}&entityId={entityId}&fieldName={Uri.EscapeDataString(fieldName ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<PagedResult<WorkflowApprovalHistoryDto>?> SearchApprovalHistoryAsync(Guid tenantId, Guid? workflowInstanceId = null, string? actionCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<WorkflowApprovalHistoryDto>>($"api/audit/approval-history?tenantId={tenantId}&workflowInstanceId={workflowInstanceId}&actionCode={Uri.EscapeDataString(actionCode ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<PagedResult<SecurityEventLogDto>?> SearchSecurityEventsAsync(Guid tenantId, string? searchTerm = null, bool? isSuccess = null, string? eventTypeCode = null, int? riskScoreMin = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<SecurityEventLogDto>>($"api/audit/security-events?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&isSuccess={isSuccess}&eventTypeCode={Uri.EscapeDataString(eventTypeCode ?? string.Empty)}&riskScoreMin={riskScoreMin}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<SecurityEventSummaryDto?> GetSecurityEventSummaryAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<SecurityEventSummaryDto>($"api/audit/security-events/summary?tenantId={tenantId}", cancellationToken);

    public Task<IReadOnlyList<SecurityEventTrendDto>?> GetSecurityEventTrendsAsync(Guid tenantId, int days = 14, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<SecurityEventTrendDto>>($"api/audit/security-events/trends?tenantId={tenantId}&days={days}", cancellationToken);

    public Task<PagedResult<ExportLogDto>?> SearchExportLogsAsync(Guid tenantId, string? entityName = null, string? exportTypeCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ExportLogDto>>($"api/audit/export-logs?tenantId={tenantId}&entityName={Uri.EscapeDataString(entityName ?? string.Empty)}&exportTypeCode={Uri.EscapeDataString(exportTypeCode ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> LogExportAsync(LogExportRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/audit/export-logs", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<RecordTimelineEntryDto>?> GetRecordTimelineAsync(Guid tenantId, string entityName, Guid entityId, int top = 100, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<RecordTimelineEntryDto>>($"api/audit/timeline/{Uri.EscapeDataString(entityName)}/{entityId}?tenantId={tenantId}&top={top}", cancellationToken);

    public Task<PagedResult<RetentionPolicyDto>?> SearchRetentionPoliciesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<RetentionPolicyDto>>($"api/audit/retention-policies?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateRetentionPolicyAsync(CreateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/audit/retention-policies", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateRetentionPolicyAsync(UpdateRetentionPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync("api/audit/retention-policies", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> ApplyRetentionPolicyAsync(Guid retentionPolicyId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/audit/retention-policies/{retentionPolicyId}/apply", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<ApplyRetentionResult>(cancellationToken: cancellationToken);
        return result?.AffectedRecords ?? 0;
    }

    private sealed class ApplyRetentionResult { public int AffectedRecords { get; set; } }

    public async Task<Guid> LogAuditEventAsync(LogAuditEventRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/audit/events", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> LogFieldChangeAsync(LogFieldChangeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/audit/field-changes/log", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> LogApprovalHistoryAsync(LogApprovalHistoryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/audit/approval-history/log", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> LogSecurityEventAsync(LogSecurityEventRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/audit/security-events/log", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    // ── Assistant ────────────────────────────────────────────
    public Task<PagedResult<AssistantConversationDto>?> SearchAssistantAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AssistantConversationDto>>($"api/assistant?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    // ── Platform Core engines ────────────────────────────────
    public Task<PagedResult<TenantBrandingDto>?> SearchTenantBrandingAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<TenantBrandingDto>>($"api/platform/branding?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<NotificationDto>?> SearchNotificationsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<NotificationDto>>($"api/notifications?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<NotificationTemplateDto>?> SearchNotificationTemplatesAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<NotificationTemplateDto>>($"api/notifications/templates?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<ReportDefinitionDto>?> SearchReportDefinitionsAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ReportDefinitionDto>>($"api/reports/definitions?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<ConfigurationSettingDto>?> SearchConfigurationSettingsAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ConfigurationSettingDto>>($"api/platform/configuration?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<SupportedLocaleDto>?> SearchSupportedLocalesAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<SupportedLocaleDto>>($"api/platform/locales?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<WorkflowDefinitionDto>?> SearchWorkflowDefinitionsAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<WorkflowDefinitionDto>>($"api/platform/workflow-definitions?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<UserSessionDto>?> SearchUserSessionsAsync(Guid tenantId, Guid? userId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<UserSessionDto>>($"api/platform/sessions?tenantId={tenantId}&userId={userId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    // ── IAM extended engines ─────────────────────────────────
    public Task<PagedResult<UserGroupDto>?> SearchUserGroupsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<UserGroupDto>>($"api/iam/user-groups?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<ExternalUserProfileDto>?> SearchExternalUserProfilesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ExternalUserProfileDto>>($"api/iam/external-profiles?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<SsoConfigurationDto>?> SearchSsoConfigurationsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<SsoConfigurationDto>>($"api/iam/sso?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<MfaDeviceDto>?> SearchMfaDevicesAsync(Guid tenantId, Guid? userId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<MfaDeviceDto>>($"api/iam/sso/mfa?tenantId={tenantId}&userId={userId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<FieldSecurityPolicyDto>?> SearchFieldSecurityPoliciesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<FieldSecurityPolicyDto>>($"api/iam/policies/fields?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<RecordSecurityPolicyDto>?> SearchRecordSecurityPoliciesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<RecordSecurityPolicyDto>>($"api/iam/policies/records?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<PrivilegedAccessRequestDto>?> SearchPrivilegedAccessRequestsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PrivilegedAccessRequestDto>>($"api/iam/pam?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<UserAccessReviewDto>?> SearchAccessReviewsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<UserAccessReviewDto>>($"api/iam/pam/reviews?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    // ── CRM extended ─────────────────────────────────────────
    public async Task<Guid> CreateOpportunityAsync(CreateOpportunityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/opportunities", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<QuoteDto>?> SearchQuotesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<QuoteDto>>($"api/crm/quotes?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateQuoteAsync(CreateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/crm/quotes", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<QuoteLineDto>?> GetQuoteLinesAsync(Guid quoteId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<QuoteLineDto>>($"api/crm/quotes/{quoteId}/lines", cancellationToken);

    public Task<PagedResult<LeadActivityDto>?> SearchLeadActivitiesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<LeadActivityDto>>($"api/crm/lead-activities?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateLeadActivityAsync(CreateLeadActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/crm/lead-activities", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<PricingRuleDto>?> SearchPricingRulesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PricingRuleDto>>($"api/crm/pricing-rules?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreatePricingRuleAsync(CreatePricingRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/crm/pricing-rules", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<ForecastEntryDto>?> SearchForecastEntriesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ForecastEntryDto>>($"api/crm/forecast?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateForecastEntryAsync(CreateForecastEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/crm/forecast", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    // ── Security / MFA ────────────────────────────────────────────────────────

    public Task<PagedResult<UserMfaStatusDto>?> SearchUsersWithMfaAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<UserMfaStatusDto>>($"api/security/mfa/users?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<PagedResult<UserMfaStatusDto>?> SearchUsersWithoutMfaAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<UserMfaStatusDto>>($"api/security/mfa/users/without?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<IReadOnlyList<MfaDeviceDto>?> GetUserMfaDevicesAsync(Guid userId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<MfaDeviceDto>>($"api/security/mfa/users/{userId}/devices", cancellationToken);

    public async Task<Guid> AddMfaMethodAsync(AddMfaMethodRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/security/mfa/devices", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task VerifyMfaMethodAsync(Guid deviceId, Guid? verifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/security/mfa/devices/{deviceId}/verify?verifiedByUserId={verifiedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DisableMfaMethodAsync(Guid deviceId, Guid? disabledByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/security/mfa/devices/{deviceId}/disable?disabledByUserId={disabledByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetMfaAsync(Guid userId, Guid? resetByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/security/mfa/users/{userId}/reset?resetByUserId={resetByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RequireMfaAsync(Guid userId, bool isRequired, Guid? setByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/security/mfa/users/{userId}/require?isRequired={isRequired}&setByUserId={setByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // ── Security / Trusted Devices ────────────────────────────────────────────

    public Task<PagedResult<TrustedDeviceDto>?> SearchTrustedDevicesAsync(Guid tenantId, Guid? userId = null, string? searchTerm = null, bool? isActive = null, bool? highRiskOnly = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/security/trusted-devices?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}";
        if (userId.HasValue)    url += $"&userId={userId}";
        if (isActive.HasValue)  url += $"&isActive={isActive.Value}";
        if (highRiskOnly == true) url += "&highRiskOnly=true";
        return _httpClient.GetFromJsonAsync<PagedResult<TrustedDeviceDto>>(url, cancellationToken);
    }

    public Task<TrustedDeviceDto?> GetTrustedDeviceByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<TrustedDeviceDto>($"api/security/trusted-devices/{id}", cancellationToken);

    public async Task RevokeTrustedDeviceAsync(Guid id, string? reason = null, Guid? revokedByUserId = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/security/trusted-devices/{id}/revoke?revokedByUserId={revokedByUserId}&reason={Uri.EscapeDataString(reason ?? string.Empty)}";
        var response = await _httpClient.PatchAsync(url, null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SubmitRiskReviewAsync(Guid id, RiskReviewRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/security/trusted-devices/{id}/risk-review", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // ── Security / User Status ─────────────────────────────────────────────────

    public Task<PagedResult<UserDto>?> SearchUsersForStatusAsync(Guid tenantId, string? searchTerm = null, string? statusCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/security/user-status?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(statusCode)) url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        return _httpClient.GetFromJsonAsync<PagedResult<UserDto>>(url, cancellationToken);
    }

    public async Task ChangeUserStatusAsync(Guid userId, ChangeUserStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/security/user-status/{userId}/change", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // ── Governance / Access Requests ──────────────────────────────────────────

    public Task<AccessRequestDto?> GetAccessRequestByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AccessRequestDto>($"api/governance/access-requests/{id}", cancellationToken);

    public Task<PagedResult<AccessRequestDto>?> SearchAccessRequestsAsync(Guid tenantId, string? searchTerm = null, string? requestTypeCode = null, string? statusCode = null, Guid? requestedForUserId = null, Guid? requestedByUserId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/governance/access-requests?tenantId={tenantId}&pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm))      url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(requestTypeCode)) url += $"&requestTypeCode={Uri.EscapeDataString(requestTypeCode)}";
        if (!string.IsNullOrEmpty(statusCode))      url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        if (requestedForUserId.HasValue)             url += $"&requestedForUserId={requestedForUserId}";
        if (requestedByUserId.HasValue)              url += $"&requestedByUserId={requestedByUserId}";
        return _httpClient.GetFromJsonAsync<PagedResult<AccessRequestDto>>(url, cancellationToken);
    }

    public async Task<Guid> SubmitAccessRequestAsync(SubmitAccessRequestRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/governance/access-requests", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result?.Id ?? Guid.Empty;
    }

    public async Task ProcessAccessRequestAsync(Guid id, ProcessAccessRequestRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/governance/access-requests/{id}/process", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // ── Governance / Access Review Campaigns ──────────────────────────────────

    public Task<PagedResult<AccessReviewCampaignDto>?> SearchAccessReviewCampaignsAsync(Guid tenantId, string? searchTerm = null, string? statusCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/governance/access-reviews?tenantId={tenantId}&pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(statusCode)) url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        return _httpClient.GetFromJsonAsync<PagedResult<AccessReviewCampaignDto>>(url, cancellationToken);
    }

    public Task<AccessReviewCampaignDto?> GetAccessReviewCampaignByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AccessReviewCampaignDto>($"api/governance/access-reviews/{id}", cancellationToken);

    public async Task<Guid> CreateAccessReviewCampaignAsync(CreateAccessReviewCampaignRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/governance/access-reviews", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result?.Id ?? Guid.Empty;
    }

    public async Task UpdateAccessReviewCampaignAsync(Guid id, UpdateAccessReviewCampaignRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/governance/access-reviews/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ActivateAccessReviewCampaignAsync(Guid id, Guid changedByUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/governance/access-reviews/{id}/activate?changedByUserId={changedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CompleteAccessReviewCampaignAsync(Guid id, Guid changedByUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/governance/access-reviews/{id}/complete?changedByUserId={changedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CancelAccessReviewCampaignAsync(Guid id, Guid changedByUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/governance/access-reviews/{id}/cancel?changedByUserId={changedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<IReadOnlyList<AccessReviewItemDto>?> GetAccessReviewItemsAsync(Guid campaignId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<AccessReviewItemDto>>($"api/governance/access-reviews/{campaignId}/items", cancellationToken);

    public async Task SubmitReviewDecisionAsync(Guid campaignId, Guid itemId, SubmitReviewDecisionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/governance/access-reviews/{campaignId}/items/{itemId}/decide", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // ── SoD / Rules ───────────────────────────────────────────────────────────

    public Task<PagedResult<SegregationOfDutyRuleDto>?> SearchSodRulesAsync(Guid? tenantId = null, string? searchTerm = null, string? severityCode = null, bool? isActive = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/sod/rules?pageNumber={pageNumber}&pageSize={pageSize}";
        if (tenantId.HasValue)                url += $"&tenantId={tenantId}";
        if (!string.IsNullOrEmpty(searchTerm))   url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(severityCode)) url += $"&severityCode={Uri.EscapeDataString(severityCode)}";
        if (isActive.HasValue)                url += $"&isActive={isActive.Value}";
        return _httpClient.GetFromJsonAsync<PagedResult<SegregationOfDutyRuleDto>>(url, cancellationToken);
    }

    public Task<SegregationOfDutyRuleDto?> GetSodRuleByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<SegregationOfDutyRuleDto>($"api/sod/rules/{id}", cancellationToken);

    public async Task<Guid> CreateSodRuleAsync(CreateSodRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/sod/rules", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result?.Id ?? Guid.Empty;
    }

    public async Task UpdateSodRuleAsync(Guid id, UpdateSodRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/sod/rules/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetSodRuleActiveAsync(Guid id, bool isActive, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var action = isActive ? "activate" : "deactivate";
        var url    = $"api/sod/rules/{id}/{action}?modifiedByUserId={modifiedByUserId}";
        var response = await _httpClient.PatchAsync(url, null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CloneSodRuleAsync(Guid id, CloneSodRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/sod/rules/{id}/clone", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result?.Id ?? Guid.Empty;
    }

    // ── SoD Conflicts ──────────────────────────────────────────────────────────

    public Task<PagedResult<SodConflictDto>?> SearchSodConflictsAsync(
        Guid? tenantId = null, string? searchTerm = null, string? statusCode = null,
        string? severityCode = null, int pageNumber = 1, int pageSize = 25,
        CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<SodConflictDto>>(
            $"api/sod/conflicts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}" +
            $"&statusCode={Uri.EscapeDataString(statusCode ?? string.Empty)}" +
            $"&severityCode={Uri.EscapeDataString(severityCode ?? string.Empty)}" +
            $"&pageNumber={pageNumber}&pageSize={pageSize}",
            cancellationToken);

    public Task<SodConflictDto?> GetSodConflictByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<SodConflictDto>($"api/sod/conflicts/{id}", cancellationToken);

    public async Task AssignSodConflictReviewerAsync(Guid id, AssignSodConflictReviewerRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/sod/conflicts/{id}/assign-reviewer", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemediateSodConflictAsync(Guid id, RemediateSodConflictRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/sod/conflicts/{id}/remediate", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResolveSodConflictAsync(Guid id, ResolveSodConflictRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/sod/conflicts/{id}/resolve", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CreateSodExceptionAsync(Guid id, CreateSodExceptionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/sod/conflicts/{id}/exception", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // ── Compliance ──────────────────────────────────────────
    public Task<PagedResult<PolicyDocumentDto>?> SearchPolicyDocumentsAsync(
        Guid? tenantId = null, string? searchTerm = null, string? typeCode = null,
        string? statusCode = null, bool? isActive = null,
        int pageNumber = 1, int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/compliance/policies?pageNumber={pageNumber}&pageSize={pageSize}";
        if (tenantId.HasValue)                   url += $"&tenantId={tenantId}";
        if (!string.IsNullOrEmpty(searchTerm))   url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(typeCode))     url += $"&typeCode={Uri.EscapeDataString(typeCode)}";
        if (!string.IsNullOrEmpty(statusCode))   url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        if (isActive.HasValue)                   url += $"&isActive={isActive.Value}";
        return _httpClient.GetFromJsonAsync<PagedResult<PolicyDocumentDto>>(url, cancellationToken);
    }

    public Task<PolicyDocumentDto?> GetPolicyDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PolicyDocumentDto>($"api/compliance/policies/{id}", cancellationToken);

    public async Task<Guid> CreatePolicyDocumentAsync(CreatePolicyDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/compliance/policies", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdatePolicyDocumentAsync(Guid id, UpdatePolicyDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/compliance/policies/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CreatePolicyDocumentVersionAsync(Guid id, VersionPolicyDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/compliance/policies/{id}/version", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task PublishPolicyDocumentAsync(Guid id, Guid? publishedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/compliance/policies/{id}/publish?publishedByUserId={publishedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RetirePolicyDocumentAsync(Guid id, Guid? retiredByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/compliance/policies/{id}/retire?retiredByUserId={retiredByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<IReadOnlyList<PolicyAcknowledgementDto>?> GetPolicyAcknowledgementsAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<PolicyAcknowledgementDto>>($"api/compliance/policies/{id}/acknowledgements", cancellationToken);

    public Task<IReadOnlyList<PolicyDocumentDto>?> GetPolicyVersionHistoryAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<PolicyDocumentDto>>($"api/compliance/policies/{id}/versions", cancellationToken);

    public Task<IReadOnlyList<PolicyAudienceDto>?> GetPolicyAudienceAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<PolicyAudienceDto>>($"api/compliance/policies/{id}/audience", cancellationToken);

    public Task<HttpResponseMessage> AddPolicyAudienceMemberAsync(Guid id, AddAudienceMemberRequest request, CancellationToken cancellationToken = default)
        => _httpClient.PostAsJsonAsync($"api/compliance/policies/{id}/audience", request, cancellationToken);

    public Task<HttpResponseMessage> RemovePolicyAudienceMemberAsync(Guid id, Guid audienceId, CancellationToken cancellationToken = default)
        => _httpClient.DeleteAsync($"api/compliance/policies/{id}/audience/{audienceId}", cancellationToken);

    // ── Compliance — Acknowledgements ─────────────────────────────────────────

    public Task<AcknowledgementSummaryDto?> GetAcknowledgementSummaryAsync(Guid? tenantId = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AcknowledgementSummaryDto>($"api/compliance/acknowledgements/summary{(tenantId.HasValue ? $"?tenantId={tenantId}" : string.Empty)}", cancellationToken);

    public Task<IReadOnlyList<PendingAcknowledgementDto>?> GetPendingAcknowledgementsAsync(Guid? tenantId = null, Guid? policyId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
    {
        var url = BuildAckUrl("api/compliance/acknowledgements/pending", tenantId, policyId, searchTerm);
        return _httpClient.GetFromJsonAsync<IReadOnlyList<PendingAcknowledgementDto>>(url, cancellationToken);
    }

    public Task<IReadOnlyList<PendingAcknowledgementDto>?> GetOverdueAcknowledgementsAsync(Guid? tenantId = null, Guid? policyId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
    {
        var url = BuildAckUrl("api/compliance/acknowledgements/overdue", tenantId, policyId, searchTerm);
        return _httpClient.GetFromJsonAsync<IReadOnlyList<PendingAcknowledgementDto>>(url, cancellationToken);
    }

    public Task<PagedResult<AcknowledgementDetailDto>?> SearchAcknowledgedAsync(Guid? tenantId = null, Guid? policyId = null, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = BuildAckUrl("api/compliance/acknowledgements", tenantId, policyId, searchTerm);
        url += (url.Contains('?') ? "&" : "?") + $"pageNumber={pageNumber}&pageSize={pageSize}";
        return _httpClient.GetFromJsonAsync<PagedResult<AcknowledgementDetailDto>>(url, cancellationToken);
    }

    private static string BuildAckUrl(string path, Guid? tenantId, Guid? policyId, string? searchTerm)
    {
        var parts = new List<string>();
        if (tenantId.HasValue)                              parts.Add($"tenantId={tenantId}");
        if (policyId.HasValue)                              parts.Add($"policyId={policyId}");
        if (!string.IsNullOrWhiteSpace(searchTerm))         parts.Add($"searchTerm={Uri.EscapeDataString(searchTerm)}");
        return parts.Count > 0 ? $"{path}?{string.Join("&", parts)}" : path;
    }

    private sealed class IdResult { public Guid Id { get; set; } }
}
