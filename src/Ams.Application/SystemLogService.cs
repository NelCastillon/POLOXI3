using Ams.Application.Abstractions.Persistence;
using Ams.Application.Abstractions.Services;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application;

public sealed class SystemLogService : ISystemLogService
{
    private readonly ISystemLogRepository _repository;

    public SystemLogService(ISystemLogRepository repository) => _repository = repository;

    public Task<PagedResult<SystemLogDto>> SearchAsync(string? keyword = null, string? level = null, string? serviceName = null, string? regionCode = null, string? correlationId = null, string? tenantId = null, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
        => _repository.SearchAsync(keyword, level, serviceName, regionCode, correlationId, tenantId, pageNumber, pageSize, cancellationToken);

    public Task<SystemLogDto?> GetByIdAsync(Guid systemLogId, CancellationToken cancellationToken = default)
        => _repository.GetByIdAsync(systemLogId, cancellationToken);
}
