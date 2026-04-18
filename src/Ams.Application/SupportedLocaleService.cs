using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class SupportedLocaleService : ISupportedLocaleService
{
    private readonly ISupportedLocaleRepository _repository;

    public SupportedLocaleService(ISupportedLocaleRepository repository)
        => _repository = repository;

    public Task<SupportedLocaleDto?> GetByIdAsync(Guid localeId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(localeId, cancellationToken);

    public Task<SupportedLocaleDto?> GetByCodeAsync(string localeCode, CancellationToken cancellationToken = default)
        => _repository.GetByCodeAsync(localeCode, cancellationToken);

    public Task<PagedResult<SupportedLocaleDto>> SearchAsync(string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, pageNumber, pageSize, cancellationToken);
}
