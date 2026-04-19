using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Services;

public interface IConfigurationService
{
    Task<ConfigurationSettingDto?> GetByIdAsync(Guid settingId, CancellationToken cancellationToken = default);
    Task<ConfigurationSettingDto?> GetByKeyAsync(string settingKey, string scopeCode, Guid? tenantId, CancellationToken cancellationToken = default);
    Task<PagedResult<ConfigurationSettingDto>> SearchAsync(string? searchTerm, string? scopeCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConfigurationSettingDto>> GetByScopeAsync(string scopeCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ConfigurationSettingDto>> GetTenantSettingsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task UpdateValueAsync(Guid settingId, string? settingValue, CancellationToken cancellationToken = default);
    Task UpsertTenantSettingAsync(Guid tenantId, string settingKey, string? settingValue, CancellationToken cancellationToken = default);
}
