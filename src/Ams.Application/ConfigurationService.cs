using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class ConfigurationService : IConfigurationService
{
    private readonly IConfigurationRepository _repository;

    public ConfigurationService(IConfigurationRepository repository)
        => _repository = repository;

    public Task<ConfigurationSettingDto?> GetByIdAsync(Guid settingId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(settingId, cancellationToken);

    public Task<ConfigurationSettingDto?> GetByKeyAsync(string settingKey, string scopeCode, Guid? tenantId, CancellationToken cancellationToken = default)
        => _repository.GetByKeyAsync(settingKey, scopeCode, tenantId, cancellationToken);

    public Task<PagedResult<ConfigurationSettingDto>> SearchAsync(string? searchTerm, string? scopeCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, scopeCode, pageNumber, pageSize, cancellationToken);
}
