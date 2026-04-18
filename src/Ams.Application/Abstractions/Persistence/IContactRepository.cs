using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.Contacts;

namespace Ams.Application.Abstractions.Persistence;

public interface IContactRepository
{
    Task<Guid> CreateAsync(CreateContactRequest request, CancellationToken cancellationToken = default);
    Task<ContactDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<ContactDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
    Task<PagedResult<ContactDto>> GetByAccountIdAsync(Guid accountId, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
