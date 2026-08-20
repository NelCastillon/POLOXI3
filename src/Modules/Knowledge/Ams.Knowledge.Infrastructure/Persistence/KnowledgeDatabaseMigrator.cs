using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Ams.Knowledge.Infrastructure.Persistence;

public sealed partial class KnowledgeDatabaseMigrator
{
    private const string ResourcePrefix = "Ams.Knowledge.Infrastructure.Migrations.";
    private readonly KnowledgeSqlConnectionFactory _connectionFactory;
    private readonly ILogger<KnowledgeDatabaseMigrator> _logger;

    public KnowledgeDatabaseMigrator(
        KnowledgeSqlConnectionFactory connectionFactory,
        ILogger<KnowledgeDatabaseMigrator> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var lockResult = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "DECLARE @Result INT; EXEC @Result = sys.sp_getapplock @Resource = N'Ams.Knowledge.DatabaseMigrator', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 120000; SELECT @Result;",
            commandTimeout: 130,
            cancellationToken: cancellationToken));

        if (lockResult < 0)
            throw new InvalidOperationException($"Could not acquire the Knowledge database migration lock. SQL application lock result: {lockResult}.");

        try
        {
            await EnsureMigrationLedgerAsync(connection, cancellationToken);
            foreach (var migration in LoadMigrations())
            {
                if (await HasBeenAppliedAsync(connection, migration.Name, cancellationToken))
                    continue;

                _logger.LogInformation("Applying Knowledge migration: {MigrationName}", migration.Name);
                await ApplyAsync(connection, migration, cancellationToken);
                _logger.LogInformation("Knowledge migration applied: {MigrationName}", migration.Name);
            }
        }
        finally
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "EXEC sys.sp_releaseapplock @Resource = N'Ams.Knowledge.DatabaseMigrator', @LockOwner = N'Session';",
                cancellationToken: cancellationToken));
        }
    }

    private static async Task EnsureMigrationLedgerAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        const string sql = """
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'knowledge')
    EXEC(N'CREATE SCHEMA knowledge');

IF OBJECT_ID(N'knowledge.__Migrations', N'U') IS NULL
BEGIN
    CREATE TABLE knowledge.__Migrations
    (
        MigrationId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_KnowledgeMigrations PRIMARY KEY,
        Name NVARCHAR(200) NOT NULL CONSTRAINT UQ_KnowledgeMigrations_Name UNIQUE,
        AppliedDateUtc DATETIME2(7) NOT NULL CONSTRAINT DF_KnowledgeMigrations_AppliedDateUtc DEFAULT SYSUTCDATETIME()
    );
END;
""";
        await connection.ExecuteAsync(new CommandDefinition(sql, cancellationToken: cancellationToken));
    }

    private static async Task<bool> HasBeenAppliedAsync(SqlConnection connection, string name, CancellationToken cancellationToken)
    {
        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT COUNT(1) FROM knowledge.__Migrations WHERE Name = @Name;",
            new { Name = name },
            cancellationToken: cancellationToken)) > 0;
    }

    private static async Task ApplyAsync(SqlConnection connection, KnowledgeMigration migration, CancellationToken cancellationToken)
    {
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var batch in SplitBatches(migration.Sql))
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    batch,
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "INSERT INTO knowledge.__Migrations (Name) VALUES (@Name);",
                new { migration.Name },
                transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static IReadOnlyList<KnowledgeMigration> LoadMigrations()
    {
        var assembly = typeof(KnowledgeDatabaseMigrator).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(name => name.StartsWith(ResourcePrefix, StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(name => name, StringComparer.Ordinal)
            .Select(name => new KnowledgeMigration(GetMigrationName(name), ReadResource(assembly, name)))
            .ToArray();
    }

    private static string GetMigrationName(string resourceName)
    {
        return resourceName[ResourcePrefix.Length..^4];
    }

    private static string ReadResource(Assembly assembly, string resourceName)
    {
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded Knowledge migration resource '{resourceName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IEnumerable<string> SplitBatches(string sql)
    {
        foreach (var batch in SqlBatchSeparatorRegex().Split(sql))
        {
            if (!string.IsNullOrWhiteSpace(batch))
                yield return batch;
        }
    }

    [GeneratedRegex(@"(?im)^\s*GO\s*(?:--[^\r\n]*)?\r?$", RegexOptions.CultureInvariant)]
    private static partial Regex SqlBatchSeparatorRegex();

    private sealed record KnowledgeMigration(string Name, string Sql);
}
