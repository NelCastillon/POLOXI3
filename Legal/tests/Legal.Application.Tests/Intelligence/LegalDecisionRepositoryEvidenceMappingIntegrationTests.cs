using System.Data;
using Dapper;
using Legal.Application.Abstractions.Persistence;
using Legal.Infrastructure.Persistence;
using Legal.Infrastructure.Persistence.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legal.Application.Tests.Intelligence;

public sealed class LegalDecisionRepositoryEvidenceMappingIntegrationTests : IAsyncLifetime
{
    private const string ConnectionStringVariable = "LEGAL_TEST_SQL";

    private readonly string? _connectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);
    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _sessionId = Guid.NewGuid();
    private readonly Guid _evidenceId = Guid.NewGuid();
    private readonly Guid _branchId = Guid.NewGuid();

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
            INSERT POLOXI.Legal_DecisionSession
                (DecisionSessionId, QueryText, ContextCode, UsePoloxiEngine, StatusCode,
                 TerminationReason, TenantId, CreatedByUserId)
            VALUES
                (@SessionId, N'Evidence mapping integration test', N'LEGAL', 1, N'RUNNING',
                 N'TEST', @TenantId, NULL);

            INSERT POLOXI.Legal_DecisionEvidence
                (DecisionEvidenceId, DecisionSessionId, DecisionBranchId, SourceRef, SourceTitle, Snippet,
                 IdentityFactor, CitationFactor, HoldingFactor, WeightFactor, PropositionFit,
                  VerificationValue, VerificationStatus, LifecycleState, SupportedObjective, SupportingPassage, TenantId)
            VALUES
                (@EvidenceId, @SessionId, @BranchId, N'https://example.test/source', N'Test source', N'Test snippet',
                  0.9, 0.8, 0.7, 0.6, 0.5, 0.151200, N'VERIFIED', N'VERIFIED',
                 N'Test objective', N'Test supporting passage', @TenantId);
            """,
            new { SessionId = _sessionId, TenantId = _tenantId, EvidenceId = _evidenceId, BranchId = _branchId });
    }

    public async Task DisposeAsync()
    {
        if (!Enabled)
            return;

        using var connection = await _factory.CreateOpenConnectionAsync();
        await connection.ExecuteAsync(
            """
            DELETE FROM POLOXI.Legal_DecisionEvidence WHERE DecisionSessionId = @SessionId;
            DELETE FROM POLOXI.Legal_DecisionSession WHERE DecisionSessionId = @SessionId;
            """,
            new { SessionId = _sessionId });
    }

    [Fact]
    public async Task GetSessionAsync_MaterializesEvidenceWithProvenance()
    {
        if (!Enabled)
            return;

        var session = await _repository.GetSessionAsync(_tenantId, _sessionId);

        Assert.NotNull(session);
        var evidence = Assert.Single(session.Evidence);
        Assert.Equal(_evidenceId, evidence.DecisionEvidenceId);
        Assert.Equal(_branchId, evidence.DecisionBranchId);
        Assert.Equal("https://example.test/source", evidence.SourceRef);
        Assert.Equal("VERIFIED", evidence.VerificationStatus);
        Assert.Equal("VERIFIED", evidence.LifecycleState);
        Assert.Equal("Test objective", evidence.SupportedObjective);
        Assert.Equal("Test supporting passage", evidence.SupportingPassage);
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
