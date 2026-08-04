using System.Data;
using Ams.Application.Abstractions.Persistence;
using Ams.Infrastructure.Persistence.Repositories;
using Dapper;
using Microsoft.Data.SqlClient;
using Xunit;

namespace Ams.Application.Tests;

public sealed class DocumentIntakeSqlIntegrationTests
{
    [SqlIntegrationFact]
    [Trait("Category","SqlIntegration")]
    public async Task ConcurrentWorkers_LeaseEachWorkItemOnce()
    {
        var connectionString=RequiredConnectionString();var factory=new TestConnectionFactory(connectionString);var repository=new DocumentIntakeRepository(factory);var seed=await SeedAsync(connectionString);
        try
        {
            var leases=await Task.WhenAll(Enumerable.Range(0,8).Select(index=>repository.LeaseWorkItemsAsync($"test-worker-{index}",5,TimeSpan.FromMinutes(5))));
            var ids=leases.SelectMany(items=>items).Select(item=>item.IntakeWorkItemId).ToArray();
            Assert.Equal(ids.Length,ids.Distinct().Count());
            Assert.Equal(seed.WorkItemIds.Count,ids.Length);
        }
        finally{await CleanupAsync(connectionString,seed);}
    }

    [SqlIntegrationFact]
    [Trait("Category","SqlIntegration")]
    public async Task ExpiredProcessingLease_IsRecoveredAfterWorkerCrash()
    {
        var connectionString=RequiredConnectionString();var factory=new TestConnectionFactory(connectionString);var repository=new DocumentIntakeRepository(factory);var seed=await SeedAsync(connectionString,1);
        try
        {
            var first=(await repository.LeaseWorkItemsAsync("crashed-worker",1,TimeSpan.FromSeconds(1))).Single();
            await using(var connection=new SqlConnection(connectionString)){await connection.OpenAsync();await connection.ExecuteAsync("UPDATE DMS.IntakeWorkItem SET LeaseExpiresDateUtc=DATEADD(SECOND,-1,SYSUTCDATETIME()) WHERE IntakeWorkItemId=@Id",new{Id=first.IntakeWorkItemId});}
            var recovered=(await repository.LeaseWorkItemsAsync("recovery-worker",1,TimeSpan.FromMinutes(5))).Single();
            Assert.Equal(first.IntakeWorkItemId,recovered.IntakeWorkItemId);
            Assert.Equal(2,recovered.AttemptCount);
        }
        finally{await CleanupAsync(connectionString,seed);}
    }

    private static string RequiredConnectionString()=>Environment.GetEnvironmentVariable("AMS_TEST_SQL_CONNECTION")!;
    private static async Task<Seed> SeedAsync(string connectionString,int count=20)
    {
        var tenantId=Guid.NewGuid();var sessionId=Guid.NewGuid();var ids=Enumerable.Range(0,count).Select(_=>Guid.NewGuid()).ToArray();await using var connection=new SqlConnection(connectionString);await connection.OpenAsync();await connection.ExecuteAsync("INSERT DMS.IntakeSession(IntakeSessionId,TenantId,SessionNumber,IdempotencyKey,ModuleCode,EntryPointCode,StatusCode,PriorityCode,CorrelationId) VALUES(@SessionId,@TenantId,@Number,@Key,N'SUBMISSION',N'TEST',N'QUEUED',N'NORMAL',@Correlation)",new{SessionId=sessionId,TenantId=tenantId,Number=$"TEST-{sessionId:N}",Key=$"TEST:{sessionId:N}",Correlation=sessionId.ToString("N")});foreach(var id in ids)await connection.ExecuteAsync("INSERT DMS.IntakeWorkItem(IntakeWorkItemId,TenantId,IntakeSessionId,WorkTypeCode,StatusCode,IdempotencyKey,SequenceNumber,CorrelationId) VALUES(@Id,@TenantId,@SessionId,N'VALIDATION',N'PENDING',@Key,1,@Correlation)",new{Id=id,TenantId=tenantId,SessionId=sessionId,Key=$"TEST:{id:N}",Correlation=sessionId.ToString("N")});return new(tenantId,sessionId,ids);
    }

public sealed class SqlIntegrationFactAttribute:FactAttribute
{
    public SqlIntegrationFactAttribute()
    {
        if(string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("AMS_TEST_SQL_CONNECTION")))Skip="Set AMS_TEST_SQL_CONNECTION to run SQL concurrency integration tests.";
    }
}
    private static async Task CleanupAsync(string connectionString,Seed seed){await using var connection=new SqlConnection(connectionString);await connection.OpenAsync();await connection.ExecuteAsync("DELETE DMS.IntakeWorkAttempt WHERE TenantId=@TenantId; DELETE DMS.IntakeWorkItem WHERE TenantId=@TenantId; DELETE DMS.IntakeSession WHERE TenantId=@TenantId;",new{seed.TenantId});}
    private sealed record Seed(Guid TenantId,Guid SessionId,IReadOnlyCollection<Guid> WorkItemIds);
    private sealed class TestConnectionFactory(string connectionString):ISqlConnectionFactory{public async Task<IDbConnection> CreateOpenConnectionAsync(CancellationToken cancellationToken=default){var connection=new SqlConnection(connectionString);await connection.OpenAsync(cancellationToken);return connection;}}
}
