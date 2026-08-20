using Ams.Application.Features.Operations;

namespace Ams.Application.Abstractions.Persistence;

public interface IOperationalOptionRepository
{
    Task<IReadOnlyList<OperationalOptionDto>> GetByGroupAsync(Guid tenantId, string optionGroupCode, CancellationToken cancellationToken = default);
}
