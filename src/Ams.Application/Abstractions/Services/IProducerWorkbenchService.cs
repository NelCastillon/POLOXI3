using Ams.Application.Common.Dtos;

namespace Ams.Application.Abstractions.Services;

public interface IProducerWorkbenchService
{
    Task<ProducerWorkbenchDto> GetWorkbenchAsync(Guid tenantId, Guid? userId, CancellationToken cancellationToken = default);
    Task<string> GetNextLeadNumberAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task LogContactAsync(Guid tenantId, Guid itemId, string itemType, CancellationToken cancellationToken = default);
}
