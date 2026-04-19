using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class SecurityEventLogService : ISecurityEventLogService
{
    private readonly ISecurityEventLogRepository _repository;

    public SecurityEventLogService(ISecurityEventLogRepository repository) => _repository = repository;

    public Task<PagedResult<SecurityEventLogDto>> SearchAsync(string? searchTerm, string? eventTypeCode, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(searchTerm, eventTypeCode, pageNumber, pageSize, cancellationToken);
}
