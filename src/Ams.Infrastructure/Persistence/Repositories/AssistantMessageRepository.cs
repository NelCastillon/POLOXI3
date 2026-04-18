using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AssistantMessageRepository : IAssistantMessageRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public AssistantMessageRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<PagedResult<AssistantMessageDto>> SearchByConversationAsync(Guid conversationId, int pageNumber = 1, int pageSize = 50, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Cte AS (SELECT MessageId, ConversationId, TenantId, Role, Content, SentDateUtc FROM Assistant.AssistantMessage WHERE ConversationId = @ConversationId AND IsDeleted = 0)
SELECT * FROM Cte ORDER BY SentDateUtc ASC OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
SELECT COUNT(1) FROM Assistant.AssistantMessage WHERE ConversationId = @ConversationId AND IsDeleted = 0;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(new CommandDefinition(sql, new { ConversationId = conversationId, Offset = (Math.Max(pageNumber, 1) - 1) * pageSize, PageSize = pageSize }, cancellationToken: cancellationToken));
        var items = (await multi.ReadAsync<AssistantMessageDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();
        return new PagedResult<AssistantMessageDto> { Items = items, TotalCount = total, PageNumber = pageNumber, PageSize = pageSize };
    }
}
