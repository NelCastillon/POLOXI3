using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;

namespace Ams.Application.Abstractions.Persistence;

public interface IAssistantMessageRepository
{
    Task<PagedResult<AssistantMessageDto>> SearchByConversationAsync(Guid conversationId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default);
}
