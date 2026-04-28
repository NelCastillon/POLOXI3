using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;

namespace Ams.Infrastructure.Persistence.Repositories;

public class BusinessRuleRepository : IBusinessRuleRepository
{
    private static readonly List<BusinessRuleDto> _data = new();
    public Task<IReadOnlyList<BusinessRuleDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<BusinessRuleDto>)_data);
    public Task<IReadOnlyList<BusinessRuleDto>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<BusinessRuleDto>)_data.Where(r => r.Category == category).ToList());
    public Task<BusinessRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(r => r.BusinessRuleId == id));
    public async Task<Guid> CreateAsync(BusinessRuleDto rule, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(rule with { BusinessRuleId = id }); return id; }
    public Task UpdateAsync(BusinessRuleDto rule, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(r => r.BusinessRuleId == rule.BusinessRuleId); if (i >= 0) _data[i] = rule; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(r => r.BusinessRuleId == id); return Task.CompletedTask; }
    public Task ToggleStatusAsync(Guid id, CancellationToken ct = default) { var r = _data.FirstOrDefault(x => x.BusinessRuleId == id); if (r != null) { var i = _data.IndexOf(r); _data[i] = r with { Status = r.Status == "Active" ? "Inactive" : "Active" }; } return Task.CompletedTask; }
}

public class DepartmentTeamRepository : IDepartmentTeamRepository
{
    private static readonly List<DepartmentTeamDto> _data = new();
    public Task<IReadOnlyList<DepartmentTeamDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<DepartmentTeamDto>)_data);
    public Task<DepartmentTeamDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(t => t.TeamId == id));
    public async Task<Guid> CreateAsync(DepartmentTeamDto team, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(team with { TeamId = id }); return id; }
    public Task UpdateAsync(DepartmentTeamDto team, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(t => t.TeamId == team.TeamId); if (i >= 0) _data[i] = team; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(t => t.TeamId == id); return Task.CompletedTask; }
}

public class ProducerStaffRepository : IProducerStaffRepository
{
    private static readonly List<ProducerStaffDto> _data = new();
    public Task<IReadOnlyList<ProducerStaffDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<ProducerStaffDto>)_data);
    public Task<IReadOnlyList<ProducerStaffDto>> GetByRoleAsync(Guid tenantId, string role, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<ProducerStaffDto>)_data.Where(s => s.Role == role).ToList());
    public Task<ProducerStaffDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(s => s.StaffId == id));
    public async Task<Guid> CreateAsync(ProducerStaffDto staff, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(staff with { StaffId = id }); return id; }
    public Task UpdateAsync(ProducerStaffDto staff, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(s => s.StaffId == staff.StaffId); if (i >= 0) _data[i] = staff; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(s => s.StaffId == id); return Task.CompletedTask; }
    public Task<IReadOnlyList<ProducerStaffDto>> GetExpiringLicensesAsync(Guid tenantId, int days, CancellationToken ct = default) { var cut = DateTime.UtcNow.AddDays(days); return Task.FromResult((IReadOnlyList<ProducerStaffDto>)_data.Where(s => s.LicenseExpiryDate.HasValue && s.LicenseExpiryDate <= cut).ToList()); }
}

public class SystemSettingsRepository : ISystemSettingsRepository
{
    private static readonly List<SystemSettingsDto> _data = new();
    public Task<IReadOnlyList<SystemSettingsDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<SystemSettingsDto>)_data);
    public Task<IReadOnlyList<SystemSettingsDto>> GetByCategoryAsync(Guid tenantId, string category, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<SystemSettingsDto>)_data.Where(s => s.Category == category).ToList());
    public Task<SystemSettingsDto?> GetByKeyAsync(Guid tenantId, string key, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(s => s.SettingKey == key));
    public Task UpdateAsync(SystemSettingsDto setting, CancellationToken ct = default) { var i = _data.FindIndex(s => s.SettingId == setting.SettingId); if (i >= 0) _data[i] = setting; return Task.CompletedTask; }
}

public class NotificationPolicyRepository : INotificationPolicyRepository
{
    private static readonly List<NotificationPolicyDto> _data = new();
    public Task<IReadOnlyList<NotificationPolicyDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<NotificationPolicyDto>)_data);
    public Task<NotificationPolicyDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(p => p.PolicyId == id));
    public async Task<Guid> CreateAsync(NotificationPolicyDto policy, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(policy with { PolicyId = id }); return id; }
    public Task UpdateAsync(NotificationPolicyDto policy, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(p => p.PolicyId == policy.PolicyId); if (i >= 0) _data[i] = policy; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(p => p.PolicyId == id); return Task.CompletedTask; }
}

public class QueueRoutingRepository : IQueueRoutingRepository
{
    private static readonly List<QueueRoutingRuleDto> _data = new();
    public Task<IReadOnlyList<QueueRoutingRuleDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<QueueRoutingRuleDto>)_data);
    public Task<QueueRoutingRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(r => r.RuleId == id));
    public async Task<Guid> CreateAsync(QueueRoutingRuleDto rule, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(rule with { RuleId = id }); return id; }
    public Task UpdateAsync(QueueRoutingRuleDto rule, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(r => r.RuleId == rule.RuleId); if (i >= 0) _data[i] = rule; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(r => r.RuleId == id); return Task.CompletedTask; }
}

public class DataQualityRepository : IDataQualityRepository
{
    private static readonly List<DataQualityRuleDto> _data = new();
    public Task<IReadOnlyList<DataQualityRuleDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<DataQualityRuleDto>)_data);
    public Task<DataQualityRuleDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(r => r.RuleId == id));
    public async Task<Guid> CreateAsync(DataQualityRuleDto rule, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(rule with { RuleId = id }); return id; }
    public Task UpdateAsync(DataQualityRuleDto rule, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(r => r.RuleId == rule.RuleId); if (i >= 0) _data[i] = rule; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(r => r.RuleId == id); return Task.CompletedTask; }
}

public class DataCenterRepository : IDataCenterRepository
{
    private static readonly List<DataCenterConfigDto> _data = new();
    public Task<IReadOnlyList<DataCenterConfigDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<DataCenterConfigDto>)_data);
    public Task<DataCenterConfigDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(c => c.ConfigId == id));
    public async Task<Guid> CreateAsync(DataCenterConfigDto config, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(config with { ConfigId = id }); return id; }
    public Task UpdateAsync(DataCenterConfigDto config, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(c => c.ConfigId == config.ConfigId); if (i >= 0) _data[i] = config; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(c => c.ConfigId == id); return Task.CompletedTask; }
}

public class SlaPolicyRepository : ISlaPolicyRepository
{
    private static readonly List<SlaPolicySetupDto> _data = new();
    public Task<IReadOnlyList<SlaPolicySetupDto>> GetAllAsync(Guid tenantId, CancellationToken ct = default) => Task.FromResult((IReadOnlyList<SlaPolicySetupDto>)_data);
    public Task<SlaPolicySetupDto?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_data.FirstOrDefault(p => p.PolicyId == id));
    public async Task<Guid> CreateAsync(SlaPolicySetupDto policy, Guid userId, CancellationToken ct = default) { var id = Guid.NewGuid(); _data.Add(policy with { PolicyId = id }); return id; }
    public Task UpdateAsync(SlaPolicySetupDto policy, Guid userId, CancellationToken ct = default) { var i = _data.FindIndex(p => p.PolicyId == policy.PolicyId); if (i >= 0) _data[i] = policy; return Task.CompletedTask; }
    public Task DeleteAsync(Guid id, CancellationToken ct = default) { _data.RemoveAll(p => p.PolicyId == id); return Task.CompletedTask; }
}
