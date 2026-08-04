using Ams.Knowledge.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Ams.Knowledge.Infrastructure.Persistence;

public sealed class KnowledgeSqlConnectionFactory
{
    private readonly string _connectionString;

    public KnowledgeSqlConnectionFactory(IOptions<KnowledgeSqlOptions> options)
    {
        _connectionString = options.Value.ConnectionString;
    }

    public async Task<SqlConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
            throw new InvalidOperationException("The Knowledge SQL connection string is not configured.");

        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
