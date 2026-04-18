using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Leads;

namespace Ams.Application.Abstractions.Services;

public interface ILeadService
{
    Task<Guid> CreateAsync(Ams.Application.Features.Leads.CreateLeadRequest request, CancellationToken cancellationToken = default);
    Task<LeadDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<LeadDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
