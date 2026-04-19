using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.SlaDefinitions;

namespace Ams.Application.Abstractions.Services;

public interface ISlaDefinitionService
{
    Task<PagedResult<SlaDefinitionDto>> SearchAsync(string? searchTerm, string? complianceStatus, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<SlaDefinitionDto?> GetByIdAsync(Guid slaDefinitionId, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateSlaDefinitionRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(Guid slaDefinitionId, UpdateSlaDefinitionRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid slaDefinitionId, CancellationToken cancellationToken = default);
}
