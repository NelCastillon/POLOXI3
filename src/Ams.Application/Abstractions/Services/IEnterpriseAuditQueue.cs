using Ams.Application.Features.Audit;

namespace Ams.Application.Abstractions.Services;

public interface IEnterpriseAuditQueue
{
    ValueTask QueueAsync(LogEnterpriseAuditEventRequest request, CancellationToken cancellationToken = default);
}
