using System.Net;
using System.Net.Http.Json;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AccountNotes;
using Ams.Application.Features.AccountSegments;
using Ams.Application.Features.Accounts;
using Ams.Application.Features.BillingAccounts;
using Ams.Application.Features.Billing;
using Ams.Application.Features.Commissions;
using Ams.Application.Features.Finance;
using Ams.Application.Features.Tenants;
using Ams.Application.Features.Compliance;
using Ams.Application.Features.Contacts;
using Ams.Application.Features.Documents;
using Ams.Application.Features.Duplicates;
using Ams.Application.Features.Enrichment;
using Ams.Application.Features.Engagements;
using Ams.Application.Features.Forecast;
using Ams.Application.Features.Audit;
using Ams.Application.Features.Governance;
using Ams.Application.Features.Iam;
using Ams.Application.Features.LeadActivities;
using Ams.Application.Features.Leads;
using Ams.Application.Features.Opportunities;
using Ams.Application.Features.Operations;
using Ams.Application.Features.Payments;
using Ams.Application.Features.PortalInvites;
using Ams.Application.Features.PricingRules;
using Ams.Application.Features.Quotes;
using Ams.Application.Features.Security;
using Ams.Application.Features.Plans;
using Ams.Application.Features.Sod;
using Ams.Application.Features.Subscriptions;
using Ams.Application.Features.FeatureCatalog;
using Ams.Application.Features.TenantFeatures;
using Ams.Application.Features.Regions;
using Ams.Application.Features.DeploymentBindings;
using Ams.Application.Features.DeploymentStamps;
using Ams.Application.Features.TenantDeploymentAssignments;
using Ams.Application.Features.QuotaRules;
using Ams.Application.Features.TenantQuotas;
using Ams.Application.Features.QuotaViolations;
using Ams.Application.Features.HealthChecks;
using Ams.Application.Features.Alerts;
using Ams.Application.Features.SlaDefinitions;
using Ams.Application.Features.PlatformEvents;
using Ams.Application.Features.BackgroundJobs;
using Ams.Application.Features.Agency;
using Ams.Application.Features.Carriers;
using Ams.Application.Features.CrmConfig;
using Ams.Application.Features.AccountConfig;
using Ams.Application.Features.PolicyConfig;

using Ams.Application.Features.Lobs;
using Ams.Application.Features.Appetite;
using Ams.Application.Features.Communications;
using Ams.Application.Features.Submissions;
using Ams.Application.Features.Workbench;

namespace Ams.Web.Services;

public sealed partial class ApiClient
{
    private readonly HttpClient _httpClient;

    public ApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // -- Dashboard --------------------------------------------
    public Task<DashboardKpiDto?> GetDashboardKpiAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<DashboardKpiDto>($"api/dashboard?tenantId={tenantId}", cancellationToken);

    public Task<ExecutiveDashboardPageDto?> GetExecutiveDashboardPageAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<ExecutiveDashboardPageDto>($"api/dashboard/executive?tenantId={tenantId}", cancellationToken);

