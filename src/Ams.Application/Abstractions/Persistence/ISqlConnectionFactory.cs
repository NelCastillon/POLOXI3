using System.Data;

namespace Ams.Application.Abstractions.Persistence;

public interface ISqlConnectionFactory
{
    Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default);
}
