using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface IConfigurationRepository
{
    Task<ConfigurationSettingDto?> GetByIdAsync(Guid settingId, CancellationToken cancellationToken = default);
    Task<ConfigurationSettingDto?> GetByKeyAsync(string settingKey, string scopeCode, Guid? tenantId, CancellationToken cancellationToken = default);
    Task<PagedResult<ConfigurationSettingDto>> SearchAsync(string? searchTerm, string? scopeCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
