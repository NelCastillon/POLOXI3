using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface IAssistantRepository
{
    Task<AssistantConversationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<PagedResult<AssistantConversationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default);
}
