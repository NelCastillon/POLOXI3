using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class PlatformEventService : IPlatformEventService
{
    private readonly IPlatformEventRepository _repository;

    public PlatformEventService(IPlatformEventRepository repository) => _repository = repository;

    public Task<PagedResult<PlatformEventDto>> SearchAsync(string? searchTerm = null, string? eventTypeCode = null, string? processingStatus = null, string? sourceService = null, Guid? tenantId = null, string? correlationId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, eventTypeCode, processingStatus, sourceService, tenantId, correlationId, pageNumber, pageSize, cancellationToken);

    public Task<PlatformEventDto?> GetByIdAsync(Guid platformEventId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(platformEventId, cancellationToken);

    public Task ReplayAsync(Guid platformEventId, CancellationToken cancellationToken = default)
        => _repository.ReplayAsync(platformEventId, cancellationToken);
}
