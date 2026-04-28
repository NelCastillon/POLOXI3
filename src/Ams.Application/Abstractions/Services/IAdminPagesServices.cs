using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Services;

/// <summary>
/// Service for managing business rules
/// </summary>
public interface IBusinessRuleService
{
    Task<IReadOnlyList<BusinessRuleDto>> GetRulesAsync(Guid tenantId, string? categoryFilter = null, CancellationToken cancellationToken = default);
    Task<BusinessRuleDto?> GetRuleByIdAsync(Guid ruleId, CancellationToken cancellationToken = default);
    Task<Guid> CreateRuleAsync(BusinessRuleDto rule, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateRuleAsync(BusinessRuleDto rule, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken = default);
    Task ToggleRuleStatusAsync(Guid ruleId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing departments and teams
/// </summary>
public interface IDepartmentTeamService
{
    Task<IReadOnlyList<DepartmentTeamDto>> GetTeamsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DepartmentTeamDto?> GetTeamByIdAsync(Guid teamId, CancellationToken cancellationToken = default);
    Task<Guid> CreateTeamAsync(DepartmentTeamDto team, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateTeamAsync(DepartmentTeamDto team, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteTeamAsync(Guid teamId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing producers and staff
/// </summary>
public interface IProducerStaffService
{
    Task<IReadOnlyList<ProducerStaffDto>> GetStaffAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ProducerStaffDto?> GetStaffByIdAsync(Guid staffId, CancellationToken cancellationToken = default);
    Task<Guid> CreateStaffAsync(ProducerStaffDto staff, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateStaffAsync(ProducerStaffDto staff, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteStaffAsync(Guid staffId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProducerStaffDto>> GetExpiringLicensesAsync(Guid tenantId, int days = 30, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing system settings
/// </summary>
public interface ISystemSettingsService
{
    Task<IReadOnlyList<SystemSettingsDto>> GetSettingsAsync(Guid tenantId, string? category = null, CancellationToken cancellationToken = default);
    Task<SystemSettingsDto?> GetSettingAsync(Guid tenantId, string settingKey, CancellationToken cancellationToken = default);
    Task UpdateSettingAsync(SystemSettingsDto setting, Guid userId, CancellationToken cancellationToken = default);
    Task<T?> GetSettingValueAsync<T>(Guid tenantId, string settingKey, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing notification policies
/// </summary>
public interface INotificationPolicyService
{
    Task<IReadOnlyList<NotificationPolicyDto>> GetPoliciesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<NotificationPolicyDto?> GetPolicyByIdAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<Guid> CreatePolicyAsync(NotificationPolicyDto policy, Guid userId, CancellationToken cancellationToken = default);
    Task UpdatePolicyAsync(NotificationPolicyDto policy, Guid userId, CancellationToken cancellationToken = default);
    Task DeletePolicyAsync(Guid policyId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing queue routing
/// </summary>
public interface IQueueRoutingService
{
    Task<IReadOnlyList<QueueRoutingRuleDto>> GetRulesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<QueueRoutingRuleDto?> GetRuleByIdAsync(Guid ruleId, CancellationToken cancellationToken = default);
    Task<Guid> CreateRuleAsync(QueueRoutingRuleDto rule, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateRuleAsync(QueueRoutingRuleDto rule, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing data quality rules
/// </summary>
public interface IDataQualityService
{
    Task<IReadOnlyList<DataQualityRuleDto>> GetRulesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DataQualityRuleDto?> GetRuleByIdAsync(Guid ruleId, CancellationToken cancellationToken = default);
    Task<Guid> CreateRuleAsync(DataQualityRuleDto rule, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateRuleAsync(DataQualityRuleDto rule, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing data center configurations
/// </summary>
public interface IDataCenterService
{
    Task<IReadOnlyList<DataCenterConfigDto>> GetConfigsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DataCenterConfigDto?> GetConfigByIdAsync(Guid configId, CancellationToken cancellationToken = default);
    Task<Guid> CreateConfigAsync(DataCenterConfigDto config, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateConfigAsync(DataCenterConfigDto config, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteConfigAsync(Guid configId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Service for managing SLA policies
/// </summary>
public interface ISlaPolicyService
{
    Task<IReadOnlyList<SlaPolicySetupDto>> GetPoliciesAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<SlaPolicySetupDto?> GetPolicyByIdAsync(Guid policyId, CancellationToken cancellationToken = default);
    Task<Guid> CreatePolicyAsync(SlaPolicySetupDto policy, Guid userId, CancellationToken cancellationToken = default);
    Task UpdatePolicyAsync(SlaPolicySetupDto policy, Guid userId, CancellationToken cancellationToken = default);
    Task DeletePolicyAsync(Guid policyId, CancellationToken cancellationToken = default);
}
