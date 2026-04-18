using System.Data;
using Ams.Application.Abstractions.Persistence;
using Ams.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;

namespace Ams.Infrastructure.Persistence.ConnectionFactory;

public sealed class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly SqlOptions _options;

    public SqlConnectionFactory(IOptions<SqlOptions> options)
    {
        _options = options.Value;
    }

    public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
            throw new InvalidOperationException("SQL connection string is not configured.");

        var connection = new SqlConnection(_options.ConnectionString);
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
