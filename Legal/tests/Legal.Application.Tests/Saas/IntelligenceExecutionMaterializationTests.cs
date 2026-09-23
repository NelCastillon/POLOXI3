using System.Data;
using Dapper;
using Legal.Application.Features.Saas;
using Xunit;

namespace Legal.Application.Tests.Saas;

public sealed class IntelligenceExecutionMaterializationTests
{
    [Fact]
    public void DapperMaterializesExecution_WhenSqlTimestampsAreDateTimeOffset()
    {
        var executionId = Guid.NewGuid();
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var createdAt = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);
        var startedAt = createdAt.AddSeconds(1);

        using var table = CreateExecutionResultTable();
        table.Rows.Add(
            executionId,
            tenantId,
            userId,
            DBNull.Value,
            "wide.search",
            "Queued",
            "correlation-id",
            DBNull.Value,
            createdAt,
            startedAt,
            DBNull.Value,
            DBNull.Value);

        using var reader = table.CreateDataReader();
        Assert.True(reader.Read());

        var deserialize = SqlMapper.GetTypeDeserializer(
            typeof(IntelligenceExecutionDto),
            reader,
            startBound: 0,
            length: -1,
            returnNullIfFirstMissing: false);

        var execution = Assert.IsType<IntelligenceExecutionDto>(deserialize(reader));

        Assert.Equal(executionId, execution.ExecutionId);
        Assert.Equal(tenantId, execution.TenantId);
        Assert.Equal(userId, execution.RequestedByUserId);
        Assert.Null(execution.MatterId);
        Assert.Equal(createdAt, execution.CreatedDateUtc);
        Assert.Equal(startedAt, execution.StartedAtUtc);
        Assert.Null(execution.CompletedAtUtc);
        Assert.Null(execution.ParentExecutionId);
    }

    private static DataTable CreateExecutionResultTable()
    {
        var table = new DataTable();
        table.Columns.Add("ExecutionId", typeof(Guid));
        table.Columns.Add("TenantId", typeof(Guid));
        table.Columns.Add("RequestedByUserId", typeof(Guid));
        table.Columns.Add("MatterId", typeof(Guid));
        table.Columns.Add("CapabilityCode", typeof(string));
        table.Columns.Add("StatusCode", typeof(string));
        table.Columns.Add("CorrelationId", typeof(string));
        table.Columns.Add("FailureCode", typeof(string));
        table.Columns.Add("CreatedDateUtc", typeof(DateTimeOffset));
        table.Columns.Add("StartedAtUtc", typeof(DateTimeOffset));
        table.Columns.Add("CompletedAtUtc", typeof(DateTimeOffset));
        table.Columns.Add("ParentExecutionId", typeof(Guid));
        return table;
    }
}
