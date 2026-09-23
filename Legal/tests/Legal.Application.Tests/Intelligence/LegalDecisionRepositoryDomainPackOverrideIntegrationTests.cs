using System.Data;
using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Infrastructure.Persistence;
using Legal.Infrastructure.Persistence.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class LegalDecisionRepositoryDomainPackOverrideIntegrationTests : IAsyncLifetime
{
    private const string ConnectionStringVariable = "LEGAL_TEST_SQL";
    private const string PackCode = "PERSONAL_INJURY";
    private const string SourceCode = "TEST_OVERRIDE_SOURCE";
    private const string TargetCode = "TEST_OVERRIDE_TARGET";

    private readonly string? _connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
    private readonly Guid _tenantId = Guid.NewGuid();
    private ISqlConnectionFactory _factory = null!;
    private LegalDecisionRepository _repository = null!;

    private bool Enabled => !string.IsNullOrWhiteSpace(_connectionString);

    public async Task InitializeAsync()
    {
        if (!Enabled)
            return;

        _factory = new TestConnectionFactory(_connectionString!);
        _repository = new LegalDecisionRepository(_factory);
        await new LegalDatabaseMigrator(_factory, NullLogger<LegalDatabaseMigrator>.Instance).MigrateAsync();

        using var connection = await _factory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            DECLARE @PackId UNIQUEIDENTIFIER =
            (
                SELECT TOP 1 DecisionDomainPackId FROM POLOXI.Legal_DecisionDomainPack
                WHERE PackCode = @PackCode AND TenantId IS NULL AND IsDeleted = 0
            );
            DECLARE @GlobalSourceId UNIQUEIDENTIFIER = NEWID();
            DECLARE @GlobalTargetId UNIQUEIDENTIFIER = NEWID();
            DECLARE @TenantSourceId UNIQUEIDENTIFIER = NEWID();
            DECLARE @TenantTargetId UNIQUEIDENTIFIER = NEWID();

            INSERT POLOXI.Legal_DecisionDomainConcept
                (DecisionDomainConceptId, DecisionDomainPackId, ConceptCode, DimensionCode, Name,
                 ConceptKindCode, SourceClassCode, IsRequiredCoverage, IsFallbackEligible, SortOrder, TenantId)
            VALUES
                (@GlobalSourceId, @PackId, @SourceCode, N'LIABILITY', N'Global source', N'LEGAL_ISSUE', N'LEGAL_AUTHORITY', 0, 1, 900, NULL),
                (@GlobalTargetId, @PackId, @TargetCode, N'LIABILITY', N'Global target', N'LEGAL_ISSUE', N'LEGAL_AUTHORITY', 0, 1, 901, NULL),
                (@TenantSourceId, @PackId, @SourceCode, N'LIABILITY', N'Tenant source', N'LEGAL_ISSUE', N'LEGAL_AUTHORITY', 0, 1, 800, @TenantId),
                (@TenantTargetId, @PackId, @TargetCode, N'LIABILITY', N'Tenant target', N'LEGAL_ISSUE', N'LEGAL_AUTHORITY', 0, 1, 801, @TenantId);

            INSERT POLOXI.Legal_DecisionDomainConceptRelation
                (DecisionDomainPackId, SourceDecisionDomainConceptId, TargetDecisionDomainConceptId,
                 SourceConceptCode, TargetConceptCode, RelationTypeCode, ConstraintCode, IsHardConstraint, SortOrder, TenantId)
            VALUES
                (@PackId, @GlobalSourceId, @GlobalTargetId, @SourceCode, @TargetCode, N'REQUIRES', N'GLOBAL_EDGE', 0, 900, NULL),
                (@PackId, @TenantSourceId, @TenantTargetId, @SourceCode, @TargetCode, N'REQUIRES', N'TENANT_EDGE', 1, 800, @TenantId);
            """,
            new { PackCode, SourceCode, TargetCode, TenantId = _tenantId });
    }

    public async Task DisposeAsync()
    {
        if (!Enabled)
            return;

        using var connection = await _factory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            DELETE relation FROM POLOXI.Legal_DecisionDomainConceptRelation relation
            INNER JOIN POLOXI.Legal_DecisionDomainPack pack ON pack.DecisionDomainPackId = relation.DecisionDomainPackId
            WHERE pack.PackCode = @PackCode AND relation.SourceConceptCode = @SourceCode;

            DELETE concept FROM POLOXI.Legal_DecisionDomainConcept concept
            INNER JOIN POLOXI.Legal_DecisionDomainPack pack ON pack.DecisionDomainPackId = concept.DecisionDomainPackId
            WHERE pack.PackCode = @PackCode AND concept.ConceptCode IN (@SourceCode, @TargetCode);
            """,
            new { PackCode, SourceCode, TargetCode });
    }

    [Fact]
    public async Task GetDomainPackAsync_TenantRowsReplaceMatchingGlobalRows()
    {
        if (!Enabled)
            return;

        var pack = await _repository.GetDomainPackAsync(_tenantId, PackCode);

        Assert.NotNull(pack);
        var source = Assert.Single(pack.Concepts.Where(concept => concept.ConceptCode == SourceCode));
        Assert.Equal("Tenant source", source.Name);
        var relation = Assert.Single(pack.ConceptRelations.Where(edge => edge.SourceConceptCode == SourceCode));
        Assert.Equal("TENANT_EDGE", relation.ConstraintCode);
        Assert.True(relation.IsHardConstraint);
    }

    private sealed class TestConnectionFactory(string connectionString) : ISqlConnectionFactory
    {
        public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            var connection = new SqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return connection;
        }
    }
}
