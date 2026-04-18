using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Ams.Application.Features.AccountNotes;

namespace Ams.Application.Abstractions.Services;

public interface IAccountNoteService
{
    Task<Guid> CreateAsync(CreateAccountNoteRequest request, CancellationToken cancellationToken = default);
    Task<AccountNoteDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AccountNoteDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
