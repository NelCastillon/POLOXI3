using Ams.Application.Abstractions.Persistence;
using Ams.Application.Common.Dtos;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class JournalEntryLineRepository : IJournalEntryLineRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public JournalEntryLineRepository(ISqlConnectionFactory connectionFactory) => _connectionFactory = connectionFactory;

    public async Task<JournalEntryLineDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT LineId, JournalEntryId, GLAccountId, DebitAmount, CreditAmount, Description, LineOrder FROM Finance.JournalEntryLine WHERE LineId = @Id;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return await cn.QuerySingleOrDefaultAsync<JournalEntryLineDto>(new CommandDefinition(sql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<JournalEntryLineDto>> GetByJournalEntryIdAsync(Guid journalEntryId, CancellationToken cancellationToken = default)
    {
        const string sql = "SELECT LineId, JournalEntryId, GLAccountId, DebitAmount, CreditAmount, Description, LineOrder FROM Finance.JournalEntryLine WHERE JournalEntryId = @JournalEntryId ORDER BY LineOrder;";
        using var cn = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        return (await cn.QueryAsync<JournalEntryLineDto>(new CommandDefinition(sql, new { JournalEntryId = journalEntryId }, cancellationToken: cancellationToken))).AsList();
    }
}
