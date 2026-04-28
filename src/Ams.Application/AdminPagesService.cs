using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;

namespace Ams.Application;

/// <summary>
/// Comprehensive admin services implementation combining all admin functionality
/// </summary>
public class AdminPagesService :
    IBusinessRuleService,
    IDepartmentTeamService,
    IProducerStaffService,
    ISystemSettingsService,
    INotificationPolicyService,
    IQueueRoutingService,
    IDataQualityService,
    IDataCenterService,
    ISlaPolicyService
{
    private readonly IBusinessRuleRepository _businessRuleRepository;
    private readonly IDepartmentTeamRepository _departmentTeamRepository;
    private readonly IProducerStaffRepository _producerStaffRepository;
    private readonly ISystemSettingsRepository _systemSettingsRepository;
    private readonly INotificationPolicyRepository _notificationPolicyRepository;
    private readonly IQueueRoutingRepository _queueRoutingRepository;
    private readonly IDataQualityRepository _dataQualityRepository;
    private readonly IDataCenterRepository _dataCenterRepository;
    private readonly ISlaPolicyRepository _slaPolicyRepository;

    public AdminPagesService(
        IBusinessRuleRepository businessRuleRepository,
        IDepartmentTeamRepository departmentTeamRepository,
        IProducerStaffRepository producerStaffRepository,
        ISystemSettingsRepository systemSettingsRepository,
        INotificationPolicyRepository notificationPolicyRepository,
        IQueueRoutingRepository queueRoutingRepository,
        IDataQualityRepository dataQualityRepository,
        IDataCenterRepository dataCenterRepository,
        ISlaPolicyRepository slaPolicyRepository)
    {
        _businessRuleRepository = businessRuleRepository;
        _departmentTeamRepository = departmentTeamRepository;
        _producerStaffRepository = producerStaffRepository;
        _systemSettingsRepository = systemSettingsRepository;
        _notificationPolicyRepository = notificationPolicyRepository;
        _queueRoutingRepository = queueRoutingRepository;
        _dataQualityRepository = dataQualityRepository;
        _dataCenterRepository = dataCenterRepository;
        _slaPolicyRepository = slaPolicyRepository;
    }

    #region Business Rules

    public Task<IReadOnlyList<BusinessRuleDto>> GetRulesAsync(Guid tenantId, string? categoryFilter = null, CancellationToken cancellationToken = default)
    {
        return categoryFilter == null
            ? _businessRuleRepository.GetAllAsync(tenantId, cancellationToken)
            : _businessRuleRepository.GetByCategoryAsync(tenantId, categoryFilter, cancellationToken);
    }

    public Task<BusinessRuleDto?> GetRuleByIdAsync(Guid ruleId, CancellationToken cancellationToken = default)
        => _businessRuleRepository.GetByIdAsync(ruleId, cancellationToken);

    public Task<Guid> CreateRuleAsync(BusinessRuleDto rule, Guid userId, CancellationToken cancellationToken = default)
        => _businessRuleRepository.CreateAsync(rule, userId, cancellationToken);

    public Task UpdateRuleAsync(BusinessRuleDto rule, Guid userId, CancellationToken cancellationToken = default)
        => _businessRuleRepository.UpdateAsync(rule, userId, cancellationToken);

    public Task DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken = default)
        => _businessRuleRepository.DeleteAsync(ruleId, cancellationToken);

    public Task ToggleRuleStatusAsync(Guid ruleId, CancellationToken cancellationToken = default)
        => _businessRuleRepository.ToggleStatusAsync(ruleId, cancellationToken);

    #endregion

    #region Departments & Teams

    public Task<IReadOnlyList<DepartmentTeamDto>> GetTeamsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _departmentTeamRepository.GetAllAsync(tenantId, cancellationToken);

    public Task<DepartmentTeamDto?> GetTeamByIdAsync(Guid teamId, CancellationToken cancellationToken = default)
        => _departmentTeamRepository.GetByIdAsync(teamId, cancellationToken);

    public Task<Guid> CreateTeamAsync(DepartmentTeamDto team, Guid userId, CancellationToken cancellationToken = default)
        => _departmentTeamRepository.CreateAsync(team, userId, cancellationToken);

    public Task UpdateTeamAsync(DepartmentTeamDto team, Guid userId, CancellationToken cancellationToken = default)
        => _departmentTeamRepository.UpdateAsync(team, userId, cancellationToken);

    public Task DeleteTeamAsync(Guid teamId, CancellationToken cancellationToken = default)
        => _departmentTeamRepository.DeleteAsync(teamId, cancellationToken);

    #endregion

    #region Producers & Staff

    public Task<IReadOnlyList<ProducerStaffDto>> GetStaffAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _producerStaffRepository.GetAllAsync(tenantId, cancellationToken);

    public Task<ProducerStaffDto?> GetStaffByIdAsync(Guid staffId, CancellationToken cancellationToken = default)
        => _producerStaffRepository.GetByIdAsync(staffId, cancellationToken);

    public Task<Guid> CreateStaffAsync(ProducerStaffDto staff, Guid userId, CancellationToken cancellationToken = default)
        => _producerStaffRepository.CreateAsync(staff, userId, cancellationToken);

    public Task UpdateStaffAsync(ProducerStaffDto staff, Guid userId, CancellationToken cancellationToken = default)
        => _producerStaffRepository.UpdateAsync(staff, userId, cancellationToken);

    public Task DeleteStaffAsync(Guid staffId, CancellationToken cancellationToken = default)
        => _producerStaffRepository.DeleteAsync(staffId, cancellationToken);

    public Task<IReadOnlyList<ProducerStaffDto>> GetExpiringLicensesAsync(Guid tenantId, int days = 30, CancellationToken cancellationToken = default)
        => _producerStaffRepository.GetExpiringLicensesAsync(tenantId, days, cancellationToken);

    #endregion

    #region System Settings

    public Task<IReadOnlyList<SystemSettingsDto>> GetSettingsAsync(Guid tenantId, string? category = null, CancellationToken cancellationToken = default)
    {
        return category == null
            ? _systemSettingsRepository.GetAllAsync(tenantId, cancellationToken)
            : _systemSettingsRepository.GetByCategoryAsync(tenantId, category, cancellationToken);
    }

    public Task<SystemSettingsDto?> GetSettingAsync(Guid tenantId, string settingKey, CancellationToken cancellationToken = default)
        => _systemSettingsRepository.GetByKeyAsync(tenantId, settingKey, cancellationToken);

    public Task UpdateSettingAsync(SystemSettingsDto setting, Guid userId, CancellationToken cancellationToken = default)
        => _systemSettingsRepository.UpdateAsync(setting, cancellationToken);

    public async Task<T?> GetSettingValueAsync<T>(Guid tenantId, string settingKey, CancellationToken cancellationToken = default)
    {
        var setting = await _systemSettingsRepository.GetByKeyAsync(tenantId, settingKey, cancellationToken);
        if (setting?.SettingValue == null) return default;

        return typeof(T) == typeof(string)
            ? (T)(object)setting.SettingValue
            : typeof(T) == typeof(int) && int.TryParse(setting.SettingValue, out var intValue)
                ? (T)(object)intValue
                : typeof(T) == typeof(bool) && bool.TryParse(setting.SettingValue, out var boolValue)
                    ? (T)(object)boolValue
                    : default;
    }

    #endregion

    #region Notification Policies

    public Task<IReadOnlyList<NotificationPolicyDto>> GetPoliciesAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _notificationPolicyRepository.GetAllAsync(tenantId, cancellationToken);

    public Task<NotificationPolicyDto?> GetPolicyByIdAsync(Guid policyId, CancellationToken cancellationToken = default)
        => _notificationPolicyRepository.GetByIdAsync(policyId, cancellationToken);

    public Task<Guid> CreatePolicyAsync(NotificationPolicyDto policy, Guid userId, CancellationToken cancellationToken = default)
        => _notificationPolicyRepository.CreateAsync(policy, userId, cancellationToken);

    public Task UpdatePolicyAsync(NotificationPolicyDto policy, Guid userId, CancellationToken cancellationToken = default)
        => _notificationPolicyRepository.UpdateAsync(policy, userId, cancellationToken);

    public Task DeletePolicyAsync(Guid policyId, CancellationToken cancellationToken = default)
        => _notificationPolicyRepository.DeleteAsync(policyId, cancellationToken);

    #endregion

    #region Queue Routing

    Task<IReadOnlyList<QueueRoutingRuleDto>> IQueueRoutingService.GetRulesAsync(Guid tenantId, CancellationToken cancellationToken)
        => _queueRoutingRepository.GetAllAsync(tenantId, cancellationToken);

    Task<QueueRoutingRuleDto?> IQueueRoutingService.GetRuleByIdAsync(Guid ruleId, CancellationToken cancellationToken)
        => _queueRoutingRepository.GetByIdAsync(ruleId, cancellationToken);

    Task<Guid> IQueueRoutingService.CreateRuleAsync(QueueRoutingRuleDto rule, Guid userId, CancellationToken cancellationToken)
        => _queueRoutingRepository.CreateAsync(rule, userId, cancellationToken);

    Task IQueueRoutingService.UpdateRuleAsync(QueueRoutingRuleDto rule, Guid userId, CancellationToken cancellationToken)
        => _queueRoutingRepository.UpdateAsync(rule, userId, cancellationToken);

    Task IQueueRoutingService.DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken)
        => _queueRoutingRepository.DeleteAsync(ruleId, cancellationToken);

    #endregion

    #region Data Quality

    Task<IReadOnlyList<DataQualityRuleDto>> IDataQualityService.GetRulesAsync(Guid tenantId, CancellationToken cancellationToken)
        => _dataQualityRepository.GetAllAsync(tenantId, cancellationToken);

    Task<DataQualityRuleDto?> IDataQualityService.GetRuleByIdAsync(Guid ruleId, CancellationToken cancellationToken)
        => _dataQualityRepository.GetByIdAsync(ruleId, cancellationToken);

    Task<Guid> IDataQualityService.CreateRuleAsync(DataQualityRuleDto rule, Guid userId, CancellationToken cancellationToken)
        => _dataQualityRepository.CreateAsync(rule, userId, cancellationToken);

    Task IDataQualityService.UpdateRuleAsync(DataQualityRuleDto rule, Guid userId, CancellationToken cancellationToken)
        => _dataQualityRepository.UpdateAsync(rule, userId, cancellationToken);

    Task IDataQualityService.DeleteRuleAsync(Guid ruleId, CancellationToken cancellationToken)
        => _dataQualityRepository.DeleteAsync(ruleId, cancellationToken);

    #endregion

    #region Data Center

    public Task<IReadOnlyList<DataCenterConfigDto>> GetConfigsAsync(Guid tenantId, CancellationToken cancellationToken = default)
        => _dataCenterRepository.GetAllAsync(tenantId, cancellationToken);

    public Task<DataCenterConfigDto?> GetConfigByIdAsync(Guid configId, CancellationToken cancellationToken = default)
        => _dataCenterRepository.GetByIdAsync(configId, cancellationToken);

    public Task<Guid> CreateConfigAsync(DataCenterConfigDto config, Guid userId, CancellationToken cancellationToken = default)
        => _dataCenterRepository.CreateAsync(config, userId, cancellationToken);

    public Task UpdateConfigAsync(DataCenterConfigDto config, Guid userId, CancellationToken cancellationToken = default)
        => _dataCenterRepository.UpdateAsync(config, userId, cancellationToken);

    public Task DeleteConfigAsync(Guid configId, CancellationToken cancellationToken = default)
        => _dataCenterRepository.DeleteAsync(configId, cancellationToken);

    #endregion

    #region SLA Policies

    Task<IReadOnlyList<SlaPolicySetupDto>> ISlaPolicyService.GetPoliciesAsync(Guid tenantId, CancellationToken cancellationToken)
        => _slaPolicyRepository.GetAllAsync(tenantId, cancellationToken);

    Task<SlaPolicySetupDto?> ISlaPolicyService.GetPolicyByIdAsync(Guid policyId, CancellationToken cancellationToken)
        => _slaPolicyRepository.GetByIdAsync(policyId, cancellationToken);

    Task<Guid> ISlaPolicyService.CreatePolicyAsync(SlaPolicySetupDto policy, Guid userId, CancellationToken cancellationToken)
        => _slaPolicyRepository.CreateAsync(policy, userId, cancellationToken);

    Task ISlaPolicyService.UpdatePolicyAsync(SlaPolicySetupDto policy, Guid userId, CancellationToken cancellationToken)
        => _slaPolicyRepository.UpdateAsync(policy, userId, cancellationToken);

    Task ISlaPolicyService.DeletePolicyAsync(Guid policyId, CancellationToken cancellationToken)
        => _slaPolicyRepository.DeleteAsync(policyId, cancellationToken);

    #endregion
}
