using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Ams.Application.Common.Models;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class AssistantRepository : IAssistantRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AssistantRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AssistantConversationDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = @"SELECT AssistantConversationId, TenantId, UserId, ContextEntityName, ContextEntityId, StartedDateUtc FROM Assistant.AssistantConversation WHERE AssistantConversationId = @Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<AssistantConversationDto>(
            new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<PagedResult<AssistantConversationDto>> SearchAsync(Guid tenantId, string? searchTerm, int pageNumber = 1, int pageSize = 25, CancellationToken cancellationToken = default)
    {
        var sql = RepositorySql.BuildPagedSearchSql(
            "Assistant.AssistantConversation",
            "AssistantConversationId, TenantId, UserId, ContextEntityName, ContextEntityId, StartedDateUtc",
            "ContextEntityName LIKE '%' + @SearchTerm + '%'",
            "CreatedDateUtc DESC",
            false);

        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        using var multi = await cn.QueryMultipleAsync(
            new CommandDefinition(sql, new
            {
                TenantId = tenantId,
                SearchTerm = searchTerm,
                Offset = (Math.Max(pageNumber, 1) - 1) * Math.Max(pageSize, 1),
                PageSize = Math.Max(pageSize, 1)
            }, cancellationToken: cancellationToken));

        var items = (await multi.ReadAsync<AssistantConversationDto>()).AsList();
        var total = await multi.ReadSingleAsync<int>();

        return new PagedResult<AssistantConversationDto>
        {
            Items = items,
            TotalCount = total,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
