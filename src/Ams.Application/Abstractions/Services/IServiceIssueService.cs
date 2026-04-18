using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Operations;

namespace Ams.Application.Abstractions.Services;

public interface IServiceIssueService
{
    Task<ServiceIssueDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ServiceIssueDto>> SearchAsync(Guid tenantId, Guid? engagementId, Guid? accountId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<Guid> CreateAsync(CreateServiceIssueRequest request, CancellationToken cancellationToken = default);
}