    public Task<PagedResult<DashboardControllerRecordDto>?> SearchDashboardRecordsAsync(Guid tenantId, string kind, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DashboardControllerRecordDto>>($"api/dashboard/records/{Uri.EscapeDataString(kind)}?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateDashboardRecordAsync(UpsertDashboardRecordRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/dashboard/records", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateDashboardRecordAsync(Guid id, UpsertDashboardRecordRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/dashboard/records/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteDashboardRecordAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/dashboard/records/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public sealed class DashboardControllerRecordDto
    {
        public Guid Id { get; set; }
        public string JsonData { get; set; } = string.Empty;
    }

    // -- My Workbench -----------------------------------------
    public Task<MyWorkbenchDto?> GetMyWorkbenchAsync(Guid tenantId, Guid? userId = null, string? searchTerm = null, string? viewCode = null, string? priorityCode = null, string? statusCode = null, DateOnly? workDate = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/workbench?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&viewCode={Uri.EscapeDataString(viewCode ?? string.Empty)}&priorityCode={Uri.EscapeDataString(priorityCode ?? string.Empty)}&statusCode={Uri.EscapeDataString(statusCode ?? string.Empty)}";
        if (userId.HasValue)
        {
            url += $"&userId={userId.Value}";
        }

        if (workDate.HasValue)
        {
            url += $"&workDate={workDate.Value:yyyy-MM-dd}";
        }

        return _httpClient.GetFromJsonAsync<MyWorkbenchDto>(url, cancellationToken);
    }

    public async Task SetMyWorkbenchTaskStatusAsync(Guid taskItemId, MyWorkbenchTaskStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/workbench/tasks/{taskItemId}/status", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetMyWorkbenchNotificationReadAsync(Guid notificationId, MyWorkbenchNotificationStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/workbench/notifications/{notificationId}/read", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Agency Dashboard -------------------------------------
    public Task<AgencyExecutiveOverviewDto?> GetAgencyOverviewAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<AgencyExecutiveOverviewDto>($"api/agency-dashboard/overview?tenantId={tenantId}", ct);

    public Task<AgencyKpiDto?> GetAgencyKpisAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<AgencyKpiDto>($"api/agency-dashboard/kpis?tenantId={tenantId}", ct);

    public Task<List<BranchPerformanceDto>?> GetBranchPerformanceAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<List<BranchPerformanceDto>>($"api/agency-dashboard/branch-performance?tenantId={tenantId}", ct);

    public Task<List<ProducerPerformanceDto>?> GetProducerPerformanceAsync(Guid tenantId, int top = 10, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<List<ProducerPerformanceDto>>($"api/agency-dashboard/producer-performance?tenantId={tenantId}&top={top}", ct);

    public Task<RenewalPipelineDto?> GetRenewalPipelineAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<RenewalPipelineDto>($"api/agency-dashboard/renewal-pipeline?tenantId={tenantId}", ct);

    public Task<ClaimsSummaryDto?> GetClaimsSummaryAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<ClaimsSummaryDto>($"api/agency-dashboard/claims-summary?tenantId={tenantId}", ct);

    public Task<BillingSummaryDto?> GetBillingSummaryAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<BillingSummaryDto>($"api/agency-dashboard/billing-summary?tenantId={tenantId}", ct);

    // -- AI & Intelligence ------------------------------------
    public Task<PagedResult<AiInsightCardDto>?> GetAiInsightCardsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AiInsightCardDto>>($"analytics/ai/insight-cards?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<AiAssistantConfigDto?> GetAiAssistantConfigAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AiAssistantConfigDto>($"analytics/ai/assistant-config?tenantId={tenantId}", cancellationToken);

    public async Task<AiAssistantResponseDto?> AskAiAssistantAsync(AiAssistantAskRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("analytics/ai/assistant/ask", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AiAssistantResponseDto>(cancellationToken: cancellationToken);
    }

    public async Task SetAiInsightDismissedAsync(Guid id, bool dismissed, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"analytics/ai/insights/{id}/dismiss?dismissed={dismissed}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CreateAiInsightTaskAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"analytics/ai/insights/{id}/task", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Platform Core ----------------------------------------
    public Task<PagedResult<TenantDto>?> SearchTenantsAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<TenantDto>>($"api/tenants?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<TenantDto?> GetTenantByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<TenantDto>($"api/tenants/{id}", cancellationToken);

    public async Task<Guid> CreateTenantAsync(CreateTenantRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/tenants", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateTenantAsync(Guid id, UpdateTenantRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tenants/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SuspendTenantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/tenants/{id}/suspend", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ActivateTenantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/tenants/{id}/activate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task TerminateTenantAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/tenants/{id}/terminate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Tenant Domains ---------------------------------------
    public Task<PagedResult<TenantDomainDto>?> SearchTenantDomainsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<TenantDomainDto>>($"api/tenant-domains?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<PagedResult<TenantDomainDto>?> SearchAllTenantDomainsAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<TenantDomainDto>>($"api/tenant-domains?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<TenantDomainDto?> GetTenantDomainByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<TenantDomainDto>($"api/tenant-domains/{id}", cancellationToken);

    public async Task<Guid> CreateTenantDomainAsync(CreateTenantDomainRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/tenant-domains", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateTenantDomainRedirectAsync(Guid id, string? redirectTarget, string? notes = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/tenant-domains/{id}/redirect", new { RedirectTarget = redirectTarget, Notes = notes }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetTenantDomainPrimaryAsync(Guid tenantId, Guid domainId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/tenant-domains/{tenantId}/set-primary/{domainId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task VerifyTenantDomainAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/tenant-domains/{id}/verify", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTenantDomainAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tenant-domains/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Plans ------------------------------------------------
    public Task<PagedResult<PlanDto>?> SearchPlansAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PlanDto>>($"api/plans?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<PlanDto?> GetPlanByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PlanDto>($"api/plans/{id}", cancellationToken);

    public async Task<Guid> CreatePlanAsync(CreatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/plans", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdatePlanAsync(Guid id, UpdatePlanRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/plans/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ClonePlanAsync(Guid id, string newPlanCode, string newPlanName, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/plans/{id}/clone", new { NewPlanCode = newPlanCode, NewPlanName = newPlanName }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ActivatePlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/plans/{id}/activate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeactivatePlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/plans/{id}/deactivate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePlanAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/plans/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Plan Sub-entities -------------------------------------
    public Task<IReadOnlyList<PlanFeatureDto>?> GetPlanFeaturesAsync(Guid planId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<PlanFeatureDto>>($"api/plans/{planId}/features", cancellationToken);

    public async Task AddPlanFeatureAsync(Guid planId, AddPlanFeatureRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/plans/{planId}/features", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemovePlanFeatureAsync(Guid planId, Guid planFeatureId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/plans/{planId}/features/{planFeatureId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<IReadOnlyList<PlanLimitDto>?> GetPlanLimitsAsync(Guid planId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<PlanLimitDto>>($"api/plans/{planId}/limits", cancellationToken);

    public async Task AddPlanLimitAsync(Guid planId, AddPlanLimitRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/plans/{planId}/limits", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdatePlanLimitAsync(Guid planId, Guid planLimitId, UpdatePlanLimitRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/plans/{planId}/limits/{planLimitId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemovePlanLimitAsync(Guid planId, Guid planLimitId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/plans/{planId}/limits/{planLimitId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<IReadOnlyList<PlanAddOnDto>?> GetPlanAddOnsAsync(Guid planId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<PlanAddOnDto>>($"api/plans/{planId}/addons", cancellationToken);

    public async Task AddPlanAddOnAsync(Guid planId, AddPlanAddOnRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/plans/{planId}/addons", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemovePlanAddOnAsync(Guid planId, Guid planAddOnId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/plans/{planId}/addons/{planAddOnId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Platform Usage -----------------------------------------
    public Task<PlatformUsageDto?> GetPlatformUsageAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PlatformUsageDto>("api/usage", cancellationToken);

    // -- Feature Catalog ----------------------------------------
    public Task<PagedResult<FeatureCatalogDto>?> SearchFeaturesAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<FeatureCatalogDto>>($"api/features?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateFeatureAsync(CreateFeatureRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/features", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateFeatureAsync(Guid id, UpdateFeatureRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/features/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetFeatureEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/features/{id}/{(enabled ? "enable" : "disable")}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteFeatureAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/features/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Tenant Features ----------------------------------------
    public Task<IReadOnlyList<TenantFeatureDto>?> GetTenantFeaturesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<TenantFeatureDto>>($"api/tenants/{tenantId}/features", cancellationToken);

    public async Task OverrideTenantFeatureAsync(Guid tenantId, OverrideTenantFeatureRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/tenants/{tenantId}/features/override", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetTenantFeatureEnabledAsync(Guid tenantId, string featureCode, bool enabled, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/tenants/{tenantId}/features/{Uri.EscapeDataString(featureCode)}/{(enabled ? "enable" : "disable")}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetTenantFeatureAsync(Guid tenantId, string featureCode, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tenants/{tenantId}/features/{Uri.EscapeDataString(featureCode)}/reset", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<UsageEventDto>?> GetUsageEventsAsync(
        Guid? tenantId = null,
        string? metricType = null,
        string? sourceService = null,
        int pageNumber = 1,
        int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/usage/events?pageNumber={pageNumber}&pageSize={pageSize}";
        if (tenantId.HasValue) url += $"&tenantId={tenantId}";
        if (!string.IsNullOrEmpty(metricType)) url += $"&metricType={Uri.EscapeDataString(metricType)}";
        if (!string.IsNullOrEmpty(sourceService)) url += $"&sourceService={Uri.EscapeDataString(sourceService)}";
        return _httpClient.GetFromJsonAsync<PagedResult<UsageEventDto>>(url, cancellationToken);
    }

    // -- Subscriptions -----------------------------------------
    public Task<SubscriptionDto?> GetSubscriptionByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<SubscriptionDto>($"api/subscriptions/{id}", cancellationToken);

    public Task<PagedResult<SubscriptionDto>?> SearchSubscriptionsAsync(string? searchTerm = null, Guid? tenantId = null, Guid? planId = null, string? statusCode = null, string? renewalType = null, string? billingCycle = null, bool? pastDue = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/subscriptions?pageNumber={pageNumber}&pageSize={pageSize}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}";
        if (tenantId.HasValue) url += $"&tenantId={tenantId}";
        if (planId.HasValue) url += $"&planId={planId}";
        if (!string.IsNullOrEmpty(statusCode)) url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        if (!string.IsNullOrEmpty(renewalType)) url += $"&renewalType={Uri.EscapeDataString(renewalType)}";
        if (!string.IsNullOrEmpty(billingCycle)) url += $"&billingCycle={Uri.EscapeDataString(billingCycle)}";
        if (pastDue.HasValue) url += $"&pastDue={pastDue.Value}";
        return _httpClient.GetFromJsonAsync<PagedResult<SubscriptionDto>>(url, cancellationToken);
    }

    public async Task<Guid> CreateSubscriptionAsync(CreateSubscriptionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/subscriptions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpgradeSubscriptionAsync(Guid id, Guid newPlanId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/subscriptions/{id}/upgrade", new { PlanId = newPlanId }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DowngradeSubscriptionAsync(Guid id, Guid newPlanId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/subscriptions/{id}/downgrade", new { PlanId = newPlanId }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RenewSubscriptionAsync(Guid id, DateTime newEndDateUtc, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/subscriptions/{id}/renew", new { NewEndDateUtc = newEndDateUtc }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CancelSubscriptionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/subscriptions/{id}/cancel", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteSubscriptionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/subscriptions/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- IAM --------------------------------------------------
    public Task<UserDto?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<UserDto>($"api/users/{userId}", cancellationToken);

    public async Task<UserProfileDto?> GetUserProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/users/{userId}/profile", cancellationToken);

        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound || response.Content.Headers.ContentLength == 0)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<UserProfileDto>(cancellationToken: cancellationToken);
    }

    public async Task UpdateUserProfileAsync(Guid userId, UpdateUserProfileRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/users/{userId}/profile", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<AuthenticatedUserDto?> ValidateLoginAsync(Guid tenantId, string userNameOrEmail, string password, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/validate", new { TenantId = tenantId, UserNameOrEmail = userNameOrEmail, Password = password }, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthenticatedUserDto>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> RegisterLoginUserAsync(RegisterLoginUserRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/auth/register", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public Task<PagedResult<UserDto>?> SearchUsersAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<UserDto>>($"api/users?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<PagedResult<UserDto>?> SearchUsersAsync(Guid tenantId, string? searchTerm, CancellationToken cancellationToken)
        => SearchUsersAsync(tenantId, searchTerm, 1, 25, cancellationToken);

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

    public async Task SetUserMfaAsync(Guid userId, bool enabled, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/users/{userId}/mfa?enabled={enabled}&modifiedByUserId={modifiedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AssignUserBranchAsync(Guid userId, Guid? branchId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/users/{userId}/branch?branchId={branchId}&modifiedByUserId={modifiedByUserId}", null, cancellationToken);
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

    public Task<PagedResult<RoleDto>?> SearchRolesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<RoleDto>>($"api/roles?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<PagedResult<RoleDto>?> SearchRolesAsync(Guid tenantId, string? searchTerm, CancellationToken cancellationToken)
        => SearchRolesAsync(tenantId, searchTerm, 1, 25, cancellationToken);

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
    public Task<PagedResult<UserRoleDto>?> SearchUserRolesAsync(Guid tenantId, Guid? userId = null, Guid? roleId = null, bool? isActive = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<UserRoleDto>>($"api/iam/user-roles?tenantId={tenantId}&userId={userId}&roleId={roleId}&isActive={isActive}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

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
    public Task<PagedResult<UserScopeDto>?> SearchUserScopesAsync(Guid tenantId, Guid? userId = null, string? scopeTypeCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<UserScopeDto>>($"api/iam/user-scopes?tenantId={tenantId}&userId={userId}&scopeTypeCode={Uri.EscapeDataString(scopeTypeCode ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

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

    // -- CRM --------------------------------------------------
    public async Task<Guid> CreateLeadAsync(CreateLeadRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/leads", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<LeadDto>?> SearchLeadsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<LeadDto>>($"api/leads?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<LeadDto?> GetLeadByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<LeadDto>($"api/leads/{id}", cancellationToken);

    public Task<IReadOnlyList<LeadScoreFactorDto>?> GetLeadScoreFactorsAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<LeadScoreFactorDto>>($"api/leads/{id}/score-factors", cancellationToken);

    public Task<LeadEngagementSummaryDto?> GetLeadEngagementSummaryAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<LeadEngagementSummaryDto>($"api/leads/{id}/engagement", cancellationToken);

    public Task<IReadOnlyList<LeadEngagementFactorDto>?> GetLeadEngagementFactorsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<LeadEngagementFactorDto>>($"api/leads/engagement-factors?tenantId={tenantId}", cancellationToken);

    public async Task<Guid> CreateLeadEngagementFactorAsync(CreateLeadEngagementFactorRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/leads/engagement-factors", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateLeadEngagementFactorAsync(Guid engagementFactorId, UpdateLeadEngagementFactorRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/leads/engagement-factors/{engagementFactorId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteLeadEngagementFactorAsync(Guid engagementFactorId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/leads/engagement-factors/{engagementFactorId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateLeadAsync(Guid id, UpdateLeadRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/leads/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<IReadOnlyList<LeadContactDto>?> GetLeadContactsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<LeadContactDto>>($"api/leads/{leadId}/contacts", cancellationToken);

    public async Task<Guid> CreateLeadContactAsync(Guid leadId, CreateLeadContactRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/leads/{leadId}/contacts", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateLeadContactAsync(Guid leadId, Guid contactId, UpdateLeadContactRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/leads/{leadId}/contacts/{contactId}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessWithDetailsAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var detail = await response.Content.ReadAsStringAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(detail))
        {
            response.EnsureSuccessStatusCode();
        }

        throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.ReasonPhrase}). {detail}", null, response.StatusCode);
    }

    public async Task DeleteLeadContactAsync(Guid contactId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/leads/contacts/{contactId}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<IReadOnlyList<LeadInterestLineDto>?> GetLeadInterestLinesAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<LeadInterestLineDto>>($"api/leads/{leadId}/interest-lines", cancellationToken);

    public async Task<Guid> CreateLeadInterestLineAsync(Guid leadId, CreateLeadInterestLineRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/leads/{leadId}/interest-lines", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateLeadInterestLineAsync(Guid leadId, Guid interestLineId, UpdateLeadInterestLineRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/leads/{leadId}/interest-lines/{interestLineId}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteLeadInterestLineAsync(Guid interestLineId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/leads/interest-lines/{interestLineId}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<IReadOnlyList<LeadCommunicationDto>?> GetLeadCommunicationsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<LeadCommunicationDto>>($"api/leads/{leadId}/communications", cancellationToken);

    public async Task<Guid> CreateLeadCommunicationAsync(Guid leadId, CreateLeadCommunicationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/leads/{leadId}/communications", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateLeadCommunicationAsync(Guid leadId, Guid communicationId, UpdateLeadCommunicationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/leads/{leadId}/communications/{communicationId}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteLeadCommunicationAsync(Guid communicationId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/leads/communications/{communicationId}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    // -- CRM Duplicate Management ------------------------------
    public Task<PagedResult<DuplicateGroupDto>?> SearchDuplicatesAsync(DuplicateSearchRequest request, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DuplicateGroupDto>>($"api/duplicates?tenantId={request.TenantId}&entityType={Uri.EscapeDataString(request.EntityType ?? string.Empty)}&searchTerm={Uri.EscapeDataString(request.SearchTerm ?? string.Empty)}&confidenceBand={Uri.EscapeDataString(request.ConfidenceBand ?? string.Empty)}&statusCode={Uri.EscapeDataString(request.StatusCode ?? string.Empty)}&pageNumber={request.PageNumber}&pageSize={request.PageSize}", cancellationToken);

    public async Task<PagedResult<DuplicateGroupDto>?> ScanDuplicatesAsync(DuplicateScanRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/duplicates/scan", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<PagedResult<DuplicateGroupDto>>(cancellationToken: cancellationToken);
    }

    public async Task SetDuplicatePrimaryAsync(Guid groupId, DuplicateSetPrimaryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/duplicates/{groupId}/primary", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task MergeDuplicateGroupAsync(Guid groupId, DuplicateResolveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/duplicates/{groupId}/merge", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DismissDuplicateGroupAsync(Guid groupId, DuplicateResolveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/duplicates/{groupId}/dismiss", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task BulkMergeDuplicateGroupsAsync(DuplicateBulkResolveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync("api/duplicates/bulk-merge", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task BulkDismissDuplicateGroupsAsync(DuplicateBulkResolveRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync("api/duplicates/bulk-dismiss", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    // -- CRM Data Enrichment -----------------------------------
    public Task<EnrichmentWorkspaceDto?> GetEnrichmentWorkspaceAsync(EnrichmentSearchRequest request, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<EnrichmentWorkspaceDto>($"api/enrichment?tenantId={request.TenantId}&searchTerm={Uri.EscapeDataString(request.SearchTerm ?? string.Empty)}&providerStatus={Uri.EscapeDataString(request.ProviderStatus ?? string.Empty)}&jobStatus={Uri.EscapeDataString(request.JobStatus ?? string.Empty)}&entityType={Uri.EscapeDataString(request.EntityType ?? string.Empty)}", cancellationToken);

    public async Task ConfigureEnrichmentProviderAsync(Guid providerId, EnrichmentProviderConfigRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/enrichment/providers/{providerId}/configuration", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task SetEnrichmentProviderStatusAsync(Guid providerId, EnrichmentProviderStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/enrichment/providers/{providerId}/status", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task<EnrichmentJobDto?> RunEnrichmentAsync(EnrichmentRunRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/enrichment/run", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<EnrichmentJobDto>(cancellationToken: cancellationToken);
    }

    public Task<IReadOnlyList<LeadCampaignEnrollmentDto>?> GetLeadCampaignsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<LeadCampaignEnrollmentDto>>($"api/leads/{leadId}/campaigns", cancellationToken);

    public async Task<Guid> CreateLeadCampaignAsync(Guid leadId, CreateLeadCampaignEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/leads/{leadId}/campaigns", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateLeadCampaignAsync(Guid leadId, Guid enrollmentId, UpdateLeadCampaignEnrollmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/leads/{leadId}/campaigns/{enrollmentId}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteLeadCampaignAsync(Guid enrollmentId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/leads/campaigns/{enrollmentId}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<IReadOnlyList<LeadDocumentDto>?> GetLeadDocumentsAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<LeadDocumentDto>>($"api/leads/{leadId}/documents", cancellationToken);

    public async Task<Guid> CreateLeadDocumentAsync(Guid leadId, CreateLeadDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/leads/{leadId}/documents", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateLeadDocumentAsync(Guid leadId, Guid documentId, UpdateLeadDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/leads/{leadId}/documents/{documentId}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteLeadDocumentAsync(Guid documentId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/leads/documents/{documentId}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<OpportunityDto>?> SearchOpportunitiesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<OpportunityDto>>($"api/opportunities?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<OpportunityDetailDto?> GetOpportunityDetailAsync(Guid opportunityId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<OpportunityDetailDto>($"api/opportunities/{opportunityId}/detail", cancellationToken);

    public async Task UpdateOpportunityAsync(Guid opportunityId, UpdateOpportunityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/opportunities/{opportunityId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateOpportunityStageAsync(Guid opportunityId, Ams.Application.Features.Opportunities.UpdateOpportunityStageRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/opportunities/{opportunityId}/stage", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> UpsertOpportunityActivityAsync(Guid opportunityId, UpsertOpportunityActivityRequest request, CancellationToken cancellationToken = default)
    {
        var url = request.ActivityId.HasValue ? $"api/opportunities/{opportunityId}/activities/{request.ActivityId}" : $"api/opportunities/{opportunityId}/activities";
        var response = request.ActivityId.HasValue
            ? await _httpClient.PutAsJsonAsync(url, request, cancellationToken)
            : await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task DeleteOpportunityActivityAsync(Guid activityId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/opportunities/activities/{activityId}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> UpsertOpportunitySubmissionAsync(Guid opportunityId, UpsertOpportunitySubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var url = request.SubmissionId.HasValue ? $"api/opportunities/{opportunityId}/submissions/{request.SubmissionId}" : $"api/opportunities/{opportunityId}/submissions";
        var response = request.SubmissionId.HasValue
            ? await _httpClient.PutAsJsonAsync(url, request, cancellationToken)
            : await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task DeleteOpportunitySubmissionAsync(Guid submissionId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/opportunities/submissions/{submissionId}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> UpsertOpportunityCompetitorAsync(Guid opportunityId, UpsertOpportunityCompetitorRequest request, CancellationToken cancellationToken = default)
    {
        var url = request.CompetitorId.HasValue ? $"api/opportunities/{opportunityId}/competitors/{request.CompetitorId}" : $"api/opportunities/{opportunityId}/competitors";
        var response = request.CompetitorId.HasValue
            ? await _httpClient.PutAsJsonAsync(url, request, cancellationToken)
            : await _httpClient.PostAsJsonAsync(url, request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task DeleteOpportunityCompetitorAsync(Guid competitorId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/opportunities/competitors/{competitorId}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<IReadOnlyList<LeadScoringRuleDto>?> GetLeadScoringRulesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<LeadScoringRuleDto>>($"api/leads/scoring-rules?tenantId={tenantId}", cancellationToken);

    public async Task<Guid> CreateLeadScoringRuleAsync(CreateLeadScoringRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/leads/scoring-rules", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateLeadScoringRuleAsync(Guid scoringRuleId, UpdateLeadScoringRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/leads/scoring-rules/{scoringRuleId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteLeadScoringRuleAsync(Guid scoringRuleId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/leads/scoring-rules/{scoringRuleId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Account Segments -------------------------------------
    public Task<PagedResult<AccountSegmentDto>?> SearchAccountSegmentsAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AccountSegmentDto>>($"api/client/segments?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateAccountSegmentAsync(CreateAccountSegmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/client/segments", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateAccountSegmentAsync(Guid id, UpdateAccountSegmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/client/segments/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAccountSegmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/client/segments/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<AccountSegmentRuleDto>?> SearchAccountSegmentRulesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AccountSegmentRuleDto>>($"api/client/segment-rules?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateAccountSegmentRuleAsync(CreateAccountSegmentRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/client/segment-rules", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result?.Id ?? Guid.Empty;
    }

    public async Task UpdateAccountSegmentRuleAsync(Guid id, UpdateAccountSegmentRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/client/segment-rules/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteAccountSegmentRuleAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/client/segment-rules/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task RecalculateAccountSegmentRulesAsync(Guid tenantId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/client/segment-rules/recalculate?tenantId={tenantId}&modifiedByUserId={modifiedByUserId}", null, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task RecalculateAccountSegmentRuleAsync(Guid tenantId, Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/client/segment-rules/{id}/recalculate?tenantId={tenantId}&modifiedByUserId={modifiedByUserId}", null, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    // -- Account Ownership ------------------------------------
    public Task<PagedResult<AccountOwnerHistoryDto>?> SearchAccountOwnershipAsync(Guid tenantId, Guid? accountId = null, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/client/account-ownership?tenantId={tenantId}&pageNumber={pageNumber}&pageSize={pageSize}";
        if (accountId.HasValue) url += $"&accountId={accountId}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        return _httpClient.GetFromJsonAsync<PagedResult<AccountOwnerHistoryDto>>(url, cancellationToken);
    }

    // -- Portal Invites ---------------------------------------
    public Task<PagedResult<PortalInviteDto>?> SearchPortalInvitesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PortalInviteDto>>($"api/client/portal-invites?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreatePortalInviteAsync(CreatePortalInviteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/client/portal-invites", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<PortalAdminUserDto>?> SearchPortalAdminUsersAsync(Guid tenantId, string? searchTerm = null, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PortalAdminUserDto>>($"api/portal-admin/users?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", ct);

    public Task<PagedResult<PortalAdminRequestDto>?> SearchPortalAdminRequestsAsync(Guid tenantId, string? searchTerm = null, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PortalAdminRequestDto>>($"api/portal-admin/requests?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", ct);

    public Task<PagedResult<PortalAdminDocumentDto>?> SearchPortalAdminDocumentsAsync(Guid tenantId, string? searchTerm = null, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PortalAdminDocumentDto>>($"api/portal-admin/documents?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", ct);

    public Task<PagedResult<PortalActivityEventDto>?> SearchPortalAdminActivityAsync(Guid tenantId, string? searchTerm = null, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PortalActivityEventDto>>($"api/portal-admin/activity?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", ct);

    public async Task UpdatePortalActivityStatusAsync(Guid id, UpdatePortalActivityEventRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/portal-admin/activity/{id}/status", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<PortalMobileInstallDto>?> SearchPortalMobileInstallsAsync(Guid tenantId, string? searchTerm = null, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PortalMobileInstallDto>>($"api/portal-admin/mobile-installs?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", ct);

    public async Task UpdatePortalMobileInstallStatusAsync(Guid id, UpdatePortalMobileInstallRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/portal-admin/mobile-installs/{id}/status", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<PortalCapabilityDto>?> GetPortalCapabilitiesAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PortalCapabilityDto>>($"api/portal-admin/capabilities?tenantId={tenantId}", ct);

    public Task<PortalBrandingSettingsDto?> GetPortalBrandingSettingsAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PortalBrandingSettingsDto>($"api/portal-admin/branding?tenantId={tenantId}", ct);

    public Task<PortalMobileSettingsDto?> GetPortalMobileSettingsAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PortalMobileSettingsDto>($"api/portal-admin/mobile?tenantId={tenantId}", ct);

    public Task<PortalMyAccountDto?> GetPortalMyAccountAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PortalMyAccountDto>($"api/portal-admin/my-account?tenantId={tenantId}", ct);

    public async Task UpdatePortalMyAccountAsync(Guid tenantId, PortalMyAccountDto account, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/portal-admin/my-account?tenantId={tenantId}", account, ct);
        response.EnsureSuccessStatusCode();
    }

    public Task<PortalWhiteLabelConfigurationDto?> GetPortalWhiteLabelConfigurationAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PortalWhiteLabelConfigurationDto>($"api/portal-admin/white-label?tenantId={tenantId}", ct);

    public async Task UpdatePortalWhiteLabelConfigurationAsync(UpdatePortalWhiteLabelConfigurationRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync("api/portal-admin/white-label", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task PublishPortalWhiteLabelAsync(Guid tenantId, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/portal-admin/white-label/publish?tenantId={tenantId}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task RunPortalWhiteLabelActionAsync(Guid tenantId, string action, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/portal-admin/white-label/action?tenantId={tenantId}&action={Uri.EscapeDataString(action)}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<PortalMetricRecordDto>?> SearchPortalMetricRecordsAsync(Guid tenantId, string kind, string? searchTerm = null, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PortalMetricRecordDto>>($"api/portal-admin/metrics/{Uri.EscapeDataString(kind)}?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", ct);

    public Task<PagedResult<PortalApiUsageDto>?> SearchPortalApiUsageAsync(Guid tenantId, string? searchTerm = null, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PortalApiUsageDto>>($"api/portal-admin/api-usage?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", ct);

    public async Task UpdatePortalApiUsageStatusAsync(Guid id, UpdatePortalApiUsageRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/portal-admin/api-usage/{id}/status", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<PortalChatSessionDto>?> SearchPortalChatSessionsAsync(Guid tenantId, string? searchTerm = null, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PortalChatSessionDto>>($"api/portal-admin/chat-sessions?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", ct);

    public async Task UpdatePortalChatSessionStatusAsync(Guid id, UpdatePortalChatSessionStatusRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/portal-admin/chat-sessions/{id}/status", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<PortalAdminRecordDto>?> SearchPortalAdminRecordsAsync(Guid tenantId, string kind, string? searchTerm = null, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PortalAdminRecordDto>>($"api/portal-admin/records?tenantId={tenantId}&kind={Uri.EscapeDataString(kind)}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", ct);

    public async Task SetPortalAdminRecordStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsync($"api/portal-admin/records/{id}/status?status={Uri.EscapeDataString(status)}", null, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CreatePortalAdminRecordAsync(UpsertPortalAdminRecordRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/portal-admin/records", request, ct);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id;
    }

    public async Task UpdatePortalAdminRecordAsync(Guid id, UpsertPortalAdminRecordRequest request, CancellationToken ct = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/portal-admin/records/{id}", request, ct);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePortalAdminRecordAsync(Guid id, CancellationToken ct = default)
    {
        var response = await _httpClient.DeleteAsync($"api/portal-admin/records/{id}", ct);
        response.EnsureSuccessStatusCode();
    }

    // -- Account Notes ----------------------------------------
    public Task<PagedResult<AccountNoteDto>?> SearchAccountNotesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AccountNoteDto>>($"api/client/account-notes?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateAccountNoteAsync(CreateAccountNoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/client/account-notes", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    // -- Client & Account -------------------------------------
    public async Task<Guid> CreateAccountAsync(CreateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/accounts", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<AccountDto>?> SearchAccountsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AccountDto>>($"api/accounts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<AccountDto?> GetAccountByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AccountDto>($"api/accounts/{id}", cancellationToken);

    public async Task UpdateAccountAsync(Guid id, UpdateAccountRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/accounts/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAccountAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/accounts/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task EnsureBillingAccountsSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/billing/accounts/ensure-seed?tenantId={tenantId}", null, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<BillingAccountDto>?> SearchBillingAccountsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<BillingAccountDto>>($"api/billing/accounts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateBillingAccountAsync(CreateBillingAccountRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/billing/accounts", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateBillingAccountAsync(Guid accountId, UpdateBillingAccountRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/billing/accounts/{accountId}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteBillingAccountAsync(Guid accountId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/billing/accounts/{accountId}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<IReadOnlyList<ContactDto>?> GetAccountContactsAsync(Guid accountId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<ContactDto>>($"api/accounts/{accountId}/contacts", cancellationToken);

    public async Task<Guid> CreateContactAsync(CreateContactRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/contacts", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<ContactDto?> GetContactByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<ContactDto>($"api/contacts/{id}", cancellationToken);

    public Task<PagedResult<ContactDto>?> SearchContactsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ContactDto>>($"api/contacts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<PagedResult<ContactDto>?> GetContactsByAccountAsync(Guid accountId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ContactDto>>($"api/contacts/by-account/{accountId}", cancellationToken);

    public async Task UpdateContactAsync(Guid id, UpdateContactRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/contacts/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteContactAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/contacts/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Operations -------------------------------------------
    public Task<PagedResult<TaskItemDto>?> SearchTaskItemsAsync(Guid tenantId, string? searchTerm = null, string? stageCode = null, string? statusCode = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<TaskItemDto>>($"api/ops/tasks?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&stageCode={Uri.EscapeDataString(stageCode ?? string.Empty)}&statusCode={Uri.EscapeDataString(statusCode ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<TaskItemDto?> GetTaskItemByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<TaskItemDto>($"api/ops/tasks/{id}", cancellationToken);

    public async Task<Guid> CreateTaskItemAsync(CreateTaskItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ops/tasks", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateTaskItemAsync(Guid id, UpdateTaskItemRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/ops/tasks/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTaskItemAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/ops/tasks/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<AgreementDto>?> SearchAgreementsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AgreementDto>>($"api/agreements?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<AgreementDto?> GetAgreementByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AgreementDto>($"api/agreements/{id}", cancellationToken);

    public async Task<Guid> CreateAgreementAsync(CreateAgreementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/agreements", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateAgreementAsync(Guid id, UpdateAgreementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/agreements/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAgreementAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/agreements/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<EngagementDto>?> SearchEngagementsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<EngagementDto>>($"api/engagements?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateEngagementAsync(CreateEngagementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/engagements", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateEngagementAsync(Guid id, UpdateEngagementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/engagements/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteEngagementAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/engagements/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

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

    public async Task UpdateEngagementMilestoneAsync(Guid id, UpdateEngagementMilestoneRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/ops/milestones/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteEngagementMilestoneAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/ops/milestones/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<TaskTypeDto>?> SearchTaskTypesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<TaskTypeDto>>($"api/ops/task-types?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateTaskTypeAsync(CreateTaskTypeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ops/task-types", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateTaskTypeAsync(Guid id, UpdateTaskTypeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/ops/task-types/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteTaskTypeAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/ops/task-types/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<ServiceIssueDto>?> SearchServiceIssuesAsync(Guid tenantId, Guid? engagementId = null, Guid? accountId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ServiceIssueDto>>($"api/ops/issues?tenantId={tenantId}&engagementId={engagementId}&accountId={accountId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateServiceIssueAsync(CreateServiceIssueRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ops/issues", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateServiceIssueAsync(Guid id, UpdateServiceIssueRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/ops/issues/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteServiceIssueAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/ops/issues/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<AgreementAmendmentDto>?> SearchAgreementAmendmentsAsync(Guid tenantId, Guid? agreementId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AgreementAmendmentDto>>($"api/ops/amendments?tenantId={tenantId}&agreementId={agreementId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateAgreementAmendmentAsync(CreateAgreementAmendmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ops/amendments", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateAgreementAmendmentAsync(Guid id, UpdateAgreementAmendmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/ops/amendments/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteAgreementAmendmentAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/ops/amendments/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
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

    public async Task UpdateServiceRequestAsync(Guid id, UpdateServiceRequestRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/ops/service-requests/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteServiceRequestAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/ops/service-requests/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<OperationalActivityLogDto>?> SearchOperationalActivitiesAsync(Guid tenantId, Guid? accountId = null, Guid? engagementId = null, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<OperationalActivityLogDto>>($"api/ops/activities?tenantId={tenantId}&accountId={accountId}&engagementId={engagementId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<OperationalActivityLogDto?> GetOperationalActivityByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<OperationalActivityLogDto>($"api/ops/activities/{id}", cancellationToken);

    public async Task<Guid> CreateOperationalActivityAsync(CreateOperationalActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ops/activities", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateOperationalActivityAsync(Guid id, UpdateOperationalActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/ops/activities/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteOperationalActivityAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/ops/activities/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<CalendarEventDto>?> SearchCalendarEventsAsync(Guid tenantId, DateTime? startUtc = null, DateTime? endUtc = null, Guid? assignedToUserId = null, string? eventTypeCode = null, string? statusCode = null, string? searchTerm = null, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CalendarEventDto>>($"api/ops/calendar-events?tenantId={tenantId}&startUtc={startUtc:o}&endUtc={endUtc:o}&assignedToUserId={assignedToUserId}&eventTypeCode={Uri.EscapeDataString(eventTypeCode ?? string.Empty)}&statusCode={Uri.EscapeDataString(statusCode ?? string.Empty)}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<CalendarEventDto?> GetCalendarEventByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<CalendarEventDto>($"api/ops/calendar-events/{id}", cancellationToken);

    public async Task<Guid> CreateCalendarEventAsync(CreateCalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/ops/calendar-events", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCalendarEventAsync(Guid id, UpdateCalendarEventRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/ops/calendar-events/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCalendarEventAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/ops/calendar-events/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<CsrWorkbenchDto?> GetCsrWorkbenchAsync(Guid tenantId, Guid? userId = null, bool teamScope = false, string? branchId = null, string? teamId = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/workbench/csr?tenantId={tenantId}&teamScope={teamScope}";
        if (userId.HasValue) url += $"&userId={userId.Value}";
        if (!string.IsNullOrWhiteSpace(branchId)) url += $"&branchId={Uri.EscapeDataString(branchId)}";
        if (!string.IsNullOrWhiteSpace(teamId)) url += $"&teamId={Uri.EscapeDataString(teamId)}";
        return _httpClient.GetFromJsonAsync<CsrWorkbenchDto>(url, cancellationToken);
    }

    public Task<ServiceManagerWorkbenchDto?> GetServiceManagerWorkbenchAsync(Guid tenantId, Guid? userId = null, bool teamScope = true, string? branchId = null, string? teamId = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/workbench/service-manager?tenantId={tenantId}&teamScope={teamScope}";
        if (userId.HasValue) url += $"&userId={userId.Value}";
        if (!string.IsNullOrWhiteSpace(branchId)) url += $"&branchId={Uri.EscapeDataString(branchId)}";
        if (!string.IsNullOrWhiteSpace(teamId)) url += $"&teamId={Uri.EscapeDataString(teamId)}";
        return _httpClient.GetFromJsonAsync<ServiceManagerWorkbenchDto>(url, cancellationToken);
    }

    public async Task AssignServiceManagerWorkbenchItemAsync(Guid tenantId, Guid itemId, Guid assignedToUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/workbench/service-manager/assign?tenantId={tenantId}&itemId={itemId}&assignedToUserId={assignedToUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<AccountingWorkbenchDto?> GetAccountingWorkbenchAsync(Guid tenantId, Guid? userId = null, bool teamScope = false, string? branchId = null, string? teamId = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/workbench/accounting?tenantId={tenantId}&teamScope={teamScope}";
        if (userId.HasValue) url += $"&userId={userId.Value}";
        if (!string.IsNullOrWhiteSpace(branchId)) url += $"&branchId={Uri.EscapeDataString(branchId)}";
        if (!string.IsNullOrWhiteSpace(teamId)) url += $"&teamId={Uri.EscapeDataString(teamId)}";
        return _httpClient.GetFromJsonAsync<AccountingWorkbenchDto>(url, cancellationToken);
    }

    // -- Billing ----------------------------------------------
    public Task<PagedResult<InvoiceDto>?> SearchInvoicesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<InvoiceDto>>($"api/invoices?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<TimeEntryDto>?> SearchTimeEntriesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<TimeEntryDto>>($"api/timeentries?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateTimeEntryAsync(CreateTimeEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/timeentries", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateTimeEntryAsync(Guid id, UpdateTimeEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/timeentries/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteTimeEntryAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/timeentries/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<ExpenseEntryDto>?> SearchExpensesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ExpenseEntryDto>>($"api/expenses?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateExpenseAsync(CreateExpenseEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/expenses", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateExpenseAsync(Guid id, UpdateExpenseEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/expenses/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteExpenseAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/expenses/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<PaymentDto>?> SearchPaymentsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PaymentDto>>($"api/payments?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/payments", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    // -- Billing extended engine -------------------------------
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

    public async Task<Guid> CreateBillingAdjustmentAsync(CreateBillingAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/billing/adjustments", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateBillingAdjustmentAsync(Guid id, UpdateBillingAdjustmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/billing/adjustments/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteBillingAdjustmentAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/billing/adjustments/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<ArAgingSnapshotDto>?> SearchArAgingSnapshotsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ArAgingSnapshotDto>>($"api/billing/ar-aging?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateArAgingSnapshotAsync(CreateArAgingSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/billing/ar-aging", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateArAgingSnapshotAsync(Guid id, UpdateArAgingSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/billing/ar-aging/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteArAgingSnapshotAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/billing/ar-aging/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task<int> SyncArAgingFromInvoicesAsync(Guid tenantId, DateOnly snapshotDate, Guid? createdByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/billing/ar-aging/sync?tenantId={tenantId}&snapshotDate={snapshotDate:yyyy-MM-dd}&createdByUserId={createdByUserId}", null, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<int>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<DelinquencyFlagDto>?> SearchDelinquencyFlagsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DelinquencyFlagDto>>($"api/billing/delinquency?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CollectionsNoteDto>?> SearchCollectionsNotesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CollectionsNoteDto>>($"api/billing/collections?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateCollectionsNoteAsync(CreateCollectionsNoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/billing/collections", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCollectionsNoteAsync(Guid id, UpdateCollectionsNoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/billing/collections/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteCollectionsNoteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/billing/collections/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    // -- Finance ----------------------------------------------
    public Task<PagedResult<GLAccountDto>?> SearchGLAccountsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<GLAccountDto>>($"api/finance/glaccounts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateGLAccountAsync(CreateGLAccountRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/finance/glaccounts", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateGLAccountAsync(Guid id, UpdateGLAccountRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/finance/glaccounts/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<JournalEntryDto>?> SearchJournalEntriesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<JournalEntryDto>>($"api/finance/journalentries?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateJournalEntryAsync(CreateJournalEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/finance/journalentries", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateJournalEntryAsync(Guid id, UpdateJournalEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/finance/journalentries/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<VendorDto>?> SearchVendorsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<VendorDto>>($"api/finance/vendors?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateVendorAsync(CreateVendorRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/finance/vendors", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateVendorAsync(Guid id, UpdateVendorRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/finance/vendors/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<ApInvoiceDto>?> SearchApInvoicesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ApInvoiceDto>>($"api/finance/ap-invoices?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateApInvoiceAsync(CreateApInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/finance/ap-invoices", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateApInvoiceAsync(Guid id, UpdateApInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/finance/ap-invoices/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<ApInvoiceLineDto>?> SearchApInvoiceLinesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ApInvoiceLineDto>>($"api/finance/ap-invoice-lines?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<ApPaymentDto>?> SearchApPaymentsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ApPaymentDto>>($"api/finance/ap-payments?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateApPaymentAsync(CreateApPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/finance/ap-payments", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateApPaymentAsync(Guid id, UpdateApPaymentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/finance/ap-payments/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<AccountingPeriodDto>?> SearchAccountingPeriodsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AccountingPeriodDto>>($"api/finance/accounting-periods?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateAccountingPeriodAsync(CreateAccountingPeriodRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/finance/accounting-periods", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateAccountingPeriodAsync(Guid id, UpdateAccountingPeriodRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/finance/accounting-periods/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<PeriodCloseEntryDto>?> SearchPeriodCloseEntriesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PeriodCloseEntryDto>>($"api/finance/period-close?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreatePeriodCloseEntryAsync(CreatePeriodCloseEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/finance/period-close", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdatePeriodCloseEntryAsync(Guid id, UpdatePeriodCloseEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/finance/period-close/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<DeferredRevenueScheduleDto>?> SearchDeferredRevenueSchedulesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DeferredRevenueScheduleDto>>($"api/finance/deferred-revenue?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateDeferredRevenueScheduleAsync(CreateDeferredRevenueScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/finance/deferred-revenue", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateDeferredRevenueScheduleAsync(Guid id, UpdateDeferredRevenueScheduleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/finance/deferred-revenue/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<DeferredRevenueRecognitionDto>?> SearchDeferredRevenueRecognitionsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DeferredRevenueRecognitionDto>>($"api/finance/deferred-revenue-recognition?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateDeferredRevenueRecognitionAsync(CreateDeferredRevenueRecognitionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/finance/deferred-revenue-recognition", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateDeferredRevenueRecognitionAsync(Guid id, UpdateDeferredRevenueRecognitionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/finance/deferred-revenue-recognition/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<BadDebtEntryDto>?> SearchBadDebtEntriesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<BadDebtEntryDto>>($"api/finance/bad-debt?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CashReceiptEntryDto>?> SearchCashReceiptEntriesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CashReceiptEntryDto>>($"api/finance/cash-receipts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateCashReceiptEntryAsync(CreateCashReceiptEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/finance/cash-receipts", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCashReceiptEntryAsync(Guid id, UpdateCashReceiptEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/finance/cash-receipts/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<TrialBalanceSnapshotDto>?> SearchTrialBalanceSnapshotsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<TrialBalanceSnapshotDto>>($"api/finance/trial-balance?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateTrialBalanceSnapshotAsync(CreateTrialBalanceSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/finance/trial-balance", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateTrialBalanceSnapshotAsync(Guid id, UpdateTrialBalanceSnapshotRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/finance/trial-balance/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<BankReconciliationDto>?> SearchBankReconciliationsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<BankReconciliationDto>>($"api/finance/bank-reconciliation?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateBankReconciliationAsync(CreateBankReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/finance/bank-reconciliation", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateBankReconciliationAsync(Guid id, UpdateBankReconciliationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/finance/bank-reconciliation/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<IReadOnlyList<JournalEntryLineDto>?> GetJournalEntryLinesAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<JournalEntryLineDto>>($"api/finance/journal-entry-lines?journalEntryId={journalEntryId}", cancellationToken);

    // -- Commission -------------------------------------------
    public Task<PagedResult<CommissionPlanDto>?> SearchCommissionPlansAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionPlanDto>>($"api/commissionplans?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateCommissionPlanAsync(CreateCommissionPlanRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissionplans", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCommissionPlanAsync(Guid id, UpdateCommissionPlanRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commissionplans/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<CommissionPayeeDto>?> SearchCommissionPayeesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionPayeeDto>>($"api/commissions/payees?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task EnsureCommissionSeedAsync(Guid tenantId, Guid? createdByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/commissions/seed?tenantId={tenantId}&createdByUserId={createdByUserId}", null, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task<Guid> CreateCommissionPayeeAsync(CreateCommissionPayeeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissions/payees", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCommissionPayeeAsync(Guid id, UpdateCommissionPayeeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commissions/payees/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<CommissionTransactionDto>?> SearchCommissionTransactionsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionTransactionDto>>($"api/commissions/transactions?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<IReadOnlyList<CommissionLedgerRowDto>?> SearchCommissionLedgerAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<CommissionLedgerRowDto>>($"api/commissions/ledger?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateCommissionTransactionAsync(CreateCommissionTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissions/transactions", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCommissionTransactionAsync(Guid id, UpdateCommissionTransactionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commissions/transactions/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<CommissionPayoutDto>?> SearchCommissionPayoutsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionPayoutDto>>($"api/commissions/payouts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateCommissionPayoutAsync(CreateCommissionPayoutRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissions/payouts", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCommissionPayoutAsync(Guid id, UpdateCommissionPayoutRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commissions/payouts/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<CommissionPayoutStatementDto>?> SearchCommissionPayoutStatementsAsync(Guid tenantId, string? searchTerm = null, string? statusCode = null, Guid? payeeId = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionPayoutStatementDto>>($"api/commissions/payout-statements?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&statusCode={Uri.EscapeDataString(statusCode ?? string.Empty)}&payeeId={payeeId}", cancellationToken);

    public async Task EnsureCommissionPayoutStatementsSeedAsync(Guid tenantId, Guid? createdByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/commissions/payout-statements/seed?tenantId={tenantId}&createdByUserId={createdByUserId}", null, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task<Guid> CreateCommissionPayoutStatementAsync(CreateCommissionPayoutStatementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissions/payout-statements", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCommissionPayoutStatementAsync(Guid id, UpdateCommissionPayoutStatementRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commissions/payout-statements/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task<IReadOnlyList<Guid>?> GenerateCommissionPayoutStatementsAsync(GenerateCommissionPayoutStatementsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissions/payout-statements/generate", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<IReadOnlyList<Guid>>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<CommissionPlanVersionDto>?> SearchCommissionPlanVersionsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionPlanVersionDto>>($"api/commissions/plan-versions?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CommissionSplitRuleDto>?> SearchCommissionSplitRulesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionSplitRuleDto>>($"api/commissions/split-rules?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task EnsureCommissionSplitRulesSeedAsync(Guid tenantId, Guid? createdByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/commissions/split-rules/seed?tenantId={tenantId}&createdByUserId={createdByUserId}", null, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task<Guid> CreateCommissionSplitRuleAsync(CreateCommissionSplitRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissions/split-rules", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCommissionSplitRuleAsync(Guid id, UpdateCommissionSplitRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commissions/split-rules/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<CommissionCalculationResultDto>?> SearchCommissionCalculationResultsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionCalculationResultDto>>($"api/commissions/calculations?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<CommissionClawbackDto>?> SearchCommissionClawbacksAsync(Guid tenantId, string? searchTerm = null, string? statusCode = null, string? reasonCode = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionClawbackDto>>($"api/commissions/clawbacks?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&statusCode={Uri.EscapeDataString(statusCode ?? string.Empty)}&reasonCode={Uri.EscapeDataString(reasonCode ?? string.Empty)}", cancellationToken);

    public async Task EnsureCommissionClawbacksSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/commissions/clawbacks/seed?tenantId={tenantId}", null, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task<Guid> CreateCommissionClawbackAsync(CreateCommissionClawbackRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissions/clawbacks", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCommissionClawbackAsync(Guid id, UpdateCommissionClawbackRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commissions/clawbacks/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<CommissionPayoutBatchDto>?> SearchCommissionPayoutBatchesAsync(Guid tenantId, string? searchTerm = null, string? statusCode = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionPayoutBatchDto>>($"api/commissions/payout-batches?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&statusCode={Uri.EscapeDataString(statusCode ?? string.Empty)}", cancellationToken);

    public async Task EnsureCommissionPayoutBatchesSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/commissions/payout-batches/seed?tenantId={tenantId}", null, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task<Guid> CreateCommissionPayoutBatchAsync(CreateCommissionPayoutBatchRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissions/payout-batches", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCommissionPayoutBatchAsync(Guid id, UpdateCommissionPayoutBatchRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commissions/payout-batches/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<CommissionExceptionDto>?> SearchCommissionExceptionsAsync(Guid tenantId, string? searchTerm = null, string? statusCode = null, string? severityCode = null, string? typeCode = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionExceptionDto>>($"api/commissions/exceptions?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&statusCode={Uri.EscapeDataString(statusCode ?? string.Empty)}&severityCode={Uri.EscapeDataString(severityCode ?? string.Empty)}&typeCode={Uri.EscapeDataString(typeCode ?? string.Empty)}", cancellationToken);

    public async Task EnsureCommissionExceptionsSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/commissions/exceptions/seed?tenantId={tenantId}", null, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task<Guid> CreateCommissionExceptionAsync(CreateCommissionExceptionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissions/exceptions", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCommissionExceptionAsync(Guid id, UpdateCommissionExceptionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commissions/exceptions/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<CommissionForecastDto>?> SearchCommissionForecastsAsync(Guid tenantId, string? searchTerm = null, string? statusCode = null, string? scenarioCode = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionForecastDto>>($"api/commissions/forecasts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&statusCode={Uri.EscapeDataString(statusCode ?? string.Empty)}&scenarioCode={Uri.EscapeDataString(scenarioCode ?? string.Empty)}", cancellationToken);

    public async Task EnsureCommissionForecastsSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/commissions/forecasts/seed?tenantId={tenantId}", null, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task<Guid> CreateCommissionForecastAsync(CreateCommissionForecastRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissions/forecasts", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCommissionForecastAsync(Guid id, UpdateCommissionForecastRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commissions/forecasts/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<CommissionPlannerScenarioDto>?> SearchCommissionPlannerScenariosAsync(Guid tenantId, string? searchTerm = null, string? statusCode = null, string? scenarioTypeCode = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionPlannerScenarioDto>>($"api/commissions/planner-scenarios?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&statusCode={Uri.EscapeDataString(statusCode ?? string.Empty)}&scenarioTypeCode={Uri.EscapeDataString(scenarioTypeCode ?? string.Empty)}", cancellationToken);

    public async Task EnsureCommissionPlannerScenariosSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/commissions/planner-scenarios/seed?tenantId={tenantId}", null, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task<Guid> CreateCommissionPlannerScenarioAsync(CreateCommissionPlannerScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissions/planner-scenarios", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCommissionPlannerScenarioAsync(Guid id, UpdateCommissionPlannerScenarioRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commissions/planner-scenarios/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<CommissionDisputeDto>?> SearchCommissionDisputesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionDisputeDto>>($"api/commissions/disputes?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateCommissionDisputeAsync(CreateCommissionDisputeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissions/disputes", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCommissionDisputeAsync(Guid id, UpdateCommissionDisputeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commissions/disputes/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<CommissionAccrualEntryDto>?> SearchCommissionAccrualEntriesAsync(Guid tenantId, string? searchTerm = null, string? statusCode = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommissionAccrualEntryDto>>($"api/commissions/accruals?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&statusCode={Uri.EscapeDataString(statusCode ?? string.Empty)}", cancellationToken);

    public async Task EnsureCommissionAccrualEntriesSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/commissions/accruals/seed?tenantId={tenantId}", null, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task<Guid> CreateCommissionAccrualEntryAsync(CreateCommissionAccrualEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commissions/accruals", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateCommissionAccrualEntryAsync(Guid id, UpdateCommissionAccrualEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commissions/accruals/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    // -- Submissions ------------------------------------------
    public Task<PagedResult<SubmissionDto>?> SearchSubmissionsAsync(Guid tenantId, string? searchTerm = null, string? status = null, string? lineOfBusiness = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<SubmissionDto>>($"api/submissions?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&status={Uri.EscapeDataString(status ?? string.Empty)}&lineOfBusiness={Uri.EscapeDataString(lineOfBusiness ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<PagedResult<PolicyRegisterDto>?> SearchPolicyRegisterAsync(Guid tenantId, string? searchTerm = null, string? status = null, string? lineOfBusiness = null, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PolicyRegisterDto>>($"api/submissions/policies?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&status={Uri.EscapeDataString(status ?? string.Empty)}&lineOfBusiness={Uri.EscapeDataString(lineOfBusiness ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<List<SubmissionReferenceOptionDto>?> GetSubmissionReferenceOptionsAsync(Guid tenantId, string? optionGroup = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<SubmissionReferenceOptionDto>>($"api/submissions/reference-options?tenantId={tenantId}&optionGroup={Uri.EscapeDataString(optionGroup ?? string.Empty)}", cancellationToken);

    public Task<SubmissionDto?> GetSubmissionByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<SubmissionDto>($"api/submissions/{id}", cancellationToken);

    public async Task<Guid> CreateSubmissionAsync(CreateSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/submissions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task<SubmissionActionResult> SubmitSubmissionToMarketAsync(Guid submissionId, SubmitSubmissionToMarketRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/submissions/{submissionId}/submit-to-market", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<SubmissionActionResult>(cancellationToken: cancellationToken))!;
    }

    public async Task<SubmissionActionResult> RequestSubmissionQuoteAsync(Guid submissionId, RequestSubmissionQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/submissions/{submissionId}/request-quote", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<SubmissionActionResult>(cancellationToken: cancellationToken))!;
    }

    public async Task<SubmissionActionResult> CopySubmissionAsync(Guid submissionId, CopySubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/submissions/{submissionId}/copy", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<SubmissionActionResult>(cancellationToken: cancellationToken))!;
    }

    public async Task<SubmissionActionResult> DeclineSubmissionAsync(Guid submissionId, DeclineSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/submissions/{submissionId}/decline", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<SubmissionActionResult>(cancellationToken: cancellationToken))!;
    }

    public async Task<SubmissionActionResult> CreatePolicyFromSubmissionAsync(Guid submissionId, CreatePolicyFromSubmissionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/submissions/{submissionId}/create-policy", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return (await response.Content.ReadFromJsonAsync<SubmissionActionResult>(cancellationToken: cancellationToken))!;
    }

    public Task<IReadOnlyList<QuoteComparisonDto>?> GetSubmissionQuoteComparisonAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<QuoteComparisonDto>>($"api/submissions/{submissionId}/quotes", cancellationToken);

    public Task<IReadOnlyList<SubmissionMarketDto>?> GetSubmissionMarketsAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<SubmissionMarketDto>>($"api/submissions/{submissionId}/markets", cancellationToken);

    public Task<IReadOnlyList<SubmissionMarketDto>?> GetSubmissionMarketSuggestionsAsync(Guid submissionId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<SubmissionMarketDto>>($"api/submissions/{submissionId}/markets/suggestions", cancellationToken);

    public async Task<Guid> AddSubmissionMarketAsync(Guid submissionId, AddSubmissionMarketRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/submissions/{submissionId}/markets", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateSubmissionMarketStatusAsync(Guid marketId, UpdateSubmissionMarketStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/submissions/markets/{marketId}/status", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task RemoveSubmissionMarketAsync(Guid marketId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/submissions/markets/{marketId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task<Guid> GenerateProposalAsync(GenerateProposalRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/proposals", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task<Guid> InitiateWorkflowForSubmissionAsync(Guid submissionId, Guid tenantId, Guid? workflowDefinitionId = null, CancellationToken cancellationToken = default)
        => await InitiateWorkflowAsync(tenantId, "Submission", submissionId, workflowDefinitionId, cancellationToken: cancellationToken);

    public async Task<Guid> InitiateWorkflowAsync(Guid tenantId, string targetEntityName, Guid targetEntityId, Guid? workflowDefinitionId = null, Guid? userId = null, string? notes = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/workflow/initiate", new { TenantId = tenantId, TargetEntityName = targetEntityName, TargetEntityId = targetEntityId, WorkflowDefinitionId = workflowDefinitionId, UserId = userId, Notes = notes }, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    // -- Workflow & Approval ----------------------------------
    public Task<PagedResult<WorkflowInstanceDto>?> SearchWorkflowAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<WorkflowInstanceDto>>($"api/workflow?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<WorkflowApprovalHistoryDto>?> GetWorkflowHistoryAsync(Guid tenantId, Guid workflowInstanceId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<WorkflowApprovalHistoryDto>>($"api/audit/approval-history?tenantId={tenantId}&workflowInstanceId={workflowInstanceId}", cancellationToken);

    public async Task ApproveWorkflowStepAsync(Guid workflowInstanceId, string? notes, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/workflow/{workflowInstanceId}/approve", new { Notes = notes }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RejectWorkflowStepAsync(Guid workflowInstanceId, string reason, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/workflow/{workflowInstanceId}/reject", new { Reason = reason }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReturnWorkflowStepAsync(Guid workflowInstanceId, string reason, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/workflow/{workflowInstanceId}/return", new { Reason = reason }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Documents --------------------------------------------
    public Task<PagedResult<DocumentDto>?> SearchDocumentsAsync(Guid tenantId, string? categoryCode = null, string? entityName = null, Guid? entityId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<DocumentDto>>($"api/documents?tenantId={tenantId}&categoryCode={Uri.EscapeDataString(categoryCode ?? string.Empty)}&entityName={Uri.EscapeDataString(entityName ?? string.Empty)}&entityId={entityId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<DocumentDto?> GetDocumentByIdAsync(Guid documentId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<DocumentDto>($"api/documents/{documentId}", cancellationToken);

    public string GetDocumentDownloadUrl(Guid documentId)
        => new Uri(_httpClient.BaseAddress!, $"api/documents/{documentId}/download").ToString();

    public async Task<Guid> CreateDocumentAsync(CreateDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/documents", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> UploadDocumentAsync(UploadDocumentRequest request, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        AddFormValue(content, nameof(request.TenantId), request.TenantId);
        AddFormValue(content, nameof(request.DocumentTypeCode), request.DocumentTypeCode);
        AddFormValue(content, nameof(request.CategoryCode), request.CategoryCode);
        AddFormValue(content, nameof(request.FileName), request.FileName);
        AddFormValue(content, nameof(request.EntityName), request.EntityName);
        AddFormValue(content, nameof(request.EntityId), request.EntityId);
        AddFormValue(content, nameof(request.Description), request.Description);
        AddFormValue(content, nameof(request.Tags), request.Tags);
        AddFormValue(content, nameof(request.RetentionDate), request.RetentionDate);
        AddFormValue(content, nameof(request.UploadedByName), request.UploadedByName);
        AddFormValue(content, nameof(request.CreatedByUserId), request.CreatedByUserId);

        content.Add(new StreamContent(request.Content), "File", request.FileName ?? request.OriginalFileName);
        var response = await _httpClient.PostAsync("api/documents/upload", content, cancellationToken);
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

    public async Task<Guid> UploadDocumentVersionAsync(Guid documentId, UploadDocumentVersionRequest request, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        AddFormValue(content, nameof(request.FileName), request.FileName);
        AddFormValue(content, nameof(request.ChangeNotes), request.ChangeNotes);
        AddFormValue(content, nameof(request.CreatedByUserId), request.CreatedByUserId);

        content.Add(new StreamContent(request.Content), "File", request.FileName ?? request.OriginalFileName);
        var response = await _httpClient.PostAsync($"api/documents/{documentId}/versions/upload", content, cancellationToken);
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

    private static void AddFormValue(MultipartFormDataContent content, string name, object? value)
    {
        if (value is null) return;
        content.Add(new StringContent(value switch
        {
            DateOnly date => date.ToString("O"),
            _ => value.ToString() ?? string.Empty
        }), name);
    }

    public sealed class UploadDocumentRequest
    {
        public Guid TenantId { get; set; }
        public string DocumentTypeCode { get; set; } = string.Empty;
        public string CategoryCode { get; set; } = "Other";
        public string? FileName { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string? EntityName { get; set; }
        public Guid? EntityId { get; set; }
        public string? Description { get; set; }
        public string? Tags { get; set; }
        public DateOnly? RetentionDate { get; set; }
        public string? UploadedByName { get; set; }
        public Guid? CreatedByUserId { get; set; }
        public Stream Content { get; set; } = Stream.Null;
    }

    public sealed class UploadDocumentVersionRequest
    {
        public string? FileName { get; set; }
        public string OriginalFileName { get; set; } = string.Empty;
        public string? ChangeNotes { get; set; }
        public Guid? CreatedByUserId { get; set; }
        public Stream Content { get; set; } = Stream.Null;
    }

    // -- E-Sign -----------------------------------------------
    public Task<IReadOnlyList<ESignRequestDto>?> GetESignRequestsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<ESignRequestDto>>($"api/esign?tenantId={tenantId}", cancellationToken);

    public async Task<Guid> SendESignRequestAsync(SendESignRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/esign", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task VoidESignRequestAsync(Guid eSignRequestId, string? voidReason, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/esign/{eSignRequestId}/void", new VoidESignRequest(eSignRequestId, voidReason), cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RemindESignRequestAsync(Guid eSignRequestId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/esign/{eSignRequestId}/remind", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- ACORD Forms -------------------------------------------
    public Task<IReadOnlyList<AcordFormDto>?> GetAcordFormsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<AcordFormDto>>($"api/acordforms?tenantId={tenantId}", cancellationToken);

    public Task<AcordFormDto?> GetAcordFormByIdAsync(Guid acordFormId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AcordFormDto>($"api/acordforms/{acordFormId}", cancellationToken);

    public async Task<Guid> CreateAcordFormAsync(CreateAcordFormRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/acordforms", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateAcordFormStatusAsync(UpdateAcordFormStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/acordforms/{request.AcordFormId}/status", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task PrefillAcordFormAsync(PrefillAcordFormRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/acordforms/{request.AcordFormId}/prefill", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Document Exceptions -----------------------------------
    public Task<IReadOnlyList<DocumentExceptionDto>?> GetDocumentExceptionsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<DocumentExceptionDto>>($"api/document-exceptions?tenantId={tenantId}", cancellationToken);

    public Task<DocumentExceptionDto?> GetDocumentExceptionByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<DocumentExceptionDto>($"api/document-exceptions/{id}", cancellationToken);

    public async Task<Guid> CreateDocumentExceptionAsync(CreateDocumentExceptionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/document-exceptions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task ClassifyDocumentExceptionAsync(ClassifyDocumentExceptionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/document-exceptions/{request.DocumentExceptionId}/classify", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateDocumentExceptionStatusAsync(UpdateDocumentExceptionStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/document-exceptions/{request.DocumentExceptionId}/status", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Document Packets --------------------------------------
    public Task<IReadOnlyList<DocumentPacketDto>?> GetDocumentPacketsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<DocumentPacketDto>>($"api/document-packets?tenantId={tenantId}", cancellationToken);

    public Task<DocumentPacketDto?> GetDocumentPacketByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<DocumentPacketDto>($"api/document-packets/{id}", cancellationToken);

    public async Task<Guid> CreateDocumentPacketAsync(CreateDocumentPacketRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/document-packets", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task<Guid> AddDocumentPacketDocumentAsync(AddDocumentPacketDocumentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/document-packets/{request.DocumentPacketId}/documents", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task RemoveDocumentPacketDocumentAsync(Guid packetDocumentId, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/document-packets/documents/{packetDocumentId}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReorderDocumentPacketDocumentsAsync(ReorderDocumentPacketDocumentsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/document-packets/{request.DocumentPacketId}/documents/reorder", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendDocumentPacketAsync(SendDocumentPacketRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/document-packets/{request.DocumentPacketId}/send", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateDocumentPacketStatusAsync(UpdateDocumentPacketStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/document-packets/{request.DocumentPacketId}/status", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteDocumentPacketAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/document-packets/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Communications — Inbox -------------------------------
    public Task<IReadOnlyList<MessageThreadDto>?> GetMessageThreadsAsync(Guid tenantId, string? channel = null, string? status = null, string? assignedTo = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<MessageThreadDto>>($"api/messages?tenantId={tenantId}&channel={Uri.EscapeDataString(channel ?? string.Empty)}&status={Uri.EscapeDataString(status ?? string.Empty)}&assignedTo={Uri.EscapeDataString(assignedTo ?? string.Empty)}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<MessageThreadDto?> GetMessageThreadByIdAsync(Guid threadId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<MessageThreadDto>($"api/messages/{threadId}", cancellationToken);

    public async Task<Guid> SendMessageAsync(SendMessageRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/messages", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task ReplyMessageAsync(Guid threadId, ReplyMessageRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/messages/{threadId}/reply", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AssignThreadAsync(Guid threadId, AssignThreadRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/messages/{threadId}/assign", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task EscalateThreadAsync(Guid threadId, EscalateThreadRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/messages/{threadId}/escalate", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResolveThreadAsync(Guid threadId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/messages/{threadId}/resolve", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task MarkThreadReadAsync(Guid threadId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/messages/{threadId}/read", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Communications — Templates ---------------------------
    public Task<IReadOnlyList<CommTemplateDto>?> GetCommTemplatesAsync(Guid tenantId, string? channel = null, string? category = null, string? status = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<CommTemplateDto>>($"api/commtemplates?tenantId={tenantId}&channel={Uri.EscapeDataString(channel ?? string.Empty)}&category={Uri.EscapeDataString(category ?? string.Empty)}&status={Uri.EscapeDataString(status ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateCommTemplateAsync(CreateCommTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/commtemplates", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (Guid.TryParse(content.Trim('"'), out var id))
        {
            return id;
        }

        var result = System.Text.Json.JsonSerializer.Deserialize<IdResult>(content, new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));
        return result!.Id;
    }

    public async Task UpdateCommTemplateAsync(Guid templateId, UpdateCommTemplateRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/commtemplates/{templateId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task IncrementCommTemplateUsageAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/commtemplates/{templateId}/use", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCommTemplateAsync(Guid templateId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/commtemplates/{templateId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<CommunicationCampaignDto>?> SearchCommunicationCampaignsAsync(Guid tenantId, string? searchTerm = null, string? status = null, string? type = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommunicationCampaignDto>>($"api/communications/campaigns?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&status={Uri.EscapeDataString(status ?? string.Empty)}&type={Uri.EscapeDataString(type ?? string.Empty)}", cancellationToken);

    public async Task EnsureCommunicationCampaignSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/communications/campaigns/seed?tenantId={tenantId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<CommunicationCampaignBuilderDataDto?> GetCommunicationCampaignBuilderAsync(Guid campaignId, Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<CommunicationCampaignBuilderDataDto>($"api/communications/campaigns/{campaignId}/builder?tenantId={tenantId}", cancellationToken);

    public Task<CommunicationCampaignBuilderDataDto?> GetCommunicationCampaignBuilderWorkspaceAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<CommunicationCampaignBuilderDataDto>($"api/communications/campaigns/builder-workspace?tenantId={tenantId}", cancellationToken);

    public async Task<Guid> CreateCommunicationCampaignAsync(CommunicationCampaignDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/communications/campaigns", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateCommunicationCampaignAsync(Guid campaignId, CommunicationCampaignDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/communications/campaigns/{campaignId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<CommunicationAppointmentDto>?> SearchCommunicationAppointmentsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommunicationAppointmentDto>>($"api/communications/appointments?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateCommunicationAppointmentAsync(UpsertCommunicationAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/communications/appointments", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateCommunicationAppointmentAsync(Guid appointmentId, UpsertCommunicationAppointmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/communications/appointments/{appointmentId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task LogCommunicationAppointmentOutcomeAsync(Guid tenantId, Guid appointmentId, AppointmentOutcomeRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/communications/appointments/{appointmentId}/outcome?tenantId={tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateCommunicationAppointmentStatusAsync(Guid tenantId, Guid appointmentId, AppointmentStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/communications/appointments/{appointmentId}/status?tenantId={tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendCommunicationAppointmentReminderAsync(Guid tenantId, Guid appointmentId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/communications/appointments/{appointmentId}/reminder?tenantId={tenantId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<CommunicationOutreachContactDto>?> SearchCommunicationOutreachAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CommunicationOutreachContactDto>>($"api/communications/outreach?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateCommunicationOutreachAsync(UpsertCommunicationOutreachRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/communications/outreach", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateCommunicationOutreachAsync(Guid outreachContactId, UpsertCommunicationOutreachRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/communications/outreach/{outreachContactId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task LogCommunicationOutreachAttemptAsync(Guid tenantId, Guid outreachContactId, OutreachLogAttemptRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/communications/outreach/{outreachContactId}/log?tenantId={tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AssignCommunicationOutreachAsync(Guid tenantId, OutreachAssignRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/communications/outreach/assign?tenantId={tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateCommunicationOutreachStatusAsync(Guid tenantId, Guid outreachContactId, OutreachStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/communications/outreach/{outreachContactId}/status?tenantId={tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendCommunicationOutreachBatchSmsAsync(Guid tenantId, OutreachBatchSmsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/communications/outreach/batch-sms?tenantId={tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCommunicationOutreachAsync(Guid tenantId, Guid outreachContactId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/communications/outreach/{outreachContactId}?tenantId={tenantId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Audit ------------------------------------------------
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
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
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

    // -- Assistant --------------------------------------------
    public Task<PagedResult<AssistantConversationDto>?> SearchAssistantAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AssistantConversationDto>>($"api/assistant?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    // -- Platform Core engines --------------------------------
    public Task<PagedResult<TenantBrandingDto>?> SearchTenantBrandingAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<TenantBrandingDto>>($"api/platform/branding?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<TenantBrandingDto?> GetTenantBrandingAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<TenantBrandingDto>($"api/platform/branding/tenant/{tenantId}", cancellationToken);

    public async Task UpdateTenantBrandingAsync(Guid tenantId, UpdateTenantBrandingRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/platform/branding/tenant/{tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetTenantBrandingToDefaultsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/platform/branding/tenant/{tenantId}/reset", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<NotificationDto>?> SearchNotificationsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<NotificationDto>>($"api/notifications?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<NotificationDto?> GetNotificationByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<NotificationDto>($"api/notifications/{id}", cancellationToken);

    public async Task<Guid> CreateNotificationAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/notifications", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (Guid.TryParse(content.Trim('"'), out var id))
        {
            return id;
        }

        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task SetNotificationReadAsync(Guid id, bool isRead, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/notifications/{id}/read?isRead={isRead}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetNotificationStatusAsync(Guid id, string statusCode, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/notifications/{id}/status?statusCode={Uri.EscapeDataString(statusCode)}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetNotificationStatusAsync(Guid id, NotificationStatusRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/notifications/{id}/status", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RetryNotificationAsync(Guid id, NotificationRetryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/notifications/{id}/retry", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task MarkAllNotificationsReadAsync(Guid tenantId, Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/notifications/mark-all-read?tenantId={tenantId}&recipientUserId={recipientUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteNotificationAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/notifications/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteReadNotificationsAsync(Guid tenantId, Guid recipientUserId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/notifications/read?tenantId={tenantId}&recipientUserId={recipientUserId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<NotificationTemplateDto>?> SearchNotificationTemplatesAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<NotificationTemplateDto>>($"api/notifications/templates?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<ReportDefinitionDto>?> SearchReportDefinitionsAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ReportDefinitionDto>>($"api/reports/definitions?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<ReportExecutionDto>?> SearchReportExecutionsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ReportExecutionDto>>($"api/reports/executions?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<ReportScheduleDto>?> SearchReportSchedulesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ReportScheduleDto>>($"api/reports/schedules?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> RunReportAsync(Guid reportDefinitionId, RunReportRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/reports/definitions/{reportDefinitionId}/run", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task<ReportDownloadFile> DownloadReportExcelAsync(Guid reportDefinitionId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.GetAsync($"api/reports/definitions/{reportDefinitionId}/download?tenantId={tenantId}", cancellationToken);
        response.EnsureSuccessStatusCode();
        return new ReportDownloadFile(GetFileName(response, "report-export.xls"), await response.Content.ReadAsByteArrayAsync(cancellationToken));
    }

    public Task<ReportPreviewDto?> GetReportPreviewAsync(Guid reportDefinitionId, Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<ReportPreviewDto>($"api/reports/definitions/{reportDefinitionId}/preview?tenantId={tenantId}", cancellationToken);

    public async Task<ReportDownloadFile> DownloadReportsExcelAsync(DownloadReportsRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/reports/definitions/download", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return new ReportDownloadFile(GetFileName(response, "reports-export.xls"), await response.Content.ReadAsByteArrayAsync(cancellationToken));
    }

    private static string GetFileName(HttpResponseMessage response, string fallback)
        => response.Content.Headers.ContentDisposition?.FileNameStar?.Trim('"')
           ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
           ?? fallback;

    public async Task<Guid> ScheduleReportAsync(ScheduleReportRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/reports/schedules", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task SetReportScheduleStatusAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/reports/schedules/{id}/status?isActive={isActive}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteReportScheduleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/reports/schedules/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<MarketingEmailBlastDto>?> SearchMarketingEmailBlastsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<MarketingEmailBlastDto>>($"api/marketing/email-blasts?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task EnsureMarketingEmailBlastSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/email-blasts/seed?tenantId={tenantId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CreateMarketingEmailBlastAsync(MarketingEmailBlastDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/marketing/email-blasts", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateMarketingEmailBlastAsync(Guid id, MarketingEmailBlastDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/marketing/email-blasts/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SendMarketingEmailBlastAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/email-blasts/{id}/send", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task PauseMarketingEmailBlastAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/email-blasts/{id}/pause", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResumeMarketingEmailBlastAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/email-blasts/{id}/resume", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DuplicateMarketingEmailBlastAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/email-blasts/{id}/duplicate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ArchiveMarketingEmailBlastAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/email-blasts/{id}/archive", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<MarketingLandingPageDto>?> SearchMarketingLandingPagesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<MarketingLandingPageDto>>($"api/marketing/landing-pages?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task EnsureMarketingLandingPageSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/landing-pages/seed?tenantId={tenantId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CreateMarketingLandingPageAsync(MarketingLandingPageDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/marketing/landing-pages", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateMarketingLandingPageAsync(Guid id, MarketingLandingPageDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/marketing/landing-pages/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task PublishMarketingLandingPageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/landing-pages/{id}/publish", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UnpublishMarketingLandingPageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/landing-pages/{id}/unpublish", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DuplicateMarketingLandingPageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/landing-pages/{id}/duplicate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ArchiveMarketingLandingPageAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/landing-pages/{id}/archive", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<MarketingAnalyticsResult?> SearchMarketingAnalyticsAsync(Guid tenantId, string? period = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<MarketingAnalyticsResult>($"api/marketing/analytics?tenantId={tenantId}&period={Uri.EscapeDataString(period ?? string.Empty)}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task EnsureMarketingAnalyticsSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/analytics/seed?tenantId={tenantId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CreateMarketingAnalyticsMetricAsync(MarketingAnalyticsMetricDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/marketing/analytics", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateMarketingAnalyticsMetricAsync(Guid id, MarketingAnalyticsMetricDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/marketing/analytics/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> DuplicateMarketingAnalyticsMetricAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/analytics/{id}/duplicate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task RecalculateMarketingAnalyticsMetricAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/analytics/{id}/recalculate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ArchiveMarketingAnalyticsMetricAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/analytics/{id}/archive", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<MarketingSegmentDto>?> SearchMarketingSegmentsAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<MarketingSegmentDto>>($"api/marketing/segments?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task EnsureMarketingSegmentSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/segments/seed?tenantId={tenantId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CreateMarketingSegmentAsync(MarketingSegmentDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/marketing/segments", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateMarketingSegmentAsync(Guid id, MarketingSegmentDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/marketing/segments/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DuplicateMarketingSegmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/segments/{id}/duplicate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RecalculateMarketingSegmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/segments/{id}/recalculate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ArchiveMarketingSegmentAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/segments/{id}/archive", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<MarketingCrossSellOpportunityDto>?> SearchMarketingCrossSellAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<MarketingCrossSellOpportunityDto>>($"api/marketing/cross-sell?tenantId={tenantId}", cancellationToken);

    public Task<MarketingCrossSellOpportunityDto?> GetMarketingCrossSellAsync(Guid tenantId, Guid crossSellKey, string? accountName = null, CancellationToken cancellationToken = default)
    {
        var url = $"api/marketing/cross-sell/{crossSellKey}?tenantId={tenantId}";
        if (!string.IsNullOrWhiteSpace(accountName)) url += $"&accountName={Uri.EscapeDataString(accountName)}";
        return _httpClient.GetFromJsonAsync<MarketingCrossSellOpportunityDto>(url, cancellationToken);
    }

    public async Task EnsureMarketingCrossSellSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/cross-sell/seed?tenantId={tenantId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CreateMarketingCrossSellAsync(MarketingCrossSellOpportunityDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/marketing/cross-sell", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateMarketingCrossSellAsync(Guid id, MarketingCrossSellOpportunityDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/marketing/cross-sell/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> DuplicateMarketingCrossSellAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/cross-sell/{id}/duplicate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task RescoreMarketingCrossSellAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/cross-sell/{id}/rescore", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateMarketingCrossSellStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/marketing/cross-sell/{id}/status", new { Status = status }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DismissMarketingCrossSellAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/cross-sell/{id}/dismiss", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<MarketingWinBackDto>?> SearchMarketingWinBackAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<MarketingWinBackDto>>($"api/marketing/win-back?tenantId={tenantId}", cancellationToken);

    public async Task EnsureMarketingWinBackSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/win-back/seed?tenantId={tenantId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CreateMarketingWinBackAsync(MarketingWinBackDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/marketing/win-back", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateMarketingWinBackAsync(Guid id, MarketingWinBackDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/marketing/win-back/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> DuplicateMarketingWinBackAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/win-back/{id}/duplicate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task ArchiveMarketingWinBackAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/win-back/{id}/archive", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateMarketingWinBackStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/marketing/win-back/{id}/status", new { Status = status }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<MarketingReferralDto>?> SearchMarketingReferralsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<MarketingReferralDto>>($"api/marketing/referrals?tenantId={tenantId}", cancellationToken);

    public async Task EnsureMarketingReferralSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/referrals/seed?tenantId={tenantId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CreateMarketingReferralAsync(MarketingReferralDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/marketing/referrals", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateMarketingReferralAsync(Guid id, MarketingReferralDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/marketing/referrals/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> DuplicateMarketingReferralAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/referrals/{id}/duplicate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task ArchiveMarketingReferralAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/referrals/{id}/archive", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UpdateMarketingReferralStatusAsync(Guid id, string status, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/marketing/referrals/{id}/status", new { Status = status }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<MarketingReviewsResult?> SearchMarketingReviewsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<MarketingReviewsResult>($"api/marketing/reviews?tenantId={tenantId}", cancellationToken);

    public async Task EnsureMarketingReviewSeedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/reviews/seed?tenantId={tenantId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CreateMarketingReviewAsync(MarketingReviewDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/marketing/reviews", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateMarketingReviewAsync(Guid id, MarketingReviewDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/marketing/reviews/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ReplyMarketingReviewAsync(Guid id, string responseText, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/marketing/reviews/{id}/reply", new { Response = responseText }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ArchiveMarketingReviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/reviews/{id}/archive", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CreateMarketingReviewRequestAsync(MarketingReviewRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/marketing/reviews/requests", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateMarketingReviewRequestAsync(Guid id, MarketingReviewRequestDto request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/marketing/reviews/requests/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CompleteMarketingReviewRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/reviews/requests/{id}/complete", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> DuplicateMarketingReviewRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/reviews/requests/{id}/duplicate", null, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task ArchiveMarketingReviewRequestAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/marketing/reviews/requests/{id}/archive", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<ConfigurationSettingDto>?> SearchConfigurationSettingsAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ConfigurationSettingDto>>($"api/platform/configuration?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<SupportedLocaleDto>?> SearchSupportedLocalesAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<SupportedLocaleDto>>($"api/platform/locales?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<WorkflowDefinitionDto>?> SearchWorkflowDefinitionsAsync(string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<WorkflowDefinitionDto>>($"api/platform/workflow-definitions?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<PagedResult<UserSessionDto>?> SearchUserSessionsAsync(Guid tenantId, Guid? userId = null, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<UserSessionDto>>($"api/platform/sessions?tenantId={tenantId}&userId={userId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task RevokeUserSessionAsync(Guid sessionId, string? reason = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/platform/sessions/{sessionId}/revoke?reason={Uri.EscapeDataString(reason ?? string.Empty)}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RevokeAllUserSessionsAsync(Guid tenantId, Guid? userId = null, string? reason = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/platform/sessions/revoke-all?tenantId={tenantId}&userId={userId}&reason={Uri.EscapeDataString(reason ?? string.Empty)}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- IAM extended engines ---------------------------------
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

    // -- CRM extended -----------------------------------------
    public async Task<Guid> CreateOpportunityAsync(CreateOpportunityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/opportunities", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public Task<PagedResult<QuoteDto>?> SearchQuotesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<QuoteDto>>($"api/crm/quotes?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<QuoteDto?> GetQuoteByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<QuoteDto>($"api/crm/quotes/{id}", cancellationToken);

    public async Task<Guid> CreateQuoteAsync(CreateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/crm/quotes", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateQuoteAsync(Guid id, UpdateQuoteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/crm/quotes/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteQuoteAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/crm/quotes/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<IReadOnlyList<QuoteLineDto>?> GetQuoteLinesAsync(Guid quoteId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<QuoteLineDto>>($"api/crm/quotes/{quoteId}/lines", cancellationToken);

    public Task<PagedResult<LeadActivityDto>?> SearchLeadActivitiesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<LeadActivityDto>>($"api/crm/lead-activities?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public Task<IReadOnlyList<LeadActivityDto>?> GetLeadActivitiesByLeadIdAsync(Guid leadId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IReadOnlyList<LeadActivityDto>>($"api/crm/lead-activities/by-lead/{leadId}", cancellationToken);

    public async Task<Guid> CreateLeadActivityAsync(CreateLeadActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/crm/lead-activities", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    public async Task UpdateLeadActivityAsync(Guid id, UpdateLeadActivityRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/crm/lead-activities/{id}", request, cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public async Task DeleteLeadActivityAsync(Guid id, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/crm/lead-activities/{id}?modifiedByUserId={modifiedByUserId}", cancellationToken);
        await EnsureSuccessWithDetailsAsync(response, cancellationToken);
    }

    public Task<PagedResult<PricingRuleDto>?> SearchPricingRulesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PricingRuleDto>>($"api/crm/pricing-rules?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreatePricingRuleAsync(CreatePricingRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/crm/pricing-rules", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdatePricingRuleAsync(Guid id, UpdatePricingRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/crm/pricing-rules/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePricingRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/crm/pricing-rules/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<PriceClassDto>?> SearchPriceClassesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<PriceClassDto>>($"api/crm/pricing-market-rules/classes?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreatePriceClassAsync(UpsertPriceClassRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/crm/pricing-market-rules/classes", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdatePriceClassAsync(Guid id, UpsertPriceClassRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/crm/pricing-market-rules/classes/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeletePriceClassAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/crm/pricing-market-rules/classes/{id}?userId={userId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<MarketAppetiteDto>?> SearchMarketAppetiteAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<MarketAppetiteDto>>($"api/crm/pricing-market-rules/appetite?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateMarketAppetiteAsync(UpsertMarketAppetiteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/crm/pricing-market-rules/appetite", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateMarketAppetiteAsync(Guid id, UpsertMarketAppetiteRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/crm/pricing-market-rules/appetite/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteMarketAppetiteAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/crm/pricing-market-rules/appetite/{id}?userId={userId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<CarrierMappingDto>?> SearchCarrierMappingsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 250, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CarrierMappingDto>>($"api/crm/pricing-market-rules/carrier-mappings?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateCarrierMappingAsync(UpsertCarrierMappingRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/crm/pricing-market-rules/carrier-mappings", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken))!.Id;
    }

    public async Task UpdateCarrierMappingAsync(Guid id, UpsertCarrierMappingRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/crm/pricing-market-rules/carrier-mappings/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteCarrierMappingAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/crm/pricing-market-rules/carrier-mappings/{id}?userId={userId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task TestCarrierMappingAsync(Guid id, Guid? userId = null, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"api/crm/pricing-market-rules/carrier-mappings/{id}/test?userId={userId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public Task<PagedResult<ForecastEntryDto>?> SearchForecastEntriesAsync(Guid tenantId, string? searchTerm = null, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ForecastEntryDto>>($"api/crm/forecast?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}", cancellationToken);

    public async Task<Guid> CreateForecastEntryAsync(CreateForecastEntryRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/crm/forecast", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<Guid>(cancellationToken: cancellationToken);
    }

    // -- Security / MFA --------------------------------------------------------

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

    // -- Security / Trusted Devices --------------------------------------------

    public Task<PagedResult<TrustedDeviceDto>?> SearchTrustedDevicesAsync(Guid tenantId, Guid? userId = null, string? searchTerm = null, bool? isActive = null, bool? highRiskOnly = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/security/trusted-devices?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}";
        if (userId.HasValue) url += $"&userId={userId}";
        if (isActive.HasValue) url += $"&isActive={isActive.Value}";
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

    // -- Security / User Status -------------------------------------------------

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

    // -- Governance / Access Requests ------------------------------------------

    public Task<AccessRequestDto?> GetAccessRequestByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AccessRequestDto>($"api/governance/access-requests/{id}", cancellationToken);

    public Task<PagedResult<AccessRequestDto>?> SearchAccessRequestsAsync(Guid tenantId, string? searchTerm = null, string? requestTypeCode = null, string? statusCode = null, Guid? requestedForUserId = null, Guid? requestedByUserId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/governance/access-requests?tenantId={tenantId}&pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(requestTypeCode)) url += $"&requestTypeCode={Uri.EscapeDataString(requestTypeCode)}";
        if (!string.IsNullOrEmpty(statusCode)) url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        if (requestedForUserId.HasValue) url += $"&requestedForUserId={requestedForUserId}";
        if (requestedByUserId.HasValue) url += $"&requestedByUserId={requestedByUserId}";
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

    // -- Governance / Access Review Campaigns ----------------------------------

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

    // -- SoD / Rules -----------------------------------------------------------

    public Task<PagedResult<SegregationOfDutyRuleDto>?> SearchSodRulesAsync(Guid? tenantId = null, string? searchTerm = null, string? severityCode = null, bool? isActive = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/sod/rules?pageNumber={pageNumber}&pageSize={pageSize}";
        if (tenantId.HasValue) url += $"&tenantId={tenantId}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(severityCode)) url += $"&severityCode={Uri.EscapeDataString(severityCode)}";
        if (isActive.HasValue) url += $"&isActive={isActive.Value}";
        return _httpClient.GetFromJsonAsync<PagedResult<SegregationOfDutyRuleDto>>(url, cancellationToken);
    }

    public Task<SegregationOfDutyRuleDto?> GetSodRuleByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<SegregationOfDutyRuleDto>($"api/sod/rules/{id}", cancellationToken);

    public async Task<Guid> CreateSodRuleAsync(CreateSodRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/sod/rules", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateSodRuleAsync(Guid id, UpdateSodRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/sod/rules/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetSodRuleActiveAsync(Guid id, bool isActive, Guid? modifiedByUserId = null, CancellationToken cancellationToken = default)
    {
        var action = isActive ? "activate" : "deactivate";
        var response = await _httpClient.PatchAsync($"api/sod/rules/{id}/{action}?modifiedByUserId={modifiedByUserId}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CloneSodRuleAsync(Guid id, CloneSodRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/sod/rules/{id}/clone", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    // -- SoD Conflicts ----------------------------------------------------------

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

    public async Task CreateSodExceptionAsync(Guid conflictId, CreateSodExceptionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/sod/conflicts/{conflictId}/exception", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Compliance ------------------------------------------
    public Task<PagedResult<PolicyDocumentDto>?> SearchPolicyDocumentsAsync(
        Guid? tenantId = null, string? searchTerm = null, string? typeCode = null,
        string? statusCode = null, bool? isActive = null,
        int pageNumber = 1, int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        var url = $"api/compliance/policies?pageNumber={pageNumber}&pageSize={pageSize}";
        if (tenantId.HasValue) url += $"&tenantId={tenantId}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(typeCode)) url += $"&typeCode={Uri.EscapeDataString(typeCode)}";
        if (!string.IsNullOrEmpty(statusCode)) url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        if (isActive.HasValue) url += $"&isActive={isActive.Value}";
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

    // -- Compliance — Acknowledgements -----------------------------------------

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
        if (tenantId.HasValue) parts.Add($"tenantId={tenantId}");
        if (policyId.HasValue) parts.Add($"policyId={policyId}");
        if (!string.IsNullOrWhiteSpace(searchTerm)) parts.Add($"searchTerm={Uri.EscapeDataString(searchTerm)}");
        return parts.Count > 0 ? $"{path}?{string.Join("&", parts)}" : path;
    }

    // -- Regions ----------------------------------------------
    public Task<PagedResult<RegionDto>?> SearchRegionsAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<RegionDto>>($"api/regions?searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public Task<RegionDto?> GetRegionByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<RegionDto>($"api/regions/{id}", cancellationToken);

    public async Task<Guid> CreateRegionAsync(CreateRegionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/regions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateRegionAsync(Guid id, UpdateRegionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/regions/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetRegionActiveAsync(Guid id, bool activate, CancellationToken cancellationToken = default)
    {
        var action = activate ? "activate" : "deactivate";
        var response = await _httpClient.PatchAsync($"api/regions/{id}/{action}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteRegionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/regions/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Deployment Bindings -----------------------------------
    public Task<PagedResult<DeploymentBindingDto>?> SearchDeploymentBindingsAsync(string? searchTerm = null, string? statusCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/deployment-bindings?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(statusCode)) url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        return _httpClient.GetFromJsonAsync<PagedResult<DeploymentBindingDto>>(url, cancellationToken);
    }

    public Task<DeploymentBindingDto?> GetDeploymentBindingByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<DeploymentBindingDto>($"api/deployment-bindings/{id}", cancellationToken);

    public async Task<Guid> CreateDeploymentBindingAsync(CreateDeploymentBindingRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/deployment-bindings", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateDeploymentBindingAsync(Guid id, UpdateDeploymentBindingRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/deployment-bindings/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetDeploymentBindingStatusAsync(Guid id, string statusCode, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/deployment-bindings/{id}/status?statusCode={Uri.EscapeDataString(statusCode)}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteDeploymentBindingAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/deployment-bindings/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Deployment Stamps -------------------------------------
    public Task<PagedResult<DeploymentStampDto>?> SearchDeploymentStampsAsync(string? searchTerm = null, string? statusCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/deployment-stamps?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(statusCode)) url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        return _httpClient.GetFromJsonAsync<PagedResult<DeploymentStampDto>>(url, cancellationToken);
    }

    public Task<DeploymentStampDto?> GetDeploymentStampByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<DeploymentStampDto>($"api/deployment-stamps/{id}", cancellationToken);

    public async Task<Guid> CreateDeploymentStampAsync(CreateDeploymentStampRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/deployment-stamps", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateDeploymentStampAsync(Guid id, UpdateDeploymentStampRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/deployment-stamps/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task SetDeploymentStampStatusAsync(Guid id, string statusCode, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsync($"api/deployment-stamps/{id}/status?statusCode={Uri.EscapeDataString(statusCode)}", null, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteDeploymentStampAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/deployment-stamps/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Tenant Deployment Assignments -------------------------
    public Task<TenantDeploymentAssignmentDto?> GetTenantDeploymentAssignmentAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<TenantDeploymentAssignmentDto>($"api/tenant-deployment-assignments/{tenantId}", cancellationToken);

    public async Task<Guid> UpsertTenantDeploymentAssignmentAsync(Guid tenantId, UpsertTenantDeploymentAssignmentRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tenant-deployment-assignments/{tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task DeleteTenantDeploymentAssignmentAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tenant-deployment-assignments/{tenantId}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Quota Rules ---------------------------------------
    public Task<PagedResult<QuotaRuleDto>?> SearchQuotaRulesAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/quota-rules?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        return _httpClient.GetFromJsonAsync<PagedResult<QuotaRuleDto>>(url, cancellationToken);
    }

    public Task<QuotaRuleDto?> GetQuotaRuleByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<QuotaRuleDto>($"api/quota-rules/{id}", cancellationToken);

    public async Task<Guid> CreateQuotaRuleAsync(CreateQuotaRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/quota-rules", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateQuotaRuleAsync(Guid id, UpdateQuotaRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/quota-rules/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteQuotaRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/quota-rules/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<Guid> CloneQuotaRuleAsync(Guid id, CloneQuotaRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync($"api/quota-rules/{id}/clone", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task ActivateQuotaRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/quota-rules/{id}/activate", new { }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeactivateQuotaRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/quota-rules/{id}/deactivate", new { }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Tenant Quotas -----------------------------------------
    public Task<PagedResult<TenantQuotaDto>?> SearchTenantQuotasAsync(string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/tenant-quotas?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        return _httpClient.GetFromJsonAsync<PagedResult<TenantQuotaDto>>(url, cancellationToken);
    }

    public Task<List<TenantQuotaDto>?> GetTenantQuotasAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<TenantQuotaDto>>($"api/tenant-quotas/by-tenant/{tenantId}", cancellationToken);

    public async Task<Guid> UpsertTenantQuotaAsync(Guid tenantId, UpsertTenantQuotaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/tenant-quotas/by-tenant/{tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task DeleteTenantQuotaAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/tenant-quotas/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task OverrideTenantQuotaLimitAsync(Guid id, OverrideLimitRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/tenant-quotas/{id}/override-limit", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResetTenantQuotaOverrideAsync(Guid id, ResetOverrideRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/tenant-quotas/{id}/reset-override", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task NotifyTenantQuotaAsync(Guid id, NotifyTenantQuotaRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/tenant-quotas/{id}/notify", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Quota Violations --------------------------------------
    public Task<PagedResult<QuotaViolationDto>?> SearchQuotaViolationsAsync(string? searchTerm = null, string? statusCode = null, string? severityCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/quota-violations?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(statusCode)) url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        if (!string.IsNullOrEmpty(severityCode)) url += $"&severityCode={Uri.EscapeDataString(severityCode)}";
        return _httpClient.GetFromJsonAsync<PagedResult<QuotaViolationDto>>(url, cancellationToken);
    }

    public Task<QuotaViolationDto?> GetQuotaViolationByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<QuotaViolationDto>($"api/quota-violations/{id}", cancellationToken);

    public Task<int> GetQuotaViolationOpenCountAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<int>("api/quota-violations/open-count", cancellationToken);

    public async Task AcknowledgeQuotaViolationAsync(Guid id, AcknowledgeQuotaViolationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/quota-violations/{id}/acknowledge", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResolveQuotaViolationAsync(Guid id, ResolveQuotaViolationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/quota-violations/{id}/resolve", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task NotifyQuotaViolationAsync(Guid id, NotifyQuotaViolationRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/quota-violations/{id}/notify", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ApplyQuotaRestrictionAsync(Guid id, ApplyRestrictionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/quota-violations/{id}/restrict", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task GrantTemporaryQuotaIncreaseAsync(Guid id, GrantTemporaryIncreaseRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/quota-violations/{id}/temporary-increase", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ConvertQuotaToOverageAsync(Guid id, ConvertToOverageRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/quota-violations/{id}/convert-to-overage", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Health Checks -----------------------------------------
    public Task<PagedResult<HealthCheckDto>?> SearchHealthChecksAsync(string? searchTerm = null, string? statusCode = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/health-checks?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(statusCode)) url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        return _httpClient.GetFromJsonAsync<PagedResult<HealthCheckDto>>(url, cancellationToken);
    }

    public Task<HealthCheckDto?> GetHealthCheckByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<HealthCheckDto>($"api/health-checks/{id}", cancellationToken);

    public async Task<Guid> CreateHealthCheckAsync(CreateHealthCheckRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/health-checks", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateHealthCheckAsync(Guid id, UpdateHealthCheckRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/health-checks/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteHealthCheckAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/health-checks/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Alerts ------------------------------------------------
    public Task<PagedResult<AlertDto>?> SearchAlertsAsync(string? searchTerm = null, string? statusCode = null, string? severityCode = null, string? regionCode = null, Guid? tenantId = null, bool? openOnly = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/alerts?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(statusCode)) url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        if (!string.IsNullOrEmpty(severityCode)) url += $"&severityCode={Uri.EscapeDataString(severityCode)}";
        if (!string.IsNullOrEmpty(regionCode)) url += $"&regionCode={Uri.EscapeDataString(regionCode)}";
        if (tenantId.HasValue) url += $"&tenantId={tenantId.Value}";
        if (openOnly == true) url += "&openOnly=true";
        return _httpClient.GetFromJsonAsync<PagedResult<AlertDto>>(url, cancellationToken);
    }

    public Task<AlertDto?> GetAlertByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AlertDto>($"api/alerts/{id}", cancellationToken);

    public Task<int> GetAlertOpenCountAsync(CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<int>("api/alerts/open-count", cancellationToken);

    public async Task AcknowledgeAlertAsync(Guid id, AcknowledgeAlertRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/alerts/{id}/acknowledge", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task ResolveAlertAsync(Guid id, ResolveAlertRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/alerts/{id}/resolve", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task AssignAlertAsync(Guid id, AssignAlertRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/alerts/{id}/assign", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task EscalateAlertAsync(Guid id, EscalateAlertRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/alerts/{id}/escalate", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- SLA Definitions --------------------------------------
    public Task<PagedResult<SlaDefinitionDto>?> SearchSlaDefinitionsAsync(string? searchTerm = null, string? complianceStatus = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/sla-definitions?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(complianceStatus)) url += $"&complianceStatus={Uri.EscapeDataString(complianceStatus)}";
        return _httpClient.GetFromJsonAsync<PagedResult<SlaDefinitionDto>>(url, cancellationToken);
    }

    public Task<SlaDefinitionDto?> GetSlaDefinitionByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<SlaDefinitionDto>($"api/sla-definitions/{id}", cancellationToken);

    public async Task<Guid> CreateSlaDefinitionAsync(CreateSlaDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/sla-definitions", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateSlaDefinitionAsync(Guid id, UpdateSlaDefinitionRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/sla-definitions/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteSlaDefinitionAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/sla-definitions/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Audit Logs -------------------------------------------
    public Task<PagedResult<AuditLogDto>?> SearchAuditLogsAsync(string? searchTerm = null, string? eventTypeCode = null, string? actor = null, string? entityName = null, string? tenantId = null, DateTime? fromDate = null, DateTime? toDate = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/audit-logs?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(eventTypeCode)) url += $"&eventTypeCode={Uri.EscapeDataString(eventTypeCode)}";
        if (!string.IsNullOrEmpty(actor)) url += $"&actor={Uri.EscapeDataString(actor)}";
        if (!string.IsNullOrEmpty(entityName)) url += $"&entityName={Uri.EscapeDataString(entityName)}";
        if (!string.IsNullOrEmpty(tenantId)) url += $"&tenantId={Uri.EscapeDataString(tenantId)}";
        if (fromDate.HasValue) url += $"&fromDate={fromDate.Value:O}";
        if (toDate.HasValue) url += $"&toDate={toDate.Value:O}";
        return _httpClient.GetFromJsonAsync<PagedResult<AuditLogDto>>(url, cancellationToken);
    }

    public Task<AuditLogDto?> GetAuditLogByIdAsync(Guid auditLogId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AuditLogDto>($"api/audit-logs/{auditLogId}", cancellationToken);

    // -- Field Change Logs ------------------------------------
    public Task<PagedResult<FieldChangeLogDto>?> SearchFieldChangeLogsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/field-change-logs?tenantId={tenantId}&pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        return _httpClient.GetFromJsonAsync<PagedResult<FieldChangeLogDto>>(url, cancellationToken);
    }

    // -- Security Event Logs ----------------------------------
    public Task<PagedResult<SecurityEventLogDto>?> SearchSecurityEventLogsAsync(string? searchTerm = null, string? eventTypeCode = null, Guid? tenantId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/security-event-logs?pageNumber={pageNumber}&pageSize={pageSize}";
        if (tenantId.HasValue) url += $"&tenantId={tenantId.Value}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(eventTypeCode)) url += $"&eventTypeCode={Uri.EscapeDataString(eventTypeCode)}";
        return _httpClient.GetFromJsonAsync<PagedResult<SecurityEventLogDto>>(url, cancellationToken);
    }

    // -- System Logs -----------------------------------------
    public Task<PagedResult<SystemLogDto>?> SearchSystemLogsAsync(string? keyword = null, string? level = null, string? serviceName = null, string? regionCode = null, string? correlationId = null, string? tenantId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/system-logs?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(keyword)) url += $"&keyword={Uri.EscapeDataString(keyword)}";
        if (!string.IsNullOrEmpty(level)) url += $"&level={Uri.EscapeDataString(level)}";
        if (!string.IsNullOrEmpty(serviceName)) url += $"&serviceName={Uri.EscapeDataString(serviceName)}";
        if (!string.IsNullOrEmpty(regionCode)) url += $"&regionCode={Uri.EscapeDataString(regionCode)}";
        if (!string.IsNullOrEmpty(correlationId)) url += $"&correlationId={Uri.EscapeDataString(correlationId)}";
        if (!string.IsNullOrEmpty(tenantId)) url += $"&tenantId={Uri.EscapeDataString(tenantId)}";
        return _httpClient.GetFromJsonAsync<PagedResult<SystemLogDto>>(url, cancellationToken);
    }

    public Task<SystemLogDto?> GetSystemLogByIdAsync(Guid systemLogId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<SystemLogDto>($"api/system-logs/{systemLogId}", cancellationToken);

    // -- Platform Configuration -------------------------------
    public Task<List<ConfigurationSettingDto>?> GetConfigurationByScopeAsync(string scopeCode, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<List<ConfigurationSettingDto>>($"api/platform/configuration/scope/{Uri.EscapeDataString(scopeCode)}", cancellationToken);

    public async Task UpdateConfigurationValueAsync(Guid settingId, string? settingValue, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/platform/configuration/{settingId}/value", new { SettingValue = settingValue }, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Platform Events ----------------------------------------
    public Task<PagedResult<PlatformEventDto>?> SearchPlatformEventsAsync(string? searchTerm = null, string? eventTypeCode = null, string? processingStatus = null, string? sourceService = null, Guid? tenantId = null, string? correlationId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/platform-events?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(eventTypeCode)) url += $"&eventTypeCode={Uri.EscapeDataString(eventTypeCode)}";
        if (!string.IsNullOrEmpty(processingStatus)) url += $"&processingStatus={Uri.EscapeDataString(processingStatus)}";
        if (!string.IsNullOrEmpty(sourceService)) url += $"&sourceService={Uri.EscapeDataString(sourceService)}";
        if (tenantId.HasValue) url += $"&tenantId={tenantId}";
        if (!string.IsNullOrEmpty(correlationId)) url += $"&correlationId={Uri.EscapeDataString(correlationId)}";
        return _httpClient.GetFromJsonAsync<PagedResult<PlatformEventDto>>(url, cancellationToken);
    }

    public Task<PlatformEventDto?> GetPlatformEventByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PlatformEventDto>($"api/platform-events/{id}", cancellationToken);

    public async Task ReplayPlatformEventAsync(Guid id, ReplayPlatformEventRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/platform-events/{id}/replay", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Background Jobs ---------------------------------------
    public Task<PagedResult<BackgroundJobDto>?> SearchBackgroundJobsAsync(string? searchTerm = null, string? jobTypeCode = null, string? statusCode = null, Guid? tenantId = null, bool? failedOnly = null, DateTime? fromDateUtc = null, DateTime? toDateUtc = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var url = $"api/background-jobs?pageNumber={pageNumber}&pageSize={pageSize}";
        if (!string.IsNullOrEmpty(searchTerm)) url += $"&searchTerm={Uri.EscapeDataString(searchTerm)}";
        if (!string.IsNullOrEmpty(jobTypeCode)) url += $"&jobTypeCode={Uri.EscapeDataString(jobTypeCode)}";
        if (!string.IsNullOrEmpty(statusCode)) url += $"&statusCode={Uri.EscapeDataString(statusCode)}";
        if (tenantId.HasValue) url += $"&tenantId={tenantId}";
        if (failedOnly == true) url += "&failedOnly=true";
        if (fromDateUtc.HasValue) url += $"&fromDateUtc={fromDateUtc.Value:O}";
        if (toDateUtc.HasValue) url += $"&toDateUtc={toDateUtc.Value:O}";
        return _httpClient.GetFromJsonAsync<PagedResult<BackgroundJobDto>>(url, cancellationToken);
    }

    public Task<BackgroundJobDto?> GetBackgroundJobByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<BackgroundJobDto>($"api/background-jobs/{id}", cancellationToken);

    public async Task RetryBackgroundJobAsync(Guid id, RetryBackgroundJobRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/background-jobs/{id}/retry", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task CancelBackgroundJobAsync(Guid id, CancelBackgroundJobRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/background-jobs/{id}/cancel", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task RequeueBackgroundJobAsync(Guid id, RequeueBackgroundJobRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PatchAsJsonAsync($"api/background-jobs/{id}/requeue", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private sealed class IdResult { public Guid Id { get; set; } }

    // -- Tenant Configuration ---------------------------------
    public Task<IEnumerable<ConfigurationSettingDto>?> GetTenantConfigurationAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<IEnumerable<ConfigurationSettingDto>>($"api/platform/configuration/tenant/{tenantId}", cancellationToken);

    public async Task SaveTenantConfigurationAsync(Guid tenantId, IEnumerable<UpsertTenantSettingModel> settings, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/platform/configuration/tenant/{tenantId}/settings", settings, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Agency Profile ---------------------------------------
    public Task<AgencyProfileDto?> GetAgencyProfileAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AgencyProfileDto>($"api/agency/{tenantId}", cancellationToken);

    public async Task UpdateAgencyProfileAsync(Guid tenantId, UpdateAgencyProfileRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/agency/{tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Agency Business Hours --------------------------------
    public Task<AgencyBusinessHoursDto?> GetAgencyBusinessHoursAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<AgencyBusinessHoursDto>($"api/agency/business-hours/tenant/{tenantId}", cancellationToken);

    public async Task UpdateAgencyBusinessHoursAsync(Guid tenantId, UpdateAgencyBusinessHoursRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/agency/business-hours/tenant/{tenantId}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Branches --------------------------------------------
    public Task<PagedResult<BranchDto>?> SearchBranchesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<BranchDto>>($"api/branches?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateBranchAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/branches", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateBranchAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/branches/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task DeleteBranchAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.DeleteAsync($"api/branches/{id}", cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Carriers ---------------------------------------------
    public Task<PagedResult<CarrierDto>?> SearchCarriersAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<CarrierDto>>($"api/carriers?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateCarrierAsync(CreateCarrierRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/carriers", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateCarrierAsync(Guid id, UpdateCarrierRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/carriers/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Lines of Business ------------------------------------
    public Task<PagedResult<LineOfBusinessDto>?> SearchLobsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 100, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<LineOfBusinessDto>>($"api/lobs?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateLobAsync(CreateLineOfBusinessRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/lobs", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateLobAsync(Guid id, UpdateLineOfBusinessRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/lobs/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Appetite Rules ---------------------------------------
    public Task<PagedResult<AppetiteRuleDto>?> SearchAppetiteRulesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AppetiteRuleDto>>($"api/appetite?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", cancellationToken);

    public async Task<Guid> CreateAppetiteRuleAsync(CreateAppetiteRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("api/appetite", request, cancellationToken);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<IdResult>(cancellationToken: cancellationToken);
        return result!.Id;
    }

    public async Task UpdateAppetiteRuleAsync(Guid id, UpdateAppetiteRuleRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/appetite/{id}", request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    // -- Account Config: Account Types -------------------------------
    public Task<PagedResult<AccountTypeDto>?> SearchAccountTypesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AccountTypeDto>>($"api/accounts/types?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);
    public async Task<Guid> CreateAccountTypeAsync(CreateAccountTypeRequest request, CancellationToken ct = default)
    {
        var r = await _httpClient.PostAsJsonAsync("api/accounts/types", request, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id;
    }
    public async Task UpdateAccountTypeAsync(Guid id, UpdateAccountTypeRequest request, CancellationToken ct = default)
    { (await _httpClient.PutAsJsonAsync($"api/accounts/types/{id}", request, ct)).EnsureSuccessStatusCode(); }
    public async Task DeleteAccountTypeAsync(Guid id, CancellationToken ct = default)
    { (await _httpClient.DeleteAsync($"api/accounts/types/{id}", ct)).EnsureSuccessStatusCode(); }

    // -- Account Config: Relationship Types --------------------------
    public Task<PagedResult<RelationshipTypeDto>?> SearchRelationshipTypesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<RelationshipTypeDto>>($"api/accounts/rel-types?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);
    public async Task<Guid> CreateRelationshipTypeAsync(CreateRelationshipTypeRequest request, CancellationToken ct = default)
    {
        var r = await _httpClient.PostAsJsonAsync("api/accounts/rel-types", request, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id;
    }
    public async Task UpdateRelationshipTypeAsync(Guid id, UpdateRelationshipTypeRequest request, CancellationToken ct = default)
    { (await _httpClient.PutAsJsonAsync($"api/accounts/rel-types/{id}", request, ct)).EnsureSuccessStatusCode(); }
    public async Task DeleteRelationshipTypeAsync(Guid id, CancellationToken ct = default)
    { (await _httpClient.DeleteAsync($"api/accounts/rel-types/{id}", ct)).EnsureSuccessStatusCode(); }

    public Task<List<AccountReferenceOptionDto>?> GetAccountReferenceOptionsAsync(Guid tenantId, string? optionGroup = null, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<List<AccountReferenceOptionDto>>($"api/accounts/reference-options?tenantId={tenantId}&optionGroup={Uri.EscapeDataString(optionGroup ?? string.Empty)}", ct);

    // -- Account Config: Household Settings --------------------------
    public Task<List<HouseholdSettingDto>?> GetHouseholdSettingsAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<List<HouseholdSettingDto>>($"api/accounts/household-settings?tenantId={tenantId}", ct);
    public async Task UpdateHouseholdSettingAsync(Guid id, UpdateHouseholdSettingRequest request, CancellationToken ct = default)
    { (await _httpClient.PutAsJsonAsync($"api/accounts/household-settings/{id}", request, ct)).EnsureSuccessStatusCode(); }

    // -- Account Config: Commercial Entity Settings ------------------
    public Task<List<CommercialEntitySettingDto>?> GetCommercialEntitySettingsAsync(Guid tenantId, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<List<CommercialEntitySettingDto>>($"api/accounts/commercial-settings?tenantId={tenantId}", ct);
    public async Task UpdateCommercialEntitySettingAsync(Guid id, UpdateCommercialEntitySettingRequest request, CancellationToken ct = default)
    { (await _httpClient.PutAsJsonAsync($"api/accounts/commercial-settings/{id}", request, ct)).EnsureSuccessStatusCode(); }

    // -- Account Config: Contact Types -------------------------------
    public Task<PagedResult<ContactTypeDto>?> SearchContactTypesAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<ContactTypeDto>>($"api/accounts/contact-types?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);
    public async Task<Guid> CreateContactTypeAsync(CreateContactTypeRequest request, CancellationToken ct = default)
    {
        var r = await _httpClient.PostAsJsonAsync("api/accounts/contact-types", request, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id;
    }
    public async Task UpdateContactTypeAsync(Guid id, UpdateContactTypeRequest request, CancellationToken ct = default)
    { (await _httpClient.PutAsJsonAsync($"api/accounts/contact-types/{id}", request, ct)).EnsureSuccessStatusCode(); }
    public async Task DeleteContactTypeAsync(Guid id, CancellationToken ct = default)
    { (await _httpClient.DeleteAsync($"api/accounts/contact-types/{id}", ct)).EnsureSuccessStatusCode(); }

    // -- Account Config: Custom Fields -------------------------------
    public Task<PagedResult<AccountCustomFieldDto>?> SearchAccountCustomFieldsAsync(Guid tenantId, string? searchTerm = null, int pageNumber = 1, int pageSize = 50, CancellationToken ct = default)
        => _httpClient.GetFromJsonAsync<PagedResult<AccountCustomFieldDto>>($"api/accounts/custom-fields?tenantId={tenantId}&searchTerm={Uri.EscapeDataString(searchTerm ?? string.Empty)}&pageNumber={pageNumber}&pageSize={pageSize}", ct);
    public async Task<Guid> CreateAccountCustomFieldAsync(CreateAccountCustomFieldRequest request, CancellationToken ct = default)
    {
        var r = await _httpClient.PostAsJsonAsync("api/accounts/custom-fields", request, ct);
        r.EnsureSuccessStatusCode();
        return (await r.Content.ReadFromJsonAsync<IdResult>(cancellationToken: ct))!.Id;
    }
    public async Task UpdateAccountCustomFieldAsync(Guid id, UpdateAccountCustomFieldRequest request, CancellationToken ct = default)
    { (await _httpClient.PutAsJsonAsync($"api/accounts/custom-fields/{id}", request, ct)).EnsureSuccessStatusCode(); }
    public async Task DeleteAccountCustomFieldAsync(Guid id, CancellationToken ct = default)
    { (await _httpClient.DeleteAsync($"api/accounts/custom-fields/{id}", ct)).EnsureSuccessStatusCode(); }
}

public sealed class UpsertTenantSettingModel
{
    public string SettingKey { get; set; } = string.Empty;
    public string? SettingValue { get; set; }
}
