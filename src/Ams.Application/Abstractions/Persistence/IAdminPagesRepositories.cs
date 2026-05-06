using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Persistence;

/// <summary>
/// Repository interface for business rules
/// </summary>
public interface IBusinessRuleRepository
{
    Task<IReadOnlyList<BusinessRuleDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BusinessRuleDto>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken cancellationToken = default);
    Task<BusinessRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(BusinessRuleDto rule, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(BusinessRuleDto rule, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task ToggleStatusAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for departments and teams
/// </summary>
public interface IDepartmentTeamRepository
{
    Task<IReadOnlyList<DepartmentTeamDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DepartmentTeamDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(DepartmentTeamDto team, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(DepartmentTeamDto team, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for departments
/// </summary>
public interface IDepartmentRepository
{
    Task<IReadOnlyList<DepartmentDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DepartmentDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(DepartmentDto department, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(DepartmentDto department, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for producers and staff
/// </summary>
public interface IProducerStaffRepository
{
    Task<IReadOnlyList<ProducerStaffDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProducerStaffDto>> GetByRoleAsync(Guid tenantId, string role, CancellationToken cancellationToken = default);
    Task<ProducerStaffDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(ProducerStaffDto staff, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(ProducerStaffDto staff, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProducerStaffDto>> GetExpiringLicensesAsync(Guid tenantId, int days, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for system settings
/// </summary>
public interface ISystemSettingsRepository
{
    Task<IReadOnlyList<SystemSettingsDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SystemSettingsDto>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken cancellationToken = default);
    Task<SystemSettingsDto?> GetByKeyAsync(Guid tenantId, string key, CancellationToken cancellationToken = default);
    Task UpdateAsync(SystemSettingsDto setting, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for notification policies
/// </summary>
public interface INotificationPolicyRepository
{
    Task<IReadOnlyList<NotificationPolicyDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<NotificationPolicyDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(NotificationPolicyDto policy, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(NotificationPolicyDto policy, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for queue routing rules
/// </summary>
public interface IQueueRoutingRepository
{
    Task<IReadOnlyList<QueueRoutingRuleDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<QueueRoutingRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(QueueRoutingRuleDto rule, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(QueueRoutingRuleDto rule, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for data quality rules
/// </summary>
public interface IDataQualityRepository
{
    Task<IReadOnlyList<DataQualityRuleDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DataQualityRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(DataQualityRuleDto rule, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(DataQualityRuleDto rule, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for data center configurations
/// </summary>
public interface IDataCenterRepository
{
    Task<IReadOnlyList<DataCenterConfigDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DataCenterConfigDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(DataCenterConfigDto config, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(DataCenterConfigDto config, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

/// <summary>
/// Repository interface for SLA policies
/// </summary>
public interface ISlaPolicyRepository
{
    Task<IReadOnlyList<SlaPolicySetupDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<SlaPolicySetupDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(SlaPolicySetupDto policy, Guid userId, CancellationToken cancellationToken = default);
    Task UpdateAsync(SlaPolicySetupDto policy, Guid userId, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
