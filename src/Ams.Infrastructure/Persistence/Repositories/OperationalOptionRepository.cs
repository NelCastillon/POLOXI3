using Ams.Application.Abstractions.Persistence;
using Ams.Application.Features.Operations;
using Dapper;

namespace Ams.Infrastructure.Persistence.Repositories;

public sealed class OperationalOptionRepository : IOperationalOptionRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public OperationalOptionRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<OperationalOptionDto>> GetByGroupAsync(Guid tenantId, string optionGroupCode, CancellationToken cancellationToken = default)
    {
        const string sql = @"
;WITH Ranked AS
(
    SELECT OperationalOptionId,TenantId,OptionGroupCode,OptionCode,DisplayName,Description,MetadataJson,SortOrder,IsDefault,
           ROW_NUMBER() OVER(PARTITION BY OptionCode ORDER BY CASE WHEN TenantId=@TenantId THEN 0 ELSE 1 END) RowNumber
    FROM Platform.OperationalOption
    WHERE (TenantId=@TenantId OR TenantId IS NULL)
      AND OptionGroupCode=@OptionGroupCode
      AND IsActive=1
      AND IsDeleted=0
)
SELECT OperationalOptionId,TenantId,OptionGroupCode,OptionCode,DisplayName,Description,MetadataJson,SortOrder,IsDefault
FROM Ranked
WHERE RowNumber=1
ORDER BY SortOrder,DisplayName;";

        using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<OperationalOptionDto>(new CommandDefinition(
            sql,
            new { TenantId = tenantId, OptionGroupCode = optionGroupCode.Trim() },
            cancellationToken: cancellationToken));
        return rows.AsList();
    }
}
