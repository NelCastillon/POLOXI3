using System.Reflection;
using System.Text.RegularExpressions;
using Dapper;
using Legal.Application.Abstractions.Persistence;
using Microsoft.Extensions.Logging;

namespace Legal.Infrastructure.Persistence;

/// <summary>
/// Script-based migration runner for the Legal module.
/// Runs every embedded resource under Legal.Infrastructure.Migrations in name order.
/// Applied migrations are tracked in dbo._LegalMigrations.
/// </summary>
public sealed partial class LegalDatabaseMigrator
{
    private readonly ISqlConnectionFactory _connectionFactory;
    private readonly ILogger<LegalDatabaseMigrator> _logger;

    public LegalDatabaseMigrator(ISqlConnectionFactory connectionFactory, ILogger<LegalDatabaseMigrator> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task MigrateAsync(CancellationToken cancellationToken = default)
    {
        using var lockConnection = await _connectionFactory.CreateOpenConnectionAsync(cancellationToken);
        var lockResult = await lockConnection.ExecuteScalarAsync<int>(new CommandDefinition(
            "DECLARE @Result INT; EXEC @Result = sys.sp_getapplock @Resource = N'Legal.DatabaseMigrator', @LockMode = N'Exclusive', @LockOwner = N'Session', @LockTimeout = 120000; SELECT @Result;",
            commandTimeout: 130,
            cancellationToken: cancellationToken));
        if (lockResult < 0)
            throw new InvalidOperationException($"Could not acquire the Legal database migration lock. SQL application lock result: {lockResult}.");

        try
        {
            await lockConnection.ExecuteAsync(new CommandDefinition(
                """
                IF OBJECT_ID(N'dbo._LegalMigrations',N'U') IS NULL
                CREATE TABLE dbo._LegalMigrations
                (
                    MigrationName NVARCHAR(256) NOT NULL CONSTRAINT PK__LegalMigrations PRIMARY KEY,
                    AppliedDateUtc DATETIME2 NOT NULL CONSTRAINT DF__LegalMigrations_Applied DEFAULT SYSUTCDATETIME()
                );
                """,
                cancellationToken: cancellationToken));

            foreach (var (name, sql) in LoadMigrations())
            {
                var applied = await lockConnection.ExecuteScalarAsync<int>(new CommandDefinition(
                    "SELECT COUNT(1) FROM dbo._LegalMigrations WHERE MigrationName=@Name;",
                    new { Name = name },
                    cancellationToken: cancellationToken));
                if (applied > 0)
                    continue;

                _logger.LogInformation("Applying Legal migration: {Name}", name);
                foreach (var batch in SplitBatches(sql))
                {
                    if (string.IsNullOrWhiteSpace(batch))
                        continue;
                    await lockConnection.ExecuteAsync(new CommandDefinition(batch, commandTimeout: 600, cancellationToken: cancellationToken));
                }

                await lockConnection.ExecuteAsync(new CommandDefinition(
                    "INSERT dbo._LegalMigrations(MigrationName) VALUES(@Name);",
                    new { Name = name },
                    cancellationToken: cancellationToken));
                _logger.LogInformation("Legal migration applied: {Name}", name);
            }

            var astraPromptCount = await AstraPromptContractMigration.ApplyAsync(lockConnection, cancellationToken);
            if (astraPromptCount > 0)
                _logger.LogInformation("Applied {Count} Astra prompt contracts", astraPromptCount);

            var mathBasePromptCount = await MathBasePromptMigration.ApplyAsync(lockConnection, cancellationToken);
            if (mathBasePromptCount > 0)
                _logger.LogInformation("Seeded {Count} Math base prompts", mathBasePromptCount);

            var mathPromptCount = await MathPromptContractMigration.ApplyAsync(lockConnection, cancellationToken);
            if (mathPromptCount > 0)
                _logger.LogInformation("Applied {Count} Math prompt contracts", mathPromptCount);
        }
        finally
        {
            await lockConnection.ExecuteAsync(new CommandDefinition(
                "EXEC sys.sp_releaseapplock @Resource = N'Legal.DatabaseMigrator', @LockOwner = N'Session';",
                cancellationToken: cancellationToken));
        }
    }

    private static IReadOnlyList<(string Name, string Sql)> LoadMigrations()
    {
        var assembly = typeof(LegalDatabaseMigrator).Assembly;
        return assembly.GetManifestResourceNames()
            .Where(static n => n.Contains(".Migrations.", StringComparison.Ordinal) && n.EndsWith(".sql", StringComparison.OrdinalIgnoreCase))
            .OrderBy(static n => n, StringComparer.Ordinal)
            .Select(n => (Name: n, Sql: ReadResource(assembly, n)))
            .ToArray();
    }

    private static string ReadResource(Assembly assembly, string name)
    {
        using var stream = assembly.GetManifestResourceStream(name)
            ?? throw new InvalidOperationException($"Embedded migration resource not found: {name}.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    private static IEnumerable<string> SplitBatches(string sql) => GoSeparator().Split(sql);

    [GeneratedRegex(@"^\s*GO\s*;?\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline)]
    private static partial Regex GoSeparator();
}
